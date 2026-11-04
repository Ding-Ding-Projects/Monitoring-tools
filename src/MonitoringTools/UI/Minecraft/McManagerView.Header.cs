using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI.Minecraft
{
    // The workspace header: name, state badge, a detail line and the actions that change with the state.
    internal sealed partial class McManagerView
    {
        private ContentControl _badgeHost;
        private TextBlock _detail, _status;
        private WrapPanel _actions;
        private McRunState _shownState = (McRunState)(-1);

        internal TextBlock StatusLine => _status;

        /// <summary>The address players type in their server list.</summary>
        internal static string JoinAddress(ServerDef s)
        {
            if (!s.IsSsh) return "localhost:" + s.Port;
            var host = ServerManager.SplitHost(s.SshHost).Host;
            if (host.Contains('@')) host = host[(host.IndexOf('@') + 1)..];
            return host + ":" + s.Port;
        }

        private FrameworkElement BuildHeader(ServerDef s)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left = new StackPanel();
            var nameRow = new WrapPanel();
            var name = McUi.Literal(s.Name, "B.Text", 28);
            name.FontFamily = Ui.Font("F.Display"); name.Margin = new Thickness(0, 0, 14, 4);
            AutomationProperties.SetHeadingLevel(name, AutomationHeadingLevel.Level2);
            nameRow.Children.Add(name);
            _badgeHost = new ContentControl { IsTabStop = false, VerticalAlignment = VerticalAlignment.Center };
            nameRow.Children.Add(_badgeHost);
            left.Children.Add(nameRow);
            _detail = McUi.Body("", "B.TextSoft", 13.5);
            left.Children.Add(_detail);
            _actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            left.Children.Add(_actions);
            _status = McUi.Body("", "B.TextMute", 12.5);
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
            _status.Margin = new Thickness(0, 2, 0, 0); _status.Visibility = Visibility.Collapsed;
            left.Children.Add(_status);
            g.Children.Add(left);
            _shownState = (McRunState)(-1);
            RefreshHeader();
            return g;
        }

        internal void SetStatus(string text, string fg)
        {
            if (_status == null) return;
            _status.Text = text ?? "";
            _status.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            _status.Foreground = Ui.Res(fg);
            Core.Language.Refresh(_status);
        }

        private void RefreshHeader()
        {
            var s = Current;
            if (s == null || _badgeHost == null || Creating) return;
            var st = McState.Cached(s);
            var live = McLive.For(s);
            string where = s.Target switch
            {
                Target.LocalProcess => "on this computer",
                Target.LocalDocker => "in Docker on this computer",
                Target.SshProcess => "on " + s.SshHost + " over SSH",
                _ => "in Docker on " + s.SshHost,
            };
            string state = st.State switch
            {
                McRunState.Running when s.Adopted && st.Detail.Contains("outside this app") => st.Detail,
                McRunState.Running when live.StartedAt is DateTime t => "started " + McUi.Span(DateTime.Now - t) + " ago",
                McRunState.HostOffline => "the host cannot be reached, so its state is unknown",
                McRunState.Missing => st.Detail,
                _ => McState.Label(st.State).ToLowerInvariant(),
            };
            _detail.Text = $"{s.Flavour} {s.Version}{(string.IsNullOrEmpty(s.Build) ? "" : " build " + s.Build)} · {where} · {state}";
            Core.Language.Refresh(_detail);
            if (st.State == _shownState) return;
            _shownState = st.State;
            KeepFocus(_actions, () => PaintActions(s, st));
        }

        private void PaintActions(ServerDef s, McStatus st)
        {
            _badgeHost.Content = McUi.StateBadge(st.State, true);
            Core.Language.Refresh(_badgeHost);
            _actions.Children.Clear();
            var copy = Ui.Button(JoinAddress(s), () =>
            {
                try { Clipboard.SetText(JoinAddress(s)); SetStatus("Copied the join address.", "B.Mint"); } catch (Exception ex) { SetStatus(ex.Message, "B.Amber"); }
            }, "Btn.Ghost", Icons.Clipboard);
            Core.Language.SetLiteral(copy, true);
            AutomationProperties.SetName(copy, "Copy join address " + JoinAddress(s));
            copy.ToolTip = "Copy join address";
            _actions.Children.Add(copy);
            bool running = st.State == McRunState.Running || st.State == McRunState.Starting;
            if (st.State == McRunState.HostOffline)
                _actions.Children.Add(Ui.Button("Reconnect", () => _ = Act(s, "Reconnect", async () =>
                {
                    var now = await McState.ProbeAsync(s);
                    if (now.State == McRunState.HostOffline) throw new InvalidOperationException(now.Detail);
                }), "Btn.Accent", Icons.Restart));
            else
            {
                _actions.Children.Add(Ui.Button("Restart", () => _ = Act(s, "Restart", async () => { if (running) await ServerManager.StopAsync(s); await ServerManager.StartAsync(s, t => Dispatcher.BeginInvoke(new Action(() => SetStatus(t, "B.TextMute")))); }), "Btn.Ghost", Icons.Restart));
                if (running) _actions.Children.Add(Ui.Button("Stop", () => _ = Act(s, "Stop", async () => { await ServerManager.StopAsync(s); McLive.For(s).Stopped(); }), "Btn.Accent", Icons.Pause));
                else _actions.Children.Add(Ui.Button("Start", () => _ = Act(s, "Start", () => ServerManager.StartAsync(s, t => Dispatcher.BeginInvoke(new Action(() => SetStatus(t, "B.TextMute"))))), "Btn.Accent", Icons.Play));
            }
            if (!s.IsBedrock) _actions.Children.Add(Ui.Button("Commands", () => ShowSection("commands"), "Btn.Ghost", Icons.Bolt));
            if (s.IsLocal) _actions.Children.Add(Ui.Button("Open folder", () => ActionProvider.Shell(s.Folder), "Btn.Ghost", Icons.Folder));
            var remove = McUi.IconButton(Icons.Trash, "Remove from the list", () =>
            {
                var dlg = new ConfirmWindow($"Remove {s.Name} from the list?", "The server is stopped first if it runs. Its world, settings and backups stay in their folders; only the entry in this list goes.", "Remove", danger: true);
                dlg.Confirmed += async _ =>
                {
                    try { if (McState.Cached(s).State == McRunState.Running) await ServerManager.StopAsync(s); } catch { }
                    ServerManager.Servers.Remove(s);
                    ServerManager.Save();
                    SelectedId = null;
                    RebuildList(); BuildWorkspace();
                };
                dlg.Show();
            });
            remove.ToolTip = "Remove from the list. The world and files stay."; remove.VerticalAlignment = VerticalAlignment.Top; remove.Margin = new Thickness(0, 0, 8, 8);
            _actions.Children.Add(remove);
            Core.Language.Refresh(_actions);
        }
    }
}
