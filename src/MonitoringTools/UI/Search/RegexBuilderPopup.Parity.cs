using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using TreeNode = MonitoringTools.Core.RegexTree.Node;

namespace MonitoringTools.UI.Search
{
    // Sample tools of the Test view, other engines in Compare, pattern pairs, token profile and history in Benchmark,
    // editable code templates, the Help view with builder preferences, and the Files view's presets, history and backups.
    public sealed partial class RegexBuilderPopup
    {
        // ---------------------------------------------------------------- shared bits

        /// <summary>A labelled row of mutually exclusive chips. The chip of the current value is painted as chosen.</summary>
        private WrapPanel ChoiceRow(string title, IEnumerable<string> options, Func<string> get, Action<string> set, string help, Func<string, string> label = null)
        {
            var row = new WrapPanel();
            row.Children.Add(Label(title));
            void repaint() { foreach (var c in row.Children.OfType<Button>()) Paint(c, (string)c.Tag == get()); }
            foreach (var o in options)
            {
                var value = o;
                var chip = Chip(label?.Invoke(value) ?? value, help + " " + value, () => { set(value); repaint(); });
                chip.Tag = value;
                row.Children.Add(chip);
            }
            repaint();
            return row;
        }

        private static Border Boxed(UIElement child) => new Border
        {
            Child = child as UIElement, Background = Ui.Res("B.Sunken"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 8, 10, 4), Margin = new Thickness(0, 0, 0, 6)
        };

        private static TextBlock Body(string text, bool mono = false) =>
            new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextSoft"), FontFamily = mono ? Ui.Font("F.Mono") : Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) };

        // ---------------------------------------------------------------- Test: scope, line breaks, clipboard, file, hex

        private string _scope = "Whole text", _sampleEncoding = "Auto", _sampleNote = "";
        private bool _hexOn;
        private TextBox _hex;
        private TextBlock _sampleInfo;

