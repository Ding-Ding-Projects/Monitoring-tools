using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;

namespace MonitoringTools.Core
{
    // More live checks for the file store work: Linux Bedrock started for real over SSH on a glibc host (with a backup of the
    // running server and an on-host backup), the catalogues and Undo over SSH, and bulk actions on a real online player.
    //   MT_LIVE_ONLY=minecraft-bedrock-ssh   MT_LIVE_ONLY=minecraft-ssh-catalogue   MT_LIVE_ONLY=minecraft-bulk-bot
    public static partial class LiveCheck
    {
        private static async Task McFilesLive2(List<Result> results, string only)
        {
            if (only == null || only == "minecraft-bedrock-ssh")
                await Check(results, "minecraft: the Linux Bedrock server is installed over SSH on a glibc host, started, answers list, is backed up while it runs (here and on the host) and stops", BedrockSsh);
            if (only == null || only == "minecraft-ssh-catalogue")
                await Check(results, "minecraft: Modrinth and Hangar installs, badges, Update all and Undo on a server over SSH", SshCatalogue);
            if (only == null || only == "minecraft-bulk-bot")
                await Check(results, "minecraft: bulk teleport, kick and ban reach a real online player (a headless bot) on a real server", BulkBot);
        }

        private sealed class LiveHost { public string Container, Host, Key, Dir; }

        /// <summary>A throwaway SSH host in a container: Alpine (small) or Debian (glibc). Loopback only; removed by <see cref="DropHost"/>.</summary>
        private static async Task<(LiveHost H, string Problem)> MakeHost(bool debian)
        {
            var dir = Path.Combine(Settings.DataDir, "livehost-" + Stamp());
            Directory.CreateDirectory(dir);
            var (key, pub) = KeyPair(dir);
            int sshPort = 22520; while (!ServerManager.PortFree(sshPort)) sshPort++;
            var name = "mt-live-host-" + Stamp();
            var user = ThrowawayUser();
            var h = new LiveHost { Container = name, Key = key, Dir = dir, Host = SshHostSpec(user, sshPort) };
            if (!debian)
            {
                var run = Docker($"run -d --name {name} -e PUID=1000 -e PGID=1000 -e USER_NAME={user} -e \"PUBLIC_KEY={pub}\" -p 127.0.0.1:{sshPort}:2222 lscr.io/linuxserver/openssh-server:latest", 600000);
                if (run.Code != 0) return (h, "docker run failed: " + ServerManager.ActionLine(run.Out));
                var apk = Docker($"exec {name} apk add --no-cache curl unzip", 600000);
                if (apk.Code != 0) return (h, "apk failed: " + ServerManager.ActionLine(apk.Out));
            }
            else
            {
                var run = Docker($"run -d --name {name} -p 127.0.0.1:{sshPort}:2222 debian:bookworm-slim sleep infinity", 900000);
                if (run.Code != 0) return (h, "docker run failed: " + ServerManager.ActionLine(run.Out));
                var inst = Docker($"exec {name} sh -c \"apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq openssh-server curl unzip ca-certificates libcurl4 libssl3 >/dev/null\"", 900000);
                if (inst.Code != 0) return (h, "installing packages failed: " + ServerManager.ActionLine(inst.Out));
                var pw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                var setup = Docker($"exec {name} sh -c \"mkdir -p /run/sshd && ssh-keygen -A >/dev/null && useradd -m -s /bin/sh {user} && echo {user}:{pw} | chpasswd && mkdir -p /home/{user}/.ssh && echo '{pub}' > /home/{user}/.ssh/authorized_keys && chown -R {user} /home/{user}/.ssh && chmod 700 /home/{user}/.ssh && chmod 600 /home/{user}/.ssh/authorized_keys && /usr/sbin/sshd -p 2222\"", 120000);
                if (setup.Code != 0) return (h, "setting up sshd failed: " + ServerManager.ActionLine(setup.Out));
            }
            var approve = await ApproveHost(h.Host);
            return (h, approve);
        }

        private static void DropHost(LiveHost h)
        {
            if (h == null) return;
            Docker($"rm -f -v {h.Container}", 60000);
            try { Directory.Delete(h.Dir, true); } catch { }
        }

