using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The few parts that are drawn differently in each look, beyond colours, type and corners: the label above a page
    /// title, card outlines and shadows, the readout window for big figures, the grid behind Blueprint pages and the small
    /// preview of each look in the picker.
    /// </summary>
    internal static class LookParts
    {
        /// <summary>The small label above a page title: a sentence-case label (Graphite), a lit plate (Instrument), a drawing
        /// note with a rule (Blueprint) or label tape (Pop).</summary>
        public static FrameworkElement PageEyebrow(string text)
        {
            var look = Looks.Current;
            switch (look.Header)
            {
                case "plate":
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(Led(true));
                    var t = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                    row.Children.Add(t);
                    var plate = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 3, 10, 4), HorizontalAlignment = HorizontalAlignment.Left, Child = row };
                    plate.SetResourceReference(Border.BorderBrushProperty, "B.LineStrong");
                    return plate;
                }
                case "rule":
                {
                    var g = new Grid();
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    var t = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Mono"), Foreground = Ui.Res("B.Mint"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                    var rule = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Center };
                    rule.SetResourceReference(Border.BackgroundProperty, "B.Line");
                    Grid.SetColumn(rule, 1);
                    g.Children.Add(t); g.Children.Add(rule);
                    return g;
                }
                case "tape":
                {
                    var t = new TextBlock { Text = text, FontSize = 13.5, FontWeight = FontWeights.Bold, FontFamily = Ui.Font("F.Body") };
                    t.SetResourceReference(TextBlock.ForegroundProperty, "B.Window");
                    var tape = new Border { CornerRadius = new CornerRadius(3), Padding = new Thickness(12, 3, 12, 5), HorizontalAlignment = HorizontalAlignment.Left, Child = t, RenderTransform = new RotateTransform(-2), RenderTransformOrigin = new Point(0.5, 0.5), Margin = new Thickness(2, 2, 0, 2) };
                    tape.SetResourceReference(Border.BackgroundProperty, "B.Text");
                    return tape;
                }
                default:
                {
                    var t = new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Medium, FontFamily = Ui.Font("F.Body") };
                    t.SetResourceReference(TextBlock.ForegroundProperty, "B.Mint");
                    return t;
                }
            }
        }

        /// <summary>An indicator light: lit in the seed's bright tone, or dark.</summary>
        public static FrameworkElement Led(bool lit)
        {
            var dot = new Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
            if (lit)
            {
                dot.SetResourceReference(Shape.FillProperty, "B.Readout");
                dot.Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Opacity = 0.8, Color = ((SolidColorBrush)Ui.Res("B.Readout")).Color };
            }
            else dot.SetResourceReference(Shape.FillProperty, "B.Line");
            return dot;
        }

        /// <summary>Gives a card the look's outline and, in Pop, its hard shadow. Graphite cards stay tonal with no outline.</summary>
        public static void DecorateCard(Border card, bool keepOwnEdge = false)
        {
            var look = Looks.Current;
            if (look.CardEdge > 0 && !keepOwnEdge)
            {
                card.BorderThickness = new Thickness(look.CardEdge);
                card.SetResourceReference(Border.BorderBrushProperty, "B.CardEdge");
            }
            if (look.HardShadow > 0) card.SetResourceReference(UIElement.EffectProperty, "Fx.Card");
        }

        /// <summary>Instrument shows a big figure in a dark readout window; the other looks show it as it is.</summary>
        public static FrameworkElement Readout(TextBlock value)
        {
            if (!Looks.Current.Readouts) return value;
            value.SetResourceReference(TextBlock.ForegroundProperty, "B.Readout");
            value.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 0, Opacity = 0.35, Color = ((SolidColorBrush)Ui.Res("B.Readout")).Color };
            var well = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 6, 12, 7), Child = value, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = value.Margin };
            value.Margin = new Thickness(0);
            well.SetResourceReference(Border.BackgroundProperty, "B.ReadoutWell");
            return well;
        }

        /// <summary>
        /// A small drawing of a look for the picker: the side rail with its open page, a heading, a card with a figure and a
        /// button, always in that look's own dark colours, type and corners, whichever look is in use.
        /// </summary>
        public static FrameworkElement LookPreview(LookSpec l)
        {
            Brush B(string hex) { Appearance.TryParseHex(hex, out var c); var b = new SolidColorBrush(c); b.Freeze(); return b; }
            string bg, bar, sel, selEdge, head, card, cardEdge, num, numBg, btn, btnEdge;
            double rSel, rHead, rCard, rBtn, edge = 0, shadow = 0; bool grid = false;
            switch (l.Id)
            {
                case "instrument": bg = "#181B19"; bar = "#858B84"; sel = "#292D2A"; selEdge = "#FFB95F"; head = "#EAECE6"; card = "#202421"; cardEdge = "#3B403C"; num = "#FFB95F"; numBg = "#0B0D0C"; btn = "#FFB95F"; btnEdge = null; rSel = 3; rHead = 2; rCard = 6; rBtn = 4; edge = 1; break;
                case "blueprint": bg = "#0A1D3A"; bar = "#7EA6D8"; sel = "#9ECAFF"; selEdge = null; head = "#E8F1FF"; card = "#0E2546"; cardEdge = "#7EA6D8"; num = "#9ECAFF"; numBg = null; btn = "#9ECAFF"; btnEdge = null; rSel = 1; rHead = 0; rCard = 2; rBtn = 2; edge = 1; grid = true; break;
                case "pop": bg = "#F4F5F7"; bar = "#16171B"; sel = "#FFB2BC"; selEdge = "#16171B"; head = "#16171B"; card = "#FFFFFF"; cardEdge = "#16171B"; num = "#16171B"; numBg = null; btn = "#B32148"; btnEdge = "#16171B"; rSel = 7; rHead = 4; rCard = 12; rBtn = 7; edge = 2; shadow = 3; break;
                default: bg = "#0F1512"; bar = "#BFC9C2"; sel = "#00513A"; selEdge = null; head = "#DEE4DF"; card = "#1B211E"; cardEdge = null; num = "#6FDBB4"; numBg = null; btn = "#6FDBB4"; btnEdge = null; rSel = 7; rHead = 4; rCard = 12; rBtn = 7; break;
            }
            var root = LookShape.Keep(new Border { Height = 104, CornerRadius = new CornerRadius(l.Id == "blueprint" ? 2 : 12), Background = B(bg), ClipToBounds = true });
            var g = new Grid();
            if (grid)
            {
                var pen = new Pen(B("#1F7EA6D8"), 1); pen.Freeze();
                var tile = new DrawingBrush(new GeometryDrawing { Pen = pen, Geometry = Geometry.Parse("M0,0 L12,0 M0,0 L0,12") }) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 12, 12), ViewportUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 12, 12), ViewboxUnits = BrushMappingMode.Absolute };
                g.Background = tile;
            }
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var rail = new StackPanel { Margin = new Thickness(8, 10, 6, 0) };
            rail.Children.Add(LookShape.Keep(new Border { Height = 12, CornerRadius = new CornerRadius(rSel), Background = B(sel), BorderBrush = selEdge == null ? null : B(selEdge), BorderThickness = new Thickness(selEdge == null ? 0 : 1.2), Margin = new Thickness(0, 0, 0, 6) }));
            for (int i = 0; i < 4; i++) rail.Children.Add(LookShape.Keep(new Border { Height = 6, CornerRadius = new CornerRadius(Math.Min(rSel, 3)), Background = B(bar), Opacity = l.Id == "pop" ? 0.35 : 0.55, Margin = new Thickness(0, 0, 4, 6) }));
            g.Children.Add(rail);
            var main = new StackPanel { Margin = new Thickness(4, 12, 12, 0) };
            Grid.SetColumn(main, 1);
            main.Children.Add(LookShape.Keep(new Border { Height = 10, Width = 70, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(rHead), Background = B(head), Margin = new Thickness(0, 0, 0, 8) }));
            var c = LookShape.Keep(new Border { Height = 58, CornerRadius = new CornerRadius(rCard), Background = B(card), BorderBrush = cardEdge == null ? null : B(cardEdge), BorderThickness = new Thickness(edge), Padding = new Thickness(8) });
            if (shadow > 0) c.Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = shadow, Direction = 315, Opacity = 1, Color = Colors.Black };
            var cg = new Grid();
            var figure = new TextBlock { Text = "16 ms", FontSize = 14, Foreground = B(num), FontFamily = (FontFamily)Application.Current.Resources[l.DisplayFont], FontWeight = l.TitleWeight, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
            cg.Children.Add(numBg == null ? (UIElement)figure : LookShape.Keep(new Border { Background = B(numBg), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = figure }));
            cg.Children.Add(LookShape.Keep(new Border { Width = 36, Height = 13, CornerRadius = new CornerRadius(rBtn), Background = B(btn), BorderBrush = btnEdge == null ? null : B(btnEdge), BorderThickness = new Thickness(btnEdge == null ? 0 : 1.2), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom }));
            c.Child = cg;
            main.Children.Add(c);
            g.Children.Add(main);
            root.Child = g;
            System.Windows.Automation.AutomationProperties.SetName(root, l.Name + " preview");
            return root;
        }

        /// <summary>The faint drawing grid behind Blueprint pages, or nothing.</summary>
        public static Brush PageBackground()
        {
            if (!Looks.Current.Grid) return Brushes.Transparent;
            var line = ((SolidColorBrush)Ui.Res("B.LineStrong")).Color;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(0x1C, line.R, line.G, line.B)), 1);
            var drawing = new GeometryDrawing { Pen = pen, Geometry = Geometry.Parse("M0,0 L24,0 M0,0 L0,24") };
            var brush = new DrawingBrush(drawing) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 24, 24), ViewportUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 24, 24), ViewboxUnits = BrushMappingMode.Absolute };
            brush.Freeze();
            return brush;
        }
    }
}
