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
                    await Shot(w, name, caption, 900);
                    w.CloseAnimated(); await Task.Delay(350);
                }
                var d = Dialogs.ConversionsDialog.Open("5 kg");
                await Shot(d, "dialog-conversions", "Quick conversions: every unit at once, click any result to copy it", 1100);
                d.SetText("72°F");
                await Shot(d, "dialog-conversions-temperature", "Quick conversions for a temperature", 900);
                d.Close();
            }
            finally
            {
                Settings.Current.AutoLookups = lookups;
                ClipboardWatcher.TextSinkForTest = null;
            }
        }
    }
}
