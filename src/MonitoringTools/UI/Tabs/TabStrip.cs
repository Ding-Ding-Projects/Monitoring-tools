using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using MonitoringTools.Core;
using static MonitoringTools.UI.Tabs.TabText;

namespace MonitoringTools.UI.Tabs
{
    /// <summary>
    /// The control centre's tab strip. Each open tab is one page of the control centre. The strip docks left (default),
    /// top, right or bottom, and keeps its open tabs, order, pins, groups, collapsed state and dock edge in settings.
    /// </summary>
    internal sealed class TabStrip
    {
        public const string DragFormat = "mt.tab";
        public const string PinIcon = "M9 4h6l-1 6 3 3v2H7v-2l3-3zM12 15v6";

        private static readonly (string Name, string Key)[] Colours =
        {
            ("Primary", "B.Mint"), ("Violet", "B.Violet"), ("Amber", "B.Amber"), ("Tertiary", "B.OnTertiaryContainer"), ("Alert", "B.Danger"),
        };

        private static readonly string[] Docks = { "left", "top", "right", "bottom" };

        /// <summary>Raised when something outside the strip changes the dock edge (for example a settings control).</summary>
        public static event Action DockRequested;

        private readonly ControlCentreWindow _w;
        private readonly IReadOnlyList<PageEntry> _pages;
        private readonly TabListPanel _panel = new TabListPanel();
        private readonly Dictionary<string, StripTab> _tabViews = new Dictionary<string, StripTab>();
        private readonly Dictionary<string, GroupHeader> _headers = new Dictionary<string, GroupHeader>();
        private readonly List<TextBlock> _actionLabels = new List<TextBlock>();
        private Button _newBtn, _moreBtn, _optBtn;
        private bool _compact;
        private string _active;
        private string _lookSignature;
        private readonly List<(Button Button, TextBlock Label, string Name, string Keys)> _actionDefs = new List<(Button, TextBlock, string, string)>();

        private static Settings S => Settings.Current;

        public TabStrip(ControlCentreWindow w, IReadOnlyList<PageEntry> pages)
        {
            _w = w; _pages = pages;
            Normalize();
            _active = S.ActiveTab;
            w.TabScroll.Content = _panel;
            BuildActions();
            w.TabHost.MouseRightButtonUp += (s, e) => { e.Handled = true; OpenStripMenu(e.GetPosition(w)); };
            w.SizeChanged += (s, e) =>
            {
                var c = ComputeCompact();
                if (c != _compact) { _compact = c; ApplyDock(); Rebuild(); }
            };
            w.TabScroll.SizeChanged += (s, e) => UpdateOverflow();
            w.TabScroll.ScrollChanged += (s, e) => UpdateOverflow();
            w.TabScroll.PreviewMouseWheel += (s, e) =>
            {
                if (IsVertical) return;
                w.TabScroll.ScrollToHorizontalOffset(w.TabScroll.HorizontalOffset - e.Delta);
                e.Handled = true;
            };
            w.PreviewKeyDown += OnWindowKey;
            DockRequested += OnDockRequested;
            Appearance.Changed += OnLook;
            Language.Changed += OnLanguage;
            ElementStyles.Changed += OnStyles;
            w.Closed += (s, e) =>
            {
                DockRequested -= OnDockRequested;
                Appearance.Changed -= OnLook;
                Language.Changed -= OnLanguage;
                ElementStyles.Changed -= OnStyles;
                if (S.ActiveTab != _active) { S.ActiveTab = _active; Settings.Save(); }
            };
            _compact = ComputeCompact();
            ApplyDock();
            Rebuild();
        }

        // Density, language and per-tab looks all rebuild the strip, which re-applies every saved look.
        private void OnLook() { ApplyDock(); Rebuild(); }

        private void OnLanguage()
        {
            foreach (var d in _actionDefs)
            {
                d.Label.Text = T(d.Name);
                d.Button.ToolTip = d.Keys == null ? T(d.Name) : T(d.Name) + " (" + d.Keys + ")";
                AutomationProperties.SetName(d.Button, T(d.Name));
            }
            Rebuild();
            UpdateOverflow();
        }

        private void OnStyles()
        {
            if (LookSignature() == _lookSignature) return;
            Rebuild();
        }

        // -------------------------------------------------------------- per-tab and per-group looks

        public static string TabKey(string id) => "tab:" + id;
        public static string GroupKey(string name) => "group:" + name;

        private static string LookSignature()
        {
            var parts = new List<string>();
            foreach (var kv in S.ElementStyles ?? new Dictionary<string, ElementStyle>())
                if (kv.Key.StartsWith("tab:", StringComparison.Ordinal) || kv.Key.StartsWith("group:", StringComparison.Ordinal))
                    parts.Add(kv.Key + "=" + System.Text.Json.JsonSerializer.Serialize(kv.Value));
            parts.Sort(StringComparer.Ordinal);
            return string.Join("\n", parts);
        }

