using System;
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
    // Worlds: each dimension with its size and seed, import, download, reset or replace, and the world generator.
    internal sealed partial class McManagerView
    {
        internal static bool ShowGenerator;

        private FrameworkElement BuildWorlds(ServerDef s)
        {
            var root = new StackPanel();
            var card = new StackPanel();
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(McUi.Title("Worlds"));
            var top = new WrapPanel();
            Grid.SetColumn(top, 1); head.Children.Add(top);
            card.Children.Add(head);
            var result = McUi.Body("", "B.TextMute", 12.5);
            AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);
            var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            card.Children.Add(list);
            card.Children.Add(result);
            void Say(string text, string fg) { result.Text = text; result.Foreground = Ui.Res(fg); Core.Language.Refresh(result); }

            if (s.IsBedrock)
            {
                list.Children.Add(BedrockWorldsBody(s, top, Say));
            }
            else if (!s.IsLocal)
            {
                top.Children.Add(Ui.Button("Import a world…", () => Import(s, Say), "Btn.Ghost", Icons.Folder));
                top.Children.Add(Ui.Button("Reset the world…", () => Reset(s, null, Say), "Btn.Danger", Icons.Restart));
                list.Children.Add(SshWorldsBody(s, Say));
            }
            else
            {
                top.Children.Add(Ui.Button("Import a world…", () => Import(s, Say), "Btn.Ghost", Icons.Folder));
                top.Children.Add(Ui.Button("Reset the world…", () => Reset(s, null, Say), "Btn.Danger", Icons.Restart));
                var level = McWorlds.LevelName(s);
                var dims = McWorlds.Dimensions(s.Folder, level);
                var props = ServerManager.ReadProperties(s);
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
                    string seed = "";
                    if (d.Kind == "Overworld")
                    {
                        var fileSeed = McWorlds.Seed(d.Folder);
                        seed = fileSeed != null ? " · seed " + fileSeed : props.TryGetValue("level-seed", out var ps) && ps.Length > 0 ? " · seed " + ps : "";
                    }
                    var meta = new TextBlock { Text = d.Exists ? $"{d.Kind} · {Ui.Bytes(d.Size)}{seed}{(IsUp(s) && d.Kind == "Overworld" ? " · in use" : "")}" : $"{d.Kind} · not made yet; the server makes it on its first start", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap };
                    text.Children.Add(meta);
                    Grid.SetColumn(text, 1); g.Children.Add(text);
                    if (d.Exists)
                    {
                        var dd = d;
                        var dl = Ui.Button("Download ZIP", () => Download(dd, Say), "Btn.Ghost", Icons.Archive);
                        AutomationProperties.SetName(dl, "Download " + d.Name + " as a ZIP");
                        dl.Margin = new Thickness(8, 0, 0, 0);
                        Grid.SetColumn(dl, 2); g.Children.Add(dl);
                    }
                    list.Children.Add(g);
                }
                var foot = McUi.Body("Reset and replace stop the server, take a backup first and ask for the two-key confirmation. An imported world is copied and checked for a level.dat, never moved.", "B.TextMute", 12.5);
                foot.Margin = new Thickness(0, 12, 0, 0);
                card.Children.Add(foot);
            }
            root.Children.Add(McUi.Card(card));
            if (s.IsBedrock) return root;   // the generator runs the Java server jar

            var gen = new StackPanel();
            var gh = new Grid();
            gh.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gh.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var gt = new StackPanel();
            gt.Children.Add(McUi.Title("Generate a new world, or pre-generate chunks", 16));
            gt.Children.Add(McUi.Body("Builds terrain with the real server jar: a seed, a world type and a square area of chunks generated ahead of time, saved as a folder or a ZIP, or written into this server.", "B.TextMute", 12.5));
            gh.Children.Add(gt);
            var toggle = Ui.Button(ShowGenerator ? "Hide the generator" : "Open the generator", () => { ShowGenerator = !ShowGenerator; BuildWorkspace(); }, "Btn.Ghost", Icons.Wand);
            toggle.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(toggle, 1); gh.Children.Add(toggle);
            gen.Children.Add(gh);
            root.Children.Add(McUi.Card(gen));
            if (ShowGenerator) root.Children.Add(_owner.WorldGeneratorCard(s));
            return root;
        }

        /// <summary>Set by the self-test in place of the file and folder pickers: answers the path a person would have chosen, or null for cancel.</summary>
        internal static Func<string> WorldSourceForTest;
        /// <summary>Set by the self-test in place of the two-key confirmation window: answers whether it was confirmed.</summary>
        internal static Func<bool> WorldConfirmForTest;
        /// <summary>The most recent import, reset or replace, so a test can wait for it to finish.</summary>
        internal static Task WorldActionTask = Task.CompletedTask;

        private void Import(ServerDef s, Action<string, string> say)
        {
            string source = null;
            if (WorldSourceForTest != null) source = WorldSourceForTest();
            else
            {
                var pick = new Microsoft.Win32.OpenFileDialog { Filter = "World ZIP (*.zip)|*.zip", Title = "Choose a world ZIP, or cancel to choose a folder" };
                if (pick.ShowDialog(Window.GetWindow(this)) == true) source = pick.FileName;
                else
                {
                    var folder = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a world folder (the one with level.dat)" };
                    if (folder.ShowDialog(Window.GetWindow(this)) == true) source = folder.FolderName;
                }
            }
            if (source == null) return;
            var (_, problem) = McWorlds.Validate(source);
            if (problem != null) { say(problem, "B.Amber"); return; }
            if (s.IsSsh)
            {
                bool exists;
                try { var f = McFiles.For(s); var lv = McRemote.LevelName(f, false); exists = f.Exists(lv); }
                catch (Exception ex) { say(ex.Message, "B.Amber"); return; }
                if (exists) { Reset(s, source, say); return; }
                if (IsUp(s) && !McDemo.IsSample(s)) { say("Stop the server first: it holds its world open while it runs.", "B.Amber"); return; }
                WorldActionTask = RunSshWorld(s, source, "Importing…", "Imported. The server uses this world on its next start.", say);
                return;
            }
            if (Directory.Exists(Path.Combine(s.Folder, McWorlds.LevelName(s)))) { Reset(s, source, say); return; }
            try { McWorlds.ImportInto(s, source); say("Imported. The server uses this world on its next start.", "B.Mint"); BuildWorkspace(); }
            catch (Exception ex) { say(ex.Message, "B.Amber"); }
        }

        private Task RunSshWorld(ServerDef s, string source, string start, string done, Action<string, string> say)
        {
            return Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(() => say(start, "B.TextMute"));
                try
                {
                    var files = McFiles.For(s);
                    await Task.Run(() => McRemoteWorlds.Import(files, source, McRemote.LevelName(files, false), false));
                    await Dispatcher.InvokeAsync(() => { BuildWorkspace(); SetStatus(done, "B.Mint"); });
                }
                catch (Exception ex) { await Dispatcher.InvokeAsync(() => say(ex.Message, "B.Amber")); }
            });
        }

        private void Reset(ServerDef s, string replacement, Action<string, string> say)
        {
            async Task Go()
            {
                say("Stopping and backing up…", "B.TextMute");
                try
                {
                    if (IsUp(s) && !McDemo.IsSample(s)) await ServerManager.StopAsync(s);
                    if (s.IsSsh)
                    {
                        var files = McFiles.For(s);
                        var r = await Task.Run(() => { var lv = McRemote.LevelName(files, false); return replacement == null ? McRemoteWorlds.Reset(files, lv) : McRemoteWorlds.Import(files, replacement, lv, true); });
                        BuildWorkspace();
                        SetStatus((replacement == null ? "The world was reset." : "The world was replaced.") + (r.Backups.Count > 0 ? " The old world is kept on the host in " + string.Join(", ", r.Backups) + "." : ""), "B.Mint");
                        return;
                    }
                    var b = await Task.Run(() => McWorlds.ResetOrReplace(s, replacement));
                    BuildWorkspace();
                    SetStatus((replacement == null ? "The world was reset." : "The world was replaced.") + (b != null ? " The old world is in the backup " + Path.GetFileName(b.Path) + "." : ""), "B.Mint");
                }
                catch (Exception ex) { say(ex.Message, "B.Amber"); }
            }
            if (WorldConfirmForTest != null)
            {
                if (WorldConfirmForTest()) WorldActionTask = Go(); else say("Not changed: it was not confirmed.", "B.TextMute");
                return;
            }
            var dlg = new ConfirmWindow(replacement == null ? $"Reset the world of {s.Name}?" : $"Replace the world of {s.Name}?",
                (replacement == null ? "The world folders are removed, and the server makes a new world on its next start." : "The current world is swapped for the one you chose.") +
                " The server is stopped first, and a backup of the current world is taken before anything is removed.", replacement == null ? "Reset" : "Replace", danger: true);
            dlg.Confirmed += _ => WorldActionTask = Go();
            dlg.Show();
        }

        private void Download(McDimension d, Action<string, string> say)
        {
            var save = new Microsoft.Win32.SaveFileDialog { Filter = "ZIP (*.zip)|*.zip", FileName = Path.GetFileName(d.Folder) + ".zip", Title = "Save the world as a ZIP" };
            if (save.ShowDialog(Window.GetWindow(this)) != true) return;
            say("Writing the ZIP…", "B.TextMute");
            var target = save.FileName;
            _ = Task.Run(() => McWorlds.Download(d.Folder, target)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                say(t.IsFaulted ? t.Exception.InnerException.Message : "Saved " + Path.GetFileName(target) + ".", t.IsFaulted ? "B.Amber" : "B.Mint"))));
        }
    }
}
