using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;
using Lang = MonitoringTools.Core.Language;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The modes page: School mode, ADHD modes, the dim sum surprise, scheduled settings and an external settings file.
        private FrameworkElement ModesPage()
        {
            var root = PageRoot("Settings", "Modes", "School mode, ADHD modes, the dim sum surprise and scheduled settings. Each control says what it does and what it starts as.");

            var externalStatus = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) };
            void PaintExternal()
            {
                externalStatus.Text = Modes.ExternalStatus;
                externalStatus.Foreground = Ui.Res(Modes.ExternalOk ? "B.TextMute" : "B.Amber");
            }
            PaintExternal();

            root.Children.Add(ModesSchoolCard());
            root.Children.Add(ModesAdhdCard());
            if (!Modes.Blocks(ModeFeature.DimSum)) root.Children.Add(ModesDimSumCard());
            var refreshRules = new List<Action>();
            root.Children.Add(ModesScheduleCard(refreshRules));
            root.Children.Add(ModesExternalCard(externalStatus, PaintExternal));

            // Follow changes made elsewhere (another app, the schedule, the file) while this page is open.
            string Snapshot() => $"{Modes.SchoolActive}|{Modes.SchoolName}|{Modes.SchoolRecordError}";
            var shape = Snapshot();
            Action changed = () =>
            {
                if (!root.IsLoaded) return;
                if (Snapshot() != shape) { Navigate("modes", animate: false); return; }
                if (_spotlightSwitch != null && _spotlightSwitch.IsChecked != Settings.Current.AdhdSpotlight) _spotlightSwitch.IsChecked = Settings.Current.AdhdSpotlight;
                PaintExternal();
                foreach (var r in refreshRules) r();
            };
            root.Loaded += (s, e) => Modes.Changed += changed;
            root.Unloaded += (s, e) => Modes.Changed -= changed;
            return root;
        }

        private static TextBlock ModesNote(string text, string brush = "B.TextMute", double size = 12)
            => new TextBlock { Text = text, Style = Ui.Style("T.Body"), FontSize = size, Foreground = Ui.Res(brush), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) };

        private static PasswordBox ModesPin(string name, int maxLength = 12)
        {
            // A Material 3 filled field with its label above the box, so each PIN or password box says what it is for.
            var p = new PasswordBox
            {
                Width = maxLength > 12 ? 280 : 200, MaxLength = maxLength, Margin = new Thickness(0, 0, 12, 8), HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Mint"), FontSize = 15, Tag = name
            };
            p.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='PasswordBox'>" +
                "<StackPanel><TextBlock Text='{TemplateBinding Tag}' TextWrapping='Wrap' FontSize='12' Margin='4,0,0,4' Foreground='{DynamicResource B.TextSoft}' FontFamily='{DynamicResource F.Body}'/>" +
                "<Border x:Name='Bd' Height='44' CornerRadius='12' Background='{DynamicResource B.Sunken}' BorderBrush='Transparent' BorderThickness='2' Padding='12,0'>" +
                "<ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/></Border></StackPanel>" +
                "<ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Bd' Property='BorderBrush' Value='{DynamicResource B.Mint}'/></Trigger></ControlTemplate.Triggers>" +
                "</ControlTemplate>");
            AutomationProperties.SetName(p, name);
            return p;
        }

        // -------------------------------------------------------------- School mode

        private FrameworkElement ModesSchoolCard()
        {
            string n = Modes.SchoolName;
            var sp = new StackPanel();
            sp.Children.Add(CardTitle(n, "A shared switch. While it is on: game mode stays on and cannot be switched off, sounds are muted, quick commands that push to git are paused, and the app shows English only, with Cantonese, bilingual, humour levels, personal wording and the dim sum surprise left out entirely. It is turned off with the PIN or the password you set. Starts off."));

            if (Modes.SchoolRecordError != null)
            {
                sp.Children.Add(ModesNote(Modes.SchoolRecordError, "B.Amber"));
                sp.Children.Add(Ui.Button("Reset the shared record", () => { Modes.ResetSchoolRecord(); Navigate("modes", animate: false); }, "Btn.Alert"));
                sp.Children.Add(ModesNote("Resetting deletes the record, which turns the mode off and forgets the name, PIN and password."));
            }
            if (Modes.SchoolWatchError != null) sp.Children.Add(ModesNote(Modes.SchoolWatchError, "B.Amber"));

            var result = ModesNote("");
            void Say(string text) { result.Text = text; result.Foreground = Ui.Res("B.Amber"); Lang.Refresh(result); }
            bool on = Modes.SchoolActive;
            sp.Children.Add(new TextBlock { Text = on ? "Status: on" : "Status: off", Foreground = Ui.Res(on ? "B.Mint" : "B.TextSoft"), FontFamily = Ui.Font("F.Body"), FontSize = 14, Margin = new Thickness(0, 0, 0, 8) });

            // Rename
            var nameBox = Input(n, 260, "The name every surface will show. Defaults to \"" + Modes.DefaultSchoolName + "\".");
            AutomationProperties.SetName(nameBox, "Mode name");
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(nameBox);
            nameRow.Children.Add(Ui.Button("Rename", () =>
            {
                if (Modes.RenameSchool(nameBox.Text, out var err)) Navigate("modes", animate: false);
                else Say(err);
            }));
            sp.Children.Add(nameRow);
            sp.Children.Add(ModesNote("Rename it to anything up to 60 characters. After a rename only the new name appears in this app."));

            if (Modes.SchoolRecordError == null)
            {
                if (!on)
                {
                    PasswordBox pin = null, again = null, pw = null, pwAgain = null;
                    if (!Modes.CredentialIsSet)
                        sp.Children.Add(ModesNote("Choose a way to turn the mode off: a PIN of 4 to 12 digits, a password of at least 8 characters, or both. Either one works later. They are stored only as salted PBKDF2-SHA256 hashes (200,000 rounds)."));
                    if (!Modes.PinIsSet)
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal };
                        pin = ModesPin("New PIN"); again = ModesPin("Repeat PIN");
                        row.Children.Add(pin); row.Children.Add(again);
                        sp.Children.Add(row);
                    }
                    if (!Modes.PasswordIsSet)
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal };
                        pw = ModesPin("New password (8 or more characters)", Modes.PasswordMaxLength); pwAgain = ModesPin("Repeat password", Modes.PasswordMaxLength);
                        row.Children.Add(pw); row.Children.Add(pwAgain);
                        sp.Children.Add(row);
                    }
                    sp.Children.Add(Ui.Button("Turn on", () =>
                    {
                        if (Modes.EnableSchool(pin?.Password, again?.Password, pw?.Password, pwAgain?.Password, out var err)) Navigate("modes", animate: false);
                        else Say(err);
                    }, "Btn.Accent", Icons.Shield));
                }
                else
                {
                    var word = Modes.CredentialWord;
                    var secret = ModesPin(word == "PIN" ? "PIN to turn off" : word == "password" ? "Password to turn off" : "PIN or password to turn off", Modes.PasswordMaxLength);
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(secret);
                    var off = Ui.Button("Turn off", () =>
                    {
                        if (Modes.DisableSchool(secret.Password, out var err)) Navigate("modes", animate: false);
                        else { secret.Clear(); Say(err); }
                    }, "Btn.Alert");
                    row.Children.Add(off);
                    sp.Children.Add(row);
                    sp.Children.Add(ModesNote("Five wrong tries start a wait that doubles with each further wrong try, up to 15 minutes. It is a pause, never a permanent lock."));
                    if (Modes.LockSeconds > 0) { result.Text = $"Locked for another {Modes.LockSeconds} seconds."; result.Foreground = Ui.Res("B.Amber"); }
                }
            }
            sp.Children.Add(result);
            if (Modes.SchoolRecordError == null && Modes.CredentialIsSet) sp.Children.Add(ModesHelloRow(on, Say));

            sp.Children.Add(ModesNote("This is a user-experience lock, not a security boundary. There is no way to recover a forgotten PIN or password: close the app and delete the shared record below, which turns the mode off and forgets its name, PIN and password. Anyone with this computer can do the same.", "B.TextMute"));
            // Shown with the profile folder as a variable so the user name does not appear on screen.
            var shownPath = Modes.SchoolRecordPath.Replace(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%");
            sp.Children.Add(Ui.Mono(shownPath, Ui.Res("B.TextMute"), 11));
            var open = Ui.Button("Open the record's folder", () => ActionProvider.Shell(System.IO.Path.GetDirectoryName(Modes.SchoolRecordPath)));
            open.Margin = new Thickness(0, 8, 8, 0);
            sp.Children.Add(open);
            return SearchScope.SetItem(Card(sp, on ? "#FF3DD6A6" : null), n + " lock pin shared english parental");
        }

        // -------------------------------------------------------------- ADHD modes

        private CheckBox _spotlightSwitch;

        private FrameworkElement ModesAdhdCard()
        {
            var s = Settings.Current;
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("ADHD modes", "Five independent modes and a Focus spotlight, each off by default. Turn on any mix. They are aids for focus, not medical advice, and they never hide anything you cannot get back with one click."));
            sp.Children.Add(Switch("Focus", "Off by default. Messages that are not urgent (a copied item, an address change) go quietly to History instead of popping up. Failures and lost connections still show.",
                s.AdhdFocus, v => { s.AdhdFocus = v; Modes.NotifyChanged(); }, "B.Violet"));
            sp.Children.Add(Switch("Low stimulation", "Off by default. No fade or slide motion, and only warnings and failures make a sound. It adds to Windows' own animation setting and never overrides it. Windows animation is currently " + (SystemParameters.ClientAreaAnimation ? "on." : "off, so this app already moves less."),
                s.AdhdLowStimulation, v => { s.AdhdLowStimulation = v; Modes.NotifyChanged(); }, "B.Violet"));
            sp.Children.Add(Switch("Time awareness", "Off by default. Running jobs show how long they have taken, and a plain reminder appears at the interval below.",
                s.AdhdTimeAwareness, v => { s.AdhdTimeAwareness = v; Modes.NotifyChanged(); }, "B.Violet"));
            sp.Children.Add(Choice("Reminder interval (default 30 minutes, used only with Time awareness)",
                new[] { ("15 min", 15), ("30 min", 30), ("45 min", 45), ("60 min", 60), ("90 min", 90) }, s.TimeReminderMinutes, v => { s.TimeReminderMinutes = v; Modes.NotifyChanged(); }));
            sp.Children.Add(Switch("One thing at a time", "Off by default. Only one popup is on screen at once. The rest wait their turn and also appear in History.",
                s.AdhdOneThing, v => { s.AdhdOneThing = v; Modes.NotifyChanged(); }, "B.Violet"));
            sp.Children.Add(Switch("Momentum", "Off by default. This window reopens on the page you were last using, so you can pick up where you stopped.",
                s.AdhdMomentum, v => { s.AdhdMomentum = v; Modes.NotifyChanged(); }, "B.Violet"));
            _spotlightSwitch = (CheckBox)Switch("Focus spotlight", "Off by default. Dims everything except the card under keyboard focus or the mouse, so one thing stands out. Also switched with Ctrl+Shift+Space from any window of this app. It never blocks clicks, and it moves without animation when motion is reduced.",
                s.AdhdSpotlight, v => FocusSpotlight.Set(v), "B.Violet");
            sp.Children.Add(_spotlightSwitch);
            sp.Children.Add(ModesNote($"While {Modes.SchoolName} is on, these modes stay as you set them. {Modes.SchoolName} only adds its own limits on top."));
            return Card(sp);
        }

        // -------------------------------------------------------------- Dim sum surprise

        private FrameworkElement ModesDimSumCard()
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Dim sum surprise", "Each time the app starts there is a 10% chance it shows one dim sum dish for a few seconds, with its English and Cantonese names and a picture. It never takes focus, never delays startup, and skips first-run setup and quiet moments such as game mode. There is no switch for it by design."));
            sp.Children.Add(ModesNote("This launch: " + Modes.DimSumLaunchStatus));
            sp.Children.Add(ModesNote("Pictures and names come from the public catalogue " + Modes.DimSumSource + " on GitHub, fetched over HTTPS only when a dish is shown, then cached in this app's data folder. Nothing is bundled with the app. If the catalogue or a picture cannot be reached, nothing is shown."));
            var cache = ModesNote(Modes.DimSumCacheInfo);
            sp.Children.Add(cache);
            var status = ModesNote("");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Ui.Button("Show one now (preview)", async () =>
            {
                status.Text = "Fetching a dish..."; status.Foreground = Ui.Res("B.TextMute");
                bool ok = await Modes.ShowDimSumAsync();
                status.Text = ok ? "Shown. A preview does not count as the launch draw." : "Unavailable: " + Modes.DimSumError + ".";
                status.Foreground = Ui.Res(ok ? "B.TextMute" : "B.Amber");
                cache.Text = Modes.DimSumCacheInfo;
            }, "Btn.Ghost", Icons.Play));
            row.Children.Add(Ui.Button("Clear cached pictures", () => { Modes.ClearDimSumCache(); cache.Text = Modes.DimSumCacheInfo; status.Text = "Cache cleared."; }));
            sp.Children.Add(row);
            sp.Children.Add(status);
            return SearchScope.SetItem(Card(sp), "dim sum dish surprise random photo catalogue");
        }

        // -------------------------------------------------------------- Scheduled settings

        private static string ModesSettingLabel(string key) => key switch
        {
            "language" => "Language mode", "theme" => "Theme", "sounds" => "Sound effects", "toasts" => "Clipboard toasts", "gamemode" => "Game mode", _ => key
        };

        private static string ModesValueLabel(string key, string v) => key switch
        {
            "language" => v == "en" ? "English" : v == "yue" ? "Cantonese" : "Bilingual",
            "theme" => char.ToUpperInvariant(v[0]) + v.Substring(1),
            _ => v == "on" ? "On" : "Off"
        };

        private FrameworkElement ModesScheduleCard(List<Action> refreshHooks)
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Scheduled settings", "A rule says: between two times on chosen days, set one value. Rules are checked every minute. When a rule ends, your saved choice comes back; a rule never rewrites it. None by default."));
            sp.Children.Add(ModesNote($"Times use this computer's clock in {TimeZoneInfo.Local.DisplayName}. A window that crosses midnight belongs to the day it starts on. Equal start and end mean all day. Daylight-saving changes follow the wall clock, so a window inside a skipped hour does not happen that day. If rules overlap on one setting, the one lower in the list wins."));

            var guideHost = new ContentControl { IsTabStop = false };
            var guideBtn = Ui.Button("Add a rule step by step", () =>
            {
                guideHost.Content = ModesRuleGuide(() => { guideHost.Content = null; foreach (var h in refreshHooks.ToList()) h(); }, () => guideHost.Content = null);
            }, "Btn.Accent", Icons.Wand);
            guideBtn.Margin = new Thickness(0, 0, 0, 10);
            sp.Children.Add(guideBtn);
            sp.Children.Add(guideHost);

            string setting = "sounds", value = "off";
            var days = new HashSet<int>();
            var label = Input("", 220, "Optional name for the rule");
            AutomationProperties.SetName(label, "Rule name");
            var start = Input("09:00", 80, "Start time, 24-hour HH:mm"); AutomationProperties.SetName(start, "Start time");
            var end = Input("17:00", 80, "End time, 24-hour HH:mm"); AutomationProperties.SetName(end, "End time");
            var fromDate = Input("", 110, "Optional first date, yyyy-MM-dd. Empty means no start limit."); AutomationProperties.SetName(fromDate, "Start date (optional)");
            var toDate = Input("", 110, "Optional last date, yyyy-MM-dd, included. Empty means no end limit."); AutomationProperties.SetName(toDate, "End date (optional)");
            var valueHost = new ContentControl();
            var settingHost = new ContentControl();
            ScheduleRule editing = null;
            var msg = ModesNote("");

            IEnumerable<(string, string)> ValuesFor(string key)
            {
                switch (key)
                {
                    case "language":
                        foreach (var v in Modes.LanguageValues)
                            if (v == "en" || !(Modes.Blocks(ModeFeature.Cantonese))) yield return (ModesValueLabel(key, v), v);
                        break;
                    case "theme": foreach (var v in Modes.ThemeValues) yield return (ModesValueLabel(key, v), v); break;
                    default: yield return ("On", "on"); yield return ("Off", "off"); break;
                }
            }
            string DefaultFor(string key) => ValuesFor(key).First().Item2;
            void BuildValue() => valueHost.Content = Choice("Value", ValuesFor(setting), value, v => value = v);
            value = "off"; BuildValue();

            var settingKeys = Modes.SettingKeys.Select(k => (ModesSettingLabel(k), k)).ToList();
            void BuildSetting() => settingHost.Content = Choice("Setting to change", settingKeys, setting, v => { setting = v; value = (v == "language" || v == "theme") ? DefaultFor(v) : "off"; BuildValue(); });
            BuildSetting();
            sp.Children.Add(settingHost);
            sp.Children.Add(valueHost);

            // Weekdays
            var dayBar = new WrapPanel();
            var dayButtons = new List<(Button B, int D)>();
            void PaintDays()
            {
                foreach (var (b, d) in dayButtons)
                {
                    bool on = days.Contains(d);
                    b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                    b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                    b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                }
            }
            foreach (var d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                int dd = d;
                var b = Ui.Button(((DayOfWeek)d).ToString().Substring(0, 3), null, "Btn.Chip");
                b.Click += (x, y) => { if (!days.Remove(dd)) days.Add(dd); PaintDays(); };
                AutomationProperties.SetName(b, ((DayOfWeek)d).ToString());
                dayButtons.Add((b, d)); dayBar.Children.Add(b);
            }
            var every = Ui.Button("Every day", () => { days.Clear(); PaintDays(); }, "Btn.Ghost");
            dayBar.Children.Add(every);
            PaintDays();
            var dayBox = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            dayBox.Children.Add(new TextBlock { Text = "Days (none selected means every day)", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 8) });
            dayBox.Children.Add(dayBar);
            sp.Children.Add(dayBox);

            var timeRow = new StackPanel { Orientation = Orientation.Horizontal };
            timeRow.Children.Add(new TextBlock { Text = "From", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 8), Foreground = Ui.Res("B.TextSoft") });
            timeRow.Children.Add(start);
            timeRow.Children.Add(new TextBlock { Text = "to", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 8), Foreground = Ui.Res("B.TextSoft") });
            timeRow.Children.Add(end);
            timeRow.Children.Add(label);
            sp.Children.Add(timeRow);

            var dateRow = new StackPanel { Orientation = Orientation.Horizontal };
            dateRow.Children.Add(new TextBlock { Text = "Only between dates (optional)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 8), Foreground = Ui.Res("B.TextSoft") });
            dateRow.Children.Add(fromDate);
            dateRow.Children.Add(new TextBlock { Text = "and", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 8), Foreground = Ui.Res("B.TextSoft") });
            dateRow.Children.Add(toDate);
            sp.Children.Add(dateRow);
            sp.Children.Add(ModesNote("Dates are written yyyy-MM-dd and both ends are included. Leave both empty for a rule with no date limit, or fill only one for an open-ended range. The window still follows the times and days above.", "B.TextMute"));

            // Existing rules
            var addBtn = Ui.Button("Add rule", null, "Btn.Accent", Icons.Plus);
            var cancelEdit = Ui.Button("Cancel edit", null, "Btn.Ghost");
            cancelEdit.Visibility = Visibility.Collapsed;
            var filter = new SearchField("modes:rules", "Search schedule rules", compact: true) { MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 8) };
            var list = new StackPanel();
            // Select mode for the rule list: enable, disable or delete the chosen rules together.
            var barHost = new ContentControl { IsTabStop = false };
            BulkSelect<ScheduleRule> bulk = null;
            Action refillAll = null;
            bulk = new BulkSelect<ScheduleRule>(() => refillAll(), "rules");
            string RuleNoun(int n) => n == 1 ? "rule" : "rules";
            void RebuildBar() => barHost.Content = bulk.Bar(new[]
            {
                new BulkAction<ScheduleRule> { Label = "Enable", Hint = "Turn the chosen rules on.",
                    Run = items => { foreach (var r in items) r.Enabled = true; Settings.Save(); Modes.Evaluate(); refillAll(); } },
                new BulkAction<ScheduleRule> { Label = "Disable", Hint = "Turn the chosen rules off without deleting them.",
                    Run = items => { foreach (var r in items) r.Enabled = false; Settings.Save(); Modes.Evaluate(); refillAll(); } },
                new BulkAction<ScheduleRule> { Label = "Delete", Danger = true, Hint = "Delete the chosen rules, after you confirm.",
                    Run = items =>
                    {
                        var chosen = items.ToList();
                        bulk.ConfirmThen($"Delete {chosen.Count} {RuleNoun(chosen.Count)}?", "The rules are removed. Your saved settings are not changed.", "Delete", () => { foreach (var r in chosen) Settings.Current.ScheduleRules.Remove(r); Settings.Save(); Modes.Evaluate(); });
                    } },
            });
            void Refill()
            {
                var rules = Settings.Current.ScheduleRules;
                bulk.Prune(rules);
                bulk.Begin();
                RebuildBar();
                filter.SampleSource = () => rules.Select(r => r.Label + " " + Modes.DescribeRule(r));
                filter.State.TimedOut = false;
                list.Children.Clear();
                int shown = 0;
                foreach (var r in rules.ToList())
                {
                    var rule = r;
                    var text = (string.IsNullOrWhiteSpace(rule.Label) ? "" : rule.Label + " ") + Modes.DescribeRule(rule);
                    if (!filter.State.Matches(text)) continue;
                    shown++;
                    var row = new StackPanel();
                    row.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(rule.Label) ? ModesSettingLabel(rule.Setting) + ": " + ModesValueLabel(rule.Setting, rule.Value) : rule.Label,
                        Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body")
                    });
                    row.Children.Add(Ui.Mono(Modes.DescribeRule(rule), Ui.Res("B.TextMute"), 11));
                    string state = !rule.Enabled ? "Disabled" : Modes.IsWinning(rule) ? "In effect now" : "Waiting for its time";
                    row.Children.Add(Ui.Mono(state, Ui.Res(Modes.IsWinning(rule) && rule.Enabled ? "B.Mint" : "B.TextMute"), 11));
                    var btns = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
                    void Changed() { Settings.Save(); Modes.Evaluate(); Refill(); }
                    btns.Children.Add(Ui.Button("Edit", () => { LoadRule(rule); }, "Btn.Ghost"));
                    btns.Children.Add(Ui.Button(rule.Enabled ? "Disable" : "Enable", () => { rule.Enabled = !rule.Enabled; Changed(); }));
                    btns.Children.Add(Ui.Button("Move earlier", () => { var i = rules.IndexOf(rule); if (i > 0) { rules.RemoveAt(i); rules.Insert(i - 1, rule); Changed(); } }));
                    btns.Children.Add(Ui.Button("Move later", () => { var i = rules.IndexOf(rule); if (i >= 0 && i < rules.Count - 1) { rules.RemoveAt(i); rules.Insert(i + 1, rule); Changed(); } }));
                    btns.Children.Add(Ui.Button("Delete", () => { rules.Remove(rule); Changed(); }, "Btn.Danger"));
                    row.Children.Add(btns);
                    list.Children.Add(bulk.Wrap(rule, SearchScope.SetItem(Card(row, null, 12), text), string.IsNullOrWhiteSpace(rule.Label) ? ModesSettingLabel(rule.Setting) : rule.Label));
                }
                if (rules.Count == 0) list.Children.Add(ModesNote("No rules yet."));
                filter.SetCount(shown, rules.Count, "rules");
            }
            refillAll = Refill;
            filter.Filter += Refill;
            refreshHooks.Add(Refill);

            void SetEditing(ScheduleRule r)
            {
                editing = r;
                addBtn.Content = r == null ? "Add rule" : "Save changes";
                cancelEdit.Visibility = r == null ? Visibility.Collapsed : Visibility.Visible;
            }
            void LoadRule(ScheduleRule r)
            {
                setting = r.Setting; value = r.Value;
                days.Clear(); foreach (var d in r.Days ?? new List<int>()) days.Add(d);
                start.Text = r.Start; end.Text = r.End; label.Text = r.Label ?? "";
                fromDate.Text = r.FromDate ?? ""; toDate.Text = r.ToDate ?? "";
                BuildSetting(); BuildValue(); PaintDays();
                SetEditing(r);
                msg.Text = "Editing this rule in place. Change the fields above, then choose Save changes."; msg.Foreground = Ui.Res("B.TextMute");
                start.Focus();
            }
            cancelEdit.Click += (x, y) => { SetEditing(null); msg.Text = "Edit cancelled. The rule is unchanged."; msg.Foreground = Ui.Res("B.TextMute"); };
            var addRow = new WrapPanel();
            addRow.Children.Add(addBtn); addRow.Children.Add(cancelEdit);
            addBtn.Click += (x0, y0) =>
            {
                if (!Modes.TryParseTime(start.Text, out _) || !Modes.TryParseTime(end.Text, out _))
                { msg.Text = "Enter both times as 24-hour HH:mm, for example 08:30."; msg.Foreground = Ui.Res("B.Amber"); return; }
                if (editing == null && Settings.Current.ScheduleRules.Count >= 64) { msg.Text = "The limit is 64 rules."; msg.Foreground = Ui.Res("B.Amber"); return; }
                if (!Modes.ValidValue(setting, value)) { msg.Text = "Pick a value first."; msg.Foreground = Ui.Res("B.Amber"); return; }
                if (!Modes.TryParseDate(fromDate.Text, out _) || !Modes.TryParseDate(toDate.Text, out _))
                { msg.Text = "Enter dates as yyyy-MM-dd, for example 2026-12-24, or leave them empty."; msg.Foreground = Ui.Res("B.Amber"); return; }
                if (!Modes.ValidDateRange(fromDate.Text, toDate.Text))
                { msg.Text = "The end date is before the start date."; msg.Foreground = Ui.Res("B.Amber"); return; }
                var target = editing ?? new ScheduleRule();
                target.Label = label.Text.Trim(); target.Setting = setting; target.Value = value;
                target.Start = start.Text.Trim(); target.End = end.Text.Trim();
                target.Days = days.OrderBy(d => (d + 6) % 7).ToList();
                target.FromDate = fromDate.Text.Trim(); target.ToDate = toDate.Text.Trim();
                bool wasEditing = editing != null;
                if (!wasEditing) Settings.Current.ScheduleRules.Add(target);
                Settings.Save(); Modes.Evaluate(); SetEditing(null); Refill();
                msg.Text = wasEditing ? "Rule updated." : "Rule added."; msg.Foreground = Ui.Res("B.TextMute");
                label.Text = "";
            };
            sp.Children.Add(addRow);
            sp.Children.Add(msg);
            sp.Children.Add(ModesNote("Language and theme rules are stored and offered to the rest of the app as the scheduled choice. Sounds, toasts and game mode rules apply immediately. A rule never changes your saved choice.", "B.TextMute"));
            sp.Children.Add(filter);
            sp.Children.Add(barHost);
            sp.Children.Add(list);
            Refill();
            return Card(sp);
        }

        // -------------------------------------------------------------- External settings source

        private FrameworkElement ModesExternalCard(TextBlock status, Action paint)
        {
            var s = Settings.Current;
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Settings file", "Follow a JSON file you choose. It is read again whenever it changes and applied on top of your saved settings, below any scheduled rule. Off by default."));
            var path = Input(s.ExternalSettingsPath, 460, "Full path to a .json file");
            AutomationProperties.SetName(path, "Settings file path");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(path);
            row.Children.Add(Ui.Button("Browse", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON files (*.json)|*.json", CheckFileExists = true };
                if (dlg.ShowDialog() == true) path.Text = dlg.FileName;
            }));
            sp.Children.Add(row);

            var follow = new StackPanel { Orientation = Orientation.Horizontal };
            follow.Children.Add(Ui.Button("Follow this file", () =>
            {
                var p = path.Text.Trim().Trim('"');
                if (p.Length == 0 || !p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) { status.Text = "Choose a file that ends in .json."; status.Foreground = Ui.Res("B.Amber"); return; }
                Modes.StartFollowing(p); paint();
            }, "Btn.Accent"));
            follow.Children.Add(Ui.Button("Apply now", () => { if (string.IsNullOrWhiteSpace(Settings.Current.ExternalSettingsPath)) Settings.Current.ExternalSettingsPath = path.Text.Trim().Trim('"'); Modes.ApplyExternalNow(); paint(); }));
            follow.Children.Add(Ui.Button("Stop following", () => { Modes.StopFollowing(); paint(); }, "Btn.Alert"));
            sp.Children.Add(follow);
            sp.Children.Add(new TextBlock { Text = s.ExternalFollowing ? "Following: yes" : "Following: no", Foreground = Ui.Res(s.ExternalFollowing ? "B.Mint" : "B.TextSoft"), FontSize = 13, Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(status);
            sp.Children.Add(ModesNote("The file must be under 64 KB. Expected shape (keys you leave out change nothing; unknown keys are ignored; a wrong type rejects the whole file and keeps the last good values):"));
            sp.Children.Add(Ui.Mono("{ \"version\": 1, \"settings\": { \"language\": \"en|yue|bilingual\", \"theme\": \"system|light|dark\", \"sounds\": true, \"toasts\": true, \"gameMode\": false } }", Ui.Res("B.TextSoft"), 11));
            return SearchScope.SetItem(Card(sp), "external settings json file follow watch");
        }
    }
}
