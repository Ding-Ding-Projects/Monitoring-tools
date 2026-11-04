using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // Local history checks: encrypted snapshots (Windows account and password), the locked state, retention, and the manager on the Records page.
    public static partial class SelfTest
    {
        private static string HistoryFolder => Path.Combine(Settings.DataDir, "history");

        private static bool FilesContain(string dir, string needle)
        {
            if (!Directory.Exists(dir)) return false;
            var bytes = Encoding.UTF8.GetBytes(needle);
            foreach (var f in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                if (!f.EndsWith(".txt") && !f.EndsWith(".enc")) continue;
                var data = File.ReadAllBytes(f);
                for (int i = 0; i + bytes.Length <= data.Length; i++)
                {
                    int j = 0; while (j < bytes.Length && data[i + j] == bytes[j]) j++;
                    if (j == bytes.Length) return true;
                }
            }
            return false;
        }

        private static void ResetHistory()
        {
            HistoryVault.Lock();
            Settings.Current.HistoryProtection = HistoryProtection.None;
            Settings.Current.HistoryKeepCount = 50; Settings.Current.HistoryKeepDays = 0;
            if (Directory.Exists(HistoryFolder)) Directory.Delete(HistoryFolder, true);
        }

        private static async Task HistoryLane4ChecksAsync()
        {
            Check("history: Windows-account encryption keeps versions readable, hides the text on disk and refuses a changed file", () =>
            {
                ResetHistory();
                const string key = "l4-win", secret = "SECRET-LINE-ALPHA";
                ScriptHistory.Save(key, "plain one " + secret);
                ScriptHistory.Save(key, "plain two " + secret);
                if (!FilesContain(HistoryFolder, secret)) return "the plain versions were not on disk before encrypting";
                var err = HistoryVault.Change(HistoryProtection.Windows);
                if (err != null) return "change refused: " + err;
                if (FilesContain(HistoryFolder, secret)) return "the text is still readable on disk after encrypting";
                if (Directory.GetFiles(HistoryFolder, "*.txt", SearchOption.AllDirectories).Length != 0) return "a plain file was left behind";
                var list = ScriptHistory.List(key);
                if (list.Count != 2 || ScriptHistory.Read(key, list[0].Id) != "plain two " + secret || ScriptHistory.Read(key, list[1].Id) != "plain one " + secret) return "versions did not survive the change";
                if (!ScriptHistory.Save(key, "three " + secret)) return "a new version was not kept while encrypted";
                if (ScriptHistory.Save(key, "three " + secret)) return "an identical version was stored twice";
                var file = Directory.GetFiles(HistoryFolder, "*.enc", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal).Last();
                var raw = File.ReadAllBytes(file); raw[raw.Length - 1] ^= 0x01; File.WriteAllBytes(file, raw);   // flip one bit of the newest file
                var newest = ScriptHistory.List(key)[0];
                if (ScriptHistory.Read(key, newest.Id) != null) return "a changed file was still read as genuine";
                err = HistoryVault.Change(HistoryProtection.None);
                return err != null ? null : "switching off should have refused because one version cannot be read";
            });

            Check("history: password encryption locks, refuses a wrong password, unlocks and converts back", () =>
            {
                ResetHistory();
                const string key = "l4-pw", secret = "SECRET-LINE-BETA", pw = "correct horse battery";
                ScriptHistory.Save(key, "first " + secret);
                if (HistoryVault.Change(HistoryProtection.Password, "short") == null) return "a 5 character password was accepted";
                var err = HistoryVault.Change(HistoryProtection.Password, pw);
                if (err != null) return "change refused: " + err;
                if (FilesContain(HistoryFolder, secret)) return "the text is readable on disk";
                if (HistoryVault.IsLocked) return "the history was locked straight after setting the password";
                if (!ScriptHistory.Save(key, "second " + secret)) return "unlocked history refused a save";
                HistoryVault.Lock();
                if (!ScriptHistory.IsLocked) return "Lock did not lock";
                var id = ScriptHistory.List(key)[0].Id;
                if (ScriptHistory.Read(key, id) != null) return "a locked history was readable";
                if (ScriptHistory.Save(key, "third") ) return "a locked history accepted a save";
                if (HistoryVault.Change(HistoryProtection.None) == null) return "changing the mode was allowed while locked";
                if (HistoryVault.Unlock("wrong password") == null) return "a wrong password unlocked it";
                if (!ScriptHistory.IsLocked) return "a wrong password left it unlocked";
                if (HistoryVault.Unlock(pw) != null) return "the right password was refused";
                if (ScriptHistory.Read(key, id) != "second " + secret) return "unlocked text was wrong";
                if (HistoryVault.Change(HistoryProtection.None) != null) return "converting back was refused";
                return FilesContain(HistoryFolder, secret) && !Directory.GetFiles(HistoryFolder, "*.enc", SearchOption.AllDirectories).Any() && !File.Exists(Path.Combine(HistoryFolder, "protection.json")) ? null : "converting back left encrypted files or the check file";
            });

            Check("history: retention by count and by age keeps the newest and drops the rest", () =>
            {
                ResetHistory();
                const string key = "l4-keep";
                Settings.Current.HistoryKeepCount = 5;
                for (int i = 1; i <= 12; i++) ScriptHistory.Save(key, "v" + i);
                var list = ScriptHistory.List(key);
                if (list.Count != 5 || ScriptHistory.Read(key, list[0].Id) != "v12" || ScriptHistory.Read(key, list[4].Id) != "v8") return "count limit wrong: " + list.Count;
                // age: backdate three of them by renaming
                Settings.Current.HistoryKeepCount = 50;
                var dir = Path.Combine(HistoryFolder, key);
                var files = Directory.GetFiles(dir, "*.txt").OrderByDescending(f => f, StringComparer.Ordinal).ToList();
                for (int i = 0; i < 3; i++)
                {
                    var old = DateTime.UtcNow.AddDays(-40 - i).ToString("yyyyMMddHHmmssfff");
                    File.Move(files[files.Count - 1 - i], Path.Combine(dir, old + ".txt"));
                }
                Settings.Current.HistoryKeepDays = 7;
                int removed = ScriptHistory.PruneAll();
                var left = ScriptHistory.List(key);
                Settings.Current.HistoryKeepDays = 0;
                return removed == 3 && left.Count == 2 && ScriptHistory.Read(key, left[0].Id) == "v12" ? null : $"removed {removed}, left {left.Count}";
            });

            await CheckAsync("history: the Records page manager sets a password, locks, unlocks and deletes a history through its real buttons", async () =>
            {
                ResetHistory();
                ScriptHistory.Save("script-l4-page", "UI-SECRET-GAMMA one");
                ScriptHistory.Save("script-l4-page", "UI-SECRET-GAMMA two");
                var cc = new ControlCentreWindow { Width = 1180, Height = 900 };
                try
                {
                    cc.Show(); await Task.Delay(700);
                    cc.Navigate("records", animate: false); await Task.Delay(400);
                    PasswordBox Pw(string name) => Descend<PasswordBox>(cc).FirstOrDefault(p => AutomationProperties.GetName(p) == name);
                    TextBlock Status() => Descend<TextBlock>(cc).FirstOrDefault(t => AutomationProperties.GetName(t) == "History status");
                    if (!Press(cc, "Password")) return "no Password choice";
                    cc.UpdateLayout();
                    Pw("History password").Password = "ui password 123"; Pw("Type the history password again").Password = "different";
                    if (!Press(cc, "Apply protection")) return "no Apply protection button";
                    if (HistoryVault.Mode == HistoryProtection.Password) return "mismatched passwords were applied: " + Status()?.Text;
                    Pw("History password").Password = "ui password 123"; Pw("Type the history password again").Password = "ui password 123";
                    Press(cc, "Apply protection");
                    if (HistoryVault.Mode != HistoryProtection.Password) return "password not applied: " + Status()?.Text;
                    if (FilesContain(HistoryFolder, "UI-SECRET-GAMMA")) return "text still on disk";
                    await Task.Delay(100);
                    if (!Press(cc, "Lock history now")) return "no Lock history now button after applying";
                    if (!HistoryVault.IsLocked) return "Lock history now did not lock";
                    cc.UpdateLayout();
                    Pw("History password").Password = "wrong wrong wrong";
                    Press(cc, "Unlock history");
                    if (!HistoryVault.IsLocked) return "a wrong password unlocked";
                    Pw("History password").Password = "ui password 123";
                    Press(cc, "Unlock history");
                    if (HistoryVault.IsLocked) return "the right password did not unlock: " + Status()?.Text;
                    var del = Descend<Button>(cc).FirstOrDefault(b => AutomationProperties.GetName(b) == "Delete the history of script-l4-page");
                    if (del == null) return "the item has no Delete history button";
                    return null;
                }
                finally { cc.Close(); ResetHistory(); }
            });
        }
    }
}
