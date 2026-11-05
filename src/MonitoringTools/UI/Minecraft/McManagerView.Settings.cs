using System;
using System.Collections.Generic;
using System.Globalization;
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
    // Settings: grouped server.properties rows with what each does, its key, default and when it applies, and an unsaved bar.
    internal sealed partial class McManagerView
    {
        internal static string SettingsGroup = "Gameplay";
        /// <summary>Unsaved values per server: a server.properties key (or the memory key, or "gamerule:name") and its new value.</summary>
        internal static readonly Dictionary<string, Dictionary<string, string>> Pending = new Dictionary<string, Dictionary<string, string>>();
        private string _propsText;
        private string _propsFor;
        private McCommandNode _rulesTree;

        private Dictionary<string, string> PendingFor(ServerDef s) { if (!Pending.TryGetValue(s.Id, out var p)) Pending[s.Id] = p = new Dictionary<string, string>(); return p; }

        private FrameworkElement BuildSettings(ServerDef s)
        {
            var root = new StackPanel();
            var host = new ContentControl { IsTabStop = false };
            root.Children.Add(host);
            if (_propsFor != s.Id || _propsText == null)
            {
                host.Content = McUi.Body("Reading server.properties…", "B.TextMute", 12);
                _ = Task.Run(() => ServerManager.ReadPropertiesText(s)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                {
                    _propsText = t.IsFaulted ? null : t.Result; _propsFor = s.Id;
                    if (Current?.Id == s.Id && Section == "settings") host.Content = _propsText == null ? McUi.Body("The settings could not be read from the host. It may be offline.", "B.Amber", 12) : SettingsBody(s);
                    Core.Language.Refresh(host);
                })));
            }
            else host.Content = SettingsBody(s);
            return root;
        }

        internal void ReloadSettings() { _propsText = null; _propsFor = null; }

        private FrameworkElement SettingsBody(ServerDef s)
        {
            var doc = McProperties.Parse(_propsText ?? "");
            var pending = PendingFor(s);
            var outer = new StackPanel();
            var bar = new ContentControl { IsTabStop = false };
            outer.Children.Add(bar);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outer.Children.Add(g);

            var nav = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            AutomationProperties.SetName(nav, "Setting groups");
            g.Children.Add(nav);
            var search = new SearchField("minecraft:settings", "Search settings", compact: true) { Margin = new Thickness(0, 0, 0, 8) };
            search.Margin = new Thickness(0, 0, 0, 12); outer.Children.Insert(outer.Children.IndexOf(g), search);
            var groupButtons = new StackPanel();
            nav.Children.Add(groupButtons);

            var right = new StackPanel();
            Grid.SetColumn(right, 1);
            g.Children.Add(right);
            var title = McUi.Title("", 22);
            var context = McUi.Body($"Values and limits are for {s.Flavour} {s.Version}. Keys in server.properties that are not listed here are kept exactly as written.", "B.TextMute", 12);
            context.Margin = new Thickness(0, 2, 0, 10);
            right.Children.Add(title);
            right.Children.Add(context);
            var rows = new StackPanel();
            right.Children.Add(McUi.Card(rows, 18));

            var rules = McCommandTree.GameRules(_rulesTree);
            var unknown = doc.Keys.Where(k => McPropertyCatalog.Find(k, s) == null).ToList();
            int CountOf(string group) => group == "Game rules" ? rules.Count : group == "Other keys" ? unknown.Count : McPropertyCatalog.For(s).Count(d => d.Group == group);
            string Value(string key) => pending.TryGetValue(key, out var v) ? v : key == McPropertyCatalog.MemoryKey ? s.MemoryMb.ToString() : doc.Get(key);

            void Bar()
            {
                if (pending.Count == 0) { bar.Content = null; return; }
                var b = new StackPanel();
                bool restartNeeded = pending.Keys.Any(k => { var d = McPropertyCatalog.Find(k, s); return d == null ? !k.StartsWith("gamerule:") : d.Applies == McApplies.Restart; });
                var text = McUi.Body(pending.Count == 1 ? "1 unsaved change." + (restartNeeded ? " It needs a restart to take effect." : "") : $"{pending.Count} unsaved changes." + (restartNeeded ? " Some need a restart to take effect." : ""), "B.Text", 14);
                text.VerticalAlignment = VerticalAlignment.Center;
                b.Children.Add(text);
                var btns = new WrapPanel();
                btns.Children.Add(Ui.Button("Discard", () => { pending.Clear(); BuildWorkspace(); }, "Btn.Ghost"));
                btns.Children.Add(Ui.Button("Save, apply at next start", () => _ = SaveSettings(s, false), "Btn.Ghost", Icons.Check));
                btns.Children.Add(Ui.Button("Save and restart", () => _ = SaveSettings(s, true), "Btn.Accent", Icons.Restart));
                btns.HorizontalAlignment = HorizontalAlignment.Right; btns.Margin = new Thickness(0, 6, 0, 0); b.Children.Add(btns);
                var card = new Border { Background = Ui.Res("B.SecondaryContainer"), CornerRadius = new CornerRadius(28), Padding = new Thickness(20, 10, 12, 4), Margin = new Thickness(0, 0, 0, 14), Child = b };
                AutomationProperties.SetName(card, "Unsaved changes");
                bar.Content = card;
                Ui.Expand(card);
                Core.Language.Refresh(bar);
            }

            void Set(string key, string value)
            {
                var original = key == McPropertyCatalog.MemoryKey ? s.MemoryMb.ToString() : doc.Get(key);
                var def = McPropertyCatalog.Find(key, s)?.Default;
                if (value == original || (original == null && value == def)) pending.Remove(key); else pending[key] = value;
                Bar();
            }

            FrameworkElement Head(string label, McApplies applies, string key, string def, bool changed)
            {
                var w = new WrapPanel();
                w.Children.Add(new TextBlock { Text = label, FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 10, 2), VerticalAlignment = VerticalAlignment.Center });
                var (txt, icon, bg, fg) = applies switch
                {
                    McApplies.Now => ("Applies now", Icons.Bolt, "B.MintWash", "B.Mint"),
                    McApplies.NewWorlds => ("New worlds only", Icons.Globe, "B.TertiaryContainer", "B.OnTertiaryContainer"),
                    McApplies.NotOnThisVersion => ("Not on this version", Icons.Close, "B.Raised", "B.TextMute"),
                    _ => ("Needs restart", Icons.Restart, "B.AmberWash", "B.Amber"),
                };
                w.Children.Add(McUi.Badge(txt, bg, fg, icon));
                if (changed) w.Children.Add(McUi.Badge("Changed", "B.VioletWash", "B.Violet"));
                return w;
            }

            TextBlock KeyLine(string key, string def)
            {
                var t = new TextBlock { Text = key + (def == null ? "" : " · default " + (def.Length == 0 ? "(empty)" : def)), FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Mono"), Margin = new Thickness(0, 2, 0, 0) };
                Core.Language.SetLiteral(t, true);
                return t;
            }

            FrameworkElement Row(McPropDef d)
            {
                var applies = McPropertyCatalog.AppliesOn(d, s.Version);
                var cur = Value(d.Key);
                bool changed = pending.ContainsKey(d.Key);
                var help = d.Help + (s.IsDocker && d.Key == McPropertyCatalog.MemoryKey ? " A Docker server's memory is fixed when its container is made, so a change here applies only to new containers." : "");
                bool off = applies == McApplies.NotOnThisVersion;
                FrameworkElement row;
                switch (d.Kind)
                {
                    case McPropKind.Switch:
                    {
                        var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = (cur ?? d.Default) == "true", IsEnabled = !off, Margin = new Thickness(0, 4, 0, 4) };
                        var text = new StackPanel();
                        text.Children.Add(Head(d.Label, applies, d.Key, d.Default, changed));
                        text.Children.Add(new TextBlock { Text = help + (off ? $" Not available on {s.Version}." : ""), Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap });
                        text.Children.Add(KeyLine(d.Key, d.Default));
                        cb.Content = text;
                        AutomationProperties.SetName(cb, d.Label);
                        AutomationProperties.SetHelpText(cb, help);
                        cb.Checked += (o, e) => Set(d.Key, "true");
                        cb.Unchecked += (o, e) => Set(d.Key, "false");
                        row = cb;
                        break;
                    }
                    case McPropKind.Choice:
                    {
                        var sp = McUi.Choice(d.Label, help, d.Choices.Select(c => (c, c)), cur ?? d.Default, v => Set(d.Key, v), literal: true);
                        sp.Children.RemoveAt(0);
                        sp.Children.Insert(0, Head(d.Label, applies, d.Key, d.Default, changed));
                        var kl = KeyLine(d.Key, d.Default); kl.Margin = new Thickness(0, 0, 0, 8); sp.Children.Insert(2, kl);
                        if (off) sp.IsEnabled = false;
                        row = sp;
                        break;
                    }
                    case McPropKind.Number:
                    {
                        var grid = new Grid();
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var text = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
                        text.Children.Add(Head(d.Label, applies, d.Key, d.Default, changed));
                        text.Children.Add(McUi.Body(help, "B.TextSoft", 12));
                        text.Children.Add(KeyLine(d.Key, d.Default));
                        grid.Children.Add(text);
                        long.TryParse(cur ?? d.Default, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v);
                        var box = new McNumberBox(d.Label, v, d.Min, d.Max, d.Step, d.Unit) { IsEnabled = !off, VerticalAlignment = VerticalAlignment.Center };
                        box.ValueChanged += x => Set(d.Key, x.ToString(CultureInfo.InvariantCulture));
                        var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                        col.Children.Add(box);
                        var range = new TextBlock { Text = $"{d.Min} to {d.Max}{(d.Unit.Length > 0 ? " " + d.Unit : "")}", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
                        col.Children.Add(range);
                        Grid.SetColumn(col, 1); grid.Children.Add(col);
                        row = grid;
                        break;
                    }
                    default:
                    {
                        var sp = new StackPanel();
                        sp.Children.Add(Head(d.Label, applies, d.Key, d.Default, changed));
                        sp.Children.Add(McUi.Body(help, "B.TextSoft", 12));
                        sp.Children.Add(KeyLine(d.Key, d.Default));
                        var box = McUi.Field(cur ?? "", d.Label, 320);
                        box.Margin = new Thickness(0, 8, 0, 0);
                        box.TextChanged += (o, e) => Set(d.Key, box.Text);
                        sp.Children.Add(box);
                        row = sp;
                        break;
                    }
                }
                SearchScope.SetItem(row, d.Key + " " + d.Label + " " + d.Help);
                return Wrap(row);
            }

            Border Wrap(FrameworkElement row) => new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 12, 0, 12), Child = row };

            FrameworkElement RuleRow((string Name, string Parser) r)
            {
                bool up = IsUp(s);
                var key = "gamerule:" + r.Name;
                McPropertyCatalog.RuleDefaults.TryGetValue(r.Name, out var def);
                string reported; lock (McLive.For(s).GameRules) McLive.For(s).GameRules.TryGetValue(r.Name, out reported);
                var cur = pending.TryGetValue(key, out var pv) ? pv : reported ?? def;
                var help = up ? (reported != null ? "The value the server reported." : def != null ? "Shown at its usual default; read the current values to be sure." : "Read the current values to see this rule.") : "Game rules live inside the world. Start the server to change them.";
                if (r.Parser == "brigadier:bool")
                {
                    var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = cur == "true", IsEnabled = up, Margin = new Thickness(0, 4, 0, 4) };
                    var text = new StackPanel();
                    text.Children.Add(Head(r.Name, McApplies.Now, r.Name, def, pending.ContainsKey(key)));
                    text.Children.Add(McUi.Body(help, "B.TextSoft", 12));
                    text.Children.Add(KeyLine("gamerule " + r.Name, def));
                    cb.Content = text;
                    AutomationProperties.SetName(cb, r.Name);
                    cb.Checked += (o, e) => { pending[key] = "true"; Bar(); };
                    cb.Unchecked += (o, e) => { pending[key] = "false"; Bar(); };
                    SearchScope.SetItem(cb, "gamerule " + r.Name);
                    return Wrap(cb);
                }
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var t2 = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
                t2.Children.Add(Head(r.Name, McApplies.Now, r.Name, def, pending.ContainsKey(key)));
                t2.Children.Add(McUi.Body(help, "B.TextSoft", 12));
                t2.Children.Add(KeyLine("gamerule " + r.Name, def));
                grid.Children.Add(t2);
                long.TryParse(cur, out var n);
                var nb = new McNumberBox(r.Name, n, r.Parser == "brigadier:integer" ? 0 : int.MinValue, int.MaxValue) { IsEnabled = up, VerticalAlignment = VerticalAlignment.Center };
                nb.ValueChanged += x => { pending[key] = x.ToString(CultureInfo.InvariantCulture); Bar(); };
                Grid.SetColumn(nb, 1); grid.Children.Add(nb);
                SearchScope.SetItem(grid, "gamerule " + r.Name);
                return Wrap(grid);
            }

            void Fill()
            {
                rows.Children.Clear();
                bool searching = search.State.IsActive;
                title.Text = searching ? "Matching settings" : SettingsGroup;
                IEnumerable<string> groups = searching ? McPropertyCatalog.Groups : new[] { SettingsGroup };
                int shown = 0, total = McPropertyCatalog.For(s).Count + rules.Count + unknown.Count;
                foreach (var grp in groups)
                {
                    if (grp == "Game rules")
                    {
                        if (!searching)
                        {
                            var note = McUi.Body(rules.Count == 0 ? "This version's game rules have not been read yet. They come from the jar's own command list." : IsUp(s) ? "Changes are sent with /gamerule when you save, and apply at once." : "Game rules live inside the world, so they can only be changed while the server runs.", "B.TextMute", 12);
                            rows.Children.Add(note);
                            var btns = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
                            if (rules.Count == 0) btns.Children.Add(Ui.Button("Read the game rules from the jar", async () =>
                            {
                                note.Text = "Reading…"; Core.Language.Refresh(note);
                                var (tree, why) = await McCommandTree.LoadAsync(s.Version, t => Dispatcher.BeginInvoke(new Action(() => { note.Text = t; Core.Language.Refresh(note); })), true);
                                if (tree == null) { note.Text = why; Core.Language.Refresh(note); return; }
                                _rulesTree = tree; BuildWorkspace();
                            }, "Btn.Ghost", Icons.Code));
                            else if (IsUp(s)) btns.Children.Add(Ui.Button("Read the current values", async () =>
                            {
                                foreach (var r in rules) await ServerManager.CommandAsync(s, "gamerule " + r.Name);
                                await Task.Delay(800);
                                Fill();
                            }, "Btn.Ghost", Icons.Restart));
                            rows.Children.Add(btns);
                        }
                        foreach (var r in rules.Where(r => search.State.Matches("gamerule " + r.Name))) { rows.Children.Add(RuleRow(r)); shown++; }
                        continue;
                    }
                    if (grp == "Other keys")
                    {
                        if (!searching) rows.Children.Add(McUi.Body(unknown.Count == 0 ? "server.properties has no keys beyond the documented ones." : "Keys this app does not document for this version. They are shown as they are and saved back unchanged.", "B.TextMute", 12));
                        foreach (var k in unknown.Where(k => search.State.Matches(k + " " + doc.Get(k))))
                        {
                            var sp = new StackPanel();
                            sp.Children.Add(Head(k, McApplies.Restart, k, null, false));
                            sp.Children.Add(McUi.Body("Kept exactly as written in server.properties.", "B.TextSoft", 12));
                            sp.Children.Add(KeyLine(k + " = " + doc.Get(k), null));
                            rows.Children.Add(Wrap(sp));
                            shown++;
                        }
                        continue;
                    }
                    if (grp == "Management" && !searching) rows.Children.Add(McUi.Body("Settings for the management interface that arrived with 1.21.9.", "B.TextMute", 12));
                    foreach (var d in McPropertyCatalog.For(s).Where(d => d.Group == grp && search.State.Matches(d.Key + " " + d.Label + " " + d.Help))) { rows.Children.Add(Row(d)); shown++; }
                }
                if (rows.Children.Count == 0) rows.Children.Add(McUi.Body("No settings match the search.", "B.TextMute", 12));
                search.SetCount(searching ? shown : total, total, "settings");
                Core.Language.Refresh(right);
            }

            void Groups()
            {
                groupButtons.Children.Clear();
                foreach (var grp in McPropertyCatalog.Groups)
                {
                    var gg = grp;
                    bool on = SettingsGroup == grp && !search.State.IsActive;
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.Children.Add(new TextBlock { Text = grp, FontSize = 14, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                    var c = new TextBlock { Text = CountOf(grp).ToString(), FontSize = 12, FontFamily = Ui.Font("F.Body"), Opacity = 0.8, Margin = new Thickness(8, 0, 0, 0) };
                    Grid.SetColumn(c, 1); row.Children.Add(c);
                    var b = McUi.Row(row, grp + ", " + CountOf(grp) + " settings");
                    b.Background = (on ? Ui.Res("B.SecondaryContainer") : System.Windows.Media.Brushes.Transparent);
                    b.Foreground = Ui.Res(on ? "B.OnSecondaryContainer" : "B.TextSoft");
                    b.Click += (o, e) => { SettingsGroup = gg; search.State.SetQuery(""); Groups(); Fill(); };
                    groupButtons.Children.Add(b);
                    if (on) Dispatcher.BeginInvoke(new Action(() => { var sh = McUi.Shape(b); if (sh != null) sh.CornerRadius = new CornerRadius(28); }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }

            search.SampleSource = () => McPropertyCatalog.For(s).Select(d => d.Key + " " + d.Label);
            search.Filter += () => { Groups(); Fill(); };
            if (_rulesTree == null && System.IO.File.Exists(McCommandTree.FileFor(s.Version)))
            {
                try { _rulesTree = McCommandTree.Parse(System.IO.File.ReadAllText(McCommandTree.FileFor(s.Version))); rules = McCommandTree.GameRules(_rulesTree); } catch { }
            }
            Groups();
            Fill();
            Bar();
            return outer;
        }

        /// <summary>Writes the unsaved values: server.properties keeps every other line; live keys and game rules go through the console while the server runs.</summary>
        internal async Task SaveSettings(ServerDef s, bool restart)
        {
            var pending = PendingFor(s);
            if (pending.Count == 0) return;
            bool up = IsUp(s);
            try
            {
                var props = pending.Where(p => p.Key != McPropertyCatalog.MemoryKey && !p.Key.StartsWith("gamerule:")).ToList();
                if (props.Count > 0)
                {
                    var text = await Task.Run(() => ServerManager.ReadPropertiesText(s)) ?? throw new InvalidOperationException("server.properties could not be read, so nothing was saved.");
                    var doc = McProperties.Parse(text);
                    foreach (var (k, v) in props) doc.Set(k, v);
                    if (!McDemo.IsSample(s) || s.IsLocal) await Task.Run(() => ServerManager.WritePropertiesText(s, doc.ToString()));
                    _propsText = doc.ToString(); _propsFor = s.Id;
                }
                if (pending.TryGetValue(McPropertyCatalog.MemoryKey, out var mem) && int.TryParse(mem, out var mb)) { s.MemoryMb = mb; ServerManager.Save(); }
                var sent = new List<string>();
                if (up && !restart && !McDemo.IsSample(s))
                {
                    foreach (var (k, v) in pending)
                    {
                        string line = null;
                        if (k.StartsWith("gamerule:")) line = $"gamerule {k[9..]} {v}";
                        else if (McPropertyCatalog.Find(k, s) is McPropDef d && d.LiveCommand != null && McPropertyCatalog.AppliesOn(d, s.Version) == McApplies.Now)
                            line = string.Format(CultureInfo.InvariantCulture, d.LiveCommand, d.Kind == McPropKind.Switch ? (v == "true" ? "on" : "off") : v);
                        if (line != null) { await ServerManager.CommandAsync(s, line); sent.Add("/" + line); }
                    }
                }
                pending.Clear();
                BuildWorkspace();
                SetStatus(sent.Count > 0 ? "Saved. Applied now: " + string.Join(", ", sent) : restart ? "Saved. Restarting…" : "Saved. The changes apply at the next start.", "B.Mint");
                if (restart && !McDemo.IsSample(s)) await Act(s, "Restart", async () => { if (up) await ServerManager.StopAsync(s); await ServerManager.StartAsync(s, null); });
            }
            catch (Exception ex) { BuildWorkspace(); SetStatus(ex.Message, "B.Amber"); }
        }
    }
}
