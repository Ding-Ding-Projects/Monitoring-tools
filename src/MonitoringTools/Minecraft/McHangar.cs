using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// The Hangar catalogue (hangar.papermc.io, public, no account needed): plugins for Paper, which Purpur also runs. Results
    /// are filtered to the server's game version. A file is installed only after its bytes match the SHA-256 Hangar lists for
    /// it, and a version that Hangar offers only as an external link (with no checksum) is refused.
    /// </summary>
    public static class McHangar
    {
        public const string Api = "https://hangar.papermc.io/api/v1";
        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp() { var h = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0 (server manager)"); return h; }

        public static bool Serves(Flavour f) => f == Flavour.Paper || f == Flavour.Purpur;

        public static async Task<List<McCatalogHit>> SearchAsync(string query, Flavour f, string version, int limit = 10)
        {
            if (!Serves(f)) throw new NotSupportedException("Hangar lists plugins for Paper and Purpur.");
            var url = $"{Api}/projects?limit={limit}&offset=0&platform=PAPER&version={Uri.EscapeDataString(version)}&q={Uri.EscapeDataString(query ?? "")}";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            return doc.RootElement.GetProperty("result").EnumerateArray().Select(p => new McCatalogHit
            {
                Source = "Hangar",
                ProjectId = p.GetProperty("id").GetInt64().ToString(),
                Title = p.GetProperty("name").GetString(),
                Description = p.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                Slug = p.GetProperty("namespace").GetProperty("slug").GetString(),
                Author = p.GetProperty("namespace").GetProperty("owner").GetString(),
                Downloads = p.TryGetProperty("stats", out var st) && st.TryGetProperty("downloads", out var n) ? n.GetInt64() : 0,
            }).ToList();
        }

        /// <summary>One downloadable version: its name, file, address, SHA-256 and the game versions it declares.</summary>
        public sealed class HangarVersion
        {
            public string Name = "", File = "", Url, Sha256, Channel = "";
            public List<string> GameVersions = new List<string>();
        }

        /// <summary>The versions Hangar lists for this game version, newest first. A Release-channel version is preferred by <see cref="Newest"/>.</summary>
        public static async Task<List<HangarVersion>> VersionsAsync(string projectIdOrSlug, string version, int limit = 10)
        {
            var url = $"{Api}/projects/{Uri.EscapeDataString(projectIdOrSlug)}/versions?limit={limit}&offset=0&platform=PAPER&platformVersion={Uri.EscapeDataString(version)}";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var list = new List<HangarVersion>();
            foreach (var v in doc.RootElement.GetProperty("result").EnumerateArray())
            {
                if (!v.TryGetProperty("downloads", out var dls) || !dls.TryGetProperty("PAPER", out var paper)) continue;
                var hv = new HangarVersion
                {
                    Name = v.GetProperty("name").GetString(),
                    Channel = v.TryGetProperty("channel", out var ch) && ch.TryGetProperty("name", out var cn) ? cn.GetString() : "",
                    Url = paper.TryGetProperty("downloadUrl", out var du) && du.ValueKind == JsonValueKind.String ? du.GetString() : null,
                };
                if (paper.TryGetProperty("fileInfo", out var fi) && fi.ValueKind == JsonValueKind.Object)
                {
                    hv.File = fi.TryGetProperty("name", out var fn) ? fn.GetString() : "";
                    hv.Sha256 = fi.TryGetProperty("sha256Hash", out var sh) && sh.ValueKind == JsonValueKind.String ? sh.GetString() : null;
                }
                if (v.TryGetProperty("platformDependencies", out var pd) && pd.TryGetProperty("PAPER", out var pv))
                    hv.GameVersions = pv.EnumerateArray().Select(x => x.GetString()).ToList();
                list.Add(hv);
            }
            return list;
        }

        public static HangarVersion Newest(List<HangarVersion> list) =>
            list.FirstOrDefault(v => string.Equals(v.Channel, "Release", StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault();

        public static async Task<string> InstallAsync(McCatalogHit hit, ServerDef s, Action<string> report, HangarVersion pick = null, string replaces = null)
        {
            var folder = McContent.FolderOf(s) ?? throw new NotSupportedException("This server runs no plugins.");
            if (pick == null)
            {
                var versions = await VersionsAsync(hit.ProjectId, s.Version);
                pick = Newest(versions) ?? throw new InvalidOperationException($"{hit.Title} has no file on Hangar for Paper {s.Version}.");
            }
            if (pick.Url == null) throw new InvalidDataException($"Hangar lists {hit.Title} {pick.Name} only as a link to another site, with no checksum, so it was not installed.");
            if (pick.Sha256 == null) throw new InvalidDataException("Hangar lists no SHA-256 for this file, so it was not installed.");
            if (!pick.File.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The Hangar file is not a jar.");
            report?.Invoke($"Downloading {pick.File}…");
            var tmp = Path.Combine(Path.GetTempPath(), "mt-hangar-" + Guid.NewGuid().ToString("N") + ".jar");
            try
            {
                await ServerManager.Download(pick.Url, tmp, report);
                var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(tmp)));
                if (!string.Equals(actual, pick.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The download did not match Hangar's SHA-256, so it was not installed.");
                McCatalogue.Replace(folder, tmp, pick.File, replaces);
            }
            finally { try { File.Delete(tmp); } catch { } }
            McCatalogue.Record(s, new McCatalogue.Entry { File = pick.File, Source = "Hangar", ProjectId = hit.ProjectId, Title = hit.Title, VersionId = pick.Name, VersionName = pick.Name, GameVersions = pick.GameVersions });
            return $"Installed {hit.Title} {pick.Name} ({pick.File}) from Hangar. The SHA-256 matched. Restart the server to load it.";
        }
    }
}
