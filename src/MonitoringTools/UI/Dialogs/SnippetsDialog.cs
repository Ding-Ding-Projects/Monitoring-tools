using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Snippets: a list on the left, the chosen snippet on the right with its name, trigger, optional shortcut and text,
    /// buttons that insert fill-ins, and a live preview of what gets typed. Saved as you type. Ctrl+N adds one.
    /// </summary>
    internal sealed class SnippetsDialog : ToolDialogWindow
    {
        internal static SnippetsDialog Current { get; private set; }
        private readonly StackPanel _list = new StackPanel();
        private readonly TextBox _name = Box(false), _trigger = Box(true), _hotkey = Box(true), _body = new TextBox { Style = Ui.Style("Input.Multi"), AcceptsReturn = true, Height = 150, FontFamily = Ui.Font("F.Mono"), FontSize = 13.5, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private readonly TextBlock _preview = new TextBlock { FontSize = 14, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap };
        private TextBlock _status;
        private Snippet _cur;
        private bool _loading;

        private static List<Snippet> All => Settings.Current.Snippets;

        public static SnippetsDialog Open()
        {
            if (Current == null || !Current.IsLoaded) { Current = new SnippetsDialog(); Current.Closed += (s, e) => { Current = null; ClipPower.RegisterAll(); ClipPower.UpdateHook(); }; Current.Show(); }
            Current.Activate();
            return Current;
        }

        private static TextBox Box(bool mono) { var t = new TextBox { Style = Ui.Style("Input"), Height = 44, FontSize = 14 }; if (mono) t.FontFamily = Ui.Font("F.Mono"); return t; }

        private SnippetsDialog() : base("Snippets")
        {
            AutomationProperties.SetName(_name, "Name"); AutomationProperties.SetName(_trigger, "Trigger"); AutomationProperties.SetName(_hotkey, "Shortcut, optional"); AutomationProperties.SetName(_body, "Text");
            _hotkey.ToolTip = "For example Ctrl+Alt+S. Leave empty for none.";
            foreach (var t in new[] { _name, _trigger, _hotkey, _body }) t.TextChanged += (s, e) => Changed();

            var grid = new Grid { Height = 520 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(22), Padding = new Thickness(8), Child = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false } });

            var ed = new StackPanel();
            var fields = new Grid();
            fields.ColumnDefinitions.Add(new ColumnDefinition()); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            fields.Children.Add(Field("Name", _name)); var tr = Field("Trigger", _trigger); Grid.SetColumn(tr, 2); fields.Children.Add(tr);
            var hk = Field("Shortcut (optional)", _hotkey); Grid.SetColumn(hk, 4); fields.Children.Add(hk);
            ed.Children.Add(fields);
            ed.Children.Add(DialogParts.Label("Text"));
            ed.Children.Add(_body);
            var fills = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            fills.Children.Add(new TextBlock { Text = "Insert", Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            foreach (var (code, tip) in new[] { ("{date}", "Today's date"), ("{time}", "The time now"), ("{clipboard}", "What you copied"), ("{clipboard:domain}", "Just the domain of a copied link"), ("{ask:Name}", "Asks you for a value"), ("{cursor}", "Where the cursor ends up") })
            {
                var c = code;
                var b = Ui.Button(code, () => { int at = _body.CaretIndex; _body.Text = _body.Text.Insert(at, c); _body.CaretIndex = at + c.Length; _body.Focus(); }, "Btn.Chip");
                b.ToolTip = tip; b.Margin = new Thickness(0, 0, 6, 6); AutomationProperties.SetName(b, "Insert " + tip);
                fills.Children.Add(b);
            }
            ed.Children.Add(fills);
            var pv = new StackPanel();
            pv.Children.Add(new TextBlock { Text = "PREVIEW · WHAT GETS TYPED", FontFamily = Ui.Font("F.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 0, 0, 6) });
            pv.Children.Add(_preview);
            ed.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 12, 16, 14), Margin = new Thickness(0, 10, 0, 0), Child = pv });
            ed.Children.Add(DialogParts.Hint("Start triggers with ; so normal typing never fires one. Typing is only compared with your triggers; nothing you type is kept."));
            var edScroll = new ScrollViewer { Content = ed, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
            Grid.SetColumn(edScroll, 2); grid.Children.Add(edScroll);

            var more = new List<(string, string, Action)>
            {
                ("Duplicate this snippet", "Ctrl+D", Duplicate),
                ("Delete this snippet", null, Delete),
            };
            var add = Ui.Button("New snippet", New, "Btn.Accent", Icons.Plus);
            var body = new StackPanel();
            body.Children.Add(grid);
            body.Children.Add(SmartDialogParts.ActionBar(more, "Saved as you type.", null, add, "Ctrl+N", out _status));
            ShowView("form", body, 960, "Snippets", "Type a trigger in any app and it becomes the text.", Icons.Code, Ui.Res("B.Mint"), Ui.Res("B.MintWash"));
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.N && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; New(); }
                else if (e.Key == System.Windows.Input.Key.D && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Duplicate(); }
            };
            Pick(All.FirstOrDefault());
        }

        private static FrameworkElement Field(string label, TextBox box) { var sp = new StackPanel(); sp.Children.Add(DialogParts.Label(label)); sp.Children.Add(box); return sp; }

        private void PaintList()
        {
            _list.Children.Clear();
            foreach (var sn in All)
            {
                var s = sn;
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(sn.Name) ? "(no name)" : sn.Name, FontSize = 14.5, Foreground = Ui.Res("B.Text"), FontWeight = FontWeights.Medium });
                sp.Children.Add(Ui.Mono(sn.Trigger + (string.IsNullOrEmpty(sn.Hotkey) ? "" : " · " + sn.Hotkey), Ui.Res("B.TextSoft"), 11.5));
                var b = new Button { Content = sp, Style = Ui.Style("Btn.Row"), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 0, 0, 2) };
                if (sn == _cur) b.Background = Ui.Res("B.MintWash");
                AutomationProperties.SetName(b, sn.Name + ", trigger " + sn.Trigger);
                b.Click += (o, e) => Pick(s);
                _list.Children.Add(b);
            }
        }

        private void Pick(Snippet s)
        {
            _cur = s; _loading = true;
            _name.Text = s?.Name ?? ""; _trigger.Text = s?.Trigger ?? ""; _hotkey.Text = s?.Hotkey ?? ""; _body.Text = s?.Body ?? "";
            foreach (var t in new[] { _name, _trigger, _hotkey, _body }) t.IsEnabled = s != null;
            _loading = false;
            PaintList(); Preview();
        }

        private void Changed()
        {
            if (_loading || _cur == null) return;
            _cur.Name = _name.Text; _cur.Trigger = _trigger.Text.Trim(); _cur.Hotkey = _hotkey.Text.Trim(); _cur.Body = _body.Text;
            Settings.Save();
            var clash = All.FirstOrDefault(x => x != _cur && x.Trigger.Length > 0 && x.Trigger == _cur.Trigger);
            _status.Text = _cur.Trigger.Length < 2 ? "Give it a trigger of at least two characters." : clash != null ? "\"" + clash.Name + "\" already uses " + clash.Trigger + "." : _cur.Hotkey.Length > 0 && !NativeInput.ParseShortcut(_cur.Hotkey, out _, out _) ? "The shortcut needs Ctrl, Alt or Win and a key." : "Saved as you type.";
            PaintList(); Preview();
        }

        private void Preview()
        {
            if (_cur == null) { _preview.Text = "Add a snippet to start."; return; }
            var e = SnippetMatcher.Fill(_cur.Body, DateTime.Now, ClipPower.CurrentText() ?? "https://example.com/report", _cur.Body.Contains("{ask:") ? new Dictionary<string, string>(Asks(_cur.Body).Select(a => new KeyValuePair<string, string>(a, "‹" + a + "›"))) : null);
            _preview.Text = e.CursorFromEnd > 0 ? e.Text.Insert(e.Text.Length - e.CursorFromEnd, "|") : e.Text;
        }

        private static IEnumerable<string> Asks(string body) => System.Text.RegularExpressions.Regex.Matches(body, @"\{ask:([^}]{1,40})\}").Select(m => m.Groups[1].Value).Distinct();

        private void New()
        {
            var s = new Snippet { Name = "New snippet", Trigger = ";new" + (All.Count + 1), Body = "" };
            All.Add(s); Settings.Save(); Pick(s); _name.Focus(); _name.SelectAll();
        }

        private void Duplicate()
        {
            if (_cur == null) return;
            var s = new Snippet { Name = _cur.Name + " copy", Trigger = _cur.Trigger + "2", Body = _cur.Body };
            All.Add(s); Settings.Save(); Pick(s);
        }

        private void Delete()
        {
            if (_cur == null) return;
            var gone = _cur; int i = All.IndexOf(gone);
            All.Remove(gone); Settings.Save();
            Pick(All.Count == 0 ? null : All[Math.Min(i, All.Count - 1)]);
            _status.Text = "Deleted \"" + gone.Name + "\". Press Ctrl+Z to bring it back.";
            PreviewKeyDown += Undo;
            void Undo(object o, KeyEventArgs e)
            {
                if (e.Key != System.Windows.Input.Key.Z || Keyboard.Modifiers != ModifierKeys.Control || Keyboard.FocusedElement is TextBox) return;
                PreviewKeyDown -= Undo; e.Handled = true;
                All.Insert(Math.Min(i, All.Count), gone); Settings.Save(); Pick(gone); _status.Text = "Brought back \"" + gone.Name + "\".";
            }
        }
    }
}
