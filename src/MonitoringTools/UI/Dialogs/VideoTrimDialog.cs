using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Threading;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>Trim and cut video: keep the part between two handles or cut that part out. A cut copies the video as it is (no
    /// re-encode, no quality loss) when its points sit on key frames; otherwise it says so and re-encodes only as needed.</summary>
    public sealed class VideoTrimDialog : MediaToolDialog
    {
        public static readonly string[] Extensions = { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v" };
        public static bool Handles(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        private readonly string _src;
        private readonly MediaInfo _info;
        private List<double> _keys;
        private Task _keysTask = Task.CompletedTask;
        private RangeTimeline _timeline;
        private TextBox _startBox, _endBox, _lenBox, _nameBox;
        private FT.Segmented _mode;
        private CheckBox _exactSwitch;
        private StackPanel _bannerHost;
        private Image _preview;
        private TextBlock _previewTime;
        private Button _playBtn;
        private MediaElement _player;
        private DispatcherTimer _tick;
        private bool _playing;
        private CancellationTokenSource _frameCts = new CancellationTokenSource();
        private string _folder;
        private bool _exactChoice;
        private bool _forceExact;
        private string _lastOutput;
        private TrimPlan _lastPlan;

        internal TrimPlan Plan => new TrimPlan { Start = _timeline?.Start ?? 0, End = _timeline?.End ?? _info.Duration, Remove = _mode?.Index == 1, Exact = EffectiveExact };
        private bool EffectiveExact => _exactChoice || _forceExact;
        internal bool IsLossless => !EffectiveExact;
        internal string OutputName => _nameBox?.Text;
        internal string LastOutput => _lastOutput;
        internal MediaInfo Info => _info;
        internal RangeTimeline Timeline => _timeline;
        internal IReadOnlyList<double> Keyframes => _keys;

        public static VideoTrimDialog OpenFor(string path)
        {
            if (!File.Exists(path)) { App.Toasts.ShowNotice("Nothing to trim", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var ex = Existing<VideoTrimDialog>(d => string.Equals(d._src, path, StringComparison.OrdinalIgnoreCase));
            if (ex != null) { ex.Activate(); return ex; }
            var d = new VideoTrimDialog(path);
            d.Show(); d.Activate();
            return d;
        }

        private VideoTrimDialog(string path) : base("Trim and cut video", MediaIcons.Scissors, 760, "Trim", MediaIcons.Scissors)
        {
            _src = path;
            _info = MediaProc.Probe(path);
            if (!_info.Ok || !_info.HasVideo)
            {
                SetFile(Path.GetFileName(path), "could not trim");
                Show(ToolState.Error, ErrorView("This video could not be read", _info.Error == null ? "There is no video in this file." : "FFmpeg reported: " + MediaProc.Plain(_info.Error), $"Nothing was written and {Path.GetFileName(path)} is untouched.", null, null, _info.Error), "Nothing was saved.");
                return;
            }
            SetFile(Path.GetFileName(path), $"{MediaTime.Format(_info.Duration, 0)} · {_info.Width}×{_info.Height} · {Ui.Bytes(_info.Bytes)}");
            Build();
            _keysTask = ScanKeyframesAsync();
            _ = LoadThumbsAsync();
            _ = ShowFrameAsync(0);
            PreviewKeyDown += OnKeys;
        }

        protected override void OnClosed2() { try { _frameCts.Cancel(); } catch { } StopPlayback(); }

        // ------------------------------------------------------------ building the Ready view

        private void Build()
        {
            double fps = _info.Fps > 0 ? _info.Fps : 25;
            _timeline = new RangeTimeline(_info.Duration, false) { FrameStep = 1 / fps, Margin = new Thickness(0, 6, 0, 8) };
            _timeline.SetRange(Math.Min(_info.Duration * 0.1, Math.Max(0, _info.Duration - 1)), _info.Duration * 0.9);
            double lastStart = _timeline.Start, lastEnd = _timeline.End;
            _timeline.RangeChanged += (a, b, user) =>
            {
                bool endMoved = Math.Abs(b - lastEnd) > 1e-6 && Math.Abs(a - lastStart) < 1e-6;
                lastStart = a; lastEnd = b;
                SyncBoxes(); Update();
                if (user) _ = ShowFrameAsync(endMoved ? Math.Max(0, b - 0.05) : a);
            };
            _timeline.PlayheadChanged += (t, user) => { if (user) { StopPlayback(); _ = ShowFrameAsync(t); } };

            var root = new StackPanel();

            // preview + mode
            var top = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });
            top.ColumnDefinitions.Add(new ColumnDefinition());
            _preview = new Image { Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapScalingMode(_preview, BitmapScalingMode.HighQuality);
            _player = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed, IsMuted = false };
            _player.MediaFailed += (s, e) => { StopPlayback(); SetStatus("This format cannot be played in the preview. The trim still works."); };
            var pg = new Grid { Height = 132 };
            pg.Children.Add(_preview); pg.Children.Add(_player);
            _playBtn = FT.OverlayButton(Icons.Play, "Play", TogglePlay);
            pg.Children.Add(_playBtn);
            _previewTime = new TextBlock { Text = "0:00.000", FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Brushes.White };
            Core.Language.SetLiteral(_previewTime, true);
            pg.Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(190, 8, 9, 12)), Padding = new Thickness(7, 2, 7, 3), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Child = _previewTime });
            var previewBox = FT.Rounded(pg, 16);
            top.Children.Add(previewBox);

            var right = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
            right.Children.Add(FT.Eyebrow("What to do with the selection"));
            _mode = new FT.Segmented(new[] { "Keep selected", "Remove selected" }, 0, "What to do with the selection");
            _mode.Changed += i => { _timeline.DimInside = i == 1; Update(); _timeline.InvalidateVisual(); };
            right.Children.Add(_mode);
            right.Children.Add(FT.Body("Keep: the clip between the two handles becomes a new file. Remove: the section between them is cut out and the rest is joined.", 12, null, new Thickness(0, 10, 0, 0)));
            Grid.SetColumn(right, 1);
            top.Children.Add(right);
            root.Children.Add(top);
            root.Children.Add(_timeline);

            // times
            var times = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            for (int i = 0; i < 3; i++) times.ColumnDefinitions.Add(new ColumnDefinition());
            _startBox = FT.TextField("Start", "", s => CommitTime(s, true));
            _endBox = FT.TextField("End", "", s => CommitTime(s, false));
            _lenBox = FT.TextField("New length", "", null); _lenBox.IsReadOnly = true; _lenBox.Opacity = 0.7; _lenBox.IsTabStop = false;
            var f1 = FT.Field("Start", _startBox, null, new Thickness(0, 0, 8, 0)); var f2 = FT.Field("End", _endBox, null, new Thickness(4, 0, 4, 0)); var f3 = FT.Field("New length", _lenBox, null, new Thickness(8, 0, 0, 0));
            Grid.SetColumn(f2, 1); Grid.SetColumn(f3, 2);
            times.Children.Add(f1); times.Children.Add(f2); times.Children.Add(f3);
            root.Children.Add(times);

            _bannerHost = new StackPanel();
            root.Children.Add(_bannerHost);

            // save as + exact
            var save = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            save.ColumnDefinitions.Add(new ColumnDefinition());
            save.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            _nameBox = FT.TextField("Save as", Path.GetFileName(DefaultOutput()), null, false);
            var nameField = FT.Field("Save as", FT.FileNameField(_nameBox), "Saved next to the original. A name that exists gets a number added; nothing is overwritten.", new Thickness(0, 0, 16, 0));
            _exactSwitch = FT.SwitchRow("Exact cut instead", "Re-encodes so the cut lands on the exact frame (slower)", false, on => { _exactChoice = on; Update(); });
            _exactSwitch.Margin = new Thickness(0, 18, 0, 0);
            Grid.SetColumn(_exactSwitch, 1);
            save.Children.Add(nameField); save.Children.Add(_exactSwitch);
            root.Children.Add(save);

            SetLeftActions(FT.Tonal("Choose folder", ChooseFolder, Icons.Folder));
            _readyView = root;
            Show(ToolState.Ready, root);
            SyncBoxes(); Update();
        }

        private FrameworkElement _readyView;

        /// <summary>Back to the controls with the handles, mode and name as they were.</summary>
        private void BackToReady(bool newName)
        {
            if (_readyView == null) { Build(); return; }
            if (newName) { _nameBox.Text = Path.GetFileName(DefaultOutput()); _nameBox.Tag = _nameBox.Text; }
            SetSummary($"{MediaTime.Format(_info.Duration, 0)} · {_info.Width}×{_info.Height} · {Ui.Bytes(_info.Bytes)}");
            SetPrimary(true);
            Show(ToolState.Ready, _readyView);
            SyncBoxes(); Update();
        }

        private string DefaultOutput() => MediaProc.UniquePath(_folder ?? Path.GetDirectoryName(_src), Path.GetFileNameWithoutExtension(_src) + "-trim", Path.GetExtension(_src), _src);

        private void ChooseFolder()
        {
            var f = FolderChoice(this, "Choose where the trimmed video goes");
            if (f == null) return;
            _folder = f;
            _nameBox.Text = Path.GetFileName(DefaultOutput()); _nameBox.Tag = _nameBox.Text;
            SetLeftActions(FT.Tonal("Save in " + PathDisplay.Shown(f), ChooseFolder, Icons.Folder));
            Update();
        }

        // ------------------------------------------------------------ keyframes, thumbnails, preview frames

        private async Task ScanKeyframesAsync()
        {
            SetBanner(BannerKind.Info, "Checking where the key frames are…", "A lossless cut needs its points on key frames. This takes a moment.");
            try { _keys = await VideoTrimEngine.KeyframesAsync(_src, Cts.Token); }
            catch { _keys = new List<double>(); }
            Update();
        }

        private async Task LoadThumbsAsync()
        {
            try
            {
                var strip = await VideoTrimEngine.ThumbnailsAsync(_src, _info.Duration, 16, 56, Cts.Token);
                if (strip.Count > 0) _timeline.SetStrip(strip); else _timeline.Failed();
            }
            catch { _timeline?.Failed(); }
        }

        private async Task ShowFrameAsync(double t)
        {
            try { _frameCts.Cancel(); } catch { }
            _frameCts = new CancellationTokenSource();
            var ct = _frameCts.Token;
            _previewTime.Text = MediaTime.Format(t);
            try
            {
                await Task.Delay(90, ct);
                var img = await MediaProc.FrameAtAsync(_src, t, 480, ct);
                if (img != null && !ct.IsCancellationRequested) _preview.Source = img;
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        // ------------------------------------------------------------ playing the preview

        private void TogglePlay()
        {
            if (_playing) { StopPlayback(); return; }
            try
            {
                _player.Source = new Uri(_src);
                _player.Visibility = Visibility.Visible;
                _player.Position = TimeSpan.FromSeconds(_timeline.Playhead >= _timeline.End - 0.1 ? _timeline.Start : _timeline.Playhead);
                _player.Play();
                _playing = true;
                _playBtn.Content = Ui.Icon(Icons.Pause, Brushes.White, 20, 2);
                AutomationProperties.SetName(_playBtn, "Pause");
                _tick ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                _tick.Tick -= OnTick; _tick.Tick += OnTick; _tick.Start();
            }
            catch { StopPlayback(); }
        }

        private void OnTick(object s, EventArgs e)
        {
            if (!_playing) return;
            double t = _player.Position.TotalSeconds;
            _timeline.SetPlayhead(t, false);
            _previewTime.Text = MediaTime.Format(t);
            if (t >= _timeline.End) StopPlayback();
        }

        private void StopPlayback()
        {
            if (!_playing && _player?.Visibility != Visibility.Visible) return;
            _playing = false;
            try { _tick?.Stop(); _player?.Pause(); _player?.Close(); } catch { }
            if (_player != null) _player.Visibility = Visibility.Collapsed;
            if (_playBtn != null) { _playBtn.Content = Ui.Icon(Icons.Play, Brushes.White, 20, 2); AutomationProperties.SetName(_playBtn, "Play"); }
        }

        private void OnKeys(object s, System.Windows.Input.KeyEventArgs e)
        {
            if (State != ToolState.Ready || Keyboard.FocusedElement is TextBox) return;
            if (e.Key == System.Windows.Input.Key.I) { e.Handled = true; SetStartAtPlayhead(); }
            else if (e.Key == System.Windows.Input.Key.O) { e.Handled = true; SetEndAtPlayhead(); }
            else if (e.Key == System.Windows.Input.Key.Space && !(Keyboard.FocusedElement is Button) && !(Keyboard.FocusedElement is CheckBox)) { e.Handled = true; TogglePlay(); }
        }

        internal void SetStartAtPlayhead() { _timeline.SetRange(Math.Min(_timeline.Playhead, _timeline.End - _timeline.MinGap), _timeline.End); SyncBoxes(); Update(); }
        internal void SetEndAtPlayhead() { _timeline.SetRange(_timeline.Start, Math.Max(_timeline.Playhead, _timeline.Start + _timeline.MinGap)); SyncBoxes(); Update(); }

        // ------------------------------------------------------------ the numbers

        private void CommitTime(string text, bool isStart)
        {
            if (!MediaTime.TryParse(text, out var t)) { SetStatus("Type a time like 0:12.5 or 1:02:03."); SyncBoxes(); return; }
            t = Math.Clamp(t, 0, _info.Duration);
            if (isStart) _timeline.SetRange(Math.Min(t, _timeline.End - _timeline.MinGap), _timeline.End);
            else _timeline.SetRange(_timeline.Start, Math.Max(t, _timeline.Start + _timeline.MinGap));
            SyncBoxes(); Update();
        }

        /// <summary>Types times as a person would: for tests and for the keyboard route.</summary>
        internal void SetTimes(double start, double end)
        {
            _timeline.SetRange(start, end);
            SyncBoxes(); Update();
        }

        internal void SetStartText(string s) { _startBox.Text = s; CommitTime(s, true); }
        internal void SetEndText(string s) { _endBox.Text = s; CommitTime(s, false); }
        internal void SetRemoveMode(bool remove) { _mode.Select(remove ? 1 : 0, true); }
        internal void SetExact(bool on) { _exactSwitch.IsChecked = on; }
        internal void SetOutputName(string n) { _nameBox.Text = n; _nameBox.Tag = n; }
        internal void SetOutputFolder(string f) { _folder = f; }
        internal Task KeysReady => _keysTask;

        private void SyncBoxes()
        {
            if (_startBox == null) return;
            _startBox.Text = MediaTime.Format(_timeline.Start); _startBox.Tag = _startBox.Text;
            _endBox.Text = MediaTime.Format(_timeline.End); _endBox.Tag = _endBox.Text;
            var kept = new TrimPlan { Start = _timeline.Start, End = _timeline.End, Remove = _mode?.Index == 1 }.Kept(_info.Duration);
            _lenBox.Text = MediaTime.Format(kept);
        }

        private void SetBanner(BannerKind kind, string title, string text)
        {
            if (_bannerHost == null) return;
            _bannerHost.Children.Clear();
            _bannerHost.Children.Add(FT.Banner(kind, title, text));
        }

        private void Update()
        {
            if (_timeline == null) return;
            var plan = new TrimPlan { Start = _timeline.Start, End = _timeline.End, Remove = _mode.Index == 1 };
            double kept = plan.Kept(_info.Duration);
            bool scanned = _keysTask.IsCompleted && _keys != null;
            bool lossless = scanned && VideoTrimEngine.CanBeLossless(_keys, _info.Duration, plan);
            _forceExact = scanned && !lossless;
            if (!scanned) { /* the banner already says the key frames are being checked */ }
            else if (_exactChoice) SetBanner(BannerKind.Info, "Exact cut: the video is re-encoded", "The cut lands on the exact frame. It is slower and costs a tiny amount of quality across the whole clip.");
            else if (lossless) SetBanner(BannerKind.Success, "Lossless: no re-encode", "Both points sit on keyframes, so the cut copies the video as it is. It is instant and loses no quality.");
            else SetBanner(BannerKind.Warning, "Exact cut needed", "The start is between keyframes, so the first seconds are re-encoded. Slower, with a tiny quality cost at the cut.");
            long est = _info.Duration > 0 ? (long)(_info.Bytes * kept / _info.Duration) : 0;
            SetStatus($"{MediaTime.Format(kept)} kept · about {Ui.Bytes(est)}");
            SetPrimary(kept >= 0.05);
        }

        // ------------------------------------------------------------ running

        protected override void PrimaryPressed() => Run(GoAsync);

        internal async Task GoAsync()
        {
            if (State != ToolState.Ready) return;
            try { await _keysTask; } catch { }
            var plan = Plan; _lastPlan = plan;
            var name = (_nameBox.Text ?? "").Trim();
            if (name.Length == 0) name = Path.GetFileName(DefaultOutput());
            if (string.IsNullOrEmpty(Path.GetExtension(name))) name += Path.GetExtension(_src);
            name = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var dir = _folder ?? Path.GetDirectoryName(_src);
            var dest = MediaProc.UniquePath(dir, Path.GetFileNameWithoutExtension(name), Path.GetExtension(name), _src);
            double kept = plan.Kept(_info.Duration);
            long est = _info.Duration > 0 ? (long)(_info.Bytes * kept / _info.Duration) : 0;
            NewRun(); var ct = Cts.Token;
            var pb = WorkingView("Trim progress", "Trim", "Cancel stops now and deletes the unfinished .part file.", out var report);
            SetSummary($"keeping {MediaTime.Format(plan.Start)} to {MediaTime.Format(plan.End)}".Replace("keeping", plan.Remove ? "removing" : "keeping"));
            var sw = Stopwatch.StartNew();
            var part = dest + ".part";
            var result = await VideoTrimEngine.RunAsync(_src, dest, plan, _info, (v, msg) =>
            {
                long done = 0; try { if (File.Exists(part)) done = new FileInfo(part).Length; } catch { }
                double mbs = sw.Elapsed.TotalSeconds > 0.5 ? done / 1048576.0 / sw.Elapsed.TotalSeconds : 0;
                report(v, msg, $"{Ui.Bytes(done)} of about {Ui.Bytes(est)} · {mbs:0} MB/s · writing {Path.GetFileName(part)}");
            }, ct);
            if (result.Cancelled)
            {
                BackToReady(false);
                SetStatus("Cancelled. Nothing was saved and the unfinished file was deleted.");
                return;
            }
            if (!result.Ok) { ShowError(result, plan); return; }
            _lastOutput = dest;
            ShowDone(dest, plan, result);
        }

        private void ShowDone(string dest, TrimPlan plan, TrimResult result)
        {
            long outBytes = new FileInfo(dest).Length;
            var outInfo = MediaProc.Probe(dest);
            var sp = new StackPanel();
            sp.Children.Add(FT.Banner(BannerKind.Success, "Saved " + Path.GetFileName(dest), "The original was not changed."));
            sp.Children.Add(FT.FactsCard(new[]
            {
                ("Length", MediaTime.Format(outInfo.Ok ? outInfo.Duration : plan.Kept(_info.Duration))),
                ("Size", $"{Ui.Bytes(outBytes)} (original {Ui.Bytes(_info.Bytes)})"),
                ("Method", result.Method),
            }));
            var row = new WrapPanel();
            row.Children.Add(FT.Tonal("Show in folder", () => Reveal(dest), Icons.Folder));
            row.Children.Add(FT.Tonal("Play", () => Play(dest), Icons.Play));
            row.Children.Add(FT.Outline("Trim another section", () => BackToReady(true)));
            sp.Children.Add(row);
            SetSummary("done");
            Show(ToolState.Done, sp, $"{Path.GetFileName(dest)} saved.");
        }

        private void ShowError(TrimResult result, TrimPlan plan)
        {
            string title = result.StoppedAt > 0 ? "The trim stopped at " + MediaTime.Format(result.StoppedAt, 1) : "The trim could not be finished";
            string tip = result.StoppedAt > 0 ? $" Try an end point before {MediaTime.Format(Math.Max(0, result.StoppedAt - 1), 0)} or turn on Exact cut." : " Try turning on Exact cut.";
            var sp = ErrorView(title, "FFmpeg reported: " + MediaProc.Plain(result.Error), $"No output file was kept. {Path.GetFileName(_src)} is untouched." + tip,
                plan.Exact ? (Action)null : () => { BackToReady(false); _exactSwitch.IsChecked = true; Run(GoAsync); },
                "Try again with exact cut", result.Error);
            SetSummary("could not trim");
            Show(ToolState.Error, sp, "Nothing was saved.");
        }
    }
}