        /// <summary>Applies a saved look to one view: the box (background, corners, padding, border) and its text.</summary>
        private static void ApplyLook(Border box, TextBlock text, ElementStyle st)
        {
            if (st == null || st.IsEmpty) return;
            if (ElementStyles.TryColour(st.Background, out var bg)) box.Background = new SolidColorBrush(bg);
            if (st.CornerRadius is double r) box.CornerRadius = new CornerRadius(Math.Max(0, r));
            if (st.Padding is double p) box.Padding = new Thickness(Math.Max(0, p));
            if (st.BorderThickness is double bt) box.BorderThickness = new Thickness(Math.Max(0, bt));
            if (ElementStyles.TryColour(st.BorderColour, out var bc)) box.BorderBrush = new SolidColorBrush(bc);
            if (text == null) return;
            if (!string.IsNullOrWhiteSpace(st.FontFamily)) text.FontFamily = new FontFamily(st.FontFamily);
            if (st.FontSize is double sz && sz > 0) text.FontSize = sz;
            if (st.FontWeight is int w) text.FontWeight = FontWeight.FromOpenTypeWeight(Math.Clamp(w, 1, 999));
            if (st.Italic is bool it) text.FontStyle = it ? FontStyles.Italic : FontStyles.Normal;
            if (ElementStyles.TryColour(st.Foreground, out var fg)) text.Foreground = new SolidColorBrush(fg);
        }

        /// <summary>Opens the appearance editor for one tab. The look is saved under "tab:" plus the tab id.</summary>
        public void EditTabAppearance(string id)
        {
            if (_tabViews.TryGetValue(id, out var v)) AppearanceEditorWindow.Open(_w, v, TabKey(id));
        }

        /// <summary>Opens the appearance editor for one group header. The look is saved under "group:" plus the group name.</summary>
        public void EditGroupAppearance(string gid)
        {
            var g = Group(gid);
            if (g != null && _headers.TryGetValue(gid, out var h)) AppearanceEditorWindow.Open(_w, h, GroupKey(g.Name));
        }

        // -------------------------------------------------------------- settings control used by the appearance page

        /// <summary>A labelled left, top, right, bottom choice for the dock edge. It changes the live strip at once and persists.</summary>
        public static FrameworkElement DockChoice()
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            sp.Children.Add(new TextBlock { Text = T("Tab strip position"), Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(new TextBlock { Text = T("Which edge of the control centre the tabs sit on. Default: left."), Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 8) });
            var wrap = new WrapPanel();
            var buttons = new List<(Button B, string V)>();
            void Paint()
            {
                foreach (var (b, v) in buttons)
                {
                    bool on = v == Normalise(S.TabDock);
                    b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                    b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                    b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                }
            }
            foreach (var d in Docks)
            {
                var v = d;
                var b = Ui.Button(T(char.ToUpperInvariant(d[0]) + d.Substring(1)), null, "Btn.Chip");
                b.Click += (s, e) => { S.TabDock = v; Settings.Save(); Paint(); DockRequested?.Invoke(); };
                buttons.Add((b, d));
                wrap.Children.Add(b);
            }
            Paint();
            sp.Children.Add(wrap);
            Search.SearchScope.SetItem(sp, "setting choice tab strip position dock edge left top right bottom tabs");
            return sp;
        }

        private static string Normalise(string d) => Docks.Contains(d) ? d : "left";
        private string Dock => Normalise(S.TabDock);
        private bool IsVertical => Dock == "left" || Dock == "right";

        // -------------------------------------------------------------- state

        public string StartupTab => S.OpenTabs.Contains(S.ActiveTab) ? S.ActiveTab : "overview";

        private void Normalize()
        {
            S.OpenTabs ??= new List<string>();
            S.PinnedTabs ??= new List<string>();
            S.TabGroups ??= new List<TabGroupState>();
            S.TabGroupOf ??= new Dictionary<string, string>();
            var valid = new HashSet<string>(_pages.Select(p => p.Id));
            S.OpenTabs = S.OpenTabs.Where(valid.Contains).Distinct().ToList();
            OpenUnseenPages();
            if (S.OpenTabs.Count == 0) S.OpenTabs.Add("overview");
            S.PinnedTabs = S.PinnedTabs.Where(S.OpenTabs.Contains).Distinct().ToList();
            S.TabGroups = S.TabGroups.Where(g => g != null && !string.IsNullOrEmpty(g.Id)).GroupBy(g => g.Id).Select(g => g.First()).ToList();
            foreach (var g in S.TabGroups) if (string.IsNullOrWhiteSpace(g.Name)) g.Name = "Group";
            foreach (var k in S.TabGroupOf.Keys.ToList())
                if (!S.OpenTabs.Contains(k) || S.PinnedTabs.Contains(k) || !S.TabGroups.Any(g => g.Id == S.TabGroupOf[k])) S.TabGroupOf.Remove(k);
            S.TabDock = Normalise(S.TabDock);
            if (!S.OpenTabs.Contains(S.ActiveTab)) S.ActiveTab = S.OpenTabs[0];
        }

