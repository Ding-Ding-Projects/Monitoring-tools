using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The copied-text toast with an editable body (design board 1). The toast never takes focus on its own: the first click
    /// in the text makes it activatable and puts the caret there. While it has focus the countdown is held. What is typed is
    /// written to the clipboard 400 ms after the last key (through <see cref="ClipboardWatcher.SetText"/>, which makes the
    /// watcher ignore the app's own write, so no second toast opens). The history entry is updated in place. Esc in the text
    /// puts the original text back, Ctrl+Enter keeps the edit.
    /// </summary>
    internal sealed class TextToastEditor
    {
        public const int DebounceMilliseconds = 400;

        private readonly ToastWindow _w;
        private readonly ClipItem _item;
        private readonly string _original;
        private readonly TextBox _box;
        private readonly Border _countRow;
        private readonly TextBlock _count;
        private readonly Border _copied;
        private readonly DispatcherTimer _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds) };
        private List<ToastAction> _idle = new List<ToastAction>();
        private bool _editing;
        private bool _dirty;
        private string _lastWritten;

        public bool Editing => _editing;
        public TextBox Box => _box;
        /// <summary>How many times the clipboard was written by this toast. For the self-test.</summary>
        public int Writes { get; private set; }

        public static string Counts(string t)
        {
            t ??= "";
            int lines = t.Length == 0 ? 0 : t.Split('\n').Length;
            return $"{t.Length:N0} chars · {lines:N0} {(lines == 1 ? "line" : "lines")}";
        }

        public TextToastEditor(ToastWindow w, ClipItem item)
        {
            _w = w; _item = item; _original = item.OriginalText ?? item.Text ?? "";
            _box = new TextBox
            {
                Style = Ui.Style("Input.Multi"), Text = item.Text ?? "", IsReadOnly = true, MaxHeight = 180, MinHeight = 48,
                Cursor = Cursors.IBeam, Margin = new Thickness(0)
            };
            TextBlock.SetLineHeight(_box, 20);
            TextBlock.SetLineStackingStrategy(_box, LineStackingStrategy.BlockLineHeight);
            Core.Language.SetLiteral(_box, true);   // what was copied is the person's own text
            AutomationProperties.SetName(_box, "Copied text, editable");
            _box.PreviewMouseLeftButtonDown += (s, e) => { if (!_editing) Begin(fromClick: true); };
            _box.TextChanged += OnTextChanged;

            _count = Ui.Mono("", Ui.Res("B.TextSoft"), 12);
            _copied = new Border
            {
                Background = Ui.Res("B.MintWash"), CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 3, 10, 3), HorizontalAlignment = HorizontalAlignment.Right,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new ContentControl { Content = Ui.Icon(Icons.Check, Ui.Res("B.Mint"), 12, 2.6), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = "Copied", FontSize = 12, Foreground = Ui.Res("B.Mint"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            };
            AutomationProperties.SetLiveSetting(_copied, AutomationLiveSetting.Polite);
            AutomationProperties.SetName(_copied, "Copied");
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(_count);
            Grid.SetColumn(_copied, 1);
            grid.Children.Add(_copied);
            _countRow = new Border { Child = grid, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };

            _debounce.Tick += (s, e) => { _debounce.Stop(); Write(_box.Text); };
            _w.KeyHook = OnKey;
            // Whatever closes the toast, a pending edit is written first so the clipboard never lags behind the text.
            _w.Closed += (s, e) => { if (_debounce.IsEnabled) { _debounce.Stop(); Write(_box.Text); } };
        }

        public UIElement Build() => new StackPanel { Children = { _box, _countRow } };

        /// <summary>The header and hint for the idle state, so the toast and the editor agree on one wording.</summary>
        public void ApplyIdleHeader()
        {
            _w.MetaText.Text = Counts(_item.Text);
            Core.Language.Refresh(_w.MetaText);
            var lines = (_item.Text ?? "").Split('\n').Length;
            SetHint(lines > 8 ? $"Scroll to read, click to edit. Showing 8 of {lines} lines." : "Click the text to edit it. The clipboard follows your edits.");
        }

        /// <summary>Puts "Edit text" first and primary, and demotes the other text actions so one button leads.</summary>
        public void Decorate(List<ToastAction> actions)
        {
            foreach (var a in actions) a.Primary = false;
            actions.Insert(0, new ToastAction
            {
                Label = "Edit text", Icon = ToastIcons.Edit, Primary = true, Stay = true,
                Tip = "Edit the copied text here. The clipboard follows your edits.",
                Run = () => Begin(fromClick: false),
            });
            _idle = actions;
        }

        private void SetHint(string hint)
        {
            _w.SetHint(hint);
            Core.Language.Refresh(_w.HintText);
        }

        // -------------------------------------------------------------- editing

        public void Begin(bool fromClick)
        {
            if (_editing) return;
            _editing = true;
            _box.IsReadOnly = false;
            _w.TakeFocusHeld();
            _w.MetaText.Text = "edited";
            Core.Language.Refresh(_w.MetaText);
            _count.Text = Counts(_box.Text);
            _copied.Opacity = 1;
            _countRow.Visibility = Visibility.Visible;
            SetHint("The clipboard already holds your edit.");
            _w.SetActions(new List<ToastAction>
            {
                new ToastAction { Label = "Keep", Icon = Icons.Check, Primary = true, HandsOff = true, KeyHint = "Ctrl+Enter", Tip = "Keep the edited text on the clipboard and close.", Run = Keep },
                new ToastAction { Label = "Revert", Icon = ToastIcons.Undo, Stay = true, KeyHint = "Esc", Tip = "Put the original text back on the clipboard.", Run = Revert },
            });
            _w.ExpandNow();
            Core.Language.Refresh(_w);
            FocusManager.SetFocusedElement(_w, _box);
            _box.Focus();
            Keyboard.Focus(_box);
            if (!fromClick) _box.CaretIndex = _box.Text.Length;   // a click places the caret where it landed
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_editing) return;
            _dirty = true;
            _count.Text = Counts(_box.Text);
            _copied.Opacity = 0.4;            // dims while typing, returns when the clipboard write is done
            _debounce.Stop();
            _debounce.Start();
        }

        private void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) { _copied.Opacity = 0.4; return; }   // nothing to put on the clipboard
            if (text != _lastWritten)
            {
                try { ClipboardWatcher.SetText(text); Writes++; _lastWritten = text; }
                catch (Exception ex) { Log.Warn("Edited text not copied: " + ex.Message); return; }
            }
            // One entry, updated in place and marked edited; never a second entry for the same copy.
            _item.OriginalText ??= _original;
            _item.Text = text;
            _item.Edited = text != _original;
            _item.Signature = "t:" + text.GetHashCode();
            App.History?.Touch(_item);
            _copied.Opacity = 1;
            try { FrameworkElementAutomationPeer.FromElement(_copied)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged); } catch { }
        }

        /// <summary>Ctrl+Enter or the Keep button: the edit stays on the clipboard and the toast closes.</summary>
        public void Keep()
        {
            if (_debounce.IsEnabled) { _debounce.Stop(); Write(_box.Text); }
            _w.CloseAnimated();
        }

        /// <summary>Esc in the text or the Revert button: the original text goes back to the clipboard and the history entry.</summary>
        public void Revert()
        {
            if (!_editing) return;
            _debounce.Stop();
            _editing = false;                  // the text assignment below is not an edit
            _box.Text = _original;
            _box.IsReadOnly = true;
            if (_dirty || _item.Edited || _lastWritten != null)
            {
                try { ClipboardWatcher.SetText(_original); Writes++; _lastWritten = null; } catch (Exception ex) { Log.Warn("Original text not restored: " + ex.Message); }
            }
            _item.Text = _original; _item.OriginalText = null; _item.Edited = false;
            _item.Signature = "t:" + _original.GetHashCode();
            App.History?.Touch(_item);
            _dirty = false;
            _countRow.Visibility = Visibility.Collapsed;
            _w.MetaText.Text = Counts(_original);
            Core.Language.Refresh(_w.MetaText);
            SetHint("Reverted. The clipboard holds the original text again.");
            _w.SetActions(_idle);
            _w.ExpandNow();
            Core.Language.Refresh(_w);
            _w.ReleaseFocusHeld(Settings.Current.ToastSeconds);   // the timer resumes
        }

        // -------------------------------------------------------------- keys

        private bool OnKey(KeyEventArgs e)
        {
            if (!_editing) return false;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var mods = ModifiersForTest ?? Keyboard.Modifiers;
            if (key == Key.Escape && ReferenceEquals(FocusedNow(), _box)) { Revert(); return true; }
            if ((key == Key.Enter || key == Key.Return) && (mods & ModifierKeys.Control) != 0) { Keep(); return true; }
            if (key == Key.Tab) return Tab((mods & ModifierKeys.Shift) != 0);
            return false;
        }

        /// <summary>Self-test seam: pretend these modifier keys are held.</summary>
        internal static ModifierKeys? ModifiersForTest;

        // The element with focus in this toast: the keyboard focus when the toast is active, otherwise its logical focus.
        private IInputElement FocusedNow() => Keyboard.FocusedElement is FrameworkElement k && ReferenceEquals(Window.GetWindow(k), _w) ? k : FocusManager.GetFocusedElement(_w);

        // Tab order while editing: the text, Keep, Revert, Close, then back to the text.
        private bool Tab(bool back)
        {
            var order = new List<FrameworkElement> { _box };
            order.AddRange(_w.ActionButtons);
            order.Add(_w.CloseButton);
            var now = FocusedNow();
            int at = order.FindIndex(x => ReferenceEquals(x, now) || x.IsKeyboardFocusWithin);
            if (at < 0) return false;
            var next = order[(at + (back ? order.Count - 1 : 1)) % order.Count];
            FocusManager.SetFocusedElement(_w, next);
            next.Focus();
            return true;
        }
    }
}
