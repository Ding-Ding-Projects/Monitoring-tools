using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Minecraft;

namespace MonitoringTools.UI
{
    // Checks for discovery and adoption, the catalogue badges and Update all, Bedrock, SSH keys and keeping keyboard focus
    // through the poll. Nothing here starts a server or touches the network; discovery reads a fake process list.
    public static partial class SelfTest
    {
        private static async Task McWave8ChecksAsync(ControlCentreWindow cc)
        {
            var mc = cc.MinecraftManager;

            Check("minecraft: discovery picks Java, Bedrock and Docker servers from a process list, and skips managed ones and other programs", () =>
            {
                var (procs, containers) = McDemo.FoundSample();
                var managed = ServerManager.Servers.First(x => x.Id == "sample-friends");
                procs.Add(new McProcInfo { Pid = 9001, Name = "java", CommandLine = "java -Xmx4G -jar server.jar nogui", WorkingDir = managed.Folder });   // the app's own server
                containers.Add(new McContainerInfo { Name = "monitoring-tools-mc-sample-creative", Image = "itzg/minecraft-server:latest", State = "exited", Labels = { ["monitoring-tools.server"] = "sample-creative" } });
                var found = McDiscovery.Classify(procs, containers, ServerManager.Servers);
                var titles = string.Join(", ", found.Select(f => f.Title));
                if (found.Count != 3) return "found " + found.Count + ": " + titles;
                var paper = found.FirstOrDefault(f => f.Pid == 4120);
                if (paper == null || paper.Flavour != Flavour.Paper || paper.Version != "1.21.4" || paper.Build != "232" || paper.Port != 25600 || paper.Jar != "paper-1.21.4-232.jar") return "the Paper process was read wrongly: " + paper?.Detail;
                var bedrock = found.FirstOrDefault(f => f.Pid == 5560);
                if (bedrock == null || bedrock.Flavour != Flavour.Bedrock || bedrock.Port != 19140 || bedrock.Version != "1.26.61.1") return "the Bedrock process was read wrongly";
                var c = found.FirstOrDefault(f => f.Kind == "docker");
                if (c == null || c.Container != "mc-creative-old" || c.Flavour != Flavour.Purpur || c.Version != "1.21.1" || c.Port != 25610) return "the container was read wrongly";
                if (found.Any(f => f.Pid == 7004 || f.Pid == 9001 || f.Container == "web-cache" || f.Container.StartsWith("monitoring-tools-mc-"))) return "a managed server or another program was listed: " + titles;
                if (McDiscovery.HostPath("/run/desktop/mnt/host/c/Servers/mc") != @"C:\Servers\mc" || McDiscovery.HostPath("my-volume") != "") return "Docker Desktop bind paths are not turned back into folders";
                return null;
            });

            await CheckAsync("minecraft: the Found list offers Adopt, and adopting points at the folder without changing it or stopping anything", async () =>
            {
                var (procs, containers) = McDemo.FoundSample();
                McDiscovery.FakeProcesses = () => procs; McDiscovery.FakeContainers = () => containers;
                ServerDef adopted = null;
                try
                {
                    mc.Select("sample-friends");
                    await mc.Discover();
                    await Wave6Flows.Settle(cc, 300);
                    if (!mc.FoundTitles.SequenceEqual(new[] { "survival-2024", "bedrock-family", "mc-creative-old" })) return "Found shows: " + string.Join(", ", mc.FoundTitles);
                    if (FindButton(mc, "Adopt") == null) return "no Adopt button";
                    var folder = McManagerView.Found[0].Folder;
                    var before = string.Join("|", Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(f => f + File.GetLastWriteTimeUtc(f).Ticks));
                    adopted = mc.AdoptNow(McManagerView.Found[0]);
                    await Wave6Flows.Settle(cc, 400);
                    var after = string.Join("|", Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(f => f + File.GetLastWriteTimeUtc(f).Ticks));
                    if (before != after) return "adopting changed the server's folder";
                    if (!adopted.Adopted || adopted.Folder != folder || adopted.JarName != "paper-1.21.4-232.jar" || adopted.Flavour != Flavour.Paper || adopted.Port != 25600) return "the adopted entry is wrong";
                    if (McManagerView.SelectedId != adopted.Id) return "the adopted server was not opened";
                    if (mc.FoundTitles.Contains("survival-2024")) return "the adopted server is still under Found";
                    var st = await McState.ProbeAsync(adopted);
                    if (st.State != McRunState.Running || !st.Detail.Contains("outside this app")) return "the adopted, still running process reads as " + st.State + ": " + st.Detail;
                    try { await ServerManager.StopAsync(adopted); return "a stop without RCON was accepted"; }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("RCON")) { }
                    var answer = await ServerManager.CommandAsync(adopted, "list");
                    if (!answer.Contains("RCON")) return "a command without RCON answered: " + answer;
                    if (!procs.Any(p => p.Pid == 4120)) return "the process left the list";
                    var again = McDiscovery.Classify(procs, containers, ServerManager.Servers);
                    return again.Any(f => f.Pid == 4120) ? "the adopted server is still offered for adoption" : null;
                }
                finally
                {
                    McDiscovery.FakeProcesses = null; McDiscovery.FakeContainers = null;
                    if (adopted != null) { ServerManager.Servers.Remove(adopted); ServerManager.Save(); }
                    McManagerView.Found.Clear();
                    mc.Select("sample-friends");
                }
            });

