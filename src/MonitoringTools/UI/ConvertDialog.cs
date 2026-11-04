using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The "Convert to…" dialog: every format the chosen file (or files) can become, grouped by kind, with what each format
    /// is for, what it keeps or loses, and any note for this source. Pick one and convert; the original is never changed.
    /// </summary>
    public sealed class ConvertDialog : Window
    {
        private static ConvertDialog _open;
        internal static ConvertDialog Current => _open;
        /// <summary>Captures and checks: picks a format as a click would.</summary>
        internal void PickForTest(string id) { _picked = _targets.Select(t => t.Spec).FirstOrDefault(s => s.Id == id); Render(); }
        private readonly string[] _files;
        private readonly string _folder;
        private readonly List<(FormatSpec Spec, int Count)> _targets;
        private readonly SearchField _search;
        private readonly StackPanel _list = new StackPanel();
        private readonly TextBlock _summary = new TextBlock();
        private readonly Button _go;
        private FormatSpec _picked;
        private string _filter = "all";
        private string _outFolder;

        /// <summary>Opens the dialog for one file, a selection, or every file in a folder.</summary>
        public static void Open(string[] files, string folder = null)
        {
            files = (files ?? Array.Empty<string>()).Where(File.Exists).ToArray();
            if (files.Length == 0) { App.Toasts.ShowNotice("Nothing to convert", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return; }
            _open?.Close();
            _open = new ConvertDialog(files, folder);
            _open.Show();
            _open.Activate();
        }

        /// <summary>Formats at least one of the files can become, with how many of them can.</summary>
        internal static List<(FormatSpec Spec, int Count)> TargetsFor(string[] files) =>
            Formats.Visible.Select(f => (f, files.Count(p => Converter.Compatible(p, f)))).Where(t => t.Item2 > 0).ToList();

        private ConvertDialog(string[] files, string folder)
        {
            _files = files;
            _folder = folder;
            _targets = TargetsFor(files);
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize; Width = 640; SizeToContent = SizeToContent.Height;
            MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 48);
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true; ShowInTaskbar = true;
            Title = "Convert to…";
            AutomationProperties.SetName(this, "Convert to");
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            Closed += (s, e) => { if (_open == this) _open = null; };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
                else if (e.Key == Key.Enter && _picked != null && !(Keyboard.FocusedElement is TextBox)) { e.Handled = true; Convert(); }
            };
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is Border) try { DragMove(); } catch { } };

            _search = new SearchField("convert-dialog", "Search formats") { Margin = new Thickness(0, 0, 0, 10) };
            _go = Ui.Button("Convert", Convert, "Btn.Accent", Icons.Bolt);
            _go.IsEnabled = false;
            _go.ToolTip = "Starts the conversion. Off until you pick a format. The original file is never changed.";
            Content = Build();
            ElementMenu.Attach(this, null, ElementMenu.Options.Copy);
            Render();
        }

        // ------------------------------------------------------------ layout

        private FrameworkElement Build()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition());
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new TextBlock { Text = _files.Length == 1 ? "Convert " + Path.GetFileName(_files[0]) : $"Convert {_files.Length} files", Style = Ui.Style("T.Title"), FontSize = 20, TextTrimming = TextTrimming.CharacterEllipsis };
            var close = Ui.Button("", Close, "Btn.Ghost", Icons.Close);
            AutomationProperties.SetName(close, "Close");
            close.ToolTip = "Closes without converting (Esc).";
            Grid.SetColumn(close, 1);
            titleRow.Children.Add(title); titleRow.Children.Add(close);
            head.Children.Add(titleRow);
            head.Children.Add(new TextBlock { Text = SourceLine(), Style = Ui.Style("T.Body"), FontSize = 13, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 12) });
            head.Children.Add(_search);

            var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var (id, label) in new[] { ("all", "All"), ("video", "Video"), ("audio", "Audio"), ("image", "Pictures"), ("lossless", "Lossless only") })
            {
                var n = id switch
                {
                    "all" => _targets.Count,
                    "lossless" => _targets.Count(t => t.Spec.Lossless),
                    _ => _targets.Count(t => KindId(t.Spec) == id)
                };
                if (n == 0 && id != "all") continue;
                var fid = id;
                var chip = Ui.Button($"{label} {n}", () => { _filter = fid; Render(); }, "Btn.Chip");
                chip.Tag = fid;
                chip.Margin = new Thickness(0, 0, 6, 6);
                AutomationProperties.SetName(chip, $"Show {label.ToLowerInvariant()}, {n} formats");
                chips.Children.Add(chip);
            }
            head.Children.Add(chips);
            root.Children.Add(head);

            var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 520 };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var foot = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            _summary.Style = Ui.Style("T.Body"); _summary.FontSize = 13; _summary.Foreground = Ui.Res("B.TextSoft"); _summary.TextWrapping = TextWrapping.Wrap;
            foot.Children.Add(_summary);
            var actions = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Button where = null;
            where = Ui.Button("Save next to the original", () => PickFolder(), "Btn.Ghost", Icons.Folder);
            where.ToolTip = "Where the converted file goes. Default: the original's own folder. A file with the same name is never overwritten; a number is added instead.";
            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost");
            Grid.SetColumn(cancel, 1); Grid.SetColumn(_go, 2);
            cancel.Margin = new Thickness(8, 0, 8, 0);
            actions.Children.Add(where); actions.Children.Add(cancel); actions.Children.Add(_go);
            foot.Children.Add(actions);
            Grid.SetRow(foot, 2);
            root.Children.Add(foot);

            void PickFolder()
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose where converted files go" };
                if (dlg.ShowDialog(this) != true) return;
                _outFolder = dlg.FolderName;
                ((TextBlock)((StackPanel)where.Content).Children.OfType<TextBlock>().First()).Text = "Save in " + PathDisplay.Shown(_outFolder);
                Render();
            }

            return new Grid
            {
                Margin = new Thickness(16),
                Children = { new Border { Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(28), Padding = new Thickness(24, 20, 20, 18), Child = root, Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"] } }
            };
        }

        private string SourceLine()
        {
            if (_files.Length == 1)
            {
                var f = _files[0];
                var kind = ClipItem.Classify(f);
                long bytes = 0; try { bytes = new FileInfo(f).Length; } catch { }
                return $"{KindLabel(kind)} · {Path.GetExtension(f).TrimStart('.').ToUpperInvariant()} · {Ollama.Bytes(bytes)}. Pick what it should become. The original is kept as it is.";
            }
            var kinds = _files.GroupBy(ClipItem.Classify).Select(g => $"{g.Count()} {KindLabel(g.Key).ToLowerInvariant()}");
            return string.Join(", ", kinds) + (_folder != null ? " in " + PathDisplay.Shown(_folder) : "") + ". Each format below says how many of them it can take.";
        }

        private static string KindLabel(FileKind k) => k switch { FileKind.Video => "Video", FileKind.Audio => "Audio", FileKind.Image => "Picture", _ => "File" };
        private static string KindId(FormatSpec f) => f.Kind == TargetKind.Video ? "video" : f.Kind == TargetKind.Audio ? "audio" : "image";
        private static string GroupTitle(string id) => id switch { "video" => "Video", "audio" => "Audio", _ => "Pictures and animations" };

        // ------------------------------------------------------------ rows

        private void Render()
        {
            _list.Children.Clear();
            int shown = 0;
            foreach (var group in new[] { "video", "audio", "image" })
            {
                var rows = _targets.Where(t => KindId(t.Spec) == group)
                    .Where(t => _filter == "all" || (_filter == "lossless" ? t.Spec.Lossless : KindId(t.Spec) == _filter))
                    .Where(t => !_search.State.IsActive || _search.State.Matches(SearchText(t.Spec)))
                    .ToList();
                if (rows.Count == 0) continue;
                _list.Children.Add(new TextBlock { Text = GroupTitle(group), Style = Ui.Style("T.Body"), FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Mint"), Margin = new Thickness(4, shown == 0 ? 0 : 14, 0, 6) });
                foreach (var t in rows) { _list.Children.Add(Row(t.Spec, t.Count)); shown++; }
            }
            if (shown == 0)
                _list.Children.Add(new TextBlock { Text = _targets.Count == 0 ? "None of these files can be converted here. Video, audio and picture files can." : "No format matches. Clear the search or pick All.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 4, 8) });
            _search.SetCount(shown, _targets.Count, "formats");
            if (_picked != null && !_targets.Any(t => t.Spec == _picked)) _picked = null;
            _go.IsEnabled = _picked != null;
            _summary.Text = Summary();
        }

        private static string SearchText(FormatSpec f) => $"{f.Label} {f.Extension} {f.Id} {f.Description} {f.Quality} {(f.Lossless ? "lossless" : "")}";

        private FrameworkElement Row(FormatSpec f, int count)
        {
            bool sel = f == _picked;
            var b = new Button { Style = Ui.Style("Btn.Row"), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 1, 0, 1), Height = double.NaN };
            if (sel) { b.Background = Ui.Res("B.SecondaryContainer"); b.Foreground = Ui.Res("B.OnSecondaryContainer"); }
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            var top = new WrapPanel();
            top.Children.Add(new TextBlock { Text = f.Label, FontSize = 15, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            top.Children.Add(Tag(f.Extension, true));
            if (f.Lossless) top.Children.Add(Tag("Lossless", false));
            left.Children.Add(top);
            left.Children.Add(new TextBlock { Text = f.Description, FontSize = 13, Foreground = sel ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 3, 0, 0) });
            var notes = new List<string> { f.Quality };
            var kinds = _files.Select(ClipItem.Classify).Distinct().ToList();
            foreach (var k in kinds) { var n = f.SourceNote(k); if (!string.IsNullOrEmpty(n)) notes.Add(n); }
            left.Children.Add(new TextBlock { Text = string.Join(" · ", notes.Where(x => !string.IsNullOrEmpty(x))), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 2, 0, 0) });
            g.Children.Add(left);
            if (_files.Length > 1)
            {
                var c = new TextBlock { Text = $"{count} of {_files.Length}", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                Grid.SetColumn(c, 1); g.Children.Add(c);
            }
            b.Content = g;
            b.Click += (s, e) => { _picked = f; Render(); };
            b.MouseDoubleClick += (s, e) => { _picked = f; Convert(); };
            AutomationProperties.SetName(b, $"{f.Label}, {f.Extension}{(f.Lossless ? ", lossless" : "")}. {f.Description}" + (sel ? " Selected." : ""));
            return SearchScope.SetItem(b, SearchText(f));
        }

        private static Border Tag(string text, bool mono) => new Border
        {
            CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 1, 8, 2), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
            Background = mono ? Ui.Res("B.Sunken") : Ui.Res("B.MintWash"),
            Child = new TextBlock { Text = text, FontSize = 11.5, Foreground = mono ? Ui.Res("B.TextSoft") : Ui.Res("B.OnPrimaryContainer"), FontFamily = mono ? Ui.Font("F.Mono") : Ui.Font("F.Body") }
        };

        private string Summary()
        {
            if (_picked == null) return _targets.Count + " formats available. Pick one, then Convert, or double-click it.";
            int n = _files.Count(p => Converter.Compatible(p, _picked));
            var where = _outFolder != null ? PathDisplay.Shown(_outFolder) : "the same folder as the original";
            return n == 1 && _files.Length == 1
                ? $"{Path.GetFileNameWithoutExtension(_files[0])}{_picked.Extension} will be saved in {where}."
                : $"{n} of {_files.Length} files become {_picked.Label}, saved in {where}. The rest are skipped.";
        }

        // ------------------------------------------------------------ action

        private void Convert()
        {
            if (_picked == null) return;
            var id = _picked.Id;
            var todo = _files.Where(p => Converter.Compatible(p, _picked)).ToArray();
            Close();
            if (todo.Length == 1) Converter.Start(todo[0], id, _outFolder);
            else if (todo.Length > 1) Converter.StartBatch(todo, id, _folder, _outFolder);
        }
    }
}
