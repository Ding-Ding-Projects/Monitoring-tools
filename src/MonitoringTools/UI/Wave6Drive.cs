using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Helpers shared by the self-test and the capture run for driving real windows the way a person would:
    /// find a control by its visible label or accessible name, press it, type into it, open its element menu.
    /// Nothing here moves the mouse; every action goes through the control's own events.
    /// </summary>
    internal static class Drive
    {
        public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            if (root == null) yield break;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                yield return c;
                foreach (var d in Descendants(c)) yield return d;
            }
        }

        public static IEnumerable<DependencyObject> Ancestors(DependencyObject d)
        {
            for (d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); d != null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d)) yield return d;
        }

        public static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject => Descendants(root).OfType<T>();

        /// <summary>The text a button shows, whether its content is a string or a panel holding a text block.</summary>
        /// <summary>The English a translated label was made from, or null when the label was never translated.</summary>
        public static string OriginalLabelOf(ContentControl b)
        {
            var tb = b.Content as TextBlock ?? (b.Content is DependencyObject d ? Descendants(d).OfType<TextBlock>().FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Text)) : null);
            var run = tb?.Inlines.OfType<System.Windows.Documents.Run>().FirstOrDefault();
            return run == null ? null : Core.Language.OriginalText(run);
        }

        public static string LabelOf(ContentControl b)
        {
            if (b.Content is string s) return s;
            if (b.Content is TextBlock t) return t.Text;
            if (b.Content is DependencyObject d)
            {
                var tb = Descendants(d).OfType<TextBlock>().FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Text));
                if (tb != null) return tb.Text;
            }
            return null;
        }

        /// <summary>A visible control whose accessible name or visible label equals <paramref name="name"/>.</summary>
        public static T Find<T>(DependencyObject root, string name, bool visibleOnly = true) where T : FrameworkElement
        {
            foreach (var e in All<T>(root))
            {
                if (visibleOnly && !e.IsVisible) continue;
                if (AutomationProperties.GetName(e) == name || Core.Language.OriginalName(e) == name) return e;   // a translated name still answers to its English original
                if (e is ContentControl cc && (LabelOf(cc) == name || OriginalLabelOf(cc) == name)) return e;   // a translated label still answers to its English original
            }
            return null;
        }

        public static T Need<T>(DependencyObject root, string name) where T : FrameworkElement
            => Find<T>(root, name) ?? throw new InvalidOperationException($"no visible {typeof(T).Name} named \"{name}\"");

        /// <summary>Presses a button the way the user would. A switch or check box toggles; a plain toggle also raises Click.</summary>
        public static void Click(ButtonBase b)
        {
            if (!b.IsEnabled) throw new InvalidOperationException("the control is disabled: " + (AutomationProperties.GetName(b) ?? ""));
            if (b is CheckBox cb) { cb.IsChecked = cb.IsChecked != true; return; }
            if (b is ToggleButton tb && b is not RadioButton) { tb.IsChecked = tb.IsChecked != true; b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); return; }
            b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        public static void Press(DependencyObject root, string name) => Click(Need<ButtonBase>(root, name));

        /// <summary>Types into a text box and, when asked, commits with Enter.</summary>
        public static void Type(TextBox box, string text, bool enter = false)
        {
            box.Text = text;
            if (!enter) return;
            var src = PresentationSource.FromVisual(box);
            if (src == null) return;
            box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, src, Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        }

        /// <summary>
        /// Raises the right-click a person would make on <paramref name="target"/>. When no handler of the window sees that synthetic event
        /// (a hidden desktop has no real pointer), the same menu is opened through <see cref="ElementMenu.OpenFor"/>, which is the call the handler makes.
        /// </summary>
        public static void RightClick(FrameworkElement target)
        {
            int seen = ElementMenu.DiagSeen;
            RaiseRightClick(target);
            if (ElementMenu.DiagSeen != seen) return;
            var w = Window.GetWindow(target);
            if (w != null && ElementMenu.OpenFor(w, target)) UsedDirectOpen++;
        }

        public static int UsedDirectOpen;

        private static void RaiseRightClick(FrameworkElement target)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = UIElement.PreviewMouseRightButtonUpEvent,
                Source = target
            };
            target.RaiseEvent(args);
        }

        public static string TextOf(DependencyObject root) => string.Join(" ", All<TextBlock>(root).Where(t => t.IsVisible).Select(t => t.Text));
    }
}
