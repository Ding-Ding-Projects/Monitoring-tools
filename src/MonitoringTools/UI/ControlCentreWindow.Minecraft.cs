using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        private ServerDef _draft;
        /// <summary>The new-server draft, so the self-test can choose a version without the network.</summary>
        internal ServerDef DraftForTest => _draft;
        private string _versionType = "release";
        private string _openConsole;
        private TextBlock _console;

        private FrameworkElement MinecraftPage()
        {
            var root = PageRoot("Minecraft", "Minecraft servers",
                "Create and run Java edition servers on this computer, in Docker, or on another machine over SSH. Versions come straight from Mojang's official catalogue.");

            if (ServerManager.Servers.Count > 0) root.Children.Add(ServerSummary());
            else root.Children.Add(NoServersCard());
            foreach (var s in ServerManager.Servers.ToList()) root.Children.Add(ServerCard(s));
            root.Children.Add(WorldGenCard());
            root.Children.Add(CreateCard());
            return root;
        }

        // -------------------------------------------------------------- a server

        private readonly HashSet<string> _openSettings = new HashSet<string>();

        /// <summary>
        /// One server: an icon tile, its name and metadata chips, a live status pill (the dot breathes while it runs),
        /// the main actions, then a terminal console with the command builder and a settings drawer, both opened on demand.
        /// Design: design/motion-and-pages/Minecraft.dc.html.
        /// </summary>
        private FrameworkElement ServerCard(ServerDef s)
        {
            var sp = new StackPanel();
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tile = Ui.IconChip(Icons.Box, Ui.Res("B.TextMute"), Ui.Res("B.Raised"), 52, 26);
            tile.CornerRadius = new CornerRadius(16);
            tile.Margin = new Thickness(0, 0, 16, 0);
            head.Children.Add(tile);
            var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock { Text = s.Name, Style = Ui.Style("T.Title"), FontSize = 20 });
            var meta = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            meta.Children.Add(Ui.Pill(s.Version ?? "?"));
            meta.Children.Add(Ui.Pill(s.Flavour.ToString()));
            meta.Children.Add(Ui.Pill(TargetLabel(s.Target) + (string.IsNullOrEmpty(s.SshHost) ? "" : " · " + s.SshHost)));
            meta.Children.Add(Ui.Pill(":" + s.Port, mono: true));
            title.Children.Add(meta);
            Grid.SetColumn(title, 1); head.Children.Add(title);

            // status pill: dot + word, tinted by state
            var (dot, setDot) = Ui.LiveDot(Ui.Res("B.TextMute"), false, 10);
            var statusText = new TextBlock { Text = "checking…", FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, FontFamily = Ui.Font("F.Body") };
            var pillRow = new StackPanel { Orientation = Orientation.Horizontal };
            pillRow.Children.Add(dot); pillRow.Children.Add(statusText);
            var pill = new Border { CornerRadius = new CornerRadius(999), Padding = new Thickness(4, 2, 14, 2), Background = Ui.Res("B.Raised"), Child = pillRow, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(pill, "Server status");
            Grid.SetColumn(pill, 2); head.Children.Add(pill);
            sp.Children.Add(head);
            _ = Refresh();
            async System.Threading.Tasks.Task Refresh()
            {
                var st = await ServerManager.StatusAsync(s);
                bool running = st == "running";
                bool warn = st.Contains("unreach") || st == "missing";
                statusText.Text = running ? "Running" : st.Length > 0 ? char.ToUpperInvariant(st[0]) + st.Substring(1) : st;
                statusText.Foreground = Ui.Res(running ? "B.OnPrimaryContainer" : warn ? "B.Amber" : "B.TextSoft");
                pill.Background = Ui.Res(running ? "B.MintWash" : warn ? "B.AmberWash" : "B.Raised");
                setDot(Ui.Res(running ? "B.Mint" : warn ? "B.Amber" : "B.TextMute"), running);
                tile.Background = Ui.Res(running ? "B.MintWash" : "B.Raised");
                if (tile.Child is System.Windows.Shapes.Path ic) ic.Stroke = Ui.Res(running ? "B.Mint" : "B.TextMute");
            }

            var result = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            result.Margin = new Thickness(0, 4, 0, 0);
            async void Do(string what, Func<System.Threading.Tasks.Task> act)
            {
                result.Text = what + "…"; result.Foreground = Ui.Res("B.TextMute");
                try { await act(); result.Text = "✓ " + what + " done"; result.Foreground = Ui.Res("B.Mint"); Sounds.Play(Cue.Success); }
                catch (Exception ex) { result.Text = ex.Message; result.Foreground = Ui.Res("B.Amber"); Sounds.Play(Cue.Warning); }
                await Refresh();
            }

            bool consoleOpen = _openConsole == s.Id, settingsOpen = _openSettings.Contains(s.Id);
            bool local = s.Target == Target.LocalProcess || s.Target == Target.LocalDocker;
            var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            actions.Children.Add(Ui.Button("Start", () => Do("Start", () => ServerManager.StartAsync(s, t => Dispatcher.BeginInvoke(new Action(() => result.Text = t)))), "Btn.Accent", Icons.Play));
            actions.Children.Add(Ui.Button("Stop", () => Do("Stop", () => ServerManager.StopAsync(s)), "Btn.Ghost", Icons.Pause));
            actions.Children.Add(Ui.Button("Restart", () => Do("Restart", async () => { await ServerManager.StopAsync(s); await ServerManager.StartAsync(s, null); }), "Btn.Ghost", Icons.Restart));
            actions.Children.Add(Ui.Button(consoleOpen ? "Hide console" : "Console", () => { _openConsole = consoleOpen ? null : s.Id; Navigate("minecraft", animate: false); }, "Btn.Ghost", Icons.Lines));
            if (local)
                actions.Children.Add(Ui.Button(settingsOpen ? "Hide settings" : "Settings", () => { if (!_openSettings.Remove(s.Id)) _openSettings.Add(s.Id); Navigate("minecraft", animate: false); }, "Btn.Ghost", Icons.Wand));
            actions.Children.Add(Ui.Button("Add plugin or mod…", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Plugin or mod (*.jar)|*.jar", Title = "Choose a plugin or mod" };
                if (dlg.ShowDialog(this) == true) { result.Text = ServerManager.InstallPlugin(s, dlg.FileName); result.Foreground = Ui.Res("B.TextSoft"); }
            }, "Btn.Ghost", Icons.Plus));
            if (local)
                actions.Children.Add(Ui.Button("Open folder", () => ActionProvider.Shell(s.Folder), "Btn.Ghost", Icons.Folder));
            actions.Children.Add(Ui.Button("Remove", () =>
            {
                var dlg = new ConfirmWindow($"Remove {s.Name}?", "This removes it from the list. Its world and files stay where they are, and a running server is stopped first.", "Remove", danger: true);
                dlg.Confirmed += async _ => { try { await ServerManager.StopAsync(s); } catch { } ServerManager.Servers.Remove(s); ServerManager.Save(); Navigate("minecraft", animate: false); };
                dlg.Show();
            }, "Btn.Danger", Icons.Trash));
            sp.Children.Add(actions);
            sp.Children.Add(result);

            // ---- console: a terminal panel with the live log and the typed command builder (a known command plus its arguments, never a shell)
            if (consoleOpen)
            {
                var term = new StackPanel();
                _console = new TextBlock { Style = Ui.Style("T.Mono"), FontSize = 11.5, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap };
                var logView = new ScrollViewer { Content = _console, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 280 };
                term.Children.Add(logView);
                var commands = new[] { "say", "list", "op", "deop", "whitelist add", "whitelist remove", "kick", "ban", "pardon", "time set", "weather", "difficulty", "gamemode", "save-all" };
                string chosen = "say";
                var chips = new WrapPanel { Margin = new Thickness(0, 12, 0, 4) };
                var prompt = new TextBlock { Text = "> say", FontFamily = Ui.Font("F.Mono"), FontSize = 12.5, Foreground = Ui.Res("B.Mint"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) };
                void PaintCmds()
                {
                    chips.Children.Clear();
                    foreach (var c in commands)
                    {
                        var cc = c;
                        var b = Ui.Button(c, () => { chosen = cc; prompt.Text = "> " + cc; PaintCmds(); }, "Btn.Chip");
                        b.Height = 28; b.Margin = new Thickness(0, 0, 4, 4); b.FontFamily = Ui.Font("F.Mono"); b.FontSize = 12;
                        bool on = c == chosen;
                        b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                        chips.Children.Add(b);
                    }
                }
                PaintCmds();
                term.Children.Add(chips);
                var arg = Input("", 260, "Arguments, for example a player name or 'day'");
                AutomationProperties.SetName(arg, "Command arguments");
                var send = Ui.Button("Send", async () =>
                {
                    var c = chosen + (string.IsNullOrWhiteSpace(arg.Text) ? "" : " " + arg.Text.Trim());
                    result.Text = await ServerManager.CommandAsync(s, c);
                    result.Foreground = Ui.Res("B.TextSoft");
                    arg.Text = "";
                }, "Btn.Accent", Icons.Bolt);
                var cmdRow = new WrapPanel();
                cmdRow.Children.Add(prompt); cmdRow.Children.Add(arg); cmdRow.Children.Add(send);
                term.Children.Add(cmdRow);
                var box = new Border { Background = Ui.Res("B.Ground"), CornerRadius = new CornerRadius(16), Padding = new Thickness(16), Margin = new Thickness(0, 12, 0, 0), Child = term };
                SearchScope.SetItem(box, "console command say list op kick ban time weather");
                sp.Children.Add(box);
                Ui.Expand(box);
                _ = Fill();
                async System.Threading.Tasks.Task Fill() { _console.Text = await ServerManager.LogTailAsync(s); logView.ScrollToEnd(); }
            }

            // ---- settings drawer (local targets keep server.properties here)
            if (local && settingsOpen)
            {
                var props = ServerManager.ReadProperties(s);
                var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 10, 0, 0) };
                foreach (var (key, label, kind, choices, min, max) in ServerManager.Properties)
                {
                    props.TryGetValue(key, out var val);
                    var k = key;
                    FrameworkElement control;
                    switch (kind)
                    {
                        case "switch":
                            control = Switch(label, key, val == "true", v => ServerManager.WriteProperties(s, new Dictionary<string, string> { [k] = v ? "true" : "false" }));
                            break;
                        case "choice":
                            control = Choice(label, choices.Select(c => (c, c)), val ?? choices[0], v => ServerManager.WriteProperties(s, new Dictionary<string, string> { [k] = v }));
                            break;
                        default:
                            var box = Input(val ?? "", 220, kind == "number" ? $"{min} to {max}" : label);
                            box.LostFocus += (o, e) =>
                            {
                                if (kind == "number" && (!int.TryParse(box.Text, out var n) || n < min || n > max)) { box.Text = val ?? ""; return; }
                                ServerManager.WriteProperties(s, new Dictionary<string, string> { [k] = box.Text });
                            };
                            var wrap = new StackPanel { Margin = new Thickness(0, 4, 12, 8) };
                            wrap.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 6) });
                            wrap.Children.Add(box);
                            control = wrap;
                            break;
                    }
                    grid.Children.Add(control);
                }
                var drawer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
                drawer.Children.Add(new TextBlock { Text = "Settings · saved to server.properties, applied on the next start", Style = Ui.Style("T.Eyebrow") });
                drawer.Children.Add(grid);
                sp.Children.Add(drawer);
                Ui.Expand(drawer);
            }
            return Card(sp, s.Target == Target.SshProcess || s.Target == Target.SshDocker ? "#FF3A2F5A" : null);
        }

        /// <summary>Page summary: how many servers there are and how many run right now (filled in as each status arrives).</summary>
        private FrameworkElement ServerSummary()
        {
            var row = new WrapPanel { Margin = new Thickness(0, 0, 0, Appearance.Gap(14)) };
            int total = ServerManager.Servers.Count;
            var running = Ui.Pill("checking…", Ui.Res("B.MintWash"), Ui.Res("B.OnPrimaryContainer"), size: 13);
            running.Padding = new Thickness(14, 6, 14, 7); running.CornerRadius = new CornerRadius(999);
            var count = Ui.Pill(total == 1 ? "1 server" : $"{total} servers", null, null, size: 13);
            count.Padding = new Thickness(14, 6, 14, 7); count.CornerRadius = new CornerRadius(999);
            row.Children.Add(running); row.Children.Add(count);
            _ = Count();
            async System.Threading.Tasks.Task Count()
            {
                int n = 0;
                foreach (var s in ServerManager.Servers.ToList())
                {
                    try { if (await ServerManager.StatusAsync(s) == "running") n++; } catch { }
                }
                ((TextBlock)running.Child).Text = $"{n} running";
            }
            return row;
        }

        /// <summary>Shown instead of server cards when there are none yet.</summary>
        private FrameworkElement NoServersCard()
        {
            var g = new StackPanel { Orientation = Orientation.Horizontal };
            var chip = Ui.IconChip(Icons.Box, Ui.Res("B.Mint"), Ui.Res("B.MintWash"), 52, 26);
            chip.CornerRadius = new CornerRadius(16);
            chip.Margin = new Thickness(0, 0, 16, 0);
            g.Children.Add(chip);
            var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = "No servers yet", Style = Ui.Style("T.Title"), FontSize = 18 });
            t.Children.Add(new TextBlock { Text = "Create one with the guide below. Nothing is downloaded until the last step.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap });
            g.Children.Add(t);
            return Card(g);
        }

        private static string TargetLabel(Target t) => t switch
        {
            Target.LocalProcess => "this computer", Target.LocalDocker => "Docker here", Target.SshProcess => "SSH host", _ => "Docker over SSH"
        };

        // -------------------------------------------------------------- create

        private int _mcStep;

        /// <summary>
        /// The new-server guide. The same choices as before, one step at a time: name and version, where it runs, memory and port,
        /// then a review with the EULA. The draft lives in <c>_draft</c> and the step in <c>_mcStep</c>, so a page rebuild resumes where you were.
        /// </summary>
        private FrameworkElement CreateCard()
        {
            _draft ??= new ServerDef { Name = "Weekend server", Port = NextPort() };
            var d = _draft;
            bool IsSsh() => d.Target == Target.SshProcess || d.Target == Target.SshDocker;
            bool IsDocker() => d.Target == Target.LocalDocker || d.Target == Target.SshDocker;

            // ---- step 1: name and version (the whole official catalogue, searchable and filtered by type)
            FrameworkElement BuildVersionStep()
            {
                var sp = new StackPanel();
                var name = Input(d.Name, 280, "Server name, also used as the message of the day");
                name.TextChanged += (o, e) => d.Name = name.Text;
                AutomationProperties.SetName(name, "Server name");
                sp.Children.Add(new StackPanel { Children = { Ui.Mono("NAME", Ui.Res("B.TextMute"), 11), name } });
                sp.Children.Add(Ui.Mono("VERSION", Ui.Res("B.TextMute"), 11));
                var typeRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
                var typeButtons = new List<(Button B, string T)>();
                void PaintTypes()
                {
                    foreach (var (b, t) in typeButtons)
                    {
                        bool on = _versionType == t;
                        b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                    }
                }
                var filter = new SearchField("minecraft:versions", "Search versions, for example 1.21", compact: true) { MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
                var list = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                var verNote = Ui.Mono(d.Version == null ? "No version chosen yet." : "Chosen: " + d.Version, Ui.Res(d.Version == null ? "B.TextMute" : "B.Mint"), 11);
                async void Versions()
                {
                    try
                    {
                        var all = await ServerManager.VersionsAsync();
                        var ofType = all.Where(v => v.Type == _versionType).ToList();
                        filter.SampleSource = () => ofType.Select(v => v.Id);
                        filter.State.TimedOut = false;
                        var matching = ofType.Where(v => filter.State.Matches(v.Id)).ToList();
                        filter.SetCount(matching.Count, ofType.Count, "versions");
                        var shown = matching.Take(120).ToList();
                        list.Children.Clear();
                        foreach (var v in shown)
                        {
                            var vv = v;
                            var b = Ui.Button(v.Id, async () =>
                            {
                                d.Version = vv.Id;
                                verNote.Text = $"Chosen: {vv.Id} · checking for a dedicated server…"; verNote.Foreground = Ui.Res("B.TextMute");
                                try
                                {
                                    var dist = await ServerManager.ResolveAsync(vv.Id);
                                    verNote.Text = dist.Missing ?? $"Chosen: {vv.Id} · server {dist.Size / 1048576.0:0.0} MB · needs Java {dist.JavaMajor}";
                                    verNote.Foreground = Ui.Res(dist.Missing == null ? "B.Mint" : "B.Amber");
                                }
                                catch (Exception ex) { verNote.Text = "Could not read the version metadata: " + ex.Message; verNote.Foreground = Ui.Res("B.Amber"); }
                                foreach (var c in list.Children.OfType<Button>())
                                {
                                    bool on = c.Content as string == vv.Id;
                                    c.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); c.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                                }
                            }, "Btn.Chip");
                            b.Height = 26; b.Margin = new Thickness(0, 0, 4, 4);
                            if (vv.Id == d.Version) { b.Background = Ui.Res("B.Mint"); b.Foreground = Ui.Res("B.MintInk"); }
                            list.Children.Add(b);
                        }
                        if (shown.Count == 0) list.Children.Add(Ui.Mono("No versions match.", Ui.Res("B.TextMute"), 11));
                    }
                    catch (Exception ex) { list.Children.Clear(); list.Children.Add(Ui.Mono("Could not load the official catalogue: " + ex.Message, Ui.Res("B.Amber"), 11)); }
                }
                foreach (var (label, type) in new[] { ("Releases", "release"), ("Snapshots", "snapshot"), ("Beta", "old_beta"), ("Alpha", "old_alpha") })
                {
                    var t = type;
                    var b = Ui.Button(label, () => { _versionType = t; PaintTypes(); Versions(); }, "Btn.Chip");
                    typeButtons.Add((b, t));
                    typeRow.Children.Add(b);
                }
                PaintTypes();
                sp.Children.Add(typeRow);
                sp.Children.Add(filter);
                sp.Children.Add(new ScrollViewer { Content = list, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
                verNote.Margin = new Thickness(0, 8, 0, 4);
                sp.Children.Add(verNote);
                filter.Filter += Versions;
                Versions();
                return sp;
            }

            // ---- step 2: where it runs, flavour and SSH host
            FrameworkElement BuildWhereStep()
            {
                var host = new ContentControl { IsTabStop = false };
                void Build()
                {
                    var sp = new StackPanel();
                    sp.Children.Add(Choice("Where it runs", new[] { ("This computer", Target.LocalProcess), ("Docker here", Target.LocalDocker), ("SSH host", Target.SshProcess), ("Docker over SSH", Target.SshDocker) }, d.Target,
                        v => { d.Target = v; if (v == Target.LocalProcess || v == Target.SshProcess) d.Flavour = Flavour.Vanilla; Build(); },
                        "This computer runs it as a normal process. The Docker choices run it in a container. The SSH choices do the same on another machine you can sign in to with an SSH key. Default: this computer."));
                    if (IsDocker())
                        sp.Children.Add(Choice("Flavour", new[] { ("Vanilla", Flavour.Vanilla), ("Paper", Flavour.Paper), ("Fabric", Flavour.Fabric) }, d.Flavour, v => d.Flavour = v,
                            "Vanilla is the official server. Paper is a faster, plugin-friendly build. Fabric supports mods. Default: Vanilla."));
                    else
                        sp.Children.Add(Ui.Mono("Flavour: Vanilla. Paper and Fabric run on the Docker targets.", Ui.Res("B.TextMute"), 11));
                    if (IsSsh())
                    {
                        var hostNote = Ui.Mono("", Ui.Res("B.TextMute"), 11);
                        var sshHost = Input(d.SshHost, 280, "user@host or user@host:port, signed in with an SSH key");
                        sshHost.TextChanged += (o, e) => d.SshHost = sshHost.Text.Trim();
                        AutomationProperties.SetName(sshHost, "SSH host");
                        var dir = Input(d.RemoteDir, 280, "Folder on the host (optional)");
                        dir.TextChanged += (o, e) => d.RemoteDir = dir.Text.Trim();
                        AutomationProperties.SetName(dir, "Folder on the host");
                        var row = new WrapPanel();
                        row.Children.Add(new StackPanel { Children = { Ui.Mono("SSH HOST", Ui.Res("B.TextMute"), 11), sshHost } });
                        row.Children.Add(new StackPanel { Children = { Ui.Mono("FOLDER ON HOST", Ui.Res("B.TextMute"), 11), dir } });
                        sp.Children.Add(row);
                        sp.Children.Add(Ui.Button("Check host key", async () =>
                        {
                            if (string.IsNullOrWhiteSpace(d.SshHost)) { hostNote.Text = "Enter the host first."; return; }
                            hostNote.Text = "Reading the host key…";
                            var (known, fp) = await System.Threading.Tasks.Task.Run(() => ServerManager.HostKey(d.SshHost));
                            if (known) { hostNote.Text = "✓ This host's key is already approved."; hostNote.Foreground = Ui.Res("B.Mint"); return; }
                            hostNote.Text = "Approve only if these match the host:\n" + fp; hostNote.Foreground = Ui.Res("B.Amber");
                            var ok = new ConfirmWindow("Approve this host key?", "Compare these with the host's own fingerprints before approving.\n\n" + fp, "Approve", danger: false);
                            ok.Confirmed += _ => { ServerManager.ApproveHostKey(fp); hostNote.Text = "✓ Host key approved for Minecraft servers."; hostNote.Foreground = Ui.Res("B.Mint"); };
                            ok.Show();
                        }, "Btn.Ghost", Icons.Shield));
                        sp.Children.Add(hostNote);
                    }
                    host.Content = sp;
                }
                Build();
                return host;
            }

            // ---- step 3: memory and port
            var port = Input(d.Port.ToString(), 120, "1024 to 65535");
            AutomationProperties.SetName(port, "Port");
            FrameworkElement BuildResourceStep()
            {
                var sp = new StackPanel();
                sp.Children.Add(Choice("Memory", new[] { ("1 GB", 1024), ("2 GB", 2048), ("4 GB", 4096), ("8 GB", 8192) }, d.MemoryMb, v => d.MemoryMb = v,
                    "How much memory the server may use. More suits more players and bigger worlds. Default: 2 GB."));
                sp.Children.Add(new StackPanel { Children = { Ui.Mono("PORT", Ui.Res("B.TextMute"), 11), port } });
                sp.Children.Add(Ui.Mono("Players connect on this port. 25565 is the usual one; a free port is suggested for you.", Ui.Res("B.TextMute"), 11));
                return sp;
            }

            // ---- step 4: review and EULA
            var summary = new TextBlock { Style = Ui.Style("T.Body"), TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text") };
            FrameworkElement BuildReviewStep()
            {
                var sp = new StackPanel();
                sp.Children.Add(summary);
                var eula = new CheckBox { Style = Ui.Style("Switch"), IsChecked = d.EulaAccepted, Margin = new Thickness(0, 12, 0, 6) };
                var eulaText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body") };
                eulaText.Inlines.Add(new System.Windows.Documents.Run("I accept the "));
                eulaText.Inlines.Add(Markdown.MakeLink("Minecraft EULA", "https://aka.ms/MinecraftEULA"));
                eulaText.Inlines.Add(new System.Windows.Documents.Run(" for this server"));
                eula.Content = new StackPanel { Children = { eulaText, new TextBlock { Text = "Required: the server will not run without it. Off until you accept.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 2, 0, 0) } } };
                eula.Checked += (o, e) => d.EulaAccepted = true;
                eula.Unchecked += (o, e) => d.EulaAccepted = false;
                sp.Children.Add(eula);
                return sp;
            }

            string CheckPort()
            {
                if (!int.TryParse(port.Text.Trim(), out var p) || p < 1024 || p > 65535) return "The port must be a number from 1024 to 65535.";
                if (!IsSsh() && ServerManager.Servers.Any(x => x.Port == p && x.Target == d.Target)) return $"Another server here already uses port {p}. Pick a different one.";
                d.Port = p;
                return null;
            }

            var steps = new List<GuidedStep>
            {
                new GuidedStep
                {
                    Title = "Name and version", Help = "Pick a name and a version from the official catalogue. Nothing is downloaded until the last step.",
                    Build = BuildVersionStep,
                    Validate = () => string.IsNullOrWhiteSpace(d.Name) ? "Give the server a name." : d.Version == null ? "Choose a version first." : null
                },
                new GuidedStep
                {
                    Title = "Where it runs", Help = "Choose the place the server runs. SSH targets sign in with your SSH key; nothing is stored beyond the host name.",
                    Build = BuildWhereStep,
                    Validate = () => IsSsh() && string.IsNullOrWhiteSpace(d.SshHost) ? "Enter the SSH host." : null
                },
                new GuidedStep
                {
                    Title = "Memory and port", Help = "How much memory the server may use, and the port players connect on.",
                    Build = BuildResourceStep,
                    Validate = CheckPort
                },
                new GuidedStep
                {
                    Title = "Review and accept the EULA", Help = "The server will not run without accepting the EULA.",
                    Build = BuildReviewStep,
                    Entered = () => summary.Text = $"Name: {d.Name}\nVersion: {d.Version}\nRuns: {TargetLabel(d.Target)}{(IsSsh() ? " (" + d.SshHost + ")" : "")} · {d.Flavour}\nMemory: {d.MemoryMb / 1024} GB · Port: {d.Port}",
                    Validate = () => d.EulaAccepted ? null : "Accept the EULA first; the server will not run without it."
                },
            };

            GuidedForm form = null;
            form = new GuidedForm("New server", steps, "Create server", null, () => { _draft = null; _mcStep = 0; Navigate("minecraft", animate: false); }, _mcStep);
            form.StepChanged += i => _mcStep = i;
            form.FinishAsync = async () =>
            {
                try
                {
                    await ServerManager.CreateAsync(d, t => Dispatcher.BeginInvoke(new Action(() => form.Report(t))));
                    _draft = null; _mcStep = 0;
                    Sounds.Play(Cue.Complete);
                    return null;
                }
                catch (Exception ex) { Sounds.Play(Cue.Warning); return ex.Message; }
            };
            form.Finished += () => Navigate("minecraft", animate: false);
            return form;
        }

        private static int NextPort()
        {
            int p = 25565;
            while (ServerManager.Servers.Any(s => s.Port == p)) p++;
            return p;
        }

        /// <summary>Live console lines for the open server.</summary>
        private void OnServerOutput(string id, string line)
        {
            if (id != _openConsole || _console == null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _console.Text += (_console.Text.Length > 0 ? "\n" : "") + line;
                if (_console.Text.Length > 60000) _console.Text = _console.Text[^40000..];
            }));
        }
    }
}
