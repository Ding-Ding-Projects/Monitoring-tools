using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>One file or folder shown in a <see cref="CheckTree"/>.</summary>
    internal sealed class TreeItem
    {
        public string Path;                 // forward slashes, no trailing slash
        public bool IsDirectory;
        public long Size;
        public DateTime? Modified;
        public string Note;                 // a short warning shown after the name (for example an unsafe path)
        public object Tag;
    }

    /// <summary>
    /// A list of files and folders with check marks, folders that expand and collapse, and sizes and dates. Used by the archive
    /// and disc image dialogs. Keyboard: Up and Down move between rows, Right and Left expand and collapse, Space toggles.
    /// Only the first <see cref="RowCap"/> visible rows are drawn, and the footer line says when more exist.
    /// </summary>
    internal sealed class CheckTree : Border
    {
        public const int RowCap = 1000;

        private sealed class Node
        {
            public string Name, Path; public bool IsDir; public long Size; public DateTime? Modified; public string Note; public object Tag;
            public Node Parent; public List<Node> Kids = new List<Node>(); public bool Expanded; public bool On;
            public int Depth;
        }

        private readonly Dictionary<string, Node> _byPath = new Dictionary<string, Node>(StringComparer.Ordinal);
        private readonly List<Node> _roots = new List<Node>();
        private readonly StackPanel _rows = new StackPanel();
        private readonly ScrollViewer _scroll;
        private readonly TextBlock _empty = new TextBlock { FontSize = 12, Margin = new Thickness(16, 14, 16, 14), TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body") };
        private Func<string, bool> _filter;
        private readonly bool _checkFolders;
        private readonly string _label;

        public event Action Changed;

        public CheckTree(string accessibleName, bool checkFolders, double maxHeight = 260)
        {
            _checkFolders = checkFolders; _label = accessibleName;
            _empty.Foreground = Ui.Res("B.TextMute");
            CornerRadius = new CornerRadius(16); Background = Ui.Res("B.Surface"); Margin = new Thickness(0, 0, 0, 8);
            var host = new StackPanel();
            host.Children.Add(Header());
            _scroll = new ScrollViewer { Content = _rows, MaxHeight = maxHeight, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            host.Children.Add(_scroll);
            Child = host;
            AutomationProperties.SetName(this, accessibleName);
            _rows.PreviewKeyDown += OnRowKey;
        }

        private static FrameworkElement Header()
        {
            var g = new Grid { Margin = new Thickness(16, 8, 16, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            foreach (var (t, c, right) in new[] { ("Name", 0, false), ("Size", 1, true), ("Modified", 2, true) })
            {
                var tb = new TextBlock { Text = t, FontSize = 12, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.TextSoft"), HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(right ? 0 : 56, 0, right ? 8 : 0, 0) };
                Grid.SetColumn(tb, c); g.Children.Add(tb);
            }
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Child = g };
        }

        // ------------------------------------------------------------ data

        public void SetItems(IEnumerable<TreeItem> items, bool selectAll = true)
        {
            _byPath.Clear(); _roots.Clear();
            foreach (var it in items.OrderBy(i => i.Path, StringComparer.OrdinalIgnoreCase))
            {
                var parts = it.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                Node parent = null; string acc = "";
                for (int i = 0; i < parts.Length; i++)
                {
                    acc = acc.Length == 0 ? parts[i] : acc + "/" + parts[i];
                    bool last = i == parts.Length - 1;
                    if (!_byPath.TryGetValue(acc, out var n))
                    {
                        n = new Node { Name = parts[i], Path = acc, IsDir = !last || it.IsDirectory, Parent = parent, Depth = i, Expanded = true };
                        _byPath[acc] = n;
                        (parent?.Kids ?? _roots).Add(n);
                    }
                    if (last) { n.IsDir = it.IsDirectory; n.Size = it.Size; n.Modified = it.Modified; n.Note = it.Note; n.Tag = it.Tag; }
                    parent = n;
                }
            }
            foreach (var n in _byPath.Values.Where(x => !x.IsDir)) n.On = selectAll;
            Render();
            Changed?.Invoke();
        }

        public void SetFilter(Func<string, bool> filter) { _filter = filter; Render(); }

        private IEnumerable<Node> Files => _byPath.Values.Where(n => !n.IsDir);

        public int FileCount => Files.Count();
        public int SelectedCount => Files.Count(n => n.On);
        public long SelectedBytes => Files.Where(n => n.On).Sum(n => n.Size);
        public long TotalBytes => Files.Sum(n => n.Size);
        public int MatchCount => _filter == null ? FileCount : Files.Count(n => _filter(n.Path));
        public IEnumerable<TreeItem> Selected => Files.Where(n => n.On).Select(ToItem);
        public IEnumerable<TreeItem> NotSelected => Files.Where(n => !n.On).Select(ToItem);

        private static TreeItem ToItem(Node n) => new TreeItem { Path = n.Path, IsDirectory = n.IsDir, Size = n.Size, Modified = n.Modified, Note = n.Note, Tag = n.Tag };

        public void SelectAll() { foreach (var n in Files) n.On = true; Render(); Changed?.Invoke(); }
        public void SelectNone() { foreach (var n in Files) n.On = false; Render(); Changed?.Invoke(); }

        /// <summary>Selects exactly the files this predicate accepts (for checks and captures).</summary>
        public void SelectWhere(Func<TreeItem, bool> pick) { foreach (var n in Files) n.On = pick(ToItem(n)); Render(); Changed?.Invoke(); }

        /// <summary>Collapses or expands every folder (for captures).</summary>
        public void SetAllExpanded(bool open) { foreach (var n in _byPath.Values.Where(x => x.IsDir)) n.Expanded = open; Render(); }

        public bool? FolderState(string path)
        {
            if (!_byPath.TryGetValue(path, out var n) || !n.IsDir) return null;
            return StateOf(n);
        }

        private bool? StateOf(Node n)
        {
            if (!n.IsDir) return n.On;
            var f = Descendants(n).Where(x => !x.IsDir).ToList();
            if (f.Count == 0) return false;
            if (f.All(x => x.On)) return true;
            if (f.All(x => !x.On)) return false;
            return null;
        }

        private static IEnumerable<Node> Descendants(Node n) { foreach (var k in n.Kids) { yield return k; foreach (var d in Descendants(k)) yield return d; } }

        // ------------------------------------------------------------ drawing

        private bool Active => _filter != null;

        private IEnumerable<Node> Visible()
        {
            if (Active) { foreach (var n in Files.Where(x => _filter(x.Path))) yield return n; yield break; }
            IEnumerable<Node> Walk(IEnumerable<Node> level)
            {
                foreach (var n in level)
                {
                    yield return n;
                    if (n.IsDir && n.Expanded) foreach (var k in Walk(n.Kids)) yield return k;
                }
            }
            foreach (var n in Walk(_roots)) yield return n;
        }

        public void Render()
        {
            _rows.Children.Clear();
            int drawn = 0, total = 0;
            foreach (var n in Visible())
            {
                total++;
                if (drawn >= RowCap) continue;
                _rows.Children.Add(Row(n)); drawn++;
            }
            if (total == 0)
            {
                _empty.Text = FileCount == 0 ? "There are no files to show." : "No file matches the search.";
                _rows.Children.Add(_empty);
            }
            else if (total > drawn)
                _rows.Children.Add(new TextBlock { Text = $"{drawn} of {total} rows are drawn. Search to narrow the list.", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(16, 8, 16, 8) });
        }

        private FrameworkElement Row(Node n)
        {
            var g = new Grid { MinHeight = 40, Background = Brushes.Transparent, Tag = n };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            var left = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10 + (Active ? 0 : n.Depth * 22), 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

            if (n.IsDir && !Active)
            {
                var chev = Flat(Ui.Icon(DocIcons.Chevron, Ui.Res("B.TextSoft"), 14, 2), () => { n.Expanded = !n.Expanded; Render(); FocusNode(n.Path, 0); }, (n.Expanded ? "Collapse " : "Expand ") + n.Name);
                chev.Width = 26; chev.Height = 26;
                if (n.Expanded) chev.LayoutTransform = new RotateTransform(90);
                chev.Tag = "chevron";
                left.Children.Add(chev);
            }
            else left.Children.Add(new Border { Width = Active ? 0 : 26 });

            if (!n.IsDir || _checkFolders)
            {
                var st = StateOf(n);
                var box = CheckBox(st);
                var cb = Flat(box, () => Toggle(n), (n.IsDir ? "Select folder " : "Select ") + n.Path);
                cb.Tag = "check";
                AutomationProperties.SetItemStatus(cb, st == true ? "checked" : st == false ? "unchecked" : "mixed");
                left.Children.Add(cb);
            }
            else left.Children.Add(new Border { Width = 28 });

            left.Children.Add(new ContentControl { Content = Ui.Icon(n.IsDir ? Icons.Folder : Icons.File, Ui.Res("B.TextSoft"), 16, 1.8), Margin = new Thickness(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, IsTabStop = false });
            var name = new TextBlock { Text = Active ? n.Path : n.Name, FontSize = 12, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 380 };
            name.ToolTip = n.Path;
            left.Children.Add(name);
            if (!string.IsNullOrEmpty(n.Note))
                left.Children.Add(new TextBlock { Text = n.Note, FontSize = 12, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.Amber"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
            g.Children.Add(left);

            if (!n.IsDir)
            {
                var size = new TextBlock { Text = Ui.Bytes(n.Size), FontSize = 12, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.TextSoft"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                Grid.SetColumn(size, 1); g.Children.Add(size);
                if (n.Modified.HasValue)
                {
                    var when = new TextBlock { Text = n.Modified.Value.ToString("d MMM yyyy", System.Globalization.CultureInfo.CurrentCulture), FontSize = 12, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.TextSoft"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                    Grid.SetColumn(when, 2); g.Children.Add(when);
                }
            }
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Child = g };
        }

        private void Toggle(Node n)
        {
            if (n.IsDir) { bool to = StateOf(n) != true; foreach (var d in Descendants(n).Where(x => !x.IsDir)) d.On = to; }
            else n.On = !n.On;
            Render(); Changed?.Invoke();
            FocusNode(n.Path, 1);
        }

        private static Border CheckBox(bool? state)
        {
            var b = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };
            if (state == false) { b.BorderBrush = Ui.Res("B.LineStrong"); b.Background = Brushes.Transparent; }
            else
            {
                b.BorderBrush = Ui.Res("B.Mint"); b.Background = Ui.Res("B.Mint");
                b.Child = Ui.Icon(state == true ? Icons.Check : "M6 12h12", Ui.Res("B.MintInk"), 12, 2.4);
            }
            return b;
        }

        /// <summary>A focusable, flat button around an icon or box, with a visible focus ring.</summary>
        private static Button Flat(UIElement content, Action click, string name)
        {
            var tpl = new ControlTemplate(typeof(Button));
            var bd = new FrameworkElementFactory(typeof(Border)); bd.Name = "Bd"; bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(2)); bd.SetValue(Border.BorderBrushProperty, Brushes.Transparent); bd.SetValue(Border.PaddingProperty, new Thickness(2));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp); tpl.VisualTree = bd;
            var trig = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            trig.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Res("B.Mint"), "Bd"));
            tpl.Triggers.Add(trig);
            var b = new Button { Template = tpl, Content = content, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null };
            AutomationProperties.SetName(b, name);
            b.Click += (s, e) => click();
            return b;
        }

        // ------------------------------------------------------------ keyboard

        private IEnumerable<Button> RowChecks() => Drive.All<Button>(_rows).Where(b => (b.Tag as string) == "check" || (b.Tag as string) == "chevron");

        private void FocusNode(string path, int which)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var b in RowChecks())
                {
                    var host = Drive.Ancestors(b).OfType<Grid>().FirstOrDefault(x => x.Tag is Node);
                    if (host?.Tag is Node n && n.Path == path && (b.Tag as string) == (which == 1 ? "check" : "chevron")) { b.Focus(); return; }
                }
                foreach (var b in RowChecks())
                {
                    var host = Drive.Ancestors(b).OfType<Grid>().FirstOrDefault(x => x.Tag is Node);
                    if (host?.Tag is Node n && n.Path == path) { b.Focus(); return; }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnRowKey(object sender, KeyEventArgs e)
        {
            if (!(Keyboard.FocusedElement is Button cur)) return;
            var host = Drive.Ancestors(cur).OfType<Grid>().FirstOrDefault(x => x.Tag is Node);
            if (host?.Tag is not Node node) return;
            var rows = _rows.Children.OfType<Border>().Select(b => b.Child as Grid).Where(g => g?.Tag is Node).ToList();
            int at = rows.IndexOf(host);
            switch (e.Key)
            {
                case Key.Down: case Key.Up:
                    int to = at + (e.Key == Key.Down ? 1 : -1);
                    if (to >= 0 && to < rows.Count)
                    {
                        var target = Drive.All<Button>(rows[to]).FirstOrDefault(b => (b.Tag as string) == (cur.Tag as string)) ?? Drive.All<Button>(rows[to]).FirstOrDefault();
                        target?.Focus(); target?.BringIntoView();
                    }
                    e.Handled = true; break;
                case Key.Right when node.IsDir && !node.Expanded && !Active: node.Expanded = true; Render(); FocusNode(node.Path, 0); e.Handled = true; break;
                case Key.Left when node.IsDir && node.Expanded && !Active: node.Expanded = false; Render(); FocusNode(node.Path, 0); e.Handled = true; break;
            }
        }

        /// <summary>Presses a row's check by path (for checks).</summary>
        internal void ToggleForTest(string path) { if (_byPath.TryGetValue(path, out var n)) Toggle(n); }
        internal void ToggleExpandForTest(string path) { if (_byPath.TryGetValue(path, out var n) && n.IsDir) { n.Expanded = !n.Expanded; Render(); } }
        internal int DrawnRows => _rows.Children.OfType<Border>().Count();
    }
}
