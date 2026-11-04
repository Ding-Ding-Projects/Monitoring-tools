using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI.Minecraft
{
    /// <summary>
    /// The Minecraft server manager: the server list on the left and the chosen server's workspace on the right, with its
    /// own sections. At narrow widths the list stacks above the workspace. Which server, section and create step are open
    /// is kept across page rebuilds, so a theme change or a language switch returns to the same place.
    /// </summary>
    internal sealed partial class McManagerView : Grid
    {
        internal static string SelectedId;
        internal static string Section = "overview";
        internal static bool Creating;
        private static int _lastSection;

        internal static readonly (string Id, string Label, string Icon)[] Sections =
        {
            ("overview", "Overview", Icons.Pulse), ("console", "Console", Icons.Code), ("commands", "Commands", Icons.Bolt), ("players", "Players", Icons.Lines),
            ("settings", "Settings", Icons.Wand), ("worlds", "Worlds", Icons.Globe), ("content", "Plugins", Icons.Box),
            ("backups", "Backups", Icons.Archive), ("schedules", "Schedules", Icons.Clock),
        };

        private readonly ControlCentreWindow _owner;
        private readonly Border _listPane;
        private readonly ContentControl _workspace = new ContentControl { IsTabStop = false };
        private readonly DispatcherTimer _timer;
        private Action _tick;
        private bool _stacked;
        private int _ticks;
        private bool _pendingRefresh;

        internal ControlCentreWindow Owner => _owner;
        internal FrameworkElement SectionBody { get; private set; }
        internal static ServerDef Current => ServerManager.Servers.FirstOrDefault(s => s.Id == SelectedId) ?? ServerManager.Servers.FirstOrDefault();

        public McManagerView(ControlCentreWindow owner)
        {
            _owner = owner;
            AutomationProperties.SetName(this, "Minecraft server manager");
            _listPane = new Border { Child = BuildList() };
            Children.Add(_listPane);
            Children.Add(_workspace);
            Arrange(false);
            BuildWorkspace();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _timer.Tick += async (s, e) => await PollAsync();
            Loaded += (s, e) =>
            {
                McState.Changed += OnStateChanged;
                McLive.Updated += OnLiveUpdated;
                ServerManager.Changed += OnServersChanged;
                _timer.Start();
                _ = ProbeAllAsync();
            };
            Unloaded += (s, e) =>
            {
                McState.Changed -= OnStateChanged;
                McLive.Updated -= OnLiveUpdated;
                ServerManager.Changed -= OnServersChanged;
                _timer.Stop();
            };
            SizeChanged += (s, e) => { bool narrow = e.NewSize.Width < 820; if (narrow != _stacked) Arrange(narrow); };
        }

        /// <summary>Two columns when there is room; the list above the workspace when there is not.</summary>
        private void Arrange(bool stacked)
        {
            _stacked = stacked;
            ColumnDefinitions.Clear(); RowDefinitions.Clear();
            if (stacked)
            {
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                SetColumn(_listPane, 0); SetRow(_listPane, 0); SetColumn(_workspace, 0); SetRow(_workspace, 1);
                _listPane.Margin = new Thickness(0, 0, 0, 16);
            }
            else
            {
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                SetColumn(_listPane, 0); SetRow(_listPane, 0); SetColumn(_workspace, 1); SetRow(_workspace, 0);
                _listPane.Margin = new Thickness(0, 0, 20, 0);
            }
        }

        // -------------------------------------------------------------- navigation

        internal void Select(string id)
        {
            SelectedId = id;
            Creating = false;
            RepaintList();
            BuildWorkspace();
            _ = PollAsync();
        }

        internal void ShowSection(string id)
        {
            Section = id;
            BuildWorkspace();
        }

        internal void OpenCreate()
        {
            Creating = true;
            RepaintList();
            BuildWorkspace();
        }

        internal void CloseCreate(string selectId = null)
        {
            Creating = false;
            if (selectId != null) SelectedId = selectId;
            RebuildList();
            BuildWorkspace();
        }

        /// <summary>Rebuilds the right side for the current server and section.</summary>
        internal void BuildWorkspace()
        {
            _tick = null;
            FrameworkElement content;
            var s = Current;
            if (Creating) content = BuildCreate();
            else if (s == null) content = BuildEmpty();
            else
            {
                SelectedId = s.Id;
                var sp = new StackPanel();
                sp.Children.Add(BuildHeader(s));
                sp.Children.Add(BuildTabs(s));
                var body = Section switch
                {
                    "console" => BuildConsole(s),
                    "commands" => BuildCommands(s),
                    "players" => BuildPlayers(s),
                    "settings" => BuildSettings(s),
                    "worlds" => BuildWorlds(s),
                    "content" => BuildContent(s),
                    "backups" => BuildBackups(s),
                    "schedules" => BuildSchedules(s),
                    _ => BuildOverview(s),
                };
                sp.Children.Add(body);
                SectionBody = body;
                int at = Array.FindIndex(Sections, x => x.Id == Section);
                Ui.SharedAxis(body, Math.Sign(at - _lastSection));
                _lastSection = at;
                content = sp;
            }
            _workspace.Content = content;
            // The create flow uses the whole width, as its own page does in the design; the list returns when it closes.
            _listPane.Visibility = Creating ? Visibility.Collapsed : Visibility.Visible;
            if (!_stacked && ColumnDefinitions.Count == 2) ColumnDefinitions[0].Width = Creating ? new GridLength(0) : new GridLength(300);
        }

        private FrameworkElement BuildEmpty()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            sp.Children.Add(McUi.Title("No servers yet", 24));
            sp.Children.Add(McUi.Body("Create a Java edition server on this computer, in Docker, or on another machine over SSH. The guide asks one question at a time and downloads nothing until the last step.", "B.TextSoft", 14));
            var b = Ui.Button("New server", OpenCreate, "Btn.Accent", Icons.Plus);
            b.Margin = new Thickness(0, 16, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(b);
            return McUi.Card(sp, 28);
        }

        private FrameworkElement BuildTabs(ServerDef s)
        {
            var bar = new WrapPanel { Margin = new Thickness(0, 4, 0, 16) };
            AutomationProperties.SetName(bar, "Server sections");
            var buttons = new List<Button>();
            foreach (var (id, label0, icon) in Sections)
            {
                var label = id == "content" ? (s.ContentFolder == "mods" ? "Mods" : "Plugins") : label0;
                bool on = Section == id;
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new ContentControl { Content = Ui.Icon(icon, Ui.Res(on ? "B.Mint" : "B.TextMute"), 15), Margin = new Thickness(0, 0, 8, 0), IsTabStop = false, VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
                var stack = new StackPanel();
                stack.Children.Add(sp);
                stack.Children.Add(new Border { Height = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 8, 0, 0), Background = on ? Ui.Res("B.Mint") : System.Windows.Media.Brushes.Transparent });
                var b = new Button { Style = Ui.Style("Btn.Row"), Content = stack, Height = double.NaN, Padding = new Thickness(14, 10, 14, 0), Foreground = Ui.Res(on ? "B.Mint" : "B.TextSoft"), Margin = new Thickness(0, 0, 2, 0) };
                AutomationProperties.SetName(b, label);
                AutomationProperties.SetItemStatus(b, on ? "current section" : "");
                var sid = id;
                b.Click += (o, e) => ShowSection(sid);
                b.PreviewKeyDown += (o, e) =>
                {
                    int i = buttons.IndexOf(b);
                    if (e.Key == Key.Right && i < buttons.Count - 1) { buttons[i + 1].Focus(); e.Handled = true; }
                    else if (e.Key == Key.Left && i > 0) { buttons[i - 1].Focus(); e.Handled = true; }
                };
                buttons.Add(b);
                bar.Children.Add(b);
            }
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 0, 16), Child = bar };
        }

        // -------------------------------------------------------------- live updates

        private void OnStateChanged(string id) => Later();
        private void OnLiveUpdated(string id) { if (id == SelectedId) Later(); }
        private void OnServersChanged() => Dispatcher.BeginInvoke(new Action(() => { RebuildList(); }));

        /// <summary>Coalesces updates from other threads into one repaint on the UI thread.</summary>
        private void Later()
        {
            if (_pendingRefresh) return;
            _pendingRefresh = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _pendingRefresh = false;
                RepaintList();
                RefreshHeader();
                _tick?.Invoke();
            }), DispatcherPriority.Background);
        }

        private bool _polling;

        private async Task PollAsync()
        {
            if (_polling || Creating) return;
            _polling = true;
            try
            {
                var s = Current;
                if (s == null) return;
                if (!McDemo.IsSample(s))
                {
                    var st = await McState.ProbeAsync(s);
                    var live = McLive.For(s);
                    if (st.State == McRunState.Running || st.State == McRunState.Starting) await live.PollAsync(s);
                    else if (live.Lines().Count == 0) { var tail = await ServerManager.LogTailAsync(s, 200); if (!string.IsNullOrWhiteSpace(tail)) live.ReplaceWith(tail); }
                }
                if (++_ticks % 8 == 0) await ProbeAllAsync();
            }
            catch (Exception ex) { Log.Error("Minecraft poll", ex); }
            finally { _polling = false; Later(); }
        }

        private async Task ProbeAllAsync()
        {
            foreach (var s in ServerManager.Servers.ToList())
                if (s.Id != SelectedId && !McDemo.IsSample(s)) { try { await McState.ProbeAsync(s); } catch { } }
        }

        /// <summary>Runs a server action, shows its outcome in the header line and refreshes the state.</summary>
        internal async Task Act(ServerDef s, string what, Func<Task> act)
        {
            SetStatus(what + "…", "B.TextMute");
            try
            {
                await act();
                SetStatus(what + ": done.", "B.Mint");
                Sounds.Play(Cue.Success);
            }
            catch (Exception ex)
            {
                if (what == "Start" || what == "Restart") McLive.For(s).Failed(ex.Message);
                SetStatus(ex.Message, "B.Amber");
                Sounds.Play(Cue.Warning);
            }
            if (!McDemo.IsSample(s)) await McState.ProbeAsync(s);
            Later();
        }
    }
}
