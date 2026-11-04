using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public enum ForgeKind { GitHub, GitLab, Gitea }

    /// <summary>A remote address split into the parts the publish flow needs. Kind is null when the host is not recognised.</summary>
    public sealed record ForgeRemote(ForgeKind? Kind, string Host, string BaseUrl, string Owner, string Name);

    public sealed class ForgeCreated
    {
        public bool Ok;
        public string Error;
        public string CloneUrl;
        public string WebUrl;
        public string Login;
    }

    /// <summary>
    /// Everything specific to GitLab and Gitea/Forgejo: telling the hosts apart, parsing remote addresses, finding their
    /// command-line tools, and creating an empty repository through the REST API with a personal access token that the person
    /// pasted for this one action. The token is only held in memory by the caller, sent in one request header, and never logged.
    /// </summary>
    public static class ForgeHosts
    {
        public static string Display(ForgeKind k) => k switch { ForgeKind.GitLab => "GitLab", ForgeKind.Gitea => "Gitea or Forgejo", _ => "GitHub" };

        public static string CliName(ForgeKind k) => k == ForgeKind.GitLab ? "glab" : k == ForgeKind.Gitea ? "tea" : "gh";

        /// <summary>Guesses the forge from a host name alone. Returns null for a host that could be anything (a self-hosted address).</summary>
        public static ForgeKind? GuessKind(string host)
        {
            host = (host ?? "").ToLowerInvariant();
            if (host == "github.com" || host.EndsWith(".github.com")) return ForgeKind.GitHub;
            if (host == "gitlab.com" || host.StartsWith("gitlab.")) return ForgeKind.GitLab;
            if (host.Contains("gitea") || host.Contains("forgejo") || host == "codeberg.org") return ForgeKind.Gitea;
            return null;
        }

        /// <summary>Normalises a typed server address to scheme://host[:port][/path] with no trailing slash, or null if it is not a plain web address.</summary>
        public static string NormaliseBase(string input)
        {
            var s = (input ?? "").Trim().Trim('"');
            if (s.Length == 0) return null;
            if (!s.Contains("://")) s = "https://" + s;
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return null;
            if (u.Scheme != "https" && u.Scheme != "http") return null;
            if (!string.IsNullOrEmpty(u.UserInfo) || !string.IsNullOrEmpty(u.Query) || !string.IsNullOrEmpty(u.Fragment)) return null;
            if (string.IsNullOrEmpty(u.Host)) return null;
            return u.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        /// <summary>Parses https://host/owner/name(.git), git@host:owner/name.git and ssh://git@host/owner/name.git. Returns null when it is none of those.</summary>
        public static ForgeRemote ParseRemote(string url)
        {
            var s = (url ?? "").Trim();
            if (s.Length == 0) return null;
            string host, path, scheme = "https";
            var scp = Regex.Match(s, @"^(?:[\w.-]+@)?(?<h>[A-Za-z0-9.-]+):(?!//)(?<p>[^\s]+)$");
            if (!s.Contains("://") && scp.Success) { host = scp.Groups["h"].Value; path = scp.Groups["p"].Value; }
            else
            {
                if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || (u.Scheme != "https" && u.Scheme != "http" && u.Scheme != "ssh" && u.Scheme != "git")) return null;
                bool web = u.Scheme == "https" || u.Scheme == "http";
                host = web && !u.IsDefaultPort ? u.Host + ":" + u.Port : u.Host;
                path = u.AbsolutePath.TrimStart('/'); scheme = u.Scheme == "http" ? "http" : "https";
            }
            path = Regex.Replace(path.Trim('/'), @"\.git$", "");
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || host.Length == 0) return null;
            var name = parts[^1];
            var owner = string.Join("/", parts.Take(parts.Length - 1));
            return new ForgeRemote(GuessKind(host.Split(':')[0]), host, scheme + "://" + host, owner, name);
        }

        /// <summary>Looks for an installed command-line tool (glab or tea) on PATH and in its usual install folder.</summary>
        public static string CliPath(ForgeKind k)
        {
            var exe = CliName(k) + ".exe";
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Select(d => d.Trim().Trim('"')).Where(d => d.Length > 0))
            {
                try { var p = Path.Combine(dir, exe); if (File.Exists(p)) return p; } catch { }
            }
            return null;
        }

        /// <summary>The Basic authorization header value for pushing with a token. Used only in a per-process environment variable.</summary>
        public static string BasicHeader(string user, string token) =>
            "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes((string.IsNullOrEmpty(user) ? "oauth2" : user) + ":" + token));

        private static HttpClient Client(string token, ForgeKind kind)
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools");
            if (kind == ForgeKind.GitLab) c.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
            else c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
            return c;
        }

        private static string Scrub(string text, string token) =>
            string.IsNullOrEmpty(token) || string.IsNullOrEmpty(text) ? text : text.Replace(token, "***");

        private static string Message(string body)
        {
            try
            {
                using var d = JsonDocument.Parse(body);
                foreach (var key in new[] { "message", "error", "error_description" })
                    if (d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty(key, out var m)) return m.ToString();
            }
            catch { }
            return body.Length > 300 ? body.Substring(0, 300) : body;
        }

        /// <summary>Creates an empty repository on GitLab through its REST API. The owner is a group or user path, or empty for the token's own account.</summary>
        public static async Task<ForgeCreated> CreateGitLabAsync(string baseUrl, string token, string owner, string name, bool isPrivate, CancellationToken ct)
        {
            try
            {
                using var c = Client(token, ForgeKind.GitLab);
                var body = new Dictionary<string, object> { ["name"] = name, ["path"] = name, ["visibility"] = isPrivate ? "private" : "public", ["initialize_with_readme"] = false };
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    var ns = await c.GetAsync(baseUrl + "/api/v4/namespaces/" + Uri.EscapeDataString(owner.Trim()), ct);
                    var nsText = await ns.Content.ReadAsStringAsync(ct);
                    if (!ns.IsSuccessStatusCode) return new ForgeCreated { Error = Describe(ns, nsText, token, "Could not find the group or user \"" + owner + "\"") };
                    using var nd = JsonDocument.Parse(nsText);
                    body["namespace_id"] = nd.RootElement.GetProperty("id").GetInt64();
                }
                var resp = await c.PostAsync(baseUrl + "/api/v4/projects", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
                var text = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode) return new ForgeCreated { Error = Describe(resp, text, token, "GitLab did not create the repository") };
                using var d = JsonDocument.Parse(text);
                return new ForgeCreated { Ok = true, CloneUrl = d.RootElement.GetProperty("http_url_to_repo").GetString(), WebUrl = d.RootElement.GetProperty("web_url").GetString(), Login = "oauth2" };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return new ForgeCreated { Error = Scrub("Could not reach " + baseUrl + ": " + ex.Message, token) }; }
        }

        /// <summary>Creates an empty repository on a Gitea or Forgejo server through its REST API.</summary>
        public static async Task<ForgeCreated> CreateGiteaAsync(string baseUrl, string token, string owner, string name, bool isPrivate, CancellationToken ct)
        {
            try
            {
                using var c = Client(token, ForgeKind.Gitea);
                var me = await c.GetAsync(baseUrl + "/api/v1/user", ct);
                var meText = await me.Content.ReadAsStringAsync(ct);
                if (!me.IsSuccessStatusCode) return new ForgeCreated { Error = Describe(me, meText, token, "The server did not accept the access token") };
                string login;
                using (var md = JsonDocument.Parse(meText)) login = md.RootElement.GetProperty("login").GetString();
                var body = JsonSerializer.Serialize(new Dictionary<string, object> { ["name"] = name, ["private"] = isPrivate, ["auto_init"] = false });
                bool org = !string.IsNullOrWhiteSpace(owner) && !owner.Trim().Equals(login, StringComparison.OrdinalIgnoreCase);
                var url = org ? baseUrl + "/api/v1/orgs/" + Uri.EscapeDataString(owner.Trim()) + "/repos" : baseUrl + "/api/v1/user/repos";
                var resp = await c.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);
                var text = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode) return new ForgeCreated { Error = Describe(resp, text, token, "The server did not create the repository") };
                using var d = JsonDocument.Parse(text);
                return new ForgeCreated { Ok = true, CloneUrl = d.RootElement.GetProperty("clone_url").GetString(), WebUrl = d.RootElement.GetProperty("html_url").GetString(), Login = login };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { return new ForgeCreated { Error = Scrub("Could not reach " + baseUrl + ": " + ex.Message, token) }; }
        }

        private static string Describe(HttpResponseMessage r, string body, string token, string lead)
        {
            var code = (int)r.StatusCode;
            var hint = code is 401 or 403 ? " Check that the token is valid, has permission to create repositories (api or write:repository scope) and has not expired." : "";
            return Scrub($"{lead} (HTTP {code}): {Message(body)}.{hint}", token);
        }
    }
}
