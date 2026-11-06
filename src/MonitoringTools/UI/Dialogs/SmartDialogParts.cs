using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// The one action bar every clipboard dialog uses, so actions are always in the same place: More on the left (a menu of
    /// everything else, the same list as right-click), a status line, at most two helpers, and the one main action on the
    /// right with its key shown inside it.
    /// </summary>
    internal static class SmartDialogParts
    {
        public static FrameworkElement ActionBar(IList<(string Label, string Key, Action Run)> more, string status, IEnumerable<Button> helpers, Button primary, string primaryKey, out TextBlock statusText)
        {
            var g = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (more != null && more.Count > 0)
            {
                var m = MoreButton(more);
                g.Children.Add(m);
            }
            statusText = new TextBlock { Text = status ?? "", FontSize = 12.5, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            AutomationProperties.SetLiveSetting(statusText, AutomationLiveSetting.Polite);
            Grid.SetColumn(statusText, 1); g.Children.Add(statusText);
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var h in helpers ?? Enumerable.Empty<Button>()) { h.Margin = new Thickness(0, 0, 8, 0); right.Children.Add(h); }
            if (primary != null)
            {
                if (!string.IsNullOrEmpty(primaryKey)) primary.ToolTip = (primary.ToolTip as string ?? "") + (primary.ToolTip == null ? "" : " ") + "(" + primaryKey + ")";
                AutomationProperties.SetAcceleratorKey(primary, primaryKey ?? "");
                right.Children.Add(primary);
            }
            Grid.SetColumn(right, 2); g.Children.Add(right);
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 14, 0, 0), Margin = new Thickness(0, 12, 0, 0), Child = g };
        }

        public static FrameworkElement ActionBar(IList<(string Label, string Key, Action Run)> more, string status, IEnumerable<Button> helpers, Button primary, string primaryKey) =>
            ActionBar(more, status, helpers, primary, primaryKey, out _);

        /// <summary>"More ▾": a menu of every other action with its key.</summary>
        public static Button MoreButton(IList<(string Label, string Key, Action Run)> items)
        {
            var b = Ui.Button("More ▾", null, "Btn.Ghost");
            AutomationProperties.SetName(b, "More actions");
            var menu = new ContextMenu();
            foreach (var it in items)
            {
                var run = it.Run;
                var mi = new MenuItem { Header = it.Label, InputGestureText = it.Key ?? "" };
                mi.Click += (s, e) => run();
                menu.Items.Add(mi);
            }
            b.ContextMenu = menu;
            b.Click += (s, e) => { menu.PlacementTarget = b; menu.Placement = PlacementMode.Top; menu.IsOpen = true; };
            return b;
        }

        /// <summary>A labelled box for settings that change what the dialog does.</summary>
        public static Border Options(params UIElement[] children)
        {
            var sp = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = "OPTIONS", FontFamily = Ui.Font("F.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) });
            foreach (var c in children) sp.Children.Add(c);
            return new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 0, 12), Child = sp };
        }
    }
}
