using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using MonitoringTools.Minecraft;

namespace MonitoringTools.Core
{
    /// <summary>One model tag in the catalogue. Sizes are typical figures for the default quantisation, not measurements.</summary>
    public sealed class OllamaCatalogueEntry
    {
        public string Model;          // name:tag, exactly what "ollama pull" takes
        public string Family;
        public string Parameters;     // e.g. "7B"
        public double SizeGb;         // typical download size
        public int ContextK;          // context length in thousands of tokens, 0 when unknown
        public string Note;
        public bool Live;             // true when the entry came from a live library search (size is then an estimate from the parameter count)
        public bool SizeEstimated;
        public long SizeBytes => (long)(SizeGb * 1073741824.0);
    }

    public enum FitLevel { Good, Ok, Tight, No }

    public sealed class FitVerdict
    {
        public FitLevel Level;
        public string Summary, Reason;
    }

    public sealed class GpuInfo
    {
        public string Name;
        public long VramBytes;
        public bool VramFromAdapterRam;   // WMI AdapterRAM is a 32-bit field and cannot report more than 4 GB
    }

    /// <summary>Facts about this computer, read from Windows. Nothing here is estimated or guessed.</summary>
    public sealed class HardwareFacts
    {
        public long TotalRam, FreeRam, FreeDisk;
        public string ModelsFolder, DiskRoot;
        public List<GpuInfo> Gpus = new List<GpuInfo>();
        public string GpuError;
        public long BestVram => Gpus.Count == 0 ? 0 : Gpus.Max(g => g.VramBytes);
    }

    /// <summary>The embedded model snapshot, the optional live library search and the hardware-fit calculation.</summary>
    public static class OllamaCatalogue
    {
        /// <summary>The date the embedded list was written. Newer models exist; the live search covers them.</summary>
        public const string SnapshotDate = "2026-11-04";

        private static OllamaCatalogueEntry E(string model, string family, string p, double gb, int ctxK, string note) =>
            new OllamaCatalogueEntry { Model = model, Family = family, Parameters = p, SizeGb = gb, ContextK = ctxK, Note = note };

        /// <summary>Well-known Ollama library tags with typical download sizes (default 4-bit quantisation). A snapshot, not a live view.</summary>
        public static readonly IReadOnlyList<OllamaCatalogueEntry> Snapshot = new List<OllamaCatalogueEntry>
        {
            E("llama3.2:1b", "Llama", "1B", 1.3, 128, "Small and quick; good for trying things out."),
            E("llama3.2:3b", "Llama", "3B", 2.0, 128, "A capable small general model."),
            E("llama3.1:8b", "Llama", "8B", 4.9, 128, "A strong all-rounder for everyday chat."),
            E("llama3.3:70b", "Llama", "70B", 43, 128, "Large; needs a very big GPU or lots of RAM."),
            E("qwen2.5:0.5b", "Qwen", "0.5B", 0.4, 32, "Tiny; for very constrained machines."),
            E("qwen2.5:1.5b", "Qwen", "1.5B", 1.0, 32, "Small multilingual model."),
            E("qwen2.5:3b", "Qwen", "3B", 1.9, 32, "Small multilingual model."),
            E("qwen2.5:7b", "Qwen", "7B", 4.7, 32, "Good multilingual and coding balance."),
            E("qwen2.5:14b", "Qwen", "14B", 9.0, 32, "Noticeably stronger; wants about 12 GB of memory."),
            E("qwen2.5:32b", "Qwen", "32B", 20, 32, "Large; wants a 24 GB GPU or plenty of RAM."),
            E("qwen2.5-coder:7b", "Qwen", "7B", 4.7, 32, "Tuned for writing and reading code."),
            E("mistral:7b", "Mistral", "7B", 4.1, 32, "A long-standing 7B general model."),
            E("mistral-nemo:12b", "Mistral", "12B", 7.1, 128, "Long context at a moderate size."),
            E("gemma2:2b", "Gemma", "2B", 1.6, 8, "Small and fast."),
            E("gemma2:9b", "Gemma", "9B", 5.4, 8, "Strong for its size."),
            E("gemma2:27b", "Gemma", "27B", 16, 8, "Large; wants a 24 GB GPU or plenty of RAM."),
            E("gemma3:4b", "Gemma", "4B", 3.3, 128, "Small with a long context window."),
            E("gemma3:12b", "Gemma", "12B", 8.1, 128, "Mid-size with a long context window."),
            E("gemma3:27b", "Gemma", "27B", 17, 128, "Large; wants a 24 GB GPU or plenty of RAM."),
            E("phi3:mini", "Phi", "3.8B", 2.2, 128, "Compact reasoning-oriented model."),
            E("phi4:14b", "Phi", "14B", 9.1, 16, "Reasoning-oriented mid-size model."),
            E("deepseek-r1:1.5b", "DeepSeek", "1.5B", 1.1, 128, "Small step-by-step reasoning model."),
            E("deepseek-r1:7b", "DeepSeek", "7B", 4.7, 128, "Reasoning model that shows its working."),
            E("deepseek-r1:14b", "DeepSeek", "14B", 9.0, 128, "Reasoning model that shows its working."),
            E("deepseek-r1:32b", "DeepSeek", "32B", 20, 128, "Large reasoning model."),
            E("codellama:7b", "Code Llama", "7B", 3.8, 16, "Older code-focused model."),
            E("llava:7b", "LLaVA", "7B", 4.7, 32, "Can read images as well as text."),
            E("tinyllama:1.1b", "TinyLlama", "1.1B", 0.64, 2, "Very small; short context."),
            E("nomic-embed-text:latest", "Nomic", "137M", 0.27, 8, "Embedding model for search, not chat."),
        };

