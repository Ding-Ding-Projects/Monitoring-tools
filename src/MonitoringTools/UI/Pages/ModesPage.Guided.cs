using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        /// <summary>
        /// A guided way to add a schedule rule: what to change, when, which dates, then a review. It adds through the same
        /// <see cref="Modes.AddRule"/> check as the single form, so both routes accept and refuse the same rules.
        /// </summary>
        internal GuidedForm ModesRuleGuide(Action done, Action cancel)
        {
            string setting = "sounds", value = "off";
            var days = new HashSet<int>();
            var valueHost = new ContentControl { IsTabStop = false };
            var label = Input("", 240, "Optional name for the rule");
            var start = new TimeField("Start time", "09:00");
            var end = new TimeField("End time", "17:00");
            var fromDate = new DateField("Start date (optional)", "");
            var toDate = new DateField("End date (optional)", "");
            var review = new TextBlock { Style = Ui.Style("T.Body"), TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text") };

            IEnumerable<(string, string)> ValuesFor(string key)
            {
                switch (key)
                {
                    case "language": foreach (var v in Modes.LanguageValues) if (v == "en" || !Modes.Blocks(ModeFeature.Cantonese)) yield return (ModesValueLabel(key, v), v); break;
                    case "theme": foreach (var v in Modes.ThemeValues) yield return (ModesValueLabel(key, v), v); break;
                    default: yield return ("On", "on"); yield return ("Off", "off"); break;
                }
            }
            void BuildValue() => valueHost.Content = Choice("Value", ValuesFor(setting), value, v => value = v);

            ScheduleRule Draft() => new ScheduleRule
            {
                Label = label.Text.Trim(), Setting = setting, Value = value, Start = start.Value.Trim(), End = end.Value.Trim(),
                Days = days.OrderBy(d => (d + 6) % 7).ToList(), FromDate = fromDate.Value.Trim(), ToDate = toDate.Value.Trim()
            };

            var steps = new List<GuidedStep>
            {
                new GuidedStep
                {
                    Title = "What should change?", Help = "Pick one setting and the value it takes while the rule is in effect. When the rule ends, your own saved choice comes back.",
                    Build = () =>
                    {
                        var sp = new StackPanel();
                        sp.Children.Add(Choice("Setting to change", Modes.SettingKeys.Select(k => (ModesSettingLabel(k), k)).ToList(), setting,
                            v => { setting = v; value = ValuesFor(v).First().Item2; BuildValue(); }));
                        BuildValue();
                        sp.Children.Add(valueHost);
                        return sp;
                    },
                    Validate = () => Modes.CheckRuleValue(setting, value)
                },
                new GuidedStep
                {
                    Title = "When does it apply?", Help = "Choose the days (none selected means every day) and the 24-hour start and end. Equal times mean all day, and a window that crosses midnight belongs to the day it starts on.",
                    Build = () =>
                    {
                        var sp = new StackPanel();
                        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
                        var chips = new List<(Button B, int D)>();
                        void Paint() { foreach (var (b, d) in chips) { bool on = days.Contains(d); b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised"); b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft"); b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong"); } }
                        foreach (var d in new[] { 1, 2, 3, 4, 5, 6, 0 })
                        {
                            int dd = d;
                            var b = Ui.Button(((DayOfWeek)d).ToString().Substring(0, 3), null, "Btn.Chip");
                            b.Click += (x, y) => { if (!days.Remove(dd)) days.Add(dd); Paint(); };
                            System.Windows.Automation.AutomationProperties.SetName(b, ((DayOfWeek)d).ToString());
                            chips.Add((b, d)); bar.Children.Add(b);
                        }
                        bar.Children.Add(Ui.Button("Every day", () => { days.Clear(); Paint(); }, "Btn.Ghost"));
                        Paint();
                        sp.Children.Add(bar);
                        var row = new StackPanel { Orientation = Orientation.Horizontal };
                        row.Children.Add(Labeled("From", start, "Start time"));
                        row.Children.Add(new Border { Width = 12 });
                        row.Children.Add(Labeled("To", end, "End time"));
                        sp.Children.Add(row);
                        sp.Children.Add(Labeled("Name (optional)", label, "Rule name"));
                        return sp;
                    },
                    Validate = () => Modes.CheckRuleTimes(start.Value, end.Value)
                },
                new GuidedStep
                {
                    Title = "Only between certain dates?", Help = "Optional. Leave both empty for no date limit, or fill only one for an open-ended range. Both ends are included.",
                    Build = () =>
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal };
                        row.Children.Add(Labeled("First date", fromDate, "Start date (optional)"));
                        row.Children.Add(new Border { Width = 12 });
                        row.Children.Add(Labeled("Last date", toDate, "End date (optional)"));
                        return row;
                    },
                    Validate = () => Modes.CheckRuleDates(fromDate.Value, toDate.Value)
                },
                new GuidedStep
                {
                    Title = "Review and add", Help = "Adding puts the rule at the end of the list. If rules overlap on one setting, the one lower in the list wins.",
                    Build = () => review,
                    Entered = () =>
                    {
                        var r = Draft();
                        review.Text = (string.IsNullOrEmpty(r.Label) ? "" : "Name: " + r.Label + "\n") + $"Changes: {ModesSettingLabel(r.Setting)} to {ModesValueLabel(r.Setting, r.Value)}\nWhen: {Modes.DescribeRule(r)}";
                    }
                }
            };
            var form = new GuidedForm("Add a schedule rule", steps, "Add rule", () => Modes.AddRule(Draft()), cancel);
            form.Finished += done;
            return form;
        }
    }
}
