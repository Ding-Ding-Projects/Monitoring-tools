using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>What an extension may do. The app enforces these on what it hands over and on the answers
    /// it accepts; the extension is still a program running as you, so only add ones you trust.</summary>
    public static class ExtensionPermission
    {
        public const string ReadText = "read-text";          // receives copied text that matches its patterns
        public const string ReadAllText = "read-all-text";   // patterns may match any text
        public const string ChangeClipboard = "change-clipboard";
        public const string OpenLinks = "open-links";
        public const string Internet = "internet";           // declared so you can see it; the app cannot block it

        public static readonly string[] All = { ReadText, ReadAllText, ChangeClipboard, OpenLinks, Internet };

        public static string Describe(string p) => p switch
        {
            ReadText => "Read text you copy that matches its patterns",
            ReadAllText => "Read any text you copy",
            ChangeClipboard => "Put new text on the clipboard",
            OpenLinks => "Open web links",
            Internet => "Use the internet",
            _ => p,
        };
    }

    public sealed class ExtensionDetector
    {
        public string Kind, Label, Pattern;
        public Regex Rx;
    }

    public sealed class ExtensionAction
    {
        public string Id, Label;
        public string[] For = Array.Empty<string>();
        public bool Primary;
    }

    /// <summary>An extension folder's extension.json, checked strictly. Version 1 of the format:
    /// id, name, version, api (1), optional description and author, detect, actions, run, args, permissions.</summary>
    public sealed class ExtensionManifest
    {
        public const int ApiVersion = 1;
        public string Id, Name, Version, Description, Author, Run, Folder;
        public string[] Args = Array.Empty<string>();
        public int Api;
        public List<ExtensionDetector> Detect = new List<ExtensionDetector>();
        public List<ExtensionAction> Actions = new List<ExtensionAction>();
        public HashSet<string> Permissions = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The program to start, inside the extension folder only.</summary>
        public string RunPath => Run == null || Folder == null ? null : Path.GetFullPath(Path.Combine(Folder, Run));

        /// <summary>A short fingerprint of what it asks for, so changed permissions ask again.</summary>
        public string PermissionKey => Version + "|" + string.Join(",", Permissions.OrderBy(p => p, StringComparer.Ordinal));

        public static ExtensionManifest Parse(string json, string folder, out string error)
        {
            error = null;
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var r = doc.RootElement;
                var m = new ExtensionManifest { Folder = folder };
                m.Id = Str(r, "id"); m.Name = Str(r, "name"); m.Version = Str(r, "version") ?? "0.0.0";
                m.Description = Str(r, "description"); m.Author = Str(r, "author"); m.Run = Str(r, "run");
                m.Api = r.TryGetProperty("api", out var api) && api.TryGetInt32(out var a) ? a : 0;
                if (string.IsNullOrEmpty(m.Id) || !Regex.IsMatch(m.Id, @"^[a-z0-9][a-z0-9\-]{1,63}$")) { error = "\"id\" must be 2 to 64 lower-case letters, digits or dashes."; return null; }
                if (string.IsNullOrWhiteSpace(m.Name)) { error = "\"name\" is missing."; return null; }
                if (m.Api != ApiVersion) { error = "\"api\" must be " + ApiVersion + "."; return null; }
                if (string.IsNullOrEmpty(m.Run)) { error = "\"run\" must name the program to start."; return null; }
                if (folder != null)
                {
                    var full = m.RunPath;
                    var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { error = "\"run\" must be inside the extension folder."; return null; }
                }
                if (r.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array) m.Args = args.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
                if (r.TryGetProperty("permissions", out var perms) && perms.ValueKind == JsonValueKind.Array)
                    foreach (var p in perms.EnumerateArray())
                    {
                        var s = p.GetString();
                        if (!ExtensionPermission.All.Contains(s)) { error = "Unknown permission \"" + s + "\"."; return null; }
                        m.Permissions.Add(s);
                    }
                if (r.TryGetProperty("detect", out var det) && det.ValueKind == JsonValueKind.Array)
                    foreach (var d in det.EnumerateArray())
                    {
                        var x = new ExtensionDetector { Kind = Str(d, "kind"), Label = Str(d, "label"), Pattern = Str(d, "pattern") };
                        if (string.IsNullOrEmpty(x.Kind) || string.IsNullOrEmpty(x.Pattern)) { error = "Each \"detect\" entry needs \"kind\" and \"pattern\"."; return null; }
                        var opts = RegexOptions.CultureInvariant;
                        var flags = Str(d, "flags") ?? "";
                        if (flags.Contains('i')) opts |= RegexOptions.IgnoreCase;
                        if (flags.Contains('m')) opts |= RegexOptions.Multiline;
                        if (flags.Contains('s')) opts |= RegexOptions.Singleline;
                        try { x.Rx = new Regex(x.Pattern, opts, TimeSpan.FromMilliseconds(100)); }
                        catch (ArgumentException ex) { error = "Pattern for \"" + x.Kind + "\" is not valid: " + ex.Message; return null; }
                        if (!m.Permissions.Contains(ExtensionPermission.ReadAllText) && (x.Rx.IsMatch("") || x.Rx.IsMatch("any ordinary sentence you might copy")))
                        { error = "Pattern for \"" + x.Kind + "\" matches any text; add the \"read-all-text\" permission if that is meant."; return null; }
                        m.Detect.Add(x);
                    }
                if (m.Detect.Count > 0 && !m.Permissions.Contains(ExtensionPermission.ReadText) && !m.Permissions.Contains(ExtensionPermission.ReadAllText))
                { error = "Patterns need the \"read-text\" permission."; return null; }
                if (r.TryGetProperty("actions", out var acts) && acts.ValueKind == JsonValueKind.Array)
                    foreach (var aEl in acts.EnumerateArray())
                    {
                        var x = new ExtensionAction { Id = Str(aEl, "id"), Label = Str(aEl, "label"), Primary = aEl.TryGetProperty("primary", out var pr) && pr.ValueKind == JsonValueKind.True };
                        if (aEl.TryGetProperty("for", out var f) && f.ValueKind == JsonValueKind.Array) x.For = f.EnumerateArray().Select(e => e.GetString()).Where(e => e != null).ToArray();
                        if (string.IsNullOrEmpty(x.Id) || string.IsNullOrEmpty(x.Label)) { error = "Each action needs \"id\" and \"label\"."; return null; }
                        m.Actions.Add(x);
                    }
                if (m.Actions.Count == 0) { error = "Add at least one action."; return null; }
                return m;
            }
            catch (JsonException ex) { error = "extension.json is not valid JSON: " + ex.Message; return null; }
        }

        private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        /// <summary>Facts for every pattern that matches, each carrying this extension's buttons for that kind.</summary>
        public IEnumerable<SmartFact> Match(string text)
        {
            foreach (var d in Detect)
            {
                bool hit;
                try { hit = d.Rx.IsMatch(text); } catch (RegexMatchTimeoutException) { hit = false; }
                if (!hit) continue;
                var acts = Actions.Where(a => a.For.Length == 0 || a.For.Contains(d.Kind)).ToList();
                if (acts.Count == 0) continue;
                var f = new SmartFact("ext:" + Id + "/" + d.Kind, d.Label ?? Name, Name) { FromExtension = Id };
                f.Set("kind", d.Kind).Set("text", text);
                foreach (var a in acts) f.Act(a.Label, SmartDo.Run, "ext:" + Id + ":" + a.Id + ":" + d.Kind, primary: a.Primary, tip: "From the " + Name + " extension");
                yield return f;
            }
        }
    }
}
