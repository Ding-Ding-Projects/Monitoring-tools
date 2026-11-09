using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>One remote of a Git repository that has a web page.</summary>
    public sealed class RemotePage
    {
        /// <summary>The remote's name in the repository's configuration, for example origin.</summary>
        public string Remote;
        /// <summary>The page to open: always http or https, never with a user name, password, token, query or fragment.</summary>
        public string Url;
        /// <summary>The service the page belongs to (GitHub, GitLab, Bitbucket…) or, for anything else, its host name.</summary>
        public string Host;
        /// <summary>The page without its scheme, for a list: github.com/owner/repo.</summary>
        public string Shown;
    }

    /// <summary>
    /// The web pages of a repository's remotes, read from its own Git configuration without starting Git, so a copied
    /// folder's toast can offer them at once. Remote URLs are turned into pages without ever keeping a credential.
    /// </summary>
    public static class RemotePages
    {
        /// <summary>Every remote of the repository at <paramref name="repoRoot"/> that has a web page: origin first, then the
        /// others in the order they are configured. Empty when there are none or the configuration cannot be read.</summary>
        public static IReadOnlyList<RemotePage> For(string repoRoot)
        {
            try
            {
                var config = ConfigPath(repoRoot);
                if (config == null || !File.Exists(config)) return Array.Empty<RemotePage>();
                var local = Parse(File.ReadAllText(config));
                var rewrites = local.InsteadOf.Concat(GlobalConfigs().SelectMany(p => ReadOrEmpty(p).InsteadOf)).ToList();
                return Pages(local.Remotes.Select(r => (r.Name, Rewrite(r.Url, rewrites))));
            }
            catch { return Array.Empty<RemotePage>(); }
        }

        /// <summary>The pages of the given remotes, origin first. Remotes without a page are left out.</summary>
        public static IReadOnlyList<RemotePage> Pages(IEnumerable<(string Name, string Url)> remotes)
        {
            var pages = new List<RemotePage>();
            foreach (var (name, url) in remotes)
            {
                var page = WebPage(url);
                if (page == null || string.IsNullOrEmpty(name)) continue;
                pages.Add(new RemotePage { Remote = name, Url = page, Host = HostName(page), Shown = Regex.Replace(Regex.Replace(page, @"^https?://", ""), @"\?.*$", "") });
            }
            var origin = pages.FindIndex(p => p.Remote == "origin");
            if (origin > 0) { var o = pages[origin]; pages.RemoveAt(origin); pages.Insert(0, o); }
            return pages;
        }

        private static readonly Regex SchemeRx = new Regex(@"^([a-z][a-z0-9+.-]*)://", RegexOptions.IgnoreCase);
        // Git's scp-like form, [user@]host:path. A one-letter host is a Windows drive (C:\repo), so it is a local path.
        private static readonly Regex ScpRx = new Regex(@"^(?:[^@/\\\s]+@)?([^@:/\\\s]{2,}):(?!//)(.+)$");
        private static readonly HashSet<string> SshSchemes = new HashSet<string> { "ssh", "git+ssh", "ssh+git", "git" };
        private static readonly HashSet<string> WellKnown = new HashSet<string> { "github.com", "gitlab.com", "bitbucket.org", "codeberg.org", "gitea.com", "dev.azure.com", "huggingface.co" };

        /// <summary>
        /// The web page of the repository a remote URL points at, or null for a local path, a file: URL, a remote helper
        /// (codecommit::…) or anything that is not a URL. An SSH remote opens the HTTPS page of the same host; an HTTP(S)
        /// remote keeps its scheme and port. User names, passwords, tokens, queries and fragments are never kept.
        /// </summary>
        public static string WebPage(string remoteUrl)
        {
            var s = (remoteUrl ?? "").Trim();
            if (s.Length == 0 || s.Any(c => char.IsWhiteSpace(c) || c == '\0') || Regex.IsMatch(s, @"^[a-z][a-z0-9+.-]*::", RegexOptions.IgnoreCase)) return null;

            string scheme = null, hostPort, path, sshPort = "";
            bool http = false;
            var sm = SchemeRx.Match(s);
            if (sm.Success)
            {
                scheme = sm.Groups[1].Value.ToLowerInvariant();
                if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || string.IsNullOrEmpty(u.Host)) return null;
                if (scheme == "http" || scheme == "https")
                {
                    http = true;
                    hostPort = u.IsDefaultPort ? u.Host : u.Host + ":" + u.Port;
                }
                else if (SshSchemes.Contains(scheme))
                {
                    hostPort = u.Host;
                    if (!u.IsDefaultPort && u.Port > 0) sshPort = u.Port.ToString();
                }
                else return null;
                path = u.AbsolutePath;
            }
            else
            {
                var m = ScpRx.Match(s);
                if (!m.Success) return null;
                hostPort = m.Groups[1].Value;
                path = m.Groups[2].Value.Split('?', '#')[0];
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Unescape).ToList();
            if (segments.Count > 0)
            {
                var last = Regex.Replace(segments[^1], @"\.git$", "", RegexOptions.IgnoreCase);
                if (last.Length == 0) segments.RemoveAt(segments.Count - 1); else segments[^1] = last;
            }
            if (segments.Count == 0) return null;

            var host = hostPort.ToLowerInvariant();
            string query = "";
            Known(ref host, ref segments, ref query, http, sshPort);
            var built = (scheme == "http" ? "http" : "https") + "://" + host + "/" + string.Join("/", segments.Select(Uri.EscapeDataString)) + query;
            return Uri.TryCreate(built, UriKind.Absolute, out var page) && (page.Scheme == "http" || page.Scheme == "https") && string.IsNullOrEmpty(page.UserInfo) ? built : null;
        }

        private static string Unescape(string segment)
        {
            try { return Uri.UnescapeDataString(segment); } catch { return segment; }
        }

        /// <summary>Services whose clone addresses and web pages are shaped differently.</summary>
        private static void Known(ref string host, ref List<string> seg, ref string query, bool http, string sshPort)
        {
            // Azure DevOps over SSH: ssh.dev.azure.com:v3/org/project/repo, and the older vs-ssh.visualstudio.com form.
            if (host == "ssh.dev.azure.com" && seg.Count >= 4 && seg[0] == "v3") { seg = new List<string> { seg[1], seg[2], "_git", seg[3] }; host = "dev.azure.com"; return; }
            if (host == "vs-ssh.visualstudio.com" && seg.Count >= 4 && seg[0] == "v3") { host = seg[1].ToLowerInvariant() + ".visualstudio.com"; seg = new List<string> { seg[2], "_git", seg[3] }; return; }
            // AWS CodeCommit: git-codecommit.<region>.amazonaws.com/v1/repos/<name> opens in the console for that region.
            var cc = Regex.Match(host, @"^git-codecommit\.([a-z0-9-]+)\.amazonaws\.com$");
            if (cc.Success && seg.Count >= 3 && seg[0] == "v1" && seg[1] == "repos")
            {
                var region = cc.Groups[1].Value;
                host = region + ".console.aws.amazon.com";
                seg = new List<string> { "codesuite", "codecommit", "repositories", seg[2], "browse" };
                query = "?region=" + Uri.EscapeDataString(region);
                return;
            }
            // Bitbucket Data Center clones over HTTP(S) from /scm/<PROJECT>/<repo> and over SSH from port 7999; its pages
            // live under /projects/<PROJECT>/repos/<repo>, or /users/<user>/repos/<repo> for a personal one (~user).
            if (WellKnown.Contains(host)) return;
            List<string> dc = http && seg.Count == 3 && seg[0] == "scm" ? seg.Skip(1).ToList() : sshPort == "7999" && seg.Count == 2 ? seg : null;
            if (dc == null) return;
            seg = dc[0].StartsWith("~") ? new List<string> { "users", dc[0].Substring(1), "repos", dc[1] } : new List<string> { "projects", dc[0].ToUpperInvariant(), "repos", dc[1] };
        }

        /// <summary>The service a page belongs to, for "Open on …". A host this does not know is named by itself.</summary>
        public static string HostName(string webUrl)
        {
            if (!Uri.TryCreate(webUrl, UriKind.Absolute, out var u)) return webUrl;
            var h = u.Host.ToLowerInvariant();
            if (h == "github.com" || h.EndsWith(".ghe.com") || h.StartsWith("github.")) return "GitHub";
            if (h == "gitlab.com" || h.StartsWith("gitlab.")) return "GitLab";
            if (h == "bitbucket.org" || h.StartsWith("bitbucket.")) return "Bitbucket";
            if (h == "dev.azure.com" || h.EndsWith(".visualstudio.com")) return "Azure DevOps";
            if (h == "codeberg.org") return "Codeberg";
            if (h == "gitea.com" || h.StartsWith("gitea.")) return "Gitea";
            if (h == "sr.ht" || h.EndsWith(".sr.ht")) return "SourceHut";
            if (h.EndsWith(".console.aws.amazon.com")) return "AWS CodeCommit";
            if (h == "huggingface.co") return "Hugging Face";
            return u.IsDefaultPort ? h : h + ":" + u.Port;
        }

        // ------------------------------------------------------------------ reading the configuration

        /// <summary>The configuration file of the repository at repoRoot. A linked worktree's .git is a file naming its git
        /// directory, whose commondir names the main repository's, where the remotes are kept.</summary>
        internal static string ConfigPath(string repoRoot)
        {
            var dotGit = Path.Combine(repoRoot, ".git");
            if (Directory.Exists(dotGit)) return Path.Combine(dotGit, "config");
            if (!File.Exists(dotGit)) return null;
            var line = File.ReadLines(dotGit).FirstOrDefault(l => l.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase));
            if (line == null) return null;
            var gitDir = Path.GetFullPath(Path.Combine(repoRoot, line.Substring(7).Trim()));
            var common = Path.Combine(gitDir, "commondir");
            if (File.Exists(common)) gitDir = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(common).Trim()));
            return Path.Combine(gitDir, "config");
        }

        private static IEnumerable<string> GlobalConfigs()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(home)) yield return Path.Combine(home, ".gitconfig");
            yield return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home ?? "", ".config") : xdg, "git", "config");
        }

        private static ParsedConfig ReadOrEmpty(string path)
        {
            try { return File.Exists(path) ? Parse(File.ReadAllText(path)) : new ParsedConfig(); } catch { return new ParsedConfig(); }
        }

        internal sealed class ParsedConfig
        {
            public readonly List<(string Name, string Url)> Remotes = new List<(string, string)>();
            public readonly List<(string Prefix, string Base)> InsteadOf = new List<(string, string)>();
        }

        private static readonly Regex SectionRx = new Regex(@"^\s*\[\s*([A-Za-z0-9.-]+)(?:\s+""((?:[^""\\]|\\.)*)"")?\s*\]\s*(.*)$");
        private static readonly Regex KeyRx = new Regex(@"^\s*([A-Za-z][A-Za-z0-9-]*)\s*(?:=\s*(.*))?$");

        /// <summary>The remotes (name and first url, in order) and url.*.insteadOf rewrites of one Git configuration file.</summary>
        internal static ParsedConfig Parse(string text)
        {
            var result = new ParsedConfig();
            string section = null, sub = null;
            var seen = new HashSet<string>();
            var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // A value may continue on the next line after a trailing backslash.
                while (line.EndsWith("\\") && !line.EndsWith("\\\\") && i + 1 < lines.Length) line = line.Substring(0, line.Length - 1) + lines[++i];
                var sm = SectionRx.Match(line);
                if (sm.Success)
                {
                    var name = sm.Groups[1].Value;
                    if (sm.Groups[2].Success) { section = name.ToLowerInvariant(); sub = Regex.Replace(sm.Groups[2].Value, @"\\(.)", "$1"); }
                    else
                    {
                        // The old [remote.origin] form: the part after the first dot is the subsection.
                        int dot = name.IndexOf('.');
                        section = (dot < 0 ? name : name.Substring(0, dot)).ToLowerInvariant();
                        sub = dot < 0 ? null : name.Substring(dot + 1).ToLowerInvariant();
                    }
                    line = sm.Groups[3].Value;   // a key may follow the header on the same line
                    if (string.IsNullOrWhiteSpace(line)) continue;
                }
                var km = KeyRx.Match(line);
                if (!km.Success || section == null || sub == null) continue;
                var key = km.Groups[1].Value.ToLowerInvariant();
                var value = km.Groups[2].Success ? Value(km.Groups[2].Value) : "";
                if (section == "remote" && key == "url" && seen.Add(sub)) result.Remotes.Add((sub, value));
                else if (section == "url" && key == "insteadof" && value.Length > 0) result.InsteadOf.Add((value, sub));
            }
            return result;
        }

        /// <summary>A configuration value: quotes removed, escapes resolved, a # or ; comment outside quotes dropped.</summary>
        private static string Value(string raw)
        {
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '"') { quoted = !quoted; continue; }
                if (!quoted && (c == '#' || c == ';')) break;
                if (c == '\\' && i + 1 < raw.Length)
                {
                    char n = raw[++i];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'b' ? '\b' : n);
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>Applies the longest matching url.&lt;base&gt;.insteadOf prefix, as Git does.</summary>
        private static string Rewrite(string url, List<(string Prefix, string Base)> rules)
        {
            var best = rules.Where(r => url.StartsWith(r.Prefix, StringComparison.Ordinal)).OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
            return best.Prefix == null ? url : best.Base + url.Substring(best.Prefix.Length);
        }
    }
}
