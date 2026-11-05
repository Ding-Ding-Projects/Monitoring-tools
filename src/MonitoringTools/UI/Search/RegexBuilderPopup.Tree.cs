using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Core;
using TreeNode = MonitoringTools.Core.RegexTree.Node;

namespace MonitoringTools.UI.Search
{
    /// <summary>
    /// The Build view's syntax tree: an expandable tree of the pattern, two-way selection between
    /// the tree and the pattern box, brief or detailed explanations, export of the analysis, a
    /// guided editor for quantifiers, character sets and groups, and the Replace view's
    /// replacement explanation.
    /// </summary>
    public sealed partial class RegexBuilderPopup
    {
        private const int MaxTreeRows = 400;

        private RegexTree.Result _tree;
        private string _selKey;
        private bool _detailed, _flat;
        private readonly HashSet<string> _collapsed = new HashSet<string>();
        private readonly Dictionary<string, Border> _rows = new Dictionary<string, Border>();
        private readonly Dictionary<string, TreeNode> _nodes = new Dictionary<string, TreeNode>();
        private StackPanel _editor, _replExplain;
        private TextBlock _treeStatus;

        // ---------------------------------------------------------------- layout

        private StackPanel BuildTreeColumn()
        {
            var right = new StackPanel();
            right.Children.Add(Eyebrow("Syntax tree of the pattern"));
            var bar = new WrapPanel();
            Button brief = null, detail = null, tree = null, flat = null;
            void paint() { Paint(brief, !_detailed); Paint(detail, _detailed); Paint(tree, !_flat); Paint(flat, _flat); }
            brief = Chip("Brief", "Short labels in the tree (default)", () => { _detailed = false; paint(); RenderExplain(); });
            detail = Chip("Detailed", "Full sentences in the tree", () => { _detailed = true; paint(); RenderExplain(); });
            tree = Chip("Tree", "Show the nested syntax tree (default)", () => { _flat = false; paint(); RenderExplain(); });
            flat = Chip("Flat tokens", "Show a flat token-by-token list instead of the tree", () => { _flat = true; paint(); RenderExplain(); });
            foreach (var b in new[] { brief, detail, tree, flat }) bar.Children.Add(b);
            paint();
            right.Children.Add(bar);

            _treeStatus = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(2, 0, 0, 6), Foreground = Ui.Res("B.TextMute") };
            right.Children.Add(_treeStatus);

            _explain = new StackPanel();
            right.Children.Add(new Border
            {
                Background = Ui.Res("B.Sunken"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 6, 6, 6), Margin = new Thickness(0, 2, 0, 0),
                Child = new ScrollViewer { Content = _explain, MaxHeight = 230, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
            });

            _editor = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            right.Children.Add(_editor);

            var export = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            export.Children.Add(Ui.Button("Export Markdown", () => ExportAnalysis(false), "Btn.Ghost"));
            export.Children.Add(Ui.Button("Export HTML", () => ExportAnalysis(true), "Btn.Ghost"));
            right.Children.Add(export);
            return right;
        }

        // ---------------------------------------------------------------- render

