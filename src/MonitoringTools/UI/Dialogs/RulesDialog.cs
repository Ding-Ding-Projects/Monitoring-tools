using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Per-app rules: what happens to copies made in a particular app, whether they toast, go to history only or are never
    /// kept, and one thing to do automatically. Ctrl+N adds a rule, Ctrl+P picks from the apps that are running.
    /// </summary>
    internal sealed class RulesDialog : ToolDialogWindow
    {
        internal static RulesDialog Current { get; private set; }
        private readonly StackPanel _rows = new StackPanel();
        private readonly StackPanel _picker = new StackPanel { Visibility = Visibility.Collapsed };
        private TextBlock _status;

        private static List<AppRule> All => Settings.Current.AppRules;

        private static readonly Choice[] ToastChoices = { new Choice("show", "Toast and history"), new Choice("quiet", "History only, no toast"), new Choice("never", "Never keep") };
        private static readonly Choice[] AutoChoices = { new Choice("none", "Nothing"), new Choice("strip", "Strip tracking from links"), new Choice("plain", "Keep as plain text"), new Choice("clear", "Clear after 30 s"), new Choice("table", "Open tables as a table") };

        public static RulesDialog Open()
        {
            if (Current == null || !Current.IsLoaded) { Current = new RulesDialog(); Current.Closed += (s, e) => Current = null; Current.Show(); }
            Current.Activate();
            return Current;
        }

        private RulesDialog() : base("Per-app rules")
        {
            var head = new Grid { Margin = new Thickness(14, 0, 14, 6) };
            Columns(head);
            AddHead(head, "App", 0); AddHead(head, "Toast and history", 1); AddHead(head, "Do automatically", 2);
            var body = new StackPanel();
            body.Children.Add(head);
            var rowsScroll = new ScrollViewer { Content = _rows, Height = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
            body.Children.Add(rowsScroll);
            body.Children.Add(_picker);
            body.Children.Add(DialogParts.Banner(DialogParts.Info, "Already built in: password managers that mark copies as private (KeePass, 1Password, Bitwarden and others) are never kept, whatever these rules say.", "info"));
            var pick = Ui.Button("Pick a running app", Pick, "Btn.Ghost", Icons.Search);
            var add = Ui.Button("Add a rule", () => AddRule(NativeInput.ProcessOf(NativeInput.GetForegroundWindow()) is string p && p != "MonitoringTools" ? p : "", ""), "Btn.Accent", Icons.Plus);
            var more = new List<(string, string, Action)>
            {
                ("Remove every rule", null, () => { var keep = All.ToList(); All.Clear(); Settings.Save(); Paint(); _status.Text = "Removed every rule. Press Ctrl+Z to bring them back."; _undo = keep; }),
            };
            body.Children.Add(SmartDialogParts.ActionBar(more, "Saved as you change it.", new[] { pick }, add, "Ctrl+N", out _status));
            ShowView("form", body, 900, "Per-app rules", "", Icons.Shield);
            Flex(rowsScroll, 140);
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.N && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; AddRule("", ""); }
                else if (e.Key == System.Windows.Input.Key.P && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Pick(); }
                else if (e.Key == System.Windows.Input.Key.Z && Keyboard.Modifiers == ModifierKeys.Control && _undo != null && !(Keyboard.FocusedElement is TextBox)) { e.Handled = true; All.AddRange(_undo); _undo = null; Settings.Save(); Paint(); _status.Text = "Brought the rules back."; }
            };
            Paint();
        }

        private List<AppRule> _undo;

        private static void Columns(Grid g)
        {
            // the two choices keep 220 and 240 px while there is room; on a narrow window all three share it
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 220 });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 240 });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        private static void AddHead(Grid g, string text, int col)
        {
            var t = new TextBlock { Text = text, FontFamily = Ui.Font("F.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextSoft") };
            Grid.SetColumn(t, col); g.Children.Add(t);
        }

        private void Paint()
        {
            SubText.Text = "What happens when you copy from a particular app · " + All.Count + (All.Count == 1 ? " rule" : " rules");
            _rows.Children.Clear();
            if (All.Count == 0) _rows.Children.Add(DialogParts.Hint("No rules yet. Add one for an app whose copies you never want kept, or want cleaned up automatically."));
            int n = 0;
            foreach (var rule in All)
            {
                var r = rule;
                var g = new Grid();
                Columns(g);
                var name = new TextBox { Style = Ui.Style("Input"), Text = r.App, Height = 40, FontFamily = Ui.Font("F.Mono"), FontSize = 13, Margin = new Thickness(0, 0, 12, 0) };
                AutomationProperties.SetName(name, "App name");
                name.ToolTip = "The program's name without .exe, for example msedge, WINWORD or KeePassXC";
                name.TextChanged += (s, e) => { r.App = name.Text.Trim().Replace(".exe", ""); Settings.Save(); };
                var toast = new Dropdown("Toast and history for " + r.App, ToastChoices, r.Toast) { Height = 40, Margin = new Thickness(0, 0, 12, 0) };
                toast.Changed += id => { r.Toast = id; Settings.Save(); _status.Text = (r.App.Length > 0 ? r.App : "This app") + ": " + ToastChoices.First(c => c.Id == id).Label.ToLowerInvariant() + ". Saved."; };
                var auto = new Dropdown("Automatic action for " + r.App, AutoChoices, r.Auto) { Height = 40, Margin = new Thickness(0, 0, 8, 0) };
                auto.Changed += id => { r.Auto = id; Settings.Save(); _status.Text = (r.App.Length > 0 ? r.App : "This app") + " rule saved."; };
                var del = Ui.Button("", () => { All.Remove(r); Settings.Save(); Paint(); _status.Text = "Removed the rule for " + r.App + "."; }, "Btn.Icon", Icons.Trash);
                AutomationProperties.SetName(del, "Remove the rule for " + r.App);
                Grid.SetColumn(toast, 1); Grid.SetColumn(auto, 2); Grid.SetColumn(del, 3);
                g.Children.Add(name); g.Children.Add(toast); g.Children.Add(auto); g.Children.Add(del);
                var row = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(18), Padding = new Thickness(12, 8, 8, 8), Margin = new Thickness(0, 0, 0, 8), Child = g };
                _rows.Children.Add(row);
                Ui.Rise(row, n++ * 40, 8, 300);
            }
        }

        private void AddRule(string app, string label)
        {
            if (app.Length > 0 && All.Any(r => string.Equals(r.App, app, StringComparison.OrdinalIgnoreCase))) { _status.Text = app + " already has a rule."; return; }
            All.Add(new AppRule { App = app, Label = label });
            Settings.Save(); Paint();
            _status.Text = app.Length > 0 ? "Added " + app + ". Choose what it should do." : "Type the app's name, then choose what it should do.";
        }

        private void Pick()
        {
            _picker.Children.Clear();
            if (_picker.Visibility == Visibility.Visible) { _picker.Visibility = Visibility.Collapsed; return; }
            _picker.Visibility = Visibility.Visible;
            _picker.Children.Add(DialogParts.Label("Apps with a window open now"));
            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            IEnumerable<(string Name, string Title)> apps;
            try { apps = Process.GetProcesses().Where(p => { try { return p.MainWindowHandle != IntPtr.Zero && p.ProcessName != "MonitoringTools"; } catch { return false; } }).Select(p => (p.ProcessName, p.MainWindowTitle)).GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(p => p.ProcessName).ToList(); }
            catch { apps = Enumerable.Empty<(string, string)>(); }
            foreach (var (name, title) in apps)
            {
                var n = name;
                var b = Ui.Button(name, () => { AddRule(n, title); _picker.Visibility = Visibility.Collapsed; }, "Btn.Chip");
                b.ToolTip = title; b.Margin = new Thickness(0, 0, 6, 6);
                wrap.Children.Add(b);
            }
            _picker.Children.Add(wrap);
        }
    }
}
