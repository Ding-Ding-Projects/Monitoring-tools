using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Helpers that make code-built colours follow the theme. A brush taken once with <see cref="Ui.Res"/> keeps its old colour when
    /// the theme changes, because the theme replaces the brush objects. A resource reference is looked up again on every change.
    /// </summary>
    public static class ThemeLive
    {
        private static readonly DependencyProperty[] BrushProperties =
        {
            TextElement.ForegroundProperty, Control.BackgroundProperty, Control.BorderBrushProperty,
            Border.BackgroundProperty, Border.BorderBrushProperty, Panel.BackgroundProperty,
            TextBlock.BackgroundProperty, Shape.FillProperty, Shape.StrokeProperty,
            // control templates read an accent brush from Tag
            FrameworkElement.TagProperty
        };

        /// <summary>Sets a brush property to follow a theme brush by key, so a theme change repaints it.</summary>
        public static T Set<T>(T element, DependencyProperty property, string key) where T : FrameworkElement
        {
            element.SetResourceReference(property, key);
            return element;
        }

        /// <summary>
        /// Walks a tree and turns every locally set brush that is one of the theme's own brushes into a resource reference to that
        /// same key. Brushes that are not theme brushes (custom colours) are left alone.
        /// </summary>
        public static void Bind(DependencyObject root)
        {
            if (root == null || Application.Current == null) return;
            var keys = new Dictionary<Brush, string>(ReferenceComparer.Instance);
            Collect(Application.Current.Resources, keys, new HashSet<ResourceDictionary>());
            if (keys.Count == 0) return;
            Walk(root, keys, new HashSet<DependencyObject>());
        }

        private static void Collect(ResourceDictionary d, Dictionary<Brush, string> keys, HashSet<ResourceDictionary> seen)
        {
            if (d == null || !seen.Add(d)) return;
            foreach (var k in d.Keys)
                if (k is string s && s.StartsWith("B.", System.StringComparison.Ordinal) && d[k] is Brush b && !keys.ContainsKey(b)) keys[b] = s;
            foreach (var m in d.MergedDictionaries) Collect(m, keys, seen);
        }

        private static void Walk(DependencyObject d, Dictionary<Brush, string> keys, HashSet<DependencyObject> seen)
        {
            if (d == null || !seen.Add(d)) return;
            foreach (var dp in BrushProperties)
            {
                if (d.ReadLocalValue(dp) is Brush b && keys.TryGetValue(b, out var key))
                {
                    if (d is FrameworkElement fe) fe.SetResourceReference(dp, key);
                    else if (d is FrameworkContentElement fce) fce.SetResourceReference(dp, key);
                }
            }
            foreach (var c in LogicalTreeHelper.GetChildren(d))
                if (c is DependencyObject co) Walk(co, keys, seen);
            if (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
            {
                int n = VisualTreeHelper.GetChildrenCount(d);
                for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(d, i), keys, seen);
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<Brush>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(Brush x, Brush y) => ReferenceEquals(x, y);
            public int GetHashCode(Brush obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