        private void RenderExplain()
        {
            if (_explain == null) return;
            _explain.Children.Clear(); _rows.Clear(); _nodes.Clear();
            _tree = RegexTree.Parse(_s.Query, _s.EngineOptions);
            if (_s.Query.Length == 0)
            {
                _treeStatus.Text = ""; _selKey = null;
                _explain.Children.Add(Ui.Mono("Nothing to explain yet.", Ui.Res("B.TextMute"), 12));
                RenderEditor(null);
                return;
            }
            SetTreeStatus(_tree.Valid
                ? $"Valid · {_tree.NodeCount} node(s) · {_tree.Captures} capturing group(s)" + (_tree.Note != null ? " · " + _tree.Note : "")
                : $"The engine rejects this pattern{(_tree.MessageOffset >= 0 ? " at position " + _tree.MessageOffset : "")}: {_tree.Message}." + (_tree.Note != null ? " " + _tree.Note : ""),
                !_tree.Valid);
            if (!_tree.Valid && _tree.MessageOffset >= 0)
            {
                var off = _tree.MessageOffset;
                var go = Ui.Button("Select the error position", () =>
                {
                    var o = Math.Min(off, _pattern.Text.Length);
                    _pattern.Focus();
                    _pattern.Select(o, o < _pattern.Text.Length ? 1 : 0);
                }, "Btn.Ghost");
                go.Margin = new Thickness(0, 0, 0, 4); go.HorizontalAlignment = HorizontalAlignment.Left;
                _explain.Children.Add(go);
            }

            if (_flat)
            {
                _selKey = null;
                foreach (var t in RegexKit.Explain(_s.Query))
                {
                    var row = new DockPanel { Margin = new Thickness(t.Depth * 14, 2, 0, 2) };
                    var code = Ui.Mono(t.Text, Ui.Res("B.Mint"), 12);
                    code.MinWidth = 72; code.Margin = new Thickness(0, 0, 10, 0);
                    DockPanel.SetDock(code, Dock.Left); row.Children.Add(code);
                    row.Children.Add(new TextBlock { Text = t.Meaning, Foreground = Ui.Res("B.TextSoft"), FontSize = 12, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body") });
                    _explain.Children.Add(row);
                }
                RenderEditor(null);
                return;
            }

            var rows = 0; var truncated = false;
            void add(TreeNode parent, int depth)
            {
                foreach (var c in parent.Children)
                {
                    if (rows >= MaxTreeRows) { truncated = true; return; }
                    AddRow(c, depth); rows++;
                    if (!_collapsed.Contains(c.Key)) add(c, depth + 1);
                    if (truncated) return;
                }
            }
            add(_tree.Root, 0);
            if (truncated) _explain.Children.Add(Ui.Mono($"Showing the first {MaxTreeRows} rows. Collapse a branch to see the rest.", Ui.Res("B.TextMute"), 11));

            TreeNode sel = null;
            if (_selKey != null) _nodes.TryGetValue(_selKey, out sel);
            if (sel == null) _selKey = null;
            PaintRows();
            RenderEditor(sel);
        }

        private void SetTreeStatus(string text, bool warn)
        {
            _treeStatus.Text = text;
            _treeStatus.Foreground = warn ? Ui.Res("B.Amber") : Ui.Res("B.TextMute");
        }

        private static string Snippet(string s, int max)
        {
            s = s.Replace("\n", "↵").Replace("\r", "").Replace("\t", "⇥");
            return s.Length <= max ? s : s[..(max - 1)] + "…";
        }

        private static string Sentence(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

        private void AddRow(TreeNode n, int depth)
        {
            _nodes[n.Key] = n;
            var hasKids = n.Children.Count > 0;
            var collapsed = _collapsed.Contains(n.Key);
            var text = n.Text(_tree.Pattern);
            var chev = new TextBlock { Text = hasKids ? (collapsed ? "▸" : "▾") : "", Width = 16, FontSize = 12, Foreground = Ui.Res("B.TextMute"), VerticalAlignment = VerticalAlignment.Center };
            if (hasKids) { chev.Cursor = Cursors.Hand; chev.MouseLeftButtonDown += (o, e) => { e.Handled = true; Toggle(n); }; }
            var code = Ui.Mono(Snippet(text, 26), Ui.Res("B.Mint"), 12);
            code.MinWidth = 84; code.Margin = new Thickness(0, 0, 10, 0); code.VerticalAlignment = VerticalAlignment.Center; Core.Language.SetLiteral(code, true);   // the pattern text itself
            var label = new TextBlock { Text = _detailed ? Sentence(n.Detailed) : n.Brief, Foreground = Ui.Res("B.TextSoft"), FontSize = 12, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center };
            var dock = new DockPanel();
            DockPanel.SetDock(chev, Dock.Left); dock.Children.Add(chev);
            DockPanel.SetDock(code, Dock.Left); dock.Children.Add(code);
            dock.Children.Add(label);
            var row = new Border
            {
                Margin = new Thickness(depth * 14, 1, 0, 1), Padding = new Thickness(4, 3, 6, 3), CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent, Focusable = true, Cursor = Cursors.Hand, Child = dock, ToolTip = Sentence(n.Detailed)
            };
            AutomationProperties.SetName(row, text + ": " + n.Brief);
            AutomationProperties.SetHelpText(row, Sentence(n.Detailed) + ". Enter selects it in the pattern; Left and Right collapse and expand.");
            row.MouseLeftButtonDown += (o, e) => { SelectNode(n, true); row.Focus(); };
            row.KeyDown += (o, e) =>
            {
                switch (e.Key)
                {
                    case Key.Enter:
                    case Key.Space: SelectNode(n, true); e.Handled = true; break;
                    case Key.Right: if (hasKids && _collapsed.Contains(n.Key)) Toggle(n); e.Handled = true; break;
                    case Key.Left:
                        if (hasKids && !_collapsed.Contains(n.Key)) Toggle(n);
                        else if (n.Parent != null && _rows.TryGetValue(n.Parent.Key, out var pr)) pr.Focus();
                        e.Handled = true; break;
                    case Key.Down: row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); e.Handled = true; break;
                    case Key.Up: row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)); e.Handled = true; break;
                }
            };
            _rows[n.Key] = row;
            // Drag a row onto a neighbour at the same level to swap their order.
            row.AllowDrop = true;
            Point? dragFrom = null;
            row.PreviewMouseLeftButtonDown += (o, e) => dragFrom = e.GetPosition(row);
            row.PreviewMouseLeftButtonUp += (o, e) => dragFrom = null;
            row.MouseMove += (o, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || dragFrom == null) return;
                var d = e.GetPosition(row) - dragFrom.Value;
                if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                dragFrom = null;
                DragDrop.DoDragDrop(row, new DataObject("regex-node", n.Key), DragDropEffects.Move);
            };
            row.DragOver += (o, e) =>
            {
                e.Effects = e.Data.GetData("regex-node") is string k && _nodes.TryGetValue(k, out var mv) && RegexTree.CanReorder(mv, n) ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            row.Drop += (o, e) =>
            {
                if (e.Data.GetData("regex-node") is string k && _nodes.TryGetValue(k, out var mv)) { e.Handled = true; DropNode(mv, n); }
            };
            _explain.Children.Add(row);
        }

