using System;
using System.Collections.Generic;
using System.IO;
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
    public static partial class SelfTest
    {
        private static string TbText(TextBlock t) => new System.Windows.Documents.TextRange(t.ContentStart, t.ContentEnd).Text.Trim();
        private static string ToastTexts(DependencyObject root) => string.Join(" | ", Descend<TextBlock>(root).Select(TbText));
        private static bool Has(DependencyObject root, string text) => Descend<TextBlock>(root).Any(t => TbText(t).Contains(text));

        private static async Task<bool> ToastWait(Func<bool> cond, int ms = 4000)
        {
            for (int waited = 0; waited < ms; waited += 40) { if (cond()) return true; await Task.Delay(40); }
            return cond();
        }

        // ---------------------------------------------------------------- the parser

        private static void InstallParserChecks()
        {
            Check("install commands: plain choco and winget commands are read, with the exact arguments that would run", () =>
            {
                var cases = new (string Text, InstallManager M, string Verb, string[] Packages, string Display)[]
                {
                    ("choco install 7zip vlc -y", InstallManager.Chocolatey, "install", new[] { "7zip", "vlc" }, "choco install 7zip vlc -y"),
                    ("  choco upgrade git  ", InstallManager.Chocolatey, "upgrade", new[] { "git" }, "choco upgrade git"),
                    ("choco.exe install 7zip --yes --no-progress", InstallManager.Chocolatey, "install", new[] { "7zip" }, "choco install 7zip --yes --no-progress"),
                    ("CHOCO INSTALL Notepad2-mod", InstallManager.Chocolatey, "install", new[] { "Notepad2-mod" }, "choco install Notepad2-mod"),
                    ("winget install --id Microsoft.PowerToys -e", InstallManager.Winget, "install", new[] { "Microsoft.PowerToys" }, "winget install --id Microsoft.PowerToys -e"),
                    ("winget install Microsoft.PowerToys", InstallManager.Winget, "install", new[] { "Microsoft.PowerToys" }, "winget install --id Microsoft.PowerToys"),
                    ("winget upgrade --id Git.Git --exact --silent", InstallManager.Winget, "upgrade", new[] { "Git.Git" }, "winget upgrade --id Git.Git --exact --silent"),
                    ("choco install 7zip --source=chocolatey", InstallManager.Chocolatey, "install", new[] { "7zip" }, "choco install 7zip"),
                    ("winget install --id Git.Git --source winget -e", InstallManager.Winget, "install", new[] { "Git.Git" }, "winget install --id Git.Git -e"),
                    ("winget install --id Git.Git --scope user", InstallManager.Winget, "install", new[] { "Git.Git" }, "winget install --id Git.Git --scope=user"),
                    ("choco install 7zip 7zip VLC", InstallManager.Chocolatey, "install", new[] { "7zip", "VLC" }, "choco install 7zip VLC"),
                };
                foreach (var c in cases)
                {
                    var p = InstallCommands.Parse(c.Text);
                    if (p == null) return $"'{c.Text}' was not recognised";
                    if (!p.Accepted) return $"'{c.Text}' was refused: " + string.Join("; ", p.Refusals.Select(r => r.Token + " " + r.Reason));
                    if (p.Manager != c.M || p.Verb != c.Verb) return $"'{c.Text}' read as {p.Manager} {p.Verb}";
                    if (!p.Packages.SequenceEqual(c.Packages)) return $"'{c.Text}' read packages [{string.Join(",", p.Packages)}]";
                    if (p.Display != c.Display) return $"'{c.Text}' would run '{p.Display}', not '{c.Display}'";
                }
                if (InstallCommands.Parse("winget install --id X --scope machine").Permission != InstallPermission.ConsentExpected) return "winget machine scope should expect a prompt";
                if (InstallCommands.Parse("winget install --id X --scope user").Permission != InstallPermission.NoPromptExpected) return "winget user scope should expect no prompt";
                if (InstallCommands.Parse("winget install --id X").Permission != InstallPermission.InstallerDecides) return "winget default scope leaves it to the installer";
                if (InstallCommands.Parse("choco install x").Permission != InstallPermission.ConsentAlways) return "choco always asks";
                return null;
            });

            Check("install commands: anything off the allow list is refused with a reason, never ignored or run", () =>
            {
                var refused = new (string Text, string Token)[]
                {
                    ("choco install 7zip --ignore-checksums -s http://pkgs.example.net/feed", "--ignore-checksums"),
                    ("choco install 7zip --ignore-checksums -s http://pkgs.example.net/feed", "-s"),
                    ("choco install 7zip --source https://pkgs.example.net/feed", "--source"),
                    ("choco install 7zip --source=evil", "--source=evil"),
                    ("choco install 7zip && del C:\\x", "&&"),
                    ("choco install 7zip; calc", "7zip;"),
                    ("choco install 7zip | more", "|"),
                    ("choco install 7zip > out.txt", ">"),
                    ("choco install 7zip < in.txt", "<"),
                    ("choco install `calc`", "`calc`"),
                    ("choco install $env:USERNAME", "$env:USERNAME"),
                    ("choco install %TEMP%", "%TEMP%"),
                    ("choco install \"7zip\"", "\"7zip\""),
                    ("choco install 7zip & calc", "&"),
                    ("choco install 7zip --params '/x:y'", "--params"),
                    ("choco install 7zip --package-parameters=/x", "--package-parameters=/x"),
                    ("choco install 7zip --install-arguments=foo", "--install-arguments=foo"),
                    ("choco install 7zip --override", "--override"),
                    ("choco install 7zip --force", "--force"),
                    ("choco install 7zip -f", "-f"),
                    ("choco install 7zip --pre", "--pre"),
                    ("choco install 7zip --prerelease", "--prerelease"),
                    ("choco install 7zip --allow-downgrade", "--allow-downgrade"),
                    ("choco install 7zip --ignore-package-exit-codes", "--ignore-package-exit-codes"),
                    ("choco install 7zip --scripts", "--scripts"),
                    ("choco install 7zip --version 1.0", "--version"),
                    ("choco install https://example.net/x.nupkg", "https://example.net/x.nupkg"),
                    ("choco install C:\\tools\\x.config", "C:\\tools\\x.config"),
                    ("choco install packages.config", "packages.config"),
                    ("choco install -bad", "-bad"),
                    ("choco upgrade all", "all"),
                    ("winget install --id A --id B", "--id"),
                    ("winget install a b", "b"),
                    ("winget upgrade --all", "--all"),
                    ("winget install --id X --source msstore", "--source"),
                    ("winget install --id X -s https://evil.example/", "-s"),
                    ("winget install --id X --scope root", "--scope"),
                    ("winget install --id X --interactive", "--interactive"),
                    ("winget install --id X --override=/s", "--override=/s"),
                    ("choco install 7zip\nchoco install vlc", "line break"),
                };
                foreach (var c in refused)
                {
                    var p = InstallCommands.Parse(c.Text);
                    if (p == null) return $"'{c.Text}' was not recognised as an install command, so it would be ignored";
                    if (p.Accepted) return $"'{c.Text}' was accepted";
                    if (!p.Refusals.Any(r => r.Token.Split(' ')[0] == c.Token || r.Token == c.Token)) return $"'{c.Text}' did not name '{c.Token}'; named: " + string.Join(" | ", p.Refusals.Select(r => r.Token));
                    if (p.Refusals.Any(r => string.IsNullOrWhiteSpace(r.Reason))) return $"'{c.Text}' has a refusal with no reason";
                }
                var none = InstallCommands.Parse("choco install");
                if (none == null || none.Accepted) return "choco install with no package was not refused";
                // text that only mentions a command is just text
                foreach (var t in new[] { "I use choco install 7zip daily", "winget search foo", "choco list", "choco", "please winget install x", "echo choco install 7zip" })
                    if (InstallCommands.Parse(t) != null) return $"'{t}' is not an install command";
                if (ClipItem.FromText("choco install 7zip").Kind != ClipKind.Install) return "a copied choco command was not classed as an install command";
                if (ClipItem.FromText("remember to choco install 7zip").Kind == ClipKind.Install) return "text that merely mentions choco became an install command";
                return null;
            });

            Check("install commands: automatic mode accepts only the strict subset and respects the package limit", () =>
            {
                string R(string text, int max = 5) => InstallCommands.Parse(text).AutoRefusal(max);
                foreach (var ok in new[] { "choco install 7zip", "choco install 7zip vlc -y", "choco upgrade git --yes", "winget install --id Git.Git", "winget install --id Git.Git -e", "winget upgrade --id Git.Git --exact" })
                    if (R(ok) != null) return $"'{ok}' should run automatically but: " + R(ok);
                foreach (var no in new[] { "choco install 7zip --no-progress", "winget install Git.Git", "winget install --id Git.Git --silent", "winget install --id Git.Git --scope user", "choco install 7zip --force", "choco install 7zip && x" })
                    if (R(no) == null) return $"'{no}' should not run automatically";
                if (R("choco install a b c d e f") == null) return "six packages ran automatically with a limit of five";
                if (R("choco install a b c d e f", 6) != null) return "six packages were refused with a limit of six";
                var s = new Settings();
                if (s.AutoInstallCommands || s.AutoInstallDelaySeconds != 10 || s.AutoInstallMaxPackages != 5) return "automatic mode is not off by default with 10 s and 5 packages";
                return null;
            });

            Check("install commands: the runner starts the program with exactly the checked arguments and reports its exit code", () =>
            {
                // a throwaway stand-in for winget, so nothing is installed
                var root = Path.Combine(Path.GetTempPath(), "mt-install-run-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                var fake = Path.Combine(root, "winget.cmd");
                try
                {
                    File.WriteAllText(fake, "@echo off\r\necho ARGS: %*\r\nif \"%FAKE_EXIT%\"==\"\" exit /b 0\r\nexit /b %FAKE_EXIT%\r\n");
                    InstallRunner.PathForTest = m => m == InstallManager.Winget ? fake : null;
                    var steps = new List<string>();
                    var c = InstallCommands.Parse("winget install --id Git.Git -e");
                    var r = Task.Run(() => InstallRunner.RunAsync(c, steps.Add, CancellationToken.None)).GetAwaiter().GetResult();
                    if (!r.Ok || r.ExitCode != 0) return $"fake winget failed: {r.ExitCode} {r.Message}";
                    if (!r.Output.Contains("ARGS: install --id Git.Git -e")) return "arguments were not passed exactly: " + r.Output;
                    if (!steps.SequenceEqual(new[] { "check", "install" })) return "steps were " + string.Join(",", steps);
                    if (string.IsNullOrEmpty(r.LogPath) || !File.Exists(r.LogPath) || !File.ReadAllText(r.LogPath).Contains("winget install --id Git.Git -e")) return "no log with the exact command";
                    Environment.SetEnvironmentVariable("FAKE_EXIT", "7");
                    var bad = Task.Run(() => InstallRunner.RunAsync(c, null, CancellationToken.None)).GetAwaiter().GetResult();
                    if (bad.Ok || bad.ExitCode != 7 || !bad.Message.Contains("exit code 7")) return $"a failing run reported {bad.Ok} {bad.ExitCode} {bad.Message}";
                    InstallRunner.PathForTest = m => null;
                    var none = Task.Run(() => InstallRunner.RunAsync(c, null, CancellationToken.None)).GetAwaiter().GetResult();
                    if (none.Ok || !none.Message.Contains("not installed")) return "a missing winget was not reported: " + none.Message;
                    return null;
                }
                finally { Environment.SetEnvironmentVariable("FAKE_EXIT", null); InstallRunner.PathForTest = null; try { Directory.Delete(root, true); } catch { } }
            });
        }

        // ---------------------------------------------------------------- the install toast

        private static async Task InstallToastChecksAsync()
        {
            var savedSecs = Settings.Current.ToastSeconds;
            var sink = new List<string>();
            int ran = 0; InstallCommand last = null; string mode = "ok"; TaskCompletionSource<bool> gate = null;   // holds the fake install until a check has read the running state
            string logFile = Path.Combine(Path.GetTempPath(), "mt-install-log-" + Guid.NewGuid().ToString("N") + ".log");
            File.WriteAllText(logFile, "log");
            InstallRunner.PathForTest = m => "C:\\fake\\" + m;
            ClipboardWatcher.TextSinkForTest = sink.Add;
            InstallRunner.RunnerForTest = async (c, step, ct) =>
            {
                ran++; last = c;
                step?.Invoke("check");
                try
                {
                    if (c.Manager == InstallManager.Chocolatey) step?.Invoke("consent");
                    if (gate != null) await gate.Task.WaitAsync(ct); else await Task.Delay(mode == "hang" ? 60000 : 250, ct);
                }
                catch (OperationCanceledException) { return new InstallResult { Cancelled = true, Message = "Cancelled. The install was stopped." }; }
                step?.Invoke("install");
                if (mode == "declined") return new InstallResult { Declined = true, ExitCode = 1223, Message = "The Windows consent prompt was declined or timed out. Nothing was installed.", Took = TimeSpan.FromSeconds(14) };
                if (mode == "fail") return new InstallResult { ExitCode = 5, Message = "The install ended with exit code 5.", Output = "line one\nline two" };
                var r = new InstallResult { Ok = true, Message = "Installed.", Took = TimeSpan.FromSeconds(52), LogPath = logFile };
                foreach (var p in c.Packages) r.Versions[p] = p == "7zip" ? "24.09" : "3.0.21";
                return r;
            };
            Settings.Current.ToastSeconds = 600;
            try
            {
                await CheckAsync("install toast: choco offers names, source and the consent note, and runs nothing until Install is pressed", async () =>
                {
                    gate?.TrySetResult(true); gate = null; ran = 0; mode = "ok";
                    var w = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip vlc -y"));
                    await Task.Delay(500); w.UpdateLayout();
                    var t = ToastTexts(w);
                    foreach (var want in new[] { "Install command", "Packages", "7zip", "vlc", "Source", "Chocolatey community feed (default)", "Permission", "Windows will ask for permission (consent prompt)", "Nothing runs until you press Install." })
                        if (!t.Contains(want)) return $"missing '{want}' in: {t}";
                    if (FindButton(w, "Install 2 packages") == null || FindButton(w, "Copy command") == null) return "buttons: " + string.Join(", ", w.ActionButtons.Select(b => ButtonLabel(b)));
                    await Task.Delay(800);
                    if (ran != 0) return "the command ran before Install was pressed";
                    w.CloseAnimated();
                    return null;
                });

                await CheckAsync("install toast: winget names the package, says the installer decides, and offers a single Install", async () =>
                {
                    var w = App.Toasts.ShowClip(ClipItem.FromText("winget install --id Microsoft.PowerToys -e"));
                    await Task.Delay(400); w.UpdateLayout();
                    var t = ToastTexts(w);
                    foreach (var want in new[] { "Package", "Microsoft.PowerToys", "winget community source (default)", "The package's installer decides. Windows may ask." })
                        if (!t.Contains(want)) return $"missing '{want}' in: {t}";
                    if (FindButton(w, "Install") == null) return "no Install button";
                    w.CloseAnimated();
                    return null;
                });

                await CheckAsync("install toast: Install runs the exact command, shows the steps, then the versions with Open log and Copy summary", async () =>
                {
                    ran = 0; mode = "ok"; sink.Clear(); var held = gate = new TaskCompletionSource<bool>();
                    var w = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip vlc -y"));
                    await Task.Delay(300);
                    if (!Press(w, "Install 2 packages")) return "Install 2 packages could not be pressed";
                    await ToastWait(() => ToastTexts(w).Contains("Waiting for the Windows consent prompt"), 3000);
                    var running = ToastTexts(w);
                    if (!running.Contains("Command checked") || !running.Contains("Waiting for the Windows consent prompt") || !running.Contains("The app never answers it for you.")) return "no steps while running: " + running;
                    if (!w.TitleText.Text.Contains("Installing 2 packages")) return "title while running: " + w.TitleText.Text;
                    if (FindButton(w, "Cancel") == null) return "no Cancel while installing";
                    gate = null; held.TrySetResult(true);   // let the fake install finish now that the running state was read
                    if (!await ToastWait(() => w.TitleText.Text == "Installed")) return "never reached Installed: " + w.TitleText.Text;
                    if (ran != 1) return $"the runner started {ran} times";
                    if (last.Display != "choco install 7zip vlc -y") return "ran '" + last.Display + "'";
                    var t = ToastTexts(w);
                    if (!t.Contains("7zip 24.09") || !t.Contains("vlc 3.0.21")) return "versions missing: " + t;
                    if (!t.Contains("Took 52 s. The command is saved in Activity.")) return "no timing line: " + t;
                    if (FindButton(w, "Open log") == null || FindButton(w, "Copy summary") == null) return "no Open log / Copy summary";
                    if (!Activity.Recent(6).Any(e => e.Title.Contains("choco install 7zip vlc -y") && e.Title.StartsWith("Install finished"))) return "the run was not logged in Activity with its command";
                    Press(w, "Copy summary");
                    if (!sink.Any(s => s.Contains("Installed 7zip 24.09, vlc 3.0.21 with choco") && s.Contains("choco install 7zip vlc -y"))) return "summary not copied: " + string.Join("|", sink);
                    w.CloseAnimated();
                    return null;
                });

                await CheckAsync("install toast: a declined consent prompt is a plain outcome with exit code 1223 and Try again runs it again", async () =>
                {
                    gate?.TrySetResult(true); gate = null; ran = 0; mode = "declined";
                    var w = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip"));
                    await Task.Delay(300);
                    Press(w, "Install");
                    if (!await ToastWait(() => w.TitleText.Text == "Not installed")) return "title: " + w.TitleText.Text;
                    var t = ToastTexts(w);
                    if (!t.Contains("The Windows consent prompt was declined or timed out. Nothing was installed.") || !t.Contains("exit code 1223")) return "outcome text: " + t;
                    if (FindButton(w, "Try again") == null || FindButton(w, "Copy details") == null) return "no Try again / Copy details";
                    mode = "ok";
                    Press(w, "Try again");
                    if (!await ToastWait(() => w.TitleText.Text == "Installed")) return "retry did not finish: " + w.TitleText.Text;
                    if (ran != 2) return $"ran {ran} times";
                    mode = "fail";
                    w.CloseAnimated();
                    var w2 = App.Toasts.ShowClip(ClipItem.FromText("winget install --id Git.Git"));
                    await Task.Delay(300); Press(w2, "Install");
                    if (!await ToastWait(() => w2.TitleText.Text == "Not installed")) return "a failed run title: " + w2.TitleText.Text;
                    if (!ToastTexts(w2).Contains("exit code 5") || !ToastTexts(w2).Contains("line two")) return "failure details: " + ToastTexts(w2);
                    w2.CloseAnimated();
                    mode = "ok";
                    return null;
                });

                await CheckAsync("install toast: Cancel stops a waiting install and says so", async () =>
                {
                    gate?.TrySetResult(true); gate = null; ran = 0; mode = "hang";
                    var w = App.Toasts.ShowClip(ClipItem.FromText("winget install --id Git.Git"));
                    await Task.Delay(300); Press(w, "Install");
                    await Task.Delay(300);
                    if (!Press(w, "Cancel")) return "no Cancel";
                    if (!await ToastWait(() => w.TitleText.Text == "Cancelled")) return "title: " + w.TitleText.Text;
                    mode = "ok";
                    w.CloseAnimated();
                    return null;
                });

                await CheckAsync("install toast: a refused command says why per flag, offers only Copy command and runs nothing", async () =>
                {
                    ran = 0; sink.Clear();
                    var w = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip --ignore-checksums -s http://pkgs.example.net/feed"));
                    await Task.Delay(400); w.UpdateLayout();
                    if (w.TitleText.Text != "Not offered to install") return "title: " + w.TitleText.Text;
                    var t = ToastTexts(w);
                    if (!t.Contains("--ignore-checksums") || !t.Contains("skips the file check") || !t.Contains("picks a custom source; only the default is used")) return "reasons: " + t;
                    var labels = w.ActionButtons.Select(b => ButtonLabel(b)).ToList();
                    if (!labels.SequenceEqual(new[] { "Copy command" })) return "buttons: " + string.Join(", ", labels);
                    Press(w, "Copy command");
                    if (!sink.Contains("choco install 7zip --ignore-checksums -s http://pkgs.example.net/feed")) return "copied: " + string.Join("|", sink);
                    await Task.Delay(500);
                    if (ran != 0) return "a refused command ran";
                    w.CloseAnimated();
                    return null;
                });

                await CheckAsync("install toast: a missing package manager is stated and nothing that would run is offered", async () =>
                {
                    ran = 0;
                    var keep = InstallRunner.PathForTest;
                    InstallRunner.PathForTest = m => null;
                    try
                    {
                        var w = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip"));
                        await Task.Delay(400); w.UpdateLayout();
                        if (!ToastTexts(w).Contains("Chocolatey is not installed on this computer, so nothing here can run it.")) return "no missing note: " + ToastTexts(w);
                        var labels = w.ActionButtons.Select(b => ButtonLabel(b)).ToList();
                        if (!labels.SequenceEqual(new[] { "Copy command" })) return "buttons: " + string.Join(", ", labels);
                        w.CloseAnimated();
                        var w2 = App.Toasts.ShowClip(ClipItem.FromText("winget install --id Git.Git"));
                        await Task.Delay(300);
                        if (FindButton(w2, "Install") != null) return "winget offered Install without winget";
                        w2.CloseAnimated();
                        return null;
                    }
                    finally { InstallRunner.PathForTest = keep; }
                });

                await CheckAsync("install toast: automatic mode counts down with Cancel, never starts a refused command, and starts a plain one by itself", async () =>
                {
                    var s = Settings.Current;
                    var saved = (s.AutoInstallCommands, s.AutoInstallDelaySeconds, s.AutoInstallMaxPackages);
                    try
                    {
                        s.AutoInstallCommands = false;
                        if (InstallToast.AutoEligible(ClipItem.FromText("choco install 7zip"), out _)) return "eligible while the setting is off";
                        s.AutoInstallCommands = true; s.AutoInstallDelaySeconds = 3; s.AutoInstallMaxPackages = 2;
                        foreach (var no in new[] { "choco install 7zip --ignore-checksums", "choco install a b c", "choco install x && y", "winget install Git.Git", "choco install 7zip -s evil" })
                            if (InstallToast.AutoEligible(ClipItem.FromText(no), out _)) return $"'{no}' was eligible";
                        bool chocoOk = !ServiceHost.IsElevated;
                        if (chocoOk && !InstallToast.AutoEligible(ClipItem.FromText("choco install 7zip -y"), out var why)) return "plain command not eligible: " + why;
                        if (!InstallToast.AutoEligible(ClipItem.FromText("winget install --id Git.Git -e"), out var why2)) return "plain winget not eligible: " + why2;

                        // cancel during the delay
                        gate?.TrySetResult(true); gate = null; ran = 0; mode = "ok"; InstallToast.ForgetLastAutoForTest();
                        var item = ClipItem.FromText("winget install --id Git.Git -e");
                        var t1 = App.Toasts.ShowAutoInstall(item);
                        if (t1 == null) return "no countdown toast";
                        await Task.Delay(300);
                        if (t1.State != "countdown") return "state " + t1.State;
                        var w = App.Toasts.LiveToasts.First(x => !x.IsClosing && x.TitleText.Text == "Installing automatically");
                        if (!ToastTexts(w).Contains("winget install --id Git.Git -e")) return "the countdown does not show the command: " + ToastTexts(w);
                        if (!Press(w, "Cancel")) return "no Cancel on the countdown";
                        await Task.Delay(3800);
                        if (ran != 0 || t1.State != "cancelled") return $"cancel failed: ran {ran}, state {t1.State}";

                        // the same command is not started twice in ten minutes
                        if (App.Toasts.ShowAutoInstall(item) != null) return "the same command started again straight away";

                        // let it run
                        InstallToast.ForgetLastAutoForTest();
                        var t2 = App.Toasts.ShowAutoInstall(item);
                        if (t2 == null) return "no second countdown";
                        await Task.Delay(1200);
                        if (ran != 0) return "ran before the delay was over";
                        if (!await ToastWait(() => ran == 1, 6000)) return "did not start after the delay";
                        if (!await ToastWait(() => t2.State == "done", 4000)) return "state " + t2.State;
                        foreach (var x in App.Toasts.LiveToasts.ToList()) x.CloseAnimated();
                        return null;
                    }
                    finally { (s.AutoInstallCommands, s.AutoInstallDelaySeconds, s.AutoInstallMaxPackages) = saved; InstallToast.ForgetLastAutoForTest(); }
                });

                await CheckAsync("settings: the automatic install card is off by default, dims its numbers until the switch is on, and keeps the rules visible", async () =>
                {
                    var saved = Settings.Current.AutoInstallCommands;
                    Settings.Current.AutoInstallCommands = false;
                    var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                    try
                    {
                        cc.Show(); await Task.Delay(700);
                        cc.Navigate("clipboard", animate: false); await Task.Delay(500); cc.UpdateLayout();
                        var sw = Descend<CheckBox>(cc).FirstOrDefault(c => Descend<TextBlock>(c).Any(t => t.Text == "Install without asking first"));
                        if (sw == null) return "no switch on the Clipboard page";
                        if (sw.IsChecked == true) return "the switch is on by default";
                        var dim = Descend<NumberField>(cc).Select(n => n.Parent).OfType<StackPanel>().FirstOrDefault(p => !p.IsEnabled);
                        if (dim == null) return "the numbers are not dimmed while the switch is off";
                        sw.IsChecked = true; await Task.Delay(100);
                        if (!Settings.Current.AutoInstallCommands) return "checking the switch did not save the setting";
                        if (!dim.IsEnabled) return "the numbers stay dimmed after the switch is on";
                        var t = ToastTexts(cc);
                        foreach (var want in new[] { "Installed automatically only when", "Refused flags", "--ignore-checksums", "--source / -s", "Windows shows its consent prompt every time for choco." })
                            if (!t.Contains(want)) return $"card is missing '{want}'";
                        return null;
                    }
                    finally { Settings.Current.AutoInstallCommands = saved; cc.Close(); }
                });

                await CheckAsync("install toast: every state is written in Cantonese when Cantonese is on", async () =>
                {
                    var saved = Settings.Current.LanguageMode;
                    Settings.Current.LanguageMode = LanguageMode.Cantonese; Language.Refresh();
                    try
                    {
                        var left = new List<string>();
                        async Task Look(string name, ToastWindow w, Func<Task> act = null)
                        {
                            await Task.Delay(350); if (act != null) { await act(); await Task.Delay(350); }
                            LanguageAudit.Reset(); LanguageAudit.Scan(name, w);
                            left.AddRange(LanguageAudit.Findings.Select(f => name + ": " + f.Text));
                            w.CloseAnimated();
                        }
                        mode = "ok";
                        await Look("choco offer", App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip vlc -y")));
                        await Look("winget offer", App.Toasts.ShowClip(ClipItem.FromText("winget install --id Git.Git")));
                        await Look("refused", App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip --ignore-checksums --force -s http://pkgs.example.net/feed && calc")));
                        var running = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip"));
                        mode = "hang";
                        await Look("running", running, async () => { Press(running, "Install"); await Task.Delay(200); });
                        mode = "ok";
                        var done = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip"));
                        await Look("installed", done, async () => { await Task.Delay(300); Press(done, "Install"); await ToastWait(() => done.TitleText.Text.Contains("已安裝") || done.TitleText.Text == "Installed"); });
                        mode = "declined";
                        var dec = App.Toasts.ShowClip(ClipItem.FromText("choco install 7zip"));
                        await Look("declined", dec, async () => { await Task.Delay(300); Press(dec, "Install"); await ToastWait(() => dec.TitleText.Text.Contains("未安裝") || dec.TitleText.Text == "Not installed"); });
                        mode = "ok";
                        return left.Count == 0 ? null : "untranslated: " + string.Join(" | ", left.Take(12));
                    }
                    finally { Settings.Current.LanguageMode = saved; Language.Refresh(); mode = "ok"; }
                });
            }
            finally
            {
                InstallRunner.RunnerForTest = null; InstallRunner.PathForTest = null; ClipboardWatcher.TextSinkForTest = null;
                Settings.Current.ToastSeconds = savedSecs;
                try { File.Delete(logFile); } catch { }
                foreach (var x in App.Toasts.LiveToasts.ToList()) x.CloseAnimated();
            }
        }

        // ---------------------------------------------------------------- the text toast

        private static async Task TextToastChecksAsync()
        {
            var savedSecs = Settings.Current.ToastSeconds;
            var sink = new List<string>();
            ClipboardWatcher.TextSinkForTest = sink.Add;
            Settings.Current.ToastSeconds = 600;
            const string original = "Meeting notes, Thursday\nShip the clipboard update on Friday.\nAsk Dana about the invoice wording.\nBook the room for 14:30.";
            TextBox BoxOf(DependencyObject w) => Descend<TextBox>(w).FirstOrDefault(t => AutomationProperties.GetName(t) == "Copied text, editable");
            void KeyDown(Window w, Key k, ModifierKeys? mods = null)
            {
                TextToastEditor.ModifiersForTest = mods ?? ModifierKeys.None;
                try
                {
                    var src = PresentationSource.FromVisual(w);
                    w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, k) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                }
                finally { TextToastEditor.ModifiersForTest = null; }
            }
            void Click(TextBox box) => box.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = box });
            try
            {
                await CheckAsync("text toast: opens quiet and read-only with the counts, and Edit text is the one lead button", async () =>
                {
                    var item = ClipItem.FromText(original);
                    var w = App.Toasts.ShowClip(item);
                    await Task.Delay(400); w.UpdateLayout();
                    if (item.Kind != ClipKind.Text) return "kind " + item.Kind;
                    if (w.TitleText.Text != "Copied text") return "title: " + w.TitleText.Text;
                    if (w.MetaText.Text != $"{original.Length} chars · 4 lines") return "meta: " + w.MetaText.Text;
                    var box = BoxOf(w);
                    if (box == null) return "no text field";
                    if (!box.IsReadOnly) return "the field is editable before any click";
                    if (!Native.IsNoActivate(w) || w.FocusHeld) return "the toast can take focus before a click";
                    if (!Has(w, "Click the text to edit it. The clipboard follows your edits.")) return "no hint: " + ToastTexts(w);
                    if (FindButton(w, "Edit text") == null) return "no Edit text";
                    if (box.MaxHeight != 180) return "body max height " + box.MaxHeight;
                    w.CloseAnimated();
                    var long1 = App.Toasts.ShowClip(ClipItem.FromText(string.Join("\n", Enumerable.Range(1, 96).Select(i => "Line number " + i))));
                    await Task.Delay(300);
                    if (!Has(long1, "Showing 8 of 96 lines")) return "long text hint: " + ToastTexts(long1);
                    long1.CloseAnimated();
                    var md = App.Toasts.ShowClip(ClipItem.FromText("# Heading\n\n- one\n- two"));
                    await Task.Delay(300);
                    if (BoxOf(md) != null) return "a Markdown toast became editable";
                    md.CloseAnimated();
                    return null;
                });

                await CheckAsync("text toast: a click makes the toast activatable, holds its timer and swaps the buttons to Keep and Revert", async () =>
                {
                    var item = ClipItem.FromText(original);
                    var w = App.Toasts.ShowClip(item);
                    await Task.Delay(300);
                    var box = BoxOf(w);
                    Click(box); await Task.Delay(150); w.UpdateLayout();
                    if (Native.IsNoActivate(w)) return "still no-activate after the click";
                    if (!w.FocusHeld || box.IsReadOnly) return $"held {w.FocusHeld}, readonly {box.IsReadOnly}";
                    if (w.MetaText.Text != "edited") return "meta: " + w.MetaText.Text;
                    var labels = w.ActionButtons.Select(b => ButtonLabel(b)).ToList();
                    if (!labels.SequenceEqual(new[] { "Keep", "Revert" })) return "buttons: " + string.Join(", ", labels);
                    var t = ToastTexts(w);
                    if (!t.Contains("Ctrl+Enter") || !t.Contains("Esc")) return "no key hints: " + t;
                    if (!t.Contains("The clipboard already holds your edit.") || !t.Contains("Copied")) return "no hint or Copied pill: " + t;
                    w.CloseAnimated();
                    return null;
                });

                await CheckAsync("text toast: edits reach the clipboard 400 ms after the last key, once, in place in history, and the watcher is told to ignore it", async () =>
                {
                    sink.Clear();
                    var item = ClipItem.FromText(original);
                    App.History.Add(item);
                    int before = App.History.Items.Count, updates = 0, ignores = ClipboardWatcher.IgnoreCallsForTest;
                    Action bump = () => updates++;
                    App.History.Updated += bump;
                    try
                    {
                        var w = App.Toasts.ShowClip(item);
                        await Task.Delay(300);
                        var box = BoxOf(w);
                        Click(box); await Task.Delay(100);
                        box.Text = original.Replace("Friday", "Mon");
                        box.Text = original.Replace("Friday", "Monda");
                        await Task.Delay(100);
                        box.Text = original.Replace("Friday", "Monday");
                        if (sink.Count != 0) return "the clipboard was written before the pause";
                        await Task.Delay(250);
                        if (sink.Count != 0) return "written after only 350 ms";
                        await Task.Delay(400);
                        if (sink.Count != 1 || sink[0] != original.Replace("Friday", "Monday")) return "writes: " + string.Join(" || ", sink);
                        if (ClipboardWatcher.IgnoreCallsForTest != ignores + 1) return "the watcher was told to ignore " + (ClipboardWatcher.IgnoreCallsForTest - ignores) + " writes, not 1";
                        if (App.History.Items.Count != before) return "history grew from " + before + " to " + App.History.Items.Count;
                        if (item.Text != original.Replace("Friday", "Monday") || !item.Edited) return $"history entry not updated in place: edited {item.Edited}";
                        if (updates < 1) return "the History page was not told";
                        if (!Has(w, $"{item.Text.Length} chars · 4 lines")) return "count line: " + ToastTexts(w);
                        w.CloseAnimated();
                        App.History.Items.Remove(item);
                        return null;
                    }
                    finally { App.History.Updated -= bump; }
                });

                await CheckAsync("text toast: Esc in the text restores the original to the clipboard and history, and the toast goes quiet again", async () =>
                {
                    sink.Clear();
                    var item = ClipItem.FromText(original);
                    App.History.Add(item);
                    var w = App.Toasts.ShowClip(item);
                    await Task.Delay(300);
                    var box = BoxOf(w);
                    Click(box); await Task.Delay(100);
                    box.Text = "something else entirely";
                    await Task.Delay(700);
                    if (sink.Count != 1) return "edit not written: " + sink.Count;
                    FocusManager.SetFocusedElement(w, box);
                    KeyDown(w, Key.Escape);
                    await Task.Delay(150);
                    if (box.Text != original) return "text not restored: " + box.Text;
                    if (sink.Count != 2 || sink[1] != original) return "clipboard not restored: " + string.Join(" || ", sink);
                    if (item.Text != original || item.Edited) return "history entry not restored";
                    if (!Has(w, "Reverted. The clipboard holds the original text again.")) return "no reverted hint: " + ToastTexts(w);
                    if (w.FocusHeld || !Native.IsNoActivate(w) || !box.IsReadOnly) return "the toast did not go quiet again";
                    if (FindButton(w, "Edit text") == null) return "Edit text did not come back";
                    if (w.IsClosing) return "Esc in the text closed the toast";
                    w.CloseAnimated();
                    App.History.Items.Remove(item);
                    return null;
                });

                await CheckAsync("text toast: Esc anywhere else dismisses, Ctrl+Enter keeps the edit and closes, Tab runs text, Keep, Revert, Close", async () =>
                {
                    // Esc on a button dismisses and leaves the edit on the clipboard
                    sink.Clear();
                    var w = App.Toasts.ShowClip(ClipItem.FromText(original));
                    await Task.Delay(300);
                    var box = BoxOf(w); Click(box); await Task.Delay(100);
                    box.Text = "kept by Esc elsewhere"; await Task.Delay(700);
                    FocusManager.SetFocusedElement(w, FindButton(w, "Keep"));
                    KeyDown(w, Key.Escape); await Task.Delay(100);
                    if (!w.IsClosing) return "Esc on a button did not dismiss";
                    if (sink.LastOrDefault() != "kept by Esc elsewhere") return "dismissing lost the edit: " + string.Join("|", sink);

                    // Ctrl+Enter writes a pending edit and closes
                    sink.Clear();
                    var w2 = App.Toasts.ShowClip(ClipItem.FromText(original));
                    await Task.Delay(300);
                    var box2 = BoxOf(w2); Click(box2); await Task.Delay(100);
                    box2.Text = "kept by Ctrl+Enter";
                    FocusManager.SetFocusedElement(w2, box2);
                    KeyDown(w2, Key.Enter, ModifierKeys.Control); await Task.Delay(100);
                    if (!w2.IsClosing) return "Ctrl+Enter did not close";
                    if (sink.Count != 1 || sink[0] != "kept by Ctrl+Enter") return "Ctrl+Enter did not write the pending edit: " + string.Join("|", sink);

                    // Tab order
                    var w3 = App.Toasts.ShowClip(ClipItem.FromText(original));
                    await Task.Delay(300);
                    var box3 = BoxOf(w3); Click(box3); await Task.Delay(100);
                    var order = new List<object> { box3, FindButton(w3, "Keep"), FindButton(w3, "Revert"), w3.CloseButton, box3 };
                    FocusManager.SetFocusedElement(w3, box3);
                    for (int i = 1; i < order.Count; i++)
                    {
                        KeyDown(w3, Key.Tab);
                        if (!ReferenceEquals(FocusManager.GetFocusedElement(w3), order[i])) return $"Tab step {i} went to {FocusManager.GetFocusedElement(w3)}";
                    }
                    KeyDown(w3, Key.Tab, ModifierKeys.Shift);
                    if (!ReferenceEquals(FocusManager.GetFocusedElement(w3), w3.CloseButton)) return "Shift+Tab from the text did not go to Close";
                    w3.CloseAnimated();
                    return null;
                });

                await CheckAsync("text toast: the timer is held while the text has focus and resumes after Revert", async () =>
                {
                    Settings.Current.ToastSeconds = 1.5;
                    try
                    {
                        var w = App.Toasts.ShowClip(ClipItem.FromText(original));
                        await Task.Delay(300);
                        var box = BoxOf(w); Click(box);
                        await Task.Delay(3200);
                        if (w.IsClosing) return "the toast closed while it had focus";
                        FocusManager.SetFocusedElement(w, box);
                        KeyDown(w, Key.Escape);
                        if (!await ToastWait(() => w.IsClosing, 5000)) return "the timer did not resume after Revert";
                        return null;
                    }
                    finally { Settings.Current.ToastSeconds = 600; }
                });

                await CheckAsync("text toast: the Edit text button puts the caret at the end, and text toast strings are Cantonese when Cantonese is on", async () =>
                {
                    var w = App.Toasts.ShowClip(ClipItem.FromText(original));
                    await Task.Delay(300);
                    Press(w, "Edit text"); await Task.Delay(150);
                    var box = BoxOf(w);
                    if (box.IsReadOnly || box.CaretIndex != box.Text.Length) return $"readonly {box.IsReadOnly}, caret {box.CaretIndex}";
                    w.CloseAnimated();
                    var saved = Settings.Current.LanguageMode;
                    Settings.Current.LanguageMode = LanguageMode.Cantonese; Language.Refresh();
                    try
                    {
                        var left = new List<string>();
                        var a = App.Toasts.ShowClip(ClipItem.FromText(original)); await Task.Delay(400);
                        LanguageAudit.Reset(); LanguageAudit.Scan("text idle", a); left.AddRange(LanguageAudit.Findings.Select(f => "idle: " + f.Text));
                        Press(a, "編輯文字"); await Task.Delay(200);
                        LanguageAudit.Reset(); LanguageAudit.Scan("text editing", a); left.AddRange(LanguageAudit.Findings.Select(f => "editing: " + f.Text));
                        a.CloseAnimated();
                        return left.Count == 0 ? null : "untranslated: " + string.Join(" | ", left);
                    }
                    finally { Settings.Current.LanguageMode = saved; Language.Refresh(); }
                });
            }
            finally
            {
                ClipboardWatcher.TextSinkForTest = null; TextToastEditor.ModifiersForTest = null;
                Settings.Current.ToastSeconds = savedSecs;
                foreach (var x in App.Toasts.LiveToasts.ToList()) x.CloseAnimated();
            }
        }
    }
}
