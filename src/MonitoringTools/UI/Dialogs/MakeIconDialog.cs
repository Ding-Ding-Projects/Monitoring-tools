using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>Make an icon: turns a picture (PNG, JPG, BMP, GIF, WebP or SVG) into a multi-size .ico, with a live preview of every size
    /// on a light and a dark tile, optional padding and a shape. Built in .NET with no external tool. Sizes larger than the picture
    /// are flagged, and the person can still continue.</summary>
    public sealed class MakeIconDialog : MediaToolDialog
    {
        public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".svg" };
        public static bool Handles(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        private readonly string _src;
        private BitmapSource _source;
        private bool _hasAlpha;
        private readonly Dictionary<int, bool> _sizes = IconMaker.AllSizes.ToDictionary(s => s, s => true);
        private readonly Dictionary<int, Button> _chips = new Dictionary<int, Button>();
        private FT.ChipGroup _shape;
        private CheckBox _transparent;
        private Slider _padding;
        private TextBlock _paddingVal, _hint;
        private TextBox _nameBox;
        private WrapPanel _lightTile, _darkTile;
        private StackPanel _bannerHost, _chipHost;
        private Image _sourceImage;
        private FrameworkElement _readyView;
        private string _folder;
        private bool _nameTouched;

        internal bool HasAlpha => _hasAlpha;
        internal int SourceSize => _source == null ? 0 : Math.Min(_source.PixelWidth, _source.PixelHeight);
        internal IconOptions Options => new IconOptions
        {
            Sizes = _sizes.Where(k => k.Value).Select(k => k.Key).OrderBy(x => x).ToList(),
            KeepTransparency = _transparent?.IsChecked == true, Padding = (_padding?.Value ?? 0) / 100.0,
            Shape = (IconShape)(_shape?.Index ?? 0)
        };
        internal string LastOutput { get; private set; }
        internal string LastError { get; private set; }

        public static MakeIconDialog OpenFor(string path)
        {
            if (!File.Exists(path)) { App.Toasts.ShowNotice("Nothing to convert", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var ex = Existing<MakeIconDialog>(d => string.Equals(d._src, path, StringComparison.OrdinalIgnoreCase));
            if (ex != null) { ex.Activate(); return ex; }
            var d = new MakeIconDialog(path);
            d.Show(); d.Activate();
            return d;
        }

        private MakeIconDialog(string path) : base("Make an icon", MediaIcons.Stamp, 820, "Make icon", Icons.Check)
        {
            _src = path;
            try { _source = IconMaker.Load(path); _hasAlpha = IconMaker.HasAlpha(_source); }
            catch (Exception ex)
            {
                SetFile(Path.GetFileName(path), "could not read");
                Show(ToolState.Error, ErrorView("This picture could not be read", "The imaging component reported: " + ex.Message, $"Nothing was written and {Path.GetFileName(path)} is untouched.", null, null, ex.Message), "Nothing was saved.");
                return;
            }
            SetFile(Path.GetFileName(path), $"{_source.PixelWidth}×{_source.PixelHeight}" + (_hasAlpha ? " · transparent" : ""));
            Build();
        }

        private void Build()
        {
            var root = new StackPanel();
            _bannerHost = new StackPanel();
            root.Children.Add(_bannerHost);

            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            top.ColumnDefinitions.Add(new ColumnDefinition());

            var left = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
            _sourceImage = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(14), Source = _source };
            RenderOptions.SetBitmapScalingMode(_sourceImage, BitmapScalingMode.HighQuality);
            var check = FT.Rounded(_sourceImage, 16, Checker());
            check.Height = 190; check.Width = 190;
            AutomationProperties.SetName(check, "The picture on a checkerboard, so transparent parts show");
            left.Children.Add(check);
            _transparent = FT.SwitchRow("Keep transparency", null, _hasAlpha, on => Preview());
            _transparent.Margin = new Thickness(0, 14, 0, 6);
            left.Children.Add(_transparent);
            _paddingVal = FT.Body("0%", 12);
            _padding = FT.MakeSlider("Padding", 0, 40, 8, 1, v => { _paddingVal.Text = (int)v + "%"; Preview(); });
            _paddingVal.Text = "8%";
            var ph = new Grid();
            ph.ColumnDefinitions.Add(new ColumnDefinition()); ph.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ph.Children.Add(FT.Label("Padding", 13, Ui.Res("B.Text"), FontWeights.Medium, new Thickness(0, 0, 0, 2))); Grid.SetColumn(_paddingVal, 1); ph.Children.Add(_paddingVal);
            left.Children.Add(ph); left.Children.Add(_padding);
            top.Children.Add(left);

            var right = new StackPanel();
            right.Children.Add(FT.Eyebrow("Sizes inside the .ico"));
            _chipHost = new StackPanel();
            var chips = new WrapPanel();
            foreach (var s in IconMaker.AllSizes)
            {
                int size = s;
                var c = FT.Chip(size.ToString(), true, on => { _sizes[size] = on; Preview(); UpdateChips(); }, size + " px");
                _chips[size] = c; chips.Children.Add(c);
            }
            right.Children.Add(chips);
            _hint = FT.Body("Windows picks the closest size for the taskbar, title bars and Explorer. Keep all seven unless you know you need fewer.", 12, null, new Thickness(0, 0, 0, 12));
            right.Children.Add(_hint);
            right.Children.Add(FT.Eyebrow("Preview (128 and 256 shown smaller)"));
            var tiles = new Grid();
            tiles.ColumnDefinitions.Add(new ColumnDefinition()); tiles.ColumnDefinitions.Add(new ColumnDefinition());
            _lightTile = new WrapPanel(); _darkTile = new WrapPanel();
            var lt = new Border { CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)), Padding = new Thickness(12), Margin = new Thickness(0, 0, 6, 0), Child = _lightTile, MinHeight = 236 };
            var dt = new Border { CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)), Padding = new Thickness(12), Margin = new Thickness(6, 0, 0, 0), Child = _darkTile, MinHeight = 236 };
            AutomationProperties.SetName(lt, "Preview of every size on a light background"); AutomationProperties.SetName(dt, "Preview of every size on a dark background");
            Grid.SetColumn(dt, 1); tiles.Children.Add(lt); tiles.Children.Add(dt);
            right.Children.Add(tiles);
            Grid.SetColumn(right, 1);
            top.Children.Add(right);
            root.Children.Add(top);

            var bottom = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition());
            _shape = new FT.ChipGroup(new[] { "Keep the picture as it is", "Rounded square", "Circle" }, 0, "Shape");
            _shape.Changed += i => Preview();
            var shapeField = FT.Field("Shape", _shape, null, new Thickness(0, 0, 12, 0));
            _nameBox = FT.TextField("Save as", Path.GetFileName(DefaultOutput()), s => _nameTouched = true, false);
            var nameField = FT.Field("Save as", FT.FileNameField(_nameBox), null, new Thickness(12, 0, 0, 0));
            Grid.SetColumn(nameField, 1);
            bottom.Children.Add(shapeField); bottom.Children.Add(nameField);
            root.Children.Add(bottom);

            SetLeftActions(FT.Tonal("Choose folder", ChooseFolder, Icons.Folder));
            _readyView = root;
            Show(ToolState.Ready, root);
            UpdateChips(); Preview();
        }

        private static Brush Checker()
        {
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x2A, 0x2E, 0x31)), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x18, 0x1B, 0x1D)), null, new GeometryGroup { Children = { new RectangleGeometry(new Rect(0, 0, 8, 8)), new RectangleGeometry(new Rect(8, 8, 8, 8)) } }));
            var b = new DrawingBrush(dg) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 16, 16), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None };
            b.Freeze();
            return b;
        }

        private string DefaultOutput() => MediaProc.UniquePath(_folder ?? Path.GetDirectoryName(_src), Path.GetFileNameWithoutExtension(_src), ".ico", _src);

        private void ChooseFolder()
        {
            var f = FolderChoice(this, "Choose where the icon goes");
            if (f == null) return;
            _folder = f;
            if (!_nameTouched) { _nameBox.Text = Path.GetFileName(DefaultOutput()); _nameBox.Tag = _nameBox.Text; }
            SetLeftActions(FT.Tonal("Save in " + PathDisplay.Shown(f), ChooseFolder, Icons.Folder));
        }

        /// <summary>Marks the sizes bigger than the picture and shows the warning that says what that means.</summary>
        private void UpdateChips()
        {
            int src = SourceSize;
            var big = IconMaker.AllSizes.Where(s => s > src).ToList();
            foreach (var s in IconMaker.AllSizes)
            {
                var b = _chips[s];
                bool on = _sizes[s], warn = s > src;
                var fg = on ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft");
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                if (warn) sp.Children.Add(new ContentControl { Content = Ui.Icon(MediaIcons.Warn, Ui.Res("B.Amber"), 13, 1.8), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
                else if (on) sp.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Check, fg, 13, 2), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = s.ToString(), VerticalAlignment = VerticalAlignment.Center });
                b.Content = sp;
                b.Background = on ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
                b.Foreground = fg; b.BorderBrush = on ? Brushes.Transparent : Ui.Res("B.LineStrong");
                b.Tag = on;
                AutomationProperties.SetName(b, s + " px" + (warn ? ", larger than the picture" : ""));
                AutomationProperties.SetItemStatus(b, on ? "on" : "off");
            }
            _bannerHost.Children.Clear();
            var enlarged = big.Where(s => _sizes[s]).ToList();
            if (big.Count > 0)
            {
                var turnOff = FT.Accent("Turn off 128 and 256", () => { foreach (var s in big) _sizes[s] = false; UpdateChips(); Preview(); });
                _bannerHost.Children.Add(FT.Banner(BannerKind.Warning, $"This picture is smaller than {big.Max()} px",
                    $"Sizes {string.Join(" and ", big)} would be enlarged and look soft. Choose them off, or start from a larger picture.", enlarged.Count > 0 ? turnOff : null));
            }
            int n = _sizes.Count(k => k.Value);
            string note = enlarged.Count > 0 ? $"Not enough pixels: {string.Join(" and ", enlarged)}. You can still continue; the result will be soft." : null;
            SetStatus(n == 0 ? "Choose at least one size." : note != null ? $"{n} sizes selected. {note}" : $"{n} {(n == 1 ? "size" : "sizes")} · about {Ui.Bytes(EstimateBytes())}");
            SetPrimary(n > 0);
        }

        private long EstimateBytes()
        {
            long total = 6 + 16 * _sizes.Count(k => k.Value);
            foreach (var s in _sizes.Where(k => k.Value).Select(k => k.Key)) total += (long)(s * s * 4 * 0.45) + 200;
            return total;
        }

        private void Preview()
        {
            if (_lightTile == null || _source == null) return;
            var o = Options;
            _lightTile.Children.Clear(); _darkTile.Children.Clear();
            foreach (var s in IconMaker.AllSizes.Where(s => _sizes[s]))
            {
                var img = IconMaker.Render(_source, s, o);
                double shown = s >= 128 ? 96 : s;
                foreach (var host in new[] { _lightTile, _darkTile })
                {
                    var sp = new StackPanel { Margin = new Thickness(0, 0, 12, 8), VerticalAlignment = VerticalAlignment.Bottom };
                    var im = new Image { Source = img, Width = shown, Height = shown, Stretch = Stretch.Uniform };
                    RenderOptions.SetBitmapScalingMode(im, s >= 128 ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
                    sp.Children.Add(im);
                    sp.Children.Add(new TextBlock { Text = s.ToString(), FontSize = 11, Foreground = host == _lightTile ? new SolidColorBrush(Color.FromRgb(0x44, 0x4B, 0x50)) : new SolidColorBrush(Color.FromRgb(0xC0, 0xC8, 0xCC)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
                    host.Children.Add(sp);
                }
            }
            UpdateStatusOnly();
        }

        private void UpdateStatusOnly() { if (State == ToolState.Ready) { int n = _sizes.Count(k => k.Value); if (n > 0 && SourceSize >= 256) SetStatus($"{n} {(n == 1 ? "size" : "sizes")} · about {Ui.Bytes(EstimateBytes())}"); } }

        // ------------------------------------------------------------ test seams

        internal void SetSize(int size, bool on) { _sizes[size] = on; UpdateChips(); Preview(); }
        internal void SetShape(int i) => _shape.Select(i, true);
        internal void SetPadding(double percent) => _padding.Value = percent;
        internal void SetTransparency(bool on) => _transparent.IsChecked = on;
        internal void SetOutputName(string n) { _nameBox.Text = n; _nameBox.Tag = n; _nameTouched = true; }
        internal void SetOutputFolder(string f) { _folder = f; }
        internal FrameworkElement ReadyView => _readyView;

        // ------------------------------------------------------------ running

        protected override void PrimaryPressed() => Run(GoAsync);

        internal async Task GoAsync()
        {
            if (State != ToolState.Ready) return;
            var o = Options;
            if (o.Sizes.Count == 0) return;
            var name = (_nameBox.Text ?? "").Trim();
            if (name.Length == 0) name = Path.GetFileName(DefaultOutput());
            if (!string.Equals(Path.GetExtension(name), ".ico", StringComparison.OrdinalIgnoreCase)) name = Path.GetFileNameWithoutExtension(name) + ".ico";
            name = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var dest = MediaProc.UniquePath(_folder ?? Path.GetDirectoryName(_src), Path.GetFileNameWithoutExtension(name), ".ico", _src);
            NewRun(); var ct = Cts.Token;
            var pb = new FT.ProgressBlock("Progress");
            var sp = new StackPanel(); sp.Children.Add(pb);
            Show(ToolState.Working, sp, "Cancel stops now. Nothing is written until every size is ready.");
            SetSummary("working");
            byte[] data = null; string error = null;
            try
            {
                var src = _source;
                data = await Task.Run(() => IconMaker.Build(src, o, (i, n, size) =>
                {
                    ct.ThrowIfCancellationRequested();
                    pb.Set((double)i / n, $"Size {i + 1} of {n}", $"Resampling to {size}×{size} with a high-quality filter.");
                }), ct);
                pb.Set(0.95, "Writing the file", "Checking the sizes inside it.");
                var found = IconMaker.ReadSizes(data);
                if (!o.Sizes.OrderBy(x => x).SequenceEqual(found)) error = "The icon was built with different sizes than the ones chosen, so it was not saved.";
                else
                {
                    var part = dest + ".part";
                    File.WriteAllBytes(part, data);
                    File.Move(part, dest);
                }
            }
            catch (OperationCanceledException) { error = "Cancelled"; }
            catch (Exception ex) { error = ex.Message; }
            if (error == "Cancelled") { Show(ToolState.Ready, _readyView); UpdateChips(); SetStatus("Cancelled. Nothing was saved."); return; }
            if (error != null)
            {
                LastError = error;
                SetSummary("could not make the icon");
                Show(ToolState.Error, ErrorView("The icon could not be made", error.StartsWith("The icon was built") ? error : "The imaging component reported: " + error, $"No icon file was kept. {Path.GetFileName(_src)} is untouched.", () => { Show(ToolState.Ready, _readyView); UpdateChips(); }, "Back to the settings", error), "Nothing was saved.");
                return;
            }
            LastOutput = dest;
            var done = new StackPanel();
            done.Children.Add(FT.Banner(BannerKind.Success, "Saved " + Path.GetFileName(dest), $"{(o.Sizes.Count == 7 ? "Seven" : o.Sizes.Count.ToString())} {(o.Sizes.Count == 1 ? "size" : "sizes")} in one file. The picture was not changed."));
            done.Children.Add(FT.FactsCard(new[] { ("Sizes", string.Join(", ", o.Sizes)), ("File size", Ui.Bytes(new FileInfo(dest).Length) + (o.Sizes.Contains(256) ? " (256 px stored compressed)" : "")) }));
            var row = new WrapPanel();
            row.Children.Add(FT.Tonal("Show in folder", () => Reveal(dest), Icons.Folder));
            row.Children.Add(FT.Tonal("Copy path", () => { try { Clipboard.SetText(dest); SetStatus("Path copied."); } catch { } }, MediaIcons.Copy));
            done.Children.Add(row);
            SetSummary("done");
            Show(ToolState.Done, done, $"{Path.GetFileName(dest)} saved.");
        }
    }
}
