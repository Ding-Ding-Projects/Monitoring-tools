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

        private static readonly List<WeakReference<Window>> Attached = new List<WeakReference<Window>>();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, LockBadge> Badges = new System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, LockBadge>();
        private static readonly Dictionary<string, Window> OpenPrompts = new Dictionary<string, Window>();
        private static bool _wired;

        /// <summary>Supplies extra, window-specific items (for example "Search this page").</summary>
        public static Func<FrameworkElement, IEnumerable<Item>> Extra { get; set; }

        public static void Attach(Window w, Func<FrameworkElement, IEnumerable<Item>> extra = null)
        {
            WireLocks(w);
            w.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (!Locks.Any || e.OriginalSource is not DependencyObject d) return;
                var locked = LockedAncestor(w, d);
                if (locked != null) { e.Handled = true; Prompt(w, locked); }
            };
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
                if (Locks.Any && (e.Key == Key.Enter || e.Key == Key.Space) && Keyboard.FocusedElement is DependencyObject lf && !InsideText(lf))
                {
                    var locked = LockedAncestor(w, lf);
                    if (locked != null) { e.Handled = true; Prompt(w, locked); return; }
                }
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
            var pageId = PageIdOf(w);
            var key = KeyOf(target);
            var entry = key == null ? null : Locks.Find(pageId, key);
            bool locked = entry != null && !Locks.IsOpen(entry);
            var items = new List<Item>();
            if (!locked && target is ButtonBase bb && bb.IsEnabled)
                items.Add(new Item("Press", Icons.Play, "Enter", () => bb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent))));
            if (text.Length > 0)
                items.Add(new Item("Copy text", Icons.Clipboard, "Ctrl+C", () => ClipboardWatcher.SetText(text)));
            if (extra != null) items.AddRange(extra(target) ?? Enumerable.Empty<Item>());
            items.Add(new Item("Edit appearance…", Icons.Palette, null, () => App.Current.OpenControlCentre("appearance")));
            if (key != null)
            {
                if (entry == null)
                    items.Add(new Item("Lock this element…", Icons.Shield, null, () => new LockWizardWindow(target, pageId, key, key, null).Show()));
                else if (locked)
                    items.Add(new Item("Unlock this element…", Icons.Shield, null, () => PromptEntry(entry, target)));
                else
                {
                    items.Add(new Item("Lock again", Icons.Shield, null, () => Locks.Relock(entry.Id)));
                    items.Add(new Item("Remove this lock", Icons.Trash, null, () => { var err = Locks.Remove(entry.Id); if (err != null) Log.Warn("Lock not removed: " + err); }, true));
                }
            }

            var menu = new MenuPopup(items, target);
            menu.Show(w, at);
        }

        // -------------------------------------------------------------- toy locks

        /// <summary>The identity of the page a window is showing, so the same words on two pages can be locked separately.</summary>
        public static string PageIdOf(Window w) => w is ControlCentreWindow c && c.PageSearch != null ? c.PageSearch.State.Id.Replace("page:", "") : w.GetType().Name;

        /// <summary>The lock key for an element's visible text, or null when it has none. Search-only words are left out so the key does not change when a setting flips.</summary>
        public static string KeyOf(FrameworkElement t)
        {
            var s = SearchScope.Text(t);
            var extra = SearchScope.GetItem(t);
            if (!string.IsNullOrEmpty(extra) && s.StartsWith(extra)) s = s.Substring(extra.Length);
            return KeyOfText(s);
        }

        public static string KeyOfText(string s)
        {
            s = string.Join(" ", (s ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (s.Length == 0) return null;
            return s.Length > 120 ? s[..120] : s;
        }

        private static bool IsCandidate(FrameworkElement fe) =>
            fe is ButtonBase || SearchScope.GetItem(fe) != null || (fe is Border b && b.CornerRadius.TopLeft >= 20 && b.Child != null);

        /// <summary>The nearest ancestor (or the element itself) that is under a lock that is still closed.</summary>
        private static FrameworkElement LockedAncestor(Window w, DependencyObject d)
        {
            var page = PageIdOf(w);
            for (; d != null; d = Parent(d))
            {
                if (d is FrameworkElement fe && IsCandidate(fe))
                {
                    var key = KeyOf(fe);
                    if (key != null && Locks.IsLocked(page, key)) return fe;
                }
            }
            return null;
        }

        private static void Prompt(Window w, FrameworkElement locked)
        {
            var key = KeyOf(locked);
            var entry = key == null ? null : Locks.Find(PageIdOf(w), key);
            if (entry != null) PromptEntry(entry, locked);
        }

        /// <summary>Opens the unlock prompt for a lock beside its element. One prompt per lock.</summary>
        public static void PromptEntry(LockEntry entry, FrameworkElement origin)
        {
            if (OpenPrompts.TryGetValue(entry.Id, out var existing) && existing.IsLoaded) { existing.Activate(); return; }
            var win = new LockPromptWindow(entry, origin, null);
            OpenPrompts[entry.Id] = win;
            win.Closed += (s, e) => OpenPrompts.Remove(entry.Id);
            win.Show();
        }

        private static void WireLocks(Window w)
        {
            Locks.Start();
            Attached.RemoveAll(r => !r.TryGetTarget(out _));
            Attached.Add(new WeakReference<Window>(w));
            if (_wired) return;
            _wired = true;
            // A page that is built after a lock exists still shows its lock the moment its elements load.
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) =>
            {
                if (!Locks.Any || s is not FrameworkElement fe || !IsCandidate(fe)) return;
                var win = Window.GetWindow(fe);
                if (win != null && IsAttached(win)) Style(win, fe);
            }));
            // A locked button refuses to be clicked by any route, including a programmatic one.
            EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                if (!Locks.Any || s is not ButtonBase b) return;
                var win = Window.GetWindow(b);
                if (win != null && IsAttached(win) && LockedAncestor(win, b) != null) e.Handled = true;
            }));
            Locks.Changed += RefreshAll;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) => { if (Locks.Any) RefreshAll(); };
            timer.Start();
        }

        private static bool IsAttached(Window w) => Attached.Any(r => r.TryGetTarget(out var x) && x == w);

        private static void RefreshAll()
        {
            foreach (var r in Attached.ToList())
                if (r.TryGetTarget(out var w) && w.IsLoaded) Walk(w, w);
        }

        private static void Walk(Window w, DependencyObject d)
        {
            if (d is FrameworkElement fe && IsCandidate(fe)) Style(w, fe);
            int n = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < n; i++) Walk(w, VisualTreeHelper.GetChild(d, i));
        }

        /// <summary>Shows or clears the lock look on one element: a dimmed veil, a lock chip, and a spoken state.</summary>
        private static void Style(Window w, FrameworkElement fe)
        {
            var key = KeyOf(fe);
            bool locked = key != null && Locks.IsLocked(PageIdOf(w), key);
            if (Badges.TryGetValue(fe, out var badge))
            {
                if (!locked) { badge.Remove(); Badges.Remove(fe); AutomationProperties.SetHelpText(fe, ""); }
                return;
            }
            if (!locked) return;
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(fe);
            Badges.Add(fe, new LockBadge(fe, layer));
            AutomationProperties.SetHelpText(fe, "Locked. Activate it to unlock.");
        }

        /// <summary>A veil and a lock chip over a locked element. It does not take clicks, so the window's own handler opens the prompt.</summary>
        private sealed class LockBadge : System.Windows.Documents.Adorner
        {
            private readonly System.Windows.Documents.AdornerLayer _layer;
            public LockBadge(FrameworkElement el, System.Windows.Documents.AdornerLayer layer) : base(el)
            {
                _layer = layer; IsHitTestVisible = false;
                layer?.Add(this);
                el.ToolTip = "Locked. Click to unlock. Just for fun, not security.";
            }
            public void Remove() { _layer?.Remove(this); if (AdornedElement is FrameworkElement f && f.ToolTip is string t && t.StartsWith("Locked")) f.ToolTip = null; }
            protected override void OnRender(DrawingContext dc)
            {
                var size = AdornedElement.RenderSize;
                var veil = new SolidColorBrush(((SolidColorBrush)Ui.Res("B.Window")).Color) { Opacity = 0.55 };
                dc.DrawRoundedRectangle(veil, null, new Rect(size), 12, 12);
                double r = Math.Min(11, Math.Max(7, Math.Min(size.Width, size.Height) / 3));
                var c = new Point(Math.Max(r + 2, size.Width - r - 4), Math.Min(size.Height - r - 2, r + 4));
                dc.DrawEllipse(Ui.Res("B.Mint"), null, c, r, r);
                var pen = new Pen(Ui.Res("B.MintInk"), 1.4);
                dc.DrawRectangle(null, pen, new Rect(c.X - r * 0.42, c.Y - r * 0.05, r * 0.84, r * 0.6));
                var shackle = Geometry.Parse(string.Format(System.Globalization.CultureInfo.InvariantCulture, "M{0},{1} L{0},{2} A{3},{3} 0 0 1 {4},{2} L{4},{1}", c.X - r * 0.25, c.Y - r * 0.05, c.Y - r * 0.4, r * 0.25, c.X + r * 0.25));
                dc.DrawGeometry(null, pen, shackle);
            }
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
