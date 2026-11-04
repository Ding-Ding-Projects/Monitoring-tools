using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A right-click menu for every element in a window, also opened with Shift+F10 or the Menu key.
    /// It has its own filter field with an anchored regex builder, shows each item's shortcut, and
    /// offers actions for the exact element: copy its text, search the page for it, press it,
    /// edit the appearance, or lock it.
    /// </summary>
    public static class ElementMenu
    {
        public sealed record Item(string Label, string Icon, string Keys, Action Run, bool Danger = false);

        /// <summary>Elements locked by the user this session, by their text.</summary>
        private static readonly HashSet<string> Locked = new HashSet<string>();

        /// <summary>Supplies extra, window-specific items (for example "Search this page").</summary>
        public static Func<FrameworkElement, IEnumerable<Item>> Extra { get; set; }

        public static void Attach(Window w, Func<FrameworkElement, IEnumerable<Item>> extra = null)
        {
            w.PreviewMouseRightButtonUp += (s, e) =>
            {
                if (e.OriginalSource is not DependencyObject d) return;
                if (InsideText(d)) return; // text boxes keep their own cut, copy and paste menu
                var target = Target(d);
                if (target == null) return;
                e.Handled = true;
                Open(w, target, extra, e.GetPosition(w));
            };
            w.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is DependencyObject fd && !InsideText(fd))
                {
                    var t = Target(fd);
                    var tx = t == null ? "" : TextOf(t);
                    if (tx.Length > 0) { ClipboardWatcher.SetText(tx); e.Handled = true; }
                    return;
                }
                bool menuKey = e.Key == Key.Apps || (e.Key == Key.System && e.SystemKey == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift) || (e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift);
                if (!menuKey) return;
                if (Keyboard.FocusedElement is not DependencyObject d || InsideText(d)) return;
                var target = Target(d);
                if (target == null) return;
                e.Handled = true;
                var p = target.TranslatePoint(new Point(12, target.ActualHeight / 2), w);
                Open(w, target, extra, p);
            };
        }

        private static bool InsideText(DependencyObject d)
        {
            for (; d != null; d = Parent(d)) if (d is TextBox) return true;
            return false;
        }

        private static DependencyObject Parent(DependencyObject d) => d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

        /// <summary>The nearest element a person would think of as "this": a search result, a button, or a card.</summary>
        private static FrameworkElement Target(DependencyObject d)
        {
            FrameworkElement button = null, card = null;
            for (; d != null; d = Parent(d))
            {
                if (d is FrameworkElement fe)
                {
                    if (SearchScope.GetItem(fe) != null) return fe;
                    if (button == null && fe is ButtonBase) button = fe;
                    if (card == null && fe is Border b && b.CornerRadius.TopLeft >= 20 && b.Child != null) card = fe;
                }
            }
            return button ?? card;
        }

        private static string TextOf(FrameworkElement t)
        {
            var s = SearchScope.Text(t);
            s = string.Join(" ", s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            return s.Trim();
        }

        public static void Open(Window w, FrameworkElement target, Func<FrameworkElement, IEnumerable<Item>> extra, Point at)
        {
            var text = TextOf(target);
            var key = text.Length > 120 ? text[..120] : text;
            var items = new List<Item>();
            if (target is ButtonBase bb && bb.IsEnabled)
                items.Add(new Item("Press", Icons.Play, "Enter", () => bb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent))));
            if (text.Length > 0)
                items.Add(new Item("Copy text", Icons.Clipboard, "Ctrl+C", () => ClipboardWatcher.SetText(text)));
            if (extra != null) items.AddRange(extra(target) ?? Enumerable.Empty<Item>());
            items.Add(new Item("Edit appearance…", Icons.Palette, null, () => AppearanceEditorWindow.Open(w, target)));
            if (Locked.Contains(key))
                items.Add(new Item("Unlock this element", Icons.Shield, null, () => { Locked.Remove(key); SetLocked(target, false); }));
            else
                items.Add(new Item("Lock this element…", Icons.Shield, null, () =>
                {
                    var ok = new ConfirmWindow("Lock this element?", "It stays visible but cannot be clicked until you unlock it from this menu. Locks last until the app restarts.", "Lock");
                    ok.Confirmed += yes => { if (yes) { Locked.Add(key); SetLocked(target, true); } };
                    ok.Show();
                }));

            var menu = new MenuPopup(items, target);
            menu.Show(w, at);
        }

        private static void SetLocked(FrameworkElement t, bool locked)
        {
            t.IsHitTestVisible = !locked;
            t.Opacity = locked ? 0.5 : 1;
            AutomationProperties.SetHelpText(t, locked ? "Locked from its menu" : "");
        }

        /// <summary>The menu surface: a Material 3 menu with a filter, keyboard navigation and shortcut hints.</summary>
        private sealed class MenuPopup
        {
            private readonly List<Item> _items;
            private readonly Popup _popup;
            private readonly StackPanel _rows = new StackPanel();
            private readonly SearchField _filter;
            private readonly Border _card;
            private readonly FrameworkElement _origin;
            private List<Item> _shown;
            private int _sel;

            public MenuPopup(List<Item> items, FrameworkElement origin)
            {
                _items = items;
                _origin = origin;
                _filter = new SearchField("menu:element", "Filter actions", compact: true) { Margin = new Thickness(0, 0, 0, 6) };
                _filter.SampleSource = () => _items.Select(i => i.Label);
                _filter.Filter += Render;
                var stack = new StackPanel();
                stack.Children.Add(_filter);
                stack.Children.Add(_rows);
                _card = new Border
                {
                    Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(16), Padding = new Thickness(8), MinWidth = 280, MaxWidth = 380,
                    Margin = new Thickness(0, 0, 20, 20), Child = stack,
                    Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"],
                    RenderTransformOrigin = new Point(0, 0), RenderTransform = new ScaleTransform(1, 1)
                };
                AutomationProperties.SetName(_card, "Actions for this element");
                _popup = new Popup { Child = _card, StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Relative, Focusable = true };
                _card.PreviewKeyDown += OnKey;
                _popup.Closed += (s, e) => { if (!_filter.BuilderOpen) _origin.Focus(); };
            }

            public void Show(Window w, Point at)
            {
                _filter.State.SetQuery("");
                _popup.PlacementTarget = w;
                _popup.HorizontalOffset = at.X;
                _popup.VerticalOffset = at.Y;
                Render();
                _popup.IsOpen = true;
                if (!Appearance.ReduceMotion)
                {
                    var t = (ScaleTransform)_card.RenderTransform;
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    t.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
                    t.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
                    _card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
                }
                _filter.Box.Focus();
            }

            private void Render()
            {
                _shown = _items.Where(i => _filter.State.Matches(i.Label + " " + (i.Keys ?? ""))).ToList();
                _filter.SetCount(_shown.Count, _items.Count, "actions");
                if (_sel >= _shown.Count) _sel = 0;
                _rows.Children.Clear();
                for (int i = 0; i < _shown.Count; i++) _rows.Children.Add(Row(_shown[i], i));
            }

            private FrameworkElement Row(Item it, int index)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var ink = it.Danger ? Ui.Res("B.Danger") : Ui.Res("B.Text");
                g.Children.Add(new ContentControl { Content = Ui.Icon(it.Icon ?? Icons.Bolt, it.Danger ? ink : Ui.Res("B.TextSoft"), 16), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Focusable = false });
                var label = new TextBlock { Text = it.Label, Foreground = ink, FontSize = 14, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(label, 1); g.Children.Add(label);
                if (!string.IsNullOrEmpty(it.Keys))
                {
                    var k = Ui.Mono(it.Keys, Ui.Res("B.TextMute"), 11);
                    k.VerticalAlignment = VerticalAlignment.Center; k.Margin = new Thickness(16, 0, 0, 0);
                    Grid.SetColumn(k, 2); g.Children.Add(k);
                }
                var b = new Button { Style = Ui.Style("Btn.Row"), Content = g, Height = 44, Margin = new Thickness(0, 1, 0, 1) };
                if (index == _sel) b.Background = Ui.Res("B.SecondaryContainer");
                AutomationProperties.SetName(b, it.Label + (string.IsNullOrEmpty(it.Keys) ? "" : ", " + it.Keys));
                b.Click += (s, e) => Run(it);
                return b;
            }

            private void Run(Item it)
            {
                _popup.IsOpen = false;
                try { it.Run?.Invoke(); } catch (Exception ex) { Log.Warn("Menu action failed: " + ex.Message); }
            }

            private void OnKey(object sender, KeyEventArgs e)
            {
                if (_filter.BuilderOpen) return;
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    if (_filter.State.Query.Length > 0) _filter.State.SetQuery("");
                    else _popup.IsOpen = false;
                }
                else if (e.Key == Key.Down && _shown.Count > 0) { e.Handled = true; _sel = (_sel + 1) % _shown.Count; Render(); }
                else if (e.Key == Key.Up && _shown.Count > 0) { e.Handled = true; _sel = (_sel - 1 + _shown.Count) % _shown.Count; Render(); }
                else if (e.Key == Key.Enter && _shown.Count > 0) { e.Handled = true; Run(_shown[_sel]); }
            }
        }
    }
}
