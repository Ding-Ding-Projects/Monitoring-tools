using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MonitoringTools.Core;
using static MonitoringTools.UI.Tabs.TabText;

namespace MonitoringTools.UI.Tabs
{
    /// <summary>The panel that holds the tabs. It reports itself to assistive technology as a tab list with the strip's real orientation.</summary>
    internal sealed class TabListPanel : StackPanel
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

        private sealed class Peer : FrameworkElementAutomationPeer
        {
            public Peer(TabListPanel owner) : base(owner) { }
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
            protected override string GetClassNameCore() => "TabList";
            protected override string GetNameCore() => T("Open tabs");
            protected override AutomationOrientation GetOrientationCore() => ((TabListPanel)Owner).Orientation == Orientation.Vertical ? AutomationOrientation.Vertical : AutomationOrientation.Horizontal;
        }
    }

    /// <summary>One browser-style tab: icon, label, pin or close glyph, state layers, drag, keyboard and a tab automation peer.</summary>
    internal sealed class StripTab : Border
    {
        private readonly TabStrip _o;
        public readonly string TabId;
        public bool IsActive { get; }
        /// <summary>The label text, or null in the narrow icon-only strip. Saved looks are applied to it.</summary>
        public TextBlock LabelBlock { get; private set; }
        private readonly bool _vertical;
        private readonly Border _hi, _ring;
        private Point _down;
        private bool _pressed;

        public StripTab(TabStrip owner, TabInfo info, bool vertical, bool compact, string accentKey, int position, int size)
        {
            _o = owner; TabId = info.Id; IsActive = info.Active; _vertical = vertical;
            Height = Appearance.Row(40); CornerRadius = new CornerRadius(20);
            System.Windows.Documents.TextElement.SetFontSize(this, 14); System.Windows.Documents.TextElement.SetFontFamily(this, Ui.Font("F.Body"));
            Focusable = true; KeyboardNavigation.SetIsTabStop(this, false); FocusVisualStyle = null; Cursor = Cursors.Hand; AllowDrop = true;
            Margin = vertical ? new Thickness(0, 0, 0, Appearance.Gap(2)) : new Thickness(0, 0, 4, 0);
            if (compact) { Width = 44; HorizontalAlignment = HorizontalAlignment.Center; }
            else if (!vertical) { MinWidth = 120; MaxWidth = 230; }
            if (info.Active) SetResourceReference(BackgroundProperty, "B.SecondaryContainer"); else Background = Brushes.Transparent;
            var fg = info.Active ? "B.OnSecondaryContainer" : "B.TextSoft";

            var grid = new Grid();
            _hi = new Border { CornerRadius = new CornerRadius(20), Opacity = 0, IsHitTestVisible = false };
            _hi.SetResourceReference(BackgroundProperty, "B.Text");
            _ring = new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(2), Opacity = 0, IsHitTestVisible = false };
            _ring.SetResourceReference(BorderBrushProperty, "B.Mint");
            grid.Children.Add(_hi);
            grid.Children.Add(_ring);
            if (accentKey != null)
            {
                var bar = new Border { Width = 3, Height = 18, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0), IsHitTestVisible = false };
                bar.SetResourceReference(BackgroundProperty, accentKey);
                grid.Children.Add(bar);
            }

            var row = new Grid { Margin = new Thickness(compact ? 0 : 16, 0, compact ? 0 : 6, 0), IsHitTestVisible = true };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = Ui.Icon(info.Icon, Ui.Res("B.TextSoft"), 16);
            icon.SetResourceReference(Shape.StrokeProperty, fg);
            icon.IsHitTestVisible = false;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            icon.Margin = new Thickness(0, 0, compact ? 0 : 12, 0);
            row.Children.Add(icon);
            if (compact) row.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            if (!compact)
            {
                var label = new TextBlock { Text = info.Label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 14, FontFamily = Ui.Font("F.Body"), FontWeight = info.Active ? FontWeights.SemiBold : FontWeights.Medium, IsHitTestVisible = false };
                label.SetResourceReference(TextBlock.ForegroundProperty, fg);
                LabelBlock = label;
                Grid.SetColumn(label, 1);
                row.Children.Add(label);
                FrameworkElement trail;
                if (info.Pinned)
                {
                    var pin = Ui.Icon(TabStrip.PinIcon, Ui.Res("B.TextMute"), 13, 1.8);
                    pin.SetResourceReference(Shape.StrokeProperty, "B.TextMute");
                    pin.Margin = new Thickness(6, 0, 8, 0);
                    pin.IsHitTestVisible = false;
                    pin.VerticalAlignment = VerticalAlignment.Center;
                    trail = pin;
                }
                else
                {
                    var x = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = Brushes.Transparent, Opacity = 0.65, Cursor = Cursors.Arrow, ToolTip = T("Close tab") + " (Ctrl+W)", VerticalAlignment = VerticalAlignment.Center };
                    var glyph = Ui.Icon(Icons.Close, Ui.Res("B.TextSoft"), 11, 1.8);
                    glyph.SetResourceReference(Shape.StrokeProperty, fg);
                    x.Child = glyph;
                    x.MouseEnter += (s, e) => { x.SetResourceReference(BackgroundProperty, "B.Line"); x.Opacity = 1; };
                    x.MouseLeave += (s, e) => { x.Background = Brushes.Transparent; x.Opacity = 0.65; };
                    x.MouseLeftButtonDown += (s, e) => e.Handled = true;
                    x.MouseLeftButtonUp += (s, e) => { e.Handled = true; _o.CloseTab(TabId); };
                    x.MouseRightButtonUp += (s, e) => { e.Handled = true; _o.OpenTabMenuMouse(this, e.GetPosition(Window.GetWindow(this))); };
                    AutomationProperties.SetName(x, T("Close tab") + " " + info.Label);
                    trail = x;
                }
                Grid.SetColumn(trail, 2);
                row.Children.Add(trail);
            }
            grid.Children.Add(row);
            Child = grid;

            ToolTip = info.Label + (info.Pinned ? " " + T("(pinned)") : "") + (info.GroupName != null ? " · " + T("group") + " " + info.GroupName : "");
            AutomationProperties.SetName(this, info.Label + (info.Pinned ? ", " + T("pinned") : "") + (info.GroupName != null ? ", " + T("group") + " " + info.GroupName : ""));
            AutomationProperties.SetItemStatus(this, T(info.Active ? "selected" : "not selected"));
            AutomationProperties.SetPositionInSet(this, position);
            AutomationProperties.SetSizeOfSet(this, size);
            AutomationProperties.SetHelpText(this, T("Enter opens. Delete or Ctrl+W closes. Ctrl+Shift+P pins. Ctrl+Shift+Up or Down reorders. Applications key opens the menu. Shift plus right-click edits the tab appearance."));

            MouseEnter += (s, e) => _hi.Opacity = 0.08;
            MouseLeave += (s, e) => { _hi.Opacity = 0; _pressed = false; };
            GotKeyboardFocus += (s, e) => _ring.Opacity = 1;
            LostKeyboardFocus += (s, e) => _ring.Opacity = 0;
            MouseLeftButtonDown += (s, e) => { _pressed = true; _down = e.GetPosition(this); _hi.Opacity = 0.12; Focus(); };
            MouseLeftButtonUp += (s, e) => { _hi.Opacity = 0.08; if (_pressed) { _pressed = false; _o.Activate(TabId); } };
            MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Middle) { e.Handled = true; _o.CloseTab(TabId); } };
            MouseRightButtonUp += (s, e) => { e.Handled = true; _o.OpenTabMenuMouse(this, e.GetPosition(Window.GetWindow(this))); };
            MouseMove += (s, e) =>
            {
                if (!_pressed || e.LeftButton != MouseButtonState.Pressed) return;
                var p = e.GetPosition(this);
                if (Math.Abs(p.X - _down.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - _down.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                _pressed = false;
                DragDrop.DoDragDrop(this, new DataObject(TabStrip.DragFormat, TabId), DragDropEffects.Move);
            };
            DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(TabStrip.DragFormat) && _o.CanDrop((string)e.Data.GetData(TabStrip.DragFormat), TabId) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            Drop += (s, e) => { if (e.Data.GetDataPresent(TabStrip.DragFormat)) _o.DropOn((string)e.Data.GetData(TabStrip.DragFormat), TabId); e.Handled = true; };
            KeyDown += OnKey;
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            var k = e.Key == Key.System ? e.SystemKey : e.Key;
            var next = _vertical ? Key.Down : Key.Right;
            var prev = _vertical ? Key.Up : Key.Left;
            if (Keyboard.Modifiers != ModifierKeys.None && !(Keyboard.Modifiers == ModifierKeys.Shift && k == Key.F10)) return;
            if (k == Key.Enter || k == Key.Space) _o.Activate(TabId);
            else if (k == Key.Delete) _o.CloseTab(TabId);
            else if (k == next) _o.FocusNeighbour(TabId, 1);
            else if (k == prev) _o.FocusNeighbour(TabId, -1);
            else if (k == Key.Home) _o.FocusEdge(false);
            else if (k == Key.End) _o.FocusEdge(true);
            else if (k == Key.Apps || k == Key.F10) _o.OpenTabMenu(this, TranslatePoint(new Point(16, ActualHeight / 2), Window.GetWindow(this)));
            else return;
            e.Handled = true;
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

        private sealed class Peer : FrameworkElementAutomationPeer, ISelectionItemProvider
        {
            public Peer(StripTab owner) : base(owner) { }
            private StripTab Tab => (StripTab)Owner;
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
            protected override string GetClassNameCore() => "Tab";
            protected override bool IsKeyboardFocusableCore() => true;
            public override object GetPattern(PatternInterface pattern) => pattern == PatternInterface.SelectionItem ? this : base.GetPattern(pattern);
            public bool IsSelected => Tab.IsActive;
            public IRawElementProviderSimple SelectionContainer => null;
            public void Select() => Tab._o.Activate(Tab.TabId);
            public void AddToSelection() => Select();
            public void RemoveFromSelection() { }
        }
    }

    /// <summary>The header of a tab group: colour, name, member count and a collapse chevron. Collapsing never touches the tabs themselves.</summary>
    internal sealed class GroupHeader : Border
    {
        private readonly TabStrip _o;
        public readonly string GroupId;
        private readonly Border _hi, _ring;
        /// <summary>The group name text, or null in the narrow icon-only strip. Saved looks are applied to it.</summary>
        public TextBlock NameBlock { get; private set; }

        public GroupHeader(TabStrip owner, TabGroupInfo g, bool vertical, bool compact)
        {
            _o = owner; GroupId = g.Id;
            Height = Appearance.Row(32); CornerRadius = new CornerRadius(10);
            Focusable = true; KeyboardNavigation.SetIsTabStop(this, false); FocusVisualStyle = null; Cursor = Cursors.Hand; AllowDrop = true; Background = Brushes.Transparent;
            Margin = vertical ? new Thickness(0, Appearance.Gap(6), 0, Appearance.Gap(2)) : new Thickness(6, 0, 4, 0);
            if (compact) { Width = 44; HorizontalAlignment = HorizontalAlignment.Center; }
            var grid = new Grid();
            _hi = new Border { CornerRadius = new CornerRadius(10), Opacity = 0, IsHitTestVisible = false };
            _hi.SetResourceReference(BackgroundProperty, "B.Text");
            _ring = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2), Opacity = 0, IsHitTestVisible = false };
            _ring.SetResourceReference(BorderBrushProperty, "B.Mint");
            grid.Children.Add(_hi);
            grid.Children.Add(_ring);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(compact ? 0 : 10, 0, 8, 0), HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            var chevron = Ui.Icon(g.Collapsed ? "M9 6l6 6-6 6" : "M6 9l6 6 6-6", Ui.Res("B.TextMute"), 11, 2);
            chevron.SetResourceReference(Shape.StrokeProperty, "B.TextMute");
            chevron.Margin = new Thickness(0, 0, 8, 0);
            chevron.VerticalAlignment = VerticalAlignment.Center;
            var dot = new Ellipse { Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center };
            dot.SetResourceReference(Shape.FillProperty, g.Colour);
            row.Children.Add(chevron);
            row.Children.Add(dot);
            if (!compact)
            {
                var name = new TextBlock { Text = g.Name, Margin = new Thickness(8, 0, 0, 0), FontSize = 12, FontWeight = FontWeights.SemiBold, FontFamily = Ui.Font("F.Body"), MaxWidth = vertical ? 120 : 160, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                name.SetResourceReference(TextBlock.ForegroundProperty, "B.TextSoft");
                NameBlock = name;
                var count = new TextBlock { Text = g.Count.ToString(), Margin = new Thickness(6, 0, 0, 0), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                count.SetResourceReference(TextBlock.ForegroundProperty, "B.TextMute");
                row.Children.Add(name);
                row.Children.Add(count);
            }
            grid.Children.Add(row);
            Child = grid;
            ToolTip = g.Name + " · " + TabCount(g.Count) + (g.Collapsed ? " · " + T("collapsed") : "");
            AutomationProperties.SetName(this, T("Group") + " " + g.Name + ", " + TabCount(g.Count) + ", " + T(g.Collapsed ? "collapsed" : "expanded"));
            AutomationProperties.SetHelpText(this, T("Enter or Space collapses or expands. F2 renames. Applications key opens the group menu. Shift plus right-click edits the group appearance."));

            MouseEnter += (s, e) => _hi.Opacity = 0.08;
            MouseLeave += (s, e) => _hi.Opacity = 0;
            GotKeyboardFocus += (s, e) => _ring.Opacity = 1;
            LostKeyboardFocus += (s, e) => _ring.Opacity = 0;
            MouseLeftButtonUp += (s, e) => { Focus(); _o.ToggleCollapse(GroupId); e.Handled = true; };
            MouseRightButtonUp += (s, e) => { e.Handled = true; _o.OpenGroupMenuMouse(this, e.GetPosition(Window.GetWindow(this))); };
            DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(TabStrip.DragFormat) && _o.CanDropOnGroup((string)e.Data.GetData(TabStrip.DragFormat)) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            Drop += (s, e) => { if (e.Data.GetDataPresent(TabStrip.DragFormat)) _o.DropOnGroup((string)e.Data.GetData(TabStrip.DragFormat), GroupId); e.Handled = true; };
            var next = vertical ? Key.Down : Key.Right;
            var prev = vertical ? Key.Up : Key.Left;
            KeyDown += (s, e) =>
            {
                var k = e.Key == Key.System ? e.SystemKey : e.Key;
                if (Keyboard.Modifiers != ModifierKeys.None && !(Keyboard.Modifiers == ModifierKeys.Shift && k == Key.F10)) return;
                if (k == Key.Enter || k == Key.Space) _o.ToggleCollapse(GroupId);
                else if (k == Key.F2) _o.RenameGroup(GroupId);
                else if (k == next) _o.FocusNeighbourOf(this, 1);
                else if (k == prev) _o.FocusNeighbourOf(this, -1);
                else if (k == Key.Home) _o.FocusEdge(false);
                else if (k == Key.End) _o.FocusEdge(true);
                else if (k == Key.Apps || k == Key.F10) _o.OpenGroupMenu(this, TranslatePoint(new Point(16, ActualHeight / 2), Window.GetWindow(this)));
                else return;
                e.Handled = true;
            };
        }
    }

    internal sealed record TabGroupInfo(string Id, string Name, string Colour, bool Collapsed, int Count);
}
