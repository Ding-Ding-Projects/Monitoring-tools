using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MonitoringTools.Core;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MonitoringTools.UI
{
    /// <summary>One step of a guided form. <see cref="Build"/> runs once, so going Back keeps what was entered.</summary>
    public sealed class GuidedStep
    {
        public string Title { get; init; }
        /// <summary>A sentence or two on what this step is for.</summary>
        public string Help { get; init; }
        public Func<FrameworkElement> Build { get; init; }
        /// <summary>Returns an error message when the step cannot be left yet, or null when it is fine.</summary>
        public Func<string> Validate { get; init; }
        /// <summary>Runs each time the step is shown, for steps that summarise earlier answers.</summary>
        public Action Entered { get; init; }
    }

    /// <summary>
    /// A reusable step-by-step form: a progress indicator, one step at a time, Back and Next, a validation message that blocks
    /// leaving an invalid step, Cancel, and a final action that reports its own result honestly.
    /// </summary>
    public sealed class GuidedForm : Border
    {
        private readonly IReadOnlyList<GuidedStep> _steps;
        private readonly Func<string> _finish;
        private readonly Action _cancel;
        private readonly FrameworkElement[] _built;
        private readonly TextBlock _progress = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.TextMute") };
        private readonly StackPanel _segments = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 12) };
        private readonly TextBlock _title = new TextBlock { Style = Ui.Style("T.Title"), FontSize = 16 };
        private readonly TextBlock _help = new TextBlock { Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 12) };
        private readonly ContentControl _host = new ContentControl { IsTabStop = false };
        private readonly TextBlock _error = new TextBlock { Foreground = Ui.Res("B.Danger"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        private readonly Button _back, _next, _cancelBtn;
        private int _index;
        private bool _errorIsProblem;

        /// <summary>Raised after the final action succeeded.</summary>
        public event Action Finished;

        /// <summary>Raised whenever a different step is shown, so a host that rebuilds the page can resume on the same step.</summary>
        public event Action<int> StepChanged;

        /// <summary>
        /// Optional asynchronous final action. When set it replaces the synchronous one: the buttons are disabled while it runs
        /// and it returns null on success or a message to show and stay. Use <see cref="Report"/> from it for progress text.
        /// </summary>
        public Func<Task<string>> FinishAsync { get; set; }

        public int StepIndex => _index;

        /// <param name="finish">Runs on the last step's button. Return null on success or a message to show and stay.</param>
        public GuidedForm(string title, IReadOnlyList<GuidedStep> steps, string finishLabel, Func<string> finish, Action cancel, int startStep = 0)
        {
            _steps = steps; _finish = finish; _cancel = cancel; _formTitle = title;
            _built = new FrameworkElement[steps.Count];
            Background = Ui.Res("B.Surface"); CornerRadius = new CornerRadius(28); Padding = new Thickness(22); Margin = new Thickness(0, 0, 0, 16);
            AutomationProperties.SetName(this, title);

            var root = new StackPanel();
            root.Children.Add(new TextBlock { Text = title, Style = Ui.Style("T.Title"), FontSize = 16 });
            root.Children.Add(_segments);
            root.Children.Add(_progress);
            root.Children.Add(_title);
            root.Children.Add(_help);
            root.Children.Add(_host);
            AutomationProperties.SetLiveSetting(_error, AutomationLiveSetting.Assertive);
            root.Children.Add(_error);
            // the finish step's work: indeterminate until a report carries a percentage, then it glides to it
            _work = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed, IsIndeterminate = true };
            AutomationProperties.SetName(_work, "Progress");
            root.Children.Add(_work);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            _cancelBtn = Ui.Button("Cancel", CancelNow, "Btn.Ghost");
            _back = Ui.Button("Back", () => Go(-1), "Btn.Ghost");
            _next = Ui.Button("Next", Next, "Btn.Accent");
            _finishLabel = finishLabel;
            bar.Children.Add(_cancelBtn); bar.Children.Add(_back); bar.Children.Add(_next);
            root.Children.Add(bar);
            Child = root;

            // A validation message describes what was entered when Next was pressed; once the person edits a field or flips a switch it is stale, so it goes.
            AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => ClearStaleError()));
            AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent, new RoutedEventHandler((s, e) => ClearStaleError()));
            AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent, new RoutedEventHandler((s, e) => ClearStaleError()));
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && !(Keyboard.FocusedElement is TextBox tb && tb.AcceptsReturn)) { e.Handled = true; CancelNow(); }
            };
            Show(startStep);
        }

        private readonly string _finishLabel;
        private readonly string _formTitle;

        /// <summary>
        /// Cancel throws away what was typed, so it is recorded in the local history first, where it can be copied back.
        /// Only text fields are kept: passwords are never in a text box here, and switches and choices are not work.
        /// </summary>
        private void CancelNow()
        {
            try
            {
                var lines = new List<string>();
                foreach (var built in _built) if (built != null) CollectTyped(built, lines);
                if (lines.Count > 0) LocalHistory.RecordDiscard("the " + _formTitle + " form", string.Join("\n", lines));
            }
            catch (Exception ex) { Log.Warn("The cancelled form was not recorded: " + ex.Message); }
            _cancel?.Invoke();
        }

        private static void CollectTyped(object node, List<string> lines)
        {
            if (node is TextBox tb && !tb.IsReadOnly && !string.IsNullOrWhiteSpace(tb.Text))
            {
                var name = AutomationProperties.GetName(tb);
                lines.Add((string.IsNullOrWhiteSpace(name) ? "Text" : name) + ": " + tb.Text);
                return;
            }
            if (node is DependencyObject d) foreach (var child in LogicalTreeHelper.GetChildren(d)) CollectTyped(child, lines);
        }

        private void Show(int i)
        {
            _index = Math.Clamp(i, 0, _steps.Count - 1);
            var step = _steps[_index];
            _built[_index] ??= step.Build();
            step.Entered?.Invoke();
            _host.Content = _built[_index];
            _title.Text = step.Title;
            _help.Text = step.Help ?? "";
            _help.Visibility = string.IsNullOrEmpty(step.Help) ? Visibility.Collapsed : Visibility.Visible;
            _progress.Text = $"Step {_index + 1} of {_steps.Count}";
            _error.Visibility = Visibility.Collapsed;
            _segments.Children.Clear();
            for (int s = 0; s < _steps.Count; s++)
                _segments.Children.Add(new Border { Width = 36, Height = 5, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 6, 0), Background = s <= _index ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong") });
            AutomationProperties.SetItemStatus(this, $"Step {_index + 1} of {_steps.Count}: {step.Title}");
            _back.IsEnabled = _index > 0;
            _next.Content = _index == _steps.Count - 1 ? _finishLabel : "Next";
            StepChanged?.Invoke(_index);
        }

        private void Go(int delta) => Show(_index + delta);

        /// <summary>Returns to an earlier step, as a step rail beside the form does. Later steps are reached with Next, so their checks run.</summary>
        public void GoTo(int step) { if (step >= 0 && step < _index) Show(step); }

        private async void Next()
        {
            var err = _steps[_index].Validate?.Invoke();
            if (err != null) { Fail(err); return; }
            if (_index < _steps.Count - 1) { Show(_index + 1); return; }
            string result;
            if (FinishAsync != null)
            {
                _next.IsEnabled = _back.IsEnabled = _cancelBtn.IsEnabled = false;
                Working = true;
                _work.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null);
                _work.Value = 0; _work.IsIndeterminate = true; _work.Visibility = Visibility.Visible;
                Ui.Expand(_work);
                try { result = await FinishAsync(); } catch (Exception ex) { result = ex.Message; }
                finally { Working = false; _work.Visibility = Visibility.Collapsed; }
                _next.IsEnabled = _cancelBtn.IsEnabled = true; _back.IsEnabled = _index > 0;
            }
            else
            {
                try { result = _finish?.Invoke(); } catch (Exception ex) { result = ex.Message; }
            }
            if (result != null) { Fail(result); return; }
            Finished?.Invoke();
        }

        /// <summary>Shows a progress or result line under the step without blocking it. <paramref name="problem"/> uses the error colour.</summary>
        private void ClearStaleError()
        {
            if (_errorIsProblem) { _errorIsProblem = false; _error.Visibility = Visibility.Collapsed; }
        }

        private readonly ProgressBar _work;

        /// <summary>True while the finish step's work runs (the bar shows and the buttons wait).</summary>
        public bool Working { get; private set; }

        /// <summary>The finish step's bar: the last percentage reported, or null while it is indeterminate.</summary>
        public double? WorkPercent => _work.IsIndeterminate ? (double?)null : _work.Value;

        private static readonly System.Text.RegularExpressions.Regex Pct = new System.Text.RegularExpressions.Regex(@"(\d{1,3}(?:\.\d+)?)\s?%");

        public void Report(string message, bool problem = false)
        {
            if (Working && !string.IsNullOrEmpty(message))
            {
                var m = Pct.Match(message);
                if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                { _work.IsIndeterminate = false; Ui.Glide(_work, Math.Max(0, Math.Min(100, v)), 400); }
                else { _work.IsIndeterminate = true; }
            }
            _errorIsProblem = false;
            _error.Text = message;
            _error.Foreground = Ui.Res(problem ? "B.Danger" : "B.TextMute");
            _error.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Fail(string message)
        {
            _errorIsProblem = true;
            _error.Text = message;
            _error.Foreground = Ui.Res("B.Danger");
            _error.Visibility = Visibility.Visible;
        }
    }
}
