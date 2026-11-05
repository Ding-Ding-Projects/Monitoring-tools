using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        // Every interactive style in the five states a person can see, in both themes. A capture cannot hold a pointer over a control,
        // press it or give it the keyboard, so the read-only state properties the templates react to (IsMouseOver, IsPressed,
        // IsKeyboardFocused, IsKeyboardFocusWithin) are set through their property keys. The templates are the real ones; only the
        // state is forced. Disabled is simply IsEnabled = false.
        private static DependencyPropertyKey StateKey(Type owner, string name) =>
            owner.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as DependencyPropertyKey;

        private static void ForceState(DependencyObject d, string state)
        {
            void Set(Type t, string key) { var k = StateKey(t, key); if (k != null) d.SetValue(k, true); }
            switch (state)
            {
                case "hover": Set(typeof(UIElement), "IsMouseOverPropertyKey"); break;
                case "pressed": Set(typeof(UIElement), "IsMouseOverPropertyKey"); Set(typeof(ButtonBase), "IsPressedPropertyKey"); break;
                case "focused": Set(typeof(UIElement), "IsKeyboardFocusedPropertyKey"); Set(typeof(UIElement), "IsKeyboardFocusWithinPropertyKey"); break;
            }
        }

        private static async Task StateScenesAsync()
        {
            var savedMode = Settings.Current.ThemeMode;
            try
            {
                foreach (var theme in new[] { "dark", "light" })
                {
                    Settings.Current.ThemeMode = theme; Appearance.Apply();
                    var win = new Window { Width = 1180, Height = 700, Title = "Interactive states", ResizeMode = ResizeMode.NoResize };
                    win.SetResourceReference(Window.BackgroundProperty, "B.Window");
                    var grid = new Grid { Margin = new Thickness(24) };
                    var heads = new[] { "State", "Switch off", "Switch on", "Tonal", "Filled", "Chip", "Row", "Text field", "Date field" };
                    foreach (var h in heads) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = h == "State" ? new GridLength(90) : GridLength.Auto });
                    var states = new[] { "resting", "hover", "pressed", "focused", "disabled" };
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    for (int c = 0; c < heads.Length; c++)
                    {
                        var t = new TextBlock { Text = heads[c], FontSize = 12, Margin = new Thickness(0, 0, 18, 12) };
                        t.SetResourceReference(TextBlock.ForegroundProperty, "B.TextSoft"); Grid.SetColumn(t, c); grid.Children.Add(t);
                    }
                    var forced = new System.Collections.Generic.List<(DependencyObject D, string State)>();
                    for (int r = 0; r < states.Length; r++)
                    {
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        var st = states[r];
                        void Put(int col, FrameworkElement e, bool target = true)
                        {
                            e.Margin = new Thickness(0, 0, 18, 18); e.VerticalAlignment = VerticalAlignment.Center; e.HorizontalAlignment = HorizontalAlignment.Left;
                            if (st == "disabled") e.IsEnabled = false;
                            Grid.SetRow(e, r + 1); Grid.SetColumn(e, col); grid.Children.Add(e);
                            if (target && st != "resting" && st != "disabled") forced.Add((e, st));
                        }
                        var label = new TextBlock { Text = st, FontSize = 14, Margin = new Thickness(0, 0, 18, 18), VerticalAlignment = VerticalAlignment.Center };
                        label.SetResourceReference(TextBlock.ForegroundProperty, "B.Text"); Grid.SetRow(label, r + 1); grid.Children.Add(label);
                        Put(1, new CheckBox { Style = Ui.Style("Switch"), Content = "", Width = 70, IsChecked = false });
                        Put(2, new CheckBox { Style = Ui.Style("Switch"), Content = "", Width = 70, IsChecked = true });
                        Put(3, Ui.Button("Tonal", null, "Btn.Ghost"));
                        Put(4, Ui.Button("Filled", null, "Btn.Accent"));
                        Put(5, Ui.Button("Chip", null, "Btn.Chip"));
                        var row = Ui.Button("Row", null, "Btn.Row"); row.Width = 90; Put(6, row);
                        Put(7, new TextBox { Style = Ui.Style("Input"), Text = "text", Width = 110 });
                        Put(8, new DateField("Date " + st, "2026-12-24"));
                    }
                    win.Content = grid;
                    win.Show();
                    await Task.Delay(900);
                    win.UpdateLayout();
                    foreach (var (d, state) in forced) ForceState(d, state);
                    await Shot(win, "states-" + theme, "Every interactive style at rest, hovered, pressed, focused and disabled in the " + theme + " theme (hover, pressed and focus are forced on the real templates)", 900);
                    win.Close();
                    // the calendar a date field drops down, drawn on its own because a popup is not part of the window's picture
                    var cw = new Window { Width = 360, Height = 400, Title = "Calendar", ResizeMode = ResizeMode.NoResize };
                    cw.SetResourceReference(Window.BackgroundProperty, "B.Window");
                    cw.Content = new System.Windows.Controls.Calendar { Style = DateField.ThemedCalendarStyle(), DisplayDate = new DateTime(2026, 12, 1), SelectedDate = new DateTime(2026, 12, 24), Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                    cw.Show(); await Task.Delay(700);
                    await Shot(cw, "calendar-" + theme, "The calendar of a date field in the " + theme + " theme", 600);
                    cw.Close();
                }
            }
            finally { Settings.Current.ThemeMode = savedMode; Appearance.Apply(); }
        }
    }
}
