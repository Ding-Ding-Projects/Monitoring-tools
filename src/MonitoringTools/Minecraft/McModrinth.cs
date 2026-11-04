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
    public sealed class McCatalogHit
    {
        public string ProjectId, Title, Description, Slug, Author;
        public long Downloads;
        /// <summary>The catalogue the hit came from: "Modrinth" or "Hangar".</summary>
        public string Source = "Modrinth";
    }

    /// <summary>
    /// The Modrinth catalogue (api.modrinth.com, public, no account needed), filtered to the server's game version and
    /// software. A file is only installed after its bytes match the SHA-512 the catalogue lists for it.
    /// </summary>
    public static class McModrinth
    {
        public const string Api = "https://api.modrinth.com/v2";
        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp() { var h = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0 (server manager)"); return h; }

        /// <summary>The Modrinth loader names a server software accepts. Paper and Purpur also run Spigot and Bukkit plugins.</summary>
        public static string[] Loaders(Flavour f) => f switch
        {
            Flavour.Paper => new[] { "paper", "spigot", "bukkit" },
            Flavour.Purpur => new[] { "purpur", "paper", "spigot", "bukkit" },
            Flavour.Fabric => new[] { "fabric" },
            Flavour.Forge => new[] { "forge" },
            Flavour.NeoForge => new[] { "neoforge" },
            _ => Array.Empty<string>(),
        };

        public static string ProjectType(Flavour f) => f == Flavour.Paper || f == Flavour.Purpur ? "plugin" : "mod";

        /// <summary>The facets parameter: every group must match, any value inside a group may match.</summary>
        internal static string Facets(Flavour f, string version) =>
            JsonSerializer.Serialize(new[]
            {
                new[] { "versions:" + version },
                Loaders(f).Select(l => "categories:" + l).ToArray(),
                new[] { "project_type:" + ProjectType(f) },
                new[] { "server_side:required", "server_side:optional" },
            });

        public static async Task<List<McCatalogHit>> SearchAsync(string query, Flavour f, string version, int limit = 10)
        {
            if (Loaders(f).Length == 0) throw new NotSupportedException("Vanilla runs no plugins or mods.");
            var url = $"{Api}/search?limit={limit}&index=relevance&query={Uri.EscapeDataString(query ?? "")}&facets={Uri.EscapeDataString(Facets(f, version))}";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            return doc.RootElement.GetProperty("hits").EnumerateArray().Select(h => new McCatalogHit
            {
                ProjectId = h.GetProperty("project_id").GetString(),
                Title = h.GetProperty("title").GetString(),
                Description = h.TryGetProperty("description", out var d) ? d.GetString() : "",
                Slug = h.TryGetProperty("slug", out var sl) ? sl.GetString() : "",
                Author = h.TryGetProperty("author", out var a) ? a.GetString() : "",
                Downloads = h.TryGetProperty("downloads", out var n) ? n.GetInt64() : 0,
            }).ToList();
        }

        /// <summary>Every version of a project for this game version and software, newest first, as the catalogue returns them.</summary>
        public static async Task<List<JsonElement>> VersionsAsync(string projectId, ServerDef s)
        {
            var url = $"{Api}/project/{Uri.EscapeDataString(projectId)}/version?game_versions={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { s.Version }))}&loaders={Uri.EscapeDataString(JsonSerializer.Serialize(Loaders(s.Flavour)))}";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            return doc.RootElement.EnumerateArray().Select(v => v.Clone()).ToList();
        }

        /// <summary>
        /// Downloads the newest file of a project for this version and software, checks it against the catalogue's SHA-512,
        /// and copies it into the server's folder (which is backed up first). Returns what was installed.
        /// </summary>
        public static async Task<string> InstallAsync(McCatalogHit hit, ServerDef s, Action<string> report)
        {
            if (McContent.FolderOf(s) == null) throw new NotSupportedException("Vanilla runs no plugins or mods.");
            var ver = (await VersionsAsync(hit.ProjectId, s)).FirstOrDefault();
            if (ver.ValueKind != JsonValueKind.Object) throw new InvalidOperationException($"{hit.Title} has no file for {s.Flavour} {s.Version}.");
            return await InstallVersionAsync(ver, s, report, null, hit.Title);
        }

        /// <summary>Installs one given catalogue version, replacing <paramref name="replaces"/> when it is an update.</summary>
        public static async Task<string> InstallVersionAsync(JsonElement ver, ServerDef s, Action<string> report, string replaces = null, string title = null)
        {
            var folder = McContent.FolderOf(s) ?? throw new NotSupportedException("Vanilla runs no plugins or mods.");
            var files = ver.GetProperty("files").EnumerateArray().ToList();
            var file = files.FirstOrDefault(x => x.TryGetProperty("primary", out var p) && p.GetBoolean());
            if (file.ValueKind != JsonValueKind.Object) file = files.FirstOrDefault();
            if (file.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("The catalogue lists no file to download.");
            var name = file.GetProperty("filename").GetString();
            var dl = file.GetProperty("url").GetString();
            var sha512 = file.GetProperty("hashes").TryGetProperty("sha512", out var h) ? h.GetString() : null;
            if (sha512 == null) throw new InvalidDataException("The catalogue lists no SHA-512 for this file, so it was not installed.");
            if (!name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The catalogue file is not a jar.");
            report?.Invoke($"Downloading {name}…");
            var tmp = Path.Combine(Path.GetTempPath(), "mt-modrinth-" + Guid.NewGuid().ToString("N") + ".jar");
            try
            {
                await ServerManager.Download(dl, tmp, report);
                var actual = Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(tmp)));
                if (!string.Equals(actual, sha512, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The download did not match the catalogue's SHA-512, so it was not installed.");
                McCatalogue.Replace(folder, tmp, name, replaces);
            }
            finally { try { File.Delete(tmp); } catch { } }
            var vn = ver.TryGetProperty("version_number", out var vnum) ? vnum.GetString() : "";
            McCatalogue.Record(s, new McCatalogue.Entry
            {
                File = name, Source = "Modrinth", ProjectId = ver.GetProperty("project_id").GetString(), Title = title ?? name, VersionId = ver.GetProperty("id").GetString(), VersionName = vn,
                GameVersions = ver.TryGetProperty("game_versions", out var gv) ? gv.EnumerateArray().Select(x => x.GetString()).ToList() : new List<string>(),
            });
            return $"Installed {title ?? name} {vn} ({name}). The SHA-512 matched the catalogue. Restart the server to load it.";
        }
    }
}
