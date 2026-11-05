using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The local history manager: how saved versions of scripts and templates are protected, how long they are kept, and what is stored.
        private FrameworkElement RecordsLocalHistory()
        {
            var box = new StackPanel();
            box.Children.Add(CardTitle("Local history", "Scripts and the commit message template keep earlier versions on this computer. Choose how they are stored and how long they are kept. Nothing here is uploaded."));
            var status = SafetyUi.Live("History status");
            status.Margin = new Thickness(0, 0, 0, 8);
            box.Children.Add(status);

            string chosen = HistoryVault.Mode;
            Button apply = null, unlock = null, lockNow = null;
            var items = new StackPanel();
            var (pass, passFrame) = SafetyUi.Pass("History password");
            var (pass2, passFrame2) = SafetyUi.Pass("Type the history password again");
            var passHost = new StackPanel { Visibility = Visibility.Collapsed };
            void ShowPassword()
            {
                bool locked = HistoryVault.IsLocked;
                passHost.Visibility = chosen == HistoryProtection.Password || locked ? Visibility.Visible : Visibility.Collapsed;
                passFrame2.Visibility = chosen == HistoryProtection.Password && !locked ? Visibility.Visible : Visibility.Collapsed;
                if (unlock != null) unlock.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
                if (lockNow != null) lockNow.Visibility = HistoryVault.Mode == HistoryProtection.Password && !locked ? Visibility.Visible : Visibility.Collapsed;
            }
            var chips = Pick("Protection", new[] { ("Off", HistoryProtection.None), ("This Windows account", HistoryProtection.Windows), ("Password", HistoryProtection.Password) }, chosen, v => { chosen = v; ShowPassword(); }, "How saved versions are stored");
            box.Children.Add(chips);
            box.Children.Add(SafetyUi.Text("Off keeps versions as plain text files. This Windows account encrypts them with a key that only this Windows account can open. Password encrypts them with a key made from a password you choose (8 or more characters). The password is not stored and cannot be recovered, and the history stays locked until you type it. Default: Off.", 12, "B.TextMute", null, 0));

            passHost.Children.Add(SafetyUi.Text("History password", 12, "B.TextSoft", null, 8));
            passHost.Children.Add(passFrame);
            passHost.Children.Add(SafetyUi.Text("Type the history password again", 12, "B.TextSoft", null, 0));
            passHost.Children.Add(passFrame2);
            box.Children.Add(passHost);

            var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
            void Report(string text) { status.Text = text; }

            apply = Ui.Button("Apply protection", () =>
            {
                if (chosen == HistoryProtection.Password && pass.Password != pass2.Password) { Report("The two passwords are different. Nothing was changed."); return; }
                var err = HistoryVault.Change(chosen, pass.Password);
                pass.Clear(); pass2.Clear();
                Report(err ?? (chosen == HistoryProtection.None ? "Saved versions are stored as plain text again." : chosen == HistoryProtection.Windows ? "Saved versions are now encrypted for this Windows account." : "Saved versions are now encrypted with your password. They are unlocked until you lock them or close the app."));
                ShowPassword(); RebuildItems();
            }, "Btn.Accent", Icons.Shield);
            apply.ToolTip = "Moves every saved version to the chosen protection. If it cannot read one, nothing is changed.";
            unlock = Ui.Button("Unlock history", () =>
            {
                var err = HistoryVault.Unlock(pass.Password);
                pass.Clear();
                Report(err ?? "The history is unlocked until you lock it or close the app.");
                ShowPassword(); RebuildItems();
            }, "Btn.Ghost", Icons.Shield);
            unlock.ToolTip = "Types the history password. Saved versions can be read and new ones kept until the history is locked again.";
            lockNow = Ui.Button("Lock history now", () => { HistoryVault.Lock(); Report("The history is locked."); ShowPassword(); RebuildItems(); }, "Btn.Ghost", Icons.Shield);
            lockNow.ToolTip = "Forgets the password in memory. Saved versions cannot be read until it is typed again.";
            actions.Children.Add(apply); actions.Children.Add(unlock); actions.Children.Add(lockNow);
            box.Children.Add(actions);

            box.Children.Add(new NumberField("Versions kept per item", "The most saved versions each script or template keeps; the oldest are dropped first. Default: 50.", ScriptHistory.KeepCount, 5, 500, 5, v =>
            {
                Settings.Current.HistoryKeepCount = (int)v; Settings.Save();
                int n = ScriptHistory.PruneAll(); Report(n == 0 ? "Keeping up to " + (int)v + " versions per item." : "Keeping up to " + (int)v + " versions per item. " + n + " older versions were removed.");
                RebuildItems();
            }));
            box.Children.Add(new NumberField("Drop versions older than", "Saved versions older than this many days are removed; the newest of each item always stays. 0 keeps them for ever. Default: 0.", ScriptHistory.KeepDays, 0, 3650, 1, v =>
            {
                Settings.Current.HistoryKeepDays = (int)v; Settings.Save();
                int n = ScriptHistory.PruneAll(); Report(v == 0 ? "Versions are kept for ever." : "Versions older than " + (int)v + " days are dropped. " + n + " were removed now.");
                RebuildItems();
            }, "days"));

            box.Children.Add(items);
            void RebuildItems()
            {
                items.Children.Clear();
                if (HistoryVault.IsLocked) { items.Children.Add(SafetyUi.Text("The history is locked. Type the password and choose Unlock history to see and restore saved versions.", 12, "B.TextMute")); return; }
                var all = ScriptHistory.Items();
                if (all.Count == 0) { items.Children.Add(SafetyUi.Text("No script or template has a history yet. The first save of one creates it.", 12, "B.TextMute")); return; }
                items.Children.Add(SafetyUi.Text("Saved histories", 14, "B.Text", null, 4));
                foreach (var (key, count) in all)
                {
                    var k = key;
                    var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                    var del = Ui.Button("Delete history", () =>
                    {
                        var dlg = new ConfirmWindow("Delete the history of " + k + "?", "All " + count + " saved versions of this item are removed for good. The script or template itself is not changed.", "Delete", danger: true);
                        dlg.Confirmed += _ => { ScriptHistory.Delete(k); Report("The history of " + k + " was deleted."); RebuildItems(); };
                        dlg.Show();
                    }, "Btn.Ghost");
                    AutomationProperties.SetName(del, "Delete the history of " + k);
                    DockPanel.SetDock(del, Dock.Right);
                    row.Children.Add(del);
                    row.Children.Add(SafetyUi.Text(k + "   ·   " + count + (count == 1 ? " version" : " versions"), 12, "B.TextSoft"));
                    items.Children.Add(row);
                }
            }
            ShowPassword(); RebuildItems();
            box.Loaded += (s, e) => { ShowPassword(); RebuildItems(); };
            return box;
        }
    }
}