        private void Toggle(TreeNode n)
        {
            var key = n.Key;
            if (!_collapsed.Remove(key)) _collapsed.Add(key);
            RenderExplain();
            Dispatcher_Focus(key);
        }

        private void Dispatcher_Focus(string key) =>
            _card.Dispatcher.BeginInvoke(new Action(() => { if (_rows.TryGetValue(key, out var r)) r.Focus(); }));

        private void PaintRows()
        {
            foreach (var kv in _rows) kv.Value.Background = kv.Key == _selKey ? Ui.Res("B.SecondaryContainer") : Brushes.Transparent;
        }

        // ---------------------------------------------------------------- two-way selection

        private void SelectNode(TreeNode n, bool fromTree)
        {
            if (n == null || n.Kind == RegexTree.NodeKind.Pattern)
            {
                if (_selKey != null) { _selKey = null; PaintRows(); RenderEditor(null); }
                return;
            }
            var changed = _selKey != n.Key;
            _selKey = n.Key;
            PaintRows();
            if (fromTree) _pattern.Select(n.Start, n.End - n.Start);
            if (_rows.TryGetValue(n.Key, out var r)) r.BringIntoView();
            if (changed || fromTree) RenderEditor(n);
        }

        /// <summary>Moving the caret or selecting text in the pattern box highlights the node it falls in.</summary>
        private void OnPatternSelection()
        {
            if (_tab != "Build" || _flat || _tree == null || _rows.Count == 0) return;
            if (_pattern.Text != _tree.Pattern) return; // mid-edit: the tree is about to be rebuilt
            int st = _pattern.SelectionStart, len = _pattern.SelectionLength;
            if (_selKey != null && _nodes.TryGetValue(_selKey, out var cur) && cur.Start == st && cur.End - cur.Start == len) return;
            var n = RegexTree.Smallest(_tree.Root, st, len);
            while (n != null && !_rows.ContainsKey(n.Key)) n = n.Parent;
            SelectNode(n, false);
        }