        private FrameworkElement SampleTools()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            sp.Children.Add(ChoiceRow("Match against", RegexSample.Scopes, () => _scope, v => { _scope = v; RunTest(); }, "Match the pattern against"));
            var tools = new WrapPanel();
            tools.Children.Add(Chip("Convert to LF", "Change every line break in the sample to a line feed", () => ConvertSampleLines("LF")));
            tools.Children.Add(Chip("Convert to CRLF", "Change every line break in the sample to a carriage return and line feed", () => ConvertSampleLines("CRLF")));
            tools.Children.Add(Chip("Paste from clipboard", "Replace the sample with the text on the clipboard", PasteSample));
            tools.Children.Add(Chip("Open file...", "Read a text file into the sample, in the encoding chosen below", OpenSampleFile));
            Button hexChip = null;
            hexChip = Chip("Hex view", "Show the sample as bytes in hexadecimal, in the encoding chosen below", () => { _hexOn = !_hexOn; Paint(hexChip, _hexOn); UpdateHex(); });
            Paint(hexChip, _hexOn);
            tools.Children.Add(hexChip);
            sp.Children.Add(tools);
            sp.Children.Add(ChoiceRow("Encoding", new[] { "Auto", "UTF-8", "UTF-16", "Latin-1" }, () => _sampleEncoding, v => { _sampleEncoding = v; UpdateHex(); }, "Use this encoding for reading files and for the hex view:"));
            _sampleInfo = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            _sampleInfo.TextWrapping = TextWrapping.Wrap;
            AutomationProperties.SetLiveSetting(_sampleInfo, AutomationLiveSetting.Polite);
            sp.Children.Add(_sampleInfo);
            _hex = new TextBox
            {
                Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), FontSize = 11, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 110,
                VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 4, 0, 4), Visibility = _hexOn ? Visibility.Visible : Visibility.Collapsed
            };
            AutomationProperties.SetName(_hex, "Sample as hexadecimal bytes");
            Core.Language.SetLiteral(_hex, true);
            sp.Children.Add(_hex);
            return sp;
        }

        private void ConvertSampleLines(string style)
        {
            if (_sample == null) return;
            _sampleNote = "Line breaks converted to " + style + ".";
            _sample.Text = RegexSample.NormalizeLineEndings(_sample.Text, style);
        }

        private void PasteSample()
        {
            string text = null;
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch (Exception ex) { Log.Warn("Clipboard read failed: " + ex.Message); }
            PasteText(text);
        }

        /// <summary>Puts text in the sample the way the Paste button does with the clipboard's text.</summary>
        internal void PasteText(string text)
        {
            if (string.IsNullOrEmpty(text)) { _sampleNote = "The clipboard has no text."; RefreshSampleInfo(); return; }
            if (text.Length > RegexKit.MaxSampleLength) { text = text[..RegexKit.MaxSampleLength]; _sampleNote = "The clipboard text was cut to " + RegexKit.MaxSampleLength.ToString("N0") + " characters."; }
            else _sampleNote = "Pasted " + text.Length.ToString("N0") + " characters from the clipboard.";
            if (_sample != null) _sample.Text = text;
        }

        private void OpenSampleFile()
        {
            var d = new Microsoft.Win32.OpenFileDialog { Title = "Choose a text file for the sample" };
            if (d.ShowDialog() != true) return;
            LoadSampleFile(d.FileName);
        }

        /// <summary>Reads a file into the sample using the chosen encoding. Large files are cut, with a message.</summary>
        internal void LoadSampleFile(string path)
        {
            try
            {
                var info = new FileInfo(path);
                var bytes = File.ReadAllBytes(path);
                var text = RegexSample.Decode(bytes, _sampleEncoding, out var used);
                var cut = info.Length > RegexKit.MaxSampleLength;
                _sampleNote = $"Read {Path.GetFileName(path)} as {used}" + (cut ? $"; only the first {RegexKit.MaxSampleLength:N0} bytes were read." : ".");
                if (_sample != null) _sample.Text = text;
            }
            catch (Exception ex) { _sampleNote = "Could not read the file: " + ex.Message; RefreshSampleInfo(); }
        }

        private void UpdateHex()
        {
            if (_hex == null) return;
            _hex.Visibility = _hexOn ? Visibility.Visible : Visibility.Collapsed;
            if (_hexOn && _sample != null) _hex.Text = RegexSample.HexDump(_sample.Text, _sampleEncoding, 2048);
        }

        private void RefreshSampleInfo()
        {
            if (_sampleInfo == null || _sample == null) return;
            _sampleInfo.Text = (_sampleNote.Length > 0 ? _sampleNote + " " : "") + "Sample: " + _sample.Text.Length.ToString("N0") + " characters · " + RegexSample.DescribeLineEndings(_sample.Text);
        }

        // ---------------------------------------------------------------- Compare: other engines

        private StackPanel _engResults;
        private TextBlock _engStatus;
        private int _engRun;

        private FrameworkElement EnginesPanel()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            sp.Children.Add(Eyebrow("Other engines on this computer"));
            sp.Children.Add(Note("Runs the same pattern text in the other regular expression engines this computer really has: the Windows JScript engine (ECMAScript 3), the .NET Framework engine in Windows PowerShell 5.1, and Python or Node.js when they are installed. An engine that is missing is listed as not available. PCRE, Java, Perl, Ruby and Go engines are not installed here, so they cannot be run; the Code view translates a pattern for Java but cannot run it. Each engine receives the pattern as data, is stopped after a few seconds, and uses its own syntax rules, so a construct it lacks shows its own error."));
            var run = Ui.Button("Run in other engines", RunEngines, "Btn.Accent");
            run.ToolTip = "Run the pattern over the sample in every other engine found on this computer";
            run.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(run);
            _engStatus = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body") };
            AutomationProperties.SetLiveSetting(_engStatus, AutomationLiveSetting.Polite);
            sp.Children.Add(_engStatus);
            _engResults = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            sp.Children.Add(_engResults);
            return sp;
        }

        private void RunEngines()
        {
            if (_engResults == null) return;
            if (_s.Query.Length == 0 || _s.Error != null) { _engStatus.Text = _s.Error != null ? "Fix the pattern first: " + _s.Error : "Type a pattern, then run the engines."; return; }
            var pattern = _s.Query; var opts = _s.EngineOptions; var sample = _s.Sample ?? DefaultSample();
            int id = ++_engRun;
            _engResults.Children.Clear();
            _engStatus.Text = "Running...";
            Task.Run(() =>
            {
                try { return RegexFlavours.RunAll(pattern, opts, sample); }
                catch (Exception ex) { Log.Warn("Engine comparison failed: " + ex.Message); return null; }
            }).ContinueWith(t => _card.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (id != _engRun || _engResults == null) return;
                var list = t.Result;
                _engResults.Children.Clear();
                if (list == null) { _engStatus.Text = "The engines could not run."; return; }
                var reference = list[0].Hits;
                _engStatus.Text = $"Sample of {Math.Min(sample.Length, RegexFlavours.MaxSubject):N0} characters. Positions are in UTF-16 units, like .NET.";
                foreach (var r in list) _engResults.Children.Add(EngineRow(r, reference, pattern));
            })));
        }

        private static FrameworkElement EngineRow(RegexFlavours.EngineResult r, List<RegexFlavours.Hit> reference, string original)
        {
            var sp = new StackPanel();
            var head = new DockPanel();
            string state = !r.Available ? "not available" : r.Error != null ? "rejects the pattern" : r.TimedOut ? "hit the limit" : "ran";
            var st = Ui.Mono(state, Ui.Res(!r.Available ? "B.TextMute" : r.Error != null ? "B.Danger" : r.TimedOut ? "B.Amber" : "B.Mint"), 11);
            DockPanel.SetDock(st, Dock.Right); head.Children.Add(st);
            var name = new TextBlock { Text = r.Engine + (string.IsNullOrEmpty(r.Version) || r.Engine.Contains(r.Version) ? "" : " " + r.Version), FontSize = 13, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") };
            Core.Language.SetLiteral(name, true);
            head.Children.Add(name);
            sp.Children.Add(head);
            if (!r.Available) sp.Children.Add(Note(r.Note));
            else if (r.Error != null)
            {
                var e = Note(r.Error); Core.Language.SetLiteral(e, true); e.Foreground = Ui.Res("B.Amber"); sp.Children.Add(e);
                if (!string.IsNullOrEmpty(r.Note)) sp.Children.Add(Note(r.Note));
            }
            else
            {
                sp.Children.Add(Ui.Mono($"{r.Matches} match(es) · {r.Ms:0.0} ms", Ui.Res("B.TextSoft"), 11));
                if (!ReferenceEquals(r.Hits, reference)) sp.Children.Add(Note(RegexFlavours.Difference(reference, r.Hits)));
                if (!string.IsNullOrEmpty(r.Note)) sp.Children.Add(Note(r.Note));
            }
            if (r.Available && r.Converted != null && r.Converted != original)
            {
                var c = Ui.Mono("Pattern sent: " + r.Converted, Ui.Res("B.TextMute"), 11); c.TextWrapping = TextWrapping.Wrap; Core.Language.SetLiteral(c, true); sp.Children.Add(c);
            }
            return Boxed(sp);
        }

        // ---------------------------------------------------------------- Benchmark: two patterns, token profile, saved history

        private TextBox _pairB;
        private StackPanel _pairResults, _profileResults, _historyList;
        private TextBlock _pairStatus, _profileStatus;
        private RegexBench.SubjectBench _lastBench;
        private int _pairRun;

        private FrameworkElement BenchmarkExtras()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };

            sp.Children.Add(Eyebrow("Compare with another pattern"));
            sp.Children.Add(Note("Pattern A is the pattern above. Type a second pattern to run both on the sample with the same options and see which finds what and which is faster."));
                        var pb = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Text = _pairBText ?? "", Margin = new Thickness(0, 2, 0, 6) };
            AutomationProperties.SetName(pb, "Second pattern to compare with the first");
            pb.TextChanged += (o, e) => _pairBText = pb.Text;
            _pairB = pb;
            sp.Children.Add(pb);
            var runPair = Ui.Button("Compare the two patterns", RunPair, "Btn.Accent");
            runPair.ToolTip = "Run pattern A and pattern B on the sample and compare their matches and times";
            runPair.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(runPair);
            _pairStatus = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body") };
            AutomationProperties.SetLiveSetting(_pairStatus, AutomationLiveSetting.Polite);
            sp.Children.Add(_pairStatus);
            _pairResults = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            sp.Children.Add(_pairResults);

            sp.Children.Add(Eyebrow("Time per part of the pattern"));
            sp.Children.Add(Note("Times the pattern one top-level part at a time: the first part alone, then the first two, and so on. The jump between neighbours is the cost that part added on the sample. These are measurements of the real engine, not an internal profile of it, so short times are noisy."));
            var runProf = Ui.Button("Profile the parts", RunProfile, "Btn.Ghost");
            runProf.ToolTip = "Measure how much each top-level part of the pattern adds to the time on the sample";
            runProf.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(runProf);
            _profileStatus = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body") };
            sp.Children.Add(_profileStatus);
            _profileResults = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            sp.Children.Add(_profileResults);

            sp.Children.Add(Eyebrow("Saved benchmark history"));
            var histRow = new WrapPanel();
            var save = Ui.Button("Save last result", SaveBenchRecord, "Btn.Ghost");
            save.ToolTip = "Keep the result of the last benchmark run, with its pattern, so a later version of the pattern can be compared with it";
            var clear = Ui.Button("Clear history", () => { Settings.Current.RegexBenchHistory.Clear(); Settings.Save(); RenderHistory(); }, "Btn.Ghost");
            clear.ToolTip = "Delete every saved benchmark result";
            histRow.Children.Add(save); histRow.Children.Add(clear);
            sp.Children.Add(histRow);
            _historyList = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            sp.Children.Add(_historyList);
            RenderHistory();
            return sp;
        }

        private string _pairBText;

        private void RunPair()
        {
            if (_pairResults == null) return;
            var a = _s.Query; var b = _pairBText ?? "";
            if (a.Length == 0 || b.Length == 0) { _pairStatus.Text = "Enter both patterns."; return; }
            var opts = _s.EngineOptions; var sample = _s.Sample ?? DefaultSample();
            int id = ++_pairRun;
            _pairResults.Children.Clear(); _pairStatus.Text = "Running...";
            Task.Run(() => RegexBench.ComparePatterns(a, b, opts, sample)).ContinueWith(t => _card.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (id != _pairRun || _pairResults == null) return;
                _pairResults.Children.Clear();
                if (t.IsFaulted) { _pairStatus.Text = "The comparison could not run."; return; }
                var r = t.Result;
                _pairStatus.Text = r.Verdict;
                foreach (var run in new[] { r.A, r.B })
                {
                    var inner = new StackPanel();
                    var t1 = new TextBlock { Text = "Pattern " + run.Label, FontSize = 13, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") };
                    inner.Children.Add(t1);
                    var code = Ui.Mono(run.Pattern, Ui.Res("B.Mint"), 11); code.TextWrapping = TextWrapping.Wrap; Core.Language.SetLiteral(code, true); inner.Children.Add(code);
                    if (run.Error != null) { var e = Note("Not valid: " + run.Error); Core.Language.SetLiteral(e, true); inner.Children.Add(e); }
                    else inner.Children.Add(Ui.Mono($"{run.Matches} match(es) · " + (run.TimedOut ? "hit the 200 ms limit" : $"{run.Ms:0.000} ms"), Ui.Res(run.TimedOut ? "B.Amber" : "B.TextSoft"), 11));
                    _pairResults.Children.Add(Boxed(inner));
                }
            })));
        }

        private void RunProfile()
        {
            if (_profileResults == null) return;
            if (_s.Query.Length == 0 || _s.Error != null) { _profileStatus.Text = _s.Error != null ? "Fix the pattern first: " + _s.Error : "Type a pattern first."; return; }
            var p = _s.Query; var opts = _s.EngineOptions; var sample = _s.Sample ?? DefaultSample();
            _profileResults.Children.Clear(); _profileStatus.Text = "Measuring...";
            var disp = _card.Dispatcher;
            Task.Run(() => RegexBench.Profile(p, opts, sample)).ContinueWith(t => disp.BeginInvoke(new Action(() =>
            {
                if (_profileResults == null) return;
                _profileResults.Children.Clear();
                if (t.IsFaulted) { _profileStatus.Text = "The measurement could not finish."; return; }
                var list = t.Result;
                var worst = list.Where(x => !x.Skipped).OrderByDescending(x => x.DeltaMs).FirstOrDefault();
                _profileStatus.Text = worst == null ? "Nothing could be timed." : $"Most added time: {Snippet(worst.Text, 40)} (about {worst.DeltaMs:0.000} ms on this sample).";
                foreach (var tt in list)
                {
                    var line = Ui.Mono($"{Snippet(tt.Text, 34),-36} " + (tt.Skipped ? tt.Note : tt.TimedOut ? "hit the 200 ms limit" : $"+{tt.DeltaMs:0.000} ms · whole so far {tt.PrefixMs:0.000} ms · {tt.PrefixMatches} match(es)"), Ui.Res(tt.TimedOut ? "B.Amber" : tt.Skipped ? "B.TextMute" : "B.TextSoft"), 11);
                    line.TextWrapping = TextWrapping.Wrap; Core.Language.SetLiteral(line, true);
                    _profileResults.Children.Add(line);
                }
            })));
        }

        private void SaveBenchRecord()
        {
            if (_lastBench == null || _lastBench.Skipped) { if (_benchStatus != null) _benchStatus.Text = "Run a benchmark first, then save its result."; return; }
            RegexBench.Remember(_s.Query, _s.EngineOptions, _lastBench);
            Settings.Save();
            RenderHistory();
            if (_benchStatus != null) _benchStatus.Text = "Saved. The history keeps the " + RegexBench.MaxHistory + " newest results.";
        }

        private void RenderHistory()
        {
            if (_historyList == null) return;
            _historyList.Children.Clear();
            var hist = Settings.Current.RegexBenchHistory ?? new List<RegexBenchRecord>();
            if (hist.Count == 0) { _historyList.Children.Add(Ui.Mono("Nothing saved yet.", Ui.Res("B.TextMute"), 11)); return; }
            foreach (var h in hist.Take(10))
            {
                var rec = h;
                var inner = new DockPanel();
                var use = Ui.Button("Use pattern", () => _s.SetQuery(rec.Pattern, this), "Btn.Ghost");
                use.ToolTip = "Put this saved pattern back in the pattern box";
                DockPanel.SetDock(use, Dock.Right); inner.Children.Add(use);
                var text = new StackPanel();
                var head = Ui.Mono($"{rec.When} · {rec.SubjectLength:N0} characters · " + (rec.TimedOut ? "hit the 200 ms limit" : $"{rec.MsPerRun:0.000} ms per run"), Ui.Res("B.TextSoft"), 11);
                var pat = Ui.Mono(rec.Pattern, Ui.Res("B.Mint"), 11); pat.TextWrapping = TextWrapping.Wrap; Core.Language.SetLiteral(pat, true);
                text.Children.Add(head); text.Children.Add(pat);
                inner.Children.Add(text);
                _historyList.Children.Add(Boxed(inner));
            }
            if (hist.Count > 10) _historyList.Children.Add(Ui.Mono($"and {hist.Count - 10} older result(s).", Ui.Res("B.TextMute"), 11));
        }

        // ---------------------------------------------------------------- Code: kinds and editable templates

        private string _codeKind = "Match";
        private bool _codeEditing;
        private TextBox _templateBox;
        private TextBlock _templateStatus;

        private FrameworkElement CodeViewFull()
        {
            var sp = new StackPanel();
            var langs = new WrapPanel();
            foreach (var l in RegexKit.CodeLanguages)
            {
                var lang = l;
                var b = Chip(l, "Generate " + l, () => { _language = lang; Show("Code"); });
                Paint(b, l == _language);
                langs.Children.Add(b);
            }
            sp.Children.Add(langs);
            sp.Children.Add(ChoiceRow("Snippet for", RegexCode.Kinds, () => _codeKind, v => { _codeKind = v; Show("Code"); }, "Write a snippet that does this:", v => v + " snippet"));
            _code = new TextBox
            {
                Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), FontSize = 12, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                MinHeight = 110, VerticalContentAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 8)
            };
            AutomationProperties.SetName(_code, "Generated code");
            Core.Language.SetLiteral(_code, true);
            sp.Children.Add(_code);
            var buttons = new WrapPanel();
            buttons.Children.Add(Ui.Button("Copy code", () => ClipboardWatcher.SetText(_code.Text), "Btn.Ghost"));
            Button edit = null;
            edit = Chip("Edit template", "Show the template this snippet is made from so you can change it", () => { _codeEditing = !_codeEditing; Paint(edit, _codeEditing); Show("Code"); });
            Paint(edit, _codeEditing);
            buttons.Children.Add(edit);
            sp.Children.Add(buttons);
            _templateBox = null; _templateStatus = null;
            if (_codeEditing)
            {
                sp.Children.Add(Eyebrow("Template for " + _language + " " + _codeKind.ToLowerInvariant()));
                sp.Children.Add(Note("Placeholders: {{literal}} the pattern as a string in this language, {{flags}} its option expression, {{replacement}} the replacement as a string, {{timeout}} the match limit in milliseconds, {{notes}} comment lines about anything that could not be translated. Everything else is copied as written. The change is kept on this computer and applies to this language and snippet only."));
                _templateBox = new TextBox
                {
                    Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), FontSize = 12, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 120,
                    VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Text = RegexCode.TemplateFor(_language, _codeKind), Margin = new Thickness(0, 4, 0, 6)
                };
                AutomationProperties.SetName(_templateBox, "Code template");
                Core.Language.SetLiteral(_templateBox, true);
                _templateBox.TextChanged += (o, e) => UpdateCode();
                sp.Children.Add(_templateBox);
                var tb = new WrapPanel();
                tb.Children.Add(Ui.Button("Save template", SaveTemplate, "Btn.Accent"));
                tb.Children.Add(Ui.Button("Restore built-in", () => { RegexCode.SaveTemplate(_language, _codeKind, RegexCode.DefaultTemplate(_language, _codeKind)); Settings.Save(); _templateBox.Text = RegexCode.DefaultTemplate(_language, _codeKind); UpdateCode(); }, "Btn.Ghost"));
                sp.Children.Add(tb);
                _templateStatus = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body") };
                AutomationProperties.SetLiveSetting(_templateStatus, AutomationLiveSetting.Polite);
                sp.Children.Add(_templateStatus);
            }
            UpdateCode();
            return sp;
        }

        private void UpdateCode()
        {
            if (_code == null) return;
            var template = _templateBox?.Text;
            _code.Text = RegexCode.Render(_language, _codeKind, _s.Query, _s.EngineOptions, _s.Replacement ?? "", string.IsNullOrWhiteSpace(template) ? null : template);
            if (_templateStatus != null)
            {
                var unknown = RegexCode.UnknownPlaceholders(_templateBox.Text);
                _templateStatus.Text = unknown.Count > 0 ? "Unknown placeholder(s), printed as written: " + string.Join(", ", unknown.Select(u => "{{" + u + "}}")) : RegexCode.IsCustom(_language, _codeKind) ? "Your saved template is in use." : "The built-in template is in use.";
                _templateStatus.Foreground = Ui.Res(unknown.Count > 0 ? "B.Amber" : "B.TextMute");
            }
        }

        private void SaveTemplate()
        {
            if (_templateBox == null) return;
            RegexCode.SaveTemplate(_language, _codeKind, _templateBox.Text);
            Settings.Save();
            UpdateCode();
            if (_templateStatus != null && RegexCode.UnknownPlaceholders(_templateBox.Text).Count == 0) _templateStatus.Text = RegexCode.IsCustom(_language, _codeKind) ? "Saved. Your template is in use." : "That is the built-in template, so nothing needs saving.";
        }

        // ---------------------------------------------------------------- Library: search, description, parameters

        private string _libQuery = "";
        private bool _libRegex;
        private readonly Dictionary<SavedPattern, Dictionary<string, TextBox>> _libParamBoxes = new Dictionary<SavedPattern, Dictionary<string, TextBox>>();

        private FrameworkElement LibrarySearchBar(Action rerender)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            Button rx = null;
            rx = Chip("Regex", "Treat the search as a regular expression over the name, description, pattern and parameter names", () => { _libRegex = !_libRegex; Paint(rx, _libRegex); rerender(); });
            Paint(rx, _libRegex);
            rx.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(rx, Dock.Right); row.Children.Add(rx);
            var box = new TextBox { Style = Ui.Style("Input"), Text = _libQuery };
            AutomationProperties.SetName(box, "Search the saved patterns");
            box.ToolTip = "Words are matched in the name, description, pattern and parameter names";
            box.TextChanged += (o, e) => { _libQuery = box.Text; rerender(); };
            row.Children.Add(box);
            if (_libFocusSearch)
            {
                _libFocusSearch = false;
                _card.Dispatcher.BeginInvoke(new Action(() => { box.Focus(); box.CaretIndex = box.Text.Length; }));
            }
            return row;
        }

        // ---------------------------------------------------------------- Help and preferences

        private string _helpQuery = "", _helpTopic;
        private StackPanel _helpBody;

        private FrameworkElement HelpView()
        {
            var sp = new StackPanel();
            sp.Children.Add(Eyebrow("Syntax help"));
            sp.Children.Add(Note("Short pages for each kind of syntax. When a part of the pattern is selected in the Build view, or the caret is inside it, its page comes first."));
            var search = new TextBox { Style = Ui.Style("Input"), Text = _helpQuery, Margin = new Thickness(0, 2, 0, 6) };
            AutomationProperties.SetName(search, "Search the help pages");
            search.TextChanged += (o, e) => { _helpQuery = search.Text; RenderHelp(); };
            sp.Children.Add(search);
            _helpBody = new StackPanel();
            sp.Children.Add(_helpBody);
            sp.Children.Add(PreferencesPanel());
            RenderHelp();
            return sp;
        }

        /// <summary>The help page for the part of the pattern the caret or selection is in, or null.</summary>
        internal RegexHelp.Topic ContextTopic()
        {
            if (string.IsNullOrEmpty(_s.Query)) return null;
            var tree = RegexTree.Parse(_s.Query, _s.EngineOptions & ~(RegexOptions.NonBacktracking | RegexOptions.ECMAScript | RegexOptions.RightToLeft));
            if (tree.Root == null) return null;
            int st = Math.Min(_pattern.SelectionStart, _s.Query.Length), len = Math.Min(_pattern.SelectionLength, _s.Query.Length - st);
            if (len == 0 && st > 0 && st >= _s.Query.Length) st--;   // the caret sits after the last character: use that character
            var node = RegexTree.Smallest(tree.Root, st, Math.Max(len, 1));
            while (node != null && RegexHelp.For(node) == null) node = node.Parent;
            return RegexHelp.For(node);
        }

        private void RenderHelp()
        {
            if (_helpBody == null) return;
            _helpBody.Children.Clear();
            var topics = RegexHelp.Search(_helpQuery);
            var ctx = string.IsNullOrWhiteSpace(_helpQuery) ? ContextTopic() : null;
            if (ctx != null)
            {
                _helpBody.Children.Add(Eyebrow("For the selected part of your pattern"));
                _helpBody.Children.Add(HelpCard(ctx));
            }
            var chips = new WrapPanel();
            foreach (var t in topics)
            {
                var topic = t;
                var chip = Chip(t.Title, "Show the page: " + t.Title, () => { _helpTopic = topic.Id; RenderHelp(); });
                Paint(chip, _helpTopic == t.Id);
                chips.Children.Add(chip);
            }
            _helpBody.Children.Add(Eyebrow("All pages"));
            if (topics.Count == 0) _helpBody.Children.Add(Ui.Mono("No page matches.", Ui.Res("B.TextMute"), 12));
            _helpBody.Children.Add(chips);
            var shown = topics.FirstOrDefault(t => t.Id == _helpTopic);
            if (shown != null) _helpBody.Children.Add(HelpCard(shown));
        }

        private FrameworkElement HelpCard(RegexHelp.Topic t)
        {
            var inner = new StackPanel();
            inner.Children.Add(new TextBlock { Text = t.Title, FontSize = 13, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) });
            inner.Children.Add(Body(t.Body));
            var ex = Ui.Mono(t.Example, Ui.Res("B.Mint"), 12); ex.TextWrapping = TextWrapping.Wrap; Core.Language.SetLiteral(ex, true);
            inner.Children.Add(Eyebrow("Example"));
            inner.Children.Add(ex);
            var use = Ui.Button("Use this example", () => { _s.SetQuery(t.Example, this); if (_sample != null) { _s.Sample = t.Sample; _sample.Text = t.Sample; } else _s.Sample = t.Sample; }, "Btn.Ghost");
            use.ToolTip = "Put the example in the pattern box and its sample text in the Test view";
            use.HorizontalAlignment = HorizontalAlignment.Left;
            inner.Children.Add(use);
            return Boxed(inner);
        }

        private WrapPanel _prefTabs;

        private FrameworkElement PreferencesPanel()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            sp.Children.Add(Eyebrow("Builder preferences"));
            sp.Children.Add(Note("These stay on this computer and apply to every regex builder in the app."));
            var s = Settings.Current;
            var visible = RegexBuilderPopup.Tabs.Where(t => t != "Build" && t != "Help").ToList();
            sp.Children.Add(ChoiceRow("Open on", Tabs.Where(t => !(s.RegexHiddenTabs ?? new List<string>()).Contains(t)), () => s.RegexDefaultTab, v => { s.RegexDefaultTab = v; Settings.Save(); }, "Open the builder on the", v => "Start on " + v));
            sp.Children.Add(ChoiceRow("Width", new[] { "Match the search bar", "Narrow", "Wide", "Extra wide" }, () => WidthName(s.RegexCardWidth), v => { s.RegexCardWidth = WidthValue(v); Settings.Save(); ApplyPreferences(); }, "Make the builder"));
            sp.Children.Add(ChoiceRow("Height", new[] { "Short", "Normal", "Tall" }, () => s.RegexCardMaxHeight <= 460 ? "Short" : s.RegexCardMaxHeight >= 800 ? "Tall" : "Normal", v => { s.RegexCardMaxHeight = v == "Short" ? 420 : v == "Tall" ? 840 : 620; Settings.Save(); ApplyPreferences(); }, "Make the builder"));
            var hidden = new WrapPanel();
            hidden.Children.Add(Label("Views shown"));
            _prefTabs = hidden;
            foreach (var t in visible)
            {
                var name = t;
                Button chip = null;
                chip = Chip("Show " + name, "Show or hide the " + name + " view in the tab strip", () =>
                {
                    var h = s.RegexHiddenTabs ??= new List<string>();
                    if (h.Contains(name)) h.Remove(name); else h.Add(name);
                    if (h.Contains(s.RegexDefaultTab)) s.RegexDefaultTab = "Build";
                    Settings.Save(); Paint(chip, !h.Contains(name)); ApplyPreferences();
                });
                Paint(chip, !(s.RegexHiddenTabs ?? new List<string>()).Contains(name));
                chip.Tag = name;
                hidden.Children.Add(chip);
            }
            sp.Children.Add(hidden);
            var det = new WrapPanel();
            Button detChip = null;
            detChip = Chip("Detailed explanations by default", "Open the Build view with full sentences instead of short labels", () => { s.RegexDetailedByDefault = !s.RegexDetailedByDefault; _detailed = s.RegexDetailedByDefault; Settings.Save(); Paint(detChip, s.RegexDetailedByDefault); });
            Paint(detChip, s.RegexDetailedByDefault);
            det.Children.Add(detChip);
            det.Children.Add(Ui.Button("Reset preferences", ResetPreferences, "Btn.Ghost"));
            sp.Children.Add(det);
            return sp;
        }

        private static string WidthName(int w) => w <= 0 ? "Match the search bar" : w <= 640 ? "Narrow" : w >= 1000 ? "Extra wide" : "Wide";
        private static int WidthValue(string name) => name switch { "Narrow" => 600, "Wide" => 860, "Extra wide" => 1100, _ => 0 };

        private void ResetPreferences()
        {
            var s = Settings.Current;
            s.RegexDefaultTab = "Build"; s.RegexCardWidth = 0; s.RegexCardMaxHeight = 620; s.RegexHiddenTabs = new List<string>(); s.RegexDetailedByDefault = false;
            _detailed = false;
            Settings.Save();
            ApplyPreferences();
            Show("Help");
        }

        /// <summary>Applies the stored width, height and tab choices to the open builder.</summary>
        internal void ApplyPreferences()
        {
            var s = Settings.Current;
            var hidden = s.RegexHiddenTabs ?? new List<string>();
            foreach (Button b in _tabStrip.Children) b.Visibility = hidden.Contains((string)b.Tag) && (string)b.Tag != "Build" && (string)b.Tag != "Help" ? Visibility.Collapsed : Visibility.Visible;
            if (_scroll != null) _scroll.MaxHeight = Math.Clamp(s.RegexCardMaxHeight <= 0 ? 620 : s.RegexCardMaxHeight, 360, 900);
            var min = s.RegexCardWidth > 0 ? Math.Clamp(s.RegexCardWidth, 560, 1100) : Math.Max(_field.Bar.ActualWidth, 680);
            _card.Width = min;
        }

        internal IReadOnlyList<string> VisibleTabs => _tabStrip.Children.OfType<Button>().Where(b => b.Visibility == Visibility.Visible).Select(b => (string)b.Tag).ToList();

        // ---------------------------------------------------------------- Files: presets, history, backups, open at line

        private FrameworkElement FilesExtras()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 4) };
            sp.Children.Add(Eyebrow("Saved searches"));
            var nameRow = new DockPanel();
            var name = new TextBox { Style = Ui.Style("Input"), Text = _gPresetName ?? "", Margin = new Thickness(0, 2, 6, 6) };
            AutomationProperties.SetName(name, "Name for the saved file search");
            name.TextChanged += (o, e) => _gPresetName = name.Text;
            var save = Ui.Button("Save these settings", SaveGrepPreset, "Btn.Ghost");
            save.ToolTip = "Keep the folder, masks, name pattern and options under this name";
            DockPanel.SetDock(save, Dock.Right); nameRow.Children.Add(save); nameRow.Children.Add(name);
            sp.Children.Add(nameRow);
            _gPresets = new StackPanel();
            sp.Children.Add(_gPresets);
            RenderPresets();
            sp.Children.Add(Eyebrow("Backups and history"));
            var bh = new WrapPanel();
            bh.Children.Add(Ui.Button("Choose a backup...", RenderBackups, "Btn.Ghost"));
            bh.Children.Add(Ui.Button("Show history", RenderGrepHistory, "Btn.Ghost"));
            sp.Children.Add(bh);
            _gBackups = new StackPanel();
            _gHistory = new StackPanel();
            sp.Children.Add(_gBackups);
            sp.Children.Add(_gHistory);
            return sp;
        }

        private void SaveGrepPreset()
        {
            var n = (_gPresetName ?? "").Trim();
            if (n.Length == 0) { if (_gStatus != null) { _gStatus.Text = "Give the saved search a name first."; _gStatus.Foreground = Ui.Res("B.Amber"); } return; }
            var list = Settings.Current.RegexGrepPresets ??= new List<RegexGrepPreset>();
            list.RemoveAll(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, new RegexGrepPreset { Name = n, Folder = _gFolder, Masks = _gMasks, ExcludeMasks = _gExclude, NameRegex = _gNameRx, Subfolders = _gSub, SkipBinary = _gBinary });
            if (list.Count > 50) list.RemoveRange(50, list.Count - 50);
            Settings.Save();
            RenderPresets();
        }

        private void RenderPresets()
        {
            if (_gPresets == null) return;
            _gPresets.Children.Clear();
            var list = Settings.Current.RegexGrepPresets ?? new List<RegexGrepPreset>();
            if (list.Count == 0) { _gPresets.Children.Add(Ui.Mono("Nothing saved yet.", Ui.Res("B.TextMute"), 11)); return; }
            foreach (var p in list.ToList())
            {
                var preset = p;
                var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
                var del = Ui.Button("Remove", () => { Settings.Current.RegexGrepPresets.Remove(preset); Settings.Save(); RenderPresets(); }, "Btn.Ghost");
                DockPanel.SetDock(del, Dock.Right); row.Children.Add(del);
                var use = Ui.Button("Load", () => { LoadGrepPreset(preset); }, "Btn.Ghost");
                DockPanel.SetDock(use, Dock.Right); row.Children.Add(use);
                var t = new TextBlock { Text = preset.Name, VerticalAlignment = VerticalAlignment.Center, Foreground = Ui.Res("B.Text"), FontSize = 12, FontFamily = Ui.Font("F.Body") };
                Core.Language.SetLiteral(t, true);
                row.Children.Add(t);
                _gPresets.Children.Add(row);
            }
        }

        /// <summary>Puts a saved search back into the Files view and redraws it.</summary>
        internal void LoadGrepPreset(RegexGrepPreset p)
        {
            _gFolder = p.Folder; _gMasks = p.Masks; _gExclude = p.ExcludeMasks; _gNameRx = p.NameRegex ?? ""; _gSub = p.Subfolders; _gBinary = p.SkipBinary; _gPresetName = p.Name;
            Show("Files");
        }

        private void RenderBackups()
        {
            if (_gBackups == null) return;
            _gBackups.Children.Clear();
            var list = RegexGrep.ListBackups();
            if (list.Count == 0) { _gBackups.Children.Add(Ui.Mono("No backups yet. A replace in files creates one.", Ui.Res("B.TextMute"), 11)); return; }
            foreach (var b in list.Take(20))
            {
                var info = b;
                var btn = Ui.Button($"Restore {info.Name} · {info.Files} file(s)", () => StartRestore(info.Folder), "Btn.Row");
                btn.HorizontalContentAlignment = HorizontalAlignment.Left; btn.FontFamily = Ui.Font("F.Mono"); btn.FontSize = 11;
                btn.ToolTip = "Copy the files of this replace run back over their originals, after a confirmation";
                _gBackups.Children.Add(btn);
            }
        }

        private void RecordGrepHistory(RegexGrep.Report rep)
        {
            var list = Settings.Current.RegexGrepHistory ??= new List<RegexGrepRecord>();
            list.Insert(0, new RegexGrepRecord { When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Folder = _gFolder, Pattern = _gQuery ?? "", FilesScanned = rep.FilesScanned, FilesWithMatches = rep.FilesWithHits, Matches = rep.TotalMatches });
            if (list.Count > 20) list.RemoveRange(20, list.Count - 20);
            Settings.Save();
        }

        private void RenderGrepHistory()
        {
            if (_gHistory == null) return;
            _gHistory.Children.Clear();
            var list = Settings.Current.RegexGrepHistory ?? new List<RegexGrepRecord>();
            if (list.Count == 0) { _gHistory.Children.Add(Ui.Mono("No searches yet.", Ui.Res("B.TextMute"), 11)); return; }
            foreach (var h in list.Take(10))
            {
                var rec = h;
                var btn = Ui.Button($"{rec.When} · {rec.Pattern} · {rec.Matches:N0} match(es) in {rec.FilesWithMatches} of {rec.FilesScanned} file(s)", () => { _gFolder = rec.Folder; _s.SetQuery(rec.Pattern, this); Show("Files"); }, "Btn.Row");
                btn.HorizontalContentAlignment = HorizontalAlignment.Left; btn.FontFamily = Ui.Font("F.Mono"); btn.FontSize = 11;
                btn.ToolTip = "Load this folder and pattern again";
                Core.Language.SetLiteral(btn, true);
                _gHistory.Children.Add(btn);
            }
        }

        private void OpenHit(string path, RegexGrep.Hit h)
        {
            if (!ExternalEditor.OpenAtLine(path, h.Line, h.Column, out var message))
                App.Toasts?.ShowNotice("Open in editor", message, App.Toasts.Amber, 6, icon: Icons.Code);
            else if (_gStatus != null) { _gStatus.Text = message; _gStatus.Foreground = Ui.Res("B.TextSoft"); }
        }

        // ---------------------------------------------------------------- Build: moving pieces of the pattern

        /// <summary>What dropping one tree row onto another does: the dragged part takes the place of the one it landed on, in the direction it travelled.</summary>
        internal bool DropNode(TreeNode moving, TreeNode target)
        {
            if (moving == null || target == null || moving.Parent == null || moving.Parent != target.Parent) { SetTreeStatus("Parts can only be swapped with a neighbour at the same level.", true); return false; }
            int a = moving.Parent.Children.IndexOf(moving), b = moving.Parent.Children.IndexOf(target);
            return ReorderNodes(moving, target, a < b);
        }

        /// <summary>Moves a piece of the pattern next to a sibling piece and puts the result in the pattern box. Returns false with a status when it cannot.</summary>
        internal bool ReorderNodes(TreeNode moving, TreeNode target, bool after)
        {
            var result = RegexTree.Reorder(_s.Query, moving, target, after, _s.EngineOptions);
            if (result == null) { SetTreeStatus("That move is not possible: the parts must be neighbours at the same level, and the result must still be a valid pattern.", true); return false; }
            _s.SetQuery(result, null);
            SetTreeStatus("Moved " + Snippet(moving.Text(_tree.Pattern), 24) + (after ? " after " : " before ") + Snippet(target.Text(_tree.Pattern), 24) + ".", false);
            return true;
        }
    }
}
