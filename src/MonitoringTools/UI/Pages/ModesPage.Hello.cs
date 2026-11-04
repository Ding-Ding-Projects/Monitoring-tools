using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // School mode: the optional Windows Hello way to turn the mode off. Shown only once a PIN or password exists.
        private FrameworkElement ModesHelloRow(bool on, Action<string> say)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 4), Tag = "school-hello" };
            AutomationProperties.SetName(sp, "Windows Hello for this mode");
            sp.Children.Add(new TextBlock { Text = "Windows Hello (optional)", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(ModesNote(Modes.HelloDisclosure));
            var state = ModesNote("Checking Windows Hello...");
            AutomationProperties.SetLiveSetting(state, AutomationLiveSetting.Polite);
            sp.Children.Add(state);
            var row = new WrapPanel();
            sp.Children.Add(row);

            async void Paint()
            {
                row.Children.Clear();
                var (available, text) = await Modes.HelloStateAsync();
                state.Text = (Modes.HelloUnlockEnabled ? "Allowed for this mode. " : "Not set up for this mode. ") + text;
                state.Foreground = Ui.Res(available ? "B.TextMute" : "B.Amber");
                if (on)
                {
                    if (Modes.HelloUnlockEnabled)
                    {
                        var b = Ui.Button("Turn off with Windows Hello", async () =>
                        {
                            var (ok, msg) = await Modes.DisableSchoolWithHelloAsync();
                            if (ok) Navigate("modes", animate: false); else say(msg);
                        }, "Btn.Alert", Icons.Shield);
                        b.IsEnabled = available;
                        row.Children.Add(b);
                    }
                    else row.Children.Add(ModesNote("To add Windows Hello, turn the mode off first, then allow it here."));
                    return;
                }
                if (Modes.HelloUnlockEnabled)
                    row.Children.Add(Ui.Button("Stop allowing Windows Hello", () =>
                    {
                        if (Modes.RemoveHelloUnlock(out var err)) Paint(); else say(err);
                    }, "Btn.Ghost"));
                else
                {
                    var set = Ui.Button("Allow Windows Hello", async () =>
                    {
                        var (ok, msg) = await Modes.SetUpHelloAsync();
                        say(msg);
                        if (ok) Paint();
                    }, "Btn.Accent", Icons.Shield);
                    set.IsEnabled = available;
                    set.ToolTip = available ? "Asks Windows Hello once to confirm it is you, then allows it as a third way to turn the mode off." : "Windows Hello cannot be used on this computer right now, so this stays off.";
                    row.Children.Add(set);
                }
            }
            Paint();
            return sp;
        }
    }
}
