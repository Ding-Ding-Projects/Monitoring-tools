using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    // Walks the element types that can carry a toy lock on the real pages of the control centre: for each type found, a lock is made on one
    // example, a left press (and a key press) on it and on the controls inside it is shown to be stopped with the unlock prompt opened and the
    // control's value untouched, then the PIN opens it and the same press goes through.
    public static partial class SelfTest
    {
        private static string LockCategory(FrameworkElement e)
        {
            string kind = e switch
            {
                CheckBox => "switch (CheckBox)",
                RadioButton => "choice button (RadioButton)",
                ToggleButton => "toggle button (ToggleButton)",
                Button => "button (Button)",
                ButtonBase => "button (" + e.GetType().Name + ")",
                NumberField => "number field (NumberField)",
                _ => e.GetType().Name
            };
            var item = SearchScope.GetItem(e);
            if (item != null && e is not ButtonBase && e is not NumberField)
            {
                string role = item.StartsWith("setting switch") || item.StartsWith("on setting") || item.StartsWith("off setting") ? "switch row"
                    : item.StartsWith("setting choice") ? "choice group"
                    : "list row or card";
                kind = role + " (" + e.GetType().Name + ")";
            }
            else if (item == null && e is Border) kind = "card (Border)";
            return kind;
        }

        private static string StateOf(FrameworkElement e) => string.Join("|", new[] { e }.Concat(Drive.Descendants(e).OfType<FrameworkElement>()).Select(x => x switch
        {
            ToggleButton t => "T" + t.IsChecked,
            TextBox b => "X" + b.Text,
            Selector s => "S" + s.SelectedIndex,
            RangeBase r => "R" + r.Value,
            _ => null
        }).Where(x => x != null));


        private static KeyEventArgs KeyDown(FrameworkElement target, Key key)
        {
            var src = PresentationSource.FromVisual(target);
            return new KeyEventArgs(Keyboard.PrimaryDevice, src, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = target };
        }

        private static void ClosePrompts()
        {
            foreach (var w in Application.Current.Windows.OfType<Window>().Where(w => w.GetType().Name == "LockPromptWindow").ToList()) w.Close();
        }

        private static async Task LockWalkChecksAsync()
        {
            await CheckAsync("locks on every element type: each type found on the real pages is locked, stopped for a press and a key with the prompt opened, and opened again by the PIN", async () =>
            {
                var cc = new ControlCentreWindow { Width = 1300, Height = 900 };
                var walked = new Dictionary<string, string>();   // category -> the example's key
                var inner = new Dictionary<string, string>();    // inner control type -> proof
                var problems = new List<string>();
                var madeLocks = new List<(string Page, string Key)>();
                try
                {
                    cc.Show(); await Wave6Flows.Settle(cc, 900);
                    var pages = new[] { "modes", "appearance", "language", "network", "game", "conversion", "service", "tools", "safety", "records", "shortcuts", "clipboard", "history", "activity", "help", "ollama", "minecraft" };
                    foreach (var id in pages)
                    {
                        cc.Navigate(id, animate: false); await Wave6Flows.Settle(cc, 600);
                        var pg = ElementMenu.PageIdOf(cc);
                        var cands = Drive.Descendants(cc).OfType<FrameworkElement>().Where(f => f.IsVisible && f.ActualWidth > 6 && f.ActualHeight > 6 && ElementMenu.IsLockCandidateForTest(f) && ElementMenu.KeyOf(f) != null).ToList();
                        var fresh = cands.Where(f => !walked.ContainsKey(LockCategory(f))).GroupBy(LockCategory).Select(g => g.First()).Take(10).ToList();
                        foreach (var e in fresh)
                        {
                            var tkind = LockCategory(e); var key = ElementMenu.KeyOf(e);
                            // an element that shares its words with another one on the page would be locked together with it; take only a clear example
                            if (cands.Count(c => ElementMenu.KeyOf(c) == key) > 1 && cands.Count(c => LockCategory(c) == tkind && ElementMenu.KeyOf(c) != key) > 0) continue;
                            if (Window.GetWindow(e) != cc) continue;   // a popup is its own window, with its own handlers
                            walked[tkind] = key;
                            var before = StateOf(e);
                            var err = Locks.Create(pg, key, key, LockPolicy.Pin, 5, "4826", null, null);
                            if (err != null) { problems.Add(tkind + ": lock not made: " + err); continue; }
                            madeLocks.Add((pg, key));
                            await Task.Delay(250);
                            try
                            {
                                if (!Locks.IsLocked(pg, key)) { problems.Add(tkind + ": not locked"); continue; }
                                // 1. a left press on the element itself
                                int p0 = ElementMenu.PromptsOpened; bool stopped = ElementMenu.BlockPress(cc, e); await Task.Delay(120);
                                if (!stopped || ElementMenu.PromptsOpened <= p0) problems.Add(tkind + " (" + key + "): a press was not stopped (handled " + stopped + ", prompts " + (ElementMenu.PromptsOpened - p0) + ")");
                                ClosePrompts(); await Task.Delay(80);
                                // 2. a press on every interactive control inside it
                                foreach (var c in Drive.Descendants(e).OfType<FrameworkElement>().Where(c => c.IsVisible && c.ActualWidth > 4 && (c is ButtonBase || c is TextBox || c is Selector || c is RangeBase)).GroupBy(c => c.GetType().Name).Select(g => g.First()).Take(4))
                                {
                                    p0 = ElementMenu.PromptsOpened; bool d2 = ElementMenu.BlockPress(cc, c); await Task.Delay(100);
                                    var tn = c.GetType().Name + " inside a " + tkind;
                                    if (!d2 || ElementMenu.PromptsOpened <= p0) problems.Add(tn + ": a press was not stopped");
                                    else inner[tn] = "stopped";
                                    ClosePrompts(); await Task.Delay(60);
                                }
                                // 3. a key: Space on a button, a choice or a switch; typing and Backspace in a text field inside
                                if (e is ButtonBase || Drive.Descendants(e).OfType<ButtonBase>().Any())
                                {
                                    var target = e as ButtonBase ?? Drive.Descendants(e).OfType<ButtonBase>().First(b => b.IsVisible && b.Focusable);
                                    target.Focus();
                                    if (Keyboard.FocusedElement == target)
                                    {
                                        p0 = ElementMenu.PromptsOpened; var k = KeyDown(target, Key.Space); target.RaiseEvent(k); await Task.Delay(100);
                                        if (!k.Handled || ElementMenu.PromptsOpened <= p0) problems.Add(tkind + ": Space was not stopped");
                                        else inner["Space on " + target.GetType().Name] = "stopped";
                                        ClosePrompts();
                                    }
                                    else inner["Space on " + target.GetType().Name] = "not driven: the control could not take keyboard focus here";
                                }
                                var tb = Drive.Descendants(e).OfType<TextBox>().FirstOrDefault(t => t.IsVisible && t.IsEnabled && !t.IsReadOnly);
                                if (tb != null)
                                {
                                    tb.Focus();
                                    if (Keyboard.FocusedElement == tb)
                                    {
                                        var tin = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, tb, "7")) { RoutedEvent = UIElement.PreviewTextInputEvent, Source = tb };
                                        p0 = ElementMenu.PromptsOpened; tb.RaiseEvent(tin); await Task.Delay(100);
                                        if (!tin.Handled || ElementMenu.PromptsOpened <= p0) problems.Add(tkind + ": typing into the text field inside was not stopped");
                                        else inner["typing into a TextBox inside a " + tkind] = "stopped";
                                        ClosePrompts();
                                        tb.Focus();
                                        var bk = KeyDown(tb, Key.Back); tb.RaiseEvent(bk); await Task.Delay(80);
                                        if (Keyboard.FocusedElement == tb && !bk.Handled) problems.Add(tkind + ": Backspace in the text field inside was not stopped");
                                        ClosePrompts();
                                    }
                                    else inner["typing into a TextBox inside a " + tkind] = "not driven: the box could not take keyboard focus here";
                                }
                                if (StateOf(e) != before) problems.Add(tkind + ": the control's value changed while locked: " + before + " -> " + StateOf(e));
                            }
                            finally
                            {
                                ClosePrompts();
                                var ent = Locks.Find(pg, key);
                                // 4. the PIN opens it, and then the same press is not stopped
                                if (ent != null)
                                {
                                    var r = new Locks.Session(ent).Submit("4826");
                                    await Task.Delay(250);
                                    if (Locks.IsLocked(pg, key)) problems.Add(tkind + ": the PIN did not open the lock (" + r.Status + ")");
                                    else
                                    {
                                        int p0 = ElementMenu.PromptsOpened; ElementMenu.BlockPress(cc, e); await Task.Delay(100);
                                        if (ElementMenu.PromptsOpened != p0) problems.Add(tkind + ": after the PIN the press still opened the prompt");
                                    }
                                    ClosePrompts();
                                    var gone = Locks.Remove(ent.Id);
                                    if (gone != null) problems.Add(tkind + ": an open lock could not be removed: " + gone);
                                    madeLocks.Remove((pg, key));
                                }
                            }
                        }
                    }
                    NoteForReport("locks walk element types", string.Join("; ", walked.Select(kv => kv.Key + " = \"" + kv.Value + "\"")));
                    NoteForReport("locks walk inner controls", string.Join("; ", inner.Select(kv => kv.Key + ": " + kv.Value)));
                    if (walked.Count < 6) problems.Add("only " + walked.Count + " element types were found to walk");
                    return problems.Count == 0 ? null : string.Join(" | ", problems);
                }
                finally
                {
                    ClosePrompts();
                    foreach (var (pg, key) in madeLocks.ToList()) { var ent = Locks.Find(pg, key); if (ent != null) { try { new Locks.Session(ent).Submit("4826"); } catch { } Locks.Remove(ent.Id); } }
                    cc.Close();
                }
            });
        }
    }
}
