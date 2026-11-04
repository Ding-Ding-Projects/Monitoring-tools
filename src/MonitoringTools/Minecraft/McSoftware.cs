using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MonitoringTools.Minecraft
{
    /// <summary>One build of a server software for one game version, as its own public API lists it.</summary>
    public sealed class McBuild
    {
        public string Id;            // "121", "0.16.9", "54.0.16"
        public string Channel;       // "default", "stable", "experimental", "beta"
        public string DownloadUrl;   // a direct jar, when the API gives one
        public string HashKind;      // "sha256", "md5" or null when the API publishes none
        public string Hash;
    }

    /// <summary>The builds of a software for a version, or the honest reason there are none.</summary>
    public sealed class McBuildList
    {
        public List<McBuild> Builds = new List<McBuild>();
        public string Unavailable;   // null when the list came from the API
        public McBuild Latest => Builds.FirstOrDefault();
    }

    /// <summary>
    /// Server software builds from the projects' own public APIs: Paper (PaperMC), Purpur, the Fabric meta service and the
    /// Forge and NeoForge Maven metadata. Nothing is invented: when an API cannot be reached or lists nothing for a version,
    /// the reason is returned instead of a guess.
    /// </summary>
    public static class McSoftware
    {
        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp() { var h = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0 (server manager)"); return h; }
        private static readonly Dictionary<string, McBuildList> Cache = new Dictionary<string, McBuildList>();

        /// <summary>A short description of each software for the create flow.</summary>
        public static string Describe(Flavour f) => f switch
        {
            Flavour.Vanilla => "Mojang's own server.",
            Flavour.Paper => "Fast, with Bukkit and Spigot plugins.",
            Flavour.Purpur => "Paper with many extra options.",
            Flavour.Fabric => "A light mod loader.",
            Flavour.Forge => "The classic mod loader.",
            _ => "A modern fork of Forge.",
        };

        /// <summary>Software this app can download and run directly as a process, because its API gives a jar and a checksum.</summary>
        public static bool RunsAsLocalProcess(Flavour f) => f == Flavour.Vanilla || f == Flavour.Paper || f == Flavour.Purpur;

        /// <summary>Paper and Purpur answer the "tps" console command; the others do not report ticks per second.</summary>
        public static bool ReportsTps(Flavour f) => f == Flavour.Paper || f == Flavour.Purpur;

        public static async Task<McBuildList> BuildsAsync(Flavour f, string version)
        {
            if (f == Flavour.Vanilla) return new McBuildList { Builds = { new McBuild { Id = "", Channel = "release" } } };
            var key = f + "|" + version;
            lock (Cache) if (Cache.TryGetValue(key, out var hit)) return hit;
            McBuildList list;
            try
            {
                list = f switch
                {
                    Flavour.Paper => await PaperAsync(version),
                    Flavour.Purpur => await PurpurAsync(version),
                    Flavour.Fabric => await FabricAsync(version),
                    Flavour.Forge => await MavenAsync("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml", v => v.StartsWith(version + "-", StringComparison.Ordinal), v => v[(version.Length + 1)..]),
                    _ => await MavenAsync("https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml", v => v.StartsWith(NeoForgePrefix(version), StringComparison.Ordinal), v => v),
                };
                if (list.Unavailable == null && list.Builds.Count == 0) list.Unavailable = $"{f} lists no build for {version}.";
            }
            catch (Exception ex) { list = new McBuildList { Unavailable = $"The {f} build list could not be read: {ex.Message}" }; }
            if (list.Unavailable == null) lock (Cache) Cache[key] = list;
            return list;
        }

        /// <summary>NeoForge numbers its builds after the game version: 1.21.4 is 21.4.x, 1.21 is 21.0.x. Older games have no NeoForge.</summary>
        internal static string NeoForgePrefix(string game)
        {
            var parts = (game ?? "").Split('.');
            if (parts.Length < 2 || parts[0] != "1") return "\u0000";
            return parts[1] + "." + (parts.Length > 2 ? parts[2] : "0") + ".";
        }

        private static async Task<McBuildList> PaperAsync(string version)
        {
            var url = $"https://api.papermc.io/v2/projects/paper/versions/{Uri.EscapeDataString(version)}/builds";
            using var r = await Http.GetAsync(url);
            if ((int)r.StatusCode == 404) return new McBuildList { Unavailable = $"Paper has no builds for {version}." };
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            var list = new McBuildList();
            foreach (var b in doc.RootElement.GetProperty("builds").EnumerateArray().Reverse())
            {
                var id = b.GetProperty("build").GetInt32().ToString();
                string name = null, sha = null;
                if (b.TryGetProperty("downloads", out var dl) && dl.TryGetProperty("application", out var app))
                { name = app.GetProperty("name").GetString(); sha = app.TryGetProperty("sha256", out var h) ? h.GetString() : null; }
                list.Builds.Add(new McBuild
                {
                    Id = id, Channel = b.TryGetProperty("channel", out var c) ? c.GetString() : "default",
                    DownloadUrl = name == null ? null : $"https://api.papermc.io/v2/projects/paper/versions/{Uri.EscapeDataString(version)}/builds/{id}/downloads/{Uri.EscapeDataString(name)}",
                    HashKind = sha == null ? null : "sha256", Hash = sha
                });
            }
            return list;
        }

        private static async Task<McBuildList> PurpurAsync(string version)
        {
            using var r = await Http.GetAsync($"https://api.purpurmc.org/v2/purpur/{Uri.EscapeDataString(version)}");
            if ((int)r.StatusCode == 404) return new McBuildList { Unavailable = $"Purpur has no builds for {version}." };
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            var list = new McBuildList();
            if (!doc.RootElement.TryGetProperty("builds", out var builds) || !builds.TryGetProperty("all", out var all)) return new McBuildList { Unavailable = $"Purpur lists no builds for {version}." };
            foreach (var b in all.EnumerateArray().Select(x => x.GetString()).Reverse())
                list.Builds.Add(new McBuild { Id = b, Channel = "default", DownloadUrl = $"https://api.purpurmc.org/v2/purpur/{Uri.EscapeDataString(version)}/{b}/download" });
            return list;
        }

        /// <summary>Purpur publishes an MD5 per build on the build's own page; it is read just before downloading.</summary>
        internal static async Task<string> PurpurMd5Async(string version, string build)
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://api.purpurmc.org/v2/purpur/{Uri.EscapeDataString(version)}/{Uri.EscapeDataString(build)}"));
            return doc.RootElement.TryGetProperty("md5", out var m) ? m.GetString() : null;
        }

        private static async Task<McBuildList> FabricAsync(string version)
        {
            using var r = await Http.GetAsync($"https://meta.fabricmc.net/v2/versions/loader/{Uri.EscapeDataString(version)}");
            if (!r.IsSuccessStatusCode) return new McBuildList { Unavailable = $"Fabric lists no loader for {version}." };
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            var list = new McBuildList();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var l = e.GetProperty("loader");
                list.Builds.Add(new McBuild { Id = l.GetProperty("version").GetString(), Channel = l.TryGetProperty("stable", out var st) && st.GetBoolean() ? "stable" : "beta" });
            }
            // Stable loaders first, newest first, as the meta service already orders them by version.
            list.Builds = list.Builds.OrderBy(b => b.Channel == "stable" ? 0 : 1).ToList();
            return list;
        }

        private static async Task<McBuildList> MavenAsync(string metadataUrl, Func<string, bool> forVersion, Func<string, string> buildId)
        {
            var xml = XDocument.Parse(await Http.GetStringAsync(metadataUrl));
            var versions = xml.Descendants("version").Select(v => v.Value).Where(forVersion).Reverse().ToList();
            var list = new McBuildList();
            foreach (var v in versions) list.Builds.Add(new McBuild { Id = buildId(v), Channel = v.Contains("beta", StringComparison.OrdinalIgnoreCase) ? "beta" : "release" });
            return list;
        }

        /// <summary>
        /// Compares the installed build with the newest one the API lists. Returns null when there is nothing newer, or when the
        /// software has no build API (Vanilla) or the API could not be read; the caller shows that as "no update information".
        /// </summary>
        public static async Task<(McBuild Newer, string Unavailable)> UpdateAsync(ServerDef s)
        {
            if (s.Flavour == Flavour.Vanilla) return (null, "Vanilla has no build API; new game versions appear in Mojang's catalogue.");
            if (string.IsNullOrWhiteSpace(s.Build)) return (null, "The installed build was not recorded when this server was created.");
            var list = await BuildsAsync(s.Flavour, s.Version);
            if (list.Unavailable != null) return (null, list.Unavailable);
            var latest = list.Latest;
            if (latest == null || latest.Id == s.Build) return (null, null);
            bool newer = int.TryParse(latest.Id, out var a) && int.TryParse(s.Build, out var b) ? a > b : list.Builds.FindIndex(x => x.Id == s.Build) != 0;
            return (newer ? latest : null, null);
        }
    }
}