        // -------------------------------------------------------------- live search of the official library page

        private static readonly HttpClient Web = CreateWeb();
        private static HttpClient CreateWeb() { var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0"); return h; }

        private static readonly Regex Item = new Regex(@"<li\b[^>]*>\s*<a href=""/library/(?<name>[A-Za-z0-9._\-]+)""(?<body>.*?)</li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex Desc = new Regex(@"<p\b[^>]*>(?<t>[^<]{3,400})</p>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex SizeChip = new Regex(@"text-blue-600[^>]*>\s*(?<n>\d+(?:\.\d+)?)\s*(?<u>[bBmM])\s*<", RegexOptions.Compiled);

        /// <summary>
        /// Looks a search term up on the official library page (https://ollama.com/search) and reads the result list.
        /// This is the only call in the app that contacts the Ollama website; it sends only the search words and runs only
        /// when asked. The page layout is not a documented API, so a changed layout yields an error, never invented rows.
        /// Each row is one model family with one entry per advertised parameter size; sizes on disk are estimated from the
        /// parameter count and are marked as estimates.
        /// </summary>
        public static async Task<List<OllamaCatalogueEntry>> LiveSearchAsync(string query, CancellationToken ct)
        {
            query = (query ?? "").Trim();
            if (query.Length > 60) query = query[..60];
            var html = await Web.GetStringAsync("https://ollama.com/search?q=" + Uri.EscapeDataString(query), ct);
            if (html.Length > 3_000_000) throw new InvalidDataException("The library page was larger than expected, so it was not read.");
            var result = new List<OllamaCatalogueEntry>();
            foreach (Match m in Item.Matches(html))
            {
                var name = m.Groups["name"].Value;
                var body = m.Groups["body"].Value;
                var dm = Desc.Match(body);
                var note = dm.Success ? System.Net.WebUtility.HtmlDecode(dm.Groups["t"].Value).Trim() : "";
                var sizes = SizeChip.Matches(body).Select(x => (x.Groups["n"].Value, x.Groups["u"].Value)).ToList();
                if (sizes.Count == 0)
                {
                    result.Add(new OllamaCatalogueEntry { Model = name + ":latest", Family = name, Parameters = "?", SizeGb = 0, Note = note, Live = true, SizeEstimated = true });
                    continue;
                }
                foreach (var (n, u) in sizes)
                {
                    double b = double.Parse(n, System.Globalization.CultureInfo.InvariantCulture) * (char.ToLowerInvariant(u[0]) == 'm' ? 0.001 : 1);
                    result.Add(new OllamaCatalogueEntry
                    {
                        Model = $"{name}:{n}{char.ToLowerInvariant(u[0])}", Family = name, Parameters = n + char.ToUpperInvariant(u[0]),
                        SizeGb = Math.Round(b * 0.6 + 0.1, 1), Note = note, Live = true, SizeEstimated = true
                    });
                }
            }
            if (result.Count == 0) throw new InvalidDataException("The library page returned no models that could be read (no match, or its layout has changed).");
            return result.Take(80).ToList();
        }

        // -------------------------------------------------------------- hardware

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatusEx
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        /// <summary>The folder the Ollama server stores models in: OLLAMA_MODELS when set, otherwise the user's .ollama\models.</summary>
        public static string ModelsFolder()
        {
            var env = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
            if (!string.IsNullOrWhiteSpace(env)) return env.Trim().Trim('"');
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama", "models");
        }

        /// <summary>Reads memory, the graphics adapters and free disk space. Runs a short PowerShell query for the adapters, so call it off the UI thread.</summary>
        public static HardwareFacts ReadHardware()
        {
            var h = new HardwareFacts { ModelsFolder = ModelsFolder() };
            var ms = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref ms)) { h.TotalRam = (long)ms.ullTotalPhys; h.FreeRam = (long)ms.ullAvailPhys; }
            try
            {
                // Walk up to the nearest existing folder so a models folder that does not exist yet still resolves to its drive.
                var probe = h.ModelsFolder;
                while (!string.IsNullOrEmpty(probe) && !Directory.Exists(probe)) probe = Path.GetDirectoryName(probe);
                var root = Path.GetPathRoot(string.IsNullOrEmpty(probe) ? h.ModelsFolder : probe);
                var drive = new DriveInfo(root);
                h.DiskRoot = drive.Name; h.FreeDisk = drive.AvailableFreeSpace;
            }
            catch { /* leaves FreeDisk at 0, which the verdict reports as unknown */ }
            ReadGpus(h);
            return h;
        }