        /// <summary>
        /// Every page starts open so nothing is hidden behind New tab. A page the strip has never offered opens once: on a
        /// new install that is every page, after an update it is just the new ones, and a tab the person closed stays closed.
        /// A strip still on the old single-tab default is laid out in page order.
        /// </summary>
        private void OpenUnseenPages()
        {
            S.KnownTabs ??= new List<string>();
            if (TabDefaults.OpenUnseen(S.OpenTabs, S.KnownTabs, _pages.Select(p => p.Id).ToList())) Settings.Save();
        }

        /// <summary>Opens every page that is not open yet, in page order after the open ones.</summary>
        public void OpenEveryPage()
        {
            var missing = _pages.Select(p => p.Id).Where(id => !S.OpenTabs.Contains(id)).ToList();
            if (missing.Count == 0) { FocusTab(_active); return; }
            S.OpenTabs.AddRange(missing);
            Settings.Save();
            Rebuild();
            FocusTab(_active);
        }

        public bool AllPagesOpen => _pages.All(p => S.OpenTabs.Contains(p.Id));

        private bool IsPinned(string id) => S.PinnedTabs.Contains(id);
        private string GroupIdOf(string id) => S.TabGroupOf.TryGetValue(id, out var g) ? g : null;
        private TabGroupState Group(string gid) => gid == null ? null : S.TabGroups.FirstOrDefault(g => g.Id == gid);
        private PageEntry Page(string id) => _pages.FirstOrDefault(p => p.Id == id);
        private IEnumerable<string> Members(string gid) => S.OpenTabs.Where(t => !IsPinned(t) && GroupIdOf(t) == gid);
        private bool GroupShown(TabGroupState g) => !g.Collapsed || Members(g.Id).Contains(_active);

        private TabInfo Info(string id)
        {
            var p = Page(id);
            var g = Group(GroupIdOf(id));
            return new TabInfo(id, T(p.Label), p.Icon, IsPinned(id), g?.Id, g?.Name, id == _active);
        }

        private IReadOnlyList<TabInfo> AllInfos()
        {
            var order = S.OpenTabs.Where(IsPinned).Concat(S.OpenTabs.Where(t => !IsPinned(t) && GroupIdOf(t) == null))
                .Concat(S.TabGroups.SelectMany(g => Members(g.Id)));
            return order.Select(Info).ToList();
        }

        private IReadOnlyList<(TabGroupState, int)> GroupList() => S.TabGroups.Select(g => (g, Members(g.Id).Count())).ToList();

        /// <summary>Ids of the tabs currently shown in the strip, in display order.</summary>
        private List<string> VisibleIds()
        {
            var ids = new List<string>();
            ids.AddRange(S.OpenTabs.Where(IsPinned));
            ids.AddRange(S.OpenTabs.Where(t => !IsPinned(t) && GroupIdOf(t) == null));
            foreach (var g in S.TabGroups) if (GroupShown(g)) ids.AddRange(Members(g.Id));
            return ids;
        }

        // -------------------------------------------------------------- show, activate, close

        /// <summary>Called by the window whenever a page is shown: opens its tab when needed and marks it active. Does not navigate.</summary>
        public void Show(string id)
        {
            if (Page(id) == null) id = "overview";
            bool structural = false;
            if (!S.OpenTabs.Contains(id)) { S.OpenTabs.Add(id); structural = true; }
            bool changed = _active != id;
            _active = id;
            S.ActiveTab = id;
            if (structural) Settings.Save();
            if (structural || changed) Rebuild();
            if (_tabViews.TryGetValue(id, out var v)) v.BringIntoView();
        }

        public void Activate(string id) => _w.Navigate(id);

        public void CloseTab(string id) => CloseMany(new[] { id });

        public void CloseMany(IEnumerable<string> ids)
        {
            var set = new HashSet<string>(ids.Where(S.OpenTabs.Contains));
            if (set.Count == 0) return;
            var order = VisibleIds();
            string next = null;
            if (set.Contains(_active))
            {
                int idx = Math.Max(0, order.IndexOf(_active));
                var rest = order.Where(x => !set.Contains(x)).ToList();
                var after = order.Skip(idx + 1).FirstOrDefault(x => !set.Contains(x));
                var before = order.Take(idx).LastOrDefault(x => !set.Contains(x));
                next = after ?? before ?? (rest.Count > 0 ? rest[0] : null);
            }
            S.OpenTabs.RemoveAll(set.Contains);
            S.PinnedTabs.RemoveAll(set.Contains);
            foreach (var k in set) S.TabGroupOf.Remove(k);
            if (S.OpenTabs.Count == 0) { S.OpenTabs.Add("overview"); next = "overview"; }
            if (next == null && !S.OpenTabs.Contains(_active)) next = S.OpenTabs[0];
            Settings.Save();
            Rebuild();
            if (next != null) _w.Navigate(next);
        }

