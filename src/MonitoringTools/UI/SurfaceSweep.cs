using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The every-surface sweep. It opens every control centre page, toast, menu, dialog and setup step in every look, every
    /// language mode, at a desktop size, on a 1080p screen at 200 percent (960 by 540, less the taskbar) and at 150 percent
    /// text, and walks each window's real layout. It reports text that is cut off or trimmed, text taller than its box,
    /// content wider than a box that cannot scroll, parts outside their window, windows bigger than the screen, overlapping
    /// text, controls with no accessible name or no visible focus, and a keyboard walk that lands on hidden controls.
    /// Results go to sweep.json and sweep.md; the run fails the build when a finding is left.
    /// </summary>
    internal static class SurfaceSweep
    {
        internal sealed class Finding
        {
            public string Kind { get; set; }
            public string Surface { get; set; }
            public string Element { get; set; }
            public string Detail { get; set; }
            public List<string> Configs { get; set; } = new List<string>();
        }

        /// <summary>Kinds that fail the sweep. Trimmed text is listed for review: an ellipsis on a long path or address is by design.</summary>
        internal static readonly string[] Gating = { "clipped", "text-area-overflow", "client-overflow", "outside-window", "window-too-big", "overlap", "unnamed", "no-focus-ring", "focus-hidden" };

        private static readonly Dictionary<string, Finding> Found = new Dictionary<string, Finding>();
        private static int _scans;
        private static string _config = "";
        private static Rect _area;

        public static async Task RunAsync(string dir)
        {
            Directory.CreateDirectory(dir);
            var s = Settings.Current;
            var started = DateTime.UtcNow;
            s.MotionMode = "on";            // no entrance animations: every scan sees the settled layout
            s.ToastSeconds = 600;
            Demo.SeedLivedIn();
            var samples = Demo.EnsureSamples();
            var sizes = new (string Name, double W, double H, Rect? Area, int Text)[]
            {
                ("desktop 1180x820", 1180, 820, null, 100),
                ("1080p at 200% (960x540)", 960, 500, new Rect(0, 0, 960, 500), 100),
                ("desktop at 150% text", 1180, 820, null, 150),
            };
            var langs = new[] { (LanguageMode.English, "English"), (LanguageMode.Cantonese, "Cantonese"), (LanguageMode.Bilingual, "bilingual") };
            try
            {
                foreach (var look in Looks.All)
                foreach (var (mode, langName) in langs)
                foreach (var size in sizes)
                {
                    if (size.Text != 100 && mode != LanguageMode.English) continue;   // large text is checked in English per look
                    s.Look = look.Id; s.SeedColour = look.DefaultSeed; s.ThemeMode = "dark"; s.LanguageMode = mode; s.TextScalePercent = size.Text;
                    Ui.WorkAreaOverride = size.Area;
                    _area = Ui.WorkArea;
                    Appearance.Apply();
                    Language.Refresh();
                    _config = $"{look.Name}, {langName}, {size.Name}";
                    await SweepConfig(size.W, size.H, samples);
                }
            }
            finally
            {
                Ui.WorkAreaOverride = null;
                s.LanguageMode = LanguageMode.English; s.TextScalePercent = 100; s.Look = "graphite"; s.SeedColour = "mint";
                Appearance.Apply();
            }
            Write(dir, DateTime.UtcNow - started);
        }

        private static async Task SweepConfig(double w, double h, Dictionary<string, string> samples)
        {
            // the control centre, every page
            var cc = new ControlCentreWindow { Width = Math.Min(w, _area.Width), Height = Math.Min(h, _area.Height) };
            cc.Show();
            await Settle(400);
            foreach (var page in cc.SectionIds.ToList())
            {
                cc.Navigate(page, animate: false);
                await Settle(120);
                Scan(cc, "control centre: " + page);
            }
            Walk(cc, "control centre");
            cc.Close();

            // toasts
            foreach (var (name, item) in new (string, Func<ClipItem>)[]
            {
                ("text toast", () => ClipItem.FromText("Meeting moved to Thursday at 3 pm. Bring the printed agenda and the budget notes.")),
                ("link toast", () => ClipItem.FromText(Demo.SampleUrl)),
                ("files toast, cut", () => { var c = ClipItem.FromFiles(new[] { samples["image"], samples["image2"], samples["csv"] }); c.Gesture = global::MonitoringTools.Clip.Smart.CopyGesture.Cut; return c; }),
                ("phone number toast", () => ClipItem.FromText("+1 416 555 0134")),
            })
            {
                var t = App.Toasts.ShowClip(item());
                t.ExpandNow();
                await Settle(250);
                Scan(t, name);
                t.CloseAnimated();
            }

            await Surface(new TrayMenuWindow(), "tray menu", m => ((TrayMenuWindow)m).ShowAtCursor());
            await Surface(new WelcomeWindow(), "welcome tour");
            await Surface(new OfflineWindow(Demo.SampleOutage()), "offline dialog");
            App.Toasts.ReservedBottom = 0;
            await Surface(new ConfirmWindow("Uninstall Monitoring Tools?", "This stops the background service, removes it from start-up and deletes the program.", "Uninstall", danger: true), "confirmation");

            var wiz = new SetupWizardWindow();
            wiz.Show();
            await Settle(300);
            for (int i = 0; i < 8; i++)
            {
                wiz.GoTo(i);
                await Settle(150);
                Scan(wiz, "setup step " + (i + 1));
            }
            Walk(wiz, "setup wizard");
            wiz.Close();
            Settings.Current.OnboardingStep = 0;

            CommandPalette.Open();
            await Settle(200);
            if (CommandPalette.Current != null) { Scan(CommandPalette.Current, "command palette"); Walk(CommandPalette.Current, "command palette"); CommandPalette.Current.Close(); }

            foreach (var (name, open) in new (string, Func<Window>)[]
            {
                ("snippets", () => Dialogs.SnippetsDialog.Open()),
                ("transform recipes", () => Dialogs.RecipesDialog.Open()),
                ("per-app rules", () => Dialogs.RulesDialog.Open()),
                ("extensions", () => Dialogs.ExtensionsDialog.Open()),
                ("quick conversions", () => Dialogs.ConversionsDialog.Open("12 kg")),
                ("clipboard settings", () => Dialogs.ClipboardSettingsDialog.Open()),
            })
            {
                Window d = null;
                try { d = open(); } catch (Exception ex) { Add("open-failed", name, "", ex.Message); }
                if (d == null) continue;
                await Settle(300);
                Scan(d, name);
                Walk(d, name);
                d.Close();
            }
        }

        private static async Task Surface(Window w, string surface, Action<Window> show = null)
        {
            if (show != null) show(w); else w.Show();
            await Settle(300);
            Scan(w, surface);
            Walk(w, surface);
            w.Close();
        }

        private static async Task Settle(int ms)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(ms);
            await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }

        // ------------------------------------------------------------------ the layout scan

        private static void Scan(Window w, string surface)
        {
            _scans++;
            w.UpdateLayout();
            if (w.ActualWidth > _area.Width + 1 || w.ActualHeight > _area.Height + 1)
                Add("window-too-big", surface, "window", $"{w.ActualWidth:0} x {w.ActualHeight:0} on a {_area.Width:0} x {_area.Height:0} screen");
            var texts = new List<(TextBlock Tb, Rect R)>();
            var client = new Rect(0, 0, w.ActualWidth, w.ActualHeight);
            Visit(w, w, client, client, 1.0, "the window", surface, texts);
            // overlapping text: two labels drawn on top of each other, compared by the part of each that is visible
            for (int i = 0; i < texts.Count; i++)
                for (int j = i + 1; j < texts.Count; j++)
                {
                    var a = texts[i].R; var b = texts[j].R;
                    var x = Rect.Intersect(a, b);
                    if (x.IsEmpty || x.Width < 3 || x.Height < 3) continue;
                    if (Contains(a, b) || Contains(b, a)) continue;
                    Add("overlap", surface, Describe(texts[i].Tb) + " and " + Describe(texts[j].Tb), $"{x.Width:0} x {x.Height:0} px overlap");
                }
        }

        private static bool Contains(Rect outer, Rect inner) => outer.Left <= inner.Left + 0.5 && outer.Top <= inner.Top + 0.5 && outer.Right >= inner.Right - 0.5 && outer.Bottom >= inner.Bottom - 0.5;

        /// <summary>
        /// Walks the visual tree carrying two boxes. The visible box is what is on screen right now; the hard box is what
        /// can ever be on screen, made only by boxes that clip and do not scroll: a box that clips to its bounds, the layout
        /// slot of an element given less room than it needs (drawn cut, with no ellipsis), and a scroll viewer's viewport on
        /// an axis it cannot scroll. Text past the hard box is cut for good; text past only the visible box is scrolled out
        /// of view, which is not a cut, even when the box that clips it is itself partly scrolled out.
        /// </summary>
        private static void Visit(Window w, DependencyObject d, Rect clip, Rect hard, double opacity, string clipBy, string surface, List<(TextBlock, Rect)> texts)
        {
            if (d is UIElement ue && (!ue.IsVisible || ue.Opacity * opacity < 0.05)) return;
            if (d is UIElement u2) opacity *= u2.Opacity;
            if (d is FrameworkElement fe && fe.ActualWidth > 0 && fe.ActualHeight > 0 && !(d is Window))
            {
                Rect r;
                GeneralTransform toWindow;
                try { toWindow = fe.TransformToAncestor(w); r = toWindow.TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight)); } catch (InvalidOperationException) { return; }
                bool textual = fe is TextBlock tbc && !string.IsNullOrWhiteSpace(tbc.Text) || fe is ButtonBase || fe is TextBox;
                // wholly outside what can ever be shown: never on screen, so nothing to check
                bool hidden = r.Right < hard.Left - 1 || r.Left > hard.Right + 1 || r.Bottom < hard.Top - 1 || r.Top > hard.Bottom + 1;
                if (!hidden)
                {
                    double cx = Math.Max(hard.Left - r.Left, r.Right - hard.Right), cy = Math.Max(hard.Top - r.Top, r.Bottom - hard.Bottom);
                    bool cutX = cx > 1.5, cutY = cy > 1.5;
                    if ((cutX || cutY) && textual)
                        Add(hard.Width >= w.ActualWidth - 1 && hard.Height >= w.ActualHeight - 1 ? "outside-window" : "clipped", surface, Describe(fe),
                            Cut(cutX ? cx : 0, cutY ? cy : 0) + " by " + clipBy + Where(fe));
                }
                // the element's own layout slot, when it is smaller than the element
                Rect slot = Rect.Empty;
                var lg = LayoutInformation.GetLayoutClip(fe);
                if (lg != null && !(fe is ScrollContentPresenter))
                {
                    var lb = lg.Bounds;
                    if (!lb.IsEmpty)
                    {
                        double sx = Math.Max(-lb.Left, fe.ActualWidth - lb.Right), sy = Math.Max(-lb.Top, fe.ActualHeight - lb.Bottom);
                        if (!hidden && textual && !fe.ClipToBounds && (sx > 1.5 || sy > 1.5))
                            Add("clipped", surface, Describe(fe), Cut(sx > 1.5 ? sx : 0, sy > 1.5 ? sy : 0) + " by its own slot" + Where(fe));
                        slot = toWindow.TransformBounds(lb);
                    }
                }
                var visible = Rect.Intersect(r, clip);
                if (!slot.IsEmpty) visible = Rect.Intersect(visible, slot);
                if (!hidden && fe is TextBlock tb) CheckText(tb, visible, surface, texts);
                if (!hidden && fe is Control c) CheckControl(c, surface);
                if (fe is ScrollViewer sv)
                {
                    if (sv.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled && sv.ExtentWidth > sv.ViewportWidth + 1.5)
                        Add("client-overflow", surface, Describe(sv), $"content {sv.ExtentWidth:0} px wide in a {sv.ViewportWidth:0} px box that cannot scroll sideways");
                    if (sv.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled && sv.ExtentHeight > sv.ViewportHeight + 1.5)
                        Add("client-overflow", surface, Describe(sv), $"content {sv.ExtentHeight:0} px tall in a {sv.ViewportHeight:0} px box that cannot scroll");
                }
                if (fe is ScrollContentPresenter scp && scp.TemplatedParent is ScrollViewer owner)
                {
                    clip = Rect.Intersect(clip, r);
                    if (clip.IsEmpty) clip = new Rect(r.Left, r.Top, 0, 0);   // scrolled out: the content is still checked against the hard box
                    bool sxOk = owner.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled, syOk = owner.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
                    // on an axis that scrolls, the content may run past the viewport and every box around it: that is scrolling, not a cut
                    double hl = sxOk ? -1e7 : Math.Max(hard.Left, r.Left), hr = sxOk ? 1e7 : Math.Min(hard.Right, r.Right);
                    double ht = syOk ? -1e7 : Math.Max(hard.Top, r.Top), hb = syOk ? 1e7 : Math.Min(hard.Bottom, r.Bottom);
                    hard = new Rect(new Point(hl, ht), new Point(Math.Max(hl, hr), Math.Max(ht, hb)));
                    if (!sxOk || !syOk) clipBy = "the scrolling area " + Describe(owner);
                }
                else if (fe.ClipToBounds || !slot.IsEmpty)
                {
                    var box = slot.IsEmpty ? r : slot;
                    clip = Rect.Intersect(clip, box);
                    if (clip.IsEmpty) clip = new Rect(box.Left, box.Top, 0, 0);
                    hard = Rect.Intersect(hard, box);
                    clipBy = Describe(fe);
                    if (hard.IsEmpty) return;
                }
            }
            int n = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = 0; i < n; i++) Visit(w, VisualTreeHelper.GetChild(d, i), clip, hard, opacity, clipBy, surface, texts);
        }

        private static string Cut(double x, double y) =>
            x > 0 && y > 0 ? $"sides cut by {x:0} px and top or bottom by {y:0} px" : x > 0 ? $"sides cut by {x:0} px" : $"top or bottom cut by {y:0} px";

        /// <summary>The element's nearest named or typed ancestors, so a finding can be traced to the code that builds it.</summary>
        private static string Where(FrameworkElement fe)
        {
            var parts = new List<string>();
            for (var p = VisualTreeHelper.GetParent(fe); p != null && parts.Count < 4; p = VisualTreeHelper.GetParent(p))
            {
                if (!(p is FrameworkElement pf)) continue;
                if (pf is ContentPresenter || pf is System.Windows.Documents.AdornerDecorator || pf is System.Windows.Documents.AdornerLayer) continue;
                var name = AutomationProperties.GetName(pf);
                parts.Add(pf.GetType().Name + (string.IsNullOrEmpty(pf.Name) ? "" : "#" + pf.Name) + (string.IsNullOrWhiteSpace(name) ? "" : " \"" + (name.Length > 30 ? name.Substring(0, 27) + "..." : name) + "\"") + $" {pf.ActualWidth:0}w");
            }
            return parts.Count == 0 ? "" : "; inside " + string.Join(" < ", parts);
        }

        private static void CheckText(TextBlock tb, Rect visible, string surface, List<(TextBlock, Rect)> texts)
        {
            var text = tb.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!visible.IsEmpty && visible.Width >= 1 && visible.Height >= 1) texts.Add((tb, visible));
            double avail = tb.ActualWidth - tb.Padding.Left - tb.Padding.Right;
            if (avail <= 0) return;
            FormattedText ft;
            try
            {
                // measured the way the text block lays itself out: the same formatting mode (Display snaps glyphs to pixels)
                ft = new FormattedText(text, CultureInfo.CurrentUICulture, tb.FlowDirection, new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                    tb.FontSize, Brushes.Black, null, TextOptions.GetTextFormattingMode(tb), VisualTreeHelper.GetDpi(tb).PixelsPerDip);
            }
            catch { return; }
            // Text that does not fit its slot is drawn cut by WPF's layout clip, which the walk reports from the layout itself.
            // Measuring again here only tells what an ellipsis hides, and catches wrapped text held to a set height.
            if (tb.TextWrapping == TextWrapping.NoWrap)
            {
                if (tb.TextTrimming != TextTrimming.None && ft.WidthIncludingTrailingWhitespace > avail + 2 && ft.Width > avail + 2)
                    Add("trimmed", surface, Describe(tb), $"needs {ft.Width:0} px, has {avail:0} px" + Where(tb));
            }
            else if (!double.IsNaN(tb.Height) || !double.IsPositiveInfinity(tb.MaxHeight))
            {
                ft.MaxTextWidth = avail + 0.5;
                if (!double.IsNaN(tb.LineHeight) && tb.LineHeight > 0) ft.LineHeight = tb.LineHeight;
                double availH = tb.ActualHeight - tb.Padding.Top - tb.Padding.Bottom;
                if (ft.Height > availH + 3)
                    Add(tb.TextTrimming == TextTrimming.None ? "text-area-overflow" : "trimmed", surface, Describe(tb), $"needs {ft.Height:0} px tall, has {availH:0} px" + Where(tb));
            }
        }

        private static void CheckControl(Control c, string surface)
        {
            if (!c.IsEnabled || !c.Focusable || !KeyboardNavigation.GetIsTabStop(c)) return;
            if (!(c is ButtonBase || c is TextBox || c is ComboBox || c is Slider || c is PasswordBox || c is ListBoxItem)) return;
            string name = null;
            try { name = UIElementAutomationPeer.CreatePeerForElement(c)?.GetName(); } catch { }
            if (string.IsNullOrWhiteSpace(name)) Add("unnamed", surface, Describe(c), "a control with no accessible name");
            if (c.FocusVisualStyle == null && !HasFocusTrigger(c)) Add("no-focus-ring", surface, Describe(c), "no focus visual and no keyboard-focus trigger in its template or style");
        }

        private static bool HasFocusTrigger(Control c)
        {
            static bool Is(DependencyProperty p) => p == UIElement.IsKeyboardFocusedProperty || p == UIElement.IsFocusedProperty || p == UIElement.IsKeyboardFocusWithinProperty;
            static bool InTriggers(TriggerCollection ts) => ts != null && ts.Any(t => t is Trigger tr && Is(tr.Property) || t is MultiTrigger mt && mt.Conditions.Any(x => Is(x.Property)));
            if (c.Template != null && InTriggers(c.Template.Triggers)) return true;
            for (var st = c.Style; st != null; st = st.BasedOn) if (InTriggers(st.Triggers)) return true;
            return Focus.HasCodeRing(c);
        }

        // ------------------------------------------------------------------ the keyboard walk

        /// <summary>Tabs through the window from its first control, at most 200 stops, and flags focus that lands somewhere unseen.</summary>
        private static void Walk(Window w, string surface)
        {
            try
            {
                w.Activate();
                Keyboard.ClearFocus();
                if (!w.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))) return;
                var first = Keyboard.FocusedElement;
                var seen = new HashSet<object>();
                for (int i = 0; i < 200; i++)
                {
                    if (!(Keyboard.FocusedElement is UIElement f)) break;
                    if (System.Windows.Window.GetWindow(f) != w) { Add("focus-hidden", surface, Describe(f as FrameworkElement), "Tab left the window"); break; }
                    if (!seen.Add(f)) break;
                    var fe = f as FrameworkElement;
                    if (fe != null && (!fe.IsVisible || fe.ActualWidth < 1 || fe.ActualHeight < 1 || AncestorOpacity(fe) < 0.05))
                        Add("focus-hidden", surface, Describe(fe), "keyboard focus on a control that cannot be seen");
                    if (!f.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next))) break;
                    if (Keyboard.FocusedElement == first) break;
                }
            }
            catch (Exception ex) { Add("walk-failed", surface, "", ex.Message); }
        }

        private static double AncestorOpacity(DependencyObject d)
        {
            double o = 1;
            for (var p = d; p != null; p = VisualTreeHelper.GetParent(p)) if (p is UIElement u) o *= u.Opacity;
            return o;
        }

        // ------------------------------------------------------------------ results

        private static string Describe(FrameworkElement fe)
        {
            if (fe == null) return "(nothing)";
            string label = fe is TextBlock tb ? tb.Text : AutomationProperties.GetName(fe);
            if (string.IsNullOrWhiteSpace(label) && fe is ContentControl cc && cc.Content is string str) label = str;
            label = (label ?? "").Replace('\n', ' ').Trim();
            if (label.Length > 70) label = label.Substring(0, 67) + "...";
            return fe.GetType().Name + (label.Length > 0 ? " \"" + label + "\"" : "") + (string.IsNullOrEmpty(fe.Name) ? "" : " #" + fe.Name);
        }

        private static void Add(string kind, string surface, string element, string detail)
        {
            var key = kind + "|" + surface + "|" + element;
            if (!Found.TryGetValue(key, out var f)) Found[key] = f = new Finding { Kind = kind, Surface = surface, Element = element, Detail = detail };
            if (!f.Configs.Contains(_config)) f.Configs.Add(_config);
        }

        private static void Write(string dir, TimeSpan took)
        {
            var all = Found.Values.OrderBy(f => Array.IndexOf(Gating, f.Kind) < 0 ? 1 : 0).ThenBy(f => f.Kind).ThenBy(f => f.Surface).ToList();
            int gating = all.Count(f => Gating.Contains(f.Kind));
            File.WriteAllText(Path.Combine(dir, "sweep.json"), JsonSerializer.Serialize(new { scans = _scans, seconds = (int)took.TotalSeconds, gating, findings = all }, new JsonSerializerOptions { WriteIndented = true }));
            var md = new StringBuilder();
            md.AppendLine("# Every-surface sweep");
            md.AppendLine();
            md.AppendLine($"{_scans} surface scans across 4 looks, 3 language modes, a desktop window, a 1080p screen at 200 percent and 150 percent text, in {took.TotalMinutes:0.0} minutes.");
            md.AppendLine($"Findings that fail the sweep: {gating}. Trimmed text to review: {all.Count(f => f.Kind == "trimmed")}.");
            md.AppendLine();
            foreach (var g in all.GroupBy(f => f.Kind))
            {
                md.AppendLine($"## {g.Key} ({g.Count()})");
                md.AppendLine();
                foreach (var f in g.Take(400)) md.AppendLine($"- {f.Surface}: {f.Element}. {f.Detail}. Seen in {f.Configs.Count} of the runs, first {f.Configs[0]}.");
                md.AppendLine();
            }
            File.WriteAllText(Path.Combine(dir, "sweep.md"), md.ToString());
        }

        /// <summary>Controls that draw their own keyboard-focus ring in code register here so the sweep knows the ring exists.</summary>
        internal static class Focus
        {
            private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Rings = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
            public static void Mark(Control c) => Rings.AddOrUpdate(c, null);
            public static bool HasCodeRing(Control c) => Rings.TryGetValue(c, out _);
        }
    }
}
