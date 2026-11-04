using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public sealed class ReleaseNote
    {
        public string Tag { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime? Published { get; set; }
        public string Body { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public sealed class ChangelogResult
    {
        public List<ReleaseNote> Releases = new List<ReleaseNote>();
        /// <summary>True when the list came from the disk cache because the fetch failed.</summary>
        public bool FromCache;
        public DateTime? CachedAt;
        public string Error;
    }

    /// <summary>Fetches the project's GitHub releases, keeps the last good answer on disk and falls back to it offline.</summary>
    public static class Changelog
    {
        private const string Api = "https://api.github.com/repos/Ding-Ding-Projects/Monitoring-tools/releases";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private static string CacheFile => Path.Combine(Settings.DataDir, "changelog-cache.json");

        public static string RunningVersion => typeof(Changelog).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        /// <summary>True when the release tag names the running version, ignoring a leading v.</summary>
        public static bool IsRunning(ReleaseNote r)
        {
            var tag = (r.Tag ?? "").TrimStart('v', 'V');
            return tag.Length > 0 && tag == RunningVersion;
        }

        public static async Task<ChangelogResult> LoadAsync()
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, Api + "?per_page=30");
                req.Headers.UserAgent.ParseAdd("MonitoringTools-Changelog/" + RunningVersion);
                req.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var resp = await Http.SendAsync(req).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var list = Parse(json);
                try { File.WriteAllText(CacheFile, json); } catch (Exception ex) { Log.Warn("Changelog cache not written: " + ex.Message); }
                return new ChangelogResult { Releases = list };
            }
            catch (Exception ex)
            {
                var res = new ChangelogResult { Error = ex is TaskCanceledException ? "the request timed out after 10 seconds" : ex.Message };
                try
                {
                    if (File.Exists(CacheFile))
                    {
                        res.Releases = Parse(File.ReadAllText(CacheFile));
                        res.CachedAt = File.GetLastWriteTime(CacheFile);
                        res.FromCache = true;
                    }
                }
                catch (Exception ex2) { Log.Warn("Changelog cache unreadable: " + ex2.Message); }
                return res;
            }
        }

        public static List<ReleaseNote> Parse(string json)
        {
            var list = new List<ReleaseNote>();
            using var d = JsonDocument.Parse(json);
            if (d.RootElement.ValueKind != JsonValueKind.Array) return list;
            foreach (var e in d.RootElement.EnumerateArray())
            {
                string S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
                DateTime? when = DateTime.TryParse(S("published_at"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t.ToLocalTime() : null;
                var name = S("name");
                list.Add(new ReleaseNote { Tag = S("tag_name"), Name = string.IsNullOrWhiteSpace(name) ? S("tag_name") : name, Published = when, Body = S("body"), Url = S("html_url") });
            }
            return list.OrderByDescending(r => r.Published ?? DateTime.MinValue).ToList();
        }
    }
}
