using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MonitoringTools.Core
{
    /// <summary>A file attached to one chat message: an image sent to a vision model, or a text file inlined into the message.</summary>
    public sealed class OllamaAttachment
    {
        public string Name { get; set; }
        /// <summary>"image" or "text".</summary>
        public string Kind { get; set; }
        /// <summary>Base64 of the original image bytes (no data: prefix), as /api/chat expects. Empty for text.</summary>
        public string Base64 { get; set; } = "";
        /// <summary>The text that gets inlined into the message. Empty for images.</summary>
        public string Text { get; set; } = "";
        public long Bytes { get; set; }
        public bool Truncated { get; set; }
    }

    /// <summary>Reads and encodes chat attachments. Pure code: no network and no UI.</summary>
    public static class OllamaAttachments
    {
        /// <summary>Largest image accepted, in bytes. Larger images are refused rather than silently shrunk.</summary>
        public const long MaxImageBytes = 12L * 1024 * 1024;
        /// <summary>Largest text file read, in bytes.</summary>
        public const long MaxTextFileBytes = 1024L * 1024;
        /// <summary>Most characters of one text file that are inlined; the rest is cut and the message says so.</summary>
        public const int MaxInlineChars = 24000;
        /// <summary>Most characters of attached text allowed in one message in total.</summary>
        public const int MaxInlineTotalChars = 60000;

        /// <summary>The image format named by the file's first bytes, or null when the bytes are not a known image.</summary>
        public static string SniffImage(byte[] b)
        {
            if (b == null || b.Length < 12) return null;
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "png";
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpeg";
            if (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38) return "gif";
            if (b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "webp";
            if (b[0] == 0x42 && b[1] == 0x4D) return "bmp";
            return null;
        }

        /// <summary>True when the bytes contain a NUL in the first 4096 bytes, which plain text files do not.</summary>
        public static bool LooksBinary(byte[] b)
        {
            int n = Math.Min(b.Length, 4096);
            for (int i = 0; i < n; i++) if (b[i] == 0) return true;
            return false;
        }

        public static OllamaAttachment FromImageBytes(string name, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new InvalidOperationException(name + " is empty.");
            if (bytes.Length > MaxImageBytes) throw new InvalidOperationException($"{name} is {bytes.Length / 1048576.0:0.0} MB; images above {MaxImageBytes / 1048576} MB are not sent.");
            if (SniffImage(bytes) == null) throw new InvalidOperationException(name + " does not start like a PNG, JPEG, GIF, WebP or BMP image.");
            return new OllamaAttachment { Name = name, Kind = "image", Base64 = Convert.ToBase64String(bytes), Bytes = bytes.Length };
        }

        public static OllamaAttachment FromTextBytes(string name, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new InvalidOperationException(name + " is empty.");
            if (LooksBinary(bytes)) throw new InvalidOperationException(name + " looks like a binary file, not text, so it is not attached.");
            var text = DecodeText(bytes);
            bool cut = text.Length > MaxInlineChars;
            if (cut) text = text[..MaxInlineChars];
            return new OllamaAttachment { Name = name, Kind = "text", Text = text, Bytes = bytes.Length, Truncated = cut };
        }

        private static string DecodeText(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException) { return Encoding.Latin1.GetString(b); }
        }

        /// <summary>Reads a file from disk: an image when its bytes say so, otherwise text. Throws InvalidOperationException with a plain message when it cannot be attached.</summary>
        public static OllamaAttachment Load(string path)
        {
            var name = Path.GetFileName(path);
            FileInfo fi;
            try { fi = new FileInfo(path); } catch (Exception ex) { throw new InvalidOperationException("Could not read " + name + ": " + ex.Message); }
            if (!fi.Exists) throw new InvalidOperationException(name + " does not exist.");
            if (fi.Length > Math.Max(MaxImageBytes, MaxTextFileBytes)) throw new InvalidOperationException($"{name} is {fi.Length / 1048576.0:0.0} MB, which is more than can be attached.");
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); } catch (Exception ex) { throw new InvalidOperationException("Could not read " + name + ": " + ex.Message); }
            if (SniffImage(bytes) != null) return FromImageBytes(name, bytes);
            if (bytes.Length > MaxTextFileBytes) throw new InvalidOperationException($"{name} is larger than {MaxTextFileBytes / 1024} KB, the most a text file may be.");
            return FromTextBytes(name, bytes);
        }

        /// <summary>The message content to send: the typed text followed by each text attachment in a labelled fenced block. Images travel separately.</summary>
        public static string BuildContent(string typed, IEnumerable<OllamaAttachment> attachments)
        {
            var sb = new StringBuilder(typed ?? "");
            int budget = MaxInlineTotalChars;
            foreach (var a in (attachments ?? Enumerable.Empty<OllamaAttachment>()).Where(x => x.Kind == "text"))
            {
                var t = a.Text ?? "";
                bool cut = a.Truncated;
                if (t.Length > budget) { t = t[..Math.Max(0, budget)]; cut = true; }
                budget -= t.Length;
                var fence = new string('`', Math.Max(3, LongestBacktickRun(t) + 1));
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("Attached file ").Append(a.Name).Append(":\n").Append(fence).Append('\n').Append(t);
                if (!t.EndsWith("\n")) sb.Append('\n');
                sb.Append(fence);
                if (cut) sb.Append("\n(The file was longer; only the first part is included.)");
            }
            return sb.ToString();
        }

        private static int LongestBacktickRun(string s)
        {
            int best = 0, run = 0;
            foreach (var c in s) { run = c == '`' ? run + 1 : 0; if (run > best) best = run; }
            return best;
        }
    }

    /// <summary>Settings sent with a chat request. Null or empty fields are left out so the model's own default applies.</summary>
    public sealed class OllamaChatOptions
    {
        public string SystemPrompt { get; set; } = "";
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public int? NumCtx { get; set; }
        public string KeepAlive { get; set; } = "";

        public const int MinContext = 128;
        public const int MaxContext = 1048576;

        /// <summary>Plain-language problems with the values; empty when they are acceptable. modelContext is the model's stated limit, or 0 when unknown.</summary>
        public List<string> Validate(long modelContext = 0)
        {
            var bad = new List<string>();
            if (Temperature is double t && (double.IsNaN(t) || t < 0 || t > 2)) bad.Add("Temperature must be between 0 and 2.");
            if (TopP is double p && (double.IsNaN(p) || p <= 0 || p > 1)) bad.Add("Top-p must be above 0 and at most 1.");
            if (NumCtx is int c)
            {
                if (c < MinContext || c > MaxContext) bad.Add($"Context length must be between {MinContext} and {MaxContext} tokens.");
                else if (modelContext > 0 && c > modelContext) bad.Add($"Context length {c} is above this model's stated limit of {modelContext} tokens.");
            }
            if (!string.IsNullOrWhiteSpace(KeepAlive) && !IsKeepAlive(KeepAlive.Trim())) bad.Add("Keep-alive must be a duration such as 5m, 1h or 30s, or 0 to unload at once.");
            return bad;
        }

        public static bool IsKeepAlive(string s) =>
            s == "0" || System.Text.RegularExpressions.Regex.IsMatch(s, @"^[1-9][0-9]{0,5}(s|m|h)$");

        public static bool TryParseDouble(string text, out double? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ||
                double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out d)) { value = d; return true; }
            return false;
        }

        public static bool TryParseInt(string text, out int? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (int.TryParse(text.Trim(), out var i)) { value = i; return true; }
            return false;
        }

        /// <summary>The JSON body for POST /api/chat. Images ride on their message in the "images" array as the API documents.</summary>
        public static string BuildChatBody(string model, IReadOnlyList<OllamaChatTurn> turns, OllamaChatOptions opt, string fallbackKeepAlive)
        {
            opt ??= new OllamaChatOptions();
            var messages = new JsonArray();
            if (!string.IsNullOrWhiteSpace(opt.SystemPrompt))
                messages.Add(new JsonObject { ["role"] = "system", ["content"] = opt.SystemPrompt });
            foreach (var t in turns)
            {
                var m = new JsonObject { ["role"] = t.Role, ["content"] = t.Content ?? "" };
                if (t.Images != null && t.Images.Count > 0)
                {
                    var arr = new JsonArray();
                    foreach (var i in t.Images) arr.Add(i);
                    m["images"] = arr;
                }
                messages.Add(m);
            }
            var body = new JsonObject { ["model"] = model, ["messages"] = messages, ["stream"] = true };
            var ka = string.IsNullOrWhiteSpace(opt.KeepAlive) ? fallbackKeepAlive : opt.KeepAlive.Trim();
            if (ka == "0") body["keep_alive"] = 0; else if (!string.IsNullOrWhiteSpace(ka)) body["keep_alive"] = ka;
            var o = new JsonObject();
            if (opt.Temperature is double t1) o["temperature"] = t1;
            if (opt.TopP is double p1) o["top_p"] = p1;
            if (opt.NumCtx is int c1) o["num_ctx"] = c1;
            if (o.Count > 0) body["options"] = o;
            return body.ToJsonString();
        }
    }

    /// <summary>What the server says a model can do, from the capabilities list of /api/show. Nothing here is guessed from the model's name.</summary>
    public sealed class OllamaCapabilityInfo
    {
        /// <summary>False when the server's reply carried no capabilities list at all (an older server).</summary>
        public bool Reported;
        public bool Vision, Tools, Embedding, Completion, Thinking;
        public List<string> All = new List<string>();

        public static OllamaCapabilityInfo From(IEnumerable<string> caps)
        {
            var list = (caps ?? Enumerable.Empty<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim().ToLowerInvariant()).Distinct().ToList();
            return new OllamaCapabilityInfo
            {
                Reported = list.Count > 0, All = list,
                Vision = list.Contains("vision"), Tools = list.Contains("tools"), Embedding = list.Contains("embedding"),
                Completion = list.Contains("completion"), Thinking = list.Contains("thinking")
            };
        }

        /// <summary>Null when images may be attached; otherwise the reason they must not be.</summary>
        public string ImageBlockReason() =>
            !Reported ? null : Vision ? null : "This model does not report the vision capability, so an image would be ignored or rejected.";

        /// <summary>Null when chat is possible; otherwise why not.</summary>
        public string ChatBlockReason() =>
            Reported && Embedding && !Completion ? "This model reports only the embedding capability; it produces vectors, not chat replies." : null;
    }

    // ================================================================ saved chats

    public sealed class OllamaSessionMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        /// <summary>Names of attached files. Image bytes are not stored in a saved chat; text attachments are already part of Content.</summary>
        public List<string> Attachments { get; set; } = new List<string>();
    }

    public sealed class OllamaSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string Title { get; set; } = "";
        public string Model { get; set; } = "";
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Updated { get; set; } = DateTime.Now;
        public string Profile { get; set; } = "";
        public string SystemPrompt { get; set; } = "";
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public int? NumCtx { get; set; }
        public string KeepAlive { get; set; } = "";
        public List<OllamaSessionMessage> Messages { get; set; } = new List<OllamaSessionMessage>();

        public string AutoTitle()
        {
            var first = Messages.FirstOrDefault(m => m.Role == "user")?.Content ?? "";
            first = string.Join(" ", first.Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            if (first.Length == 0) return "Chat " + Created.ToString("yyyy-MM-dd HH:mm");
            return first.Length > 48 ? first[..48] + "…" : first;
        }
    }

    /// <summary>Saved chats as one JSON file per chat under the app data folder. Everything stays on this computer.</summary>
    public static class OllamaSessionStore
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true };
        public static string Dir { get { var d = Path.Combine(Settings.DataDir, "ollama-chats"); Directory.CreateDirectory(d); return d; } }

        private static string PathFor(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsLetterOrDigit(c))) throw new ArgumentException("Invalid chat id.");
            return Path.Combine(Dir, id + ".json");
        }

        public static void Save(OllamaSession s)
        {
            s.Updated = DateTime.Now;
            if (string.IsNullOrWhiteSpace(s.Title)) s.Title = s.AutoTitle();
            var path = PathFor(s.Id);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(s, Json));
            File.Move(path + ".tmp", path, true);
        }

        public static List<OllamaSession> List()
        {
            var list = new List<OllamaSession>();
            foreach (var f in Directory.GetFiles(Dir, "*.json"))
            {
                try { var s = JsonSerializer.Deserialize<OllamaSession>(File.ReadAllText(f)); if (s != null && !string.IsNullOrEmpty(s.Id)) list.Add(s); }
                catch { /* a damaged file is skipped, never deleted */ }
            }
            return list.OrderByDescending(s => s.Updated).ToList();
        }

        public static bool Delete(string id)
        {
            var p = PathFor(id);
            if (!File.Exists(p)) return false;
            File.Delete(p);
            return true;
        }

        /// <summary>A readable Markdown transcript of a saved chat.</summary>
        public static string ToMarkdown(OllamaSession s)
        {
            var sb = new StringBuilder();
            sb.Append("# ").AppendLine(string.IsNullOrWhiteSpace(s.Title) ? s.AutoTitle() : s.Title);
            sb.AppendLine();
            sb.AppendLine($"- Model: {(string.IsNullOrWhiteSpace(s.Model) ? "unknown" : s.Model)}");
            sb.AppendLine($"- Started: {s.Created:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- Last saved: {s.Updated:yyyy-MM-dd HH:mm}");
            if (!string.IsNullOrWhiteSpace(s.Profile)) sb.AppendLine($"- Profile: {s.Profile}");
            var opts = new List<string>();
            if (s.Temperature != null) opts.Add("temperature " + s.Temperature.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (s.TopP != null) opts.Add("top-p " + s.TopP.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (s.NumCtx != null) opts.Add("context " + s.NumCtx + " tokens");
            if (!string.IsNullOrWhiteSpace(s.KeepAlive)) opts.Add("keep-alive " + s.KeepAlive);
            if (opts.Count > 0) sb.AppendLine("- Settings: " + string.Join(", ", opts));
            if (!string.IsNullOrWhiteSpace(s.SystemPrompt))
            {
                sb.AppendLine().AppendLine("## System prompt").AppendLine();
                foreach (var line in s.SystemPrompt.Replace("\r", "").Split('\n')) sb.Append("> ").AppendLine(line);
            }
            foreach (var m in s.Messages)
            {
                sb.AppendLine().AppendLine(m.Role == "user" ? "## You" : m.Role == "assistant" ? "## " + (string.IsNullOrWhiteSpace(s.Model) ? "Model" : s.Model) : "## " + m.Role).AppendLine();
                if (m.Attachments != null && m.Attachments.Count > 0) sb.AppendLine("_Attached: " + string.Join(", ", m.Attachments) + "_").AppendLine();
                sb.AppendLine(m.Content);
            }
            return sb.ToString();
        }
    }

    // ================================================================ profiles (system prompt presets)

    /// <summary>A named, editable chat profile: a system prompt and optional default parameters. Tool-calling definitions are not part of a profile.</summary>
    public sealed class OllamaProfile
    {
        public string Name { get; set; } = "";
        public string SystemPrompt { get; set; } = "";
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public int? NumCtx { get; set; }
    }

    public static class OllamaProfiles
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true };
        private static string FilePath => Path.Combine(Settings.DataDir, "ollama-profiles.json");

        /// <summary>Starter profiles shown until the user saves their own list. They are ordinary editable entries, not hidden behaviour.</summary>
        public static List<OllamaProfile> Starters() => new List<OllamaProfile>
        {
            new OllamaProfile { Name = "Plain", SystemPrompt = "" },
            new OllamaProfile { Name = "Concise", SystemPrompt = "Answer briefly and directly. Use short sentences and no filler.", Temperature = 0.3 },
            new OllamaProfile { Name = "Careful reviewer", SystemPrompt = "You review text and code carefully. List concrete problems first, then suggestions. If you are unsure, say so instead of guessing.", Temperature = 0.2 },
        };

        public static List<OllamaProfile> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var list = JsonSerializer.Deserialize<List<OllamaProfile>>(File.ReadAllText(FilePath));
                    if (list != null) return list.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
                }
            }
            catch { /* fall back to the starters; the damaged file is left alone */ }
            return Starters();
        }

        public static void Save(List<OllamaProfile> list)
        {
            File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(list, Json));
            File.Move(FilePath + ".tmp", FilePath, true);
        }

        /// <summary>Builds a profile from the text of a form. Returns a problem text, or null with the profile filled in.</summary>
        public static string TryBuild(string name, string prompt, string temperature, string topP, string numCtx, out OllamaProfile profile)
        {
            profile = null;
            if (!OllamaChatOptions.TryParseDouble(temperature, out var t)) return "Temperature is not a number.";
            if (!OllamaChatOptions.TryParseDouble(topP, out var p)) return "Top-p is not a number.";
            if (!OllamaChatOptions.TryParseInt(numCtx, out var c)) return "Context length must be a whole number.";
            var made = new OllamaProfile { Name = (name ?? "").Trim(), SystemPrompt = (prompt ?? "").Trim(), Temperature = t, TopP = p, NumCtx = c };
            if (made.Name.Length == 0) return "Give the profile a name first.";
            if (made.Name.Length > 40) return "Profile names are limited to 40 characters.";
            var bad = new OllamaChatOptions { Temperature = t, TopP = p, NumCtx = c }.Validate();
            if (bad.Count > 0) return bad[0];
            profile = made;
            return null;
        }

        /// <summary>Adds the profile or replaces the one with the same name (case-insensitive). Returns a problem text, or null.</summary>
        public static string Upsert(List<OllamaProfile> list, OllamaProfile p)
        {
            var name = (p.Name ?? "").Trim();
            if (name.Length == 0) return "Give the profile a name first.";
            if (name.Length > 40) return "Profile names are limited to 40 characters.";
            var bad = new OllamaChatOptions { Temperature = p.Temperature, TopP = p.TopP, NumCtx = p.NumCtx }.Validate();
            if (bad.Count > 0) return bad[0];
            p.Name = name;
            var i = list.FindIndex(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) list[i] = p; else list.Add(p);
            return null;
        }
    }
}
