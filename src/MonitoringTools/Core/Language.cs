using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The language layer: English, Hong Kong Cantonese or both, applied live to every window,
    /// plus the humour-level headline strings. It works the same way as the personal wording layer:
    /// it remembers each string's English original, so switching back restores it exactly, and it
    /// cooperates with that layer by applying the language first and the personal wording after.
    /// </summary>
    public static class Language
    {
        public static event Action Changed;

        /// <summary>
        /// The language in effect: the saved choice, unless a schedule rule or the settings file overrides it, and always
        /// English while School mode is on.
        /// </summary>
        public static LanguageMode Mode
        {
            get
            {
                switch (Modes.ScheduledLanguage)
                {
                    case "en": return LanguageMode.English;
                    case "yue": return LanguageMode.Cantonese;
                    case "bilingual": return LanguageMode.Bilingual;
                    default: return Settings.Current.LanguageMode;
                }
            }
        }
        public static bool IsEnglish => Mode == LanguageMode.English;
        /// <summary>English with no personal wording: the layer has nothing to do and leaves text alone.</summary>
        private static bool Plain => IsEnglish && !PersonalWording.Active;

        private static LanguageMode _lastMode;
        private static bool _lastSchool;

        // A mode change (School mode, a schedule rule, the settings file) can change the effective language: re-apply it live.
        private static void OnModesChanged()
        {
            var mode = Mode; var school = Modes.SchoolActive;
            if (mode == _lastMode && school == _lastSchool) return;
            bool schoolChanged = school != _lastSchool;
            _lastMode = mode; _lastSchool = school;
            if (schoolChanged) PersonalWording.Reapply();
            Refresh();
        }

        private static bool _installed;

        // The English original, the language-only text and the final on-screen text of a Run.
        private static readonly DependencyProperty EnProp = DependencyProperty.RegisterAttached("En", typeof(string), typeof(Language));
        private static readonly DependencyProperty BaseProp = DependencyProperty.RegisterAttached("Base", typeof(string), typeof(Language));
        private static readonly DependencyProperty FinalProp = DependencyProperty.RegisterAttached("Final", typeof(string), typeof(Language));
        private static readonly DependencyProperty TipEnProp = DependencyProperty.RegisterAttached("TipEn", typeof(string), typeof(Language));
        private static readonly DependencyProperty TipFinalProp = DependencyProperty.RegisterAttached("TipFinal", typeof(string), typeof(Language));
        private static readonly DependencyProperty TitleEnProp = DependencyProperty.RegisterAttached("TitleEn", typeof(string), typeof(Language));
        private static readonly DependencyProperty TitleFinalProp = DependencyProperty.RegisterAttached("TitleFinal", typeof(string), typeof(Language));
        private static readonly DependencyProperty NameEnProp = DependencyProperty.RegisterAttached("NameEn", typeof(string), typeof(Language));
        private static readonly DependencyProperty NameFinalProp = DependencyProperty.RegisterAttached("NameFinal", typeof(string), typeof(Language));
        private static readonly DependencyProperty HelpEnProp = DependencyProperty.RegisterAttached("HelpEn", typeof(string), typeof(Language));
        private static readonly DependencyProperty HelpFinalProp = DependencyProperty.RegisterAttached("HelpFinal", typeof(string), typeof(Language));

        // The personal wording layer keeps each Run's pre-wording text in a private property; we keep it
        // in step with the language text so that layer always starts from the right base.
        private static readonly DependencyProperty WordingOriginal = FindWordingOriginal();

        private static DependencyProperty FindWordingOriginal()
        {
            try
            {
                var t = Type.GetType("MonitoringTools.UI.WordingLayer");
                return t?.GetField("OriginalProperty", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as DependencyProperty;
            }
            catch { return null; }
        }

        /// <summary>Call once at start-up, after Settings.Load and WordingLayer.Install.</summary>
        public static void Initialize()
        {
            var s = Settings.Current;
            s.EnglishHumour = Math.Clamp(s.EnglishHumour, 1, 5);
            s.CantoneseHumour = Math.Clamp(s.CantoneseHumour, 1, 5);
            s.NarratorRate = Math.Clamp(s.NarratorRate, 0.5, 2.0);
            s.EnglishVoiceId ??= ""; s.CantoneseVoiceId ??= "";
            if (!Enum.IsDefined(typeof(LanguageMode), s.LanguageMode)) s.LanguageMode = LanguageMode.English;
            if (_installed) return;
            _installed = true;

            EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.LoadedEvent, new RoutedEventHandler((e, a) => Rewrite((TextBlock)e)));
            // WPF raises Loaded only on elements that have their own Loaded handler, so the class handler above misses most
            // labels added after a window opened (every page the control centre builds on a tab switch, cards, dialogs).
            // Each window instead marks itself when its layout changes, and a short coalescing timer re-applies the language
            // and the personal wording to marked windows. Rewriting is idempotent, so a pass over finished text changes nothing.
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((e, a) => Watch((Window)e)));
            _sweep = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
            _sweep.Tick += (s2, e2) => Sweep();
            _sweep.Start();
            PersonalWording.Changed += () => Refresh();
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((e, a) => Walk((Window)e)));
            Narrator.Attach();
            _lastMode = Mode; _lastSchool = Modes.SchoolActive;
            Modes.Changed += OnModesChanged;
            Refresh();
        }

        private static System.Windows.Threading.DispatcherTimer _sweep;
        private static readonly HashSet<Window> _dirty = new HashSet<Window>();
        private static readonly DependencyProperty WatchedProp = DependencyProperty.RegisterAttached("Watched", typeof(bool), typeof(Language), new PropertyMetadata(false));

        private static void Watch(Window w)
        {
            if ((bool)w.GetValue(WatchedProp)) return;
            w.SetValue(WatchedProp, true);
            w.LayoutUpdated += (s, e) => _dirty.Add(w);
            w.Closed += (s, e) => _dirty.Remove(w);
            _dirty.Add(w);
        }

        /// <summary>Re-applies the language and wording to every window whose layout changed since the last pass.</summary>
        internal static void Sweep()
        {
            if (_dirty.Count == 0) return;
            if (Plain) { _dirty.Clear(); return; }   // English without personal wording: nothing to apply (Refresh restores originals)
            var list = _dirty.ToList();
            _dirty.Clear();
            foreach (var w in list)
                if (w.IsVisible) { try { Walk(w); } catch (Exception ex) { Log.Warn("Language sweep: " + ex.Message); } }
            _dirty.Clear();   // the rewrite itself caused layout passes; those need no second sweep
        }

        /// <summary>Re-applies the language to every open window and tells listeners. Call after changing the mode.</summary>
        public static void Refresh()
        {
            if (Application.Current != null)
                foreach (Window w in Application.Current.Windows) Walk(w);
            Changed?.Invoke();
        }

        /// <summary>Re-applies the language to one subtree, for text that was set after the window loaded.</summary>
        public static void Refresh(DependencyObject root) => Walk(root);

        // ------------------------------------------------------------------ translation

        /// <summary>The Cantonese text for an English string, or null when there is none (paths, commands and names stay as they are).</summary>
        public static string Yue(string english)
        {
            if (string.IsNullOrWhiteSpace(english) || HasCjk(english)) return null;
            var core = english.Trim();
            var lead = english.Substring(0, english.Length - english.TrimStart().Length);
            var trail = english.Substring(english.TrimEnd().Length);
            var found = Lookup(core);
            return found == null ? null : lead + found + trail;
        }

        private static string Lookup(string core)
        {
            if (Strings.Table.TryGetValue(core, out var exact)) return exact;
            foreach (var (rx, fn) in Strings.Patterns)
            {
                var m = rx.Match(core);
                if (m.Success) { var r = fn(m); if (r != null) return r; }
            }
            foreach (var sep in new[] { " · ", "\n" })
            {
                if (!core.Contains(sep)) continue;
                var parts = core.Split(new[] { sep }, StringSplitOptions.None);
                bool any = false;
                var outParts = new string[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i].Trim();
                    var y = p.Length == 0 ? null : Lookup(p);
                    if (y != null) any = true;
                    outParts[i] = y ?? parts[i];
                }
                if (any) return string.Join(sep, outParts);
            }
            // A list of short choices, such as a control's help text "GPT, MBR, Leave it empty": every item is translated or is
            // a name, code or number that stays as it is, and the items are joined with the Chinese list comma. A list with an
            // item that is neither stays English, so a sentence with commas is never half translated.
            if (core.Contains(", "))
            {
                var items = core.Split(new[] { ", " }, StringSplitOptions.None);
                if (items.Length >= 2 && items.All(ListItemShape))
                {
                    bool any = false;
                    var outItems = new string[items.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i].Trim();
                        var y = Lookup(item);
                        if (y != null) { outItems[i] = y; any = true; }
                        else if (StaysAsIs(item)) outItems[i] = item;
                        else return null;
                    }
                    if (any) return string.Join("、", outItems);
                }
            }
            return null;
        }

        private static bool ListItemShape(string item)
        {
            var t = item.Trim();
            return t.Length > 0 && t.Length <= 40 && t.IndexOfAny(new[] { '.', '?', '!', ':', ';' }) < 0 && t.Split(' ').Length <= 5;
        }

        private static readonly System.Text.RegularExpressions.Regex AsIsToken = new System.Text.RegularExpressions.Regex(
            @"^(?:\d[\d,]*|[A-Z0-9][A-Z0-9+#-]+|[A-Za-z]*[a-z][A-Z][A-Za-z0-9]*)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>A list item written the same in every language: numbers, codes in capitals (GPT, VHDX, FAT32) and names with
        /// a capital inside them (ReFS, exFAT, PowerShell).</summary>
        private static bool StaysAsIs(string item) => item.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => AsIsToken.IsMatch(w));

        /// <summary>The text in the current language mode. English stays as it is; Bilingual shows English then Cantonese.</summary>
        public static string T(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            var mode = Mode;
            if (mode == LanguageMode.English) return english;
            var core = english.Trim();
            var yue = Yue(core);
            if (yue == null) return english;
            var lead = english.Substring(0, english.Length - english.TrimStart().Length);
            var trail = english.Substring(english.TrimEnd().Length);
            return lead + (mode == LanguageMode.Cantonese ? yue : Compose(core, yue)) + trail;
        }

        /// <summary>English then Cantonese: a short label on one line, a sentence on two.</summary>
        public static string Compose(string english, string yue)
        {
            if (string.IsNullOrEmpty(yue)) return english;
            // A text of several lines whose translation has the same lines is composed line by line, so lines that do not change are not repeated.
            if (english.Contains('\n') && yue.Contains('\n'))
            {
                var a = english.Split('\n'); var b = yue.Split('\n');
                if (a.Length == b.Length) return string.Join("\n", a.Select((l, i) => string.Equals(l, b[i], StringComparison.Ordinal) ? l : Compose(l, b[i])));
            }
            bool sentence = english.Length > 36 || english.Contains('\n') || english.Contains(" · ") || english.EndsWith(".") || english.EndsWith("?") || english.EndsWith("!");
            return sentence ? english + "\n" + yue : english + " · " + yue;
        }

        internal static bool HasCjk(string s)
        {
            foreach (var c in s) if ((c >= 0x3400 && c <= 0x9FFF) || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0x3000 && c <= 0x303F && c != 0x3000)) return true;
            return false;
        }

        // ------------------------------------------------------------------ emoji

        /// <summary>Removes leading and trailing emoji from a title when the "Emoji in dialogs" switch is off.</summary>
        public static string Title(string title) => Settings.Current.EmojiInDialogs ? title : StripEmoji(title);

        public static string StripEmoji(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var runes = text.EnumerateRunes().ToList();
            int a = 0, b = runes.Count;
            while (a < b && (IsEmoji(runes[a]) || Rune.IsWhiteSpace(runes[a]))) a++;
            while (b > a && (IsEmoji(runes[b - 1]) || Rune.IsWhiteSpace(runes[b - 1]))) b--;
            var sb = new StringBuilder();
            for (int i = a; i < b; i++) sb.Append(runes[i].ToString());
            return sb.ToString();
        }

        private static bool IsEmoji(Rune r)
        {
            int cp = r.Value;
            if (cp == 0xFE0F || cp == 0x200D || cp == 0x20E3) return true;
            if (cp >= 0x1F000) return true;
            if (cp >= 0x2600 && cp <= 0x27BF) return true;
            var category = Rune.GetUnicodeCategory(r);
            return (cp >= 0x2300 && cp <= 0x23FF || cp >= 0x2B00 && cp <= 0x2BFF) && category == UnicodeCategory.OtherSymbol;
        }

        // ------------------------------------------------------------------ humour headlines

        /// <summary>
        /// A headline in the current language, at that language's humour level. Bilingual returns both,
        /// each at its own level. Facts come from <paramref name="args"/> and never change with the level.
        /// </summary>
        public static string Headline(string key, params object[] args)
        {
            var result = HeadlineNow(key, args);
            Remember(result, key, args);
            return result;
        }

        // Rendered headline -> (key, args), so a stored title can be rendered again in the language and level in force later.
        private static readonly Dictionary<string, (string Key, object[] Args)> Sources = new Dictionary<string, (string, object[])>(StringComparer.Ordinal);
        private static void Remember(string rendered, string key, object[] args)
        {
            if (string.IsNullOrEmpty(rendered)) return;
            lock (Sources)
            {
                if (Sources.Count > 400) Sources.Clear();
                Sources[rendered] = (key, args);
            }
        }

        /// <summary>
        /// Renders a title that came from <see cref="Headline"/> again in the language and humour level in force now,
        /// or returns it unchanged when it was never a headline (plain titles are translated by the live layer instead).
        /// </summary>
        public static Func<string> RendererFor(string rendered)
        {
            if (string.IsNullOrEmpty(rendered)) return null;
            (string Key, object[] Args) src;
            lock (Sources) { if (!Sources.TryGetValue(rendered, out src)) return null; }
            return () => HeadlineNow(src.Key, src.Args);
        }

        public static string Rerender(string rendered)
        {
            if (string.IsNullOrEmpty(rendered)) return rendered;
            (string Key, object[] Args) src;
            lock (Sources) { if (!Sources.TryGetValue(rendered, out src)) return rendered; }
            var again = HeadlineNow(src.Key, src.Args);
            Remember(again, src.Key, src.Args);   // the new wording must stay re-renderable for the next switch
            return again;
        }

        private static string HeadlineNow(string key, object[] args)
        {
            var s = Settings.Current;
            var mode = Mode;
            // School mode leaves humour out entirely: every headline is the plain level 1 wording.
            bool plain = Modes.Blocks(ModeFeature.Funny);
            string en = mode != LanguageMode.Cantonese ? Variant(key, LanguageMode.English, plain ? 1 : s.EnglishHumour, args) : null;
            string yue = mode != LanguageMode.English ? Variant(key, LanguageMode.Cantonese, plain ? 1 : s.CantoneseHumour, args) : null;
            if (!s.EmojiInDialogs) { en = en == null ? null : StripEmoji(en); yue = yue == null ? null : StripEmoji(yue); }
            if (mode == LanguageMode.English) return en ?? key;
            if (mode == LanguageMode.Cantonese) return yue ?? key;
            if (en == null) return yue ?? key;
            if (yue == null) return en;
            return en.Length + yue.Length <= 70 ? en + " · " + yue : en + "\n" + yue;
        }

        /// <summary>One variant of a headline: language English or Cantonese, level 1 to 5. Used by the page previews.</summary>
        public static string Variant(string key, LanguageMode language, int level, params object[] args)
        {
            var table = language == LanguageMode.Cantonese ? Humour.Yue : Humour.En;
            if (!table.TryGetValue(key, out var set)) return key;
            var text = set[Math.Clamp(level, 1, 5) - 1];
            try { return args != null && args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, text, args) : text; }
            catch (FormatException) { return text; }
        }

        public static IEnumerable<string> HeadlineKeys => Humour.En.Keys;

        // Toast titles written by other parts of the app, mapped to their headline keys so every notice follows the humour level.
        private static readonly Dictionary<string, string> NoticeKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Open in editor"] = "notice.editor", ["Time check"] = "notice.timecheck", ["IP address renewed"] = "notice.ip.renewed",
            ["Cancelled"] = "notice.cancelled", ["ipconfig finished"] = "notice.ipconfig", ["Administrator rights unchanged"] = "notice.admin.unchanged",
            ["DNS switch failed"] = "notice.dns.failed", ["Checking the connection"] = "notice.checking", ["Uninstalled"] = "notice.uninstalled",
            ["Wording file refused"] = "notice.wording.refused", ["Replace in files"] = "notice.replace", ["Restore backup"] = "notice.restore",
        };
        private static readonly System.Text.RegularExpressions.Regex DnsNow = new System.Text.RegularExpressions.Regex(@"^DNS is now (.+)$", System.Text.RegularExpressions.RegexOptions.Singleline);
        private static readonly System.Text.RegularExpressions.Regex BatchTo = new System.Text.RegularExpressions.Regex(@"^Batch to (.+)$", System.Text.RegularExpressions.RegexOptions.Singleline);

        /// <summary>The headline for a notice title the app wrote in plain English; any other title is returned unchanged.</summary>
        public static string NoticeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            if (NoticeKeys.TryGetValue(title, out var key)) return Headline(key);
            var m = DnsNow.Match(title);
            if (m.Success) return Headline("notice.dns.now", m.Groups[1].Value);
            m = BatchTo.Match(title);
            if (m.Success) return Headline("notice.batch", m.Groups[1].Value);
            return title;
        }

        private static readonly Dictionary<string, string> NetKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Internet connection lost"] = "offline.title", ["No network connection"] = "offline.noadapter",
            ["No IP address from the router"] = "offline.noaddress", ["No route to the internet"] = "offline.noroute",
            ["Sign-in required"] = "offline.signin", ["Router is not responding"] = "offline.router",
        };

        /// <summary>The offline dialog headline for a network diagnosis, at the current humour level. Unknown headlines are returned unchanged.</summary>
        public static string NetHeadline(string diagnosis)
            => diagnosis != null && NetKeys.TryGetValue(diagnosis, out var key) ? Headline(key) : diagnosis;

        /// <summary>
        /// Splits a title (plain, or already a headline in any mode) into the English and Cantonese text to speak,
        /// so the narrator reads each part with the matching voice.
        /// </summary>
        public static (string En, string Yue) SpeechParts(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return ("", "");
            if (!HasCjk(title)) return (title, Yue(title));
            int cut = -1;
            for (int i = 0; i < title.Length; i++) if (HasCjk(title[i].ToString())) { cut = i; break; }
            var en = title.Substring(0, cut).Trim(' ', '·', '\n', '\r');
            var yue = title.Substring(cut).Trim();
            return (en, yue);
        }

        // ------------------------------------------------------------------ the live layer

        private static void Walk(DependencyObject root)
        {
            if (root == null) return;
            if (root is Window w) Retitle(w);
            if (root is TextBlock tb) Rewrite(tb);
            if (root is FrameworkElement fe && fe.ToolTip is string) Retip(fe);
            if (root is UIElement) { Retext(root, AutomationProperties.NameProperty, NameEnProp, NameFinalProp); Retext(root, AutomationProperties.HelpTextProperty, HelpEnProp, HelpFinalProp); }
            int n = root is Visual || root is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(root, i));
            // a name that follows the element's own text (see AccessibleLabels) is read again once that text has its final wording
            if (root is UIElement) System.Windows.Data.BindingOperations.GetBindingExpression(root, AutomationProperties.NameProperty)?.UpdateTarget();
        }

        /// <summary>Marks an element whose text is already in its final language (previews, voice names) so the layer leaves it alone.</summary>
        public static readonly DependencyProperty LiteralProperty =
            DependencyProperty.RegisterAttached("Literal", typeof(bool), typeof(Language), new PropertyMetadata(false));
        public static void SetLiteral(DependencyObject d, bool value) => d.SetValue(LiteralProperty, value);
        public static bool GetLiteral(DependencyObject d) => (bool)d.GetValue(LiteralProperty);

        internal static string OriginalText(Run r) => (string)r.GetValue(EnProp);
        internal static string OriginalTip(FrameworkElement fe) => (string)fe.GetValue(TipEnProp);
        internal static string OriginalName(DependencyObject d) => (string)d.GetValue(NameEnProp);
        internal static string OriginalHelp(DependencyObject d) => (string)d.GetValue(HelpEnProp);
        internal static string OriginalTitle(Window w) => (string)w.GetValue(TitleEnProp);

        internal static bool IsLiteral(TextBlock tb)
        {
            for (DependencyObject d = tb; d != null; d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(d) : null)
                if (GetLiteral(d)) return true;   // a text box's own hint and label are interface text; what is typed is not drawn by a TextBlock
            return false;
        }

        private static void Rewrite(TextBlock tb)
        {
            if (IsLiteral(tb)) return;
            foreach (var run in Runs(tb.Inlines))
            {
                if (run.Parent is Hyperlink || GetLiteral(run)) continue;
                var en = (string)run.GetValue(EnProp);
                if (en == null && Plain) continue;   // nothing was ever translated and nothing is wanted
                var cur = run.Text;
                if (en != null)
                {
                    var final = (string)run.GetValue(FinalProp);
                    var baseText = (string)run.GetValue(BaseProp);
                    bool ours = cur == final || cur == WordingOut(baseText);
                    if (!ours) en = null;   // the app wrote new text since: it is the new English original
                }
                if (en == null)
                {
                    var theirs = WordingOriginal != null ? (string)run.GetValue(WordingOriginal) : null;
                    en = run.GetValue(EnProp) == null && theirs != null ? theirs : cur;
                }
                if (string.IsNullOrWhiteSpace(en)) continue;
                var baseNext = T(en);
                if (WordingOriginal != null) run.SetValue(WordingOriginal, baseNext);
                var next = WordingOut(baseNext);
                run.SetValue(EnProp, en);
                run.SetValue(BaseProp, baseNext);
                run.SetValue(FinalProp, next);
                if (!string.Equals(run.Text, next, StringComparison.Ordinal)) run.Text = next;
            }
        }

        private static string WordingOut(string text) =>
            text == null ? null : (PersonalWording.Active ? PersonalWording.Apply(text) : text);

        private static IEnumerable<Run> Runs(InlineCollection inlines)
        {
            foreach (var i in inlines.ToList())
            {
                if (i is Run r) yield return r;
                else if (i is Span sp) foreach (var x in Runs(sp.Inlines)) yield return x;
            }
        }

        private static void Retip(FrameworkElement fe)
        {
            var cur = fe.ToolTip as string;
            if (cur == null || GetLiteral(fe)) return;
            var en = (string)fe.GetValue(TipEnProp);
            if (en != null && cur != (string)fe.GetValue(TipFinalProp)) en = null;
            if (en == null) { if (Plain) return; en = cur; }
            var next = WordingOut(T(en));
            fe.SetValue(TipEnProp, en); fe.SetValue(TipFinalProp, next);
            if (!string.Equals(cur, next, StringComparison.Ordinal)) fe.ToolTip = next;
        }

        internal static bool HasLiteralAncestor(DependencyObject d)
        {
            for (int guard = 0; d != null && guard < 200; guard++)
            {
                if (GetLiteral(d)) return true;
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        // Accessible names and help text are translated like visible text and restored exactly in English.
        private static void Retext(DependencyObject d, DependencyProperty target, DependencyProperty enProp, DependencyProperty finalProp)
        {
            if (!(d.ReadLocalValue(target) is string cur) || cur.Length == 0) return;
            if (HasLiteralAncestor(d)) return;
            var en = (string)d.GetValue(enProp);
            if (en != null && cur != (string)d.GetValue(finalProp)) en = null;   // the app wrote a new name since
            if (en == null) { if (Plain) return; en = cur; }
            var next = WordingOut(T(en));
            d.SetValue(enProp, en); d.SetValue(finalProp, next);
            if (!string.Equals(cur, next, StringComparison.Ordinal)) d.SetValue(target, next);
        }

        private static void Retitle(Window w)
        {
            var cur = w.Title;
            if (string.IsNullOrEmpty(cur)) return;
            var en = (string)w.GetValue(TitleEnProp);
            if (en != null && cur != (string)w.GetValue(TitleFinalProp)) en = null;
            if (en == null) { if (Plain) return; en = cur; }
            var next = WordingOut(T(en));
            w.SetValue(TitleEnProp, en); w.SetValue(TitleFinalProp, next);
            if (!string.Equals(cur, next, StringComparison.Ordinal)) w.Title = next;
        }
    }

    /// <summary>
    /// Headline strings with five tone levels in each language. Level 1 is plain and precise; level 5
    /// is playful. The facts (names, versions, times) arrive as format arguments and are identical at every level.
    /// </summary>
    internal static class Humour
    {
        internal static readonly Dictionary<string, string[]> En = new Dictionary<string, string[]>
        {
            ["offline.title"] = new[] { "Internet connection lost", "The internet connection dropped", "Looks like the internet took a break", "The internet has wandered off", "The internet packed its bags and left 🧳" },
            ["online.title"] = new[] { "Back online", "The connection is back", "And we're back", "The internet came crawling back", "It lives! The internet is back 🎉" },
            ["ip.changed"] = new[] { "Public IP changed to {0}", "Your public IP is now {0}", "New public IP: {0}", "Fresh address, who dis? {0}", "Plot twist: you are now {0} 🕵️" },
            ["convert.done"] = new[] { "Saved as {0}", "Conversion to {0} finished", "Your {0} is ready", "Nailed it: {0} is done", "Ta-da! Fresh {0}, hot off the press 🔥" },
            ["convert.failed"] = new[] { "Conversion to {0} failed", "Could not convert to {0}", "That {0} did not work out", "The {0} conversion tripped over its shoelaces", "{0} said no. Dramatically 💥" },
            ["push.done"] = new[] { "Pushed {0}", "Commit and push finished on {0}", "Pushed to {0}, all good", "{0} is safely upstairs", "Shipped! {0} has left the building 🚀" },
            ["push.failed"] = new[] { "Push failed on {0}", "Could not push {0}", "The push to {0} did not go through", "{0} got stuck at the door", "{0} bounced off the wall. Ouch 🧱" },
            ["game.mode"] = new[] { "Game mode on: {0}", "Popups paused while {0} runs", "Game on! Popups are on hold for {0}", "Enjoy {0}, we'll keep quiet", "{0} detected. Silence, mortals 🎮" },
            ["snooze"] = new[] { "Snoozed until {0}", "Notifications paused until {0}", "Quiet until {0}", "Shhh. Back at {0}", "Napping until {0}. Do not disturb 😴" },
            ["history.empty"] = new[] { "Nothing copied yet", "The history is empty", "Nothing here yet. Copy something!", "Crickets. Copy something already", "Empty as a fridge on Sunday night 🦗" },
            ["search.none"] = new[] { "No results for “{0}”", "Nothing matches “{0}”", "Could not find “{0}”", "“{0}” is hiding really well", "We looked everywhere for “{0}”. Even under the couch 🔍" },
            ["update.available"] = new[] { "Update available: {0}", "Version {0} is available", "A new version is here: {0}", "Shiny new {0} is waiting for you", "{0} just dropped. Treat yourself 🎁" },
            ["welcome"] = new[] { "Welcome to Monitoring Tools", "Welcome. Monitoring Tools is running", "Hello! Monitoring Tools is on watch", "Hey you! Monitoring Tools has your back", "Welcome aboard! Let's keep an eye on everything 👀" },
            ["narrator.test"] = new[] { "This is the English voice.", "This is how the English voice sounds.", "Hello, this is your English voice.", "Testing, one, two. Can you hear me?", "Ladies and gentlemen, the English voice has entered the chat!" },
            ["convert.start"] = new[] { "Converting to {0}", "Converting to {0} now", "Working on your {0}", "Cooking up a fresh {0}", "Spinning up the {0} machine ⚡" },
            ["convert.cancelled"] = new[] { "Conversion cancelled", "The conversion was cancelled", "Okay, conversion stopped", "Conversion halted mid-flight", "Conversion aborted. Parachutes deployed 🪂" },
            ["ffmpeg.missing"] = new[] { "FFmpeg is missing from this build", "This build does not include FFmpeg", "FFmpeg did not make it into this build", "FFmpeg forgot to show up", "FFmpeg ghosted this build 👻" },
            ["task.done"] = new[] { "{0}", "Done: {0}", "All done: {0}", "Nailed it: {0}", "Boom, done: {0} 🎉" },
            ["task.failed"] = new[] { "{0}", "Problem: {0}", "Hmm, that did not work: {0}", "Oops: {0}", "Ouch: {0} 💥" },
            ["shortcut.done"] = new[] { "{0} finished in {1} s", "{0} completed in {1} s", "{0} is done in {1} s", "{0} zipped through in {1} s", "{0} flew by in {1} s ⚡" },
            ["shortcut.failed"] = new[] { "{0} exited with {1}", "{0} stopped with exit code {1}", "{0} ended with code {1}", "{0} tripped on the way out with {1}", "{0} crashed out with {1} 💥" },
            ["offline.noadapter"] = new[] { "No network connection", "There is no network connection", "No network found", "The network has left the building", "No network anywhere. Spooky 🔌" },
            ["offline.noaddress"] = new[] { "No IP address from the router", "The router gave no IP address", "The router ignored our request for an address", "The router forgot to hand out an address", "The router left us homeless, no address 🏚️" },
            ["offline.noroute"] = new[] { "No route to the internet", "There is no route to the internet", "We cannot find the way out", "Every road to the internet is closed", "Lost in the network maze 🗺️" },
            ["offline.signin"] = new[] { "Sign-in required", "This network needs you to sign in", "Knock knock, this Wi-Fi wants a sign-in", "The Wi-Fi wants to see some ID", "Doorman says: sign in first 🚪" },
            ["offline.router"] = new[] { "Router is not responding", "The router is not answering", "Nobody is home at the router", "The router is giving us the silent treatment", "The router is ignoring us. Rude 📡" },
            ["notice.editor"] = new[] { "Open in editor", "Open in editor: a note", "A note about opening your editor", "The editor has something to say", "Editor news, hot off the press 📰" },
            ["notice.timecheck"] = new[] { "Time check", "Quick time check", "Psst, time check", "Tick tock, time check", "Time flies, check the clock ⏰" },
            ["notice.ip.renewed"] = new[] { "IP address renewed", "Your IP address was renewed", "Fresh IP address, renewed", "New lease on life: IP renewed", "IP renewed. You are brand new 🔄" },
            ["notice.cancelled"] = new[] { "Cancelled", "That was cancelled", "Okay, cancelled", "Cancelled, as you wish", "Cancelled. Poof 💨" },
            ["notice.ipconfig"] = new[] { "ipconfig finished", "ipconfig has finished", "ipconfig is done", "ipconfig wrapped up", "ipconfig has left the chat 🏁" },
            ["notice.admin.unchanged"] = new[] { "Administrator rights unchanged", "Administrator rights were not changed", "No change to administrator rights", "Administrator rights stayed put", "Administrator rights did not budge 🪨" },
            ["notice.dns.now"] = new[] { "DNS is now {0}", "DNS switched to {0}", "You are now using {0} for DNS", "DNS moved house: {0}", "DNS is now {0}. New phonebook, who dis? 📖" },
            ["notice.dns.failed"] = new[] { "DNS switch failed", "Could not switch DNS", "The DNS switch did not work", "DNS refused to move", "DNS dug its heels in 🐴" },
            ["notice.checking"] = new[] { "Checking the connection", "Checking your connection now", "Taking the connection's pulse", "Poking the internet to see if it flinches", "Interrogating the internet 🔎" },
            ["notice.uninstalled"] = new[] { "Uninstalled", "Monitoring Tools was uninstalled", "All uninstalled", "Packed up and gone", "Uninstalled. Poof, vanished 💨" },
            ["notice.wording.refused"] = new[] { "Wording file refused", "The wording file was refused", "That wording file was not accepted", "The wording file did not pass the door check", "Wording file bounced at the door 🚫" },
            ["notice.replace"] = new[] { "Replace in files", "Replace in files report", "Replace report", "News from the replace run", "Replace run bulletin 📋" },
            ["notice.restore"] = new[] { "Restore backup", "Restore backup report", "Backup restore report", "News from the backup restore", "Backup restore bulletin 📋" },
            ["notice.batch"] = new[] { "Batch to {0}", "Batch conversion to {0}", "About that {0} batch", "The {0} batch has news", "{0} batch bulletin 📋" },
            ["mode.on"] = new[] { "{0} is on", "{0} is switched on", "Heads up, {0} is on", "{0} is on duty", "{0} is on, and the rules are watching 🏫" },
        };

        internal static readonly Dictionary<string, string[]> Yue = new Dictionary<string, string[]>
        {
            ["offline.title"] = new[] { "網絡連線中斷", "網絡斷咗線", "網絡好似去咗休息", "網絡走咗路喇", "網絡執包袱走人喇 🧳" },
            ["online.title"] = new[] { "已恢復連線", "網絡返嚟喇", "返嚟喇，搞掂", "網絡乖乖地返嚟喇", "活過嚟喇！網絡返嚟喇 🎉" },
            ["ip.changed"] = new[] { "公開 IP 已更改為 {0}", "你而家個公開 IP 係 {0}", "有新 IP：{0}", "換咗個新地址喎：{0}", "劇情反轉！你而家係 {0} 🕵️" },
            ["convert.done"] = new[] { "已儲存為 {0}", "轉做 {0} 完成", "你嘅 {0} 搞掂", "撈掂喇，{0} 完成", "叮！新鮮辣辣嘅 {0} 出爐 🔥" },
            ["convert.failed"] = new[] { "轉做 {0} 失敗", "轉唔到做 {0}", "個 {0} 唔成功喎", "轉 {0} 彈咗腳筋", "{0} 話唔得，仲好戲劇性 💥" },
            ["push.done"] = new[] { "已推送 {0}", "{0} 提交同推送完成", "已推上 {0}，冇問題", "{0} 安全送到上面喇", "出貨！{0} 已經離開大廈 🚀" },
            ["push.failed"] = new[] { "{0} 推送失敗", "推唔到 {0}", "推去 {0} 唔成功", "{0} 卡咗喺門口", "{0} 撞牆彈返嚟，好痛 🧱" },
            ["game.mode"] = new[] { "遊戲模式已開：{0}", "{0} 行緊，通知暫停", "開波喇！{0} 期間通知收埋", "盡情玩 {0}，我哋唔阻你", "偵測到 {0}，全場肅靜 🎮" },
            ["snooze"] = new[] { "已暫停到 {0}", "通知暫停到 {0}", "靜靜地等到 {0}", "噓，{0} 再見", "瞓覺瞓到 {0}，請勿打擾 😴" },
            ["history.empty"] = new[] { "未有複製紀錄", "紀錄係空嘅", "暫時冇嘢，複製啲嘢啦", "靜到得蟋蟀聲，複製啲嘢啦", "空過星期日夜晚嘅雪櫃 🦗" },
            ["search.none"] = new[] { "搵唔到「{0}」", "冇嘢符合「{0}」", "搵唔到「{0}」喎", "「{0}」收埋得好好", "「{0}」搵遍晒，連梳化底都搵過 🔍" },
            ["update.available"] = new[] { "有更新：{0}", "有 {0} 版本可以用", "新版本到喇：{0}", "靚靚新版 {0} 等緊你", "{0} 剛剛出街，疼惜下自己 🎁" },
            ["welcome"] = new[] { "歡迎使用 Monitoring Tools", "歡迎，Monitoring Tools 運行緊", "你好！Monitoring Tools 睇緊", "喂！Monitoring Tools 幫你睇住", "歡迎上船！一齊盯實晒所有嘢 👀" },
            ["narrator.test"] = new[] { "呢個係廣東話語音。", "你而家聽到嘅係廣東話語音。", "你好，呢個係你嘅廣東話語音。", "試音試音，聽唔聽到呀？", "各位觀眾，廣東話語音正式登場！" },
            ["convert.start"] = new[] { "轉緊做 {0}", "而家轉緊做 {0}", "處理緊你個 {0}", "煮緊新鮮嘅 {0}", "{0} 轉換機開動 ⚡" },
            ["convert.cancelled"] = new[] { "轉檔已取消", "轉檔已經取消咗", "好，轉檔停咗", "轉檔中途剎車", "轉檔中止，彈射傘打開 🪂" },
            ["ffmpeg.missing"] = new[] { "呢個版本冇 FFmpeg", "呢個版本唔包含 FFmpeg", "FFmpeg 冇跟埋呢個版本", "FFmpeg 唔記得返嚟", "FFmpeg 玩失蹤 👻" },
            ["task.done"] = new[] { "{0}", "完成：{0}", "搞掂：{0}", "叮！{0}", "大功告成：{0} 🎉" },
            ["task.failed"] = new[] { "{0}", "有問題：{0}", "唔順利喎：{0}", "嘩，{0}", "好痛：{0} 💥" },
            ["shortcut.done"] = new[] { "{0} 用咗 {1} 秒完成", "{0} 喺 {1} 秒內完成", "{0} {1} 秒搞掂", "{0} 嗖一聲，{1} 秒就完", "{0} 飛一樣，{1} 秒完成 ⚡" },
            ["shortcut.failed"] = new[] { "{0} 結束，代碼 {1}", "{0} 停咗，結束代碼 {1}", "{0} 完咗，代碼 {1}", "{0} 臨走絆倒，代碼 {1}", "{0} 撞咗牆走，代碼 {1} 💥" },
            ["offline.noadapter"] = new[] { "冇網絡連線", "而家冇網絡連線", "搵唔到網絡", "網絡離開咗大廈", "邊度都冇網絡，好驚 🔌" },
            ["offline.noaddress"] = new[] { "路由器冇派 IP 地址", "路由器冇俾 IP 地址", "路由器唔理我哋要地址", "路由器唔記得派地址", "路由器冇派地址，我哋無家可歸 🏚️" },
            ["offline.noroute"] = new[] { "去互聯網冇路", "去互聯網搵唔到路", "搵唔到出路", "去互聯網條條路都封", "喺網絡迷宮迷咗路 🗺️" },
            ["offline.signin"] = new[] { "需要登入", "呢個網絡要你登入", "叩叩，呢個 Wi-Fi 要登入", "Wi-Fi 要睇你證件", "門衞話：先登入 🚪" },
            ["offline.router"] = new[] { "路由器冇回應", "路由器冇答", "路由器度冇人喺度", "路由器唔理我哋", "路由器唔睬我哋，好無禮 📡" },
            ["notice.editor"] = new[] { "用編輯器開啟", "用編輯器開啟：備註", "關於開啟編輯器嘅提示", "編輯器有嘢要講", "編輯器快訊 📰" },
            ["notice.timecheck"] = new[] { "時間提示", "簡單時間提示", "喂，睇下時間", "滴答滴答，睇時間", "時間飛咗，睇下鐘 ⏰" },
            ["notice.ip.renewed"] = new[] { "IP 地址已更新", "你嘅 IP 地址已更新", "新鮮 IP 地址更新咗", "IP 租約續期，重新做人", "IP 更新咗，成個人煥然一新 🔄" },
            ["notice.cancelled"] = new[] { "已取消", "已經取消咗", "好，取消咗", "照你意思取消咗", "取消，噗一聲冇咗 💨" },
            ["notice.ipconfig"] = new[] { "ipconfig 已完成", "ipconfig 完成咗", "ipconfig 搞掂", "ipconfig 收工", "ipconfig 跑完線 🏁" },
            ["notice.admin.unchanged"] = new[] { "管理員權限冇改變", "管理員權限冇更改", "管理員權限冇變", "管理員權限原地踏步", "管理員權限紋風不動 🪨" },
            ["notice.dns.now"] = new[] { "DNS 已改為 {0}", "DNS 已切換到 {0}", "你而家用緊 {0} 做 DNS", "DNS 搬咗屋：{0}", "DNS 轉用 {0}，換咗本電話簿 📖" },
            ["notice.dns.failed"] = new[] { "切換 DNS 失敗", "切換唔到 DNS", "DNS 切換唔成功", "DNS 唔肯搬", "DNS 死都唔肯郁 🐴" },
            ["notice.checking"] = new[] { "檢查緊連線", "而家檢查緊你嘅連線", "探緊連線嘅脈搏", "戳下互聯網睇下有冇反應", "盤問緊互聯網 🔎" },
            ["notice.uninstalled"] = new[] { "已解除安裝", "Monitoring Tools 已解除安裝", "全部解除安裝晒", "執包袱走咗", "解除安裝，噗一聲冇晒 💨" },
            ["notice.wording.refused"] = new[] { "用語檔案被拒絕", "用語檔案畀人拒絕咗", "嗰個用語檔案唔獲接受", "用語檔案過唔到門口檢查", "用語檔案喺門口被攔低 🚫" },
            ["notice.replace"] = new[] { "喺檔案中取代", "喺檔案中取代報告", "取代報告", "取代運行嘅消息", "取代運行快訊 📋" },
            ["notice.restore"] = new[] { "還原備份", "還原備份報告", "備份還原報告", "備份還原消息", "備份還原快訊 📋" },
            ["notice.batch"] = new[] { "批量轉做 {0}", "批量轉檔做 {0}", "關於嗰批 {0}", "{0} 批次有消息", "{0} 批次快訊 📋" },
            ["mode.on"] = new[] { "{0} 已開啟", "{0} 已經開咗", "注意，{0} 開咗", "{0} 當值中", "{0} 開咗，規矩睇住你 🏫" },
        };
    }
}
