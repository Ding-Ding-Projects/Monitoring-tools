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
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Minecraft
{
    // Plugins or mods: the installed list with an enable switch and a compatibility badge, install from a file, remove,
    // update all, undo the last change, and the Modrinth and Hangar catalogues.
    internal sealed partial class McManagerView
    {
        internal static readonly Dictionary<string, List<McCatalogHit>> LastFinds = new Dictionary<string, List<McCatalogHit>>();
        /// <summary>Which catalogue the search uses: "Modrinth" or "Hangar" (Hangar only for Paper and Purpur).</summary>
        internal static string CatalogueSource = "Modrinth";
        private static readonly HashSet<string> CheckedOnce = new HashSet<string>();

        private FrameworkElement BuildContent(ServerDef s)
        {
            var root = new StackPanel();
            var noun = s.ContentFolder == "mods" ? "mods" : "plugins";
            if (s.IsBedrock) return BuildBedrockAddons(s);
            if (s.ContentFolder == null)
            {
                var sp = new StackPanel();
                sp.Children.Add(McUi.Title("Plugins and mods"));
                sp.Children.Add(McUi.Body("Vanilla runs neither plugins nor mods. A Paper or Purpur server takes plugins, and a Fabric, Forge or NeoForge server takes mods; make a new server with one of those to use them.", "B.TextSoft", 12));
                root.Children.Add(McUi.Card(sp));
                return root;
            }
            if (!McHangar.Serves(s.Flavour)) CatalogueSource = "Modrinth";
            var installed = new StackPanel();
            installed.Children.Add(McUi.Title($"Installed on {s.Flavour} {s.Version}"));
            // The actions sit on their own row below the title and wrap, so none is cut off at narrow widths.
            var ib = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            installed.Children.Add(ib);
            var result = McUi.Body("", "B.TextMute", 12);
            AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);
            void Say(string text, string fg) { result.Text = text; result.Foreground = Ui.Res(fg); Core.Language.Refresh(result); }
            var checkLine = McUi.Body("", "B.TextMute", 12);
            checkLine.Margin = new Thickness(0, 6, 0, 0);
            installed.Children.Add(checkLine);
            var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            AutomationProperties.SetName(list, "Installed " + noun);
            installed.Children.Add(list);
            installed.Children.Add(result);
            var foot = McUi.Body($"Turning one off renames its jar to .jar.disabled, which the server skips. Before any install, update, removal or switch, the whole {noun} folder is copied to a dated backup beside it, so the change can be rolled back. Changes take effect at the next start.", "B.TextMute", 12);
            foot.Margin = new Thickness(0, 10, 0, 0);
            installed.Children.Add(foot);
            root.Children.Add(McUi.Card(installed));

            Button updateAll = null, check = null;
            // A server on an SSH host: its folder is listed over SSH, and a switch or removal runs there.
            List<McAddon> remoteItems = null; string remoteError = null;
            async void LoadRemote()
            {
                try { var files = McFiles.For(s); remoteItems = await Task.Run(() => McContent.List(files, s.ContentFolder)); remoteError = null; }
                catch (Exception ex) { remoteError = ex.Message; }
                Fill();
            }
            ib.Children.Add(Ui.Button("Install from a file…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Plugin or mod (*.jar)|*.jar", Title = "Choose a plugin or mod" };
                if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
                try
                {
                    if (s.IsLocal) { McContent.Install(McContent.FolderOf(s), dlg.FileName); Say("Installed " + Path.GetFileName(dlg.FileName) + ". The folder was backed up first. Restart the server to load it.", "B.Mint"); Fill(); }
                    else
                    {
                        var jar = dlg.FileName;
                        Say("Copying " + Path.GetFileName(jar) + " to the SSH host…", "B.TextMute");
                        _ = Task.Run(() => McContent.Install(McFiles.For(s), s.ContentFolder, jar)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (t.IsFaulted) Say(t.Exception.InnerException.Message, "B.Amber");
                            else Say("Copied " + Path.GetFileName(jar) + " into " + noun + " on the host. The folder was copied to a dated backup beside it first. Restart the server to load it.", "B.Mint");
                            LoadRemote();
                        })));
                    }
                }
                catch (Exception ex) { Say(ex.Message, "B.Amber"); }
            }, "Btn.Ghost", Icons.File));
            if (s.ContentFolder != null)
            {
                check = Ui.Button("Check for updates", () => _ = Check(), "Btn.Ghost", Icons.Search);
                ib.Children.Add(check);
                updateAll = Ui.Button("Update all", async () =>
                {
                    if (!McCatalogue.Last.TryGetValue(s.Id, out var checks)) { Say("Check for updates first.", "B.Amber"); return; }
                    if (McDemo.IsSample(s)) { Say("Sample server: nothing was downloaded.", "B.TextMute"); return; }
                    updateAll.IsEnabled = false;
                    try { Say(await McCatalogue.UpdateAllAsync(s, checks, t => Dispatcher.BeginInvoke(new Action(() => Say(t, "B.TextMute")))), "B.Mint"); await Check(); }
                    catch (Exception ex) { Say(ex.Message, "B.Amber"); Fill(); }
                    updateAll.IsEnabled = true;
                }, "Btn.Accent", Icons.Bolt);
                ib.Children.Add(updateAll);
                ib.Children.Add(Ui.Button("Undo the last change", () =>
                {
                    var folder = McContent.FolderOf(s);
                    var hostFiles = s.IsSsh ? McFiles.For(s) : null;
                    string last;
                    try { last = hostFiles != null ? McContent.LatestBackup(hostFiles, s.ContentFolder) : McContent.LatestBackup(folder); }
                    catch (Exception ex) { Say(ex.Message, "B.Amber"); return; }
                    if (last == null) { Say("There is no backup of the folder yet, so there is nothing to undo.", "B.TextMute"); return; }
                    var dlg = new ConfirmWindow("Put the folder back as it was?", $"The {noun} folder is put back as the backup from {Path.GetFileName(last)} holds it. The folder as it is now is backed up first, so this can be undone too.", "Put back", danger: true);
                    dlg.Confirmed += okk =>
                    {
                        if (hostFiles != null)
                        {
                            Say("Working on the SSH host…", "B.TextMute");
                            _ = Task.Run(() => McContent.RestoreFrom(hostFiles, s.ContentFolder, last, keepCurrent: true)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                            {
                                McCatalogue.Last.Remove(s.Id);
                                Say(t.IsFaulted ? t.Exception.InnerException.Message : "The folder was put back as the backup holds it, on the host. Restart the server to apply it.", t.IsFaulted ? "B.Amber" : "B.Mint");
                                Refill();
                            })));
                            return;
                        }
                        try { McContent.RestoreFrom(folder, last, keepCurrent: true); McCatalogue.Last.Remove(s.Id); Say("The folder was put back as the backup holds it. Restart the server to apply it.", "B.Mint"); }
                        catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                        Fill();
                    };
                    dlg.Show();
                }, "Btn.Ghost", Icons.Archive));
            }

            void Fill()
            {
                list.Children.Clear();
                List<McAddon> items;
                if (!s.IsLocal)
                {
                    if (remoteError != null) { list.Children.Add(McUi.Body(remoteError, "B.Amber", 12)); Core.Language.Refresh(installed); return; }
                    if (remoteItems == null) { list.Children.Add(McUi.Body($"Reading the {noun} folder on the SSH host…", "B.TextMute", 12)); Core.Language.Refresh(installed); return; }
                    items = remoteItems;
                }
                else items = McContent.List(McContent.FolderOf(s));
                McCatalogue.Last.TryGetValue(s.Id, out var checks);
                if (items.Count == 0) list.Children.Add(McUi.Body($"No {noun} yet. Install one from a file or find one in the catalogue.", "B.TextMute", 12));
                foreach (var a in items)
                {
                    McCompatInfo info = null;
                    checks?.TryGetValue(Path.GetFileName(a.File), out info);
                    list.Children.Add(AddonRow(s, a, info, Say, Refill));
                }
                int due = checks?.Values.Count(c => c.Kind == McCompat.Update) ?? 0;
                checkLine.Text = checks == null ? "Not checked against the catalogues yet." : due == 0 ? "Checked against Modrinth and Hangar: nothing newer for this version." : due == 1 ? "1 update for this version." : $"{due} updates for this version.";
                if (updateAll != null) updateAll.IsEnabled = due > 0;
                Core.Language.Refresh(installed);
            }
            async Task Check()
            {
                if (McDemo.IsSample(s)) { Fill(); return; }
                checkLine.Text = "Checking the catalogues…"; Core.Language.Refresh(checkLine);
                if (check != null) check.IsEnabled = false;
                try { await McCatalogue.CheckAsync(s); }
                catch (Exception ex) { Say("The catalogues could not be reached: " + ex.Message, "B.Amber"); }
                if (check != null) check.IsEnabled = true;
                Fill();
            }
            // After a change the badges are read again, so they always describe the files that are there now.
            void Refill() { if (!s.IsLocal) LoadRemote(); else if (McCatalogue.Last.ContainsKey(s.Id) || McDemo.IsSample(s)) Fill(); else _ = Check(); }
            Fill();
            if (!s.IsLocal) LoadRemote();
            if (s.IsLocal && !McDemo.IsSample(s) && CheckedOnce.Add(s.Id) && McContent.List(McContent.FolderOf(s)).Count > 0) _ = Check();

            // ---- catalogue
            var find = new StackPanel();
            find.Children.Add(McUi.Title("Find " + noun, 16));
            var filters = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
            find.Children.Add(filters);
            var sources = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            AutomationProperties.SetName(sources, "Catalogue");
            find.Children.Add(sources);
            var q = new SearchField("minecraft:catalogue", "Search the catalogue", compact: true) { Margin = new Thickness(0, 0, 0, 6) };
            find.Children.Add(q);
            var go = Ui.Button("Search the catalogue", null, "Btn.Ghost", Icons.Search);
            go.HorizontalAlignment = HorizontalAlignment.Left;
            find.Children.Add(go);
            var hits = new StackPanel();
            AutomationProperties.SetName(hits, "Catalogue results");
            find.Children.Add(hits);
            var catalogueNote = McUi.Body("", "B.TextMute", 12);
            catalogueNote.Margin = new Thickness(0, 8, 0, 0);
            find.Children.Add(catalogueNote);
            root.Children.Add(McUi.Card(find));

            void Sources()
            {
                filters.Children.Clear();
                filters.Children.Add(McUi.Badge($"Works with {s.Version} only", "B.SecondaryContainer", "B.OnSecondaryContainer"));
                filters.Children.Add(McUi.Badge(s.Flavour.ToString(), "B.Raised", "B.TextSoft"));
                sources.Children.Clear();
                // The catalogue names are proper names, so the chips keep them in every language.
                var modrinth = McUi.Chip("Modrinth", CatalogueSource == "Modrinth", () => { CatalogueSource = "Modrinth"; Sources(); Show(); }, "Search Modrinth");
                Core.Language.SetLiteral(modrinth, true);
                sources.Children.Add(modrinth);
                if (McHangar.Serves(s.Flavour))
                {
                    var hangar = McUi.Chip("Hangar", CatalogueSource == "Hangar", () => { CatalogueSource = "Hangar"; Sources(); Show(); }, "Search Hangar");
                    Core.Language.SetLiteral(hangar, true);
                    sources.Children.Add(hangar);
                }
                catalogueNote.Text = CatalogueSource == "Hangar"
                    ? "Results come from the public Hangar catalogue, filtered to Paper on this version. Each download is checked against Hangar's SHA-256 before it is copied in; a version Hangar offers only as a link to another site is refused."
                    : "Results come from the public Modrinth catalogue, filtered to this version and software. Each download is checked against the catalogue's SHA-512 before it is copied in." + (McHangar.Serves(s.Flavour) ? " Hangar is the other catalogue for Paper plugins." : "");
                Core.Language.Refresh(find);
            }
            string FindKey() => s.Id + "|" + CatalogueSource;
            void Show() { hits.Children.Clear(); if (LastFinds.TryGetValue(FindKey(), out var prev)) ShowHits(prev); }

            void ShowHits(List<McCatalogHit> found)
            {
                hits.Children.Clear();
                if (found.Count == 0) hits.Children.Add(McUi.Body($"Nothing in the catalogue matches for {s.Flavour} {s.Version}.", "B.TextMute", 12));
                foreach (var h in found)
                {
                    var g = new Grid { Margin = new Thickness(0, 8, 0, 0) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var t = new StackPanel();
                    t.Children.Add(McUi.Literal(h.Title, "B.Text", 14));
                    t.Children.Add(McUi.Literal($"{h.Source} · {h.Author} · {h.Downloads:N0} downloads · {h.Description}", "B.TextMute", 12));
                    g.Children.Add(t);
                    var hh = h;
                    Button inst = null;
                    inst = Ui.Button("Install", async () =>
                    {
                        if (McDemo.IsSample(s)) { Say("Sample server: nothing was downloaded.", "B.TextMute"); return; }
                        inst.IsEnabled = false;
                        Say("Downloading " + hh.Title + "…", "B.TextMute");
                        void Report(string t2) => Dispatcher.BeginInvoke(new Action(() => Say(t2, "B.TextMute")));
                        try { Say(hh.Source == "Hangar" ? await McHangar.InstallAsync(hh, s, Report) : await McModrinth.InstallAsync(hh, s, Report), "B.Mint"); McCatalogue.Last.Remove(s.Id); Refill(); }
                        catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                        inst.IsEnabled = true;
                    }, "Btn.Ghost", Icons.Plus);
                    AutomationProperties.SetName(inst, "Install " + h.Title);
                    inst.Margin = new Thickness(12, 0, 0, 0);
                    inst.VerticalAlignment = VerticalAlignment.Top;
                    Grid.SetColumn(inst, 1); g.Children.Add(inst);
                    hits.Children.Add(g);
                }
                Core.Language.Refresh(hits);
            }
            async void Search()
            {
                hits.Children.Clear();
                hits.Children.Add(McUi.Body("Searching…", "B.TextMute", 12));
                var src = CatalogueSource;
                try
                {
                    var found = src == "Hangar" ? await McHangar.SearchAsync(q.State.Query, s.Flavour, s.Version) : await McModrinth.SearchAsync(q.State.Query, s.Flavour, s.Version);
                    LastFinds[s.Id + "|" + src] = found;
                    if (src == CatalogueSource) ShowHits(found);
                }
                catch (Exception ex) { hits.Children.Clear(); hits.Children.Add(McUi.Body("The catalogue could not be reached: " + ex.Message, "B.Amber", 12)); }
            }
            go.Click += (o, e) => Search();
            q.Box.KeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.Enter) Search(); };
            Sources();
            Show();
            return root;
        }

        private FrameworkElement AddonRow(ServerDef s, McAddon a, McCompatInfo info, Action<string, string> say, Action refill)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.VioletWash"), Child = Ui.Icon(Icons.Box, Ui.Res("B.Violet"), 18), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top });
            var sub = $"{(a.Version.Length > 0 ? a.Version + " · " : "")}{Path.GetFileName(a.File)} · {Ui.Bytes(a.Size)}";
            var cb = McUi.Switch(a.Name, a.Enabled ? "On: the server loads it at its next start." : "Off: the server skips it.", a.Enabled, v =>
            {
                if (a.Files != null)
                {
                    say("Working on the SSH host…", "B.TextMute");
                    _ = Task.Run(() => McContent.SetEnabled(a, v)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                    {
                        say(t.IsFaulted ? t.Exception.InnerException.Message : (v ? "Turned on " : "Turned off ") + a.Name + ". The folder was copied to a dated backup on the host first; restart the server to apply it.", t.IsFaulted ? "B.Amber" : "B.Mint");
                        refill();
                    })));
                    return;
                }
                try { McContent.SetEnabled(a, v); McCatalogue.Last.Remove(s.Id); say((v ? "Turned on " : "Turned off ") + a.Name + ". The folder was backed up first; restart the server to apply it.", "B.Mint"); refill(); }
                catch (Exception ex) { say(ex.Message, "B.Amber"); refill(); }
            }, literalLabel: true);
            // The switch carries the name as its label; the jar's file and version go underneath, then what the catalogue says.
            var meta = McUi.Literal(sub, "B.TextMute", 12);
            var stack = new StackPanel();
            stack.Children.Add(cb);
            stack.Children.Add(meta);
            if (info != null)
            {
                var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                var (bg, fg, icon) = info.Kind switch
                {
                    McCompat.Compatible => ("B.MintWash", "B.Mint", Icons.Check),
                    McCompat.Update => ("B.SecondaryContainer", "B.OnSecondaryContainer", Icons.Restart),
                    McCompat.MayNotWork => ("B.AmberWash", "B.Amber", Icons.Bolt),
                    _ => ("B.Raised", "B.TextMute", (string)null),
                };
                var badge = McUi.Badge(info.Label, bg, fg, icon);
                AutomationProperties.SetName(badge, info.Label + ": " + info.Explain(s.Version));
                row.Children.Add(badge);
                var why = McUi.Body(info.Explain(s.Version), "B.TextMute", 12);
                why.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(why);
                stack.Children.Add(row);
            }
            Grid.SetColumn(stack, 1); g.Children.Add(stack);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            if (info?.Kind == McCompat.Update && info.Apply != null)
            {
                Button upd = null;
                upd = Ui.Button("Update", async () =>
                {
                    upd.IsEnabled = false;
                    try { say(await info.Apply(t => Dispatcher.BeginInvoke(new Action(() => say(t, "B.TextMute")))), "B.Mint"); McCatalogue.Last.Remove(s.Id); }
                    catch (Exception ex) { say(ex.Message, "B.Amber"); }
                    refill();
                }, "Btn.Ghost", Icons.Bolt);
                AutomationProperties.SetName(upd, "Update " + a.Name);
                btns.Children.Add(upd);
            }
            var more = McUi.IconButton(Icons.Trash, "Remove " + a.Name, () =>
            {
                var dlg = new ConfirmWindow($"Remove {a.Name}?", $"The jar is deleted from the folder. A backup of the whole folder is taken first, so it can be put back.", "Remove", danger: true);
                dlg.Confirmed += ok =>
                {
                    if (a.Files != null)
                    {
                        say("Working on the SSH host…", "B.TextMute");
                        _ = Task.Run(() => McContent.Remove(a)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                        {
                            say(t.IsFaulted ? t.Exception.InnerException.Message : "Removed " + a.Name + ". The folder was copied to a dated backup on the host first.", t.IsFaulted ? "B.Amber" : "B.Mint");
                            refill();
                        })));
                        return;
                    }
                    try { McContent.Remove(a); McCatalogue.Last.Remove(s.Id); say("Removed " + a.Name + ". The folder was backed up first.", "B.Mint"); } catch (Exception ex) { say(ex.Message, "B.Amber"); } refill(); };
                dlg.Show();
            });
            btns.Children.Add(more);
            Grid.SetColumn(btns, 2); g.Children.Add(btns);
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 4, 0, 8), Child = g };
        }
    }
}
