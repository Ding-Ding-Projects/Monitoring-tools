using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>A running process as discovery sees it: its id, image name, command line and working folder.</summary>
    public sealed class McProcInfo
    {
        public int Pid;
        public string Name = "", CommandLine = "", WorkingDir = "";
    }

    /// <summary>A Docker container as discovery sees it.</summary>
    public sealed class McContainerInfo
    {
        public string Id = "", Name = "", Image = "", State = "";
        public Dictionary<string, string> Labels = new Dictionary<string, string>();
        public Dictionary<string, string> Env = new Dictionary<string, string>();
        /// <summary>The host folder mounted at /data, when it is a folder on this computer; empty for a named volume.</summary>
        public string DataFolder = "";
        public int HostPort;
    }

    /// <summary>One server that discovery found and this app does not manage yet.</summary>
    public sealed class McFound
    {
        public string Key = "", Kind = "";   // Kind: "process" or "docker"
        public string Title = "", Folder = "", Container = "", Jar = "", Version = "", Image = "", Build = "";
        public Flavour Flavour = Flavour.Vanilla;
        public int Port, Pid;
        public bool Running;
        public string Detail => Kind == "docker"
            ? $"Docker container {Container} · {Image} · {(Running ? "running" : "not running")}"
            : $"Process {Pid} · {(Jar.Length > 0 ? Jar : "bedrock_server.exe")} · {Folder}";
    }

    /// <summary>
    /// Finds Minecraft servers this app did not create: Java processes running a server jar, Bedrock server processes, and
    /// Docker containers whose image is a Minecraft server. Discovery only reads; nothing is stopped, changed or written
    /// until a server is adopted, and adopting only records an entry that points at the existing folder or container.
    /// </summary>
    public static class McDiscovery
    {
        /// <summary>Replaces what the computer reports, for the self-test and the capture run.</summary>
        internal static Func<List<McProcInfo>> FakeProcesses;
        internal static Func<List<McContainerInfo>> FakeContainers;

        private static readonly Regex JarArg = new Regex(@"-jar\s+(""[^""]+\.jar""|\S+\.jar)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PaperName = new Regex(@"^(?<f>paper|purpur)-(?<v>\d+\.\d+(\.\d+)?)-(?<b>\d+)\.jar$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static async Task<List<McFound>> FindAsync() => await Task.Run(() => Classify(Processes(), Containers(), ServerManager.Servers.ToList()));

        // ------------------------------------------------------------------ classification (pure, self-tested)

        public static List<McFound> Classify(IEnumerable<McProcInfo> procs, IEnumerable<McContainerInfo> containers, IEnumerable<ServerDef> known)
        {
            var knownList = known.ToList();
            var found = new List<McFound>();
            foreach (var p in procs ?? Enumerable.Empty<McProcInfo>())
            {
                var f = FromProcess(p);
                if (f == null) continue;
                if (knownList.Any(s => s.Target == Target.LocalProcess && SameFolder(s.Folder, f.Folder))) continue;   // already managed
                found.Add(f);
            }
            foreach (var c in containers ?? Enumerable.Empty<McContainerInfo>())
            {
                var f = FromContainer(c);
                if (f == null) continue;
                if (knownList.Any(s => s.Target == Target.LocalDocker && string.Equals(s.ContainerName, c.Name, StringComparison.Ordinal))) continue;
                if (c.Labels.TryGetValue("monitoring-tools.server", out var id) && knownList.Any(s => s.Id == id)) continue;
                found.Add(f);
            }
            return found;
        }

        internal static McFound FromProcess(McProcInfo p)
        {
            var name = (p.Name ?? "").ToLowerInvariant().Replace(".exe", "");
            var folder = (p.WorkingDir ?? "").TrimEnd('\\', '/');
            if (name == "bedrock_server")
            {
                if (folder.Length == 0) return null;
                return new McFound
                {
                    Key = "process:" + p.Pid, Kind = "process", Pid = p.Pid, Folder = folder, Flavour = Flavour.Bedrock, Running = true,
                    Title = Path.GetFileName(folder), Port = PortIn(folder, 19132), Version = BedrockVersion(folder),
                };
            }
            if (name != "java" && name != "javaw") return null;
            var m = JarArg.Match(p.CommandLine ?? "");
            bool forgeArgs = (p.CommandLine ?? "").Contains("net/minecraftforge", StringComparison.OrdinalIgnoreCase) || (p.CommandLine ?? "").Contains("neoforged", StringComparison.OrdinalIgnoreCase);
            if (!m.Success && !forgeArgs) return null;
            var jar = m.Success ? m.Groups[1].Value.Trim('"') : "";
            var jarFile = Path.GetFileName(jar);
            bool hasProps = folder.Length > 0 && File.Exists(Path.Combine(folder, "server.properties"));
            bool nogui = Regex.IsMatch(p.CommandLine ?? "", @"(^|\s)-{0,2}nogui(\s|$)", RegexOptions.IgnoreCase);
            // A Java program counts only when its folder holds a server.properties, it was started with nogui, or it is a Forge
            // launch; a jar that is merely named like a server is not enough on its own.
            if (!hasProps && !nogui && !forgeArgs) return null;
            if (folder.Length == 0 && Path.IsPathRooted(jar)) folder = Path.GetDirectoryName(jar);
            if (string.IsNullOrEmpty(folder)) return null;
            var f = new McFound { Key = "process:" + p.Pid, Kind = "process", Pid = p.Pid, Folder = folder, Jar = jarFile, Running = true, Title = Path.GetFileName(folder) };
            var pm = PaperName.Match(jarFile);
            if (pm.Success) { f.Flavour = pm.Groups["f"].Value.ToLowerInvariant() == "purpur" ? Flavour.Purpur : Flavour.Paper; f.Version = pm.Groups["v"].Value; f.Build = pm.Groups["b"].Value; }
            else if ((p.CommandLine ?? "").Contains("neoforged", StringComparison.OrdinalIgnoreCase)) f.Flavour = Flavour.NeoForge;
            else if (forgeArgs) f.Flavour = Flavour.Forge;
            else if (jarFile.Contains("fabric", StringComparison.OrdinalIgnoreCase)) f.Flavour = Flavour.Fabric;
            else if (jarFile.Contains("purpur", StringComparison.OrdinalIgnoreCase)) f.Flavour = Flavour.Purpur;
            else if (jarFile.Contains("paper", StringComparison.OrdinalIgnoreCase)) f.Flavour = Flavour.Paper;
            if (f.Version.Length == 0) f.Version = JarVersion(Path.IsPathRooted(jar) ? jar : Path.Combine(folder, jar));
            f.Port = PortIn(folder, 25565);
            return f;
        }

        internal static McFound FromContainer(McContainerInfo c)
        {
            var image = (c.Image ?? "").ToLowerInvariant();
            bool bedrock = image.Contains("minecraft-bedrock-server");
            if (!bedrock && !image.Contains("minecraft-server") && !image.Contains("minecraft")) return null;
            var f = new McFound
            {
                Key = "docker:" + c.Name, Kind = "docker", Container = c.Name, Image = c.Image, Folder = c.DataFolder ?? "", Running = c.State == "running",
                Title = c.Name, Port = c.HostPort > 0 ? c.HostPort : (bedrock ? 19132 : 25565),
            };
            c.Env.TryGetValue("VERSION", out var v);
            f.Version = string.IsNullOrEmpty(v) || v.Equals("LATEST", StringComparison.OrdinalIgnoreCase) ? "latest" : v;
            c.Env.TryGetValue("TYPE", out var type);
            f.Flavour = bedrock ? Flavour.Bedrock : (type ?? "").ToUpperInvariant() switch
            {
                "PAPER" => Flavour.Paper, "PURPUR" => Flavour.Purpur, "FABRIC" => Flavour.Fabric, "FORGE" => Flavour.Forge, "NEOFORGE" => Flavour.NeoForge, _ => Flavour.Vanilla,
            };
            return f;
        }

        private static bool SameFolder(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private static int PortIn(string folder, int fallback)
        {
            try
            {
                var f = Path.Combine(folder, "server.properties");
                if (!File.Exists(f)) return fallback;
                var v = McProperties.Load(f).Get("server-port");
                return int.TryParse(v, out var n) ? n : fallback;
            }
            catch { return fallback; }
        }

        /// <summary>The version a vanilla server jar declares in its version.json, or "unknown".</summary>
        private static string JarVersion(string jar)
        {
            try
            {
                if (!File.Exists(jar)) return "unknown";
                using var z = ZipFile.OpenRead(jar);
                var e = z.GetEntry("version.json");
                if (e == null) return "unknown";
                using var doc = JsonDocument.Parse(new StreamReader(e.Open()).ReadToEnd());
                return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : "unknown";
            }
            catch { return "unknown"; }
        }

        private static string BedrockVersion(string folder)
        {
            var marker = Path.Combine(folder, McBedrock.VersionFile);
            try { return File.Exists(marker) ? File.ReadAllText(marker).Trim() : "unknown"; } catch { return "unknown"; }
        }

        // ------------------------------------------------------------------ adopting

        /// <summary>Records a found server as one this app manages. Nothing in its folder or container changes.</summary>
        public static ServerDef ToServer(McFound f)
        {
            var s = new ServerDef
            {
                Name = f.Title.Length > 0 ? f.Title : "Adopted server", Version = f.Version, Flavour = f.Flavour, Build = f.Build ?? "",
                Port = f.Port, EulaAccepted = true, Adopted = true, Created = DateTime.Now,
                Target = f.Kind == "docker" ? Target.LocalDocker : Target.LocalProcess,
                FolderPath = f.Folder ?? "", Container = f.Kind == "docker" ? f.Container : "", JarName = string.IsNullOrEmpty(f.Jar) ? "server.jar" : f.Jar,
            };
            return s;
        }

        public static ServerDef Adopt(McFound f)
        {
            var s = ToServer(f);
            ServerManager.Servers.Add(s);
            ServerManager.Save();
            Core.Activity.Add(Core.ActivityKind.Service, $"Minecraft server adopted: {s.Name}", f.Kind == "docker" ? "container " + f.Container : f.Folder);
            return s;
        }

        /// <summary>The live process of an adopted process server that runs outside this app, found by its working folder.</summary>
        public static McProcInfo ExternalProcess(ServerDef s)
        {
            if (!s.Adopted || s.Target != Target.LocalProcess) return null;
            var procs = FakeProcesses != null ? FakeProcesses() : Processes();
            return procs.FirstOrDefault(p => SameFolder(p.WorkingDir, s.Folder) && FromProcess(p) != null);
        }

        // ------------------------------------------------------------------ reading this computer

        public static List<McProcInfo> Processes()
        {
            if (FakeProcesses != null) return FakeProcesses();
            var list = new List<McProcInfo>();
            foreach (var name in new[] { "java", "javaw", "bedrock_server" })
            {
                Process[] ps;
                try { ps = Process.GetProcessesByName(name); } catch { continue; }
                foreach (var p in ps)
                {
                    try
                    {
                        var (cmd, cwd) = ReadPeb(p.Id);
                        if (cmd == null) continue;
                        list.Add(new McProcInfo { Pid = p.Id, Name = name, CommandLine = cmd, WorkingDir = cwd ?? "" });
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            return list;
        }

        public static List<McContainerInfo> Containers()
        {
            if (FakeContainers != null) return FakeContainers();
            var list = new List<McContainerInfo>();
            var ps = ServerManager.Run("docker", "ps -a --no-trunc --format \"{{.Names}}\t{{.Image}}\"", 20000);
            if (ps.Code != 0) return list;
            foreach (var line in ps.Out.Split('\n').Select(l => l.Trim()).Where(l => l.Contains('\t')))
            {
                var parts = line.Split('\t');
                if (!parts[1].Contains("minecraft", StringComparison.OrdinalIgnoreCase)) continue;
                var c = new McContainerInfo { Name = parts[0], Image = parts[1] };
                var ins = ServerManager.Run("docker", $"inspect {c.Name}", 20000);
                if (ins.Code != 0) continue;
                try
                {
                    using var doc = JsonDocument.Parse(ins.Out);
                    var root = doc.RootElement[0];
                    c.Id = root.GetProperty("Id").GetString();
                    c.State = root.GetProperty("State").GetProperty("Status").GetString();
                    var cfg = root.GetProperty("Config");
                    if (cfg.TryGetProperty("Labels", out var labels) && labels.ValueKind == JsonValueKind.Object)
                        foreach (var l in labels.EnumerateObject()) c.Labels[l.Name] = l.Value.GetString();
                    if (cfg.TryGetProperty("Env", out var env) && env.ValueKind == JsonValueKind.Array)
                        foreach (var e in env.EnumerateArray()) { var kv = e.GetString(); var at = kv.IndexOf('='); if (at > 0) c.Env[kv[..at]] = kv[(at + 1)..]; }
                    if (root.TryGetProperty("Mounts", out var mounts))
                        foreach (var mnt in mounts.EnumerateArray())
                            if (mnt.GetProperty("Destination").GetString() == "/data" && mnt.GetProperty("Type").GetString() == "bind") c.DataFolder = HostPath(mnt.GetProperty("Source").GetString());
                    var hc = root.GetProperty("HostConfig");
                    if (hc.TryGetProperty("PortBindings", out var pb) && pb.ValueKind == JsonValueKind.Object)
                        foreach (var b in pb.EnumerateObject())
                            if ((b.Name.StartsWith("25565/") || b.Name.StartsWith("19132/")) && b.Value.ValueKind == JsonValueKind.Array && b.Value.GetArrayLength() > 0 && int.TryParse(b.Value[0].GetProperty("HostPort").GetString(), out var hp)) c.HostPort = hp;
                }
                catch { continue; }
                list.Add(c);
            }
            return list;
        }

        /// <summary>Docker Desktop reports a Windows folder bind as /run/desktop/mnt/host/c/...; this turns it back into C:\....</summary>
        internal static string HostPath(string source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            var m = Regex.Match(source, @"^/run/desktop/mnt/host/([a-zA-Z])/(.*)$");
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant() + ":\\" + m.Groups[2].Value.Replace('/', '\\');
            m = Regex.Match(source, @"^/([a-zA-Z])/(.*)$");
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant() + ":\\" + m.Groups[2].Value.Replace('/', '\\');
            return Regex.IsMatch(source, @"^[a-zA-Z]:\\") ? source : "";
        }

        // ---- the command line and working folder of another process, read from its process environment block (64-bit)

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicInfo { public IntPtr Reserved1, PebBaseAddress, Reserved2a, Reserved2b, UniqueProcessId, Reserved3; }

        [DllImport("ntdll.dll")] private static extern int NtQueryInformationProcess(IntPtr process, int infoClass, ref BasicInfo info, int size, out int returned);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(int access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr read);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] private static extern bool IsWow64Process(IntPtr process, out bool wow64);

        private static (string Cmd, string Cwd) ReadPeb(int pid)
        {
            if (IntPtr.Size != 8) return (null, null);
            var h = OpenProcess(0x0400 | 0x0010, false, pid);   // query information, read memory
            if (h == IntPtr.Zero) return (null, null);
            try
            {
                if (IsWow64Process(h, out var wow) && wow) return (null, null);   // a 32-bit process keeps a different layout
                var bi = new BasicInfo();
                if (NtQueryInformationProcess(h, 0, ref bi, Marshal.SizeOf<BasicInfo>(), out _) != 0) return (null, null);
                var parameters = ReadPtr(h, bi.PebBaseAddress + 0x20);
                if (parameters == IntPtr.Zero) return (null, null);
                return (ReadUnicode(h, parameters + 0x70), ReadUnicode(h, parameters + 0x38));
            }
            finally { CloseHandle(h); }
        }

        private static IntPtr ReadPtr(IntPtr h, IntPtr at)
        {
            var b = new byte[8];
            return ReadProcessMemory(h, at, b, (IntPtr)8, out _) ? (IntPtr)BitConverter.ToInt64(b, 0) : IntPtr.Zero;
        }

        private static string ReadUnicode(IntPtr h, IntPtr at)
        {
            var head = new byte[16];
            if (!ReadProcessMemory(h, at, head, (IntPtr)16, out _)) return null;
            int len = BitConverter.ToUInt16(head, 0);
            var buf = (IntPtr)BitConverter.ToInt64(head, 8);
            if (len == 0 || buf == IntPtr.Zero) return "";
            var text = new byte[len];
            return ReadProcessMemory(h, buf, text, (IntPtr)len, out _) ? Encoding.Unicode.GetString(text) : null;
        }
    }
}
