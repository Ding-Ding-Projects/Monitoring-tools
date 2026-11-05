using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // The effects of the ADHD modes, read from a running window: held toasts, one toast at a time, no motion or sound,
    // elapsed time on a running task, the reminder timer and reopening on the page left open.
    public static partial class SelfTest
    {
        private static void CloseToasts()
        {
            foreach (var t in Application.Current.Windows.OfType<ToastWindow>().ToList()) { try { t.Close(); } catch { } }
            var live = (System.Collections.IList)typeof(ToastManager).GetField("_live", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(App.Toasts);
            live.Clear();
            var q = (Queue<ToastWindow>)typeof(ToastManager).GetField("_waiting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(App.Toasts);
            q.Clear();
        }

        private static async Task AdhdLane4ChecksAsync()
        {
            var s = Settings.Current;
            bool f0 = s.AdhdFocus, o0 = s.AdhdOneThing, l0 = s.AdhdLowStimulation, t0 = s.AdhdTimeAwareness, m0 = s.AdhdMomentum;
            int rem0 = s.TimeReminderMinutes;
            void Restore() { s.AdhdFocus = f0; s.AdhdOneThing = o0; s.AdhdLowStimulation = l0; s.AdhdTimeAwareness = t0; s.AdhdMomentum = m0; s.TimeReminderMinutes = rem0; Modes.NotifyChanged(); CloseToasts(); }

            await CheckAsync("adhd focus: a toast that is not urgent goes to the history and a warning still shows", async () =>
            {
                try
                {
                    CloseToasts(); s.AdhdFocus = false; Modes.NotifyChanged();
                    int logged = NotificationLog.All().Count;
                    App.Toasts.ShowNotice("Focus plain", "Shown when Focus is off.", App.Toasts.Violet, 30);
                    await Task.Delay(300);
                    if (App.Toasts.LiveToasts.Count != 1) return "with Focus off the toast was not shown";
                    CloseToasts();
                    s.AdhdFocus = true; Modes.NotifyChanged();
                    int before = NotificationLog.All().Count;
                    App.Toasts.ShowNotice("Focus held", "Held back when Focus is on.", App.Toasts.Violet, 30);
                    await Task.Delay(300);
                    if (App.Toasts.LiveToasts.Count != 0) return "with Focus on a non-urgent toast was shown";
                    if (NotificationLog.All().Count != before + 1 || !NotificationLog.All().Any(n => n.Title.Contains("Focus held") || n.Detail.Contains("Focus held"))) return "the held toast is not in the notification history";
                    App.Toasts.ShowNotice("Focus warning", "A warning gets through.", App.Toasts.Amber, 30);
                    await Task.Delay(300);
                    return App.Toasts.LiveToasts.Count == 1 ? null : "a warning was held back";
                }
                finally { Restore(); }
            });

            await CheckAsync("adhd one thing at a time: a second toast waits, in order, until the first goes", async () =>
            {
                try
                {
                    CloseToasts(); s.AdhdFocus = false; s.AdhdLowStimulation = true; s.AdhdOneThing = true; Modes.NotifyChanged();   // low stimulation so closing is immediate
                    var a = App.Toasts.ShowNotice("First", "On screen now.", App.Toasts.Violet, 60);
                    var b = App.Toasts.ShowNotice("Second", "Waits.", App.Toasts.Violet, 60);
                    var c = App.Toasts.ShowNotice("Third", "Waits longer.", App.Toasts.Violet, 60);
                    await Task.Delay(300);
                    if (App.Toasts.LiveToasts.Count != 1 || App.Toasts.WaitingCount != 2) return $"expected 1 live and 2 waiting, got {App.Toasts.LiveToasts.Count} and {App.Toasts.WaitingCount}";
                    if (b.IsVisible || c.IsVisible) return "a waiting toast is visible";
                    a.CloseAnimated(); await Task.Delay(300);
                    if (!b.IsVisible || c.IsVisible || App.Toasts.WaitingCount != 1) return $"after the first went: second visible={b.IsVisible}, third visible={c.IsVisible}, waiting={App.Toasts.WaitingCount}";
                    b.CloseAnimated(); await Task.Delay(300);
                    if (!c.IsVisible) return "the third toast never got its turn";
                    s.AdhdOneThing = false; Modes.NotifyChanged();
                    var d = App.Toasts.ShowNotice("Fourth", "Free to show with the mode off.", App.Toasts.Violet, 60);
                    var e = App.Toasts.ShowNotice("Fifth", "Free to show with the mode off.", App.Toasts.Violet, 60);
                    await Task.Delay(300);
                    return d.IsVisible && e.IsVisible ? null : "with the mode off two toasts did not show together";
                }
                finally { Restore(); }
            });

            await CheckAsync("adhd low stimulation: motion is skipped in a live window and sounds are limited to warnings", async () =>
            {
                try
                {
                    var host = new Window { Width = 300, Height = 200, Content = new Border { Background = Brushes.Black, Opacity = 0 } };
                    host.Show();
                    var el = (Border)host.Content;
                    s.AdhdLowStimulation = false; Modes.NotifyChanged();
                    if (Ui.Reduced && !Modes.LowStimulation) { /* Windows itself reduces animation: the "off" half cannot be read here */ }
                    bool windowsReduces = !SystemParameters.ClientAreaAnimation;
                    el.Opacity = 0; Ui.Fade(el, 1, 1200);
                    bool offHalf = windowsReduces || el.Opacity < 0.5;   // the animation has not run to the end yet
                    el.BeginAnimation(UIElement.OpacityProperty, null); el.Opacity = 0;
                    var ordinary = Modes.SoundAllowed(false); var essential = Modes.SoundAllowed(true);
                    s.AdhdLowStimulation = true; Modes.NotifyChanged();
                    if (!Ui.Reduced) { host.Close(); return "Ui.Reduced is false with Low stimulation on"; }
                    el.BeginAnimation(UIElement.OpacityProperty, null); el.Opacity = 0; Ui.Fade(el, 1, 1200);
                    await WaitFor(() => el.Opacity >= 0.99, 450);
                    bool onHalf = el.Opacity >= 0.99;   // finished within a blink, not over 1.2 s
                    bool soundOrdinary = Modes.SoundAllowed(false), soundEssential = Modes.SoundAllowed(true);
                    // a toast shown now has no slide: its resting opacity is full straight away
                    CloseToasts();
                    var t = App.Toasts.ShowNotice("Still", "No motion.", App.Toasts.Violet, 30);
                    await Task.Delay(100);
                    bool toastStill = t.Opacity >= 0.99;
                    host.Close();
                    if (!offHalf) return "with the mode off the fade did not animate (opacity " + el.Opacity + ")";
                    if (!onHalf) return "with the mode on the fade still animated";
                    if (!toastStill) return "a toast was still fading in with the mode on";
                    if (Modes.SoundsOn && ordinary && soundOrdinary) return "an ordinary sound still plays with the mode on";
                    return !Modes.SoundsOn || soundEssential ? null : "a warning sound is silenced by the mode";
                }
                finally { Restore(); }
            });

            await CheckAsync("adhd low stimulation: the sound service really starts playback for an allowed cue and never reaches the player for a refused one", async () =>
            {
                bool snd0 = s.SoundsEnabled; double vol0 = s.SoundVolume;
                try
                {
                    s.SoundsEnabled = true; s.SoundVolume = 0.05; Sounds.QuietOverrideForTest = false;
                    s.AdhdLowStimulation = false; Modes.NotifyChanged();
                    async Task<(int Started, string File, string Failure)> Fire(Cue cue)
                    {
                        await Task.Delay(600);   // clear of the one-cue-at-a-time debounce
                        int before = Sounds.StartedForTest; Sounds.FailureForTest = null;
                        Sounds.Play(cue);
                        await Task.Delay(400);   // a playback failure from the media engine arrives a moment later
                        return (Sounds.StartedForTest - before, Sounds.LastStartedForTest, Sounds.FailureForTest);
                    }
                    var open = await Fire(Cue.Success);
                    if (open.Started != 1) return "with Low stimulation off an ordinary cue did not start playback (" + open.Started + " starts)";
                    if (string.IsNullOrEmpty(open.File)) return "playback started but named no sound";
                    // Low stimulation: an ordinary cue is refused before the player, a warning still plays
                    s.AdhdLowStimulation = true; Modes.NotifyChanged();
                    var refused = await Fire(Cue.Success);
                    if (refused.Started != 0) return "with Low stimulation on an ordinary cue still reached the player";
                    var warn = await Fire(Cue.Warning);
                    if (warn.Started != 1) return "with Low stimulation on a warning did not start playback";
                    // the master switch and a zero volume refuse everything
                    s.AdhdLowStimulation = false; s.SoundsEnabled = false; Modes.NotifyChanged();
                    var off = await Fire(Cue.Warning);
                    if (off.Started != 0) return "with sounds off a cue reached the player";
                    s.SoundsEnabled = true; s.SoundVolume = 0; Modes.NotifyChanged();
                    var mute = await Fire(Cue.Success);
                    if (mute.Started != 0) return "with the volume at zero a cue reached the player";
                    // and quiet time (a game, a presentation, Windows quiet hours) is honoured
                    s.SoundVolume = 0.05; Sounds.QuietOverrideForTest = true;
                    var quiet = await Fire(Cue.Success);
                    if (quiet.Started != 0) return "during quiet time a cue reached the player";
                    NoteForReport("sound playback", "allowed cue started " + Path.GetFileName(open.File) + (open.Failure == null ? " with no media error" : "; the media engine reported: " + open.Failure));
                    return null;
                }
                finally { Sounds.QuietOverrideForTest = null; s.SoundsEnabled = snd0; s.SoundVolume = vol0; Restore(); }
            });

            await CheckAsync("adhd time awareness: a running task shows elapsed time, then how long it took, and the reminder timer follows the setting", async () =>
            {
                try
                {
                    CloseToasts(); s.AdhdFocus = false; s.AdhdOneThing = false; s.AdhdTimeAwareness = false; Modes.NotifyChanged();
                    if (Modes.ReminderRunning) return "the reminder was running with the mode off";
                    s.AdhdTimeAwareness = true; s.TimeReminderMinutes = 45; Modes.NotifyChanged(); Settings.Save();
                    await Task.Delay(200);
                    if (!Modes.ReminderRunning || Modes.ReminderEvery != TimeSpan.FromMinutes(45)) return "the reminder did not start at the chosen interval: " + Modes.ReminderEvery;
                    ToastWindow found = null;
                    App.Toasts.ShowTask("Slow job", "for the time check", async () => { await Task.Delay(2600); return (true, "done", "ok"); });
                    await Task.Delay(1500);
                    found = App.Toasts.LiveToasts.FirstOrDefault();
                    if (found == null) return "the task toast did not show";
                    var running = found.WhenText.Text;
                    if (!System.Text.RegularExpressions.Regex.IsMatch(running, @"^\d+ s elapsed$")) return "no elapsed time while running: \"" + running + "\"";
                    await Task.Delay(2200);
                    var done = found.WhenText.Text;
                    if (!System.Text.RegularExpressions.Regex.IsMatch(done, @"^Took \d+ s$")) return "no duration when finished: \"" + done + "\"";
                    CloseToasts();
                    s.AdhdTimeAwareness = false; Modes.NotifyChanged(); Settings.Save(); await Task.Delay(200);
                    return Modes.ReminderRunning ? "the reminder kept running after the mode went off" : null;
                }
                finally { Restore(); }
            });

            await CheckAsync("adhd time awareness: the Time check reminder toast appears on its interval (a test-only short interval) and is not held by Focus", async () =>
            {
                try
                {
                    CloseToasts(); s.AdhdFocus = true; s.AdhdOneThing = false; s.AdhdTimeAwareness = false; Modes.NotifyChanged(); Settings.Save();
                    Modes.ReminderIntervalForTest = TimeSpan.FromSeconds(1);
                    s.AdhdTimeAwareness = true; Modes.NotifyChanged(); Settings.Save();
                    bool shown = await WaitFor(() => App.Toasts.LiveToasts.Any(t => t.TitleText.Text.Contains("time check", StringComparison.OrdinalIgnoreCase)), 6000);
                    var body = App.Toasts.LiveToasts.FirstOrDefault(t => t.TitleText.Text.Contains("time check", StringComparison.OrdinalIgnoreCase));
                    if (!shown) return $"no Time check toast within 6 seconds (reminder running {Modes.ReminderRunning} every {Modes.ReminderEvery}, suppress {App.ShouldSuppress}, live: {string.Join('|', App.Toasts.LiveToasts.Select(t => t.TitleText.Text))}, waiting {App.Toasts.WaitingCount})";
                    return null;
                }
                finally { Modes.ReminderIntervalForTest = null; Restore(); Settings.Save(); }
            });

            await CheckAsync("adhd momentum: a finished task toast names the next step with the mode on and not with it off", async () =>
            {
                try
                {
                    s.AdhdFocus = false; s.AdhdOneThing = false;
                    async Task<bool> HasChip(bool momentum)
                    {
                        CloseToasts(); s.AdhdMomentum = momentum; Modes.NotifyChanged();
                        App.Toasts.ShowTask("Quick job " + momentum, "momentum chip", () => Task.FromResult((true, "done", "ok")));
                        await Task.Delay(1200);
                        var t = App.Toasts.LiveToasts.FirstOrDefault();
                        if (t == null) throw new Exception("the task toast did not show");
                        return Descend<System.Windows.Controls.Primitives.ButtonBase>(t).Any(b => ButtonLabel(b) == "Next: open Activity" || Descend<System.Windows.Controls.TextBlock>(b).Any(x => x.Text == "Next: open Activity"));
                    }
                    if (!await HasChip(true)) return "Momentum is on but the finished task toast names no next step";
                    return await HasChip(false) ? "the next step shows with Momentum off" : null;
                }
                finally { Restore(); }
            });

            await CheckAsync("adhd momentum: the control centre reopens on the page left open, and not with the mode off", async () =>
            {
                try
                {
                    s.AdhdMomentum = true; Modes.NotifyChanged();
                    var a = new ControlCentreWindow { Width = 1180, Height = 820 };
                    a.Show(); await Task.Delay(700);
                    a.Navigate("records", animate: false); await Task.Delay(200);
                    a.Navigate("modes", animate: false); await Task.Delay(200);
                    a.Close();
                    Settings.Current.ActiveTab = "overview";   // the tab strip alone would reopen on the overview now, so only the mode can bring the modes page back
                    if (Settings.Current.LastPage != "modes") return "the last page was not remembered: \"" + Settings.Current.LastPage + "\"";
                    var b = new ControlCentreWindow { Width = 1180, Height = 820 };
                    b.Show(); await Task.Delay(800);
                    var reopened = b.CurrentIdForTest;
                    b.Close();
                    if (reopened != "modes") return "reopened on \"" + reopened + "\" instead of the modes page";
                    s.AdhdMomentum = false; Modes.NotifyChanged();
                    Settings.Current.ActiveTab = "overview";
                    var c = new ControlCentreWindow { Width = 1180, Height = 820 };
                    c.Show(); await Task.Delay(800);
                    var plain = c.CurrentIdForTest;
                    c.Close();
                    return plain != "modes" ? null : "the mode is off but the window still reopened on the remembered page";
                }
                finally { Restore(); }
            });
        }
    }
}
