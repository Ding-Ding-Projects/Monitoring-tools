using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Minecraft
{
    public sealed class McFileEntry
    {
        public string Name = "";
        public bool IsDir;
        public long Size;
    }

    /// <summary>
    /// The files in a server's folder, wherever that folder is. Paths are relative to the server folder and use "/"; a path
    /// that climbs out of the folder is refused. <see cref="McLocalFiles"/> reads the folder on this computer (which is
    /// also where a local Docker server keeps its bind-mounted data); <see cref="McSshFiles"/> reads the folder on an SSH
    /// host over the same ssh and scp routes the rest of the manager uses.
    /// </summary>
    public interface IMcFiles
    {
        /// <summary>"this computer" or the SSH host, for messages.</summary>
        string Where { get; }
        bool Exists(string rel);
        /// <summary>The file's text, or null when the file is not there.</summary>
        string ReadText(string rel);
        /// <summary>Several text files in one round trip; a missing file is absent from the result.</summary>
        Dictionary<string, string> ReadMany(IEnumerable<string> rels);
        void WriteText(string rel, string text);
        /// <summary>The files and folders directly inside <paramref name="rel"/> ("" is the server folder); empty when it is not there.</summary>
        List<McFileEntry> List(string rel);
        /// <summary>Removes a file or a folder and everything in it.</summary>
        void Delete(string rel);
        void Move(string from, string to);
        /// <summary>Copies a file or folder to a path that does not exist yet.</summary>
        void Copy(string from, string to);
        void MakeDir(string rel);
        void Upload(string localFile, string rel);
        /// <summary>Copies a folder on this computer to <paramref name="rel"/>, which must not exist yet.</summary>
        void UploadDir(string localDir, string rel);
        void DownloadFile(string rel, string localFile);
        /// <summary>Copies the folder to <paramref name="localParent"/>/&lt;its name&gt;.</summary>
        void DownloadDir(string rel, string localParent);
        /// <summary>Bytes in a file or folder (on a host, from du, so rounded to kilobytes).</summary>
        long Size(string rel);
        /// <summary>Writes the listed files and folders into one .tar.gz inside the server folder (a file that changes while it is read is tolerated, as a running world does).</summary>
        void Archive(IEnumerable<string> rels, string archiveRel);
        /// <summary>Unpacks a .tar.gz made by <see cref="Archive"/> into the server folder, replacing what is there.</summary>
        void Extract(string archiveRel);
        /// <summary>Lowercase SHA-512 of each file that exists (a missing file is absent from the result), for matching jars against a catalogue.</summary>
        Dictionary<string, string> Sha512(IEnumerable<string> rels);
        /// <summary>Copies a folder next to itself as "&lt;rel&gt;-backups/&lt;stamp&gt;" and returns that relative path, or null when there is nothing to copy.</summary>
        string BackUpFolder(string rel);
    }

    public static class McFiles
    {
        /// <summary>
        /// Lets the self-test and the capture run point an SSH sample server at a folder on this computer, since their sample
        /// hosts do not exist. Returns null for a server it does not stand in for. Never set outside those runs.
        /// </summary>
        internal static Func<ServerDef, IMcFiles> Override;

        public static IMcFiles For(ServerDef s)
        {
            var o = Override?.Invoke(s);
            if (o != null) return o;
            return s.IsSsh ? new McSshFiles(s) : new McLocalFiles(s.Folder);
        }

        private static readonly Regex Segment = new Regex(@"^[^\x00-\x1f\\:*?""<>|]+$", RegexOptions.Compiled);

        /// <summary>Returns the path with "/" separators, or throws when it is empty, absolute, or climbs out of the folder.</summary>
        public static string Clean(string rel)
        {
            rel = (rel ?? "").Replace('\\', '/');
            if (rel.StartsWith("/", StringComparison.Ordinal)) throw new ArgumentException($"\"{rel}\" is not a path inside the server's folder.");
            rel = rel.TrimEnd('/');
            if (rel.Length == 0) return "";
            foreach (var seg in rel.Split('/'))
            {
                if (seg.Length == 0 || seg == "." || seg == "..") throw new ArgumentException($"\"{rel}\" is not a path inside the server's folder.");
                if (!Segment.IsMatch(seg)) throw new ArgumentException($"\"{seg}\" has a character a server folder name cannot have.");
            }
            return rel;
        }

        private static int _stampCounter;
        /// <summary>A sortable timestamp that is never repeated within one run, even for two changes in the same millisecond.</summary>
        public static string Stamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + (System.Threading.Interlocked.Increment(ref _stampCounter) % 1000).ToString("000");
    }

    /// <summary>The server folder on this computer.</summary>
    public sealed class McLocalFiles : IMcFiles
    {
        private readonly string _root;
        public McLocalFiles(string root) { _root = root; }
        public string Where => "this computer";
        private string P(string rel) { rel = McFiles.Clean(rel); return rel.Length == 0 ? _root : Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar)); }

        public bool Exists(string rel) { var p = P(rel); return File.Exists(p) || Directory.Exists(p); }
        public string ReadText(string rel) { var p = P(rel); return File.Exists(p) ? File.ReadAllText(p) : null; }
        public Dictionary<string, string> ReadMany(IEnumerable<string> rels)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var r in rels) { var t = ReadText(r); if (t != null) d[r] = t; }
            return d;
        }
        public void WriteText(string rel, string text)
        {
            var p = P(rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            var tmp = p + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            File.Move(tmp, p, true);
        }
        public List<McFileEntry> List(string rel)
        {
            var p = P(rel);
            if (!Directory.Exists(p)) return new List<McFileEntry>();
            var l = new List<McFileEntry>();
            foreach (var d in Directory.EnumerateDirectories(p)) l.Add(new McFileEntry { Name = Path.GetFileName(d), IsDir = true });
            foreach (var f in Directory.EnumerateFiles(p)) l.Add(new McFileEntry { Name = Path.GetFileName(f), Size = new FileInfo(f).Length });
            return l.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        public void Delete(string rel)
        {
            if (McFiles.Clean(rel).Length == 0) throw new ArgumentException("The server's own folder is never removed from here.");
            var p = P(rel);
            if (Directory.Exists(p)) Directory.Delete(p, true); else if (File.Exists(p)) File.Delete(p);
        }
        public void Move(string from, string to)
        {
            var a = P(from); var b = P(to);
            Directory.CreateDirectory(Path.GetDirectoryName(b));
            if (Directory.Exists(a)) Directory.Move(a, b); else File.Move(a, b);
        }
        public void Copy(string from, string to)
        {
            var a = P(from); var b = P(to);
            if (File.Exists(b) || Directory.Exists(b)) throw new IOException($"{to} is already there.");
            if (Directory.Exists(a)) CopyDir(a, b); else { Directory.CreateDirectory(Path.GetDirectoryName(b)); File.Copy(a, b); }
        }
        public void MakeDir(string rel) => Directory.CreateDirectory(P(rel));
        public void Upload(string localFile, string rel) { var p = P(rel); Directory.CreateDirectory(Path.GetDirectoryName(p)); File.Copy(localFile, p, true); }
        public void UploadDir(string localDir, string rel)
        {
            var p = P(rel);
            if (Directory.Exists(p) || File.Exists(p)) throw new IOException($"{rel} is already there.");
            CopyDir(localDir, p);
        }
        public void DownloadFile(string rel, string localFile) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(localFile))); File.Copy(P(rel), localFile, true); }
        public void DownloadDir(string rel, string localParent)
        {
            var p = P(rel);
            if (!Directory.Exists(p)) throw new DirectoryNotFoundException($"{rel} is not there.");
            CopyDir(p, Path.Combine(localParent, Path.GetFileName(p.TrimEnd('\\', '/'))));
        }
        public long Size(string rel)
        {
            var p = P(rel);
            if (File.Exists(p)) return new FileInfo(p).Length;
            long n = 0;
            if (Directory.Exists(p)) foreach (var f in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)) { try { n += new FileInfo(f).Length; } catch { } }
            return n;
        }
        public void Archive(IEnumerable<string> rels, string archiveRel)
        {
            var target = P(archiveRel);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            var tmp = target + ".part";
            using (var fs = File.Create(tmp))
            using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Fastest))
            using (var tar = new System.Formats.Tar.TarWriter(gz, System.Formats.Tar.TarEntryFormat.Pax))
                foreach (var rel in rels)
                {
                    var root = P(rel);
                    if (File.Exists(root)) { tar.WriteEntry(root, McFiles.Clean(rel)); continue; }
                    foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        using var src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        var entry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, McFiles.Clean(rel) + "/" + Path.GetRelativePath(root, f).Replace('\\', '/')) { DataStream = src };
                        tar.WriteEntry(entry);
                    }
                }
            File.Move(tmp, target, true);
        }
        public void Extract(string archiveRel)
        {
            using var fs = File.OpenRead(P(archiveRel));
            using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
            System.Formats.Tar.TarFile.ExtractToDirectory(gz, _root, true);
        }
        public Dictionary<string, string> Sha512(IEnumerable<string> rels)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var r in rels) { var p = P(r); if (File.Exists(p)) d[r] = Convert.ToHexString(System.Security.Cryptography.SHA512.HashData(File.ReadAllBytes(p))).ToLowerInvariant(); }
            return d;
        }
        public string BackUpFolder(string rel)
        {
            var p = P(rel);
            if (!Directory.Exists(p) || !Directory.EnumerateFileSystemEntries(p).Any()) return null;
            var to = McFiles.Clean(rel) + "-backups/" + McFiles.Stamp();
            CopyDir(p, P(to));
            return to;
        }

        internal static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                var t = Path.Combine(to, Path.GetRelativePath(from, f));
                Directory.CreateDirectory(Path.GetDirectoryName(t));
                using var src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var dst = File.Create(t);
                src.CopyTo(dst);
            }
            foreach (var d in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, d)));
        }
    }

    /// <summary>
    /// The server folder on an SSH host. Every command is one ssh call with the app's own known_hosts; paths are quoted for
    /// the shell, and text travels as base64 on standard input so no character of it is interpreted. Folders and binary
    /// files travel with scp. A host that cannot be reached (ssh exit 255) is an error with that reason, never an empty
    /// answer, so an unreachable host is not mistaken for an empty folder.
    /// </summary>
    public sealed class McSshFiles : IMcFiles
    {
        private readonly ServerDef _s;
        public McSshFiles(ServerDef s) { _s = s; }
        public string Where => _s.SshHost;
        private string Root => _s.RemoteFolder;
        private string Q(string rel) { rel = McFiles.Clean(rel); return ServerManager.Sh(rel.Length == 0 ? Root : Root.TrimEnd('/') + "/" + rel); }
        private string Raw(string rel) { rel = McFiles.Clean(rel); return rel.Length == 0 ? Root : Root.TrimEnd('/') + "/" + rel; }

        /// <summary>One ssh call, with optional standard input. Code 255 means the host was not reached.</summary>
        internal (int Code, string Out) Run(string remote, int timeoutMs = 60000, string stdin = null)
        {
            var (host, port) = ServerManager.SplitHost(_s.SshHost);
            var args = $"-p {port} {ServerManager.KeyArgs(_s)}-o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=\"{ServerManager.KnownHosts}\" -o ConnectTimeout=15 {host} \"{remote.Replace("\"", "\\\"")}\"";
            return ServerManager.Run(ServerManager.SshExe(), args, timeoutMs, stdin);
        }

        private string Ok(string remote, string what, int timeoutMs = 60000, string stdin = null)
        {
            var r = Run(remote, timeoutMs, stdin);
            // Every command here can be run twice with the same result, so one call that hangs is tried once more before it is reported.
            if (r.Code == -1 && r.Out == "timed out") r = Run(remote, timeoutMs, stdin);
            if (r.Code == 255 || r.Code == -1) throw new InvalidOperationException("The SSH host could not be reached: " + ServerManager.ActionLine(r.Out));
            if (r.Code != 0) throw new InvalidOperationException($"Could not {what} on the host: {ServerManager.ActionLine(r.Out)}");
            return r.Out;
        }

        public bool Exists(string rel) => Ok($"test -e {Q(rel)} && echo yes || echo no", "check a path").Trim().EndsWith("yes");

        public string ReadText(string rel) => ReadMany(new[] { rel }).TryGetValue(rel, out var t) ? t : null;

        public Dictionary<string, string> ReadMany(IEnumerable<string> rels)
        {
            var list = rels.ToList();
            if (list.Count == 0) return new Dictionary<string, string>(StringComparer.Ordinal);
            var script = new StringBuilder();
            for (int i = 0; i < list.Count; i++) script.Append($"if [ -f {Q(list[i])} ]; then echo '@@' {i}; base64 < {Q(list[i])}; echo; fi; ");
            var o = Ok(script.ToString(), "read files");
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            string cur = null; var b64 = new StringBuilder();
            void Flush() { if (cur != null) d[cur] = Encoding.UTF8.GetString(Convert.FromBase64String(b64.ToString())); cur = null; b64.Clear(); }
            foreach (var raw in o.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("@@ ", StringComparison.Ordinal)) { Flush(); cur = list[int.Parse(line[3..].Trim())]; }
                else if (cur != null) b64.Append(line);
            }
            Flush();
            return d;
        }

        public void WriteText(string rel, string text)
        {
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            Ok($"mkdir -p \"$(dirname {Q(rel)})\" && base64 -d > {Q(rel)}.tmp && mv {Q(rel)}.tmp {Q(rel)}", "write a file", 60000, b64);
        }

        public List<McFileEntry> List(string rel)
        {
            var o = Ok($"cd {Q(rel)} 2>/dev/null || exit 0; for f in ./* ./.[!.]*; do [ -e \"$f\" ] || continue; if [ -d \"$f\" ]; then echo \"d|0|${{f#./}}\"; else echo \"f|$(stat -c %s \"$f\")|${{f#./}}\"; fi; done", "list a folder");
            var l = new List<McFileEntry>();
            foreach (var line in o.Replace("\r", "").Split('\n'))
            {
                var p = line.Split('|', 3);
                if (p.Length == 3 && (p[0] == "d" || p[0] == "f") && p[2].Length > 0) l.Add(new McFileEntry { IsDir = p[0] == "d", Size = long.TryParse(p[1], out var n) ? n : 0, Name = p[2] });
            }
            return l.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void Delete(string rel)
        {
            if (McFiles.Clean(rel).Length == 0) throw new ArgumentException("The server's own folder is never removed from here.");
            Ok($"rm -rf -- {Q(rel)}", "remove a path", 180000);
        }

        public void Move(string from, string to) => Ok($"mkdir -p \"$(dirname {Q(to)})\" && mv -- {Q(from)} {Q(to)}", "move a path", 180000);
        public void Copy(string from, string to) => Ok($"test ! -e {Q(to)} && mkdir -p \"$(dirname {Q(to)})\" && cp -a {Q(from)} {Q(to)}", "copy a path", 300000);
        public void MakeDir(string rel) => Ok($"mkdir -p {Q(rel)}", "make a folder");

        // ---- scp

        private static readonly Regex ScpSafe = new Regex(@"^[A-Za-z0-9._/~@+=, -]+$", RegexOptions.Compiled);

        private (int Code, string Out) Scp(string args, string workingDir, int timeoutMs)
        {
            var (_, port) = ServerManager.SplitHost(_s.SshHost);
            var scp = Path.Combine(Path.GetDirectoryName(ServerManager.SshExe()), "scp.exe");
            return ServerManager.Run(scp, $"-P {port} {ServerManager.KeyArgs(_s)}-o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=\"{ServerManager.KnownHosts}\" {args}", timeoutMs, null, workingDir);
        }

        private string Remote(string rel)
        {
            var path = Raw(rel);
            if (!ScpSafe.IsMatch(path)) throw new InvalidOperationException($"scp cannot copy to \"{path}\": a folder name for files that travel by scp may use letters, digits, spaces and . _ - / only.");
            return ServerManager.SplitHost(_s.SshHost).Host + ":" + path;
        }

        private static void CheckScp((int Code, string Out) r, string what)
        {
            if (r.Code == 255) throw new InvalidOperationException("The SSH host could not be reached: " + ServerManager.ActionLine(r.Out));
            if (r.Code != 0) throw new InvalidOperationException($"Could not {what}: {ServerManager.ActionLine(r.Out)}");
        }

        public void Upload(string localFile, string rel)
        {
            MakeDir(string.Join("/", McFiles.Clean(rel).Split('/').SkipLast(1)));
            var dir = Path.GetDirectoryName(Path.GetFullPath(localFile));
            CheckScp(Scp($"\"{Path.GetFileName(localFile)}\" \"{Remote(rel)}\"", dir, 900000), "copy the file to the host");
        }

        public void UploadDir(string localDir, string rel)
        {
            if (Exists(rel)) throw new IOException($"{rel} is already there on the host.");
            var parent = string.Join("/", McFiles.Clean(rel).Split('/').SkipLast(1));
            MakeDir(parent);
            var full = Path.GetFullPath(localDir.TrimEnd('\\', '/'));
            var name = McFiles.Clean(rel).Split('/').Last();
            // scp -r puts the folder into the destination under its own name, so a folder with another name is staged under the right one.
            string stage = null;
            try
            {
                if (!string.Equals(Path.GetFileName(full), name, StringComparison.Ordinal))
                {
                    stage = Path.Combine(Path.GetTempPath(), "mt-up-" + Guid.NewGuid().ToString("N")[..8]);
                    McLocalFiles.CopyDir(full, Path.Combine(stage, name));
                    full = Path.Combine(stage, name);
                }
                CheckScp(Scp($"-r \"{name}\" \"{Remote(parent)}/\"", Path.GetDirectoryName(full), 3600000), "copy the folder to the host");
            }
            finally { if (stage != null) try { Directory.Delete(stage, true); } catch { } }
        }

        public void DownloadFile(string rel, string localFile)
        {
            var full = Path.GetFullPath(localFile);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            CheckScp(Scp($"\"{Remote(rel)}\" \"{Path.GetFileName(full)}\"", Path.GetDirectoryName(full), 900000), "copy the file from the host");
        }

        public void DownloadDir(string rel, string localParent)
        {
            Directory.CreateDirectory(localParent);
            CheckScp(Scp($"-r \"{Remote(rel)}\" .", Path.GetFullPath(localParent), 3600000), "copy the folder from the host");
        }

        public long Size(string rel)
        {
            var o = Ok($"du -sk {Q(rel)} 2>/dev/null | cut -f1", "measure a path").Trim();
            return long.TryParse(o.Split('\n').Last().Trim(), out var kb) ? kb * 1024 : 0;
        }

        public void Archive(IEnumerable<string> rels, string archiveRel)
        {
            var parts = string.Join(" ", rels.Select(r => ServerManager.Sh(McFiles.Clean(r))));
            // tar exits 1 when a file changed while it was read, which a running world does; that is a warning, not a failure.
            Ok($"mkdir -p \"$(dirname {Q(archiveRel)})\" && tar czf {Q(archiveRel)}.part -C {Q("")} {parts}; rc=$?; if [ $rc -le 1 ]; then mv {Q(archiveRel)}.part {Q(archiveRel)}; else rm -f {Q(archiveRel)}.part; exit $rc; fi", "archive the folders", 3600000);
        }

        public void Extract(string archiveRel) => Ok($"tar xzf {Q(archiveRel)} -C {Q("")}", "unpack an archive", 3600000);

        public Dictionary<string, string> Sha512(IEnumerable<string> rels)
        {
            var list = rels.ToList();
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (list.Count == 0) return d;
            var script = new StringBuilder();
            for (int i = 0; i < list.Count; i++) script.Append($"if [ -f {Q(list[i])} ]; then echo \"@@ {i} $(sha512sum < {Q(list[i])} | cut -d' ' -f1)\"; fi; ");
            foreach (var raw in Ok(script.ToString(), "hash files", 300000).Replace("\r", "").Split('\n'))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length == 3 && p[0] == "@@" && int.TryParse(p[1], out var i) && p[2].Length == 128) d[list[i]] = p[2].ToLowerInvariant();
            }
            return d;
        }

        public string BackUpFolder(string rel)
        {
            var to = McFiles.Clean(rel) + "-backups/" + McFiles.Stamp();
            var o = Ok($"if [ -d {Q(rel)} ] && [ \"$(ls -A {Q(rel)} | head -n1)\" ]; then mkdir -p \"$(dirname {Q(to)})\" && cp -a {Q(rel)} {Q(to)} && echo copied; fi", "back up a folder", 300000);
            return o.Contains("copied") ? to : null;
        }
    }
}
