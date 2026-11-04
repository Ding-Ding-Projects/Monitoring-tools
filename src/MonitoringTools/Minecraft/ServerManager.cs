using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MonitoringTools.Core;
using Activity = MonitoringTools.Core.Activity;
using ActivityKind = MonitoringTools.Core.ActivityKind;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// Creates and runs Minecraft Java servers on four targets: a local process, local Docker,
    /// a process on an SSH host, and Docker on an SSH host. Versions come from Mojang's official
    /// catalogue; server jars are checked against the SHA-1 in their version metadata; Java
    /// runtimes come from Adoptium and are checked against its published SHA-256. Nothing runs
    /// until the EULA is accepted for that server.
    /// </summary>
    public static class ServerManager
    {
        public const string Catalogue = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        private static readonly Dictionary<string, Process> Running = new Dictionary<string, Process>();
        public static event Action<string, string> Output;   // (server id, line)
        public static event Action Changed;

        static ServerManager() { Http.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0"); }

        public static string Root { get { var d = Path.Combine(Settings.DataDir, "minecraft"); Directory.CreateDirectory(d); return d; } }
        private static string ListFile => Path.Combine(Root, "servers.json");

        public static List<ServerDef> Servers { get; private set; } = Load();

        private static List<ServerDef> Load()
        {
            try
            {
                if (File.Exists(ListFile))
                {
                    var list = JsonSerializer.Deserialize<List<ServerDef>>(File.ReadAllText(ListFile)) ?? new List<ServerDef>();
                    foreach (var s in list) Migrate(s);
                    return list;
                }
            }
            catch (Exception ex)
            {
                // A file that cannot be read is kept aside, never overwritten, so no server is lost.
                try { File.Copy(ListFile, ListFile + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), false); } catch { }
                Log.Error("Minecraft servers.json could not be read", ex);
            }
            return new List<ServerDef>();
        }

        /// <summary>Fills fields that an older servers.json does not have, in place. Nothing that was stored is changed.</summary>
        internal static void Migrate(ServerDef s)
        {
            s.Build ??= "";
            s.BackupDir ??= "";
            s.SshHost ??= "";
            s.RemoteDir ??= "";
            if (s.KeepBackups <= 0) s.KeepBackups = 14;
            s.Schedules ??= new List<McSchedule>();
            s.RecentCommands ??= new List<string>();
            s.SshKey ??= "";
            s.FolderPath ??= "";
            s.Container ??= "";
            if (string.IsNullOrWhiteSpace(s.JarName)) s.JarName = "server.jar";
            s.FavouriteCommands ??= new List<string>();
        }

        /// <summary>Reloads servers.json (used when the data folder changes under a self-test).</summary>
        internal static void Reload() { Servers = Load(); Changed?.Invoke(); }

        public static void Save()
        {
            // Write to a temporary file first, so a crash mid-write never leaves a half-written list.
            var tmp = ListFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Servers, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, ListFile, true);
            Changed?.Invoke();
        }

        // -------------------------------------------------------------- versions

        private static List<McVersion> _versions;

        /// <summary>Every entry in the official catalogue, newest first. Nothing is curated out.</summary>
        public static async Task<List<McVersion>> VersionsAsync()
        {
            if (_versions != null) return _versions;
            var json = await Http.GetStringAsync(Catalogue);
            using var doc = JsonDocument.Parse(json);
            _versions = doc.RootElement.GetProperty("versions").EnumerateArray().Select(v => new McVersion
            {
                Id = v.GetProperty("id").GetString(),
                Type = v.GetProperty("type").GetString(),
                Url = v.GetProperty("url").GetString(),
                Sha1 = v.TryGetProperty("sha1", out var s) ? s.GetString() : null,
                Released = v.GetProperty("releaseTime").GetDateTime()
            }).ToList();
            return _versions;
        }

        public sealed class Distribution { public string Url, Sha1; public long Size; public int JavaMajor; public string Missing; }

        /// <summary>Reads the version's own metadata: is there a dedicated server, and which Java does it need.</summary>
        public static async Task<Distribution> ResolveAsync(string versionId)
        {
            var v = (await VersionsAsync()).FirstOrDefault(x => x.Id == versionId);
            if (v == null) return new Distribution { Missing = "That version is not in the official catalogue." };
            var bytes = await Http.GetByteArrayAsync(v.Url);
            if (v.Sha1 != null && !string.Equals(Convert.ToHexString(SHA1.HashData(bytes)), v.Sha1, StringComparison.OrdinalIgnoreCase))
                return new Distribution { Missing = "The version metadata did not match its catalogue checksum." };
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            int java = root.TryGetProperty("javaVersion", out var jv) && jv.TryGetProperty("majorVersion", out var mv) ? mv.GetInt32() : 0;
            if (!root.TryGetProperty("downloads", out var dl) || !dl.TryGetProperty("server", out var srv))
                return new Distribution { Missing = $"Mojang publishes no dedicated server for {versionId}.", JavaMajor = java };
            return new Distribution { Url = srv.GetProperty("url").GetString(), Sha1 = srv.GetProperty("sha1").GetString(), Size = srv.GetProperty("size").GetInt64(), JavaMajor = java == 0 ? 8 : java };
        }

        // -------------------------------------------------------------- Java runtimes

        /// <summary>A Temurin JRE for the major version, obtained once per user and checked against Adoptium's SHA-256.</summary>
        public static async Task<string> JavaAsync(int major, Action<string> report)
        {
            var dir = Path.Combine(Settings.DataDir, "runtimes", "jre-" + major);
            var exe = Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "java.exe", SearchOption.AllDirectories).FirstOrDefault() : null;
            if (exe != null) return exe;

            report?.Invoke($"Getting Java {major} (Eclipse Temurin)…");
            var api = $"https://api.adoptium.net/v3/assets/latest/{major}/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(api));
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Object)
            {
                // Some older majors ship only a JDK build for Windows.
                api = api.Replace("image_type=jre", "image_type=jdk");
                using var doc2 = JsonDocument.Parse(await Http.GetStringAsync(api));
                first = doc2.RootElement.EnumerateArray().FirstOrDefault().Clone();
                if (first.ValueKind != JsonValueKind.Object) throw new InvalidOperationException($"Adoptium has no Windows Java {major} build.");
            }
            var pkg = first.GetProperty("binary").GetProperty("package");
            var url = pkg.GetProperty("link").GetString();
            var sha = pkg.GetProperty("checksum").GetString();
            var zip = Path.Combine(Path.GetTempPath(), $"mt-jre-{major}-{Guid.NewGuid():N}.zip");
            await Download(url, zip, report);
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))), sha, StringComparison.OrdinalIgnoreCase))
            { File.Delete(zip); throw new InvalidDataException($"Java {major} download failed its SHA-256 check."); }
            ZipFile.ExtractToDirectory(zip, dir, true);
            File.Delete(zip);
            return Directory.EnumerateFiles(dir, "java.exe", SearchOption.AllDirectories).First();
        }

        internal static async Task Download(string url, string path, Action<string> report)
        {
            using var r = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            r.EnsureSuccessStatusCode();
            var total = r.Content.Headers.ContentLength ?? 0;
            await using var s = await r.Content.ReadAsStreamAsync();
            await using var f = File.Create(path);
            var buf = new byte[1 << 16];
            long done = 0; int n; var last = DateTime.UtcNow;
            while ((n = await s.ReadAsync(buf)) > 0)
            {
                await f.WriteAsync(buf.AsMemory(0, n));
                done += n;
                if ((DateTime.UtcNow - last).TotalMilliseconds > 400 && total > 0) { report?.Invoke($"Downloading {done * 100 / total}% of {total / 1048576} MB"); last = DateTime.UtcNow; }
            }
        }

        // -------------------------------------------------------------- prerequisites

        public static bool PortFree(int port) =>
            !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port);

        /// <summary>Bedrock players connect over UDP, so a Bedrock port is checked against UDP listeners.</summary>
        public static bool UdpPortFree(int port) =>
            !IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(e => e.Port == port);

        public static bool PortFreeFor(ServerDef s) => s.IsBedrock ? UdpPortFree(s.Port) : PortFree(s.Port);

        /// <summary>Runs a tool and collects its output. <paramref name="workingDir"/> matters for a Minecraft jar: its bundler
        /// unpacks libraries/, versions/ and logs/ into the working folder, so a jar must never run in the app's own.</summary>
        public static (int Code, string Out) Run(string exe, string args, int timeoutMs = 120000, string stdin = null, string workingDir = null)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = stdin != null, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                if (!string.IsNullOrEmpty(workingDir)) { Directory.CreateDirectory(workingDir); psi.WorkingDirectory = workingDir; }
                using var p = Process.Start(psi);
                if (stdin != null) { p.StandardInput.Write(stdin); p.StandardInput.Close(); }
                var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return (-1, "timed out"); }
                return (p.ExitCode, (o.Result + "\n" + e.Result).Trim());
            }
            catch (Exception ex) { return (-1, ex.Message); }
        }

        public static string SshExe()
        {
            var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
            if (File.Exists(sys)) return sys;
            var git = BundledTools.GitExe();
            var mingitSsh = git == null ? null : Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(git)), "usr", "bin", "ssh.exe");
            return mingitSsh != null && File.Exists(mingitSsh) ? mingitSsh : "ssh";
        }

        internal static (string Host, string Port) SplitHost(string hostSpec)
        {
            var h = hostSpec.Trim();
            var at = h.LastIndexOf(':');
            if (at > h.IndexOf('@') && at > 0 && int.TryParse(h[(at + 1)..], out _)) return (h[..at], h[(at + 1)..]);
            return (h, "22");
        }

        internal static string KnownHosts => Path.Combine(Root, "known_hosts");

        /// <summary>The host key fingerprint to show for explicit approval before first use.</summary>
        public static (bool Known, string Fingerprints) HostKey(string hostSpec)
        {
            var (host, port) = SplitHost(hostSpec);
            var bare = host.Contains('@') ? host[(host.IndexOf('@') + 1)..] : host;
            if (File.Exists(KnownHosts) && Run(Path.Combine(Path.GetDirectoryName(SshExe()), "ssh-keygen.exe"), $"-F \"{(port == "22" ? bare : $"[{bare}]:{port}")}\" -f \"{KnownHosts}\"").Code == 0)
                return (true, "approved");
            var scan = Run(Path.Combine(Path.GetDirectoryName(SshExe()), "ssh-keyscan.exe"), $"-p {port} {bare}", 20000);
            if (scan.Code == 0 && KeyLines(scan.Out).Any()) return (false, scan.Out);
            // ssh-keyscan cannot agree a key exchange with some newer servers (OpenSSH 10 offers only methods the Windows
            // build of ssh-keyscan lacks), while ssh itself can. So ssh is asked to record the key into a scratch file it
            // is told to accept new keys into; nothing is signed in, and the app's own known_hosts is not touched.
            var scratch = Path.Combine(Path.GetTempPath(), "mt-hostkey-" + Guid.NewGuid().ToString("N"));
            try
            {
                Run(SshExe(), $"-p {port} -o BatchMode=yes -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile=\"{scratch}\" -o PreferredAuthentications=none -o ConnectTimeout=15 {host} exit", 25000);
                var lines = File.Exists(scratch) ? KeyLines(File.ReadAllText(scratch)).ToList() : new List<string>();
                if (lines.Count > 0) return (false, string.Join("\n", lines));
            }
            finally { try { File.Delete(scratch); } catch { } }
            return (false, "Could not reach the host to read its key: " + ActionLine(scan.Out));
        }

        private static IEnumerable<string> KeyLines(string text) =>
            (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#") && (l.Contains(" ssh-") || l.Contains(" ecdsa-")));

        public static void ApproveHostKey(string scanOutput)
        {
            // Only key lines are kept; any message ssh-keyscan printed beside them is left out.
            File.AppendAllLines(KnownHosts, KeyLines(scanOutput));
        }

        /// <summary>Runs a command on the SSH host with key authentication only and a strict, app-owned known_hosts.</summary>
        public static (int Code, string Out) Ssh(ServerDef s, string remote, int timeoutMs = 180000)
        {
            var (host, port) = SplitHost(s.SshHost);
            var args = $"-p {port} {KeyArgs(s)}-o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=\"{KnownHosts}\" -o ConnectTimeout=15 {host} {Quote(remote)}";
            return Run(SshExe(), args, timeoutMs);
        }

        private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

        /// <summary>The identity file option when the server names its own key, so ssh uses that key and no other.</summary>
        internal static string KeyArgs(ServerDef s) => string.IsNullOrWhiteSpace(s.SshKey) ? "" : $"-i \"{s.SshKey.Trim()}\" -o IdentitiesOnly=yes ";
        internal static string Sh(string s) => "'" + s.Replace("'", "'\\''") + "'";

        // -------------------------------------------------------------- create

        /// <param name="initial">server.properties values for the first start, such as max-players and level-seed.</param>
        /// <param name="worldSource">A world folder or ZIP to copy in as the server's world, or null for a new world.</param>
        public static async Task CreateAsync(ServerDef s, Action<string> report, IDictionary<string, string> initial = null, string worldSource = null)
        {
            if (!s.EulaAccepted) throw new InvalidOperationException("Accept the Minecraft EULA for this server first.");
            var props = new Dictionary<string, string> { ["server-port"] = s.Port.ToString(), ["motd"] = Escape(s.Name) };
            if (initial != null) foreach (var (k, v) in initial) props[k] = Escape(v);
            if (s.IsBedrock && s.Target != Target.LocalProcess)
            {
                if (worldSource != null) throw new NotSupportedException("Bedrock worlds are made new here; import an existing world from the Worlds section after the server is made.");
                await McBedrock.InstallOtherAsync(s, report, initial);
                Servers.Add(s);
                Save();
                Activity.Add(ActivityKind.Service, $"Minecraft server created: {s.Name}", $"Bedrock {s.Version} · {s.Target}");
                return;
            }
            switch (s.Target)
            {
                case Target.LocalProcess when s.IsBedrock:
                {
                    if (worldSource != null) throw new NotSupportedException("Bedrock worlds are made new here; copy an existing world into its worlds folder after the server is made.");
                    await McBedrock.InstallAsync(s, report, initial);
                    break;
                }
                case Target.LocalProcess:
                {
                    if (!McSoftware.RunsAsLocalProcess(s.Flavour)) throw new NotSupportedException($"{s.Flavour} needs its own installer, so it runs on the Docker targets. On this computer as a process, choose Vanilla, Paper or Purpur.");
                    var d = await ResolveAsync(s.Version);
                    if (d.Missing != null) throw new InvalidOperationException(d.Missing);
                    Directory.CreateDirectory(s.Folder);
                    var jar = Path.Combine(s.Folder, "server.jar");
                    if (s.Flavour == Flavour.Vanilla)
                    {
                        report($"Downloading the {s.Version} server ({d.Size / 1048576.0:0.0} MB)…");
                        await Download(d.Url, jar + ".part", report);
                        if (!string.Equals(Convert.ToHexString(SHA1.HashData(File.ReadAllBytes(jar + ".part"))), d.Sha1, StringComparison.OrdinalIgnoreCase))
                        { File.Delete(jar + ".part"); throw new InvalidDataException("The server jar failed its SHA-1 check."); }
                    }
                    else await DownloadSoftwareJarAsync(s, jar + ".part", report);
                    File.Move(jar + ".part", jar, true);
                    await JavaAsync(d.JavaMajor, report);
                    File.WriteAllText(Path.Combine(s.Folder, "eula.txt"), $"# Accepted in Monitoring Tools on {DateTime.Now:O}\neula=true\n");
                    if (worldSource != null) McWorlds.ImportInto(s, worldSource, report);
                    WriteProperties(s, props);
                    break;
                }
                case Target.LocalDocker:
                {
                    var info = Run("docker", "info --format \"{{.ServerVersion}}\"", 20000);
                    if (info.Code != 0) throw new InvalidOperationException("Docker is not running on this computer: " + ActionLine(info.Out));
                    Directory.CreateDirectory(s.Folder);
                    if (worldSource != null) McWorlds.ImportInto(s, worldSource, report);
                    WriteProperties(s, props);
                    report("Pulling the server image…");
                    var pull = Run("docker", "pull itzg/minecraft-server:latest", 900000);
                    if (pull.Code != 0) throw new InvalidOperationException("Pull failed: " + ActionLine(pull.Out));
                    var mk = Run("docker", DockerCreateArgs(s, $"\"{s.Folder}\"", props), 60000);
                    if (mk.Code != 0) throw new InvalidOperationException("Container create failed: " + ActionLine(mk.Out));
                    break;
                }
                case Target.SshProcess:
                {
                    if (s.Flavour != Flavour.Vanilla) throw new NotSupportedException("SSH process servers run Vanilla. Use an SSH Docker target for other software.");
                    if (worldSource != null) throw new NotSupportedException("Copy an existing world to an SSH host from the Worlds section after the server is created.");
                    var d = await ResolveAsync(s.Version);
                    if (d.Missing != null) throw new InvalidOperationException(d.Missing);
                    report("Preparing the folder on the SSH host…");
                    var dir = s.RemoteFolder;
                    var lines = string.Concat(props.Select(kv => $"{kv.Key}={kv.Value}\\n"));
                    var script = $"set -e; mkdir -p {Sh(dir)}; cd {Sh(dir)}; " +
                                 $"command -v java >/dev/null || {{ echo 'Java is not installed on this host (needs Java {d.JavaMajor}+)'; exit 3; }}; " +
                                 $"curl -fsSL -o server.jar.part {Sh(d.Url)} || wget -q -O server.jar.part {Sh(d.Url)}; " +
                                 $"echo {Sh(d.Sha1)}'  server.jar.part' | sha1sum -c - >/dev/null; mv server.jar.part server.jar; " +
                                 $"printf 'eula=true\\n' > eula.txt; printf {Sh(lines)} > server.properties";
                    var r = Ssh(s, script, 900000);
                    if (r.Code != 0) throw new InvalidOperationException("SSH setup failed: " + ActionLine(r.Out));
                    break;
                }
                case Target.SshDocker:
                {
                    if (worldSource != null) throw new NotSupportedException("Copy an existing world to an SSH host from the Worlds section after the server is created.");
                    var info = Ssh(s, "docker info --format '{{.ServerVersion}}'", 30000);
                    if (info.Code != 0) throw new InvalidOperationException("Docker is not available on the SSH host: " + ActionLine(info.Out));
                    report("Pulling the server image on the SSH host…");
                    var dir = s.RemoteFolder;
                    var r = Ssh(s, $"mkdir -p {Sh(dir)} && docker pull itzg/minecraft-server:latest >/dev/null && docker {DockerCreateArgs(s, $"\"$(cd {Sh(dir)} && pwd)\"", props)}", 900000);
                    if (r.Code != 0) throw new InvalidOperationException("Container create failed: " + ActionLine(r.Out));
                    break;
                }
            }
            Servers.Add(s);
            Save();
            Activity.Add(ActivityKind.Service, $"Minecraft server created: {s.Name}", $"{s.Version} · {s.Target}");
        }

        /// <summary>Downloads a Paper or Purpur jar from its project's API and checks it against the hash that API publishes.</summary>
        private static async Task DownloadSoftwareJarAsync(ServerDef s, string path, Action<string> report)
        {
            var list = await McSoftware.BuildsAsync(s.Flavour, s.Version);
            if (list.Unavailable != null) throw new InvalidOperationException(list.Unavailable);
            var build = string.IsNullOrWhiteSpace(s.Build) ? list.Latest : list.Builds.FirstOrDefault(b => b.Id == s.Build);
            if (build?.DownloadUrl == null) throw new InvalidOperationException($"{s.Flavour} build {s.Build} is not listed for {s.Version}.");
            s.Build = build.Id;
            report($"Downloading {s.Flavour} {s.Version} build {build.Id}…");
            await Download(build.DownloadUrl, path, report);
            var bytes = File.ReadAllBytes(path);
            string expected = build.Hash, kind = build.HashKind;
            if (s.Flavour == Flavour.Purpur) { expected = await McSoftware.PurpurMd5Async(s.Version, build.Id); kind = "md5"; }
            if (expected == null) { File.Delete(path); throw new InvalidDataException($"{s.Flavour} publishes no checksum for this build, so it was not installed."); }
            var actual = kind == "md5" ? Convert.ToHexString(MD5.HashData(bytes)) : Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) { File.Delete(path); throw new InvalidDataException($"The {s.Flavour} jar failed its {kind.ToUpperInvariant()} check."); }
        }

        private static string DockerCreateArgs(ServerDef s, string volume, IDictionary<string, string> props)
        {
            var env = new StringBuilder();
            env.Append($"-e EULA=TRUE -e VERSION={s.Version} -e TYPE={s.Flavour.ToString().ToUpperInvariant()} -e MEMORY={s.MemoryMb}M -e MOTD=\"{Escape(s.Name)}\" ");
            if (!string.IsNullOrWhiteSpace(s.Build))
            {
                var key = s.Flavour switch { Flavour.Paper => "PAPER_BUILD", Flavour.Purpur => "PURPUR_BUILD", Flavour.Fabric => "FABRIC_LOADER_VERSION", Flavour.Forge => "FORGE_VERSION", Flavour.NeoForge => "NEOFORGE_VERSION", _ => null };
                if (key != null) env.Append($"-e {key}={Escape(s.Build)} ");
            }
            if (props.TryGetValue("max-players", out var mp)) env.Append($"-e MAX_PLAYERS={Escape(mp)} ");
            if (props.TryGetValue("level-seed", out var seed) && seed.Length > 0) env.Append($"-e SEED=\"{Escape(seed)}\" ");
            return $"create --name {s.ContainerName} --label monitoring-tools.server={s.Id} -p {s.Port}:25565 -v {volume}:/data {env}-i -t itzg/minecraft-server:latest";
        }

        /// <summary>The live process of a local process server, or null when it is not running from this app.</summary>
        internal static Process ProcessOf(ServerDef s) => Running.TryGetValue(s.Id, out var p) && !p.HasExited ? p : null;

        internal static string Escape(string s) => (s ?? "").Replace("\\", "").Replace("\"", "").Replace("'", "").Replace("\n", " ");
        internal static string ActionLine(string s) => (s ?? "").Split('\n').Where(l => l.Trim().Length > 0).LastOrDefault() ?? "";

        // -------------------------------------------------------------- run

        public static bool IsRunning(ServerDef s)
        {
            switch (s.Target)
            {
                case Target.LocalProcess: return (Running.TryGetValue(s.Id, out var p) && !p.HasExited) || McDiscovery.ExternalProcess(s) != null;
                case Target.LocalDocker: return Run("docker", $"inspect -f \"{{{{.State.Running}}}}\" {s.ContainerName}", 15000).Out.Trim() == "true";
                default: return false;   // remote state is read on demand with Status()
            }
        }

        public static async Task<string> StatusAsync(ServerDef s) => await Task.Run(() =>
        {
            switch (s.Target)
            {
                case Target.LocalProcess: return IsRunning(s) ? "running" : "stopped";
                case Target.LocalDocker: { var r = Run("docker", $"inspect -f \"{{{{.State.Status}}}}\" {s.ContainerName}", 15000); return r.Code == 0 ? r.Out.Trim() : "missing"; }
                case Target.SshProcess: { var r = Ssh(s, $"cd {Sh(s.RemoteFolder)} 2>/dev/null && test -f server.pid && kill -0 $(head -n1 server.pid) 2>/dev/null && echo running || echo stopped", 30000); return r.Code == 0 ? r.Out.Trim() : "host unreachable"; }
                case Target.SshDocker: { var r = Ssh(s, $"docker inspect -f '{{{{.State.Status}}}}' {s.ContainerName}", 30000); return r.Code == 0 ? r.Out.Trim() : "host unreachable"; }
            }
            return "unknown";
        });

        public static async Task StartAsync(ServerDef s, Action<string> report)
        {
            if (s.Target == Target.LocalProcess && IsRunning(s)) return;
            if (!PortFreeFor(s) && (s.Target == Target.LocalProcess || s.Target == Target.LocalDocker))
                throw new InvalidOperationException($"Port {s.Port} is already in use on this computer.");
            McLive.For(s).MarkStarting();
            switch (s.Target)
            {
                case Target.LocalProcess:
                {
                    if (IsRunning(s)) return;
                    ProcessStartInfo psi;
                    if (s.IsBedrock)
                    {
                        var exe = Path.Combine(s.Folder, McBedrock.Exe);
                        if (!File.Exists(exe)) throw new FileNotFoundException("bedrock_server.exe is not in the server's folder.");
                        psi = new ProcessStartInfo(exe, "");
                    }
                    else
                    {
                        var d = await ResolveAsync(s.Version);
                        var java = await JavaAsync(d.JavaMajor == 0 ? 8 : d.JavaMajor, report);
                        var jarName = string.IsNullOrWhiteSpace(s.JarName) ? "server.jar" : s.JarName;
                        psi = new ProcessStartInfo(java, $"-Xms{Math.Min(1024, s.MemoryMb)}M -Xmx{s.MemoryMb}M -jar \"{jarName}\" nogui");
                    }
                    psi.WorkingDirectory = s.Folder; psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    psi.RedirectStandardInput = true; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                    var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    var log = Path.Combine(s.Folder, "console.log");
                    void Line(string l) { if (l == null) return; try { File.AppendAllText(log, l + Environment.NewLine); } catch { } Output?.Invoke(s.Id, l); }
                    p.OutputDataReceived += (o, e) => Line(e.Data);
                    p.ErrorDataReceived += (o, e) => Line(e.Data);
                    p.Exited += (o, e) => { Output?.Invoke(s.Id, $"[server exited with code {p.ExitCode}]"); Changed?.Invoke(); };
                    p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    Running[s.Id] = p;
                    break;
                }
                case Target.LocalDocker: Check(Run("docker", $"start {s.ContainerName}", 60000), "start"); break;
                case Target.SshProcess:
                    Check(Ssh(s, $"cd {Sh(s.RemoteFolder)} && (test -f server.pid && kill -0 $(head -n1 server.pid) 2>/dev/null) || " +
                                 $"{{ mkfifo -m 600 console.in 2>/dev/null || true; nohup sh -c 'tail -f console.in | {(s.IsBedrock ? "env LD_LIBRARY_PATH=. ./bedrock_server" : $"java -Xmx{s.MemoryMb}M -jar server.jar nogui")}' > console.log 2>&1 & echo $! > server.pid; }}", 60000), "start");
                    break;
                case Target.SshDocker: Check(Ssh(s, $"docker start {s.ContainerName}", 60000), "start"); break;
            }
            Activity.Add(ActivityKind.Service, $"Minecraft server started: {s.Name}", s.Target.ToString());
            Changed?.Invoke();
        }

        /// <summary>Graceful stop: the server's own stop command, so the world is saved.</summary>
        public static async Task StopAsync(ServerDef s) => await Task.Run(() =>
        {
            switch (s.Target)
            {
                case Target.LocalProcess:
                    if (Running.TryGetValue(s.Id, out var p) && !p.HasExited)
                    {
                        p.StandardInput.WriteLine("stop");
                        if (!p.WaitForExit(60000)) { try { p.Kill(true); } catch { } }
                    }
                    else if (McDiscovery.ExternalProcess(s) != null)
                    {
                        // Started outside this app: its console belongs to another program, so the only graceful stop is RCON.
                        // The process is never killed from here.
                        var rc = McRcon.From(s) ?? throw new InvalidOperationException("This server was started outside the app. Turn on RCON in its settings (enable-rcon and rcon.password) to stop it from here, or stop it where it was started.");
                        McRcon.SendAsync(rc.Port, rc.Password, "stop").GetAwaiter().GetResult();
                        for (int i = 0; i < 60 && McDiscovery.ExternalProcess(s) != null; i++) System.Threading.Thread.Sleep(1000);
                        if (McDiscovery.ExternalProcess(s) != null) throw new InvalidOperationException("The server was asked to stop over RCON but is still running after a minute.");
                    }
                    break;
                case Target.LocalDocker: Check(Run("docker", $"stop -t 60 {s.ContainerName}", 90000), "stop"); break;
                case Target.SshProcess: Check(Ssh(s, $"cd {Sh(s.RemoteFolder)} && test -f server.pid && {{ echo stop > console.in; sleep 20; kill $(head -n1 server.pid) 2>/dev/null; rm -f server.pid; }} || true", 90000), "stop"); break;
                case Target.SshDocker: Check(Ssh(s, $"docker stop -t 60 {s.ContainerName}", 90000), "stop"); break;
            }
            McLive.For(s).Stopped();
            Activity.Add(ActivityKind.Service, $"Minecraft server stopped: {s.Name}", s.Target.ToString());
            Changed?.Invoke();
        });

        private static void Check((int Code, string Out) r, string what)
        {
            if (r.Code != 0) throw new InvalidOperationException($"Could not {what} the server: {ActionLine(r.Out)}");
        }

        /// <summary>Sends one console command built by the typed command builder.</summary>
        public static async Task<string> CommandAsync(ServerDef s, string command) => await Task.Run(() =>
        {
            command = command.Replace("\r", " ").Replace("\n", " ").Trim().TrimStart('/');
            switch (s.Target)
            {
                case Target.LocalProcess:
                    if (Running.TryGetValue(s.Id, out var p) && !p.HasExited) { p.StandardInput.WriteLine(command); return "Sent."; }
                    if (McDiscovery.ExternalProcess(s) != null)
                    {
                        var rc = McRcon.From(s);
                        if (rc == null) return "This server was started outside the app, so commands need RCON. Turn on enable-rcon and set rcon.password in its settings.";
                        try { var a = McRcon.SendAsync(rc.Port, rc.Password, command).GetAwaiter().GetResult(); return a.Length > 0 ? a : "Sent."; }
                        catch (Exception ex) { return "RCON: " + ex.Message; }
                    }
                    return "The server is not running.";
                case Target.LocalDocker: { var r = Run("docker", $"exec {s.ContainerName} {(s.IsBedrock ? "send-command \"" + command.Replace("\"", "\\\"") + "\"" : "rcon-cli " + command)}", 30000); return r.Code == 0 ? (r.Out.Length > 0 ? r.Out : "Sent.") : ActionLine(r.Out); }
                case Target.SshProcess: { var r = Ssh(s, $"cd {Sh(s.RemoteFolder)} && echo {Sh(command)} > console.in", 30000); return r.Code == 0 ? "Sent." : ActionLine(r.Out); }
                case Target.SshDocker: { var r = Ssh(s, $"docker exec {s.ContainerName} {(s.IsBedrock ? "send-command" : "rcon-cli")} {Sh(command)}", 30000); return r.Code == 0 ? (r.Out.Length > 0 ? r.Out : "Sent.") : ActionLine(r.Out); }
            }
            return "Unknown target.";
        });

        public static async Task<string> LogTailAsync(ServerDef s, int lines = 120) => await Task.Run(() =>
        {
            switch (s.Target)
            {
                case Target.LocalProcess:
                {
                    var f = Path.Combine(s.Folder, "console.log");
                    // An adopted Java server that was started elsewhere writes its log to logs/latest.log.
                    if (s.Adopted && !s.IsBedrock && !Running.ContainsKey(s.Id)) { var latest = Path.Combine(s.Folder, "logs", "latest.log"); if (File.Exists(latest)) f = latest; }
                    if (!File.Exists(f)) return "";
                    using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs);
                    var all = new List<string>(); string l;
                    while ((l = sr.ReadLine()) != null) { all.Add(l); if (all.Count > lines * 4) all.RemoveRange(0, all.Count - lines); }
                    return string.Join("\n", all.TakeLast(lines));
                }
                case Target.LocalDocker: return Run("docker", $"logs --tail {lines} {s.ContainerName}", 20000).Out;
                case Target.SshProcess: return Ssh(s, $"tail -n {lines} {Sh(s.RemoteFolder)}/console.log 2>/dev/null", 30000).Out;
                case Target.SshDocker: return Ssh(s, $"docker logs --tail {lines} {s.ContainerName} 2>&1", 30000).Out;
            }
            return "";
        });

        // -------------------------------------------------------------- configuration and plugins

        /// <summary>The settings with rich controls, their types and (where Mojang documents them) their choices.</summary>
        public static readonly (string Key, string Label, string Kind, string[] Choices, int Min, int Max)[] Properties =
        {
            ("motd", "Message of the day", "text", null, 0, 0),
            ("max-players", "Maximum players", "number", null, 1, 1000),
            ("difficulty", "Difficulty", "choice", new[] { "peaceful", "easy", "normal", "hard" }, 0, 0),
            ("gamemode", "Game mode", "choice", new[] { "survival", "creative", "adventure", "spectator" }, 0, 0),
            ("pvp", "Player versus player", "switch", null, 0, 0),
            ("online-mode", "Check accounts with Mojang", "switch", null, 0, 0),
            ("white-list", "Allow list only", "switch", null, 0, 0),
            ("view-distance", "View distance (chunks)", "number", null, 3, 32),
            ("simulation-distance", "Simulation distance (chunks)", "number", null, 3, 32),
            ("spawn-protection", "Spawn protection (blocks)", "number", null, 0, 64),
            ("allow-flight", "Allow flight", "switch", null, 0, 0),
            ("hardcore", "Hardcore", "switch", null, 0, 0),
        };

        public static Dictionary<string, string> ReadProperties(ServerDef s)
        {
            var f = PropertiesPath(s);
            if (f == null) return new Dictionary<string, string>();
            if (InContainerOnly(s)) { var t = ReadPropertiesText(s); return t == null ? new Dictionary<string, string>() : McProperties.Parse(t).ToDictionary(); }
            return McProperties.Load(f).ToDictionary();
        }

        /// <summary>An adopted container whose /data is a Docker volume rather than a folder here: its files are read and written through docker exec.</summary>
        internal static bool InContainerOnly(ServerDef s) => s.Target == Target.LocalDocker && s.Adopted && string.IsNullOrWhiteSpace(s.FolderPath);

        /// <summary>Updates only the given keys and keeps every other line, comment and unknown setting.</summary>
        public static void WriteProperties(ServerDef s, IDictionary<string, string> changes)
        {
            if (InContainerOnly(s)) { var d0 = McProperties.Parse(ReadPropertiesText(s) ?? ""); foreach (var (k, v) in changes) d0.Set(k, v); WritePropertiesText(s, d0.ToString()); return; }
            var f = PropertiesPath(s) ?? throw new NotSupportedException("Edit remote settings on the host; this target keeps them there.");
            var doc = McProperties.Load(f);
            foreach (var (k, v) in changes) doc.Set(k, v);
            doc.Save(f);
        }

        /// <summary>The whole server.properties file, from this computer or from the SSH host. Null when it cannot be read.</summary>
        public static string ReadPropertiesText(ServerDef s)
        {
            if (InContainerOnly(s))
            {
                var dr = Run("docker", $"exec {s.ContainerName} tail -n +1 /data/server.properties", 20000);
                return dr.Code == 0 ? dr.Out.Replace("\r\n", "\n") + (dr.Out.Length > 0 ? "\n" : "") : null;
            }
            var f = PropertiesPath(s);
            if (f != null) return File.Exists(f) ? File.ReadAllText(f) : "";
            var r = Ssh(s, $"tail -n +1 {Sh(s.RemoteFolder)}/server.properties 2>/dev/null || true", 30000);
            return r.Code == 0 ? r.Out.Replace("\r\n", "\n") + (r.Out.Length > 0 ? "\n" : "") : null;
        }

        /// <summary>Writes the whole file back. On an SSH host it travels as base64, so no character of it is interpreted by a shell.</summary>
        public static void WritePropertiesText(ServerDef s, string text)
        {
            if (InContainerOnly(s))
            {
                var b = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                var dr = Run("docker", $"exec {s.ContainerName} sh -c \"echo {b} | base64 -d > /data/server.properties.tmp && mv /data/server.properties.tmp /data/server.properties\"", 20000);
                if (dr.Code != 0) throw new InvalidOperationException("Could not write server.properties in the container: " + ActionLine(dr.Out));
                return;
            }
            var f = PropertiesPath(s);
            if (f != null) { McProperties.Parse(text).Save(f); return; }
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            var r = Ssh(s, $"cd {Sh(s.RemoteFolder)} && echo {b64} | base64 -d > server.properties.tmp && mv server.properties.tmp server.properties", 30000);
            if (r.Code != 0) throw new InvalidOperationException("Could not write server.properties on the host: " + ActionLine(r.Out));
        }

        private static string PropertiesPath(ServerDef s) =>
            s.Target == Target.LocalProcess || s.Target == Target.LocalDocker ? Path.Combine(s.Folder, "server.properties") : null;

        public static string InstallPlugin(ServerDef s, string jarPath)
        {
            if (!jarPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) return "Choose a .jar plugin or mod.";
            var sub = s.Flavour == Flavour.Fabric ? "mods" : "plugins";
            switch (s.Target)
            {
                case Target.LocalProcess:
                case Target.LocalDocker:
                    var dir = Path.Combine(s.Folder, sub);
                    Directory.CreateDirectory(dir);
                    File.Copy(jarPath, Path.Combine(dir, Path.GetFileName(jarPath)), true);
                    return $"Installed into {sub}. Restart the server to load it.";
                default:
                    var (host, port) = SplitHost(s.SshHost);
                    var scp = Path.Combine(Path.GetDirectoryName(SshExe()), "scp.exe");
                    var mk = Ssh(s, $"mkdir -p {Sh(s.RemoteFolder)}/{sub}", 30000);
                    if (mk.Code != 0) return "Could not reach the host: " + ActionLine(mk.Out);
                    var r = Run(scp, $"-P {port} {KeyArgs(s)}-o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=\"{KnownHosts}\" \"{jarPath}\" {host}:{s.RemoteFolder}/{sub}/", 300000);
                    return r.Code == 0 ? $"Copied into {sub} on the host. Restart the server to load it." : "Copy failed: " + ActionLine(r.Out);
            }
        }

        /// <summary>
        /// Removes the container of a local Docker server, only when Docker reports this app's own label on it for this
        /// server. An adopted container, or any container without that label, is never removed from here.
        /// </summary>
        public static void DeleteContainer(ServerDef s)
        {
            if (s.Target != Target.LocalDocker) throw new NotSupportedException("Only a local Docker server has a container to remove.");
            var label = Run("docker", "inspect -f \"{{ index .Config.Labels \\\"monitoring-tools.server\\\" }}\" " + s.ContainerName, 15000);
            if (label.Code != 0) throw new InvalidOperationException("The container is not on this computer: " + ActionLine(label.Out));
            if (label.Out.Trim() != s.Id) throw new InvalidOperationException("That container does not carry this app's label for this server, so it was not removed.");
            Check(Run("docker", "rm -f " + s.ContainerName, 60000), "remove");
            Changed?.Invoke();
        }

        /// <summary>Stops every local process server gracefully when the service exits.</summary>
        public static void StopAllLocal()
        {
            foreach (var (id, p) in Running.ToList())
            {
                try { if (!p.HasExited) { p.StandardInput.WriteLine("stop"); p.WaitForExit(30000); } } catch { }
            }
        }
    }
}
