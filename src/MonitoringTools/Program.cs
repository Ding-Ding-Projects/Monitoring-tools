using System;
using System.IO;
using System.Linq;
using System.Threading;
using MonitoringTools.Core;

namespace MonitoringTools
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--regex") return RegexCli.Run(args.Skip(1).ToArray());
                if (args.Length > 0 && args[0] == Core.Disks.VhdHelper.Arg) return Core.Disks.VhdHelper.Main(args);   // the elevated half of the virtual disk dialog
                if (args.Contains("--supervise")) return ServiceHost.Supervise();
                if (args.Contains("--elevate-on")) return ServiceHost.ElevateHelper(true);
                if (args.Contains("--elevate-off")) return ServiceHost.ElevateHelper(false);
                if (args.Contains("--agent")) return RunAgent();
                if (args.Contains(AutoUpdater.InstallArg)) return AutoUpdater.RunInstall();
                var testIdx = Array.IndexOf(args, "--selftest");
                if (testIdx >= 0)
                {
                    Settings.DataDirOverride = Path.Combine(Path.GetTempPath(), "MonitoringTools-capture-" + Environment.ProcessId);
                    App.SelfTestDir = testIdx + 1 < args.Length ? args[testIdx + 1] : "selftest";
                    var t = new App();
                    t.InitializeComponent();
                    return t.Run();
                }
                var liveIdx = Array.IndexOf(args, "--live-check");
                if (liveIdx >= 0)
                {
                    // Talks to real local services (voices, Ollama, the dish catalogue, a forge, the Minecraft jar),
                    // still inside a throwaway data folder so the real settings are never touched.
                    Settings.DataDirOverride = Path.Combine(Path.GetTempPath(), "MonitoringTools-live-" + Environment.ProcessId);
                    return LiveCheck.Run(liveIdx + 1 < args.Length ? args[liveIdx + 1] : "live-check");
                }
                var shotIdx = Array.IndexOf(args, "--screenshots");
                if (shotIdx >= 0)
                {
                    // A capture run uses its own empty data folder: it must never read or write the real settings.
                    Settings.DataDirOverride = Path.Combine(Path.GetTempPath(), "MonitoringTools-capture-" + Environment.ProcessId);
                    App.ScreenshotDir = shotIdx + 1 < args.Length ? args[shotIdx + 1] : "screenshots";
                    var demo = new App();
                    demo.InitializeComponent();
                    return demo.Run();
                }

                // Launched by hand (double-click, Start menu, explorer relaunch)
                if (!ServiceHost.IsRunningInstalled)
                {
                    // Look at what was here before the copy is replaced: only a genuine first install gets the welcome,
                    // an upgrade by hand gets the quiet "updated" notice and a reinstall just opens the control centre.
                    bool installedBefore = File.Exists(ServiceHost.InstalledExe);
                    var before = installedBefore ? AutoUpdater.OwnVersion(ServiceHost.InstalledExe) : null;
                    bool settingsExisted = File.Exists(Path.Combine(Settings.DataDir, "settings.json"));
                    ServiceHost.Install();
                    var kind = InstallNote.Decide(installedBefore, settingsExisted, before, AutoUpdater.Current);
                    try { InstallNote.Write(Settings.DataDir, kind, before, AutoUpdater.Current); }
                    catch (Exception ex) { Log.Warn("Could not leave the install note: " + ex.Message); }
                    ServiceHost.LaunchSupervisor();
                    return 0;
                }

                if (EventWaitHandle.TryOpenExisting(ServiceHost.ShowEventName, out var show))
                {
                    show.Set();
                    return 0;
                }

                Settings.Load();
                ServiceHost.ApplyAutostart();
                return ServiceHost.Supervise();
            }
            catch (Exception ex)
            {
                Log.Error("Fatal startup error", ex);
                return 1;
            }
        }

        private static int RunAgent()
        {
            using var mutex = new Mutex(true, ServiceHost.AgentMutexName, out bool first);
            if (!first) return ServiceHost.ExitQuit;
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}
