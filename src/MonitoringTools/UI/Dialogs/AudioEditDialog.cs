using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>Trim, fade and loudness for an audio file. The waveform shows what is kept; fades are sliders; loudness is two passes of
    /// FFmpeg's loudnorm with a true-peak limit of -1.0 dB, so a gain that would clip is held back by a limiter.</summary>
    public sealed class AudioEditDialog : FileToolDialog
    {
        public static readonly string[] Extensions = { ".wav", ".mp3", ".flac", ".m4a", ".ogg", ".aac" };
        public static bool Handles(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        private static readonly string[] OutExt = { ".wav", ".mp3", ".flac", ".m4a", ".ogg", ".aac" };
        private static readonly double[] Targets = { 0, -16, -14, -23, 0 };

        private readonly string _src;
        private readonly MediaInfo _info;
        private RangeTimeline _timeline;
        private TextBox _startBox, _endBox, _lenBox, _nameBox, _customBox;
        private Slider _fadeIn, _fadeOut;
        private TextBlock _fadeInVal, _fadeOutVal, _measured, _hint;
        private FT.ChipGroup _chips;
        private FrameworkElement _customField;
        private FrameworkElement _readyView;
        private Loudness _loud;
        private double _custom = -16;
        private string _folder;
        private bool _nameTouched;

        internal MediaInfo Info => _info;
        internal Loudness Measured => _loud;
        internal Task MeasuredReady { get; private set; } = Task.CompletedTask;
        internal AudioResult LastResult { get; private set; }
        internal string LastOutput { get; private set; }
        internal AudioPlan Plan => new AudioPlan
        {
            Start = _timeline?.Start ?? 0, End = _timeline?.End ?? _info.Duration,
            FadeIn = _fadeIn?.Value ?? 0, FadeOut = _fadeOut?.Value ?? 0,
            Normalize = (_chips?.Index ?? 0) != 0, TargetLufs = TargetLufs
        };
        private double TargetLufs => _chips == null ? -16 : _chips.Index == 4 ? _custom : Targets[_chips.Index];

        public static AudioEditDialog OpenFor(string path)
        {
            if (!File.Exists(path)) { App.Toasts.ShowNotice("Nothing to edit", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var ex = Existing<AudioEditDialog>(d => string.Equals(d._src, path, StringComparison.OrdinalIgnoreCase));
            if (ex != null) { ex.Activate(); return ex; }
            var d = new AudioEditDialog(path);
            d.Show(); d.Activate();
            return d;
        }

        private AudioEditDialog(string path) : base("Trim, fade and loudness", Icons.Audio, 780, "Save", Icons.Check)
        {
            _src = path;
            _info = MediaProc.Probe(path);
            if (!_info.Ok || !_info.HasAudio || _info.Duration <= 0.05)
            {
                SetFile(Path.GetFileName(path), "could not read");
                Show(ToolState.Error, ErrorView("This audio could not be decoded", "FFmpeg reported: " + MediaProc.Plain(_info.Error ?? "There is no audio in this file."), $"Nothing was written and {Path.GetFileName(path)} is untouched.", null, null, _info.Error), "Nothing was saved.");
                return;
            }
            SetFile(Path.GetFileName(path), $"{MediaTime.Format(_info.Duration, 0)} · {_info.SampleRate / 1000.0:0.#} kHz · {(_info.Channels == 1 ? "mono" : _info.Channels == 2 ? "stereo" : _info.Channels + " channels")} · {Ui.Bytes(_info.Bytes)}");
            Build();
            _ = LoadWaveAsync();
            MeasuredReady = MeasureAsync();
        }

        private void Build()
        {
            _timeline = new RangeTimeline(_info.Duration, true, 80) { MinGap = 0.1, FrameStep = 0.05, Margin = new Thickness(0, 0, 0, 10) };
            _timeline.SetRange(Math.Min(0.1, _info.Duration * 0.01), _info.Duration - Math.Min(0.1, _info.Duration * 0.01));
            if (_timeline.End - _timeline.Start < 0.2) _timeline.SetRange(0, _info.Duration);
            _timeline.RangeChanged += (a, b, user) => { Sync(); Update(); };

            var root = new StackPanel();
            root.Children.Add(FT.Rounded(_timeline, 16));
            ((Border)root.Children[0]).Padding = new Thickness(8, 6, 8, 2);

            var times = new Grid { Margin = new Thickness(0, 12, 0, 8) };
            for (int i = 0; i < 3; i++) times.ColumnDefinitions.Add(new ColumnDefinition());
            _startBox = FT.TextField("Start", "", s => Commit(s, true)); _endBox = FT.TextField("End", "", s => Commit(s, false));
            _lenBox = FT.TextField("New length", "", null); _lenBox.IsReadOnly = true; _lenBox.Opacity = 0.7; _lenBox.IsTabStop = false;
            var f1 = FT.Field("Start", _startBox, null, new Thickness(0, 0, 8, 0)); var f2 = FT.Field("End", _endBox, null, new Thickness(4, 0, 4, 0)); var f3 = FT.Field("New length", _lenBox, null, new Thickness(8, 0, 0, 0));
            Grid.SetColumn(f2, 1); Grid.SetColumn(f3, 2);
            times.Children.Add(f1); times.Children.Add(f2); times.Children.Add(f3);
            root.Children.Add(times);

            var fades = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            fades.ColumnDefinitions.Add(new ColumnDefinition()); fades.ColumnDefinitions.Add(new ColumnDefinition());
            _fadeInVal = FT.Body("0.0 s", 12, null); _fadeOutVal = FT.Body("0.0 s", 12, null);
            _fadeIn = FT.MakeSlider("Fade in", 0, 10, 0.5, 0.1, v => { _fadeInVal.Text = v.ToString("0.0", CultureInfo.InvariantCulture) + " s"; _timeline?.InvalidateVisual(); Update(); });
            _fadeOut = FT.MakeSlider("Fade out", 0, 10, 2.0, 0.1, v => { _fadeOutVal.Text = v.ToString("0.0", CultureInfo.InvariantCulture) + " s"; Update(); });
            _fadeInVal.Text = "0.5 s"; _fadeOutVal.Text = "2.0 s";
            fades.Children.Add(SliderColumn("Fade in", _fadeIn, _fadeInVal, new Thickness(0, 0, 12, 0)));
            var c2 = SliderColumn("Fade out", _fadeOut, _fadeOutVal, new Thickness(12, 0, 0, 0)); Grid.SetColumn(c2, 1); fades.Children.Add(c2);
            root.Children.Add(fades);

            // loudness card
            var card = new StackPanel();
            var head = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = "Loudness", FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") });
            _measured = FT.Body("Measuring the loudness…", 13); Grid.SetColumn(_measured, 1); head.Children.Add(_measured);
            card.Children.Add(head);
            _chips = new FT.ChipGroup(new[] { "Leave as it is", "Podcast −16 LUFS", "Streaming −14 LUFS", "Broadcast −23 LUFS", "Custom" }, 0, "Loudness target");
            _chips.Changed += i => { _customField.Visibility = i == 4 ? Visibility.Visible : Visibility.Collapsed; Update(); };
            card.Children.Add(_chips);
            _customBox = FT.TextField("Custom target in LUFS", "-16", s => CommitCustom(s));
            _customField = FT.Field("Custom target (LUFS)", _customBox, null, new Thickness(0, 0, 0, 8)); _customField.Visibility = Visibility.Collapsed; ((FrameworkElement)_customField).Width = 160; ((FrameworkElement)_customField).HorizontalAlignment = HorizontalAlignment.Left;
            card.Children.Add(_customField);
            _hint = FT.Body("", 12, Ui.Res("B.TextSoft"));
            card.Children.Add(_hint);
            root.Children.Add(FT.Card(card));

            _nameBox = FT.TextField("Save as", Path.GetFileName(DefaultOutput()), s => _nameTouched = true, false);
            root.Children.Add(FT.Field("Save as", FT.FileNameField(_nameBox), "Same format as the original. Type .mp3, .flac, .wav, .m4a or .ogg at the end of the name to change it.", new Thickness(0, 0, 0, 4)));

            SetLeftActions();
            _readyView = root;
            Show(ToolState.Ready, root);
            Sync(); Update();
        }

        private static StackPanel SliderColumn(string label, Slider slider, TextBlock value, Thickness margin)
        {
            var sp = new StackPanel { Margin = margin };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.Children.Add(FT.Label(label, 13, Ui.Res("B.Text"), FontWeights.Medium, new Thickness(0, 0, 0, 2)));
            Grid.SetColumn(value, 1); top.Children.Add(value);
            sp.Children.Add(top); sp.Children.Add(slider);
            return sp;
        }

        private string DefaultOutput() => MediaProc.UniquePath(_folder ?? Path.GetDirectoryName(_src), Path.GetFileNameWithoutExtension(_src) + "-edit", AudioEngine.OutputExtension(Path.GetExtension(_src)), _src);

        private async Task LoadWaveAsync()
        {
            try
            {
                var peaks = await AudioEngine.PeaksAsync(_src, _info.Duration, 800, Cts.Token);
                if (peaks != null) _timeline.SetPeaks(peaks); else _timeline.Failed();
            }
            catch { _timeline?.Failed(); }
        }

        private async Task MeasureAsync()
        {
            try
            {
                _loud = await AudioEngine.MeasureAsync(_src, 0, _info.Duration, 0, 0, Cts.Token, wholeFile: true);
                _measured.Text = _loud.Ok ? $"Measured now: {_loud.I:0.0} LUFS · true peak {_loud.TP:0.0} dB" : "Loudness could not be measured";
            }
            catch { _measured.Text = "Loudness could not be measured"; }
            Update();
        }

        private void Commit(string text, bool isStart)
        {
            if (!MediaTime.TryParse(text, out var t)) { SetStatus("Type a time like 0:03.2 or 1:02."); Sync(); return; }
            t = Math.Clamp(t, 0, _info.Duration);
            if (isStart) _timeline.SetRange(Math.Min(t, _timeline.End - _timeline.MinGap), _timeline.End);
            else _timeline.SetRange(_timeline.Start, Math.Max(t, _timeline.Start + _timeline.MinGap));
            Sync(); Update();
        }

        private void CommitCustom(string text)
        {
            if (!double.TryParse(text.Replace('−', '-').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v > -5 || v < -50) { SetStatus("Type a target from -50 to -5 LUFS."); _customBox.Text = _custom.ToString("0.#", CultureInfo.InvariantCulture); return; }
            _custom = v; Update();
        }

        internal void SetTimes(double s, double e) { _timeline.SetRange(s, e); Sync(); Update(); }
        internal void SetFades(double inSec, double outSec) { _fadeIn.Value = inSec; _fadeOut.Value = outSec; }
        internal void SetTarget(int chipIndex, double custom = -16) { _custom = custom; _customBox.Text = custom.ToString("0.#", CultureInfo.InvariantCulture); _chips.Select(chipIndex, true); Update(); }
        internal void SetOutputName(string n) { _nameBox.Text = n; _nameBox.Tag = n; _nameTouched = true; }
        internal void SetOutputFolder(string f) { _folder = f; }

        private void Sync()
        {
            _startBox.Text = MediaTime.Format(_timeline.Start); _startBox.Tag = _startBox.Text;
            _endBox.Text = MediaTime.Format(_timeline.End); _endBox.Tag = _endBox.Text;
            _lenBox.Text = MediaTime.Format(_timeline.End - _timeline.Start);
        }

        private void Update()
        {
            if (_timeline == null) return;
            double len = _timeline.End - _timeline.Start;
            if (_chips.Index == 0) _hint.Text = "Leave as it is: the level is not changed. Fades and the trim are still applied.";
            else if (_loud == null || !_loud.Ok) _hint.Text = "The loudness is measured first, then the level is set to the target. Two passes: measure, then apply.";
            else
            {
                var (gain, limited) = AudioEngine.GainFor(_loud, TargetLufs);
                string dir = gain >= 0 ? "Raises" : "Lowers";
                _hint.Text = $"{dir} the whole recording by {Math.Abs(gain):0.0} dB" + (limited ? "; a limiter keeps the peak under −1.0 dB. Two passes: measure, then apply." : ". No limiter is needed. Two passes: measure, then apply.");
            }
            SetStatus($"{MediaTime.Format(len)}" + (_chips.Index == 0 ? "" : $" · {TargetLufs:0.0} LUFS target").Replace("-", "−"));
            SetPrimary(len >= 0.05);
        }

        // ------------------------------------------------------------ running

        protected override void PrimaryPressed() => Run(GoAsync);

        internal async Task GoAsync()
        {
            if (State != ToolState.Ready) return;
            var plan = Plan;
            var name = (_nameBox.Text ?? "").Trim();
            if (name.Length == 0) name = Path.GetFileName(DefaultOutput());
            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (!OutExt.Contains(ext)) { name = Path.GetFileNameWithoutExtension(name) + AudioEngine.OutputExtension(Path.GetExtension(_src)); ext = Path.GetExtension(name); }
            name = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var dest = MediaProc.UniquePath(_folder ?? Path.GetDirectoryName(_src), Path.GetFileNameWithoutExtension(name), ext, _src);
            NewRun(); var ct = Cts.Token;
            WorkingView("Progress", "Save", "Cancel deletes the unfinished file.", out var report);
            SetSummary("working");
            var res = await AudioEngine.RunAsync(_src, dest, plan, _info, (v, msg) => report(v, msg, plan.Normalize && _loud != null && _loud.Ok ? $"The whole file measures {_loud.I:0.0} LUFS at the start." : "Writing " + Path.GetFileName(dest) + ".part"), ct);
            LastResult = res;
            if (res.Cancelled) { Back("Cancelled. Nothing was saved and the unfinished file was deleted."); return; }
            if (!res.Ok) { ShowError(res); return; }
            LastOutput = dest;
            ShowDone(dest, res, plan);
        }

        private void Back(string status)
        {
            SetSummary($"{MediaTime.Format(_info.Duration, 0)} · {_info.SampleRate / 1000.0:0.#} kHz · {(_info.Channels == 1 ? "mono" : _info.Channels == 2 ? "stereo" : _info.Channels + " channels")} · {Ui.Bytes(_info.Bytes)}");
            SetPrimary(true);
            Show(ToolState.Ready, _readyView);
            Sync(); Update();
            if (status != null) SetStatus(status);
        }

        private void ShowDone(string dest, AudioResult res, AudioPlan plan)
        {
            var sp = new StackPanel();
            sp.Children.Add(FT.Banner(BannerKind.Success, "Saved " + Path.GetFileName(dest), "The original recording was not changed."));
            var rows = new System.Collections.Generic.List<(string, string)>();
            var outInfo = MediaProc.Probe(dest);
            rows.Add(("Length", $"{MediaTime.Format(outInfo.Ok ? outInfo.Duration : res.Seconds)} (was {MediaTime.Format(_info.Duration, 0)})"));
            if (res.Before != null && res.Before.Ok && res.After != null && res.After.Ok)
            {
                rows.Add(("Loudness", $"{res.Before.I:0.0} LUFS before, {res.After.I:0.0} LUFS after".Replace("-", "−")));
                bool limited = plan.Normalize && AudioEngine.GainFor(res.Before, plan.TargetLufs).Limited;
                rows.Add(("True peak", $"{res.Before.TP:0.0} dB before, {res.After.TP:0.0} dB after{(limited ? " (limited)" : "")}, {(res.After.TP < 0 ? "no clipping" : "clipping is possible")}".Replace("-", "−")));
            }
            sp.Children.Add(FT.FactsCard(rows));
            var row = new WrapPanel();
            row.Children.Add(FT.Tonal("Show in folder", () => Reveal(dest), Icons.Folder));
            row.Children.Add(FT.Tonal("Play", () => Play(dest), Icons.Play));
            sp.Children.Add(row);
            SetSummary("done");
            Show(ToolState.Done, sp, $"{Path.GetFileName(dest)} saved.");
        }

        private void ShowError(AudioResult res)
        {
            var sp = ErrorView("The audio could not be saved", "FFmpeg reported: " + MediaProc.Plain(res.Error), $"Nothing was written and {Path.GetFileName(_src)} is untouched.", () => Back(null), "Back to the settings", res.Error);
            SetSummary("could not save");
            Show(ToolState.Error, sp, "Nothing was saved.");
        }
    }
}
