using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
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

        public static LanguageMode Mode => Settings.Current.LanguageMode;
        public static bool IsEnglish => Mode == LanguageMode.English;

        private static bool _installed;

        // The English original, the language-only text and the final on-screen text of a Run.
        private static readonly DependencyProperty EnProp = DependencyProperty.RegisterAttached("En", typeof(string), typeof(Language));
        private static readonly DependencyProperty BaseProp = DependencyProperty.RegisterAttached("Base", typeof(string), typeof(Language));
        private static readonly DependencyProperty FinalProp = DependencyProperty.RegisterAttached("Final", typeof(string), typeof(Language));
        private static readonly DependencyProperty TipEnProp = DependencyProperty.RegisterAttached("TipEn", typeof(string), typeof(Language));
        private static readonly DependencyProperty TipFinalProp = DependencyProperty.RegisterAttached("TipFinal", typeof(string), typeof(Language));
        private static readonly DependencyProperty TitleEnProp = DependencyProperty.RegisterAttached("TitleEn", typeof(string), typeof(Language));
        private static readonly DependencyProperty TitleFinalProp = DependencyProperty.RegisterAttached("TitleFinal", typeof(string), typeof(Language));

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
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((e, a) => Walk((Window)e)));
            Narrator.Attach();
            Refresh();
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
                if (m.Success) return fn(m);
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
            return null;
        }

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
            bool sentence = english.Length > 36 || english.Contains('\n') || english.Contains(" · ") || english.EndsWith(".") || english.EndsWith("?") || english.EndsWith("!");
            return sentence ? english + "\n" + yue : english + " · " + yue;
        }

        private static bool HasCjk(string s)
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
            var s = Settings.Current;
            var mode = s.LanguageMode;
            string en = mode != LanguageMode.Cantonese ? Variant(key, LanguageMode.English, s.EnglishHumour, args) : null;
            string yue = mode != LanguageMode.English ? Variant(key, LanguageMode.Cantonese, s.CantoneseHumour, args) : null;
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

        // ------------------------------------------------------------------ the live layer

        private static void Walk(DependencyObject root)
        {
            if (root == null) return;
            if (root is Window w) Retitle(w);
            if (root is TextBlock tb) Rewrite(tb);
            if (root is FrameworkElement fe && fe.ToolTip is string) Retip(fe);
            int n = root is Visual || root is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(root, i));
        }

        /// <summary>Marks an element whose text is already in its final language (previews, voice names) so the layer leaves it alone.</summary>
        public static readonly DependencyProperty LiteralProperty =
            DependencyProperty.RegisterAttached("Literal", typeof(bool), typeof(Language), new PropertyMetadata(false));
        public static void SetLiteral(DependencyObject d, bool value) => d.SetValue(LiteralProperty, value);
        public static bool GetLiteral(DependencyObject d) => (bool)d.GetValue(LiteralProperty);

        private static bool IsLiteral(TextBlock tb)
        {
            var f = tb.FontFamily?.Source ?? "";
            if (f.IndexOf("Mono", StringComparison.OrdinalIgnoreCase) >= 0 || f.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (DependencyObject d = tb; d != null; d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(d) : null)
                if (d is TextBox || d is PasswordBox || GetLiteral(d)) return true;
            return false;
        }

        private static void Rewrite(TextBlock tb)
        {
            if (IsLiteral(tb)) return;
            foreach (var run in Runs(tb.Inlines))
            {
                if (run.Parent is Hyperlink) continue;
                var en = (string)run.GetValue(EnProp);
                if (en == null && IsEnglish) continue;   // nothing was ever translated and nothing is wanted: leave it to the wording layer
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
            if (cur == null) return;
            var en = (string)fe.GetValue(TipEnProp);
            if (en != null && cur != (string)fe.GetValue(TipFinalProp)) en = null;
            if (en == null) { if (IsEnglish) return; en = cur; }
            var next = T(en);
            fe.SetValue(TipEnProp, en); fe.SetValue(TipFinalProp, next);
            if (!string.Equals(cur, next, StringComparison.Ordinal)) fe.ToolTip = next;
        }

        private static void Retitle(Window w)
        {
            var cur = w.Title;
            if (string.IsNullOrEmpty(cur)) return;
            var en = (string)w.GetValue(TitleEnProp);
            if (en != null && cur != (string)w.GetValue(TitleFinalProp)) en = null;
            if (en == null) { if (IsEnglish) return; en = cur; }
            var next = T(en);
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
        };
    }
}
