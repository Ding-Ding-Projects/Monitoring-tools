using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Gives every rounded corner the shape of the current look. Corners are written once, for Graphite, in code and in XAML;
    /// when a border is first laid out its corner is remembered and replaced with the look's version (square for Blueprint,
    /// molded for Instrument, round for Pop). Changing the look reshapes every open window from the remembered corners, and
    /// a corner that code changes later (a search bar opening its builder) becomes the new remembered corner.
    /// Circles stay circles, and a corner the person set themselves in the appearance editor is left alone.
    /// </summary>
    internal static class LookShape
    {
        private static readonly DependencyProperty OriginalProperty =
            DependencyProperty.RegisterAttached("OriginalCorner", typeof(CornerRadius?), typeof(LookShape), new PropertyMetadata(null));
        private static readonly DependencyProperty ShapedProperty =
            DependencyProperty.RegisterAttached("ShapedCorner", typeof(CornerRadius?), typeof(LookShape), new PropertyMetadata(null));
        private static readonly DependencyProperty KeepProperty =
            DependencyProperty.RegisterAttached("KeepCorner", typeof(bool), typeof(LookShape), new PropertyMetadata(false));

        /// <summary>Keeps this border's corner as drawn whatever the look (the previews of the other looks in the picker).</summary>
        public static T Keep<T>(T b) where T : Border { b.SetValue(KeepProperty, true); return b; }

        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            // Loaded is only delivered to elements that have their own Loaded handler, so most borders never see a class
            // handler for it. Every border is laid out, and its first layout changes its size, so that is the hook.
            EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler((o, e) => Shape((Border)o)), true);
        }

        /// <summary>
        /// Sets a corner written for Graphite on a border that is already on screen, shaped for the look in use. Code that
        /// changes a corner after the box first appeared (a search bar opening its builder) uses this, because the box's
        /// size, and so the layout hook, may not change with it.
        /// </summary>
        public static void Set(Border b, CornerRadius design)
        {
            if ((bool)b.GetValue(KeepProperty) || ElementStyles.HasCustomRadius(b)) { b.CornerRadius = design; return; }
            b.SetValue(OriginalProperty, design);
            var shaped = Looks.Corner(design, b.ActualWidth, b.ActualHeight);
            b.SetValue(ShapedProperty, shaped);
            b.CornerRadius = shaped;
        }

        /// <summary>The corner a border was drawn with before any look reshaped it.</summary>
        public static CornerRadius OriginalOf(Border b) => b.GetValue(OriginalProperty) is CornerRadius c ? c : b.CornerRadius;

        private static void Shape(Border b)
        {
            if ((bool)b.GetValue(KeepProperty) || ElementStyles.HasCustomRadius(b)) return;
            var now = b.CornerRadius;
            var original = b.GetValue(OriginalProperty) as CornerRadius?;
            // first sight, or something changed the corner since it was last shaped: what is there now is the corner to keep
            if (original == null || !(b.GetValue(ShapedProperty) is CornerRadius last) || last != now)
            {
                if (now.TopLeft == 0 && now.TopRight == 0 && now.BottomRight == 0 && now.BottomLeft == 0)
                {
                    if (original != null) { b.ClearValue(OriginalProperty); b.ClearValue(ShapedProperty); }
                    return;
                }
                original = now;
                b.SetValue(OriginalProperty, original);
            }
            var shaped = Looks.Corner(original.Value, b.ActualWidth, b.ActualHeight);
            b.SetValue(ShapedProperty, shaped);
            if (shaped != now) b.CornerRadius = shaped;
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
