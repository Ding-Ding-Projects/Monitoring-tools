using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Dependencies embedded in the exe by the build (FFmpeg with FFprobe, and MinGit).
    /// They are extracted once per pinned version into the user's data folder, verified
    /// against the embedded bytes, and always preferred over anything on the machine.
    /// </summary>
    public static class BundledTools
    {
        private static readonly object Gate = new object();
        private static readonly Assembly Asm = typeof(BundledTools).Assembly;

        public sealed class Resolution
        {
            public string Path;
            public string Origin;                       // "bundled", "settings", "machine", or null when missing
            public List<string> Searched = new List<string>();
        }

        private static string _pinsKey;

        /// <summary>A short key per pinned set, so a new release extracts fresh copies beside the old.</summary>
        private static string PinsKey
        {
            get
            {
                if (_pinsKey != null) return _pinsKey;
                try
                {
                    using var s = Asm.GetManifestResourceStream("bundled/manifest.json");
                    if (s == null) return _pinsKey = "unbundled";
                    using var doc = JsonDocument.Parse(s);
                    var tags = doc.RootElement.GetProperty("dependencies").EnumerateArray().Select(d => d.GetProperty("tag").GetString());
                    var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("|", tags)));
                    _pinsKey = Convert.ToHexString(hash)[..12].ToLowerInvariant();
                }
                catch { _pinsKey = "unbundled"; }
                return _pinsKey;
            }
        }

        public static string ToolsDir => System.IO.Path.Combine(Settings.DataDir, "tools", PinsKey);

        public static bool HasFfmpegBundle => Asm.GetManifestResourceNames().Any(n => n == "bundled/ffmpeg/ffmpeg.exe");
        public static bool HasGitBundle => Asm.GetManifestResourceNames().Contains("bundled/mingit.zip");

        /// <summary>Pinned versions as recorded at build time, for the About page.</summary>
        public static List<(string Name, string Tag, string License)> Pins()
        {
            var list = new List<(string, string, string)>();
            try
            {
                using var s = Asm.GetManifestResourceStream("bundled/manifest.json");
                if (s == null) return list;
                using var doc = JsonDocument.Parse(s);
                foreach (var d in doc.RootElement.GetProperty("dependencies").EnumerateArray())
                    list.Add((d.GetProperty("name").GetString(), d.GetProperty("tag").GetString(), d.GetProperty("license").GetString()));
            }
            catch { }
            return list;
        }

        // -------------------------------------------------------------- FFmpeg

        public static string FfmpegFolder()
        {
            if (!HasFfmpegBundle) return null;
            var dir = System.IO.Path.Combine(ToolsDir, "ffmpeg");
            lock (Gate)
            {
                foreach (var name in Asm.GetManifestResourceNames().Where(n => n.StartsWith("bundled/ffmpeg/")))
                    ExtractVerified(name, System.IO.Path.Combine(dir, name.Substring("bundled/ffmpeg/".Length)));
            }
            return dir;
        }

        // -------------------------------------------------------------- MinGit

        public static string GitExe()
        {
            if (!HasGitBundle) return null;
            var dir = System.IO.Path.Combine(ToolsDir, "mingit");
            var exe = System.IO.Path.Combine(dir, "cmd", "git.exe");
            var stamp = System.IO.Path.Combine(dir, ".extracted");
            lock (Gate)
            {
                if (File.Exists(exe) && File.Exists(stamp)) return exe;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                using var s = Asm.GetManifestResourceStream("bundled/mingit.zip");
                using var z = new ZipArchive(s, ZipArchiveMode.Read);
                z.ExtractToDirectory(dir, true);
                File.WriteAllText(stamp, DateTime.UtcNow.ToString("O"));
                Log.Info("Extracted bundled MinGit to " + dir);
            }
            return exe;
        }

        /// <summary>Bundled first, then the machine's git. Origin says which one is in use.</summary>
        public static Resolution ResolveGit()
        {
            var r = new Resolution();
            try
            {
                r.Searched.Add("bundled MinGit inside the app");
                var b = GitExe();
                if (b != null && File.Exists(b)) { r.Path = b; r.Origin = "bundled"; return r; }
            }
            catch (Exception ex) { r.Searched[^1] += $" ({ex.Message})"; }
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Where(d => d.Length > 0))
            {
                var p = System.IO.Path.Combine(dir.Trim().Trim('"'), "git.exe");
                r.Searched.Add(p);
                if (File.Exists(p)) { r.Path = p; r.Origin = "machine"; return r; }
            }
            return r;
        }

        // -------------------------------------------------------------- extraction

        /// <summary>Writes an embedded file once and verifies its SHA-256 against the embedded bytes.</summary>
        private static void ExtractVerified(string resource, string target)
        {
            using var s = Asm.GetManifestResourceStream(resource);
            if (s == null) return;
            var stamp = target + ".sha256";
            if (File.Exists(target) && File.Exists(stamp) && new FileInfo(target).Length == s.Length) return;

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
            string expected;
            using (var sha = SHA256.Create())
            using (var cs = new CryptoStream(s, sha, CryptoStreamMode.Read))
            using (var f = File.Create(target + ".part"))
            {
                cs.CopyTo(f);
                cs.FlushFinalBlock();
                expected = Convert.ToHexString(sha.Hash);
            }
            string written;
            using (var f = File.OpenRead(target + ".part")) written = Convert.ToHexString(SHA256.HashData(f));
            if (written != expected)
            {
                File.Delete(target + ".part");
                throw new InvalidDataException($"Bundled {System.IO.Path.GetFileName(target)} failed verification after extraction.");
            }
            File.Move(target + ".part", target, true);
            File.WriteAllText(stamp, expected);
        }
    }
}