            Check("minecraft: catalogue badges follow the declared versions, and Update all rolls the folder back when one update fails", () =>
            {
                if (McCatalogue.Decide("1.21.4", new[] { "1.21.3", "1.21.4" }, "v1", "v1") != McCompat.Compatible) return "a file that lists the version is not Compatible";
                if (McCatalogue.Decide("1.21.4", new[] { "1.21.4" }, "v1", "v2") != McCompat.Update) return "a newer compatible file is not an Update";
                if (McCatalogue.Decide("1.21.4", new[] { "1.20.6" }, "v1", null) != McCompat.MayNotWork) return "a file without the version is not May not work";
                var s = new ServerDef { Id = "probe-update", Flavour = Flavour.Paper, Version = "1.21.4" };
                var folder = McContent.FolderOf(s);
                if (Directory.Exists(s.Folder)) Directory.Delete(s.Folder, true);
                Directory.CreateDirectory(folder);
                McDemo.Plugin(Path.Combine(folder, "A-1.0.jar"), "A", "1.0");
                McDemo.Plugin(Path.Combine(folder, "B-1.0.jar"), "B", "1.0");
                var newA = Path.Combine(McTemp("update-src"), "A-2.0.jar");
                McDemo.Plugin(newA, "A", "2.0");
                McCompatInfo Upd(string file, bool fail) => new McCompatInfo
                {
                    Kind = McCompat.Update, Current = "1.0", Newest = "2.0", Source = "Modrinth",
                    Apply = _ => { if (fail) throw new System.Net.Http.HttpRequestException("offline"); McCatalogue.Replace(folder, newA, "A-2.0.jar", Path.Combine(folder, file)); return Task.FromResult("ok"); },
                };
                var checks = new Dictionary<string, McCompatInfo> { ["A-1.0.jar"] = Upd("A-1.0.jar", false), ["B-1.0.jar"] = Upd("B-1.0.jar", true) };
                try { McCatalogue.UpdateAllAsync(s, checks, null).GetAwaiter().GetResult(); return "a failed update was reported as done"; }
                catch (InvalidOperationException ex) when (ex.Message.Contains("put back")) { }
                var names = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(x => x).ToList();
                if (!names.SequenceEqual(new[] { "A-1.0.jar", "B-1.0.jar" })) return "after the failed Update all the folder holds " + string.Join(", ", names);
                var msg = McCatalogue.UpdateAllAsync(s, new Dictionary<string, McCompatInfo> { ["A-1.0.jar"] = Upd("A-1.0.jar", false) }, null).GetAwaiter().GetResult();
                names = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(x => x).ToList();
                if (!msg.StartsWith("Updated 1 plugin") || !names.SequenceEqual(new[] { "A-2.0.jar", "B-1.0.jar" })) return "Update all left " + string.Join(", ", names) + ": " + msg;
                McContent.RestoreFrom(folder, McContent.LatestBackup(folder), keepCurrent: true);   // what Undo the last change does
                names = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(x => x).ToList();
                Directory.Delete(s.Folder, true);
                return names.SequenceEqual(new[] { "A-1.0.jar", "B-1.0.jar" }) ? null : "undo left " + string.Join(", ", names);
            });

