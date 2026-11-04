using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Tabs
{
    /// <summary>A page the control centre can show in a tab.</summary>
    public sealed record PageEntry(string Id, string Label, string Icon);

    /// <summary>One row of the tab strip as the dialogs and searches see it.</summary>
    public sealed record TabInfo(string Id, string Label, string Icon, bool Pinned, string GroupId, string GroupName, bool Active);

    /// <summary>The modal surfaces of the tab strip: the New tab catalog, the move-into-group picker, text prompts, colour choice and bulk close.</summary>
    internal static class TabDialogs
    {
        // -------------------------------------------------------------- shell

        public static Window Make(Window owner, string title, double width, double height, out StackPanel body)
        {
            var w = new Window
            {
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Width = width, Height = height, BorderThickness = new Thickness(1),
                UseLayoutRounding = true, Title = title,
            };
            w.MaxHeight = Math.Max(320, SystemParameters.WorkArea.Height - 40);
            w.SetResourceReference(Control.BackgroundProperty, "B.Window");
            w.SetResourceReference(Control.BorderBrushProperty, "B.LineStrong");
            AutomationProperties.SetName(w, title);

            var grid = new Grid { Margin = new Thickness(22, 18, 22, 20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition());
            var head = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(new TextBlock { Text = title, Style = Ui.Style("T.Title"), FontSize = 20, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            var close = new Button { Style = Ui.Style("Btn.Icon"), Content = Ui.Icon(Icons.Close, Ui.Res("B.TextSoft"), 14), Width = 36, Height = 36, ToolTip = "Close (Esc)" };
            AutomationProperties.SetName(close, "Close dialog");
            close.Click += (s, e) => w.Close();
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            grid.Children.Add(head);
            body = new StackPanel();
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = body };
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);
            w.Content = grid;
            w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && !e.Handled) { w.Close(); e.Handled = true; } };
            return w;
        }

        /// <summary>A two-line result row with an icon, used by every list in these dialogs.</summary>
        public static Button RowButton(string icon, string title, string sub, Brush dot = null)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            FrameworkElement lead;
            if (dot != null) lead = new Ellipse { Width = 12, Height = 12, Fill = dot, Margin = new Thickness(4, 0, 0, 0) };
            else lead = Ui.Icon(icon ?? Icons.Lines, Ui.Res("B.TextSoft"), 16);
            lead.VerticalAlignment = VerticalAlignment.Center;
            lead.Margin = new Thickness(lead.Margin.Left, 0, 14, 0);
            g.Children.Add(lead);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = title, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis });
            if (!string.IsNullOrEmpty(sub)) text.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(text, 1);
            g.Children.Add(text);
            var b = new Button { Style = Ui.Style("Btn.Row"), Content = g, Height = 52, HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 2) };
            AutomationProperties.SetName(b, string.IsNullOrEmpty(sub) ? title : title + ", " + sub);
            return b;
        }

        /// <summary>Arrow-key navigation between a search box and the buttons of a result list.</summary>
        public static void WireList(Panel list, TextBox box)
        {
            box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Down)
                {
                    var f = list.Children.OfType<Button>().FirstOrDefault(x => x.IsEnabled);
                    if (f != null) { f.Focus(); e.Handled = true; }
                }
                else if (e.Key == Key.Enter)
                {
                    var f = list.Children.OfType<Button>().FirstOrDefault(x => x.IsEnabled);
                    if (f != null) { f.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); e.Handled = true; }
                }
            };
            list.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Up && e.Key != Key.Down) return;
                var items = list.Children.OfType<Button>().Where(x => x.IsEnabled).ToList();
                var i = items.FindIndex(x => x.IsKeyboardFocused);
                if (i < 0) return;
                if (e.Key == Key.Down && i < items.Count - 1) items[i + 1].Focus();
                else if (e.Key == Key.Up) { if (i > 0) items[i - 1].Focus(); else box.Focus(); }
                e.Handled = true;
            };
        }

        private static SearchField Field(string id, string label)
        {
            var f = new SearchField(id, label) { MinWidth = 0, Margin = new Thickness(0, 0, 0, 10) };
            f.State.SetQuery("");
            return f;
        }

        // -------------------------------------------------------------- New tab catalog

        /// <summary>Lists every page with its icon. Returns the chosen page id, or null when cancelled.</summary>
        public static string Catalog(Window owner, IReadOnlyList<PageEntry> pages, ISet<string> open)
        {
            var w = Make(owner, "New tab", 540, 600, out var body);
            body.Children.Add(Ui.Text("Every page of the control centre. Choosing one opens it in a tab, or switches to its tab when it is already open.", "T.Body"));
            ((TextBlock)body.Children[0]).Margin = new Thickness(0, 0, 0, 12);
            ((TextBlock)body.Children[0]).Foreground = Ui.Res("B.TextMute");
            var search = Field("tabs:catalog", "Search every page");
            search.SampleSource = () => pages.Select(p => p.Label);
            body.Children.Add(search);
            var list = new StackPanel();
            body.Children.Add(list);
            string chosen = null;
            void Render()
            {
                list.Children.Clear();
                int shown = 0;
                foreach (var p in pages)
                {
                    bool isOpen = open.Contains(p.Id);
                    if (!search.State.Matches(p.Label + " " + p.Id + (isOpen ? " open" : ""))) continue;
                    shown++;
                    var b = RowButton(p.Icon, p.Label, isOpen ? "Open. Choosing it switches to its tab." : "Opens in a new tab.");
                    var id = p.Id;
                    b.Click += (s, e) => { chosen = id; w.Close(); };
                    list.Children.Add(b);
                }
                search.SetCount(shown, pages.Count, "pages");
            }
            search.Filter += Render;
            Render();
            WireList(list, search.Box);
            w.Loaded += (s, e) => search.Box.Focus();
            w.ShowDialog();
            return chosen;
        }

        // -------------------------------------------------------------- move into group

        public sealed record MoveChoice(string GroupId, string NewGroupName);

        /// <summary>Anchored-style picker listing existing groups, with a path to create one. Returns null when cancelled.</summary>
        public static MoveChoice MovePicker(Window owner, string tabLabel, IReadOnlyList<(TabGroupState Group, int Count)> groups)
        {
            var w = Make(owner, "Move “" + tabLabel + "” into a group", 480, 560, out var body);
            var search = Field("tabs:move-picker", "Search groups");
            search.SampleSource = () => groups.Select(g => g.Group.Name);
            var list = new StackPanel();
            MoveChoice result = null;
            var empty = Ui.Text("No groups yet. Create the first one below and the tab moves into it.", "T.Body");
            empty.Foreground = Ui.Res("B.TextMute");
            empty.Margin = new Thickness(0, 4, 0, 12);
            empty.TextWrapping = TextWrapping.Wrap;
            if (groups.Count == 0) body.Children.Add(empty);
            else
            {
                body.Children.Add(search);
                body.Children.Add(list);
            }
            void Render()
            {
                list.Children.Clear();
                int shown = 0;
                foreach (var (g, count) in groups)
                {
                    if (!search.State.Matches(g.Name)) continue;
                    shown++;
                    var b = RowButton(null, g.Name, count == 1 ? "1 tab" : count + " tabs" + (g.Collapsed ? ", collapsed" : ""), Ui.Res(g.Colour));
                    var id = g.Id;
                    b.Click += (s, e) => { result = new MoveChoice(id, null); w.Close(); };
                    list.Children.Add(b);
                }
                search.SetCount(shown, groups.Count, "groups");
            }
            search.Filter += Render;
            Render();
            WireList(list, search.Box);

            var create = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            create.Children.Add(new TextBlock { Text = "Create a new group", Style = Ui.Style("T.Title"), FontSize = 14, Margin = new Thickness(0, 0, 0, 8) });
            var name = new TextBox { Style = Ui.Style("Input"), Margin = new Thickness(0, 0, 0, 8) };
            AutomationProperties.SetName(name, "New group name");
            var go = Ui.Button("Create group and move", () =>
            {
                var n = name.Text.Trim();
                if (n.Length == 0) return;
                result = new MoveChoice(null, n);
                w.Close();
            }, "Btn.Accent", Icons.Plus);
            name.KeyDown += (s, e) => { if (e.Key == Key.Enter) { go.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); e.Handled = true; } };
            create.Children.Add(name);
            create.Children.Add(go);
            body.Children.Add(create);
            w.Loaded += (s, e) => { if (groups.Count > 0) search.Box.Focus(); else name.Focus(); };
            w.ShowDialog();
            return result;
        }

        // -------------------------------------------------------------- prompt and colour

        public static string Prompt(Window owner, string title, string label, string initial, string ok)
        {
            var w = Make(owner, title, 420, 230, out var body);
            body.Children.Add(new TextBlock { Text = label, Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
            var box = new TextBox { Style = Ui.Style("Input"), Text = initial ?? "", Margin = new Thickness(0, 0, 0, 12) };
            AutomationProperties.SetName(box, label);
            body.Children.Add(box);
            string result = null;
            var btn = Ui.Button(ok, () => { var t = box.Text.Trim(); if (t.Length == 0) return; result = t; w.Close(); }, "Btn.Accent");
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) { btn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); e.Handled = true; } };
            body.Children.Add(btn);
            w.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
            w.ShowDialog();
            return result;
        }

        public static string Colour(Window owner, string current, IReadOnlyList<(string Name, string Key)> options)
        {
            var w = Make(owner, "Group colour", 420, 420, out var body);
            body.Children.Add(new TextBlock { Text = "Colours come from the app theme, so they follow light, dark and your appearance choices.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            var list = new StackPanel();
            string result = null;
            foreach (var (name, key) in options)
            {
                var b = RowButton(null, name, key == current ? "Current colour" : null, Ui.Res(key));
                var k = key;
                b.Click += (s, e) => { result = k; w.Close(); };
                list.Children.Add(b);
            }
            body.Children.Add(list);
            list.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Up && e.Key != Key.Down) return;
                var items = list.Children.OfType<Button>().ToList();
                var i = items.FindIndex(x => x.IsKeyboardFocused);
                if (i < 0) return;
                var n = e.Key == Key.Down ? Math.Min(items.Count - 1, i + 1) : Math.Max(0, i - 1);
                items[n].Focus();
                e.Handled = true;
            };
            w.Loaded += (s, e) => (list.Children.OfType<Button>().FirstOrDefault() as UIElement)?.Focus();
            w.ShowDialog();
            return result;
        }

        // -------------------------------------------------------------- bulk close

        /// <summary>
        /// Close tabs whose label contains (or does not contain) the entered text. Shows the match mode and a preview
        /// first, never runs on an empty query or an invalid pattern, and protects pinned tabs unless included.
        /// Returns the tab ids to close, or null when cancelled.
        /// </summary>
        public static IReadOnlyList<string> BulkClose(Window owner, bool containing, IReadOnlyList<TabInfo> tabs)
        {
            var title = containing ? "Close tabs containing text" : "Close tabs not containing text";
            var w = Make(owner, title, 540, 640, out var body);
            body.Children.Add(new TextBlock
            {
                Text = "Matches the visible tab label only. Nothing is closed until you press the button below, and the preview shows exactly which tabs would close.",
                Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap
            });
            var field = Field(containing ? "tabs:bulk-in" : "tabs:bulk-out", containing ? "Text in the tab label" : "Text the tab label must contain");
            field.SampleSource = () => tabs.Select(t => t.Label);
            body.Children.Add(field);
            var include = new CheckBox { Style = Ui.Style("Switch"), Tag = Ui.Res("B.Mint"), Margin = new Thickness(0, 0, 0, 12) };
            include.Content = new TextBlock { Text = "Include pinned tabs (off by default)", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") };
            body.Children.Add(include);
            var mode = Ui.Mono("", Ui.Res("B.TextMute"));
            mode.Margin = new Thickness(0, 0, 0, 4);
            var summary = new TextBlock { Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
            AutomationProperties.SetLiveSetting(summary, AutomationLiveSetting.Polite);
            var preview = new StackPanel();
            var go = Ui.Button("Close tabs", null, "Btn.Danger", Icons.Trash);
            go.IsEnabled = false;
            body.Children.Add(mode);
            body.Children.Add(summary);
            body.Children.Add(preview);
            body.Children.Add(go);
            List<string> result = null;
            var pending = new List<string>();

            void Compute()
            {
                var st = field.State;
                mode.Text = st.IsRegex ? "Mode: regular expression" + (st.MatchCase ? ", match case" : ", ignore case") + (st.Error != null ? ", INVALID" : "") : "Mode: plain text, every word must " + (containing ? "appear" : "appear for a tab to be kept") + (st.MatchCase ? ", match case" : ", ignore case");
                pending.Clear();
                preview.Children.Clear();
                int protectedCount = 0;
                if (!st.IsActive) summary.Text = "Enter text first. Nothing closes on an empty query.";
                else if (st.Error != null) summary.Text = "The pattern is not valid, so nothing can close: " + st.Error;
                else
                {
                    foreach (var t in tabs)
                    {
                        if (st.Matches(t.Label) != containing) continue;
                        if (t.Pinned && include.IsChecked != true) { protectedCount++; continue; }
                        pending.Add(t.Id);
                    }
                    summary.Text = $"{pending.Count} of {tabs.Count} tabs would close." + (protectedCount > 0 ? $" {protectedCount} pinned {(protectedCount == 1 ? "tab is" : "tabs are")} protected." : "") + (st.TimedOut ? " Some labels took too long to check and were left out." : "");
                    foreach (var id in pending.Take(14))
                    {
                        var t = tabs.First(x => x.Id == id);
                        preview.Children.Add(new TextBlock { Text = "• " + t.Label + (t.Pinned ? "  (pinned)" : "") + (t.GroupName != null ? "  in " + t.GroupName : ""), Style = Ui.Style("T.Body"), Margin = new Thickness(0, 0, 0, 2) });
                    }
                    if (pending.Count > 14) preview.Children.Add(Ui.Text($"and {pending.Count - 14} more"));
                }
                go.IsEnabled = pending.Count > 0 && st.IsActive && st.Error == null;
                go.Content = pending.Count > 0 ? $"Close {pending.Count} {(pending.Count == 1 ? "tab" : "tabs")}" : "Close tabs";
            }
            field.Filter += Compute;
            include.Click += (s, e) => Compute();
            go.Click += (s, e) => { if (pending.Count == 0) return; result = pending.ToList(); w.Close(); };
            Compute();
            w.Loaded += (s, e) => field.Box.Focus();
            w.ShowDialog();
            return result;
        }

        // -------------------------------------------------------------- the four tab searches

        public enum SearchMode { Strip, Group, GroupNames, Master }

        public sealed record SearchHooks(
            Func<IReadOnlyList<TabInfo>> Tabs,
            Func<IReadOnlyList<(TabGroupState Group, int Count)>> Groups,
            Action<string> Activate,
            Action<string> RevealGroup,
            Action<string> TogglePin,
            Action<string> Close);

        /// <summary>
        /// One tab search surface in four scopes: the current strip, one group, group names, or the master search of every open tab.
        /// Each scope has its own SearchField and regex builder with its own state. Results stay live while the query is kept.
        /// </summary>
        public static void Search(Window owner, SearchMode mode, TabGroupState group, string dock, SearchHooks hooks)
        {
            string title = mode switch
            {
                SearchMode.Strip => "Search this tab strip",
                SearchMode.Group => "Search the group “" + group?.Name + "”",
                SearchMode.GroupNames => "Search tab groups",
                _ => "Search all open tabs",
            };
            var w = Make(owner, title, 600, 640, out var body);
            string id = mode switch { SearchMode.Strip => "tabs:strip", SearchMode.Group => "tabs:group:" + group?.Id, SearchMode.GroupNames => "tabs:groups", _ => "tabs:master" };
            string label = mode switch { SearchMode.Strip => "Search tabs in this strip", SearchMode.Group => "Search tabs in this group", SearchMode.GroupNames => "Search group names", _ => "Search every open tab" };
            string scopeNote = mode switch
            {
                SearchMode.Strip => "Searches every tab of this strip. Groups and pinned state are shown on each result.",
                SearchMode.Group => "Searches only the tabs of this group. It never looks at other groups.",
                SearchMode.GroupNames => "Searches the names of tab groups.",
                _ => "Searches every open tab in this app. There is one window and one strip, shown on each result.",
            };
            body.Children.Add(new TextBlock { Text = scopeNote, Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            var field = new SearchField(id, label) { Margin = new Thickness(0, 0, 0, 10) };
            body.Children.Add(field);
            var list = new StackPanel();
            body.Children.Add(list);
            string activate = null, reveal = null;

            void Render()
            {
                list.Children.Clear();
                int shown = 0, total = 0;
                if (mode == SearchMode.GroupNames)
                {
                    var groups = hooks.Groups();
                    total = groups.Count;
                    foreach (var (g, count) in groups)
                    {
                        if (!field.State.Matches(g.Name)) continue;
                        shown++;
                        var b = RowButton(null, g.Name, (count == 1 ? "1 tab" : count + " tabs") + (g.Collapsed ? ", collapsed" : ", expanded"), Ui.Res(g.Colour));
                        var gid = g.Id;
                        b.Click += (s, e) => { reveal = gid; w.Close(); };
                        list.Children.Add(b);
                    }
                    field.SetCount(shown, total, "groups");
                    return;
                }
                var tabs = hooks.Tabs().Where(t => mode != SearchMode.Group || t.GroupId == group?.Id).ToList();
                total = tabs.Count;
                foreach (var t in tabs)
                {
                    if (!field.State.Matches(t.Label)) continue;
                    shown++;
                    var sub = (mode == SearchMode.Master ? "Control centre · tab strip on the " + dock + " · " : "") + (t.Pinned ? "pinned" : t.GroupName != null ? "group " + t.GroupName : "no group") + (t.Active ? " · active" : "");
                    var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var b = RowButton(t.Icon, t.Label, sub);
                    var tid = t.Id;
                    b.Click += (s, e) => { activate = tid; w.Close(); };
                    row.Children.Add(b);
                    var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
                    var pin = Ui.Button(t.Pinned ? "Unpin" : "Pin", () => { hooks.TogglePin(tid); Render(); }, "Btn.Chip");
                    pin.Margin = new Thickness(0, 0, 6, 0);
                    AutomationProperties.SetName(pin, (t.Pinned ? "Unpin " : "Pin ") + t.Label);
                    var cls = Ui.Button("Close", () => { hooks.Close(tid); Render(); }, "Btn.Chip");
                    cls.Margin = new Thickness(0);
                    AutomationProperties.SetName(cls, "Close " + t.Label);
                    actions.Children.Add(pin);
                    actions.Children.Add(cls);
                    Grid.SetColumn(actions, 1);
                    row.Children.Add(actions);
                    list.Children.Add(row);
                }
                field.SetCount(shown, total, "tabs");
            }
            field.SampleSource = () => mode == SearchMode.GroupNames ? hooks.Groups().Select(g => g.Group.Name) : hooks.Tabs().Select(t => t.Label);
            field.Filter += Render;
            Render();
            // Arrow keys: the main row buttons are nested in grids, so wire a flat list of them.
            field.Box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Down && e.Key != Key.Enter) return;
                var first = FlatButtons(list).FirstOrDefault();
                if (first == null) return;
                if (e.Key == Key.Down) first.Focus(); else first.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                e.Handled = true;
            };
            list.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Up && e.Key != Key.Down) return;
                var items = FlatButtons(list).ToList();
                var i = items.FindIndex(x => x.IsKeyboardFocused);
                if (i < 0) return;
                if (e.Key == Key.Down && i < items.Count - 1) items[i + 1].Focus();
                else if (e.Key == Key.Up) { if (i > 0) items[i - 1].Focus(); else field.Box.Focus(); }
                e.Handled = true;
            };
            w.Loaded += (s, e) => field.Box.Focus();
            w.ShowDialog();
            if (reveal != null) hooks.RevealGroup(reveal);
            else if (activate != null) hooks.Activate(activate);
        }

        private static IEnumerable<Button> FlatButtons(Panel list)
        {
            foreach (var child in list.Children)
            {
                if (child is Button b) yield return b;
                else if (child is Grid g && g.Children.Count > 0 && g.Children[0] is Button main) yield return main;
            }
        }
    }
}