        public void TogglePin(string id)
        {
            if (!S.OpenTabs.Contains(id)) return;
            if (!S.PinnedTabs.Remove(id)) { S.PinnedTabs.Add(id); S.TabGroupOf.Remove(id); }
            Settings.Save();
            Rebuild();
            FocusTab(id);
        }

        public void Move(string id, int delta)
        {
            if (!S.OpenTabs.Contains(id)) return;
            bool pinned = IsPinned(id);
            var gid = GroupIdOf(id);
            var region = S.OpenTabs.Where(t => IsPinned(t) == pinned && (pinned || GroupIdOf(t) == gid)).ToList();
            int i = region.IndexOf(id), j = i + delta;
            if (j < 0 || j >= region.Count) return;
            int a = S.OpenTabs.IndexOf(region[i]), b = S.OpenTabs.IndexOf(region[j]);
            (S.OpenTabs[a], S.OpenTabs[b]) = (S.OpenTabs[b], S.OpenTabs[a]);
            Settings.Save();
            Rebuild();
            FocusTab(id);
        }

        // -------------------------------------------------------------- drag and drop

        public bool CanDrop(string source, string target) => source != target && S.OpenTabs.Contains(source) && S.OpenTabs.Contains(target) && IsPinned(source) == IsPinned(target);

        public void DropOn(string source, string target)
        {
            if (!CanDrop(source, target)) return;
            var list = S.OpenTabs;
            int si = list.IndexOf(source), oi = list.IndexOf(target);
            list.RemoveAt(si);
            int ti = list.IndexOf(target);
            list.Insert(si < oi ? ti + 1 : ti, source);
            var gid = IsPinned(source) ? null : GroupIdOf(target);
            if (gid == null) S.TabGroupOf.Remove(source); else S.TabGroupOf[source] = gid;
            Settings.Save();
            Rebuild();
            FocusTab(source);
        }

        public bool CanDropOnGroup(string source) => S.OpenTabs.Contains(source) && !IsPinned(source);

        public void DropOnGroup(string source, string gid)
        {
            if (!CanDropOnGroup(source) || Group(gid) == null) return;
            S.TabGroupOf[source] = gid;
            Settings.Save();
            Rebuild();
            FocusTab(source);
        }

        // -------------------------------------------------------------- groups

        private TabGroupState NewGroup(string name)
        {
            var g = new TabGroupState { Name = name, Colour = Colours[S.TabGroups.Count % Colours.Length].Key };
            S.TabGroups.Add(g);
            return g;
        }

        public void ToggleCollapse(string gid)
        {
            var g = Group(gid);
            if (g == null) return;
            g.Collapsed = !g.Collapsed;
            Settings.Save();
            Rebuild();
        }

        public void RenameGroup(string gid)
        {
            var g = Group(gid);
            if (g == null) return;
            var key = "g:" + gid;
            var name = TabDialogs.Prompt(_w, T("Rename group"), T("Group name"), g.Name, T("Rename"));
            if (name != null) { g.Name = name; Settings.Save(); Rebuild(); }
            RestoreFocus(key);
        }

        private void ChooseColour(string gid)
        {
            var g = Group(gid);
            if (g == null) return;
            var c = TabDialogs.Colour(_w, g.Colour, Colours);
            if (c != null) { g.Colour = c; Settings.Save(); Rebuild(); }
            RestoreFocus("g:" + gid);
        }

        private void RemoveGroup(string gid)
        {
            foreach (var k in S.TabGroupOf.Where(kv => kv.Value == gid).Select(kv => kv.Key).ToList()) S.TabGroupOf.Remove(k);
            S.TabGroups.RemoveAll(g => g.Id == gid);
            Settings.Save();
            Rebuild();
        }

        private void NewGroupFlow(string tabId)
        {
            var name = TabDialogs.Prompt(_w, T("New group"), T("Group name"), "", T("Create group"));
            if (name == null) { if (tabId != null) FocusTab(tabId); return; }
            var g = NewGroup(name);
            if (tabId != null && !IsPinned(tabId)) S.TabGroupOf[tabId] = g.Id;
            Settings.Save();
            Rebuild();
            if (tabId != null) FocusTab(tabId); else FocusHeader(g.Id);
        }

        private void MoveIntoGroupFlow(string tabId)
        {
            if (IsPinned(tabId)) return;
            var choice = TabDialogs.MovePicker(_w, T(Page(tabId).Label), GroupList());
            if (choice == null) { FocusTab(tabId); return; }
            string gid = choice.GroupId;
            if (gid == null) gid = NewGroup(choice.NewGroupName).Id;
            S.TabGroupOf[tabId] = gid;
            Settings.Save();
            Rebuild();
            FocusTab(tabId);
        }

        private void RemoveFromGroup(string tabId)
        {
            S.TabGroupOf.Remove(tabId);
            Settings.Save();
            Rebuild();
            FocusTab(tabId);
        }

        // -------------------------------------------------------------- dialogs

        public void OpenCatalog()
        {
            var chosen = TabDialogs.Catalog(_w, _pages, new HashSet<string>(S.OpenTabs));
            if (chosen != null) { Activate(chosen); FocusTab(chosen); }
            else _newBtn?.Focus();
        }

