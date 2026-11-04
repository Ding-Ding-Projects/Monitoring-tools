using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Flows that drive the typed number fields through their real controls, shared by the self-test (assert) and the capture run.
    /// Each returns null on success or a message saying what did not happen.
    /// </summary>
    internal static class NumberFieldFlows
    {
        private static Task Settle(Window w, int ms = 300) => Wave6Flows.Settle(w, ms);

        private static NumberField Field(DependencyObject root, string label) => Drive.All<NumberField>(root).FirstOrDefault(f => f.Label == label && f.IsVisible);

        /// <summary>Raises a key press on the box the way the window would deliver it.</summary>
        private static void Press(TextBox box, Key key)
        {
            var src = PresentationSource.FromVisual(box);
            if (src == null) throw new InvalidOperationException("the box is not in a shown window");
            box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, src, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }

        private static async Task Reveal(Window w, FrameworkElement e) { e?.BringIntoView(); await Settle(w, 300); }

        // ------------------------------------------------------------------ no setting is a row of numeric presets

        /// <summary>
        /// Walks every control-centre page and every setup step. A choice whose every option starts with a number (2 s, 15 min, 1x, 20)
        /// is a row of presets that cannot be typed over, so it fails; the setting must use a number field. Also requires a minimum
        /// number of choices and number fields so the walk cannot pass by finding nothing.
        /// </summary>
        public static async Task<string> NoNumericPresets()
        {
            var bad = new List<string>();
            int choices = 0, fields = 0;
            var number = new Regex(@"^\s*[\d.,]+\s*\S{0,14}(\s+\S{1,12}){0,2}\s*$");
            void Scan(DependencyObject root, string where)
            {
                foreach (var f in Drive.All<NumberField>(root)) if (f.IsVisible) fields++;
                foreach (var sp in Drive.All<StackPanel>(root))
                {
                    var tag = Search.SearchScope.GetItem(sp);
                    if (tag == null || !tag.StartsWith("setting choice ") || tag.StartsWith("setting choice number field") || !sp.IsVisible) continue;
                    choices++;
                    var labels = sp.Children.OfType<WrapPanel>().SelectMany(w => w.Children.OfType<Button>()).Select(Drive.LabelOf).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                    if (labels.Count == 0) continue;
                    if (labels.All(l => char.IsDigit(l.Trim()[0]) && number.IsMatch(l)))
                    {
                        var head = sp.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? "(unnamed)";
                        bad.Add($"{where}: \"{head}\" offers only {string.Join(", ", labels)}");
                    }
                }
            }
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show();
                await Settle(cc, 800);
                foreach (var id in cc.SectionIds.ToList())
                {
                    cc.Navigate(id, animate: false);
                    await Settle(cc, 300);
                    Scan(cc, id);
                }
            }
            finally { cc.Close(); }
            var wiz = new SetupWizardWindow();
            try
            {
                wiz.Show();
                await Settle(wiz, 600);
                for (int i = 0; i <= 6; i++) { wiz.GoTo(i); await Settle(wiz, 250); Scan(wiz, "setup step " + (i + 1)); }
            }
            finally { wiz.Close(); Settings.Current.OnboardingStep = 0; }
            if (choices < 8) return $"only {choices} choices were found, so the walk is not covering the app";
            if (fields < 10) return $"only {fields} number fields were found, so the numeric settings were not converted";
            return bad.Count == 0 ? null : $"{bad.Count} choices are rows of numeric presets: " + string.Join("; ", bad);
        }

        // ------------------------------------------------------------------ the clipboard history size, driven by typing

        public static async Task<string> HistorySize(IWave6Snap snap = null)
        {
            var s = Settings.Current;
            int saved = s.HistorySize;
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                s.HistorySize = 50;
                for (int i = 0; i < 30; i++) App.History.Add(ClipItem.FromText("number field sample " + i));
                cc.Show();
                await Settle(cc, 800);
                cc.Navigate("clipboard", animate: false);
                await Settle(cc, 500);
                var f = Field(cc, "History size");
                if (f == null) return "the History size number field is not on the Clipboard page";
                if (f.Value != 50 || f.Box.Text != "50") return $"the field shows {f.Box.Text} for a stored 50";
                if (App.History.Items.Count < 30) return "the history was not filled for the check";

                Drive.Type(f.Box, "10", true);
                await Settle(cc, 150);
                if (s.HistorySize != 10 || f.Value != 10) return $"typing 10 and Enter left the setting at {s.HistorySize}";
                if (App.History.Items.Count != 10) return $"the history holds {App.History.Items.Count} copies after the size was lowered to 10";
                if (App.History.Items[0].Text == null || !App.History.Items[0].Text.EndsWith("29")) return "the newest copy was dropped instead of the oldest";
                if (f.Message.Length != 0) return "a message is shown after a valid entry: " + f.Message;

                foreach (var (typed, why) in new[] { ("5000", "above the largest size"), ("3", "below the smallest size"), ("abc", "not a number"), ("2.5", "not a whole number"), ("", "empty") })
                {
                    Drive.Type(f.Box, typed, true);
                    await Settle(cc, 100);
                    if (s.HistorySize != 10 || f.Value != 10) return $"typing \"{typed}\" ({why}) changed the setting to {s.HistorySize}";
                    if (!f.Message.StartsWith("Not changed")) return $"typing \"{typed}\" ({why}) showed no refusal message: \"{f.Message}\"";
                    if (App.History.Items.Count != 10) return $"typing \"{typed}\" changed the history length";
                }
                if (snap != null) { Drive.Type(f.Box, "5000", true); await Reveal(cc, f); await snap.Window(cc, "number-field-clipboard", "Clipboard: History size is a typed number field; an out-of-range entry is refused and the last good value stays"); }

                // steppers and keys
                Drive.Type(f.Box, "10", true);
                Drive.Click(Drive.Need<System.Windows.Controls.Primitives.ButtonBase>(cc, "Increase History size"));
                if (s.HistorySize != 15) return $"Increase moved the size to {s.HistorySize}, not 15";
                var down = Drive.Need<System.Windows.Controls.Primitives.ButtonBase>(cc, "Decrease History size");
                Drive.Click(down); Drive.Click(down);
                if (s.HistorySize != 5) return $"Decrease twice moved the size to {s.HistorySize}, not 5";
                if (f.Box.Text != "5") return "the box did not follow the stepper";
                Press(f.Box, Key.Up);
                if (s.HistorySize != 10) return $"the Up arrow moved the size to {s.HistorySize}, not 10";
                Press(f.Box, Key.PageUp);
                if (s.HistorySize != 60) return $"Page Up moved the size to {s.HistorySize}, not 60";
                Press(f.Box, Key.PageDown);
                if (s.HistorySize != 10) return $"Page Down moved the size to {s.HistorySize}, not 10";
                return null;
            }
            finally { s.HistorySize = saved; Settings.Save(); cc.Close(); }
        }

        // ------------------------------------------------------------------ decimals, rounding and the stored value of other fields

        public static async Task<string> Others()
        {
            var s = Settings.Current;
            double toast = s.ToastSeconds; int scale = s.TextScalePercent; double vol = s.SoundVolume; int probe = s.ProbeIntervalSeconds; string keep = s.OllamaKeepAlive;
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show();
                await Settle(cc, 800);
                cc.Navigate("clipboard", animate: false); await Settle(cc, 400);
                var t = Field(cc, "Toast duration");
                if (t == null) return "no Toast duration field";
                Drive.Type(t.Box, "2.5", true);
                if (Math.Abs(s.ToastSeconds - 2.5) > 1e-9) return $"a typed 2.5 s stored {s.ToastSeconds}";
                Drive.Type(t.Box, "0.2", true);
                if (Math.Abs(s.ToastSeconds - 2.5) > 1e-9 || !t.Message.StartsWith("Not changed")) return "a toast time under one second was accepted";
                var v = Field(cc, "Volume");
                if (v == null) return "no Volume field";
                Drive.Type(v.Box, "35", true);
                if (Math.Abs(s.SoundVolume - 0.35) > 1e-9) return $"a typed volume of 35 % stored {s.SoundVolume}";
                Drive.Type(v.Box, "101", true);
                if (Math.Abs(s.SoundVolume - 0.35) > 1e-9) return "a volume over 100 % was accepted";

                cc.Navigate("network", animate: false); await Settle(cc, 400);
                var checks = Drive.All<NumberField>(cc).Where(f => f.Label == "Check every" && f.IsVisible).ToList();
                if (checks.Count != 2) return $"the Network page has {checks.Count} Check every fields, not 2";
                int ip = s.PublicIpIntervalMinutes;
                foreach (var c in checks) Drive.Type(c.Box, "7", true);
                if (s.ProbeIntervalSeconds != 7 || s.PublicIpIntervalMinutes != 7) return $"a typed 7 stored probe {s.ProbeIntervalSeconds} and public IP {s.PublicIpIntervalMinutes}";
                s.PublicIpIntervalMinutes = ip;

                cc.Navigate("appearance", animate: false); await Settle(cc, 500);
                var z = Field(cc, "Text size");
                if (z == null) return "no Text size field";
                Drive.Type(z.Box, "105", true);
                if (s.TextScalePercent != 110 || !z.Message.StartsWith("Rounded to")) return $"a typed 105 % stored {s.TextScalePercent} (message \"{z.Message}\")";
                Drive.Type(z.Box, "200", true);
                if (s.TextScalePercent != 110) return "a text size of 200 % was accepted";

                cc.Navigate("ollama", animate: false); await Settle(cc, 500);
                var k = Drive.All<NumberField>(cc).FirstOrDefault(f => f.Label.StartsWith("Default keep-alive"));
                if (k == null) return "the keep-alive number field is not on the Ollama page";
                Drive.Type(k.Box, "45", true);
                if (s.OllamaKeepAlive != "45m") return $"a typed 45 min stored \"{s.OllamaKeepAlive}\"";
                Drive.Type(k.Box, "0", true);
                if (s.OllamaKeepAlive != "0") return $"a typed 0 stored \"{s.OllamaKeepAlive}\"";
                return null;
            }
            finally
            {
                s.ToastSeconds = toast; s.TextScalePercent = scale; s.SoundVolume = vol; s.ProbeIntervalSeconds = probe; s.OllamaKeepAlive = keep;
                Appearance.Apply(); Settings.Save(); cc.Close();
            }
        }

        // ------------------------------------------------------------------ a slider and its typed box follow each other

        public static async Task<string> SliderPairs()
        {
            var s = Settings.Current;
            int en = s.EnglishHumour; double rate = s.NarratorRate;
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show();
                await Settle(cc, 800);
                cc.Navigate("language", animate: false); await Settle(cc, 500);
                var box = Drive.All<NumberField>(cc).FirstOrDefault(f => f.Label == "English humour");
                var slider = Drive.All<Slider>(cc).FirstOrDefault(sl => System.Windows.Automation.AutomationProperties.GetName(sl) == "English humour");
                if (box == null || slider == null) return "the English humour slider has no typed box beside it";
                Drive.Type(box.Box, "5", true);
                if (s.EnglishHumour != 5 || Math.Round(slider.Value) != 5) return $"typing 5 left the setting at {s.EnglishHumour} and the slider at {slider.Value}";
                slider.Value = 2;
                if (box.Box.Text != "2" || s.EnglishHumour != 2) return "moving the slider did not update the box and the setting";
                Drive.Type(box.Box, "9", true);
                if (s.EnglishHumour != 2 || !box.Message.StartsWith("Not changed")) return "a humour level of 9 was accepted";
                var rb = Drive.All<NumberField>(cc).FirstOrDefault(f => f.Label == "Speaking rate");
                if (rb == null) return "the speaking rate has no typed box";
                Drive.Type(rb.Box, "1.5", true);
                if (Math.Abs(s.NarratorRate - 1.5) > 1e-9) return $"a typed rate of 1.5 stored {s.NarratorRate}";
                return null;
            }
            finally { s.EnglishHumour = en; s.NarratorRate = rate; Settings.Save(); cc.Close(); }
        }

        // ------------------------------------------------------------------ captures

        public static async Task Capture(IWave6Snap snap)
        {
            var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
            try
            {
                cc.Show();
                await Settle(cc, 800);
                cc.Navigate("modes", animate: false); await Settle(cc, 600);
                var f = Drive.All<NumberField>(cc).FirstOrDefault(x => x.Label.StartsWith("Reminder interval"));
                if (f != null) { await Reveal(cc, f); await snap.Window(cc, "number-field-modes", "Modes: the reminder interval is a typed number with steppers, a unit and a subtitle with its default"); }
                cc.Navigate("safety", animate: false); await Settle(cc, 600);
                var l = Drive.All<NumberField>(cc).FirstOrDefault(x => x.Label.StartsWith("New locks stay unlocked"));
                if (l != null) { await Reveal(cc, l); await snap.Window(cc, "number-field-safety", "Safety: the default unlock time is typed in minutes, with 0 meaning until the app closes"); }
            }
            finally { cc.Close(); }
        }
    }
}
