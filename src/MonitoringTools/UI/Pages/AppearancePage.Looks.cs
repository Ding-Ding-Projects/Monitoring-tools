using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        /// <summary>The look picker at the top of the Appearance page: four looks, each drawn in its own style.</summary>
        private FrameworkElement LookPicker()
        {
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Look", "Changes type, shapes, colour and depth everywhere at once. Your text size, motion and language choices stay as they are."));
            var grid = LookGrid();
            foreach (var l in Looks.All) grid.Children.Add(LookChoice(l, Looks.Current == l, ChooseLook, double.NaN));
            sp.Children.Add(grid);
            sp.Children.Add(new TextBlock
            {
                Text = "Picking a look also picks the colour it was drawn with. Change the colour under Theme and colour whenever you like. Default: Graphite.",
                Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, MaxWidth = 640,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 0)
            });
            return SearchScope.SetItem(Card(sp), "look style design graphite instrument blueprint pop type font shapes corners outlines shadows");
        }

        /// <summary>Switches to a look and its colour, then everything redraws in it.</summary>
        internal static void ChooseLook(LookSpec l)
        {
            var s = Settings.Current;
            if (s.Look == l.Id) return;
            s.Look = l.Id;
            s.SeedColour = l.DefaultSeed;
            Settings.Save();
            Appearance.Apply();
        }

        /// <summary>The four looks two by two, so a row never ends with one look on its own.</summary>
        internal static CardGrid LookGrid()
        {
            var grid = new CardGrid { Columns = 2, Margin = new Thickness(0, 4, -12, 0) };
            AutomationProperties.SetName(grid, "Look");
            return grid;
        }

        /// <summary>One choice in a look picker: the look's preview, its name and what it is like. Used here and in setup.</summary>
        internal static FrameworkElement LookChoice(LookSpec l, bool chosen, Action<LookSpec> pick, double width = 214)
        {
            var body = new StackPanel();
            body.Children.Add(LookParts.LookPreview(l));
            var nameRow = new DockPanel { Margin = new Thickness(4, 10, 4, 2), LastChildFill = true };
            if (chosen)
            {
                var tick = Ui.IconChip(Icons.Check, Ui.Res("B.MintInk"), Ui.Res("B.Mint"), 22, 13);
                DockPanel.SetDock(tick, Dock.Right);
                nameRow.Children.Add(tick);
            }
            nameRow.Children.Add(new TextBlock { Text = l.Name, Style = Ui.Style("T.Title"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
            body.Children.Add(nameRow);
            body.Children.Add(new TextBlock { Text = l.Description, Style = Ui.Style("T.Body"), FontSize = 12.5, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 2) });
            var b = new Button { Content = body, Style = Ui.Style("Btn.Row"), Height = double.NaN, Width = width, Padding = new Thickness(8, 8, 8, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
            var frame = new Border { BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(22), Margin = new Thickness(0, 0, 12, 12), Child = b };
            frame.SetResourceReference(Border.BorderBrushProperty, chosen ? "B.Mint" : "B.Line");
            frame.SetResourceReference(Border.BackgroundProperty, chosen ? "B.Raised" : "B.Surface");
            AutomationProperties.SetName(b, l.Name + ". " + l.Description);
            AutomationProperties.SetItemStatus(b, chosen ? "selected" : "not selected");
            b.Click += (s, e) => pick(l);
            return frame;
        }
    }
}
