using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Clipboard history on Win+Shift+V (Windows keeps Win+V). Every row previews what it is: a link card, a picture
    /// thumbnail, a file with its type and size, a mini table, a colour swatch, a tidy phone number, a stack trace. The
    /// picked copy shows large on the right. Enter pastes into the app you were in, Shift+Enter pastes plain text,
    /// Ctrl+P pins, Ctrl+Q adds to the paste queue, Delete removes, Esc closes.
    /// </summary>
    public sealed class HistoryPopup : Window
    {
        private static HistoryPopup _open;
        private readonly IntPtr _returnTo;
        private readonly SearchField _search = new SearchField("clip-history-popup", "Search your copies", compact: true);
        private readonly ListBox _list = new ListBox();
        private readonly ContentControl _preview = new ContentControl { Focusable = false };
        private readonly WrapPanel _kinds = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        private string _kind = "all";

        internal ListBox List => _list;
        internal static HistoryPopup Current => _open;

        public static void Toggle()
        {
            if (_open != null) { _open.Close(); return; }
            _open = new HistoryPopup(NativeInput.GetForegroundWindow()) { AutoClose = true };
            _open.Closed += (s, e) => _open = null;
            _open.Show(); _open.Activate();
            _open._search.Box.Focus();
        }

        internal HistoryPopup(IntPtr returnTo)
        {
            _returnTo = returnTo;
            Title = "Clipboard history"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; Width = 860; Height = 640; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            AutomationProperties.SetName(this, "Clipboard history");
            Deactivated += (s, e) => { if (IsLoaded && !_keepOpen && AutoClose) Close(); };
            PreviewKeyDown += OnKey;

            var close = Ui.Button("", Close, "Btn.Icon", Icons.Close);
            AutomationProperties.SetName(close, "Close (Esc)");
            var head = new DockPanel();
            DockPanel.SetDock(close, Dock.Right);
            var hint = Ui.Mono(Settings.Current.HotkeyHistory, Ui.Res("B.TextSoft"), 11); hint.VerticalAlignment = VerticalAlignment.Center; hint.Margin = new Thickness(10, 0, 6, 0);
            DockPanel.SetDock(hint, Dock.Right);
            head.Children.Add(close); head.Children.Add(hint); head.Children.Add(_search);
            _search.SampleSource = () => App.History.Items.Take(30).Select(i => i.Preview);
            _search.Filter += Fill;

            foreach (var (id, label) in new[] { ("all", "All"), ("text", "Text"), ("link", "Links"), ("pic", "Pictures"), ("file", "Files"), ("pinned", "Pinned") })
            {
                var k = id;
                var chip = Ui.Button(label, () => { _kind = k; PaintKinds(); Fill(); }, "Btn.Chip");
                chip.Tag = id; chip.Margin = new Thickness(0, 0, 6, 0);
                _kinds.Children.Add(chip);
            }
            PaintKinds();

            _list.Background = Brushes.Transparent; _list.BorderThickness = new Thickness(0);
            _list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
            AutomationProperties.SetName(_list, "Copies");
            _list.SelectionChanged += (s, e) => ShowPreview();
            _list.MouseDoubleClick += (s, e) => Paste(false);

            var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(_list);

            var pasteBtn = Ui.Button("Paste", () => Paste(false), "Btn.Accent", Icons.Clipboard);
            var plainBtn = Ui.Button("Plain", () => Paste(true), "Btn.Ghost", Icons.Text);
            plainBtn.ToolTip = "Paste as plain text (Shift+Enter)";
            var more = Dialogs.SmartDialogParts.MoreButton(new List<(string, string, Action)>
            {
                ("Pin or unpin", "Ctrl+P", TogglePinned),
                ("Pin on screen", "Ctrl+Shift+P", () => { if (Selected is ClipItem it) { _keepOpen = true; PinWindow.Show(it); Close(); } }),
                ("Add to the paste queue", "Ctrl+Q", AddToQueue),
                ("Copy without pasting", "Ctrl+C", () => { if (Selected is ClipItem it) { ClipPower.Restore(it); Close(); } }),
                ("Remove from history", "Delete", Remove),
                ("Open the History page", null, () => { App.Current.OpenControlCentre("history"); Close(); }),
            });
            var bar = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            plainBtn.Margin = new Thickness(0, 0, 8, 0);
            right.Children.Add(plainBtn); right.Children.Add(pasteBtn);
            DockPanel.SetDock(right, Dock.Right); bar.Children.Add(right); bar.Children.Add(more); more.HorizontalAlignment = HorizontalAlignment.Left;
            var previewCard = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(22), Padding = new Thickness(18, 16, 18, 14) };
            var pdock = new DockPanel();
            DockPanel.SetDock(bar, Dock.Bottom);
            pdock.Children.Add(bar);
            pdock.Children.Add(new ScrollViewer { Content = _preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });
            previewCard.Child = pdock;
            Grid.SetColumn(previewCard, 2); grid.Children.Add(previewCard);

            var root = new DockPanel();
            DockPanel.SetDock(head, Dock.Top); DockPanel.SetDock(_kinds, Dock.Top);
            var foot = Ui.Mono("Enter paste · Shift+Enter plain · Ctrl+P pin · Ctrl+Q queue · Delete remove · Esc close", Ui.Res("B.TextSoft"), 11);
            foot.Margin = new Thickness(4, 10, 0, 0);
            DockPanel.SetDock(foot, Dock.Bottom);
            root.Children.Add(head); root.Children.Add(_kinds); root.Children.Add(foot); root.Children.Add(grid);
            Content = new Border
            {
                Margin = new Thickness(14), Padding = new Thickness(16), CornerRadius = new CornerRadius(28), Background = Ui.Res("B.Raised"),
                BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"], Child = root,
            };
            Fill();
        }

        private bool _keepOpen;
        /// <summary>Closes when another window takes focus, like the Windows clipboard history. Off for captures.</summary>
        internal bool AutoClose;
        private static readonly HashSet<ClipItem> Pinned = new HashSet<ClipItem>();
        private object Selected => (_list.SelectedItem as ListBoxItem)?.Tag;

        private void PaintKinds()
        {
            foreach (Button c in _kinds.Children) c.Style = Ui.Style((string)c.Tag == _kind ? "Btn.Accent" : "Btn.Chip");
        }

        private static string KindOf(ClipItem i) => i.Kind switch
        {
            ClipKind.Url or ClipKind.Email => "link",
            ClipKind.Image => "pic",
            ClipKind.Files or ClipKind.Command => i.FirstFileKind is FileKind.Image or FileKind.Video ? "pic" : "file",
            _ => "text",
        };

        internal void Fill()
        {
            var keep = Selected;
            _list.Items.Clear();
            var items = App.History.Items.Where(i => _kind == "all" || _kind == "pinned" ? (_kind != "pinned" || Pinned.Contains(i)) : KindOf(i) == _kind)
                .Where(i => !_search.State.IsActive || _search.State.Matches(i.Preview + " " + (i.BestFact?.Title ?? "") + " " + (i.SourceApp ?? "")))
                .OrderByDescending(i => Pinned.Contains(i)).ToList();
            int n = 0;
            foreach (var it in items)
            {
                var row = new ListBoxItem { Tag = it, Content = Row(it), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 2) };
                AutomationProperties.SetName(row, Name(it));
                _list.Items.Add(row);
                if (n < 12) Ui.Rise(row, n * 30, 8, 300);
                n++;
            }
            _search.SetCount(items.Count, App.History.Items.Count, "copies");
            var again = _list.Items.Cast<ListBoxItem>().FirstOrDefault(r => r.Tag == keep);
            _list.SelectedItem = again ?? (_list.Items.Count > 0 ? _list.Items[0] : null);
            if (_list.Items.Count == 0) _preview.Content = Ui.Text(App.History.Items.Count == 0 ? "Nothing copied yet. Copies you make while the app runs appear here." : "Nothing matches.", "T.Body");
        }

        private static string Name(ClipItem i) => (i.BestFact?.Title ?? i.Kind.ToString()) + ": " + ClipPower.Short(i.Preview);

        // ------------------------------------------------------------------ rich previews

        private static string When(DateTime at)
        {
            var d = DateTime.Now - at;
            return d.TotalMinutes < 1 ? "just now" : d.TotalHours < 1 ? (int)d.TotalMinutes + " min ago" : d.TotalDays < 1 ? (int)d.TotalHours + " h ago" : at.ToString("d MMM HH:mm");
        }

        private static string KindLabel(ClipItem i) => i.BestFact != null ? i.BestFact.Title.Replace("You copied ", "").Replace("an ", "").Replace("a ", "") : i.Kind switch
        {
            ClipKind.Url => "Link", ClipKind.Email => "Email", ClipKind.Color => "Colour", ClipKind.Json => "JSON", ClipKind.Markdown => "Markdown",
            ClipKind.Image => "Picture", ClipKind.Audio => "Audio", ClipKind.Files => i.Files.Length > 1 ? i.Files.Length + " files" : i.FirstFileKind == FileKind.Folder ? "Folder" : i.FirstFileKind + " file",
            _ => "Text",
        };

        private FrameworkElement Row(ClipItem it)
        {
            var sp = new StackPanel();
            var meta = Ui.Mono(KindLabel(it) + " · " + When(it.At) + (Pinned.Contains(it) ? " · ● pinned" : ""), Pinned.Contains(it) ? Ui.Res("B.Mint") : Ui.Res("B.TextSoft"), 11);
            meta.Margin = new Thickness(0, 0, 0, 6);
            sp.Children.Add(meta);
            sp.Children.Add(Small(it));
            return sp;
        }

        /// <summary>The preview inside a row, chosen by what the copy is.</summary>
        private static FrameworkElement Small(ClipItem it)
        {
            var f = it.BestFact;
            if (it.Kind == ClipKind.Image && it.Image != null) return Thumb(it.Image, it.Image.PixelWidth + " × " + it.Image.PixelHeight, "picture");
            if (it.Kind == ClipKind.Files && it.Files.Length > 0)
            {
                var file = it.FirstFile; var k = it.FirstFileKind;
                ImageSource img = null;
                if (k == FileKind.Image) try { var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(file); b.DecodePixelWidth = 160; b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze(); img = b; } catch { }
                string size = ""; try { if (File.Exists(file)) size = Ui.Bytes(new FileInfo(file).Length); } catch { }
                return Thumb(img, Path.GetFileName(file), (k == FileKind.Folder ? "folder" : Path.GetExtension(file).TrimStart('.').ToUpperInvariant()) + (size.Length > 0 ? " · " + size : "") + (it.Files.Length > 1 ? " · +" + (it.Files.Length - 1) + " more" : ""), k);
            }
            if (it.Kind == ClipKind.Url) return Card(Host(it.Text), it.Text);
            if (it.Kind == ClipKind.Color && ActionProvider.ParseColor(it.Text) is Color col) return Swatch(col, it.Text, 40);
            if (f?.Kind == "table") return MiniTable(Developer2.ReadTable(it.Text), 3);
            if (f?.Kind == "phone") return Big(f.Values.FirstOrDefault()?.Text ?? it.Text, 17);
            if (f?.Kind == "trace") return Code(f.Values.FirstOrDefault()?.Text, f.Values.Skip(1).FirstOrDefault()?.Text);
            if (f?.Kind == "unit" || f?.Kind == "money" || f?.Kind == "maths") return Big(it.Text.Trim() + (f.Values.Count > 0 ? "  →  " + f.Values[0].Text : ""), 15);
            if (f != null && f.Sensitive) return Ui.Text("Hidden", "T.Body");
            var t = new TextBlock { Text = (it.Text ?? it.Preview).Trim(), FontSize = 14, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap, MaxHeight = 42, TextTrimming = TextTrimming.CharacterEllipsis };
            Core.Language.SetLiteral(t, true);
            return t;
        }

        private static string Host(string url) => Uri.TryCreate(url.StartsWith("www.") ? "https://" + url : url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : url;

        private static FrameworkElement Thumb(ImageSource img, string line1, string line2, FileKind kind = FileKind.Image)
        {
            var box = new Border { Width = 96, Height = 54, CornerRadius = new CornerRadius(8), Background = Ui.Res("B.Sunken"), ClipToBounds = true, Margin = new Thickness(0, 0, 10, 0) };
            if (img != null) box.Child = new Image { Source = img, Stretch = Stretch.UniformToFill };
            else box.Child = Ui.Icon(kind == FileKind.Video ? Icons.Video : kind == FileKind.Audio ? Icons.Audio : kind == FileKind.Folder ? Icons.Folder : Icons.File, Ui.Res("B.TextSoft"), 22);
            var g = new DockPanel();
            g.Children.Add(box);
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = line1, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis });
            sp.Children.Add(new TextBlock { Text = line2, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextTrimming = TextTrimming.CharacterEllipsis });
            g.Children.Add(sp);
            return g;
        }

        private static FrameworkElement Card(string title, string url)
        {
            var tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = Ui.Res("B.MintWash"), Margin = new Thickness(0, 0, 10, 0), Child = new TextBlock { Text = (title.Length > 0 ? title.Substring(0, Math.Min(2, title.Length)) : "?").ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, Foreground = Ui.Res("B.Mint") } };
            var g = new DockPanel(); g.Children.Add(tile);
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis });
            var u = Ui.Mono(url, Ui.Res("B.TextSoft"), 11); u.TextTrimming = TextTrimming.CharacterEllipsis; sp.Children.Add(u);
            g.Children.Add(sp);
            return g;
        }

        private static FrameworkElement Swatch(Color c, string text, double size)
        {
            var g = new StackPanel { Orientation = Orientation.Horizontal };
            g.Children.Add(new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 3.5), Background = new SolidColorBrush(c), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) });
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Ui.Mono(text, Ui.Res("B.Text"), 14));
            sp.Children.Add(new TextBlock { Text = $"rgb({c.R}, {c.G}, {c.B})", FontSize = 12, Foreground = Ui.Res("B.TextSoft") });
            g.Children.Add(sp);
            return g;
        }

        private static FrameworkElement MiniTable(List<string[]> rows, int maxRows)
        {
            if (rows == null) return new TextBlock();
            var g = new Grid();
            int cols = Math.Min(rows[0].Length, 4);
            for (int c = 0; c < cols; c++) g.ColumnDefinitions.Add(new ColumnDefinition());
            for (int r = 0; r < Math.Min(rows.Count, maxRows); r++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int c = 0; c < cols; c++)
                {
                    var cell = new Border { Background = r == 0 ? Ui.Res("B.Line") : Ui.Res("B.Sunken"), Padding = new Thickness(8, 3, 8, 3), Child = Ui.Mono(rows[r][c], Ui.Res("B.Text"), 11.5) };
                    Grid.SetRow(cell, r); Grid.SetColumn(cell, c); g.Children.Add(cell);
                }
            }
            return new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = g };
        }

        private static FrameworkElement Big(string text, double size)
        {
            var t = Ui.Mono(text, Ui.Res("B.Text"), size); t.TextTrimming = TextTrimming.CharacterEllipsis;
            Core.Language.SetLiteral(t, true);
            return t;
        }

        private static FrameworkElement Code(string first, string second)
        {
            var sp = new StackPanel();
            var a = Ui.Mono(first ?? "", Ui.Res("B.Amber"), 11.5); a.TextTrimming = TextTrimming.CharacterEllipsis; sp.Children.Add(a);
            if (second != null) { var b = Ui.Mono(second, Ui.Res("B.Mint"), 11.5); b.TextTrimming = TextTrimming.CharacterEllipsis; sp.Children.Add(b); }
            return new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6, 8, 6), Child = sp };
        }

        // ------------------------------------------------------------------ the large preview

        private void ShowPreview()
        {
            if (!(Selected is ClipItem it)) return;
            var sp = new StackPanel();
            var meta = Ui.Mono(KindLabel(it) + " · copied " + When(it.At) + (it.SourceApp != null ? " · from " + it.SourceApp : ""), Ui.Res("B.TextSoft"), 12);
            meta.Margin = new Thickness(0, 0, 0, 12); sp.Children.Add(meta);
            var f = it.BestFact;
            if (it.Kind == ClipKind.Image && it.Image != null) sp.Children.Add(new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true, Child = new Image { Source = it.Image, MaxHeight = 300, Stretch = Stretch.Uniform } });
            else if (it.Kind == ClipKind.Files) foreach (var file in it.Files.Take(8)) { sp.Children.Add(Small(new ClipItem { Kind = ClipKind.Files, Files = new[] { file } })); sp.Children.Add(new Border { Height = 8 }); }
            else if (it.Kind == ClipKind.Color && ActionProvider.ParseColor(it.Text) is Color col) { sp.Children.Add(new Border { Height = 140, CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(col), Margin = new Thickness(0, 0, 0, 10) }); sp.Children.Add(Swatch(col, it.Text, 0)); }
            else if (f?.Kind == "table") sp.Children.Add(MiniTable(Developer2.ReadTable(it.Text), 12));
            else if (f != null && f.Sensitive) sp.Children.Add(Ui.Text("This copy is hidden because it looks like a secret.", "T.Body"));
            else
            {
                var t = Ui.Mono(it.Text ?? it.Preview, Ui.Res("B.Text"), 14);
                t.TextWrapping = TextWrapping.Wrap; Core.Language.SetLiteral(t, true);
                sp.Children.Add(t);
                if (it.Text != null) sp.Children.Add(Ui.Mono(it.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length + " words · " + it.Text.Length + " characters", Ui.Res("B.TextSoft"), 11));
            }
            if (f != null && !f.Sensitive && f.Values.Count > 0 && f.Kind != "table")
            {
                var wrap = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
                foreach (var v in f.Values.Take(8)) { var p = Ui.Pill(v.Label + ": " + ClipPower.Short(v.Text), Ui.Res("B.Raised"), Ui.Res("B.Text"), true, 11.5); p.Margin = new Thickness(0, 0, 6, 6); wrap.Children.Add(p); }
                sp.Children.Add(wrap);
            }
            _preview.Content = sp;
            Ui.Rise(sp, 0, 8, 280);
        }

        // ------------------------------------------------------------------ actions

        private void OnKey(object s, KeyEventArgs e)
        {
            var m = Keyboard.Modifiers;
            if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            else if (e.Key == Key.Enter) { e.Handled = true; Paste(m == ModifierKeys.Shift); }
            else if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (_list.Items.Count == 0) return;
                int i = Math.Clamp(_list.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, _list.Items.Count - 1);
                _list.SelectedIndex = i; _list.ScrollIntoView(_list.SelectedItem); e.Handled = true;
            }
            else if (e.Key == Key.P && m == ModifierKeys.Control) { e.Handled = true; TogglePinned(); }
            else if (e.Key == Key.Q && m == ModifierKeys.Control) { e.Handled = true; AddToQueue(); }
            else if (e.Key == Key.Delete && !_search.Box.IsKeyboardFocusWithin) { e.Handled = true; Remove(); }
        }

        private void Paste(bool plain)
        {
            if (!(Selected is ClipItem it)) return;
            _keepOpen = true;
            Close();
            if (plain || it.Kind == ClipKind.Text || it.Kind == ClipKind.Url || it.Text != null && it.Kind != ClipKind.Files && it.Kind != ClipKind.Image)
                _ = ClipPower.PasteText(it.Text ?? it.Preview, _returnTo);
            else
            {
                ClipPower.Restore(it);
                if (_returnTo != IntPtr.Zero) NativeInput.SetForegroundWindow(_returnTo);
                _ = System.Threading.Tasks.Task.Delay(90).ContinueWith(_ => NativeInput.PressCtrlV());
            }
        }

        private void TogglePinned()
        {
            if (!(Selected is ClipItem it)) return;
            if (!Pinned.Remove(it)) Pinned.Add(it);
            Fill();
        }

        private void AddToQueue()
        {
            if (!(Selected is ClipItem it) || it.Text == null) return;
            if (PasteQueue.Pasting) return;
            if (!PasteQueue.Take(it)) { PasteQueue.Toggle(); PasteQueue.Take(it); }
        }

        private void Remove()
        {
            if (!(Selected is ClipItem it)) return;
            int i = _list.SelectedIndex;
            App.History.Items.Remove(it); Pinned.Remove(it);
            Fill();
            if (_list.Items.Count > 0) _list.SelectedIndex = Math.Min(i, _list.Items.Count - 1);
        }
    }
}
