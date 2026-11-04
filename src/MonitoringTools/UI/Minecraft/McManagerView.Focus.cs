using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MonitoringTools.UI.Minecraft
{
    // Keeping keyboard focus and the scroll position while the 4-second poll redraws part of a section.
    internal sealed partial class McManagerView
    {
        /// <summary>
        /// Runs <paramref name="rebuild"/>, which replaces the children of <paramref name="region"/>, and then puts keyboard
        /// focus back on the control with the same identity (its type, accessible name and text) and the page back at the
        /// same scroll position. A control that is no longer there leaves focus where WPF puts it.
        /// </summary>
        internal void KeepFocus(DependencyObject region, Action rebuild)
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            var win = Window.GetWindow(this);
            if (focused == null && win != null) focused = FocusManager.GetFocusedElement(win) as DependencyObject;
            string key = null; int nth = 0;
            if (focused is FrameworkElement fe && IsInside(fe, region))
            {
                key = Identity(fe);
                nth = Focusables(region).Where(x => Identity(x) == key).TakeWhile(x => !ReferenceEquals(x, fe)).Count();
            }
            var scroller = _owner?.Scroller;
            double offset = scroller?.VerticalOffset ?? 0;
            rebuild();
            if (key == null) return;
            (region as UIElement)?.UpdateLayout();
            var same = Focusables(region).Where(x => Identity(x) == key).ToList();
            var target = same.ElementAtOrDefault(nth) ?? same.FirstOrDefault();
            if (target != null) target.Focus();
            if (scroller != null)
            {
                scroller.ScrollToVerticalOffset(offset);
                Dispatcher.BeginInvoke(new Action(() => scroller.ScrollToVerticalOffset(offset)), DispatcherPriority.Loaded);
            }
        }

        internal static string Identity(FrameworkElement e)
        {
            var name = AutomationProperties.GetName(e);
            var text = e is ContentControl cc && cc.Content is string str ? str : Descend<TextBlock>(e).FirstOrDefault()?.Text;
            return e.GetType().Name + "|" + name + "|" + text;
        }

        private static bool IsInside(DependencyObject e, DependencyObject region)
        {
            for (var p = e; p != null; p = VisualTreeHelper.GetParent(p) ?? LogicalTreeHelper.GetParent(p))
                if (ReferenceEquals(p, region)) return true;
            return false;
        }

        private static IEnumerable<FrameworkElement> Focusables(DependencyObject root) =>
            Descend<FrameworkElement>(root).Where(x => x.Focusable && x.IsVisible && x.IsEnabled && (x is ButtonBase || x is TextBox || x is Selector));

        private static IEnumerable<T> Descend<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T t) yield return t;
                foreach (var d in Descend<T>(c)) yield return d;
            }
        }
    }
}
