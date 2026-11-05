using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Core.DocTools;
using Cue = MonitoringTools.Core.DocTools.Cue;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// "Subtitle timing" (design/file-tools, board 13). Reads an .srt or .vtt with the app's own parser and shifts every cue,
    /// stretches the timing between two anchor cues (with frame rate presets), or converts between SRT and WebVTT. Cues with
    /// overlaps or unreadable times are listed, with Fix overlaps and Skip broken cue. The original file is never changed:
    /// the result is saved under a new name.
    /// </summary>
    public sealed class SubtitleTimingDialog : FileToolDialog
    {
        private SubtitleFile _file;
        private readonly string _readError;
        private int _mode;                                   // 0 shift, 1 stretch, 2 convert
        private string _shiftText = "+0.000";
        private string _firstText, _lastText;
        private SubtitleFormat _convertTo;
        private string _saveName;
        private bool _saveNameEdited;
        private TextBox _shiftBox, _firstBox, _lastBox, _saveBox;
        private TextBlock _hint, _resultText, _stretchNote;
        private Button _save;
        private string _problemNote;

        public int Mode => _mode;
        public string SavedPath { get; private set; }
        internal SubtitleFile File => _file;

        private static readonly List<SubtitleTimingDialog> OpenList = new List<SubtitleTimingDialog>();
        internal static SubtitleTimingDialog Current => OpenList.LastOrDefault();

        public static SubtitleTimingDialog Open(string file)
        {
            if (!System.IO.File.Exists(file)) { App.Toasts.ShowNotice("Nothing to open", "The file is no longer there.", App.Toasts.Amber, 4, icon: DocIcons.Subtitle); return null; }
            var d = new SubtitleTimingDialog(file);
            d.Show(); d.Activate();
            return d;
        }

        private SubtitleTimingDialog(string file) : base("Subtitle timing", file, DocIcons.Subtitle, 800)
        {
            OpenList.Add(this);
            Closed += (s, e) => OpenList.Remove(this);
            try { _file = SubtitleTools.Read(file); }
            catch (Exception ex) { _readError = ex.Message; }
            if (_file == null || _file.Cues.Count == 0)
            {
                _file ??= new SubtitleFile();
                ShowUnreadable(_readError ?? "No subtitle cues were found in this file.");
                return;
            }
            _convertTo = _file.Format == SubtitleFormat.Srt ? SubtitleFormat.Vtt : SubtitleFormat.Srt;
            ResetStretchDefaults();
            Render();
        }

        private void ResetStretchDefaults()
        {
            var ok = _file.Cues.Where(c => !c.Broken).ToList();
            _firstText = ok.Count > 0 ? SubtitleTools.FormatTimeShort(ok.First().Start) : "00:00:00,000";
            _lastText = ok.Count > 0 ? SubtitleTools.FormatTimeShort(ok.Last().Start) : "00:00:00,000";
        }

        private SubtitleFormat Target => _mode == 2 ? _convertTo : _file.Format;
        private string Tag => _mode == 0 ? "shifted" : _mode == 1 ? "stretched" : null;

        private string Duration() { var ok = _file.Cues.Where(c => !c.Broken).ToList(); if (ok.Count == 0) return "0:00"; var end = ok.Max(c => c.End); var ts = TimeSpan.FromMilliseconds(end); return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}" : $"{ts.Minutes}:{ts.Seconds:00}"; }

        // ------------------------------------------------------------ drawing

        private void Render()
        {
            var problems = SubtitleTools.Problems(_file);
            SetSummary(problems.Count > 0 ? "problem found" : $"{(_file.Cues.Count == 1 ? "1 cue" : _file.Cues.Count + " cues")} · {Duration()}");
            var parts = new List<UIElement>();
            if (problems.Count > 0) parts.AddRange(ProblemParts(problems));
            parts.Add(CueTable());
            parts.Add(Segmented("What to do", new[] { "Shift", "Stretch", "Convert" }, _mode, i => { _mode = i; _saveNameEdited = false; Render(); }));
            if (_mode == 0) parts.AddRange(ShiftParts());
            else if (_mode == 1) parts.AddRange(StretchParts());
            else parts.AddRange(ConvertParts());
            _saveName = _saveNameEdited ? _saveName : Path.GetFileName(SubtitleTools.DefaultOutput(FilePath, Tag, Target));
            parts.Add(FieldLabel("Save as"));
            _saveBox = Field("Save as", _saveName);
            _saveBox.TextChanged += (s, e) => { _saveName = _saveBox.Text; _saveNameEdited = true; };
            parts.Add(_saveBox);
            SetBody(parts.ToArray());
            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost");
            _save = Ui.Button(_mode == 2 ? "Convert" : "Save", Save, "Btn.Accent", _mode == 2 ? DocIcons.Refresh : Icons.Check);
            SetFooter("", null, new[] { cancel, _save }, _save);
            Recalculate();
        }

        private IEnumerable<UIElement> ProblemParts(List<SubtitleTools.Problem> problems)
        {
            int overlaps = problems.Count(p => p.Kind == "overlap" || p.Kind == "backwards"), broken = problems.Count(p => p.Kind == "broken");
            string title = overlaps > 0 && broken > 0 ? $"{(overlaps == 1 ? "1 cue overlaps" : overlaps + " cues overlap")} and {(broken == 1 ? "1 is broken" : broken + " are broken")}"
                : overlaps > 0 ? (overlaps == 1 ? "1 cue overlaps" : $"{overlaps} cues overlap") : (broken == 1 ? "1 cue is broken" : $"{broken} cues are broken");
            yield return Banner(BannerKind.Warn, title, "The file can still be shifted. Fix or skip the cues below first if you want a clean result.");
            var g = new StackPanel();
            foreach (var p in problems.Take(6))
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                foreach (var w in new[] { 44.0, 124, 124, 0 }) row.ColumnDefinitions.Add(new ColumnDefinition { Width = w == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(w) });
                void Cell(string t, int c, bool mono) { var tb = Text(t, 13, null, null, TextWrapping.NoWrap); if (mono) tb.FontFamily = Ui.Font("F.Mono"); Grid.SetColumn(tb, c); row.Children.Add(tb); }
                Cell(p.Cue.Number.ToString(), 0, false);
                Cell(p.Cue.Broken ? p.Cue.RawTimes.Split(new[] { "-->" }, StringSplitOptions.None)[0].Trim() : SubtitleTools.FormatTimeShort(p.Cue.Start), 1, true);
                Cell(p.Cue.Broken ? (p.Cue.RawTimes.Contains("-->") ? p.Cue.RawTimes.Split(new[] { "-->" }, StringSplitOptions.None)[1].Trim().Split(' ')[0] : "") : SubtitleTools.FormatTimeShort(p.Cue.End), 2, true);
                Cell(p.Reason, 3, false);
                AutomationProperties.SetName(row, $"Cue {p.Cue.Number}: {p.Reason}");
                g.Children.Add(row);
            }
            if (problems.Count > 6) g.Children.Add(Hint(problems.Count - 6 == 1 ? "1 more problem cue." : $"{problems.Count - 6} more problem cues."));
            yield return Card(g, 8);
            var fix = Ui.Button("Fix overlaps", () => { int n = SubtitleTools.FixOverlaps(_file); _problemNote = null; Render(); SetStatus(n == 1 ? "Fixed 1 overlap." : $"Fixed {n} overlaps."); }, "Btn.Chip", Icons.Check);
            var skip = Ui.Button("Skip broken cue", () => { int n = SubtitleTools.SkipBroken(_file); ResetStretchDefaults(); Render(); SetStatus(n == 1 ? "Skipped 1 broken cue." : $"Skipped {n} broken cues."); }, "Btn.Chip");
            fix.IsEnabled = overlaps > 0; skip.IsEnabled = broken > 0;
            AutomationProperties.SetName(skip, "Skip broken cue");
            var act = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) }; act.Children.Add(fix); act.Children.Add(skip);
            yield return act;
        }

        private UIElement CueTable()
        {
            var host = new StackPanel();
            var head = new Grid { Margin = new Thickness(16, 8, 16, 4) };
            foreach (var w in new[] { 44.0, 130, 130, 0 }) head.ColumnDefinitions.Add(new ColumnDefinition { Width = w == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(w) });
            int c = 0; foreach (var t in new[] { "#", "Start", "End", "Text" }) { var tb = Text(t, 12, Ui.Res("B.TextSoft")); Grid.SetColumn(tb, c++); head.Children.Add(tb); }
            host.Children.Add(head);
            var rows = new StackPanel();
            foreach (var cue in _file.Cues.Take(200))
            {
                var g = new Grid { Margin = new Thickness(16, 5, 16, 5) };
                foreach (var w in new[] { 44.0, 130, 130, 0 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = w == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(w) });
                void Cell(string t, int col, bool mono) { var tb = Text(t, 13, null, null, TextWrapping.NoWrap); tb.TextTrimming = TextTrimming.CharacterEllipsis; if (mono) tb.FontFamily = Ui.Font("F.Mono"); Grid.SetColumn(tb, col); g.Children.Add(tb); }
                Cell(cue.Number.ToString(), 0, false);
                Cell(cue.Broken ? "xx:xx:xx,xxx" : SubtitleTools.FormatTime(cue.Start, _file.Format), 1, true);
                Cell(cue.Broken ? "xx:xx:xx,xxx" : SubtitleTools.FormatTime(cue.End, _file.Format), 2, true);
                Cell(cue.Text.Replace("\n", " / "), 3, false);
                rows.Children.Add(new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 1, 0, 0), Child = g });
            }
            if (_file.Cues.Count > 200) rows.Children.Add(Hint($"The first 200 of {_file.Cues.Count} cues are shown. Every cue is changed."));
            host.Children.Add(new ScrollViewer { Content = rows, MaxHeight = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var card = Card(host); ((Border)card).Padding = new Thickness(0, 4, 0, 4);
            AutomationProperties.SetName(card, "Subtitle cues");
            return card;
        }

        private IEnumerable<UIElement> ShiftParts()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = new StackPanel { Margin = new Thickness(0, 0, 14, 0) }; l.Children.Add(FieldLabel("Move all by"));
            _shiftBox = Field("Move all by, in seconds", _shiftText, true);
            _shiftBox.TextChanged += (s, e) => { _shiftText = _shiftBox.Text; Recalculate(); };
            var unit = new Grid(); unit.Children.Add(_shiftBox);
            unit.Children.Add(new TextBlock { Text = "seconds", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0), IsHitTestVisible = false });
            l.Children.Add(unit);
            _hint = Hint("Negative moves earlier."); l.Children.Add(_hint);
            var r = new StackPanel(); r.Children.Add(FieldLabel("Result for the first and last cue"));
            _resultText = Text("", 13, Ui.Res("B.TextSoft")); _resultText.FontFamily = Ui.Font("F.Mono");
            r.Children.Add(new Border { Height = 42, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.Surface"), Padding = new Thickness(14, 0, 14, 0), Child = new Grid { Children = { _resultText } } });
            _resultText.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(_resultText, "Result for the first and last cue");
            Grid.SetColumn(r, 1); g.Children.Add(l); g.Children.Add(r);
            yield return g;
            yield return new Border { Height = 10 };
            yield return Banner(BannerKind.Info, "Out of sync by a changing amount?", "Use Stretch: set where the first and last cue should land and every cue is spread between them.");
        }

        private IEnumerable<UIElement> StretchParts()
        {
            var g = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ok = _file.Cues.Where(c => !c.Broken).ToList();
            var l = new StackPanel { Margin = new Thickness(0, 0, 14, 0) }; l.Children.Add(FieldLabel("First cue should start at"));
            _firstBox = Field("First cue should start at", _firstText, true); _firstBox.TextChanged += (s, e) => { _firstText = _firstBox.Text; Recalculate(); };
            l.Children.Add(_firstBox); l.Children.Add(Hint("Now " + SubtitleTools.FormatTimeShort(ok.First().Start).Replace(',', ',')));
            var r = new StackPanel(); r.Children.Add(FieldLabel("Last cue should start at"));
            _lastBox = Field("Last cue should start at", _lastText, true); _lastBox.TextChanged += (s, e) => { _lastText = _lastBox.Text; Recalculate(); };
            r.Children.Add(_lastBox); r.Children.Add(Hint("Now " + SubtitleTools.FormatTimeShort(ok.Last().Start)));
            Grid.SetColumn(r, 1); g.Children.Add(l); g.Children.Add(r);
            yield return g;
            var presets = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            presets.Children.Add(new TextBlock { Text = "Frame rate", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 6) });
            foreach (var p in SubtitleTools.FrameRatePresets)
            {
                var pp = p;
                var chip = Ui.Button(pp.Label, () => { var (a, b) = SubtitleTools.PresetAnchors(_file, pp.From, pp.To); _firstText = SubtitleTools.FormatTimeShort(a); _lastText = SubtitleTools.FormatTimeShort(b); _firstBox.Text = _firstText; _lastBox.Text = _lastText; SetStatus($"Preset {pp.Label} fps filled in the two times."); }, "Btn.Chip");
                AutomationProperties.SetName(chip, "Frame rate " + pp.Label.Replace("→", "to"));
                chip.Margin = new Thickness(0, 0, 6, 6);
                presets.Children.Add(chip);
            }
            yield return presets;
            _stretchNote = Text("", 13, Ui.Res("B.TextSoft"));
            yield return Card(_stretchNote);
        }

        private IEnumerable<UIElement> ConvertParts()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = new StackPanel { Margin = new Thickness(0, 0, 14, 0) }; l.Children.Add(FieldLabel("From"));
            var from = Field("From", _file.Format == SubtitleFormat.Srt ? "SubRip (.srt)" : "WebVTT (.vtt)"); from.IsEnabled = false; l.Children.Add(from);
            var r = new StackPanel(); r.Children.Add(FieldLabel("To"));
            r.Children.Add(Segmented("To", new[] { "WebVTT (.vtt)", "SubRip (.srt)" }, _convertTo == SubtitleFormat.Vtt ? 0 : 1, i => { _convertTo = i == 0 ? SubtitleFormat.Vtt : SubtitleFormat.Srt; _saveNameEdited = false; Render(); }, new[] { "To WebVTT", "To SubRip" }));
            Grid.SetColumn(r, 1); g.Children.Add(l); g.Children.Add(r);
            yield return g;
            var note = SubtitleTools.ConvertNote(_file.Format, _convertTo);
            var dropped = SubtitleTools.DroppedOnConvert(_file, _convertTo);
            var sp = new StackPanel(); sp.Children.Add(Text(note, 13, Ui.Res("B.TextSoft")));
            if (dropped.Count > 0) sp.Children.Add(Text("Dropped and listed afterwards: " + string.Join(", ", dropped) + ".", 13, Ui.Res("B.Amber")));
            yield return new Border { Height = 10 };
            yield return Card(sp);
        }

        // ------------------------------------------------------------ live results

        private bool TryDelta(out long ms, out string why)
        {
            ms = 0; why = null;
            var t = (_shiftText ?? "").Trim().Replace(',', '.').Replace(" ", "");
            bool msUnit = t.EndsWith("ms", StringComparison.OrdinalIgnoreCase);
            if (msUnit) t = t[..^2]; else if (t.EndsWith("s", StringComparison.OrdinalIgnoreCase)) t = t[..^1];
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { why = "Type a number of seconds, for example +2.4 or -0.5."; return false; }
            ms = (long)Math.Round(msUnit ? v : v * 1000);
            return true;
        }

        private bool TryAnchors(out long a, out long b, out string why)
        {
            a = b = 0; why = null;
            var x = SubtitleTools.ParseTime(_firstText); var y = SubtitleTools.ParseTime(_lastText);
            if (!x.HasValue || !y.HasValue) { why = "Type each time as hours:minutes:seconds,milliseconds, for example 00:00:05,100."; return false; }
            var ok = _file.Cues.Where(c => !c.Broken).ToList();
            if (ok.Count < 2 || ok.First().Start == ok.Last().Start) { why = "Stretch needs two cues that start at different times."; return false; }
            if (y.Value <= x.Value) { why = "The last cue must start after the first one."; return false; }
            a = x.Value; b = y.Value; return true;
        }

        private bool _valid;

        private void Recalculate()
        {
            if (_save == null) return;
            var ok = _file.Cues.Where(c => !c.Broken).ToList();
            string status; bool valid = true;
            if (ok.Count == 0) { valid = false; status = "Every cue is broken. Nothing can be changed."; }
            else if (_mode == 0)
            {
                if (!TryDelta(out var d, out var why)) { valid = false; status = why; if (_hint != null) { _hint.Text = why; _hint.Foreground = Ui.Res("B.Danger"); } if (_resultText != null) _resultText.Text = "–"; }
                else
                {
                    var bad = SubtitleTools.WouldGoNegative(_file, d);
                    if (_resultText != null) _resultText.Text = SubtitleTools.FormatTimeShort(Math.Max(0, ok.First().Start + d)) + " · " + SubtitleTools.FormatTimeShort(Math.Max(0, ok.Last().Start + d));
                    if (bad != null)
                    {
                        valid = false;
                        status = $"Cue {bad.Number} would start before 00:00:00,000. Move by less.";
                        if (_hint != null) { _hint.Text = status; _hint.Foreground = Ui.Res("B.Danger"); }
                    }
                    else
                    {
                        if (_hint != null) { _hint.Text = $"Negative moves earlier. Cue 1 would start at {SubtitleTools.FormatTimeShort(ok.First().Start + d)}."; _hint.Foreground = Ui.Res("B.TextMute"); }
                        status = _file.Cues.Count == 1 ? "1 cue changes" : $"{_file.Cues.Count} cues change";
                        if (d == 0) status = "Moving by zero changes nothing.";
                    }
                }
            }
            else if (_mode == 1)
            {
                if (!TryAnchors(out var a, out var b, out var why)) { valid = false; status = why; if (_stretchNote != null) _stretchNote.Text = why; }
                else
                {
                    double f = SubtitleTools.Factor(_file, a, b);
                    var longest = _file.Cues.Where(c => !c.Broken).Max(c => (c.End - c.Start) * f) / 1000.0;
                    if (_stretchNote != null) _stretchNote.Text = string.Create(CultureInfo.InvariantCulture, $"Speed change: × {f:0.0000} (23.976 → {23.976 * f:0.000} fps equivalent). Longest cue stays under {Math.Ceiling(longest)} s.");
                    status = _file.Cues.Count == 1 ? "1 cue changes" : $"{_file.Cues.Count} cues change";
                    if (SubtitleTools.WouldGoNegative(_file, 0) == null && a < 0) { valid = false; status = "The first cue cannot start before zero."; }
                }
            }
            else
            {
                status = _file.Format == _convertTo ? "Both are the same format, so nothing changes." : $"{_file.Cues.Count} cues are written as {(_convertTo == SubtitleFormat.Vtt ? "WebVTT" : "SubRip")}.";
                valid = _file.Format != _convertTo;
            }
            _valid = valid;
            _save.IsEnabled = valid;
            SetStatus(status);
        }

        // ------------------------------------------------------------ saving

        private SubtitleFile Clone()
        {
            var c = new SubtitleFile { Format = _file.Format, HadBom = _file.HadBom, Dropped = new List<string>(_file.Dropped) };
            foreach (var x in _file.Cues) c.Cues.Add(new Cue { Number = x.Number, Start = x.Start, End = x.End, Text = x.Text, RawTimes = x.RawTimes, Settings = x.Settings });
            return c;
        }

        private string OutputPath()
        {
            var name = (_saveName ?? "").Trim();
            if (name.Length == 0) name = Path.GetFileName(SubtitleTools.DefaultOutput(FilePath, Tag, Target));
            var full = Path.IsPathRooted(name) ? name : Path.Combine(Path.GetDirectoryName(FilePath), name);
            return full;
        }

        private void Save()
        {
            if (!_valid) return;
            var work = Clone();
            if (_mode == 0 && TryDelta(out var d, out _)) SubtitleTools.Shift(work, d);
            else if (_mode == 1 && TryAnchors(out var a, out var b, out _)) SubtitleTools.Stretch(work, a, b);
            var target = Target;
            var path = OutputPath();
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase)) { SetStatus("Pick a new name: the original file is never overwritten."); return; }
            if (System.IO.File.Exists(path)) path = FileOps.Unique(path);
            try
            {
                var dropped = SubtitleTools.DroppedOnConvert(_file, target);
                SubtitleTools.Save(work, path, target);
                SavedPath = path;
                var rows = new List<(string, string)> { ("Saved as", Path.GetFileName(path)), ("Folder", PathDisplay.Shown(Path.GetDirectoryName(path))), ("Cues", work.Cues.Count.ToString()), ("Format", target == SubtitleFormat.Vtt ? "WebVTT" : "SubRip") };
                if (dropped.Count > 0) rows.Add(("Dropped", string.Join(", ", dropped)));
                SetSummary("saved");
                var open = Ui.Button("Open folder", () => ActionProvider.Reveal(path), "Btn.Ghost", Icons.Folder);
                SetBody(Banner(BannerKind.Ok, "Saved " + Path.GetFileName(path), "The original file was not changed."), KeyValues(rows), open);
                SetFooterClose("Done.");
            }
            catch (Exception ex)
            {
                SetSummary("could not save");
                SetBody(Banner(BannerKind.Bad, "The file could not be saved", ex.Message), Card(new StackPanel { Children = { Text("WHAT WAS NOT CHANGED", 12, Ui.Res("B.Mint"), FontWeights.Medium), Text("The original file was not changed and no partial file was left.", 13) } }));
                SetFooterClose("Check the folder is writable, or pick another name.");
            }
        }

        private void ShowUnreadable(string reason)
        {
            SetSummary("could not read");
            var copy = Ui.Button("Copy details", () => { try { Clipboard.SetText(reason); } catch { } }, "Btn.Ghost", DocIcons.Copy);
            SetBody(Banner(BannerKind.Bad, "This is not a readable subtitle file", reason),
                    Card(new StackPanel { Children = { Text("WHAT WAS NOT CHANGED", 12, Ui.Res("B.Mint"), FontWeights.Medium), Text("Nothing was changed.", 13) } }));
            SetFooterClose("SRT and WebVTT files open here.", copy);
        }

        // ------------------------------------------------------------ for checks and captures

        internal void ShowMode(int mode) { _mode = mode; _saveNameEdited = false; Render(); }
        internal void SetShift(string text) { if (_mode != 0) ShowMode(0); _shiftBox.Text = text; }
        internal void SetAnchors(string first, string last) { if (_mode != 1) ShowMode(1); _firstBox.Text = first; _lastBox.Text = last; }
        internal void SetConvertTo(SubtitleFormat f) { _convertTo = f; ShowMode(2); }
        internal void SetSaveName(string name) { _saveBox.Text = name; }
        internal void SaveNow() => Save();
        internal bool SaveEnabled => _save != null && _save.IsEnabled;
    }
}
