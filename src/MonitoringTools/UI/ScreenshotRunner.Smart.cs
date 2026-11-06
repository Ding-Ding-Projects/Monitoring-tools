using System.Collections.Generic;
using System.Threading.Tasks;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        // Smart clipboard toasts and the quick conversions dialog. Lookups stay off so nothing leaves the machine.
        internal static async Task SmartScenesAsync()
        {
            var sink = new List<string>();
            ClipboardWatcher.TextSinkForTest = sink.Add;
            bool lookups = Settings.Current.AutoLookups;
            Settings.Current.AutoLookups = false;
            try
            {
                foreach (var (text, name, caption) in new[]
                {
                    ("5 kg", "toast-smart-unit", "An amount: every unit of its kind, tap one to copy it"),
                    ("(416) 555-0134", "toast-smart-phone", "A phone number, tidied, with Call and Text"),
                    ("System.NullReferenceException: Object reference not set\n   at App.Toast.Show() in C:\\src\\Toast.cs:line 42\n   at App.Main() in C:\\src\\App.cs:line 7", "toast-smart-trace", "A stack trace: open the exact line in VS Code"),
                    ("Item\tQty\tPrice\nCable\t2\t12.50\nAdapter\t1\t18.00\nHub\t1\t24.00", "toast-smart-table", "Cells from a spreadsheet: sums, and copy as Markdown, CSV, JSON or HTML"),
                    ("0 9 * * 1-5", "toast-smart-cron", "A cron schedule in words, with the next runs"),
                    ("ghp_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789", "toast-smart-secret", "A secret: kept out of history and cleared after 30 seconds"),
                })
                {
                    var w = App.Toasts.ShowClip(ClipItem.FromText(text));
                    w.ExpandNow();   // buttons show on hover; captures show them
                    await Shot(w, name, caption, 900);
                    w.CloseAnimated(); await Task.Delay(350);
                }
                // Clipboard history with a rich preview on every row.
                foreach (var t in new[] { "https://github.com/Ding-Ding-Projects/Monitoring-tools/releases", "#7CF2C2", "Item\tQty\tPrice\nCable\t2\t12.50\nAdapter\t1\t18.00", "(416) 555-0134", "5 kg", "System.NullReferenceException: Object reference not set\n   at App.Toast.Show() in C:\\src\\Toast.cs:line 42", "Thanks for the review, merging now." })
                {
                    var ci = ClipItem.FromText(t); ci.SourceApp = t.StartsWith("http") ? "msedge" : "Teams";
                    App.History.Add(ci);
                }
                var hp = new HistoryPopup(System.IntPtr.Zero);
                hp.Show();
                await Shot(hp, "popup-history", "Clipboard history on Win+Shift+V: every row previews what it is", 1200);
                hp.List.SelectedIndex = 4;
                await Shot(hp, "popup-history-table", "The picked copy shows large on the right", 700);
                hp.Close();
                var pin = PinWindow.Show(ClipItem.FromText("Item\tQty\tPrice\nCable\t2\t12.50\nAdapter\t1\t18.00\nHub\t1\t24.00"));
                await Shot(pin, "window-pinned-table", "A table pinned on screen, on top of everything", 800);
                pin.Close();

                PasteQueue.Toggle();
                foreach (var t in new[] { "Alex Chen", "alex@example.com", "+1 416 555 0134", "42 Example Street" }) PasteQueue.Take(ClipItem.FromText(t));
                await Shot(PasteQueue.Current, "window-paste-queue", "The paste queue: copy several things, then each Ctrl+V pastes the next", 900);
                PasteQueue.Toggle();

                var cs = Dialogs.ClipboardSettingsDialog.Open();
                await Shot(cs, "dialog-clipboard-settings", "Clipboard settings: what lookups send where, kinds, the secret guard and shortcuts", 1100);
                cs.Close();
                var sd = Dialogs.SnippetsDialog.Open();
                await Shot(sd, "dialog-snippets", "Snippets with fill-ins and a live preview", 1000);
                sd.Close();
                var rd = Dialogs.RecipesDialog.Open();
                await Shot(rd, "dialog-recipes", "Transform recipes: steps, a live result and Show on text toasts", 1000);
                rd.Close();
                Settings.Current.AppRules.Add(new global::MonitoringTools.Clip.Smart.AppRule { App = "msedge", Toast = "show", Auto = "strip" });
                Settings.Current.AppRules.Add(new global::MonitoringTools.Clip.Smart.AppRule { App = "KeePassXC", Toast = "never" });
                var ru = Dialogs.RulesDialog.Open();
                await Shot(ru, "dialog-rules", "Per-app rules: toast, history only or never, and one automatic action", 1000);
                ru.Close();
                Settings.Current.AppRules.Clear();

                var d = Dialogs.ConversionsDialog.Open("5 kg");
                await Shot(d, "dialog-conversions", "Quick conversions: every unit at once, click any result to copy it", 1100);
                d.SetText("72°F");
                await Shot(d, "dialog-conversions-temperature", "Quick conversions for a temperature", 900);
                d.Close();

                // The extensions manager over a scratch folder with two sample extensions: one on, one waiting to be allowed.
                var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mt-shots-ext-" + System.Guid.NewGuid().ToString("N"));
                foreach (var (id, name, pattern, label, perms) in new[]
                {
                    ("ticket-keys", "Ticket keys", "^[A-Z][A-Z0-9]{1,9}-\\\\d{1,7}$", "Open ticket", "\"read-text\", \"open-links\", \"change-clipboard\""),
                    ("morse-code", "Morse code", "^[.\\\\-/ ]{5,}$", "Decode Morse", "\"read-text\", \"change-clipboard\""),
                })
                {
                    var dir = System.IO.Path.Combine(root, id);
                    System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "run.ps1"), "# sample");
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "extension.json"),
                        "{ \"id\": \"" + id + "\", \"name\": \"" + name + "\", \"version\": \"1.0.0\", \"api\": 1, \"description\": \"A sample extension.\", " +
                        "\"detect\": [{ \"kind\": \"k\", \"label\": \"" + (id == "morse-code" ? "Morse code" : "Ticket numbers like ABC-1234") + "\", \"pattern\": \"" + pattern + "\" }], \"actions\": [{ \"id\": \"a\", \"label\": \"" + label + "\", \"primary\": true }], " +
                        "\"run\": \"run.ps1\", \"permissions\": [" + perms + "] }");
                }
                var host = new global::MonitoringTools.Clip.Smart.ExtensionHost(root);
                host.Load();
                host.Allow("ticket-keys");
                var ed = new Dialogs.ExtensionsDialog(host);
                ed.Show();
                await Shot(ed, "dialog-extensions", "Extensions: what each one notices, the buttons it adds and what it may do", 1100);
                ed.Ask(host.Find("morse-code"));
                await Shot(ed, "dialog-extensions-allow", "Turning an extension on shows exactly what it asks for first", 700);
                ed.Close();
                try { System.IO.Directory.Delete(root, true); } catch { }
            }
            finally
            {
                Settings.Current.AutoLookups = lookups;
                ClipboardWatcher.TextSinkForTest = null;
            }
        }
    }
}
