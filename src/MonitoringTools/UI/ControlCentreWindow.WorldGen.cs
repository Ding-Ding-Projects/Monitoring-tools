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

            // ---- server and version
            var servers = ServerManager.Servers.Where(s => s.Target == Target.LocalProcess || s.Target == Target.LocalDocker).ToList();
            var versionBox = Input(o.Version ?? "", 200, "A Java edition version id such as 1.21.4. Default: the newest release.");
            AutomationProperties.SetName(versionBox, "World version");
            sp.Children.Add(Ui.Mono("WHICH SERVER", Ui.Res("B.TextMute"), 11));
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
            sp.Children.Add(Ui.Mono("VERSION", Ui.Res("B.TextMute"), 11));
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

            void PaintType()
            {
                typePanel.Children.Clear();
                var singleOk = current?.DataDriven == true;
                var opts = new List<(string, WorldType)> { ("Normal", WorldType.Normal), ("Flat", WorldType.Flat), ("Large biomes", WorldType.LargeBiomes), ("Amplified", WorldType.Amplified) };
                if (singleOk) opts.Add(("Single biome", WorldType.SingleBiome));
                if (!singleOk && o.Type == WorldType.SingleBiome) o.Type = WorldType.Normal;
                typePanel.Children.Add(Choice("World type (default Normal)", opts, o.Type, v => { o.Type = v; PaintType(); }));
                biomePanel.Children.Clear();
                if (o.Type == WorldType.SingleBiome) BuildBiomePicker();
                supportNote.Text = (current?.SingleBiomeReason != null ? "Single biome is hidden: " + current.SingleBiomeReason + "\n" : "") +
                                   (current?.ForceloadReason != null ? "Pre-generation unavailable: " + current.ForceloadReason : "");
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
                PaintType();
            }

            // ---- structures
            sp.Children.Add(Switch("Generate structures", "Villages, strongholds, temples and the rest. Default on. Written as generate-structures.", o.Structures, v => o.Structures = v));

            // ---- area
            var radius = Input(o.RadiusChunks.ToString(), 100, $"Half the side of the square in chunks, 0 to {WorldGenerator.MaxRadiusChunks}. 50 gives 100 x 100 chunks. 0 skips pre-generation.");
            AutomationProperties.SetName(radius, "Area radius in chunks");
            void Estimate()
            {
                var r = Math.Clamp(o.RadiusChunks, 0, WorldGenerator.MaxRadiusChunks);
                estimate.Text = r == 0
                    ? "No pre-generation: the world is created around spawn and grows as it is explored."
                    : $"{r * 2} x {r * 2} chunks = {r * 2 * 16} x {r * 2 * 16} blocks, {(long)r * r * 4:N0} chunks in {WorldGenerator.BatchCount(r)} batches of at most 256 chunks, centred on 0, 0. Overworld only; the Nether and the End are not pre-generated. Time and disk grow with the square of the radius.";
            }
            radius.LostFocus += (a, b) =>
            {
                if (int.TryParse(radius.Text, out var r) && r >= 0 && r <= WorldGenerator.MaxRadiusChunks) o.RadiusChunks = r; else radius.Text = o.RadiusChunks.ToString();
                Estimate();
            };
            sp.Children.Add(Ui.Mono("AREA RADIUS IN CHUNKS (default 50)", Ui.Res("B.TextMute"), 11));
            sp.Children.Add(radius);
            estimate.TextWrapping = TextWrapping.Wrap; estimate.Margin = new Thickness(0, 0, 0, 8);
            sp.Children.Add(estimate);
            Estimate();
            sp.Children.Add(Switch("Set the world border to this size", "Runs worldborder set after generation. Default off.", o.BorderToSize, v => o.BorderToSize = v));

            // ---- output
            sp.Children.Add(Choice("Save the world as (default folder)", new[] { ("Folder", false), ("ZIP file", true) }, o.AsZip, v => o.AsZip = v));
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
            sp.Children.Add(Ui.Mono("DESTINATION", Ui.Res("B.TextMute"), 11));
            sp.Children.Add(destRow);

            // ---- EULA and actions
            var eula = new CheckBox { Style = Ui.Style("Switch"), IsChecked = o.EulaAccepted, Margin = new Thickness(0, 10, 0, 6) };
            var eulaText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body") };
            eulaText.Inlines.Add(new System.Windows.Documents.Run("I accept the "));
            eulaText.Inlines.Add(Markdown.MakeLink("Minecraft EULA", "https://aka.ms/MinecraftEULA"));
            eulaText.Inlines.Add(new System.Windows.Documents.Run(" so a private server may run once to build this world"));
            eula.Content = eulaText;
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
            var apply = Ui.Button("Write to server settings", null, "Btn.Ghost", Icons.Lines);
            apply.ToolTip = "Saves the seed, world type and structures into the chosen local server's server.properties. No world is built.";
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
                generate.IsEnabled = false; cancel.IsEnabled = true; bar.Visibility = Visibility.Visible; bar.Value = 0;
                status.Foreground = Ui.Res("B.TextMute");
                cts = new CancellationTokenSource();
                try
                {
                    var path = await Task.Run(() => WorldGenerator.GenerateAsync(o, (t, f) => Dispatcher.BeginInvoke(new Action(() => { status.Text = t; bar.Value = f; })), cts.Token));
                    status.Text = "✓ Saved to " + path; status.Foreground = Ui.Res("B.Mint"); Sounds.Play(Cue.Complete);
                }
                catch (OperationCanceledException) { status.Text = "Cancelled. The temporary server folder was removed."; status.Foreground = Ui.Res("B.TextMute"); }
                catch (Exception ex) { status.Text = ex.Message; status.Foreground = Ui.Res("B.Amber"); Sounds.Play(Cue.Warning); }
                bar.Visibility = Visibility.Collapsed; generate.IsEnabled = true; cancel.IsEnabled = false;
            };
            var actions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            actions.Children.Add(generate); actions.Children.Add(cancel); actions.Children.Add(apply);
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
