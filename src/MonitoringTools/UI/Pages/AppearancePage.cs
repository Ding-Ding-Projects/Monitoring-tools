using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The appearance page. Its content is built in this file only.
        private FrameworkElement AppearancePage()
        {
            var s = Settings.Current;
            var root = PageRoot("Settings", "Appearance", "Theme, colour, text size, density and motion.");

            // Wraps a control with a line saying what it does and what it starts as, as one searchable item.
            FrameworkElement Described(FrameworkElement control, string what, string def, string words)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
                sp.Children.Add(control);
                sp.Children.Add(new TextBlock
                {
                    Text = what + " Default: " + def + ".",
                    Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"),
                    Margin = new Thickness(0, -4, 0, 6), TextWrapping = TextWrapping.Wrap, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left
                });
                return SearchScope.SetItem(sp, words);
            }

            // ---- Live preview, drawn with dynamic references so it follows each change as it happens
            var preview = new StackPanel();
            preview.Children.Add(CardTitle("Preview", "Drawn with the colours in use right now."));
            preview.Children.Add(PreviewSample());
            root.Children.Add(Card(SearchScope.SetItem(preview, "preview sample buttons chips switch")));

            root.Children.Add(AppearanceLookCard());

            // ---- Theme and colour
            var look = new StackPanel();
            look.Children.Add(CardTitle("Theme and colour", "Light, dark or whatever Windows is using, plus the colour the app is built around."));
            look.Children.Add(Described(
                Choice("Theme", new[] { ("Light", "light"), ("Dark", "dark"), ("System", "system") }, Appearance.ThemeMode,
                    v => { s.ThemeMode = v; Appearance.Apply(); }),
                "Light or dark colours. System follows the Windows app mode and changes with it.", "System", "theme mode light dark system"));
            look.Children.Add(Described(
                Choice("Seed colour", Appearance.SeedIds.Select((id, i) => (Appearance.SeedLabels[i], id)).ToArray(), Appearance.Seed,
                    v => { s.SeedColour = v; Appearance.Apply(); }),
                "The colour every other colour is derived from. Each one has a full light and dark set.", "Mint", "seed colour accent mint violet amber ocean rose"));
            look.Children.Add(Described(
                Switch("High-contrast outlines", null, s.HighContrastOutlines, v => { s.HighContrastOutlines = v; Appearance.Apply(); }),
                "Draws dividers and card borders in the stronger outline colour so edges are easier to see.", "off", "contrast outlines borders dividers"));
            root.Children.Add(Card(look));

            // ---- Size, density and motion
            var feel = new StackPanel();
            feel.Children.Add(CardTitle("Size, density and motion", "How big things are, how tightly they sit and how much moves."));
            feel.Children.Add(SearchScope.SetItem(
                Number("Text size", "Scales text and spacing together in open windows, 90 to 150 percent. Default: 100 %.", Appearance.ClampPercent(s.TextScalePercent), 90, 150, 10,
                    v => { s.TextScalePercent = (int)v; Appearance.Apply(); }, "%", 0, true),
                "setting choice number field text size font scale zoom bigger smaller"));
            feel.Children.Add(Described(
                Choice("Density", new[] { ("Comfortable", false), ("Compact", true) }, Appearance.Compact,
                    v => { s.CompactDensity = v; Appearance.Apply(); }),
                "Compact tightens row heights so more fits on screen.", "Comfortable", "density compact comfortable rows spacing"));
            feel.Children.Add(Described(
                Choice("Reduce motion", new[] { ("Follow Windows", "follow"), ("On", "on"), ("Off", "off") }, s.MotionMode ?? "follow",
                    v => { s.MotionMode = v; Appearance.Apply(); }),
                "On skips animation. Follow Windows uses the Windows animation setting. Off always animates.", "Follow Windows", "motion animation reduce"));
            root.Children.Add(Card(feel));
            root.Children.Add(Card(SearchScope.SetItem(Tabs.TabStrip.DockChoice(), "tab strip dock edge left top right bottom")));

            // ---- Name and logo
            var ident = new StackPanel();
            ident.Children.Add(CardTitle("Name and logo", "What the app calls itself and the tint of its glyph."));

            var nameBox = Input(Appearance.DisplayName, 320, "Shown in headers and the tray tooltip");
            void CommitName()
            {
                var v = nameBox.Text.Trim();
                s.DisplayName = v.Length == 0 ? Appearance.DefaultName : v;
                if (v.Length == 0) nameBox.Text = Appearance.DefaultName;
                Settings.Save();
                Appearance.Apply();
            }
            nameBox.LostFocus += (a, b) => CommitName();
            nameBox.KeyDown += (a, e) => { if (e.Key == System.Windows.Input.Key.Enter) CommitName(); };
            var nameRow = new StackPanel();
            nameRow.Children.Add(new TextBlock { Text = "Display name", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 8) });
            nameRow.Children.Add(nameBox);
            ident.Children.Add(Described(nameRow, "The name shown for the app. An empty name goes back to the default.", Appearance.DefaultName, "display name rename title"));

            var hexBox = Input(s.LogoCustomHex, 140, "#RRGGBB");
            var hexNote = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.Danger"), Margin = new Thickness(0, 0, 0, 6) };
            var swatch = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), BorderBrush = Ui.Res("B.LineStrong"), Margin = new Thickness(0, 0, 10, 8), Background = new SolidColorBrush(Appearance.LogoColor) };
            void CommitHex()
            {
                var t = hexBox.Text.Trim();
                if (t.Length == 0) { s.LogoCustomHex = ""; hexNote.Text = ""; }
                else if (Appearance.TryParseHex(t, out var c)) { s.LogoCustomHex = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); hexNote.Text = ""; }
                else { hexNote.Text = "Use six hex digits, like #6FDBB4."; return; }
                Settings.Save();
                Appearance.Apply();
                swatch.Background = new SolidColorBrush(Appearance.LogoColor);
            }
            hexBox.LostFocus += (a, b) => CommitHex();
            hexBox.KeyDown += (a, e) => { if (e.Key == System.Windows.Input.Key.Enter) CommitHex(); };

            ident.Children.Add(Described(
                Choice("Logo tint", new[] { ("Match seed colour", "seed"), ("Custom colour", "custom") }, s.LogoSource ?? "seed",
                    v => { s.LogoSource = v; Appearance.Apply(); swatch.Background = new SolidColorBrush(Appearance.LogoColor); }),
                "The colour of the tray and header glyph. Custom uses the hex value below.", "Match seed colour", "logo glyph icon tint colour tray"));
            var hexRow = new StackPanel { Orientation = Orientation.Horizontal };
            hexRow.Children.Add(swatch);
            hexRow.Children.Add(hexBox);
            var hexWrap = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            hexWrap.Children.Add(hexRow);
            hexWrap.Children.Add(hexNote);
            ident.Children.Add(Described(hexWrap, "A custom logo colour as #RRGGBB, applied when the field loses focus or you press Enter. Only used when Logo tint is Custom colour.", "empty", "custom logo hex colour"));
            root.Children.Add(Card(ident));
            root.Children.Add(AppearanceLogoCard());

            // ---- Reset
            var reset = new StackPanel { Orientation = Orientation.Horizontal };
            reset.Children.Add(Ui.Button("Reset to defaults", () => Appearance.ResetToDefaults(), "Btn.Ghost"));
            var resetBlock = new StackPanel();
            resetBlock.Children.Add(CardTitle("Reset", "Puts every choice on this page back to its default: System theme, Mint, 100 %, Comfortable, Follow Windows, normal outlines, the default name and a seed-coloured logo."));
            resetBlock.Children.Add(reset);
            root.Children.Add(Card(SearchScope.SetItem(resetBlock, "reset defaults restore")));

            root.Children.Add(new TextBlock
            {
                Text = "Colours and text size apply at once. Code-built pages pick up new colours when the page reloads; a few older windows pick them up the next time they open.",
                Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left
            });
            return root;
        }

        // A small stand-in for real controls: filled, tonal and text buttons, chips and a switch.
        private static FrameworkElement PreviewSample()
        {
            Border Pill(string text, string bg, string fg, string border = null)
            {
                var b = new Border { CornerRadius = new CornerRadius(20), Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 10, 8) };
                b.SetResourceReference(Border.BackgroundProperty, bg ?? "B.Surface");
                if (border != null) { b.BorderThickness = new Thickness(1); b.SetResourceReference(Border.BorderBrushProperty, border); }
                var t = new TextBlock { Text = text, FontSize = 12, FontFamily = Ui.Font("F.Body") };
                t.SetResourceReference(TextBlock.ForegroundProperty, fg);
                b.Child = t;
                return b;
            }

            var host = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(16), BorderThickness = new Thickness(1) };
            host.SetResourceReference(Border.BackgroundProperty, "B.Window");
            host.SetResourceReference(Border.BorderBrushProperty, "B.Line");

            var col = new StackPanel();
            var buttons = new WrapPanel();
            buttons.Children.Add(Pill("Filled", "B.Mint", "B.MintInk"));
            buttons.Children.Add(Pill("Tonal", "B.SecondaryContainer", "B.OnSecondaryContainer"));
            buttons.Children.Add(Pill("Outlined", null, "B.Mint", "B.LineStrong"));
            buttons.Children.Add(Pill("Text", null, "B.Mint"));
            col.Children.Add(buttons);

            var chips = new WrapPanel();
            chips.Children.Add(Pill("Chip", "B.MintWash", "B.OnPrimaryContainer"));
            chips.Children.Add(Pill("Another", "B.TertiaryContainer", "B.OnTertiaryContainer"));
            chips.Children.Add(Pill("Plain", "B.Raised", "B.TextSoft", "B.Line"));
            col.Children.Add(chips);

            var sw = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var track = new Border { Width = 44, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            track.SetResourceReference(Border.BackgroundProperty, "B.Mint");
            var knob = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            knob.SetResourceReference(Border.BackgroundProperty, "B.MintInk");
            track.Child = knob;
            sw.Children.Add(track);
            var label = new TextBlock { Text = "A switch, turned on", FontSize = 14, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "B.Text");
            sw.Children.Add(label);
            col.Children.Add(sw);

            host.Child = col;
            return host;
        }
    }
}