        private TabDialogs.SearchHooks Hooks() => new TabDialogs.SearchHooks(AllInfos, GroupList, id => { Activate(id); FocusTab(id); }, FocusHeader, TogglePin, CloseTab);

        public void SearchStrip() => TabDialogs.Search(_w, TabDialogs.SearchMode.Strip, null, Dock, Hooks());
        public void SearchMaster() => TabDialogs.Search(_w, TabDialogs.SearchMode.Master, null, Dock, Hooks());
        public void SearchGroupNames() => TabDialogs.Search(_w, TabDialogs.SearchMode.GroupNames, null, Dock, Hooks());
        public void SearchGroup(string gid) { var g = Group(gid); if (g != null) TabDialogs.Search(_w, TabDialogs.SearchMode.Group, g, Dock, Hooks()); }

        public void BulkClose(bool containing)
        {
            var ids = TabDialogs.BulkClose(_w, containing, AllInfos());
            if (ids != null && ids.Count > 0) CloseMany(ids);
            else FocusTab(_active);
        }

        private void CloseOthers(string id) => CloseMany(S.OpenTabs.Where(t => t != id && !IsPinned(t)).ToList());

        // -------------------------------------------------------------- menus

        private IEnumerable<ElementMenu.Item> DockItems() =>
            Docks.Where(d => d != Dock).Select(d => new ElementMenu.Item(T("Dock tabs to the " + d), Icons.Drag, null, () => SetDock(d)));

        private void SetDock(string d)
        {
            S.TabDock = d;
            Settings.Save();
            ApplyDock();
            Rebuild();
        }

        private IEnumerable<ElementMenu.Item> SharedItems()
        {
            yield return new ElementMenu.Item(T("Search this tab strip…"), Icons.Search, "Ctrl+Shift+E", SearchStrip);
            yield return new ElementMenu.Item(T("Search all open tabs…"), Icons.Search, "Ctrl+Shift+A", SearchMaster);
            yield return new ElementMenu.Item(T("Search tab groups…"), Icons.Search, "Ctrl+Shift+G", SearchGroupNames);
            yield return new ElementMenu.Item(T("Close tabs containing text…"), Icons.Trash, null, () => BulkClose(true));
            yield return new ElementMenu.Item(T("Close tabs not containing text…"), Icons.Trash, null, () => BulkClose(false));
            foreach (var d in DockItems()) yield return d;
        }

        public void OpenTabMenu(StripTab t, Point at)
        {
            var id = t.TabId;
            bool pinned = IsPinned(id);
            bool horizontal = !IsVertical;
            var items = new List<ElementMenu.Item>
            {
                new ElementMenu.Item(T(pinned ? "Unpin tab" : "Pin tab"), Icons.Shield, "Ctrl+Shift+P", () => TogglePin(id)),
                new ElementMenu.Item(T(horizontal ? "Move tab earlier" : "Move tab up"), Icons.Drag, horizontal ? "Ctrl+Shift+Left" : "Ctrl+Shift+Up", () => Move(id, -1)),
                new ElementMenu.Item(T(horizontal ? "Move tab later" : "Move tab down"), Icons.Drag, horizontal ? "Ctrl+Shift+Right" : "Ctrl+Shift+Down", () => Move(id, 1)),
            };
            if (!pinned) items.Add(new ElementMenu.Item(T("Move… into group…"), Icons.Folder, null, () => MoveIntoGroupFlow(id)));
            if (GroupIdOf(id) != null)
            {
                items.Add(new ElementMenu.Item(T("Remove from group"), Icons.Folder, null, () => RemoveFromGroup(id)));
                var gid = GroupIdOf(id);
                items.Add(new ElementMenu.Item(T("Search this group…"), Icons.Search, null, () => SearchGroup(gid)));
            }
            if (!pinned) items.Add(new ElementMenu.Item(T("New group with this tab…"), Icons.Plus, null, () => NewGroupFlow(id)));
            items.Add(new ElementMenu.Item(T("Edit tab appearance…"), Icons.Palette, null, () => EditTabAppearance(id)));
            items.Add(new ElementMenu.Item(T("Close tab"), Icons.Close, "Ctrl+W", () => CloseTab(id)));
            items.Add(new ElementMenu.Item(T("Close other tabs"), Icons.Close, null, () => CloseOthers(id)));
            items.AddRange(SharedItems());
            ElementMenu.Open(_w, t, _ => items, at);
        }

