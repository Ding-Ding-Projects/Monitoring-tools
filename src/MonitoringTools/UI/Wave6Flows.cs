using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI
{
    /// <summary>Takes a capture during a flow. The self-test passes null; the capture run supplies one.</summary>
    internal interface IWave6Snap
    {
        /// <summary>Renders a whole window.</summary>
        Task Window(Window w, string name, string caption);
        /// <summary>Renders a window with a popup card (a menu or the regex builder) composited beside <paramref name="anchor"/>.</summary>
        Task Popup(Window w, FrameworkElement card, FrameworkElement anchor, string name, string caption);
    }

    /// <summary>
    /// Flows that drive features in the built app through their real controls, shared by the self-test (assert) and the
    /// capture run (also photograph). Each returns null on success or a message saying what did not happen.
    /// </summary>
    internal static class Wave6Flows
    {
        private static bool _seeded;

        public static async Task Settle(Window w, int ms = 300)
        {
            await Task.Delay(ms);
            w?.UpdateLayout();
        }

        public static async Task Reveal(Window w, FrameworkElement e)
        {
            e?.BringIntoView();
            await Settle(w, 300);
        }

        private static void Seed()
        {
            if (_seeded) return;
            _seeded = true;
            Demo.SeedLivedIn();
        }

        private static async Task Snap(IWave6Snap snap, Window w, string name, string caption)
        {
            if (snap != null) await snap.Window(w, name, caption);
        }

        // ------------------------------------------------------------------ 1. element menus

        private static async Task<string> MenuOn(IWave6Snap snap, Window w, FrameworkElement target, string what, string name, string[] must, bool copyOnly)
        {
            ElementMenu.CloseLast();
            ElementMenu.ClearLabels();
            Drive.RightClick(target);
            await Task.Delay(450);
            var card = ElementMenu.LastCard;
            var labels = ElementMenu.LastLabels.ToList();
            if (card == null) return labels.Count > 0 ? $"{what}: the element menu opened and closed again at once" : $"{what}: the element menu did not open (handler saw {ElementMenu.DiagSeen} clicks, {ElementMenu.DiagTarget} with a target)";
            try
            {
                if (copyOnly)
                {
                    if (labels.Count != 1 || labels[0] != "Copy text") return $"{what}: the menu should offer only Copy text but offered: {string.Join(", ", labels)}";
                }
                foreach (var m in must)
                    if (!labels.Contains(m)) return $"{what}: the menu lacks \"{m}\" (offered: {string.Join(", ", labels)})";
                if (!copyOnly && !labels.Contains("Edit appearance…")) return $"{what}: no Edit appearance… entry";
                if (snap != null && name != null) await snap.Popup(w, card, target, name, $"Element menu on {what}" + (copyOnly ? ": Copy text only" : ""));
                return null;
            }
            finally { ElementMenu.CloseLast(); await Task.Delay(150); }
        }

        public static async Task<string> Menus(IWave6Snap snap)
        {
            var saved = Settings.Current.ToastSeconds;
            Settings.Current.ToastSeconds = 600;
            ElementMenu.KeepOpenForTest = true;
            var samples = Demo.EnsureSamples();
            var errs = new List<string>();
            async Task Window(Window w, string what, string name, string[] must, bool copyOnly, Func<ButtonBase> pick)
            {
                try
                {
                    await Settle(w, 900);
                    var btn = pick();
                    if (btn == null) { errs.Add(what + ": no button to right-click"); return; }
                    var err = await MenuOn(snap, w, btn, what, name, must, copyOnly);
                    if (err != null) errs.Add(err);
                }
                catch (Exception ex) { errs.Add(what + ": " + ex.GetType().Name + ": " + ex.Message); }
            }
            ButtonBase First(Window w) => Drive.All<ButtonBase>(w).FirstOrDefault(b => b.IsVisible && !string.IsNullOrWhiteSpace(Drive.LabelOf(b)));
            try
            {
                // a toast
                var toast = App.Toasts.ShowClip(ClipItem.FromText(Demo.SampleUrl));
                try
                {
                    toast.ExpandNow();
                    await Window(toast, "a toast", "menu-toast", new[] { "Dismiss", "Pause toasts for 15 minutes" }, false, () => Drive.All<ButtonBase>(toast).FirstOrDefault());
                }
                finally { toast.CloseAnimated(); await Task.Delay(400); }

                // the offline dialog
                var off = new OfflineWindow(Demo.SampleOutage());
                try { off.Show(); await Window(off, "the offline dialog", "menu-offline", new[] { "Copy text" }, false, () => First(off)); }
                finally { off.Close(); App.Toasts.ReservedBottom = 0; }

                // the setup wizard
                var wiz = new SetupWizardWindow();
                try { wiz.Show(); await Window(wiz, "the setup wizard", "menu-setup", new[] { "Copy text" }, false, () => First(wiz)); }
                finally { wiz.Close(); Settings.Current.OnboardingStep = 0; }

                // the confirmation window: Copy text only, so no entry can press its confirm control or step around its two keys
                var gate = new ConfirmWindow("Uninstall Monitoring Tools?", "This stops the background service, removes it from start-up and deletes the program.", "Uninstall", danger: true);
                try
                {
                    gate.Show();
                    await Window(gate, "the confirmation window", "menu-confirm", new[] { "Copy text" }, true, () => Drive.Find<ButtonBase>(gate, "Emergency exit"));
                    await Window(gate, "the confirmation window's first key", null, new[] { "Copy text" }, true, () => Drive.Find<ButtonBase>(gate, "First key"));
                    if (!gate.IsLoaded) errs.Add("the confirmation window closed from a menu");
                }
                finally { gate.Close(); }

                // file tools
                var ft = new FileToolsWindow(new[] { samples["image"], samples["csv"] });
                try { ft.Show(); await Window(ft, "file tools", "menu-filetools", new[] { "Copy text" }, false, () => First(ft)); }
                finally { ft.Close(); }
                return errs.Count == 0 ? null : string.Join("; ", errs);
            }
            finally { Settings.Current.ToastSeconds = saved; ElementMenu.KeepOpenForTest = false; ElementMenu.CloseLast(); }
        }

        // ------------------------------------------------------------------ 2. scheduled settings

        public static async Task<string> Schedule(IWave6Snap snap)
        {
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            var rules = Settings.Current.ScheduleRules;
            int baseline = rules.Count;
            try
            {
                cc.Show();
                await Settle(cc, 800);
                cc.Navigate("modes", animate: false);
                await Settle(cc, 500);
                Drive.Type(Drive.Need<TextBox>(cc, "Rule name"), "Selftest holiday");
                Drive.Type(Drive.Need<TextBox>(cc, "Start time"), "08:30");
                Drive.Type(Drive.Need<TextBox>(cc, "End time"), "12:00");
                Drive.Type(Drive.Need<TextBox>(cc, "Start date (optional)"), "2026-12-24");
                Drive.Type(Drive.Need<TextBox>(cc, "End date (optional)"), "2026-12-26");
                Drive.Press(cc, "Monday"); Drive.Press(cc, "Friday");
                Drive.Press(cc, "Theme"); await Settle(cc, 150);
                Drive.Press(cc, "Dark"); await Settle(cc, 150);
                Drive.Press(cc, "Add rule"); await Settle(cc, 400);
                if (rules.Count != baseline + 1) return $"the rule was not stored (rules {baseline} -> {rules.Count})";
                var rule = rules[^1];
                if (rule.Label != "Selftest holiday" || rule.Start != "08:30" || rule.End != "12:00" || rule.FromDate != "2026-12-24" || rule.ToDate != "2026-12-26")
                    return $"stored fields wrong: {rule.Label} {rule.Start}-{rule.End} {rule.FromDate}..{rule.ToDate}";
                if (rule.Setting != "theme" || rule.Value != "dark") return $"stored setting wrong: {rule.Setting}={rule.Value}";
                if (!rule.Days.SequenceEqual(new[] { 1, 5 })) return "stored days wrong: " + string.Join(",", rule.Days);
                var id = rule.Id;

                // a bad end date is refused with a message and stores nothing
                Drive.Type(Drive.Need<TextBox>(cc, "Start date (optional)"), "2026-12-30");
                Drive.Type(Drive.Need<TextBox>(cc, "End date (optional)"), "2026-12-01");
                Drive.Press(cc, "Add rule"); await Settle(cc, 250);
                if (rules.Count != baseline + 1) return "a range ending before it starts was stored";
                if (!Drive.TextOf(cc).Contains("The end date is before the start date")) return "no message for a reversed date range";

                // edit in place
                var edit = Drive.All<ButtonBase>(cc).First(b => b.IsVisible && Drive.LabelOf(b) == "Edit");
                Drive.Click(edit); await Settle(cc, 350);
                if (Drive.Need<TextBox>(cc, "Start date (optional)").Text != "2026-12-24") return "Edit did not load the rule's start date";
                if (Drive.Find<ButtonBase>(cc, "Save changes") == null) return "the form did not switch to Save changes";
                Drive.Type(Drive.Need<TextBox>(cc, "End time"), "13:15");
                Drive.Type(Drive.Need<TextBox>(cc, "End date (optional)"), "2026-12-31");
                await Reveal(cc, Drive.Need<ButtonBase>(cc, "Save changes"));
                await Snap(snap, cc, "modes-schedule-edit", "Scheduled settings: editing a rule with a date range in place");
                Drive.Press(cc, "Save changes"); await Settle(cc, 400);
                if (rules.Count != baseline + 1) return "saving an edit added a second rule";
                var after = rules.FirstOrDefault(r => r.Id == id);
                if (after == null || after.End != "13:15" || after.ToDate != "2026-12-31" || after.FromDate != "2026-12-24") return "the edited rule was not saved in place";
                if (!Modes.DescribeRule(after).Contains("2026-12-24 to 2026-12-31")) return "the rule description lacks the new range: " + Modes.DescribeRule(after);
                return null;
            }
            finally
            {
                while (rules.Count > baseline) rules.RemoveAt(rules.Count - 1);
                Modes.Evaluate();
                cc.Close();
            }
        }

        // ------------------------------------------------------------------ 3. bulk select

        private static string CountText(DependencyObject root)
        {
            foreach (var t in Drive.All<TextBlock>(root))
                if (Regex.IsMatch(t.Text ?? "", @"^\d+ selected$")) return t.Text;
            return null;
        }

        private static List<CheckBox> RowBoxes(DependencyObject root)
            => Drive.All<CheckBox>(root).Where(c => (AutomationProperties.GetName(c) ?? "").StartsWith("Select ")).ToList();

        private static async Task<string> Gate(string action, Func<DependencyObject> root, Func<int> count, bool complete, int expectRemoved)
        {
            int before = count();
            Drive.Press(root(), action);
            await Task.Delay(600);
            var dlg = Application.Current.Windows.OfType<ConfirmWindow>().LastOrDefault();
            if (dlg == null) return $"{action}: no confirmation window appeared";
            try
            {
                if (count() != before) return $"{action}: it acted before any confirmation";
                var slider = Drive.All<Slider>(dlg).FirstOrDefault();
                if (slider == null) return $"{action}: the confirmation has no two-key slider";
                var k1 = Drive.Need<ToggleButton>(dlg, "First key");
                var k2 = Drive.Need<ToggleButton>(dlg, "Second key");
                if (slider.IsEnabled) return $"{action}: the slider is usable with both keys off";
                slider.Value = 100; await Task.Delay(300);
                if (count() != before) return $"{action}: sliding with both keys off still confirmed";
                Drive.Click(k1); await Task.Delay(100);
                if (slider.IsEnabled) return $"{action}: one key was enough to unlock the slider";
                slider.Value = 100; await Task.Delay(300);
                if (count() != before) return $"{action}: sliding with one key still confirmed";
                Drive.Click(k2); await Task.Delay(100);
                if (!slider.IsEnabled) return $"{action}: both keys did not unlock the slider";
                if (!complete)
                {
                    Drive.Press(dlg, "Emergency exit"); await Task.Delay(300);
                    return count() == before ? null : $"{action}: Emergency exit still changed the list";
                }
                slider.Value = 0; slider.Value = 100;
                for (int i = 0; i < 30 && count() == before; i++) await Task.Delay(150);
                return count() == before - expectRemoved ? null : $"{action}: expected {before - expectRemoved} items left, found {count()}";
            }
            finally { if (dlg.IsLoaded) dlg.Close(); }
        }

        private static async Task<string> BulkList(IWave6Snap snap, Window host, Func<DependencyObject> root, Func<FrameworkElement> popupCard, FrameworkElement anchor,
            string list, string snapName, string[] actions, string danger, bool completeDanger, Func<int> count)
        {
            Drive.Press(root(), "Select"); await Settle(host, 450);
            var boxes = RowBoxes(root());
            if (boxes.Count < 2) return $"{list}: select mode showed {boxes.Count} row check boxes";
            if (CountText(root()) != "0 selected") return $"{list}: count reads \"{CountText(root())}\" before any choice";
            foreach (var a in actions)
                if (Drive.Need<ButtonBase>(root(), a).IsEnabled) return $"{list}: \"{a}\" is enabled with nothing selected";
            Drive.Click(boxes[0]); Drive.Click(boxes[1]); await Settle(host, 150);
            if (CountText(root()) != "2 selected") return $"{list}: after choosing two rows the count reads \"{CountText(root())}\"";
            foreach (var a in actions)
                if (!Drive.Need<ButtonBase>(root(), a).IsEnabled) return $"{list}: \"{a}\" stayed disabled with two rows chosen";
            Drive.Press(root(), "Select all shown"); await Settle(host, 150);
            if (CountText(root()) != boxes.Count + " selected") return $"{list}: Select all shown gave \"{CountText(root())}\" for {boxes.Count} rows";
            Drive.Press(root(), "Select none"); await Settle(host, 150);
            if (CountText(root()) != "0 selected") return $"{list}: Select none left \"{CountText(root())}\"";
            boxes = RowBoxes(root());
            Drive.Click(boxes[0]); Drive.Click(boxes[1]); await Settle(host, 200);
            if (snap != null)
            {
                if (popupCard != null) await snap.Popup(host, popupCard(), anchor, snapName, $"Select mode on {list}: two rows chosen");
                else await snap.Window(host, snapName, $"Select mode on {list}: two rows chosen");
            }
            if (danger != null)
            {
                var g = await Gate(danger, root, count, completeDanger, completeDanger ? 2 : 0);
                if (g != null) return $"{list}: {g}";
                await Settle(host, 450);
            }
            Drive.Press(root(), "Done selecting"); await Settle(host, 450);
            if (RowBoxes(root()).Count != 0) return $"{list}: leaving select mode kept the row check boxes";
            return null;
        }

        public static async Task<string> Bulk(IWave6Snap snap)
        {
            Seed();
            for (int i = 0; i < 3; i++) NotificationLog.Add("Notice", "Bulk drive notice " + (i + 1), "detail " + (i + 1));
            var patterns = Settings.Current.SavedPatterns;
            patterns.Clear();
            patterns.Add(new SavedPattern { Name = "ISO date", Pattern = @"\d{4}-\d{2}-\d{2}" });
            patterns.Add(new SavedPattern { Name = "IPv4 address", Pattern = @"\b(?:\d{1,3}\.){3}\d{1,3}\b" });
            patterns.Add(new SavedPattern { Name = "Windows path", Pattern = @"^[A-Za-z]:\\" });
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show();
                await Settle(cc, 800);

                cc.Navigate("activity", animate: false); await Settle(cc, 500);
                var err = await BulkList(snap, cc, () => cc, null, null, "activity", "bulk-activity", new[] { "Copy", "Export as text…" }, null, false, null);
                if (err != null) return err;

                cc.Navigate("records", animate: false); await Settle(cc, 600);
                err = await BulkList(snap, cc, () => cc, null, null, "notifications", "bulk-notifications", new[] { "Copy", "Remove" }, "Remove", true, () => NotificationLog.All().Count);
                if (err != null) return err;

                cc.Navigate("shortcuts", animate: false); await Settle(cc, 600);
                err = await BulkList(snap, cc, () => cc, null, null, "shortcuts", "bulk-shortcuts", new[] { "Duplicate", "Delete…" }, "Delete…", false, () => Settings.Current.Shortcuts.Count);
                if (err != null) return err;

                cc.Navigate("history", animate: false); await Settle(cc, 600);
                var field = cc.PageSearch;
                field.OpenBuilder("Library"); await Settle(cc, 600);
                if (field.BuilderCard == null) return "the regex builder did not open";
                err = await BulkList(snap, cc, () => field.BuilderCard, () => field.BuilderCard, field.Bar, "the regex Library", "bulk-library", new[] { "Export selected…", "Delete selected…" }, "Delete selected…", false, () => Settings.Current.SavedPatterns.Count);
                if (err != null) return err;
                return null;
            }
            finally { cc.Close(); }
        }

        // ------------------------------------------------------------------ 4. guided forms

        private static GuidedForm FormNamed(DependencyObject root, string name)
            => Drive.All<GuidedForm>(root).FirstOrDefault(f => AutomationProperties.GetName(f) == name);

        private static string StepOf(GuidedForm f) => AutomationProperties.GetItemStatus(f) ?? "";

        public static async Task<string> Guided(IWave6Snap snap)
        {
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            var s = Settings.Current;
            int profiles = s.DnsProfiles.Count;
            try
            {
                cc.Show();
                await Settle(cc, 800);

                // ---- DNS profile
                cc.Navigate("network", animate: false); await Settle(cc, 500);
                Drive.Press(cc, "Add a profile…"); await Settle(cc, 500);
                var form = FormNamed(cc, "Add a DNS profile");
                if (form == null) return "the DNS profile form did not open";
                if (!StepOf(form).StartsWith("Step 1 of 3")) return "DNS form starts at: " + StepOf(form);
                await Reveal(cc, form);
                await Snap(snap, cc, "guided-dns-1", "Add a DNS profile, step 1: name");
                Drive.Press(form, "Next"); await Settle(cc, 150);
                if (!StepOf(form).StartsWith("Step 1 of 3")) return "an empty name was allowed to continue";
                Drive.Type(Drive.Need<TextBox>(form, "Profile name"), "Selftest DNS");
                Drive.Press(form, "Next"); await Settle(cc, 300);
                if (!StepOf(form).StartsWith("Step 2 of 3")) return "step 2 not reached: " + StepOf(form);
                Drive.Type(Drive.Need<TextBox>(form, "Primary DNS server"), "1.1.1");
                Drive.Press(form, "Next"); await Settle(cc, 150);
                if (!StepOf(form).StartsWith("Step 2 of 3")) return "an invalid server address was allowed to continue";
                Drive.Type(Drive.Need<TextBox>(form, "Primary DNS server"), "203.0.113.53");
                Drive.Type(Drive.Need<TextBox>(form, "Secondary DNS server"), "198.51.100.53");
                if (Drive.TextOf(form).Contains("must be an IPv4 address")) return "the refusal message stayed on screen after the address was corrected";
                await Reveal(cc, form);
                await Snap(snap, cc, "guided-dns-2", "Add a DNS profile, step 2: servers");
                Drive.Press(form, "Next"); await Settle(cc, 300);
                if (!StepOf(form).StartsWith("Step 3 of 3")) return "step 3 not reached: " + StepOf(form);
                var review = Drive.TextOf(form);
                if (!review.Contains("Name: Selftest DNS") || !review.Contains("Primary: 203.0.113.53")) return "the review step does not summarise the answers: " + review;
                await Reveal(cc, form);
                await Snap(snap, cc, "guided-dns-3", "Add a DNS profile, step 3: review and save");
                Drive.Press(form, "Back"); await Settle(cc, 200);
                if (Drive.Need<TextBox>(form, "Primary DNS server").Text != "203.0.113.53") return "going Back lost what was typed";
                Drive.Press(form, "Next"); await Settle(cc, 200);
                Drive.Press(form, "Save profile"); await Settle(cc, 500);
                if (!s.DnsProfiles.Any(p => p.Name == "Selftest DNS" && p.Primary == "203.0.113.53" && p.Secondary == "198.51.100.53")) return "the DNS profile was not stored";

                // ---- new Minecraft server (the six-step create flow of the server manager)
                cc.Navigate("minecraft", animate: false); await Settle(cc, 900);
                cc.MinecraftManager.OpenCreate(); await Settle(cc, 500);
                GuidedForm F() => FormNamed(cc, "New server");   // the manager may rebuild itself when the version catalogue arrives, so the form is looked up again each time
                form = F();
                if (form == null) return "the new-server form is missing";
                var draft = UI.Minecraft.McManagerView.Draft;
                if (draft == null) return "the new-server draft is missing";
                draft.Version = null;
                if (!StepOf(F()).StartsWith("Step 1 of 6")) return "new server starts at: " + StepOf(F());
                await Reveal(cc, F());
                await Snap(snap, cc, "guided-minecraft-1", "New server, step 1: edition and version");
                Drive.Press(F(), "Next"); await Settle(cc, 150);
                if (!StepOf(F()).StartsWith("Step 1 of 6")) return "a server without a version was allowed to continue";
                draft.Version = "1.21.1";
                Drive.Press(F(), "Next"); await Settle(cc, 400);
                if (!StepOf(F()).StartsWith("Step 2 of 6")) return "step 2 not reached: " + StepOf(F());
                await Reveal(cc, F());
                await Snap(snap, cc, "guided-minecraft-2", "New server, step 2: software");
                Drive.Press(F(), "Next"); await Settle(cc, 400);
                if (!StepOf(F()).StartsWith("Step 3 of 6")) return "step 3 not reached: " + StepOf(F());
                Drive.Press(F(), "Next"); await Settle(cc, 400);
                if (!StepOf(F()).StartsWith("Step 4 of 6")) return "step 4 not reached: " + StepOf(F());
                Drive.Type(Drive.Need<TextBox>(F(), "Port"), "80");
                Drive.Press(F(), "Next"); await Settle(cc, 150);
                if (!StepOf(F()).StartsWith("Step 4 of 6")) return "a port below 1024 was allowed";
                Drive.Type(Drive.Need<TextBox>(F(), "Port"), "25577");
                await Reveal(cc, F());
                await Snap(snap, cc, "guided-minecraft-3", "New server, step 4: name, memory and port");
                Drive.Press(F(), "Next"); await Settle(cc, 400);
                if (!StepOf(F()).StartsWith("Step 5 of 6")) return "step 5 not reached: " + StepOf(F());
                Drive.Press(F(), "Next"); await Settle(cc, 400);
                if (!StepOf(F()).StartsWith("Step 6 of 6")) return "step 6 not reached: " + StepOf(F());
                if (!Drive.TextOf(F()).Contains("25577")) return "the review does not show the port";
                Drive.Press(F(), "Create server"); await Settle(cc, 300);
                if (!StepOf(F()).StartsWith("Step 6 of 6") || !Drive.TextOf(F()).Contains("Accept the EULA")) return "creating without accepting the EULA was not refused";
                Drive.Click(Drive.All<CheckBox>(F()).First());
                if (draft.Eula != true) return "the EULA switch did not register";
                await Reveal(cc, F());
                await Snap(snap, cc, "guided-minecraft-4", "New server, step 6: review and accept the EULA");
                if (ServerManager.Servers.Any(x => x.Port == 25577 && x.Version == "1.21.1")) return "a server was created without pressing Create server";
                Drive.Press(F(), "Cancel"); await Settle(cc, 400);
                return UI.Minecraft.McManagerView.Draft == null ? null : "Cancel kept the draft";
            }
            finally
            {
                s.DnsProfiles.RemoveAll(p => p.Name == "Selftest DNS");
                while (s.DnsProfiles.Count > profiles) s.DnsProfiles.RemoveAt(s.DnsProfiles.Count - 1);
                cc.Close();
            }
        }

        // ------------------------------------------------------------------ 5. ADHD Focus spotlight

        public static async Task<string> Spotlight(IWave6Snap snap)
        {
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show();
                await Settle(cc, 800);
                cc.Navigate("modes", animate: false); await Settle(cc, 500);
                var root = (UIElement)cc.Content;
                var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(root);
                if (layer == null) return "the window has no adorner layer";
                int Count() => layer.GetAdorners(root)?.Length ?? 0;
                int before = Count();
                var sw = Drive.All<CheckBox>(cc).FirstOrDefault(c => c.IsVisible && Drive.LabelOf(c) == "Focus spotlight");
                if (sw == null) return "the Focus spotlight switch is missing from the Modes page";
                Drive.Click(sw); await Settle(cc, 350);
                if (!Settings.Current.AdhdSpotlight) return "the switch did not turn the setting on";
                if (Count() != before + 1) return $"no dim layer after switching on (layers {before} -> {Count()})";
                var near = Drive.All<ButtonBase>(cc).FirstOrDefault(b => b.IsVisible && Drive.LabelOf(b) == "Turn on");
                near?.Focus();
                await Settle(cc, 500);
                await Snap(snap, cc, "adhd-spotlight", "Focus spotlight on: everything dims except the card under focus");
                Drive.Click(sw); await Settle(cc, 350);
                if (Settings.Current.AdhdSpotlight) return "the switch did not turn the setting off";
                if (Count() != before) return $"the dim layer was not removed (layers {before} -> {Count()})";
                return null;
            }
            finally { if (Settings.Current.AdhdSpotlight) FocusSpotlight.Set(false); cc.Close(); }
        }

        // ------------------------------------------------------------------ 6. School mode

        private static bool HasChoice(DependencyObject root, string label)
            => Drive.All<ButtonBase>(root).Any(b => b.IsVisible && Drive.LabelOf(b) == label);

        public static async Task<string> School(IWave6Snap snap)
        {
            var saved = Settings.Current.LanguageMode;
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                if (Modes.SchoolActive) return "School mode was already on at the start";
                cc.Show();
                await Settle(cc, 800);
                Settings.Current.LanguageMode = LanguageMode.Cantonese;
                Core.Language.Refresh();
                cc.Navigate("modes", animate: false); await Settle(cc, 500);
                var pin = Drive.Find<PasswordBox>(cc, "New PIN");
                var again = Drive.Find<PasswordBox>(cc, "Repeat PIN");
                if (pin == null || again == null) return "the PIN boxes are missing";
                pin.Password = "4826"; again.Password = "4826";
                Drive.Press(cc, "Turn on"); await Settle(cc, 700);
                if (!Modes.SchoolActive) return "the Turn on button did not switch School mode on";
                if (!Core.Language.IsEnglish) return "English is not forced while School mode is on";
                cc.Navigate("language", animate: false); await Settle(cc, 500);
                if (HasChoice(cc, "廣東話")) return "a Cantonese choice is still shown on the Language page";
                var text = Drive.TextOf(cc);
                if (text.Contains("English humour") || text.Contains("Cantonese humour")) return "a humour slider is still shown";
                if (!text.Contains("English only")) return "the Language page does not say the mode is English only";
                await Snap(snap, cc, "school-on-language", "School mode on: the Language page offers English only");

                cc.Navigate("modes", animate: false); await Settle(cc, 500);
                var secret = Drive.Find<PasswordBox>(cc, "PIN to turn off");
                if (secret == null) return "the turn-off PIN box is missing";
                secret.Password = "1111";
                Drive.Press(cc, "Turn off"); await Settle(cc, 500);
                if (!Modes.SchoolActive) return "a wrong PIN turned School mode off";
                secret = Drive.Need<PasswordBox>(cc, "PIN to turn off");
                secret.Password = "4826";
                Drive.Press(cc, "Turn off"); await Settle(cc, 700);
                if (Modes.SchoolActive) return "the right PIN did not turn School mode off";
                if (Core.Language.IsEnglish && Settings.Current.LanguageMode != LanguageMode.English) return "English stayed forced after School mode turned off";
                cc.Navigate("language", animate: false); await Settle(cc, 500);
                return HasChoice(cc, "廣東話") ? null : "the language choices did not return after School mode turned off";
            }
            finally
            {
                if (Modes.SchoolActive || Modes.PinIsSet) Modes.ResetSchoolRecord();
                Settings.Current.LanguageMode = saved;
                Core.Language.Refresh();
                cc.Close();
            }
        }

        // ------------------------------------------------------------------ 7. appearance editor

        private static Color Hex(string h) => (Color)ColorConverter.ConvertFromString(h);

        private static bool Near(Color a, Color b, int tol) => Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol;

        public static async Task<string> Appearance(IWave6Snap snap)
        {
            var host = new Window { Width = 560, Height = 320, Title = "Appearance drive" };
            var first = new Button { Content = "Sample action", Style = Ui.Style("Btn.Ghost"), Margin = new Thickness(20) };
            var second = new Button { Content = "Second action", Style = Ui.Style("Btn.Ghost"), Margin = new Thickness(20) };
            host.Content = new StackPanel { Children = { first, second } };
            string k1 = null, k2 = null;
            const string preset = "W6 drive look";
            try
            {
                host.Show();
                await Settle(host, 500);
                k1 = ElementStyles.KeyFor(ElementStyles.PageIdFor(host), first);
                k2 = ElementStyles.KeyFor(ElementStyles.PageIdFor(host), second);
                AppearanceEditorWindow.Open(host, first);
                await Settle(host, 600);
                var ed = Application.Current.Windows.OfType<AppearanceEditorWindow>().LastOrDefault();
                if (ed == null) return "the appearance editor did not open";

                // every tab
                foreach (var (tab, label) in new[] { ("font", "Font"), ("colour", "Colour"), ("shape", "Shape"), ("library", "Library") })
                {
                    Drive.Press(ed, label + " settings"); await Settle(ed, 400);
                    await Snap(snap, ed, "appearance-editor-" + tab, "Appearance editor: " + label + " tab");
                }

                // per-state style: the Hover state's text colour
                Drive.Press(ed, "Colour settings"); await Settle(ed, 300);
                Drive.Press(ed, "Hover state"); await Settle(ed, 250);
                var picker = Drive.All<ColorPicker>(ed).FirstOrDefault();
                if (picker == null) return "the colour tab has no colour picker";
                Drive.Type(Drive.Need<TextBox>(ed, "HEX value"), "#FF3366", enter: true); await Settle(ed, 250);
                var st = ElementStyles.Get(k1);
                if (st?.Hover?.Foreground == null || !Near(Hex(st.Hover.Foreground), Hex("#FF3366"), 1)) return "the Hover text colour was not stored: " + st?.Hover?.Foreground;
                if (st.Foreground != null) return "colouring the Hover state changed the normal look";
                Drive.Press(ed, "Pressed state"); await Settle(ed, 250);
                Drive.Type(Drive.Need<TextBox>(ed, "HEX value"), "#33CC99", enter: true); await Settle(ed, 250);
                st = ElementStyles.Get(k1);
                if (st?.Pressed?.Foreground == null || !Near(Hex(st.Pressed.Foreground), Hex("#33CC99"), 1)) return "the Pressed text colour was not stored";
                await Snap(snap, ed, "appearance-editor-states", "Appearance editor: a different colour for the Pressed state");

                // undo and redo
                var undo = Drive.Need<ButtonBase>(ed, "Undo appearance change");
                var redo = Drive.Need<ButtonBase>(ed, "Redo appearance change");
                if (!undo.IsEnabled) return "Undo is disabled after two changes";
                Drive.Click(undo); await Settle(ed, 250);
                if (ElementStyles.Get(k1)?.Pressed?.Foreground != null) return "Undo did not remove the Pressed colour";
                if (ElementStyles.Get(k1)?.Hover?.Foreground == null) return "Undo removed more than the last change";
                if (!redo.IsEnabled) return "Redo is disabled after Undo";
                Drive.Click(redo); await Settle(ed, 250);
                if (ElementStyles.Get(k1)?.Pressed?.Foreground == null) return "Redo did not bring the Pressed colour back";

                // presets: save from this element, apply to another
                Drive.Press(ed, "Library settings"); await Settle(ed, 300);
                Drive.Type(Drive.Need<TextBox>(ed, "Preset name"), preset);
                Drive.Press(ed, "Save preset"); await Settle(ed, 300);
                if (!ElementStyles.PresetNames.Contains(preset)) return "the preset was not saved";
                await Snap(snap, ed, "appearance-editor-presets", "Appearance editor: a saved preset in the Library tab");
                AppearanceEditorWindow.Open(host, second);
                await Settle(host, 600);
                ed = Application.Current.Windows.OfType<AppearanceEditorWindow>().LastOrDefault();
                Drive.Press(ed, "Library settings"); await Settle(ed, 300);
                Drive.Press(ed, "Apply preset " + preset); await Settle(ed, 300);
                var s2 = ElementStyles.Get(k2);
                if (s2?.Hover?.Foreground == null || !Near(Hex(s2.Hover.Foreground), Hex("#FF3366"), 1)) return "applying the preset did not give the second element the look";

                // export, remove, import: the looks come back, states included
                var json = ElementStyles.ExportJson();
                ElementStyles.Store(k1, null, false);
                if (ElementStyles.Get(k1) != null) return "the look was not removed before import";
                var err = ElementStyles.ImportJson(json, out var n, out _);
                if (err != null) return "import refused: " + err;
                if (ElementStyles.Get(k1)?.Pressed?.Foreground == null || ElementStyles.Get(k1)?.Hover?.Foreground == null) return "the imported look lost its per-state colours";
                if (n < 2) return "the import reported " + n + " looks";
                return null;
            }
            finally
            {
                foreach (var w in Application.Current.Windows.OfType<AppearanceEditorWindow>().ToList()) w.Close();
                ElementStyles.DeletePreset(preset);
                if (k1 != null) ElementStyles.Store(k1, null, false);
                if (k2 != null) ElementStyles.Store(k2, null, false);
                host.Close();
            }
        }

        // ------------------------------------------------------------------ 8. colour picker

        public static async Task<string> Picker(IWave6Snap snap)
        {
            var host = new Window { Width = 460, Height = 1020, Title = "Colour picker drive", Background = Ui.Res("B.Window") };
            var picker = new ColorPicker(Colors.Black) { Margin = new Thickness(16) };
            host.Content = new ScrollViewer { Content = picker, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            try
            {
                host.Show();
                await Settle(host, 600);
                var labels = new[] { "HEX", "RGB", "HSL", "HSV", "HWB", "CMYK", "OKLCH", "LAB", "LCH" };
                foreach (var (startHex, tol) in new[] { ("#FF8000", 1), ("#3D7BF2", 6) })
                {
                    var start = Hex(startHex);
                    picker.SetColor(start, false);
                    await Settle(host, 100);
                    var shown = labels.ToDictionary(l => l, l => Drive.Need<TextBox>(picker, l + " value").Text);
                    foreach (var l in labels)
                    {
                        picker.SetColor(Hex("#202428"), false);
                        await Settle(host, 60);
                        Drive.Type(Drive.Need<TextBox>(picker, l + " value"), shown[l], enter: true);
                        await Settle(host, 60);
                        var v = picker.Value;
                        int t = l is "HEX" or "RGB" or "OKLCH" or "LAB" or "LCH" ? Math.Min(tol, 3) : tol;
                        if (!Near(v, start, t)) return $"{l} \"{shown[l]}\" came back as {ElementStyles.Hex(v)} instead of {startHex}";
                        var hex = Drive.Need<TextBox>(picker, "HEX value").Text;
                        if (!Near(Hex(hex), start, t)) return $"after typing {l}, the HEX field reads {hex} instead of {startHex}";
                    }
                }
                // a value outside the display range is clipped and says so
                Drive.Type(Drive.Need<TextBox>(picker, "OKLCH value"), "oklch(0.7 0.4 150)", enter: true); await Settle(host, 150);
                if (!Drive.TextOf(picker).Contains("outside the sRGB range")) return "an out-of-range OKLCH value was not reported as clipped";
                // short hex and a bad value
                Drive.Type(Drive.Need<TextBox>(picker, "HEX value"), "#f80", enter: true); await Settle(host, 100);
                if (!Near(picker.Value, Hex("#FF8800"), 0)) return "#f80 did not expand to #FF8800";
                var before = picker.Value;
                Drive.Type(Drive.Need<TextBox>(picker, "RGB value"), "not a colour", enter: true); await Settle(host, 100);
                if (picker.Value != before) return "an unreadable RGB value changed the colour";
                if (!Drive.TextOf(picker).Contains("could not be read")) return "an unreadable value gave no message";
                picker.SetColor(Hex("#3D7BF2"), false);
                picker.SetColor(Hex("#3D7BF2"), false);
                await Settle(host, 200);
                await Snap(snap, host, "colour-picker", "Colour picker: every field shows the same colour in its own notation");
                return null;
            }
            finally { host.Close(); }
        }

        // ------------------------------------------------------------------ 9. local history

        public static async Task<string> History(IWave6Snap snap)
        {
            Seed();
            var cc = new ControlCentreWindow { Width = 1180, Height = 900 };
            try
            {
                cc.Show();
                await Settle(cc, 800);
                var sc = Settings.Current.Shortcuts.FirstOrDefault(x => x.Name == "backup") ?? Settings.Current.Shortcuts.FirstOrDefault();
                if (sc == null) return "no shortcut to edit";
                var key = ScriptHistory.ScriptKey(sc.Name);
                int before = ScriptHistory.List(key).Count;
                cc.Navigate("shortcuts", animate: false); await Settle(cc, 600);
                var open = Drive.All<ButtonBase>(cc).FirstOrDefault(b => b.IsVisible && Drive.LabelOf(b) == ":" + sc.Name);
                if (open != null) { Drive.Click(open); await Settle(cc, 500); }
                foreach (var text in new[] { "echo version one", "echo version two", "echo version three" })
                {
                    var code = Drive.All<TextBox>(cc).First(t => t.AcceptsReturn && t.IsVisible);
                    code.Text = text;
                    Drive.Press(cc, "Save"); await Settle(cc, 600);
                }
                var versions = ScriptHistory.List(key);
                if (versions.Count < before + 3) return $"three saves kept {versions.Count - before} versions";
                Drive.Press(cc, "History"); await Settle(cc, 600);
                var rows = Drive.All<ButtonBase>(cc).Where(b => b.IsVisible && (AutomationProperties.GetName(b) ?? "").StartsWith("Version saved")).ToList();
                if (rows.Count < 3) return $"the history panel lists {rows.Count} versions";
                // pick the saved version whose text is "version one"
                ButtonBase pick = null;
                foreach (var r in rows)
                {
                    Drive.Click(r); await Settle(cc, 150);
                    if (Drive.TextOf(cc).Contains("echo version one")) { pick = r; break; }
                }
                if (pick == null) return "no saved version shows the first text in its comparison";
                var diff = Drive.TextOf(cc);
                if (!diff.Contains("Restoring this version would add")) return "the comparison legend is missing";
                if (!diff.Contains("echo version three")) return "the comparison does not show the current text";
                var restore = Drive.Need<ButtonBase>(cc, "Restore this version");
                if (!restore.IsEnabled) return "Restore stayed disabled after choosing a version";
                await Reveal(cc, restore);
                await Snap(snap, cc, "history-panel", "Local history of a script: three saved versions and a comparison with the current text");
                Drive.Click(restore); await Task.Delay(500);
                var dlg = Application.Current.Windows.OfType<ConfirmWindow>().LastOrDefault();
                if (dlg == null) return "Restore did not ask for the two-key confirmation";
                bool gated = Drive.All<Slider>(dlg).Any();
                dlg.Close();
                return gated ? null : "the restore confirmation has no two-key gate";
            }
            finally { cc.Close(); }
        }

        // ------------------------------------------------------------------ 10. settings explain themselves

        /// <summary>Every switch and choice on every page, and on every setup step, must show a sentence saying what it does.</summary>
        public static async Task<string> Explain()
        {
            var bad = new List<string>();
            int switches = 0, choices = 0;
            var sw = Ui.Style("Switch");
            // A caption directly under the control (a wrapper's description line) counts as its visible subtitle.
            static bool CaptionBelow(FrameworkElement e)
            {
                if (LogicalTreeHelper.GetParent(e) is not Panel p) return false;
                int i = p.Children.IndexOf(e);
                return i >= 0 && i + 1 < p.Children.Count && p.Children[i + 1] is TextBlock t && !string.IsNullOrWhiteSpace(t.Text) && t.IsVisible;
            }
            void Scan(DependencyObject root, string where)
            {
                foreach (var cb in Drive.All<CheckBox>(root))
                {
                    if (cb.Style != sw || !cb.IsVisible) continue;
                    switches++;
                    var texts = Drive.All<TextBlock>(cb).Where(t => t.IsVisible && (!string.IsNullOrWhiteSpace(t.Text) || t.Inlines.Count > 0) && !Drive.Ancestors(t).TakeWhile(x => x != cb).Any(x => x is ButtonBase)).Select(t => t.Text).ToList();
                    string label = texts.FirstOrDefault() ?? Drive.LabelOf(cb) ?? "(unnamed)";
                    if (texts.Count < 2 && !CaptionBelow(cb)) bad.Add($"{where}: switch \"{Trim(label)}\"");
                }
                foreach (var sp in Drive.All<StackPanel>(root))
                {
                    var tag = Search.SearchScope.GetItem(sp);
                    if (tag == null || !tag.StartsWith("setting choice") || !sp.IsVisible) continue;
                    choices++;
                    var head = (sp.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<TextBlock>().FirstOrDefault()?.Text) ?? "(unnamed)";
                    var subs = sp.Children.OfType<TextBlock>().Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToList();
                    if (subs.Count == 0 && !CaptionBelow(sp)) bad.Add($"{where}: choice \"{Trim(head)}\"");
                }
            }
            static string Trim(string s) => s.Length > 50 ? s[..50] + "…" : s;

            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show();
                await Settle(cc, 800);
                foreach (var id in cc.SectionIds.ToList())
                {
                    cc.Navigate(id, animate: false);
                    await Settle(cc, 350);
                    Scan(cc, id);
                }
                // the Minecraft server manager: every section of a sample server, written only into the throwaway data folder
                MonitoringTools.Minecraft.McDemo.Seed();
                try
                {
                    cc.Navigate("minecraft", animate: false); await Settle(cc, 400);
                    var mcv = cc.MinecraftManager;
                    mcv.Select("sample-friends");
                    foreach (var (sec, _, _) in UI.Minecraft.McManagerView.Sections) { mcv.ShowSection(sec); await Settle(cc, 400); Scan(cc, "minecraft " + sec); }
                    foreach (var grp in MonitoringTools.Minecraft.McPropertyCatalog.Groups) { UI.Minecraft.McManagerView.SettingsGroup = grp; mcv.ShowSection("settings"); await Settle(cc, 300); Scan(cc, "minecraft settings " + grp); }
                    UI.Minecraft.McManagerView.SettingsGroup = "Gameplay";
                    UI.Minecraft.McManagerView.Draft = new UI.Minecraft.McCreateDraft { Version = "1.21.4" };
                    mcv.OpenCreate(); await Settle(cc, 500);
                    for (int i = 0; i < 6; i++) { Scan(cc, "minecraft create step " + (i + 1)); if (!SelfTest.Press(mcv.CreateForm, "Next")) break; await Settle(cc, 500); }
                    UI.Minecraft.McManagerView.Draft = null; mcv.CloseCreate();
                }
                finally { MonitoringTools.Minecraft.McDemo.Remove(); }
            }
            finally { cc.Close(); }
            var wiz = new SetupWizardWindow();
            try
            {
                wiz.Show();
                await Settle(wiz, 600);
                for (int i = 0; i <= 6; i++)
                {
                    wiz.GoTo(i);
                    await Settle(wiz, 250);
                    Scan(wiz, "setup step " + (i + 1));
                }
            }
            finally { wiz.Close(); Settings.Current.OnboardingStep = 0; }
            if (switches + choices < 20) return $"only {switches} switches and {choices} choices were found, so the walk is not covering the app";
            return bad.Count == 0 ? null : $"{bad.Count} of {switches} switches and {choices} choices have no visible subtitle: " + string.Join("; ", bad.Distinct());
        }
    }
}
