using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // A toy lock made through the real wizard window, refused when the PINs differ, then unlocked and removed.
    public static partial class SelfTest
    {
        private static async Task LocksLane4ChecksAsync()
        {
            await CheckAsync("locks: the lock wizard refuses PINs that differ, makes a lock from its real fields and buttons, and the lock then refuses removal until opened with the PIN", async () =>
            {
                var host = new Window { Width = 400, Height = 240, Title = "Lock drive", Content = new Button { Content = "Wizard sample", Margin = new Thickness(30) } };
                const string page = "l4-wizard", key = "Wizard sample";
                LockWizardWindow wiz = null; bool created = false;
                try
                {
                    host.Show(); await Task.Delay(400);
                    wiz = new LockWizardWindow((FrameworkElement)host.Content, page, key, key, () => created = true);
                    wiz.Show(); await Task.Delay(500);
                    PasswordBox Pw(string name) => Descend<PasswordBox>(wiz).FirstOrDefault(p => AutomationProperties.GetName(p) == name);
                    var pin = Pw("PIN, 4 to 8 digits"); var again = Pw("Type the PIN again");
                    if (pin == null || again == null) return "the wizard has no PIN fields";
                    pin.Password = "4826"; again.Password = "1111";
                    Press(wiz, "Lock it"); await Task.Delay(200);
                    if (created || Locks.IsLocked(page, key)) return "a lock was made from two different PINs";
                    pin.Password = "12"; again.Password = "12";
                    Press(wiz, "Lock it"); await Task.Delay(200);
                    if (created || Locks.IsLocked(page, key)) return "a lock was made from a 2 digit PIN";
                    pin.Password = "4826"; again.Password = "4826";
                    Press(wiz, "Lock it"); await Task.Delay(400);
                    if (!created || !Locks.IsLocked(page, key)) return "the wizard did not make the lock";
                    var e = Locks.Find(page, key);
                    if (Locks.Remove(e.Id) == null) return "a closed lock was removed without its PIN";
                    new Locks.Session(e).Submit("4826");
                    if (Locks.IsLocked(page, key)) return "the right PIN did not open the lock";
                    return null;
                }
                finally
                {
                    var e = Locks.Find(page, key);
                    if (e != null) { try { new Locks.Session(e).Submit("4826"); } catch { } Locks.Remove(e.Id); }
                    wiz?.Close(); host.Close();
                }
            });
        }
    }
}
