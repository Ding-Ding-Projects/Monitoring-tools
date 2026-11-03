using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Game;
using MonitoringTools.Media;
using MonitoringTools.Net;
using MonitoringTools.UI;

namespace MonitoringTools
{
    public partial class App : Application
    {
        public static ClipboardWatcher Clip { get; private set; }
        public static NetworkMonitor Net { get; private set; }
        public static PublicIpMonitor PublicIp { get; private set; }
        public static GameDetector Games { get; private set; }
        public static ToastManager Toasts { get; private set; }
        public static TrayIcon Tray { get; private set; }
        public static ClipHistory History { get; } = new ClipHistory();
        public static DateTime SnoozeUntil { get; set; } = DateTime.MinValue;
        public static bool IsSnoozed => DateTime.Now < SnoozeUntil;
        /// <summary>Set by "--screenshots DIR": render every surface with sample data and exit.</summary>
        public static string ScreenshotDir { get; set; }

        private OfflineWindow _offline;
        private bool _offlineDeferred;
        private Cause? _dismissedCause;
        private ControlCentreWindow _centre;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += (s, ex) =>
            {
                Log.Error("UI exception", ex.Exception);
                ex.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, ex) => Log.Error("Unhandled", ex.ExceptionObject as Exception);

            Settings.Load();
            Games = new GameDetector();
            Toasts = new ToastManager();
            Tray = new TrayIcon();
            Net = new NetworkMonitor();
            PublicIp = new PublicIpMonitor();

            if (ScreenshotDir != null)
            {
                // No watchers, no install, no saved settings: just render and leave.
                Dispatcher.BeginInvoke(new Action(async () => await ScreenshotRunner.RunAsync(ScreenshotDir)), DispatcherPriority.ApplicationIdle);
                return;
            }

            Log.Info($"Agent starting (elevated: {ServiceHost.IsElevated})");
            Clip = new ClipboardWatcher();

            Clip.Changed += OnClipboard;
            Net.StateChanged += OnNetworkState;
            PublicIp.Changed += (prev, cur) => { if (!ShouldSuppress && Settings.Current.PublicIpEnabled) Toasts.ShowIpChanged(prev, cur); };
            Games.GamingChanged += gaming =>
            {
                Tray.Refresh();
                if (!gaming && _offlineDeferred && Net.Current != null && !Net.Current.Online) ShowOffline(Net.Current);
            };

            Games.Start();
            Clip.Start();
            Net.Start();
            PublicIp.Start();
            Tray.Refresh();

            ListenForSignal(ServiceHost.QuitEventName, () => Quit(ServiceHost.ExitQuit));
            ListenForSignal(ServiceHost.ShowEventName, () => OpenControlCentre(null));

            var flag = Path.Combine(Settings.DataDir, "welcome.flag");
            if (File.Exists(flag))
            {
                try { File.Delete(flag); } catch { }
                ShowWelcome();
            }
        }

        private void ListenForSignal(string name, Action onSignal)
        {
            var handle = new EventWaitHandle(false, EventResetMode.AutoReset, name);
            var t = new Thread(() =>
            {
                while (true)
                {
                    handle.WaitOne();
                    Dispatcher.BeginInvoke(onSignal);
                }
            }) { IsBackground = true, Name = "signal:" + name };
            t.Start();
        }

        private void OnClipboard(ClipItem item)
        {
            History.Add(item);
            if (!Settings.Current.ClipboardEnabled) return;

            if (item.Command != null && Settings.Current.QuickCommandsEnabled)
            {
                // Quick commands always run, even in game mode; only the toast is held back.
                RunCommand(item.Command);
                return;
            }
            if (ShouldSuppress) return;
            Toasts.ShowClip(item);
        }

        public static void RunCommand(QuickCommand c)
        {
            switch (c.Kind)
            {
                case CommandKind.Convert:
                    if (System.IO.Directory.Exists(c.SourcePath)) Converter.StartBatch(Converter.CompatibleIn(c.SourcePath, c.Format), c.Format, c.SourcePath);
                    else Converter.Start(c.SourcePath, c.Format);
                    break;
                case CommandKind.Push: Toasts.StartPush(c.SourcePath); break;
                case CommandKind.Custom:
                    var sc = Shortcuts.Find(c.Format);
                    if (sc != null) Toasts.RunShortcut(sc, c.SourcePath);
                    break;
            }
        }

        public static bool ShouldSuppress => IsSnoozed || (Settings.Current.GameModeEnabled && Games.IsGaming);

        private void OnNetworkState(NetworkState state)
        {
            Tray.Refresh();
            if (_centre != null) _centre.RefreshStatus();

            if (state.Online)
            {
                _offlineDeferred = false;
                _dismissedCause = null;
                Activity.OutageEnded();
                PublicIp.CheckSoon();
                if (_offline != null) { _offline.ShowRestored(state); _offline = null; }
                return;
            }

            Activity.OutageStarted(state.Headline);
            if (_offline != null) { _offline.Update(state); return; }
            if (!Settings.Current.NetworkEnabled) return;
            if (_dismissedCause == state.Cause) return; // dismissed: stay quiet until the situation changes
            _dismissedCause = null;
            ShowOffline(state);
        }

        private void ShowOffline(NetworkState state)
        {
            if (ShouldSuppress) { _offlineDeferred = true; return; }
            _offlineDeferred = false;
            _offline = new OfflineWindow(state);
            _offline.Dismissed += () => { _offline = null; _dismissedCause = state.Cause; };
            _offline.Show();
        }

        public void OpenControlCentre(string section)
        {
            if (_centre == null)
            {
                _centre = new ControlCentreWindow();
                _centre.Closed += (s, e) => _centre = null;
                _centre.Show();
            }
            if (_centre.WindowState == WindowState.Minimized) _centre.WindowState = WindowState.Normal;
            _centre.Activate();
            if (section != null) _centre.Navigate(section);
        }

        public void ShowWelcome() => new WelcomeWindow().Show();

        public static new App Current => (App)Application.Current;

        public void Quit(int code)
        {
            try { Clip?.Dispose(); } catch { }
            try { Net?.Dispose(); } catch { }
            try { PublicIp?.Dispose(); } catch { }
            try { Tray?.Dispose(); } catch { }
            Log.Info("Agent exiting with " + code);
            Shutdown(code);
        }
    }
}