        private static void ReadGpus(HardwareFacts h)
        {
            // Win32_VideoController through CIM. Its AdapterRAM field is 32-bit and tops out at 4 GB, so the display driver's own
            // 64-bit figure from the registry is preferred when it exists for the same adapter.
            var r = ServerManager.Run("powershell.exe",
                "-NoProfile -NonInteractive -Command \"Get-CimInstance Win32_VideoController | Select-Object Name,AdapterRAM | ConvertTo-Json -Compress\"", 20000);
            if (r.Code != 0 || string.IsNullOrWhiteSpace(r.Out)) { h.GpuError = "The graphics adapter list could not be read (" + (r.Out ?? "no output").Split('\n')[0].Trim() + ")."; return; }
            try
            {
                using var doc = JsonDocument.Parse(r.Out);
                var items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement> { doc.RootElement };
                var reg = ReadRegistryVram();
                foreach (var it in items)
                {
                    var name = it.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : "Unknown adapter";
                    long ram = it.TryGetProperty("AdapterRAM", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt64() : 0;
                    bool fromWmi = true;
                    if (reg.TryGetValue(name, out var big) && big > ram) { ram = big; fromWmi = false; }
                    h.Gpus.Add(new GpuInfo { Name = name, VramBytes = ram, VramFromAdapterRam = fromWmi && ram >= 4000000000L });
                }
            }
            catch (Exception ex) { h.GpuError = "The graphics adapter list could not be parsed: " + ex.Message; }
        }

        private static Dictionary<string, long> ReadRegistryVram()
        {
            var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                if (cls == null) return map;
                foreach (var sub in cls.GetSubKeyNames())
                {
                    using var k = cls.OpenSubKey(sub);
                    var desc = k?.GetValue("DriverDesc") as string;
                    var mem = k?.GetValue("HardwareInformation.qwMemorySize");
                    if (desc == null || mem == null) continue;
                    long v = mem is long l ? l : mem is byte[] b && b.Length >= 8 ? BitConverter.ToInt64(b, 0) : 0;
                    if (v > 0) map[desc] = v;
                }
            }
            catch { /* the registry value is optional */ }
            return map;
        }

        // -------------------------------------------------------------- fit

        private static string Gb(long bytes) => $"{bytes / 1073741824.0:0.0} GB";

        /// <summary>
        /// Compares a model's memory need with the facts about this computer. The need is the download size plus about 15 percent
        /// and 0.5 GB for the context cache, which is a rule of thumb, and the verdict says so; real use varies with context length.
        /// </summary>
        public static FitVerdict Fit(long downloadBytes, HardwareFacts h)
        {
            if (downloadBytes <= 0) return new FitVerdict { Level = FitLevel.Tight, Summary = "Size unknown", Reason = "The size of this model is not known, so no verdict can be given." };
            long need = (long)(downloadBytes * 1.15) + 536870912L;
            string needS = Gb(need);
            if (h.FreeDisk > 0 && downloadBytes > h.FreeDisk)
                return new FitVerdict { Level = FitLevel.No, Summary = "Not enough disk", Reason = $"Download is about {Gb(downloadBytes)}, but only {Gb(h.FreeDisk)} is free on {h.DiskRoot}." };
            if (h.BestVram >= need)
                return new FitVerdict { Level = FitLevel.Good, Summary = "Fits on the GPU", Reason = $"Needs about {needS}; your best GPU reports {Gb(h.BestVram)} of memory. Should run fast." };
            if (h.FreeRam >= need)
                return new FitVerdict { Level = FitLevel.Ok, Summary = h.BestVram > 0 ? "Runs, partly on the CPU" : "Runs on the CPU", Reason = $"Needs about {needS}; you have {Gb(h.FreeRam)} of free RAM and {(h.BestVram > 0 ? Gb(h.BestVram) + " of GPU memory" : "no GPU memory reported")}. It will work but slower than a full GPU fit." };
            if (h.TotalRam >= need)
                return new FitVerdict { Level = FitLevel.Tight, Summary = "Tight: close other programs", Reason = $"Needs about {needS}, you have {Gb(h.FreeRam)} free of {Gb(h.TotalRam)} RAM. It fits only if other programs release memory, and may swap to disk." };
            return new FitVerdict { Level = FitLevel.No, Summary = "Too large", Reason = $"Needs about {needS}, but this computer has {Gb(h.TotalRam)} of RAM and {Gb(h.BestVram)} of GPU memory." };
        }
    }
}
