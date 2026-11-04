using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// The official Bedrock Dedicated Server for Windows. The download link comes from the same links service the
    /// minecraft.net download page reads; only a link on minecraft.net over HTTPS is accepted. Mojang publishes no checksum
    /// for the archive, so instead the server program inside it must carry a valid Authenticode signature from Microsoft
    /// Corporation before anything is copied into the server's folder.
    /// </summary>
    public static class McBedrock
    {
        public const string LinksApi = "https://net-secondary.web.minecraft-services.net/api/v1.0/download/links";
        public const string Exe = "bedrock_server.exe";
        /// <summary>A small file the app writes beside the server, naming the version it installed.</summary>
        public const string VersionFile = "monitoring-tools-bedrock-version.txt";
        public const int DefaultPort = 19132;

        public sealed class Release { public string Version = "", Url = ""; public bool Preview; }

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp() { var h = new HttpClient { Timeout = TimeSpan.FromMinutes(15) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0 (server manager)"); return h; }
        private static Release _latest;
        private static readonly Regex FileVersion = new Regex(@"bedrock-server-(?<v>\d+(\.\d+){2,3})\.zip$", RegexOptions.Compiled);

        /// <summary>Used by the self-test and the capture run so the create flow can show a version without the network.</summary>
        internal static void SetLatestForTest(Release r) => _latest = r;

        /// <summary>The current Windows release Mojang links to. Mojang offers only the newest one, so that is the one installed.</summary>
        public static async Task<Release> LatestAsync()
        {
            if (_latest != null) return _latest;
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(LinksApi));
            var link = doc.RootElement.GetProperty("result").GetProperty("links").EnumerateArray()
                .FirstOrDefault(l => l.GetProperty("downloadType").GetString() == "serverBedrockWindows");
            if (link.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Mojang's download links list no Bedrock server for Windows right now.");
            var url = link.GetProperty("downloadUrl").GetString();
            var problem = CheckUrl(url);
            if (problem != null) throw new InvalidDataException(problem);
            var m = FileVersion.Match(url);
            _latest = new Release { Url = url, Version = m.Success ? m.Groups["v"].Value : "unknown" };
            return _latest;
        }

        /// <summary>Null when the link is an HTTPS address on minecraft.net, otherwise why it is refused.</summary>
        internal static string CheckUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return "The Bedrock download link is not an address.";
            if (u.Scheme != Uri.UriSchemeHttps) return "The Bedrock download link is not HTTPS, so it was not used.";
            if (!(u.Host == "minecraft.net" || u.Host.EndsWith(".minecraft.net", StringComparison.OrdinalIgnoreCase))) return "The Bedrock download link is not on minecraft.net, so it was not used.";
            return null;
        }

        /// <summary>Downloads, checks and unpacks the server into its folder, then writes the first settings.</summary>
        public static async Task InstallAsync(ServerDef s, Action<string> report, IDictionary<string, string> initial)
        {
            var rel = await LatestAsync();
            Directory.CreateDirectory(s.Folder);
            var zip = Path.Combine(Path.GetTempPath(), "mt-bedrock-" + Guid.NewGuid().ToString("N") + ".zip");
            var stage = Path.Combine(Path.GetTempPath(), "mt-bedrock-" + Guid.NewGuid().ToString("N"));
            try
            {
                report?.Invoke($"Downloading the Bedrock server {rel.Version} from minecraft.net…");
                await ServerManager.Download(rel.Url, zip, report);
                report?.Invoke("Checking the archive…");
                using (var z = ZipFile.OpenRead(zip))
                {
                    if (z.GetEntry(Exe) == null) throw new InvalidDataException("The download has no bedrock_server.exe, so it was not installed.");
                    z.ExtractToDirectory(stage, true);
                }
                var sig = SignatureProblem(Path.Combine(stage, Exe));
                if (sig != null) throw new InvalidDataException(sig);
                report?.Invoke("Copying the server into its folder…");
                // A server's own files (settings, allow list, permissions, worlds) are never replaced by the archive's defaults.
                var keep = new[] { "server.properties", "allowlist.json", "permissions.json" };
                foreach (var f in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
                {
                    var rel2 = Path.GetRelativePath(stage, f);
                    var to = Path.Combine(s.Folder, rel2);
                    if (keep.Contains(rel2, StringComparer.OrdinalIgnoreCase) && File.Exists(to)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(to));
                    File.Copy(f, to, true);
                }
                File.WriteAllText(Path.Combine(s.Folder, VersionFile), rel.Version);
            }
            finally
            {
                try { File.Delete(zip); } catch { }
                try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { }
            }
            s.Version = rel.Version;
            s.Build = "";
            var props = new Dictionary<string, string>
            {
                ["server-name"] = ServerManager.Escape(s.Name),
                ["server-port"] = s.Port.ToString(),
                // The IPv6 port defaults to 19133 for every server, so two servers on one computer would collide; it follows the IPv4 port.
                ["server-portv6"] = (s.Port + 1).ToString(),
            };
            if (initial != null)
                foreach (var (k, v) in initial)
                    if (k == "max-players" || (k == "level-seed" && !string.IsNullOrEmpty(v)) || k == "gamemode" || k == "difficulty") props[k] = ServerManager.Escape(v);
            var path = Path.Combine(s.Folder, "server.properties");
            var doc = File.Exists(path) ? McProperties.Load(path) : McProperties.Parse("");
            foreach (var (k, v) in props) doc.Set(k, v);
            doc.Save(path);
        }

        // ---- the other targets

        public const string DockerImage = "itzg/minecraft-bedrock-server:latest";
        /// <summary>The port the Bedrock server listens on inside the itzg image; the server's own port is published onto it.</summary>
        public const int ContainerPort = 19132;
        private static Release _latestLinux;

        /// <summary>The current Linux release Mojang links to, for a process on an SSH host. The link is held to the same rule as the Windows one.</summary>
        public static async Task<Release> LatestLinuxAsync()
        {
            if (_latestLinux != null) return _latestLinux;
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(LinksApi));
            var link = doc.RootElement.GetProperty("result").GetProperty("links").EnumerateArray()
                .FirstOrDefault(l => l.GetProperty("downloadType").GetString() == "serverBedrockLinux");
            if (link.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Mojang's download links list no Bedrock server for Linux right now.");
            var url = link.GetProperty("downloadUrl").GetString();
            var problem = CheckUrl(url);
            if (problem != null) throw new InvalidDataException(problem);
            var m = FileVersion.Match(url);
            _latestLinux = new Release { Url = url, Version = m.Success ? m.Groups["v"].Value : "unknown" };
            return _latestLinux;
        }

        internal static void SetLatestLinuxForTest(Release r) => _latestLinux = r;

        /// <summary>The shell script that unpacks the Linux server on an SSH host. Existing settings, allow list, permissions and worlds are never overwritten (unzip -n).</summary>
        internal static string LinuxInstallScript(string dir, string url) =>
            $"set -e; mkdir -p {ServerManager.Sh(dir)}; cd {ServerManager.Sh(dir)}; " +
            "command -v unzip >/dev/null || { echo 'unzip is not installed on this host'; exit 3; }; " +
            $"curl -fsSL -o bedrock-server.zip.part {ServerManager.Sh(url)} || wget -q -O bedrock-server.zip.part {ServerManager.Sh(url)}; " +
            "unzip -q -n bedrock-server.zip.part && rm -f bedrock-server.zip.part && chmod +x bedrock_server && test -f bedrock_server";

        /// <summary>
        /// Installs the server on the other targets. A process on an SSH host downloads Mojang's Linux zip (HTTPS on minecraft.net
        /// only; Mojang publishes no checksum or signature for it, so it is unpacked but its contents cannot be verified). A Docker
        /// target runs the community image itzg/minecraft-bedrock-server, which downloads the server itself on its first start.
        /// </summary>
        internal static async Task InstallOtherAsync(ServerDef s, Action<string> report, IDictionary<string, string> initial)
        {
            var props = new Dictionary<string, string> { ["server-name"] = ServerManager.Escape(s.Name) };
            if (s.Target == Target.SshProcess) { props["server-port"] = s.Port.ToString(); props["server-portv6"] = (s.Port + 1).ToString(); }
            if (initial != null)
                foreach (var (k, v) in initial)
                    if (k == "max-players" || (k == "level-seed" && !string.IsNullOrEmpty(v)) || k == "gamemode" || k == "difficulty") props[k] = ServerManager.Escape(v);
            switch (s.Target)
            {
                case Target.SshProcess:
                {
                    var rel = await LatestLinuxAsync();
                    report?.Invoke($"Downloading the Linux Bedrock server {rel.Version} on the SSH host…");
                    var r = ServerManager.Ssh(s, LinuxInstallScript(s.RemoteFolder, rel.Url), 900000);
                    if (r.Code != 0) throw new InvalidOperationException("Installing the Bedrock server on the host failed: " + ServerManager.ActionLine(r.Out));
                    s.Version = rel.Version; s.Build = "";
                    var doc = McProperties.Parse(ServerManager.ReadPropertiesText(s) ?? "");
                    foreach (var (k, v) in props) doc.Set(k, v);
                    ServerManager.WritePropertiesText(s, doc.ToString());
                    ServerManager.Ssh(s, $"cd {ServerManager.Sh(s.RemoteFolder)} && echo {ServerManager.Sh(rel.Version)} > {VersionFile}", 30000);
                    break;
                }
                case Target.LocalDocker:
                {
                    var info = ServerManager.Run("docker", "info --format \"{{.ServerVersion}}\"", 20000);
                    if (info.Code != 0) throw new InvalidOperationException("Docker is not running on this computer: " + ServerManager.ActionLine(info.Out));
                    Directory.CreateDirectory(s.Folder);
                    WriteDockerProperties(Path.Combine(s.Folder, "server.properties"), props);
                    report?.Invoke("Pulling the Bedrock server image…");
                    var pull = ServerManager.Run("docker", "pull " + DockerImage, 900000);
                    if (pull.Code != 0) throw new InvalidOperationException("Pull failed: " + ServerManager.ActionLine(pull.Out));
                    var mk = ServerManager.Run("docker", DockerCreateArgs(s, $"\"{s.Folder}\""), 60000);
                    if (mk.Code != 0) throw new InvalidOperationException("Container create failed: " + ServerManager.ActionLine(mk.Out));
                    break;
                }
                case Target.SshDocker:
                {
                    var info = ServerManager.Ssh(s, "docker info --format '{{.ServerVersion}}'", 30000);
                    if (info.Code != 0) throw new InvalidOperationException("Docker is not available on the SSH host: " + ServerManager.ActionLine(info.Out));
                    report?.Invoke("Pulling the Bedrock server image on the SSH host…");
                    var dir = s.RemoteFolder;
                    var lines = string.Concat(props.Select(kv => $"{kv.Key}={kv.Value}\\n"));
                    var r = ServerManager.Ssh(s, $"mkdir -p {ServerManager.Sh(dir)} && cd {ServerManager.Sh(dir)} && (test -f server.properties || printf {ServerManager.Sh(lines)} > server.properties) && docker pull {DockerImage} >/dev/null && docker {DockerCreateArgs(s, $"\"$(pwd)\"")}", 900000);
                    if (r.Code != 0) throw new InvalidOperationException("Container create failed: " + ServerManager.ActionLine(r.Out));
                    break;
                }
                default: throw new NotSupportedException("The Windows Bedrock server is installed by InstallAsync.");
            }
        }

        private static void WriteDockerProperties(string path, IDictionary<string, string> props)
        {
            var doc = File.Exists(path) ? McProperties.Load(path) : McProperties.Parse("");
            foreach (var (k, v) in props) doc.Set(k, v);
            doc.Save(path);
        }

        /// <summary>
        /// The arguments that create the container. The image keeps the server's port at 19132 inside the container, so the
        /// server's own port is published onto it over UDP; settings come from server.properties in the data folder, which
        /// the image only changes for settings named in its environment. The container keeps standard input open
        /// (-i -t) because the image's send-command helper writes console commands to the server's input.
        /// </summary>
        internal static string DockerCreateArgs(ServerDef s, string volume) =>
            $"create --name {s.ContainerName} --label monitoring-tools.server={s.Id} -p {s.Port}:{ContainerPort}/udp -v {volume}:/data -e EULA=TRUE -e VERSION=LATEST -i -t {DockerImage}";

        // ---- Authenticode

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FileInfoW { public int Size; public string Path; public IntPtr Handle; public IntPtr Subject; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TrustData
        {
            public int Size; public IntPtr Policy, Sip; public int UiChoice, Revocation, UnionChoice; public IntPtr File; public int StateAction; public IntPtr State, Url; public int ProviderFlags, UiContext; public IntPtr Signature;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)] private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);

        /// <summary>Null when the file has a valid Authenticode signature from Microsoft Corporation, otherwise why not.</summary>
        internal static string SignatureProblem(string file)
        {
            var info = new FileInfoW { Size = Marshal.SizeOf<FileInfoW>(), Path = file };
            var pInfo = Marshal.AllocHGlobal(info.Size);
            try
            {
                Marshal.StructureToPtr(info, pInfo, false);
                var data = new TrustData { Size = Marshal.SizeOf<TrustData>(), UiChoice = 2 /* none */, Revocation = 0, UnionChoice = 1 /* file */, File = pInfo, StateAction = 0, ProviderFlags = 0x10 /* no network for revocation */ };
                var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");   // WINTRUST_ACTION_GENERIC_VERIFY_V2
                int r = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                if (r != 0) return $"bedrock_server.exe does not carry a valid signature (code 0x{r:X8}), so it was not installed.";
            }
            finally { Marshal.DestroyStructure<FileInfoW>(pInfo); Marshal.FreeHGlobal(pInfo); }
            try
            {
#pragma warning disable SYSLIB0057
                using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(file));
#pragma warning restore SYSLIB0057
                if (!cert.Subject.Contains("O=Microsoft Corporation", StringComparison.Ordinal)) return "bedrock_server.exe is signed, but not by Microsoft Corporation, so it was not installed.";
            }
            catch (Exception ex) { return "The signature of bedrock_server.exe could not be read: " + ex.Message; }
            return null;
        }
    }
}
