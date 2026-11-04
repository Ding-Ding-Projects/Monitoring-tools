using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A time picker: an hour and a minute, each a typed number field with steppers (hour 0 to 23, minute 0 to 59), read and written as the
    /// 24-hour text "HH:mm" that the schedule rules store. It cannot hold an unreadable time.
    /// </summary>
    public sealed class TimeField : StackPanel
    {
        private readonly NumberField _hour, _minute;
        private int _h, _m;

        public TimeField(string name, string value)
        {
            Orientation = Orientation.Horizontal;
            Margin = new Thickness(0, 0, 8, 8);
            Parse(value, out _h, out _m);
            _hour = new NumberField(name + " hour", null, _h, 0, 23, 1, v => _h = (int)v, "h", 0, false, true) { Margin = new Thickness(0, 0, 6, 0) };
            _minute = new NumberField(name + " minute", null, _m, 0, 59, 1, v => _m = (int)v, "min", 0, false, true);
            Children.Add(_hour); Children.Add(_minute);
            AutomationProperties.SetName(this, name);
        }

        public NumberField HourField => _hour;
        public NumberField MinuteField => _minute;

        public static bool Parse(string text, out int h, out int m)
        {
            h = 9; m = 0;
            if (!Modes.TryParseTime(text, out int total)) return false;
            h = total / 60; m = total % 60;
            return true;
        }

        /// <summary>The time as "HH:mm".</summary>
        public string Value
        {
            get => _h.ToString("00", CultureInfo.InvariantCulture) + ":" + _m.ToString("00", CultureInfo.InvariantCulture);
            set { if (Parse(value, out int h, out int m)) { _h = h; _m = m; _hour.SetValue(h); _minute.SetValue(m); } }
        }
    }

    /// <summary>
    /// A date picker: the system DatePicker with its calendar, read and written as the text "yyyy-MM-dd" that the schedule rules store, or the
    /// empty text when no date is chosen.
    /// </summary>
    public sealed class DateField : DatePicker
    {
        public DateField(string name, string value)
        {
            Margin = new Thickness(0, 0, 8, 8);
            MinWidth = 150;
            HorizontalAlignment = HorizontalAlignment.Left;
            Foreground = Ui.Res("B.Text");
            Background = Ui.Res("B.Sunken");
            BorderBrush = Ui.Res("B.LineStrong");
            FirstDayOfWeek = DayOfWeek.Monday;
            var cal = new Style(typeof(System.Windows.Controls.Calendar));
            cal.Setters.Add(new Setter(Control.BackgroundProperty, Ui.Res("B.Surface")));
            cal.Setters.Add(new Setter(Control.ForegroundProperty, Ui.Res("B.Text")));
            cal.Setters.Add(new Setter(Control.BorderBrushProperty, Ui.Res("B.LineStrong")));
            CalendarStyle = cal;
            AutomationProperties.SetName(this, name);
            Value = value;
        }

        /// <summary>The date as "yyyy-MM-dd", or the empty text when none is chosen.</summary>
        public string Value
        {
            get => SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
            set
            {
                if (string.IsNullOrWhiteSpace(value)) { SelectedDate = null; return; }
                if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) SelectedDate = d;
            }
        }
    }
}
