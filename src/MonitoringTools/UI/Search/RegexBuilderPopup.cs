using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Search
{
    /// <summary>
    /// The regex builder for one search field, anchored directly under it. Build, Test, Replace,
    /// Code and Library views share one pattern box that stays in step with the field.
    /// </summary>
    public sealed class RegexBuilderPopup
    {
        public static readonly string[] Tabs = { "Build", "Test", "Replace", "Code", "Library" };

        private readonly SearchField _field;
        private readonly SearchState _s;
        private readonly Popup _popup;
        private readonly Border _card;
        private readonly TextBox _pattern;
        private readonly TextBlock _status, _engine;
        private readonly Border _risk;
        private readonly TextBlock _riskText;
        private readonly WrapPanel _options, _tabStrip;
        private readonly ContentControl _body;
        private string _tab = "Build";
        private bool _syncing;
        private DateTime _closedAt;

        // per-view state that survives tab switches
        private TextBox _sample, _replacement, _replacePreview, _code;
        private StackPanel _matchList, _groupTable, _explain;
        private TextBlock _testSummary;
        private bool _split;
        private string _language = "C#";
        private int _selected;

        public event Action Closed;
        /// <summary>The builder's surface, for the screenshot run, which renders popups separately from their window.</summary>
        public FrameworkElement Card => _card;
        public bool IsOpen => _popup.IsOpen;

        public RegexBuilderPopup(SearchField field)
        {
            _field = field;
            _s = field.State;

            var title = new TextBlock { Text = "Regex builder", Style = Ui.Style("T.Title"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
            _engine = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            _engine.VerticalAlignment = VerticalAlignment.Center; _engine.Margin = new Thickness(12, 0, 0, 0); _engine.TextWrapping = TextWrapping.Wrap;
            var close = Ui.Button("", Close, "Btn.Icon", Icons.Close);
            AutomationProperties.SetName(close, "Close the regex builder");
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(close, Dock.Right); head.Children.Add(close);
            DockPanel.SetDock(title, Dock.Left); head.Children.Add(title);
            head.Children.Add(_engine);

            _pattern = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), FontSize = 14, Margin = new Thickness(0), AcceptsReturn = false, Text = _s.Query };
            AutomationProperties.SetName(_pattern, "Pattern for " + field.Label + ", kept in step with the search box");
            _pattern.TextChanged += (o, e) => { if (_syncing) return; _s.SetQuery(_pattern.Text, this); };

            _status = new TextBlock { FontSize = 12, Margin = new Thickness(2, 8, 0, 0), TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body") };
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
            _riskText = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.Amber"), TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body") };
            _risk = new Border { Background = Ui.Res("B.AmberWash"), BorderBrush = Ui.Res("B.Amber"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 8, 0, 0), Child = _riskText, Visibility = Visibility.Collapsed };

            _options = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            foreach (var (flag, label, help) in RegexKit.OptionDefs)
            {
                var f = flag;
                var chip = Chip(label, help, () => _s.SetOption(f, (_s.Options & f) == 0, this));
                chip.Tag = f;
                _options.Children.Add(chip);
            }

            _tabStrip = new WrapPanel { Margin = new Thickness(0, 12, 0, 10) };
            foreach (var t in Tabs)
            {
                var name = t;
                var b = Chip(t, "Show the " + t + " view", () => Show(name));
                b.Tag = t;
                _tabStrip.Children.Add(b);
            }
            _body = new ContentControl { Focusable = false };

            var plain = Ui.Button("Back to plain text", () => { _s.SetRegex(false, this); Close(); }, "Btn.Ghost");
            var done = Ui.Button("Done", Close, "Btn.Accent");
            done.Margin = new Thickness(8, 0, 0, 0);
            var hint = Ui.Mono("Esc closes and returns to the search box. Patterns stay on this computer.", Ui.Res("B.TextMute"), 11);
            hint.VerticalAlignment = VerticalAlignment.Center; hint.TextWrapping = TextWrapping.Wrap;
            var foot = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            right.Children.Add(plain); right.Children.Add(done);
            DockPanel.SetDock(right, Dock.Right); foot.Children.Add(right);
            foot.Children.Add(hint);

            var stack = new StackPanel();
            stack.Children.Add(head);
            stack.Children.Add(_pattern);
            stack.Children.Add(_status);
            stack.Children.Add(_risk);
            stack.Children.Add(_options);
            stack.Children.Add(_tabStrip);
            stack.Children.Add(_body);
            stack.Children.Add(foot);

            _card = new Border
            {
                Background = Ui.Res("B.Surface"), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18), Margin = new Thickness(0, 0, 24, 24),
                Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"],
                Child = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 620 }
            };
            AutomationProperties.SetName(_card, "Regex builder for " + field.Label);
            _card.PreviewKeyDown += (o, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };

            _popup = new Popup
            {
                Child = _card, PlacementTarget = field.Bar, Placement = PlacementMode.Bottom, VerticalOffset = 8,
                StaysOpen = false, AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade, Focusable = true
            };
            _popup.Closed += (o, e) => { _closedAt = DateTime.UtcNow; _s.Remember(); Closed?.Invoke(); };
            _s.Changed += src => { if (_popup.IsOpen) Refresh(src); };
        }

        public void Open(string tab = null)
        {
            if (_popup.IsOpen) { if (tab != null) Show(tab); return; }
            if ((DateTime.UtcNow - _closedAt).TotalMilliseconds < 250) return; // the click that closed it should not reopen it
            _card.Width = Math.Max(_field.Bar.ActualWidth, 680);
            _popup.IsOpen = true;
            Show(tab ?? _tab);
            Refresh(null);
            _pattern.Focus();
            _pattern.CaretIndex = _pattern.Text.Length;
        }

        public void Close() { if (_popup.IsOpen) _popup.IsOpen = false; }

        // ---------------------------------------------------------------- shared header

        private void Refresh(object source)
        {
            if (source != this && _pattern.Text != _s.Query)
            {
                _syncing = true;
                var caret = _pattern.CaretIndex;
                _pattern.Text = _s.Query;
                _pattern.CaretIndex = Math.Min(caret, _pattern.Text.Length);
                _syncing = false;
            }
            _engine.Text = RegexKit.EngineLabel(_s.EngineOptions);
            if (_s.Query.Length == 0) { _status.Text = "Type a pattern, or pick a token below."; _status.Foreground = Ui.Res("B.TextMute"); }
            else if (_s.Error != null) { _status.Text = "Not valid: " + _s.Error; _status.Foreground = Ui.Res("B.Amber"); }
            else { _status.Text = "Valid pattern · " + (_s.Regex?.GetGroupNumbers().Length - 1 ?? 0) + " capturing group(s)"; _status.Foreground = Ui.Res("B.Mint"); }
            _pattern.BorderBrush = _s.Error != null ? Ui.Res("B.Amber") : Ui.Res("B.LineStrong");
            var risk = RegexKit.Risk(_s.Query, _s.EngineOptions);
            _riskText.Text = risk ?? "";
            _risk.Visibility = risk == null ? Visibility.Collapsed : Visibility.Visible;
            foreach (Button b in _options.Children) Paint(b, (_s.Options & (RegexOptions)b.Tag) != 0);
            switch (_tab)
            {
                case "Build": RenderExplain(); break;
                case "Test": RunTest(); break;
                case "Replace": RunReplace(); break;
                case "Code": if (_code != null) _code.Text = RegexKit.Code(_language, _s.Query, _s.EngineOptions); break;
            }
        }

        private void Show(string tab)
        {
            _tab = tab;
            foreach (Button b in _tabStrip.Children) Paint(b, (string)b.Tag == tab);
            _body.Content = tab switch
            {
                "Test" => TestView(),
                "Replace" => ReplaceView(),
                "Code" => CodeView(),
                "Library" => LibraryView(),
                _ => BuildView(),
            };
            Refresh(null);
        }

        // ---------------------------------------------------------------- Build

        private static readonly (string Group, (string Token, string Help)[] Items)[] Palette =
        {
            ("Characters", new[] { (@"\d", "a digit"), (@"\w", "a word character"), (@"\s", "whitespace"), (".", "any character"), (@"\p{L}", "a letter in any script"), (@"\p{Lu}", "an uppercase letter"), (@"\u00E9", "a code point"), (@"\t", "a tab"), (@"\.", "a literal dot") }),
            ("Sets", new[] { ("[abc]", "one of"), ("[^abc]", "none of"), ("[a-z]", "a range"), ("[a-z-[aeiou]]", "a set with subtraction") }),
            ("Anchors", new[] { ("^", "start"), ("$", "end"), (@"\b", "word boundary"), (@"\A", "start of text"), (@"\z", "end of text"), (@"\G", "where the last match ended") }),
            ("Groups", new[] { ("()", "numbered group"), ("(?<name>)", "named group"), ("(?:)", "group that does not capture"), ("(?>)", "atomic group"), (@"\1", "backreference to group 1"), (@"\k<name>", "backreference by name"), ("(?(1)yes|no)", "conditional"), ("|", "or") }),
            ("Repeat", new[] { ("*", "zero or more"), ("+", "one or more"), ("?", "optional"), ("{2}", "exactly 2"), ("{2,5}", "2 to 5"), ("{2,}", "2 or more"), ("*?", "lazy"), ("+?", "lazy one or more") }),
            ("Look around", new[] { ("(?=)", "followed by"), ("(?!)", "not followed by"), ("(?<=)", "preceded by"), ("(?<!)", "not preceded by") }),
            ("Inline options", new[] { ("(?i)", "ignore case from here"), ("(?m)", "multiline from here"), ("(?s)", "singleline from here"), ("(?x)", "ignore pattern whitespace"), ("(?#note)", "comment") }),
        };

        private FrameworkElement BuildView()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            var left = new StackPanel();
            foreach (var (group, items) in Palette)
            {
                left.Children.Add(Eyebrow(group));
                var wrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
                foreach (var (token, help) in items)
                {
                    var t = token;
                    var b = Chip(token, help, () => Insert(t));
                    b.FontFamily = Ui.Font("F.Mono");
                    wrap.Children.Add(b);
                }
                left.Children.Add(wrap);
            }
            var literal = new TextBox { Style = Ui.Style("Input"), Margin = new Thickness(0, 4, 0, 6) };
            AutomationProperties.SetName(literal, "Literal text to escape and insert");
            left.Children.Add(Eyebrow("Insert literal text"));
            var lit = new DockPanel();
            var add = Ui.Button("Escape and insert", () => { Insert(RegexKit.Escape(literal.Text)); literal.Clear(); }, "Btn.Ghost");
            add.Margin = new Thickness(8, 4, 0, 6);
            DockPanel.SetDock(add, Dock.Right); lit.Children.Add(add); lit.Children.Add(literal);
            left.Children.Add(lit);
            grid.Children.Add(left);

            var right = new StackPanel();
            right.Children.Add(Eyebrow("Explanation, token by token"));
            _explain = new StackPanel();
            right.Children.Add(new Border
            {
                Background = Ui.Res("B.Sunken"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 4, 0, 0),
                Child = new ScrollViewer { Content = _explain, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
            });
            Grid.SetColumn(right, 2); grid.Children.Add(right);
            return grid;
        }

        private void RenderExplain()
        {
            if (_explain == null) return;
            _explain.Children.Clear();
            var tokens = RegexKit.Explain(_s.Query);
            if (tokens.Count == 0) { _explain.Children.Add(Ui.Mono("Nothing to explain yet.", Ui.Res("B.TextMute"), 12)); return; }
            foreach (var t in tokens)
            {
                var row = new DockPanel { Margin = new Thickness(t.Depth * 14, 2, 0, 2) };
                var code = Ui.Mono(t.Text, Ui.Res("B.Mint"), 12);
                code.MinWidth = 72; code.Margin = new Thickness(0, 0, 10, 0);
                DockPanel.SetDock(code, Dock.Left); row.Children.Add(code);
                row.Children.Add(new TextBlock { Text = t.Meaning, Foreground = Ui.Res("B.TextSoft"), FontSize = 12, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body") });
                _explain.Children.Add(row);
            }
        }

        /// <summary>Inserts a token at the caret; group tokens wrap the current selection.</summary>
        private void Insert(string token)
        {
            var text = _pattern.Text;
            int start = _pattern.SelectionStart, len = _pattern.SelectionLength;
            string sel = text.Substring(start, len), insert;
            int caret;
            int close = token.EndsWith(")", StringComparison.Ordinal) && token.StartsWith("(", StringComparison.Ordinal) && !token.Contains('|') && !token.StartsWith("(?i", StringComparison.Ordinal) && !token.StartsWith("(?m", StringComparison.Ordinal) && !token.StartsWith("(?s", StringComparison.Ordinal) && !token.StartsWith("(?x", StringComparison.Ordinal) && !token.StartsWith("(?#", StringComparison.Ordinal) ? token.Length - 1 : -1;
            if (close >= 0) { insert = token[..close] + sel + ")"; caret = start + close + sel.Length; }
            else { insert = token; caret = start + token.Length; }
            _pattern.Text = text.Remove(start, len).Insert(start, insert);
            _pattern.CaretIndex = caret;
            _pattern.Focus();
        }

        // ---------------------------------------------------------------- Test

        private FrameworkElement TestView()
        {
            var sp = new StackPanel();
            var head = new DockPanel();
            var fill = Ui.Button("Use this list as the sample", () => { _sample.Text = DefaultSample(); }, "Btn.Ghost");
            DockPanel.SetDock(fill, Dock.Right); head.Children.Add(fill);
            head.Children.Add(Eyebrow("Sample text"));
            sp.Children.Add(head);
            _sample = new TextBox
            {
                Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), FontSize = 12, AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.Wrap,
                Height = 130, VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 6, 0, 8),
                Text = _s.Sample ?? DefaultSample(), SelectionBrush = Ui.Res("B.Mint")
            };
            AutomationProperties.SetName(_sample, "Sample text to test the pattern against");
            _sample.TextChanged += (o, e) => { _s.Sample = _sample.Text; RunTest(); };
            sp.Children.Add(_sample);
            _testSummary = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            sp.Children.Add(_testSummary);
            var cols = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            cols.ColumnDefinitions.Add(new ColumnDefinition());
            cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            cols.ColumnDefinitions.Add(new ColumnDefinition());
            _matchList = new StackPanel();
            _groupTable = new StackPanel();
            var l = new StackPanel(); l.Children.Add(Eyebrow("Matches")); l.Children.Add(new ScrollViewer { Content = _matchList, MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var r = new StackPanel(); r.Children.Add(Eyebrow("Groups of the selected match")); r.Children.Add(_groupTable);
            cols.Children.Add(l); Grid.SetColumn(r, 2); cols.Children.Add(r);
            sp.Children.Add(cols);
            return sp;
        }

        private string DefaultSample()
        {
            var items = _field.SampleSource?.Invoke()?.Where(x => !string.IsNullOrWhiteSpace(x)).Take(200).ToList() ?? new List<string>();
            return items.Count == 0 ? "Invoice 2026-10-31 sent\nBackup finished 2026-11-03 at 22:14" : string.Join("\n", items.Select(x => x.Replace("\r", " ").Replace("\n", " ")));
        }

        private void RunTest()
        {
            if (_sample == null || _matchList == null) return;
            _matchList.Children.Clear(); _groupTable.Children.Clear();
            var rx = _s.Regex;
            if (rx == null) { _testSummary.Text = _s.Error != null ? "Fix the pattern to test it." : "Type a pattern to test it."; return; }
            var run = RegexKit.Test(rx, _sample.Text);
            _testSummary.Text = $"{run.Matches.Count} match(es) · {run.Milliseconds:0.00} ms measured" + (run.TimedOut ? " · stopped at the 200 ms limit" : "") + (run.Truncated ? " · capped" : "");
            _testSummary.Foreground = run.TimedOut ? Ui.Res("B.Amber") : Ui.Res("B.TextMute");
            if (_selected >= run.Matches.Count) _selected = 0;
            for (int i = 0; i < run.Matches.Count; i++)
            {
                var m = run.Matches[i];
                var idx = i;
                var label = m.Length == 0 ? $"#{i + 1} at {m.Index}: empty match" : $"#{i + 1} at {m.Index}: {Shorten(m.Value)}";
                var b = Ui.Button(label, () => { _selected = idx; Select(run); }, "Btn.Row");
                b.HorizontalContentAlignment = HorizontalAlignment.Left; b.FontFamily = Ui.Font("F.Mono"); b.FontSize = 12; b.Margin = new Thickness(0, 0, 0, 2);
                if (i == _selected) b.Background = Ui.Res("B.MintWash");
                _matchList.Children.Add(b);
            }
            if (run.Matches.Count == 0) _matchList.Children.Add(Ui.Mono("No match in the sample.", Ui.Res("B.TextMute"), 12));
            else Groups(run.Matches[_selected]);
        }

        private void Select(RegexKit.TestRun run)
        {
            var m = run.Matches[_selected];
            _sample.Focus();
            _sample.Select(m.Index, m.Length);
            RunTest();
        }

        private void Groups(RegexKit.MatchHit m)
        {
            foreach (var g in m.Groups)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 16, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(Ui.Mono(g.Name, Ui.Res("B.TextSoft"), 12));
                var v = Ui.Mono(g.Success ? Shorten(g.Value) : "did not take part", Ui.Res(g.Success ? "B.Mint" : "B.TextMute"), 12);
                Grid.SetColumn(v, 1); row.Children.Add(v);
                var at = Ui.Mono(g.Success ? $"{g.Index}–{g.Index + g.Length}" : "", Ui.Res("B.TextMute"), 11);
                Grid.SetColumn(at, 2); row.Children.Add(at);
                _groupTable.Children.Add(row);
            }
        }

        private static string Shorten(string s)
        {
            s = (s ?? "").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            return s.Length > 60 ? s[..60] + "…" : s;
        }

        // ---------------------------------------------------------------- Replace and split

        private FrameworkElement ReplaceView()
        {
            var sp = new StackPanel();
            var modes = new WrapPanel();
            var rep = Chip("Replace", "Preview a replacement", () => { _split = false; Show("Replace"); });
            var spl = Chip("Split", "Preview splitting the sample at every match", () => { _split = true; Show("Replace"); });
            Paint(rep, !_split); Paint(spl, _split);
            modes.Children.Add(rep); modes.Children.Add(spl);
            sp.Children.Add(modes);
            if (!_split)
            {
                sp.Children.Add(Eyebrow("Replacement"));
                _replacement = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Margin = new Thickness(0, 4, 0, 6), Text = _s.Replacement };
                AutomationProperties.SetName(_replacement, "Replacement text");
                _replacement.TextChanged += (o, e) => { _s.Replacement = _replacement.Text; RunReplace(); };
                sp.Children.Add(_replacement);
                var tokens = new WrapPanel();
                var names = _s.Regex?.GetGroupNames().Where(n => !int.TryParse(n, out _)).Select(n => "${" + n + "}") ?? Enumerable.Empty<string>();
                foreach (var t in new[] { "$0", "$1", "$2" }.Concat(names).Concat(new[] { "$$", "$`", "$'", "$_" }))
                {
                    var tok = t;
                    var b = Chip(t, t switch { "$0" => "the whole match", "$$" => "a dollar sign", "$`" => "the text before the match", "$'" => "the text after the match", "$_" => "the whole input", _ => "a group" },
                        () => { var c = _replacement.CaretIndex; _replacement.Text = _replacement.Text.Insert(c, tok); _replacement.CaretIndex = c + tok.Length; _replacement.Focus(); });
                    b.FontFamily = Ui.Font("F.Mono");
                    tokens.Children.Add(b);
                }
                sp.Children.Add(tokens);
            }
            sp.Children.Add(Eyebrow(_split ? "Pieces, one per line. Nothing is changed until you copy them." : "Preview of the test sample. Nothing is changed until you copy it."));
            _replacePreview = new TextBox
            {
                Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), FontSize = 12, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                Height = 130, VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 4, 0, 8)
            };
            AutomationProperties.SetName(_replacePreview, "Result preview");
            sp.Children.Add(_replacePreview);
            var copy = Ui.Button("Copy result", () => { if (!string.IsNullOrEmpty(_replacePreview.Text)) ClipboardWatcher.SetText(_replacePreview.Text); }, "Btn.Ghost");
            copy.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(copy);
            return sp;
        }

        private void RunReplace()
        {
            if (_replacePreview == null) return;
            var rx = _s.Regex;
            var sample = _s.Sample ?? DefaultSample();
            if (rx == null) { _replacePreview.Text = _s.Error != null ? "Fix the pattern first: " + _s.Error : "Type a pattern first."; return; }
            if (_split)
            {
                var parts = RegexKit.SplitPreview(rx, sample, out var err);
                _replacePreview.Text = err ?? string.Join("\n", parts.Select((p, i) => $"[{i}] {p}"));
            }
            else
            {
                var res = RegexKit.ReplacePreview(rx, sample, _s.Replacement, out var err);
                _replacePreview.Text = err ?? res;
            }
        }

        // ---------------------------------------------------------------- Code

        private FrameworkElement CodeView()
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
            _code = new TextBox
            {
                Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), FontSize = 12, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                MinHeight = 110, VerticalContentAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 8), Text = RegexKit.Code(_language, _s.Query, _s.EngineOptions)
            };
            AutomationProperties.SetName(_code, "Generated code");
            sp.Children.Add(_code);
            var copy = Ui.Button("Copy code", () => ClipboardWatcher.SetText(_code.Text), "Btn.Ghost");
            copy.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(copy);
            return sp;
        }

        // ---------------------------------------------------------------- Library

        private FrameworkElement LibraryView()
        {
            var sp = new StackPanel();
            var lib = Settings.Current.SavedPatterns;
            sp.Children.Add(Eyebrow("Save the current pattern"));
            var name = new TextBox { Style = Ui.Style("Input"), Margin = new Thickness(0, 4, 0, 6) };
            AutomationProperties.SetName(name, "Name for the saved pattern");
            var save = Ui.Button("Save", () =>
            {
                if (_s.Query.Length == 0 || _s.Error != null) return;
                var n = string.IsNullOrWhiteSpace(name.Text) ? _s.Query : name.Text.Trim();
                lib.RemoveAll(p => p.Name == n);
                lib.Insert(0, new SavedPattern { Name = n, Pattern = _s.Query, Options = (int)_s.EngineOptions, Replacement = _s.Replacement ?? "" });
                Settings.Save();
                Show("Library");
            }, "Btn.Accent");
            save.Margin = new Thickness(8, 4, 0, 6);
            var row = new DockPanel();
            DockPanel.SetDock(save, Dock.Right); row.Children.Add(save); row.Children.Add(name);
            sp.Children.Add(row);

            sp.Children.Add(Eyebrow($"Saved · {lib.Count}"));
            if (lib.Count == 0) sp.Children.Add(Ui.Mono("Nothing saved yet.", Ui.Res("B.TextMute"), 12));
            foreach (var p in lib.ToList())
            {
                var item = p;
                var line = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                var del = Ui.Button("Remove", () => { lib.Remove(item); Settings.Save(); Show("Library"); }, "Btn.Ghost");
                del.Margin = new Thickness(6, 0, 0, 0);
                var use = Ui.Button("Use", () => _s.Load(item, this), "Btn.Ghost");
                use.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(del, Dock.Right); line.Children.Add(del);
                DockPanel.SetDock(use, Dock.Right); line.Children.Add(use);
                var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock { Text = item.Name, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body") });
                text.Children.Add(Ui.Mono(item.Pattern, Ui.Res("B.Mint"), 11));
                line.Children.Add(text);
                sp.Children.Add(line);
            }

            sp.Children.Add(Eyebrow("Recent in this search box"));
            if (_s.Recent.Count == 0) sp.Children.Add(Ui.Mono("Patterns you use here appear while the app runs.", Ui.Res("B.TextMute"), 12));
            var recent = new WrapPanel();
            foreach (var r in _s.Recent)
            {
                var q = r;
                var b = Chip(r, "Use this pattern again", () => _s.SetQuery(q, this));
                b.FontFamily = Ui.Font("F.Mono");
                recent.Children.Add(b);
            }
            sp.Children.Add(recent);

            var io = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            io.Children.Add(Ui.Button("Export JSON", Export, "Btn.Ghost"));
            var imp = Ui.Button("Import JSON", Import, "Btn.Ghost");
            imp.Margin = new Thickness(8, 0, 0, 0);
            io.Children.Add(imp);
            sp.Children.Add(io);
            return sp;
        }

        private void Export()
        {
            var d = new Microsoft.Win32.SaveFileDialog { FileName = "regex-library.json", Filter = "JSON|*.json" };
            if (d.ShowDialog() != true) return;
            File.WriteAllText(d.FileName, JsonSerializer.Serialize(Settings.Current.SavedPatterns, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void Import()
        {
            var d = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.json" };
            if (d.ShowDialog() != true) return;
            try
            {
                var info = new FileInfo(d.FileName);
                if (info.Length > 1024 * 1024) throw new InvalidDataException("The file is larger than 1 MB.");
                var list = JsonSerializer.Deserialize<List<SavedPattern>>(File.ReadAllText(d.FileName)) ?? new List<SavedPattern>();
                foreach (var p in list.Where(p => !string.IsNullOrEmpty(p.Pattern) && p.Pattern.Length <= RegexKit.MaxPatternLength).Take(500))
                {
                    Settings.Current.SavedPatterns.RemoveAll(x => x.Name == p.Name);
                    Settings.Current.SavedPatterns.Add(p);
                }
                Settings.Save();
            }
            catch (Exception ex) { Log.Warn("Regex library import failed: " + ex.Message); }
            Show("Library");
        }

        // ---------------------------------------------------------------- bits

        private static TextBlock Eyebrow(string text) =>
            new TextBlock { Text = text.ToUpperInvariant(), Style = Ui.Style("T.Eyebrow"), FontSize = 11, Margin = new Thickness(0, 6, 0, 2) };

        private static Button Chip(string label, string help, Action click)
        {
            var b = Ui.Button(label, click, "Btn.Chip");
            b.ToolTip = help;
            b.Margin = new Thickness(0, 0, 6, 6);
            AutomationProperties.SetHelpText(b, help ?? "");
            Paint(b, false);
            return b;
        }

        private static void Paint(Button b, bool on)
        {
            b.Background = on ? Ui.Res("B.MintWash") : Ui.Res("B.Raised");
            b.Foreground = on ? Ui.Res("B.Text") : Ui.Res("B.TextSoft");
            b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
        }
    }
}
