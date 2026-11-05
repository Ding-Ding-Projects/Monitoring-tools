using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// <summary>The before and after of one picture with a draggable divider. The divider is also a slider: Left and Right move it.</summary>
    internal sealed class SplitPreview : Grid
    {
        private readonly Image _before = new Image { Stretch = Stretch.Uniform }, _after = new Image { Stretch = Stretch.Uniform };
        private readonly Border _line = new Border { Width = 2, Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
        private readonly Border _knob;
        private readonly TextBlock _beforeLabel = new TextBlock(), _afterLabel = new TextBlock();
        private double _pos = 0.5;
        public double Position => _pos;

        public SplitPreview()
        {
            Height = 190; Focusable = true; FocusVisualStyle = null; Cursor = Cursors.SizeWE; Background = Brushes.Transparent;
            RenderOptions.SetBitmapScalingMode(_before, BitmapScalingMode.HighQuality); RenderOptions.SetBitmapScalingMode(_after, BitmapScalingMode.HighQuality);
            Children.Add(_before); Children.Add(_after); Children.Add(_line);
            _knob = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Mint"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Child = Ui.Icon(MediaIcons.Swap, Ui.Res("B.MintInk"), 16, 2), IsHitTestVisible = false };
            Children.Add(_knob);
            Chip(_beforeLabel, HorizontalAlignment.Left); Chip(_afterLabel, HorizontalAlignment.Right);
            AutomationProperties.SetName(this, "Before and after divider. Left and Right move it.");
            SizeChanged += (s, e) => Place();
            MouseLeftButtonDown += (s, e) => { Focus(); CaptureMouse(); MoveTo(e.GetPosition(this).X); };
            MouseMove += (s, e) => { if (IsMouseCaptured) MoveTo(e.GetPosition(this).X); };
            MouseLeftButtonUp += (s, e) => ReleaseMouseCapture();
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Left) { _pos = Math.Max(0, _pos - 0.05); Place(); e.Handled = true; }
                else if (e.Key == Key.Right) { _pos = Math.Min(1, _pos + 0.05); Place(); e.Handled = true; }
            };
        }

        private void Chip(TextBlock t, HorizontalAlignment h)
        {
            t.FontSize = 12; t.FontWeight = FontWeights.Medium; t.Foreground = Brushes.White;
            Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(200, 8, 9, 12)), Padding = new Thickness(7, 2, 7, 3), Margin = new Thickness(8), HorizontalAlignment = h, VerticalAlignment = VerticalAlignment.Bottom, Child = t, IsHitTestVisible = false });
        }

        private void MoveTo(double x) { _pos = Math.Clamp(ActualWidth <= 0 ? 0.5 : x / ActualWidth, 0, 1); Place(); }

        private void Place()
        {
            double x = _pos * ActualWidth;
            _line.Margin = new Thickness(Math.Max(0, x - 1), 0, 0, 0);
            _knob.Margin = new Thickness(Math.Max(0, x - 16), 0, 0, 0);
            _after.Clip = new RectangleGeometry(new Rect(x, 0, Math.Max(0, ActualWidth - x), ActualHeight));
        }

        public void Set(BitmapSource before, BitmapSource after, string beforeText, string afterText)
        {
            _before.Source = before; _after.Source = after;
            _beforeLabel.Text = beforeText; _afterLabel.Text = afterText;
            Place();
        }
    }

    /// <summary>Resize, crop and compress for one or many pictures. The list shows the size each will end up at, the split preview shows
    /// the crop and the real effect of the quality on one picture, and the original files are never changed: copies are saved with the
    /// suffix -small, next to the original or in the chosen folder.</summary>
    public sealed class ImagesResizeDialog : MediaToolDialog
    {
        public static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".heic" };
        public static bool Handles(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        internal enum Pic { Pending, Working, Done, Failed }

        internal sealed class Item
        {
            public string Path, Name; public int W, H; public long Bytes; public bool Selected = true;
            public BitmapSource Thumb; public Pic Status; public string Error, Output; public long OutBytes;
            public CheckBox Check; public TextBlock Result, Dims;
        }

        private readonly List<Item> _items = new List<Item>();
        private readonly StackPanel _list = new StackPanel();
        private FT.IntBox _width, _height, _target;
        private CheckBox _keep;
        private FT.ChipGroup _crop;
        private FT.Segmented _quality;
        private Slider _qualitySlider;
        private TextBlock _qualityVal, _qualityHint;
        private FrameworkElement _qualityBox, _targetBox;
        private SplitPreview _split;
        private FrameworkElement _readyView, _emptyView;
        private Item _current;
        private string _folder;
        private bool _building;
        private int _quality2 = 82;
        private CancellationTokenSource _previewCts = new CancellationTokenSource();
        private readonly Dictionary<string, BitmapSource> _smallCache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        internal IReadOnlyList<Item> Items => _items;
        internal ImageOptions Options => new ImageOptions
        {
            Width = _width?.Value ?? 0, Height = _height?.Value ?? 0, KeepProportions = _keep?.IsChecked == true,
            Crop = new[] { "none", "1:1", "4:3", "16:9", "free" }[_crop?.Index ?? 0], UseTarget = _quality?.Index == 1,
            Quality = _quality2, TargetBytes = (_target?.Value ?? 700) * 1024L, Folder = _folder
        };
        internal Task LoadedTask { get; private set; } = Task.CompletedTask;
        internal List<ImageResult> LastResults { get; private set; } = new List<ImageResult>();

        public static ImagesResizeDialog OpenFor(IEnumerable<string> files)
        {
            var list = (files ?? Array.Empty<string>()).Where(f => File.Exists(f) && Handles(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var d = new ImagesResizeDialog(list);
            d.Show(); d.Activate();
            return d;
        }

        private ImagesResizeDialog(List<string> files) : base("Resize, crop and compress", MediaIcons.Crop, 840, "Save copies", Icons.Check)
        {
            AllowDrop = true;
            Drop += (s, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] f) AddFiles(f); };
            SetFile("reading the pictures…", "");
            BuildUi();
            LoadedTask = LoadAsync(files);
        }

        protected override void OnClosed2() { try { _previewCts.Cancel(); } catch { } }

        // ------------------------------------------------------------ loading the pictures

        private async Task LoadAsync(IEnumerable<string> files)
        {
            var add = new List<Item>();
            foreach (var f in files)
            {
                if (_items.Any(i => string.Equals(i.Path, f, StringComparison.OrdinalIgnoreCase))) continue;
                var it = new Item { Path = f, Name = System.IO.Path.GetFileName(f) };
                try { it.Bytes = new FileInfo(f).Length; } catch { }
                var (w, h) = await Task.Run(() => ImageEngine.PixelSize(f));
                it.W = w; it.H = h;
                _items.Add(it); add.Add(it);
            }
            RefreshHeader();
            if (_items.Count == 0) { ShowEmpty(); return; }
            if (!_building) { _building = true; _width.Set(Math.Min(1600, _items[0].W > 0 ? _items[0].W : 1600), false); SyncHeightFromWidth(); _building = false; }
            RebuildList();
            Show(ToolState.Ready, _readyView);
            _current ??= _items[0];
            Refresh();
            _ = LoadThumbsAsync(add);
            _ = UpdatePreviewAsync();
        }

        private void AddFiles(IEnumerable<string> files)
        {
            if (State != ToolState.Ready && State != ToolState.Done && State != ToolState.Error) return;
            if (State != ToolState.Ready) Show(ToolState.Ready, _items.Count > 0 ? _readyView : _emptyView);
            LoadedTask = LoadAsync(files.Where(f => File.Exists(f) && Handles(f)));
        }

        internal void AddForTest(params string[] files) => AddFiles(files);

        private async Task LoadThumbsAsync(IEnumerable<Item> items)
        {
            foreach (var it in items.ToList())
            {
                try
                {
                    it.Thumb = await Task.Run(() =>
                    {
                        if (it.Path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) return null;
                        var bi = new BitmapImage();
                        bi.BeginInit(); bi.UriSource = new Uri(it.Path); bi.DecodePixelWidth = 80; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
                        return (BitmapSource)bi;
                    });
                }
                catch { }
            }
            RebuildList();
        }

        private void RefreshHeader()
        {
            long total = _items.Sum(i => i.Bytes);
            if (_items.Count == 0) { SetFile("nothing selected", ""); return; }
            SetFile(_items.Count == 1 ? _items[0].Name : $"{_items.Count} pictures", _items.Count == 1 ? $"{_items[0].W}×{_items[0].H} · {Ui.Bytes(total)}" : $"{_items[0].Name} and {_items.Count - 1} more · {Ui.Bytes(total)}");
        }

        // ------------------------------------------------------------ layout

        private void BuildUi()
        {
            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // left: list and preview
            var left = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
            var listScroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 188, Focusable = false };
            var listHead = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            listHead.ColumnDefinitions.Add(new ColumnDefinition()); listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            listHead.Children.Add(FT.Eyebrow("Pictures", new Thickness(0)));
            var add = Ui.Button("Add pictures", AddDialog, "Btn.Chip", Icons.Plus); add.Height = 28; add.Margin = new Thickness(0); add.Padding = new Thickness(10, 0, 12, 0);
            Grid.SetColumn(add, 1); listHead.Children.Add(add);
            left.Children.Add(listHead);
            left.Children.Add(FT.Rounded(listScroll, 16));
            _split = new SplitPreview { Margin = new Thickness(0, 12, 0, 0) };
            left.Children.Add(FT.Rounded(_split, 16));
            root.Children.Add(left);

            // right: options
            var right = new StackPanel();
            right.Children.Add(FT.Eyebrow("Size"));
            var size = new Grid();
            size.ColumnDefinitions.Add(new ColumnDefinition()); size.ColumnDefinitions.Add(new ColumnDefinition());
            _width = new FT.IntBox("Width", 1600, "px", 1, 20000, 16, v => { if (!_building) { _building = true; if (_keep.IsChecked == true) SyncHeightFromWidth(); _building = false; } Refresh(); }, SetStatus);
            _height = new FT.IntBox("Height", 1200, "px", 1, 20000, 16, v => { if (!_building) { _building = true; if (_keep.IsChecked == true) SyncWidthFromHeight(); _building = false; } Refresh(); }, SetStatus);
            var fw = FT.Field("Width", _width.Host, null, new Thickness(0, 0, 6, 8)); var fh = FT.Field("Height", _height.Host, null, new Thickness(6, 0, 0, 8));
            Grid.SetColumn(fh, 1); size.Children.Add(fw); size.Children.Add(fh);
            right.Children.Add(size);
            _keep = FT.SwitchRow("Keep proportions", "Changing one side sets the other", true, on => { if (on) { _building = true; SyncHeightFromWidth(); _building = false; } Refresh(); });
            _keep.Margin = new Thickness(0, 0, 0, 6);
            right.Children.Add(_keep);
            right.Children.Add(FT.Eyebrow("Crop", new Thickness(0, 4, 0, 6)));
            _crop = new FT.ChipGroup(new[] { "None", "1:1", "4:3", "16:9", "Free" }, 0, "Crop");
            _crop.Changed += i => { if (_keep.IsChecked == true) { _building = true; SyncHeightFromWidth(); _building = false; } Refresh(); _ = UpdatePreviewAsync(); };
            right.Children.Add(_crop);
            right.Children.Add(FT.Eyebrow("Quality", new Thickness(0, 4, 0, 6)));
            _quality = new FT.Segmented(new[] { "Quality 82", "Target size" }, 0, "Quality mode");
            _quality.Changed += i => { ShowQualityMode(); Refresh(); _ = UpdatePreviewAsync(); };
            right.Children.Add(_quality);
            _qualityVal = FT.Body("82", 12, null);
            _qualitySlider = FT.MakeSlider("Quality", 40, 100, 82, 1, v => { _quality2 = (int)v; _qualityVal.Text = ((int)v).ToString(); _quality.Select(0, false); UpdateQualityLabel(); Refresh(); QueuePreview(); });
            var qs = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var qtop = new Grid(); qtop.ColumnDefinitions.Add(new ColumnDefinition()); qtop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            qtop.Children.Add(FT.Label("JPEG quality", 13, Ui.Res("B.Text"), FontWeights.Medium, new Thickness(0, 0, 0, 2))); Grid.SetColumn(_qualityVal, 1); qtop.Children.Add(_qualityVal);
            qs.Children.Add(qtop); qs.Children.Add(_qualitySlider);
            _qualityBox = qs;
            right.Children.Add(_qualityBox);
            _target = new FT.IntBox("Aim for under", 700, "KB each", 10, 100000, 50, v => { Refresh(); QueuePreview(); }, SetStatus);
            var tb = FT.Field("Aim for under", _target.Host, null, new Thickness(0, 10, 0, 0));
            _qualityHint = FT.Body("", 12, Ui.Res("B.TextSoft"), new Thickness(0, 6, 0, 0));
            var tsp = new StackPanel(); tsp.Children.Add(tb);
            _targetBox = tsp; _targetBox.Visibility = Visibility.Collapsed;
            right.Children.Add(_targetBox);
            right.Children.Add(_qualityHint);
            Grid.SetColumn(right, 1);
            root.Children.Add(right);
            _readyView = root;

            // empty view
            var empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 24) };
            var tile = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Sunken"), Child = Ui.Icon(Icons.Image, Ui.Res("B.TextSoft"), 24, 1.8), HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(tile);
            empty.Children.Add(new TextBlock { Text = "No pictures to work on", FontSize = 16, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 6) });
            var hint = FT.Body("Copy pictures (JPG, PNG, WebP, BMP, GIF, TIFF, HEIC) and open this tool from the notice, or drop them here.", 13); hint.TextAlignment = TextAlignment.Center; hint.MaxWidth = 520; hint.Margin = new Thickness(0, 0, 0, 14);
            empty.Children.Add(hint);
            var addBtn = FT.Tonal("Add pictures", AddDialog, Icons.Plus); addBtn.HorizontalAlignment = HorizontalAlignment.Center; addBtn.Margin = new Thickness(0);
            empty.Children.Add(addBtn);
            _emptyView = empty;

            SetLeftActions(FT.Tonal("Save in…", ChooseFolder, Icons.Folder));
            Show(ToolState.Ready, root);
            ShowQualityMode();
            UpdateQualityLabel();
        }

        private void AddDialog()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Choose pictures", Filter = "Pictures|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif;*.tif;*.tiff;*.heic|All files|*.*" };
            if (dlg.ShowDialog(this) == true) AddFiles(dlg.FileNames);
        }

        private void ChooseFolder()
        {
            var f = FolderChoice(this, "Choose where the copies go");
            if (f == null) return;
            _folder = f;
            SetLeftActions(FT.Tonal("Save in " + PathDisplay.Shown(f), ChooseFolder, Icons.Folder));
        }

        private void ShowEmpty()
        {
            Show(ToolState.Ready, _emptyView, "Add pictures to start.");
            SetPrimary(false);
            SetLeftActions();
        }

        private void ShowQualityMode()
        {
            bool target = _quality.Index == 1;
            _qualityBox.Visibility = target ? Visibility.Collapsed : Visibility.Visible;
            _targetBox.Visibility = target ? Visibility.Visible : Visibility.Collapsed;
            UpdateQualityLabel();
        }

        private void UpdateQualityLabel()
        {
            if (_quality == null) return;
            _qualityHint.Text = _quality.Index == 1 ? "Quality is lowered per picture until it fits. Only JPEG and WebP pictures can be compressed this way." : "Quality applies to JPEG and WebP pictures. PNG, BMP and GIF keep every pixel.";
        }

        internal void SetQualityMode(bool target) => _quality.Select(target ? 1 : 0, true);
        internal void SetQuality(int q) => _qualitySlider.Value = q;
        internal void SetTargetKb(int kb) => _target.Set(kb, true);
        internal void SetCrop(int i) => _crop.Select(i, true);
        internal void SetWidthText(string w) => _width.Type(w);
        internal void SetHeightText(string h) => _height.Type(h);
        internal void SetKeep(bool on) => _keep.IsChecked = on;
        internal void SetFolder(string f) { _folder = f; }

        // ------------------------------------------------------------ list and numbers

        private void SyncHeightFromWidth()
        {
            var it = _current ?? _items.FirstOrDefault(); if (it == null || it.W <= 0) return;
            var r = ImageEngine.CropRect(it.W, it.H, new[] { "none", "1:1", "4:3", "16:9", "free" }[_crop?.Index ?? 0], 0);
            _height.Set(Math.Max(1, (int)Math.Round(_width.Value * (double)r.Height / r.Width)), false);
        }

        private void SyncWidthFromHeight()
        {
            var it = _current ?? _items.FirstOrDefault(); if (it == null || it.W <= 0) return;
            var r = ImageEngine.CropRect(it.W, it.H, new[] { "none", "1:1", "4:3", "16:9", "free" }[_crop?.Index ?? 0], 0);
            _width.Set(Math.Max(1, (int)Math.Round(_height.Value * (double)r.Width / r.Height)), false);
        }

        private void RebuildList()
        {
            _list.Children.Clear();
            foreach (var it in _items) _list.Children.Add(Row(it));
            Refresh();
        }

        private FrameworkElement Row(Item it)
        {
            var g = new Grid { Margin = new Thickness(0), Background = it == _current ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent, Cursor = Cursors.Hand };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            it.Check = FT.Check(it.Selected, "Include " + it.Name, on => { it.Selected = on; Refresh(); });
            it.Check.Margin = new Thickness(10, 0, 8, 0);
            var thumb = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(8), Background = Ui.Res("B.Sunken"), Margin = new Thickness(0, 4, 10, 4), Child = it.Thumb != null ? (UIElement)new Image { Source = it.Thumb, Stretch = Stretch.UniformToFill } : Ui.Icon(Icons.Image, Ui.Res("B.TextMute"), 18, 1.6), ClipToBounds = true };
            var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Text = it.Name, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = it.Path };
            Core.Language.SetLiteral(name, true);
            it.Dims = new TextBlock { Text = $"{it.W}×{it.H} · {Ui.Bytes(it.Bytes)}", FontSize = 12, Foreground = it == _current ? Ui.Res("B.OnSecondaryContainer") : Ui.Res("B.TextSoft") };
            names.Children.Add(name); names.Children.Add(it.Dims);
            it.Result = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
            Grid.SetColumn(it.Check, 0); Grid.SetColumn(thumb, 1); Grid.SetColumn(names, 2); Grid.SetColumn(it.Result, 3);
            g.Children.Add(it.Check); g.Children.Add(thumb); g.Children.Add(names); g.Children.Add(it.Result);
            g.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource is CheckBox || (e.OriginalSource as DependencyObject)?.GetType().Name == "Border" && false) return; _current = it; if (_keep.IsChecked == true) { _building = true; SyncHeightFromWidth(); _building = false; } RebuildList(); _ = UpdatePreviewAsync(); };
            return g;
        }

        private void Refresh()
        {
            if (_items == null || _width == null) return;
            var o = Options;
            var sel = _items.Where(i => i.Selected).ToList();
            long before = sel.Sum(i => i.Bytes), after = 0;
            foreach (var it in _items)
            {
                it.OutBytes = it.Status == Pic.Done ? it.OutBytes : ImageEngine.Estimate(it.Bytes, it.W, it.H, o, System.IO.Path.GetExtension(it.Path));
                if (it.Result != null && it.Status == Pic.Pending) it.Result.Text = it.W > 0 ? "→ ~" + Ui.Bytes(it.OutBytes) : "";
            }
            foreach (var it in sel) after += it.OutBytes;
            if (_quality != null) { }
            SetStatus(sel.Count == 0 ? "No pictures are ticked." : $"{sel.Count} {(sel.Count == 1 ? "picture" : "pictures")} · {Ui.Bytes(before)} → about {Ui.Bytes(after)}");
            SetPrimary(sel.Count > 0, sel.Count == 1 ? "Save 1 copy" : $"Save {sel.Count} copies");
            if (State == ToolState.Ready && _items.Count > 0) { /* label set above */ }
        }

        // ------------------------------------------------------------ preview

        private System.Windows.Threading.DispatcherTimer _previewTimer;
        private void QueuePreview()
        {
            _previewTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _previewTimer.Stop(); _previewTimer.Tick -= OnPreviewTimer; _previewTimer.Tick += OnPreviewTimer; _previewTimer.Start();
        }
        private void OnPreviewTimer(object s, EventArgs e) { _previewTimer.Stop(); _ = UpdatePreviewAsync(); }

        private async Task UpdatePreviewAsync()
        {
            var it = _current; if (it == null) return;
            try { _previewCts.Cancel(); } catch { }
            _previewCts = new CancellationTokenSource();
            var ct = _previewCts.Token;
            try
            {
                if (!_smallCache.TryGetValue(it.Path, out var small))
                {
                    small = await ImageEngine.LoadAsync(it.Path, ct, 900);
                    _smallCache[it.Path] = small;
                }
                if (ct.IsCancellationRequested) return;
                var o = Options;
                bool lossy = new[] { ".jpg", ".jpeg", ".webp", ".heic" }.Contains(System.IO.Path.GetExtension(it.Path).ToLowerInvariant());
                var after = await Task.Run(() => ImageEngine.Simulate(small, o, lossy), ct);
                if (ct.IsCancellationRequested) return;
                long est = ImageEngine.Estimate(it.Bytes, it.W, it.H, o, System.IO.Path.GetExtension(it.Path));
                _split.Set(small, after, "Before · " + Ui.Bytes(it.Bytes), "After · ~" + Ui.Bytes(est));
            }
            catch (OperationCanceledException) { }
            catch { _split.Set(null, null, "", ""); }
        }

        // ------------------------------------------------------------ running

        protected override void PrimaryPressed() => Run(GoAsync);

        internal Task GoAsync() => GoAsync(_items.Where(i => i.Selected).ToList());

        private async Task GoAsync(List<Item> todo)
        {
            if (State != ToolState.Ready && State != ToolState.Error && State != ToolState.Done) return;
            if (todo.Count == 0) return;
            var o = Options;
            NewRun(); var ct = Cts.Token;
            var sp = new StackPanel();
            var pb = new FT.ProgressBlock("Progress");
            sp.Children.Add(pb);
            var rows = new StackPanel();
            sp.Children.Add(FT.Card(rows));
            foreach (var it in todo) { it.Status = Pic.Pending; it.Error = null; }
            Show(ToolState.Working, sp, "Cancel keeps the pictures already saved.");
            SetSummary("working");
            var rowTexts = new Dictionary<Item, TextBlock>();
            void Paint()
            {
                rows.Children.Clear(); rowTexts.Clear();
                foreach (var it in todo)
                {
                    var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    string icon = it.Status == Pic.Done ? Icons.Check : it.Status == Pic.Failed ? MediaIcons.Warn : it.Status == Pic.Working ? MediaIcons.Refresh : Icons.Clock;
                    var ic = Ui.Icon(icon, it.Status == Pic.Failed ? Ui.Res("B.Amber") : Ui.Res("B.TextSoft"), 15, 1.8); ic.Margin = new Thickness(2, 0, 12, 0);
                    var n = new TextBlock { Text = it.Name, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text") }; Core.Language.SetLiteral(n, true);
                    var r = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextSoft"), Text = it.Status == Pic.Done ? Ui.Bytes(it.OutBytes) : it.Status == Pic.Working ? "working…" : it.Status == Pic.Failed ? "Not saved" : "waiting" };
                    Grid.SetColumn(n, 1); Grid.SetColumn(r, 2);
                    g.Children.Add(ic); g.Children.Add(n); g.Children.Add(r); rows.Children.Add(g);
                }
            }
            Paint();
            var results = new List<ImageResult>();
            for (int i = 0; i < todo.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var it = todo[i];
                it.Status = Pic.Working; Paint();
                pb.Set((double)i / todo.Count, $"Picture {i + 1} of {todo.Count}", $"{it.Name} · " + (o.UseTarget ? $"finding the quality that fits {Ui.Bytes(o.TargetBytes)}" : "resizing and saving"));
                var dest = ImageEngine.OutputPath(it.Path, _folder);
                var r = await ImageEngine.ProcessAsync(it.Path, dest, o, ct);
                results.Add(r);
                if (r.Error == "Cancelled") { it.Status = Pic.Pending; break; }
                it.Status = r.Ok ? Pic.Done : Pic.Failed; it.Error = r.Error; it.Output = r.Ok ? dest : null; it.OutBytes = r.OutputBytes;
                Paint();
            }
            LastResults = results;
            if (ct.IsCancellationRequested)
            {
                int saved = results.Count(x => x.Ok);
                Show(ToolState.Ready, _readyView);
                RebuildList(); Refresh();
                SetStatus($"Cancelled after {saved} of {todo.Count}. The pictures already saved were kept.");
                return;
            }
            ShowResults(todo, results);
        }

        private void ShowResults(List<Item> todo, List<ImageResult> results)
        {
            int ok = results.Count(r => r.Ok), failed = results.Count - ok;
            long srcBytes = results.Where(r => r.Ok).Sum(r => r.SourceBytes), outBytes = results.Where(r => r.Ok).Sum(r => r.OutputBytes);
            int pct = srcBytes > 0 ? (int)Math.Round(100.0 * (srcBytes - outBytes) / srcBytes) : 0;
            var sp = new StackPanel();
            if (failed == 0)
                sp.Children.Add(FT.Banner(BannerKind.Success, ok == 1 ? "1 copy saved" : $"{ok} copies saved", $"{Ui.Bytes(srcBytes)} became {Ui.Bytes(outBytes)}, {Math.Max(0, pct)}% smaller. The originals were not changed."));
            else
                sp.Children.Add(FT.Banner(BannerKind.Warning, $"{ok} of {results.Count} saved", $"{Ui.Bytes(srcBytes)} became {Ui.Bytes(outBytes)}, {Math.Max(0, pct)}% smaller. {(failed == 1 ? "One picture" : failed + " pictures")} could not be processed."));
            var list = new StackPanel();
            foreach (var r in results)
            {
                var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var ic = Ui.Icon(r.Ok ? Icons.Check : MediaIcons.Warn, r.Ok ? Ui.Res("B.TextSoft") : Ui.Res("B.Amber"), 15, 1.8); ic.Margin = new Thickness(2, 0, 12, 0);
                var n = new TextBlock { Text = System.IO.Path.GetFileName(r.Source), FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis }; Core.Language.SetLiteral(n, true);
                var t = new TextBlock { FontSize = 12, Foreground = r.Ok ? Ui.Res("B.TextSoft") : Ui.Res("B.Amber"), TextWrapping = TextWrapping.Wrap, MaxWidth = 380 };
                if (r.Ok) t.Text = Ui.Bytes(r.OutputBytes) + (r.TargetMissed ? " (target not reached)" : "");
                else { t.Inlines.Add(new System.Windows.Documents.Run("Not saved: ")); var reason = new System.Windows.Documents.Run(Short(r.Error)); Core.Language.SetLiteral(reason, true); t.Inlines.Add(reason); }
                Grid.SetColumn(n, 1); Grid.SetColumn(t, 2);
                g.Children.Add(ic); g.Children.Add(n); g.Children.Add(t); list.Children.Add(g);
            }
            sp.Children.Add(FT.Card(list));
            var row = new WrapPanel();
            var first = results.FirstOrDefault(r => r.Ok);
            if (first != null) row.Children.Add(FT.Tonal("Show in folder", () => Reveal(first.Output), Icons.Folder));
            if (failed > 0) row.Children.Add(FT.Outline(failed == 1 ? "Retry the failed one" : "Retry the failed ones", () => Run(() => GoAsync(todo.Where(i => i.Status == Pic.Failed).ToList()))));
            sp.Children.Add(row);
            SetSummary(failed == 0 ? "done" : $"{ok} saved, {failed} failed");
            Show(ToolState.Done, sp, failed == 0 ? $"{ok} {(ok == 1 ? "copy" : "copies")} saved." : $"{ok} of {results.Count} saved.");
        }

        private static string Short(string e)
        {
            if (string.IsNullOrWhiteSpace(e)) return "unknown error";
            e = e.Replace("unsupported compression or format: ", "unsupported compression: ").Trim();
            return e.Length > 90 ? e.Substring(0, 90) + "…" : e;
        }
    }
}
