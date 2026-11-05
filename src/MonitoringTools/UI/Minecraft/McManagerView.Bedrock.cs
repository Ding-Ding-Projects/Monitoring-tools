using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI.Minecraft
{
    // Bedrock servers: players (allowlist.json and permissions.json), add-ons, worlds; and the world folders of Java servers on an SSH host.
    // Every file is read and written through McFiles, so the same section serves this computer, Docker and an SSH host.
    internal sealed partial class McManagerView
    {
        // ---------------------------------------------------------------- players

        private FrameworkElement BuildBedrockPlayers(ServerDef s)
        {
            var root = new StackPanel();
            var card = new StackPanel();
            card.Children.Add(McUi.Title("Players"));
            var how = McUi.Body("", "B.TextMute", 12.5);
            how.Margin = new Thickness(0, 4, 0, 12);
            card.Children.Add(how);

            var addRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var nameBox = McUi.Field("", "Add a player by name", 220, "A Bedrock name: 1 to 32 letters, digits, spaces and . _ # -");
            var xuidBox = McUi.Field("", "Xbox user id (optional)", 220, "Needed to make a player an operator while the server is stopped, until the server has learned it. 6 to 20 digits.");
            StackPanel Labelled(string label, UIElement box) { var c = new StackPanel { Margin = new Thickness(0, 0, 12, 4) }; c.Children.Add(new TextBlock { Text = label, FontSize = 12.5, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) }); c.Children.Add(box); return c; }
            addRow.Children.Add(Labelled("Add a player by name", nameBox));
            addRow.Children.Add(Labelled("Xbox user id (optional)", xuidBox));
            var result = McUi.Body("", "B.TextMute", 12.5);
            AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);
            PlayerBox box = null;
            List<McPlayer> loaded = null; string error = null; bool allowOn = true;
            void Say(string text, string fg) { result.Text = text; result.Foreground = Ui.Res(fg); Core.Language.Refresh(result); }

            async Task Load(bool keep)
            {
                try
                {
                    var files = McFiles.For(s);
                    var (players, allow) = await Task.Run(() =>
                    {
                        var p = McBedrockPlayers.Read(files, null);
                        var props = files.ReadText("server.properties");
                        return (p, props == null || !McProperties.Parse(props).ToDictionary().TryGetValue("allow-list", out var v) || v.Trim() != "false");
                    });
                    loaded = players; allowOn = allow; error = null;
                }
                catch (Exception ex) { error = ex.Message; }
                if (keep) KeepFocus(card, Fill); else Fill();
            }

            async Task Apply(BedrockChange c, string name, string xuid = null)
            {
                Say("Working…", "B.TextMute");
                try
                {
                    var r = McDemo.IsSample(s) && IsUp(s) ? "Sample server: the command would be /" + McBedrockPlayers.Command(c, name) : await McBedrockPlayers.ApplyAsync(s, IsUp(s), c, name, xuid);
                    Say(r, "B.TextSoft");
                }
                catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                await Task.Delay(IsUp(s) && !McDemo.IsSample(s) ? 900 : 10);   // a running server saves its files a moment after the command
                await Load(false);
            }

            box = MakePlayerBox(s, "minecraft:bedrock-players", p => BedrockPlayerRow(s, p, Apply, () => { try { McBedrockPlayers.RemovePermission(McFiles.For(s), p.Xuid); } catch (Exception ex) { Say(ex.Message, "B.Amber"); } _ = Load(false); }), () => _ = Load(false));
            addRow.Children.Add(Ui.Button("Allow list",() => _ = Apply(BedrockChange.AllowAdd, nameBox.Text.Trim(), xuidBox.Text.Trim()), "Btn.Ghost", Icons.Plus));
            addRow.Children.Add(Ui.Button("Make operator…", () => ConfirmOp(s, nameBox.Text.Trim(), true, ok => { if (ok) _ = Apply(BedrockChange.Operator, nameBox.Text.Trim(), xuidBox.Text.Trim()); }), "Btn.Ghost", Icons.Shield));
            card.Children.Add(addRow);
            card.Children.Add(result);
            card.Children.Add(box.Root);
            root.Children.Add(McUi.Card(card));

            void Fill()
            {
                bool up = IsUp(s);
                how.Text = (up
                    ? "The server is running, so allow list and operator changes go through its console (allowlist add, allowlist remove, op, deop) and it saves them itself. Operator changes ask for the two-key confirmation."
                    : s.IsSsh ? "The server is stopped, so changes are written to allowlist.json and permissions.json on the SSH host. Operator changes ask for the two-key confirmation."
                    : "The server is stopped, so changes are written to allowlist.json and permissions.json. Operator changes ask for the two-key confirmation.")
                    + (allowOn ? "" : " The allow list is switched off in server.properties (allow-list=false), so these names are not enforced until it is turned on in Settings.");
                if (error != null) { box.Show(new List<McPlayer>(), error); Core.Language.Refresh(card); return; }
                if (loaded == null) { box.Show(new List<McPlayer>(), "Reading the player files…"); Core.Language.Refresh(card); return; }
                var online = up ? McLive.For(s).PlayerNames : Array.Empty<string>();
                var players = loaded.Select(p => new McPlayer { Name = p.Name, Xuid = p.Xuid, Permission = p.Permission, Operator = p.Operator, Whitelisted = p.Whitelisted, Anonymous = p.Anonymous, Online = online.Contains(p.Name, StringComparer.OrdinalIgnoreCase) }).ToList();
                foreach (var n in online) if (!players.Any(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) players.Add(new McPlayer { Name = n, Online = true });
                box.Show(players.OrderByDescending(p => p.Online).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(), players.Count == 0 ? "No players yet. Add one by name, or they appear once they join." : null);
                Core.Language.Refresh(card);
            }

            string last = null;
            string Signature()
            {
                var names = string.Join(",", McLive.For(s).PlayerNames);
                string files = "";
                if (s.IsLocal) files = string.Join("|", new[] { McBedrockPlayers.AllowFile, McBedrockPlayers.PermissionFile }.Select(f => File.GetLastWriteTimeUtc(Path.Combine(s.Folder, f)).Ticks));
                return IsUp(s) + "|" + names + "|" + files;
            }
            _tick = () => { var now = Signature(); if (now != last) { last = now; _ = Load(true); } };
            last = Signature();
            Fill();
            _ = Load(false);
            return root;
        }

        private FrameworkElement BedrockPlayerRow(ServerDef s, McPlayer p, Func<BedrockChange, string, string, Task> apply, Action removePermission)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            var g = new Grid { MinHeight = 48 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var init = McUi.Literal(p.Name.Length > 0 ? p.Name[..1].ToUpperInvariant() : "?", "B.OnTertiaryContainer", 14);
            init.FontWeight = FontWeights.SemiBold; init.HorizontalAlignment = HorizontalAlignment.Center; init.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = Ui.Res("B.TertiaryContainer"), Child = init, Margin = new Thickness(0, 0, 12, 0) });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(McUi.Literal(p.Name, "B.Text", 14));
            var note = (p.Online ? "online" : "offline") + (p.Permission.Length > 0 ? " · " + p.Permission : " · default permission") + (p.Xuid.Length > 0 ? " · Xbox user id " + p.Xuid : p.Anonymous ? "" : " · no Xbox user id recorded yet");
            text.Children.Add(new TextBlock { Text = note, FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(text, 1); g.Children.Add(text);
            Button more = null;
            more = McUi.IconButton("M12 5h.01M12 12h.01M12 19h.01", "More actions for " + p.Name, () =>
            {
                bool up = IsUp(s);
                var items = new List<ElementMenu.Item>();
                if (p.Anonymous) items.Add(new ElementMenu.Item("Remove permission entry", Icons.Trash, null, removePermission, true));
                else
                {
                    if (up && p.Online) items.Add(new ElementMenu.Item("Kick", Icons.Close, null, () => _ = SendLine(s, "kick " + (p.Name.Contains(' ') ? "\"" + p.Name + "\"" : p.Name))));
                    if (!up) items.Add(new ElementMenu.Item("Make visitor", Icons.Shield, null, () => _ = apply(BedrockChange.Visitor, p.Name, p.Xuid)));
                    if (p.Permission.Length > 0 && !up) items.Add(new ElementMenu.Item("Back to the default permission", Icons.Restart, null, () => _ = apply(BedrockChange.Member, p.Name, p.Xuid)));
                }
                if (items.Count == 0) return;
                var w = Window.GetWindow(more);
                if (w != null) ElementMenu.OpenItems(w, more, items, more.TranslatePoint(new Point(0, more.ActualHeight), w));
            });
            Grid.SetColumn(more, 2); g.Children.Add(more);
            sp.Children.Add(g);
            if (!p.Anonymous)
            {
                var sw = new WrapPanel { Margin = new Thickness(48, 0, 0, 0) };
                bool guard = false;
                CheckBox op = null, al = null;
                op = McUi.Switch("Operator", "Can run every command.", p.Operator, v =>
                {
                    if (guard) return;
                    ConfirmOp(s, p.Name, v, ok =>
                    {
                        if (ok) _ = apply(v ? BedrockChange.Operator : BedrockChange.Member, p.Name, p.Xuid);
                        else { guard = true; op.IsChecked = !v; guard = false; }
                    });
                });
                op.Width = 230; op.Margin = new Thickness(0, 0, 24, 4);
                AutomationProperties.SetName(op, "Operator: " + p.Name);
                al = McUi.Switch("Allow list", "Can join while the allow list is on.", p.Whitelisted, v => { if (!guard) _ = apply(v ? BedrockChange.AllowAdd : BedrockChange.AllowRemove, p.Name, p.Xuid); });
                al.Width = 250; al.Margin = new Thickness(0, 0, 0, 4);
                AutomationProperties.SetName(al, "Allow list: " + p.Name);
                sw.Children.Add(op); sw.Children.Add(al);
                sp.Children.Add(sw);
            }
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 6, 0, 2), Child = sp };
        }

        // ---------------------------------------------------------------- add-ons

        internal static bool EnableAfterInstall = true;

        private FrameworkElement BuildBedrockAddons(ServerDef s)
        {
            var root = new StackPanel();
            var card = new StackPanel();
            card.Children.Add(McUi.Title("Add-ons"));
            card.Children.Add(McUi.Body("Add-ons are behavior packs and resource packs. A pack lives in the server's behavior_packs or resource_packs folder, and a world uses it only when the world's pack list names it; the switch below edits that list.", "B.TextMute", 12.5));
            var bar = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            card.Children.Add(bar);
            var result = McUi.Body("", "B.TextMute", 12.5);
            result.Margin = new Thickness(0, 6, 0, 0);
            AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);
            void Say(string text, string fg) { result.Text = text; result.Foreground = Ui.Res(fg); Core.Language.Refresh(result); }
            var worldLine = McUi.Body("", "B.TextMute", 12.5);
            worldLine.Margin = new Thickness(0, 6, 0, 0);
            card.Children.Add(worldLine);
            var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            AutomationProperties.SetName(list, "Installed add-ons");
            card.Children.Add(list);
            card.Children.Add(result);
            var foot = McUi.Body("Before every install, switch or removal, what it touches is copied to a dated folder under addons-backups, so the last change can be undone. A running server reads its packs at the next start.", "B.TextMute", 12.5);
            foot.Margin = new Thickness(0, 10, 0, 0);
            card.Children.Add(foot);
            root.Children.Add(McUi.Card(card));

            List<McPack> packs = null; string level = "Bedrock level", error = null; bool worldExists = false;

            async Task Load()
            {
                try
                {
                    var files = McFiles.For(s);
                    (packs, level, worldExists) = await Task.Run(() => { var l = McRemote.LevelName(files, true); return (McBedrockAddons.List(files, l), l, files.Exists("worlds/" + l)); });
                    error = null;
                }
                catch (Exception ex) { error = ex.Message; }
                Fill();
            }

            async Task Work(string busy, Func<string> job)
            {
                Say(busy, "B.TextMute");
                try { var m = await Task.Run(job); Say(m, "B.Mint"); Sounds.Play(Cue.Success); }
                catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                await Load();
            }

            bar.Children.Add(Ui.Button("Install an add-on…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Add-on (*.mcpack;*.mcaddon;*.zip)|*.mcpack;*.mcaddon;*.zip", Title = "Choose an add-on" };
                if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
                var file = dlg.FileName; var on = EnableAfterInstall;
                _ = Work("Installing " + Path.GetFileName(file) + "…", () =>
                {
                    var files = McFiles.For(s);
                    var r = McBedrockAddons.Install(files, file, McRemote.LevelName(files, true), on);
                    var msg = r.Installed.Count == 0 ? "Nothing was installed." : "Installed " + string.Join(", ", r.Installed) + ".";
                    if (r.NeedsWorld) msg += " The world does not exist yet: start the server once so it makes its world, then switch the add-on on.";
                    else if (on && r.Installed.Count > 0) msg += " It is switched on for the world; restart the server to use it.";
                    if (r.Skipped.Count > 0) msg += " Skipped: " + string.Join("; ", r.Skipped) + ".";
                    return msg;
                });
            }, "Btn.Ghost", Icons.File));
            bar.Children.Add(Ui.Button("Undo the last change", () =>
            {
                var dlg = new ConfirmWindow("Put the add-ons back as they were?", "The newest saved copy is put back, and anything that change added is removed. What is there now is saved first, so this can be undone too.", "Put back", danger: true);
                dlg.Confirmed += ok => _ = Work("Putting the add-ons back…", () => "Put back the state from before " + McBedrockAddons.Undo(McFiles.For(s)) + ". Restart the server to apply it.");
                dlg.Show();
            }, "Btn.Ghost", Icons.Archive));
            var enableBox = McUi.Switch("Switch on for the world after installing", "Names each new pack in the world's pack list, with the packs it depends on.", EnableAfterInstall, v => EnableAfterInstall = v);
            enableBox.Margin = new Thickness(8, 0, 0, 0);
            bar.Children.Add(enableBox);

            void Fill()
            {
                list.Children.Clear();
                if (error != null) { list.Children.Add(McUi.Body(error, "B.Amber", 13)); Core.Language.Refresh(card); return; }
                if (packs == null) { list.Children.Add(McUi.Body("Reading the pack folders…", "B.TextMute", 13)); Core.Language.Refresh(card); return; }
                worldLine.Text = worldExists ? $"World: {level}. Packs are switched on for this world." : $"World: {level} (not made yet). Start the server once so it makes its world; then packs can be switched on.";
                var custom = packs.Where(p => !p.BuiltIn).ToList();
                if (custom.Count == 0) list.Children.Add(McUi.Body("No add-ons installed. Install a .mcpack or .mcaddon file.", "B.TextMute", 13));
                foreach (var p in custom) list.Children.Add(PackRow(p));
                int built = packs.Count - custom.Count;
                if (built > 0) list.Children.Add(McUi.Body($"{built} packs that ship with the server are not listed.", "B.TextMute", 12));
                Core.Language.Refresh(card);
            }

            FrameworkElement PackRow(McPack p)
            {
                var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res(p.Behavior ? "B.VioletWash" : "B.MintWash"), Child = Ui.Icon(p.Behavior ? Icons.Box : Icons.Image, Ui.Res(p.Behavior ? "B.Violet" : "B.Mint"), 18), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top });
                var stack = new StackPanel();
                var cb = McUi.Switch(p.Name, p.Enabled ? "On: the world uses it at the next start." : "Off: the world does not use it.", p.Enabled, v =>
                    _ = Work((v ? "Switching on " : "Switching off ") + p.Name + "…", () => { var files = McFiles.For(s); McBedrockAddons.Switch(files, McRemote.LevelName(files, true), p, v); return (v ? "Switched on " : "Switched off ") + p.Name + ". Restart the server to apply it."; }), literalLabel: true);
                cb.IsEnabled = p.Problem.Length == 0;
                stack.Children.Add(cb);
                stack.Children.Add(McUi.Literal($"{p.Kind}{(p.Version.Length > 0 ? " · " + p.Version : "")} · {Path.GetFileName(p.Folder)}", "B.TextMute", 12));
                if (p.Problem.Length > 0) stack.Children.Add(McUi.Body(p.Problem, "B.Amber", 12));
                else if (p.Description.Length > 0) stack.Children.Add(McUi.Literal(p.Description, "B.TextSoft", 12));
                Grid.SetColumn(stack, 1); g.Children.Add(stack);
                var del = McUi.IconButton(Icons.Trash, "Remove " + p.Name, () =>
                {
                    var dlg = new ConfirmWindow($"Remove {p.Name}?", "The pack's folder is deleted and it is taken out of the world's pack list. What it touches is copied to addons-backups first, so it can be put back.", "Remove", danger: true);
                    dlg.Confirmed += ok => _ = Work("Removing " + p.Name + "…", () => { var files = McFiles.For(s); McBedrockAddons.Remove(files, McRemote.LevelName(files, true), p); return "Removed " + p.Name + ". The folder was copied to addons-backups first."; });
                    dlg.Show();
                });
                del.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(del, 2); g.Children.Add(del);
                return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 4, 0, 8), Child = g };
            }

            Fill();
            _ = Load();
            return root;
        }

        // ---------------------------------------------------------------- worlds

        /// <summary>The Bedrock worlds folder: each world with its size, Download ZIP, and an import from a folder or a .mcworld.</summary>
        private FrameworkElement BedrockWorldsBody(ServerDef s, WrapPanel top, Action<string, string> say)
        {
            var host = new StackPanel();
            var list = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            host.Children.Add(list);
            var foot = McUi.Body("Backups hold the whole worlds folder. Import copies a world into the folder the server uses (level-name) and checks it first; replacing a world stops the server and takes a backup first. Reset and the world generator are for Java edition.", "B.TextMute", 12.5);
            foot.Margin = new Thickness(0, 12, 0, 0);
            host.Children.Add(foot);

            async Task Load()
            {
                list.Children.Clear();
                list.Children.Add(McUi.Body("Reading the worlds folder…", "B.TextMute", 13));
                List<McWorldFolder> worlds; string level;
                try { var files = McFiles.For(s); (worlds, level) = await Task.Run(() => { var l = McRemote.LevelName(files, true); return (McBedrockWorlds.List(files, l), l); }); }
                catch (Exception ex) { list.Children.Clear(); list.Children.Add(McUi.Body(ex.Message, "B.Amber", 13)); Core.Language.Refresh(list); return; }
                list.Children.Clear();
                if (worlds.Count == 0) list.Children.Add(McUi.Body("The Bedrock server makes its world in the worlds folder at its first start. You can also import one now.", "B.TextSoft", 13));
                foreach (var w in worlds)
                {
                    var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.MintWash"), Child = Ui.Icon(Icons.Globe, Ui.Res("B.Mint"), 18), Margin = new Thickness(0, 0, 14, 0) });
                    var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    text.Children.Add(McUi.Literal(w.Name, "B.Text", 14.5));
                    text.Children.Add(new TextBlock { Text = $"{Ui.Bytes(w.Size)}{(w.Title.Length > 0 && w.Title != w.Name ? " · " + w.Title : "")}{(w.InUse ? " · the world the server uses" : "")}", FontSize = 12.5, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                    Grid.SetColumn(text, 1); g.Children.Add(text);
                    var ww = w;
                    var dl = Ui.Button("Download ZIP", () => DownloadWorldZip(s, ww.Rel, ww.Name, say), "Btn.Ghost", Icons.Archive);
                    AutomationProperties.SetName(dl, "Download " + w.Name + " as a ZIP");
                    dl.Margin = new Thickness(8, 0, 0, 0);
                    Grid.SetColumn(dl, 2); g.Children.Add(dl);
                    list.Children.Add(g);
                }
                Core.Language.Refresh(list);
            }

            top.Children.Add(Ui.Button("Import a world…", () => ImportBedrockWorld(s, say, () => _ = Load()), "Btn.Ghost", Icons.Folder));
            _ = Load();
            return host;
        }

        private void ImportBedrockWorld(ServerDef s, Action<string, string> say, Action refill)
        {
            var pick = new Microsoft.Win32.OpenFileDialog { Filter = "Bedrock world (*.mcworld;*.zip)|*.mcworld;*.zip", Title = "Choose a .mcworld file, or cancel to choose a folder" };
            string source = null;
            if (pick.ShowDialog(Window.GetWindow(this)) == true) source = pick.FileName;
            else
            {
                var folder = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a Bedrock world folder (the one with level.dat and db)" };
                if (folder.ShowDialog(Window.GetWindow(this)) == true) source = folder.FolderName;
            }
            if (source == null) return;
            var (_, problem) = McBedrockWorlds.Validate(source);
            if (problem != null) { say(problem, "B.Amber"); return; }
            var files = McFiles.For(s);
            string level; bool exists;
            try { level = McRemote.LevelName(files, true); exists = files.Exists("worlds/" + level); }
            catch (Exception ex) { say(ex.Message, "B.Amber"); return; }
            async Task Go(bool replace)
            {
                say(replace ? "Stopping and backing up…" : "Importing…", "B.TextMute");
                try
                {
                    if (IsUp(s) && !McDemo.IsSample(s)) await ServerManager.StopAsync(s);
                    if (replace) await McBackups.BackUpAsync(s, false, "Before a replace", "");
                    await Task.Run(() => McBedrockWorlds.Import(files, source, level, replace));
                    say(replace ? "The world was replaced. The old one is in the newest backup. Start the server to play it." : "Imported. The server uses this world on its next start.", "B.Mint");
                    Sounds.Play(Cue.Success);
                }
                catch (Exception ex) { say(ex.Message, "B.Amber"); }
                refill();
            }
            if (!exists)
            {
                if (IsUp(s)) { say("Stop the server first: it holds its world open while it runs.", "B.Amber"); return; }
                _ = Go(false);
                return;
            }
            var dlg = new ConfirmWindow($"Replace the world of {s.Name}?", $"The world \"{level}\" is swapped for the one you chose. The server is stopped first, and a backup of the current worlds folder is taken before anything is removed.", "Replace", danger: true);
            dlg.Confirmed += ok => _ = Go(true);
            dlg.Show();
        }

        private void DownloadWorldZip(ServerDef s, string rel, string name, Action<string, string> say)
        {
            var save = new Microsoft.Win32.SaveFileDialog { Filter = "ZIP (*.zip)|*.zip", FileName = name + ".zip", Title = "Save the world as a ZIP" };
            if (save.ShowDialog(Window.GetWindow(this)) != true) return;
            say(s.IsSsh ? "Copying the world from the SSH host and writing the ZIP…" : "Writing the ZIP…", "B.TextMute");
            var target = save.FileName; var files = McFiles.For(s);
            _ = Task.Run(() => s.IsSsh ? McRemote.DownloadZip(files, rel, target) : McWorlds.Download(Path.Combine(s.Folder, rel.Replace('/', Path.DirectorySeparatorChar)), target)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                say(t.IsFaulted ? t.Exception.InnerException.Message : "Saved " + Path.GetFileName(target) + ".", t.IsFaulted ? "B.Amber" : "B.Mint"))));
        }

        /// <summary>The world folders of a Java server on an SSH host, read over SSH: size, seed (from a copy of level.dat) and Download ZIP.</summary>
        private FrameworkElement SshWorldsBody(ServerDef s, Action<string, string> say)
        {
            var host = new StackPanel();
            var list = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            host.Children.Add(list);
            var foot = McUi.Body("These folders are on the SSH host and are read over SSH; Download ZIP copies a folder here first. Import, reset and replace work on the host from here: the new world is copied up first, the current world gets a dated copy on the host, and any failure puts it back. Backups of this server copy its world here too.", "B.TextMute", 12.5);
            foot.Margin = new Thickness(0, 12, 0, 0);
            host.Children.Add(foot);
            async Task Load()
            {
                list.Children.Clear();
                list.Children.Add(McUi.Body("Reading the world folders on the host…", "B.TextMute", 13));
                try
                {
                    var files = McFiles.For(s);
                    var (dims, seed) = await Task.Run(() =>
                    {
                        var level = McRemote.LevelName(files, false);
                        var d = McRemote.Dimensions(files, level);
                        return (d, d.Count > 0 && d[0].Exists ? McRemote.Seed(files, d[0].Folder) : null);
                    });
                    list.Children.Clear();
                    foreach (var d in dims)
                    {
                        var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var (wash, ink, icon) = d.Kind switch { "Nether" => ("B.DangerWash", "B.Danger", Icons.Bolt), "The End" => ("B.VioletWash", "B.Violet", Icons.Moon), _ => ("B.MintWash", "B.Mint", Icons.Globe) };
                        g.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res(wash), Child = Ui.Icon(icon, Ui.Res(ink), 18), Margin = new Thickness(0, 0, 14, 0) });
                        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                        text.Children.Add(McUi.Literal(d.Name, "B.Text", 14.5));
                        var sd = d.Kind == "Overworld" && seed != null ? " · seed " + seed : "";
                        text.Children.Add(new TextBlock { Text = d.Exists ? $"{d.Kind} · {Ui.Bytes(d.Size)}{sd} · on the host" : $"{d.Kind} · not made yet; the server makes it on its first start", FontSize = 12.5, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                        Grid.SetColumn(text, 1); g.Children.Add(text);
                        if (d.Exists)
                        {
                            var dd = d;
                            var dl = Ui.Button("Download ZIP", () => DownloadWorldZip(s, dd.Folder, dd.Folder.Split('/').Last(), say), "Btn.Ghost", Icons.Archive);
                            AutomationProperties.SetName(dl, "Download " + d.Name + " as a ZIP");
                            dl.Margin = new Thickness(8, 0, 0, 0);
                            Grid.SetColumn(dl, 2); g.Children.Add(dl);
                        }
                        list.Children.Add(g);
                    }
                }
                catch (Exception ex) { list.Children.Clear(); list.Children.Add(McUi.Body(ex.Message, "B.Amber", 13)); }
                Core.Language.Refresh(list);
            }
            _ = Load();
            return host;
        }
    }
}
