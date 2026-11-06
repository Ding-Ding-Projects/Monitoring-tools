using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>Things developers copy: stack traces, tokens, hashes, addresses, clone links, error codes, GUIDs.</summary>
    public static class Developer
    {
        // ------------------------------------------------------------------ stack traces

        private static readonly Regex DotNetFrame = new Regex(@"^\s*at (?<fn>.+?) in (?<file>.+?):line (?<line>\d+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex PyFrame = new Regex(@"^\s*File ""(?<file>[^""]+)"", line (?<line>\d+)(?:, in (?<fn>.+))?$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex JsFrame = new Regex(@"^\s*at (?:(?<fn>[^(\n]+?) \()?(?<file>(?:[A-Za-z]:)?[^():\n]+\.[A-Za-z]+):(?<line>\d+):\d+\)?\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        public static SmartFact StackTrace(string t)
        {
            var frames = new List<(string Fn, string File, string Line)>();
            foreach (var rx in new[] { DotNetFrame, PyFrame, JsFrame })
                foreach (Match m in rx.Matches(t)) frames.Add((m.Groups["fn"].Value.Trim(), m.Groups["file"].Value.Trim(), m.Groups["line"].Value));
            if (frames.Count == 0) return null;
            var first = t.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("at ") && !l.StartsWith("File ") && !l.StartsWith("Traceback")) ?? "Error";
            var error = Regex.Match(first, @"^[\w.]*(Exception|Error)\b").Value;
            var top = frames.FirstOrDefault(f => !f.File.Contains("node_modules") && !f.File.Contains("site-packages"));
            if (top.File == null) top = frames[0];
            var f2 = new SmartFact("trace", "You copied a stack trace", (error.Length > 0 ? error : "Error") + " · " + frames.Count + (frames.Count == 1 ? " frame" : " frames"))
                .Value("Message", first.Length > 200 ? first.Substring(0, 200) : first)
                .Act("Open " + System.IO.Path.GetFileName(top.File) + " line " + top.Line, SmartDo.Code, top.File + ":" + top.Line, primary: true, tip: "Opens the exact line in VS Code");
            foreach (var f in frames.Take(6)) f2.Value(string.IsNullOrEmpty(f.Fn) ? "Frame" : f.Fn, f.File + ":" + f.Line);
            if (error.Length > 0) f2.Act("Search the error", SmartDo.Open, "https://www.bing.com/search?q=" + Uri.EscapeDataString(first.Length > 200 ? first.Substring(0, 200) : first));
            return f2;
        }

        // ------------------------------------------------------------------ JSON web tokens

        public static SmartFact Jwt(string t, DateTimeOffset? nowForTests = null)
        {
            var s = t.Trim();
            if (s.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7).Trim();
            var parts = s.Split('.');
            if (parts.Length != 3 || !parts.All(p => Regex.IsMatch(p, @"^[A-Za-z0-9_-]*$")) || parts[0].Length < 10) return null;
            string header = B64Url(parts[0]), payload = B64Url(parts[1]);
            if (header == null || payload == null || !header.Contains("\"alg\"")) return null;
            var f = new SmartFact("jwt", "You copied a sign-in token (JWT)", "kept out of history and cleared in 30 seconds") { Sensitive = true };
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.TryGetProperty("sub", out var sub)) f.Value("Subject", sub.ToString());
                if (root.TryGetProperty("iss", out var iss)) f.Value("Issuer", iss.ToString());
                if (root.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var e))
                {
                    var at = DateTimeOffset.FromUnixTimeSeconds(e); var now = nowForTests ?? DateTimeOffset.Now;
                    f.Value("Expires", at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + (at < now ? " (expired)" : " (" + Everyday.Ago(now - at) + ")"));
                    f.Summary = (at < now ? "expired " : "expires ") + Everyday.Ago(now - at) + " · kept out of history";
                }
            }
            catch { }
            f.Value("Header", header).Value("Payload", Pretty(payload));
            return f;
        }

        private static string B64Url(string p)
        {
            try
            {
                var s = p.Replace('-', '+').Replace('_', '/'); s += new string('=', (4 - s.Length % 4) % 4);
                var text = Encoding.UTF8.GetString(Convert.FromBase64String(s));
                return text.TrimStart().StartsWith("{") ? text : null;
            }
            catch { return null; }
        }

        public static string Pretty(string json)
        {
            try { using var d = JsonDocument.Parse(json); return JsonSerializer.Serialize(d.RootElement, new JsonSerializerOptions { WriteIndented = true }); }
            catch { return json; }
        }

        // ------------------------------------------------------------------ hashes

        public static SmartFact Hash(string t)
        {
            var s = t.Trim();
            if (!Regex.IsMatch(s, @"^[0-9a-fA-F]+$") || !Regex.IsMatch(s, "[a-fA-F]") || !Regex.IsMatch(s, "[0-9]")) return null;
            string kind = s.Length switch { 32 => "MD5", 40 => "SHA-1", 64 => "SHA-256", 96 => "SHA-384", 128 => "SHA-512", _ => null };
            if (kind == null) return null;
            return new SmartFact("hash", "You copied a " + kind + " hash", s.Substring(0, 12) + "…").Set("algorithm", kind)
                .Value("Lower case", s.ToLowerInvariant()).Value("Upper case", s.ToUpperInvariant())
                .Act("Check a file against it", SmartDo.Run, "hash-check", primary: true, tip: "Pick a file, or it checks your newest download");
        }

        // ------------------------------------------------------------------ IP addresses

        public static SmartFact Ip(string t)
        {
            var s = t.Trim();
            if (s.Length > 45 || !Regex.IsMatch(s, @"^[0-9a-fA-F:.]+$") || !IPAddress.TryParse(s, out var ip)) return null;
            if (ip.AddressFamily == AddressFamily.InterNetwork && s.Count(c => c == '.') != 3) return null;
            if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !s.Contains(':')) return null;
            string where = IPAddress.IsLoopback(ip) ? "this PC" : IsPrivate(ip) ? "private · on a home or office network" : "public";
            var f = new SmartFact("ip", "You copied an IP address", s + " · " + where)
                .Value("Address", s).Act("Ping", SmartDo.Run, "ping:" + s, primary: true).Act("Trace route", SmartDo.Run, "tracert:" + s);
            if (where == "public") f.Set("lookup", s).Act("Who owns it", SmartDo.Open, "https://rdap.org/ip/" + s);
            else f.Act("Open in browser", SmartDo.Open, "http://" + (ip.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + s + "]" : s) + "/");
            return f;
        }

        public static bool IsPrivate(IPAddress ip)
        {
            if (ip.AddressFamily == AddressFamily.InterNetworkV6) return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
            var b = ip.GetAddressBytes();
            return b[0] == 10 || b[0] == 172 && b[1] >= 16 && b[1] <= 31 || b[0] == 192 && b[1] == 168 || b[0] == 169 && b[1] == 254 || b[0] == 100 && b[1] >= 64 && b[1] <= 127;
        }

        // ------------------------------------------------------------------ git clone links over SSH

        private static readonly Regex GitSsh = new Regex(@"^(?:ssh://)?git@(?<host>[\w.\-]+)[:/](?<owner>[\w.\-]+)/(?<repo>[\w.\-]+?)(?:\.git)?/?$", RegexOptions.Compiled);

        public static SmartFact GitSshUrl(string t)
        {
            var m = GitSsh.Match(t.Trim());
            if (!m.Success) return null;
            string host = m.Groups["host"].Value, owner = m.Groups["owner"].Value, repo = m.Groups["repo"].Value;
            return new SmartFact("git", "You copied a Git clone link", owner + "/" + repo + " on " + host)
                .Set("host", host).Set("owner", owner).Set("repo", repo)
                .Value("SSH", $"git@{host}:{owner}/{repo}.git").Value("HTTPS", $"https://{host}/{owner}/{repo}.git").Value("Clone command", $"git clone git@{host}:{owner}/{repo}.git")
                .Act("Clone", SmartDo.Run, $"clone:{host}/{owner}/{repo}", primary: true)
                .Act("Open in browser", SmartDo.Open, $"https://{host}/{owner}/{repo}");
        }

        // ------------------------------------------------------------------ Windows error codes

        public static SmartFact ErrorCode(string t)
        {
            var s = t.Trim(); uint code;
            if (Regex.IsMatch(s, @"^0x[0-9a-fA-F]{8}$")) code = uint.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            else if (Regex.IsMatch(s, @"^-2\d{9}$") && int.TryParse(s, out var neg)) code = unchecked((uint)neg);
            else return null;
            if ((code & 0x80000000) == 0) return null;
            uint facility = (code >> 16) & 0x1FFF, number = code & 0xFFFF;
            var hex = "0x" + code.ToString("X8", CultureInfo.InvariantCulture);
            var f = new SmartFact("hresult", "You copied a Windows error code", hex + " · " + FacilityName(facility))
                .Set("hresult", hex).Value("Hex", hex).Value("Decimal", unchecked((int)code).ToString(CultureInfo.InvariantCulture))
                .Value("Facility", facility + " · " + FacilityName(facility)).Value("Code", number.ToString(CultureInfo.InvariantCulture))
                .Act("Search Microsoft Learn", SmartDo.Open, "https://learn.microsoft.com/search/?terms=" + hex, primary: true);
            return f;
        }

        private static string FacilityName(uint f) => f switch
        {
            7 => "Win32", 4 => "COM interface", 10 => "Control", 11 => "Cert", 23 => "Windows Update", 36 => "Windows Update", 8 => "Windows", 1 => "RPC", 3 => "Storage", 9 => "Security", _ => "facility " + f,
        };

        // ------------------------------------------------------------------ GUIDs

        public static SmartFact GuidText(string t)
        {
            var s = t.Trim();
            if (s.Length < 32 || s.Length > 38 || !Guid.TryParse(s, out var g)) return null;
            return new SmartFact("guid", "You copied a GUID", g.ToString("D"))
                .Value("Standard", g.ToString("D")).Value("Upper case", g.ToString("D").ToUpperInvariant()).Value("Braces", g.ToString("B"))
                .Value("No dashes", g.ToString("N")).Value("C#", "new Guid(\"" + g.ToString("D") + "\")")
                .Act("Copy a new GUID", SmartDo.Run, "new-guid", primary: true);
        }

        // ------------------------------------------------------------------ secrets

        private static readonly (string Name, Regex Rx)[] SecretPatterns =
        {
            ("GitHub token", new Regex(@"^(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{36,}$|^github_pat_[A-Za-z0-9_]{60,}$", RegexOptions.Compiled)),
            ("API key", new Regex(@"^sk-(?:proj-|ant-)?[A-Za-z0-9_\-]{20,}$", RegexOptions.Compiled)),
            ("AWS access key", new Regex(@"^(AKIA|ASIA)[A-Z0-9]{16}$", RegexOptions.Compiled)),
            ("Slack token", new Regex(@"^xox[abposr]-[A-Za-z0-9-]{10,}$", RegexOptions.Compiled)),
            ("Google API key", new Regex(@"^AIza[0-9A-Za-z_\-]{35}$", RegexOptions.Compiled)),
            ("Private key", new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.Compiled)),
            ("Connection string with a password", new Regex(@"(?i)\b(password|pwd)\s*=\s*[^;\s]{4,}", RegexOptions.Compiled)),
        };

        /// <summary>Keys, tokens and passwords. The secret guard keeps them out of history and clears them.</summary>
        public static SmartFact Secret(string t)
        {
            var s = t.Trim();
            foreach (var (name, rx) in SecretPatterns)
                if (rx.IsMatch(s))
                    return new SmartFact("secret", "You copied a secret", name + " · kept out of history, cleared in 30 seconds") { Sensitive = true }
                        .Set("secret", name).Act("Clear it now", SmartDo.Run, "clear-clipboard", primary: true);
            return null;
        }
    }
}
