using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Clip;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Transform recipes: chains of text changes run in one press. Steps can be moved up or removed, new ones added from the
    /// palette, and the result shows live on sample text (or what you copied). Ctrl+Enter runs the recipe on the clipboard.
    /// </summary>
    internal sealed class RecipesDialog : ToolDialogWindow
    {
        internal static RecipesDialog Current { get; private set; }
        private readonly StackPanel _list = new StackPanel(), _steps = new StackPanel();
        private readonly TextBox _name = new TextBox { Style = Ui.Style("Input"), Height = 44, FontSize = 15 };
        private readonly TextBox _sample = new TextBox { Style = Ui.Style("Input.Multi"), AcceptsReturn = true, Height = 90, FontFamily = Ui.Font("F.Mono"), FontSize = 13, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _result = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Mint"), TextWrapping = TextWrapping.Wrap };
        private readonly CheckBox _onToasts;
        private TextBlock _status;
        private Recipe _cur;
        private bool _loading;

        private static List<Recipe> All => Settings.Current.Recipes;

        public static RecipesDialog Open()
        {
            if (Current == null || !Current.IsLoaded) { Current = new RecipesDialog(); Current.Closed += (s, e) => { Current = null; ClipPower.RegisterAll(); }; Current.Show(); }
            Current.Activate();
            return Current;
        }

        private RecipesDialog() : base("Transform recipes")
        {
            AutomationProperties.SetName(_name, "Recipe name"); AutomationProperties.SetName(_sample, "Try it on");
            _sample.Text = ClipPower.CurrentText() is string t && t.Length < 2000 ? t : "  Night Shift Monitor — Café Édition!  ";
            _sample.TextChanged += (s, e) => Run();
            _name.TextChanged += (s, e) => { if (!_loading && _cur != null) { _cur.Name = _name.Text; Settings.Save(); PaintList(); } };

            var grid = new Grid { Height = 450 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(22), Padding = new Thickness(8), Child = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false } });

            var ed = new StackPanel();
            ed.Children.Add(DialogParts.Label("Name")); ed.Children.Add(_name);
            ed.Children.Add(DialogParts.Label("Steps, in order")); ed.Children.Add(_steps);
            var palette = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            palette.Children.Add(new TextBlock { Text = "Add a step", Foreground = Ui.Res("B.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            foreach (var tr in Transforms.All)
            {
                var id = tr.Id;
                var b = Ui.Button(tr.Label, () => { if (_cur == null) return; _cur.Steps.Add(id); Settings.Save(); PaintSteps(); Run(); }, "Btn.Chip");
                b.Margin = new Thickness(0, 0, 6, 6); AutomationProperties.SetName(b, "Add step: " + tr.Label);
                palette.Children.Add(b);
            }
            ed.Children.Add(palette);
            var tryGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            tryGrid.ColumnDefinitions.Add(new ColumnDefinition()); tryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); tryGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new StackPanel(); left.Children.Add(DialogParts.Label("Try it on")); left.Children.Add(_sample);
            var right = new StackPanel(); right.Children.Add(DialogParts.Label("Result"));
            right.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), MinHeight = 90, Child = _result });
            Grid.SetColumn(right, 2); tryGrid.Children.Add(left); tryGrid.Children.Add(right);
            ed.Children.Add(tryGrid);
            _onToasts = DialogParts.SwitchRow("Show on text toasts", "Adds this recipe as a button whenever you copy text.", false, out var tRow);
            _onToasts.Click += (s, e) => { if (_cur != null) { _cur.OnToasts = _onToasts.IsChecked == true; Settings.Save(); _status.Text = _cur.Name + (_cur.OnToasts ? " shows on text toasts." : " is off text toasts."); } };
            ed.Children.Add(tRow);
            var edScroll = new ScrollViewer { Content = ed, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
            Grid.SetColumn(edScroll, 2); grid.Children.Add(edScroll);

            var more = new List<(string, string, Action)>
            {
                ("New recipe", "Ctrl+N", New),
                ("Duplicate this recipe", null, () => { if (_cur == null) return; var r = new Recipe { Name = _cur.Name + " copy", Steps = _cur.Steps.ToList() }; All.Add(r); Settings.Save(); Pick(r); }),
                ("Delete this recipe", null, () => { if (_cur == null) return; All.Remove(_cur); Settings.Save(); Pick(All.FirstOrDefault()); }),
            };
            var newBtn = Ui.Button("New recipe", New, "Btn.Ghost", Icons.Plus);
            var runBtn = Ui.Button("Run on the clipboard", OnPrimary, "Btn.Accent", Icons.Wand);
            var body = new StackPanel();
            body.Children.Add(grid);
            body.Children.Add(SmartDialogParts.ActionBar(more, "Saved as you change it.", new[] { newBtn }, runBtn, "Ctrl+Enter", out _status));
            ShowView("form", body, 960, "Transform recipes", "Chains of text changes you can run in one press.", Icons.Wand, Ui.Res("B.Violet"), Ui.Res("B.VioletWash"));
            PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.N && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; New(); } };
            Pick(All.FirstOrDefault());
        }

        protected override void OnPrimary()
        {
            if (_cur == null) return;
            var t = ClipPower.CurrentText();
            if (t == null) { _status.Text = "The clipboard has no text to change."; return; }
            ClipboardWatcher.SetText(Transforms.Apply(_cur, t));
            _status.Text = "Ran \"" + _cur.Name + "\" on the clipboard. Paste it anywhere.";
        }

        private void New()
        {
            var r = new Recipe { Name = "New recipe", Steps = new List<string> { "trim" } };
            All.Add(r); Settings.Save(); Pick(r); _name.Focus(); _name.SelectAll();
        }

        private void Pick(Recipe r)
        {
            _cur = r; _loading = true;
            _name.Text = r?.Name ?? ""; _name.IsEnabled = r != null;
            _onToasts.IsChecked = r?.OnToasts == true;
            _loading = false;
            PaintList(); PaintSteps(); Run();
        }

        private void PaintList()
        {
            _list.Children.Clear();
            foreach (var rec in All)
            {
                var r = rec;
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = r.Name, FontSize = 14.5, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text") });
                sp.Children.Add(Ui.Mono(r.Steps.Count + (r.Steps.Count == 1 ? " step" : " steps") + (r.OnToasts ? " · on toasts" : "") + (string.IsNullOrEmpty(r.Hotkey) ? "" : " · " + r.Hotkey), Ui.Res("B.TextSoft"), 11));
                var b = new Button { Content = sp, Style = Ui.Style("Btn.Row"), Height = double.NaN, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 0, 0, 2) };
                if (r == _cur) b.Background = Ui.Res("B.MintWash");
                b.Click += (o, e) => Pick(r);
                _list.Children.Add(b);
            }
        }

        private void PaintSteps()
        {
            _steps.Children.Clear();
            if (_cur == null) return;
            for (int i = 0; i < _cur.Steps.Count; i++)
            {
                int idx = i;
                var label = Transforms.Find(_cur.Steps[i]).Label ?? _cur.Steps[i];
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var n = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.Mint"), Margin = new Thickness(0, 0, 12, 0), Child = new TextBlock { Text = (i + 1).ToString(), FontSize = 11, Foreground = Ui.Res("B.MintInk"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                var t = new TextBlock { Text = label, FontSize = 14.5, Foreground = Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Center };
                var up = Ui.Button("↑", () => { if (idx == 0) return; (_cur.Steps[idx - 1], _cur.Steps[idx]) = (_cur.Steps[idx], _cur.Steps[idx - 1]); Settings.Save(); PaintSteps(); Run(); }, "Btn.Ghost");
                var del = Ui.Button("✕", () => { _cur.Steps.RemoveAt(idx); Settings.Save(); PaintSteps(); Run(); }, "Btn.Ghost");
                AutomationProperties.SetName(up, "Move " + label + " up"); AutomationProperties.SetName(del, "Remove " + label);
                Grid.SetColumn(t, 1); Grid.SetColumn(up, 2); Grid.SetColumn(del, 3);
                g.Children.Add(n); g.Children.Add(t); g.Children.Add(up); g.Children.Add(del);
                var row = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 4, 4, 4), Margin = new Thickness(0, 0, 0, 6), Child = g };
                _steps.Children.Add(row);
                Ui.Rise(row, i * 25, 6, 260);
            }
            PaintList();
        }

        private void Run() => _result.Text = _cur == null ? "" : Transforms.Apply(_cur, _sample.Text) is var r && r.Length > 0 ? r : "(empty)";
    }
}
