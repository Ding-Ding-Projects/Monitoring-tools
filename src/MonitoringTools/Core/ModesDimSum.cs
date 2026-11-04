using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MonitoringTools.Core
{
    /// <summary>One dish from the public catalogue.</summary>
    public sealed class DimSumDish
    {
        public string Id { get; set; } = "";
        public string En { get; set; } = "";
        public string Zh { get; set; } = "";
        public string File { get; set; } = "";
        public string AltEn { get; set; } = "";
        public string ImagePath { get; set; } = "";   // local cache path once downloaded
    }

    /// <summary>
    /// The dim sum surprise. A 10% draw once per launch. Dish names come from the public catalogue index and the
    /// picture from its published release assets, fetched over HTTPS on demand and kept in a small cache. Nothing
    /// is bundled or tracked in this repository. When the catalogue or the picture is unavailable the surprise
    /// simply does not show, and the Modes page says why.
    /// </summary>
    public static partial class Modes
    {
        public const string DimSumIndexUrl = "https://raw.githubusercontent.com/Ding-Ding-Projects/dim-sum-photos/main/catalog/index.json";
        public const string DimSumAssetBase = "https://github.com/Ding-Ding-Projects/dim-sum-photos/releases/download/";
        public const string DimSumSource = "Ding-Ding-Projects/dim-sum-photos";
        private static readonly string[] DimSumTags = new[] { "catalog-v1" }.Concat(Enumerable.Range(2, 12).Select(i => $"catalog-v1-part-{i:000}")).ToArray();

        private static readonly HttpClient Http = MakeClient();
        private static HttpClient MakeClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools-DimSum/1.0");
            return c;
        }

        /// <summary>What happened to this launch's draw, for the Modes page.</summary>
        public static string DimSumLaunchStatus { get; private set; } = "Not drawn yet.";
        /// <summary>Last problem fetching the catalogue or a picture, or null.</summary>
        public static string DimSumError { get; private set; }
        public static string DimSumCacheInfo { get; private set; } = "No catalogue cached yet.";

        private static string DimDir { get { var d = Path.Combine(Settings.DataDir, "dimsum"); Directory.CreateDirectory(d); return d; } }
        private static string DimIndexCache => Path.Combine(DimDir, "index-cache.json");

        private static void ScheduleDimSumDraw()
        {
            // One fresh draw per launch. There is deliberately no setting that turns this off.
            bool hit = Random.Shared.NextDouble() < 0.10;
            if (!hit) { DimSumLaunchStatus = "Not drawn this launch (each launch has a 10% chance)."; return; }
            DimSumLaunchStatus = "Drawn this launch. Waiting for a quiet moment.";
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            t.Tick += async (a, b) =>
            {
                t.Stop();
                string why = SkipReason();
                if (why != null) { DimSumLaunchStatus = "Drawn this launch but not shown: " + why + "."; return; }
                DimSumLaunchStatus = "Drawn this launch. Fetching a dish.";
                var shown = await ShowDimSumAsync();
                DimSumLaunchStatus = shown ? "Drawn and shown this launch." : "Drawn this launch but nothing could be shown: " + (DimSumError ?? "unknown reason") + ".";
            };
            t.Start();
        }

        private static string SkipReason()
        {
            if (Blocks(ModeFeature.DimSum)) return "a mode that removes it is on";
            if (!Settings.Current.OnboardingDone) return "first-run setup is not finished";
            if (App.ShouldSuppress) return "game mode or snooze is holding popups";
            return null;
        }

        /// <summary>Fetch one dish and show it. Used by the launch draw and the page's Preview button. Never throws.</summary>
        public static async Task<bool> ShowDimSumAsync()
        {
            if (Blocks(ModeFeature.DimSum)) { DimSumError = "unavailable while a mode that removes it is on"; return false; }
            try
            {
                var dish = await PickDishAsync();
                if (dish == null) return false;
                var mode = ScheduledLanguage ?? "bilingual";
                System.Windows.Application.Current.Dispatcher.Invoke(() => new UI.ModesDimSumWindow(dish, mode).Show());
                DimSumError = null;
                return true;
            }
            catch (Exception ex) { DimSumError = ex.Message; Log.Warn("Dim sum: " + ex.Message); return false; }
        }

        private static async Task<List<DimSumDish>> LoadCatalogueAsync()
        {
            try
            {
                if (File.Exists(DimIndexCache) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(DimIndexCache)).TotalDays < 7)
                {
                    var cached = ReadIndexCache();
                    if (cached.Count > 0) return cached;
                }
                using var resp = await Http.GetAsync(DimSumIndexUrl, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                if (resp.Content.Headers.ContentLength > 40 * 1024 * 1024) throw new InvalidDataException("the catalogue is unexpectedly large");
                var revision = resp.Headers.ETag?.Tag ?? resp.Content.Headers.LastModified?.ToString("R") ?? "unknown";
                using var stream = await resp.Content.ReadAsStreamAsync();
                var dishes = await Task.Run(() =>
                {
                    using var doc = JsonDocument.Parse(stream);
                    var list = new List<DimSumDish>();
                    foreach (var d in doc.RootElement.GetProperty("dishes").EnumerateArray())
                    {
                        if (!d.TryGetProperty("image", out var img) || !img.TryGetProperty("path", out var path)) continue;
                        var name = d.GetProperty("name");
                        var file = Path.GetFileName(path.GetString() ?? "");
                        if (file.Length == 0 || !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                        string alt = img.TryGetProperty("alt", out var a) && a.TryGetProperty("en", out var ae) ? ae.GetString() : null;
                        list.Add(new DimSumDish
                        {
                            Id = d.GetProperty("id").GetString(), En = name.GetProperty("en").GetString(), Zh = name.GetProperty("zhHant").GetString(),
                            File = file, AltEn = alt ?? ""
                        });
                    }
                    return list;
                });
                // A small cache of names and picture file names only, recording where it came from.
                File.WriteAllText(DimIndexCache, JsonSerializer.Serialize(new { source = DimSumIndexUrl, revision, fetchedUtc = DateTime.UtcNow, dishes }));
                DimSumCacheInfo = $"Catalogue cached from {DimSumSource} (revision {revision}), {dishes.Count} dishes.";
                return dishes;
            }
            catch (Exception ex)
            {
                DimSumError = "the dish catalogue is unavailable (" + ex.Message + ")";
                var stale = ReadIndexCache();
                return stale;
            }
        }

        private static List<DimSumDish> ReadIndexCache()
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(DimIndexCache));
                var list = JsonSerializer.Deserialize<List<DimSumDish>>(doc.RootElement.GetProperty("dishes").GetRawText()) ?? new List<DimSumDish>();
                var rev = doc.RootElement.TryGetProperty("revision", out var r) ? r.GetString() : "unknown";
                DimSumCacheInfo = $"Catalogue cache from {DimSumSource} (revision {rev}), {list.Count} dishes.";
                return list;
            }
            catch { return new List<DimSumDish>(); }
        }

        private static async Task<DimSumDish> PickDishAsync()
        {
            DimSumError = null;
            var all = await LoadCatalogueAsync();
            if (all.Count == 0) { DimSumError ??= "the dish catalogue is unavailable"; return null; }
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var dish = all[Random.Shared.Next(all.Count)];
                var local = Path.Combine(DimDir, dish.File);
                if (File.Exists(local)) { dish.ImagePath = local; return dish; }
                if (await DownloadImageAsync(dish, local)) { dish.ImagePath = local; return dish; }
            }
            DimSumError = "no published picture was found for the dishes tried";
            return null;
        }

        private static async Task<bool> DownloadImageAsync(DimSumDish dish, string target)
        {
            foreach (var tag in DimSumTags)
            {
                try
                {
                    var url = DimSumAssetBase + tag + "/" + Uri.EscapeDataString(dish.File);
                    using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    if (!resp.IsSuccessStatusCode) continue;               // not in this volume, try the next
                    if (resp.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps) continue;
                    if (resp.Content.Headers.ContentLength is long n && n > 12 * 1024 * 1024) continue;
                    var tmp = target + ".part";
                    await using (var fs = File.Create(tmp))
                    {
                        await using var body = await resp.Content.ReadAsStreamAsync();
                        var buf = new byte[81920]; long total = 0; int read;
                        while ((read = await body.ReadAsync(buf, 0, buf.Length)) > 0)
                        {
                            total += read;
                            if (total > 12 * 1024 * 1024) throw new InvalidDataException("picture too large");
                            await fs.WriteAsync(buf, 0, read);
                        }
                    }
                    File.Move(tmp, target, true);
                    return true;
                }
                catch (Exception ex) { DimSumError = "a picture could not be fetched (" + ex.Message + ")"; }
            }
            return false;
        }

        /// <summary>Delete the downloaded pictures and the catalogue cache.</summary>
        public static void ClearDimSumCache()
        {
            try { Directory.Delete(DimDir, true); } catch (Exception ex) { Log.Warn("Dim sum cache: " + ex.Message); }
            DimSumCacheInfo = "No catalogue cached yet.";
        }
    }
}
