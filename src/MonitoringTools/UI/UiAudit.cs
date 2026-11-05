using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The accessibility and Material 3 audit. The language audit already tours every page, window and state of the built app; while a
    /// pass is active, each of its scans also lands here, and every visible control, text block and container is measured against a
    /// named checklist (see docs/features/app/accessibility-audit.md and material3-audit.md):
    ///   accessibility: keyboard reach (tab stop and a real traversal), an accessible name on every control, the smallest target size
    ///   (24 by 24 with the spacing exception), text contrast against the real background, in both themes;
    ///   Material 3: colour roles (no literal colours), the type scale and typeface, the shape scale, state layers on every interactive
    ///   style, elevation, and component anatomy (heights and widths of buttons, switches and text fields).
    /// Findings are collected, de-duplicated by control, and written to JSON so a person can read what the audit saw.
    /// </summary>
    internal static class UiAudit
    {
        internal sealed class Finding
        {
            public string Check { get; set; }
            public string Theme { get; set; }
            public string Where { get; set; }
            public string Control { get; set; }
            public string Detail { get; set; }
            public int Seen { get; set; } = 1;
        }

        internal static bool Active;
        private static string _theme = "";
        private static readonly Dictionary<string, Finding> Found = new Dictionary<string, Finding>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly SortedDictionary<double, int> FontSizes = new SortedDictionary<double, int>();
        private static readonly SortedDictionary<double, int> Corners = new SortedDictionary<double, int>();
        private static readonly SortedDictionary<string, int> ButtonSizes = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private static readonly SortedDictionary<string, int> Shadows = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> TraversedWindows = new HashSet<string>(StringComparer.Ordinal);

        internal static IReadOnlyCollection<Finding> Findings => Found.Values;
        internal static IReadOnlyDictionary<string, int> Tally => Counts;

        internal static void Reset()
        {
            Found.Clear(); Counts.Clear(); FontSizes.Clear(); Corners.Clear(); ButtonSizes.Clear(); Shadows.Clear(); TraversedWindows.Clear();
        }

        internal static void Begin(string theme) { _theme = theme; Active = true; TraversedWindows.Clear(); }
        internal static void End() { Active = false; }

        private static void Count(string key, int by = 1) { Counts.TryGetValue(key, out var n); Counts[key] = n + by; }

        private static void Add(string check, string where, FrameworkElement e, string detail)
        {
            var ctl = Describe(e);
            var key = check + "|" + _theme + "|" + ctl + "|" + detail;
            if (Found.TryGetValue(key, out var f)) { f.Seen++; return; }
            Found[key] = new Finding { Check = check, Theme = _theme, Where = where, Control = ctl, Detail = detail };
        }

        // ------------------------------------------------------------------ naming a control

        private static string Describe(DependencyObject d)
        {
            if (d == null) return "(none)";
            var sb = new StringBuilder(d.GetType().Name);
            if (d is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name)) sb.Append('#').Append(fe.Name);
            string label = null;
            if (d is ContentControl cc) label = Drive.LabelOf(cc);
            else if (d is TextBlock tb) label = tb.Text;
            else if (d is TextBox tx) label = tx.Text;
            if (!string.IsNullOrWhiteSpace(label)) sb.Append(" \"").Append(Trunc(label.Replace("\n", " "), 36)).Append('"');
            var an = d is UIElement ? AutomationProperties.GetName(d) : null;
            if (!string.IsNullOrEmpty(an) && an != label) sb.Append(" [").Append(Trunc(an, 36)).Append(']');
            var chain = new List<string>();
            var p = d;
            for (int i = 0; i < 4 && p != null; i++)
            {
                p = p is Visual ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p);
                if (p is FrameworkElement pf && p is not Window) chain.Add(p.GetType().Name + (string.IsNullOrEmpty(pf.Name) ? "" : "#" + pf.Name));
            }
            if (chain.Count > 0) sb.Append(" < ").Append(string.Join(" < ", chain));
            return sb.ToString();
        }

        private static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";

        // ------------------------------------------------------------------ what counts as a control a person operates

        private static bool IsInsideTemplateOfOther(FrameworkElement e)
        {
            var tp = e.TemplatedParent;
            if (tp == null) return false;
            return tp is ComboBox || tp is ScrollBar || tp is Slider || tp is ScrollViewer || tp is TabControl || tp is System.Windows.Controls.Calendar || tp is DatePicker || tp is DataGrid || tp is ListBoxItem || tp is TextBoxBase || tp is PasswordBox || tp is MenuItem || tp is Track;
        }

        internal static bool IsOperable(FrameworkElement e)
        {
            if (e is RepeatButton || e is Thumb || e is ScrollBar) return false;
            if (e.Name != null && e.Name.StartsWith("PART_", StringComparison.Ordinal)) return false;
            if (IsInsideTemplateOfOther(e)) return false;
            if (e is ButtonBase || e is ComboBox || e is Slider || e is PasswordBox || e is TabItem || e is MenuItem) return true;
            if (e is TextBoxBase) return true;
            if (e is Selector && e is not ComboBox && e is not TabControl) return true;
            return false;
        }

        private static IEnumerable<FrameworkElement> Operables(DependencyObject root)
        {
            foreach (var d in Drive.Descendants(root))
                if (d is FrameworkElement fe && fe.IsVisible && fe.ActualWidth >= 1 && fe.ActualHeight >= 1 && fe.IsHitTestVisible && IsOperable(fe)) yield return fe;
        }

        // ------------------------------------------------------------------ entry point called by the language audit's tour

        internal static void Collect(string where, DependencyObject root)
        {
            if (!Active || root == null) return;
            try
            {
                var win = root as Window ?? Window.GetWindow(root);
                var ops = Operables(root).ToList();
                Count("scans");
                Count("controls", ops.Count);
                AccessibleNames(where, ops);
                TargetSizes(where, ops, win);
                if (root is Window w) Traverse(where, w, ops);
                TextAndContrast(where, root, win);
                ColoursAndShapes(where, root);
            }
            catch (Exception ex) { Add("audit error", where, null, ex.GetType().Name + ": " + ex.Message); }
        }

        // ------------------------------------------------------------------ accessibility 1: names

        private static void AccessibleNames(string where, List<FrameworkElement> ops)
        {
            foreach (var e in ops)
            {
                string name = null;
                try { name = UIElementAutomationPeer.CreatePeerForElement(e)?.GetName(); } catch { }
                Count("named-checked");
                if (string.IsNullOrWhiteSpace(name))
                {
                    // an editable control may be named by a label in front of it (LabeledBy) or by its placeholder text
                    if (e is TextBoxBase && AutomationProperties.GetLabeledBy(e) != null) continue;
                    Add("accessible name", where, e, "has no accessible name");
                    continue;
                }
                if (!name.Any(char.IsLetterOrDigit) && !(e.ToolTip is string tipText && tipText.Any(char.IsLetter))) Add("accessible name", where, e, "the name is only a symbol (\"" + Trunc(name.Trim(), 12) + "\")");
            }
        }

        // ------------------------------------------------------------------ accessibility 2: target size (WCAG 2.2, 2.5.8: 24 by 24, or enough space around it)

        private static void TargetSizes(string where, List<FrameworkElement> ops, Window win)
        {
            if (win == null) return;
            double scale = Appearance.TextScale;
            var boxes = new List<(FrameworkElement E, Rect R)>();
            foreach (var e in ops)
            {
                try
                {
                    var r = new Rect(e.TransformToAncestor(win).Transform(new Point(0, 0)), new Size(e.ActualWidth * scale, e.ActualHeight * scale));
                    boxes.Add((e, r));
                }
                catch { }
            }
            foreach (var (e, r) in boxes)
            {
                Count("size-checked");
                var kind = e is Button b ? (b.Style == Ui.Style("Btn.Icon") ? "icon button" : "button") : e.GetType().Name;
                var key = kind + " " + (int)Math.Round(r.Width / scale) + "x" + (int)Math.Round(r.Height / scale);
                ButtonSizes.TryGetValue(key, out var n); ButtonSizes[key] = n + 1;
                if (!e.IsEnabled && e is not TextBoxBase) continue;
                if (r.Width >= 23.5 && r.Height >= 23.5) continue;
                // the spacing exception: a 24 diameter circle centred on the target must not touch another target
                var c = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
                bool clear = true;
                foreach (var (o, or) in boxes)
                {
                    if (o == e) continue;
                    double dx = Math.Max(or.Left - c.X, Math.Max(0, c.X - or.Right)), dy = Math.Max(or.Top - c.Y, Math.Max(0, c.Y - or.Bottom));
                    if (dx * dx + dy * dy < 144) { clear = false; break; }
                }
                if (!clear) Add("target size", where, e, $"{r.Width:0}x{r.Height:0} is under 24x24 and another target is within 12 of its centre");
                else Count("size-spacing-exception");
            }
        }

        // ------------------------------------------------------------------ accessibility 3: keyboard reach

        private static bool IsTabReachable(FrameworkElement e)
        {
            if (!e.Focusable || !KeyboardNavigation.GetIsTabStop(e) || !e.IsEnabled || !e.IsVisible) return false;
            for (DependencyObject p = e; p != null; p = p is Visual ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p))
                if (KeyboardNavigation.GetTabNavigation(p) == KeyboardNavigationMode.None && p != e) return false;
            return true;
        }

        private static void Traverse(string where, Window w, List<FrameworkElement> ops)
        {
            var enabled = ops.Where(o => o.IsEnabled).ToList();
            // static reachability: a control a person can press must be a tab stop, with no ancestor that switches Tab off
            foreach (var e in enabled)
            {
                Count("tab-checked");
                if (e is Selector) continue;                                 // a list is entered through its items; the traversal below checks them
                if (where.StartsWith("command palette", StringComparison.Ordinal)) continue;   // the palette is driven by Up, Down, Enter and Ctrl+Space on the selected row
                if (!IsTabReachable(e)) Add("keyboard reach", where, e, "is not a tab stop (Focusable=" + e.Focusable + ", IsTabStop=" + KeyboardNavigation.GetIsTabStop(e) + ")");
            }
            // a real traversal: press Tab from the first element and record where focus lands
            if (!TraversedWindows.Add(w.GetType().Name + "|" + where)) return;
            try
            {
                if (!w.IsActive) { try { w.Activate(); } catch { } }
                var reached = new HashSet<DependencyObject>();
                var start = w.Content as UIElement;
                if (start == null) return;
                start.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                var cur = Keyboard.FocusedElement as DependencyObject;
                if (cur == null || Window.GetWindow(cur) != w) { Count("traversal-unavailable"); return; }
                int steps = 0;
                for (; steps < 600 && cur != null; steps++)
                {
                    if (!reached.Add(cur)) break;
                    if (cur is UIElement ue) ue.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    else if (cur is FrameworkContentElement fce) fce.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));   // a link inside text
                    var next = Keyboard.FocusedElement as DependencyObject;
                    if (next == null || ReferenceEquals(next, cur)) break;
                    cur = next;
                }
                Count("traversals");
                Count("traversal-steps", steps);
                foreach (var e in enabled)
                {
                    if (reached.Contains(e)) continue;
                    if (e is Selector sel && sel.Items.Count == 0) continue;   // an empty list has nothing to focus
                    if (where.StartsWith("command palette", StringComparison.Ordinal)) continue;
                    // a control reached through its own inner element (a text box inside a combo box, a button inside a template) counts
                    bool viaInner = reached.OfType<DependencyObject>().Any(r => Drive.Ancestors(r).Contains(e));
                    if (viaInner) continue;
                    Add("keyboard reach", where, e, "Tab never lands on it (traversal of " + steps + " stops, ended at " + Trunc(Describe(cur), 70) + ")");
                }
            }
            catch (Exception ex) { Count("traversal-error"); Add("keyboard reach", where, null, "traversal failed: " + ex.GetType().Name + ": " + ex.Message); }
        }

        // ------------------------------------------------------------------ text: contrast, type scale, typeface

        // Material 3 type scale, in dp: label small 11, label/body small 12, label large and body medium 14, title large 22, body large and title medium 16,
        // headline 24 to 32, display 36 to 57. The app sets font sizes in device-independent pixels, which equal dp at 100 percent.
        internal static readonly double[] TypeScale = { 11, 12, 14, 16, 22, 24, 28, 32, 36, 45, 57 };

        private static bool Opaque(Color c) => c.A >= 250;

        private static Color Over(Color top, Color under)
        {
            double a = top.A / 255.0;
            return Color.FromRgb((byte)Math.Round(top.R * a + under.R * (1 - a)), (byte)Math.Round(top.G * a + under.G * (1 - a)), (byte)Math.Round(top.B * a + under.B * (1 - a)));
        }

        internal static double Luminance(Color c)
        {
            double Ch(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }

        internal static double Ratio(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05) / (lb + 0.05);
        }

        /// <summary>The colour a pixel behind <paramref name="e"/> has, from the nearest opaque solid fill outward, or null when a gradient or picture sits there.</summary>
        private static Color? BackdropOf(DependencyObject e, Window win)
        {
            var layers = new List<Color>();
            bool done = false, unknown = false;
            void Take(Brush br, UIElement owner)
            {
                if (br == null || done) return;
                if (br is SolidColorBrush sb)
                {
                    var col = sb.Color; double op = sb.Opacity;
                    if (owner != null) op *= owner.Opacity;
                    col.A = (byte)Math.Round(col.A * op);
                    if (col.A == 0) return;
                    layers.Add(col);
                    if (Opaque(col)) done = true;
                }
                else if (br is GradientBrush || br is ImageBrush || br is VisualBrush) { unknown = true; done = true; }
            }
            DependencyObject child = e;
            for (var p = Parent(e); p != null && !done; child = p, p = Parent(p))
            {
                // a control's own Background is painted only when its template binds it; the template's borders and panels carry the real fill
                Brush br = p switch { Border b => b.Background, Panel pa => pa.Background, TextBlock t => t.Background, _ => null };
                Take(br, p as UIElement);
                if (done) break;
                // a template often paints its fill in a border that sits beneath the content in the same grid, not around it
                if (p is Panel panel && child is UIElement cu)
                {
                    int at = panel.Children.IndexOf(cu);
                    for (int i = at - 1; i >= 0 && !done; i--)
                        if (panel.Children[i] is Border sib && sib.IsVisible && sib.Opacity >= 0.9 && sib.ActualWidth >= 0.9 * panel.ActualWidth && sib.ActualHeight >= 0.9 * panel.ActualHeight)
                            Take(sib.Background, sib);
                }
                if (p is Window) break;
            }
            if (unknown) return null;
            Color acc;
            if (win?.Background is SolidColorBrush wb && !ReferenceEquals(win.Background, SystemColors.WindowBrush) && wb.Color != Colors.White) acc = wb.Color;
            else acc = Application.Current.Resources["B.Window"] is SolidColorBrush rb ? rb.Color : Colors.Black;   // a bare host window shows the theme's window colour
            acc.A = 255;
            for (int i = layers.Count - 1; i >= 0; i--) acc = Over(layers[i], acc);
            return acc;
        }

        private static DependencyObject Parent(DependencyObject d) => d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

        private static double EffectiveOpacity(DependencyObject d)
        {
            double o = 1;
            for (; d != null; d = Parent(d)) if (d is UIElement u) o *= u.Opacity;
            return o;
        }

        private static void TextAndContrast(string where, DependencyObject root, Window win)
        {
            foreach (var d in Drive.Descendants(root))
            {
                if (d is not FrameworkElement fe || !fe.IsVisible || fe.ActualWidth < 1 || fe.ActualHeight < 1) continue;
                string text = d switch { TextBlock t => t.Text, TextBox x => x.Text, _ => null };
                if (d is TextBlock tb0 && string.IsNullOrWhiteSpace(tb0.Text) && tb0.Inlines.Count > 0) text = string.Concat(tb0.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text));
                if (d is not (TextBlock or TextBox or PasswordBox)) continue;
                if (d is PasswordBox) text = "x";
                if (string.IsNullOrWhiteSpace(text)) continue;
                // font size, typeface
                double size = fe is TextBlock tbs ? tbs.FontSize : ((Control)fe).FontSize;
                var family = fe is TextBlock tbf ? tbf.FontFamily : ((Control)fe).FontFamily;
                bool mono = text.Length > 0 && (family?.Source ?? "").IndexOf("Cascadia", StringComparison.OrdinalIgnoreCase) >= 0;
                Count("text-checked");
                FontSizes.TryGetValue(size, out var fsn); FontSizes[size] = fsn + 1;
                var src = family?.Source ?? "";
                bool preview = text.Trim() == src.Trim();   // a font picker shows each font's name in that font, at its own size
                if (!preview && !TypeScale.Contains(size)) Add("type scale", where, fe, $"font size {size:0.##} is not a Material 3 type scale size (11, 12, 14, 16, 22, 24, 28, 32, 36, 45, 57)");
                if (preview) { }
                else if (src.IndexOf("Roboto", StringComparison.OrdinalIgnoreCase) < 0 && !mono && src.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) < 0 && src.IndexOf("Emoji", StringComparison.OrdinalIgnoreCase) < 0 && src.IndexOf("Segoe", StringComparison.OrdinalIgnoreCase) < 0)
                    Add("typeface", where, fe, "typeface \"" + Trunc(src, 40) + "\" is neither Roboto nor the monospace stack");
                else if (src.IndexOf("Roboto", StringComparison.OrdinalIgnoreCase) < 0 && !mono && src.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) < 0)
                    Add("typeface", where, fe, "typeface \"" + Trunc(src, 40) + "\" is not Roboto");

                // contrast
                if (!fe.IsEnabled) continue;
                Brush fb = d is TextBlock t2 ? t2.Foreground : ((Control)d).Foreground;
                if (fb is not SolidColorBrush fsb) { Count("contrast-skipped-brush"); continue; }
                var bd = BackdropOf(d, win);
                if (bd == null) { Count("contrast-skipped-backdrop"); continue; }
                double eo = EffectiveOpacity(d);
                if (eo < 0.98) { Count("contrast-skipped-opacity"); continue; }   // mid-fade or dimmed on purpose: the settled colour is what a person reads
                var fc = fsb.Color; double op = fsb.Opacity * eo;
                fc.A = (byte)Math.Round(fc.A * Math.Min(1, op));
                if (fc.A < 8) continue;
                var eff = Over(fc, bd.Value);
                double ratio = Ratio(eff, bd.Value);
                bool bold = fe is TextBlock tw ? tw.FontWeight.ToOpenTypeWeight() >= 600 : ((Control)fe).FontWeight.ToOpenTypeWeight() >= 600;
                bool large = size >= 24 || (size >= 18.66 && bold);
                double need = large ? 3.0 : 4.5;
                Count("contrast-checked");
                if (ratio + 0.005 < need)
                    Add("contrast", where, fe, $"contrast {ratio:0.00}:1 is under {need:0.0}:1 (text {Hex(eff)} on {Hex(bd.Value)}, {size:0.#} px)");
            }
        }

        private static string Hex(Color c) => "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

        // ------------------------------------------------------------------ Material 3: colour roles, shape, elevation

        private static HashSet<int> ThemeColours()
        {
            var set = new HashSet<int>();
            void Walk(ResourceDictionary d, HashSet<ResourceDictionary> seen)
            {
                if (d == null || !seen.Add(d)) return;
                foreach (var key in d.Keys.OfType<string>().Where(k => k.StartsWith("C.", StringComparison.Ordinal)))
                    if (d[key] is Color c) set.Add((c.R << 16) | (c.G << 8) | c.B);
                foreach (var m in d.MergedDictionaries) Walk(m, seen);
            }
            Walk(Application.Current.Resources, new HashSet<ResourceDictionary>());
            set.Add(0x000000); set.Add(0xFFFFFF);   // scrims and shadows are black, highlights white; they never carry text
            return set;
        }

        // Material 3 shape scale in dp: none 0, extra small 4, small 8, medium 12, large 16, large increased 20, extra large 28, extra large increased 32,
        // extra extra large 48, full (a radius of half the shortest side or more).
        internal static readonly double[] ShapeScale = { 0, 4, 8, 12, 16, 20, 28, 32, 48 };

        private static void ColoursAndShapes(string where, DependencyObject root)
        {
            var theme = ThemeColours();
            foreach (var d in Drive.Descendants(root))
            {
                if (d is not FrameworkElement fe || !fe.IsVisible) continue;
                // literal colours: a brush set directly on the element that is not one of the theme's role colours
                foreach (var dp in new[] { Control.BackgroundProperty, Control.ForegroundProperty, Control.BorderBrushProperty, Border.BackgroundProperty, Border.BorderBrushProperty, Panel.BackgroundProperty, TextBlock.ForegroundProperty, Shape.FillProperty, Shape.StrokeProperty })
                {
                    if (!dp.OwnerType.IsInstanceOfType(d) && !(dp == TextBlock.ForegroundProperty && d is TextBlock)) continue;
                    if (d.ReadLocalValue(dp) is SolidColorBrush sb && sb.Color.A > 0)
                    {
                        Count("brush-literal-checked");
                        int rgb = (sb.Color.R << 16) | (sb.Color.G << 8) | sb.Color.B;
                        if (!theme.Contains(rgb)) Add("colour roles", where, fe, "literal colour " + Hex(sb.Color) + " on " + dp.Name + " is not one of the theme's roles");
                    }
                }
                if (d is Border b && b.CornerRadius.TopLeft == b.CornerRadius.TopRight && b.CornerRadius.TopLeft == b.CornerRadius.BottomLeft && b.CornerRadius.TopLeft == b.CornerRadius.BottomRight && b.ActualWidth > 1)
                {
                    double r = b.CornerRadius.TopLeft;
                    Corners.TryGetValue(r, out var cn); Corners[r] = cn + 1;
                    bool full = r >= Math.Min(b.ActualWidth, b.ActualHeight) / 2 - 0.6;
                    bool authored = Math.Abs(r - Math.Round(r)) < 0.001 && b.Name != "Focus" && r > 1;   // a focus ring follows its container's radius; an animated radius is not authored
                    if (!full && authored && !ShapeScale.Contains(r)) Add("shape scale", where, fe, $"corner radius {r:0.##} is not on the Material 3 shape scale (0, 4, 8, 12, 16, 20, 28, 32, 48 or full)");
                    Count("shape-checked");
                }
                if (fe.Effect is DropShadowEffect ds)
                {
                    var key = $"blur {ds.BlurRadius:0} depth {ds.ShadowDepth:0} opacity {ds.Opacity:0.00}";
                    Shadows.TryGetValue(key, out var sn); Shadows[key] = sn + 1;
                    Count("shadows");
                }
            }
        }

        // ------------------------------------------------------------------ Material 3: state layers on every interactive style

        internal static List<string> StyleStateReport(out string table)
        {
            var problems = new List<string>();
            var sb = new StringBuilder();
            var res = Application.Current.Resources;
            foreach (var key in new[] { "Btn.Ghost", "Btn.Accent", "Btn.Alert", "Btn.Danger", "Btn.Chip", "Btn.Icon", "Btn.Row", "Switch", "Input" })
            {
                if (res[key] is not Style st) { problems.Add("style " + key + " is missing"); continue; }
                ControlTemplate tpl = null;
                for (var s = st; s != null && tpl == null; s = s.BasedOn)
                    tpl = s.Setters.OfType<Setter>().FirstOrDefault(x => x.Property == Control.TemplateProperty)?.Value as ControlTemplate;
                if (tpl == null) { problems.Add("style " + key + " has no template"); continue; }
                var props = tpl.Triggers.OfType<Trigger>().Select(t => t.Property.Name + "=" + t.Value).ToList();
                bool hover = props.Any(p => p.StartsWith("IsMouseOver=")), pressed = props.Any(p => p.StartsWith("IsPressed=")), focus = props.Any(p => p.StartsWith("IsKeyboardFocused=") || p.StartsWith("IsFocused=")), disabled = props.Any(p => p.StartsWith("IsEnabled="));
                sb.AppendLine($"{key}: hover {(hover ? "yes" : "NO")}, pressed {(pressed ? "yes" : "NO")}, focus {(focus ? "yes" : "NO")}, disabled {(disabled ? "yes" : "NO")}");
                bool isText = key == "Input";
                bool toggle = key == "Switch";
                if (!hover) problems.Add($"{key}: no hover state layer");
                if (!pressed && !isText && !toggle) problems.Add($"{key}: no pressed state layer");
                if (toggle && !pressed) problems.Add($"{key}: no pressed state (a CheckBox exposes IsPressed)");
                if (!focus) problems.Add($"{key}: no focus indicator");
                if (!disabled) problems.Add($"{key}: no disabled state (38 percent)");
            }
            table = sb.ToString();
            return problems;
        }

        // ------------------------------------------------------------------ colour pairs from the palette tables, for every seed in both themes

        internal static List<string> PaletteContrast(out int pairs)
        {
            var problems = new List<string>();
            pairs = 0;
            var saved = (Settings.Current.ThemeMode, Settings.Current.SeedColour);
            try
            {
                foreach (var mode in new[] { "light", "dark" })
                    foreach (var seed in Appearance.SeedIds)
                    {
                        Settings.Current.ThemeMode = mode; Settings.Current.SeedColour = seed; Appearance.Apply();
                        Color C(string n) => (Color)Application.Current.Resources["C." + n];
                        var surfaces = new[] { "Window", "Surface", "Raised", "Sunken" };
                        foreach (var fg in new[] { "Text", "TextSoft" })
                            foreach (var bg in surfaces) { pairs++; Pair(problems, mode, seed, fg, bg, 4.5); }
                        foreach (var bg in surfaces) { pairs++; Pair(problems, mode, seed, "TextMute", bg, 4.5); }
                        Pair(problems, mode, seed, "MintInk", "Mint", 4.5); Pair(problems, mode, seed, "OnPrimaryContainer", "MintWash", 4.5);
                        Pair(problems, mode, seed, "OnSecondaryContainer", "SecondaryContainer", 4.5); Pair(problems, mode, seed, "OnTertiaryContainer", "TertiaryContainer", 4.5);
                        Pair(problems, mode, seed, "Mint", "Surface", 3.0);   // the accent as a line or icon: 3:1 for graphics
                        Pair(problems, mode, seed, "AmberInk", "Amber", 4.5); Pair(problems, mode, seed, "Danger", "DangerWash", 4.5);
                        Pair(problems, mode, seed, "Amber", "AmberWash", 4.5); Pair(problems, mode, seed, "Violet", "VioletWash", 4.5);
                        pairs += 9;
                    }
            }
            finally { Settings.Current.ThemeMode = saved.ThemeMode; Settings.Current.SeedColour = saved.SeedColour; Appearance.Apply(); }
            return problems;
        }

        private static void Pair(List<string> problems, string mode, string seed, string fg, string bg, double need)
        {
            var f = (Color)Application.Current.Resources["C." + fg]; var b = (Color)Application.Current.Resources["C." + bg];
            double r = Ratio(f, b);
            if (r + 0.005 < need) problems.Add($"{mode} {seed}: {fg} {Hex(f)} on {bg} {Hex(b)} is {r:0.00}:1, under {need:0.0}:1");
        }

        // ------------------------------------------------------------------ the report

        internal static void Write(string dir, string name)
        {
            var report = new
            {
                at = DateTime.Now.ToString("O"),
                tally = Counts,
                fontSizes = FontSizes.ToDictionary(k => k.Key.ToString(CultureInfo.InvariantCulture), v => v.Value),
                cornerRadii = Corners.ToDictionary(k => k.Key.ToString(CultureInfo.InvariantCulture), v => v.Value),
                targetSizes = ButtonSizes,
                shadows = Shadows,
                findings = Found.Values.OrderBy(f => f.Check).ThenBy(f => f.Theme).ThenBy(f => f.Where).ToList()
            };
            File.WriteAllText(System.IO.Path.Combine(dir, name), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
