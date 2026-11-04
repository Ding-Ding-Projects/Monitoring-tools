using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI.Minecraft
{
    /// <summary>
    /// A structured editor for an NBT compound: add a key, choose its type (byte, short, int, long, float, double, string,
    /// list or compound), type its value, nest compounds and lists, and read the SNBT it writes. A value that uses a form the
    /// tree does not build (typed arrays) is left as a text box instead of being damaged.
    /// </summary>
    internal sealed class McNbtEditor : StackPanel
    {
        private readonly Action<string> _changed;
        private readonly StackPanel _tree = new StackPanel();
        private readonly TextBlock _preview, _problem;
        private TextBox _raw;
        public SnbtNode Root { get; private set; } = new SnbtNode { Type = SnbtType.Compound };

        /// <summary>The SNBT the editor currently writes, or empty when a value is not valid yet.</summary>
        public string Snbt => Problem == null && Root.Items.Count > 0 ? McSnbt.Render(Root) : "";
        public string Problem { get; private set; }

        public McNbtEditor(string name, string initial, Action<string> changed)
        {
            _changed = changed;
            AutomationProperties.SetName(this, name);
            bool text = false;
            if (!string.IsNullOrWhiteSpace(initial))
            {
                var parsed = McSnbt.Parse(initial);
                if (parsed != null && parsed.Type == SnbtType.Compound) Root = parsed; else text = true;
            }
            _preview = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 0, 0) };
            Core.Language.SetLiteral(_preview, true);
            _problem = new TextBlock { FontSize = 12, Foreground = Ui.Res("B.Danger"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 0, 0), Visibility = Visibility.Collapsed };
            AutomationProperties.SetLiveSetting(_problem, AutomationLiveSetting.Polite);
            if (text) ShowRaw(initial); else { Children.Add(_tree); Children.Add(_problem); Children.Add(_preview); Rebuild(); Report(); }
        }

        private void ShowRaw(string initial)
        {
            Children.Clear();
            Children.Add(McUi.Body("This value uses a form the tree editor does not build (for example a typed array), so it stays as text. Edit it here, or start again with the tree.", "B.TextMute", 12));
            _raw = McCommandControls.Text(initial, "NBT as text", 420, "NBT in curly braces");
            _raw.TextChanged += (o, e) => _changed(_raw.Text.Trim());
            Children.Add(_raw);
            var fresh = Ui.Button("Start again with the tree", () =>
            {
                Children.Clear(); Root = new SnbtNode { Type = SnbtType.Compound };
                Children.Add(_tree); Children.Add(_problem); Children.Add(_preview);
                Rebuild(); Report();
            }, "Btn.Ghost", Icons.Restart);
            fresh.HorizontalAlignment = HorizontalAlignment.Left;
            Children.Add(fresh);
            Core.Language.Refresh(this);
        }

        private void Report()
        {
            Problem = McSnbt.Validate(Root);
            _problem.Text = Problem ?? "";
            _problem.Visibility = Problem == null ? Visibility.Collapsed : Visibility.Visible;
            _preview.Text = Problem != null || Root.Items.Count == 0 ? "Nothing yet. Add a key to start." : McSnbt.Render(Root);
            Core.Language.Refresh(_problem);
            Core.Language.Refresh(_preview);
            _changed(Snbt);
        }

        private void Rebuild()
        {
            _tree.Children.Clear();
            _tree.Children.Add(Section(Root, "root"));
            Core.Language.Refresh(_tree);
        }

        private FrameworkElement Section(SnbtNode node, string path)
        {
            var sp = new StackPanel();
            for (int i = 0; i < node.Items.Count; i++)
                sp.Children.Add(Entry(node, node.Items[i], path + "/" + (i + 1)));
            bool compound = node.Type == SnbtType.Compound;
            var add = Ui.Button(compound ? "Add key" : "Add item", () =>
            {
                if (compound)
                {
                    int n = node.Items.Count + 1;
                    string key = "key" + n;
                    while (node.Items.Any(c => c.Name == key)) key = "key" + (++n);
                    node.Items.Add(new SnbtNode { Name = key, Type = SnbtType.Int, Value = "0" });
                }
                else
                {
                    var t = node.Items.Count > 0 ? node.Items[0].Type : SnbtType.Int;
                    node.Items.Add(Blank(t));
                }
                Rebuild(); Report();
            }, "Btn.Ghost", Icons.Plus);
            AutomationProperties.SetName(add, (compound ? "Add key in " : "Add item in ") + path);
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Margin = new Thickness(0, 4, 0, 4);
            sp.Children.Add(add);
            return sp;
        }

        private static SnbtNode Blank(SnbtType t) => new SnbtNode { Type = t, Value = t == SnbtType.String || t == SnbtType.List || t == SnbtType.Compound ? "" : "0" };

        private FrameworkElement Entry(SnbtNode parent, SnbtNode node, string path)
        {
            bool inCompound = parent.Type == SnbtType.Compound;
            var outer = new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 6, 0, 8) };
            var sp = new StackPanel();
            outer.Child = sp;
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var line = new WrapPanel();
            if (inCompound)
            {
                var key = McCommandControls.Text(node.Name, "Key of " + path, 150, "The name of this value, like Invulnerable or CustomName");
                key.TextChanged += (o, e) => { node.Name = key.Text.Trim(); Report(); };
                line.Children.Add(key);
            }
            else line.Children.Add(new TextBlock { Text = "#" + (parent.Items.IndexOf(node) + 1), FontFamily = Ui.Font("F.Mono"), Foreground = Ui.Res("B.TextMute"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
            var kinds = new WrapPanel();
            AutomationProperties.SetName(kinds, "Type of " + path);
            foreach (var t in McSnbt.Types)
            {
                var tt = t;
                var chip = McUi.Chip(McSnbt.TypeName(t), node.Type == t, () => SetType(parent, node, tt), $"Type {McSnbt.TypeName(t)} for {path}");
                chip.ToolTip = McSnbt.TypeHelp(t);
                chip.Padding = new System.Windows.Thickness(8, 2, 8, 2);
                kinds.Children.Add(chip);
            }
            Core.Language.SetLiteral(kinds, true);
            line.Children.Add(kinds);
            top.Children.Add(line);
            var del = McUi.IconButton(Icons.Trash, "Remove " + path, () => { parent.Items.Remove(node); Rebuild(); Report(); });
            del.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(del, 1); top.Children.Add(del);
            sp.Children.Add(top);

            if (node.IsContainer)
            {
                var inner = new Border { Margin = new Thickness(18, 4, 0, 0), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(12, 0, 0, 0) };
                inner.Child = Section(node, path);
                sp.Children.Add(inner);
            }
            else
            {
                var value = McCommandControls.Text(node.Value, "Value of " + path, node.Type == SnbtType.String ? 320 : 160, McSnbt.TypeHelp(node.Type));
                var note = new TextBlock { FontSize = 11.5, Foreground = Ui.Res("B.Danger"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, -4, 0, 0) };
                void Note() { var p = McSnbt.CheckValue(node.Type, node.Value); note.Text = p ?? ""; note.Visibility = p == null ? Visibility.Collapsed : Visibility.Visible; value.BorderBrush = Ui.Res(p == null ? "B.LineStrong" : "B.Danger"); Core.Language.Refresh(note); }
                value.TextChanged += (o, e) => { node.Value = value.Text; Note(); Report(); };
                sp.Children.Add(value);
                sp.Children.Add(note);
                Note();
            }
            return outer;
        }

        private void SetType(SnbtNode parent, SnbtNode node, SnbtType to)
        {
            var targets = parent.Type == SnbtType.List ? parent.Items : new List<SnbtNode> { node };   // a list holds one type
            foreach (var t in targets)
            {
                if (t.Type == to) continue;
                bool container = to == SnbtType.List || to == SnbtType.Compound;
                if (container) t.Value = "";
                else if (t.IsContainer) { t.Items.Clear(); t.Value = to == SnbtType.String ? "" : "0"; }
                if (container && t.Type != to) t.Items.Clear();
                if (!container && to != SnbtType.String && McSnbt.CheckValue(to, t.Value) != null) t.Value = "0";
                t.Type = to;
            }
            Rebuild(); Report();
        }
    }
}
