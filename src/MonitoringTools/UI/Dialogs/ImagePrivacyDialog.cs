using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>Image privacy clean-up: shows exactly what each picture carries (location, camera and serial numbers, dates, author
    /// notes, colour profile, embedded thumbnail), lets the person choose what to remove, and writes clean copies. JPEG, PNG and WebP are
    /// cleaned inside the file so the picture is not re-compressed. Nothing is uploaded, the map is a drawing, and the coordinates are
    /// shown only in this window: they are not logged, not copied, and not part of Copy details.</summary>
    public sealed class ImagePrivacyDialog : MediaToolDialog
    {
        public static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp", ".heic" };
        public static bool Handles(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        internal sealed class Item
        {
            public string Path, Name; public PrivacyReport Report; public bool Selected; public BitmapSource Thumb;
            public Dictionary<string, bool> Choices = Defaults();
            public CleanResult Result;
        }

        private static Dictionary<string, bool> Defaults() => new Dictionary<string, bool>
        {
            [ExifData.Location] = true, [ExifData.Camera] = true, [ExifData.DateTimeGroup] = true, [ExifData.Author] = true, [ExifData.Colour] = false, [ExifData.Thumb] = true
        };

        private readonly List<Item> _items = new List<Item>();
        private readonly StackPanel _list = new StackPanel();
        private readonly StackPanel _summary = new StackPanel();
        private readonly StackPanel _detail = new StackPanel();
        private readonly Dictionary<string, bool> _shared = Defaults();
        private CheckBox _applyAll, _keep;
        private Item _current;
        private string _folder;
        private FrameworkElement _readyView;

        internal IReadOnlyList<Item> Items => _items;
        internal bool KeepOriginal => _keep?.IsChecked == true;
        internal Task LoadedTask { get; private set; } = Task.CompletedTask;
        internal List<CleanResult> LastResults { get; private set; } = new List<CleanResult>();

        public static ImagePrivacyDialog OpenFor(IEnumerable<string> files)
        {
            var list = (files ?? Array.Empty<string>()).Where(f => File.Exists(f) && Handles(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var d = new ImagePrivacyDialog(list);
            d.Show(); d.Activate();
            return d;
        }

        private ImagePrivacyDialog(List<string> files) : base("Image privacy clean-up", MediaIcons.Eye, 860, "Remove selected", Icons.Trash)
        {
            SetFile("scanning…", "");
            BuildUi();
            LoadedTask = LoadAsync(files);
        }

        // ------------------------------------------------------------ loading

        private async Task LoadAsync(List<string> files)
        {
            foreach (var f in files)
            {
                var it = new Item { Path = f, Name = System.IO.Path.GetFileName(f) };
                it.Report = await Task.Run(() => ImagePrivacyEngine.Scan(f));
                it.Selected = it.Report.Supported && it.Report.HasHidden;
                _items.Add(it);
            }
            if (_items.Count == 0) { ShowNothing(false); return; }
            _current = _items.FirstOrDefault(i => i.Report.HasLocation) ?? _items[0];
            int hidden = _items.Count(i => i.Report.HasHidden);
            SetFile(_items.Count == 1 ? _items[0].Name : _items[0].Name, _items.Count == 1 ? FileSummary(_items[0]) : $"and {_items.Count - 1} more · {hidden} of {_items.Count} have hidden data");
            if (hidden == 0) { ShowNothing(true); return; }
            Rebuild();
            Show(ToolState.Ready, _readyView);
            _ = LoadThumbsAsync();
        }

        private static string FileSummary(Item it) => it.Report.Supported ? (it.Report.HasHidden ? "has hidden data" : "no hidden data") : "not supported";

        private async Task LoadThumbsAsync()
        {
            foreach (var it in _items.ToList())
            {
                try
                {
                    it.Thumb = await Task.Run(() =>
                    {
                        if (it.Path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) || it.Path.EndsWith(".heic", StringComparison.OrdinalIgnoreCase)) return null;
                        var bi = new BitmapImage();
                        bi.BeginInit(); bi.UriSource = new Uri(it.Path); bi.DecodePixelWidth = 80; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
                        return (BitmapSource)bi;
                    });
                }
                catch { }
            }
            if (State == ToolState.Ready) RebuildList();
        }

        private void ShowNothing(bool anyPicture)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 24) };
            sp.Children.Add(new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(18), Background = Ui.Res("B.Sunken"), Child = Ui.Icon(Icons.Check, Ui.Res("B.Mint"), 24, 2), HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = anyPicture ? "Nothing to clean" : "No pictures to clean", FontSize = 16, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 6) });
            var t = FT.Body(anyPicture ? (_items.Count == 1 ? "This picture has no location, camera, date or author data. It is safe to share as it is." : "These pictures have no location, camera, date or author data. They are safe to share as they are.")
                                       : "Copy JPEG, PNG, TIFF or WebP pictures and open this tool from the notice.", 13);
            t.TextAlignment = TextAlignment.Center; t.MaxWidth = 520;
            sp.Children.Add(t);
            var unsupported = _items.Where(i => !i.Report.Supported).ToList();
            if (unsupported.Count > 0) { var u = FT.Body(string.Join(" ", unsupported.Select(i => i.Name + ": " + i.Report.Reason + ".")), 12, Ui.Res("B.TextMute"), new Thickness(0, 10, 0, 0)); u.TextAlignment = TextAlignment.Center; u.MaxWidth = 520; sp.Children.Add(u); }
            Show(ToolState.Done, sp, "Nothing was changed.");
            if (_items.Count > 0) SetSummary(_items.Count == 1 ? "1 picture" : _items.Count + " pictures");
        }

        // ------------------------------------------------------------ layout

        private void BuildUi()
        {
            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            root.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
            var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 232, Focusable = false };
            left.Children.Add(FT.Rounded(scroll, 16));
            _summary.Margin = new Thickness(0, 12, 0, 0);
            left.Children.Add(FT.Card(_summary, new Thickness(0, 12, 0, 12)));
            _applyAll = FT.SwitchRow("Apply the same choices to all selected pictures", null, true, on => { if (on && _current != null) foreach (var k in _current.Choices) _shared[k.Key] = k.Value; RebuildDetail(); UpdateStatus(); });
            left.Children.Add(_applyAll);
            root.Children.Add(left);

            var right = new StackPanel();
            right.Children.Add(_detail);
            Grid.SetColumn(right, 1);
            root.Children.Add(right);

            var all = new StackPanel();
            all.Children.Add(root);
            _keep = FT.SwitchRow("Keep the original", "Clean copies are saved as name-clean. Turn off to replace the files; the originals go to the Recycle Bin.", true, on => UpdateStatus());
            _keep.Margin = new Thickness(0, 8, 0, 4);
            all.Children.Add(_keep);
            _readyView = all;
            SetLeftActions(FT.Outline("Select all", SelectAll), FT.Tonal("Save in…", ChooseFolder, Icons.Folder));
            Show(ToolState.Ready, new TextBlock { Text = "Reading the pictures…", Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 30, 0, 30), HorizontalAlignment = HorizontalAlignment.Center });
            SetPrimary(false);
        }

        private void SelectAll()
        {
            foreach (var it in _items) it.Selected = it.Report.Supported;
            RebuildList(); UpdateStatus();
        }

        private void ChooseFolder()
        {
            var f = FolderChoice(this, "Choose where the clean copies go");
            if (f == null) return;
            _folder = f;
            SetLeftActions(FT.Outline("Select all", SelectAll), FT.Tonal("Save in " + PathDisplay.Shown(f), ChooseFolder, Icons.Folder));
            UpdateStatus();
        }

        private void Rebuild() { RebuildList(); RebuildSummary(); RebuildDetail(); UpdateStatus(); }

        private void RebuildList()
        {
            _list.Children.Clear();
            foreach (var it in _items)
            {
                var g = new Grid { Background = it == _current ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent, Cursor = Cursors.Hand };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var cb = FT.Check(it.Selected, "Clean " + it.Name, on => { it.Selected = on; UpdateStatus(); });
                cb.Margin = new Thickness(8, 0, 6, 0); cb.IsEnabled = it.Report.Supported;
                var thumb = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(8), Background = Ui.Res("B.Sunken"), Margin = new Thickness(0, 4, 10, 4), ClipToBounds = true, Child = it.Thumb != null ? (UIElement)new Image { Source = it.Thumb, Stretch = Stretch.UniformToFill } : Ui.Icon(Icons.Image, Ui.Res("B.TextMute"), 18, 1.6) };
                var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var n = new TextBlock { Text = it.Name, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = it.Path }; Core.Language.SetLiteral(n, true);
                names.Children.Add(n);
                names.Children.Add(new TextBlock { Text = it.Report.Summary, FontSize = 12, Foreground = it == _current ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft"), TextTrimming = TextTrimming.CharacterEllipsis });
                FrameworkElement badge = !it.Report.Supported ? FT.Badge("Unsupported", BannerKind.Info, MediaIcons.Warn)
                    : it.Report.HasLocation ? FT.Badge("Location", BannerKind.Warning, MediaIcons.Pin)
                    : it.Report.HasHidden ? FT.Badge("Hidden data", BannerKind.Warning, MediaIcons.Eye)
                    : FT.Badge("Clean", BannerKind.Success, Icons.Check);
                badge.Margin = new Thickness(6, 0, 10, 0);
                Grid.SetColumn(cb, 0); Grid.SetColumn(thumb, 1); Grid.SetColumn(names, 2); Grid.SetColumn(badge, 3);
                g.Children.Add(cb); g.Children.Add(thumb); g.Children.Add(names); g.Children.Add(badge);
                g.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource is DependencyObject d && IsInside<CheckBox>(d)) return; _current = it; RebuildList(); RebuildDetail(); };
                _list.Children.Add(g);
            }
        }

        private static bool IsInside<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null) { if (d is T) return true; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
            return false;
        }

        private void RebuildSummary()
        {
            _summary.Children.Clear();
            _summary.Children.Add(new TextBlock { Text = $"Found in these {_items.Count} pictures", FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 0, 0, 8) });
            var wrap = new WrapPanel();
            int loc = _items.Count(i => i.Report.HasLocation), serial = _items.Count(i => !string.IsNullOrEmpty(i.Report.Serial)), date = _items.Count(i => i.Report.Group(ExifData.DateTimeGroup)?.Found == true);
            if (loc > 0) wrap.Children.Add(Margin(FT.Badge($"{loc} with location", BannerKind.Warning)));
            if (serial > 0) wrap.Children.Add(Margin(FT.Badge($"{serial} with camera serial", BannerKind.Warning)));
            if (date > 0) wrap.Children.Add(Margin(FT.Badge($"{date} with date", BannerKind.Info)));
            if (wrap.Children.Count == 0) wrap.Children.Add(Margin(FT.Badge("Other hidden data", BannerKind.Info)));
            _summary.Children.Add(wrap);
            _summary.Children.Add(FT.Body("Select a picture to see exactly what it holds.", 12, null, new Thickness(0, 6, 0, 0)));
        }

        private static FrameworkElement Margin(FrameworkElement e) { e.Margin = new Thickness(0, 0, 6, 6); return e; }

        private Dictionary<string, bool> ChoicesOf(Item it) => _applyAll?.IsChecked == true ? _shared : it.Choices;

        private void RebuildDetail()
        {
            _detail.Children.Clear();
            var it = _current; if (it == null) return;
            var rep = it.Report;
            if (!rep.Supported) { _detail.Children.Add(FT.Banner(BannerKind.Info, "This picture cannot be cleaned here", rep.Reason + ". Nothing will be changed.")); return; }
            if (rep.HasLocation)
                _detail.Children.Add(FT.Banner(BannerKind.Warning, "This picture shows where it was taken", "Anyone you send it to can read the location" + (!string.IsNullOrEmpty(rep.Serial) ? " and the camera's serial number." : ".")));
            else if (rep.HasHidden)
                _detail.Children.Add(FT.Banner(BannerKind.Info, "This picture carries hidden details", "Date, camera or author details can be read by anyone you send it to."));
            else
                _detail.Children.Add(FT.Banner(BannerKind.Success, "Nothing hidden in this picture", "It has no location, camera, date or author data."));
            if (rep.HasLocation) _detail.Children.Add(MapCard(rep));
            var choices = ChoicesOf(it);
            foreach (var g in rep.Groups)
            {
                var info = ImagePrivacyEngine.GroupInfo.First(x => x.Id == g.Id);
                var row = new Grid { Margin = new Thickness(0, 0, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition());
                var cb = FT.Check(g.Found && choices.TryGetValue(g.Id, out var on) && on, info.Title, v => { choices[g.Id] = v; UpdateStatus(); });
                cb.IsEnabled = g.Found; cb.Margin = new Thickness(0, 12, 14, 0); cb.VerticalAlignment = VerticalAlignment.Top;
                var text = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
                var head = new WrapPanel();
                head.Children.Add(new TextBlock { Text = info.Title, FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                if (g.Found) head.Children.Add(FT.Badge(g.Id == ExifData.Colour ? "Kept by default" : "Found", g.Id == ExifData.Colour ? BannerKind.Info : BannerKind.Warning));
                else head.Children.Add(FT.Badge("Not present", BannerKind.Info));
                text.Children.Add(head);
                var detail = FT.Body(g.Found ? g.Detail : info.Sub, 13, null, new Thickness(0, 2, 0, 0)); Core.Language.SetLiteral(detail, g.Found && (g.Id == ExifData.Camera || g.Id == ExifData.Location));
                text.Children.Add(detail);
                Grid.SetColumn(text, 1);
                row.Children.Add(cb); row.Children.Add(text);
                _detail.Children.Add(new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Child = row });
            }
        }

        /// <summary>A drawn stand-in for a map with a pin, and the coordinates under it. No map service is contacted.</summary>
        private FrameworkElement MapCard(PrivacyReport rep)
        {
            var canvas = new Canvas { Height = 110, Background = Ui.Res("B.Sunken") };
            var road = new Pen(Ui.Res("B.LineStrong"), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var geo1 = Geometry.Parse("M0,90 C120,70 160,100 260,60 S420,30 520,45");
            var geo2 = Geometry.Parse("M110,0 C130,40 170,70 230,110");
            canvas.Children.Add(new System.Windows.Shapes.Path { Data = geo1, Stroke = Ui.Res("B.LineStrong"), StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            canvas.Children.Add(new System.Windows.Shapes.Path { Data = geo2, Stroke = Ui.Res("B.Line"), StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            var pin = Ui.Icon(MediaIcons.Pin, Ui.Res("B.Danger"), 26, 2); Canvas.SetLeft(pin, 160); Canvas.SetTop(pin, 22); canvas.Children.Add(pin);
            var cap = new TextBlock { Text = "Map placeholder, no map service is contacted", FontSize = 12, Foreground = Ui.Res("B.Text"), Margin = new Thickness(10, 0, 0, 8) };
            Canvas.SetLeft(cap, 0); Canvas.SetTop(cap, 84); canvas.Children.Add(cap);
            var host = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            host.Children.Add(FT.Rounded(canvas, 14));
            var parts = new List<string> { ExifData.FormatCoordinate(rep.Lat.Value, true) + ", " + ExifData.FormatCoordinate(rep.Lon.Value, false) };
            if (rep.Accuracy.HasValue) parts.Add($"accurate to about {rep.Accuracy.Value:0} m");
            if (rep.Altitude.HasValue) parts.Add($"altitude {rep.Altitude.Value:0} m");
            var line = FT.Body(string.Join(" · ", parts), 12, null, new Thickness(0, 6, 0, 0)); Core.Language.SetLiteral(line, true);
            AutomationProperties.SetName(line, "Location shown only in this window");
            host.Children.Add(line);
            return host;
        }

        private void UpdateStatus()
        {
            var sel = _items.Where(i => i.Selected && i.Report.Supported).ToList();
            if (sel.Count == 0) { SetStatus("No pictures are ticked."); SetPrimary(false); return; }
            int groups = sel.Max(i => ChoicesOf(i).Count(c => c.Value && (i.Report.Group(c.Key)?.Found ?? false)));
            bool any = sel.Any(i => ChoicesOf(i).Any(c => c.Value && (i.Report.Group(c.Key)?.Found ?? false)));
            SetStatus(any ? $"{groups} {(groups == 1 ? "group" : "groups")} will be removed from each of {sel.Count} {(sel.Count == 1 ? "picture" : "pictures")}" : "Nothing is chosen to remove.");
            SetPrimary(any, sel.Count == 1 ? "Remove selected" : "Remove selected");
        }

        // ------------------------------------------------------------ test seams

        internal void SetChoice(string group, bool on) { _shared[group] = on; foreach (var it in _items) it.Choices[group] = on; RebuildDetail(); UpdateStatus(); }
        internal void SetKeepOriginal(bool on) { _keep.IsChecked = on; }
        internal void SetSelected(string path, bool on) { var it = _items.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)); if (it != null) { it.Selected = on; RebuildList(); UpdateStatus(); } }
        internal void SetFolder(string f) { _folder = f; }
        internal void Current(string path) { _current = _items.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)) ?? _current; RebuildList(); RebuildDetail(); }

        // ------------------------------------------------------------ running

        protected override void PrimaryPressed() => Run(GoAsync);

        internal async Task GoAsync()
        {
            if (State != ToolState.Ready) return;
            var todo = _items.Where(i => i.Selected && i.Report.Supported).ToList();
            if (todo.Count == 0) return;
            NewRun(); var ct = Cts.Token;
            var pb = new FT.ProgressBlock("Progress");
            var sp = new StackPanel(); sp.Children.Add(pb);
            Show(ToolState.Working, sp, "Cancel keeps the pictures already cleaned.");
            SetSummary("working");
            var results = new List<CleanResult>();
            for (int i = 0; i < todo.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var it = todo[i];
                pb.Set((double)i / todo.Count, $"Picture {i + 1} of {todo.Count}", "Removing metadata without re-compressing the picture.");
                var remove = new HashSet<string>(ChoicesOf(it).Where(c => c.Value).Select(c => c.Key));
                var dir = _folder ?? System.IO.Path.GetDirectoryName(it.Path);
                var dest = MediaProc.UniquePath(dir, System.IO.Path.GetFileNameWithoutExtension(it.Path) + "-clean", System.IO.Path.GetExtension(it.Path), it.Path);
                var r = await Task.Run(() => ImagePrivacyEngine.Clean(it.Path, dest, it.Report, remove));
                if (r.Ok && !KeepOriginal)
                {
                    try
                    {
                        var recycled = await FileOps.RecycleAsync(new[] { it.Path });
                        if (recycled.All(x => x.Ok)) { File.Move(dest, it.Path); r.Output = it.Path; }
                        else { r.Error = "The original could not be moved to the Recycle Bin, so it was kept and the clean copy was saved beside it."; }
                    }
                    catch (Exception ex) { r.Error = ex.Message; }
                }
                it.Result = r; results.Add(r);
                await Task.Delay(1);
            }
            LastResults = results;
            if (ct.IsCancellationRequested)
            {
                Show(ToolState.Ready, _readyView);
                RebuildList(); UpdateStatus();
                SetStatus($"Cancelled after {results.Count(r => r.Ok)} of {todo.Count}. The pictures already cleaned were kept.");
                return;
            }
            ShowDone(todo, results);
        }

        private void ShowDone(List<Item> todo, List<CleanResult> results)
        {
            int ok = results.Count(r => r.Ok), failed = results.Count - ok;
            var sp = new StackPanel();
            if (failed == 0) sp.Children.Add(FT.Banner(BannerKind.Success, ok == 1 ? "1 clean copy saved" : $"{ok} clean copies saved", KeepOriginal ? "Picture quality is unchanged, and the originals were kept." : "Picture quality is unchanged, and the originals went to the Recycle Bin."));
            else sp.Children.Add(FT.Banner(BannerKind.Warning, $"{ok} of {results.Count} cleaned", $"{(failed == 1 ? "One picture" : failed + " pictures")} could not be cleaned and was not changed."));
            var rows = new List<(string, string)>();
            foreach (var r in results)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(r.Output ?? r.Source) + System.IO.Path.GetExtension(r.Output ?? r.Source);
                if (r.Ok)
                {
                    var after = ImagePrivacyEngine.Scan(r.Output);
                    rows.Add((name, $"{r.Removed} tags removed, {after.TagTotal - (after.Group(ExifData.Colour)?.Found == true ? after.Group(ExifData.Colour).Tags : 0)} left that identify you"));
                }
                else rows.Add((System.IO.Path.GetFileName(r.Source), "Not changed: " + (r.Error ?? "unknown error")));
            }
            var kept = results.Where(r => r.Ok).SelectMany(r => r.Kept).Distinct().ToList();
            if (kept.Count > 0) rows.Add(("Kept", string.Join(", ", kept.Select(k => k == "Colour profile" ? "Colour profile" : k))));
            sp.Children.Add(FT.FactsCard(rows, 220));
            var checkHost = new StackPanel();
            var row = new WrapPanel();
            var first = results.FirstOrDefault(r => r.Ok);
            if (first != null)
            {
                row.Children.Add(FT.Tonal("Show in folder", () => Reveal(first.Output), Icons.Folder));
                row.Children.Add(FT.Outline("Check the copy", () =>
                {
                    checkHost.Children.Clear();
                    var again = ImagePrivacyEngine.Scan(first.Output);
                    if (!again.HasHidden) checkHost.Children.Add(FT.Banner(BannerKind.Success, "The copy is clean", $"{System.IO.Path.GetFileName(first.Output)} was read again: no location, camera, date or author data is left."));
                    else checkHost.Children.Add(FT.Banner(BannerKind.Info, "Some data is still in the copy", string.Join(", ", again.Groups.Where(g => g.Found && g.Id != ExifData.Colour).Select(g => g.Title)) + " was kept because it was not chosen."));
                }));
            }
            sp.Children.Add(row); sp.Children.Add(checkHost);
            SetSummary(failed == 0 ? "done" : $"{ok} cleaned, {failed} failed");
            Show(ToolState.Done, sp, failed == 0 ? $"{ok} clean {(ok == 1 ? "copy" : "copies")} saved." : $"{ok} of {results.Count} cleaned.");
        }
    }
}
