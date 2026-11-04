using System;
using System.Linq;
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
        /// <summary>Set by "--selftest DIR": drive the real features in a throwaway folder, report, and exit.</summary>
        public static string SelfTestDir { get; set; }

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
            Appearance.Initialize();
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => { if (s is Window w) Appearance.ScaleFor(w); }));
            PersonalWording.LoadCache();
            WordingLayer.Install();
            Core.Language.Initialize();
            Games = new GameDetector();
            Toasts = new ToastManager();
            Modes.Initialize();
            Tray = new TrayIcon();
            Appearance.Changed += () => Tray?.Refresh();
            Net = new NetworkMonitor();
            PublicIp = new PublicIpMonitor();

            if (SelfTestDir != null)
            {
                Dispatcher.BeginInvoke(new Action(async () => await SelfTest.RunAsync(SelfTestDir)), DispatcherPriority.ApplicationIdle);
                return;
            }
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
            Minecraft.McScheduler.Start();   // runs the Minecraft servers' schedules from the background app
            AutoUpdater.Start();             // new releases download and install themselves at a quiet moment
            Tray.Refresh();

            ListenForSignal(ServiceHost.QuitEventName, () => Quit(ServiceHost.ExitQuit));
            ListenForSignal(ServiceHost.ShowEventName, () => OpenControlCentre(null));

            var updated = AutoUpdater.TakeUpdatedNote();
            if (updated != null) Toasts.ShowNotice("Monitoring Tools updated", updated, Toasts.Mint, 6);

            var flag = Path.Combine(Settings.DataDir, "welcome.flag");
            if (File.Exists(flag))
            {
                try { File.Delete(flag); } catch { }
                // First run: the setup wizard does the real setup; the tour stays one click away in the tray.
                if (!Settings.Current.OnboardingDone) ShowSetup(); else ShowWelcome();
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
            if (TryApplyCopiedWording(item)) return;
            if (!Modes.ClipboardToastsOn) return;

            if (item.Command != null && Settings.Current.QuickCommandsEnabled)
            {
                // Quick commands always run, even in game mode; only the toast is held back.
                RunCommand(item.Command);
                return;
            }
            if (ShouldSuppress) return;
            Toasts.ShowClip(item);
        }

        // A copied path to a valid personal wording file is applied at once, exactly like a drop: same validation,
        // same celebration and sound. Unrelated JSON files fall through to the ordinary clipboard toast.
        internal static bool TryApplyCopiedWording(ClipItem item)
        {
            if (!Settings.Current.ApplyCopiedWordingFile || item.Kind != ClipKind.Files || item.Files == null || item.Files.Length != 1) return false;
            var path = item.Files[0];
            if (!PersonalWording.IsWordingFile(path)) return false;
            if (PersonalWording.TryApply(path, out var message))
            {
                Sounds.Play(Cue.Tada);
                var over = Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible) ?? Current.MainWindow;
                if (over != null && over.IsVisible) UI.WordingLayer.Celebrate(over);
                else Toasts.ShowNotice("Personal wording applied", message, Toasts.Mint, 4, icon: Icons.Text);
            }
            else
            {
                Sounds.Play(Cue.Invalid);
                Toasts.ShowNotice("Wording file refused", message, Toasts.Amber, 6, icon: Icons.Text);
            }
            return true;
        }

        public static void RunCommand(QuickCommand c)
        {
            switch (c.Kind)
            {
                case CommandKind.Convert:
                    if (System.IO.Directory.Exists(c.SourcePath)) Converter.StartBatch(Converter.CompatibleIn(c.SourcePath, c.Format), c.Format, c.SourcePath);
                    else Converter.Start(c.SourcePath, c.Format);
                    break;
                case CommandKind.Push:
                    if (Modes.Blocks(ModeFeature.Push)) { Toasts.ShowNotice(Core.Language.Headline("mode.on", Modes.SchoolName), "Pushing to git is paused while it is on.", Toasts.Amber, 3, icon: Icons.Git); break; }
                    Toasts.StartPush(c.SourcePath); break;
                case CommandKind.Custom:
                    var sc = Shortcuts.Find(c.Format);
                    if (sc != null) Toasts.RunShortcut(sc, c.SourcePath);
                    break;
            }
        }

        public static bool ShouldSuppress => IsSnoozed || (Modes.GameModeOn && Games.IsGaming);

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
            NotificationLog.Add("Offline", Core.Language.NetHeadline(state.Headline ?? "Internet connection lost"), state.Cause.ToString());
            if (ShouldSuppress) { _offlineDeferred = true; return; }
            _offlineDeferred = false;
            _offline = new OfflineWindow(state);
            _offline.Dismissed += () => { _offline = null; _dismissedCause = state.Cause; };
            _offline.Show();
        }

        public void ShowSetup() => new SetupWizardWindow().Show();

        public void OpenControlCentre(string section, string highlight)
        {
            OpenControlCentre(section);
            if (highlight != null) _centre?.Teleport(highlight);
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
            try { Minecraft.ServerManager.StopAllLocal(); } catch { }
            try { Clip?.Dispose(); } catch { }
            try { Net?.Dispose(); } catch { }
            try { PublicIp?.Dispose(); } catch { }
            try { Tray?.Dispose(); } catch { }
            Log.Info("Agent exiting with " + code);
            Shutdown(code);
        }
    }
}
