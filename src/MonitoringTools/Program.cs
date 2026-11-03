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
                if (args.Contains("--supervise")) return ServiceHost.Supervise();
                if (args.Contains("--elevate-on")) return ServiceHost.ElevateHelper(true);
                if (args.Contains("--elevate-off")) return ServiceHost.ElevateHelper(false);
                if (args.Contains("--agent")) return RunAgent();
                var shotIdx = Array.IndexOf(args, "--screenshots");
                if (shotIdx >= 0)
                {
                    App.ScreenshotDir = shotIdx + 1 < args.Length ? args[shotIdx + 1] : "huishots";
                    var demo = new App();
                    demo.InitializeComponent();
                    return demo.Run();
                }

                // Launched by hand (double-click, Start menu, explorer relaunch)
                if (!ServiceHost.IsRunningInstalled)
                {
                    ServiceHost.Install();
                    File.WriteAllText(Path.Combine(Settings.DataDir, "welcome.flag"), DateTime.Now.ToString("O"));
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
