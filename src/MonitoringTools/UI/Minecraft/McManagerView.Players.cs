using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI.Minecraft
{
    // Players: everyone the server knows, with operator and whitelist switches, a menu per player and add by name.
    internal sealed partial class McManagerView
    {
        internal static string PlayerFilter = "all";

        private bool IsUp(ServerDef s) { var st = McState.Cached(s).State; return st == McRunState.Running || st == McRunState.Starting; }

        private FrameworkElement BuildPlayers(ServerDef s)
        {
            var root = new StackPanel();
            if (s.IsBedrock) return BuildBedrockPlayers(s);
            var card = new StackPanel();
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(McUi.Title("Players"));
            var filters = new WrapPanel();
            AutomationProperties.SetName(filters, "Which players");
            Grid.SetColumn(filters, 1); head.Children.Add(filters);
            card.Children.Add(head);
            var how = McUi.Body("", "B.TextMute", 12);
            how.Margin = new Thickness(0, 4, 0, 12);
            card.Children.Add(how);

            var addRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var nameBox = McUi.Field("", "Add a player by name", 220, "A Minecraft name: 1 to 16 letters, digits or underscores.");
            var nameCol = new StackPanel(); nameCol.Children.Add(new TextBlock { Text = "Add a player by name", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) }); nameCol.Children.Add(nameBox); addRow.Children.Add(nameCol);
            var result = McUi.Body("", "B.TextMute", 12);
            AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);

            PlayerBox box = null;
            async Task Apply(McPlayers.Change c, string name, string reason = null)
            {
                result.Text = "Working…"; result.Foreground = Ui.Res("B.TextMute");
                try
                {
                    var r = McDemo.IsSample(s) && IsUp(s) ? "Sample server: the command would be /" + McPlayers.Command(c, name, reason) : await McPlayers.ApplyAsync(s, IsUp(s), c, name, reason);
                    result.Text = r; result.Foreground = Ui.Res("B.TextSoft");
                    if (McDemo.IsSample(s) && IsUp(s) && s.IsLocal) McPlayers.ApplyToFiles(s.Folder, c, name, reason);   // keep the sample's files in step so the switches show the change
                }
                catch (Exception ex) { result.Text = ex.Message; result.Foreground = Ui.Res("B.Amber"); }
                Core.Language.Refresh(result);
                await Task.Delay(IsUp(s) && !McDemo.IsSample(s) ? 900 : 10);   // a running server saves the files a moment after the command
                if (s.IsLocal) Fill(); else await LoadRemote();
            }

            // A server on an SSH host: its player files are read over SSH, in one round trip, and kept until something changes them.
            Dictionary<string, string> remoteTexts = null; string remoteError = null;
            async Task LoadRemote()
            {
                try { var files = McFiles.For(s); remoteTexts = await Task.Run(() => files.ReadMany(new[] { "ops.json", "whitelist.json", "banned-players.json", "usercache.json" })); remoteError = null; }
                catch (Exception ex) { remoteError = ex.Message; }
                KeepFocus(card, Fill);
            }
            _playerApply = Apply;

            box = MakePlayerBox(s, "minecraft:players", p => PlayerTableRow(s, p, Apply), () => { if (s.IsLocal) Fill(); else _ = LoadRemote(); });
            addRow.Children.Add(Ui.Button("Whitelist", () => _ = Apply(McPlayers.Change.WhitelistAdd, nameBox.Text.Trim()), "Btn.Ghost", Icons.Plus));
            addRow.Children.Add(Ui.Button("Make operator…", () => ConfirmOp(s, nameBox.Text.Trim(), true, ok => { if (ok) _ = Apply(McPlayers.Change.Op, nameBox.Text.Trim()); }), "Btn.Ghost", Icons.Shield));
            card.Children.Add(addRow);
            card.Children.Add(result);
            card.Children.Add(box.Root);
            root.Children.Add(McUi.Card(card));

            void Fill()
            {
                bool up = IsUp(s);
                how.Text = !s.IsLocal && !up
                    ? "The server is stopped, so changes are written to ops.json, whitelist.json and banned-players.json on the SSH host, over SSH. Bans and operator changes ask for the two-key confirmation."
                    : up ? "The server is running, so changes go through its console and it saves them itself. Bans and operator changes ask for the two-key confirmation."
                         : "The server is stopped, so changes are written to ops.json, whitelist.json and banned-players.json. Bans and operator changes ask for the two-key confirmation.";
                var players = s.IsLocal ? McPlayers.Read(s.Folder, up ? McLive.For(s).PlayerNames : null)
                    : remoteTexts != null ? McPlayers.Build(remoteTexts, up ? McLive.For(s).PlayerNames : null)
                    : (up ? McLive.For(s).PlayerNames.Select(n => new McPlayer { Name = n, Online = true }).ToList() : new List<McPlayer>());
                filters.Children.Clear();
                foreach (var (id, label, count) in new[] { ("all", "Everyone", players.Count), ("online", "Online", players.Count(p => p.Online)), ("ops", "Operators", players.Count(p => p.Operator)), ("banned", "Banned", players.Count(p => p.Banned)) })
                {
                    var fid = id;
                    filters.Children.Add(McUi.Chip($"{label} {count}", PlayerFilter == id, () => { PlayerFilter = fid; Fill(); }, $"{label}: {count}"));
                }
                var shown = players.Where(p => PlayerFilter switch { "online" => p.Online, "ops" => p.Operator, "banned" => p.Banned, _ => true }).ToList();
                string note = null;
                if (!s.IsLocal && remoteError != null) note = remoteError;
                else if (!s.IsLocal && remoteTexts == null) note = "Reading the player files on the SSH host…";
                else if (shown.Count == 0) note = players.Count == 0 ? "No players yet. Add one by name, or they appear once they join." : "No players match this filter.";
                box.Show(shown, note);
                Core.Language.Refresh(card);
            }
            // Rebuilt only when the players or the files changed, so a switch keeps keyboard focus between polls.
            string last = null;
            string Signature()
            {
                var files = s.IsLocal ? string.Join("|", new[] { "ops.json", "whitelist.json", "banned-players.json" }.Select(f => System.IO.File.GetLastWriteTimeUtc(System.IO.Path.Combine(s.Folder, f)).Ticks)) : "";
                return IsUp(s) + "|" + string.Join(",", McLive.For(s).PlayerNames) + "|" + files;
            }
            _tick = () => { var now = Signature(); if (now != last) { last = now; KeepFocus(card, Fill); } };
            last = Signature();
            Fill();
            if (!s.IsLocal) _ = LoadRemote();
            return root;
        }

        private Func<McPlayers.Change, string, string, Task> _playerApply;

        /// <summary>The two-key confirmation for operator changes; <paramref name="done"/> gets true only when it was confirmed.</summary>
        private static void ConfirmOp(ServerDef s, string name, bool make, Action<bool> done)
        {
            if (!(s.IsBedrock ? McBedrockPlayers.ValidName(name) : McPlayers.ValidName(name))) { done(false); return; }
            bool ok = false;
            var dlg = new ConfirmWindow(make ? $"Make {name} an operator?" : $"Remove {name} as an operator?",
                make ? "An operator can run every command on this server, including stopping it and changing other players." : "They lose access to operator commands straight away.",
                make ? "Make operator" : "Remove operator", danger: true);
            dlg.Confirmed += _ => { ok = true; done(true); };
            dlg.Closed += (o, e) => { if (!ok) done(false); };
            dlg.Show();
        }

        private static void ConfirmBan(string name, Action<bool> done)
        {
            bool ok = false;
            var dlg = new ConfirmWindow($"Ban {name}?", "They are disconnected and cannot join again until they are pardoned.", "Ban", danger: true);
            dlg.Confirmed += _ => { ok = true; done(true); };
            dlg.Closed += (o, e) => { if (!ok) done(false); };
            dlg.Show();
        }

        private FrameworkElement PlayerTableRow(ServerDef s, McPlayer p, Func<McPlayers.Change, string, string, Task> apply)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            var top = PlayerRow(s, p.Name, p.Banned ? "banned" + (p.BanReason.Length > 0 ? ": " + p.BanReason : "") : p.Online ? "online" : "offline", p.Online, p, apply);
            sp.Children.Add(top);
            var sw = new WrapPanel { Margin = new Thickness(48, 0, 0, 0) };
            bool guard = false;
            CheckBox op = null, wl = null;
            op = McUi.Switch("Operator", "Can run every command.", p.Operator, v =>
            {
                if (guard) return;
                ConfirmOp(s, p.Name, v, ok =>
                {
                    if (ok) _ = apply(v ? McPlayers.Change.Op : McPlayers.Change.Deop, p.Name, null);
                    else { guard = true; op.IsChecked = !v; guard = false; }
                });
            });
            op.Width = 230; op.Margin = new Thickness(0, 0, 24, 4);
            AutomationProperties.SetName(op, "Operator: " + p.Name);
            wl = McUi.Switch("Whitelist", "Can join while the whitelist is on.", p.Whitelisted, v => { if (!guard) _ = apply(v ? McPlayers.Change.WhitelistAdd : McPlayers.Change.WhitelistRemove, p.Name, null); });
            wl.Width = 250; wl.Margin = new Thickness(0, 0, 0, 4);
            AutomationProperties.SetName(wl, "Whitelist: " + p.Name);
            sw.Children.Add(op); sw.Children.Add(wl);
            sp.Children.Add(sw);
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 6, 0, 2), Child = sp };
        }

        /// <summary>A player's initial, name, a note and the menu with kick, ban, pardon, teleport and game mode.</summary>
        private FrameworkElement PlayerRow(ServerDef s, string name, string note, bool online, McPlayer p = null, Func<McPlayers.Change, string, string, Task> apply = null)
        {
            apply ??= _playerApply;
            var g = new Grid { MinHeight = 48 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var init = McUi.Literal(name.Length > 0 ? name[..1].ToUpperInvariant() : "?", "B.OnTertiaryContainer", 14);
            init.FontWeight = FontWeights.SemiBold; init.HorizontalAlignment = HorizontalAlignment.Center; init.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.TertiaryContainer"), Child = init, Margin = new Thickness(0, 0, 12, 0) });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(McUi.Literal(name, "B.Text", 14));
            text.Children.Add(new TextBlock { Text = note, FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(text, 1); g.Children.Add(text);
            Button more = null;
            more = McUi.IconButton("M12 5h.01M12 12h.01M12 19h.01", "More actions for " + name, () =>
            {
                bool up = IsUp(s);
                var items = new List<ElementMenu.Item>();
                if (up && online) items.Add(new ElementMenu.Item("Kick", Icons.Close, null, () => _ = apply(McPlayers.Change.Kick, name, null)));
                if (p?.Banned == true) items.Add(new ElementMenu.Item("Pardon", Icons.Check, null, () => _ = apply(McPlayers.Change.Pardon, name, null)));
                else items.Add(new ElementMenu.Item("Ban…", Icons.Shield, null, () => ConfirmBan(name, ok => { if (ok) _ = apply(McPlayers.Change.Ban, name, null); }), true));
                if (up && online)
                {
                    foreach (var mode in new[] { "survival", "creative", "adventure", "spectator" })
                    {
                        var m = mode;
                        items.Add(new ElementMenu.Item("Game mode: " + mode, Icons.Game, null, () => _ = SendLine(s, $"gamemode {m} {name}")));
                    }
                }
                var w = Window.GetWindow(more);
                if (w != null) ElementMenu.OpenItems(w, more, items, more.TranslatePoint(new Point(0, more.ActualHeight), w));
            });
            Grid.SetColumn(more, 2); g.Children.Add(more);
            return g;
        }

        /// <summary>Sends one console line made by the app (never free text) and shows the answer in the header line.</summary>
        internal async Task SendLine(ServerDef s, string line)
        {
            if (McDemo.IsSample(s)) { SetStatus("Sample server: the command would be /" + line, "B.TextMute"); return; }
            var r = await ServerManager.CommandAsync(s, line);
            SetStatus("/" + line + " · " + r, "B.TextSoft");
        }
    }
}
