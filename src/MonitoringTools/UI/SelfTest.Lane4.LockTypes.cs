using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MonitoringTools.Core;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    // The element types the first lock walk did not meet on the real pages: a radio button, a drop-down list (the app's own and a WPF
    // ComboBox), a tab (a WPF TabItem and a tab of the control centre's tab strip) and an item of an element's menu. Each is locked, a left
    // press and a key on it are sent through the window's real input route and shown to be stopped with the unlock prompt opened and the
    // value untouched, the PIN opens it, the same press then reaches the control, and the open lock is removed. A second check shows that
    // two elements with the same words are locked apart, and that a lock saved with the words alone still holds on the first of them.
    public static partial class SelfTest
    {
        private static MouseButtonEventArgs MouseArgs(UIElement target, RoutedEvent ev) =>
            new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = ev, Source = target };

        /// <summary>Sends a left press and release to <paramref name="target"/> the way the input system routes them: the mouse-down preview tunnels from the window (each element on the way raises its left-button preview from it), and the bubbling event follows only when no preview handler took it. Returns whether the press was taken by a preview handler.</summary>
        private static bool SendPress(UIElement target)
        {
            var down = MouseArgs(target, Mouse.PreviewMouseDownEvent);
            target.RaiseEvent(down);
            if (!down.Handled) target.RaiseEvent(MouseArgs(target, Mouse.MouseDownEvent));
            var up = MouseArgs(target, Mouse.PreviewMouseUpEvent);
            target.RaiseEvent(up);
            if (!up.Handled) target.RaiseEvent(MouseArgs(target, Mouse.MouseUpEvent));
            return down.Handled;
        }

        /// <summary>Sends a key down and up to <paramref name="target"/> the same way. Returns whether the key was taken by a preview handler.</summary>
        private static bool SendKey(UIElement target, Key key)
        {
            var src = PresentationSource.FromVisual(target);
            KeyEventArgs Args(RoutedEvent ev) => new KeyEventArgs(Keyboard.PrimaryDevice, src, Environment.TickCount, key) { RoutedEvent = ev, Source = target };
            var down = Args(Keyboard.PreviewKeyDownEvent);
            target.RaiseEvent(down);
            if (!down.Handled) target.RaiseEvent(Args(Keyboard.KeyDownEvent));
            var up = Args(Keyboard.PreviewKeyUpEvent);
            target.RaiseEvent(up);
            if (!up.Handled) target.RaiseEvent(Args(Keyboard.KeyUpEvent));
            return down.Handled;
        }

        /// <summary>
        /// One element type: lock it, show a press and a key are stopped with the prompt and the value is untouched, open it with the PIN,
        /// show the same press is no longer stopped and that <paramref name="change"/> (the control's own input route) now changes the value,
        /// then remove the lock. Problems are added to <paramref name="problems"/>; what was shown goes to <paramref name="proof"/>.
        /// </summary>
        private static async Task WalkLockType(string kind, Window w, string page, FrameworkElement el, Func<string> value, Key key, Func<Task> change, List<string> problems, Dictionary<string, string> proof)
        {
            var lockKey = ElementMenu.KeyOf(el);
            if (lockKey == null) { problems.Add(kind + ": it has no lock key"); return; }
            if (!ElementMenu.IsLockCandidateForTest(el)) { problems.Add(kind + ": it is not something a lock can be put on"); return; }
            var err = Locks.Create(page, lockKey, lockKey, LockPolicy.Pin, 5, "4826", null, null);
            if (err != null) { problems.Add(kind + ": lock not made: " + err); return; }
            var steps = new List<string>();
            try
            {
                await Task.Delay(250);
                var before = value();
                int p0 = ElementMenu.PromptsOpened;
                bool stopped = SendPress(el); await Task.Delay(150);
                if (!stopped || ElementMenu.PromptsOpened <= p0) problems.Add(kind + ": a press was not stopped (taken " + stopped + ", prompts " + (ElementMenu.PromptsOpened - p0) + ")");
                else steps.Add("press stopped with the prompt");
                ClosePrompts(); await Task.Delay(80);
                el.Focus(); await Task.Delay(60);
                if (Keyboard.FocusedElement == el || (Keyboard.FocusedElement is DependencyObject f && el.IsAncestorOf(f)))
                {
                    p0 = ElementMenu.PromptsOpened;
                    bool kstopped = SendKey(Keyboard.FocusedElement as UIElement ?? el, key); await Task.Delay(150);
                    if (!kstopped || ElementMenu.PromptsOpened <= p0) problems.Add(kind + ": " + key + " was not stopped");
                    else steps.Add(key + " stopped with the prompt");
                    ClosePrompts(); await Task.Delay(80);
                }
                else if (el is TabItem) steps.Add("keyboard focus refused, so no key can select it");
                else problems.Add(kind + ": it could not take keyboard focus, so the key was not driven");
                if (value() != before) problems.Add(kind + ": the value changed while locked: " + before + " -> " + value());
                else steps.Add("value unchanged (" + before + ")");
            }
            finally
            {
                ClosePrompts();
                var ent = Locks.Find(page, lockKey);
                if (ent != null)
                {
                    var r = new Locks.Session(ent).Submit("4826");
                    await Task.Delay(250);
                    if (Locks.IsLocked(page, lockKey)) problems.Add(kind + ": the PIN did not open the lock (" + r.Status + ")");
                    else
                    {
                        steps.Add("PIN opened it");
                        var before = value();
                        int p0 = ElementMenu.PromptsOpened;
                        await change(); await Task.Delay(250);
                        if (ElementMenu.PromptsOpened != p0) problems.Add(kind + ": after the PIN the press still opened the prompt");
                        else if (value() == before) problems.Add(kind + ": after the PIN the press did not change the value (" + before + ")");
                        else steps.Add("then the press went through (" + before + " -> " + value() + ")");
                    }
                    ClosePrompts();
                    var gone = Locks.Remove(ent.Id);
                    if (gone != null) problems.Add(kind + ": an open lock could not be removed: " + gone);
                    else steps.Add("lock removed");
                }
            }
            proof[kind] = string.Join(", ", steps);
        }

        private static async Task LockTypeChecksAsync()
        {
            await CheckAsync("locks on every element type: a radio button, a drop-down list, a tab and a menu item are locked, stopped for a press and a key with the prompt opened, and opened again by the PIN", async () =>
            {
                var problems = new List<string>();
                var proof = new Dictionary<string, string>();
                ElementMenu.KeepOpenForTest = true;

                // a window that carries locks, built from the app's own controls
                var radioA = new RadioButton { Content = "Walk choice A", GroupName = "lockwalk", IsChecked = true, Margin = new Thickness(4) };
                var radioB = new RadioButton { Content = "Walk choice B", GroupName = "lockwalk", Margin = new Thickness(4) };
                var combo = new ComboBox { Width = 220, Margin = new Thickness(4) };
                foreach (var c in new[] { "First", "Second", "Third" }) combo.Items.Add(new ComboBoxItem { Content = c });
                combo.SelectedIndex = 0;
                System.Windows.Automation.AutomationProperties.SetName(combo, "Walk list");
                var drop = new Dropdown("Walk drop-down", new[] { new Choice("one", "One"), new Choice("two", "Two"), new Choice("three", "Three") }, "one") { Width = 220, Margin = new Thickness(4) };
                var tabs = new TabControl { Height = 120, Margin = new Thickness(4) };
                tabs.Items.Add(new TabItem { Header = "Walk tab one", Content = new TextBlock { Text = "First page" } });
                var tab2 = new TabItem { Header = "Walk tab two", Content = new TextBlock { Text = "Second page" } };
                tabs.Items.Add(tab2);
                tabs.SelectedIndex = 0;
                int pressed = 0;
                var menuBtn = new Button { Content = "Walk menu sample", Style = Ui.Style("Btn.Ghost"), Margin = new Thickness(4) };
                menuBtn.Click += (s, e) => pressed++;
                var host = new Window { Width = 520, Height = 620, Title = "Lock types", Background = Ui.Res("B.Window"), Content = new StackPanel { Margin = new Thickness(16), Children = { radioA, radioB, combo, drop, tabs, menuBtn } } };
                ControlCentreWindow cc = null;
                try
                {
                    host.Show(); await Wave6Flows.Settle(host, 600);
                    ElementMenu.Attach(host);
                    var page = ElementMenu.PageIdOf(host);

                    await WalkLockType("radio button (RadioButton)", host, page, radioB, () => "B " + radioB.IsChecked, Key.Space,
                        async () => { SendPress(radioB); await Task.Delay(100); if (radioB.IsChecked != true) { radioB.Focus(); SendKey(radioB, Key.Space); } }, problems, proof);
                    await WalkLockType("drop-down list (ComboBox)", host, page, combo, () => "index " + combo.SelectedIndex, Key.Down,
                        async () => { combo.Focus(); await Task.Delay(60); SendKey(combo, Key.Down); }, problems, proof);
                    await WalkLockType("drop-down list (the app's Dropdown)", host, page, drop, () => drop.SelectedId, Key.Down,
                        async () => { drop.Focus(); await Task.Delay(60); SendKey(drop, Key.Down); }, problems, proof);
                    await WalkLockType("tab (TabItem)", host, page, tab2, () => "tab " + tabs.SelectedIndex, Key.Space,
                        async () => { SendPress(tab2); await Task.Delay(100); if (tabs.SelectedIndex != 1) tab2.IsSelected = tab2.Focus() || tab2.IsSelected; }, problems, proof);
                    // a radio button reached through UI Automation: say plainly whether a lock holds there
                    {
                        var k = ElementMenu.KeyOf(radioA);
                        radioB.IsChecked = true; await Task.Delay(60);
                        if (Locks.Create(page, k, k, LockPolicy.Pin, 5, "4826", null, null) == null)
                        {
                            await Task.Delay(200);
                            try { ((ISelectionItemProvider)new RadioButtonAutomationPeer(radioA)).Select(); } catch { }
                            await Task.Delay(100);
                            NoteForReport("locks radio through UI Automation", radioA.IsChecked == true ? "a locked radio button was still selected through UI Automation's Select" : "a locked radio button was not selected through UI Automation's Select");
                            var ent = Locks.Find(page, k); new Locks.Session(ent).Submit("4826"); Locks.Remove(ent.Id);
                        }
                    }

                    // an item of the element menu: a separate popup window, locked through the menu's own lock entry
                    if (!ElementMenu.OpenFor(host, menuBtn)) problems.Add("menu item: the element menu did not open");
                    else
                    {
                        await Task.Delay(400);
                        var mpage = ElementMenu.LastMenuLockPage;
                        var mkey = ElementMenu.LastMenuItemKey("Press");
                        if (mpage == null || mkey == null) problems.Add("menu item: the menu has no lockable Press item");
                        else
                        {
                            if (!ElementMenu.OpenItemLockMenuForTest("Press") || !ElementMenu.LastItemLockLabels.Contains("Lock this menu item…")) problems.Add("menu item: right-click on an item does not offer Lock this menu item… (" + string.Join(", ", ElementMenu.LastItemLockLabels) + ")");
                            ElementMenu.CloseLast(); await Task.Delay(150);
                            var err = Locks.Create(mpage, mkey, mkey, LockPolicy.Pin, 5, "4826", null, null);
                            if (err != null) problems.Add("menu item: lock not made: " + err);
                            else
                            {
                                var steps = new List<string>();
                                await Task.Delay(300);
                                var row = ElementMenu.LastMenuRow("Press");
                                if (row == null) problems.Add("menu item: the Press row is gone");
                                else
                                {
                                    int p0 = ElementMenu.PromptsOpened;
                                    bool st = SendPress(row); await Task.Delay(150);
                                    if (!st || ElementMenu.PromptsOpened <= p0) problems.Add("menu item: a press was not stopped"); else steps.Add("press stopped with the prompt");
                                    ClosePrompts(); await Task.Delay(80);
                                    p0 = ElementMenu.PromptsOpened;
                                    row = ElementMenu.LastMenuRow("Press");
                                    bool kst = SendKey(row, Key.Enter); await Task.Delay(150);
                                    if (!kst || ElementMenu.PromptsOpened <= p0) problems.Add("menu item: Enter was not stopped"); else steps.Add("Enter stopped with the prompt");
                                    ClosePrompts(); await Task.Delay(80);
                                    if (pressed != 0) problems.Add("menu item: the locked Press item still pressed the button (" + pressed + ")"); else steps.Add("button not pressed");
                                    var ent = Locks.Find(mpage, mkey);
                                    new Locks.Session(ent).Submit("4826"); await Task.Delay(250);
                                    if (Locks.IsLocked(mpage, mkey)) problems.Add("menu item: the PIN did not open it");
                                    else
                                    {
                                        steps.Add("PIN opened it");
                                        row = ElementMenu.LastMenuRow("Press");
                                        p0 = ElementMenu.PromptsOpened;
                                        bool after = row == null || SendPress(row);
                                        SendKey(ElementMenu.LastMenuRow("Press") ?? (UIElement)host, Key.Enter); await Task.Delay(250);
                                        if (after || ElementMenu.PromptsOpened != p0) problems.Add("menu item: after the PIN the press was still stopped");
                                        else if (pressed != 1) problems.Add("menu item: after the PIN, Enter on Press did not press the button (" + pressed + ")");
                                        else steps.Add("then Press pressed the button");
                                    }
                                    var gone = Locks.Remove(ent.Id);
                                    if (gone != null) problems.Add("menu item: an open lock could not be removed: " + gone); else steps.Add("lock removed");
                                }
                                proof["menu item (an element menu row)"] = string.Join(", ", steps);
                            }
                        }
                    }
                    ElementMenu.CloseLast();

                    // a tab of the control centre's tab strip
                    cc = new ControlCentreWindow { Width = 1300, Height = 900 };
                    cc.Show(); await Wave6Flows.Settle(cc, 900);
                    cc.Navigate("modes", animate: false); await Wave6Flows.Settle(cc, 500);
                    cc.Navigate("network", animate: false); await Wave6Flows.Settle(cc, 600);
                    var tab = Drive.Descendants(cc).OfType<Tabs.StripTab>().FirstOrDefault(t => t.TabId == "modes" && t.IsVisible);
                    if (tab == null) problems.Add("tab strip tab: no tab for the Modes page");
                    else await WalkLockType("tab strip tab (StripTab)", cc, ElementMenu.LockPageFor(cc, tab), tab, () => "page " + ElementMenu.PageIdOf(cc), Key.Enter,
                        async () =>
                        {
                            var t = Drive.Descendants(cc).OfType<Tabs.StripTab>().First(x => x.TabId == "modes" && x.IsVisible);
                            SendPress(t); await Wave6Flows.Settle(cc, 400);
                        }, problems, proof);
                    NoteForReport("locks walk more types", string.Join("; ", proof.Select(kv => kv.Key + ": " + kv.Value)));
                    if (proof.Count < 6) problems.Add("only " + proof.Count + " of 6 more element types were walked");
                    return problems.Count == 0 ? null : string.Join(" | ", problems);
                }
                finally
                {
                    ElementMenu.CloseLast();
                    ElementMenu.KeepOpenForTest = false;
                    ClosePrompts();
                    host.Close();
                    cc?.Close();
                }
            });

            await CheckAsync("locks keyed per element: two elements with the same words lock apart, and a lock saved with the words alone holds on the first of them", async () =>
            {
                var a = new Button { Content = "Same words", Margin = new Thickness(4) };
                var b = new Button { Content = "Same words", Margin = new Thickness(4) };
                var host = new Window { Width = 360, Height = 220, Title = "Same words", Background = Ui.Res("B.Window"), Content = new StackPanel { Margin = new Thickness(16), Children = { a, b } } };
                var problems = new List<string>();
                var made = new List<(string, string)>();
                ControlCentreWindow cc = null;
                try
                {
                    host.Show(); await Wave6Flows.Settle(host, 500);
                    ElementMenu.Attach(host);
                    var page = ElementMenu.PageIdOf(host);
                    string ka = ElementMenu.KeyOf(a), kb = ElementMenu.KeyOf(b);
                    if (ka != "Same words" || kb != "Same words #2") problems.Add("keys are \"" + ka + "\" and \"" + kb + "\"");
                    // lock the second: the first stays free
                    Locks.Create(page, kb, kb, LockPolicy.Pin, 5, "4826", null, null); made.Add((page, kb)); await Task.Delay(200);
                    int p0 = ElementMenu.PromptsOpened;
                    if (SendPress(a)) problems.Add("a press on the first was stopped by the lock on the second");
                    ClosePrompts();
                    if (!SendPress(b) || ElementMenu.PromptsOpened <= p0) problems.Add("a press on the locked second was not stopped");
                    ClosePrompts();
                    var e2 = Locks.Find(page, kb); new Locks.Session(e2).Submit("4826"); Locks.Remove(e2.Id); made.Clear();
                    // a lock saved before keys carried a position: the words alone
                    Locks.Create(page, "Same words", "Same words", LockPolicy.Pin, 5, "4826", null, null); made.Add((page, "Same words")); await Task.Delay(200);
                    p0 = ElementMenu.PromptsOpened;
                    if (!SendPress(a) || ElementMenu.PromptsOpened <= p0) problems.Add("an old words-only lock no longer holds on the first element");
                    ClosePrompts();
                    if (SendPress(b)) problems.Add("an old words-only lock also stopped the second element");
                    ClosePrompts();
                    var e1 = Locks.Find(page, "Same words"); new Locks.Session(e1).Submit("4826"); Locks.Remove(e1.Id); made.Clear();

                    // on the real pages: the first pair of lockable elements that say the same words
                    cc = new ControlCentreWindow { Width = 1300, Height = 900 };
                    cc.Show(); await Wave6Flows.Settle(cc, 900);
                    string found = null;
                    foreach (var id in new[] { "clipboard", "history", "records", "tools", "shortcuts", "appearance", "modes", "safety", "minecraft", "network" })
                    {
                        cc.Navigate(id, animate: false); await Wave6Flows.Settle(cc, 600);
                        var pg = ElementMenu.PageIdOf(cc);
                        var cands = Drive.Descendants(cc).OfType<FrameworkElement>().Where(f => f.IsVisible && f.ActualWidth > 6 && ElementMenu.IsLockCandidateForTest(f) && ElementMenu.BaseKeyOf(f) != null && Window.GetWindow(f) == cc).ToList();
                        var pair = cands.GroupBy(ElementMenu.BaseKeyOf).FirstOrDefault(g => g.Count() > 1 && g.All(x => x is ButtonBase));
                        if (pair == null) continue;
                        var first = pair.ElementAt(0); var second = pair.ElementAt(1);
                        var k2 = ElementMenu.KeyOf(second);
                        if (k2 == ElementMenu.KeyOf(first)) { problems.Add("on " + id + " two elements saying \"" + pair.Key + "\" share the key " + k2); break; }
                        Locks.Create(pg, k2, k2, LockPolicy.Pin, 5, "4826", null, null); made.Add((pg, k2)); await Task.Delay(250);
                        int q0 = ElementMenu.PromptsOpened;
                        bool firstStopped = ElementMenu.BlockPress(cc, first); ClosePrompts();
                        bool secondStopped = ElementMenu.BlockPress(cc, second); ClosePrompts();
                        if (firstStopped) problems.Add("on " + id + " the lock on the second \"" + pair.Key + "\" also stopped the first");
                        if (!secondStopped) problems.Add("on " + id + " the lock on the second \"" + pair.Key + "\" did not stop it");
                        found = id + ": \"" + pair.Key + "\" (" + pair.Count() + " of them), the second locked alone as \"" + k2 + "\"";
                        var en = Locks.Find(pg, k2); new Locks.Session(en).Submit("4826"); Locks.Remove(en.Id); made.Clear();
                        break;
                    }
                    NoteForReport("locks same words on a real page", found ?? "no page visited had two buttons with the same words");
                    return problems.Count == 0 ? null : string.Join(" | ", problems);
                }
                finally
                {
                    ClosePrompts();
                    foreach (var (pg, k) in made) { var ent = Locks.Find(pg, k); if (ent != null) { try { new Locks.Session(ent).Submit("4826"); } catch { } Locks.Remove(ent.Id); } }
                    host.Close();
                    cc?.Close();
                }
            });
        }
    }
}
