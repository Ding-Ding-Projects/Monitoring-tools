using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Gives every rounded corner the shape of the current look. Corners are written once, for Graphite, in code and in XAML;
    /// when a border first loads its corner is remembered and replaced with the look's version (square for Blueprint,
    /// molded for Instrument, round for Pop). Changing the look reshapes every open window from the remembered corners.
    /// Circles stay circles, and a corner the person set themselves in the appearance editor is left alone.
    /// </summary>
    internal static class LookShape
    {
        private static readonly DependencyProperty OriginalProperty =
            DependencyProperty.RegisterAttached("OriginalCorner", typeof(CornerRadius?), typeof(LookShape), new PropertyMetadata(null));
        private static readonly DependencyProperty KeepProperty =
            DependencyProperty.RegisterAttached("KeepCorner", typeof(bool), typeof(LookShape), new PropertyMetadata(false));

        /// <summary>Keeps this border's corner as drawn whatever the look (the previews of the other looks in the picker).</summary>
        public static T Keep<T>(T b) where T : Border { b.SetValue(KeepProperty, true); return b; }

        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler((o, e) => Shape((Border)o)), true);
        }

        /// <summary>The corner a border was drawn with before any look reshaped it.</summary>
        public static CornerRadius OriginalOf(Border b) => b.GetValue(OriginalProperty) is CornerRadius c ? c : b.CornerRadius;

        private static void Shape(Border b)
        {
            if ((bool)b.GetValue(KeepProperty) || ElementStyles.HasCustomRadius(b)) return;
            if (!(b.GetValue(OriginalProperty) is CornerRadius original))
            {
                original = b.CornerRadius;
                if (original.TopLeft == 0 && original.TopRight == 0 && original.BottomRight == 0 && original.BottomLeft == 0) return;
                b.SetValue(OriginalProperty, original);
            }
            var shaped = Looks.Corner(original, b.ActualWidth, b.ActualHeight);
            if (shaped != b.CornerRadius) b.CornerRadius = shaped;
        }

        /// <summary>Reshapes every border in every open window for the look now in use.</summary>
        public static void RemapAll()
        {
            var app = Application.Current;
            if (app == null) return;
            foreach (Window w in app.Windows) Walk(w);
        }

        private static void Walk(DependencyObject d)
        {
            if (d is Border b && b.GetValue(OriginalProperty) is CornerRadius) Shape(b);
            int n = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(d, i));
        }
    }
}
