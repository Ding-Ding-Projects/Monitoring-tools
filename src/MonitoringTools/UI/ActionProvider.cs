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
        /// <summary>The action changes this toast in place (editing, installing), so the toast stays open when it is pressed.</summary>
        public bool Stay;
        /// <summary>A key shown inside the button, for example "Ctrl+Enter".</summary>
        public string KeyHint;
        /// <summary>Several targets for one action, for example every remote of a repository. With two or more, the toast shows
        /// a dropdown list box named <see cref="ChoicesName"/> beside the button, and the button says and does what the picked
        /// choice says and does. The first choice is picked to start with.</summary>
        public IReadOnlyList<ToastChoice> Choices;
        /// <summary>The accessible name of the dropdown list box, for example "Remote".</summary>
        public string ChoicesName;
    }

    /// <summary>One target of an action that offers several: its line in the dropdown list box, and what the button then
    /// says, explains and does.</summary>
    public sealed class ToastChoice
    {
        public string Text;
        public string Label;
        public string Tip;
        public Action Run;
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
                case ClipKind.Files: Files(item, list); DataSecActions.Add(item, list); MediaActions.Add(item, list); break;
                case ClipKind.Image: Bitmap(item, list); list.Add(A("Pin on screen", Icons.Image, () => PinWindow.Show(item), done: "Pinned")); if (item.Image != null) list.Add(new ToastAction { Label = "Read the text", Icon = Icons.Text, HandsOff = true, Tip = "Finds text and QR codes in the picture, on this PC", Run = () => Dialogs.PictureTextDialog.Open(item.Image) }); if (item.Image != null) list.Add(new ToastAction { Label = "Blur private details", Icon = Icons.Shield, HandsOff = true, Tip = "Blurs emails, numbers and faces and copies the result, on this PC", Run = () => _ = Dialogs.PictureTextDialog.Open(item.Image).BlurAndCopy() }); break;
                case ClipKind.Audio: AudioClip(item, list); break;
                default:
                    SmartActions.Add(item, list);
                    TextActions(item.Text, list);
                    if (item.Text != null && item.Text.Contains('\n')) list.Add(A("SQL IN (…) list", null, () => Set(Clip.Smart.Developer2.InList(item.Text)), done: "Copied"));
                    if (!item.Sensitive) SmartActions.AddPowerActions(item, list);
                    break;
            }
            ArchiveDiskActions.Add(item, list);
            return list;
        }

        // -------------------------------------------------------------- text

        private static void TextActions(string text, List<ToastAction> l)
        {
            var t = text ?? "";
            if (FileOps.LooksBase64(t))
            {
                var decoded = FileOps.DecodeToText(t);
                if (decoded != null)
                    l.Add(A("Decode Base64", Icons.Code, () => Set(decoded), primary: true, done: "Decoded"));
                else
                    l.Add(A("Decode to file", Icons.File, () =>
                    {
                        FileOps.TryDecode(t, out var bytes);
                        var p = FileOps.Unique(Path.Combine(Settings.Current.ResolvedMediaFolder, "Decoded " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + GuessExt(bytes)));
                        File.WriteAllBytes(p, bytes);
                        Reveal(p);
                    }, primary: true, done: "Saved"));
            }
            l.Add(A("Encode Base64", null, () => Set(FileOps.EncodeText(t)), done: "Encoded"));
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
            if (RepoActions.GitHubRepo(full) is { } repo)
            {
                var folder = RepoActions.CloneFolder;
                l.Add(new ToastAction
                {
                    Label = "Clone to " + Path.GetFileName(folder.TrimEnd('\\')), Icon = Icons.Folder, HandsOff = true,
                    Tip = $"Clones {repo.Owner}/{repo.Name} into {Path.Combine(folder, repo.Name)}. Change the default folder on the Tools page.",
                    Run = () => CloneWithProgress(repo.Owner, repo.Name, folder),
                });
                l.Add(new ToastAction
                {
                    Label = "Clone to…", Icon = Icons.Folder, HandsOff = true, Tip = "Choose the folder to clone into this time.",
                    Run = () =>
                    {
                        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = $"Clone {repo.Owner}/{repo.Name} into…", InitialDirectory = Directory.Exists(folder) ? folder : null };
                        if (dlg.ShowDialog() == true) CloneWithProgress(repo.Owner, repo.Name, dlg.FolderName);
                    },
                });
            }
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
            l.Add(A("Copy as YAML", null, () => Set(Clip.Smart.Developer2.JsonToYaml(s)), done: "Copied"));
            l.Add(A("Save as .json", Icons.File, () => Reveal(SaveText(s, ".json")), done: "Saved"));
        }

        // -------------------------------------------------------------- files

        private static void Files(ClipItem item, List<ToastAction> l)
        {
            var f = item.FirstFile;
            var kind = item.FirstFileKind;
            bool many = item.Files.Length > 1;
            var files = item.Files;

            // Order is priority: the toast shows the first four buttons and tucks the rest behind More.
            if (item.IsPathText && item.HadQuotes)
                l.Add(A("Remove quotes", null, () => Set(string.Join(Environment.NewLine, files)), primary: true, done: "Unquoted"));

            if (many)
            {
                l.Add(new ToastAction { Label = "Move to…", Icon = Icons.Folder, Primary = !item.HadQuotes, HandsOff = true, Run = () => FileToolsWindow.MoveOrCopy(files, move: true) });
                l.Add(new ToastAction { Label = "Copy to…", Icon = Icons.Clipboard, HandsOff = true, Run = () => FileToolsWindow.MoveOrCopy(files, move: false) });
                l.Add(new ToastAction { Label = "Delete", Icon = Icons.Trash, HandsOff = true, Run = () => FileToolsWindow.ConfirmRecycle(files) });
                l.Add(new ToastAction { Label = "File tools", Icon = Icons.Shield, HandsOff = true, Tip = "Base64 and password encryption for all of them", Run = () => FileToolsWindow.Open(files) });
                l.Add(A("Show in folder", Icons.Folder, () => Reveal(f), done: "Shown"));
                l.Add(A("Copy names", null, () => Set(string.Join(Environment.NewLine, files.Select(p => Path.GetFileName(p.TrimEnd('\\'))))), done: "Copied"));
                l.Add(A(item.IsPathText ? "Add quotes" : "Copy as paths", null, () => Set(string.Join(Environment.NewLine, files.Select(p => $"\"{p}\""))), done: "Copied"));
                if (ConvertDialog.TargetsFor(files).Count > 0) l.Add(A("Convert…", Icons.Bolt, () => ConvertDialog.Open(files), done: "Opened"));
                BatchChips(files, l);
                return;
            }

            if (kind == FileKind.Folder) FolderRepo(f, l, primary: !item.HadQuotes);

            bool encrypted = kind != FileKind.Folder && FileOps.IsEncrypted(f);
            if (encrypted)
                l.Add(new ToastAction { Label = "Decrypt…", Icon = Icons.Shield, Primary = true, HandsOff = true, Run = () => FileToolsWindow.Open(files, "encrypt") });

            foreach (var d in DockerActions.For(f))
            {
                var da = d;
                l.Add(new ToastAction
                {
                    Label = da.Label, Icon = Icons.Box, Primary = da.Primary && !item.HadQuotes, Tip = da.Explain, HandsOff = !da.InTerminal,
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

            bool primaryTaken = item.HadQuotes || encrypted || DockerActions.Detect(f) != DockerFileKind.None;
            DocSysActions.Add(item, l);
            if (kind == FileKind.Folder)
            {
                l.Add(A("Open", Icons.Folder, () => Shell(f), primary: !primaryTaken, done: "Opened"));
                l.Add(A("Terminal here", Icons.Code, () => Terminal(f), done: "Opened"));
            }
            else
            {
                var app = FileOps.DefaultAppName(f);
                l.Add(A(app != null ? $"Open in {app}" : "Open", Icons.Play, () => Shell(f), primary: !primaryTaken, done: "Opened"));
                l.Add(A("Show in folder", Icons.Folder, () => Reveal(f), done: "Shown"));
            }
            l.Add(new ToastAction { Label = "File tools", Icon = Icons.Shield, HandsOff = true, Tip = "Move, copy, delete, Base64 and password encryption", Run = () => FileToolsWindow.Open(files) });
            if (kind != FileKind.Folder)
                l.Add(new ToastAction { Label = "Open with…", HandsOff = true, Run = () => FileOps.OpenWithPicker(f) });
            if (!item.HadQuotes)
                l.Add(A(item.IsPathText ? "Add quotes" : "Copy as path", null, () => Set($"\"{f}\""), done: "Copied"));
            if (!item.IsPathText) l.Add(A("Copy path text", null, () => Set(f), done: "Copied"));
            l.Add(A("Copy name", null, () => Set(Path.GetFileName(f.TrimEnd('\\'))), done: "Copied"));
            if (f.Contains('\\')) l.Add(A("Forward slashes", null, () => Set(f.Replace('\\', '/')), done: "Copied"));
            if (item.IsPathText) l.Add(A("Copy as file", Icons.Clipboard, () => ClipboardWatcher.SetFiles(f), done: "Copied"));

            // A repository folder (or a file in one) can be pushed
            if (GitPush.FindRepoRoot(f) != null)
                l.Add(new ToastAction { Label = ":push", Chip = true, Tip = "Commit everything and push. Same as copying the path with :push on the end.", HandsOff = true, Run = () => App.Toasts.StartPush(f) });

            if (kind == FileKind.Folder)
            {
                var inside = SafeFiles(f);
                if (inside.Any(p => { var k = ClipItem.Classify(p); return k == FileKind.Video || k == FileKind.Audio || k == FileKind.Image; }))
                    l.Add(A("Convert…", Icons.Bolt, () => ConvertDialog.Open(inside, f), done: "Opened"));
                BatchChips(inside, l, f);
            }
            else
            {
                if (kind == FileKind.Video || kind == FileKind.Audio || kind == FileKind.Image)
                    l.Add(A("Convert…", Icons.Bolt, () => ConvertDialog.Open(new[] { f }), done: "Opened"));
            }
            if (kind != FileKind.Folder)
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
            if (!many && f != null && System.IO.File.Exists(f))
            {
                if (kind == FileKind.Image) l.Add(A("Annotate", Icons.Wand, () => DrawingWindow.OpenFile(f, out _), done: "Opened"));
                l.Add(A("Open in editor", Icons.Code, () => ExternalEditor.OpenAndTell(f), done: "Opened"));
            }
        }

        private static string[] SafeFiles(string folder)
        {
            try { return Directory.EnumerateFiles(folder).Take(2000).ToArray(); } catch { return Array.Empty<string>(); }
        }

        /// <summary>Batch conversion chips for a selection or a folder: only formats at least one file can become.</summary>
        private static void BatchChips(string[] files, List<ToastAction> l, string folder = null)
        {
            var kinds = files.Select(ClipItem.Classify).Where(k => k == FileKind.Video || k == FileKind.Audio || k == FileKind.Image).Distinct().ToList();
            if (kinds.Count == 0) return;
            var ids = kinds.SelectMany(k => Formats.SuggestedFor(k, null)).Distinct().Take(8).ToList();
            foreach (var id in ids)
            {
                var spec = Formats.Find(id);
                int n = files.Count(p => Converter.CompatibleInBatch(p, spec));
                if (n == 0) continue;
                var fid = id;
                l.Add(new ToastAction
                {
                    Label = ":" + id, Chip = true, HandsOff = true,
                    Tip = folder != null ? $"Convert {n} file{(n == 1 ? "" : "s")} in this folder to {spec.Label}. Same as copying the folder with :{id} on the end."
                                         : $"Convert {n} of these to {spec.Label}",
                    Run = () => Converter.StartBatch(files, fid, folder)
                });
            }
        }

        // -------------------------------------------------------------- bitmaps and audio from the clipboard

        private static void Bitmap(ClipItem item, List<ToastAction> l)
        {
            var img = item.Image;
            if (img == null) return;
            var otp = AuthenticatorQr(img);
            if (otp != null)
            {
                var name = string.IsNullOrEmpty(otp.Issuer) ? otp.Account : otp.Issuer + (string.IsNullOrEmpty(otp.Account) ? "" : " · " + otp.Account);
                l.Add(new ToastAction
                {
                    Label = "Register for codes", Icon = Icons.Shield, Primary = true, Done = "Registered",
                    Tip = $"This picture is an authenticator code for {name}. Adds it to the built-in authenticator (Safety page), stored encrypted for this Windows account.",
                    Run = () => RegisterAuthenticator(otp, name),
                });
            }
            var codes = CodeScanner.Scan(img);
            foreach (var c in codes.Take(3))
            {
                var code = c;
                var what = code.IsQr ? "QR" : code.Kind.EndsWith("barcode") ? "barcode" : "code";
                l.Add(new ToastAction
                {
                    Label = $"Copy {what} text", Icon = Icons.Clipboard, Primary = otp == null && code == codes[0], Done = "Copied",
                    Tip = $"Copies what the {code.Kind} holds: {Preview(code.Text)}",
                    Run = () => ClipboardWatcher.SetText(code.Text),
                });
                if (Uri.TryCreate(code.Text.Trim(), UriKind.Absolute, out var link) && (link.Scheme == Uri.UriSchemeHttps || link.Scheme == Uri.UriSchemeHttp))
                    l.Add(new ToastAction { Label = "Open link", Icon = Icons.Globe, HandsOff = true, Tip = "Opens " + link, Run = () => Process.Start(new ProcessStartInfo(link.ToString()) { UseShellExecute = true }) });
            }
            l.Add(A("Save PNG", Icons.Image, () => Reveal(Converter.SaveBitmap(img, "png")), primary: otp == null && codes.Count == 0, done: "Saved"));
            l.Add(A("Save JPG", null, () => Reveal(Converter.SaveBitmap(img, "jpg")), done: "Saved"));
            l.Add(A("Save + copy as file", Icons.Clipboard, () => { var p = Converter.SaveBitmap(img, "png"); ClipboardWatcher.SetFiles(p); }, done: "Copied"));
            l.Add(A("Open in Paint", null, () =>
            {
                var p = Converter.SaveBitmap(img, "png");
                Process.Start(new ProcessStartInfo("mspaint.exe", $"\"{p}\"") { UseShellExecute = true });
            }, done: "Opened"));
            l.Add(A("Annotate", Icons.Wand, () => DrawingWindow.OpenBitmap(img, "Copied image"), done: "Opened"));
            foreach (var id in new[] { "webp", "avif", "ico", "bmp" })
            {
                var f = id;
                l.Add(new ToastAction { Label = ":" + f, Chip = true, HandsOff = true, Tip = $"Save as {Formats.Find(f)?.Label} in your media folder", Run = () => { var p = Converter.SaveBitmap(img, f); if (p != null) Reveal(p); } });
            }
        }

        /// <summary>Clones with a progress toast, then offers to show the folder or open it in Desktop Material.</summary>
        internal static void CloneWithProgress(string owner, string name, string parent)
        {
            App.Toasts.ShowTask("Cloning " + owner + "/" + name, parent, async () =>
            {
                var r = await System.Threading.Tasks.Task.Run(() => RepoActions.Clone(owner, name, parent));
                if (!r.Ok) return (false, "Clone failed", r.Message);
                var dm = RepoActions.DesktopMaterialExe();
                var actions = new List<ToastAction> { new ToastAction { Label = "Show in folder", Icon = Icons.Folder, HandsOff = true, Run = () => Reveal(r.Folder) } };
                if (dm != null) actions.Insert(0, new ToastAction { Label = "Open in Desktop Material", Icon = Icons.Folder, Primary = true, HandsOff = true, Run = () => RepoActions.OpenInDesktopMaterial(dm, r.Folder) });
                App.Toasts.ShowNotice(r.Message == "already there" ? "Already cloned" : "Cloned", r.Folder, App.Toasts.Mint, 6, actions, Icons.Folder);
                return (true, r.Message == "already there" ? "Already cloned" : "Cloned", r.Folder);
            });
        }

        /// <summary>A copied folder: open its Git repository in Desktop Material (added to its list if missing), or start a
        /// repository when the folder is not in one.</summary>
        private static void FolderRepo(string folder, List<ToastAction> l, bool primary)
        {
            var root = RepoActions.RepoRoot(folder);
            var dm = RepoActions.DesktopMaterialExe();
            if (root != null)
            {
                if (dm != null)
                    l.Add(new ToastAction
                    {
                        Label = "Open in Desktop Material", Icon = Icons.Folder, Primary = primary, HandsOff = true,
                        Tip = $"Opens {Path.GetFileName(root)} in Desktop Material, adding it to Desktop Material's list straight away if it is not there yet.",
                        Run = () => RepoActions.OpenInDesktopMaterial(dm, root),
                    });
                RemoteLinks(root, l);
                return;
            }
            l.Add(new ToastAction
            {
                Label = "Start a Git repository", Icon = Icons.Plus, Primary = primary, Done = "Started",
                Tip = $"Runs git init in {Path.GetFileName(folder.TrimEnd('\\'))} with the bundled Git, on a main branch. Nothing is committed.",
                Run = () =>
                {
                    var err = RepoActions.Init(folder);
                    if (err != null) { App.Toasts?.ShowNotice("Repository not started", err, App.Toasts.Amber, 6, icon: Icons.Folder); return; }
                    var actions = dm == null ? null : new List<ToastAction> { new ToastAction { Label = "Open in Desktop Material", Icon = Icons.Folder, Primary = true, HandsOff = true, Run = () => RepoActions.OpenInDesktopMaterial(dm, folder) } };
                    App.Toasts?.ShowNotice("Git repository started", Path.GetFileName(folder.TrimEnd('\\')) + " is now a Git repository on the main branch.", App.Toasts.Mint, 5, actions, Icons.Folder);
                },
            });
        }

        /// <summary>The web pages of a repository's remotes: "Open on GitHub" (or GitLab, Bitbucket, the server's name) for one
        /// remote, and a dropdown list box of every remote beside the button when there are several. Pages never carry a
        /// user name or token from the remote's address.</summary>
        internal static void RemoteLinks(string root, List<ToastAction> l)
        {
            var pages = RemotePages.For(root);
            if (pages.Count == 0) return;
            var choices = pages.Select(p => new ToastChoice
            {
                Text = $"{p.Remote} ({p.Shown})",
                Label = $"Open on {p.Host}",
                Tip = $"Opens {p.Shown}, the page of the {p.Remote} remote, in your browser.",
                Run = () => SmartActions.Open(p.Url),
            }).ToList();
            var first = choices[0];
            l.Add(new ToastAction
            {
                Label = first.Label, Icon = Icons.Globe, HandsOff = true, Tip = first.Tip, Run = first.Run,
                Choices = choices.Count > 1 ? choices : null, ChoicesName = "Remote",
            });
        }

        /// <summary>A short one-line view of a code's text for tooltips and the toast. An authenticator setup link is never
        /// shown, because its text holds the secret.</summary>
        internal static string Preview(string text)
        {
            text = (text ?? "").Trim();
            if (text.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase)) return "an authenticator setup link (secret hidden)";
            var one = string.Join(" ", text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            return one.Length > 120 ? one.Substring(0, 117) + "…" : one;
        }

        /// <summary>The authenticator account in a copied QR code picture, or null. Large pictures are skipped so a
        /// screenshot never stalls the toast.</summary>
        internal static OtpParams AuthenticatorQr(System.Windows.Media.Imaging.BitmapSource img)
        {
            try
            {
                if (img == null) return null;
                var text = CodeScanner.Scan(img).Where(c => c.IsQr).Select(c => c.Text.Trim())
                    .FirstOrDefault(t => t.StartsWith("otpauth://totp/", StringComparison.OrdinalIgnoreCase));
                if (text == null) return null;
                return Totp.TryParseUri(text, out var p, out _) ? p : null;
            }
            catch { return null; }
        }

        /// <summary>Adds the account unless the same secret is already there, and says what happened without ever showing the secret.</summary>
        internal static string RegisterAuthenticator(OtpParams p, string name)
        {
            var secret = Base32.Normalize(p.Secret);
            if (Authenticator.All.Any(a => a.Secret == secret))
            {
                App.Toasts?.ShowNotice("Already registered", name + " is already in the authenticator.", App.Toasts.Mint, 4, icon: Icons.Shield);
                return "already";
            }
            var err = Authenticator.Add(p);
            if (err != null) { App.Toasts?.ShowNotice("Not registered", err, App.Toasts.Amber, 6, icon: Icons.Shield); return err; }
            App.Toasts?.ShowNotice("Registered for codes", name + " now shows its codes on the Safety page.", App.Toasts.Mint, 5,
                new List<ToastAction> { new ToastAction { Label = "Show codes", Icon = Icons.Shield, Primary = true, HandsOff = true, Run = () => App.Current.OpenControlCentre("safety") } }, Icons.Shield);
            return null;
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

        /// <summary>Sniffs common magic numbers so a decoded blob gets a sensible extension.</summary>
        public static string GuessExt(byte[] b)
        {
            if (b == null || b.Length < 4) return ".bin";
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
            if (b[0] == 0xFF && b[1] == 0xD8) return ".jpg";
            if (b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return ".gif";
            if (b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46) return ".pdf";
            if (b[0] == 0x50 && b[1] == 0x4B) return ".zip";
            if (b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45) return ".webp";
            if (b.Length > 8 && b[4] == 0x66 && b[5] == 0x74 && b[6] == 0x79 && b[7] == 0x70) return ".mp4";
            if (b[0] == 0x49 && b[1] == 0x44 && b[2] == 0x33) return ".mp3";
            return ".bin";
        }

        public static string Tail(string s, int lines = 6)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var all = s.Split('\n');
            return string.Join("\n", all.Skip(Math.Max(0, all.Length - lines))).Trim();
        }
    }
}
