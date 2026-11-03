using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    public sealed class ToastAction
    {
        public string Label;
        public string Icon;
        public bool Primary;
        public bool Chip;          // monospace ":mp4" style quick command
        public string Tip;
        public Action Run;
        public bool HandsOff;      // the action opens its own toast, so this one closes right away
        public string Done = "Done";
    }

    public static class ActionProvider
    {
        public static List<ToastAction> For(ClipItem item)
        {
            var list = new List<ToastAction>();
            switch (item.Kind)
            {
                case ClipKind.Url: Url(item.Text, list); break;
                case ClipKind.Email: Email(item.Text, list); break;
                case ClipKind.Color: Color(item.Text, list); break;
                case ClipKind.Json: Json(item.Text, list); break;
                case ClipKind.Markdown: MarkdownActions(item.Text, list); TextActions(item.Text, list); break;
                case ClipKind.Files: Files(item, list); break;
                case ClipKind.Image: Bitmap(item, list); break;
                case ClipKind.Audio: AudioClip(item, list); break;
                default: TextActions(item.Text, list); break;
            }
            return list;
        }

        // -------------------------------------------------------------- text

        private static void TextActions(string text, List<ToastAction> l)
        {
            var t = text ?? "";
            l.Add(A("Search web", Icons.Globe, () => Markdown.Open("https://www.bing.com/search?q=" + Uri.EscapeDataString(t.Trim().Length > 300 ? t.Trim()[..300] : t.Trim()))));
            if (t != t.Trim() || Regex.IsMatch(t, @"[ \t]{2,}"))
                l.Add(A("Tidy spaces", Icons.Lines, () => Set(Regex.Replace(t.Trim(), @"[ \t]{2,}", " ")), done: "Tidied"));
            l.Add(A("UPPER", null, () => Set(t.ToUpperInvariant()), done: "Copied"));
            l.Add(A("lower", null, () => Set(t.ToLowerInvariant()), done: "Copied"));
            l.Add(A("Title Case", null, () => Set(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(t.ToLower())), done: "Copied"));
            if (t.Contains('\n'))
                l.Add(A("Join lines", null, () => Set(Regex.Replace(t, @"\s*\r?\n\s*", " ").Trim()), done: "Joined"));
            l.Add(A("Translate", null, () => Markdown.Open("https://translate.google.com/?sl=auto&tl=en&op=translate&text=" + Uri.EscapeDataString(t.Length > 2000 ? t[..2000] : t))));
            l.Add(A("Save as .txt", Icons.File, () => Reveal(SaveText(t, ".txt")), done: "Saved"));
        }

        private static void MarkdownActions(string md, List<ToastAction> l)
        {
            l.Add(A("Copy as plain text", Icons.Text, () => Set(StripMarkdown(md)), primary: true, done: "Copied"));
            l.Add(A("Save as .md", Icons.File, () => Reveal(SaveText(md, ".md")), done: "Saved"));
        }

        public static string StripMarkdown(string md)
        {
            var s = Regex.Replace(md, @"^\s{0,3}#{1,6}\s+", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"```[a-zA-Z0-9]*\n?", "");
            s = Regex.Replace(s, @"\*\*([^*]+)\*\*|__([^_]+)__", "$1$2");
            s = Regex.Replace(s, @"(?<!\*)\*([^*\s][^*]*)\*(?!\*)", "$1");
            s = Regex.Replace(s, @"~~([^~]+)~~", "$1");
            s = Regex.Replace(s, @"`([^`]+)`", "$1");
            s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", "$1 ($2)");
            s = Regex.Replace(s, @"^\s*>\s?", "", RegexOptions.Multiline);
            return s.Trim();
        }

        // -------------------------------------------------------------- urls, mail, colors, json

        private static void Url(string url, List<ToastAction> l)
        {
            var full = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + url : url;
            l.Add(A("Open", Icons.Link, () => Markdown.Open(full), primary: true, done: "Opened"));
            l.Add(A("Open private", null, () => OpenPrivate(full), done: "Opened"));
            if (Uri.TryCreate(full, UriKind.Absolute, out var u))
            {
                l.Add(A("Copy domain", null, () => Set(u.Host), done: "Copied"));
                var clean = StripTracking(u);
                if (clean != full) l.Add(A("Strip tracking", null, () => Set(clean), done: "Cleaned"));
                l.Add(A("Markdown link", null, () => Set($"[{u.Host}{(u.AbsolutePath.Length > 1 ? u.AbsolutePath.TrimEnd('/') : "")}]({full})"), done: "Copied"));
            }
        }

        private static readonly string[] Trackers = { "utm_", "fbclid", "gclid", "dclid", "msclkid", "mc_eid", "mc_cid", "igshid", "si", "ref_src", "_hsenc", "_hsmi", "yclid", "spm" };

        public static string StripTracking(Uri u)
        {
            if (string.IsNullOrEmpty(u.Query)) return u.OriginalString;
            var q = HttpUtility.ParseQueryString(u.Query);
            var keep = new List<string>();
            foreach (string k in q.AllKeys)
            {
                if (k == null) continue;
                if (Trackers.Any(t => t.EndsWith("_") ? k.StartsWith(t, StringComparison.OrdinalIgnoreCase) : k.Equals(t, StringComparison.OrdinalIgnoreCase))) continue;
                keep.Add(Uri.EscapeDataString(k) + "=" + Uri.EscapeDataString(q[k] ?? ""));
            }
            var b = new UriBuilder(u) { Query = string.Join("&", keep) };
            var s = b.Uri.AbsoluteUri;
            if (keep.Count == 0) s = s.TrimEnd('?');
            return s;
        }

        private static void OpenPrivate(string url)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var candidates = new (string exe, string flag)[]
            {
                (Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"), "--incognito"),
                (Path.Combine(local, @"Google\Chrome\Application\chrome.exe"), "--incognito"),
                (Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"), "--inprivate"),
                (Path.Combine(pf, @"Mozilla Firefox\firefox.exe"), "-private-window"),
                (Path.Combine(local, @"BraveSoftware\Brave-Browser\Application\brave.exe"), "--incognito"),
            };
            foreach (var (exe, flag) in candidates)
                if (File.Exists(exe)) { Process.Start(new ProcessStartInfo(exe, $"{flag} \"{url}\"") { UseShellExecute = false }); return; }
            Markdown.Open(url);
        }

        private static void Email(string mail, List<ToastAction> l)
        {
            l.Add(A("Compose", Icons.Mail, () => Markdown.Open("mailto:" + mail), primary: true, done: "Opened"));
            l.Add(A("Copy domain", null, () => Set(mail.Split('@')[1]), done: "Copied"));
        }

        public static System.Windows.Media.Color? ParseColor(string s)
        {
            try
            {
                s = s.Trim();
                var rgb = Regex.Match(s, @"rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})");
                if (rgb.Success) return System.Windows.Media.Color.FromRgb(byte.Parse(rgb.Groups[1].Value), byte.Parse(rgb.Groups[2].Value), byte.Parse(rgb.Groups[3].Value));
                var h = s.TrimStart('#');
                if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
                if (h.Length == 8) h = h.Substring(0, 6);
                if (h.Length != 6) return null;
                return System.Windows.Media.Color.FromRgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h.Substring(4, 2), 16));
            }
            catch { return null; }
        }

        private static void Color(string s, List<ToastAction> l)
        {
            var c = ParseColor(s);
            if (c == null) return;
            var v = c.Value;
            l.Add(A($"#{v.R:X2}{v.G:X2}{v.B:X2}", null, () => Set($"#{v.R:X2}{v.G:X2}{v.B:X2}"), chip: true, done: "Copied"));
            l.Add(A($"rgb({v.R}, {v.G}, {v.B})", null, () => Set($"rgb({v.R}, {v.G}, {v.B})"), chip: true, done: "Copied"));
            var (hh, ss, ll) = ToHsl(v);
            l.Add(A($"hsl({hh}, {ss}%, {ll}%)", null, () => Set($"hsl({hh}, {ss}%, {ll}%)"), chip: true, done: "Copied"));
        }

        private static (int, int, int) ToHsl(System.Windows.Media.Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double h = 0, s, lum = (max + min) / 2;
            if (max == min) s = 0;
            else
            {
                double d = max - min;
                s = lum > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
            }
            return ((int)Math.Round(h), (int)Math.Round(s * 100), (int)Math.Round(lum * 100));
        }

        private static void Json(string s, List<ToastAction> l)
        {
            l.Add(A("Pretty print", Icons.Braces, () =>
            {
                using var doc = JsonDocument.Parse(s);
                Set(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            }, primary: true, done: "Formatted"));
            l.Add(A("Minify", null, () =>
            {
                using var doc = JsonDocument.Parse(s);
                Set(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            }, done: "Minified"));
            l.Add(A("Save as .json", Icons.File, () => Reveal(SaveText(s, ".json")), done: "Saved"));
        }

        // -------------------------------------------------------------- files

        private static void Files(ClipItem item, List<ToastAction> l)
        {
            var f = item.FirstFile;
            var kind = item.FirstFileKind;
            bool many = item.Files.Length > 1;

            // Quote fixes come first: they are the most common reason to paste a path somewhere else.
            if (item.IsPathText && item.HadQuotes)
                l.Add(A("Remove quotes", null, () => Set(string.Join(Environment.NewLine, item.Files)), primary: true, done: "Unquoted"));
            else
                l.Add(A(item.IsPathText ? "Add quotes" : "Copy as path", null, () => Set(string.Join(Environment.NewLine, item.Files.Select(p => $"\"{p}\""))), done: "Copied"));

            if (!many)
            {
                if (kind == FileKind.Folder)
                {
                    l.Add(A("Open", Icons.Folder, () => Shell(f), primary: !item.HadQuotes, done: "Opened"));
                    l.Add(A("Terminal here", Icons.Code, () => Terminal(f), done: "Opened"));
                }
                else
                {
                    l.Add(A("Open", Icons.Play, () => Shell(f), primary: !item.HadQuotes, done: "Opened"));
                    l.Add(A("Show in folder", Icons.Folder, () => Reveal(f), done: "Shown"));
                }
                if (!item.IsPathText) l.Add(A("Copy path text", null, () => Set(f), done: "Copied"));
                l.Add(A("Copy name", null, () => Set(Path.GetFileName(f.TrimEnd('\\'))), done: "Copied"));
                if (f.Contains('\\')) l.Add(A("Forward slashes", null, () => Set(f.Replace('\\', '/')), done: "Copied"));
                if (item.IsPathText) l.Add(A("Copy as file", Icons.Clipboard, () => ClipboardWatcher.SetFiles(f), done: "Copied"));

                // Docker files explain what each button will do before you press it
                foreach (var d in DockerActions.For(f))
                {
                    var da = d;
                    l.Add(new ToastAction
                    {
                        Label = da.Label, Icon = Icons.Box, Primary = false, Tip = da.Explain, HandsOff = !da.InTerminal,
                        Run = () =>
                        {
                            if (da.InTerminal) { DockerActions.OpenInTerminal(f, da); return; }
                            App.Toasts.ShowTask("docker " + da.Label.ToLowerInvariant(), Path.GetFileName(f), async () =>
                            {
                                var (code, output) = await DockerActions.RunAsync(f, da);
                                return (code == 0, code == 0 ? da.Label + " finished" : da.Label + " failed", Tail(output));
                            });
                        }
                    });
                }

                // A repository folder (or a file in one) can be pushed
                if (GitPush.FindRepoRoot(f) != null)
                    l.Add(new ToastAction { Label = ":push", Chip = true, Tip = "Pre-dew everything and push. Same as copying the path with :push on the end.", HandsOff = true, Run = () => App.Toasts.StartPush(f) });

                // Media files: quick command chips, same as typing ":mp4" after the path
                foreach (var fmt in Formats.SuggestedFor(kind, Path.GetExtension(f)))
                {
                    var id = fmt;
                    l.Add(new ToastAction { Label = ":" + id, Chip = true, Tip = $"Convert to {Formats.Find(id)?.Label}. Or copy the path with :{id} on the end.", HandsOff = true, Run = () => Converter.Start(f, id) });
                }

                foreach (var s in Settings.Current.Shortcuts)
                {
                    var sc = s;
                    l.Add(new ToastAction { Label = ":" + sc.Name, Chip = true, Tip = string.IsNullOrWhiteSpace(sc.Description) ? "Your shortcut" : sc.Description, HandsOff = true, Run = () => App.Toasts.RunShortcut(sc, f) });
                }
            }
            else
            {
                l.Add(A("Show in folder", Icons.Folder, () => Reveal(f), done: "Shown"));
                l.Add(A("Copy names", null, () => Set(string.Join(Environment.NewLine, item.Files.Select(p => Path.GetFileName(p.TrimEnd('\\'))))), done: "Copied"));
            }
        }

        // -------------------------------------------------------------- bitmaps and audio from the clipboard

        private static void Bitmap(ClipItem item, List<ToastAction> l)
        {
            var img = item.Image;
            if (img == null) return;
            l.Add(A("Save PNG", Icons.Image, () => Reveal(Converter.SaveBitmap(img, "png")), primary: true, done: "Saved"));
            l.Add(A("Save JPG", null, () => Reveal(Converter.SaveBitmap(img, "jpg")), done: "Saved"));
            l.Add(A("Save + copy as file", Icons.Clipboard, () => { var p = Converter.SaveBitmap(img, "png"); ClipboardWatcher.SetFiles(p); }, done: "Copied"));
            l.Add(A("Open in Paint", null, () =>
            {
                var p = Converter.SaveBitmap(img, "png");
                Process.Start(new ProcessStartInfo("mspaint.exe", $"\"{p}\"") { UseShellExecute = true });
            }, done: "Opened"));
            foreach (var id in new[] { "webp", "avif", "ico", "bmp" })
            {
                var f = id;
                l.Add(new ToastAction { Label = ":" + f, Chip = true, HandsOff = true, Tip = $"Save as {Formats.Find(f)?.Label} in your media folder", Run = () => { var p = Converter.SaveBitmap(img, f); if (p != null) Reveal(p); } });
            }
        }

        private static void AudioClip(ClipItem item, List<ToastAction> l)
        {
            var wav = item.Audio;
            if (wav == null) return;
            l.Add(A("Save WAV", Icons.Audio, () => Reveal(Converter.SaveAudio(wav, "wav")), primary: true, done: "Saved"));
            foreach (var id in new[] { "mp3", "flac", "m4a", "ogg" })
            {
                var f = id;
                l.Add(new ToastAction { Label = ":" + f, Chip = true, HandsOff = true, Tip = $"Save as {Formats.Find(f)?.Label}", Run = () => Converter.SaveAudio(wav, f) });
            }
        }

        // -------------------------------------------------------------- helpers

        private static ToastAction A(string label, string icon, Action run, bool primary = false, bool chip = false, string done = "Done") =>
            new ToastAction { Label = label, Icon = icon, Run = run, Primary = primary, Chip = chip, Done = done };

        private static void Set(string text) => ClipboardWatcher.SetText(text);

        public static void Shell(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { Log.Warn(ex.Message); }
        }

        public static void Reveal(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false }); } catch { }
        }

        private static void Terminal(string dir)
        {
            try { Process.Start(new ProcessStartInfo("wt.exe", $"-d \"{dir}\"") { UseShellExecute = true }); }
            catch { Process.Start(new ProcessStartInfo("cmd.exe") { WorkingDirectory = dir, UseShellExecute = true }); }
        }

        private static string SaveText(string text, string ext)
        {
            var dir = Settings.Current.ResolvedMediaFolder;
            var p = Path.Combine(dir, "Clipboard " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ext);
            File.WriteAllText(p, text, new UTF8Encoding(false));
            return p;
        }

        public static string Tail(string s, int lines = 6)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var all = s.Split('\n');
            return string.Join("\n", all.Skip(Math.Max(0, all.Length - lines))).Trim();
        }
    }
}
