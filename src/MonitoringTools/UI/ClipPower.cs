using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The clipboard's power features: global shortcuts, paste as plain text, clipboard undo, the paste queue, collect mode,
    /// typing the clipboard for apps that block paste, snippets and per-app rules. A keyboard hook runs only while snippets
    /// or the paste queue need it, and it never keeps what you type.
    /// </summary>
    public static class ClipPower
    {
        private static HwndSource _hotkeys;
        private static readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();
        private static NativeInput.KeyboardHook _hook;
        private static readonly SnippetMatcher _matcher = new SnippetMatcher();

        /// <summary>Shortcuts that could not be registered, usually because another app already uses them.</summary>
        public static List<string> Taken { get; } = new List<string>();

        // ------------------------------------------------------------------ start and shortcuts

        public static void Start()
        {
            _matcher.Snippets = () => Settings.Current.SnippetsOn ? Settings.Current.Snippets : new List<Snippet>();
            RegisterAll();
            UpdateHook();
        }

        public static void RegisterAll()
        {
            if (_hotkeys == null)
            {
                _hotkeys = new HwndSource(new HwndSourceParameters("MonitoringTools.Hotkeys") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0 });
                _hotkeys.AddHook(Hook);
            }
            foreach (var id in _actions.Keys.ToList()) NativeInput.UnregisterHotKey(_hotkeys.Handle, id);
            _actions.Clear(); Taken.Clear();
            var s = Settings.Current;
            if (!s.ClipboardHotkeysOn) return;
            Add(s.HotkeyHistory, "Clipboard history", () => HistoryPopup.Toggle());
            Add(s.HotkeyPlainPaste, "Paste as plain text", PastePlain);
            Add(s.HotkeyQueue, "Paste queue", () => PasteQueue.Toggle());
            Add(s.HotkeyUndo, "Undo the clipboard", Undo);
            Add(s.HotkeyConversions, "Quick conversions", () => Dialogs.ConversionsDialog.Open(CurrentText() ?? ""));
            Add(s.HotkeyPin, "Pin on screen", () => { var it = App.History.Items.FirstOrDefault(); if (it != null) PinWindow.Show(it); });
            Add(s.HotkeyCollect, "Collect mode", ToggleCollect);
            Add(s.HotkeyTypeIt, "Type it instead of pasting", TypeIt);
            foreach (var sn in s.Snippets.Where(x => !string.IsNullOrWhiteSpace(x.Hotkey))) { var snip = sn; Add(sn.Hotkey, "Snippet " + sn.Name, () => _ = Insert(snip, false)); }
            foreach (var r in s.Recipes.Where(x => !string.IsNullOrWhiteSpace(x.Hotkey))) { var rec = r; Add(r.Hotkey, "Recipe " + r.Name, () => RunRecipe(rec, paste: true)); }
        }

        private static void Add(string shortcut, string what, Action run)
        {
            if (!NativeInput.ParseShortcut(shortcut, out var mods, out var vk)) return;
            int id = 0x5100 + _actions.Count;
            if (NativeInput.RegisterHotKey(_hotkeys.Handle, id, mods | NativeInput.MOD_NOREPEAT, vk)) _actions[id] = run;
            else { Taken.Add(shortcut + " (" + what + ")"); Log.Warn("Shortcut taken: " + shortcut + " for " + what); }
        }

        private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg == NativeInput.WM_HOTKEY && _actions.TryGetValue(w.ToInt32(), out var run))
            {
                handled = true;
                try { run(); } catch (Exception ex) { Log.Warn("Shortcut failed: " + ex.Message); }
            }
            return IntPtr.Zero;
        }

        /// <summary>The keyboard hook runs only while snippets have triggers or the paste queue is pasting.</summary>
        public static void UpdateHook()
        {
            bool need = (Settings.Current.SnippetsOn && Settings.Current.Snippets.Any(s => !string.IsNullOrEmpty(s.Trigger))) || PasteQueue.Pasting;
            if (need && _hook == null)
            {
                _hook = new NativeInput.KeyboardHook();
                _hook.Typed += c => { var hit = _matcher.Feed(c); if (hit != null) Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = Insert(hit, true))); };
                _hook.Pasted += () => Application.Current.Dispatcher.BeginInvoke(new Action(PasteQueue.AfterPaste));
            }
            else if (!need && _hook != null) { _hook.Dispose(); _hook = null; }
        }

        // ------------------------------------------------------------------ pasting

        public static string CurrentText()
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; } catch { return null; }
        }

        /// <summary>Puts text on the clipboard and presses Ctrl+V in the app in front (or in the given window).</summary>
        public static async Task PasteText(string text, IntPtr into = default, bool tabAfter = false)
        {
            ClipboardWatcher.SetText(text);
            if (into != IntPtr.Zero) NativeInput.SetForegroundWindow(into);
            await Task.Delay(90);
            NativeInput.PressCtrlV();
            if (tabAfter) { await Task.Delay(60); NativeInput.Press(NativeInput.VK_TAB); }
        }

        /// <summary>Pastes what is on the clipboard without its formatting.</summary>
        public static void PastePlain()
        {
            var t = CurrentText();
            if (t == null) { Say("Nothing to paste as plain text", "The clipboard has no text."); return; }
            _ = PasteText(t);
        }

        /// <summary>Types the clipboard as key presses, for apps and sites that block pasting.</summary>
        public static void TypeIt()
        {
            var t = CurrentText();
            if (string.IsNullOrEmpty(t)) { Say("Nothing to type", "The clipboard has no text."); return; }
            if (t.Length > 5000) { Say("Too long to type", "Typing is for short text, up to 5,000 characters."); return; }
            _ = Task.Run(async () => { await Task.Delay(150); NativeInput.TypeText(t); });
        }

        // ------------------------------------------------------------------ undo

        /// <summary>Puts back what was on the clipboard before the last copy: text, files or a picture.</summary>
        public static void Undo()
        {
            var items = App.History.Items;
            if (items.Count < 2) { Say("Nothing to undo", "There is no earlier copy in history."); return; }
            var prev = items[1];
            Restore(prev);
            items.Move(1, 0);
            Say("Clipboard put back", Short(prev.Preview));
        }

        public static void Restore(ClipItem it)
        {
            if (it.Kind == ClipKind.Files && it.Files.Length > 0 && !it.IsPathText) ClipboardWatcher.SetFiles(it.Files);
            else if (it.Kind == ClipKind.Image && it.Image != null) ClipboardWatcher.SetImage(it.Image);
            else if (it.Text != null) ClipboardWatcher.SetText(it.Text);
        }

        // ------------------------------------------------------------------ collect mode

        private static List<string> _collected;
        public static bool Collecting => _collected != null;

        public static void ToggleCollect()
        {
            if (_collected == null) { _collected = new List<string>(); Say("Collect mode is on", "Every copy is added to one note. Press " + Settings.Current.HotkeyCollect + " again to finish."); return; }
            var all = string.Join(Environment.NewLine, _collected);
            int n = _collected.Count; _collected = null;
            if (n > 0) ClipboardWatcher.SetText(all);
            Say("Collect mode is off", n == 0 ? "Nothing was collected." : n + (n == 1 ? " copy is" : " copies are") + " on the clipboard as one note.");
        }

        /// <summary>Called for every copy. True when collect mode or the paste queue took it.</summary>
        public static bool Take(ClipItem item)
        {
            if (_collected != null && item.Text != null && !item.Sensitive)
            {
                _collected.Add(item.Text.TrimEnd());
                App.Toasts.ShowNotice("Collected " + _collected.Count, Short(item.Text), App.Toasts.Violet, 2, null, Icons.Plus);
                return true;
            }
            return PasteQueue.Take(item);
        }

        // ------------------------------------------------------------------ snippets and recipes

        /// <summary>Fills in a snippet and pastes it. When typed, its trigger is removed first.</summary>
        public static async Task Insert(Snippet sn, bool typed)
        {
            var clip = CurrentText();
            var e = SnippetMatcher.Fill(sn.Body, DateTime.Now, clip);
            if (e.Asks.Count > 0)
            {
                var answers = Dialogs.AskDialog.Ask(sn.Name, e.Asks);
                if (answers == null) return;
                e = SnippetMatcher.Fill(sn.Body, DateTime.Now, clip, answers);
            }
            await Task.Delay(30);
            if (typed) NativeInput.Press(NativeInput.VK_BACK, sn.Trigger.Length);
            await PasteText(e.Text);
            if (e.CursorFromEnd > 0) { await Task.Delay(60); NativeInput.Press(0x25, e.CursorFromEnd); }   // Left arrow
            await Task.Delay(400);
            if (clip != null) ClipboardWatcher.SetText(clip);   // leave the clipboard as it was
        }

        public static void RunRecipe(Recipe r, bool paste)
        {
            var t = CurrentText();
            if (t == null) { Say("Nothing to change", "The clipboard has no text."); return; }
            var result = Transforms.Apply(r, t);
            if (paste) _ = PasteText(result); else ClipboardWatcher.SetText(result);
        }

        // ------------------------------------------------------------------ per-app rules

        /// <summary>Applies the rule for the app a copy came from. Returns "never", "quiet" or "show".</summary>
        public static string ApplyRule(ClipItem item)
        {
            var rule = AppRule.For(Settings.Current.AppRules, item.SourceApp);
            if (rule == null) return "show";
            if (rule.Toast == "never") return "never";
            switch (rule.Auto)
            {
                case "strip":
                    if (item.Text != null)
                    {
                        var clean = Transforms.Find("tracking").Run(item.Text);
                        if (clean != item.Text) { ClipboardWatcher.SetText(clean); item.Text = clean; }
                    }
                    break;
                case "plain": if (item.Text != null) ClipboardWatcher.SetText(item.Text); break;
                case "clear":
                    var text = item.Text;
                    var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                    t.Tick += (s, e) => { t.Stop(); if (CurrentText() == text) SmartActions.ClearClipboard(); };
                    t.Start();
                    break;
                case "table":
                    if (item.BestFact?.Kind == "table") SmartActions.Run(item.BestFact.Actions.First(a => a.Value == "table"), item.BestFact, item);
                    break;
            }
            return rule.Toast == "quiet" ? "quiet" : "show";
        }

        // ------------------------------------------------------------------ helpers

        public static string Short(string s) { s = (s ?? "").Replace("\r", "").Replace("\n", " "); return s.Length > 80 ? s.Substring(0, 77) + "…" : s; }

        private static void Say(string title, string body) => App.Toasts.ShowNotice(title, body, App.Toasts.Mint, 3, null, Icons.Clipboard);
    }
}