        // ------------------------------------------------------------------ Linux Bedrock over SSH

        private static async Task<string> BedrockSsh()
        {
            NeedDocker();
            LiveHost host = null; ServerDef s = null;
            try
            {
                var sw = Stopwatch.StartNew();
                string problem;
                (host, problem) = await MakeHost(true);
                if (problem != null) return problem;
                Say($"Debian (glibc) SSH host ready in {sw.Elapsed.TotalSeconds:0} s");
                s = new ServerDef { Name = "Live Bedrock over SSH", Flavour = Flavour.Bedrock, Target = Target.SshProcess, SshHost = host.Host, SshKey = host.Key, Port = 19132, RemoteDir = "bedrock live", EulaAccepted = true, BackupDir = Path.Combine(host.Dir, "backups"), KeepBackups = 5 };
                sw.Restart();
                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
                Say($"Linux Bedrock {s.Version} installed over SSH in {sw.Elapsed.TotalSeconds:0} s");
                var files = McFiles.For(s);
                sw.Restart();
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(4);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 300)).Contains("Server started.")) await Task.Delay(2000);
                if (!log.Contains("Server started.")) return "no \"Server started.\" within four minutes: " + Trim(string.Join(" | ", log.Split('\n').TakeLast(4)));
                Say($"\"Server started.\" after {sw.Elapsed.TotalSeconds:0} s");
                var st = await McState.ProbeAsync(s);
                if (st.State != McRunState.Running && st.State != McRunState.Starting) return "state " + st.State;
                await ServerManager.CommandAsync(s, "list");
                string answer = null;
                for (int i = 0; i < 20 && answer == null; i++) { await Task.Delay(1000); answer = (await ServerManager.LogTailAsync(s, 60)).Split('\n').LastOrDefault(l => l.Contains("players online") || l.Contains("There are ")); }
                if (answer == null) return "list gave no answer in the log";
                Say("list: " + Trim(answer));

                // a backup of the RUNNING server: save hold, copy, save resume over the console pipe
                sw.Restart();
                var b = await McBackups.BackUpAsync(s, true, "By hand", "running ssh");
                if (!b.Contents.Contains("worlds")) return "the running backup holds " + string.Join(",", b.Contents);
                var tail = await ServerManager.LogTailAsync(s, 200);
                if (!tail.Contains("Saving") && !tail.Contains("Data saved") && !tail.Contains("Changes to the level are resumed")) Say("(no save line read in the log tail)");
                else Say("the server's log shows the save hold and resume");
                Say($"backup of the running server copied here ({Ui_Bytes(b.Size)}) in {sw.Elapsed.TotalSeconds:0} s, holding {string.Join(", ", b.Contents)}");