        // ---------------------------------------------------------------- guided editor

        private static TextBlock Label(string text) =>
            new TextBlock { Text = text, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) };

        private static TextBox SmallInput(string name, string text, double width)
        {
            var t = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Width = width, Text = text, Margin = new Thickness(0, 0, 10, 6) };
            AutomationProperties.SetName(t, name);
            return t;
        }

        private void RenderEditor(TreeNode n)
        {
            if (_editor == null) return;
            _editor.Children.Clear();
            if (n == null || _tree == null)
            {
                var hint = Ui.Mono("Select a row in the tree, or put the caret in the pattern, to see what that part does. Select a quantifier, a character set or a group to edit it here.", Ui.Res("B.TextMute"), 11);
                hint.TextWrapping = TextWrapping.Wrap;
                _editor.Children.Add(hint);
                return;
            }
            _editor.Children.Add(Eyebrow("Selected: " + Snippet(n.Text(_tree.Pattern), 40)));
            var about = new TextBlock { Text = Sentence(n.Detailed) + $" (characters {n.Start} to {n.End})", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 6) };
            _editor.Children.Add(about);
            switch (n.Kind)
            {
                case RegexTree.NodeKind.Quantifier: QuantifierEditor(n); break;
                case RegexTree.NodeKind.CharClass: ClassEditor(n); break;
                case RegexTree.NodeKind.Group when RegexTree.IsEditableGroup(n): GroupEditor(n); break;
                case RegexTree.NodeKind.Group when n.Group == RegexTree.GroupKind.Balancing: BalancingEditor(n); break;
                case RegexTree.NodeKind.Anchor: ChoiceEditor(n, "Anchor", RegexTree.AnchorChoices, (p, c) => RegexTree.ApplyAnchor(p, n, c, _s.EngineOptions)); break;
                case RegexTree.NodeKind.Shorthand: ChoiceEditor(n, "Shorthand class", RegexTree.ShorthandChoices, (p, c) => RegexTree.ApplyShorthand(p, n, c, _s.EngineOptions)); break;
                case RegexTree.NodeKind.Escape: EscapeEditor(n); break;
                case RegexTree.NodeKind.Comment when RegexTree.CommentText(_tree.Pattern, n) != null: CommentEditor(n); break;
                default:
                    var note = Ui.Mono("No guided editor for this part. Edit it as text: its characters are selected in the pattern box.", Ui.Res("B.TextMute"), 11);
                    note.TextWrapping = TextWrapping.Wrap;
                    _editor.Children.Add(note);
                    break;
            }
            MoveControls(n);
        }

        private void ChoiceEditor(TreeNode n, string what, (string Text, string Meaning)[] choices, Func<string, string, string> apply)
        {
            _editor.Children.Add(Label(what + ": pick another"));
            var wrap = new WrapPanel();
            foreach (var (text, meaning) in choices)
            {
                var t = text;
                var chip = Chip(text, meaning, () =>
                {
                    var next = apply(_tree.Pattern, t);
                    if (next == null) { SetTreeStatus("That change cannot be made.", true); return; }
                    ApplyEdit(next, n);
                });
                chip.FontFamily = Ui.Font("F.Mono");
                Paint(chip, n.Text(_tree.Pattern) == text);
                wrap.Children.Add(chip);
            }
            _editor.Children.Add(wrap);
        }

        private void EscapeEditor(TreeNode n)
        {
            var box = SmallInput("Replacement escape or character", n.Text(_tree.Pattern), 160);
            _editor.Children.Add(Label("Escape, such as \\. \\t \\x41 or \\p{L}, or one letter or digit"));
            _editor.Children.Add(box);
            _editor.Children.Add(Ui.Button("Apply to the pattern", () =>
            {
                var next = RegexTree.ApplyEscape(_tree.Pattern, n, box.Text.Trim(), _s.EngineOptions);
                if (next == null) { SetTreeStatus("That is not an escape this editor accepts. Use a backslash and one character, \\xNN, \\uNNNN, \\cX, or a single letter or digit.", true); return; }
                ApplyEdit(next, n);
            }, "Btn.Accent"));
        }

        private void CommentEditor(TreeNode n)
        {
            var box = SmallInput("Comment text", RegexTree.CommentText(_tree.Pattern, n), 320);
            _editor.Children.Add(Label("Comment (ignored when matching; it cannot contain a closing parenthesis)"));
            _editor.Children.Add(box);
            _editor.Children.Add(Ui.Button("Apply to the pattern", () =>
            {
                var next = RegexTree.ApplyComment(_tree.Pattern, n, box.Text, _s.EngineOptions);
                if (next == null) { SetTreeStatus("A comment cannot contain a closing parenthesis.", true); return; }
                ApplyEdit(next, n);
            }, "Btn.Accent"));
        }

        private void BalancingEditor(TreeNode n)
        {
            var name = SmallInput("Name the group captures into, empty for none", n.Name ?? "", 140);
            var other = SmallInput("Name of the group whose capture is removed", n.Other ?? "", 140);
            var line = new WrapPanel();
            line.Children.Add(Label("Captures as")); line.Children.Add(name);
            line.Children.Add(Label("Removes from")); line.Children.Add(other);
            _editor.Children.Add(line);
            _editor.Children.Add(Ui.Button("Apply to the pattern", () =>
            {
                var next = RegexTree.ApplyBalancing(_tree.Pattern, n, name.Text.Trim(), other.Text.Trim(), _s.EngineOptions);
                if (next == null) { SetTreeStatus("Names use letters, digits and underscores; the group to remove from is required.", true); return; }
                ApplyEdit(next, n);
            }, "Btn.Accent"));
        }

        /// <summary>Earlier and Later buttons for any part that has a sibling to swap with. The text of each part is unchanged; only the order moves.</summary>
        private void MoveControls(TreeNode n)
        {
            if (n.Parent == null || n.Kind == RegexTree.NodeKind.Pattern) return;
            var sibs = n.Parent.Children;
            int i = sibs.IndexOf(n);
            var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            row.Children.Add(Label("Move this part"));
            var earlier = Ui.Button("Earlier", () => ReorderNodes(n, sibs[i - 1], false), "Btn.Ghost");
            var later = Ui.Button("Later", () => ReorderNodes(n, sibs[i + 1], true), "Btn.Ghost");
            earlier.ToolTip = "Swap this part with the one before it, keeping both exactly as written";
            later.ToolTip = "Swap this part with the one after it, keeping both exactly as written";
            earlier.IsEnabled = i > 0 && RegexTree.CanReorder(n, sibs[i - 1]);
            later.IsEnabled = i >= 0 && i + 1 < sibs.Count && RegexTree.CanReorder(n, sibs[i + 1]);
            earlier.Margin = new Thickness(0, 0, 6, 0);
            row.Children.Add(earlier); row.Children.Add(later);
            _editor.Children.Add(row);
        }

        private void QuantifierEditor(TreeNode n)
        {
            var min = SmallInput("Minimum repeat count", n.Min == int.MaxValue ? "" : n.Min.ToString(CultureInfo.InvariantCulture), 64);
            var max = SmallInput("Maximum repeat count, empty for no limit", n.Max == -1 ? "" : n.Max == int.MaxValue ? "" : n.Max.ToString(CultureInfo.InvariantCulture), 64);
            var lazy = n.Lazy;
            Button lazyChip = null;
            lazyChip = Chip("Lazy", "Match as few repeats as possible instead of as many (default off)", () => { lazy = !lazy; Paint(lazyChip, lazy); });
            Paint(lazyChip, lazy);
            var line = new WrapPanel();
            line.Children.Add(Label("Min")); line.Children.Add(min);
            line.Children.Add(Label("Max (empty = no limit)")); line.Children.Add(max);
            line.Children.Add(lazyChip);
            _editor.Children.Add(line);
            var presets = new WrapPanel();
            foreach (var (label, lo, hi) in new[] { ("* zero or more", 0, -1), ("+ one or more", 1, -1), ("? optional", 0, 1) })
            {
                var a = lo; var b = hi;
                var chip = Chip(label, "Fill in the range for this quantifier", () => { min.Text = a.ToString(CultureInfo.InvariantCulture); max.Text = b == -1 ? "" : b.ToString(CultureInfo.InvariantCulture); });
                chip.FontFamily = Ui.Font("F.Mono");
                presets.Children.Add(chip);
            }
            _editor.Children.Add(presets);
            _editor.Children.Add(Ui.Button("Apply to the pattern", () =>
            {
                if (!int.TryParse(min.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var lo)) { SetTreeStatus("The minimum must be a whole number.", true); return; }
                var hi = -1;
                if (max.Text.Trim().Length > 0 && !int.TryParse(max.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out hi)) { SetTreeStatus("The maximum must be a whole number, or empty for no limit.", true); return; }
                var next = RegexTree.ApplyQuantifier(_tree.Pattern, n, lo, hi, lazy);
                if (next == null) { SetTreeStatus("The minimum cannot be larger than the maximum.", true); return; }
                ApplyEdit(next, n);
            }, "Btn.Accent"));
        }

        private void ClassEditor(TreeNode n)
        {
            var members = new TextBox { Style = Ui.Style("Input"), FontFamily = Ui.Font("F.Mono"), Text = RegexTree.ClassMembers(_tree.Pattern, n), Margin = new Thickness(0, 0, 0, 6) };
            AutomationProperties.SetName(members, "Members of the character set");
            var neg = n.Negated;
            Button negChip = null;
            negChip = Chip("Match anything except these", "Negate the set, as [^...] does (default off)", () => { neg = !neg; Paint(negChip, neg); });
            Paint(negChip, neg);
            _editor.Children.Add(Label("Members, written as in the pattern, such as a-z\\d_"));
            _editor.Children.Add(members);
            _editor.Children.Add(negChip);
            if (n.Children.Count > 0) _editor.Children.Add(Ui.Mono("The subtracted set stays as it is.", Ui.Res("B.TextMute"), 11));
            _editor.Children.Add(Ui.Button("Apply to the pattern", () =>
            {
                var next = RegexTree.ApplyClass(_tree.Pattern, n, members.Text, neg);
                if (next == null) { SetTreeStatus("A character set needs at least one member.", true); return; }
                ApplyEdit(next, n);
            }, "Btn.Accent"));
        }

        private void GroupEditor(TreeNode n)
        {
            var kind = n.Group;
            var name = SmallInput("Name of the group", n.Name ?? "", 160);
            var nameRow = new WrapPanel { Visibility = kind == RegexTree.GroupKind.Named ? Visibility.Visible : Visibility.Collapsed };
            nameRow.Children.Add(Label("Name")); nameRow.Children.Add(name);
            var chips = new Dictionary<RegexTree.GroupKind, Button>();
            var wrap = new WrapPanel();
            void paint() { foreach (var kv in chips) Paint(kv.Value, kv.Key == kind); nameRow.Visibility = kind == RegexTree.GroupKind.Named ? Visibility.Visible : Visibility.Collapsed; }
            foreach (var k in RegexTree.EditableGroupKinds)
            {
                var kk = k;
                var chip = Chip(RegexTree.GroupKindLabel(k), "Make this a " + RegexTree.GroupKindLabel(k).ToLowerInvariant() + " group", () => { kind = kk; paint(); });
                chips[k] = chip; wrap.Children.Add(chip);
            }
            paint();
            _editor.Children.Add(Label("Kind of group"));
            _editor.Children.Add(wrap);
            _editor.Children.Add(nameRow);
            _editor.Children.Add(Ui.Button("Apply to the pattern", () =>
            {
                var next = RegexTree.ApplyGroupKind(_tree.Pattern, n, kind, name.Text.Trim());
                if (next == null) { SetTreeStatus(kind == RegexTree.GroupKind.Named ? "A group name may use letters, digits and underscores only." : "That change cannot be made.", true); return; }
                ApplyEdit(next, n);
            }, "Btn.Accent"));
        }

        /// <summary>Writes an edited pattern back. A change that would break a working pattern is refused, so a guided edit never leaves the pattern invalid.</summary>
        private void ApplyEdit(string next, TreeNode n)
        {
            var after = RegexTree.Parse(next, _s.EngineOptions);
            if (_tree.Valid && !after.Valid)
            {
                SetTreeStatus("Not applied: the result would not be valid. " + after.Message + ".", true);
                return;
            }
            var start = n.Start; var kind = n.Kind;
            _pattern.Text = next; // TextChanged updates the shared state and rebuilds the tree
            var again = _tree == null ? null : RegexTree.Walk(_tree.Root).FirstOrDefault(x => x.Kind == kind && x.Start == start);
            if (again != null) SelectNode(again, true);
            SetTreeStatus("Applied. The pattern now reads " + Snippet(next, 60), false);
        }

        // ---------------------------------------------------------------- export

        private void ExportAnalysis(bool html)
        {
            var d = new Microsoft.Win32.SaveFileDialog
            {
                FileName = html ? "regex-analysis.html" : "regex-analysis.md",
                Filter = html ? "HTML|*.html" : "Markdown|*.md"
            };
            if (d.ShowDialog() != true) return;
            try
            {
                var res = RegexTree.Parse(_s.Query, _s.EngineOptions);
                File.WriteAllText(d.FileName, html ? RegexTree.ToHtml(res, _s.EngineOptions, _detailed, _s.Sample ?? DefaultSample()) : RegexTree.ToMarkdown(res, _s.EngineOptions, _detailed, _s.Sample ?? DefaultSample()), new UTF8Encoding(false));
                SetTreeStatus("Exported the " + (_detailed ? "detailed" : "brief") + " analysis to " + d.FileName, false);
            }
            catch (Exception ex)
            {
                Log.Warn("Regex analysis export failed: " + ex.Message);
                SetTreeStatus("Could not write the file: " + ex.Message, true);
            }
        }

        // ---------------------------------------------------------------- replacement explanation

        private void RenderReplacementExplanation(Regex rx)
        {
            if (_replExplain == null) return;
            _replExplain.Children.Clear();
            var repl = _s.Replacement ?? "";
            if (repl.Length == 0)
            {
                _replExplain.Children.Add(Ui.Mono("The replacement is empty, so every match is deleted.", Ui.Res("B.TextMute"), 12));
                return;
            }
            if (rx == null)
            {
                _replExplain.Children.Add(Ui.Mono("Fix the pattern first: group references can only be checked against a valid pattern.", Ui.Res("B.TextMute"), 12));
                return;
            }
            var tokens = RegexKit.ExplainReplacement(repl, rx);
            var bad = tokens.Count(t => t.Problem);
            if (bad > 0)
            {
                var warn = new TextBlock { Text = bad == 1 ? "One part of the replacement is not a valid reference, so it is copied to the result as plain text." : $"{bad} parts of the replacement are not valid references, so they are copied to the result as plain text.", FontSize = 12, Foreground = Ui.Res("B.Amber"), TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) };
                AutomationProperties.SetLiveSetting(warn, AutomationLiveSetting.Polite);
                _replExplain.Children.Add(warn);
            }
            foreach (var t in tokens)
            {
                var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                var isLiteral = t.Meaning.StartsWith("literal text", StringComparison.Ordinal);
                var code = Ui.Mono(Snippet(t.Text, 24), t.Problem ? Ui.Res("B.Amber") : isLiteral ? Ui.Res("B.TextMute") : Ui.Res("B.Mint"), 12);
                code.MinWidth = 84; code.Margin = new Thickness(0, 0, 10, 0); Core.Language.SetLiteral(code, true);
                DockPanel.SetDock(code, Dock.Left); row.Children.Add(code);
                row.Children.Add(new TextBlock { Text = t.Meaning, Foreground = t.Problem ? Ui.Res("B.Amber") : Ui.Res("B.TextSoft"), FontSize = 12, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Body") });
                _replExplain.Children.Add(row);
            }
        }
    }
}
