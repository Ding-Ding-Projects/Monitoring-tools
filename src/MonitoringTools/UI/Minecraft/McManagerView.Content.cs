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
    // Plugins or mods: the installed list with an enable switch, install from a file, remove, and the Modrinth catalogue.
    internal sealed partial class McManagerView
    {
        private static readonly Dictionary<string, List<McCatalogHit>> LastFinds = new Dictionary<string, List<McCatalogHit>>();

        private FrameworkElement BuildContent(ServerDef s)
        {
            var root = new StackPanel();
            var noun = s.ContentFolder == "mods" ? "mods" : "plugins";
            if (s.ContentFolder == null)
            {
                var sp = new StackPanel();
                sp.Children.Add(McUi.Title("Plugins and mods"));
                sp.Children.Add(McUi.Body("Vanilla runs neither plugins nor mods. A Paper or Purpur server takes plugins, and a Fabric, Forge or NeoForge server takes mods; make a new server with one of those to use them.", "B.TextSoft", 13));
                root.Children.Add(McUi.Card(sp));
                return root;
            }
            var installed = new StackPanel();
            var ih = new Grid();
            ih.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ih.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ih.Children.Add(McUi.Title($"Installed on {s.Flavour} {s.Version}"));
            var ib = new WrapPanel();
            Grid.SetColumn(ib, 1); ih.Children.Add(ib);
            installed.Children.Add(ih);
            var result = McUi.Body("", "B.TextMute", 12.5);
            AutomationProperties.SetLiveSetting(result, AutomationLiveSetting.Polite);
            void Say(string text, string fg) { result.Text = text; result.Foreground = Ui.Res(fg); Core.Language.Refresh(result); }
            var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            installed.Children.Add(list);
            installed.Children.Add(result);
            var foot = McUi.Body($"Turning one off renames its jar to .jar.disabled, which the server skips. Before any install, removal or switch, the whole {noun} folder is copied to a dated backup beside it, so the change can be rolled back. Changes take effect at the next start.", "B.TextMute", 12.5);
            foot.Margin = new Thickness(0, 10, 0, 0);
            installed.Children.Add(foot);
            root.Children.Add(McUi.Card(installed));

            ib.Children.Add(Ui.Button("Install from a file…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Plugin or mod (*.jar)|*.jar", Title = "Choose a plugin or mod" };
                if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
                try
                {
                    if (s.IsLocal) { McContent.Install(McContent.FolderOf(s), dlg.FileName); Say("Installed " + Path.GetFileName(dlg.FileName) + ". The folder was backed up first. Restart the server to load it.", "B.Mint"); Fill(); }
                    else Say(ServerManager.InstallPlugin(s, dlg.FileName), "B.TextSoft");
                }
                catch (Exception ex) { Say(ex.Message, "B.Amber"); }
            }, "Btn.Ghost", Icons.File));

            void Fill()
            {
                list.Children.Clear();
                if (!s.IsLocal) { list.Children.Add(McUi.Body($"This server's {noun} folder is on the SSH host. Install from a file copies a jar there over SSH; the list itself is managed on the host.", "B.TextSoft", 13)); return; }
                var items = McContent.List(McContent.FolderOf(s));
                if (items.Count == 0) list.Children.Add(McUi.Body($"No {noun} yet. Install one from a file or find one in the catalogue.", "B.TextMute", 13));
                foreach (var a in items) list.Children.Add(AddonRow(s, a, Say, Fill));
                Core.Language.Refresh(list);
            }
            Fill();

            // ---- catalogue
            var find = new StackPanel();
            find.Children.Add(McUi.Title("Find " + noun, 16));
            var filters = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
            filters.Children.Add(McUi.Badge($"Works with {s.Version} only", "B.SecondaryContainer", "B.OnSecondaryContainer"));
            filters.Children.Add(McUi.Badge(s.Flavour.ToString(), "B.Raised", "B.TextSoft"));
            find.Children.Add(filters);
            var q = new SearchField("minecraft:catalogue", "Search Modrinth", compact: true) { Margin = new Thickness(0, 0, 0, 6) };
            find.Children.Add(q);
            var go = Ui.Button("Search the catalogue", null, "Btn.Ghost", Icons.Search);
            go.HorizontalAlignment = HorizontalAlignment.Left;
            find.Children.Add(go);
            var hits = new StackPanel();
            find.Children.Add(hits);
            var catalogueNote = McUi.Body("Results come from the public Modrinth catalogue, filtered to this version and software. Each download is checked against the catalogue's SHA-512 before it is copied in. Hangar is not searched yet.", "B.TextMute", 12);
            catalogueNote.Margin = new Thickness(0, 8, 0, 0);
            find.Children.Add(catalogueNote);
            root.Children.Add(McUi.Card(find));

            void ShowHits(List<McCatalogHit> found)
            {
                hits.Children.Clear();
                if (found.Count == 0) hits.Children.Add(McUi.Body($"Nothing in the catalogue matches for {s.Flavour} {s.Version}.", "B.TextMute", 12.5));
                foreach (var h in found)
                {
                    var g = new Grid { Margin = new Thickness(0, 8, 0, 0) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var t = new StackPanel();
                    t.Children.Add(McUi.Literal(h.Title, "B.Text", 14));
                    t.Children.Add(McUi.Literal($"Modrinth · {h.Author} · {h.Downloads:N0} downloads · {h.Description}", "B.TextMute", 12));
                    g.Children.Add(t);
                    var hh = h;
                    Button inst = null;
                    inst = Ui.Button("Install", async () =>
                    {
                        if (!s.IsLocal) { Say("Catalogue installs copy into a folder on this computer; this server's folder is on the SSH host.", "B.Amber"); return; }
                        inst.IsEnabled = false;
                        Say("Downloading " + hh.Title + "…", "B.TextMute");
                        try { Say(await McModrinth.InstallAsync(hh, s, t2 => Dispatcher.BeginInvoke(new Action(() => Say(t2, "B.TextMute")))), "B.Mint"); Fill(); }
                        catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                        inst.IsEnabled = true;
                    }, "Btn.Ghost", Icons.Plus);
                    AutomationProperties.SetName(inst, "Install " + h.Title);
                    inst.Margin = new Thickness(12, 0, 0, 0);
                    Grid.SetColumn(inst, 1); g.Children.Add(inst);
                    hits.Children.Add(g);
                }
                Core.Language.Refresh(hits);
            }
            async void Search()
            {
                hits.Children.Clear();
                hits.Children.Add(McUi.Body("Searching…", "B.TextMute", 12.5));
                try
                {
                    var found = await McModrinth.SearchAsync(q.State.Query, s.Flavour, s.Version);
                    LastFinds[s.Id] = found;
                    ShowHits(found);
                }
                catch (Exception ex) { hits.Children.Clear(); hits.Children.Add(McUi.Body("The catalogue could not be reached: " + ex.Message, "B.Amber", 12.5)); }
            }
            go.Click += (o, e) => Search();
            q.Box.KeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.Enter) Search(); };
            if (LastFinds.TryGetValue(s.Id, out var prev)) ShowHits(prev);
            return root;
        }

        private FrameworkElement AddonRow(ServerDef s, McAddon a, Action<string, string> say, Action refill)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.VioletWash"), Child = Ui.Icon(Icons.Box, Ui.Res("B.Violet"), 18), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top });
            var name = McUi.Literal(a.Name, "B.Text", 14.5);
            var sub = $"{(a.Version.Length > 0 ? a.Version + " · " : "")}{Path.GetFileName(a.File)} · {Ui.Bytes(a.Size)}";
            var cb = McUi.Switch(a.Name, a.Enabled ? "On: the server loads it at its next start." : "Off: the server skips it.", a.Enabled, v =>
            {
                try { McContent.SetEnabled(a, v); say((v ? "Turned on " : "Turned off ") + a.Name + ". The folder was backed up first; restart the server to apply it.", "B.Mint"); refill(); }
                catch (Exception ex) { say(ex.Message, "B.Amber"); refill(); }
            }, literalLabel: true);
            // The switch carries the name as its label; the jar's file and version go underneath.
            var meta = McUi.Literal(sub, "B.TextMute", 12);
            var stack = new StackPanel();
            stack.Children.Add(cb);
            stack.Children.Add(meta);
            Grid.SetColumn(stack, 1); g.Children.Add(stack);
            Button more = null;
            more = McUi.IconButton(Icons.Trash, "Remove " + a.Name, () =>
            {
                var dlg = new ConfirmWindow($"Remove {a.Name}?", $"The jar is deleted from the folder. A backup of the whole folder is taken first, so it can be put back.", "Remove", danger: true);
                dlg.Confirmed += _ => { try { McContent.Remove(a); say("Removed " + a.Name + ". The folder was backed up first.", "B.Mint"); } catch (Exception ex) { say(ex.Message, "B.Amber"); } refill(); };
                dlg.Show();
            });
            more.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(more, 2); g.Children.Add(more);
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 4, 0, 8), Child = g };
        }
    }
}
