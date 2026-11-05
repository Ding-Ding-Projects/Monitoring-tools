using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The language audit behind the self-test check "every visible string on every page and window has a Cantonese entry".
    /// It switches to Cantonese, opens every page and window the app has, walks the visible text, tooltips and accessible
    /// names, and collects any string that still contains English words that are not names, units or codes.
    /// </summary>
    internal static class LanguageAudit
    {
        // Product names, units, keys and abbreviations that are written the same way in every language.
        private static readonly HashSet<string> Keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AIFF", "XL", "PCX", "PPM", "QOI", "Targa", "WavPack", "QuickTime", "ProRes", "HEVC", "AV1", "Theora", "Vorbis", "Opus", "ALAC", "AMR", "Dolby",
            "Matroska", "MPEG", "FLV", "WMV", "M4V", "3GP", "OGV", "M4A", "WMA", "MKA", "APNG", "AVIF", "JXL", "TGA", "AC", "VP9", "H", "Flash", "Ogg", "Bitmap",
            "Ollama", "Minecraft", "GitHub", "Git", "FFmpeg", "PowerShell", "Windows", "Monitoring", "Tools", "Docker", "Compose", "Notepad",
            "Wi", "Fi", "DNS", "IP", "IPv", "TCP", "UDP", "URL", "URI", "JSON", "CSV", "MP", "GIF", "PNG", "WebP", "JPEG", "JPG", "PDF", "ZIP", "HTTP", "HTTPS",
            "PIN", "TOTP", "HOTP", "QR", "SHA", "PBKDF", "AES", "GCM", "UTF", "USB", "CPU", "GPU", "RAM", "ms", "KB", "MB", "GB", "TB", "kB", "Hz", "px", "fps", "kbps", "Mbps",
            "Ctrl", "Shift", "Alt", "Enter", "Esc", "Tab", "Del", "Win", "Markdown", "RGB", "RGBA", "HSL", "HSV", "HSB", "HEX", "CMYK", "OKLCH", "OKLAB", "LAB", "LCH", "HWB",
            "CSS", "XML", "YAML", "TOML", "HTML", "SVG", "MKV", "MOV", "AVI", "WAV", "FLAC", "OGG", "AAC", "WebM", "BMP", "TIFF", "ICO", "NET", "API", "SSH", "SSL", "TLS",
            "UI", "ID", "OS", "PC", "AI", "LLM", "GGUF", "CLI", "SSD", "NAT", "VPN", "LAN", "WAN", "MAC", "ARP", "ICMP", "TTL", "RAW", "UTC", "AM", "PM", "CRLF", "LF", "UAC",
            "Roblox", "League", "Legends", "Valorant", "Fortnite", "Steam", "Epic", "Cloudflare", "Google", "OpenDNS", "Quad", "Comodo", "Paper", "Spigot", "Fabric", "Forge",
            "Vanilla", "Purpur", "Bukkit", "NeoForge", "Mojang", "Java", "Gradle", "Bedrock", "Neo", "Quilt", "Folia", "Velocity", "Waterfall", "Windows", "Microsoft", "Apple", "Mac",
            "Hyper", "WSL", "Linux", "Ubuntu", "Edge", "Chrome", "Firefox", "Python", "Node", "Visual", "Studio", "Code", "VS", "Sublime", "Text", "Regex", "RegexBuddy",
            "Base", "AES", "Argon", "bcrypt", "x", "px", "pt", "em", "pm", "am", "vs", "etc", "eg", "ie", "Adapter", "Gateway", "Wi-Fi", "WiFi", "Ethernet",
            "Intel", "AMD", "NVIDIA", "Realtek", "Qualcomm", "Atheros", "Broadcom", "Home", "Offline", "Cantonese", "English", "Mint", "Violet", "Amber", "Rose", "Sky", "Coral", "Lime",
            "Aa", "ABC", "NG", "OK", "Alpha", "Beta", "AVIF", "Matroska", "Ogg", "Vorbis", "Opus", "QuickTime", "GitLab", "Gitea", "Forgejo", "Valheim", "winget", "primary", "outline", "surface", "Drawesome", "Ink", "ECMAScript", "true", "false", "Qwen", "Llama", "Gemma", "Mistral", "DeepSeek", "Phi", "LLaVA", "Nomic", "TinyLlama", "Footage", "my", "repo", "Insiders", "en", "Hong", "Kong", "Toronto", "Canada", "CA", "US", "UK", "HK", "AS", "Fibre", "Example", "Org"
        };

        // Key names are only skipped inside a Cantonese sentence; a lone English "Delete" is still a gap.
        private static readonly HashSet<string> KeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Enter", "Delete", "Shift", "Ctrl", "Alt", "Esc", "Home", "End", "Tab", "Space", "Backspace", "Left", "Right", "Up", "Down" };

        // Lock labels that earlier self-test checks wrote into the safety history, and the names of the sample Minecraft servers and plugins; they are test data, not wording of the app.
        private static readonly HashSet<string> TestLabels = new HashSet<string>(StringComparer.Ordinal) { "Frozen look", "Frozen look (PIN)", "Selftest button", "Selftest button (PIN)", "Friends survival", "Creative build", "Minigames", "Modded test", "Chunky", "EssentialsX", "LuckPerms", "paper-custom-key" };

        // Sample data and data the person owns: a commit message, a file name with its extension.
        private static readonly Regex SampleData = new Regex(@"(Update \S+ on \S+ at \S+$)|(^Selftest\b)|(\bSelftest (lock|button|outage|entry))|(\bBulk drive notice \d+$)|(Vote for the next event in #general$)|(^detail \d+$)|(^poster$)|(\.[A-Za-z0-9]{1,5}$)|(^[A-Za-z]:\\)|(^%[A-Z]+%)", RegexOptions.Compiled);

        private static readonly Regex Word = new Regex(@"[A-Za-z]+", RegexOptions.Compiled);
        private static readonly char[] CodeChars = "0123456789/\\_@#=%$~<>{}|^*+[]".ToCharArray();

        internal sealed class Finding { public string Text; public string Where; public string Kind; }

        private static readonly SortedDictionary<string, Finding> Found = new SortedDictionary<string, Finding>(StringComparer.Ordinal);
        internal static int Examined;

        internal static void Reset() { Found.Clear(); Seen.Clear(); Examined = 0; }
        internal static IReadOnlyCollection<Finding> Findings => Found.Values;

        /// <summary>True when the text still holds an English word that is not a name, unit or code.</summary>
        internal static bool HasUntranslated(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (TestLabels.Contains(text.Trim())) return false;
            if (SampleData.IsMatch(text.Trim())) return false;
            bool cjk = Language.HasCjk(text);
            int run = 0;   // inside a Cantonese sentence a technical word is fine; three English words in a row are a gap
            foreach (var chunk0 in text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                bool flagged = false;
                var chunk = chunk0.Trim('(', ')', '“', '”', '"', '\'', '.', ',', ';', ':', '!', '?', '…', '·', '–', '—', '-', '‘', '’');
                bool skip = chunk.Length == 0
                    || chunk.IndexOfAny(CodeChars) >= 0                 // numbers, paths, formats, code
                    || chunk0.Contains(':')                              // a :command, a drive letter, a key:value pair
                    || chunk.Contains('.')                               // file names, versions, domains
                    || chunk0.TrimStart('(').StartsWith("\"", StringComparison.Ordinal);   // a quoted literal such as "md"
                if (!skip)
                    foreach (Match m in Word.Matches(chunk))
                    {
                        var w = m.Value;
                        if (w.Length < 2 || Keep.Contains(w)) continue;
                        if (Regex.IsMatch(w, "^[a-z]+[A-Z]")) continue;     // camelCase names such as onPrimary
                        if (cjk && KeyNames.Contains(w)) continue;
                        flagged = true; break;
                    }
                if (!flagged) { run = 0; continue; }
                if (!cjk) return true;
                if (++run >= 3) return true;
            }
            return false;
        }
        internal static readonly Dictionary<string, string> Seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static void Note(string english, string where)
        {
            if (string.IsNullOrWhiteSpace(english) || Language.HasCjk(english)) return;
            var key = english.Trim();
            if (!Seen.ContainsKey(key) && Language.Yue(key) != null) Seen[key] = where;
        }

        /// <summary>True while a bilingual pass runs: text must show the Cantonese beside its English, not only be free of English.</summary>
        private static bool Bilingual;

        private static void Check(string text, string where, string kind, string original = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Note(original ?? text, where);
            Examined++;
            if (Bilingual)
            {
                var en = (original ?? text).Trim();
                if (Language.HasCjk(en) || !HasUntranslated(en)) return;   // already Cantonese, or a name, unit or code that stays as it is
                string why = Language.Yue(en) == null ? null : Language.HasCjk(text) ? "ok" : "English only";
                if (why == "ok") return;
                var k = en;
                if (!Found.ContainsKey(k)) Found[k] = new Finding { Text = k, Where = where, Kind = why == null ? kind + " (no Cantonese entry)" : kind + " (bilingual shows English only)" };
                return;
            }
            if (!HasUntranslated(text)) return;
            var key = text.Trim();
            if (!Found.ContainsKey(key)) Found[key] = new Finding { Text = key, Where = where, Kind = kind };
        }

        internal static void Scan(string where, DependencyObject root)
        {
            if (root == null) return;
            Language.Refresh(root);
            Visit(where, root, 0);
            if (UiAudit.Active) UiAudit.Collect(where, root);
        }

        internal static void ScanAllWindows(string where)
        {
            foreach (Window w in Application.Current.Windows.Cast<Window>().ToList())
                if (w.IsVisible) Scan(where + " / " + w.GetType().Name, w);
        }

        private static void Visit(string where, DependencyObject d, int depth)
        {
            if (d == null || depth > 80) return;
            if (d is Window win) Check(win.Title, where, "window title", Language.OriginalTitle(win));
            if (d is UIElement ue && !ue.IsVisible && !(d is Window)) return;
            if (d is TextBlock tb)
            {
                if (!Language.IsLiteral(tb))
                    foreach (var run in Runs(tb.Inlines))
                        if (!(run.Parent is Hyperlink) && !Language.GetLiteral(run)) Check(run.Text, where + "  [" + Chain(tb) + "]", "text", Language.OriginalText(run));
            }
            if (d is FrameworkElement fe)
            {
                if (fe.ToolTip is string tip && !Language.GetLiteral(fe)) Check(tip, where, "tooltip", Language.OriginalTip(fe));
                if (d is ComboBox cb)
                    foreach (var item in cb.Items)
                    {
                        if (item is string s) { var y = Language.Yue(s); Check(y ?? s, where, "choice"); }
                        else if (item is ComboBoxItem cbi && cbi.Content is string cs) { var y = Language.Yue(cs); Check(y ?? cs, where, "choice"); }
                    }
            }
            if (d is UIElement && !Language.HasLiteralAncestor(d))
            {
                if (d.ReadLocalValue(AutomationProperties.NameProperty) is string name) Check(name, where, "accessible name", Language.OriginalName(d));
                if (d.ReadLocalValue(AutomationProperties.HelpTextProperty) is string help) Check(help, where, "help text", Language.OriginalHelp(d));
            }
            int n = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = 0; i < n; i++) Visit(where, VisualTreeHelper.GetChild(d, i), depth + 1);
        }

        private static string Chain(DependencyObject d)
        {
            var parts = new List<string>();
            for (int i = 0; d != null && i < 7; i++) { parts.Add(d.GetType().Name); d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
            return string.Join(" < ", parts);
        }

        private static IEnumerable<Run> Runs(InlineCollection inlines)
        {
            foreach (var i in inlines.ToList())
            {
                if (i is Run r) yield return r;
                else if (i is Span sp) foreach (var x in Runs(sp.Inlines)) yield return x;
            }
        }

        // ------------------------------------------------------------------ the scenes

        private static async Task Settle(int ms = 220) { await Task.Delay(ms); }

        /// <summary>Opens every page and window with Cantonese on and records what is left in English.</summary>
        internal static async Task RunAsync(string dir)
        {
            Reset();
            // locks left behind by earlier self-test checks are test data, not screens of the app
            foreach (var l in Locks.All.Where(x => x.PageId == "selftest" || (x.Key ?? "").Contains("selftest")).ToList()) { try { Locks.Remove(l.Id); } catch { } }
            var saved = Settings.Current.LanguageMode;
            Settings.Current.LanguageMode = LanguageMode.Cantonese;
            Language.Refresh();
            var samples = Demo.EnsureSamples();
            try
            {
                // toasts of every kind
                var toasts = new List<ToastWindow>();
                async Task Toast(string name, ToastWindow w)
                {
                    w.ExpandNow();
                    await Settle(300);
                    Scan("toast: " + name, w);
                    w.CloseAnimated();
                }
                Settings.Current.ToastSeconds = 600;
                await Toast("markdown", App.Toasts.ShowClip(ClipItem.FromText(Demo.SampleMarkdown)));
                await Toast("link", App.Toasts.ShowClip(ClipItem.FromText(Demo.SampleUrl)));
                await Toast("image file", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { samples["image"] })));
                await Toast("csv file", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { samples["csv"] })));
                await Toast("folder", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { samples["folder"] })));
                await Toast("several files", App.Toasts.ShowClip(ClipItem.FromFiles(new[] { samples["image"], samples["image2"], samples["csv"] })));
                await Toast("colour", App.Toasts.ShowClip(ClipItem.FromText("#7CF2C2")));
                await Toast("plain text", App.Toasts.ShowClip(ClipItem.FromText("Meeting moved to 3 pm, same room.")));
                await Toast("json", App.Toasts.ShowClip(ClipItem.FromText("{\"name\":\"demo\",\"items\":[1,2,3]}")));
                await Toast("email", App.Toasts.ShowClip(ClipItem.FromText("someone@example.com")));
                await Toast("base64", App.Toasts.ShowClip(ClipItem.FromText(FileOps.EncodeText("Monitoring Tools decodes this for you."))));
                await Toast("public ip", App.Toasts.ShowIpChanged(
                    new Net.IpRecord { Ip = "203.0.113.24", FirstSeen = DateTime.Now.AddHours(-6), LastSeen = DateTime.Now.AddMinutes(-1), Org = "AS64500 Example Fibre", City = "Toronto", Country = "CA" },
                    new Net.IpRecord { Ip = "198.51.100.77", FirstSeen = DateTime.Now, LastSeen = DateTime.Now, Org = "AS64500 Example Fibre", City = "Toronto", Country = "CA", Times = 2 }));
                var job = new ConversionJob
                {
                    Source = Path.Combine(samples["folder"], "launch-teaser.mov"), Format = Formats.Find("mp4"),
                    Output = Path.Combine(samples["folder"], "launch-teaser.mp4"), SourceBytes = 412L * 1024 * 1024,
                    Duration = TimeSpan.FromSeconds(286), Position = TimeSpan.FromSeconds(134), Speed = 3.1, Progress = 0.47
                };
                var jw = App.Toasts.ShowJob(job);
                await Settle(400); job.Raise(); await Settle(300);
                Scan("toast: conversion in progress", jw);
                jw.CloseAnimated();
                var notice = App.Toasts.ShowNotice("Done", "Something finished.", App.Toasts.Mint, 600, new List<ToastAction>(), Icons.File);
                await Settle(300);
                Scan("toast: notice", notice);
                notice.CloseAnimated();

                // offline dialog
                var off = new OfflineWindow(Demo.SampleOutage());
                off.Show(); await Settle(500);
                Scan("offline dialog", off); off.Close();
                App.Toasts.ReservedBottom = 0;

                // tray menu
                var menu = new TrayMenuWindow();
                menu.ShowAtCursor(); await Settle(400);
                Scan("tray menu", menu); menu.Close();

                // setup wizard, every step
                var wiz = new SetupWizardWindow();
                wiz.Show(); await Settle(500);
                for (int i = 0; i < 9; i++) { wiz.GoTo(i); await Settle(250); Scan("setup wizard step " + (i + 1), wiz); }
                wiz.Close();
                Settings.Current.OnboardingStep = 0;

                // welcome
                var welcome = new WelcomeWindow();
                welcome.Show(); await Settle(700);
                Scan("welcome", welcome); welcome.Close();

                // command palette
                CommandPalette.Open(); await Settle(400);
                if (CommandPalette.Current != null) Scan("command palette", CommandPalette.Current);
                CommandPalette.Current?.Close();

                // file tools, every tab
                var ft = new FileToolsWindow(new[] { samples["image"], samples["image2"], samples["csv"] });
                ft.Show(); await Settle(500);
                for (int tab = 0; tab < 3; tab++) { ft.Select(tab, false); await Settle(250); Scan("file tools tab " + (tab + 1), ft); }
                ft.Close();

                // commit template, confirmations
                var commit = new CommitTemplateWindow(continueAfter: false);
                commit.Show(); await Settle(400);
                Scan("commit template", commit); commit.Close();
                var gate = new ConfirmWindow("Uninstall Monitoring Tools?", "This stops the background service, removes it from start-up and deletes the program.", "Uninstall", "Also remove settings, logs and scripts", danger: true);
                gate.Show(); await Settle(400);
                Scan("confirmation", gate); gate.ArmForPreview(60); await Settle(250); Scan("confirmation armed", gate); gate.Close();

                // the control centre: every page, then the element menu, the appearance editor, the colour picker and the regex builder
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                cc.Show(); await Settle(900);
                foreach (var id in cc.SectionIds.ToList())
                {
                    cc.Navigate(id, animate: false);
                    await Settle(id == "ollama" ? 2500 : 450);   // the Ollama page fills in after its server check
                    Scan("page: " + id, cc);
                }

                cc.Navigate("overview", animate: false); await Settle(350);
                var f = cc.PageSearch;
                if (f != null)
                {
                    f.State.SetQuery(@"(?<name>\w+)\.(png|md|csv)");
                    foreach (var tab in new[] { "Build", "Test", "Replace", "Debug", "Compare", "Files", "Code", "Library" })
                    {
                        f.OpenBuilder(tab); await Settle(450);
                        Scan("regex builder: " + tab, cc);
                        if (f.BuilderCard != null) Scan("regex builder: " + tab, f.BuilderCard);
                    }
                    f.State.SetQuery("(unclosed"); await Settle(250);
                    if (f.BuilderCard != null) Scan("regex builder: invalid pattern", f.BuilderCard);
                    f.State.SetQuery("zzzz-no-match-zzzz"); f.State.SetRegex(false); await Settle(250);
                    Scan("search: no match", cc);
                    f.State.SetQuery("");
                }

                // the element menu and the appearance editor on a real button
                cc.Navigate("overview", animate: false); await Settle(350);
                var target = FindFirst<Button>(cc, b => b.IsVisible && b.ActualWidth > 20);
                if (target != null)
                {
                    ElementMenu.Open(cc, target, null, new Point(60, 60)); await Settle(350);
                    ScanAllWindows("element menu");
                    CloseExtra(cc);
                    AppearanceEditorWindow.Open(cc, target); await Settle(600);
                    ScanAllWindows("appearance editor");
                    CloseExtra(cc);
                }

                // bulk selection, guided forms, rule and ticket forms, the forge choice and the toast menu
                foreach (var page in new[] { "history", "game", "ollama", "records" })
                {
                    cc.Navigate(page, animate: false); await Settle(400);
                    if (await Press(cc, "Select")) { Scan("bulk selection: " + page, cc); await Press(cc, "Done selecting"); }
                }
                cc.Navigate("network", animate: false); await Settle(400);
                if (await Press(cc, "Add a profile…"))
                    for (int step = 0; step < 6; step++) { Scan("guided form: DNS profile, step " + (step + 1), cc); if (!await Press(cc, "Next")) break; }
                cc.Navigate("shortcuts", animate: false); await Settle(400);
                if (await Press(cc, "New shortcut"))
                    for (int step = 0; step < 6; step++) { Scan("guided form: new shortcut, step " + (step + 1), cc); if (!await Press(cc, "Next")) break; }
                // the server manager: every section of a sample server, then every step of the create flow
                MonitoringTools.Minecraft.McDemo.Seed();
                cc.Navigate("minecraft", animate: false); await Settle(500);
                var mcv = cc.MinecraftManager;
                foreach (var sid in new[] { "sample-friends", "sample-modded", "sample-minigames", "sample-creative" })
                {
                    mcv.Select(sid);
                    foreach (var (sec, _, _) in UI.Minecraft.McManagerView.Sections) { mcv.ShowSection(sec); await Settle(350); Scan("minecraft: " + sid + " " + sec, cc); }
                }
                mcv.Select("sample-friends");
                await mcv.VisitCommandStatesAsync(async w => { await Settle(450); Scan("minecraft: " + w, cc); });
                foreach (var grp in MonitoringTools.Minecraft.McPropertyCatalog.Groups) { UI.Minecraft.McManagerView.SettingsGroup = grp; mcv.ShowSection("settings"); await Settle(300); Scan("minecraft: settings " + grp, cc); }
                UI.Minecraft.McManagerView.SettingsGroup = "Gameplay";
                UI.Minecraft.McManagerView.Draft = new UI.Minecraft.McCreateDraft { Version = "1.21.4" };
                mcv.OpenCreate(); await Settle(600);
                for (int step = 0; step < 6; step++) { Scan("guided form: new server, step " + (step + 1), cc); if (!await Press(cc, "Next")) break; await Settle(600); }
                UI.Minecraft.McManagerView.Draft = null; mcv.CloseCreate();
                MonitoringTools.Minecraft.McDemo.Remove();
                cc.Navigate("modes", animate: false); await Settle(400);
                if (await Press(cc, "Add rule")) Scan("modes: schedule rule form", cc);
                cc.Navigate("safety", animate: false); await Settle(400);
                if (await Press(cc, "File a ticket")) Scan("safety: ticket form", cc);
                cc.Navigate("tools", animate: false); await Settle(400);
                foreach (var forge in new[] { "GitLab", "Gitea or Forgejo", "GitHub" })
                    if (await Press(cc, forge)) Scan("tools: forge host " + forge, cc);

                var menuToast = App.Toasts.ShowClip(ClipItem.FromText("Audit sample"));
                await Settle(400);
                var menuItems = typeof(ToastWindow).GetMethod("MenuItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (menuItems != null)
                {
                    ElementMenu.Open(menuToast, menuToast, t => (IEnumerable<ElementMenu.Item>)menuItems.Invoke(menuToast, null), new Point(40, 40)); await Settle(350);
                    ScanAllWindows("toast menu");
                    CloseExtra(menuToast);
                }
                menuToast.CloseAnimated();

                var lockTarget = FindFirst<Button>(cc, b => b.IsVisible && b.ActualWidth > 20);
                if (lockTarget != null)
                {
                    var wizard = new LockWizardWindow(lockTarget, "selftest", "audit", "Audit button", null);
                    wizard.Show(); await Settle(500);
                    foreach (LockPolicy pol in Enum.GetValues(typeof(LockPolicy)))
                        if (await Press(wizard, Locks.Describe(pol))) Scan("lock wizard: " + pol, wizard);
                    Scan("lock wizard", wizard); wizard.Close();
                }

                // the colour picker, on its own in a window
                var pickerWindow = new Window { Width = 520, Height = 780, Content = new ScrollViewer { Content = new ColorPicker(Color.FromRgb(0x7C, 0xF2, 0xC2)) }, Title = "Colour picker" };
                pickerWindow.SetResourceReference(Window.BackgroundProperty, "B.Window");   // a test host shows the theme window colour, as the appearance editor does
                pickerWindow.Resources.MergedDictionaries.Add(Application.Current.Resources);
                pickerWindow.Show(); await Settle(500);
                Scan("colour picker", pickerWindow); pickerWindow.Close();

                // the history panel
                var history = new Window { Width = 560, Height = 640, Background = (Brush)Application.Current.Resources["B.Window"], Title = "History panel", Content = new ScrollViewer { Content = HistoryPanel.Build("selftest:audit", () => "one\ntwo", t => { }, "text") } };
                history.Resources.MergedDictionaries.Add(Application.Current.Resources);
                history.Show(); await Settle(500);
                Scan("history panel", history); history.Close();

                cc.Close();
            }
            finally
            {
                Settings.Current.LanguageMode = saved;
                Language.Refresh();
                foreach (Window w in Application.Current.Windows.Cast<Window>().ToList())
                    if (w is ToastWindow tw) try { tw.Close(); } catch { }
            }
            var sb = new StringBuilder();
            sb.AppendLine($"{Found.Count} untranslated strings out of {Examined} examined");
            foreach (var x in Found.Values) sb.AppendLine($"[{x.Kind}] {x.Where} :: {x.Text.Replace("\n", "\\n")}");
            File.WriteAllText(Path.Combine(dir, "language-untranslated.txt"), sb.ToString());

            // the table, grouped by the page or window where each entry was first seen, for the reviewer sheet
            var tsv = new StringBuilder();
            foreach (var kv in Strings.Table.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                tsv.AppendLine((Seen.TryGetValue(kv.Key, out var w) ? w : "(not shown in the audited windows)") + "\t" + kv.Key.Replace("\n", "\\n") + "\t" + kv.Value.Replace("\n", "\\n"));
            File.WriteAllText(Path.Combine(dir, "language-table.tsv"), tsv.ToString());
            var patterns = new StringBuilder();
            foreach (var kv in Seen.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                if (!Strings.Table.ContainsKey(kv.Key)) patterns.AppendLine(kv.Value + "\t" + kv.Key.Replace("\n", "\\n") + "\t" + (Language.Yue(kv.Key) ?? "").Replace("\n", "\\n"));
            File.WriteAllText(Path.Combine(dir, "language-patterns.tsv"), patterns.ToString());
        }

        /// <summary>
        /// Opens every control-centre page in the given language and reports what is wrong. With <paramref name="refresh"/> false the
        /// audit never calls Language.Refresh itself, so it proves that text written after a window loaded is translated by the
        /// window sweep alone. Bilingual mode checks that each translatable string shows its Cantonese beside the English.
        /// </summary>
        internal static async Task<(List<Finding> Findings, int Examined, int Pages)> PagesAsync(LanguageMode mode, bool refresh)
        {
            Reset();
            Bilingual = mode == LanguageMode.Bilingual;
            var saved = Settings.Current.LanguageMode;
            Settings.Current.LanguageMode = mode;
            Language.Refresh();
            int pages = 0;
            try
            {
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                cc.Show(); await Settle(900);
                foreach (var id in cc.SectionIds.ToList())
                {
                    cc.Navigate(id, animate: false);
                    await Settle(id == "ollama" ? 2500 : 700);   // longer than the 250 ms sweep, so only the sweep can have translated it
                    var where = $"{mode} page: {id}";
                    if (refresh) Scan(where, cc); else Visit(where, cc, 0);
                    pages++;
                }
                cc.Close();
            }
            finally
            {
                Bilingual = false;
                Settings.Current.LanguageMode = saved;
                Language.Refresh();
            }
            return (Found.Values.ToList(), Examined, pages);
        }

        // The English label of a button, read from the text the language layer remembers.
        private static string EnglishLabel(ButtonBase b)
        {
            var tb = FindFirst<TextBlock>(b, t => t.Inlines.Count > 0);
            if (tb != null)
                foreach (var run in Runs(tb.Inlines)) return (Language.OriginalText(run) ?? run.Text)?.Trim();
            return (b.Content as string)?.Trim();
        }

        private static async Task<bool> Press(Window w, string english)
        {
            ButtonBase hit = null;
            void Walk(DependencyObject d, int depth)
            {
                if (hit != null || depth > 80) return;
                if (d is ButtonBase bb && bb.IsVisible && bb.IsEnabled && string.Equals(EnglishLabel(bb), english, StringComparison.Ordinal)) { hit = bb; return; }
                int n = d is Visual ? VisualTreeHelper.GetChildrenCount(d) : 0;
                for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(d, i), depth + 1);
            }
            Walk(w, 0);
            if (hit == null) return false;
            if (hit is System.Windows.Controls.Primitives.ToggleButton tg) tg.IsChecked = !(tg.IsChecked ?? false);
            hit.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await Settle(450);
            return true;
        }

        private static void CloseExtra(Window keep)
        {
            foreach (Window w in Application.Current.Windows.Cast<Window>().ToList())
                if (w != keep && !(w is ToastWindow) && w.IsVisible) try { w.Close(); } catch { }
        }

        private static T FindFirst<T>(DependencyObject root, Func<T, bool> pick) where T : DependencyObject
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T t && pick(t)) return t;
                var deeper = FindFirst(c, pick);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
