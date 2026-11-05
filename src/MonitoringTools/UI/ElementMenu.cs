using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A right-click menu for every element in a window, also opened with Shift+F10 or the Menu key.
    /// It has its own filter field with an anchored regex builder, shows each item's shortcut, and
    /// offers actions for the exact element: copy its text, search the page for it, press it,
    /// edit the appearance, or lock it.
    /// </summary>
    public static class ElementMenu
    {
        public sealed record Item(string Label, string Icon, string Keys, Action Run, bool Danger = false);

        /// <summary>How the menu behaves in one window.</summary>
        public sealed class Options
        {
            /// <summary>Only "Copy text" is offered: no Press, no appearance editing and no locks. Used where a menu entry could stand in for a deliberate confirmation.</summary>
            public bool CopyOnly;
            /// <summary>The window never takes focus (a toast). The menu briefly lets it be activated so its filter field can receive keys, then restores that.</summary>
            public bool NoActivate;
            /// <summary>Called with true when the menu opens and false when it closes, for example to hold a timer.</summary>
            public Action<bool> OnMenu;
            public static Options Copy => new Options { CopyOnly = true };
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, Options> WindowOptions = new System.Runtime.CompilerServices.ConditionalWeakTable<Window, Options>();

        private static class NoActivateStyle
        {
            private const int GWL_EXSTYLE = -20;
            private const long WS_EX_NOACTIVATE = 0x08000000L;
            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr Get(IntPtr h, int i);
            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr Set(IntPtr h, int i, IntPtr v);

            /// <summary>Clears the no-activate flag and returns whether it had been set.</summary>
            public static bool Clear(Window w)
            {
                var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return false;
                long ex = Get(h, GWL_EXSTYLE).ToInt64();
                if ((ex & WS_EX_NOACTIVATE) == 0) return false;
                Set(h, GWL_EXSTYLE, new IntPtr(ex & ~WS_EX_NOACTIVATE));
                return true;
            }

            public static void Restore(Window w)
            {
                var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;
                Set(h, GWL_EXSTYLE, new IntPtr(Get(h, GWL_EXSTYLE).ToInt64() | WS_EX_NOACTIVATE));
            }
        }

        /// <summary>Labels of the most recently opened menu, for the self-test and the capture run. Cleared when it closes.</summary>
        public static IReadOnlyList<string> LastLabels { get; private set; } = Array.Empty<string>();
        /// <summary>The card of the most recently opened menu while it is open, or null.</summary>
        public static FrameworkElement LastCard { get; private set; }
        private static Action _closeLast;
        /// <summary>Keeps a menu open when its window is not the active one, which a hidden test desktop never makes it. Never set by the app itself.</summary>
        internal static int DiagSeen, DiagTarget;
        private sealed class ExtraHolder { public Func<FrameworkElement, IEnumerable<Item>> Extra; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, ExtraHolder> Extras = new System.Runtime.CompilerServices.ConditionalWeakTable<Window, ExtraHolder>();

        /// <summary>Opens the menu for <paramref name="target"/> exactly as its window's right-click handler would, using the window's own extra items. For the self-test and the capture run.</summary>
        internal static bool OpenFor(Window w, FrameworkElement target)
        {
            if (!Extras.TryGetValue(w, out var h)) return false;
            Point p;
            try { p = target.TranslatePoint(new Point(12, target.ActualHeight / 2), w); } catch { p = new Point(12, 12); }
            Open(w, target, h.Extra, p);
            return true;
        }
        internal static bool KeepOpenForTest { get; set; }
        internal static string HookDiag = "";
        /// <summary>How many unlock prompts have been opened. For the self-test.</summary>
        internal static int PromptsOpened;
        /// <summary>How many posted clicks on a locked menu item the menu's own window stopped. For the self-test.</summary>
        internal static int PostedPressesStopped;
        /// <summary>The element menu most recently shown whose items can be locked. For the self-test.</summary>
        private static MenuPopup LastMenu;
        /// <summary>Labels of the most recently opened lock menu of a menu item. For the self-test.</summary>
        internal static IReadOnlyList<string> LastItemLockLabels { get; private set; } = Array.Empty<string>();
        /// <summary>For the self-test: the lock key of an item of the menu most recently opened, or null.</summary>
        internal static string LastMenuItemKey(string label) { var m = LastMenu; var it = m?.ItemByLabel(label); return it == null ? null : m.KeyOfItem(it); }
        /// <summary>For the self-test: the page id the items of the menu most recently opened are locked under.</summary>
        internal static string LastMenuLockPage => LastMenu?.LockPage;
        /// <summary>For the self-test: opens the lock menu of an item of the menu most recently opened, as a right-click on it would.</summary>
        internal static bool OpenItemLockMenuForTest(string label) => LastMenu?.OpenItemLockMenuForTest(label) == true;
        /// <summary>For the self-test: the row of an item of the menu most recently opened.</summary>
        internal static Button LastMenuRow(string label) => LastMenu?.RowForTest(label);

        private sealed class HwndSourceHookHolder
        {
            private readonly System.Windows.Interop.HwndSource _src;
            private readonly System.Windows.Interop.HwndSourceHook _hook;
            public HwndSourceHookHolder(System.Windows.Interop.HwndSource src, System.Windows.Interop.HwndSourceHook hook) { _src = src; _hook = hook; src.AddHook(hook); }
            public void Remove() { try { _src.RemoveHook(_hook); } catch { } }
        }
        /// <summary>Closes the menu the self-test or capture run opened.</summary>
        public static void ClearLabels() => LastLabels = Array.Empty<string>();
        public static void CloseLast() { var c = _closeLast; _closeLast = null; LastCard = null; c?.Invoke(); }

        private static readonly List<WeakReference<Window>> Attached = new List<WeakReference<Window>>();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, LockBadge> Badges = new System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, LockBadge>();
        private static readonly Dictionary<string, Window> OpenPrompts = new Dictionary<string, Window>();
        private static bool _wired;

        /// <summary>Supplies extra, window-specific items (for example "Search this page").</summary>
        public static Func<FrameworkElement, IEnumerable<Item>> Extra { get; set; }

        public static void Attach(Window w, Func<FrameworkElement, IEnumerable<Item>> extra = null, Options options = null)
        {
            bool locks = options?.CopyOnly != true;
            Extras.AddOrUpdate(w, new ExtraHolder { Extra = extra });
            if (options != null) WindowOptions.AddOrUpdate(w, options);
            if (locks) WireLocks(w);
            // A click posted to the window by another program (or a remote or assistive input route) does not move the cursor, and WPF finds the
            // target of a mouse event from the cursor. So the position carried by the click message itself is hit-tested here, and the same
            // lock veil and element menu run from it. A click the hook does not act on goes on to WPF as usual.
            bool hookOpened = false;
            void InstallHook()
            {
                var src = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(w).Handle);
                src?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (msg != 0x0201 && msg != 0x0205) return IntPtr.Zero;   // WM_LBUTTONDOWN, WM_RBUTTONUP
                    if (Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed) { HookDiag = "skipped: a mouse button is down"; return IntPtr.Zero; }   // a real mouse is down: WPF's own handlers have it
                    var tf = src.CompositionTarget;
                    if (tf == null) return IntPtr.Zero;
                    long l = lParam.ToInt64();
                    var dip = tf.TransformFromDevice.Transform(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)));
                    if (w.InputHitTest(dip) is not DependencyObject d) return IntPtr.Zero;
                    if (msg == 0x0201)
                    {
                        HookDiag = "left down at " + dip + " over " + d.GetType().Name + ", locks " + (locks && Locks.Any);
                        if (!locks || !Locks.Any) return IntPtr.Zero;
                        var locked = LockedAncestor(w, d);
                        HookDiag += "; locked ancestor " + (locked?.GetType().Name ?? "none");
                        if (locked == null)
                        {
                            var pg = PageIdOf(w);
                            for (var q = d; q != null; q = Parent(q))
                                if (q is FrameworkElement qf) HookDiag += " | " + qf.GetType().Name + " cand=" + IsCandidate(qf) + " key=" + KeyOf(qf) + " locked=" + (KeyOf(qf) != null && Locks.IsLocked(pg, KeyOf(qf)));
                            HookDiag += " page=" + pg;
                        }
                        if (locked != null) { handled = true; try { Prompt(w, locked); } catch (Exception ex) { HookDiag += "; prompt failed: " + ex.GetType().Name + " " + ex.Message; } }
                        return IntPtr.Zero;
                    }
                    DiagSeen++;
                    if (InsideText(d)) return IntPtr.Zero;
                    var target = Target(d);
                    if (target == null) return IntPtr.Zero;
                    DiagTarget++;
                    hookOpened = true;   // WPF still gets the message, so its button state is released; its own handler is told to stand down
                    Open(w, target, extra, dip);
                    return IntPtr.Zero;
                });
            }
            if (new System.Windows.Interop.WindowInteropHelper(w).Handle != IntPtr.Zero) InstallHook(); else w.SourceInitialized += (s, e) => InstallHook();
            w.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (!locks || !Locks.Any || e.OriginalSource is not DependencyObject d) return;
                if (BlockPress(w, d)) e.Handled = true;
            };
            // the release of a stopped press is stopped too: a drop-down list opens, and a tab strip tab switches, on the release
            w.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (!locks || !Locks.Any || e.OriginalSource is not DependencyObject d) return;
                if (LockedAncestor(w, d) != null) e.Handled = true;
            };
            // a tab is selected as soon as it takes keyboard focus, so focus does not move into a locked tab
            w.PreviewGotKeyboardFocus += (s, e) =>
            {
                if (!locks || !Locks.Any || e.NewFocus is not TabItem ti) return;
                if (LockedAncestor(w, ti) != null) e.Handled = true;
            };
            // a middle press closes a tab, so it is stopped on a locked element too
            w.PreviewMouseDown += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle || !locks || !Locks.Any || e.OriginalSource is not DependencyObject d) return;
                if (BlockPress(w, d)) e.Handled = true;
            };
            // typing into a locked text field is stopped the same way a click on it is
            w.PreviewTextInput += (s, e) =>
            {
                if (!locks || !Locks.Any || Keyboard.FocusedElement is not DependencyObject tf) return;
                var locked = LockedAncestor(w, tf);
                if (locked != null) { e.Handled = true; Prompt(w, locked); }
            };
            w.PreviewMouseRightButtonUp += (s, e) =>
            {
                if (hookOpened) { hookOpened = false; e.Handled = true; return; }   // the click message was acted on already
                DiagSeen++;
                if (e.OriginalSource is not DependencyObject d) return;
                if (InsideText(d)) return; // text boxes keep their own cut, copy and paste menu
                var target = Target(d);
                if (target == null) return;
                DiagTarget++;
                e.Handled = true;
                Open(w, target, extra, e.GetPosition(w));
            };
            w.PreviewKeyDown += (s, e) =>
            {
                if (locks && Locks.Any && Keyboard.FocusedElement is DependencyObject lf && EditsOrActivates(lf, e))
                {
                    var locked = LockedAncestor(w, lf);
                    if (locked != null) { e.Handled = true; Prompt(w, locked); return; }
                }
                if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is DependencyObject fd && !InsideText(fd))
                {
                    var t = Target(fd);
                    var tx = t == null ? "" : TextOf(t);
                    if (tx.Length > 0) { ClipboardWatcher.SetText(tx); e.Handled = true; }
                    return;
                }
                bool menuKey = e.Key == Key.Apps || (e.Key == Key.System && e.SystemKey == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift) || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift);
                if (!menuKey) return;
                if (Keyboard.FocusedElement is not DependencyObject d || InsideText(d)) return;
                var target = Target(d);
                if (target == null) return;
                e.Handled = true;
                Point p;
                try { p = target.TranslatePoint(new Point(12, target.ActualHeight / 2), w); }
                catch { p = Mouse.GetPosition(w); }   // an element that cannot report its place falls back to the pointer
                Open(w, target, extra, p);
            };
        }

        /// <summary>What a left press on <paramref name="d"/> does under a lock: if it, or something around it, is locked, the unlock prompt opens and the press is stopped. The window's press handler and the self-test both call this.</summary>
        internal static bool BlockPress(Window w, DependencyObject d)
        {
            var locked = LockedAncestor(w, d);
            if (locked == null) return false;
            Prompt(w, locked);
            return true;
        }

        /// <summary>True for a key that would change or activate the focused element: Enter and Space anywhere, the keys that move a choice, slider or list, and the editing keys of a text box. Tab, Escape and copying are left alone so a locked page can still be left.</summary>
        private static bool EditsOrActivates(DependencyObject focused, KeyEventArgs e)
        {
            var k = e.Key == Key.System ? e.SystemKey : e.Key;
            if (k == Key.Enter || k == Key.Space) return true;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (InsideText(focused)) return k == Key.Back || k == Key.Delete || (ctrl && (k == Key.V || k == Key.X || k == Key.Z || k == Key.Y)) || (Keyboard.Modifiers == ModifierKeys.Shift && k == Key.Insert);
            // F4 and Alt+Down open a drop-down list; Delete closes a tab
            return k is Key.Up or Key.Down or Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.F4 or Key.Delete;
        }

        /// <summary>For the self-test: whether the element is something a toy lock can be put on.</summary>
        internal static bool IsLockCandidateForTest(FrameworkElement fe) => IsCandidate(fe);

        private static bool InsideText(DependencyObject d)
        {
            for (; d != null; d = Parent(d)) if (d is TextBox) return true;
            return false;
        }

        private static DependencyObject Parent(DependencyObject d) => d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

        /// <summary>The nearest element a person would think of as "this": a search result, a button, or a card.</summary>
        private static FrameworkElement Target(DependencyObject d)
        {
            FrameworkElement button = null, card = null;
            for (; d != null; d = Parent(d))
            {
                if (d is FrameworkElement fe)
                {
                    if (SearchScope.GetItem(fe) != null) return fe;
                    if (button == null && (fe is ButtonBase || fe is ComboBox || fe is Dialogs.Dropdown || fe is TabItem)) button = fe;
                    if (card == null && fe is Border b && b.CornerRadius.TopLeft >= 20 && b.Child != null) card = fe;
                }
            }
            return button ?? card;
        }

        private static string TextOf(FrameworkElement t)
        {
            var s = SearchScope.Text(t);
            s = string.Join(" ", s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            return s.Trim();
        }

        /// <summary>Shows only the given items, as an action menu for a "more" button.</summary>
        public static void OpenItems(Window w, FrameworkElement origin, IEnumerable<Item> items, Point at) => new MenuPopup(items.ToList(), origin, null).Show(w, at);

        public static void Open(Window w, FrameworkElement target, Func<FrameworkElement, IEnumerable<Item>> extra, Point at)
        {
            var text = TextOf(target);
            WindowOptions.TryGetValue(w, out var opts);
            if (opts?.CopyOnly == true)
            {
                var only = new List<Item>();
                if (text.Length > 0) only.Add(new Item("Copy text", Icons.Clipboard, "Ctrl+C", () => ClipboardWatcher.SetText(text)));
                if (only.Count == 0) return;
                new MenuPopup(only, target, null).Show(w, at);
                return;
            }
            var pageId = LockPageFor(w, target);
            var key = KeyOf(target);
            var entry = key == null ? null : FindLock(w, target, key);
            bool locked = entry != null && !Locks.IsOpen(entry);
            var items = new List<Item>();
            if (!locked && target is ButtonBase bb && bb.IsEnabled)
                items.Add(new Item("Press", Icons.Play, "Enter", () => bb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent))));
            if (text.Length > 0)
                items.Add(new Item("Copy text", Icons.Clipboard, "Ctrl+C", () => ClipboardWatcher.SetText(text)));
            if (extra != null) items.AddRange(extra(target) ?? Enumerable.Empty<Item>());
            items.Add(new Item("Edit appearance…", Icons.Palette, null, () => AppearanceEditorWindow.Open(w, target)));
            if (key != null)
            {
                if (entry == null)
                    items.Add(new Item("Lock this element…", Icons.Shield, null, () => new LockWizardWindow(target, pageId, key, key, null).Show()));
                else if (locked)
                    items.Add(new Item("Unlock this element…", Icons.Shield, null, () => PromptEntry(entry, target)));
                else
                {
                    items.Add(new Item("Lock again", Icons.Shield, null, () => Locks.Relock(entry.Id)));
                    items.Add(new Item("Remove this lock", Icons.Trash, null, () => { var err = Locks.Remove(entry.Id); if (err != null) Log.Warn("Lock not removed: " + err); }, true));
                }
            }

            bool restoreStyle = false;
            if (opts?.NoActivate == true)
            {
                restoreStyle = NoActivateStyle.Clear(w);
                try { w.Activate(); } catch { }
            }
            opts?.OnMenu?.Invoke(true);
            var menu = new MenuPopup(items, target, () =>
            {
                if (restoreStyle) NoActivateStyle.Restore(w);
                opts?.OnMenu?.Invoke(false);
            }) { LockPage = MenuPageOf(w), LockOrigin = key ?? KeyOfText(text) };
            menu.Show(w, at);
        }

        // -------------------------------------------------------------- toy locks

        /// <summary>The identity of the page a window is showing, so the same words on two pages can be locked separately.</summary>
        public static string PageIdOf(Window w) => w is ControlCentreWindow c && c.PageSearch != null ? c.PageSearch.State.Id.Replace("page:", "") : w.GetType().Name;

        /// <summary>
        /// The lock key for one element, or null when it has no words. It is the element's words, and when an earlier lockable element in
        /// the same window says exactly the same words, its position among them: " #2" for the second, " #3" for the third. So two buttons
        /// that both say "Copy" are locked separately. The first one keeps the plain words, which is also the key every lock saved before
        /// positions were added carries, so those locks still hold (on the first element that says those words).
        /// </summary>
        public static string KeyOf(FrameworkElement t) => KeyWithPosition(t, BaseKeyOf(t));

        private static string KeyWithPosition(FrameworkElement t, string baseKey)
        {
            if (baseKey == null) return null;
            int n = SameWordsPosition(t, baseKey);
            return n <= 1 ? baseKey : baseKey + " #" + n;
        }

        /// <summary>
        /// The words an element is known by, without its position: its visible text, or for a control whose text is its current value (a
        /// drop-down list) its name, or for a tab its header. Search-only words are left out so the key does not change when a setting flips.
        /// </summary>
        public static string BaseKeyOf(FrameworkElement t)
        {
            if (t is ComboBox || t is Dialogs.Dropdown)
            {
                // the shown text is the chosen value, which would change the key whenever the choice changed
                var name = AutomationProperties.GetName(t);
                if (string.IsNullOrWhiteSpace(name)) name = t.ToolTip as string;
                return string.IsNullOrWhiteSpace(name) ? null : KeyOfText("drop-down list " + name);
            }
            if (t is TabItem ti)
            {
                var header = ti.Header as string ?? (ti.Header is DependencyObject hd ? SearchScope.Text(hd) : null);
                if (string.IsNullOrWhiteSpace(header)) header = AutomationProperties.GetName(ti);
                return string.IsNullOrWhiteSpace(header) ? null : KeyOfText("tab " + header);
            }
            var s = SearchScope.Text(t);
            var extra = SearchScope.GetItem(t);
            if (!string.IsNullOrEmpty(extra) && s.StartsWith(extra)) s = s.Substring(extra.Length);
            return KeyOfText(s);
        }

        /// <summary>Where this element stands among the lockable elements of its window that say the same words: 1 for the first, in the order the window lays them out.</summary>
        private static int SameWordsPosition(FrameworkElement t, string baseKey)
        {
            DependencyObject root = Window.GetWindow(t);
            if (root == null) { root = t; for (var p = VisualTreeHelper.GetParent(root); p != null; p = VisualTreeHelper.GetParent(root)) root = p; }
            int n = 0;
            bool found = false;
            void Walk(DependencyObject d)
            {
                if (found) return;
                if (d is FrameworkElement fe && IsCandidate(fe))
                {
                    if (ReferenceEquals(fe, t)) { n++; found = true; return; }
                    if (BaseKeyOf(fe) == baseKey) n++;
                }
                int c = VisualTreeHelper.GetChildrenCount(d);
                for (int i = 0; i < c && !found; i++) Walk(VisualTreeHelper.GetChild(d, i));
            }
            Walk(root);
            return found ? n : 1;
        }

        /// <summary>The page id of the menu items of a window's element menus. Menu items are kept apart from the elements of the page itself.</summary>
        public static string MenuPageOf(Window w) => PageIdOf(w) + "/menu";

        /// <summary>The lock key of one item of an element's menu: the words of the element the menu is for, then the item. So "Press" in the menu of one button is locked apart from "Press" in the menu of another.</summary>
        public static string MenuItemKey(string originKey, string label) => (string.IsNullOrEmpty(originKey) ? "(no words)" : originKey) + " > " + label;

        /// <summary>The page id locks on the control centre's tab strip tabs are kept under. A tab shows on every page, so its lock holds on every page.</summary>
        public const string TabsPage = "tabs";

        /// <summary>The page id a lock on this element is kept under: the window's page, or for a tab strip tab the tab strip itself.</summary>
        public static string LockPageFor(Window w, FrameworkElement fe) => fe is Tabs.StripTab ? TabsPage : PageIdOf(w);

        /// <summary>The lock on an element, open or closed, or null. A tab strip tab locked before tabs had their own page id is still found on the page it was locked on.</summary>
        private static LockEntry FindLock(Window w, FrameworkElement fe, string key) =>
            Locks.Find(LockPageFor(w, fe), key) ?? (fe is Tabs.StripTab ? Locks.Find(PageIdOf(w), key) : null);

        /// <summary>The lock on an element when it is locked and still closed, or null. The position is only worked out when a lock on the page could match these words.</summary>
        private static LockEntry ClosedLockOn(Window w, FrameworkElement fe)
        {
            var b = BaseKeyOf(fe);
            if (b == null) return null;
            var page = LockPageFor(w, fe);
            if (!Locks.AnyFor(page, b) && !(fe is Tabs.StripTab && Locks.AnyFor(PageIdOf(w), b))) return null;
            var e = FindLock(w, fe, KeyWithPosition(fe, b));
            return e != null && !Locks.IsOpen(e) ? e : null;
        }

        private const string LockTip = "Locked. Click to unlock. Just for fun, not security.";

        public static string KeyOfText(string s)
        {
            // the veil puts its own tooltip on a locked element; it is not part of what the element says, or a lock would lose its key the moment it closed
            if (!string.IsNullOrEmpty(s)) { s = s.Replace(LockTip, ""); var yue = Core.Language.Yue(LockTip); if (!string.IsNullOrEmpty(yue)) s = s.Replace(yue, ""); }
            s = string.Join(" ", (s ?? "").Replace(ElementStyles.Hair.ToString(), "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (s.Length == 0) return null;
            return s.Length > 120 ? s[..120] : s;
        }

        /// <summary>What a toy lock can be put on: a button of any kind (including a radio button, a switch and a toggle), a drop-down list, a tab,
        /// a search result, or a card. A tab strip tab is a rounded card, so it is one too.</summary>
        private static bool IsCandidate(FrameworkElement fe) =>
            fe is ButtonBase || fe is ComboBox || fe is Dialogs.Dropdown || fe is TabItem || SearchScope.GetItem(fe) != null || (fe is Border b && b.CornerRadius.TopLeft >= 20 && b.Child != null);

        /// <summary>The nearest ancestor (or the element itself) that is under a lock that is still closed.</summary>
        private static FrameworkElement LockedAncestor(Window w, DependencyObject d)
        {
            for (; d != null; d = Parent(d))
            {
                if (d is FrameworkElement fe && IsCandidate(fe) && ClosedLockOn(w, fe) != null) return fe;
            }
            return null;
        }

        private static void Prompt(Window w, FrameworkElement locked)
        {
            var entry = ClosedLockOn(w, locked);
            if (entry != null) PromptEntry(entry, locked);
        }

        /// <summary>Opens the unlock prompt for a lock beside its element. One prompt per lock.</summary>
        public static void PromptEntry(LockEntry entry, FrameworkElement origin)
        {
            if (OpenPrompts.TryGetValue(entry.Id, out var existing) && existing.IsLoaded) { existing.Activate(); return; }
            PromptsOpened++;
            var win = new LockPromptWindow(entry, origin, null);
            OpenPrompts[entry.Id] = win;
            win.Closed += (s, e) => OpenPrompts.Remove(entry.Id);
            win.Show();
        }

        private static void WireLocks(Window w)
        {
            Locks.Start();
            Attached.RemoveAll(r => !r.TryGetTarget(out _));
            Attached.Add(new WeakReference<Window>(w));
            if (_wired) return;
            _wired = true;
            // A page that is built after a lock exists still shows its lock the moment its elements load.
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) =>
            {
                if (!Locks.Any || s is not FrameworkElement fe || !IsCandidate(fe)) return;
                var win = Window.GetWindow(fe);
                if (win != null && IsAttached(win)) Style(win, fe);
            }));
            // A locked button refuses to be clicked by any route, including a programmatic one.
            EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                if (!Locks.Any || s is not ButtonBase b) return;
                var win = Window.GetWindow(b);
                if (win != null && IsAttached(win) && LockedAncestor(win, b) != null) e.Handled = true;
            }));
            Locks.Changed += RefreshAll;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) => { if (Locks.Any) RefreshAll(); };
            timer.Start();
        }

        private static bool IsAttached(Window w) => Attached.Any(r => r.TryGetTarget(out var x) && x == w);

        private static void RefreshAll()
        {
            foreach (var r in Attached.ToList())
                if (r.TryGetTarget(out var w) && w.IsLoaded) Walk(w, w);
        }

        private static void Walk(Window w, DependencyObject d)
        {
            if (d is FrameworkElement fe && IsCandidate(fe)) Style(w, fe);
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++) Walk(w, VisualTreeHelper.GetChild(d, i));
        }

        /// <summary>Shows or clears the lock look on one element: a dimmed veil, a lock chip, and a spoken state.</summary>
        private static void Style(Window w, FrameworkElement fe)
        {
            bool locked = ClosedLockOn(w, fe) != null;
            if (Badges.TryGetValue(fe, out var badge))
            {
                if (!locked) { badge.Remove(); Badges.Remove(fe); AutomationProperties.SetHelpText(fe, ""); }
                return;
            }
            if (!locked) return;
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(fe);
            Badges.Add(fe, new LockBadge(fe, layer));
            AutomationProperties.SetHelpText(fe, "Locked. Activate it to unlock.");
        }

        /// <summary>A veil and a lock chip over a locked element. It does not take clicks, so the window's own handler opens the prompt.</summary>
        private sealed class LockBadge : System.Windows.Documents.Adorner
        {
            private readonly System.Windows.Documents.AdornerLayer _layer;
            public LockBadge(FrameworkElement el, System.Windows.Documents.AdornerLayer layer) : base(el)
            {
                _layer = layer; IsHitTestVisible = false;
                layer?.Add(this);
                // the element's own tooltip stays: it is part of the words the lock is keyed by, and replacing it made the key change the moment the lock closed
                _originalTip = el.ToolTip;
                el.ToolTip = _originalTip is string o && o.Length > 0 ? o + " " + LockTip : LockTip;
            }
            private readonly object _originalTip;
            public void Remove() { _layer?.Remove(this); if (AdornedElement is FrameworkElement f && f.ToolTip is string t && t.Contains(LockTip)) f.ToolTip = _originalTip; }
            protected override void OnRender(DrawingContext dc)
            {
                var size = AdornedElement.RenderSize;
                var veil = new SolidColorBrush(((SolidColorBrush)Ui.Res("B.Window")).Color) { Opacity = 0.55 };
                dc.DrawRoundedRectangle(veil, null, new Rect(size), 12, 12);
                double r = Math.Min(11, Math.Max(7, Math.Min(size.Width, size.Height) / 3));
                var c = new Point(Math.Max(r + 2, size.Width - r - 4), Math.Min(size.Height - r - 2, r + 4));
                dc.DrawEllipse(Ui.Res("B.Mint"), null, c, r, r);
                var pen = new Pen(Ui.Res("B.MintInk"), 1.4);
                dc.DrawRectangle(null, pen, new Rect(c.X - r * 0.42, c.Y - r * 0.05, r * 0.84, r * 0.6));
                var shackle = Geometry.Parse(string.Format(System.Globalization.CultureInfo.InvariantCulture, "M{0},{1} L{0},{2} A{3},{3} 0 0 1 {4},{2} L{4},{1}", c.X - r * 0.25, c.Y - r * 0.05, c.Y - r * 0.4, r * 0.25, c.X + r * 0.25));
                dc.DrawGeometry(null, pen, shackle);
            }
        }

        /// <summary>The menu surface: a Material 3 menu with a filter, keyboard navigation and shortcut hints.</summary>
        private sealed class MenuPopup
        {
            private readonly List<Item> _items;
            private readonly Popup _popup;
            private readonly StackPanel _rows = new StackPanel();
            private readonly SearchField _filter;
            private readonly Border _card;
            private readonly FrameworkElement _origin;
            private List<Item> _shown;
            private int _sel;
            private readonly Action _onClosed;
            private readonly Dictionary<Button, Item> _rowItems = new Dictionary<Button, Item>();
            private Window _owner;
            private Point _at;
            private HwndSourceHookHolder _hook;
            /// <summary>The page id the items of this menu are locked under, or null when this menu's items cannot be locked.</summary>
            public string LockPage { get; init; }
            /// <summary>The words of the element the menu is for; part of each item's lock key.</summary>
            public string LockOrigin { get; init; }

            public MenuPopup(List<Item> items, FrameworkElement origin, Action onClosed)
            {
                _items = items;
                _onClosed = onClosed;
                _origin = origin;
                _filter = new SearchField("menu:element", "Filter actions", compact: true) { Margin = new Thickness(0, 0, 0, 6) };
                _filter.SampleSource = () => _items.Select(i => i.Label);
                _filter.Filter += Render;
                var stack = new StackPanel();
                stack.Children.Add(_filter);
                stack.Children.Add(_rows);
                _card = new Border
                {
                    Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(16), Padding = new Thickness(8), MinWidth = 280, MaxWidth = 380,
                    Margin = new Thickness(0, 0, 20, 20), Child = stack,
                    Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"],
                    RenderTransformOrigin = new Point(0, 0), RenderTransform = new ScaleTransform(1, 1)
                };
                AutomationProperties.SetName(_card, "Actions for this element");
                _popup = new Popup { Child = _card, StaysOpen = KeepOpenForTest, AllowsTransparency = true, Placement = PlacementMode.Relative, Focusable = true };
                _card.PreviewKeyDown += OnKey;
                // a locked item: a press on it is stopped and its unlock prompt opens; the menu is a window of its own, so the window's lock handlers never see it
                _card.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    var row = RowOf(e.OriginalSource as DependencyObject);
                    if (row == null) return;
                    var entry = ClosedLockOf(_rowItems[row]);
                    if (entry != null) { e.Handled = true; PromptItem(entry); }
                };
                // right-click on an item offers to lock it
                _card.PreviewMouseRightButtonUp += (s, e) =>
                {
                    var row = RowOf(e.OriginalSource as DependencyObject);
                    if (row == null || LockPage == null) return;
                    e.Handled = true;
                    OpenItemLockMenu(row);
                };
                Locks.Changed += OnLocksChanged;
                _popup.Opened += (s, e) => HookPostedPress();
                _popup.Closed += (s, e) =>
                {
                    Locks.Changed -= OnLocksChanged;
                    _hook?.Remove(); _hook = null;
                    if (LastCard == _card) LastCard = null;
                    if (!_filter.BuilderOpen) _origin.Focus();
                    _onClosed?.Invoke();
                };
            }

            private void OnLocksChanged() { if (_popup.IsOpen) Render(); }

            internal Item ItemByLabel(string label) => _items.FirstOrDefault(i => i.Label == label);

            private Button RowOf(DependencyObject d)
            {
                for (; d != null && d != _card; d = Parent(d)) if (d is Button b && _rowItems.ContainsKey(b)) return b;
                return null;
            }

            /// <summary>The lock key of one item of this menu, or null when this menu's items cannot be locked.</summary>
            public string KeyOfItem(Item it) => LockPage == null ? null : MenuItemKey(LockOrigin, it.Label);

            private LockEntry ClosedLockOf(Item it)
            {
                if (LockPage == null || !Locks.Any) return null;
                var e = Locks.Find(LockPage, KeyOfItem(it));
                return e != null && !Locks.IsOpen(e) ? e : null;
            }

            private void PromptItem(LockEntry entry)
            {
                if (!KeepOpenForTest) _popup.IsOpen = false;
                PromptEntry(entry, _origin);
            }

            /// <summary>The small menu that locks, unlocks or removes the lock on one item of this menu.</summary>
            private void OpenItemLockMenu(Button row)
            {
                var it = _rowItems[row];
                var key = KeyOfItem(it);
                var entry = Locks.Find(LockPage, key);
                var items = new List<Item>();
                if (entry == null)
                    items.Add(new Item("Lock this menu item…", Icons.Shield, null, () => new LockWizardWindow(_origin, LockPage, key, key, null).Show()));
                else if (!Locks.IsOpen(entry))
                    items.Add(new Item("Unlock this menu item…", Icons.Shield, null, () => PromptEntry(entry, _origin)));
                else
                {
                    items.Add(new Item("Lock again", Icons.Shield, null, () => Locks.Relock(entry.Id)));
                    items.Add(new Item("Remove this lock", Icons.Trash, null, () => { var err = Locks.Remove(entry.Id); if (err != null) Log.Warn("Lock not removed: " + err); }, true));
                }
                Point p;
                try { p = row.TranslatePoint(new Point(row.ActualWidth / 2, row.ActualHeight / 2), _card); p = new Point(_at.X + p.X, _at.Y + p.Y); } catch { p = _at; }
                LastItemLockLabels = items.Select(i => i.Label).ToList();
                if (!KeepOpenForTest) _popup.IsOpen = false;
                if (_owner != null) new MenuPopup(items, _origin, null).Show(_owner, p);
            }

            /// <summary>For the self-test: opens the lock menu of the item with this label, as a right-click on it would.</summary>
            internal bool OpenItemLockMenuForTest(string label)
            {
                var row = _rowItems.FirstOrDefault(p => p.Value.Label == label).Key;
                if (row == null) return false;
                OpenItemLockMenu(row);
                return true;
            }

            /// <summary>For the self-test: the row of the item with this label, or null.</summary>
            internal Button RowForTest(string label) => _rowItems.FirstOrDefault(p => p.Value.Label == label).Key;

            /// <summary>
            /// A click posted to the menu's own window by another program does not move the cursor, so WPF cannot tell which item it was meant
            /// for. The point the click message carries is hit-tested here, and a press on a locked item is stopped with its unlock prompt.
            /// </summary>
            private void HookPostedPress()
            {
                if (LockPage == null || PresentationSource.FromVisual(_card) is not System.Windows.Interop.HwndSource src) return;
                _hook = new HwndSourceHookHolder(src, (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (msg != 0x0201 || !Locks.Any) return IntPtr.Zero;   // WM_LBUTTONDOWN
                    if (Mouse.LeftButton == MouseButtonState.Pressed) return IntPtr.Zero;   // a real mouse: WPF's own handlers have it
                    var ct = src.CompositionTarget;
                    if (ct == null || src.RootVisual is not UIElement root) return IntPtr.Zero;
                    long l = lParam.ToInt64();
                    var dip = ct.TransformFromDevice.Transform(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)));
                    var row = RowOf(root.InputHitTest(dip) as DependencyObject);
                    if (row == null) return IntPtr.Zero;
                    var entry = ClosedLockOf(_rowItems[row]);
                    if (entry == null) return IntPtr.Zero;
                    handled = true;
                    PostedPressesStopped++;
                    PromptItem(entry);
                    return IntPtr.Zero;
                });
            }

            public void Show(Window w, Point at)
            {
                _owner = w; _at = at;
                if (LockPage != null) LastMenu = this;
                _filter.State.SetQuery("");
                _popup.PlacementTarget = w;
                _popup.HorizontalOffset = at.X;
                _popup.VerticalOffset = at.Y;
                Render();
                _popup.IsOpen = true;
                LastLabels = _items.Select(i => i.Label).ToList();
                LastCard = _card;
                _closeLast = () => _popup.IsOpen = false;
                if (!Appearance.ReduceMotion)
                {
                    var t = (ScaleTransform)_card.RenderTransform;
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    t.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
                    t.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
                    _card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
                }
                _filter.Box.Focus();
            }

            private void Render()
            {
                _rowItems.Clear();
                _shown = _items.Where(i => _filter.State.Matches(i.Label + " " + (i.Keys ?? ""))).ToList();
                _filter.SetCount(_shown.Count, _items.Count, "actions");
                if (_sel >= _shown.Count) _sel = 0;
                _rows.Children.Clear();
                for (int i = 0; i < _shown.Count; i++) _rows.Children.Add(Row(_shown[i], i));
            }

            private FrameworkElement Row(Item it, int index)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var ink = it.Danger ? Ui.Res("B.Danger") : Ui.Res("B.Text");
                g.Children.Add(new ContentControl { Content = Ui.Icon(it.Icon ?? Icons.Bolt, it.Danger ? ink : Ui.Res("B.TextSoft"), 16), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Focusable = false });
                var label = new TextBlock { Text = it.Label, Foreground = ink, FontSize = 14, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(label, 1); g.Children.Add(label);
                if (!string.IsNullOrEmpty(it.Keys))
                {
                    var k = Ui.Mono(it.Keys, Ui.Res("B.TextMute"), 11);
                    k.VerticalAlignment = VerticalAlignment.Center; k.Margin = new Thickness(16, 0, 0, 0);
                    Grid.SetColumn(k, 2); g.Children.Add(k);
                }
                var b = new Button { Style = Ui.Style("Btn.Row"), Content = g, Height = 44, Margin = new Thickness(0, 1, 0, 1) };
                if (index == _sel) b.Background = Ui.Res("B.SecondaryContainer");
                AutomationProperties.SetName(b, it.Label + (string.IsNullOrEmpty(it.Keys) ? "" : ", " + it.Keys));
                _rowItems[b] = it;
                if (ClosedLockOf(it) != null)
                {
                    // drawn inside the row (not as an adorner over it) so it is part of the menu card wherever the card is shown or captured
                    AutomationProperties.SetHelpText(b, "Locked. Activate it to unlock.");
                    foreach (UIElement c in g.Children) c.Opacity = 0.45;
                    var chip = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.Mint"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = Ui.Icon(Icons.Shield, Ui.Res("B.MintInk"), 14), ToolTip = LockTip };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    Grid.SetColumn(chip, 3); g.Children.Add(chip);
                }
                b.Click += (s, e) => Run(it);
                return b;
            }

            private void Run(Item it)
            {
                // every way of choosing an item (a click, Enter, Space, an assistive invoke) ends here, so a locked item is stopped here too
                var entry = ClosedLockOf(it);
                if (entry != null) { PromptItem(entry); return; }
                _popup.IsOpen = false;
                try { it.Run?.Invoke(); } catch (Exception ex) { Log.Warn("Menu action failed: " + ex.Message); }
            }

            private void OnKey(object sender, KeyEventArgs e)
            {
                if (_filter.BuilderOpen) return;
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    if (_filter.State.Query.Length > 0) _filter.State.SetQuery("");
                    else _popup.IsOpen = false;
                }
                else if (e.Key == Key.Down && _shown.Count > 0) { e.Handled = true; _sel = (_sel + 1) % _shown.Count; Render(); }
                else if (e.Key == Key.Up && _shown.Count > 0) { e.Handled = true; _sel = (_sel - 1 + _shown.Count) % _shown.Count; Render(); }
                else if (e.Key == Key.Enter && _shown.Count > 0) { e.Handled = true; Run(_shown[_sel]); }
                else if (LockPage != null && _shown.Count > 0 && (e.Key == Key.Apps || (Keyboard.Modifiers == ModifierKeys.Shift && (e.Key == Key.F10 || (e.Key == Key.System && e.SystemKey == Key.F10)))))
                {
                    // the menu key on an item offers to lock it, as a right-click on it does
                    e.Handled = true;
                    var row = _rowItems.FirstOrDefault(p => p.Value == _shown[_sel]).Key;
                    if (row != null) OpenItemLockMenu(row);
                }
            }
        }
    }
}
