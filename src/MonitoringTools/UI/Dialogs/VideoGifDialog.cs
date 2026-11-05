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
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>Video to GIF or short clip: pick a range, a format, a width and a frame rate. A GIF is made in two passes with a palette
    /// built for this clip. With a target size the width and frame rate step down together until the result fits, and when even the
    /// smallest step does not fit nothing is saved and the tried sizes are shown.</summary>
    public sealed class VideoGifDialog : FileToolDialog
    {
        private readonly string _src;
        private readonly MediaInfo _info;
        private RangeTimeline _timeline;
        private TextBox _fromBox, _toBox, _nameBox, _resultBox;
        private FT.IntBox _width, _fps, _limit;
        private FT.Segmented _format;
        private CheckBox _loop, _targetSwitch;
        private FrameworkElement _limitField;
        private TextBlock _selection, _previewNote;
        private Image _preview;
        private MediaElement _player;
        private Button _playBtn;
        private DispatcherTimer _tick;
        private bool _playing;
        private CancellationTokenSource _frameCts = new CancellationTokenSource(), _estCts = new CancellationTokenSource();
        private FrameworkElement _readyView;
        private string _folder;
        private long _estimate = -1;
        private bool _nameTouched;

        internal GifOptions Options => new GifOptions
        {
            Format = (ClipFormat)(_format?.Index ?? 0),
            From = _timeline?.Start ?? 0, To = _timeline?.End ?? _info.Duration,
            Width = _width?.Value ?? 480, Fps = _fps?.Value ?? 15, Loop = _loop?.IsChecked == true,
            TargetBytes = _targetSwitch?.IsChecked == true ? _limit.Value * 1024L * 1024 : 0
        };
        internal string LastOutput { get; private set; }
        internal GifResult LastResult { get; private set; }
        internal MediaInfo Info => _info;

        public static VideoGifDialog OpenFor(string path)
        {
            if (!File.Exists(path)) { App.Toasts.ShowNotice("Nothing to convert", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var ex = Existing<VideoGifDialog>(d => string.Equals(d._src, path, StringComparison.OrdinalIgnoreCase));
            if (ex != null) { ex.Activate(); return ex; }
            var d = new VideoGifDialog(path);
            d.Show(); d.Activate();
            return d;
        }

        private VideoGifDialog(string path) : base("Video to GIF or short clip", Icons.Image, 780, "Make GIF", Icons.Bolt)
        {
            _src = path;
            _info = MediaProc.Probe(path);
            if (!_info.Ok || !_info.HasVideo)
            {
                SetFile(Path.GetFileName(path), "could not read");
                Show(ToolState.Error, ErrorView("This video could not be read", MediaProc.Plain(_info.Error ?? "There is no video in this file."), $"Nothing was written and {Path.GetFileName(path)} is untouched.", null, null, _info.Error), "Nothing was saved.");
                return;
            }
            SetFile(Path.GetFileName(path), $"{MediaTime.Format(_info.Duration, 0)} · {_info.Width}×{_info.Height} · {Ui.Bytes(_info.Bytes)}");
            Build();
            _ = LoadThumbsAsync();
            _ = ShowFrameAsync(_timeline.Start);
            _ = EstimateLaterAsync();
            PreviewKeyDown += (s, e) =>
            {
                if (State == ToolState.Ready && e.Key == Key.Space && !(Keyboard.FocusedElement is TextBox) && !(Keyboard.FocusedElement is Button) && !(Keyboard.FocusedElement is CheckBox)) { e.Handled = true; TogglePlay(); }
            };
        }

        protected override void OnClosed2() { try { _frameCts.Cancel(); _estCts.Cancel(); } catch { } StopPlayback(); }

        private void Build()
        {
            double dur = _info.Duration;
            double from = Math.Min(dur * 0.1, Math.Max(0, dur - 1)), to = Math.Min(dur, from + Math.Min(6.5, Math.Max(1, dur * 0.8)));
            _timeline = new RangeTimeline(dur, false, 40) { FrameStep = 1 / (_info.Fps > 0 ? _info.Fps : 25), MinGap = 0.2, Margin = new Thickness(0, 6, 0, 0) };
            _timeline.SetRange(from, to);
            _timeline.RangeChanged += (a, b, user) => { SyncRange(); if (user) _ = ShowFrameAsync(a); QueueEstimate(); };
            _timeline.PlayheadChanged += (t, user) => { if (user) { StopPlayback(); _ = ShowFrameAsync(t); } };

            var root = new StackPanel();
            var top = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            top.ColumnDefinitions.Add(new ColumnDefinition());

            // left: preview + timeline
            var left = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            _preview = new Image { Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapScalingMode(_preview, BitmapScalingMode.HighQuality);
            _player = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed, IsMuted = true, ScrubbingEnabled = true };
            _player.MediaFailed += (s, e) => { StopPlayback(); SetStatus("This format cannot be played in the preview. The clip can still be made."); };
            var pg = new Grid { Height = 168 };
            pg.Children.Add(_preview); pg.Children.Add(_player);
            _playBtn = FT.OverlayButton(Icons.Play, "Play", TogglePlay);
            pg.Children.Add(_playBtn);
            left.Children.Add(FT.Rounded(pg, 16));
            left.Children.Add(_timeline);
            _previewNote = FT.Body("", 12, null, new Thickness(0, 8, 0, 0));
            left.Children.Add(_previewNote);
            top.Children.Add(left);

            // right: format, range, size
            var right = new StackPanel();
            _format = new FT.Segmented(new[] { "GIF", "Animated WebP", "Short MP4" }, 0, "Format");
            _format.Changed += i => { OnFormat(); QueueEstimate(); };
            right.Children.Add(_format);
            var range = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            range.ColumnDefinitions.Add(new ColumnDefinition()); range.ColumnDefinitions.Add(new ColumnDefinition());
            _fromBox = FT.TextField("From", "", s => CommitRange(s, true)); _toBox = FT.TextField("To", "", s => CommitRange(s, false));
            var f1 = FT.Field("From", _fromBox, null, new Thickness(0, 0, 6, 6)); var f2 = FT.Field("To", _toBox, null, new Thickness(6, 0, 0, 6));
            Grid.SetColumn(f2, 1); range.Children.Add(f1); range.Children.Add(f2);
            right.Children.Add(range);
            _selection = FT.Body("", 12, null, new Thickness(0, 0, 0, 10));
            right.Children.Add(_selection);
            var size = new Grid();
            size.ColumnDefinitions.Add(new ColumnDefinition()); size.ColumnDefinitions.Add(new ColumnDefinition());
            _width = new FT.IntBox("Width", 480, "px", 16, 3840, 16, v => QueueEstimate(), SetStatus);
            _fps = new FT.IntBox("Frames per second", 15, "fps", 1, 60, 1, v => QueueEstimate(), SetStatus);
            var w1 = FT.Field("Width", _width.Host, null, new Thickness(0, 0, 6, 8)); var w2 = FT.Field("Frames per second", _fps.Host, null, new Thickness(6, 0, 0, 8));
            Grid.SetColumn(w2, 1); size.Children.Add(w1); size.Children.Add(w2);
            right.Children.Add(size);
            _loop = FT.SwitchRow("Loop forever", "Plays again when it ends", true, on => { });
            _loop.Margin = new Thickness(0, 4, 0, 0);
            right.Children.Add(_loop);
            Grid.SetColumn(right, 1);
            top.Children.Add(right);
            root.Children.Add(top);

            // target size card
            var card = new StackPanel();
            _targetSwitch = FT.SwitchRow("Fit a target file size", "Lowers width and fps in small steps until the result is under the limit", true, on => { _limitField.IsEnabled = on; _resultBox.IsEnabled = on; UpdateStatus(); });
            _targetSwitch.Margin = new Thickness(0, 0, 0, 10);
            card.Children.Add(_targetSwitch);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition());
            _limit = new FT.IntBox("Largest size", 8, "MB", 1, 500, 1, v => { QueueEstimate(); UpdateStatus(); }, SetStatus);
            _limitField = FT.Field("Largest size", _limit.Host, null, new Thickness(0, 0, 6, 0));
            _resultBox = FT.TextField("Result will be", "", null, false); _resultBox.IsReadOnly = true; _resultBox.IsTabStop = false; _resultBox.Opacity = 0.8;
            var resultField = FT.Field("Result will be", _resultBox, null, new Thickness(6, 0, 0, 0));
            Grid.SetColumn(resultField, 1);
            row.Children.Add(_limitField); row.Children.Add(resultField);
            card.Children.Add(row);
            root.Children.Add(FT.Card(card));

            _nameBox = FT.TextField("Save as", Path.GetFileName(DefaultOutput()), s => _nameTouched = true, false);
            root.Children.Add(FT.Field("Save as", FT.FileNameField(_nameBox), null, new Thickness(0, 0, 0, 4)));

            SetLeftActions(FT.Tonal("Choose folder", ChooseFolder, Icons.Folder));
            _readyView = root;
            Show(ToolState.Ready, root);
            SyncRange(); OnFormat(); UpdateStatus();
        }

        private string DefaultOutput()
        {
            var fmt = (ClipFormat)(_format?.Index ?? 0);
            return MediaProc.UniquePath(_folder ?? Path.GetDirectoryName(_src), Path.GetFileNameWithoutExtension(_src), GifEngine.ExtensionFor(fmt), _src);
        }

        private void ChooseFolder()
        {
            var f = FolderChoice(this, "Choose where the clip goes");
            if (f == null) return;
            _folder = f;
            if (!_nameTouched) { _nameBox.Text = Path.GetFileName(DefaultOutput()); _nameBox.Tag = _nameBox.Text; }
            SetLeftActions(FT.Tonal("Save in " + PathDisplay.Shown(f), ChooseFolder, Icons.Folder));
        }

        private void OnFormat()
        {
            var fmt = (ClipFormat)_format.Index;
            SetPrimary(true, fmt == ClipFormat.Gif ? "Make GIF" : fmt == ClipFormat.WebP ? "Make WebP" : "Make MP4");
            _loop.IsEnabled = fmt != ClipFormat.Mp4;
            if (!_nameTouched || Path.GetExtension(_nameBox.Text) != GifEngine.ExtensionFor(fmt))
            {
                var stem = Path.GetFileNameWithoutExtension(_nameBox.Text);
                _nameBox.Text = Path.GetFileName(MediaProc.UniquePath(_folder ?? Path.GetDirectoryName(_src), string.IsNullOrEmpty(stem) ? Path.GetFileNameWithoutExtension(_src) : Regex_Strip(stem), GifEngine.ExtensionFor(fmt), _src)); _nameBox.Tag = _nameBox.Text;
            }
        }

        private static string Regex_Strip(string stem) => System.Text.RegularExpressions.Regex.Replace(stem, @" \(\d+\)$", "");

        // ------------------------------------------------------------ range, preview, estimate

        private void CommitRange(string text, bool isFrom)
        {
            if (!MediaTime.TryParse(text, out var t)) { SetStatus("Type a time like 0:08.5 or 1:02."); SyncRange(); return; }
            t = Math.Clamp(t, 0, _info.Duration);
            if (isFrom) _timeline.SetRange(Math.Min(t, _timeline.End - _timeline.MinGap), _timeline.End);
            else _timeline.SetRange(_timeline.Start, Math.Max(t, _timeline.Start + _timeline.MinGap));
            SyncRange(); QueueEstimate();
        }

        internal void SetRange(double from, double to) { _timeline.SetRange(from, to); SyncRange(); }
        internal void SetFormat(ClipFormat f) { _format.Select((int)f, true); }
        internal void SetWidth(int w) => _width.Type(w.ToString());
        internal void SetFps(int f) => _fps.Type(f.ToString());
        internal void SetTarget(bool on, int mb) { _targetSwitch.IsChecked = on; _limit.Set(mb, false); }
        internal void SetLoop(bool on) => _loop.IsChecked = on;
        internal void SetOutputName(string n) { _nameBox.Text = n; _nameBox.Tag = n; _nameTouched = true; }
        internal void SetOutputFolder(string f) { _folder = f; }

        private void SyncRange()
        {
            _fromBox.Text = MediaTime.Format(_timeline.Start, 1); _fromBox.Tag = _fromBox.Text;
            _toBox.Text = MediaTime.Format(_timeline.End, 1); _toBox.Tag = _toBox.Text;
            _selection.Text = $"{(_timeline.End - _timeline.Start):0.0} seconds selected. Drag the handles on the timeline or type times.";
        }

        private async Task LoadThumbsAsync()
        {
            try
            {
                var strip = await VideoTrimEngine.ThumbnailsAsync(_src, _info.Duration, 12, 40, Cts.Token);
                if (strip.Count > 0) _timeline.SetStrip(strip); else _timeline.Failed();
            }
            catch { _timeline?.Failed(); }
        }

        private async Task ShowFrameAsync(double t)
        {
            try { _frameCts.Cancel(); } catch { }
            _frameCts = new CancellationTokenSource();
            var ct = _frameCts.Token;
            try
            {
                await Task.Delay(90, ct);
                var img = await MediaProc.FrameAtAsync(_src, t, 560, ct);
                if (img != null && !ct.IsCancellationRequested) _preview.Source = img;
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private void TogglePlay()
        {
            if (_playing) { StopPlayback(); return; }
            try
            {
                _player.Source = new Uri(_src);
                _player.Visibility = Visibility.Visible;
                _player.Position = TimeSpan.FromSeconds(_timeline.Start);
                _player.Play(); _playing = true;
                _playBtn.Content = Ui.Icon(Icons.Pause, Brushes.White, 20, 2); AutomationProperties.SetName(_playBtn, "Pause");
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
            if (t >= _timeline.End) { if (_loop.IsChecked == true) _player.Position = TimeSpan.FromSeconds(_timeline.Start); else StopPlayback(); }
        }

        private void StopPlayback()
        {
            if (!_playing && _player?.Visibility != Visibility.Visible) return;
            _playing = false;
            try { _tick?.Stop(); _player?.Pause(); _player?.Close(); } catch { }
            if (_player != null) _player.Visibility = Visibility.Collapsed;
            if (_playBtn != null) { _playBtn.Content = Ui.Icon(Icons.Play, Brushes.White, 20, 2); AutomationProperties.SetName(_playBtn, "Play"); }
        }

        private async Task EstimateLaterAsync() { await Task.Delay(400); QueueEstimate(true); }

        private DispatcherTimer _debounce;
        private void QueueEstimate(bool now = false)
        {
            if (_width == null) return;
            _estimate = -1;
            _previewNote.Text = "Estimating the size…";
            UpdateStatus();
            _debounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _debounce.Stop(); _debounce.Tick -= OnDebounce; _debounce.Tick += OnDebounce;
            if (now) { OnDebounce(null, null); return; }
            _debounce.Start();
        }

        private async void OnDebounce(object s, EventArgs e)
        {
            _debounce?.Stop();
            if (State != ToolState.Ready) return;
            try { _estCts.Cancel(); } catch { }
            _estCts = new CancellationTokenSource();
            var ct = _estCts.Token;
            var o = Options;
            // a long clip is estimated from its first seconds, scaled up
            double span = o.To - o.From;
            var probe = new GifOptions { Format = o.Format, From = o.From, To = o.From + Math.Min(span, 4), Width = o.Width, Fps = o.Fps, Loop = o.Loop };
            try
            {
                long b = await GifEngine.EstimateBytesAsync(_src, probe, ct);
                if (ct.IsCancellationRequested) return;
                _estimate = b > 0 ? (long)(b * (span / Math.Max(0.1, probe.To - probe.From))) : -1;
            }
            catch { _estimate = -1; }
            var (w, h) = PictureSize(o.Width);
            _previewNote.Text = _estimate > 0 ? $"Preview of the result at {o.Width} px wide, looping. Estimate: {Ui.Bytes(_estimate)}" : $"Preview of the result at {o.Width} px wide, looping.";
            _resultBox.Text = _estimate > 0 ? $"{o.Width} px · {o.Fps} fps · {Ui.Bytes(_estimate)}" : $"{o.Width} px · {o.Fps} fps";
            UpdateStatus();
        }

        private (int W, int H) PictureSize(int width) => (width, Math.Max(1, (int)Math.Round(width * (double)_info.Height / Math.Max(1, _info.Width))));

        private void UpdateStatus()
        {
            if (_limit == null) return;
            long limit = Options.TargetBytes;
            if (_estimate < 0) { SetStatus(limit > 0 ? $"Largest size {Ui.Bytes(limit)}. The size is being estimated…" : "The size is being estimated…"); return; }
            if (limit <= 0) SetStatus($"Estimated {Ui.Bytes(_estimate)}.");
            else if (_estimate <= limit) SetStatus($"Estimated {Ui.Bytes(_estimate)}, under your {Ui.Bytes(limit)} limit.");
            else SetStatus($"Estimated {Ui.Bytes(_estimate)}, over your {Ui.Bytes(limit)} limit. The width and frame rate will be lowered to fit.");
        }

        // ------------------------------------------------------------ running

        protected override void PrimaryPressed() => Run(GoAsync);

        internal async Task GoAsync()
        {
            if (State != ToolState.Ready) return;
            try { _estCts.Cancel(); _debounce?.Stop(); } catch { }
            StopPlayback();
            var o = Options;
            if (o.To - o.From < 0.2) { SetStatus("The range is too short. Move the handles apart."); return; }
            var name = (_nameBox.Text ?? "").Trim();
            var ext = GifEngine.ExtensionFor(o.Format);
            if (name.Length == 0) name = Path.GetFileName(DefaultOutput());
            if (!string.Equals(Path.GetExtension(name), ext, StringComparison.OrdinalIgnoreCase)) name = Path.GetFileNameWithoutExtension(name) + ext;
            name = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var dest = MediaProc.UniquePath(_folder ?? Path.GetDirectoryName(_src), Path.GetFileNameWithoutExtension(name), ext, _src);
            NewRun(); var ct = Cts.Token;
            var label = o.Format == ClipFormat.Gif ? "Make GIF" : o.Format == ClipFormat.WebP ? "Make WebP" : "Make MP4";
            WorkingView("Progress", label, "Cancel deletes the unfinished file.", out var report);
            SetSummary("making " + Path.GetFileName(dest));
            var res = await GifEngine.RunAsync(_src, dest, o, (v, msg) => report(v, msg, $"Clip of {(o.To - o.From):0.0} s from {MediaTime.Format(o.From, 1)}."), ct);
            LastResult = res;
            if (res.Cancelled) { BackToReady("Cancelled. Nothing was saved."); return; }
            if (res.TargetTooSmall) { ShowTooSmall(res, o); return; }
            if (!res.Ok) { ShowError(res); return; }
            LastOutput = dest;
            ShowDone(dest, res, o);
        }

        private void BackToReady(string status)
        {
            SetSummary($"{MediaTime.Format(_info.Duration, 0)} · {_info.Width}×{_info.Height} · {Ui.Bytes(_info.Bytes)}");
            SetPrimary(true);
            Show(ToolState.Ready, _readyView);
            OnFormat(); UpdateStatus();
            if (status != null) SetStatus(status);
        }

        private void ShowDone(string dest, GifResult res, GifOptions o)
        {
            var sp = new StackPanel();
            string limitText = o.TargetBytes > 0 ? $"Fits the {Ui.Bytes(o.TargetBytes)} limit. The video was not changed." : "The video was not changed.";
            sp.Children.Add(FT.Banner(BannerKind.Success, "Saved " + Path.GetFileName(dest), limitText));
            var (w, h) = PictureSize(res.Width > 0 ? res.Width : o.Width);
            bool stepped = res.Width != o.Width || res.Fps != o.Fps;
            var rows = new List<(string, string)>
            {
                ("Size", o.TargetBytes > 0 ? $"{Ui.Bytes(res.Bytes)} (limit {Ui.Bytes(o.TargetBytes)})" : Ui.Bytes(res.Bytes)),
                ("Picture", $"{w}×{h} · {res.Fps} fps · {res.Seconds:0.0} s" + (o.Format != ClipFormat.Mp4 && o.Loop ? " · loops" : "")),
                ("Settings used", stepped ? $"Lowered to {res.Width} px and {res.Fps} fps to fit" : "Width and fps unchanged, no step-down needed"),
            };
            sp.Children.Add(FT.FactsCard(rows));
            var row = new WrapPanel();
            row.Children.Add(FT.Tonal("Show in folder", () => Reveal(dest), Icons.Folder));
            row.Children.Add(FT.Tonal("Copy file", () => { try { ClipboardWatcher.SetFiles(dest); SetStatus("File copied."); } catch { } }, MediaIcons.Copy));
            row.Children.Add(FT.Outline("Make another", () => BackToReady(null)));
            sp.Children.Add(row);
            SetSummary("done");
            Show(ToolState.Done, sp, $"{Path.GetFileName(dest)} saved.");
        }

        private void ShowTooSmall(GifResult res, GifOptions o)
        {
            var last = res.Tried.LastOrDefault();
            var sp = new StackPanel();
            var sw = FT.Accent("Switch to Short MP4", () => { _format.Select((int)ClipFormat.Mp4, true); BackToReady("Short MP4 is far smaller. Check the size and make it."); });
            sw.Margin = new Thickness(0);
            sp.Children.Add(FT.Banner(BannerKind.Warning, "Cannot reach " + Ui.Bytes(o.TargetBytes),
                $"Even at {last?.Width} px and {last?.Fps} fps the best result is {Ui.Bytes(last?.Bytes ?? 0)}. Raise the limit, shorten the range, or switch to Short MP4, which is far smaller.", sw));
            var tried = new StackPanel();
            tried.Children.Add(FT.Eyebrow("Tried", new Thickness(0, 0, 0, 6)));
            foreach (var a in res.Tried)
            {
                var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var l = FT.Body($"{a.Width} px · {a.Fps} fps", 13, Ui.Res("B.Text"));
                var r = FT.Body(Ui.Bytes(a.Bytes), 13, Ui.Res("B.TextSoft")); Grid.SetColumn(r, 1);
                g.Children.Add(l); g.Children.Add(r); tried.Children.Add(g);
            }
            sp.Children.Add(FT.Card(tried));
            var row = new WrapPanel();
            row.Children.Add(FT.Tonal("Back to the settings", () => BackToReady(null)));
            sp.Children.Add(row);
            SetSummary("cannot reach the target");
            Show(ToolState.Error, sp, "Nothing was saved.");
        }

        private void ShowError(GifResult res)
        {
            var sp = ErrorView("The clip could not be made", "FFmpeg reported: " + MediaProc.Plain(res.Error), $"No output file was kept. {Path.GetFileName(_src)} is untouched.", () => BackToReady(null), "Back to the settings", res.Error);
            SetSummary("could not convert");
            Show(ToolState.Error, sp, "Nothing was saved.");
        }
    }
}
