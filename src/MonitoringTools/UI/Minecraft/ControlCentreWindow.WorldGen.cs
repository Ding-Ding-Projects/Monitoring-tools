using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        private readonly WorldGenOptions _wgOpts = new WorldGenOptions { Seed = "" };
        private string _wgServerId;
        private string _wgBiomeJar;
        private List<string> _wgBiomes;
        private JarFacts _wgFacts;
        private string _wgFactsVersion;

        /// <summary>
        /// World generator for the Minecraft page: seed, level type, structures, a bounded square area to pre-generate,
        /// and folder or ZIP output. It runs the real server jar; there is no preview image, because none can be honest.
        /// </summary>
        private FrameworkElement WorldGenCard()
        {
            var o = _wgOpts;
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("World generator",
                "Build a world with the real server for a version: choose a seed, a world type and an area, then save it as a folder or a ZIP. Or write those choices into one of your local servers."));

            var supportNote = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            supportNote.TextWrapping = TextWrapping.Wrap;
            var typePanel = new StackPanel();
            var biomePanel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            var estimate = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            WorldGenerator.Support current = null;
            TextBlock jarStatus = null, dimNote = null, flatStatus = null;
            StackPanel flatPanel = null, rulesPanel = null, rulesHost = null;
            SearchField rulesFilter = null;
            CheckBox dimNether = null, dimEnd = null;
            WorldgenEditorUi wgEd = null;

            // ---- server and version
            var servers = ServerManager.Servers.Where(s => s.Target == Target.LocalProcess || s.Target == Target.LocalDocker).ToList();
            var versionBox = Input(o.Version ?? "", 200, "A Java edition version id such as 1.21.4. Default: the newest release.");
            AutomationProperties.SetName(versionBox, "World version");
            sp.Children.Add(Ui.Mono("Which server", Ui.Res("B.TextMute"), 11));
            var serverChips = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
            void PaintServers()
            {
                serverChips.Children.Clear();
                void Chip(string label, string id, string tip)
                {
                    var b = Ui.Button(label, () =>
                    {
                        _wgServerId = id;
                        var s = servers.FirstOrDefault(x => x.Id == id);
                        if (s != null) { o.Version = s.Version; versionBox.Text = s.Version; }
                        versionBox.IsEnabled = s == null;
                        PaintServers(); _ = RefreshSupport();
                    }, "Btn.Chip");
                    b.ToolTip = tip; b.Height = 28; b.Margin = new Thickness(0, 0, 4, 4);
                    bool on = _wgServerId == id;
                    b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                    serverChips.Children.Add(b);
                }
                Chip("New world only", null, "Generate a stand-alone world; no existing server is touched");
                foreach (var s in servers) Chip($"{s.Name} ({s.Version})", s.Id, "Use this server's version and write the settings into its server.properties");
            }
            PaintServers();
            sp.Children.Add(serverChips);
            if (_wgServerId != null && servers.All(s => s.Id != _wgServerId)) _wgServerId = null;
            versionBox.IsEnabled = _wgServerId == null;
            versionBox.LostFocus += (a, b) => { o.Version = versionBox.Text.Trim(); _ = RefreshSupport(); };
            sp.Children.Add(Ui.Mono("Version", Ui.Res("B.TextMute"), 11));
            sp.Children.Add(versionBox);

            // ---- seed
            var seed = Input(o.Seed, 280, "Leave empty for a random seed chosen by the server. Any text or number works.");
            seed.TextChanged += (a, b) => o.Seed = seed.Text;
            AutomationProperties.SetName(seed, "World seed");
            var seedRow = new WrapPanel();
            seedRow.Children.Add(seed);
            var rnd = Ui.Button("Random seed", () => seed.Text = WorldGenerator.RandomSeed(), "Btn.Ghost", Icons.Restart);
            rnd.ToolTip = "Fill the seed field with a random number you can reuse later.";
            seedRow.Children.Add(rnd);
            sp.Children.Add(Ui.Mono("SEED (empty = random)", Ui.Res("B.TextMute"), 11));
            sp.Children.Add(seedRow);

            // ---- type
            sp.Children.Add(typePanel);
            sp.Children.Add(biomePanel);
            sp.Children.Add(supportNote);

            // ---- jar-driven options: read once per version, then used by the game rule editor, the layer editor and the dimension switches
            jarStatus = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            jarStatus.TextWrapping = TextWrapping.Wrap; jarStatus.Margin = new Thickness(0, 4, 0, 4);
            var readJar = Ui.Button("Read this version's jar", null, "Btn.Ghost", Icons.Folder);
            readJar.ToolTip = "Downloads this version's server jar once (checked against Mojang's SHA-1) and runs its own data generator, without starting a server, to read its game rules, block list and command list. Needed for the game rule editor and for checking superflat blocks.";
            flatPanel = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            rulesPanel = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            rulesFilter = new SearchField("minecraft:gamerules", "Search game rules", compact: true) { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
            rulesHost = new StackPanel();
            dimNote = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            dimNote.TextWrapping = TextWrapping.Wrap; dimNote.Margin = new Thickness(0, 0, 0, 6);
            

            void PaintDims()
            {
                if (dimNether == null) return;
                var f = _wgFacts;
                string why = null;
                if (current != null && !current.Forceload) why = current.ForceloadReason;
                else if (o.RadiusChunks <= 0) why = "Set an area radius above 0 first.";
                else if (f != null && !f.Available) why = f.Reason;
                else if (f != null && !f.HasExecuteIn) why = "This jar has no execute in command, so a chunk cannot be loaded in another dimension.";
                else if (f != null && !f.HasLoadedTest) why = "This jar has no command that reports whether a chunk is loaded, so pre-generation cannot be confirmed.";
                dimNether.IsEnabled = dimEnd.IsEnabled = why == null;
                if (why != null) { o.PreGenNether = o.PreGenEnd = false; dimNether.IsChecked = dimEnd.IsChecked = false; }
                dimNote.Text = why != null ? "Unavailable: " + why
                    : f == null ? "Uses the same chunk radius in each dimension. Whether this jar supports it is confirmed when you read the jar or generate; if not, that dimension is skipped and the result says so."
                    : "This jar supports execute in, so the same bounded procedure runs in each chosen dimension. Time and disk add up.";
                dimNote.Foreground = Ui.Res(why != null ? "B.Amber" : "B.TextMute");
            }

            string RuleValue(string name) => o.GameRules.TryGetValue(name, out var v) ? v : null;

            void PaintRules()
            {
                rulesHost.Children.Clear();
                var f = _wgFacts;
                if (f == null) { rulesHost.Children.Add(Ui.Text("Press “Read this version's jar” above to list this version's game rules.")); rulesFilter.SetCount(0, 0, "rules"); return; }
                if (!f.Available || !f.HasGamerule) { rulesHost.Children.Add(Ui.Text(f.Reason ?? "This jar's command list has no game rules in a form this editor can read.")); rulesFilter.SetCount(0, 0, "rules"); return; }
                rulesFilter.SampleSource = () => f.GameRules.Select(r => r.Name);
                var hits = f.GameRules.Where(r => rulesFilter.State.Matches(r.Name)).ToList();
                foreach (var r in hits)
                {
                    var def = r;
                    var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 180 });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var name = new TextBlock { Text = def.Name, Foreground = Ui.Res("B.Text"), FontSize = 12, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center };
                    row.Children.Add(name);
                    FrameworkElement ctl;
                    if (def.Kind == RuleKind.Bool)
                    {
                        var chips = new WrapPanel();
                        foreach (var (label, val) in new[] { ("Server default", (string)null), ("true", "true"), ("false", "false") })
                        {
                            var v = val;
                            var b = Ui.Button(label, () => { if (v == null) o.GameRules.Remove(def.Name); else o.GameRules[def.Name] = v; PaintRules(); }, "Btn.Chip");
                            b.Height = 26; b.Margin = new Thickness(0, 0, 4, 0);
                            bool on = RuleValue(def.Name) == v;
                            b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                            b.ToolTip = v == null ? "Leave " + def.Name + " at the server's own default." : "Set " + def.Name + " to " + v + " once the world exists.";
                            AutomationProperties.SetName(b, def.Name + " " + label);
                            chips.Children.Add(b);
                        }
                        ctl = chips;
                    }
                    else if (def.Kind == RuleKind.Integer)
                    {
                        var box = Input(RuleValue(def.Name) ?? "", 110, "A whole number. Empty keeps the server default for " + def.Name + ".");
                        AutomationProperties.SetName(box, def.Name + " value");
                        box.LostFocus += (a, b) =>
                        {
                            var t = box.Text.Trim();
                            if (t.Length == 0) o.GameRules.Remove(def.Name);
                            else if (int.TryParse(t, out _)) o.GameRules[def.Name] = t;
                            else box.Text = RuleValue(def.Name) ?? "";
                        };
                        ctl = box;
                    }
                    else ctl = Ui.Mono("type " + def.ParserType + " is not supported here", Ui.Res("B.TextMute"), 11);
                    Grid.SetColumn(ctl, 1); row.Children.Add(ctl);
                    SearchScope.SetItem(row, "game rule " + def.Name);
                    rulesHost.Children.Add(row);
                }
                rulesFilter.SetCount(hits.Count, f.GameRules.Count, "rules");
            }
            rulesFilter.Filter += PaintRules;
            var rulesScroll = new ScrollViewer { Content = rulesHost, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var resetRules = Ui.Button("Reset all rules to server defaults", () => { o.GameRules.Clear(); PaintRules(); }, "Btn.Ghost", Icons.Restart);
            resetRules.ToolTip = "Forgets every rule you changed here. Nothing is written until you generate.";
            rulesPanel.Children.Add(Ui.Mono("GAME RULES (only the ones you change are set)", Ui.Res("B.TextMute"), 11));
            var rulesNote = Ui.Mono("The jar's command list gives each rule's name and type but not its default, so a rule left on “Server default” is not touched. Rules are applied to a world this tool generates; they are not written to an existing server.", Ui.Res("B.TextMute"), 11);
            rulesNote.TextWrapping = TextWrapping.Wrap; rulesNote.Margin = new Thickness(0, 2, 0, 4);
            rulesPanel.Children.Add(rulesNote);
            rulesPanel.Children.Add(rulesFilter); rulesPanel.Children.Add(rulesScroll); rulesPanel.Children.Add(resetRules);

            flatStatus = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            flatStatus.TextWrapping = TextWrapping.Wrap; flatStatus.Margin = new Thickness(0, 4, 0, 4);
            void CheckFlat()
            {
                if (o.FlatLayers.Count == 0) { flatStatus.Text = "No custom layers: the version's own default superflat layers are used."; flatStatus.Foreground = Ui.Res("B.TextMute"); return; }
                var bad = WorldGenerator.ValidateFlat(o.FlatLayers, o.FlatBiome, _wgFacts?.Available == true ? _wgFacts.Blocks : null);
                int total = o.FlatLayers.Sum(l => Math.Max(0, l.Height));
                var checkedAgainst = _wgFacts?.Available == true ? "Block names were checked against this version's jar." : "Block names are checked for format only; read the jar to check them against this version.";
                if (bad.Count == 0) { flatStatus.Text = $"Valid: {o.FlatLayers.Count} layers, {total} blocks tall (limit {WorldGenerator.OverworldHeight}). {checkedAgainst}"; flatStatus.Foreground = Ui.Res("B.Mint"); }
                else { flatStatus.Text = string.Join("\n", bad.Take(5)) + (bad.Count > 5 ? $"\n…and {bad.Count - 5} more." : ""); flatStatus.Foreground = Ui.Res("B.Amber"); }
            }

            void PaintFlat()
            {
                flatPanel.Children.Clear();
                if (o.Type != WorldType.Flat) return;
                flatPanel.Children.Add(Ui.Mono("SUPERFLAT LAYERS (bottom to top)", Ui.Res("B.TextMute"), 11));
                if (current != null && !current.DataDriven)
                {
                    flatPanel.Children.Add(Ui.Text("Unavailable: custom layers need data-driven world presets, which arrived with Java 1.19. This version uses its default superflat layers."));
                    return;
                }
                var note = Ui.Mono("Optional. Leave the list empty to use the version's default layers. Layers are written as a data pack built from the jar's own flat preset, so the rest of the preset stays as the version defines it.", Ui.Res("B.TextMute"), 11);
                note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 2, 0, 6);
                flatPanel.Children.Add(note);
                for (int i = 0; i < o.FlatLayers.Count; i++)
                {
                    int idx = i; var layer = o.FlatLayers[i];
                    var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
                    var blk = Input(layer.Block, 280, "A block id such as minecraft:grass_block");
                    AutomationProperties.SetName(blk, $"Layer {i + 1} block");
                    blk.TextChanged += (a, b) => { layer.Block = blk.Text.Trim(); CheckFlat(); };
                    var hgt = Input(layer.Height.ToString(), 70, "Height in blocks, 1 to 384");
                    AutomationProperties.SetName(hgt, $"Layer {i + 1} height");
                    hgt.TextChanged += (a, b) => { layer.Height = int.TryParse(hgt.Text, out var h) ? h : 0; CheckFlat(); };
                    var up = Ui.Button("Up", () => { if (idx > 0) { (o.FlatLayers[idx - 1], o.FlatLayers[idx]) = (o.FlatLayers[idx], o.FlatLayers[idx - 1]); PaintFlat(); } }, "Btn.Ghost");
                    var down = Ui.Button("Down", () => { if (idx < o.FlatLayers.Count - 1) { (o.FlatLayers[idx + 1], o.FlatLayers[idx]) = (o.FlatLayers[idx], o.FlatLayers[idx + 1]); PaintFlat(); } }, "Btn.Ghost");
                    var del = Ui.Button("Remove", () => { o.FlatLayers.RemoveAt(idx); PaintFlat(); }, "Btn.Ghost", Icons.Close);
                    up.ToolTip = "Move this layer towards the bottom of the world."; down.ToolTip = "Move this layer towards the top."; del.ToolTip = "Remove this layer.";
                    AutomationProperties.SetName(up, $"Move layer {i + 1} down towards bedrock"); AutomationProperties.SetName(down, $"Move layer {i + 1} up"); AutomationProperties.SetName(del, $"Remove layer {i + 1}");
                    row.Children.Add(blk); row.Children.Add(hgt); row.Children.Add(up); row.Children.Add(down); row.Children.Add(del);
                    flatPanel.Children.Add(row);
                }
                var add = Ui.Button("Add layer", () => { o.FlatLayers.Add(new FlatLayer { Block = "minecraft:stone", Height = 1 }); PaintFlat(); }, "Btn.Ghost", Icons.Plus);
                add.ToolTip = "Adds a layer on top of the current list.";
                var classic = Ui.Button("Start from bedrock, dirt, grass", () =>
                {
                    o.FlatLayers.Clear();
                    o.FlatLayers.Add(new FlatLayer { Block = "minecraft:bedrock", Height = 1 });
                    o.FlatLayers.Add(new FlatLayer { Block = "minecraft:dirt", Height = 2 });
                    o.FlatLayers.Add(new FlatLayer { Block = "minecraft:grass_block", Height = 1 });
                    PaintFlat();
                }, "Btn.Ghost", Icons.Wand);
                classic.ToolTip = "Replaces the list with the classic superflat layout: bedrock, 2 dirt, grass.";
                var clear = Ui.Button("Use the version's default", () => { o.FlatLayers.Clear(); PaintFlat(); }, "Btn.Ghost", Icons.Restart);
                clear.ToolTip = "Empties the list so the version's own default layers are used.";
                var bar2 = new WrapPanel(); bar2.Children.Add(add); bar2.Children.Add(classic); bar2.Children.Add(clear);
                flatPanel.Children.Add(bar2);

                var biomeBox = Input(o.FlatBiome, 280, "A biome id such as minecraft:plains. Default minecraft:plains.");
                AutomationProperties.SetName(biomeBox, "Superflat biome");
                biomeBox.TextChanged += (a, b) => { o.FlatBiome = biomeBox.Text.Trim(); CheckFlat(); };
                flatPanel.Children.Add(Ui.Mono("SUPERFLAT BIOME (default minecraft:plains)", Ui.Res("B.TextMute"), 11));
                flatPanel.Children.Add(biomeBox);

                var presetBox = Input(o.FlatLayers.Count > 0 ? WorldGenerator.ExportFlatPreset(o.FlatLayers, o.FlatBiome) : "", 480, "A preset string such as minecraft:bedrock,2*minecraft:dirt,minecraft:grass_block;minecraft:plains");
                AutomationProperties.SetName(presetBox, "Superflat preset string");
                var presetNote = Ui.Mono("", Ui.Res("B.TextMute"), 11);
                presetNote.TextWrapping = TextWrapping.Wrap;
                var import = Ui.Button("Import string", () =>
                {
                    try
                    {
                        var (layers, biome, n) = WorldGenerator.ParseFlatPreset(presetBox.Text);
                        o.FlatLayers.Clear(); o.FlatLayers.AddRange(layers); o.FlatBiome = biome;
                        PaintFlat();
                        flatStatus.Text += n != null ? "\n" + n : "";
                    }
                    catch (FormatException ex) { presetNote.Text = ex.Message; presetNote.Foreground = Ui.Res("B.Amber"); }
                }, "Btn.Ghost", Icons.Plus);
                import.ToolTip = "Replaces the layers and biome with the ones in the string. Structure and feature options in the string are ignored.";
                var export = Ui.Button("Export string", () =>
                {
                    if (o.FlatLayers.Count == 0) { presetNote.Text = "There are no custom layers to export."; presetNote.Foreground = Ui.Res("B.Amber"); return; }
                    presetBox.Text = WorldGenerator.ExportFlatPreset(o.FlatLayers, o.FlatBiome);
                    try { Clipboard.SetText(presetBox.Text); presetNote.Text = "Written to the box and copied to the clipboard."; presetNote.Foreground = Ui.Res("B.Mint"); }
                    catch { presetNote.Text = "Written to the box (the clipboard was not available)."; presetNote.Foreground = Ui.Res("B.TextMute"); }
                }, "Btn.Ghost", Icons.Clipboard);
                export.ToolTip = "Builds the preset string from the layers and copies it. Layers and biome only; structure options are not included.";
                flatPanel.Children.Add(Ui.Mono("PRESET STRING (layers;biome)", Ui.Res("B.TextMute"), 11));
                flatPanel.Children.Add(presetBox);
                var pr = new WrapPanel(); pr.Children.Add(import); pr.Children.Add(export);
                flatPanel.Children.Add(pr); flatPanel.Children.Add(presetNote);
                flatPanel.Children.Add(flatStatus);
                CheckFlat();
            }


            void PaintJarParts() { PaintFlat(); PaintRules(); PaintDims(); wgEd?.Refresh(); }

            readJar.Click += async (a, b) =>
            {
                if (string.IsNullOrWhiteSpace(o.Version)) { jarStatus.Text = "Choose a version first."; jarStatus.Foreground = Ui.Res("B.Amber"); return; }
                readJar.IsEnabled = false; jarStatus.Foreground = Ui.Res("B.TextMute"); jarStatus.Text = "Reading the jar…";
                var ver = o.Version;
                try
                {
                    var facts = await WorldGenerator.JarFactsAsync(ver, t => Dispatcher.BeginInvoke(new Action(() => jarStatus.Text = t)));
                    if (ver == o.Version) { _wgFacts = facts; _wgFactsVersion = ver; }
                    jarStatus.Text = facts.Available
                        ? $"{ver}: {facts.GameRules.Count} game rules, {facts.Blocks.Count} blocks, execute in {(facts.HasExecuteIn ? "supported" : "not available")}, loaded-chunk test {(facts.HasLoadedTest ? "supported" : "not available")}."
                        : facts.Reason;
                    jarStatus.Foreground = Ui.Res(facts.Available ? "B.Mint" : "B.Amber");
                }
                catch (Exception ex) { jarStatus.Text = ex.Message; jarStatus.Foreground = Ui.Res("B.Amber"); }
                readJar.IsEnabled = true;
                PaintJarParts();
            };
            sp.Children.Add(flatPanel);
            sp.Children.Add(readJar);
            sp.Children.Add(jarStatus);
            sp.Children.Add(rulesPanel);
            wgEd = BuildWorldgenEditor(o, () => current, () => _wgFacts);
            sp.Children.Add(wgEd.Panel);

            void PaintType()
            {
                typePanel.Children.Clear();
                var singleOk = current?.DataDriven == true;
                var opts = new List<(string, WorldType)> { ("Normal", WorldType.Normal), ("Flat", WorldType.Flat), ("Large biomes", WorldType.LargeBiomes), ("Amplified", WorldType.Amplified) };
                if (singleOk) opts.Add(("Single biome", WorldType.SingleBiome));
                if (!singleOk && o.Type == WorldType.SingleBiome) o.Type = WorldType.Normal;
                typePanel.Children.Add(Choice("World type (default Normal)", opts, o.Type, v => { o.Type = v; PaintType(); },
                    "The kind of terrain generated. Single biome appears only for versions that support it. Default: Normal."));
                biomePanel.Children.Clear();
                if (o.Type == WorldType.SingleBiome) BuildBiomePicker();
                supportNote.Text = (current?.SingleBiomeReason != null ? "Single biome is hidden: " + current.SingleBiomeReason + "\n" : "") +
                                   (current?.ForceloadReason != null ? "Pre-generation unavailable: " + current.ForceloadReason : "");
                PaintJarParts();
            }

            void BuildBiomePicker()
            {
                biomePanel.Children.Add(Ui.Mono("BIOME (read from the server jar of this version)", Ui.Res("B.TextMute"), 11));
                var chosen = Ui.Mono("Chosen: " + o.Biome, Ui.Res("B.Mint"), 11);
                var host = new WrapPanel();
                var load = Ui.Button("Load biomes from the jar", null, "Btn.Ghost", Icons.Folder);
                var f = new SearchField("minecraft:worldgen-biomes", "Search biomes", compact: true) { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
                void Paint()
                {
                    host.Children.Clear();
                    if (_wgBiomes == null) return;
                    f.SampleSource = () => _wgBiomes;
                    var hits = _wgBiomes.Where(b => f.State.Matches(b)).ToList();
                    foreach (var b in hits.Take(150))
                    {
                        var id = b;
                        var btn = Ui.Button(id.Replace("minecraft:", ""), () => { o.Biome = id; chosen.Text = "Chosen: " + id; Paint(); }, "Btn.Chip");
                        btn.Height = 26; btn.Margin = new Thickness(0, 0, 4, 4);
                        bool on = id == o.Biome;
                        btn.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); btn.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                        host.Children.Add(btn);
                    }
                    f.SetCount(hits.Count, _wgBiomes.Count, "biomes");
                }
                f.Filter += Paint;
                load.ToolTip = "Downloads this version's server jar once (checked against Mojang's SHA-1) and lists its biomes.";
                load.Click += async (a, b) =>
                {
                    if (string.IsNullOrWhiteSpace(o.Version)) { chosen.Text = "Choose a version first."; chosen.Foreground = Ui.Res("B.Amber"); return; }
                    load.IsEnabled = false; chosen.Text = "Reading the jar…"; chosen.Foreground = Ui.Res("B.TextMute");
                    try
                    {
                        var (jar, _) = await WorldGenerator.EnsureJarAsync(o.Version, t => chosen.Text = t);
                        _wgBiomes = await Task.Run(() => WorldGenerator.Biomes(jar)); _wgBiomeJar = jar;
                        chosen.Text = _wgBiomes.Count == 0 ? "This jar lists no biomes." : $"Chosen: {o.Biome} · {_wgBiomes.Count} biomes in this jar";
                        chosen.Foreground = Ui.Res(_wgBiomes.Count == 0 ? "B.Amber" : "B.Mint");
                        Paint();
                    }
                    catch (Exception ex) { chosen.Text = ex.Message; chosen.Foreground = Ui.Res("B.Amber"); }
                    load.IsEnabled = true;
                };
                biomePanel.Children.Add(load);
                biomePanel.Children.Add(f);
                biomePanel.Children.Add(new ScrollViewer { Content = host, MaxHeight = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
                biomePanel.Children.Add(chosen);
                Paint();
            }

            async Task RefreshSupport()
            {
                if (string.IsNullOrWhiteSpace(o.Version)) { current = null; PaintType(); return; }
                try { current = await WorldGenerator.SupportAsync(o.Version); }
                catch (Exception ex) { current = null; supportNote.Text = "Could not read the version catalogue: " + ex.Message; }
                _wgBiomes = null;
                if (_wgFactsVersion != o.Version) { _wgFacts = null; jarStatus.Text = ""; }
                PaintType();
            }

            // ---- structures
            sp.Children.Add(Switch("Generate structures", "Villages, strongholds, temples and the rest. Default on. Written as generate-structures.", o.Structures, v => o.Structures = v));

            // ---- area
            var radius = Input(o.RadiusChunks.ToString(), 100, $"Half the side of the square in chunks, 0 to {WorldGenerator.MaxRadiusChunks}. 50 gives 100 x 100 chunks. 0 skips pre-generation.");
            AutomationProperties.SetName(radius, "Area radius in chunks");
            var centreX = Input(o.AreaCenterX.ToString(), 90, "Chunk X of the square's centre. Default 0. A chunk is 16 blocks, so chunk 10 starts at block 160.");
            var centreZ = Input(o.AreaCenterZ.ToString(), 90, "Chunk Z of the square's centre. Default 0.");
            AutomationProperties.SetName(centreX, "Area centre chunk X"); AutomationProperties.SetName(centreZ, "Area centre chunk Z");
            var picker = new ChunkAreaPicker { Margin = new Thickness(0, 4, 0, 6) };
            picker.SetSelection(o.AreaCenterX, o.AreaCenterZ, o.RadiusChunks);
            void Estimate()
            {
                var r = Math.Clamp(o.RadiusChunks, 0, WorldGenerator.MaxRadiusChunks);
                estimate.Text = r == 0
                    ? "No pre-generation: the world is created around spawn and grows as it is explored."
                    : $"{r * 2} x {r * 2} chunks = {r * 2 * 16} x {r * 2 * 16} blocks, {(long)r * r * 4:N0} chunks in {WorldGenerator.BatchCount(r)} batches of at most 256 chunks, centred on chunk {o.AreaCenterX}, {o.AreaCenterZ} (block {o.AreaCenterX * 16}, {o.AreaCenterZ * 16}) in each chosen dimension (the Overworld always, the Nether and the End if switched on below, with no coordinate scaling). Time and disk grow with the square of the radius.";
                PaintDims();
            }
            void ReadCentre()
            {
                if (int.TryParse(centreX.Text, out var x) && Math.Abs(x) <= 1800000) o.AreaCenterX = x; else centreX.Text = o.AreaCenterX.ToString();
                if (int.TryParse(centreZ.Text, out var z) && Math.Abs(z) <= 1800000) o.AreaCenterZ = z; else centreZ.Text = o.AreaCenterZ.ToString();
                picker.SetSelection(o.AreaCenterX, o.AreaCenterZ, o.RadiusChunks);
                Estimate();
            }
            radius.LostFocus += (a, b) =>
            {
                if (int.TryParse(radius.Text, out var r) && r >= 0 && r <= WorldGenerator.MaxRadiusChunks) o.RadiusChunks = r; else radius.Text = o.RadiusChunks.ToString();
                picker.SetSelection(o.AreaCenterX, o.AreaCenterZ, o.RadiusChunks);
                Estimate();
            };
            centreX.LostFocus += (a, b) => ReadCentre(); centreZ.LostFocus += (a, b) => ReadCentre();
            picker.SelectionChanged += (x, z, r) =>
            {
                o.AreaCenterX = x; o.AreaCenterZ = z; o.RadiusChunks = r;
                radius.Text = r.ToString(); centreX.Text = x.ToString(); centreZ.Text = z.ToString();
                Estimate();
            };
            sp.Children.Add(Ui.Mono("AREA TO PRE-GENERATE (default: radius 50 chunks around chunk 0, 0)", Ui.Res("B.TextMute"), 11));
            var areaNote = Ui.Mono("Drag on the grid to draw the square, or type its radius and centre. The grid shows chunk numbers and the 32-chunk region boundaries; it shows no terrain, because terrain cannot be known without running the game.", Ui.Res("B.TextMute"), 11);
            areaNote.TextWrapping = TextWrapping.Wrap; areaNote.Margin = new Thickness(0, 2, 0, 4);
            sp.Children.Add(areaNote);
            sp.Children.Add(picker);
            var areaRow = new WrapPanel();
            foreach (var (label, field) in new[] { ("RADIUS (CHUNKS)", radius), ("Centre chunk X", centreX), ("Centre chunk Z", centreZ) })
            {
                var cell = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
                cell.Children.Add(Ui.Mono(label, Ui.Res("B.TextMute"), 11)); cell.Children.Add(field);
                areaRow.Children.Add(cell);
            }
            var viewHome = Ui.Button("View 0, 0", () => picker.ResetView(), "Btn.Ghost", Icons.Restart);
            viewHome.ToolTip = "Scrolls the grid back to the world origin. The chosen square does not change.";
            var clearArea = Ui.Button("No area", () => { o.RadiusChunks = 0; radius.Text = "0"; picker.SetSelection(o.AreaCenterX, o.AreaCenterZ, 0); Estimate(); }, "Btn.Ghost", Icons.Close);
            clearArea.ToolTip = "Sets the radius to 0, so nothing is pre-generated.";
            areaRow.Children.Add(viewHome); areaRow.Children.Add(clearArea);
            sp.Children.Add(areaRow);
            estimate.TextWrapping = TextWrapping.Wrap; estimate.Margin = new Thickness(0, 0, 0, 8);
            sp.Children.Add(estimate);
            Estimate();
            dimNether = new CheckBox { Style = Ui.Style("Switch"), IsChecked = o.PreGenNether, Margin = new Thickness(0, 4, 0, 2), Content = new StackPanel { Children = { new TextBlock { Text = "Also pre-generate the Nether (default off)", Foreground = Ui.Res("B.Text"), FontSize = 12, FontFamily = Ui.Font("F.Body") }, new TextBlock { Text = "Runs the same bounded procedure in the Nether when the jar supports it. Off by default.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 0) } } } };
            dimEnd = new CheckBox { Style = Ui.Style("Switch"), IsChecked = o.PreGenEnd, Margin = new Thickness(0, 2, 0, 2), Content = new StackPanel { Children = { new TextBlock { Text = "Also pre-generate the End (default off)", Foreground = Ui.Res("B.Text"), FontSize = 12, FontFamily = Ui.Font("F.Body") }, new TextBlock { Text = "Runs the same bounded procedure in the End when the jar supports it. Off by default.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 0) } } } };
            dimNether.ToolTip = "Runs the same bounded forceload procedure in the Nether with execute in minecraft:the_nether, when the jar supports it.";
            dimEnd.ToolTip = "Runs the same bounded forceload procedure in the End with execute in minecraft:the_end, when the jar supports it.";
            dimNether.Checked += (a, b) => o.PreGenNether = true; dimNether.Unchecked += (a, b) => o.PreGenNether = false;
            dimEnd.Checked += (a, b) => o.PreGenEnd = true; dimEnd.Unchecked += (a, b) => o.PreGenEnd = false;
            sp.Children.Add(dimNether); sp.Children.Add(dimEnd); sp.Children.Add(dimNote);
            PaintDims();
            sp.Children.Add(Switch("Set the world border to this size", "Runs worldborder set after generation. Default off.", o.BorderToSize, v => o.BorderToSize = v));

            // ---- output
            sp.Children.Add(Choice("Save the world as (default folder)", new[] { ("Folder", false), ("ZIP file", true) }, o.AsZip, v => o.AsZip = v,
                "Write the finished world as a plain folder, or pack it into one ZIP file you can share. Default: folder."));
            var dest = Input(o.Destination, 420, "Where to put the new folder or ZIP. Existing files are never replaced; a numbered name is used instead.");
            dest.TextChanged += (a, b) => o.Destination = dest.Text.Trim();
            AutomationProperties.SetName(dest, "Output location");
            var browse = Ui.Button("Browse…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose where to save the world" };
                if (dlg.ShowDialog(this) == true) dest.Text = dlg.FolderName;
            }, "Btn.Ghost", Icons.Folder);
            browse.ToolTip = "Choose the destination folder.";
            var destRow = new WrapPanel(); destRow.Children.Add(dest); destRow.Children.Add(browse);
            sp.Children.Add(Ui.Mono("Destination", Ui.Res("B.TextMute"), 11));
            sp.Children.Add(destRow);

            // ---- EULA and actions
            var eula = new CheckBox { Style = Ui.Style("Switch"), IsChecked = o.EulaAccepted, Margin = new Thickness(0, 10, 0, 6) };
            var eulaText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text"), FontSize = 12, FontFamily = Ui.Font("F.Body") };
            eulaText.Inlines.Add(new System.Windows.Documents.Run("I accept the "));
            eulaText.Inlines.Add(Markdown.MakeLink("Minecraft EULA", "https://aka.ms/MinecraftEULA"));
            eulaText.Inlines.Add(new System.Windows.Documents.Run(" so a private server may run once to build this world"));
            eula.Content = new StackPanel { Children = { eulaText, new TextBlock { Text = "Required: the server that builds the world will not run without it. Off until you accept.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 0) } } };
            eula.Checked += (a, b) => o.EulaAccepted = true;
            eula.Unchecked += (a, b) => o.EulaAccepted = false;
            sp.Children.Add(eula);

            var bar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 8, 0, 6), Foreground = Ui.Res("B.Mint"), Background = Ui.Res("B.Sunken"), Visibility = Visibility.Collapsed };
            var status = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            status.TextWrapping = TextWrapping.Wrap;
            CancellationTokenSource cts = null;
            var generate = Ui.Button("Generate world", null, "Btn.Accent", Icons.Bolt);
            var cancel = Ui.Button("Cancel", () => cts?.Cancel(), "Btn.Ghost", Icons.Close);
            cancel.IsEnabled = false;
            GenPause gpause = null;
            Button pause = null;
            pause = Ui.Button("Pause pre-generation", () =>
            {
                if (gpause == null) return;
                gpause.Paused = !gpause.Paused;
                var label = gpause.Paused ? "Resume pre-generation" : "Pause pre-generation";
                pause.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { new ContentControl { Content = Ui.Icon(gpause.Paused ? Icons.Play : Icons.Pause, Ui.Res("B.TextSoft"), 14), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center } } };
                AutomationProperties.SetName(pause, label);
                Core.Language.Refresh(pause);
                status.Text = gpause.Paused ? "Pausing after the batch being built. Nothing is lost; Resume carries on from the next batch." : "Resumed.";
                Core.Language.Refresh(status);
            }, "Btn.Ghost", Icons.Pause);
            AutomationProperties.SetName(pause, "Pause pre-generation");
            pause.ToolTip = "Holds the build between batches of chunks and carries on from the next batch when you resume. The private server stays up while paused.";
            pause.IsEnabled = false;
            var apply =Ui.Button("Write to server settings", null, "Btn.Ghost", Icons.Lines);
            apply.ToolTip = "Saves the seed, world type, structures and (for a local process server on Java 1.19 or newer) custom superflat layers into the chosen local server. No world is built. Game rules and extra dimensions apply only to a generated world.";
            generate.ToolTip = "Runs a private server on this computer to build the world, then saves it to the destination.";
            cancel.ToolTip = "Stops the build and deletes the temporary server folder.";

            apply.Click += async (a, b) =>
            {
                var s = servers.FirstOrDefault(x => x.Id == _wgServerId);
                if (s == null) { status.Text = "Choose one of your local servers above first."; status.Foreground = Ui.Res("B.Amber"); return; }
                try { status.Text = await WorldGenerator.ApplyToServerAsync(s, o); status.Foreground = Ui.Res("B.Mint"); }
                catch (Exception ex) { status.Text = ex.Message; status.Foreground = Ui.Res("B.Amber"); }
            };
            generate.Click += async (a, b) =>
            {
                if (string.IsNullOrWhiteSpace(o.Version)) { status.Text = "Choose a version first."; status.Foreground = Ui.Res("B.Amber"); return; }
                if (o.Type == WorldType.Flat && o.FlatLayers.Count > 0)
                {
                    var bad = WorldGenerator.ValidateFlat(o.FlatLayers, o.FlatBiome, _wgFacts?.Available == true ? _wgFacts.Blocks : null);
                    if (bad.Count > 0) { status.Text = "Fix the superflat layers first: " + bad[0]; status.Foreground = Ui.Res("B.Amber"); return; }
                }
                generate.IsEnabled = false; cancel.IsEnabled = true; bar.Visibility = Visibility.Visible; bar.Value = 0;
                pause.IsEnabled = o.RadiusChunks > 0;
                status.Foreground = Ui.Res("B.TextMute");
                cts = new CancellationTokenSource();
                gpause = new GenPause();
                var pausable = gpause;
                try
                {
                    var path = await Task.Run(() => WorldGenerator.GenerateAsync(o, (t, f) => Dispatcher.BeginInvoke(new Action(() => { status.Text = t; bar.Value = f; })), cts.Token, pausable));
                    status.Text = "✓ Saved to " + path; status.Foreground = Ui.Res("B.Mint"); Sounds.Play(Cue.Complete);
                }
                catch (OperationCanceledException) { status.Text = "Cancelled. The temporary server folder was removed."; status.Foreground = Ui.Res("B.TextMute"); }
                catch (Exception ex) { status.Text = ex.Message; status.Foreground = Ui.Res("B.Amber"); Sounds.Play(Cue.Warning); }
                bar.Visibility = Visibility.Collapsed; generate.IsEnabled = true; cancel.IsEnabled = false;
                gpause = null; pause.IsEnabled = false;
                pause.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { new ContentControl { Content = Ui.Icon(Icons.Pause, Ui.Res("B.TextSoft"), 14), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = "Pause pre-generation", VerticalAlignment = VerticalAlignment.Center } } };
                AutomationProperties.SetName(pause, "Pause pre-generation");
                Core.Language.Refresh(pause);
            };
            var actions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            actions.Children.Add(generate); actions.Children.Add(pause); actions.Children.Add(cancel); actions.Children.Add(apply);
            sp.Children.Add(actions);
            sp.Children.Add(bar);
            sp.Children.Add(status);
            sp.Children.Add(Ui.Mono("There is no preview. Terrain cannot be drawn without running the game's own generator, so what you get is the real output of the server for the chosen version.", Ui.Res("B.TextMute"), 11));

            if (!string.IsNullOrWhiteSpace(o.Version)) _ = RefreshSupport(); else PaintType();
            if (string.IsNullOrWhiteSpace(o.Version) && _wgServerId == null)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var latest = (await ServerManager.VersionsAsync()).FirstOrDefault(v => v.Type == "release");
                        if (latest != null) await Dispatcher.BeginInvoke(new Action(() => { if (string.IsNullOrWhiteSpace(o.Version)) { o.Version = latest.Id; versionBox.Text = latest.Id; _ = RefreshSupport(); } }));
                    }
                    catch { /* offline: the version box stays empty and says so on Generate */ }
                });
            return Card(sp, "#FF2B4A3F");
        }
    }
}
