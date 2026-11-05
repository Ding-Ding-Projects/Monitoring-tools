using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Two window-wide defaults that keep every surface consistent without touching each control:
    /// a button whose content is a panel (an icon beside a text block) gets its accessible name from the text it shows, kept in step with
    /// the content and the language, and a window with no typeface of its own inherits the body typeface (Roboto with its Cantonese fallback).
    /// </summary>
    public static class AccessibleLabels
    {
        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            EventManager.RegisterClassHandler(typeof(ButtonBase), FrameworkElement.LoadedEvent, new RoutedEventHandler((o, a) => NameFromContent((ButtonBase)o)));
            EventManager.RegisterClassHandler(typeof(TextBoxBase), FrameworkElement.LoadedEvent, new RoutedEventHandler((o, a) => NameFromLabel((FrameworkElement)o)));
            EventManager.RegisterClassHandler(typeof(PasswordBox), FrameworkElement.LoadedEvent, new RoutedEventHandler((o, a) => NameFromLabel((FrameworkElement)o)));
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((o, a) =>
            {
                var w = (Window)o;
                if (w.ReadLocalValue(Control.FontFamilyProperty) == DependencyProperty.UnsetValue && Application.Current?.TryFindResource("F.Body") != null)
                    w.SetResourceReference(Control.FontFamilyProperty, "F.Body");
            }));
        }

        private static void NameFromContent(ButtonBase b)
        {
            if (b.TemplatedParent != null) return;                                   // a part inside another control's template
            var local = b.ReadLocalValue(AutomationProperties.NameProperty);
            if (local != DependencyProperty.UnsetValue && !(local is string blank && blank.Length == 0)) return;   // already named, or already bound
            if (AutomationProperties.GetLabeledBy(b) != null) return;
            var binding = new Binding("Content") { Source = b, Mode = BindingMode.OneWay, Converter = Converter.Instance };
            b.SetBinding(AutomationProperties.NameProperty, binding);
        }

        /// <summary>
        /// An input with no accessible name takes the text block that labels it (the nearest one before it in its panel, or the one that starts the
        /// panel above), or else its tooltip. The name follows that text block, so a language change reaches it.
        /// </summary>
        private static void NameFromLabel(FrameworkElement e)
        {
            if (e.TemplatedParent != null) return;
            var local = e.ReadLocalValue(AutomationProperties.NameProperty);
            if (local != DependencyProperty.UnsetValue && !(local is string blank && blank.Length == 0)) return;
            if (AutomationProperties.GetLabeledBy(e) != null) return;
            TextBlock label = null;
            DependencyObject child = e;
            for (int hop = 0; hop < 4 && label == null; hop++)
            {
                var parent = VisualTreeHelper.GetParent(child);
                if (parent == null) break;
                if (parent is Panel panel)
                {
                    int at = panel.Children.IndexOf(child as UIElement);
                    for (int i = at - 1; i >= 0 && label == null; i--)
                    {
                        if (panel.Children[i] is TextBlock t && !string.IsNullOrWhiteSpace(t.Text)) label = t;
                        else if (panel.Children[i] is Panel inner) label = inner.Children.OfType<TextBlock>().FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Text));
                    }
                }
                child = parent;
            }
            if (label != null) e.SetBinding(AutomationProperties.NameProperty, new Binding("Text") { Source = label, Mode = BindingMode.OneWay });
            else if (e.ToolTip is string tip && !string.IsNullOrWhiteSpace(tip)) AutomationProperties.SetName(e, tip);
        }

        private sealed class Converter : IValueConverter
        {
            public static readonly Converter Instance = new Converter();
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is string s) return s;
                if (value is TextBlock t) return t.Text;
                if (value is DependencyObject d)
                {
                    var tb = Drive.Descendants(d).OfType<TextBlock>().FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Text));
                    if (tb != null) return tb.Text;
                }
                return "";
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
        }
    }
}
