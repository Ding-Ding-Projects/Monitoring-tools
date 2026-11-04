using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public sealed class OllamaModel
    {
        public string Name, Family, ParameterSize, Quantization;
        public long Size;
        public DateTime Modified;
    }

    public sealed class OllamaRunning
    {
        public string Name;
        public long Size, SizeVram;
        public DateTime Expires;
    }

    public sealed class OllamaChatTurn
    {
        public string Role { get; set; }
        public string Content { get; set; }
        /// <summary>Base64 images for a vision model, sent in the message's "images" array. Null or empty for text only.</summary>
        public List<string> Images { get; set; }
    }

    /// <summary>
    /// A small client for the documented local Ollama HTTP API. It talks only to 127.0.0.1 on the
    /// configured port, and it never contacts the Ollama website: model downloads are done by the
    /// local Ollama server itself through its own pull endpoint.
    /// </summary>
    public static class Ollama
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        private static Process _started;

        public static int Port => Settings.Current.OllamaPort is >= 1024 and <= 65535 ? Settings.Current.OllamaPort : 11434;
        public static string BaseUrl => $"http://127.0.0.1:{Port}";

        // -------------------------------------------------------------- detection

        /// <summary>The path of ollama.exe if it is installed in the usual places or on PATH; otherwise null.</summary>
        public static string FindExe()
        {
            var candidates = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe"),
            };
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try { if (!string.IsNullOrWhiteSpace(dir)) candidates.Add(Path.Combine(dir.Trim().Trim('"'), "ollama.exe")); } catch { }
            }
            return candidates.FirstOrDefault(File.Exists);
        }

        public static bool WingetAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo("winget", "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var p = Process.Start(psi);
                return p != null && p.WaitForExit(8000) && p.ExitCode == 0;
            }
            catch { return false; }
        }

        /// <summary>The server's version, or null when nothing answers on the configured port.</summary>
        public static async Task<string> VersionAsync(CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(2500);
                var json = await Http.GetStringAsync(BaseUrl + "/api/version", cts.Token);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : "unknown";
            }
            catch { return null; }
        }

        // -------------------------------------------------------------- install and lifecycle

        /// <summary>Installs Ollama with the Windows Package Manager (id Ollama.Ollama). Output lines are reported as they arrive.</summary>
        public static async Task<int> InstallWithWingetAsync(Action<string> line, CancellationToken ct)
        {
            var psi = new ProcessStartInfo("winget", "install -e --id Ollama.Ollama --accept-package-agreements --accept-source-agreements")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
            using var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (o, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) line?.Invoke(e.Data.Trim()); };
            p.ErrorDataReceived += (o, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) line?.Invoke(e.Data.Trim()); };
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
            using (ct.Register(() => { try { p.Kill(true); } catch { } }))
                await p.WaitForExitAsync(CancellationToken.None);
            return p.ExitCode;
        }

        /// <summary>Starts "ollama serve" bound to loopback only, hidden, and waits briefly for it to answer.</summary>
        public static async Task StartAsync()
        {
            var exe = FindExe() ?? throw new InvalidOperationException("Ollama is not installed.");
            if (await VersionAsync() != null) return;
            var psi = new ProcessStartInfo(exe, "serve") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.Environment["OLLAMA_HOST"] = "127.0.0.1:" + Port;
            _started = Process.Start(psi);
            _started?.BeginOutputReadLine();
            _started?.BeginErrorReadLine();
            for (int i = 0; i < 40; i++)
            {
                await Task.Delay(250);
                if (await VersionAsync() != null) return;
                if (_started != null && _started.HasExited) throw new InvalidOperationException("The Ollama server exited straight away (exit code " + _started.ExitCode + ").");
            }
            throw new TimeoutException("The Ollama server did not answer within 10 seconds.");
        }

        /// <summary>Stops every Ollama server and tray process owned by this user. Models stay on disk.</summary>
        public static int StopAll()
        {
            int n = 0;
            foreach (var name in new[] { "ollama app", "ollama", "ollama_llama_server" })
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(true); n++; } catch { }
                    finally { p.Dispose(); }
                }
            _started = null;
            return n;
        }

        // -------------------------------------------------------------- models

        public static async Task<List<OllamaModel>> ModelsAsync(CancellationToken ct = default)
        {
            var json = await Http.GetStringAsync(BaseUrl + "/api/tags", ct);
            using var doc = JsonDocument.Parse(json);
            var list = new List<OllamaModel>();
            if (!doc.RootElement.TryGetProperty("models", out var arr)) return list;
            foreach (var m in arr.EnumerateArray())
            {
                var d = m.TryGetProperty("details", out var det) ? det : default;
                string S(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
                list.Add(new OllamaModel
                {
                    Name = S(m, "name"),
                    Size = m.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0,
                    Family = S(d, "family"), ParameterSize = S(d, "parameter_size"), Quantization = S(d, "quantization_level"),
                    Modified = m.TryGetProperty("modified_at", out var mt) && mt.TryGetDateTime(out var dt) ? dt.ToLocalTime() : DateTime.MinValue
                });
            }
            return list.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static async Task<List<OllamaRunning>> RunningAsync(CancellationToken ct = default)
        {
            var json = await Http.GetStringAsync(BaseUrl + "/api/ps", ct);
            using var doc = JsonDocument.Parse(json);
            var list = new List<OllamaRunning>();
            if (!doc.RootElement.TryGetProperty("models", out var arr)) return list;
            foreach (var m in arr.EnumerateArray())
                list.Add(new OllamaRunning
                {
                    Name = m.TryGetProperty("name", out var n) ? n.GetString() : "",
                    Size = m.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                    SizeVram = m.TryGetProperty("size_vram", out var v) ? v.GetInt64() : 0,
                    Expires = m.TryGetProperty("expires_at", out var e) && e.TryGetDateTime(out var dt) ? dt.ToLocalTime() : DateTime.MinValue
                });
            return list;
        }

        public static async Task DeleteAsync(string model, CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, BaseUrl + "/api/delete")
            { Content = new StringContent(JsonSerializer.Serialize(new { model }), Encoding.UTF8, "application/json") };
            using var r = await Http.SendAsync(req, ct);
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException(await ErrorText(r));
        }

        /// <summary>What the server reports about one installed model through /api/show.</summary>
        public sealed class ModelDetails
        {
            public string Name, Family, ParameterSize, Quantization, Format, Template, Parameters, LicenceSummary, LicenceKind;
            public long ContextLength;
            public List<string> Capabilities = new List<string>();
        }

        public static async Task<ModelDetails> ShowAsync(string model, CancellationToken ct = default)
        {
            using var r = await Http.PostAsync(BaseUrl + "/api/show",
                new StringContent(JsonSerializer.Serialize(new { model }), Encoding.UTF8, "application/json"), ct);
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException(await ErrorText(r));
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            string S(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
            var d = root.TryGetProperty("details", out var det) ? det : default;
            var info = new ModelDetails
            {
                Name = model, Family = S(d, "family"), ParameterSize = S(d, "parameter_size"), Quantization = S(d, "quantization_level"), Format = S(d, "format"),
                Template = S(root, "template"), Parameters = S(root, "parameters")
            };
            if (root.TryGetProperty("model_info", out var mi) && mi.ValueKind == JsonValueKind.Object)
                foreach (var p in mi.EnumerateObject())
                    if (p.Name.EndsWith(".context_length", StringComparison.Ordinal) && p.Value.ValueKind == JsonValueKind.Number) { info.ContextLength = p.Value.GetInt64(); break; }
            if (root.TryGetProperty("capabilities", out var caps) && caps.ValueKind == JsonValueKind.Array)
                foreach (var c in caps.EnumerateArray()) if (c.ValueKind == JsonValueKind.String) info.Capabilities.Add(c.GetString());
            var lic = S(root, "license");
            (info.LicenceKind, info.LicenceSummary) = SummariseLicence(lic);
            return info;
        }

        /// <summary>A short, honest summary of a licence text: a recognised name when one is clearly present, plus its opening lines. Not legal advice.</summary>
        public static (string Kind, string Summary) SummariseLicence(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return ("Not stated", "The server returned no licence text for this model. Check the model's page in the Ollama library.");
            string kind = "Custom or unrecognised licence";
            var t = text.ToLowerInvariant();
            if (t.Contains("apache license")) kind = "Apache License";
            else if (t.Contains("mit license") || t.Contains("permission is hereby granted, free of charge")) kind = "MIT License";
            else if (t.Contains("llama") && t.Contains("community license")) kind = "Llama community licence (has usage conditions)";
            else if (t.Contains("gemma") && t.Contains("terms of use")) kind = "Gemma terms of use (has usage conditions)";
            else if (t.Contains("creative commons")) kind = "Creative Commons";
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(3);
            var sum = string.Join(" ", lines);
            if (sum.Length > 360) sum = sum[..360] + "…";
            return (kind, sum);
        }

        /// <summary>Asks the server to unload a model from memory right away.</summary>
        public static async Task UnloadAsync(string model, CancellationToken ct = default)
        {
            using var r = await Http.PostAsync(BaseUrl + "/api/generate",
                new StringContent(JsonSerializer.Serialize(new { model, keep_alive = 0 }), Encoding.UTF8, "application/json"), ct);
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException(await ErrorText(r));
        }

        /// <summary>
        /// Pulls a model through the local server. The callback receives the server's status text and, when the
        /// server reports sizes for the current layer, a 0..1 fraction (otherwise -1).
        /// </summary>
        public static async Task PullAsync(string model, Action<string, double> progress, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/api/pull")
            { Content = new StringContent(JsonSerializer.Serialize(new { model, stream = true }), Encoding.UTF8, "application/json") };
            using var r = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException(await ErrorText(r));
            await using var s = await r.Content.ReadAsStreamAsync(ct);
            using var rd = new StreamReader(s);
            string line;
            while ((line = await rd.ReadLineAsync(ct)) != null)
            {
                if (line.Length == 0) continue;
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var err)) throw new InvalidOperationException(err.GetString());
                var status = root.TryGetProperty("status", out var st) ? st.GetString() : "";
                double frac = -1;
                if (root.TryGetProperty("total", out var t) && root.TryGetProperty("completed", out var c) && t.GetInt64() > 0)
                    frac = Math.Clamp((double)c.GetInt64() / t.GetInt64(), 0, 1);
                progress?.Invoke(status, frac);
            }
        }

        /// <summary>Streams a chat reply. Returns a short statistics line (tokens per second) when the server reports one.</summary>
        public static Task<string> ChatAsync(string model, IReadOnlyList<OllamaChatTurn> turns, Action<string> chunk, CancellationToken ct) =>
            ChatAsync(model, turns, null, chunk, ct);

        /// <summary>Streams a chat reply with a system prompt and per-chat parameters. Unset parameters are left out so the model's own defaults apply.</summary>
        public static async Task<string> ChatAsync(string model, IReadOnlyList<OllamaChatTurn> turns, OllamaChatOptions options, Action<string> chunk, CancellationToken ct)
        {
            var fallback = Settings.Current.OllamaKeepAlive == "0" ? "0" : (string.IsNullOrWhiteSpace(Settings.Current.OllamaKeepAlive) ? "5m" : Settings.Current.OllamaKeepAlive);
            var json = OllamaChatOptions.BuildChatBody(model, turns, options, fallback);
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/api/chat")
            { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            using var r = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException(await ErrorText(r));
            await using var s = await r.Content.ReadAsStreamAsync(ct);
            using var rd = new StreamReader(s);
            string line, stats = "";
            while ((line = await rd.ReadLineAsync(ct)) != null)
            {
                if (line.Length == 0) continue;
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var err)) throw new InvalidOperationException(err.GetString());
                if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var c)) chunk?.Invoke(c.GetString());
                if (root.TryGetProperty("done", out var d) && d.ValueKind == JsonValueKind.True
                    && root.TryGetProperty("eval_count", out var ec) && root.TryGetProperty("eval_duration", out var ed) && ed.GetInt64() > 0)
                    stats = $"{ec.GetInt64()} tokens · {ec.GetInt64() * 1e9 / ed.GetInt64():0.0} tokens/s";
            }
            return stats;
        }

        private static async Task<string> ErrorText(HttpResponseMessage r)
        {
            var text = await r.Content.ReadAsStringAsync();
            try { using var d = JsonDocument.Parse(text); if (d.RootElement.TryGetProperty("error", out var e)) return e.GetString(); } catch { }
            return $"HTTP {(int)r.StatusCode}: {text}";
        }

        public static string Bytes(long b) =>
            b >= 1L << 30 ? $"{b / 1073741824.0:0.0} GB" : b >= 1L << 20 ? $"{b / 1048576.0:0} MB" : $"{b / 1024.0:0} KB";
    }
}
