using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Applies personal wording to on-screen text in every window, live, and accepts the wording
    /// file dropped anywhere on any window. Text in monospace (paths, commands, code, addresses)
    /// is never touched, and clearing restores every original string.
    /// </summary>
    public static class WordingLayer
    {
        private static readonly DependencyProperty OriginalProperty =
            DependencyProperty.RegisterAttached("Original", typeof(string), typeof(WordingLayer));

        public static void Install()
        {
            EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => Rewrite((TextBlock)s)));
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => Prepare((Window)s)));
            PersonalWording.Changed += () => { foreach (Window w in Application.Current.Windows) Walk(w); };
        }

        // -------------------------------------------------------------- rewriting

        private static void Walk(DependencyObject root)
        {
            if (root is TextBlock tb) Rewrite(tb);
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(root, i));
        }

        private static bool IsLiteral(TextBlock tb)
        {
            var f = tb.FontFamily?.Source ?? "";
            if (f.IndexOf("Mono", StringComparison.OrdinalIgnoreCase) >= 0 || f.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (DependencyObject d = tb; d != null; d = VisualTreeHelper.GetParent(d))
                if (d is TextBox || d is PasswordBox) return true;
            return false;
        }

        private static void Rewrite(TextBlock tb)
        {
            if (IsLiteral(tb)) return;
            foreach (var run in Runs(tb.Inlines))
            {
                if (run.Parent is Hyperlink) continue;   // link text often is the address itself
                var original = (string)run.GetValue(OriginalProperty);
                if (original == null) { original = run.Text; run.SetValue(OriginalProperty, original); }
                var next = PersonalWording.Active ? PersonalWording.Apply(original) : original;
                if (!string.Equals(run.Text, next, StringComparison.Ordinal)) run.Text = next;
            }
        }

        private static System.Collections.Generic.IEnumerable<Run> Runs(InlineCollection inlines)
        {
            foreach (var i in inlines.ToList())
            {
                if (i is Run r) yield return r;
                else if (i is Span sp) foreach (var x in Runs(sp.Inlines)) yield return x;
            }
        }

        // -------------------------------------------------------------- drop anywhere

        private static void Prepare(Window w)
        {
            if (w is CelebrationWindow) return;
            w.AllowDrop = true;
            w.PreviewDragOver += (s, e) =>
            {
                if (JsonFile(e.Data) != null) { e.Effects = DragDropEffects.Copy; e.Handled = true; }
            };
            w.PreviewDrop += (s, e) =>
            {
                var file = JsonFile(e.Data);
                if (file == null) return;   // not ours: leave other drops alone
                e.Handled = true;
                if (PersonalWording.TryApply(file, out var message))
                {
                    Sounds.Play(Cue.Tada);
                    Celebrate(w);
                }
                else
                {
                    Sounds.Play(Cue.Invalid);
                    App.Toasts.ShowNotice("Wording file refused", message, App.Toasts.Amber, 6, icon: Icons.Text);
                }
            };
        }

        private static string JsonFile(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop)) return null;
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1) return null;
            return string.Equals(Path.GetExtension(files[0]), ".json", StringComparison.OrdinalIgnoreCase) ? files[0] : null;
        }

        /// <summary>Settings fallback for the same import.</summary>
        public static void PickAndApply(Window owner)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Wording file (*.json)|*.json", Title = "Choose a personal wording file" };
            if (dlg.ShowDialog(owner) != true) return;
            if (PersonalWording.TryApply(dlg.FileName, out var message)) { Sounds.Play(Cue.Tada); Celebrate(owner); }
            else { Sounds.Play(Cue.Invalid); App.Toasts.ShowNotice("Wording file refused", message, App.Toasts.Amber, 6, icon: Icons.Text); }
        }

        // -------------------------------------------------------------- celebration

        public static void Celebrate(Window over) => new CelebrationWindow(over, PersonalWording.Headline).Show();

        /// <summary>A click-through, full-screen level-up moment: headline in, hold about 3 s, out.</summary>
        private sealed class CelebrationWindow : Window
        {
            public CelebrationWindow(Window over, string headline)
            {
                WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
                ShowInTaskbar = false; Topmost = true; ShowActivated = false; IsHitTestVisible = false; Focusable = false;
                var area = SystemParameters.WorkArea;
                Left = area.Left; Top = area.Top; Width = area.Width; Height = area.Height;

                var reduced = !SystemParameters.ClientAreaAnimation;
                var grid = new Grid { IsHitTestVisible = false };
                var glow = new Border { Width = 760, Height = 760, CornerRadius = new CornerRadius(380), Opacity = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Background = new RadialGradientBrush(Color.FromArgb(0x55, 0x7C, 0xF2, 0xC2), Colors.Transparent) };
                grid.Children.Add(glow);
                var text = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var eyebrow = new TextBlock { Text = "LEVEL UP", FontFamily = Ui.Font("F.Mono"), FontSize = 16, Foreground = Ui.Res("B.Mint"), HorizontalAlignment = HorizontalAlignment.Center };
                var head = new TextBlock
                {
                    Text = headline, FontFamily = Ui.Font("F.Display"), FontWeight = FontWeights.Bold, FontSize = 64, Foreground = Ui.Res("B.Text"),
                    TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 900, Margin = new Thickness(24, 8, 24, 0),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Color = Color.FromRgb(0x7C, 0xF2, 0xC2), Opacity = 0.6 }
                };
                AutomationProperties.SetLiveSetting(head, AutomationLiveSetting.Polite);
                AutomationProperties.SetName(head, headline);
                text.Children.Add(eyebrow); text.Children.Add(head);
                var shift = new TranslateTransform(0, reduced ? 0 : 40);
                var scale = new ScaleTransform(reduced ? 1 : 0.8, reduced ? 1 : 0.8);
                text.RenderTransformOrigin = new Point(0.5, 0.5);
                text.RenderTransform = new TransformGroup { Children = { scale, shift } };
                text.Opacity = 0;
                grid.Children.Add(text);

                var sparks = new Canvas { IsHitTestVisible = false };
                grid.Children.Add(sparks);
                Content = grid;

                SourceInitialized += (s, e) =>
                {
                    var h = new WindowInteropHelper(this).Handle;
                    // click-through: the moment never blocks input
                    const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;
                    var ex = GetWindowLong(h, GWL_EXSTYLE);
                    SetWindowLong(h, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                    Native.MakeToolWindow(this, noActivate: true);
                };
                Loaded += (s, e) =>
                {
                    var peer = UIElementAutomationPeer.CreatePeerForElement(head);
                    peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                    var inMs = reduced ? 200 : 520;
                    text.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(inMs)));
                    glow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(inMs)));
                    if (!reduced)
                    {
                        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(620)) { EasingFunction = Ui.Spring });
                        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.8, 1, TimeSpan.FromMilliseconds(620)) { EasingFunction = Ui.Spring });
                        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.8, 1, TimeSpan.FromMilliseconds(620)) { EasingFunction = Ui.Spring });
                        Sparks(sparks, Width, Height);
                    }
                    Demo.After(inMs + 3000, () =>
                    {
                        var outA = new DoubleAnimation(0, TimeSpan.FromMilliseconds(reduced ? 200 : 420));
                        outA.Completed += (o, ev) => Close();
                        text.BeginAnimation(OpacityProperty, outA);
                        glow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(reduced ? 200 : 420)));
                        if (!reduced) shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-24, TimeSpan.FromMilliseconds(420)) { EasingFunction = Ui.Out });
                    });
                };
            }

            private static void Sparks(Canvas c, double w, double h)
            {
                var rnd = new Random();
                var colors = new[] { Color.FromRgb(0x7C, 0xF2, 0xC2), Color.FromRgb(0xB9, 0xA2, 0xFF), Color.FromRgb(0xFF, 0xB5, 0x47), Color.FromRgb(0x8A, 0xB4, 0xFF) };
                for (int i = 0; i < 46; i++)
                {
                    double size = 4 + rnd.NextDouble() * 7;
                    var dot = new System.Windows.Shapes.Ellipse { Width = size, Height = size, Fill = new SolidColorBrush(colors[i % colors.Length]), Opacity = 0 };
                    double x = w / 2 + (rnd.NextDouble() - 0.5) * 300, y = h / 2 + 40;
                    Canvas.SetLeft(dot, x); Canvas.SetTop(dot, y);
                    c.Children.Add(dot);
                    var tt = new TranslateTransform(); dot.RenderTransform = tt;
                    var d = TimeSpan.FromMilliseconds(1400 + rnd.Next(900));
                    var delay = TimeSpan.FromMilliseconds(rnd.Next(400));
                    tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, (rnd.NextDouble() - 0.5) * 900, d) { BeginTime = delay, EasingFunction = Ui.Out });
                    tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -200 - rnd.NextDouble() * 360, d) { BeginTime = delay, EasingFunction = Ui.Out });
                    var fade = new DoubleAnimationUsingKeyFrames { BeginTime = delay };
                    fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.1)));
                    fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(d)));
                    dot.BeginAnimation(UIElement.OpacityProperty, fade);
                }
            }

            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int i, int v);
        }
    }
}
