using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>One documentation article, read from the Markdown files embedded in the program.</summary>
    public sealed class DocArticle
    {
        /// <summary>Stable id such as "commands/convert-media". A category index uses the category id, e.g. "commands".</summary>
        public string Id;
        public string Category;
        public string Title;
        /// <summary>First paragraph after the title.</summary>
        public string Summary;
        public string Markdown;
        /// <summary>Path of the file in the repository, relative to the repository root, for the optional source link.</summary>
        public string SourcePath;
        public bool IsIndex;
        public List<string> Headings = new List<string>();
        /// <summary>Everything searchable: title, headings and body.</summary>
        public string SearchText;
    }

    public sealed class DocCategory
    {
        public string Id;
        public string Title;
        public string Intro;
        public DocArticle Index;
        public List<DocArticle> Articles = new List<DocArticle>();
    }

    /// <summary>
    /// The offline documentation: Markdown articles embedded in the executable, grouped by category.
    /// Nothing is read from disk or the network.
    /// </summary>
    public static class Docs
    {
        public const string SourceBase = "https://github.com/Ding-Ding-Projects/Monitoring-tools/blob/main/";

        private static readonly Assembly Asm = typeof(Docs).Assembly;
        private static List<DocArticle> _all;
        private static List<DocCategory> _cats;

        public static IReadOnlyList<DocArticle> All { get { Load(); return _all; } }
        public static IReadOnlyList<DocCategory> Categories { get { Load(); return _cats; } }

        public static DocArticle Find(string id)
        {
            Load();
            return _all.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static DocCategory CategoryOf(DocArticle a) => Categories.FirstOrDefault(c => c.Id == a.Category);

        private static void Load()
        {
            if (_all != null) return;
            var all = new List<DocArticle>();
            foreach (var name in Asm.GetManifestResourceNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                var n = name.Replace('\\', '/');
                if (!n.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
                string md;
                try
                {
                    using var s = Asm.GetManifestResourceStream(name);
                    if (s == null) continue;
                    using var r = new StreamReader(s, Encoding.UTF8);
                    md = r.ReadToEnd().Replace("\r\n", "\n");
                }
                catch { continue; }

                string id, cat, source; bool index = false;
                if (n.Equals("docs/README.md", StringComparison.OrdinalIgnoreCase)) { id = "readme"; cat = ""; source = "README.md"; }
                else if (n.StartsWith("docs/features/", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = n.Substring("docs/features/".Length);
                    source = "docs/features/" + rel;
                    rel = rel.Substring(0, rel.Length - 3);
                    var parts = rel.Split('/');
                    cat = parts[0];
                    if (parts.Length == 2 && parts[1].Equals("README", StringComparison.OrdinalIgnoreCase)) { id = cat; index = true; }
                    else id = rel;
                }
                else continue;

                var a = new DocArticle { Id = id, Category = cat, Markdown = md, SourcePath = source, IsIndex = index };
                Parse(a);
                all.Add(a);
            }
            _all = all;

            var cats = new List<DocCategory>();
            foreach (var g in all.Where(a => a.Category.Length > 0).GroupBy(a => a.Category).OrderBy(g => CategoryOrder(g.Key)).ThenBy(g => g.Key))
            {
                var c = new DocCategory { Id = g.Key, Index = g.FirstOrDefault(a => a.IsIndex) };
                c.Title = c.Index?.Title ?? g.Key;
                c.Intro = c.Index?.Summary ?? "";
                c.Articles = g.Where(a => !a.IsIndex).OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase).ToList();
                cats.Add(c);
            }
            _cats = cats;
        }

        private static int CategoryOrder(string id)
        {
            int i = Array.IndexOf(new[] { "clipboard", "commands", "network", "game", "files", "minecraft", "search", "app" }, id);
            return i < 0 ? 99 : i;
        }

        private static void Parse(DocArticle a)
        {
            bool inCode = false; string title = null, summary = null;
            var sb = new StringBuilder();
            foreach (var line in a.Markdown.Split('\n'))
            {
                if (line.TrimStart().StartsWith("```")) { inCode = !inCode; continue; }
                sb.Append(line).Append('\n');
                if (inCode) continue;
                var h = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                if (h.Success)
                {
                    var text = StripInline(h.Groups[2].Value);
                    if (h.Groups[1].Length == 1 && title == null) title = text;
                    else a.Headings.Add(text);
                    continue;
                }
                if (summary == null && title != null && line.Trim().Length > 0 && !line.StartsWith("|") && !line.StartsWith("!") && !line.StartsWith("-") && !line.StartsWith(">"))
                    summary = StripInline(line.Trim());
            }
            a.Title = title ?? a.Id;
            a.Summary = summary ?? "";
            a.SearchText = a.Title + "\n" + string.Join("\n", a.Headings) + "\n" + StripInline(sb.ToString());
        }

        /// <summary>Plain text of a piece of inline Markdown: emphasis, code marks, links and images reduced to their words.</summary>
        public static string StripInline(string s)
        {
            s = Regex.Replace(s, @"!\[([^\]]*)\]\([^)]*\)", "$1");
            s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]*\)", "$1");
            return Regex.Replace(s, @"[`*~]|(?<!\w)_|_(?!\w)", "");
        }

        /// <summary>The anchor name GitHub-style: lowercase, spaces to dashes, punctuation dropped.</summary>
        public static string Slug(string heading)
        {
            var t = StripInline(heading).ToLowerInvariant().Trim();
            t = Regex.Replace(t, @"[^\p{L}\p{N}\s-]", "");
            return Regex.Replace(t, @"\s", "-");
        }

        /// <summary>
        /// Resolves a link found in an article. Returns false for links that leave the documentation
        /// (web addresses), so the caller can open them in the browser instead.
        /// </summary>
        public static bool TryResolve(DocArticle from, string href, out DocArticle target, out string anchor)
        {
            target = null; anchor = null;
            if (string.IsNullOrWhiteSpace(href) || Regex.IsMatch(href, @"^[a-zA-Z][a-zA-Z0-9+.-]*:")) return false;
            var hash = href.IndexOf('#');
            if (hash >= 0) { anchor = href.Substring(hash + 1); href = href.Substring(0, hash); }
            if (href.Length == 0) { target = from; return true; }

            string baseDir = from.IsIndex ? from.Category : from.Id.Contains('/') ? from.Id.Substring(0, from.Id.LastIndexOf('/')) : "";
            var stack = new List<string>();
            var rooted = href.StartsWith("docs/features/");
            if (rooted) href = href.Substring("docs/features/".Length);
            else if (from.Id == "readme") { }
            else if (baseDir.Length > 0) stack.AddRange(baseDir.Split('/'));
            foreach (var seg in href.Split('/'))
            {
                if (seg == "." || seg.Length == 0) continue;
                if (seg == "..") { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); }
                else stack.Add(seg);
            }
            var path = string.Join("/", stack);
            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 3);
            if (path.EndsWith("/README", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 7);
            target = Find(path);
            return true;
        }

        /// <summary>Bytes of an embedded screenshot referenced by an article, or null when it is not bundled.</summary>
        public static byte[] Image(string src)
        {
            try
            {
                var file = src.Replace('\\', '/');
                file = file.Substring(file.LastIndexOf('/') + 1);
                using var s = Asm.GetManifestResourceStream("docs/screenshots/" + file);
                if (s == null) return null;
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                return ms.ToArray();
            }
            catch { return null; }
        }
    }
}