            await CheckAsync("minecraft: the Plugins section shows a badge per plugin, Update all, and Hangar beside Modrinth for Paper", async () =>
            {
                mc.Select("sample-friends");
                McManagerView.CatalogueSource = "Modrinth";
                mc.ShowSection("content");
                await Wave6Flows.Settle(cc, 500);
                var text = string.Join(" ", Descend<TextBlock>(mc.SectionBody).Select(t => t.Text));
                foreach (var want in new[] { "Compatible", "Update", "May not work", "Modrinth has 5.4.156 for 1.21.4; this is 5.4.141.", "1 update for this version." })
                    if (!text.Contains(want)) return "the section does not show \"" + want + "\"";
                var all = FindButton(mc.SectionBody, "Update all");
                if (all == null || !all.IsEnabled) return "Update all is not offered for the one update";
                if (FindButton(mc.SectionBody, "Undo the last change") == null) return "no Undo the last change";
                if (!Press(mc.SectionBody, "Hangar")) return "no Hangar chip for a Paper server";
                await Task.Delay(150);
                if (!string.Join(" ", Descend<TextBlock>(mc.SectionBody).Select(t => t.Text)).Contains("Hangar's SHA-256")) return "choosing Hangar did not switch the catalogue note";
                Press(mc.SectionBody, "Modrinth");
                mc.Select("sample-creative");
                mc.ShowSection("content");
                await Wave6Flows.Settle(cc, 300);
                return FindButton(mc.SectionBody, "Hangar") == null ? null : "a Vanilla server offers Hangar";
            });

            await CheckAsync("minecraft: Bedrock: its log lines and list answer are read, it has its own settings, and the create flow takes it to review", async () =>
            {
                var l = McLogLine.Parse("[2026-11-04 10:00:03:412 INFO] Server started.");
                if (l.Message != "Server started." || l.Time != "10:00:03" || !McLive.IsReady(l.Message)) return "the Bedrock line was parsed as: " + l.Message;
                if (!McLogLine.TryPlayers("There are 0/10 players online:", out var n, out var max, out _) || n != 0 || max != 10) return "the Bedrock list answer was not read";
                var bs = new ServerDef { Flavour = Flavour.Bedrock };
                var bedrockKeys = McPropertyCatalog.For(bs);
                if (bedrockKeys.All(d => d.Key != "server-name") || bedrockKeys.Any(d => d.Key == "motd" || d.Key == McPropertyCatalog.MemoryKey)) return "the Bedrock settings use the Java keys";
                if (McBedrock.CheckUrl("https://www.minecraft.net/bedrockdedicatedserver/bin-win/bedrock-server-1.26.61.1.zip") != null || McBedrock.CheckUrl("http://www.minecraft.net/x.zip") == null || McBedrock.CheckUrl("https://example.com/bedrock-server.zip") == null) return "the download link check is wrong";
                McBedrock.SetLatestForTest(new McBedrock.Release { Version = "1.26.61.1", Url = "https://www.minecraft.net/bedrockdedicatedserver/bin-win/bedrock-server-1.26.61.1.zip" });
                try
                {
                    McManagerView.Draft = new McCreateDraft { Port = 25565 };
                    mc.OpenCreate();
                    await Wave6Flows.Settle(cc, 400);
                    var d = McManagerView.Draft;
                    if (!Press(McForm(mc), "Bedrock edition")) return "no Bedrock edition choice";
                    await Task.Delay(300);
                    if (d.BedrockVersion != "1.26.61.1" || d.Port != 19132) return $"choosing Bedrock gave version {d.BedrockVersion}, port {d.Port}";
                    Press(McForm(mc), "Next"); await Task.Delay(250);
                    if (!McStep(McForm(mc)).StartsWith("Step 2 of 6") || !string.Join(" ", Descend<TextBlock>(McForm(mc)).Select(t => t.Text)).Contains("Bedrock Dedicated Server")) return "step 2 is not the Bedrock software: " + McStep(McForm(mc));
                    Press(McForm(mc), "Next"); await Task.Delay(250);
                    d.Target = Target.LocalDocker;
                    Press(McForm(mc), "Next"); await Task.Delay(150);
                    if (!McStep(McForm(mc)).StartsWith("Step 3 of 6")) return "Bedrock on Docker was allowed";
                    d.Target = Target.LocalProcess;
                    Press(McForm(mc), "Next"); await Task.Delay(250);
                    if (!McStep(McForm(mc)).StartsWith("Step 4 of 6")) return "step 4 not reached: " + McStep(McForm(mc));
                    if (Descend<TextBox>(McForm(mc)).Any(t => AutomationProperties.GetName(t) == "Memory")) return "Bedrock asks for Java memory";
                    Press(McForm(mc), "Next"); await Task.Delay(250);
                    d.WorldMode = "existing";
                    Press(McForm(mc), "Next"); await Task.Delay(150);
                    if (!McStep(McForm(mc)).StartsWith("Step 5 of 6")) return "an existing world was allowed for Bedrock";
                    d.WorldMode = "new";
                    Press(McForm(mc), "Next"); await Task.Delay(300);
                    var review = string.Join(" ", Descend<TextBlock>(McForm(mc)).Select(t => t.Text));
                    if (!McStep(McForm(mc)).StartsWith("Step 6 of 6") || !review.Contains("Bedrock 1.26.61.1")) return "the review does not show Bedrock 1.26.61.1";
                    return review.Contains("Memory") && review.Contains(" MB") ? "the review shows Java memory for Bedrock" : null;
                }
                finally { McManagerView.Draft = null; mc.CloseCreate(); McBedrock.SetLatestForTest(null); }
            });

