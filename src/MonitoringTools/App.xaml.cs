using System;
using System.Collections.Generic;
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
        /// <summary>Extensions found in their folder. Null during self-tests and captures, which never load your extensions.</summary>
        public static global::MonitoringTools.Clip.Smart.ExtensionHost Extensions { get; private set; }
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
            UI.ElevatedDrop.Install();   // file drops from File Explorer while running as administrator
            Core.Language.Initialize();
            Games = new GameDetector();
            Toasts = new ToastManager();
            global::MonitoringTools.Clip.Smart.SmartDetect.GroupOn = g => !Settings.Current.SmartGroupsOff.Contains(g);
            global::MonitoringTools.Clip.Smart.SmartDetect.HomeCurrency = Settings.Current.HomeCurrency;
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
            LoadExtensions();
            try { UI.ClipPower.Start(); } catch (Exception ex) { Log.Warn("Clipboard shortcuts not started: " + ex.Message); }
            Clip = new ClipboardWatcher();

            Clip.Changed += OnClipboard;
            Net.StateChanged += OnNetworkState;
            PublicIp.Changed += (prev, cur) => { if (!ShouldSuppress && Settings.Current.PublicIpEnabled) Toasts.ShowIpChanged(prev, cur); };
            Games.GamingChanged += gaming =>
            {
                Tray.Refresh();
                ShowGameNotice(gaming);
                if (!gaming && _offlineDeferred && Net.Current != null && !Net.Current.Online) ShowOffline(Net.Current);
            };

            Games.Start();
            Clip.Start();
            Net.Start();
            PublicIp.Start();
            Minecraft.McScheduler.Start();   // runs the Minecraft servers' schedules from the background app
            AutoUpdater.Start();             // new releases download and install themselves at a quiet moment
            Tray.Refresh();

            ListenForSignal(ServiceHost.QuitEventName, () => Quit(ServiceHost.ExitQuit), once: true);
            ListenForSignal(ServiceHost.ShowEventName, () => OpenControlCentre(null));

            var updated = AutoUpdater.TakeUpdatedNote();
            if (updated != null) Toasts.ShowNotice("Monitoring Tools updated", updated, Toasts.Mint, 6);

            // First install: the setup wizard does the real setup and the tour stays one click away in the tray. A reinstall
            // or the same version started again just opens the control centre. Updates never bring the tour back.
            if (InstallNote.Take(Settings.DataDir, InstallNote.WelcomeFlag))
            {
                if (!Settings.Current.OnboardingDone) ShowSetup(); else ShowWelcome();
            }
            else if (InstallNote.Take(Settings.DataDir, InstallNote.OpenFlag))
            {
                if (!Settings.Current.OnboardingDone) ShowSetup(); else OpenControlCentre(null);
            }
        }

        private string _lastGame;

        /// <summary>"Roblox detected" when a game starts and "Roblox closed" when it stops, so it is clear why toasts
        /// are being held back. Toasts never take focus, so the game keeps it.</summary>
        internal ToastWindow ShowGameNotice(bool gaming)
        {
            if (gaming) _lastGame = Games.ActiveGame ?? "A game";
            if (!Settings.Current.GameDetectedNotice || !Modes.GameModeOn || IsSnoozed) return null;
            var actions = new System.Collections.Generic.List<ToastAction>
            {
                new ToastAction { Label = "Game mode settings", Icon = Icons.Game, Run = () => OpenControlCentre("game"), HandsOff = true },
                new ToastAction { Label = "Stop these notices", Icon = Icons.Close, Run = () => { Settings.Current.GameDetectedNotice = false; Settings.Save(); }, Done = "Off" },
            };
            return gaming
                ? Toasts.ShowNotice(_lastGame + " detected", "Game mode is on. Toasts and alerts wait until you stop playing; quick commands still run.", Toasts.Violet, 4, actions, Icons.Game)
                : Toasts.ShowNotice((_lastGame ?? "The game") + " closed", "Game mode is off. Anything held back shows now.", Toasts.Mint, 3, null, Icons.Game);
        }

        /// <param name="once">For the quit signal: the supervisor creates that event as manual-reset, so it stays set
        /// once raised and a loop would call Quit hundreds of times while the app shuts down.</param>
        private void ListenForSignal(string name, Action onSignal, bool once = false)
        {
            var handle = new EventWaitHandle(false, EventResetMode.AutoReset, name);
            var t = new Thread(() =>
            {
                do
                {
                    handle.WaitOne();
                    Dispatcher.BeginInvoke(onSignal);
                } while (!once);
            }) { IsBackground = true, Name = "signal:" + name };
            t.Start();
        }

        internal static void LoadExtensions()
        {
            try
            {
                Extensions ??= new global::MonitoringTools.Clip.Smart.ExtensionHost(global::MonitoringTools.Clip.Smart.ExtensionHost.DefaultRoot);
                try { Extensions.SyncDefaults(BundledExtensions()); } catch (Exception ex) { Log.Warn("Built-in extensions not installed: " + ex.Message); }
                Extensions.Load();
                global::MonitoringTools.Clip.Smart.SmartDetect.Extensions = t => Settings.Current.ExtensionsEnabled ? Extensions.Detect(t) : Enumerable.Empty<global::MonitoringTools.Clip.Smart.SmartFact>();
                Log.Info($"Extensions: {Extensions.Items.Count(i => i.Enabled)} on of {Extensions.Items.Count}");
            }
            catch (Exception ex) { Log.Warn("Extensions not loaded: " + ex.Message); }
        }

        /// <summary>The built-in extensions embedded in the app, read from their resources.</summary>
        internal static List<global::MonitoringTools.Clip.Smart.BundledExtension> BundledExtensions()
        {
            var asm = typeof(App).Assembly;
            var byId = new Dictionary<string, global::MonitoringTools.Clip.Smart.BundledExtension>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("defaultext/")))
            {
                var rel = name.Substring("defaultext/".Length).Replace('\\', '/');
                int cut = rel.IndexOf('/');
                if (cut <= 0) continue;
                var id = rel.Substring(0, cut);
                if (!byId.TryGetValue(id, out var b)) byId[id] = b = new global::MonitoringTools.Clip.Smart.BundledExtension { Id = id };
                using var st = asm.GetManifestResourceStream(name);
                using var ms = new MemoryStream(); st.CopyTo(ms);
                b.Files[rel.Substring(cut + 1)] = ms.ToArray();
            }
            return byId.Values.ToList();
        }

        // The secret guard: a copied key, token or bank number is never kept in history, and it leaves the clipboard
        // after a short while unless something else was copied since.
        private static void GuardSecret(ClipItem item)
        {
            var text = item.Text;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Clamp(Settings.Current.SecretClearSeconds, 5, 600)) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                try { if (Clipboard.ContainsText() && Clipboard.GetText() == text) UI.SmartActions.ClearClipboard(); } catch { }
            };
            t.Start();
        }

        private void OnClipboard(ClipItem item)
        {
            var rule = UI.ClipPower.ApplyRule(item);
            if (rule == "never") return;
            if (UI.ClipPower.Take(item)) { if (!item.Sensitive) History.Add(item); return; }
            if (item.Sensitive && Settings.Current.SecretGuard) GuardSecret(item);
            else History.Add(item);
            if (rule == "quiet") return;
            if (TryApplyCopiedWording(item)) return;
            if (!Modes.ClipboardToastsOn) return;

            if (item.Command != null && Settings.Current.QuickCommandsEnabled)
            {
                // Quick commands always run, even in game mode; only the toast is held back.
                RunCommand(item.Command);
                return;
            }
            if (ShouldSuppress) return;
            if (item.Kind == ClipKind.Install && UI.InstallToast.AutoEligible(item, out _) && Toasts.ShowAutoInstall(item) != null) return;
            Toasts.ShowClip(item);
        }

        // A copied path to a valid personal wording file is applied at once, exactly like a drop: same validation,
        // same celebration and sound. Unrelated JSON files fall through to the ordinary clipboard toast.
        internal static bool TryApplyCopiedWording(ClipItem item)
        {
            if (!Settings.Current.ApplyCopiedWordingFile) return false;
            // the file itself (Ctrl+C in File Explorer) or its path as text ("Copy as path" adds quotes; a file:/// address works too)
            // (a file:/// address is read as a quick command first, with an empty file list, so the text is always tried too)
            string path = item.Kind == ClipKind.Files && item.Files != null && item.Files.Length == 1 ? item.Files[0]
                        : item.Text != null ? PersonalWording.PathFromText(item.Text) : null;
            if (path == null) return false;
            // a JSON file that is not meant as wording stays an ordinary copy; one that is meant as wording but fails says why
            if (!PersonalWording.IsWordingFile(path) && !PersonalWording.LooksLikeWordingFile(path)) return false;
            if (PersonalWording.TryApply(path, out var message))
            {
                Sounds.Play(Cue.Tada);
                // The celebration covers the whole screen and never takes focus, so it needs no window of ours: a path is
                // usually copied in Explorer, when none of this app's windows is open.
                var over = Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible);
                UI.WordingLayer.Celebrate(over);
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

        /// <summary>Opens the Virtual disks page, adding disk files to its library or planning a Quick pack of the items.</summary>
        public void OpenVirtualDisks(string[] disks = null, string[] pack = null)
        {
            OpenControlCentre(null);
            if (disks != null && disks.Length > 0) _centre.VdAdd(disks);
            _centre.Navigate("vdisks");
            if (pack != null && pack.Length > 0) _centre.VdStartPack(pack);
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
