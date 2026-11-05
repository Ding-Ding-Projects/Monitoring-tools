using System;
using System.IO;
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
    // Backups: back up now, the list with restore, how many to keep and where they go.
    internal sealed partial class McManagerView
    {
        private FrameworkElement BuildBackups(ServerDef s)
        {
            var root = new StackPanel();
            var card = new StackPanel();
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(McUi.Title("Backups"));
            var noteBox = McUi.Field("", "Note for this backup", 220, "Optional, for example: before the nether rebuild");
            var now = Ui.Button("Back up now", null, "Btn.Accent", Icons.Archive);
            now.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(now, 1); head.Children.Add(now);
            card.Children.Add(head);
            var noteRow = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            noteRow.Children.Add(new TextBlock { Text = "Note for the next backup (optional)", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) });
            noteBox.Width = 360;
            noteRow.Children.Add(noteBox);
            card.Children.Add(noteRow);
            var result = McUi.Body("", "B.TextMute", 12.5);
            AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);
            void Say(string text, string fg) { result.Text = text; result.Foreground = Ui.Res(fg); Core.Language.Refresh(result); }
            card.Children.Add(result);
            var list = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            card.Children.Add(list);
            var c1 = McUi.Card(card);

            // Backups kept on an SSH host are listed by reading the host's own folder.
            List<McBackup> hostList = null; string hostError = null;
            async void LoadHost()
            {
                try { var files = McFiles.For(s); hostList = await Task.Run(() => McRemote.HostList(files)); hostError = null; }
                catch (Exception ex) { hostError = ex.Message; }
                Fill();
            }
            void Refill() { if (s.IsSsh && s.BackupOnHost) LoadHost(); else Fill(); }
            void Fill()
            {
                list.Children.Clear();
                bool onHost = s.IsSsh && s.BackupOnHost;
                var all = onHost ? (hostList ?? new List<McBackup>()) : McBackups.List(s.BackupFolder);
                if (onHost && hostError != null) list.Children.Add(McUi.Body(hostError, "B.Amber", 13));
                else if (onHost && hostList == null) list.Children.Add(McUi.Body("Reading the backups on the SSH host…", "B.TextMute", 13));
                else if (all.Count == 0) list.Children.Add(McUi.Body("No backups yet.", "B.TextMute", 13));
                foreach (var b in all)
                {
                    var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.TertiaryContainer"), Child = Ui.Icon(b.Reason == "Scheduled" ? Icons.Clock : Icons.Archive, Ui.Res("B.OnTertiaryContainer"), 18), Margin = new Thickness(0, 0, 14, 0) });
                    var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    var when = new TextBlock { Text = b.When.ToString("yyyy-MM-dd HH:mm"), FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") };
                    Core.Language.SetLiteral(when, true);
                    t.Children.Add(when);
                    t.Children.Add(new TextBlock { Text = $"{b.Reason} · {Ui.Bytes(b.Size)} · {McUi.Age(b.When)}", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                    if (b.Contents.Count > 0) t.Children.Add(McUi.Literal(string.Join(", ", b.Contents) + (b.Note.Length > 0 ? " · " + b.Note : ""), "B.TextMute", 12));
                    Grid.SetColumn(t, 1); g.Children.Add(t);
                    var bb = b;
                    var restore = Ui.Button("Restore…", () => Restore(s, bb, Say, Refill), "Btn.Ghost", Icons.Restart);
                    AutomationProperties.SetName(restore, "Restore the backup from " + b.When.ToString("yyyy-MM-dd HH:mm"));
                    restore.Margin = new Thickness(8, 0, 0, 0);
                    Grid.SetColumn(restore, 2); g.Children.Add(restore);
                    list.Children.Add(g);
                }
                Core.Language.Refresh(list);
            }
            now.Click += async (o, e) =>
            {
                now.IsEnabled = false;
                try
                {
                    var b = await McBackups.BackUpAsync(s, IsUp(s) && !McDemo.IsSample(s), "By hand", noteBox.Text.Trim(), t => Dispatcher.BeginInvoke(new Action(() => Say(t, "B.TextMute"))));
                    noteBox.Text = "";
                    Say($"Backed up to {Path.GetFileName(b.Path)} ({Ui.Bytes(b.Size)}).", "B.Mint");
                    Sounds.Play(Cue.Success);
                }
                catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                now.IsEnabled = true;
                Refill();
            };
            Fill(); if (s.IsSsh && s.BackupOnHost) LoadHost();

            var keep = new StackPanel();
            keep.Children.Add(McUi.Title("Keep", 16));
            var kg = new Grid { Margin = new Thickness(0, 8, 0, 8) };
            kg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            kg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var kt = new StackPanel();
            kt.Children.Add(new TextBlock { Text = "Newest backups to keep", FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") });
            kt.Children.Add(McUi.Body("Older ones are removed after each new backup. Default: 14.", "B.TextMute", 12));
            kg.Children.Add(kt);
            var kb = new McNumberBox("Backups to keep", s.KeepBackups, 1, 1000);
            kb.ValueChanged += v => { s.KeepBackups = (int)v; try { ServerManager.Save(); } catch { } };
            Grid.SetColumn(kb, 1); kg.Children.Add(kb);
            keep.Children.Add(kg);
            var fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ft = new StackPanel();
            ft.Children.Add(new TextBlock { Text = "Folder", FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") });
            var path = McUi.Literal(s.IsSsh && s.BackupOnHost ? ".mt-backups on the SSH host" : PathDisplay.Shown(s.BackupFolder), "B.TextMute", 12);
            ft.Children.Add(path);
            fg.Children.Add(ft);
            var choose = Ui.Button("Choose…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Where should backups go?" };
                if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
                s.BackupDir = dlg.FolderName;
                try { ServerManager.Save(); } catch { }
                BuildWorkspace();
            }, "Btn.Ghost", Icons.Folder);
            Grid.SetColumn(choose, 1); fg.Children.Add(choose);
            choose.IsEnabled = !(s.IsSsh && s.BackupOnHost);
            keep.Children.Add(fg);
            if (s.IsSsh)
            {
                var onHost = McUi.Switch("Keep backups on the SSH host", "Archives the world into .mt-backups on the host (tar.gz) and lists them from there; nothing is copied to this computer. Off: backups are copied here.", s.BackupOnHost, v =>
                {
                    s.BackupOnHost = v;
                    try { ServerManager.Save(); } catch { }
                    BuildWorkspace();
                });
                onHost.Margin = new Thickness(0, 8, 0, 0);
                keep.Children.Add(onHost);
            }
            var how = McUi.Body("A backup of a running server runs save-off and save-all, copies the world, then save-on, so the copy is consistent while players stay online. Restore stops the server, keeps the current world as one more backup, then swaps."
                + (s.IsSsh ? " This server's world is on the SSH host: a backup copies it here with scp, and a restore copies the backup back up, moving the host's current folders aside until every folder has arrived." : ""), "B.TextMute", 12.5);
            how.Margin = new Thickness(0, 12, 0, 0);
            keep.Children.Add(how);

            root.Children.Add(c1);
            root.Children.Add(McUi.Card(keep));
            return root;
        }

        private void Restore(ServerDef s, McBackup b, Action<string, string> say, Action refill)
        {
            var dlg = new ConfirmWindow($"Restore the backup from {b.When:yyyy-MM-dd HH:mm}?",
                "The server is stopped, the current world is kept as one more backup, and then the backup's folders replace the current ones.", "Restore", danger: true);
            dlg.Confirmed += async _ =>
            {
                say("Stopping and restoring…", "B.TextMute");
                try
                {
                    if (IsUp(s) && !McDemo.IsSample(s)) await ServerManager.StopAsync(s);
                    McBackup safety;
                    if (s.IsSsh)
                    {
                        var files = McFiles.For(s);
                        safety = await Task.Run(() => s.BackupOnHost ? McRemote.HostRestore(s, files, McRemote.LevelName(files, s.IsBedrock), b) : McRemote.Restore(s, files, McRemote.LevelName(files, s.IsBedrock), b));
                    }
                    else
                    {
                        var level = McWorlds.LevelName(s);
                        safety = await Task.Run(() => McBackups.Restore(s.Folder, level, s.BackupFolder, b, s.KeepBackups));
                    }
                    say("Restored." + (safety != null ? " The world as it was is in " + Path.GetFileName(safety.Path) + "." : "") + " Start the server to play it.", "B.Mint");
                }
                catch (Exception ex) { say(ex.Message, "B.Amber"); }
                refill();
            };
            dlg.Show();
        }
    }
}