        /// <summary>Right-click on a tab: the menu, or the appearance editor straight away when Shift is held.</summary>
        public void OpenTabMenuMouse(StripTab t, Point at)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) EditTabAppearance(t.TabId);
            else OpenTabMenu(t, at);
        }

        /// <summary>Right-click on a group header: the menu, or the appearance editor straight away when Shift is held.</summary>
        public void OpenGroupMenuMouse(GroupHeader h, Point at)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) EditGroupAppearance(h.GroupId);
            else OpenGroupMenu(h, at);
        }

        public void OpenGroupMenu(GroupHeader h, Point at)
        {
            var gid = h.GroupId;
            var g = Group(gid);
            if (g == null) return;
            var items = new List<ElementMenu.Item>
            {
                new ElementMenu.Item(T(g.Collapsed ? "Expand group" : "Collapse group"), Icons.Folder, "Enter", () => ToggleCollapse(gid)),
                new ElementMenu.Item(T("Rename group…"), Icons.Text, "F2", () => RenameGroup(gid)),
                new ElementMenu.Item(T("Group colour…"), Icons.Palette, null, () => ChooseColour(gid)),
                new ElementMenu.Item(T("Edit group appearance…"), Icons.Palette, null, () => EditGroupAppearance(gid)),
                new ElementMenu.Item(T("Search this group…"), Icons.Search, null, () => SearchGroup(gid)),
                new ElementMenu.Item(T("Remove group (keeps its tabs)"), Icons.Trash, null, () => RemoveGroup(gid), Danger: true),
            };
            items.AddRange(SharedItems());
            ElementMenu.Open(_w, h, _ => items, at);
        }

        public void OpenStripMenu(Point at)
        {
            var items = new List<ElementMenu.Item>
            {
                new ElementMenu.Item(T("New tab…"), Icons.Plus, "Ctrl+T", OpenCatalog),
                new ElementMenu.Item(T("New group…"), Icons.Folder, null, () => NewGroupFlow(null)),
            };
            if (!AllPagesOpen) items.Insert(1, new ElementMenu.Item(T("Open every page"), Icons.Lines, null, OpenEveryPage));
            items.AddRange(SharedItems());
            ElementMenu.Open(_w, _w.TabHost, _ => items, at);
        }

        // -------------------------------------------------------------- keyboard

        private string FocusedOrActive() => Keyboard.FocusedElement is StripTab st ? st.TabId : _active;

        private void OnWindowKey(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            var mods = Keyboard.Modifiers;
            var k = e.Key == Key.System ? e.SystemKey : e.Key;
            bool inText = Keyboard.FocusedElement is TextBox;
            if (mods == ModifierKeys.Control)
            {
                if (k == Key.T) OpenCatalog();
                else if (k == Key.W) CloseTab(_active);
                else if (k == Key.Tab) Cycle(1);
                else return;
                e.Handled = true;
            }
            else if (mods == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (k == Key.Tab) Cycle(-1);
                else if (k == Key.P && !inText) TogglePin(FocusedOrActive());
                else if (k == Key.A) SearchMaster();
                else if (k == Key.E) SearchStrip();
                else if (k == Key.G) SearchGroupNames();
                else if (!inText && (IsVertical ? k == Key.Up : k == Key.Left)) Move(FocusedOrActive(), -1);
                else if (!inText && (IsVertical ? k == Key.Down : k == Key.Right)) Move(FocusedOrActive(), 1);
                else return;
                e.Handled = true;
            }
        }

        private void Cycle(int delta)
        {
            var ids = VisibleIds();
            if (ids.Count < 2) return;
            int i = Math.Max(0, ids.IndexOf(_active));
            Activate(ids[(i + delta + ids.Count) % ids.Count]);
        }

        // -------------------------------------------------------------- focus helpers

        private List<FrameworkElement> Focusables() => _panel.Children.OfType<FrameworkElement>().Where(c => c is StripTab || c is GroupHeader).ToList();

        public void FocusNeighbour(string id, int delta) { if (_tabViews.TryGetValue(id, out var v)) FocusNeighbourOf(v, delta); }

        public void FocusNeighbourOf(FrameworkElement el, int delta)
        {
            var list = Focusables();
            int i = list.IndexOf(el);
            if (i < 0) return;
            var n = Math.Max(0, Math.Min(list.Count - 1, i + delta));
            list[n].Focus();
            list[n].BringIntoView();
        }

        public void FocusEdge(bool end)
        {
            var list = Focusables();
            if (list.Count == 0) return;
            var f = end ? list[list.Count - 1] : list[0];
            f.Focus();
            f.BringIntoView();
        }

        private void FocusTab(string id) { if (id != null && _tabViews.TryGetValue(id, out var v)) v.Focus(); }
        private void FocusHeader(string gid) { if (_headers.TryGetValue(gid, out var h)) { h.BringIntoView(); h.Focus(); } }

        private string FocusKey() => Keyboard.FocusedElement is StripTab t ? "t:" + t.TabId : Keyboard.FocusedElement is GroupHeader g ? "g:" + g.GroupId : null;

        private void RestoreFocus(string key)
        {
            if (key == null) return;
            if (key.StartsWith("t:")) FocusTab(key.Substring(2));
            else if (key.StartsWith("g:")) FocusHeader(key.Substring(2));
        }

        private void UpdateRoving()
        {
            StripTab stop = _tabViews.TryGetValue(_active, out var a) ? a : null;
            foreach (var f in Focusables()) KeyboardNavigation.SetIsTabStop(f, false);
            if (stop != null) KeyboardNavigation.SetIsTabStop(stop, true);
            else { var first = Focusables().FirstOrDefault(); if (first != null) KeyboardNavigation.SetIsTabStop(first, true); }
        }

        // -------------------------------------------------------------- layout

        private bool ComputeCompact() => IsVertical && _w.ActualWidth > 0 && _w.ActualWidth < 1000;

        private void OnDockRequested() { _compact = ComputeCompact(); ApplyDock(); Rebuild(); }

        private void BuildActions()
        {
            _newBtn = ActionButton(Icons.Plus, "New tab", "Ctrl+T", OpenCatalog);
            _moreBtn = ActionButton(Icons.Lines, "More tabs", null, OpenMoreMenu);
            _optBtn = ActionButton(Icons.Drag, "Tab options", null, () => OpenStripMenu(OptionsAnchor()));
            _moreBtn.Visibility = Visibility.Collapsed;
            _w.ActionBar.Children.Add(_newBtn);
            _w.ActionBar.Children.Add(_moreBtn);
            _w.ActionBar.Children.Add(_optBtn);
        }

        private Point OptionsAnchor() => _optBtn.TranslatePoint(new Point(0, 0), _w);

        private Button ActionButton(string icon, string name, string keys, Action run)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var g = Ui.Icon(icon, Ui.Res("B.TextSoft"), 16);
            g.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "B.TextSoft");
            sp.Children.Add(new ContentControl { Content = g, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Focusable = false });
            var label = new TextBlock { Text = T(name), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(label);
            _actionLabels.Add(label);
            var b = new Button { Style = Ui.Style("Btn.Row"), Content = sp, Height = Appearance.Row(40), HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2) };
            b.ToolTip = keys == null ? T(name) : T(name) + " (" + keys + ")";
            AutomationProperties.SetName(b, T(name));
            _actionDefs.Add((b, label, name, keys));
            b.Click += (s, e) => run();
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            return b;
        }

        private void ApplyDock()
        {
            var d = Dock;
            bool v = IsVertical;
            var host = _w.TabHost;
            var sg = _w.StripGrid;
            Grid.SetRowSpan(host, 1);
            Grid.SetColumnSpan(host, 1);
            switch (d)
            {
                case "right": Grid.SetRow(host, 1); Grid.SetColumn(host, 2); break;
                case "top": Grid.SetRow(host, 0); Grid.SetColumn(host, 0); Grid.SetColumnSpan(host, 3); break;
                case "bottom": Grid.SetRow(host, 2); Grid.SetColumn(host, 0); Grid.SetColumnSpan(host, 3); break;
                default: Grid.SetRow(host, 1); Grid.SetColumn(host, 0); break;
            }
            _w.ContentCaptionRow.Height = d == "top" ? new GridLength(0) : new GridLength(44);
            host.Width = v ? (_compact ? 76 : 264) : double.NaN;   // wide enough for the longest page name beside the open tab's buttons
            sg.RowDefinitions.Clear();
            sg.ColumnDefinitions.Clear();
            var brand = _w.BrandPanel; var scroll = _w.TabScroll; var actions = _w.ActionBar; var foot = _w.FooterPanel;
            if (v)
            {
                sg.Margin = new Thickness(_compact ? 10 : 14, 14, _compact ? 10 : 14, 16);
                sg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                sg.RowDefinitions.Add(new RowDefinition());
                sg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                sg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Place(brand, 0, 0); Place(scroll, 1, 0); Place(actions, 2, 0); Place(foot, 3, 0);
                brand.Visibility = Visibility.Visible;
                _w.AppName.Visibility = _compact ? Visibility.Collapsed : Visibility.Visible;
                brand.Margin = new Thickness(_compact ? 10 : 8, 4, 0, 16);
                foot.Visibility = _compact ? Visibility.Collapsed : Visibility.Visible;
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroll.Margin = new Thickness(0, 0, 0, 12);
                actions.Orientation = Orientation.Vertical;
            }
            else
            {
                sg.Margin = new Thickness(12, 6, d == "top" ? 150 : 12, 6);
                sg.ColumnDefinitions.Add(new ColumnDefinition());
                sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Place(scroll, 0, 0); Place(actions, 0, 1);
                brand.Visibility = Visibility.Collapsed;
                foot.Visibility = Visibility.Collapsed;
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroll.Margin = new Thickness(0);
                actions.Orientation = Orientation.Horizontal;
            }
            foreach (var l in _actionLabels) l.Visibility = v && !_compact ? Visibility.Visible : Visibility.Collapsed;
            foreach (var b in new[] { _newBtn, _moreBtn, _optBtn })
            {
                b.Height = Appearance.Row(40);
                b.Padding = v && !_compact ? new Thickness(16, 0, 16, 0) : new Thickness(0);
                b.Width = v && !_compact ? double.NaN : 44;
                b.HorizontalAlignment = v && !_compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
                b.HorizontalContentAlignment = v && !_compact ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                b.Margin = v ? new Thickness(0, 0, 0, 2) : new Thickness(4, 0, 0, 0);
                if (b.Content is StackPanel sp && sp.Children[0] is ContentControl cc) cc.Margin = new Thickness(0, 0, v && !_compact ? 12 : 0, 0);
            }
            _panel.Orientation = v ? Orientation.Vertical : Orientation.Horizontal;
            AutomationProperties.SetName(_panel, T("Open tabs"));
        }

        private static void Place(UIElement e, int row, int col) { Grid.SetRow(e, row); Grid.SetColumn(e, col); }

        private void Rebuild()
        {
            var focus = FocusKey();
            Normalize();
            bool v = IsVertical;
            _panel.Orientation = v ? Orientation.Vertical : Orientation.Horizontal;
            _panel.Children.Clear();
            _tabViews.Clear();
            _headers.Clear();
            int size = VisibleIds().Count, pos = 0;

            void AddTab(string id)
            {
                var g = Group(GroupIdOf(id));
                var view = new StripTab(this, Info(id), v, _compact, g?.Colour, ++pos, size);
                WindowChrome.SetIsHitTestVisibleInChrome(view, true);
                ApplyLook(view, view.LabelBlock, ElementStyles.Get(TabKey(id)));
                _panel.Children.Add(view);
                _tabViews[id] = view;
            }
            void AddHeader(TabGroupState g)
            {
                var h = new GroupHeader(this, new TabGroupInfo(g.Id, g.Name, g.Colour, g.Collapsed, Members(g.Id).Count()), v, _compact);
                WindowChrome.SetIsHitTestVisibleInChrome(h, true);
                ApplyLook(h, h.NameBlock, ElementStyles.Get(GroupKey(g.Name)));
                _panel.Children.Add(h);
                _headers[g.Id] = h;
            }
            void AddSeparator()
            {
                var sep = new Border { IsHitTestVisible = false, Margin = v ? new Thickness(8, 4, 8, 6) : new Thickness(6, 8, 10, 8) };
                if (v) sep.Height = 1; else sep.Width = 1;
                sep.SetResourceReference(Border.BackgroundProperty, "B.Line");
                _panel.Children.Add(sep);
            }

            var pinned = S.OpenTabs.Where(IsPinned).ToList();
            foreach (var id in pinned) AddTab(id);
            var loose = S.OpenTabs.Where(t => !IsPinned(t) && GroupIdOf(t) == null).ToList();
            if (pinned.Count > 0 && (loose.Count > 0 || S.TabGroups.Count > 0)) AddSeparator();
            foreach (var id in loose) AddTab(id);
            foreach (var g in S.TabGroups)
            {
                AddHeader(g);
                if (GroupShown(g)) foreach (var id in Members(g.Id)) AddTab(id);
            }
            _lookSignature = LookSignature();
            UpdateRoving();
            RestoreFocus(focus);
            Language.Refresh(_panel);
            _w.Dispatcher.BeginInvoke(new Action(UpdateOverflow), DispatcherPriority.Loaded);
        }

        // -------------------------------------------------------------- overflow

        private List<StripTab> HiddenTabs()
        {
            var sv = _w.TabScroll;
            var hidden = new List<StripTab>();
            foreach (var t in _panel.Children.OfType<StripTab>())
            {
                try
                {
                    var r = t.TransformToAncestor(sv).TransformBounds(new Rect(0, 0, t.ActualWidth, t.ActualHeight));
                    bool out_ = IsVertical ? r.Top < -0.5 || r.Bottom > sv.ViewportHeight + 0.5 : r.Left < -0.5 || r.Right > sv.ViewportWidth + 0.5;
                    if (out_) hidden.Add(t);
                }
                catch (InvalidOperationException) { }
            }
            return hidden;
        }

        private void UpdateOverflow()
        {
            if (_moreBtn == null) return;
            var sv = _w.TabScroll;
            bool over = IsVertical ? sv.ExtentHeight > sv.ViewportHeight + 0.5 : sv.ExtentWidth > sv.ViewportWidth + 0.5;
            _moreBtn.Visibility = over ? Visibility.Visible : Visibility.Collapsed;
            if (over) AutomationProperties.SetName(_moreBtn, F("More tabs, {0} hidden", HiddenTabs().Count));
        }

        private void OpenMoreMenu()
        {
            var hidden = HiddenTabs();
            var items = hidden.Select(t => { var id = t.TabId; var p = Page(id); return new ElementMenu.Item(T(p.Label) + (id == _active ? " " + T("(active)") : ""), p.Icon, null, () => { Activate(id); FocusTab(id); }); }).ToList();
            if (items.Count == 0) items.Add(new ElementMenu.Item(T("All open tabs are visible; scroll the strip to move through them"), Icons.Help, null, () => { }));
            ElementMenu.Open(_w, _moreBtn, _ => items, _moreBtn.TranslatePoint(new Point(0, 0), _w));
        }
    }
}
