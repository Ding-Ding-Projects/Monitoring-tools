using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
            Title = Core.Language.Headline("welcome");
            Card("Clipboard toasts", "Every copy slides in for 3 seconds. Hover to hold it and see actions made for what you copied.", Icons.Clipboard, "Try it", Demo.TryClipboard, primary: true);
            Card("Quick commands", "Copy a path with :mp4, :gif or :push on the end and it just happens. Try converts a sample picture.", Icons.Bolt, "Try it", Demo.TryQuickCommand);
            Card("Internet watch", "When the line drops you see where it broke, plus one-press fixes. It stays until it is back.", Icons.Wifi, "Preview the dialog", Demo.PreviewOffline, tint: "B.AmberWash");
            Card("File tools", "Previews, move, copy, Base64 and password encryption for anything you copy.", Icons.Shield, "Try it", Demo.TryFileTools);
            Card("Your shortcuts", "Write a batch or PowerShell script, name it, and copy anything with :name to run it.", Icons.Code, "Open editor", () => App.Current.OpenControlCentre("shortcuts"));
            Card("Game mode", "Roblox, Minecraft, League and friends keep the screen. Popups wait until you stop.", Icons.Game, "Pick games", () => App.Current.OpenControlCentre("game"), tint: "B.VioletWash");

            var (_, status) = App.Tray.State();
            StatusText.Text = status;
            SourceInitialized += (s, e) => Native.ApplyDarkChrome(this);
            ElementMenu.Attach(this);
            CommandPalette.Attach(this);
            Loaded += (s, e) => Enter();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private void Card(string title, string body, string icon, string button, Action act, bool primary = false, string tint = null)
        {
            var sp = new StackPanel();
            var chip = Ui.IconChip(icon, Ui.Res("B.Mint"), Ui.Res("B.MintWash"), 36, 18);
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(chip);
            sp.Children.Add(new TextBlock { Text = title, Style = Ui.Style("T.Title"), FontSize = 16, Margin = new Thickness(0, 14, 0, 6) });
            sp.Children.Add(new TextBlock { Text = body, Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), LineHeight = 19, MinHeight = 58 });
            var b = Ui.Button(button, act, primary ? "Btn.Accent" : "Btn.Ghost");
            b.HorizontalAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(b);
            var card = new Border
            {
                Background = Ui.Res(tint ?? "B.Surface"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(28), Padding = new Thickness(18), Margin = new Thickness(7), Child = sp,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            // lift on hover
            var lift = new TranslateTransform();
            card.RenderTransform = lift;
            card.MouseEnter += (s, e) => { card.SetResourceReference(Border.BorderBrushProperty, "B.LineStrong"); lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-4, TimeSpan.FromMilliseconds(220)) { EasingFunction = Ui.Spring }); };
            card.MouseLeave += (s, e) => { card.SetResourceReference(Border.BorderBrushProperty, "B.Line"); lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = Ui.Out }); };
            // every brush above came from the theme, so make each one follow later theme changes
            ThemeLive.Bind(card);
            Cards.Children.Add(card);
        }

        private void Enter()
        {
            if (Core.Appearance.ReduceMotion || Core.Modes.ReduceMotion) { Root.Opacity = 1; return; }
            RootScale.ScaleX = RootScale.ScaleY = 0.95;
            RootShift.Y = 18;
            Root.Opacity = 0;
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(460)) { EasingFunction = Ui.Spring });
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(460)) { EasingFunction = Ui.Spring });
            RootShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(460)) { EasingFunction = Ui.Spring });
            Ui.Fade(Root, 1, 260);
            Ui.Rise(Hero, 80, 12, 420);
            int i = 0;
            foreach (FrameworkElement c in Cards.Children)
            {
                // cards keep their hover transform, so animate a wrapper-free fade and rise via opacity only plus a margin tween
                c.Opacity = 0;
                c.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360)) { BeginTime = TimeSpan.FromMilliseconds(180 + i * 60), EasingFunction = Ui.Out });
                ((TranslateTransform)c.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(460)) { BeginTime = TimeSpan.FromMilliseconds(180 + i * 60), EasingFunction = Ui.Spring, FillBehavior = FillBehavior.Stop });
                i++;
            }
            var grow = new DoubleAnimation(1, 2.4, TimeSpan.FromMilliseconds(1800)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = Ui.Out };
            PulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            PulseScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            Pulse.BeginAnimation(OpacityProperty, new DoubleAnimation(0.5, 0, TimeSpan.FromMilliseconds(1800)) { RepeatBehavior = RepeatBehavior.Forever });
            Activate();
        }

        private void OnClose(object s, RoutedEventArgs e) => Close();
        private void OnCentre(object s, RoutedEventArgs e) { Close(); App.Current.OpenControlCentre(null); }

        private void OnDrag(object s, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !(e.OriginalSource is DependencyObject d && IsInButton(d))) try { DragMove(); } catch { }
        }

        private static bool IsInButton(DependencyObject d)
        {
            while (d != null) { if (d is Button) return true; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
            return false;
        }
    }
}
