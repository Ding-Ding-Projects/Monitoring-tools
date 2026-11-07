using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>
    /// One reusable Material 3 number field: a label, a text box that accepts typing, minus and plus steppers, an optional unit,
    /// inline validation with a clear message, and a subtitle that says what the setting does and its default.
    /// A value is only stored through the commit callback, and only when it parses and sits inside the allowed range;
    /// anything else is refused with a message and the last good value stays. Commit happens on Enter or when focus leaves the box.
    /// Keys in the box: Up and Down step, Page Up and Page Down step ten times as far, Enter commits, Escape puts the last good value back.
    /// </summary>
    public sealed class NumberField : StackPanel
    {
        private readonly Action<double> _commit;
        private readonly string _label;
        private readonly int _decimals;
        private readonly bool _snap;
        private readonly TextBlock _message;
        private readonly Button _minus, _plus;
        private double _value;

        public TextBox Box { get; }
        public double Min { get; }
        public double Max { get; }
        public double StepSize { get; }
        public string Label => _label;
        /// <summary>The last good value: the one that was stored.</summary>
        public double Value => _value;
        /// <summary>The inline message under the box (empty when there is none).</summary>
        public string Message => _message.Text;

        /// <param name="label">The setting's name, also the accessible name of the box.</param>
        /// <param name="sub">What the setting does and its default, shown under the label. Null for a compact field that sits beside another control.</param>
        /// <param name="snap">Round a typed value to the nearest step instead of refusing it (for a setting whose consumer rounds anyway).</param>
        /// <param name="compact">No label or subtitle: only the box, steppers and unit, for use beside a slider.</param>
        public NumberField(string label, string sub, double value, double min, double max, double step, Action<double> commit,
            string unit = null, int decimals = 0, bool snap = false, bool compact = false)
        {
            _label = label; _commit = commit; _decimals = Math.Max(0, decimals); _snap = snap;
            Min = min; Max = max; StepSize = step <= 0 ? 1 : step;
            _value = value;
            Margin = compact ? new Thickness(0, 0, 0, 0) : new Thickness(0, Appearance.Gap(4), 0, Appearance.Gap(10));

            if (!compact)
            {
                // the label wraps: a long one at a large text size goes onto a second line instead of being cut
                Children.Add(new StackPanel
                {
                    Margin = new Thickness(0, 0, 0, string.IsNullOrWhiteSpace(sub) ? 8 : 2),
                    Children = { new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap } }
                });
                if (!string.IsNullOrWhiteSpace(sub))
                    Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
            }

            var row = new WrapPanel();
            _minus = Stepper("−", "Decrease " + label, -1);
            Box = new TextBox { Style = Ui.Style("Input"), Text = Format(value), Width = compact ? 64 : 96, Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Left, MaxLength = 16 };
            AutomationProperties.SetName(Box, label);
            if (!string.IsNullOrWhiteSpace(sub)) AutomationProperties.SetHelpText(Box, sub);
            Box.ToolTip = $"Type a number from {Format(min)} to {Format(max)}. Up and Down keys step it.";
            _plus = Stepper("+", "Increase " + label, +1);
            if (compact) { Box.Height = 32; Box.FontSize = 12; _minus.Height = _plus.Height = 28; _minus.Width = _plus.Width = 28; }
            _minus.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(_minus);
            row.Children.Add(Box);
            if (!string.IsNullOrWhiteSpace(unit))
                row.Children.Add(new TextBlock { Text = unit, Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            row.Children.Add(_plus);
            Children.Add(row);

            _message = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
            AutomationProperties.SetLiveSetting(_message, AutomationLiveSetting.Polite);
            Children.Add(_message);

            Box.PreviewKeyDown += OnKey;
            Box.KeyDown += OnKey;   // raised on its own when a key event is delivered without a tunnelling pass
            Box.LostKeyboardFocus += (s, e) => { if (!IsKeyboardFocusWithin) Leave(); };
            Box.TextChanged += (s, e) => { if (Box.IsKeyboardFocusWithin && _message.Visibility == Visibility.Visible && _isError) ShowMessage(null, false); };
            Paint();
            if (!compact) SearchScope.SetItem(this, "setting choice number field " + Format(value));
        }

        private bool _isError;

        private Button Stepper(string glyph, string name, int dir)
        {
            var b = Ui.Button(glyph, null, "Btn.Chip");
            b.Width = 36; b.Padding = new Thickness(0); b.FontSize = 16; b.Margin = new Thickness(0);
            AutomationProperties.SetName(b, name);
            b.ToolTip = name;
            b.Click += (s, e) => StepBy(dir);
            return b;
        }

        private string Format(double v) => v.ToString("F" + _decimals, CultureInfo.InvariantCulture);

        private void OnKey(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter: e.Handled = true; Commit(); break;
                case Key.Up: e.Handled = true; StepBy(+1); break;
                case Key.Down: e.Handled = true; StepBy(-1); break;
                case Key.PageUp: e.Handled = true; StepBy(+10); break;
                case Key.PageDown: e.Handled = true; StepBy(-10); break;
                case Key.Escape: e.Handled = true; Box.Text = Format(_value); Box.CaretIndex = Box.Text.Length; ShowMessage(null, false); break;
            }
        }

        /// <summary>Focus left the box: a refused entry is put back to the last good value and the message stays to say why.</summary>
        private void Leave()
        {
            var shown = Box.Text;
            if (!TryParse(shown, out _, out _)) { Box.Text = Format(_value); return; }
            Commit();
        }

        /// <summary>Moves the value by <paramref name="times"/> steps from what is typed (or the last good value when the box holds something else), clamped to the range.</summary>
        public bool StepBy(int times)
        {
            double basis = TryParse(Box.Text, out var typed, out _) ? typed : _value;
            double next = Math.Round(basis + times * StepSize, _decimals + 3);
            next = Math.Max(Min, Math.Min(Max, next));
            if (_snap) next = Snap(next);
            return Apply(next, null);
        }

        /// <summary>Commits what is typed. Returns true when the value was accepted (including an unchanged one).</summary>
        public bool Commit()
        {
            if (!TryParse(Box.Text, out var v, out var problem))
            {
                ShowMessage(problem, true);
                return false;
            }
            string note = null;
            if (_snap)
            {
                var snapped = Snap(v);
                if (Math.Abs(snapped - v) > 1e-9) note = $"Rounded to {Format(snapped)}.";
                v = snapped;
            }
            return Apply(v, note);
        }

        /// <summary>Sets the shown value without raising the commit callback (for a control kept in step with this one).</summary>
        public void SetValue(double v)
        {
            v = Math.Max(Min, Math.Min(Max, v));
            _value = v; Box.Text = Format(v); ShowMessage(null, false); Paint();
        }

        private bool Apply(double v, string note)
        {
            v = Math.Round(v, _decimals);
            bool changed = Math.Abs(v - _value) > 1e-9;
            _value = v;
            Box.Text = Format(v);
            Box.CaretIndex = Box.Text.Length;
            ShowMessage(note, false);
            Paint();
            if (changed) _commit?.Invoke(v);
            return true;
        }

        private double Snap(double v) => Math.Max(Min, Math.Min(Max, Math.Round((v - Min) / StepSize) * StepSize + Min));

        private bool TryParse(string text, out double v, out string problem)
        {
            v = 0; problem = null;
            text = (text ?? "").Trim();
            if (text.Length == 0) { problem = "Not changed: enter a number."; return false; }
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out v) && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                || double.IsNaN(v) || double.IsInfinity(v))
            { problem = "Not changed: that is not a number."; return false; }
            if (_decimals == 0 && Math.Abs(v - Math.Round(v)) > 1e-9)
            { problem = $"Not changed: enter a whole number from {Format(Min)} to {Format(Max)}."; return false; }
            if (v < Min - 1e-9 || v > Max + 1e-9)
            { problem = _decimals == 0 ? $"Not changed: enter a whole number from {Format(Min)} to {Format(Max)}." : $"Not changed: enter a number from {Format(Min)} to {Format(Max)}."; return false; }
            return true;
        }

        private void ShowMessage(string text, bool error)
        {
            _isError = error && !string.IsNullOrEmpty(text);
            _message.Text = text ?? "";
            _message.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            _message.Foreground = Ui.Res(error ? "B.Danger" : "B.TextMute");
            if (!string.IsNullOrEmpty(text)) Core.Language.Refresh(_message);
        }

        private void Paint()
        {
            _minus.IsEnabled = _value > Min + 1e-9;
            _plus.IsEnabled = _value < Max - 1e-9;
        }
    }
}
