using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    // Checks for School mode with Windows Hello, the guided forms, bulk actions, the logo, Export everything and the drawing board.
    public static partial class SelfTest
    {
        internal static IEnumerable<T> Descend<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            if (root is T t) yield return t;
            int n = root is Visual || root is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (int i = 0; i < n; i++)
                foreach (var x in Descend<T>(VisualTreeHelper.GetChild(root, i))) yield return x;
        }

        private static string ButtonLabel(ButtonBase b) => b.Content as string ?? (b.Content is Panel p ? Descend<TextBlock>(p).FirstOrDefault()?.Text : (b.Content as TextBlock)?.Text);
        internal static ButtonBase FindButton(DependencyObject root, string label) => Descend<ButtonBase>(root).FirstOrDefault(b => ButtonLabel(b) == label && b.IsVisible);
        internal static bool Press(DependencyObject root, string label)
        {
            Window.GetWindow(root)?.UpdateLayout(); var b = FindButton(root, label);
            if (b == null || !b.IsEnabled) return false;
            b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            return true;
        }
        private static TextBox FindBox(DependencyObject root, string name) { Window.GetWindow(root as DependencyObject)?.UpdateLayout(); return Descend<TextBox>(root).FirstOrDefault(t => AutomationProperties.GetName(t) == name); }

        private static Window Host(UIElement content)
        {
            var w = new Window { Width = 760, Height = 820, Content = new ScrollViewer { Content = content }, Background = Ui.Res("B.Window") };
            w.Show();
            w.UpdateLayout();
            return w;
        }

        private static string TempDir(string name)
        {
            var d = Path.Combine(Settings.DataDir, "wave6-" + name);
            if (Directory.Exists(d)) Directory.Delete(d, true);
            Directory.CreateDirectory(d);
            return d;
        }

        private static string MakePng(string path, int w, int h)
        {
            var px = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++) { px[i * 4] = 0x40; px[i * 4 + 1] = 0xC0; px[i * 4 + 2] = 0x90; px[i * 4 + 3] = 0xFF; }
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(path)) enc.Save(fs);
            return path;
        }

        private static string MakeIco(string path, int side)
        {
            // An ICO container with one PNG-compressed picture, the form Windows itself writes for large icons.
            var png = Path.ChangeExtension(path, ".tmp.png");
            MakePng(png, side, side);
            var data = File.ReadAllBytes(png);
            File.Delete(png);
            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)1);
            bw.Write((byte)(side >= 256 ? 0 : side)); bw.Write((byte)(side >= 256 ? 0 : side)); bw.Write((byte)0); bw.Write((byte)0);
            bw.Write((short)1); bw.Write((short)32); bw.Write(data.Length); bw.Write(22);
            bw.Write(data);
            return path;
        }

        private static async Task Wave6ChecksAsync()
        {
            // ---------------------------------------------------------------- School mode with Windows Hello
            await CheckAsync("school hello: set up, refusals, unlock and honest unavailable states", async () =>
            {
                var dir = TempDir("school");
                var checker = Modes.HelloChecker; var verifier = Modes.HelloVerifier;
                try
                {
                    Modes.RecordPathOverride = Path.Combine(dir, "school-mode.json");
                    Modes.ReloadSchoolForTest();
                    bool avail = false; HelloResult next = new HelloResult(true, "ok");
                    Modes.HelloChecker = () => Task.FromResult((avail, avail ? "Windows Hello is available." : "Windows Hello is not set up for this Windows account."));
                    Modes.HelloVerifier = _ => Task.FromResult(next);

                    var real = await WindowsHello.CheckAsync();
                    if (string.IsNullOrWhiteSpace(real.State)) return "the real Windows Hello check returned no state text";
                    var early = await Modes.SetUpHelloAsync();
                    if (early.Ok) return "Hello was allowed before any PIN or password existed";
                    if (!Modes.EnableSchool("4821", "4821", out var err)) return "could not turn the mode on: " + err;
                    var whileOn = await Modes.SetUpHelloAsync();
                    if (whileOn.Ok) return "Hello was added while the mode was on";
                    if (!Modes.DisableSchool("4821", out err)) return "PIN turn-off failed: " + err;
                    var none = await Modes.SetUpHelloAsync();
                    if (none.Ok || !none.Message.Contains("not set up")) return "an unavailable Hello did not say so: " + none.Message;
                    if (Modes.HelloUnlockEnabled) return "Hello counted as allowed after a refused setup";
                    avail = true; next = new HelloResult(false, "Windows Hello was cancelled. Nothing was changed.");
                    var cancelled = await Modes.SetUpHelloAsync();
                    if (cancelled.Ok || Modes.HelloUnlockEnabled) return "a cancelled Hello check still allowed Hello";
                    next = new HelloResult(true, "ok");
                    var done = await Modes.SetUpHelloAsync();
                    if (!done.Ok || !Modes.HelloUnlockEnabled) return "setup failed: " + done.Message;
                    var json = File.ReadAllText(Modes.SchoolRecordPath);
                    if (!json.Contains("\"HelloUnlock\": true")) return "the record does not remember Hello";
                    if (!Modes.EnableSchool(null, null, out err)) return "second turn-on failed: " + err;
                    avail = false;
                    var blocked = await Modes.DisableSchoolWithHelloAsync();
                    if (blocked.Ok || !Modes.SchoolActive) return "the mode turned off while Hello was unavailable";
                    avail = true; next = new HelloResult(false, "Windows Hello was cancelled. Nothing was changed.");
                    var no = await Modes.DisableSchoolWithHelloAsync();
                    if (no.Ok || !Modes.SchoolActive) return "the mode turned off after a cancelled Hello check";
                    if (Modes.DisableSchool("0000", out err)) return "a wrong PIN turned the mode off";
                    next = new HelloResult(true, "ok");
                    var yes = await Modes.DisableSchoolWithHelloAsync();
                    if (!yes.Ok || Modes.SchoolActive) return "Hello did not turn the mode off: " + yes.Message;
                    if (Modes.LockSeconds != 0 || Settings.Current.SchoolFailures != 0) return "Hello did not clear the wrong-PIN count";
                    if (!Modes.RemoveHelloUnlock(out err) || Modes.HelloUnlockEnabled) return "Hello could not be removed: " + err;
                    // The PIN still works after all of that.
                    if (!Modes.EnableSchool(null, null, out err) || !Modes.DisableSchool("4821", out err)) return "the PIN stopped working: " + err;
                    return null;
                }
                finally
                {
                    Modes.HelloChecker = checker; Modes.HelloVerifier = verifier;
                    Modes.RecordPathOverride = null; Modes.ReloadSchoolForTest();
                    Settings.Current.SchoolFailures = 0; Settings.Current.SchoolLockUntilUtc = DateTime.MinValue;
                }
            });

            await CheckAsync("school hello: the Modes page shows the Hello row and its button drives setup", async () =>
            {
                var dir = TempDir("school-window");
                var checker = Modes.HelloChecker; var verifier = Modes.HelloVerifier;
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                try
                {
                    Modes.RecordPathOverride = Path.Combine(dir, "school-mode.json");
                    Modes.ReloadSchoolForTest();
                    Modes.HelloChecker = () => Task.FromResult((true, "Windows Hello is available."));
                    Modes.HelloVerifier = _ => Task.FromResult(new HelloResult(true, "ok"));
                    if (!Modes.EnableSchool("4821", "4821", out var err)) return err;
                    if (!Modes.DisableSchool("4821", out err)) return err;
                    cc.Show();
                    await Task.Delay(700);
                    cc.Navigate("modes", animate: false);
                    await Task.Delay(700);
                    var text = SearchScope.Text(cc);
                    if (!text.Contains("Windows Hello (optional)") || !text.Contains("not a FIDO2")) return "the Hello row or its FIDO2 note is missing";
                    if (!Press(cc, "Allow Windows Hello")) return "no enabled Allow Windows Hello button";
                    await Task.Delay(700);
                    if (!Modes.HelloUnlockEnabled) return "pressing the button did not allow Hello";
                    cc.Navigate("modes", animate: false);
                    await Task.Delay(500);
                    if (FindButton(cc, "Stop allowing Windows Hello") == null) return "the Stop allowing button is missing after setup";
                    // Now with Hello unavailable the button is shown disabled and the state says why.
                    Modes.HelloChecker = () => Task.FromResult((false, "Windows Hello is not set up for this Windows account."));
                    if (!Modes.EnableSchool(null, null, out err)) return err;
                    cc.Navigate("modes", animate: false);
                    await Task.Delay(700);
                    var b = FindButton(cc, "Turn off with Windows Hello");
                    if (b == null || b.IsEnabled) return "the Hello turn-off button should be shown disabled when Hello is unavailable";
                    return SearchScope.Text(cc).Contains("not set up for this Windows account") ? null : "the unavailable reason is not shown";
                }
                finally
                {
                    Modes.HelloChecker = checker; Modes.HelloVerifier = verifier;
                    Modes.RecordPathOverride = null; Modes.ReloadSchoolForTest();
                    cc.Close();
                }
            });

            // ---------------------------------------------------------------- guided forms
            await CheckAsync("guided: the schedule rule form blocks bad input and adds through the same check", async () =>
            {
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                Window host = null;
                int before = Settings.Current.ScheduleRules.Count;
                try
                {
                    bool finished = false;
                    var form = cc.ModesRuleGuide(() => finished = true, () => { });
                    host = Host(form);
                    if (!Press(form, "Next")) return "no Next button on step 1";
                    if (form.StepIndex != 1) return "step 1 did not advance";
                    FindBox(form, "Start time").Text = "25:99";
                    Press(form, "Next");
                    if (form.StepIndex != 1) return "a bad time was accepted";
                    FindBox(form, "Start time").Text = "08:00"; FindBox(form, "End time").Text = "12:30";
                    Press(form, "Next");
                    if (form.StepIndex != 2) return "valid times did not advance";
                    FindBox(form, "Start date (optional)").Text = "2026-12-24"; FindBox(form, "End date (optional)").Text = "2026-12-01";
                    Press(form, "Next");
                    if (form.StepIndex != 2) return "an end date before the start date was accepted";
                    FindBox(form, "End date (optional)").Text = "2026-12-31";
                    Press(form, "Next");
                    if (form.StepIndex != 3) return "valid dates did not advance";
                    host.UpdateLayout();
                    if (!SearchScope.Text(form).Contains("08:00 to 12:30")) return "the review does not describe the rule";
                    Press(form, "Add rule");
                    await Task.Delay(100);
                    if (!finished || Settings.Current.ScheduleRules.Count != before + 1) return "the rule was not added";
                    var r = Settings.Current.ScheduleRules.Last();
                    if (r.Start != "08:00" || r.End != "12:30" || r.FromDate != "2026-12-24" || r.ToDate != "2026-12-31") return "the stored rule differs from the entered one";
                    if (Modes.AddRule(new ScheduleRule { Start = "bad", End = "10:00", Setting = "sounds", Value = "off" }) == null) return "the shared check accepted a bad rule";
                    return null;
                }
                finally { if (Settings.Current.ScheduleRules.Count > before) Settings.Current.ScheduleRules.RemoveRange(before, Settings.Current.ScheduleRules.Count - before); host?.Close(); cc.Close(); }
            });

            await CheckAsync("guided: make a look reviews the changes and applies the five choices", async () =>
            {
                var s = Settings.Current;
                var saved = AppearanceLook.FromCurrent();
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                Window host = null;
                try
                {
                    s.ThemeMode = "light"; s.SeedColour = "mint"; s.CompactDensity = false; s.HighContrastOutlines = false; s.TextScalePercent = 100;
                    bool finished = false;
                    var form = cc.AppearanceLookGuide(() => finished = true, () => { });
                    host = Host(form);
                    Press(form, "Dark"); Press(form, "Next");
                    Press(form, "Violet"); Press(form, "Next");
                    Press(form, "Compact"); Drive.Type(Descend<NumberField>(form).First(f => f.Label == "Text size").Box, "120", true);
                    var sw = Descend<CheckBox>(form).FirstOrDefault();
                    if (sw != null) sw.IsChecked = true;
                    Press(form, "Next");
                    if (form.StepIndex != 3) return "the flow did not reach the review";
                    host.UpdateLayout();
                    var review = SearchScope.Text(form);
                    if (!review.Contains("Theme: light to dark") || !review.Contains("Seed colour: mint to violet") || !review.Contains("Text size: 100 % to 120 %")) return "the review does not list the changes: " + review;
                    if (s.ThemeMode != "light") return "the look was applied before the last step";
                    Press(form, "Apply look");
                    await Task.Delay(150);
                    if (!finished) return "the flow did not finish";
                    if (s.ThemeMode != "dark" || s.SeedColour != "violet" || !s.CompactDensity || s.TextScalePercent != 120 || !s.HighContrastOutlines) return "the look was not applied: " + JsonSerializer.Serialize(AppearanceLook.FromCurrent());
                    if (new AppearanceLook { Theme = "pink" }.Check() == null) return "a theme the app does not offer passed the check";
                    return null;
                }
                finally { saved.Apply(); host?.Close(); cc.Close(); }
            });

            await CheckAsync("guided: the Ollama profile form validates parameters and saves a profile", async () =>
            {
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                Window host = null;
                try
                {
                    if (OllamaProfiles.TryBuild("", "x", "", "", "", out _) == null) return "an empty name passed";
                    if (OllamaProfiles.TryBuild("a", "x", "9", "", "", out _) == null) return "a temperature of 9 passed";
                    if (OllamaProfiles.TryBuild("a", "x", "abc", "", "", out _) != null) { /* a word is refused as not a number */ } else return "a word passed as a temperature";
                    var profiles = new List<OllamaProfile>();
                    OllamaProfile saved = null;
                    var form = cc.OllamaProfileGuide(profiles, p => saved = p, () => { });
                    host = Host(form);
                    Press(form, "Next");
                    if (form.StepIndex != 0) return "an empty name was accepted";
                    FindBox(form, "Profile name").Text = "Strict reviewer"; FindBox(form, "System prompt").Text = "Be strict.";
                    Press(form, "Next");
                    if (form.StepIndex != 1) return "the name step did not advance";
                    FindBox(form, "Temperature").Text = "5";
                    Press(form, "Next");
                    if (form.StepIndex != 1) return "a temperature of 5 was accepted";
                    FindBox(form, "Temperature").Text = "0.4"; FindBox(form, "Context length in tokens").Text = "4096";
                    Press(form, "Next");
                    if (form.StepIndex != 2) return "valid parameters did not advance";
                    Press(form, "Save profile");
                    await Task.Delay(100);
                    if (saved == null || profiles.Count != 1 || profiles[0].Temperature != 0.4 || profiles[0].NumCtx != 4096) return "the profile was not saved";
                    return OllamaProfiles.Load().Any(p => p.Name == "Strict reviewer") ? null : "the profile is not in the stored list";
                }
                finally { host?.Close(); cc.Close(); }
            });

            // ---------------------------------------------------------------- bulk actions
            Check("bulk: settings history deletes chosen snapshots but never the newest, and exports them", () =>
            {
                var s = Settings.Current;
                var old = s.ToastSeconds;
                try
                {
                    foreach (var v in new[] { 21, 22, 23 }) { s.ToastSeconds = v; Settings.Save(); }
                    var all = SettingsHistory.List();
                    if (all.Count < 3) return "only " + all.Count + " snapshots";
                    var files = all.Select(x => x.File).ToList();
                    var json = SettingsHistory.ExportJson(files.Take(2));
                    using (var d = JsonDocument.Parse(json)) if (d.RootElement.GetArrayLength() != 2) return "the export did not hold the two chosen snapshots";
                    var removed = SettingsHistory.Delete(files);
                    var left = SettingsHistory.List();
                    if (left.Count != 1 || left[0].File != files[0]) return "the newest snapshot was not the one kept";
                    if (removed != files.Count - 1) return $"removed {removed} of {files.Count - 1}";
                    if (SettingsHistory.Delete(new[] { Path.Combine(Path.GetTempPath(), "other.json") }) != 0) return "a file outside the history folder was touched";
                    return null;
                }
                finally { s.ToastSeconds = old; Settings.Save(); }
            });

            Check("bulk: release notes export and outage removal keep what they must", () =>
            {
                var md = ListActions.ReleaseNotesMarkdown(new[] { new ReleaseNote { Tag = "v1", Name = "One", Body = "Fixed a thing." }, new ReleaseNote { Tag = "v2", Name = "", Body = "" } });
                if (!md.Contains("## One") || !md.Contains("Fixed a thing.") || !md.Contains("## v2") || !md.Contains("No notes were written")) return "the Markdown is missing parts";
                var done = new Outage { Start = DateTime.Now.AddMinutes(-9), End = DateTime.Now.AddMinutes(-5), Cause = "Selftest outage done" };
                var live = new Outage { Start = DateTime.Now.AddMinutes(-1), Cause = "Selftest outage ongoing" };
                Activity.Seed(Array.Empty<ActivityEntry>(), new[] { live, done });
                var removed = Activity.RemoveOutages(new[] { done, live });
                var left = Activity.RecentOutages(100);
                Activity.RemoveOutages(new[] { live });
                if (removed != 1 || left.Contains(done) || !left.Contains(live)) return "a finished outage should go and an ongoing one stay";
                return ListActions.OutageLine(done).Contains("Selftest outage done") ? null : "the outage line lacks its cause";
            });

            await CheckAsync("bulk: schedule rules and the activity page offer select mode that acts on the chosen rows", async () =>
            {
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                int before = Settings.Current.ScheduleRules.Count;
                try
                {
                    Settings.Current.ScheduleRules.Add(new ScheduleRule { Label = "Selftest A", Setting = "sounds", Value = "off" });
                    Settings.Current.ScheduleRules.Add(new ScheduleRule { Label = "Selftest B", Setting = "toasts", Value = "off" });
                    cc.Show();
                    await Task.Delay(700);
                    cc.Navigate("modes", animate: false);
                    await Task.Delay(600);
                    if (!Press(cc, "Select")) return "no Select button on the rules list";
                    await Task.Delay(200);
                    if (!Press(cc, "Select all shown")) return "no Select all shown after entering select mode";
                    if (!Press(cc, "Disable")) return "no Disable action on the bar";
                    await Task.Delay(200);
                    var mine = Settings.Current.ScheduleRules.Skip(before).ToList();
                    if (mine.Count != 2 || mine.Any(r => r.Enabled)) return "the chosen rules were not disabled";
                    Press(cc, "Done selecting");
                    Activity.Seed(new[] { new ActivityEntry { Title = "Selftest entry", Kind = ActivityKind.Files } }, new[] { new Outage { Start = DateTime.Now.AddMinutes(-8), End = DateTime.Now.AddMinutes(-4), Cause = "Selftest outage page" } });
                    cc.Navigate("activity", animate: false);
                    await Task.Delay(600);
                    var text = SearchScope.Text(cc);
                    if (!text.Contains("Selftest outage page")) return "the outage is not listed on the activity page";
                    var selects = Descend<ButtonBase>(cc).Count(b => ButtonLabel(b) == "Select" && b.IsVisible);
                    Activity.RemoveOutages(Activity.RecentOutages(100).Where(o => o.Cause == "Selftest outage page"));
                    return selects >= 2 ? null : "expected a Select button for the activity list and the outage list, found " + selects;
                }
                finally { if (Settings.Current.ScheduleRules.Count > before) Settings.Current.ScheduleRules.RemoveRange(before, Settings.Current.ScheduleRules.Count - before); cc.Close(); }
            });

            // ---------------------------------------------------------------- logo
            await CheckAsync("logo: validation, import, glyph choice, reset and the drawer header", async () =>
            {
                var dir = TempDir("logo");
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                try
                {
                    var png = MakePng(Path.Combine(dir, "ok.png"), 64, 64);
                    var tiny = MakePng(Path.Combine(dir, "tiny.png"), 8, 8);
                    var big = MakePng(Path.Combine(dir, "big.png"), 600, 600);
                    var fake = Path.Combine(dir, "fake.png"); File.WriteAllText(fake, "this is not an image");
                    var svg = Path.Combine(dir, "x.svg"); File.WriteAllText(svg, "<svg xmlns='http://www.w3.org/2000/svg'/>");
                    var huge = Path.Combine(dir, "huge.png"); File.WriteAllBytes(huge, new byte[] { 0x89, 0x50, 0x4E, 0x47 }.Concat(new byte[300 * 1024]).ToArray());
                    var ico = MakeIco(Path.Combine(dir, "ok.ico"), 48);
                    foreach (var (file, why) in new[] { (svg, "SVG"), (fake, "fake PNG"), (tiny, "tiny image"), (big, "oversized picture"), (huge, "oversized file"), (Path.Combine(dir, "none.png"), "missing file") })
                        if (LogoKit.Validate(file, out _) == null) return why + " was accepted";
                    if (LogoKit.Validate(png, out var ext) != null || ext != ".png") return "a good PNG was refused: " + LogoKit.Validate(png, out _);
                    if (LogoKit.Validate(ico, out ext) != null || ext != ".ico") return "a good ICO was refused: " + LogoKit.Validate(ico, out _);

                    cc.Show();
                    await Task.Delay(700);
                    var sig0 = LogoKit.Signature;
                    var tray0 = LogoKit.TrayBase();
                    if (LogoKit.Import(png) != null) return "import of a good PNG failed";
                    File.Delete(png);
                    if (!LogoKit.HasCustomImage || LogoKit.LoadImage() == null) return "the stored copy is not used after the original was deleted";
                    if (LogoKit.Signature == sig0) return "the signature did not change after import";
                    await Task.Delay(300);
                    var logo = (System.Windows.Shapes.Path)cc.FindName("Logo");
                    var panel = (Panel)logo.Parent;
                    var img = panel.Children.OfType<Image>().FirstOrDefault(i => (i.Tag as string) == "logo-custom");
                    if (img == null || img.Visibility != Visibility.Visible || logo.Visibility == Visibility.Visible) return "the drawer header does not show the custom image";
                    using (var tb = LogoKit.TrayBase())
                    {
                        var px = tb.GetPixel(16, 16);
                        if (px.A == 0 || Math.Abs((int)px.G - 0xC0) > 8) return "the tray base does not draw the custom image";
                    }
                    if (LogoKit.Import(ico) != null || Path.GetExtension(LogoKit.ImagePath) != ".ico") return "replacing the PNG with an ICO failed";
                    if (LogoKit.SetGlyph("nope") == null) return "an unknown glyph was accepted";
                    if (LogoKit.SetGlyph("shield") != null || LogoKit.HasCustomImage || LogoKit.GlyphId != "shield") return "choosing a glyph did not replace the image";
                    await Task.Delay(300);
                    if (logo.Visibility != Visibility.Visible || img.Visibility == Visibility.Visible) return "the header did not go back to a glyph";
                    string Hash(System.Drawing.Bitmap b) { var h = new HashCode(); for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) h.Add(b.GetPixel(x, y).ToArgb()); return h.ToHashCode().ToString(); }
                    using (var shield = LogoKit.TrayBase()) { LogoKit.SetGlyph("bolt"); using var bolt = LogoKit.TrayBase(); if (Hash(shield) == Hash(bolt)) return "two glyphs drew the same tray base"; }
                    LogoKit.Reset();
                    if (LogoKit.IsCustomised || LogoKit.GlyphId != "pulse") return "reset did not restore the default";
                    using (var back = LogoKit.TrayBase()) if (Hash(back) != Hash(tray0)) return "the default tray base changed after a reset";
                    App.Tray?.Refresh();
                    return null;
                }
                finally { LogoKit.Reset(); cc.Close(); }
            });

            // ---------------------------------------------------------------- export everything
            await CheckAsync("export everything: one zip with every dataset in five formats and no lock secrets", async () =>
            {
                var dir = TempDir("export");
                var s = Settings.Current;
                int sc = s.Shortcuts.Count, pc = s.SavedPatterns.Count, rc = s.ScheduleRules.Count;
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                try
                {
                    s.Shortcuts.Add(new Shortcut { Name = "selftest-export", Shell = ScriptShell.PowerShell, Description = "=formula-looking description" });
                    s.SavedPatterns.Add(new SavedPattern { Name = "Selftest pattern", Pattern = @"\d+" });
                    s.ScheduleRules.Add(new ScheduleRule { Label = "Selftest rule", Setting = "sounds", Value = "off" });
                    var lockErr = Locks.Create("selftest-page", "selftest-key", "Selftest lock", LockPolicy.Pin, 5, "73915", null, null);
                    if (lockErr != null) return "could not make a lock: " + lockErr;
                    var tables = ExportAll.SnapshotAll(false);
                    var names = tables.Select(t => t.Name).ToList();
                    foreach (var want in new[] { "Settings", "Shortcuts", "Regex library", "Appearance and looks", "Schedule rules", "Lock records (no secrets)", "DNS profiles", "Notifications", "Activity", "Settings history" })
                        if (!names.Contains(want)) return "missing the dataset " + want;
                    if (names.Contains("Clipboard history")) return "clipboard history was included without being asked";
                    var path = Path.Combine(dir, "everything.zip");
                    var res = ExportAll.WriteZip(path, tables);
                    if (res.Datasets != tables.Count || res.Files != 1 + tables.Count * 5) return $"expected {1 + tables.Count * 5} files, wrote {res.Files}";
                    using (var zip = ZipFile.OpenRead(path))
                    {
                        if (zip.Entries.Count != res.Files) return "the archive entry count differs";
                        foreach (var folder in new[] { "json/", "csv/", "markdown/", "html/", "text/" })
                            if (!zip.Entries.Any(e => e.FullName.StartsWith(folder))) return "no files in " + folder;
                        string Read(string n) { using var r = new StreamReader(zip.GetEntry(n).Open(), Encoding.UTF8); return r.ReadToEnd(); }
                        var all = string.Join("\n", zip.Entries.Where(e => e.FullName.EndsWith(".json") || e.FullName.EndsWith(".csv")).Select(e => Read(e.FullName)));
                        var entry = Locks.Find("selftest-page", "selftest-key");
                        if (all.Contains(entry.PinHash) || all.Contains(entry.PinSalt) || all.Contains("73915")) return "a lock secret reached the archive";
                        if (!Read("json/lock-records-no-secrets.json").Contains("Selftest lock")) return "the lock record is missing";
                        if (!Read("json/shortcuts.json").Contains("selftest-export")) return "the shortcut is missing";
                        if (!Read("csv/shortcuts.csv").Contains("'=formula-looking")) return "the CSV is not formula-safe";
                        if (!Read("json/regex-library.json").Contains("Selftest pattern")) return "the regex pattern is missing";
                        if (!Read("markdown/schedule-rules.md").Contains("Selftest rule")) return "the schedule rule is missing";
                        if (!Read("html/appearance-and-looks.html").Contains("Seed colour")) return "the appearance rows are missing";
                        if (!Read("text/dns-profiles.txt").Contains("Primary")) return "the DNS profiles are missing";
                    }
                    cc.Show();
                    await Task.Delay(700);
                    cc.Navigate("records", animate: false);
                    await Task.Delay(700);
                    return SearchScope.Text(cc).Contains("Export everything") && FindButton(cc, "Export everything...") != null ? null : "the Records page has no Export everything action";
                }
                finally
                {
                    if (s.Shortcuts.Count > sc) s.Shortcuts.RemoveRange(sc, s.Shortcuts.Count - sc);
                    if (s.SavedPatterns.Count > pc) s.SavedPatterns.RemoveRange(pc, s.SavedPatterns.Count - pc);
                    if (s.ScheduleRules.Count > rc) s.ScheduleRules.RemoveRange(rc, s.ScheduleRules.Count - rc);
                    cc.Close();
                }
            });

            // ---------------------------------------------------------------- drawing board
            await CheckAsync("drawing: strokes, undo, redo, PNG and SVG export on a real board window", async () =>
            {
                var dir = TempDir("drawing");
                var w = DrawingWindow.CreateBlankForTest();
                try
                {
                    await Task.Delay(500);
                    if (w.StrokeCountForTest != 0) return "a fresh board is not empty";
                    w.DrawStrokeForTest(100, 100, 500, 300);
                    w.DrawStrokeForTest(120, 400, 620, 150);
                    if (w.StrokeCountForTest != 2) return "strokes were not added";
                    w.UndoForTest();
                    if (w.StrokeCountForTest != 1) return "undo did not remove the last stroke";
                    w.RedoForTest();
                    if (w.StrokeCountForTest != 2) return "redo did not bring it back";
                    var svg = w.SvgForTest();
                    if (!svg.Contains("<svg") || !svg.Contains("<path")) return "the SVG has no drawing in it";
                    var png = Path.Combine(dir, "board.png");
                    var width = w.SavePngForTest(png);
                    var head = File.ReadAllBytes(png).Take(4).ToArray();
                    if (head[1] != 0x50 || head[2] != 0x4E || head[3] != 0x47) return "the PNG file is not a PNG";
                    return width >= 1000 ? null : "the PNG is only " + width + " pixels wide";
                }
                finally { w.Close(); }
            });
        }
    }
}