                // an on-host backup: archived on the host and its list read back from the host
                s.BackupOnHost = true;
                var hb = await McBackups.BackUpAsync(s, true, "By hand", "on the host");
                var listed = McRemote.HostList(files);
                if (listed.Count != 1 || listed[0].Path != hb.Path || !listed[0].Contents.Contains("worlds") || listed[0].Size <= 0 || listed[0].Note != "on the host") return "the host list read back: " + listed.Count + " backups";
                Say($"on-host backup {System.IO.Path.GetFileName(hb.Path)} ({Ui_Bytes(hb.Size)}) read back from the host's own folder");
                await ServerManager.StopAsync(s);
                var after = await McState.ProbeAsync(s);
                if (after.State != McRunState.Stopped) return "after stopping the state is " + after.State;
                Say("stopped");
                // restore the on-host backup while stopped, with the safety backup
                string step = "writing a marker";
                McBackup safety;
                try
                {
                    files.WriteText("worlds/Bedrock level/marker.txt", "changed after the backup");
                    step = "restoring";
                    safety = await Task.Run(() => McRemote.HostRestore(s, files, McRemote.LevelName(files, true), listed[0]));
                }
                catch (Exception ex) { return $"while {step}: {ex.Message}"; }
                if (files.Exists("worlds/Bedrock level/marker.txt")) return "the on-host restore left a file that was not in the backup";
                if (safety == null || McRemote.HostList(files).Count < 2) return "no safety backup on the host";
                Say("on-host restore replaced the world and kept a safety backup on the host");
                return null;
            }
            finally
            {
                if (s != null) { ServerManager.Servers.Remove(s); ServerManager.Save(); }
                DropHost(host);
            }
        }

        // ------------------------------------------------------------------ catalogues over SSH

        private static async Task<string> SshCatalogue()
        {
            NeedDocker();
            LiveHost host = null; ServerDef s = null;
            try
            {
                string problem;
                (host, problem) = await MakeHost(false);
                if (problem != null) return problem;
                s = new ServerDef { Id = "live-sshcat-" + Stamp(), Name = "Live SSH catalogue", Version = Version, Flavour = Flavour.Paper, Target = Target.SshProcess, SshHost = host.Host, SshKey = host.Key, RemoteDir = "live catalogue" };
                var f = McFiles.For(s);
                var sw = Stopwatch.StartNew();
                var mod = await McModrinth.SearchAsync("chunky", s.Flavour, s.Version);
                var hangar = await McHangar.SearchAsync("viaversion", s.Flavour, s.Version);
                if (mod.Count == 0 || hangar.Count == 0) return "a catalogue found nothing";
                var hv = McHangar.Newest(await McHangar.VersionsAsync(hangar[0].ProjectId, s.Version));
                await McHangar.InstallAsync(hangar[0], s, null, hv);
                var back = Path.Combine(host.Dir, "hangar.jar");
                f.DownloadFile("plugins/" + hv.File, back);
                if (!Sha(back, false).Equals(hv.Sha256, StringComparison.OrdinalIgnoreCase)) return "the jar on the host does not match Hangar's SHA-256";
                Say($"Hangar: {hv.File} installed on the host, SHA-256 {hv.Sha256[..12]}… matched after the copy");
                var chunky = mod.First(h => h.Title.Contains("Chunky", StringComparison.OrdinalIgnoreCase));
                var versions = await McModrinth.VersionsAsync(chunky.ProjectId, s);
                if (versions.Count < 2) return "fewer than two Chunky files";
                var older = versions[1];
                await McModrinth.InstallVersionAsync(older, s, null, null, chunky.Title);
                var oldName = older.GetProperty("files")[0].GetProperty("filename").GetString();
                var listed = older.GetProperty("files")[0].GetProperty("hashes").GetProperty("sha512").GetString();
                if (!f.Sha512(new[] { "plugins/" + oldName }).TryGetValue("plugins/" + oldName, out var onHost) || !onHost.Equals(listed, StringComparison.OrdinalIgnoreCase)) return "the Modrinth jar on the host does not match the catalogue's SHA-512";
                Say($"Modrinth: {oldName} (older on purpose) installed on the host, SHA-512 matched");
                var checks = await McCatalogue.CheckAsync(s);
                if (!checks.TryGetValue(oldName, out var ci) || ci.Kind != McCompat.Update) return $"the older Chunky was badged {(ci == null ? "nothing" : ci.Label)}, expected Update";
                if (!checks.TryGetValue(hv.File, out var hi) || hi.Kind != McCompat.Compatible) return $"the Hangar plugin was badged {(hi == null ? "nothing" : hi.Label)}, expected Compatible";
                Say($"badges read from SHA-512 computed on the host: {oldName} Update to {ci.Newest}, {hv.File} Compatible");
                var msg = await McCatalogue.UpdateAllAsync(s, checks, null);
                var after = await McCatalogue.CheckAsync(s);
                if (f.Exists("plugins/" + oldName) && Path.GetFileName(oldName) != versions[0].GetProperty("files")[0].GetProperty("filename").GetString()) return "Update all left the older jar on the host";
                if (after.Values.Any(v => v.Kind == McCompat.Update)) return "after Update all a badge still says Update";
                Say(msg);
                // Undo: the newest dated copy on the host puts the older file back
                var last = McContent.LatestBackup(f, "plugins") ?? throw new InvalidOperationException("no dated copy on the host");
                McContent.RestoreFrom(f, "plugins", last, keepCurrent: true);
                if (!f.Exists("plugins/" + oldName)) return "Undo did not bring the older jar back on the host";
                Say("Undo put the host's folder back as the newest dated copy held it (the older Chunky is back), after copying the current folder");
                return null;
            }
            finally
            {
                if (s != null) try { File.Delete(Path.Combine(ServerManager.Root, "catalogue", s.Id + ".json")); } catch { }
                DropHost(host);
            }
        }

        // ------------------------------------------------------------------ bulk actions on a real online player

        private sealed class Bot : IDisposable
        {
            public readonly List<string> Out = new List<string>();
            private Process _p;
            public static Bot Start(string dir, int port, string name)
            {
                var b = new Bot();
                var psi = new ProcessStartInfo("node", $"bot.js 127.0.0.1 {port} {name}") { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                b._p = Process.Start(psi);
                b._p.OutputDataReceived += (o, e) => { if (e.Data != null) lock (b.Out) b.Out.Add(e.Data); };
                b._p.ErrorDataReceived += (o, e) => { if (e.Data != null) lock (b.Out) b.Out.Add("STDERR " + e.Data); };
                b._p.BeginOutputReadLine(); b._p.BeginErrorReadLine();
                return b;
            }
            public async Task<string> Wait(string contains, int seconds)
            {
                var until = DateTime.Now.AddSeconds(seconds);
                while (DateTime.Now < until)
                {
                    lock (Out) { var hit = Out.FirstOrDefault(l => l.Contains(contains)); if (hit != null) return hit; }
                    await Task.Delay(300);
                }
                return null;
            }
            public string Last(string prefix) { lock (Out) return Out.LastOrDefault(l => l.StartsWith(prefix)); }
            public void Dispose() { try { if (!_p.HasExited) _p.Kill(true); } catch { } }
        }

        private const string BotScript = @"
const mineflayer = require('mineflayer');
const [,, host, port, name] = process.argv;
const bot = mineflayer.createBot({ host, port: +port, username: name, version: '1.21.4', auth: 'offline' });
bot.on('spawn', () => console.log('EVENT spawn'));
bot.on('kicked', r => console.log('EVENT kicked ' + JSON.stringify(r)));
bot.on('end', r => { console.log('EVENT end ' + r); setTimeout(() => process.exit(0), 200); });
bot.on('error', e => console.log('EVENT error ' + e.message));
setInterval(() => { if (bot.entity) console.log('POS ' + bot.entity.position.x.toFixed(1) + ' ' + bot.entity.position.y.toFixed(1) + ' ' + bot.entity.position.z.toFixed(1)); }, 500);
";

        private static async Task<string> BulkBot()
        {
            var work = Path.Combine(Settings.DataDir, "mc-bulkbot-" + Stamp());
            Directory.CreateDirectory(work);
            var port = 25565;
            while (!ServerManager.PortFree(port)) port++;
            var s = new ServerDef { Name = "Live bulk bot", Version = Version, Flavour = Flavour.Vanilla, Target = Target.LocalProcess, Port = port, MemoryMb = 1024, EulaAccepted = true };
            Bot bot = null;
            try
            {
                var sw = Stopwatch.StartNew();
                File.WriteAllText(Path.Combine(work, "package.json"), "{\"name\":\"mtbot\",\"version\":\"1.0.0\"}");
                File.WriteAllText(Path.Combine(work, "bot.js"), BotScript);
                var npm = ServerManager.Run("cmd.exe", "/c npm install mineflayer --no-audit --no-fund", 600000, null, work);
                if (npm.Code != 0) throw new Skip("npm could not install mineflayer: " + ServerManager.ActionLine(npm.Out));
                Say($"mineflayer installed in a throwaway folder in {sw.Elapsed.TotalSeconds:0} s");
                sw.Restart();
                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["online-mode"] = "false" });
                await ServerManager.StartAsync(s, _ => { });
                var live = McLive.For(s);
                var until = DateTime.Now.AddMinutes(6);
                while (DateTime.Now < until && !live.Lines().Any(l => l.Message.StartsWith("Done (", StringComparison.Ordinal)))
                {
                    if (ServerManager.ProcessOf(s) == null) return "the server exited before it was ready";
                    await Task.Delay(1000);
                }
                Say($"server ready in {sw.Elapsed.TotalSeconds:0} s");
                var args = new BulkArgs();

                bot = Bot.Start(work, port, "LiveBot");
                if (await bot.Wait("EVENT spawn", 90) == null) return "the bot never spawned: " + Trim(string.Join(" | ", bot.Out.TakeLast(4)));
                await ServerManager.CommandAsync(s, "list");
                for (int i = 0; i < 20 && live.PlayersOnline != 1; i++) await Task.Delay(500);
                if (live.PlayersOnline != 1 || !live.PlayerNames.Contains("LiveBot")) return "list did not show the bot online: " + live.PlayersOnline;
                Say("the bot joined and list shows LiveBot online");

                // teleport: one console line, the server's reply, and the bot's own position
                var tp = await McBulk.RunAsync(s, true, BulkVerb.Teleport, new[] { "LiveBot" }, new BulkArgs { Target = "100 150 -80" });
                if (!tp[0].Ok) return "teleport failed: " + tp[0].Message;
                string pos = null;
                for (int i = 0; i < 40 && pos == null; i++)
                {
                    await Task.Delay(300);
                    var p = bot.Last("POS ");
                    if (p != null) { var parts = p.Split(' '); if (Math.Abs(double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) - 100) < 3 && Math.Abs(double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture) + 80) < 3) pos = p; }
                }
                if (pos == null) return "the bot was not teleported; its last position was " + bot.Last("POS ");
                var tpLog = live.Lines().LastOrDefault(l => l.Raw.Contains("Teleported LiveBot"));
                Say($"teleport: the server logged \"{Trim(tpLog?.Message ?? "(no Teleported line)")}\" and the bot's own position became {pos}");

                // kick, with a reason
                var kick = await McBulk.RunAsync(s, true, BulkVerb.Kick, new[] { "LiveBot" }, new BulkArgs { Reason = "see the rules" });
                if (!kick[0].Ok) return "kick failed: " + kick[0].Message;
                var kicked = await bot.Wait("EVENT kicked", 20);
                if (kicked == null || !kicked.Contains("see the rules")) return "the bot was not kicked with the reason: " + Trim(kicked ?? string.Join(" | ", bot.Out.TakeLast(3)));
                Say("kick: the bot received " + Trim(kicked) + "; the server logged " + Trim(live.Lines().LastOrDefault(l => l.Raw.Contains("Kicked") || l.Raw.Contains("lost connection"))?.Message));
                bot.Dispose();

                // ban: kicked at once, then refused at login; pardon lets it back in
                bot = Bot.Start(work, port, "LiveBot");
                if (await bot.Wait("EVENT spawn", 60) == null) return "the bot could not rejoin after the kick";
                var ban = await McBulk.RunAsync(s, true, BulkVerb.Ban, new[] { "LiveBot" }, new BulkArgs { Reason = "live check ban" });
                if (!ban[0].Ok) return "ban failed: " + ban[0].Message;
                var banned = await bot.Wait("EVENT kicked", 20);
                if (banned == null || !banned.Contains("banned")) return "the bot was not kicked by the ban: " + Trim(banned ?? string.Join(" | ", bot.Out.TakeLast(3)));
                bot.Dispose();
                bot = Bot.Start(work, port, "LiveBot");
                var refused = await bot.Wait("EVENT kicked", 30);
                if (refused == null || !refused.Contains("banned") || !refused.Contains("live check ban")) return "the banned bot was not refused at login: " + Trim(refused ?? string.Join(" | ", bot.Out.TakeLast(3)));
                Say("ban: the bot was kicked, and a new login was refused naming the reason: " + Trim(refused));
                bot.Dispose();
                await McBulk.RunAsync(s, true, BulkVerb.Pardon, new[] { "LiveBot" }, args);
                bot = Bot.Start(work, port, "LiveBot");
                if (await bot.Wait("EVENT spawn", 60) == null) return "after pardon the bot could not join: " + Trim(string.Join(" | ", bot.Out.TakeLast(3)));
                Say("pardon: the bot joined again");
                return null;
            }
            finally
            {
                bot?.Dispose();
                try { await ServerManager.StopAsync(s); } catch { }
                ServerManager.Servers.Remove(s); ServerManager.Save();
                try { Directory.Delete(s.Folder, true); } catch { }
                try { Directory.Delete(work, true); } catch { }
            }
        }
    }
}
