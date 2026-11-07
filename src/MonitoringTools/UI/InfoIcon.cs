using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A small information button that opens a non-modal panel next to a control: what the setting does, its current value,
    /// its real default, and an isolated sample to try. The sample never changes a real setting.
    /// </summary>
    public static class InfoIcon
    {
        /// <summary>Hand-written explanations and defaults for the settings the app has, keyed by label (or "label|first option" when a label is reused).</summary>
        private static readonly Dictionary<string, (string What, Func<Settings, string> Default)> Registry = Build();

        private static string OnOff(bool b) => b ? "On" : "Off";

        private static Dictionary<string, (string, Func<Settings, string>)> Build()
        {
            var d = new Dictionary<string, (string, Func<Settings, string>)>(StringComparer.OrdinalIgnoreCase);
            void Add(string key, string what, Func<Settings, string> def) => d[key] = (what, def);
            Add("Clipboard toasts", "Shows a small card in the corner of the screen every time something is copied, with actions for that item.", s => OnOff(s.ClipboardEnabled));
            Add("Show clipboard toasts", "Shows a small card in the corner of the screen every time something is copied.", s => OnOff(s.ClipboardEnabled));
            Add("Keep open on hover", "Resting the pointer on a toast holds it on screen and reveals its action buttons. Without it a toast always fades on schedule.", s => OnOff(s.KeepOpenOnHover));
            Add("Keep open while hovered", "Resting the pointer on a toast holds it on screen and reveals its action buttons.", s => OnOff(s.KeepOpenOnHover));
            Add("Render Markdown", "Shows headings, lists, code and links in the toast preview as formatted text instead of raw characters.", s => OnOff(s.RenderMarkdown));
            Add("Skip password managers", "Copies that the source app marks as private (password managers do this) never produce a toast and never reach history.", s => OnOff(s.IgnoreSensitiveSources));
            Add("Skip copies from password managers", "Copies that the source app marks as private never produce a toast and never reach history.", s => OnOff(s.IgnoreSensitiveSources));
            Add("Quick commands", "Lets a copied path or text that ends in a colon command (for example :mp4 or :push, or one of your own) run that command as soon as it lands on the clipboard.", s => OnOff(s.QuickCommandsEnabled));
            Add("Toast duration", "How many seconds a toast stays on screen before it fades.", s => s.ToastSeconds + " s");
            Add("History size", "How many recent copies are kept in memory for the History page. Quitting the app clears them.", s => s.HistorySize.ToString());
            Add("Sound effects", "Plays a short cue when an action finishes or fails, or when the connection drops and returns. Cues stay quiet during Windows quiet time, presentations and games.", s => OnOff(s.SoundsEnabled));
            Add("Volume", "How loud the sound effects are. It does not change the Windows volume.", s => Math.Round(s.SoundVolume * 100) + " %");
            Add("Network alerts", "Shows a dialog when the internet connection drops and keeps it up until the line comes back or you dismiss it.", s => OnOff(s.NetworkEnabled));
            Add("Alert me when the internet drops", "Shows a dialog when the internet connection drops.", s => OnOff(s.NetworkEnabled));
            Add("Check every|2 s", "How often the app tests the connection. Shorter notices a drop sooner and makes slightly more small requests.", s => s.ProbeIntervalSeconds + " s");
            Add("Check every|1 min", "How often the app looks up your public IP address. It also checks right after the network changes.", s => s.PublicIpIntervalMinutes + " min");
            Add("Alert after", "How many failed checks in a row are needed before the offline dialog appears. Higher numbers avoid false alarms from a single dropped check.", s => s.FailuresBeforeAlert == 1 ? "1 failed check" : s.FailuresBeforeAlert + " in a row");
            Add("Suppress while gaming", "Holds toasts and dialogs back while a listed game is running, and shows them afterwards.", s => OnOff(s.GameModeEnabled));
            Add("Hold popups while a game runs", "Holds toasts and dialogs back while a listed game is running.", s => OnOff(s.GameModeEnabled));
            Add("Any fullscreen app counts", "Treats any fullscreen window (exclusive-fullscreen games, fullscreen video, presentations) like a game, even if it is not on the game list.", s => OnOff(s.GameModeFullscreen));
            Add("Start with Windows", "Starts the background service at sign-in so the clipboard and network watchers run without opening the app.", s => OnOff(s.StartWithWindows));
            Add("Start when I sign in", "Starts the background service at sign-in so the watchers run without opening the app.", s => OnOff(s.StartWithWindows));
            Add("Watch the public IP", "Checks your public IP address every few minutes and right after the network changes, and records when it changes.", s => OnOff(s.PublicIpEnabled));
            Add("Watch my public IP address", "Checks your public IP address and shows a dialog on every change.", s => OnOff(s.PublicIpEnabled));
            Add("Look", "The whole style of every window: type, shapes, outlines and depth. Graphite, Instrument, Blueprint or Pop.", s => "Graphite");
            Add("Theme", "Light, dark, or follow the Windows app theme.", s => "System");
            Add("Seed colour", "The colour the whole Material 3 palette is generated from.", s => "Mint");
            Add("High-contrast outlines", "Draws dividers and borders with a stronger outline colour so edges are easier to see.", s => OnOff(s.HighContrastOutlines));
            Add("Text size", "Scales all text and spacing together. Layout reflows to fit.", s => "100 %");
            Add("Density", "Comfortable keeps generous spacing; compact fits more on screen.", s => "Comfortable");
            Add("Reduce motion", "Follow Windows uses the system animation setting; On removes fades and slides; Off always animates.", s => "Follow Windows");
            Add("Logo tint", "Whether the logo takes the seed colour or a custom colour.", s => "Match seed colour");
            Add("Emoji in dialogs", "Shows emoji at the start and end of toast and dialog titles. Turn it off to strip them.", s => "On");
            Add("Read toasts aloud", "Speaks toast titles with the Windows voice you pick. Quiet in game mode and when snoozed.", s => "Off");
            Add("Focus", "Messages that are not urgent go quietly to History instead of popping up. Failures and lost connections still show.", s => "Off");
            Add("Low stimulation", "No fade or slide motion, and only warnings and failures make a sound. It adds to the Windows animation setting and never overrides it.", s => "Off");
            Add("Time awareness", "Running jobs show how long they have taken, and a plain reminder appears at the interval you choose.", s => "Off");
            Add("One thing at a time", "Only one popup is on screen at once. The rest wait their turn and also appear in History.", s => "Off");
            Add("Momentum", "Reopens this window on the page you last used.", s => "Off");
            Add("Show a console window", "Lets the script's console window appear while it runs. Off runs it hidden.", s => "Off");
            Add("Run as administrator", "Runs the script with administrator rights, which prompts for consent once per run unless the service already has them.", s => "Off");
            Add("Runs with", "Whether the script is a PowerShell script (.ps1) or a batch file (.bat). The new-shortcut guide starts with PowerShell.", s => "PowerShell");
            return d;
        }

        private static (string What, string Default) Lookup(string label, string firstOption, string sub)
        {
            var s = new Settings();
            if (firstOption != null && Registry.TryGetValue(label + "|" + firstOption, out var a)) return (a.What, SafeDefault(a.Default, s));
            if (Registry.TryGetValue(label, out var b)) return (b.What, SafeDefault(b.Default, s));
            var what = !string.IsNullOrWhiteSpace(sub) ? sub : $"Changes the \"{label}\" setting.";
            return (what, null);
        }

        private static string SafeDefault(Func<Settings, string> f, Settings s) { try { return f(s); } catch { return null; } }

        /// <summary>The info button for an on/off switch.</summary>
        public static Button ForSwitch(string label, string sub, Func<bool> current)
        {
            var (what, def) = Lookup(label, null, sub);
            return Create(label, what, () => OnOff(current()), def, () =>
            {
                var cb = new CheckBox { Style = Ui.Style("Switch"), Tag = Ui.Res("B.Mint"), IsChecked = def == "On", Margin = new Thickness(0, 4, 0, 0) };
                cb.Content = new TextBlock { Text = "Sample switch", Foreground = Ui.Res("B.Text"), FontSize = 12 };
                return cb;
            });
        }

        /// <summary>The info button for a row of choices.</summary>
        public static Button ForChoice<T>(string label, IReadOnlyList<(string Label, T Value)> options, Func<T> current)
        {
            var (what, def) = Lookup(label, options.Count > 0 ? options[0].Label : null, null);
            string Now() => options.Where(o => EqualityComparer<T>.Default.Equals(o.Value, current())).Select(o => o.Label).FirstOrDefault() ?? "";
            return Create(label, what, Now, def, () =>
            {
                var wrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                var chips = new List<Button>();
                int pick = Math.Max(0, options.ToList().FindIndex(o => o.Label == def));
                void Paint() { for (int i = 0; i < chips.Count; i++) { chips[i].Background = i == pick ? Ui.Res("B.Mint") : Ui.Res("B.Raised"); chips[i].Foreground = i == pick ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft"); } }
                for (int i = 0; i < options.Count; i++)
                {
                    int idx = i;
                    var b = Ui.Button(options[i].Label, null, "Btn.Chip");
                    b.Click += (s, e) => { pick = idx; Paint(); };
                    chips.Add(b); wrap.Children.Add(b);
                }
                Paint();
                return wrap;
            });
        }

        /// <summary>Builds an information button. <paramref name="sample"/> builds a fresh, isolated sample each time the panel opens.</summary>
        public static Button Create(string target, string explanation, Func<string> current, string defaultText, Func<FrameworkElement> sample = null)
        {
            var b = new Button
            {
                Style = Ui.Style("Btn.Icon"), Width = 36, Height = 36, Margin = new Thickness(4, -6, 0, -6), VerticalAlignment = VerticalAlignment.Center,
                Content = Ui.Icon(Icons.Help, Ui.Res("B.TextMute"), 16), ToolTip = "About " + target, Focusable = true
            };
            AutomationProperties.SetName(b, "About " + target);
            AutomationProperties.SetHelpText(b, "Opens an explanation of this setting, its default and a sample you can try.");
            AutomationProperties.SetItemStatus(b, "collapsed");
            Popup open = null;
            b.Click += (s, e) =>
            {
                e.Handled = true;
                if (open != null && open.IsOpen) { open.IsOpen = false; return; }
                open = Panel(b, target, explanation, current?.Invoke(), defaultText, sample);
                open.Closed += (o, a) => { AutomationProperties.SetItemStatus(b, "collapsed"); if (!b.IsKeyboardFocusWithin) b.Focus(); };
                AutomationProperties.SetItemStatus(b, "expanded");
                open.IsOpen = true;
            };
            return b;
        }

        private static Popup Panel(Button anchor, string target, string explanation, string current, string defaultText, Func<FrameworkElement> sample)
        {
            var sp = new StackPanel { MaxWidth = 340 };
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = target, Style = Ui.Style("T.Title"), FontSize = 14, TextWrapping = TextWrapping.Wrap });
            var close = new Button { Style = Ui.Style("Btn.Icon"), Width = 32, Height = 32, Content = Ui.Icon(Icons.Close, Ui.Res("B.TextSoft"), 14) };
            AutomationProperties.SetName(close, "Close explanation");
            Grid.SetColumn(close, 1); head.Children.Add(close);
            sp.Children.Add(head);
            sp.Children.Add(new TextBlock { Text = explanation, Style = Ui.Style("T.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Foreground = Ui.Res("B.TextSoft") });
            if (current != null || defaultText != null)
            {
                string line = defaultText == null ? $"Current value: {current}. This setting has no recorded default yet."
                    : $"Current value: {current}. Default: {defaultText}.{(string.Equals(current, defaultText, StringComparison.OrdinalIgnoreCase) ? " It is at its default." : " You have changed it from the default.")}";
                sp.Children.Add(new TextBlock { Text = line, Style = Ui.Style("T.Body"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Foreground = Ui.Res("B.Text") });
            }
            if (sample != null)
            {
                sp.Children.Add(Ui.Mono("TRY IT (SAMPLE ONLY, CHANGES NOTHING)", Ui.Res("B.TextMute"), 10));
                ((FrameworkElement)sp.Children[sp.Children.Count - 1]).Margin = new Thickness(0, 10, 0, 0);
                sp.Children.Add(sample());
            }
            var card = new Border
            {
                Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(20), Padding = new Thickness(16), Margin = new Thickness(0, 0, 18, 18), Child = sp,
                Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"]
            };
            AutomationProperties.SetName(card, "About " + target);
            var pop = new Popup { Child = card, StaysOpen = false, AllowsTransparency = true, PlacementTarget = anchor, Placement = PlacementMode.Bottom, Focusable = true };
            close.Click += (s, e) => pop.IsOpen = false;
            pop.Opened += (s, e) => close.Focus();
            card.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { e.Handled = true; pop.IsOpen = false; } };
            return pop;
        }
    }
}
