using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MonitoringTools.UI.Search
{
    /// <summary>
    /// Filters a page in place. Elements marked with <see cref="SetItem"/> are the things a search
    /// finds (a history row, a setting, a shortcut); top-level blocks with no marked item inside are
    /// searched as one unit, and a block whose items are all hidden is hidden too.
    /// </summary>
    public static class SearchScope
    {
        public static readonly DependencyProperty ItemProperty =
            DependencyProperty.RegisterAttached("Item", typeof(string), typeof(SearchScope), new PropertyMetadata(null));

        /// <summary>Marks an element as one search result. <paramref name="text"/> adds words that are not on screen, such as a kind or a full path.</summary>
        public static T SetItem<T>(T element, string text = "") where T : DependencyObject
        {
            element.SetValue(ItemProperty, text ?? "");
            return element;
        }

        public static string GetItem(DependencyObject d) => (string)d.GetValue(ItemProperty);

        /// <summary>Wires a search field to the blocks of a page that follow it.</summary>
        public static SearchField Attach(Panel page, SearchField field, string noun)
        {
            void Run()
            {
                var (shown, total) = Apply(page, field.State, field);
                field.SetCount(shown, total, noun);
            }
            field.Filter += Run;
            field.SampleSource = () => Units(page, field).Select(u => string.IsNullOrEmpty(GetItem(u)) ? Text(u) : GetItem(u));
            page.Loaded += (s, e) => Run();
            Run();
            return field;
        }

        private static IEnumerable<FrameworkElement> Blocks(Panel page, UIElement except)
        {
            bool seen = false;
            foreach (UIElement c in page.Children)
            {
                if (c == except) { seen = true; continue; }
                if (seen && c is FrameworkElement fe) yield return fe;
            }
        }

        private static IEnumerable<FrameworkElement> Units(Panel page, UIElement except)
        {
            foreach (var block in Blocks(page, except))
            {
                var items = Marked(block).ToList();
                if (items.Count == 0) yield return block;
                else foreach (var i in items) yield return i;
            }
        }

        public static (int Shown, int Total) Apply(Panel page, SearchState state, UIElement except)
        {
            state.TimedOut = false;
            int shown = 0, total = 0;
            foreach (var block in Blocks(page, except))
            {
                var items = Marked(block).ToList();
                if (items.Count == 0)
                {
                    total++;
                    bool hit = state.Matches(Text(block));
                    block.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
                    if (hit) shown++;
                    continue;
                }
                int visible = 0;
                foreach (var it in items)
                {
                    total++;
                    bool hit = state.Matches(Text(it));
                    it.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
                    if (hit) { visible++; shown++; }
                }
                block.Visibility = visible > 0 || !state.IsActive ? Visibility.Visible : Visibility.Collapsed;
            }
            return (shown, total);
        }

        private static IEnumerable<FrameworkElement> Marked(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                if (GetItem(child) != null && child is FrameworkElement fe) { yield return fe; continue; }
                foreach (var m in Marked(child)) yield return m;
            }
        }

        /// <summary>Everything a person could read on an element, plus the words its item mark adds.</summary>
        public static string Text(DependencyObject root)
        {
            var sb = new StringBuilder();
            var extra = GetItem(root);
            if (!string.IsNullOrEmpty(extra)) sb.Append(extra).Append(' ');
            Collect(root, sb, true);
            return sb.ToString();
        }

        // Buttons inside a result ("Copy again", "Actions") are skipped so every row does not match their labels;
        // a result that is itself a button is still read.
        private static void Collect(DependencyObject d, StringBuilder sb, bool root = false)
        {
            if (!root && d is System.Windows.Controls.Primitives.ButtonBase) return;
            switch (d)
            {
                case TextBlock tb: sb.Append(tb.Text).Append(' '); break;
                case TextBox tx: sb.Append(tx.Text).Append(' '); break;
                case ContentControl cc when cc.Content is string s: sb.Append(s).Append(' '); break;
            }
            if (d is FrameworkElement fe && fe.ToolTip is string tip) sb.Append(tip).Append(' ');
            foreach (var child in LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>()) Collect(child, sb);
        }
    }
}
