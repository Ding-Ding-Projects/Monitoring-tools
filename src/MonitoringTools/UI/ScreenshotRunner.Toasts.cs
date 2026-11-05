using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        // The editable text toast and every state of the install command toast, plus the automatic install card.
        // Installs are never run: a stand-in runner answers, so the states are real windows with scripted outcomes.
        internal static async Task ToastScenesAsync()
        {
            const string text = "Meeting notes, Thursday\nShip the clipboard update on Friday.\nAsk Dana about the invoice wording.\nBook the room for 14:30.";
            var sink = new List<string>();
            ClipboardWatcher.TextSinkForTest = sink.Add;
            InstallRunner.PathForTest = m => "C:\\sample\\" + m;
            string mode = "ok";
            InstallRunner.RunnerForTest = async (c, step, ct) =>
            {
                step?.Invoke("check");
                if (c.Manager == InstallManager.Chocolatey) step?.Invoke("consent");
                try { await Task.Delay(mode == "hang" ? 600000 : 200, ct); } catch (OperationCanceledException) { return new InstallResult { Cancelled = true, Message = "Cancelled." }; }
                if (mode == "declined") return new InstallResult { Declined = true, ExitCode = 1223, Message = "The Windows consent prompt was declined or timed out. Nothing was installed.", Took = TimeSpan.FromSeconds(14) };
                var r = new InstallResult { Ok = true, Message = "Installed.", Took = TimeSpan.FromSeconds(52) };
                foreach (var p in c.Packages) r.Versions[p] = p == "7zip" ? "24.09" : "3.0.21";
                return r;
            };
            try
            {
                // text toast: idle, editing, reverted
                var w = App.Toasts.ShowClip(ClipItem.FromText(text));
                await Shot(w, "toast-text-idle", "Copied text, idle: nothing has focus", 900);
                var box = SelfTest.Descend<TextBox>(w).First(t => AutomationProperties.GetName(t) == "Copied text, editable");
                box.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = box });
                box.Text = text.Replace("Friday", "Monday");
                await Shot(w, "toast-text-editing", "Copied text, editing in place", 900);
                await Task.Delay(500);
                SelfTest.Press(w, "Revert");
                await Shot(w, "toast-text-reverted", "Copied text, reverted", 700);
                w.CloseAnimated(); await Task.Delay(350);

                var longText = string.Join("\n", Enumerable.Range(1, 96).Select(i => i switch { 1 => "Action items from the planning call", 2 => "1. Move the release to the second week.", _ => $"{i - 1}. Follow-up item number {i - 1} for the team." }));
                var lw = App.Toasts.ShowClip(ClipItem.FromText(longText));
                await Shot(lw, "toast-text-long", "Long copied text scrolls inside the toast", 900);
                lw.CloseAnimated(); await Task.Delay(350);

                // install command toasts
                await Toast("toast-install-choco", "choco install: names, source, consent note", App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip vlc -y")), 900);
                await Toast("toast-install-winget", "winget install: the installer decides", App.Toasts.ShowClip(ClipItem.FromText("winget install --id Microsoft.PowerToys -e")), 900);
                await Toast("toast-install-refused", "Refused command, with a reason per flag", App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip --ignore-checksums -s http://pkgs.example.net/feed")), 900);

                mode = "hang";
                var run = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip vlc -y"));
                await Task.Delay(500); SelfTest.Press(run, "Install 2 packages");
                await Shot(run, "toast-install-running", "Installing: steps and the consent step named", 800);
                SelfTest.Press(run, "Cancel"); await Task.Delay(400); run.CloseAnimated(); await Task.Delay(350);

                mode = "ok";
                var done = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip vlc -y"));
                await Task.Delay(500); SelfTest.Press(done, "Install 2 packages"); await Task.Delay(900);
                await Shot(done, "toast-install-done", "Installed: versions, Open log, Copy summary", 500);
                done.CloseAnimated(); await Task.Delay(350);

                mode = "declined";
                var dec = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip vlc -y"));
                await Task.Delay(500); SelfTest.Press(dec, "Install 2 packages"); await Task.Delay(900);
                await Shot(dec, "toast-install-declined", "Consent prompt declined: exit code 1223", 500);
                dec.CloseAnimated(); await Task.Delay(350);

                // automatic mode countdown
                var s = Settings.Current;
                var saved = (s.AutoInstallCommands, s.AutoInstallDelaySeconds);
                s.AutoInstallCommands = true; s.AutoInstallDelaySeconds = 60;
                mode = "hang";
                var t = App.Toasts.ShowAutoInstall(ClipItem.FromText("winget install --id Git.Git -e"));
                var aw = App.Toasts.LiveToasts.FirstOrDefault(x => x.TitleText.Text.Contains("Installing automatically") || x.TitleText.Text.Contains("自動"));
                if (aw != null) { await Shot(aw, "toast-install-auto", "Automatic mode: command, countdown, Cancel", 900); SelfTest.Press(aw, "Cancel"); await Task.Delay(300); aw.CloseAnimated(); }
                (s.AutoInstallCommands, s.AutoInstallDelaySeconds) = saved;
                await Task.Delay(350);
            }
            finally { InstallRunner.RunnerForTest = null; InstallRunner.PathForTest = null; ClipboardWatcher.TextSinkForTest = null; InstallToast.ForgetLastAutoForTest(); }
        }

        // The Clipboard page scrolled to the automatic install card.
        internal static async Task InstallCardSceneAsync(ControlCentreWindow cc)
        {
            cc.Navigate("clipboard", animate: false);
            await Task.Delay(700);
            var title = SelfTest.Descend<TextBlock>(cc).FirstOrDefault(t => t.Text == "Install copied commands automatically");
            ScrollTo(title);
            await Shot(cc, "settings-install-auto", "Settings, Clipboard: install copied commands automatically", 900);
        }
    }
}