            Check("minecraft: an SSH server's own key is passed to ssh and scp, and only that key", () =>
            {
                var s = new ServerDef { SshHost = "mc@198.51.100.7:2222", SshKey = @"C:\keys\id_ed25519" };
                var a = ServerManager.KeyArgs(s);
                if (a != "-i \"C:\\keys\\id_ed25519\" -o IdentitiesOnly=yes ") return "key arguments: " + a;
                return ServerManager.KeyArgs(new ServerDef()) == "" ? null : "a server without a key got key arguments";
            });

            await CheckAsync("minecraft: keyboard focus and the scroll position stay put in every section through two polls", async () =>
            {
                mc.Select("sample-friends");
                var bad = new List<string>();
                var live = McLive.ForId("sample-friends");
                foreach (var (id, _, _) in McManagerView.Sections)
                {
                    mc.ShowSection(id);
                    await Wave6Flows.Settle(cc, 900);
                    cc.Activate();
                    var target = Descend<System.Windows.Controls.Primitives.ButtonBase>(mc.SectionBody).LastOrDefault(b => b.IsVisible && b.IsEnabled && b.Focusable && McManagerView.Identity(b).Length > 12);
                    if (target == null) { bad.Add(id + ": no button"); continue; }
                    target.BringIntoView();
                    await Task.Delay(200);
                    target.Focus();
                    Keyboard.Focus(target);
                    var key = McManagerView.Identity(target);
                    var offset = cc.Scroller.VerticalOffset;
                    // Two polls, with a new console line between them so the live parts really redraw.
                    await Task.Delay(4300);
                    live.Add($"[{DateTime.Now:HH:mm:ss} INFO]: Focus check line for {id}");
                    await Task.Delay(4300);
                    var now = (Keyboard.FocusedElement ?? FocusManager.GetFocusedElement(cc)) as FrameworkElement;
                    if (now == null || McManagerView.Identity(now) != key || !now.IsVisible || Window.GetWindow(now) != cc) bad.Add($"{id}: focus moved from {key} to {(now == null ? "nothing" : McManagerView.Identity(now))}");
                    else if (Math.Abs(cc.Scroller.VerticalOffset - offset) > 1) bad.Add($"{id}: scrolled from {offset:0} to {cc.Scroller.VerticalOffset:0}");
                }
                cc.Scroller.ScrollToTop();
                mc.ShowSection("overview");
                return bad.Count == 0 ? null : string.Join("; ", bad);
            });
        }
    }
}
