using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public enum StepState { Pending, Running, Done, Skipped, Failed }

    /// <summary>A recovery choice shown beside the control where an operation failed.</summary>
    public sealed class RecoveryAction
    {
        public string Label;
        public Action Run;
        public RecoveryAction(string label, Action run) { Label = label; Run = run; }
    }

    /// <summary>What a finished operation reports back to its card.</summary>
    public sealed class OperationOutcome
    {
        public bool Ok;
        public string Summary;
        public string Details;
        public List<RecoveryAction> Recovery = new List<RecoveryAction>();
        public static OperationOutcome Success(string summary, string details = null) => new OperationOutcome { Ok = true, Summary = summary, Details = details };
        public static OperationOutcome Failure(string summary, string details, params RecoveryAction[] recovery) =>
            new OperationOutcome { Ok = false, Summary = summary, Details = details, Recovery = recovery.ToList() };
    }

    /// <summary>The handle an operation uses to report its own progress to the card. Safe to call from any thread.</summary>
    public sealed class OperationRun
    {
        private readonly OperationCard _card;
        public CancellationToken Token { get; }
        internal OperationRun(OperationCard card, CancellationToken token) { _card = card; Token = token; }
        public void Step(int index, StepState state, string detail = null) => _card.Dispatch(() => _card.SetStep(index, state, detail));
        public void Message(string text) => _card.Dispatch(() => _card.SetMessage(text));
        /// <summary>Reports a fraction from 0 to 1. Without a call to this the bar is indeterminate while running.</summary>
        public void Progress(double fraction) => _card.Dispatch(() => _card.SetFraction(fraction));
    }

    /// <summary>
    /// Shows a long operation where it was started: named steps, a progress bar, Cancel while it runs, and on failure a plain
    /// message with Retry, Copy details and any operation-specific recovery actions. Re-entry is refused in the handler,
    /// not only by disabling the submitting control, so a keyboard submit cannot start a second run.
    /// </summary>
    public sealed class OperationCard : Border
    {
        private readonly StackPanel _stack = new StackPanel();
        private readonly TextBlock _title = new TextBlock();
        private readonly TextBlock _message = new TextBlock();
        private readonly StackPanel _steps = new StackPanel();
        private readonly ProgressBar _bar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 8, 0, 4) };
        private readonly WrapPanel _actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        private readonly List<(TextBlock Glyph, TextBlock Label, TextBlock Detail)> _stepViews = new List<(TextBlock, TextBlock, TextBlock)>();
        private CancellationTokenSource _cts;
        private Func<OperationRun, Task<OperationOutcome>> _lastWork;
        private OperationOutcome _lastOutcome;
        private readonly List<UIElement> _guarded = new List<UIElement>();
        private bool _running;

        public bool IsRunning => _running;
        public event Action<bool> RunningChanged;

        public OperationCard(string title)
        {
            Background = Ui.Res("B.Raised");
            CornerRadius = new CornerRadius(20);
            Padding = new Thickness(18, 14, 18, 14);
            Margin = new Thickness(0, 8, 0, 8);
            Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(this, title + " progress");
            _title.Text = title;
            _title.Style = Ui.Style("T.Title");
            _title.FontSize = 15;
            _message.Style = Ui.Style("T.Body");
            _message.TextWrapping = TextWrapping.Wrap;
            _message.Foreground = Ui.Res("B.TextSoft");
            _message.Margin = new Thickness(0, 4, 0, 4);
            AutomationProperties.SetLiveSetting(_message, AutomationLiveSetting.Polite);
            _bar.Foreground = Ui.Res("B.Mint");
            _bar.Background = Ui.Res("B.Sunken");
            _stack.Children.Add(_title);
            _stack.Children.Add(_message);
            _stack.Children.Add(_steps);
            _stack.Children.Add(_bar);
            _stack.Children.Add(_actions);
            Child = _stack;
        }

        /// <summary>Disables these controls for the whole run, as the visible half of the re-entry guard.</summary>
        public void Guard(params UIElement[] controls) => _guarded.AddRange(controls.Where(c => c != null));

        public void SetSteps(params string[] labels)
        {
            _steps.Children.Clear();
            _stepViews.Clear();
            foreach (var l in labels)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                var glyph = new TextBlock { Width = 22, FontSize = 13, Foreground = Ui.Res("B.TextMute"), Text = "○", VerticalAlignment = VerticalAlignment.Top };
                var label = new TextBlock { Text = l, Style = Ui.Style("T.Body"), FontSize = 13, Foreground = Ui.Res("B.Text") };
                var detail = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(8, 0, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
                row.Children.Add(glyph); row.Children.Add(label); row.Children.Add(detail);
                _steps.Children.Add(row);
                _stepViews.Add((glyph, label, detail));
            }
        }

        internal void Dispatch(Action a)
        {
            if (Dispatcher.CheckAccess()) a(); else Dispatcher.BeginInvoke(a);
        }

        internal void SetMessage(string t) { _message.Text = t ?? ""; }

        internal void SetFraction(double f)
        {
            _bar.IsIndeterminate = false;
            _bar.Value = Math.Max(0, Math.Min(1, f));
        }

        internal void SetStep(int i, StepState state, string detail)
        {
            if (i < 0 || i >= _stepViews.Count) return;
            var (g, l, d) = _stepViews[i];
            // Words as well as glyphs, so the state never depends on colour or shape alone.
            g.Text = state switch { StepState.Done => "✓", StepState.Running => "…", StepState.Failed => "✕", StepState.Skipped => "–", _ => "○" };
            g.Foreground = Ui.Res(state == StepState.Done ? "B.Mint" : state == StepState.Failed ? "B.Danger" : state == StepState.Running ? "B.Text" : "B.TextMute");
            if (detail != null) d.Text = detail;
            AutomationProperties.SetName(l, $"{l.Text}: {state.ToString().ToLowerInvariant()}{(string.IsNullOrEmpty(d.Text) ? "" : ", " + d.Text)}");
        }

        /// <summary>Runs the operation. Returns false without doing anything when a run is already active.</summary>
        public async Task<bool> RunAsync(Func<OperationRun, Task<OperationOutcome>> work)
        {
            if (_running) return false;                      // the real guard: a keyboard submit gets here too
            _running = true;
            _lastWork = work;
            _cts = new CancellationTokenSource();
            Visibility = Visibility.Visible;
            _actions.Children.Clear();
            _message.Text = "Working…";
            _message.Foreground = Ui.Res("B.TextSoft");
            _bar.IsIndeterminate = true;
            _bar.Visibility = Visibility.Visible;
            for (int i = 0; i < _stepViews.Count; i++) { SetStep(i, StepState.Pending, ""); }
            foreach (var g in _guarded) g.IsEnabled = false;
            var cancel = Ui.Button("Cancel", () => { try { _cts?.Cancel(); } catch { } _message.Text = "Cancelling… the current step will stop as soon as it safely can."; }, "Btn.Ghost");
            AutomationProperties.SetName(cancel, "Cancel " + _title.Text);
            _actions.Children.Add(cancel);
            RunningChanged?.Invoke(true);

            OperationOutcome outcome;
            try { outcome = await work(new OperationRun(this, _cts.Token)); }
            catch (OperationCanceledException) { outcome = OperationOutcome.Failure("Cancelled", "The operation was cancelled before it finished. Anything already completed was left as it is."); }
            catch (Exception ex) { Log.Warn(_title.Text + " failed: " + ex.Message); outcome = OperationOutcome.Failure("Something went wrong", ex.Message); }
            if (_cts.IsCancellationRequested && outcome.Ok == false && outcome.Summary == null) outcome.Summary = "Cancelled";

            _lastOutcome = outcome;
            _running = false;
            _cts.Dispose(); _cts = null;
            foreach (var g in _guarded) g.IsEnabled = true;
            _bar.IsIndeterminate = false;
            ShowOutcome(outcome);
            RunningChanged?.Invoke(false);
            return outcome.Ok;
        }

        private void ShowOutcome(OperationOutcome o)
        {
            _actions.Children.Clear();
            _bar.Visibility = Visibility.Collapsed;
            _message.Text = (o.Ok ? "Done: " : "Did not finish: ") + o.Summary + (string.IsNullOrWhiteSpace(o.Details) ? "" : "\n" + o.Details.Trim());
            _message.Foreground = Ui.Res(o.Ok ? "B.Mint" : "B.Amber");
            if (o.Ok)
            {
                foreach (var r in o.Recovery) _actions.Children.Add(Ui.Button(r.Label, r.Run));
                _actions.Children.Add(Ui.Button("Dismiss", () => Visibility = Visibility.Collapsed));
                return;
            }
            // Recovery sits beside the control where the failure surfaced.
            _actions.Children.Add(Ui.Button("Retry", () => { var w = _lastWork; if (w != null) _ = RunAsync(w); }, "Btn.Accent"));
            foreach (var r in o.Recovery) _actions.Children.Add(Ui.Button(r.Label, r.Run));
            _actions.Children.Add(Ui.Button("Copy details", () =>
            {
                try { MonitoringTools.Clip.ClipboardWatcher.SetText(_title.Text + ": " + o.Summary + "\n" + (o.Details ?? "")); _message.Text += "\nDetails copied."; } catch { }
            }));
            _actions.Children.Add(Ui.Button("Dismiss", () => Visibility = Visibility.Collapsed));
        }
    }
}
