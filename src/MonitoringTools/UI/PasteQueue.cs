using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The paste queue: start it, copy several things, then each Ctrl+V pastes the next one in order (and can press Tab
    /// after, to move to the next field). A small window shows the queue; drag order with the up buttons, remove with ✕.
    /// </summary>
    public sealed class PasteQueue : Window
    {
        private static PasteQueue _win;
        private readonly List<string> _items = new List<string>();
        private int _next;
        private readonly StackPanel _list = new StackPanel();
        private readonly TextBlock _title = new TextBlock { FontSize = 16, FontWeight = FontWeights.Medium };
        private readonly Button _primary;
        private readonly CheckBox _tab;

        /// <summary>True once pasting has started, so each Ctrl+V moves the queue on.</summary>
        public static bool Pasting => _win != null && _win._pasting;
        internal static PasteQueue Current => _win;
        private bool _pasting;

        public static void Toggle()
        {
            if (_win != null) { _win.Close(); return; }
            _win = new PasteQueue();
            _win.Closed += (s, e) => { _win = null; ClipPower.UpdateHook(); };
            _win.Show();
        }

        /// <summary>While the queue is collecting, copies join it instead of popping a toast.</summary>
        public static bool Take(ClipItem item)
        {
            if (_win == null || _win._pasting || item.Text == null || item.Sensitive) return false;
            _win._items.Add(item.Text);
            _win.Paint();
            return true;
        }

        /// <summary>After each Ctrl+V the next item goes on the clipboard.</summary>
        public static void AfterPaste()
        {
            var w = _win;
            if (w == null || !w._pasting) return;
            w._next++;
            if (Settings.Current.QueueTabAfterPaste) NativeInput.Press(NativeInput.VK_TAB);
            if (w._next >= w._items.Count) { w._pasting = false; ClipPower.UpdateHook(); w.Paint(); App.Toasts.ShowNotice("Queue finished", "Everything in the queue was pasted.", App.Toasts.Mint, 3, null, Icons.Check); return; }
            ClipboardWatcher.SetText(w._items[w._next]);
            w.Paint();
        }

        private PasteQueue()
        {
            Title = "Paste queue"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ShowActivated = false; SizeToContent = SizeToContent.Height; Width = 380; ResizeMode = ResizeMode.NoResize;
            var area = SystemParameters.WorkArea; Left = area.Right - Width - 24; Top = area.Top + 80;
            MouseLeftButtonDown += (s, e) => { if (e.OriginalSource is Border || e.OriginalSource is TextBlock) try { DragMove(); } catch { } };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            _title.Foreground = Ui.Res("B.Text");

            var close = Ui.Button("", Close, "Btn.Icon", Icons.Close);
            AutomationProperties.SetName(close, "Close the paste queue (Esc)");
            var head = new DockPanel();
            DockPanel.SetDock(close, Dock.Right); head.Children.Add(close); head.Children.Add(_title);

            _tab = new CheckBox { Style = Ui.Style("Switch"), IsChecked = Settings.Current.QueueTabAfterPaste, Content = new TextBlock { Text = "After each paste press Tab", Foreground = Ui.Res("B.TextSoft"), FontSize = 13 }, Margin = new Thickness(0, 10, 0, 0) };
            _tab.Click += (s, e) => { Settings.Current.QueueTabAfterPaste = _tab.IsChecked == true; Settings.Save(); };
            AutomationProperties.SetName(_tab, "After each paste press Tab");

            _primary = Ui.Button("Start pasting", Start, "Btn.Accent", Icons.Play);
            var more = Dialogs.SmartDialogParts.MoreButton(new List<(string, string, Action)>
            {
                ("Reverse the order", null, () => { _items.Reverse(); Paint(); }),
                ("Clear the queue", null, () => { _items.Clear(); _next = 0; _pasting = false; ClipPower.UpdateHook(); Paint(); }),
                ("Copy everything as one note", null, () => ClipboardWatcher.SetText(string.Join(Environment.NewLine, _items))),
            });
            var bar = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(_primary, Dock.Right); bar.Children.Add(_primary); bar.Children.Add(more);
            more.HorizontalAlignment = HorizontalAlignment.Left;

            var stack = new StackPanel();
            stack.Children.Add(head);
            stack.Children.Add(new ScrollViewer { Content = _list, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 10, 0, 0), Focusable = false });
            stack.Children.Add(_tab);
            stack.Children.Add(new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 12, 0, 0), Child = bar });
            Content = new Border
            {
                Margin = new Thickness(12), Padding = new Thickness(16, 14, 16, 14), CornerRadius = new CornerRadius(24), Background = Ui.Res("B.Raised"),
                BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"], Child = stack,
            };
            Paint();
        }

        private void Start()
        {
            if (_items.Count == 0) return;
            if (_pasting) { _pasting = false; ClipPower.UpdateHook(); Paint(); return; }
            _next = 0; _pasting = true;
            ClipboardWatcher.SetText(_items[0]);
            ClipPower.UpdateHook();
            Paint();
        }

        private void Paint()
        {
            int left = _pasting ? _items.Count - _next : _items.Count;
            _title.Text = _pasting ? "Pasting · " + left + " left" : _items.Count == 0 ? "Paste queue · copy things to add them" : "Paste queue · " + _items.Count + " ready";
            _primary.Content = _pasting ? "Pause" : "Start pasting";
            _primary.IsEnabled = _items.Count > 0;
            _primary.ToolTip = _pasting ? "Each Ctrl+V pastes the next one" : "Puts the first one on the clipboard; then each Ctrl+V pastes the next";
            _list.Children.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                bool done = _pasting && i < _next, next = _pasting && i == _next;
                var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var n = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = next ? Ui.Res("B.Mint") : Ui.Res("B.Sunken"), Margin = new Thickness(0, 0, 10, 0), Child = new TextBlock { Text = (i + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Foreground = next ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft") } };
                var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var t = new TextBlock { Text = ClipPower.Short(_items[i]), FontSize = 14, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis, TextDecorations = done ? TextDecorations.Strikethrough : null, Opacity = done ? .5 : 1 };
                text.Children.Add(t);
                if (next) text.Children.Add(Ui.Mono("next · Ctrl+V", Ui.Res("B.Mint"), 10.5));
                var up = Ui.Button("↑", () => { if (idx == 0) return; (_items[idx - 1], _items[idx]) = (_items[idx], _items[idx - 1]); Paint(); }, "Btn.Ghost");
                var del = Ui.Button("✕", () => { _items.RemoveAt(idx); if (_next > idx) _next--; Paint(); }, "Btn.Ghost");
                AutomationProperties.SetName(up, "Move up: " + ClipPower.Short(_items[i])); AutomationProperties.SetName(del, "Remove: " + ClipPower.Short(_items[i]));
                Grid.SetColumn(text, 1); Grid.SetColumn(up, 2); Grid.SetColumn(del, 3);
                g.Children.Add(n); g.Children.Add(text); g.Children.Add(up); g.Children.Add(del);
                _list.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(14), Padding = new Thickness(10, 6, 6, 6), Child = g, BorderBrush = next ? Ui.Res("B.Mint") : null, BorderThickness = new Thickness(next ? 2 : 0), Margin = new Thickness(0, 0, 0, 6) });
            }
        }
    }
}
