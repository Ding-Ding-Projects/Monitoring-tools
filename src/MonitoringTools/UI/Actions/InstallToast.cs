using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Core;

using Activity = MonitoringTools.Core.Activity;
using ActivityKind = MonitoringTools.Core.ActivityKind;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The toast for a copied choco or winget install command (design board 4). It shows what would be installed, from where and
    /// whether Windows will ask for permission, and nothing runs until Install is pressed. The one exception is automatic mode,
    /// an opt-in setting that starts a plain command after a visible countdown with a Cancel button. The same card then shows
    /// the steps, the result and, when the Windows consent prompt is declined, a plain outcome with the exit code.
    /// </summary>
    internal sealed class InstallToast
    {
        private readonly ToastWindow _w;
        private readonly ClipItem _item;
        private readonly InstallCommand _c;
        private CancellationTokenSource _cts;
        private DispatcherTimer _clock;
        private DateTime _began;
        private readonly List<(Border Mark, TextBlock Text, TextBlock Sub)> _steps = new List<(Border, TextBlock, TextBlock)>();
        private Border _barFill;
        private Border _barTrack;
        private bool _auto;

        /// <summary>offer, refused, missing, countdown, running, done, declined, failed or cancelled.</summary>
        public string State { get; private set; } = "";
        public InstallResult Result { get; private set; }
        public InstallCommand Command => _c;
        public double OfferSeconds => Math.Max(Settings.Current.ToastSeconds, 12);

        public InstallToast(ToastWindow w, ClipItem item)
        {
            _w = w; _item = item; _c = item.Install;
            _w.Closed += (s, e) =>
            {
                _clock?.Stop();
                // Closing the countdown toast is the same as pressing Cancel: nothing runs.
                if (State == "countdown") { State = "cancelled"; Activity.Add(ActivityKind.Service, "Install cancelled · " + _c.Display, "The toast was closed during the start delay. Nothing ran."); }
            };
        }

        private static Brush B(string k) => Ui.Res(k);

        // -------------------------------------------------------------- offer

        public void Offer()
        {
            if (!_c.Accepted) { Refused(); return; }
            var missing = InstallRunner.PathOf(_c.Manager) == null;
            State = missing ? "missing" : "offer";
            _w.SetAccent(Ui.C("C.Mint"), Ui.C("C.MintWash"));
            _w.SetHeader(Icons.Archive, "Install command", "");
            _w.SetWhen("now");
            _w.SetBody(OfferBody(missing));
            _w.SetHint(null);
            var actions = new List<ToastAction>();
            if (!missing)
            {
                var n = _c.Packages.Count;
                actions.Add(new ToastAction
                {
                    Label = n > 1 ? $"Install {n} packages" : "Install", Icon = ToastIcons.Download, Primary = true, Stay = true,
                    Tip = "Nothing runs until you press Install.", Run = () => Start(auto: false),
                });
            }
            actions.Add(CopyCommand(primary: missing));
            _w.SetActions(actions);
            _w.ExpandNow();
            _w.TipDefault = missing ? null : "Nothing runs until you press Install.";
            Core.Language.Refresh(_w);
        }

        private ToastAction CopyCommand(bool primary) => new ToastAction
        {
            Label = "Copy command", Icon = ToastIcons.Copy, Primary = primary, Done = "Copied",
            Tip = primary ? "Run it yourself in a terminal if you trust it." : "Nothing runs until you press Install.",
            Run = () => ClipboardWatcher.SetText(_item.Install.Raw),
        };

        private UIElement OfferBody(bool missing)
        {
            var root = new StackPanel();
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            int row = 0;
            void Row(string label, UIElement value)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var l = new TextBlock { Text = label, FontSize = 12, Foreground = B("B.TextSoft"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
                Grid.SetRow(l, row); grid.Children.Add(l);
                if (value is FrameworkElement fe) fe.Margin = new Thickness(0, 2, 0, 2);
                Grid.SetRow(value, row); Grid.SetColumn(value, 1); grid.Children.Add(value);
                row++;
            }

            var pills = new WrapPanel();
            foreach (var p in _c.Packages) { var pill = Ui.Pill(p, B("B.Sunken"), B("B.Text"), mono: true, size: 12); Core.Language.SetLiteral(pill, true); pills.Children.Add(pill); }
            Row(_c.Packages.Count > 1 ? "Packages" : "Package", pills);
            Row("Source", new TextBlock { Text = _c.SourceName + " (default)", FontSize = 12, Foreground = B("B.Text"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });

            var amber = _c.Permission == InstallPermission.ConsentAlways || _c.Permission == InstallPermission.ConsentExpected;
            var perm = new StackPanel { Orientation = Orientation.Horizontal };
            perm.Children.Add(new ContentControl { Content = Ui.Icon(amber ? Icons.Shield : Icons.Help, amber ? B("B.Amber") : B("B.TextSoft"), 14, 2), Margin = new Thickness(0, 2, 8, 0), VerticalAlignment = VerticalAlignment.Top });
            perm.Children.Add(new TextBlock { Text = PermissionText(_c), FontSize = 12, Foreground = B("B.Text"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, MaxWidth = 270 });
            Row("Permission", perm);
            root.Children.Add(grid);

            if (missing)
            {
                root.Children.Add(new TextBlock
                {
                    Text = _c.Manager == InstallManager.Chocolatey ? "Chocolatey is not installed on this computer, so nothing here can run it." : "winget is not installed on this computer, so nothing here can run it.",
                    FontSize = 12, Foreground = B("B.Amber"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
                });
            }
            return root;
        }

        internal static string PermissionText(InstallCommand c) => c.Permission switch
        {
            InstallPermission.ConsentAlways => "Windows will ask for permission (consent prompt)",
            InstallPermission.ConsentExpected => "Installing for every user. Windows will ask for permission.",
            InstallPermission.NoPromptExpected => "Installing for this account only. Windows should not ask.",
            _ => "The package's installer decides. Windows may ask.",
        };

        // -------------------------------------------------------------- refused

        public void Refused()
        {
            State = "refused";
            _w.SetAccent(Ui.C("C.Amber"), Ui.C("C.AmberWash"));
            _w.SetHeader(ToastIcons.Block, "Not offered to install", "");
            _w.SetWhen("now");
            var bad = new HashSet<string>(_c.Refusals.SelectMany(r => r.Token.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)), StringComparer.OrdinalIgnoreCase);

            var root = new StackPanel();
            var cmd = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = B("B.Text"), LineHeight = 18 };
            Core.Language.SetLiteral(cmd, true);
            var parts = System.Text.RegularExpressions.Regex.Split(_c.Raw, @"(\s+)");
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                var run = new Run(part.Replace("\r", "").Replace("\n", " "));
                if (!string.IsNullOrWhiteSpace(part) && bad.Contains(part)) run.Foreground = B("B.Amber");
                cmd.Inlines.Add(run);
            }
            root.Children.Add(new Border { Background = B("B.Sunken"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10), Child = cmd });

            var reasons = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            foreach (var r in _c.Refusals.Take(5))
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                line.Children.Add(new ContentControl { Content = Ui.Icon(ToastIcons.Block, B("B.Amber"), 14, 2), Margin = new Thickness(0, 1, 9, 0), VerticalAlignment = VerticalAlignment.Top });
                var text = new TextBlock { FontSize = 12, Foreground = B("B.Text"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
                // The token is the person's own text and stays as written; the reason is the app's wording and is translated.
                bool prefixed = r.Reason.StartsWith(r.Token + " ", StringComparison.Ordinal) || r.Reason.StartsWith(r.Token.Split(' ')[0] + " ", StringComparison.Ordinal);
                var token = new Run(prefixed ? r.Token.Split(' ')[0] : r.Token) { FontFamily = Ui.Font("F.Mono"), FontSize = 12 };
                text.Inlines.Add(token);
                var reason = prefixed ? r.Reason.Substring(r.Reason.IndexOf(' ') + 1) : r.Reason;
                text.Inlines.Add(new Run(prefixed ? " " : ": ") { });
                text.Inlines.Add(new Run(Core.Language.T(reason)));
                Core.Language.SetLiteral(text, true);
                line.Children.Add(text);
                reasons.Children.Add(line);
            }
            if (_c.Refusals.Count > 5) reasons.Children.Add(new TextBlock { Text = $"and {_c.Refusals.Count - 5} more", FontSize = 12, Foreground = B("B.TextSoft"), Margin = new Thickness(23, 2, 0, 0) });
            root.Children.Add(reasons);
            _w.SetBody(root);
            _w.SetHint(null);
            _w.SetActions(new List<ToastAction> { CopyCommand(primary: true) });
            _w.ExpandNow();
            _w.TipDefault = "Run it yourself in a terminal if you trust it.";
            Core.Language.Refresh(_w);
        }

        // -------------------------------------------------------------- automatic mode

        /// <summary>True when automatic mode is on and this exact command passes every automatic rule on this computer.</summary>
        public static bool AutoEligible(ClipItem item, out string why)
        {
            why = null;
            var s = Settings.Current;
            var c = item?.Install;
            if (c == null) { why = "not an install command"; return false; }
            if (!s.AutoInstallCommands) { why = "automatic mode is off"; return false; }
            why = c.AutoRefusal(Math.Clamp(s.AutoInstallMaxPackages, 1, 10));
            if (why != null) return false;
            if (InstallRunner.PathOf(c.Manager) == null) { why = c.Exe + " is not installed"; return false; }
            // Chocolatey is meant to run behind the Windows consent prompt every time. An app that is already an administrator never gets that prompt.
            if (c.Manager == InstallManager.Chocolatey && ServiceHost.IsElevated) { why = "the app is running as administrator, so Windows would not ask"; return false; }
            return true;
        }

        private static string _lastAuto; private static DateTime _lastAutoAt = DateTime.MinValue;
        internal static void ForgetLastAutoForTest() { _lastAuto = null; _lastAutoAt = DateTime.MinValue; }

        /// <summary>Shows the command with a visible countdown and a Cancel button, then starts it. The same command is not started twice in ten minutes.</summary>
        public static InstallToast ShowAuto(ToastManager toasts, ClipItem item, Func<ToastWindow, bool> present)
        {
            var c = item.Install;
            if (_lastAuto == c.Display && DateTime.UtcNow - _lastAutoAt < TimeSpan.FromMinutes(10)) { Log.Info("Automatic install skipped, the same command just ran: " + c.Display); return null; }
            var w = new ToastWindow();
            var t = new InstallToast(w, item) { _auto = true };
            t.Countdown(Math.Clamp(Settings.Current.AutoInstallDelaySeconds, 3, 60));
            if (!present(w))
            {
                // Nothing is on screen to cancel, so nothing runs.
                t._clock?.Stop(); t.State = "cancelled"; w.CloseAnimated();
                Log.Info("Automatic install not started, its toast could not be shown: " + c.Display); return null;
            }
            _lastAuto = c.Display; _lastAutoAt = DateTime.UtcNow;
            return t;
        }

        public void Countdown(int seconds)
        {
            State = "countdown";
            _w.SetAccent(Ui.C("C.Mint"), Ui.C("C.MintWash"));
            _w.SetHeader(Icons.Archive, "Installing automatically", $"starts in {seconds} s");
            _w.SetWhen("");
            var cmd = Ui.Mono(_c.Display, B("B.Text"), 12);
            cmd.TextWrapping = TextWrapping.Wrap; cmd.LineHeight = 18;
            Core.Language.SetLiteral(cmd, true);
            _w.SetBody(new Border { Background = B("B.Sunken"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10), Child = cmd });
            _w.SetHint("Automatic mode is on. Press Cancel to stop it.");
            var cancel = new ToastAction { Label = "Cancel", Icon = Icons.Close, Primary = true, Stay = true, Run = CancelCountdown };
            _w.SetActions(new List<ToastAction> { cancel });
            _w.ExpandNow();
            _w.StartTimer(0);
            Core.Language.Refresh(_w);
            int left = seconds;
            _clock?.Stop();
            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (s, e) =>
            {
                left--;
                if (State != "countdown") { _clock.Stop(); return; }
                if (left <= 0) { _clock.Stop(); Start(auto: true); return; }
                _w.MetaText.Text = $"starts in {left} s"; Core.Language.Refresh(_w.MetaText);
            };
            _clock.Start();
            _w.TimerScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromSeconds(seconds)));
        }

        private void CancelCountdown()
        {
            if (State != "countdown") return;
            _clock?.Stop();
            State = "cancelled";
            Activity.Add(ActivityKind.Service, "Install cancelled · " + _c.Display, "Cancelled during the start delay. Nothing ran.", ok: true);
            _w.CloseAnimated();
        }

        // -------------------------------------------------------------- running

        public void Start(bool auto)
        {
            if (State == "running") return;
            _auto = auto || _auto;
            State = "running";
            _cts = new CancellationTokenSource();
            _began = DateTime.Now;
            var n = _c.Packages.Count;
            _w.SetAccent(Ui.C("C.Mint"), Ui.C("C.MintWash"));
            _w.SetHeader(Icons.Archive, n > 1 ? $"Installing {n} packages" : "Installing", _c.Exe);
            _w.SetWhen("0 s");
            _w.SetHint(null);
            _w.TipDefault = null;
            BuildSteps();
            _w.SetActions(new List<ToastAction> { new ToastAction { Label = "Cancel", Icon = Icons.Close, Stay = true, Tip = "Stops waiting. An elevated install that already started cannot be stopped from here.", Run = () => _cts?.Cancel() } });
            _w.ExpandNow();
            _w.StartTimer(0);
            _w.SetProgress(0.1);
            Core.Language.Refresh(_w);

            _clock?.Stop();
            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (s, e) => { if (State != "running") { _clock.Stop(); return; } _w.SetWhen($"{(int)(DateTime.Now - _began).TotalSeconds} s"); };
            _clock.Start();

            var token = _cts.Token;
            Activity.Add(ActivityKind.Service, "Install started · " + _c.Display, _auto ? "Started by automatic mode." : "Started with the Install button.");
            // The consent prompt blocks the thread that starts the process until it is answered, so the run never starts on the UI thread.
            Task.Run(() => InstallRunner.RunAsync(_c, step => _w.Dispatcher.BeginInvoke(new Action(() => OnStep(step))), token))
                 .ContinueWith(t => _w.Dispatcher.BeginInvoke(new Action(() => Finish(t))));
        }

        private bool ConsentStep => _c.Manager == InstallManager.Chocolatey && !ServiceHost.IsElevated;

        private void BuildSteps()
        {
            _steps.Clear();
            var root = new StackPanel();
            StepRow(root, "Command checked", null);
            if (ConsentStep) StepRow(root, "Waiting for the Windows consent prompt", "The app never answers it for you.");
            StepRow(root, Core.Language.T("Install") + " " + string.Join(", ", _c.Packages), null, literalTail: true);
            _barTrack = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = B("B.Sunken"), Margin = new Thickness(0, 10, 0, 0), ClipToBounds = true };
            _barFill = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = B("B.Mint"), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            _barTrack.Child = _barFill;
            root.Children.Add(_barTrack);
            _w.SetBody(root);
            SetStep(0, "active");
            for (int i = 1; i < _steps.Count; i++) SetStep(i, "pending");
            SetBar(0.12);
        }

        private void StepRow(StackPanel root, string text, string sub, bool literalTail = false)
        {
            var mark = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 10, 0) };
            var tb = new TextBlock { Text = text, FontSize = 14, Foreground = B("B.Text"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, MaxWidth = 290 };
            if (literalTail) Core.Language.SetLiteral(tb, true);
            var subtb = new TextBlock { Text = sub ?? "", FontSize = 12, Foreground = B("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Visibility = sub == null ? Visibility.Collapsed : Visibility.Visible };
            var col = new StackPanel(); col.Children.Add(tb); col.Children.Add(subtb);
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            line.Children.Add(mark); line.Children.Add(col);
            root.Children.Add(line);
            _steps.Add((mark, tb, subtb));
        }

        private void SetStep(int i, string kind)
        {
            if (i < 0 || i >= _steps.Count) return;
            var m = _steps[i].Mark;
            switch (kind)
            {
                case "done": m.Background = B("B.Mint"); m.BorderThickness = new Thickness(0); m.Child = Ui.Icon(Icons.Check, B("B.MintInk"), 11, 2.8); break;
                case "consent": m.Background = Brushes.Transparent; m.BorderThickness = new Thickness(0); m.Child = Ui.Icon(Icons.Shield, B("B.Amber"), 18, 2); break;
                case "active": m.Background = Brushes.Transparent; m.BorderBrush = B("B.Mint"); m.BorderThickness = new Thickness(2); m.Child = null; break;
                default: m.Background = Brushes.Transparent; m.BorderBrush = B("B.Line"); m.BorderThickness = new Thickness(2); m.Child = null; _steps[i].Text.Foreground = B("B.TextSoft"); break;
            }
            if (kind != "pending") _steps[i].Text.Foreground = B("B.Text");
        }

        private void SetBar(double fraction)
        {
            if (_barFill == null) return;
            _barTrack.UpdateLayout();
            double width = _barTrack.ActualWidth > 0 ? _barTrack.ActualWidth : 330;
            _barFill.Width = Math.Max(0, Math.Min(1, fraction)) * width;
            _barFill.Tag = fraction;
            _w.SetProgress(fraction);
        }

        private void OnStep(string step)
        {
            if (State != "running") return;
            int at = ConsentStep ? 1 : 0;
            switch (step)
            {
                case "check": SetStep(0, "active"); break;
                case "consent": SetStep(0, "done"); SetStep(1, "consent"); SetBar(0.3); break;
                case "install":
                    SetStep(0, "done");
                    if (ConsentStep) SetStep(1, "done");
                    SetStep(_steps.Count - 1, "active");
                    SetBar(0.6);
                    break;
            }
        }

        // -------------------------------------------------------------- results

        private void Finish(Task<InstallResult> t)
        {
            _clock?.Stop();
            var r = t.IsFaulted ? new InstallResult { Ok = false, ExitCode = -1, Message = t.Exception?.GetBaseException().Message ?? "Failed" } : t.Result;
            Result = r;
            var secs = Math.Max(0, (int)Math.Round(r.Took.TotalSeconds > 0 ? r.Took.TotalSeconds : (DateTime.Now - _began).TotalSeconds));
            Activity.Add(ActivityKind.Service, $"Install {(r.Ok ? "finished" : r.Declined ? "declined" : r.Cancelled ? "cancelled" : "failed")} · {_c.Display}",
                $"exit code {r.ExitCode}; {r.Message}{(r.Versions.Count > 0 ? "; " + string.Join(", ", r.Versions.Select(v => v.Key + " " + v.Value)) : "")}", ok: r.Ok || r.Cancelled);
            if (_w.IsClosing) return;
            _w.SetProgress(1);
            Sounds.Play(r.Ok ? Cue.Complete : Cue.Warning);
            if (r.Ok) Done(r, secs);
            else if (r.Declined) Declined(r, secs);
            else if (r.Cancelled) Cancelled(r);
            else Failed(r, secs);
        }

        private string Summary(InstallResult r, int secs)
        {
            var what = string.Join(", ", _c.Packages.Select(p => r.Versions.TryGetValue(p, out var v) ? p + " " + v : p));
            return (r.Ok ? $"Installed {what} with {_c.Exe} in {secs} s." : $"Not installed: {what}. {r.Message}") + $"\nCommand: {_c.Display}\nExit code: {r.ExitCode}";
        }

        private void Done(InstallResult r, int secs)
        {
            State = "done";
            _w.SetAccent(Ui.C("C.Mint"), Ui.C("C.MintWash"));
            var vers = string.Join(" · ", _c.Packages.Select(p => r.Versions.TryGetValue(p, out var v) ? p + " " + v : p));
            _w.SetHeader(Icons.Check, "Installed", vers);
            _w.MetaText.FontFamily = Ui.Font("F.Mono");
            _w.SetWhen(secs + " s");
            var root = new StackPanel();
            var pills = new WrapPanel();
            foreach (var p in _c.Packages) { var pill = Ui.Pill(r.Versions.TryGetValue(p, out var v) ? p + " " + v : p, B("B.Sunken"), B("B.Text"), mono: true, size: 12); Core.Language.SetLiteral(pill, true); pills.Children.Add(pill); }
            root.Children.Add(pills);
            root.Children.Add(new TextBlock { Text = $"Took {secs} s. The command is saved in Activity.", FontSize = 12, Foreground = B("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 6, 0, 0) });
            if (r.ExitCode != 0) root.Children.Add(new TextBlock { Text = "Windows asks for a restart to finish.", FontSize = 12, Foreground = B("B.Amber"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 4, 0, 0) });
            _w.SetBody(root);
            _w.SetHint(null);
            var actions = new List<ToastAction>();
            if (!string.IsNullOrEmpty(r.LogPath)) actions.Add(new ToastAction { Label = "Open log", Icon = Icons.File, Primary = true, HandsOff = true, Run = () => ActionProvider.Shell(r.LogPath) });
            actions.Add(new ToastAction { Label = "Copy summary", Icon = ToastIcons.Copy, Primary = actions.Count == 0, Done = "Copied", Run = () => ClipboardWatcher.SetText(Summary(r, secs)) });
            _w.SetActions(actions);
            _w.ExpandNow();
            _w.StartTimer(10);
            Core.Language.Refresh(_w);
        }

        private void Declined(InstallResult r, int secs)
        {
            State = "declined";
            _w.SetAccent(Ui.C("C.Danger"), Ui.C("C.DangerWash"));
            _w.SetHeader(ToastIcons.Block, "Not installed", _c.Exe);
            _w.SetWhen(secs + " s");
            var root = new StackPanel();
            root.Children.Add(new TextBlock { Text = r.Message, FontSize = 14, Foreground = B("B.Text"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            var code = Ui.Mono($"exit code {r.ExitCode} · cancelled by the user", B("B.TextSoft"), 12);
            code.Margin = new Thickness(0, 8, 0, 0);
            root.Children.Add(code);
            _w.SetBody(root);
            _w.SetHint(null);
            _w.SetActions(RetryActions(r, secs));
            _w.ExpandNow();
            _w.StartTimer(0);
            Core.Language.Refresh(_w);
        }

        private void Failed(InstallResult r, int secs)
        {
            State = "failed";
            _w.SetAccent(Ui.C("C.Danger"), Ui.C("C.DangerWash"));
            _w.SetHeader(ToastIcons.Block, "Not installed", _c.Exe);
            _w.SetWhen(secs + " s");
            var root = new StackPanel();
            root.Children.Add(new TextBlock { Text = r.Message, FontSize = 14, Foreground = B("B.Text"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            var code = Ui.Mono($"exit code {r.ExitCode}", B("B.TextSoft"), 12);
            code.Margin = new Thickness(0, 8, 0, 0);
            root.Children.Add(code);
            if (!string.IsNullOrWhiteSpace(r.Output))
            {
                var tail = Ui.Mono(ActionProvider.Tail(r.Output, 4), B("B.TextSoft"), 11);
                tail.TextWrapping = TextWrapping.Wrap; tail.MaxHeight = 70; tail.Margin = new Thickness(0, 6, 0, 0);
                Core.Language.SetLiteral(tail, true);
                root.Children.Add(tail);
            }
            _w.SetBody(root);
            _w.SetHint(null);
            _w.SetActions(RetryActions(r, secs));
            _w.ExpandNow();
            _w.StartTimer(0);
            Core.Language.Refresh(_w);
        }

        private void Cancelled(InstallResult r)
        {
            State = "cancelled";
            _w.SetAccent(Ui.C("C.Amber"), Ui.C("C.AmberWash"));
            _w.SetHeader(ToastIcons.Block, "Cancelled", _c.Exe);
            _w.SetBody(new TextBlock { Text = r.Message, FontSize = 14, Foreground = B("B.Text"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            _w.SetHint(null);
            _w.SetActions(new List<ToastAction> { new ToastAction { Label = "Try again", Icon = Icons.Restart, Primary = true, Stay = true, Run = () => Start(auto: false) }, CopyCommand(primary: false) });
            _w.ExpandNow();
            _w.StartTimer(8);
            Core.Language.Refresh(_w);
        }

        private List<ToastAction> RetryActions(InstallResult r, int secs)
        {
            var l = new List<ToastAction>
            {
                new ToastAction { Label = "Try again", Icon = Icons.Restart, Primary = true, Stay = true, Run = () => Start(auto: false) },
                new ToastAction { Label = "Copy details", Icon = ToastIcons.Copy, Done = "Copied", Run = () => ClipboardWatcher.SetText(Summary(r, secs) + (string.IsNullOrWhiteSpace(r.Output) ? "" : "\n" + ActionProvider.Tail(r.Output, 12))) },
            };
            if (!string.IsNullOrEmpty(r.LogPath)) l.Add(new ToastAction { Label = "Open log", Icon = Icons.File, HandsOff = true, Run = () => ActionProvider.Shell(r.LogPath) });
            return l;
        }
    }
}
