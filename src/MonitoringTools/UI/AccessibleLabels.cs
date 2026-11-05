using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

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
            if (b.ReadLocalValue(AutomationProperties.NameProperty) != DependencyProperty.UnsetValue) return;   // already named, or already bound
            if (b.Content is string) return;                                         // the peer reads a plain string by itself
            if (AutomationProperties.GetLabeledBy(b) != null) return;
            var binding = new Binding("Content") { Source = b, Mode = BindingMode.OneWay, Converter = Converter.Instance };
            b.SetBinding(AutomationProperties.NameProperty, binding);
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
