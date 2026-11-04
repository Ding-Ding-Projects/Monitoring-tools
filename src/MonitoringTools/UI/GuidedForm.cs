using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
        private readonly TextBlock _title = new TextBlock { Style = Ui.Style("T.Title"), FontSize = 18 };
        private readonly TextBlock _help = new TextBlock { Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 12) };
        private readonly ContentControl _host = new ContentControl { IsTabStop = false };
        private readonly TextBlock _error = new TextBlock { Foreground = Ui.Res("B.Danger"), FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        private readonly Button _back, _next, _cancelBtn;
        private int _index;

        /// <summary>Raised after the final action succeeded.</summary>
        public event Action Finished;

        /// <param name="finish">Runs on the last step's button. Return null on success or a message to show and stay.</param>
        public GuidedForm(string title, IReadOnlyList<GuidedStep> steps, string finishLabel, Func<string> finish, Action cancel)
        {
            _steps = steps; _finish = finish; _cancel = cancel;
            _built = new FrameworkElement[steps.Count];
            Background = Ui.Res("B.Surface"); CornerRadius = new CornerRadius(24); Padding = new Thickness(22); Margin = new Thickness(0, 0, 0, 16);
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

            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            _cancelBtn = Ui.Button("Cancel", () => _cancel?.Invoke(), "Btn.Ghost");
            _back = Ui.Button("Back", () => Go(-1), "Btn.Ghost");
            _next = Ui.Button("Next", Next, "Btn.Accent");
            _finishLabel = finishLabel;
            bar.Children.Add(_cancelBtn); bar.Children.Add(_back); bar.Children.Add(_next);
            root.Children.Add(bar);
            Child = root;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && !(Keyboard.FocusedElement is TextBox tb && tb.AcceptsReturn)) { e.Handled = true; _cancel?.Invoke(); }
            };
            Show(0);
        }

        private readonly string _finishLabel;

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
        }

        private void Go(int delta) => Show(_index + delta);

        private void Next()
        {
            var err = _steps[_index].Validate?.Invoke();
            if (err != null) { Fail(err); return; }
            if (_index < _steps.Count - 1) { Show(_index + 1); return; }
            string result;
            try { result = _finish?.Invoke(); } catch (Exception ex) { result = ex.Message; }
            if (result != null) { Fail(result); return; }
            Finished?.Invoke();
        }

        private void Fail(string message)
        {
            _error.Text = message;
            _error.Visibility = Visibility.Visible;
        }
    }
}
