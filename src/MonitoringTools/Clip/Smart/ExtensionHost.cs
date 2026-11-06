using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Clip.Smart
{
    public sealed class LoadedExtension
    {
        public ExtensionManifest Manifest;
        public string Folder;
        /// <summary>Why the folder could not be used, in plain words, or null.</summary>
        public string Error;
        public bool Enabled;
        /// <summary>True when it is new or its permissions changed since you allowed it.</summary>
        public bool NeedsApproval;
        public List<string> Log = new List<string>();
        public string Name => Manifest?.Name ?? Path.GetFileName(Folder);
    }

    /// <summary>What an extension answered. Anything it was not allowed to do is dropped and noted in Error.</summary>
    public sealed class ExtensionResult
    {
        public string Copy, Open, Toast, Error;
        public List<SmartValue> Values = new List<SmartValue>();
        public long Ms;
        public bool Ok => Error == null;
    }

    /// <summary>Finds extensions in their folder, remembers which you turned on and what you allowed, matches copied
    /// text against their patterns and runs their programs with one line of JSON in and one line out.</summary>
    public sealed class ExtensionHost
    {
        public static readonly TimeSpan RunLimit = TimeSpan.FromSeconds(5);
        private const int MaxAnswer = 1 << 20;

        public string Root { get; }
        public List<LoadedExtension> Items { get; private set; } = new List<LoadedExtension>();
        private Dictionary<string, (bool Enabled, string Approved)> state = new Dictionary<string, (bool, string)>();

        public ExtensionHost(string root) { Root = root; }

        public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitoringTools", "Extensions");
        private string StatePath => Path.Combine(Root, "state.json");

        public void Load()
        {
            Directory.CreateDirectory(Root);
            ReadState();
            var list = new List<LoadedExtension>();
            foreach (var dir in Directory.GetDirectories(Root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var item = new LoadedExtension { Folder = dir };
                var file = Path.Combine(dir, "extension.json");
                if (!File.Exists(file)) { item.Error = "No extension.json in this folder."; list.Add(item); continue; }
                item.Manifest = ExtensionManifest.Parse(File.ReadAllText(file), dir, out var err);
                item.Error = err;
                if (item.Manifest != null)
                {
                    if (!File.Exists(item.Manifest.RunPath)) item.Error = "The program \"" + item.Manifest.Run + "\" is missing.";
                    else if (list.Any(x => x.Manifest?.Id == item.Manifest.Id)) item.Error = "Another folder already uses the id \"" + item.Manifest.Id + "\".";
                    state.TryGetValue(item.Manifest.Id, out var s);
                    item.NeedsApproval = s.Approved != item.Manifest.PermissionKey;
                    item.Enabled = item.Error == null && s.Enabled && !item.NeedsApproval;
                }
                list.Add(item);
            }
            Items = list;
        }

        public LoadedExtension Find(string id) => Items.FirstOrDefault(i => i.Manifest?.Id == id);

        /// <summary>Turns an extension on, recording that you allowed what it asks for now.</summary>
        public void Allow(string id)
        {
            var it = Find(id); if (it?.Manifest == null || it.Error != null) return;
            state[id] = (true, it.Manifest.PermissionKey); it.Enabled = true; it.NeedsApproval = false; WriteState();
        }

        public void TurnOff(string id)
        {
            var it = Find(id); if (it?.Manifest == null) return;
            state.TryGetValue(id, out var s); state[id] = (false, s.Approved); it.Enabled = false; WriteState();
        }

        /// <summary>Copies an extension folder into the extensions folder. Returns the manifest, or null with an error.
        /// An existing folder with the same id is only replaced when replace is true.</summary>
        public ExtensionManifest Install(string sourceFolder, bool replace, out string error)
        {
            error = null;
            var file = Path.Combine(sourceFolder, "extension.json");
            if (!File.Exists(file)) { error = "That folder has no extension.json."; return null; }
            var m = ExtensionManifest.Parse(File.ReadAllText(file), sourceFolder, out error);
            if (m == null) return null;
            var target = Path.Combine(Root, m.Id);
            if (string.Equals(Path.GetFullPath(sourceFolder).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) { Load(); return m; }
            if (Directory.Exists(target))
            {
                if (!replace) { error = "exists"; return m; }
                var old = target + ".old-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                Directory.Move(target, old);
                try { Copy(sourceFolder, target); Directory.Delete(old, true); }
                catch { if (Directory.Exists(target)) Directory.Delete(target, true); Directory.Move(old, target); throw; }
            }
            else Copy(sourceFolder, target);
            Load();
            return m;
        }

        private static void Copy(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
            foreach (var d in Directory.GetDirectories(from)) Copy(d, Path.Combine(to, Path.GetFileName(d)));
        }

        /// <summary>Facts from every turned-on extension whose patterns match.</summary>
        public IEnumerable<SmartFact> Detect(string text)
        {
            foreach (var it in Items.Where(i => i.Enabled && i.Manifest != null))
                foreach (var f in it.Manifest.Match(text)) yield return f;
        }

        /// <summary>Runs "ext:id:action:kind" on the copied text.</summary>
        public async Task<ExtensionResult> RunAsync(string runValue, string text, CancellationToken cancel = default)
        {
            var parts = runValue.Split(':');
            if (parts.Length != 4 || parts[0] != "ext") return new ExtensionResult { Error = "Not an extension action." };
            var it = Find(parts[1]);
            if (it == null || !it.Enabled) return new ExtensionResult { Error = "That extension is turned off." };
            var res = await RunProgramAsync(it.Manifest, parts[2], parts[3], text, cancel).ConfigureAwait(false);
            lock (it.Log)
            {
                it.Log.Insert(0, DateTime.Now.ToString("HH:mm") + "  " + parts[2] + " · " + (res.Ok ? res.Ms + " ms" : res.Error));
                if (it.Log.Count > 10) it.Log.RemoveAt(10);
            }
            return res;
        }

        public static async Task<ExtensionResult> RunProgramAsync(ExtensionManifest m, string action, string kind, string text, CancellationToken cancel = default)
        {
            var res = new ExtensionResult();
            var sw = Stopwatch.StartNew();
            var psi = new ProcessStartInfo(m.RunPath)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = m.Folder,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            };
            foreach (var a in m.Args) psi.ArgumentList.Add(a);
            psi.Environment["MONITORING_TOOLS_EXTENSION_API"] = ExtensionManifest.ApiVersion.ToString();
            Process p;
            try { p = Process.Start(psi); } catch (Exception ex) { res.Error = "Could not start it: " + ex.Message; return res; }
            using (p)
            {
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                limit.CancelAfter(RunLimit);
                try
                {
                    var request = JsonSerializer.Serialize(new Dictionary<string, object> { ["api"] = ExtensionManifest.ApiVersion, ["action"] = action, ["kind"] = kind, ["text"] = text, ["extension"] = m.Id });
                    await p.StandardInput.WriteLineAsync(request).ConfigureAwait(false);
                    p.StandardInput.Close();
                    _ = p.StandardError.ReadToEndAsync();
                    var line = await ReadLineLimited(p.StandardOutput, limit.Token).ConfigureAwait(false);
                    res.Ms = sw.ElapsedMilliseconds;
                    if (line == null) { res.Error = "It gave no answer."; return res; }
                    Accept(m, line, res);
                }
                catch (OperationCanceledException) { res.Error = "It took longer than " + (int)RunLimit.TotalSeconds + " seconds, so it was stopped."; }
                catch (Exception ex) { res.Error = "It failed: " + ex.Message; }
                finally { try { if (!p.HasExited) p.Kill(true); } catch { } }
                res.Ms = sw.ElapsedMilliseconds;
            }
            return res;
        }

        private static async Task<string> ReadLineLimited(StreamReader r, CancellationToken token)
        {
            var sb = new StringBuilder(); var buf = new char[4096];
            while (true)
            {
                int n = await r.ReadAsync(buf.AsMemory(), token).ConfigureAwait(false);
                if (n == 0) return sb.Length == 0 ? null : sb.ToString();
                for (int i = 0; i < n; i++)
                {
                    if (buf[i] == '\n') return sb.ToString().TrimEnd('\r');
                    sb.Append(buf[i]);
                    if (sb.Length > MaxAnswer) throw new InvalidDataException("The answer was larger than 1 MB.");
                }
            }
        }

        /// <summary>Keeps only what the extension is allowed to do.</summary>
        public static void Accept(ExtensionManifest m, string line, ExtensionResult res)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(line); } catch { res.Error = "Its answer was not JSON."; return; }
            using (doc)
            {
                var r = doc.RootElement;
                if (r.ValueKind != JsonValueKind.Object) { res.Error = "Its answer was not a JSON object."; return; }
                if (r.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String) { res.Error = e.GetString(); return; }
                if (r.TryGetProperty("copy", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    if (m.Permissions.Contains(ExtensionPermission.ChangeClipboard)) res.Copy = c.GetString();
                    else res.Error = "It tried to change the clipboard without permission.";
                }
                if (r.TryGetProperty("open", out var o) && o.ValueKind == JsonValueKind.String)
                {
                    var url = o.GetString();
                    if (!m.Permissions.Contains(ExtensionPermission.OpenLinks)) res.Error = "It tried to open a link without permission.";
                    else if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http")) res.Open = u.AbsoluteUri;
                    else res.Error = "It asked to open something that is not a web link.";
                }
                if (r.TryGetProperty("toast", out var t) && t.ValueKind == JsonValueKind.String) res.Toast = Clip(t.GetString(), 300);
                if (r.TryGetProperty("values", out var vs) && vs.ValueKind == JsonValueKind.Array)
                    foreach (var v in vs.EnumerateArray().Take(20))
                        if (v.TryGetProperty("label", out var l) && v.TryGetProperty("text", out var x) && l.ValueKind == JsonValueKind.String && x.ValueKind == JsonValueKind.String)
                            res.Values.Add(new SmartValue(Clip(l.GetString(), 60), Clip(x.GetString(), 4000)));
            }
        }

        private static string Clip(string s, int n) => s == null ? null : s.Length > n ? s.Substring(0, n) : s;

        private void ReadState()
        {
            state = new Dictionary<string, (bool, string)>();
            try
            {
                if (!File.Exists(StatePath)) return;
                using var d = JsonDocument.Parse(File.ReadAllText(StatePath));
                foreach (var p in d.RootElement.EnumerateObject())
                    state[p.Name] = (p.Value.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.True,
                                     p.Value.TryGetProperty("approved", out var ap) ? ap.GetString() : null);
            }
            catch { }
        }

        private void WriteState()
        {
            Directory.CreateDirectory(Root);
            var obj = state.ToDictionary(k => k.Key, k => new Dictionary<string, object> { ["enabled"] = k.Value.Enabled, ["approved"] = k.Value.Approved });
            var tmp = StatePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, StatePath, true);
        }
    }
}
