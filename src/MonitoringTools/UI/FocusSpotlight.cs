using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The Focus spotlight, one of the ADHD aids: everything in the window is dimmed except the card under keyboard focus or
    /// the mouse. It is switched on from the Modes page or with Ctrl+Shift+Space, never blocks clicks, and moves the
    /// highlight without animation when motion is reduced. Windows without an adorner layer are left alone.
    /// </summary>
    public static class FocusSpotlight
    {
        private static bool _installed;
        private static readonly ConditionalWeakTable<Window, SpotAdorner> Active = new ConditionalWeakTable<Window, SpotAdorner>();

        public static bool Enabled => Settings.Current.AdhdSpotlight;

        /// <summary>Hook the shortcut and follow the setting. Call once at start-up, after the settings are loaded.</summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKey));
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => Sync((Window)s)));
            EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnFocus), true);
            Modes.Changed += SyncAll;
        }

        /// <summary>Turn the spotlight on or off and keep the choice.</summary>
        public static void Set(bool on)
        {
            Settings.Current.AdhdSpotlight = on;
            Settings.Save();
            Modes.NotifyChanged();
        }

        private static void OnKey(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Space && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                e.Handled = true;
                Set(!Enabled);
            }
        }

        private static void OnFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!Enabled || !(e.NewFocus is DependencyObject d)) return;
            var w = Window.GetWindow(d);
            if (w != null && Active.TryGetValue(w, out var a)) a.Follow(e.NewFocus as UIElement, fromKeyboard: true);
        }

        private static void SyncAll()
        {
            if (Application.Current == null) return;
            foreach (Window w in Application.Current.Windows) Sync(w);
        }

        private static bool Eligible(Window w)
            => w.IsLoaded && w.ShowInTaskbar && w.WindowStyle != WindowStyle.None && !w.AllowsTransparency && w.Content is UIElement;

        private static void Sync(Window w)
        {
            bool want = Enabled && Eligible(w);
            bool has = Active.TryGetValue(w, out var existing);
            if (want && !has)
            {
                var layer = AdornerLayer.GetAdornerLayer((UIElement)w.Content);
                if (layer == null) return;   // no adorner layer in this window: honest no-op
                var adorner = new SpotAdorner((UIElement)w.Content, w);
                layer.Add(adorner);
                Active.Add(w, adorner);
                adorner.Follow(Keyboard.FocusedElement as UIElement, fromKeyboard: true);
            }
            else if (!want && has)
            {
                existing.Detach();
                Active.Remove(w);
            }
        }

        // ------------------------------------------------------------------ the dim layer

        private sealed class SpotAdorner : Adorner
        {
            private readonly Window _window;
            private readonly UIElement _root;
            private Rect _from = Rect.Empty, _to = Rect.Empty;

            public static readonly DependencyProperty ProgressProperty =
                DependencyProperty.Register("Progress", typeof(double), typeof(SpotAdorner),
                    new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
            private double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

            public SpotAdorner(UIElement root, Window window) : base(root)
            {
                _root = root; _window = window;
                IsHitTestVisible = false;
                window.PreviewMouseMove += OnMouse;
                window.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) => Retarget(animate: false)), true);
                window.SizeChanged += (s, e) => Retarget(animate: false);
                Focusable = false;
                Retarget(animate: false);
            }

            public void Detach()
            {
                _window.PreviewMouseMove -= OnMouse;
                (Parent as AdornerLayer)?.Remove(this);
            }

            private void OnMouse(object sender, MouseEventArgs e)
            {
                Follow(e.OriginalSource as UIElement ?? (e.OriginalSource as FrameworkContentElement)?.Parent as UIElement, fromKeyboard: false);
            }

            public void Follow(UIElement element, bool fromKeyboard)
            {
                var spot = Card(element);
                if (spot == null) return;
                Retarget(animate: true, spot);
            }

            private UIElement _spot;

            private void Retarget(bool animate, UIElement spot = null)
            {
                if (spot != null) _spot = spot;
                if (_spot == null || !_spot.IsVisible || !_root.IsVisible) { _to = Rect.Empty; InvalidateVisual(); return; }
                Rect now;
                try { now = _spot.TransformToAncestor(_root).TransformBounds(new Rect(_spot.RenderSize)); }
                catch (InvalidOperationException) { return; }
                now.Inflate(6, 6);
                if (now == _to) return;
                var shown = Current();
                _from = shown; _to = now;
                bool still = Appearance.ReduceMotion || Modes.ReduceMotion || !animate || shown.IsEmpty;
                BeginAnimation(ProgressProperty, null);
                if (still) { Progress = 1; _from = _to; }
                else
                {
                    Progress = 0;
                    BeginAnimation(ProgressProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = Ui.Out });
                }
                InvalidateVisual();
            }

            private Rect Current()
            {
                if (_to.IsEmpty) return Rect.Empty;
                if (_from.IsEmpty) return _to;
                double t = Progress;
                return new Rect(_from.X + (_to.X - _from.X) * t, _from.Y + (_to.Y - _from.Y) * t,
                                Math.Max(0, _from.Width + (_to.Width - _from.Width) * t), Math.Max(0, _from.Height + (_to.Height - _from.Height) * t));
            }

            /// <summary>The card around an element: the nearest rounded container, or the element itself when none surrounds it.</summary>
            private UIElement Card(UIElement start)
            {
                DependencyObject d = start;
                UIElement fallback = null;
                for (int i = 0; d != null && i < 60; i++)
                {
                    if (d == _root) break;
                    if (d is Border b && b.CornerRadius.TopLeft >= 16 && b.ActualWidth > 120 && b.ActualHeight > 40) return b;
                    if (fallback == null && d is Control c && c.ActualHeight > 20 && !(d is ScrollViewer)) fallback = c;
                    d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
                }
                return fallback;
            }

            protected override void OnRender(DrawingContext dc)
            {
                var size = _root.RenderSize;
                var spot = Current();
                if (spot.IsEmpty || size.Width <= 0 || size.Height <= 0) return;
                var all = new RectangleGeometry(new Rect(0, 0, size.Width, size.Height));
                var hole = new RectangleGeometry(spot, 22, 22);
                var shade = new CombinedGeometry(GeometryCombineMode.Exclude, all, hole);
                shade.Freeze();
                var dim = new SolidColorBrush(Color.FromArgb(0xA8, 0, 0, 0));
                dim.Freeze();
                dc.DrawGeometry(dim, null, shade);
            }
        }
    }
}
