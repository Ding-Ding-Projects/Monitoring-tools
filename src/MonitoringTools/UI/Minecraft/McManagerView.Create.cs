using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <summary>The answers of the create flow, kept while the person moves between steps, sections and pages.</summary>
    internal sealed class McCreateDraft
    {
        public bool Bedrock;
        public string Version;
        /// <summary>The Bedrock release Mojang links to right now (only the newest is offered), or null until it is read.</summary>
        public string BedrockVersion;
        public string BedrockProblem;
        public bool Snapshots, OldVersions;
        public Flavour Flavour = Flavour.Vanilla;
        public string Build = "";
        public Target Target = Target.LocalProcess;
        public string SshHost = "", RemoteDir = "";
        public string Name = "My server";
        public long MemoryMb = 2048, Port = 25565, MaxPlayers = 20;
        public string WorldMode = "new";   // new, existing, backup
        public string WorldSource;
        public string Seed = "";
        public string LevelType = "minecraft:normal";
        public bool Eula;
        public int Step;
    }

    // The six-step create flow: edition and version, software, where it runs, name and resources, world, review.
    internal sealed partial class McManagerView
    {
        internal static McCreateDraft Draft;
        private GuidedForm _form;
        internal GuidedForm CreateForm => _form;

        internal static string[] StepTitles = { "Edition and version", "Software", "Where it runs", "Name and resources", "World", "Review" };

        private FrameworkElement BuildCreate()
        {
            Draft ??= new McCreateDraft { Port = FreePort(25565) };
            var d = Draft;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var rail = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            AutomationProperties.SetName(rail, "Steps");
            g.Children.Add(rail);

            var steps = new List<GuidedStep>
            {
                new GuidedStep { Title = StepTitles[0], Help = "Every version in Mojang's official catalogue. Nothing is downloaded until the last step.", Build = () => VersionStep(d), Validate = () => d.Bedrock ? (d.BedrockVersion == null ? d.BedrockProblem ?? "Wait for Mojang's download link to be read." : null) : d.Version == null ? "Choose a version first." : null },
                new GuidedStep { Title = StepTitles[1], Help = "Only software with a build for the chosen version can be picked.", Build = () => Fresh(() => SoftwareStep(d)), Validate = () => SoftwareProblem(d) },
                new GuidedStep { Title = StepTitles[2], Help = "Each place is checked as you pick it.", Build = () => Fresh(() => TargetStep(d)), Validate = () => TargetProblem(d) },
                new GuidedStep { Title = StepTitles[3], Help = "Typed numbers with their limits. The port is checked against this computer.", Build = () => Fresh(() => ResourceStep(d)), Validate = () => ResourceProblem(d) },
                new GuidedStep { Title = StepTitles[4], Help = "Start with a new world, one you already have, or a backup of another server.", Build = () => WorldStep(d), Validate = () => WorldProblem(d) },
                new GuidedStep { Title = StepTitles[5], Help = "Check the answers and accept the EULA. Creating downloads the server and makes its folder.", Build = () => ReviewStep(d), Validate = () => d.Eula ? null : "Accept the EULA first; the server will not run without it." },
            };
            _form = new GuidedForm("New server", steps, "Create server", null, () => { Draft = null; CloseCreate(); }, d.Step);
            _form.Margin = new Thickness(0);
            Grid.SetColumn(_form, 1);
            g.Children.Add(_form);

            void Rail()
            {
                rail.Children.Clear();
                var cancel = Ui.Button("Cancel", () => { Draft = null; CloseCreate(); }, "Btn.Ghost", Icons.Close);
                cancel.HorizontalAlignment = HorizontalAlignment.Left;
                rail.Children.Add(cancel);
                rail.Children.Add(new TextBlock { Text = "New server", Style = Ui.Style("T.Title"), FontSize = 24, Margin = new Thickness(4, 6, 0, 14) });
                for (int i = 0; i < StepTitles.Length; i++)
                {
                    int at = i;
                    bool done = i < d.Step, cur = i == d.Step;
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var mark = new TextBlock { Text = done ? "✓" : (i + 1).ToString(), FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Ui.Res(done || cur ? "B.MintInk" : "B.TextSoft"), FontFamily = Ui.Font("F.Body") };
                    Core.Language.SetLiteral(mark, true);
                    row.Children.Add(new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(16), Background = Ui.Res(done || cur ? "B.Mint" : "B.Raised"), Child = mark, Margin = new Thickness(0, 0, 12, 0) });
                    var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    t.Children.Add(new TextBlock { Text = StepTitles[i], FontSize = 14, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                    var sum = done ? Summary(d, i) : "";
                    if (sum.Length > 0) { var st = McUi.Literal(sum, cur ? "B.OnSecondaryContainer" : "B.TextMute", 11.5); t.Children.Add(st); }
                    Grid.SetColumn(t, 1); row.Children.Add(t);
                    var b = McUi.Row(row, StepTitles[i] + (done ? ", done: " + sum : cur ? ", current step" : ""));
                    b.Background = (cur ? Ui.Res("B.SecondaryContainer") : System.Windows.Media.Brushes.Transparent);
                    b.Foreground = Ui.Res(cur ? "B.OnSecondaryContainer" : "B.Text");
                    b.IsEnabled = done || cur;
                    b.Click += (o, e) => _form.GoTo(at);
                    rail.Children.Add(b);
                    if (cur) Dispatcher.BeginInvoke(new Action(() => { var sh = McUi.Shape(b); if (sh != null) McUi.Morph(sh, 28); }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                rail.Children.Add(McUi.Body("Your answers are kept as a draft, so you can leave and come back.", "B.TextMute", 12));
                Core.Language.Refresh(rail);
            }
            _form.StepChanged += i => { d.Step = i; Rail(); };
            Rail();
            _form.FinishAsync = async () =>
            {
                var s = new ServerDef
                {
                    Name = d.Name.Trim(), Version = d.Bedrock ? d.BedrockVersion : d.Version, Flavour = d.Bedrock ? Flavour.Bedrock : d.Flavour, Build = d.Bedrock || d.Flavour == Flavour.Vanilla ? "" : d.Build, Target = d.Target,
                    Port = (int)d.Port, MemoryMb = (int)d.MemoryMb, EulaAccepted = d.Eula, SshHost = d.SshHost.Trim(), RemoteDir = d.RemoteDir.Trim()
                };
                var initial = new Dictionary<string, string> { ["max-players"] = d.MaxPlayers.ToString(CultureInfo.InvariantCulture) };
                if (d.WorldMode == "new") { initial["level-seed"] = d.Seed.Trim(); if (!d.Bedrock) initial["level-type"] = d.LevelType; }
                try
                {
                    await ServerManager.CreateAsync(s, t => Dispatcher.BeginInvoke(new Action(() => _form.Report(t))), initial, d.WorldMode == "new" ? null : d.WorldSource);
                    Sounds.Play(Cue.Complete);
                    Draft = null;
                    Dispatcher.BeginInvoke(new Action(() => CloseCreate(s.Id)));
                    return null;
                }
                catch (Exception ex) { Sounds.Play(Cue.Warning); return ex.Message; }
            };
            Ui.SharedAxis(g, 1);
            return g;
        }

        /// <summary>A step that is rebuilt each time it is shown, because it depends on answers from earlier steps.</summary>
        private static FrameworkElement Fresh(Func<FrameworkElement> build)
        {
            var host = new ContentControl { IsTabStop = false };
            host.Loaded += (o, e) => host.Content = build();
            return host;
        }

        private static string Summary(McCreateDraft d, int step) => step switch
        {
            0 => d.Bedrock ? "Bedrock " + d.BedrockVersion : "Java " + d.Version,
            1 => d.Bedrock ? "Bedrock Dedicated Server" : d.Flavour + (d.Build.Length > 0 && d.Flavour != Flavour.Vanilla ? " " + d.Build : ""),
            2 => McUi.TargetLabel(d.Target) + (d.Target == Target.SshProcess || d.Target == Target.SshDocker ? " · " + d.SshHost : ""),
            3 => d.Bedrock ? $"port {d.Port}" : $"{d.MemoryMb} MB · port {d.Port}",
            4 => d.WorldMode == "new" ? "New world" + (d.Seed.Length > 0 ? " · seed " + d.Seed : "") : Path.GetFileName(d.WorldSource ?? ""),
            _ => "",
        };

        internal static int FreePort(int from)
        {
            int p = from;
            while (p < 65535 && (ServerManager.Servers.Any(s => s.Port == p && s.IsLocal) || !ServerManager.PortFree(p))) p++;
            return p;
        }

        /// <summary>A free UDP port pair for Bedrock (the IPv6 port is the next one), starting at the given one.</summary>
        internal static int FreeBedrockPort(int from)
        {
            int p = from;
            while (p < 65534 && (ServerManager.Servers.Any(s => s.IsLocal && (s.Port == p || s.Port == p + 1)) || !ServerManager.UdpPortFree(p) || !ServerManager.UdpPortFree(p + 1))) p += 2;
            return p;
        }

        // ---- step 1: edition and version

        private FrameworkElement VersionStep(McCreateDraft d)
        {
            var outer = new StackPanel();
            var below = new ContentControl { IsTabStop = false };
            outer.Children.Add(McUi.Choice("Edition", "Java edition is every version in Mojang's catalogue plus Paper, Fabric, Forge and others. Bedrock is the dedicated server for phones, consoles and Windows players.",
                new[] { ("Java edition", false), ("Bedrock edition", true) }, d.Bedrock, v =>
                {
                    if (d.Bedrock == v) return;
                    d.Bedrock = v;
                    d.Target = Target.LocalProcess;
                    d.Port = v ? FreeBedrockPort(McBedrock.DefaultPort) : FreePort(25565);
                    if (v) d.WorldMode = "new";
                    below.Content = v ? BedrockVersionPanel(d) : JavaVersionPanel(d);
                    Core.Language.Refresh(below);
                }));
            outer.Children.Add(below);
            below.Content = d.Bedrock ? BedrockVersionPanel(d) : JavaVersionPanel(d);
            return outer;
        }

        /// <summary>Bedrock: Mojang links only the newest Windows server, so that is shown and installed.</summary>
        private FrameworkElement BedrockVersionPanel(McCreateDraft d)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            var card = new StackPanel();
            card.Children.Add(McUi.Title("Bedrock Dedicated Server for Windows", 16));
            var note = McUi.Body(d.BedrockVersion != null ? "Version " + d.BedrockVersion + " from minecraft.net." : "Reading Mojang's download link…", d.BedrockVersion != null ? "B.Mint" : "B.TextMute", 13);
            AutomationProperties.SetLiveSetting(note, AutomationLiveSetting.Polite);
            card.Children.Add(note);
            card.Children.Add(McUi.Body("Mojang offers only the newest release of the Bedrock server, so that is the one installed. Players connect over UDP. On this computer it runs as a Windows process; Mojang publishes no checksum for it, so the server program must carry Microsoft's signature before it is installed. Over SSH it is Mojang's Linux build, unpacked on the host (Mojang publishes no checksum or signature for it, so the download is held to HTTPS on minecraft.net and nothing more). In Docker it is the community image itzg/minecraft-bedrock-server, which downloads the server itself.", "B.TextSoft", 12.5));
            sp.Children.Add(new Border { Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 12, 16, 12), Child = card, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left });
            if (d.BedrockVersion == null)
                _ = McBedrock.LatestAsync().ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted) { d.BedrockProblem = "Mojang's download link could not be read: " + t.Exception.InnerException.Message; note.Text = d.BedrockProblem; note.Foreground = Ui.Res("B.Amber"); }
                    else { d.BedrockVersion = t.Result.Version; d.BedrockProblem = null; note.Text = "Version " + d.BedrockVersion + " from minecraft.net."; note.Foreground = Ui.Res("B.Mint"); }
                    Core.Language.Refresh(note);
                })));
            return sp;
        }

        private FrameworkElement JavaVersionPanel(McCreateDraft d)
        {
            var sp = new StackPanel();
            var filter = new SearchField("minecraft:versions", "Search versions", compact: true) { Margin = new Thickness(0, 4, 0, 6) };
            sp.Children.Add(filter);
            var toggles = new WrapPanel();
            sp.Children.Add(toggles);
            var list = new StackPanel();
            AutomationProperties.SetName(list, "Versions");
            sp.Children.Add(new ScrollViewer { Content = list, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var note = McUi.Body(d.Version == null ? "No version chosen yet." : "Chosen: " + d.Version, d.Version == null ? "B.TextMute" : "B.Mint", 12.5);
            note.Margin = new Thickness(0, 8, 0, 0);
            AutomationProperties.SetLiveSetting(note, AutomationLiveSetting.Polite);
            sp.Children.Add(note);
            List<McVersion> all = null;

            void Toggles()
            {
                toggles.Children.Clear();
                toggles.Children.Add(McUi.Chip("Show snapshots", d.Snapshots, () => { d.Snapshots = !d.Snapshots; Toggles(); Fill(); }));
                toggles.Children.Add(McUi.Chip("Show old alpha and beta", d.OldVersions, () => { d.OldVersions = !d.OldVersions; Toggles(); Fill(); }));
                Core.Language.Refresh(toggles);
            }

            void Fill()
            {
                list.Children.Clear();
                if (all == null) { list.Children.Add(McUi.Body("Loading Mojang's catalogue…", "B.TextMute", 12.5)); return; }
                var ofType = all.Where(v => v.Type == "release" || (d.Snapshots && v.Type == "snapshot") || (d.OldVersions && (v.Type == "old_alpha" || v.Type == "old_beta"))).ToList();
                filter.SampleSource = () => ofType.Select(v => v.Id);
                var hits = ofType.Where(v => filter.State.Matches(v.Id)).ToList();
                filter.SetCount(hits.Count, ofType.Count, "versions");
                foreach (var v in hits.Take(80))
                {
                    var vv = v;
                    var g = new Grid();
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var id = McUi.Literal(v.Id, "B.Text", 14.5, mono: true);
                    g.Children.Add(id);
                    var meta = new TextBlock { Text = v.Type.Replace("old_", "old ") + " · " + v.Released.ToString("d MMM yyyy", CultureInfo.InvariantCulture), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(meta, 1); g.Children.Add(meta);
                    var row = McUi.Row(g, "Version " + v.Id);
                    bool sel = v.Id == d.Version;
                    row.Background = (sel ? Ui.Res("B.SecondaryContainer") : System.Windows.Media.Brushes.Transparent);
                    row.Foreground = Ui.Res(sel ? "B.OnSecondaryContainer" : "B.Text");
                    row.Margin = new Thickness(0, 0, 0, 2);
                    row.Click += async (o, e) =>
                    {
                        d.Version = vv.Id; d.Build = "";
                        Fill();
                        note.Text = $"Chosen: {vv.Id} · checking for a dedicated server…"; note.Foreground = Ui.Res("B.TextMute"); Core.Language.Refresh(note);
                        try
                        {
                            var dist = await ServerManager.ResolveAsync(vv.Id);
                            note.Text = dist.Missing ?? $"Chosen: {vv.Id} · server {dist.Size / 1048576.0:0.0} MB · needs Java {dist.JavaMajor}";
                            note.Foreground = Ui.Res(dist.Missing == null ? "B.Mint" : "B.Amber");
                        }
                        catch (Exception ex) { note.Text = "Could not read the version metadata: " + ex.Message; note.Foreground = Ui.Res("B.Amber"); }
                        Core.Language.Refresh(note);
                    };
                    list.Children.Add(row);
                    if (sel) Dispatcher.BeginInvoke(new Action(() => { var sh = McUi.Shape(row); if (sh != null) sh.CornerRadius = new CornerRadius(28); }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                if (hits.Count == 0) list.Children.Add(McUi.Body("No versions match.", "B.TextMute", 12.5));
                Core.Language.Refresh(list);
            }
            filter.Filter += Fill;
            Toggles();
            Fill();
            _ = Load();
            async Task Load()
            {
                try { all = await ServerManager.VersionsAsync(); }
                catch (Exception ex) { list.Children.Clear(); list.Children.Add(McUi.Body("Could not load the official catalogue: " + ex.Message, "B.Amber", 12.5)); return; }
                Fill();
            }
            return sp;
        }

        // ---- step 2: software

        private static readonly Dictionary<string, McBuildList> SoftwareSeen = new Dictionary<string, McBuildList>();

        private string SoftwareProblem(McCreateDraft d)
        {
            if (d.Bedrock || d.Flavour == Flavour.Vanilla) return null;
            if (!SoftwareSeen.TryGetValue(d.Flavour + "|" + d.Version, out var l)) return $"Wait for the {d.Flavour} build list, or choose Vanilla.";
            if (l.Unavailable != null) return l.Unavailable;
            if (string.IsNullOrEmpty(d.Build)) d.Build = l.Latest?.Id ?? "";
            return null;
        }

        private FrameworkElement SoftwareStep(McCreateDraft d)
        {
            var sp = new StackPanel();
            if (d.Bedrock)
            {
                sp.Children.Add(McUi.Title($"Server software for Bedrock {d.BedrockVersion}", 18));
                var card = new StackPanel { Width = 320 };
                card.Children.Add(new TextBlock { Text = "Bedrock Dedicated Server", FontSize = 16, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body") });
                card.Children.Add(new TextBlock { Text = "Mojang's own server for Bedrock players on phones, consoles and Windows.", FontSize = 12, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Opacity = 0.85, Margin = new Thickness(0, 4, 0, 6) });
                var b = new Border { Background = Ui.Res("B.SecondaryContainer"), CornerRadius = new CornerRadius(20), Padding = new Thickness(16, 14, 16, 14), Margin = new Thickness(0, 10, 0, 0), Child = card, HorizontalAlignment = HorizontalAlignment.Left };
                System.Windows.Documents.TextElement.SetForeground(b, Ui.Res("B.OnSecondaryContainer"));
                sp.Children.Add(b);
                sp.Children.Add(McUi.Body("Bedrock has no other server software; plugins and mods are for Java edition.", "B.TextMute", 12.5));
                return sp;
            }
            sp.Children.Add(McUi.Title($"Server software for {d.Version}", 18));
            var opts = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            sp.Children.Add(opts);
            var builds = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            sp.Children.Add(builds);

            void Paint()
            {
                opts.Children.Clear();
                foreach (Flavour f in new[] { Flavour.Vanilla, Flavour.Paper, Flavour.Purpur, Flavour.Fabric, Flavour.Forge, Flavour.NeoForge })
                {
                    SoftwareSeen.TryGetValue(f + "|" + d.Version, out var list);
                    string build = f == Flavour.Vanilla ? "from Mojang" : list == null ? "checking…" : list.Unavailable != null ? "no build for this version" : (f == Flavour.Fabric ? "loader " : f == Flavour.Paper || f == Flavour.Purpur ? "build " : "") + list.Latest?.Id;
                    bool ok = f == Flavour.Vanilla || (list != null && list.Unavailable == null);
                    var card = new StackPanel { Width = 168 };
                    card.Children.Add(new TextBlock { Text = f.ToString(), FontSize = 16, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body") });
                    card.Children.Add(new TextBlock { Text = McSoftware.Describe(f), FontSize = 12, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Opacity = 0.85, Margin = new Thickness(0, 4, 0, 6) });
                    var bt = new TextBlock { Text = build, FontSize = 12, FontFamily = Ui.Font("F.Mono"), Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
                    card.Children.Add(bt);
                    var b = McUi.Row(card, f + ": " + build);
                    bool sel = d.Flavour == f;
                    b.Background = Ui.Res(sel ? "B.SecondaryContainer" : "B.Surface");
                    b.Foreground = Ui.Res(sel ? "B.OnSecondaryContainer" : "B.Text");
                    b.Padding = new Thickness(16, 14, 16, 14);
                    b.Margin = new Thickness(0, 0, 10, 10);
                    b.IsEnabled = ok;
                    b.Opacity = ok ? 1 : 0.55;
                    AutomationProperties.SetItemStatus(b, sel ? "selected" : ok ? "" : "unavailable");
                    var ff = f;
                    b.Click += (o, e) => { d.Flavour = ff; d.Build = SoftwareSeen.TryGetValue(ff + "|" + d.Version, out var l) ? l.Latest?.Id ?? "" : ""; Paint(); };
                    opts.Children.Add(b);
                }
                builds.Children.Clear();
                if (d.Flavour != Flavour.Vanilla && SoftwareSeen.TryGetValue(d.Flavour + "|" + d.Version, out var chosen) && chosen.Unavailable == null)
                {
                    builds.Children.Add(McUi.Choice("Build", $"The newest {d.Flavour} builds for {d.Version}, newest first. The newest is the default.",
                        chosen.Builds.Take(8).Select(b => (b.Id + (b.Channel != null && b.Channel != "default" && b.Channel != "release" && b.Channel != "stable" ? " (" + b.Channel + ")" : ""), b.Id)), d.Build, v => d.Build = v, literal: true));
                }
                if (!McSoftware.RunsAsLocalProcess(d.Flavour))
                    builds.Children.Add(McUi.Body($"{d.Flavour} runs in Docker, on this computer or over SSH, because it needs its own installer.", "B.TextMute", 12.5));
                Core.Language.Refresh(sp);
            }
            Paint();
            foreach (Flavour f in new[] { Flavour.Paper, Flavour.Purpur, Flavour.Fabric, Flavour.Forge, Flavour.NeoForge })
            {
                var ff = f;
                if (SoftwareSeen.ContainsKey(f + "|" + d.Version)) continue;
                _ = Task.Run(() => McSoftware.BuildsAsync(ff, d.Version)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                {
                    SoftwareSeen[ff + "|" + d.Version] = t.IsFaulted ? new McBuildList { Unavailable = t.Exception.InnerException.Message } : t.Result;
                    Paint();
                })));
            }
            return sp;
        }

        // ---- step 3: where it runs

        private static readonly Dictionary<Target, string> Readiness = new Dictionary<Target, string>();
        private static readonly Dictionary<Target, bool> Ready = new Dictionary<Target, bool>();

        private string TargetProblem(McCreateDraft d)
        {
            if ((d.Target == Target.SshProcess || d.Target == Target.SshDocker) && string.IsNullOrWhiteSpace(d.SshHost)) return "Enter the SSH host.";
            if (d.Target == Target.LocalProcess && !d.Bedrock && !McSoftware.RunsAsLocalProcess(d.Flavour)) return $"{d.Flavour} runs in Docker. Choose a Docker target, or go back and choose Vanilla, Paper or Purpur.";
            if (d.Target == Target.SshProcess && d.Flavour != Flavour.Vanilla && !d.Bedrock) return "A process over SSH runs Vanilla. Choose Docker over SSH for other software.";
            if (d.Target == Target.LocalDocker && Ready.TryGetValue(Target.LocalDocker, out var ok) && !ok) return Readiness[Target.LocalDocker];
            return null;
        }

        private FrameworkElement TargetStep(McCreateDraft d)
        {
            var sp = new StackPanel();
            var opts = new WrapPanel();
            sp.Children.Add(opts);
            var sshBox = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            sp.Children.Add(sshBox);

            void Check(Target t)
            {
                if (Readiness.ContainsKey(t) && t != Target.SshProcess && t != Target.SshDocker) return;
                Readiness[t] = "Checking…";
                _ = Task.Run(async () =>
                {
                    string msg; bool ok;
                    switch (t)
                    {
                        case Target.LocalProcess:
                        {
                            if (d.Bedrock) { msg = "The Windows Bedrock server runs here. It needs no Java."; ok = true; break; }
                            if (d.Version == null) { msg = "Choose a version first."; ok = false; break; }
                            var dist = await ServerManager.ResolveAsync(d.Version);
                            var have = Directory.Exists(Path.Combine(Settings.DataDir, "runtimes", "jre-" + dist.JavaMajor));
                            ok = dist.Missing == null && McSoftware.RunsAsLocalProcess(d.Flavour);
                            msg = dist.Missing ?? (!McSoftware.RunsAsLocalProcess(d.Flavour) ? $"{d.Flavour} needs Docker." : have ? $"Java {dist.JavaMajor} is ready on this computer." : $"Java {dist.JavaMajor} will be downloaded for it (Eclipse Temurin).");
                            break;
                        }
                        case Target.LocalDocker:
                        {
                            var r = ServerManager.Run("docker", "info --format \"{{.ServerVersion}}\"", 15000);
                            ok = r.Code == 0; msg = ok ? $"Docker {r.Out.Trim()} is running." : "Docker is not running on this computer.";
                            break;
                        }
                        default:
                        {
                            if (string.IsNullOrWhiteSpace(d.SshHost)) { ok = false; msg = "Enter the host below; its key is checked first."; break; }
                            var probe = new ServerDef { SshHost = d.SshHost.Trim() };
                            var r = ServerManager.Ssh(probe, t == Target.SshDocker ? "docker info --format '{{.ServerVersion}}'" : "command -v java >/dev/null && java -version 2>&1 | head -n1 || echo no-java", 25000);
                            ok = r.Code == 0 && !r.Out.Contains("no-java");
                            msg = r.Code == 255 || r.Code == -1 ? "The host could not be reached, or its key is not approved yet." : t == Target.SshDocker ? (ok ? $"Docker {r.Out.Trim()} is running on the host." : "Docker is not available on the host.") : (ok ? "Java is installed on the host." : "Java is not installed on the host.");
                            break;
                        }
                    }
                    return (msg, ok);
                }).ContinueWith(task => Dispatcher.BeginInvoke(new Action(() =>
                {
                    Readiness[t] = task.IsFaulted ? task.Exception.InnerException.Message : task.Result.msg;
                    Ready[t] = !task.IsFaulted && task.Result.ok;
                    Paint();
                })));
            }

            void Paint()
            {
                opts.Children.Clear();
                foreach (var (t, name, note, icon) in new[]
                {
                    (Target.LocalProcess, "This computer", "A process on this PC. Simplest.", Icons.Monitor),
                    (Target.LocalDocker, "Docker on this computer", "A container with its own folder for the world.", Icons.Box),
                    (Target.SshProcess, "Another computer over SSH", "A process on a machine you can sign in to with an SSH key.", Icons.Link),
                    (Target.SshDocker, "Docker over SSH", "A container on another machine.", Icons.Globe),
                })
                {
                    Readiness.TryGetValue(t, out var ready);
                    Ready.TryGetValue(t, out var ok);
                    var card = new StackPanel { Width = 240 };
                    var ic = Ui.Icon(icon, Ui.Res("B.Mint"), 20); ic.HorizontalAlignment = HorizontalAlignment.Left; card.Children.Add(ic);
                    card.Children.Add(new TextBlock { Text = name, FontSize = 16, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 8, 0, 2), HorizontalAlignment = HorizontalAlignment.Left });
                    card.Children.Add(new TextBlock { Text = note, FontSize = 12, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
                    card.Children.Add(new TextBlock { Text = ready ?? "Pick it to check.", FontSize = 12, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Foreground = Ui.Res(ready == null || ready == "Checking…" ? "B.TextMute" : ok ? "B.Mint" : "B.Amber") });
                    var b = McUi.Row(card, name + ". " + (ready ?? ""));
                    bool sel = d.Target == t;
                    b.Background = Ui.Res(sel ? "B.SecondaryContainer" : "B.Surface");
                    b.Foreground = Ui.Res(sel ? "B.OnSecondaryContainer" : "B.Text");
                    b.Padding = new Thickness(16, 14, 16, 14);
                    b.Margin = new Thickness(0, 0, 10, 10);
                    AutomationProperties.SetItemStatus(b, sel ? "selected" : "");
                    var tt = t;
                    b.Click += (o, e) => { d.Target = tt; if (tt == Target.SshProcess || tt == Target.SshDocker) Readiness.Remove(tt); Check(tt); Paint(); };
                    opts.Children.Add(b);
                }
                sshBox.Children.Clear();
                if (d.Target == Target.SshProcess || d.Target == Target.SshDocker)
                {
                    var host = McUi.Field(d.SshHost, "SSH host", 280, "user@host or user@host:port, signed in with an SSH key");
                    host.TextChanged += (o, e) => d.SshHost = host.Text.Trim();
                    var dir = McUi.Field(d.RemoteDir, "Folder on the host", 280, "Optional. Default: monitoring-tools-mc/<id> in the home folder");
                    dir.TextChanged += (o, e) => d.RemoteDir = dir.Text.Trim();
                    var row = new WrapPanel();
                    row.Children.Add(new StackPanel { Children = { new TextBlock { Text = "SSH host", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body") }, host } });
                    row.Children.Add(new StackPanel { Children = { new TextBlock { Text = "Folder on the host", FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body") }, dir } });
                    sshBox.Children.Add(row);
                    var hostNote = McUi.Body("", "B.TextMute", 12.5);
                    var btns = new WrapPanel();
                    btns.Children.Add(Ui.Button("Check host key", async () =>
                    {
                        if (string.IsNullOrWhiteSpace(d.SshHost)) { hostNote.Text = "Enter the host first."; Core.Language.Refresh(hostNote); return; }
                        hostNote.Text = "Reading the host key…"; Core.Language.Refresh(hostNote);
                        var (known, fp) = await Task.Run(() => ServerManager.HostKey(d.SshHost));
                        if (known) { hostNote.Text = "This host's key is already approved."; hostNote.Foreground = Ui.Res("B.Mint"); Core.Language.Refresh(hostNote); return; }
                        hostNote.Text = "Approve only if these match the host's own fingerprints:\n" + fp; hostNote.Foreground = Ui.Res("B.Amber");
                        var ok = new ConfirmWindow("Approve this host key?", "Compare these with the host's own fingerprints before approving.\n\n" + fp, "Approve", danger: false);
                        ok.Confirmed += _ => { ServerManager.ApproveHostKey(fp); hostNote.Text = "Host key approved for Minecraft servers."; hostNote.Foreground = Ui.Res("B.Mint"); Core.Language.Refresh(hostNote); };
                        ok.Show();
                    }, "Btn.Ghost", Icons.Shield));
                    btns.Children.Add(Ui.Button("Check the host", () => { Readiness.Remove(d.Target); Check(d.Target); Paint(); }, "Btn.Ghost", Icons.Restart));
                    sshBox.Children.Add(btns);
                    sshBox.Children.Add(hostNote);
                }
                Core.Language.Refresh(sp);
            }
            Check(d.Target);
            if (d.Target != Target.LocalDocker) Check(Target.LocalDocker);
            Paint();
            return sp;
        }

        // ---- step 4: name and resources

        private string ResourceProblem(McCreateDraft d)
        {
            if (string.IsNullOrWhiteSpace(d.Name)) return "Give the server a name.";
            if (d.MemoryMb < 512 || d.MemoryMb > 65536) return "Memory must be from 512 MB to 65536 MB.";
            if (d.Port < 1024 || d.Port > 65535) return "The port must be from 1024 to 65535.";
            if (d.MaxPlayers < 1 || d.MaxPlayers > 1000) return "Maximum players must be from 1 to 1000.";
            if (d.Target == Target.LocalProcess || d.Target == Target.LocalDocker)
            {
                if (ServerManager.Servers.Any(x => x.Port == d.Port && x.IsLocal)) return $"Another server here already uses port {d.Port}. Pick a different one.";
                if (d.Bedrock ? !ServerManager.UdpPortFree((int)d.Port) : !ServerManager.PortFree((int)d.Port)) return $"Port {d.Port} is in use by another program on this computer.";
            }
            return null;
        }

        private FrameworkElement ResourceStep(McCreateDraft d)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = "Name", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body") });
            var name = McUi.Field(d.Name, "Server name", 320, "Also used as the message of the day");
            name.TextChanged += (o, e) => d.Name = name.Text;
            sp.Children.Add(name);
            var check = McUi.Body("", "B.Mint", 13);
            AutomationProperties.SetLiveSetting(check, AutomationLiveSetting.Polite);
            long installed = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1048576;
            void Check()
            {
                var problems = new List<string>();
                bool local = d.Target == Target.LocalProcess || d.Target == Target.LocalDocker;
                if (local)
                {
                    if (ServerManager.Servers.Any(x => x.Port == d.Port && x.IsLocal)) problems.Add($"Port {d.Port} is used by another server here.");
                    else if (d.Bedrock ? !ServerManager.UdpPortFree((int)d.Port) : !ServerManager.PortFree((int)d.Port)) problems.Add($"Port {d.Port} is in use by another program.");
                    if (!d.Bedrock && d.MemoryMb > installed * 3 / 4) problems.Add($"{d.MemoryMb} MB is more than three quarters of the {installed} MB this computer has.");
                }
                check.Text = problems.Count > 0 ? string.Join(" ", problems) : d.Bedrock ? $"UDP port {d.Port} is free on this computer. The Bedrock server takes the memory it needs." : local ? $"Port {d.Port} is free on this computer. {installed / 1024} GB of memory is installed, so {d.MemoryMb} MB is comfortable." : "The port and memory are checked on the host when the server starts.";
                check.Foreground = Ui.Res(problems.Count > 0 ? "B.Amber" : "B.Mint");
                Core.Language.Refresh(check);
            }
            FrameworkElement Num(string label, string help, long value, long min, long max, long step, string unit, Action<long> set)
            {
                var g = new Grid { Margin = new Thickness(0, 12, 0, 0), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var t = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
                t.Children.Add(new TextBlock { Text = label, FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body") });
                t.Children.Add(McUi.Body(help, "B.TextSoft", 12.5));
                g.Children.Add(t);
                var nb = new McNumberBox(label, value, min, max, step, unit) { MinWidth = 200 };
                nb.ValueChanged += v => { set(v); Check(); };
                nb.Box.TextChanged += (o, e) => { set(nb.Valid ? nb.Value : -1); Check(); };
                Grid.SetColumn(nb, 1); g.Children.Add(nb);
                return g;
            }
            if (!d.Bedrock) sp.Children.Add(Num("Memory", "Memory for the Java server. 512 MB to 65536 MB. Default: 2048 MB.", d.MemoryMb, 512, 65536, 256, "MB", v => d.MemoryMb = v));
            sp.Children.Add(Num("Port", d.Bedrock ? "The UDP port players connect to; the next port is used for IPv6. 1024 to 65535. A free pair is suggested." : "The port players connect to. 1024 to 65535. A free one is suggested.", d.Port, 1024, 65535, 1, "", v => d.Port = v));
            sp.Children.Add(Num("Maximum players", "How many can be online at once. 1 to 1000. Default: 20.", d.MaxPlayers, 1, 1000, 1, "players", v => d.MaxPlayers = v));
            sp.Children.Add(new Border { Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 10, 16, 12), Margin = new Thickness(0, 16, 0, 0), Child = check, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left });
            Check();
            return sp;
        }

        // ---- step 5: world

        private string WorldProblem(McCreateDraft d)
        {
            if (d.WorldMode == "new") return d.Seed.Trim().Contains(' ') ? "A seed has no spaces." : null;
            if (d.Bedrock) return "A Bedrock server starts with a new world here. Import an existing world from its Worlds section after it is made.";
            if (d.Target == Target.SshProcess || d.Target == Target.SshDocker) return "An existing world can be copied to an SSH host after the server is made. Choose a new world here.";
            if (string.IsNullOrEmpty(d.WorldSource)) return d.WorldMode == "backup" ? "Choose a backup." : "Choose a world folder or ZIP.";
            return McWorlds.Validate(d.WorldSource).Problem;
        }

        private FrameworkElement WorldStep(McCreateDraft d)
        {
            var sp = new StackPanel();
            var host = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            void Paint()
            {
                host.Children.Clear();
                if (d.WorldMode == "new")
                {
                    host.Children.Add(new TextBlock { Text = "Seed (empty picks one at random)", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body") });
                    var seed = McUi.Field(d.Seed, "Seed", 320, "For example 4127803392");
                    seed.TextChanged += (o, e) => d.Seed = seed.Text;
                    host.Children.Add(seed);
                    if (!d.Bedrock) host.Children.Add(McUi.Choice("World type", "The generator for the new world. Default: normal.",
                        new[] { ("Normal", "minecraft:normal"), ("Superflat", "minecraft:flat"), ("Large biomes", "minecraft:large_biomes"), ("Amplified", "minecraft:amplified") }, d.LevelType, v => d.LevelType = v));
                }
                else if (d.WorldMode == "existing")
                {
                    var b = new WrapPanel();
                    b.Children.Add(Ui.Button("Choose a ZIP…", () =>
                    {
                        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "World ZIP (*.zip)|*.zip" };
                        if (dlg.ShowDialog(Window.GetWindow(this)) == true) { d.WorldSource = dlg.FileName; Paint(); }
                    }, "Btn.Ghost", Icons.Archive));
                    b.Children.Add(Ui.Button("Choose a folder…", () =>
                    {
                        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a world folder (the one with level.dat)" };
                        if (dlg.ShowDialog(Window.GetWindow(this)) == true) { d.WorldSource = dlg.FolderName; Paint(); }
                    }, "Btn.Ghost", Icons.Folder));
                    host.Children.Add(b);
                    if (d.WorldSource != null)
                    {
                        var (_, problem) = McWorlds.Validate(d.WorldSource);
                        host.Children.Add(McUi.Literal(PathDisplay.Shown(d.WorldSource), "B.TextSoft", 12, mono: true));
                        host.Children.Add(McUi.Body(problem ?? "It has a level.dat, so it is a Minecraft world. It is copied in when the server is made.", problem == null ? "B.Mint" : "B.Amber", 12.5));
                    }
                }
                else
                {
                    var any = false;
                    foreach (var other in ServerManager.Servers.Where(x => x.IsLocal))
                        foreach (var bk in McBackups.List(other.BackupFolder).Take(6))
                        {
                            any = true;
                            var bb = bk;
                            var row = McUi.Row(new TextBlock { Text = $"{other.Name} · {bk.When:yyyy-MM-dd HH:mm} · {bk.Reason} · {Ui.Bytes(bk.Size)}", FontFamily = Ui.Font("F.Body"), FontSize = 12 }, "Backup of " + other.Name + " from " + bk.When.ToString("yyyy-MM-dd HH:mm"));
                            Core.Language.SetLiteral(row, true);
                            bool sel = d.WorldSource == bk.Path;
                            row.Background = (sel ? Ui.Res("B.SecondaryContainer") : System.Windows.Media.Brushes.Transparent);
                            row.Click += (o, e) => { d.WorldSource = bb.Path; Paint(); };
                            host.Children.Add(row);
                        }
                    if (!any) host.Children.Add(McUi.Body("No backups of other servers on this computer yet.", "B.TextMute", 12.5));
                }
                Core.Language.Refresh(host);
            }
            sp.Children.Add(McUi.Choice("World", "A new world from a seed; a world you have as a folder or ZIP, checked and copied, never moved; or a backup of another server.",
                new[] { ("New world", "new"), ("Use a world I have", "existing"), ("From a backup", "backup") }, d.WorldMode, v => { d.WorldMode = v; d.WorldSource = null; Paint(); }));
            sp.Children.Add(host);
            Paint();
            return sp;
        }

        // ---- step 6: review

        private FrameworkElement ReviewStep(McCreateDraft d)
        {
            var sp = new StackPanel();
            var table = new StackPanel();
            void Row(string k, string v, bool literal = true)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 0) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.Children.Add(new TextBlock { Text = k, FontSize = 14, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body") });
                var vt = literal ? McUi.Literal(v, "B.Text", 13.5) : McUi.Body(v, "B.Text", 13.5);
                Grid.SetColumn(vt, 1); g.Children.Add(vt);
                table.Children.Add(new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 10, 0, 10), Child = g });
            }
            void Fill()
            {
                table.Children.Clear();
                Row("Name", d.Name);
                Row("Edition", d.Bedrock ? "Bedrock " + d.BedrockVersion : "Java " + d.Version);
                Row("Software", d.Bedrock ? "Bedrock Dedicated Server" : d.Flavour + (d.Flavour == Flavour.Vanilla ? "" : " " + d.Build));
                Row("Runs on", McUi.TargetLabel(d.Target) + (d.Target == Target.SshProcess || d.Target == Target.SshDocker ? " · " + d.SshHost : ""));
                if (!d.Bedrock) Row("Memory", d.MemoryMb + " MB");
                Row("Port", d.Port.ToString());
                Row("Maximum players", d.MaxPlayers.ToString());
                Row("World", d.WorldMode == "new" ? (d.Seed.Length > 0 ? "New world, seed " + d.Seed : "New world, random seed") + (d.Bedrock ? "" : " · " + d.LevelType) : PathDisplay.Shown(d.WorldSource ?? ""));
            }
            Fill();
            sp.Children.Add(new Border { Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(20), Padding = new Thickness(18, 4, 18, 4), Child = table, Margin = new Thickness(0, 0, 0, 14) });
            var eula = new CheckBox { Style = Ui.Style("Switch"), IsChecked = d.Eula, Margin = new Thickness(0, 4, 0, 6) };
            var eulaText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") };
            eulaText.Inlines.Add(new System.Windows.Documents.Run("I accept the "));
            eulaText.Inlines.Add(Markdown.MakeLink("Minecraft EULA", "https://aka.ms/MinecraftEULA"));
            eulaText.Inlines.Add(new System.Windows.Documents.Run(" for this server"));
            eula.Content = new StackPanel { Children = { eulaText, new TextBlock { Text = "Required: the server will not run without it. Off until you accept.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 0) } } };
            AutomationProperties.SetName(eula, "Accept the Minecraft EULA");
            eula.Checked += (o, e) => d.Eula = true;
            eula.Unchecked += (o, e) => d.Eula = false;
            sp.Children.Add(eula);
            sp.Loaded += (o, e) => Fill();
            return sp;
        }
    }
}
