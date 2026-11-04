using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Live checks for the server manager's catalogues, Bedrock, discovery and adoption, and the Docker and SSH targets.
    /// Every server, container and process they make lives in the throwaway data folder or carries a "mt-live-" name, and
    /// each is removed in a finally block whether the check passed or not. Containers that were not made here are never
    /// touched.
    /// </summary>
    public static partial class LiveCheck
    {
        /// <summary>Extra facts a passing check reports (counts, files, timings), written after "ok" in its detail.</summary>
        private static readonly StringBuilder Note = new StringBuilder();
        private static void Say(string fact) { lock (Note) { if (Note.Length > 0) Note.Append(" · "); Note.Append(fact); } }

        private static string Version => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MT_LIVE_MC")) ? "1.21.4" : Environment.GetEnvironmentVariable("MT_LIVE_MC");
        private static string Stamp() => DateTime.UtcNow.ToString("HHmmss") + Guid.NewGuid().ToString("N")[..4];

        private static async Task McWave8(List<Result> results)
        {
            await Check(results, "minecraft: Modrinth and Hangar search, a real install from each with its hash checked, a badge and Update all", Catalogues);
            await Check(results, "minecraft: a real Bedrock server is created, started, answers list and stops", Bedrock);
            await Check(results, "minecraft: a Java server started outside the app is discovered, adopted, answers over RCON and stops", DiscoverProcess);
            await Check(results, "minecraft: a Minecraft container the app did not make is discovered, adopted and read, never removed by the app", DiscoverContainer);
            await Check(results, "minecraft: a real server on the local Docker target is created, started, answers list, stops and is removed", DockerTarget);
            await Check(results, "minecraft: a real server over SSH is created, started, answers list and stops", SshTarget);
            await Check(results, "minecraft: a real server in Docker on an SSH host is created, started, answers list and stops", SshDockerTarget);
        }

        /// <summary>A generated account name for a throwaway SSH host, so no real account is ever named.</summary>
        private static string ThrowawayUser() => "live" + Guid.NewGuid().ToString("N")[..6];

        /// <summary>The host spec the app takes, for the loopback address only.</summary>
        private static string SshHostSpec(string user, int port) => string.Join("@", user, "127.0.0.1") + ":" + port;

        /// <summary>A throwaway SSH key pair in <paramref name="dir"/>; the private key is readable by this account only.</summary>
        private static (string Key, string Pub) KeyPair(string dir)
        {
            var key = Path.Combine(dir, "id_ed25519");
            var keygen = Path.Combine(Path.GetDirectoryName(ServerManager.SshExe()), "ssh-keygen.exe");
            var kg = ServerManager.Run(keygen, $"-q -t ed25519 -N \"\" -C live-check -f \"{key}\"", 30000);
            if (kg.Code != 0) throw new InvalidOperationException("ssh-keygen failed: " + kg.Out);
            // OpenSSH refuses a private key that other accounts can read.
            ServerManager.Run("icacls", $"\"{key}\" /inheritance:r /grant:r \"{Environment.UserName}:F\"", 20000);
            return (key, File.ReadAllText(key + ".pub").Trim());
        }

        /// <summary>Reads the host key the way the Approve button's check does, and approves it into the app's known_hosts.</summary>
        private static async Task<string> ApproveHost(string host)
        {
            (bool Known, string Fingerprints) hk = (false, "");
            for (int i = 0; i < 40; i++) { hk = ServerManager.HostKey(host); if (hk.Fingerprints.Contains("ssh-")) break; await Task.Delay(1500); }
            if (hk.Known) return "the host key was approved before the check approved it";
            if (!hk.Fingerprints.Contains("ssh-ed25519")) return "the host key could not be read: " + Trim(hk.Fingerprints);
            ServerManager.ApproveHostKey(hk.Fingerprints);   // the same call the Approve button makes
            return ServerManager.HostKey(host).Known ? null : "the approved host key is not recognised";
        }

        /// <summary>
        /// Docker on an SSH host, for real: a throwaway Docker-in-Docker container (privileged, so it can run its own Docker
        /// engine, isolated from this computer's containers) with an SSH server added, published on 127.0.0.1 only.
        /// </summary>
        private static async Task<string> SshDockerTarget()
        {
            NeedDocker();
            var dir = Path.Combine(Settings.DataDir, "sshdind-live-" + Stamp());
            Directory.CreateDirectory(dir);
            var (key, pub) = KeyPair(dir);
            int sshPort = 22320; while (!ServerManager.PortFree(sshPort)) sshPort++;
            var name = "mt-live-sshdind-" + Stamp();
            ServerDef s = null;
            try
            {
                var sw = Stopwatch.StartNew();
                var run = Docker($"run -d --privileged --name {name} -p 127.0.0.1:{sshPort}:2222 docker:dind", 600000);
                if (run.Code != 0) return "docker run failed: " + ServerManager.ActionLine(run.Out);
                var pw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                for (int i = 0; i < 60 && Docker($"exec {name} docker info --format \"{{{{.ServerVersion}}}}\"", 15000).Code != 0; i++) await Task.Delay(1000);
                // A generated throwaway account that may use the inner Docker engine: it joins the group that owns the engine's
                // socket, read once the engine is up.
                var user = ThrowawayUser();
                var setup = Docker($"exec {name} sh -c \"apk add --no-cache openssh >/dev/null && ssh-keygen -A >/dev/null && adduser -D {user} && echo {user}:{pw} | chpasswd && addgroup {user} $(stat -c %G /var/run/docker.sock) && mkdir -p /home/{user}/.ssh && echo '{pub}' > /home/{user}/.ssh/authorized_keys && chown -R {user} /home/{user}/.ssh && chmod 700 /home/{user}/.ssh && chmod 600 /home/{user}/.ssh/authorized_keys && /usr/sbin/sshd -p 2222\"", 600000);
                if (setup.Code != 0) return "setting up SSH in the Docker host failed: " + ServerManager.ActionLine(setup.Out);
                Say($"SSH Docker host {name} on 127.0.0.1:{sshPort} ready in {sw.Elapsed.TotalSeconds:0} s");
                var host = SshHostSpec(user, sshPort);
                var approve = await ApproveHost(host);
                if (approve != null) return approve;
                s = new ServerDef { Name = "Live SSH Docker", Version = Version, Flavour = Flavour.Vanilla, Target = Target.SshDocker, SshHost = host, SshKey = key, Port = 25565, MemoryMb = 1024, EulaAccepted = true, RemoteDir = "live-check" };
                sw.Restart();
                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
                Say($"created over SSH in {sw.Elapsed.TotalSeconds:0} s (image pulled on the host)");
                sw.Restart();
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(10);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 400)).Contains("Done (")) await Task.Delay(3000);
                if (!log.Contains("Done (")) return "no Done line within ten minutes: " + Trim(ServerManager.ActionLine(log));
                Say($"Done after {sw.Elapsed.TotalSeconds:0} s");
                var st = await McState.ProbeAsync(s);
                if (st.State != McRunState.Running && st.State != McRunState.Starting) return "the state probe said " + st.State + ": " + st.Detail;
                var answer = await ServerManager.CommandAsync(s, "list");
                if (!answer.Contains("of a max of 7")) return "list answered: " + answer;
                Say("list: " + answer.Trim());
                await ServerManager.StopAsync(s);
                var after = await McState.ProbeAsync(s);
                if (after.State != McRunState.Stopped) return "after stopping the state is " + after.State;
                Say("stopped");
                return null;
            }
            finally
            {
                if (s != null) { ServerManager.Servers.Remove(s); ServerManager.Save(); }
                Docker($"rm -f -v {name}", 120000);   // the container this check made, with the inner Docker's data
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private static (int Code, string Out) Docker(string args, int timeoutMs = 120000) => ServerManager.Run("docker", args, timeoutMs);

        private static void NeedDocker()
        {
            if (Docker("info --format \"{{.ServerVersion}}\"", 20000).Code != 0) throw new Skip("Docker is not running on this computer.");
        }

        private static string Sha(string file, bool sha512) => Convert.ToHexString(sha512 ? SHA512.HashData(File.ReadAllBytes(file)) : SHA256.HashData(File.ReadAllBytes(file)));

        // ------------------------------------------------------------------ catalogues

        private static async Task<string> Catalogues()
        {
            var s = new ServerDef { Id = "live-catalogue-" + Stamp(), Name = "Live catalogue", Version = Version, Flavour = Flavour.Paper, Target = Target.LocalProcess };
            Directory.CreateDirectory(McContent.FolderOf(s));
            try
            {
                var sw = Stopwatch.StartNew();
                var mod = await McModrinth.SearchAsync("chunky", s.Flavour, s.Version);
                if (mod.Count == 0) return "Modrinth found nothing for chunky";
                var hangar = await McHangar.SearchAsync("viaversion", s.Flavour, s.Version);
                if (hangar.Count == 0 || hangar[0].Source != "Hangar") return "Hangar found nothing for viaversion";
                Say($"Modrinth: {mod.Count} hits, first {mod[0].Title}; Hangar: {hangar.Count} hits, first {hangar[0].Title} ({sw.Elapsed.TotalSeconds:0.0} s)");

                // Hangar: the newest Paper file for this version, checked against the SHA-256 Hangar lists.
                var hv = McHangar.Newest(await McHangar.VersionsAsync(hangar[0].ProjectId, s.Version));
                if (hv == null) return "Hangar lists no version of " + hangar[0].Title + " for " + s.Version;
                await McHangar.InstallAsync(hangar[0], s, null, hv);
                var hjar = Path.Combine(McContent.FolderOf(s), hv.File);
                if (!File.Exists(hjar)) return "the Hangar jar was not installed";
                if (!Sha(hjar, false).Equals(hv.Sha256, StringComparison.OrdinalIgnoreCase)) return "the installed Hangar jar does not match Hangar's SHA-256";
                Say($"Hangar installed {hv.File} {hv.Name}, SHA-256 {hv.Sha256[..12]}… matched");

                // Modrinth: deliberately an older file, so the badge must say Update and Update all must replace it.
                var chunky = mod.First(h => h.Title.Contains("Chunky", StringComparison.OrdinalIgnoreCase));
                var versions = await McModrinth.VersionsAsync(chunky.ProjectId, s);
                if (versions.Count < 2) return "Modrinth lists fewer than two Chunky files for " + s.Version;
                var older = versions[1];
                await McModrinth.InstallVersionAsync(older, s, null, null, chunky.Title);
                var oldName = older.GetProperty("files")[0].GetProperty("filename").GetString();
                var ojar = Path.Combine(McContent.FolderOf(s), oldName);
                if (!File.Exists(ojar)) return "the older Chunky jar was not installed";
                var listed = older.GetProperty("files")[0].GetProperty("hashes").GetProperty("sha512").GetString();
                if (!Sha(ojar, true).Equals(listed, StringComparison.OrdinalIgnoreCase)) return "the installed Modrinth jar does not match the catalogue's SHA-512";
                Say($"Modrinth installed {oldName} (older on purpose), SHA-512 matched");

                var checks = await McCatalogue.CheckAsync(s);
                if (!checks.TryGetValue(oldName, out var ci) || ci.Kind != McCompat.Update) return $"the older Chunky was badged {(ci == null ? "nothing" : ci.Label)}, expected Update";
                if (!checks.TryGetValue(hv.File, out var hi) || hi.Kind != McCompat.Compatible) return $"the Hangar plugin was badged {(hi == null ? "nothing" : hi.Label)}, expected Compatible";
                Say($"badges: {oldName} Update to {ci.Newest}, {hv.File} Compatible");

                var msg = await McCatalogue.UpdateAllAsync(s, checks, null);
                var after = await McCatalogue.CheckAsync(s);
                if (File.Exists(ojar) && Path.GetFileName(ojar) != versions[0].GetProperty("files")[0].GetProperty("filename").GetString()) return "Update all left the older jar in place";
                if (after.Values.Any(v => v.Kind == McCompat.Update)) return "after Update all a badge still says Update";
                if (McContent.LatestBackup(McContent.FolderOf(s)) == null) return "no folder backup was taken";
                Say(msg + " After it every badge is Compatible.");
                return null;
            }
            finally
            {
                try { Directory.Delete(s.Folder, true); } catch { }
                try { File.Delete(Path.Combine(ServerManager.Root, "catalogue", s.Id + ".json")); } catch { }
            }
        }

        // ------------------------------------------------------------------ Bedrock

        private static async Task<string> Bedrock()
        {
            int port = 19132;
            while (!ServerManager.UdpPortFree(port) || !ServerManager.UdpPortFree(port + 1)) port += 2;
            var s = new ServerDef { Name = "Live Bedrock", Flavour = Flavour.Bedrock, Target = Target.LocalProcess, Port = port, EulaAccepted = true };
            var sw = Stopwatch.StartNew();
            await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
            Say($"installed Bedrock {s.Version} in {sw.Elapsed.TotalSeconds:0} s, signature checked");
            var live = McLive.For(s);
            try
            {
                sw.Restart();
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(3);
                while (DateTime.Now < until && !live.Lines().Any(l => l.Message.StartsWith("Server started.", StringComparison.Ordinal)))
                {
                    if (ServerManager.ProcessOf(s) == null) return "the server exited before it was ready: " + Trim(string.Join(" | ", live.Lines().TakeLast(5).Select(l => l.Raw)));
                    await Task.Delay(500);
                }
                if (!live.Lines().Any(l => l.Message.StartsWith("Server started.", StringComparison.Ordinal))) return "no \"Server started.\" line within three minutes";
                Say($"\"Server started.\" after {sw.Elapsed.TotalSeconds:0.0} s");
                var st = await McState.ProbeAsync(s);
                if (st.State != McRunState.Running) return "the state probe said " + st.State;
                await ServerManager.CommandAsync(s, "list");
                for (int i = 0; i < 20 && live.PlayersMax == null; i++) await Task.Delay(500);
                if (live.PlayersOnline != 0 || live.PlayersMax != 7) return $"list answered {live.PlayersOnline} of {live.PlayersMax}, expected 0 of 7";
                Say("list answered 0 of 7");
                var b = await McBackups.BackUpAsync(s, true, "By hand", "live check");
                if (!b.Contents.Contains("worlds")) return "the backup does not hold the worlds folder: " + string.Join(", ", b.Contents);
                Say("a backup while running held the worlds folder");
            }
            finally
            {
                await ServerManager.StopAsync(s);
                ServerManager.Servers.Remove(s); ServerManager.Save();
            }
            if (ServerManager.ProcessOf(s) != null) return "the server did not stop";
            if (!live.Lines().Any(l => l.Raw.Contains("Quit correctly") || l.Raw.Contains("Stopping server"))) return "the log shows no graceful stop: " + Trim(string.Join(" | ", live.Lines().TakeLast(4).Select(l => l.Raw)));
            var props = ServerManager.ReadProperties(s);
            if (props.GetValueOrDefault("server-port") != port.ToString() || props.GetValueOrDefault("max-players") != "7") return "server.properties did not get the port and player limit";
            try { Directory.Delete(s.Folder, true); } catch { }
            try { Directory.Delete(s.BackupFolder, true); } catch { }
            return null;
        }

        // ------------------------------------------------------------------ discovery: a process

        private static async Task<string> DiscoverProcess()
        {
            var dir = Path.Combine(Settings.DataDir, "mt-live-foreign-" + Stamp());
            Directory.CreateDirectory(dir);
            var (jar, javaMajor) = await WorldGenerator.EnsureJarAsync(Version, _ => { });
            var java = await ServerManager.JavaAsync(javaMajor, _ => { });
            File.Copy(jar, Path.Combine(dir, "server.jar"));
            int port = 25565; while (!ServerManager.PortFree(port)) port++;
            int rcon = port + 100; while (!ServerManager.PortFree(rcon)) rcon++;
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
            File.WriteAllText(Path.Combine(dir, "eula.txt"), "eula=true\n");
            File.WriteAllText(Path.Combine(dir, "server.properties"), $"server-port={port}\nmax-players=7\nonline-mode=false\nenable-rcon=true\nrcon.port={rcon}\nrcon.password={password}\nlevel-type=minecraft\\:flat\n");
            // Started the way a person would start it in a terminal, not through the app.
            var p = Process.Start(new ProcessStartInfo(java, "-Xmx1024M -jar server.jar nogui") { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true });
            var output = new StringBuilder();
            p.OutputDataReceived += (o, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            ServerDef s = null;
            try
            {
                await Task.Delay(1500);
                var found = await McDiscovery.FindAsync();
                var mine = found.FirstOrDefault(f => f.Pid == p.Id);
                if (mine == null) return "the process was not discovered; found: " + string.Join(", ", found.Select(f => f.Title + " " + f.Pid));
                if (!string.Equals(Path.GetFullPath(mine.Folder), Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase)) return "the working folder was read as " + mine.Folder;
                if (mine.Jar != "server.jar" || mine.Version != Version || mine.Port != port) return $"read jar {mine.Jar}, version {mine.Version}, port {mine.Port}";
                Say($"discovered process {p.Id}: {mine.Jar}, {mine.Version}, port {port}, folder read from the process");
                if (p.HasExited) return "discovery stopped the process";
                s = McDiscovery.Adopt(mine);
                if (s.FolderPath != mine.Folder || !s.Adopted) return "the adopted entry does not point at the folder";
                var until = DateTime.Now.AddMinutes(4);
                while (DateTime.Now < until && !output.ToString().Contains("RCON running on")) { if (p.HasExited) return "the foreign server exited: " + Trim(output.ToString()); await Task.Delay(1000); }
                if (!output.ToString().Contains("RCON running on")) return "the foreign server never opened RCON";
                var st = await McState.ProbeAsync(s);
                if (st.State != McRunState.Running) return "the adopted server reads as " + st.State + ": " + st.Detail;
                var answer = await ServerManager.CommandAsync(s, "list");
                if (!answer.Contains("of a max of 7")) return "list over RCON answered: " + answer;
                Say("adopted; state Running; list over RCON: " + answer.Trim());
                await ServerManager.StopAsync(s);
                if (!p.WaitForExit(30000)) return "the RCON stop did not end the process";
                Say("stopped over RCON, exit code " + p.ExitCode);
                var after = await McState.ProbeAsync(s);
                return after.State == McRunState.Stopped ? null : "after stopping the state is " + after.State;
            }
            finally
            {
                try { if (!p.HasExited) { p.Kill(true); p.WaitForExit(10000); } } catch { }   // the process this check started
                if (s != null) { ServerManager.Servers.Remove(s); ServerManager.Save(); }
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        // ------------------------------------------------------------------ discovery: a container

        private static async Task<string> DiscoverContainer()
        {
            NeedDocker();
            var name = "mt-live-foreign-" + Stamp();
            int port = 25565; while (!ServerManager.PortFree(port)) port++;
            ServerDef s = null;
            try
            {
                var run = Docker($"run -d --name {name} -e EULA=TRUE -e VERSION={Version} -e MAX_PLAYERS=7 -p 127.0.0.1:{port}:25565 itzg/minecraft-server:latest", 600000);
                if (run.Code != 0) return "docker run failed: " + ServerManager.ActionLine(run.Out);
                var found = await McDiscovery.FindAsync();
                var it = found.FirstOrDefault(f => f.Container == name);
                if (it == null) return "the container was not discovered; found: " + string.Join(", ", found.Select(f => f.Title));
                if (it.Port != port || it.Version != Version || it.Flavour != Flavour.Vanilla) return $"read port {it.Port}, version {it.Version}, software {it.Flavour}";
                if (found.Any(f => f.Kind == "docker" && ServerManager.Servers.Any(x => x.Target == Target.LocalDocker && x.ContainerName == f.Container))) return "a container the app already manages was listed as found";
                Say($"discovered container {name}: {it.Image}, version {it.Version}, port {it.Port}");
                s = McDiscovery.Adopt(it);
                if (s.ContainerName != name || !s.Adopted) return "the adopted entry does not point at the container";
                var st = await McState.ProbeAsync(s);
                if (st.State != McRunState.Running) return "the adopted container reads as " + st.State;
                try { ServerManager.DeleteContainer(s); return "the app removed a container it did not make"; }
                catch (InvalidOperationException) { }
                if (Docker($"inspect {name}", 15000).Code != 0) return "the container went away";
                // Its server.properties lives in a Docker volume, so it is read through docker exec once the server writes it.
                string text = null;
                for (int i = 0; i < 120 && (text == null || !text.Contains("max-players")); i++) { text = ServerManager.ReadPropertiesText(s); await Task.Delay(1000); }
                if (text == null || !text.Contains("max-players=7")) return "server.properties could not be read from the volume: " + Trim(text);
                Say("adopted; state Running; server.properties read through docker exec; the app refused to remove the container");
                return null;
            }
            finally
            {
                if (s != null) { ServerManager.Servers.Remove(s); ServerManager.Save(); }
                Docker($"rm -f {name}", 60000);   // the container this check made
            }
        }

        // ------------------------------------------------------------------ the local Docker target

        private static async Task<string> DockerTarget()
        {
            NeedDocker();
            int port = 25565; while (!ServerManager.PortFree(port) || ServerManager.Servers.Any(x => x.Port == port)) port++;
            var s = new ServerDef { Name = "Live Docker", Version = Version, Flavour = Flavour.Vanilla, Target = Target.LocalDocker, Port = port, MemoryMb = 1024, EulaAccepted = true };
            var sw = Stopwatch.StartNew();
            await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7" });
            Say($"created {s.ContainerName} in {sw.Elapsed.TotalSeconds:0} s");
            try
            {
                sw.Restart();
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(8);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 400)).Contains("Done (")) await Task.Delay(2000);
                if (!log.Contains("Done (")) return "no Done line within eight minutes: " + Trim(ServerManager.ActionLine(log));
                Say($"Done after {sw.Elapsed.TotalSeconds:0} s");
                var st = await McState.ProbeAsync(s);
                if (st.State != McRunState.Running && st.State != McRunState.Starting) return "the state probe said " + st.State;
                var answer = await ServerManager.CommandAsync(s, "list");
                if (!answer.Contains("of a max of 7")) return "list answered: " + answer;
                Say("list: " + answer.Trim());
                await ServerManager.StopAsync(s);
                var after = await McState.ProbeAsync(s);
                if (after.State != McRunState.Stopped) return "after stopping the state is " + after.State;
                ServerManager.DeleteContainer(s);
                if (Docker($"inspect {s.ContainerName}", 15000).Code == 0) return "the container is still there after removal";
                Say("stopped and removed (label checked)");
                return null;
            }
            finally
            {
                if (Docker($"inspect {s.ContainerName}", 15000).Code == 0) { try { ServerManager.DeleteContainer(s); } catch { } }
                ServerManager.Servers.Remove(s); ServerManager.Save();
                try { Directory.Delete(s.Folder, true); } catch { }
            }
        }

        // ------------------------------------------------------------------ the SSH target

        private static async Task<string> SshTarget()
        {
            NeedDocker();
            var dir = Path.Combine(Settings.DataDir, "ssh-live-" + Stamp());
            Directory.CreateDirectory(dir);
            var (key, pub) = KeyPair(dir);
            int sshPort = 22220; while (!ServerManager.PortFree(sshPort)) sshPort++;
            var name = "mt-live-ssh-" + Stamp();
            var user = ThrowawayUser();
            ServerDef s = null;
            try
            {
                var sw = Stopwatch.StartNew();
                var run = Docker($"run -d --name {name} -e PUID=1000 -e PGID=1000 -e USER_NAME={user} -e \"PUBLIC_KEY={pub}\" -p 127.0.0.1:{sshPort}:2222 lscr.io/linuxserver/openssh-server:latest", 600000);
                if (run.Code != 0) return "docker run failed: " + ServerManager.ActionLine(run.Out);
                // The throwaway host needs Java 21 and curl for the app's SSH route; the image has neither.
                var apk = Docker($"exec {name} apk add --no-cache openjdk21-jre-headless curl", 900000);
                if (apk.Code != 0) return "installing Java in the SSH host failed: " + ServerManager.ActionLine(apk.Out);
                Say($"SSH host {name} on 127.0.0.1:{sshPort} with Java 21 ready in {sw.Elapsed.TotalSeconds:0} s");

                var host = SshHostSpec(user, sshPort);
                var approve = await ApproveHost(host);
                if (approve != null) return approve;
                Say("host key read and approved into the app's own known_hosts");

                s = new ServerDef { Name = "Live SSH", Version = Version, Flavour = Flavour.Vanilla, Target = Target.SshProcess, SshHost = host, SshKey = key, Port = 25565, MemoryMb = 1024, EulaAccepted = true, RemoteDir = "live-check" };
                sw.Restart();
                await ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7", ["online-mode"] = "false" });
                Say($"created over SSH in {sw.Elapsed.TotalSeconds:0} s (jar SHA-1 checked on the host)");
                sw.Restart();
                await ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(8);
                string log = "";
                while (DateTime.Now < until && !(log = await ServerManager.LogTailAsync(s, 300)).Contains("Done (")) await Task.Delay(2000);
                if (!log.Contains("Done (")) return "no Done line within eight minutes: " + Trim(ServerManager.ActionLine(log));
                Say($"Done after {sw.Elapsed.TotalSeconds:0} s");
                var st = await McState.ProbeAsync(s);
                if (st.State != McRunState.Running && st.State != McRunState.Starting) return "the state probe said " + st.State + ": " + st.Detail;
                await ServerManager.CommandAsync(s, "list");
                string answer = null;
                for (int i = 0; i < 20 && answer == null; i++)
                {
                    await Task.Delay(1000);
                    answer = (await ServerManager.LogTailAsync(s, 40)).Split('\n').LastOrDefault(l => l.Contains("players online"));
                }
                if (answer == null || !answer.Contains("of a max of 7")) return "list answered: " + Trim(answer);
                Say("list: " + McLogLine.Parse(answer).Message.Trim());
                await ServerManager.StopAsync(s);
                var after = await McState.ProbeAsync(s);
                if (after.State != McRunState.Stopped) return "after stopping the state is " + after.State;
                Say("stopped");
                return null;
            }
            finally
            {
                if (s != null) { ServerManager.Servers.Remove(s); ServerManager.Save(); }
                Docker($"rm -f {name}", 60000);   // the container this check made
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
