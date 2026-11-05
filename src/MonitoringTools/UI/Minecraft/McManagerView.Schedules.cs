using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI.Minecraft
{
    // Schedules: restart, backup, message and update-check jobs with their next run, an on switch and a guided form.
    internal sealed partial class McManagerView
    {
        internal static McSchedule ScheduleDraft;
        private static int _scheduleStep;

        private FrameworkElement BuildSchedules(ServerDef s)
        {
            var root = new StackPanel();
            if (ScheduleDraft != null) { root.Children.Add(ScheduleForm(s)); return root; }
            var card = new StackPanel();
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(McUi.Title("Schedules"));
            var add = Ui.Button("New schedule, step by step", () => { ScheduleDraft = new McSchedule(); _scheduleStep = 0; BuildWorkspace(); }, "Btn.Accent", Icons.Plus);
            Grid.SetColumn(add, 1); head.Children.Add(add);
            card.Children.Add(head);
            var list = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            card.Children.Add(list);
            if (s.Schedules.Count == 0) list.Children.Add(McUi.Body("No schedules yet. A daily restart and a backup every few hours suit most servers.", "B.TextMute", 13));
            foreach (var job in s.Schedules.ToList())
            {
                var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var icon = job.Kind switch { McScheduleKind.Restart => Icons.Restart, McScheduleKind.Backup => Icons.Archive, McScheduleKind.Message => Icons.Mail, McScheduleKind.Command => Icons.Code, _ => Icons.Globe };
                g.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.SecondaryContainer"), Child = Ui.Icon(icon, Ui.Res("B.OnSecondaryContainer"), 18), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top });
                var j = job;
                var next = j.Enabled ? (j.Due(DateTime.Now) ? "Due now: it runs within a minute while this app is running." : j.Next(DateTime.Now) is DateTime n ? "Next: " + n.ToString("ddd HH:mm", CultureInfo.InvariantCulture) : "No next time.") : "Off";
                var last = j.LastRun is DateTime lr ? $" · Last run {McUi.Age(lr)}: {j.LastResult}" : "";
                var sw = McUi.Switch(McSchedule.KindLabel(j.Kind), j.Describe(), j.Enabled, v => { j.Enabled = v; if (v && j.LastRun == null) j.Created = DateTime.Now; try { ServerManager.Save(); } catch { } BuildWorkspace(); });
                var stack = new StackPanel();
                stack.Children.Add(sw);
                var nx = new TextBlock { Text = next + last, FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap };
                stack.Children.Add(nx);
                Grid.SetColumn(stack, 1); g.Children.Add(stack);
                var del = McUi.IconButton(Icons.Trash, "Delete the " + McSchedule.KindLabel(j.Kind).ToLowerInvariant() + " schedule", () => { s.Schedules.Remove(j); try { ServerManager.Save(); } catch { } BuildWorkspace(); });
                del.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(del, 2); g.Children.Add(del);
                list.Children.Add(new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 4, 0, 8), Child = g });
            }
            var foot = McUi.Body("Schedules run in the background app, in this computer's time zone. If the computer was asleep or the app was closed at the time, a missed run happens once when it is next running, never several times.", "B.TextMute", 12.5);
            foot.Margin = new Thickness(0, 12, 0, 0);
            card.Children.Add(foot);
            root.Children.Add(McUi.Card(card));
            return root;
        }

        private FrameworkElement ScheduleForm(ServerDef s)
        {
            var d = ScheduleDraft;
            var timeBox = McUi.Field(d.Time, "Time of day", 120, "24-hour time, for example 04:00");
            timeBox.TextChanged += (o, e) => d.Time = timeBox.Text.Trim();
            var review = McUi.Body("", "B.Text", 14);
            var steps = new List<GuidedStep>
            {
                new GuidedStep
                {
                    Title = "What should run?", Help = "Pick the job. Restart and message need the server to be running at the time; they are skipped and recorded when it is not.",
                    Build = () => McUi.Choice("Job", "A restart warns players first; a backup copies the world; a message is said in chat; a command runs a line from the command builder; an update check only tells you about a newer build.",
                        new[] { ("Restart", McScheduleKind.Restart), ("Backup", McScheduleKind.Backup), ("Message", McScheduleKind.Message), ("Run a command", McScheduleKind.Command), ("Check for updates", McScheduleKind.UpdateCheck) }, d.Kind, v => d.Kind = v),
                },
                new GuidedStep
                {
                    Title = "When?", Help = "Every day at a time, on chosen days at a time, or every so many minutes.",
                    Build = () =>
                    {
                        var sp = new StackPanel();
                        var host = new StackPanel();
                        void Paint()
                        {
                            host.Children.Clear();
                            if (d.Repeat == McRepeat.Every)
                            {
                                var row = new WrapPanel();
                                row.Children.Add(new TextBlock { Text = "Every", FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                                var nb = new McNumberBox("Minutes between runs", d.EveryMinutes, 5, 10080, 5, "min");
                                nb.ValueChanged += v => d.EveryMinutes = (int)v;
                                row.Children.Add(nb);
                                host.Children.Add(row);
                                return;
                            }
                            host.Children.Add(new TextBlock { Text = "Time of day", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 4, 0, 4) });
                            host.Children.Add(timeBox);
                            if (d.Repeat == McRepeat.Weekly)
                            {
                                var days = new WrapPanel();
                                AutomationProperties.SetName(days, "Days");
                                foreach (DayOfWeek day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday })
                                {
                                    var dd = day;
                                    Button b = null;
                                    b = McUi.Chip(day.ToString(), d.Days.Contains(day), () => { if (!d.Days.Remove(dd)) d.Days.Add(dd); McUi.Paint(b, d.Days.Contains(dd)); });
                                    days.Children.Add(b);
                                }
                                host.Children.Add(days);
                            }
                        }
                        sp.Children.Add(McUi.Choice("Repeat", "Daily runs every day; weekly runs on the days you pick; every runs on a fixed gap from now.",
                            new[] { ("Daily", McRepeat.Daily), ("Weekly", McRepeat.Weekly), ("Every", McRepeat.Every) }, d.Repeat, v => { d.Repeat = v; Paint(); Core.Language.Refresh(host); }));
                        sp.Children.Add(host);
                        Paint();
                        return sp;
                    },
                    Validate = () => d.Repeat != McRepeat.Every && !TimeSpan.TryParseExact(d.Time ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out _) ? "Type the time as HH:mm, for example 04:00." : d.Repeat == McRepeat.Weekly && d.Days.Count == 0 ? "Pick at least one day." : null,
                },
                new GuidedStep
                {
                    Title = "Details", Help = "A restart can warn players in chat first; a message needs its text.",
                    Build = () =>
                    {
                        var sp = new StackPanel();
                        if (d.Kind == McScheduleKind.Restart)
                        {
                            var row = new WrapPanel();
                            row.Children.Add(new TextBlock { Text = "Warn players this many minutes before", FontSize = 14, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                            var nb = new McNumberBox("Warning minutes", d.WarnMinutes, 0, 60, 1, "min");
                            nb.ValueChanged += v => d.WarnMinutes = (int)v;
                            row.Children.Add(nb);
                            sp.Children.Add(row);
                            sp.Children.Add(McUi.Body("0 restarts without a warning.", "B.TextMute", 12));
                        }
                        else if (d.Kind == McScheduleKind.Message)
                        {
                            var box = McUi.Field(d.Message, "Message", 420, "What the server says in chat");
                            box.TextChanged += (o, e) => d.Message = box.Text.Replace("\n", " ");
                            sp.Children.Add(box);
                        }
                        else if (d.Kind == McScheduleKind.Command)
                        {
                            var box = McUi.Field(d.Command, "Command", 420, "The command line to run, as the command builder makes it");
                            box.FontFamily = Ui.Font("F.Mono");
                            Core.Language.SetLiteral(box, true);
                            box.TextChanged += (o, e) => d.Command = box.Text.Replace("\r", " ").Replace("\n", " ");
                            sp.Children.Add(box);
                            var open = Ui.Button("Build it in the command builder", () => { ScheduleDraft = null; _scheduleStep = 0; ShowSection("commands"); }, "Btn.Ghost", Icons.Bolt);
                            open.HorizontalAlignment = HorizontalAlignment.Left;
                            sp.Children.Add(McUi.Body("It runs through the server's console and is skipped, and recorded as skipped, when the server is not running.", "B.TextMute", 12));
                            sp.Children.Add(open);
                        }
                        else sp.Children.Add(McUi.Body(d.Kind == McScheduleKind.Backup ? $"Backups go to the server's backup folder and the newest {s.KeepBackups} are kept." : "Only the Paper, Purpur, Fabric, Forge and NeoForge build lists can be checked. Nothing is installed by itself.", "B.TextSoft", 13));
                        return sp;
                    },
                    Validate = () => d.Kind == McScheduleKind.Message && string.IsNullOrWhiteSpace(d.Message) ? "Type the message." : d.Kind == McScheduleKind.Command && string.IsNullOrWhiteSpace(d.Command) ? "Type the command, or build it in the command builder." : null,
                },
                new GuidedStep
                {
                    Title = "Review and add", Help = "The schedule starts counting from now.",
                    Build = () => review,
                    Entered = () => { d.Created = DateTime.Now; review.Text = McSchedule.KindLabel(d.Kind) + " · " + d.Describe() + (d.Next(DateTime.Now) is DateTime n ? "\nFirst run: " + n.ToString("ddd HH:mm", CultureInfo.InvariantCulture) : ""); Core.Language.Refresh(review); },
                },
            };
            var form = new GuidedForm("New schedule", steps, "Add schedule", () =>
            {
                d.Created = DateTime.Now;
                s.Schedules.Add(d);
                ServerManager.Save();
                ScheduleDraft = null; _scheduleStep = 0;
                return null;
            }, () => { ScheduleDraft = null; _scheduleStep = 0; BuildWorkspace(); }, _scheduleStep);
            form.StepChanged += i => _scheduleStep = i;
            form.Finished += BuildWorkspace;
            return form;
        }
    }
}
