using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Ctrl+Shift+F everywhere in the app: every page, every setting (with its live control
    /// inline) and every action. Plain-text search by default, with an adjacent regex mode.
    /// Choosing a result teleports to the exact control and highlights it.
    /// </summary>
    public static class CommandPalette
    {
        public sealed class Item
        {
            public string Group, Title, Detail, Icon, Page;
            public bool LiteralDetail;   // the detail is the person's own text, such as a shortcut description
            public Action Run;
            public Func<FrameworkElement> Inline;   // live control rendered in the row
        }

        private static Window _open;
        public static Window Current => _open;
        public static Search.SearchField CurrentSearch => (_open as PaletteWindow)?.FindField;

        /// <summary>Opens the palette with another field's query, mode and options, to search every page.</summary>
        public static void OpenWith(Search.SearchState from, Window owner = null)
        {
            Open(owner);
            var to = CurrentSearch?.State;
            if (to == null) return;
            to.SetRegex(from.IsRegex);
            to.SetMatchCase(from.MatchCase);
            foreach (var (flag, _, _) in RegexKit.OptionDefs) to.SetOption(flag, (from.Options & flag) != 0);
            to.SetQuery(from.Query);
        }

        public static void Open(Window owner = null)
        {
            if (_open != null) { _open.Activate(); return; }
            var w = new PaletteWindow(Items());
            if (owner != null && owner.IsVisible) w.Owner = owner;
            _open = w;
            w.Closed += (s, e) => _open = null;
            w.Show();
        }

        /// <summary>Hook Ctrl+Shift+F on a window.</summary>
        public static void Attach(Window w)
        {
            w.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { e.Handled = true; Open(w); }
            };
        }

        /// <summary>
        /// The closed lock that guards a palette item, or null. A lock is keyed by a page and an element's visible text, so an item is
        /// guarded when a closed lock carries the item's title: on the item's own page, or on any page for a page or an action.
        /// </summary>
        public static LockEntry LockFor(Item it)
        {
            if (it == null || !Locks.Any) return null;
            var key = ElementMenu.KeyOfText(it.Title);
            if (key == null) return null;
            bool anyPage = it.Page == null || it.Group == "Pages";
            foreach (var l in Locks.All)
                if (l.Key == key && (anyPage || l.PageId == it.Page) && Locks.IsLocked(l.PageId, l.Key)) return l;
            return null;
        }

        private static void Go(string page, string label = null) => App.Current.OpenControlCentre(page, label);

        public static List<Item> Items()
        {
            var s = Settings.Current;
            var list = new List<Item>();
            void Page(string id, string title, string icon, string detail) =>
                list.Add(new Item { Group = "Pages", Title = title, Detail = detail, Icon = icon, Page = id, Run = () => Go(id) });
            void Toggle(string title, string page, Func<bool> get, Action<bool> set, string detail = null) =>
                list.Add(new Item
                {
                    Group = "Settings", Title = title, Detail = detail ?? "Control centre · " + page, Icon = Icons.Shield, Page = page,
                    Run = () => Go(page, title),
                    Inline = () =>
                    {
                        var cb = new CheckBox { Style = Ui.Style("Switch"), IsChecked = get(), Tag = Ui.Res("B.Mint"), Focusable = false };
                        AutomationProperties.SetName(cb, title);
                        cb.Checked += (o, e) => { set(true); Settings.Save(); App.Tray.Refresh(); };
                        cb.Unchecked += (o, e) => { set(false); Settings.Save(); App.Tray.Refresh(); };
                        return cb;
                    }
                });
            // A number setting is typed, never picked from presets, so its entry opens the page where the number field sits.
            void NumberSetting(string title, string page) =>
                list.Add(new Item { Group = "Settings", Title = title, Detail = "Control centre · " + page + " · type a number", Icon = Icons.Shield, Page = page, Run = () => Go(page, title) });
            void Act(string title, string icon, Action run, string detail = null, string group = "Actions", bool literalDetail = false) =>
                list.Add(new Item { Group = group, Title = title, Detail = detail, Icon = icon, Run = run, LiteralDetail = literalDetail });

            Page("overview", "Overview", Icons.Pulse, "Live latency, outages and activity");
            Page("activity", "Activity", Icons.Lines, "Everything the service has done");
            Page("history", "Clipboard history", Icons.Clipboard, "Recent copies, kept in memory only");
            Page("clipboard", "Clipboard settings", Icons.Text, "Toasts, Markdown, media folder");
            Page("shortcuts", "Shortcuts and scripts", Icons.Code, "Your :commands and the script editor");
            Page("network", "Network and DNS", Icons.Wifi, "Public IP history, DNS profiles, alerts");
            Page("game", "Game mode", Icons.Game, "Watched games and fullscreen");
            Page("conversion", "Conversion", Icons.Bolt, "Command playground, FFmpeg, formats");
            Page("minecraft", "Minecraft servers", Icons.Box, "Create and run servers here, in Docker or over SSH");
            Page("service", "Service", Icons.Shield, "Start-up, administrator rights, uninstall");
            Page("about", "About and updates", Icons.Globe, "Version, bundled tools, update check");
            Page("appearance", "Appearance", Icons.Palette, "Look, theme, colour, text size and motion");

            // The four looks switch from here at once, the way they do on the Appearance page.
            foreach (var look in Looks.All)
            {
                var l = look;
                Act("Look: " + l.Name + (Looks.Current == l ? " (in use)" : ""), Icons.Palette, () => ControlCentreWindow.ChooseLook(l), l.Description, "Settings");
            }

            Toggle("Clipboard toasts", "clipboard", () => s.ClipboardEnabled, v => s.ClipboardEnabled = v);
            Toggle("Keep open on hover", "clipboard", () => s.KeepOpenOnHover, v => s.KeepOpenOnHover = v);
            Toggle("Render Markdown", "clipboard", () => s.RenderMarkdown, v => s.RenderMarkdown = v);
            Toggle("Skip password managers", "clipboard", () => s.IgnoreSensitiveSources, v => s.IgnoreSensitiveSources = v);
            Toggle("Quick commands", "clipboard", () => s.QuickCommandsEnabled, v => s.QuickCommandsEnabled = v);
            NumberSetting("Toast duration", "clipboard");
            Toggle("Network alerts", "network", () => s.NetworkEnabled, v => s.NetworkEnabled = v);
            Toggle("Watch the public IP", "network", () => s.PublicIpEnabled, v => s.PublicIpEnabled = v);
            NumberSetting("Check every", "network");
            Toggle("Suppress while gaming", "game", () => s.GameModeEnabled, v => s.GameModeEnabled = v);
            Toggle("Any fullscreen app counts", "game", () => s.GameModeFullscreen, v => s.GameModeFullscreen = v);
            Toggle("Sound effects", "clipboard", () => s.SoundsEnabled, v => s.SoundsEnabled = v);
            Toggle("Start with Windows", "service", () => s.StartWithWindows, v => { s.StartWithWindows = v; ServiceHost.ApplyAutostart(); });
            NumberSetting("Volume", "clipboard");
            // School mode leaves personal wording out, so its entries are left out too.
            if (!Modes.Blocks(ModeFeature.PersonalVocabulary))
            {
                list.Add(new Item { Group = "Settings", Title = "Personal wording", Detail = PersonalWording.Active ? $"Loaded, {PersonalWording.Count} phrases" : "No file loaded", Icon = Icons.Text, Page = "service", Run = () => Go("service", "Personal wording") });
                Act("Choose a personal wording file", Icons.Text, () => WordingLayer.PickAndApply(App.Current.MainWindow));
                if (PersonalWording.Active) Act("Clear personal wording", Icons.Restart, PersonalWording.Clear);
            }

            // The ADHD switches and the Focus spotlight. Each one says what it does, as on the Modes page.
            Toggle("Focus spotlight", "modes", () => s.AdhdSpotlight, v => FocusSpotlight.Set(v), "Dim all but the card under focus or the mouse · Ctrl+Shift+Space");
            Toggle("Focus", "modes", () => s.AdhdFocus, v => { s.AdhdFocus = v; Modes.NotifyChanged(); }, "ADHD mode · hold non-urgent popups in History");
            Toggle("Low stimulation", "modes", () => s.AdhdLowStimulation, v => { s.AdhdLowStimulation = v; Modes.NotifyChanged(); }, "ADHD mode · no motion, only warnings make a sound");
            Toggle("Time awareness", "modes", () => s.AdhdTimeAwareness, v => { s.AdhdTimeAwareness = v; Modes.NotifyChanged(); }, "ADHD mode · elapsed time and a gentle reminder");
            NumberSetting("Reminder interval", "modes");
            Toggle("One thing at a time", "modes", () => s.AdhdOneThing, v => { s.AdhdOneThing = v; Modes.NotifyChanged(); }, "ADHD mode · one popup on screen at once");
            Toggle("Momentum", "modes", () => s.AdhdMomentum, v => { s.AdhdMomentum = v; Modes.NotifyChanged(); }, "ADHD mode · reopen on the page you left");

            Act("Quick conversions", Icons.Lines, () => Dialogs.ConversionsDialog.Open(""), "Every unit at once, each one copyable (Ctrl+Shift+Alt+U)", "Clipboard");
            Act("Clipboard history", Icons.Clipboard, () => HistoryPopup.Toggle(), "Every copy with a preview (" + Settings.Current.HotkeyHistory + ")", "Clipboard");
            Act("Clipboard settings", Icons.Clipboard, () => Dialogs.ClipboardSettingsDialog.Open(), "Kinds, lookups, the secret guard and shortcuts", "Clipboard");
            Act("Snippets", Icons.Code, () => Dialogs.SnippetsDialog.Open(), "Type a trigger anywhere and it becomes text", "Clipboard");
            Act("Transform recipes", Icons.Wand, () => Dialogs.RecipesDialog.Open(), "Chains of text changes in one press", "Clipboard");
            Act("Per-app rules", Icons.Shield, () => Dialogs.RulesDialog.Open(), "What happens to copies from a particular app", "Clipboard");
            Act("Paste queue", Icons.Lines, () => PasteQueue.Toggle(), "Copy several things, paste them in order (" + Settings.Current.HotkeyQueue + ")", "Clipboard");
            Act("Collect mode", Icons.Plus, ClipPower.ToggleCollect, "Every copy joins one note (" + Settings.Current.HotkeyCollect + ")", "Clipboard");
            Act("Undo the clipboard", Icons.Restart, ClipPower.Undo, "Put back what was there before (" + Settings.Current.HotkeyUndo + ")", "Clipboard");
            Act("Extensions", Icons.Wand, () => Dialogs.ExtensionsDialog.Open(), "Add kinds of copies and buttons, and choose what they may do", "Clipboard");
            Act("Test connection now", Icons.Wifi, () => App.Net.ProbeNow());
            Act("Check public IP now", Icons.Globe, async () => { await App.PublicIp.CheckAsync(); Go("network", "Public IP"); });
            Act("Release and renew IP", Icons.Bolt, async () => { await Elevated.RunAsync("ipconfig /release", "ipconfig /renew", "ipconfig /flushdns"); App.Net.ProbeNow(); });
            foreach (var p in s.DnsProfiles)
            {
                var prof = p;
                Act($"Switch DNS to {p.Name}", Icons.Globe, async () => { await DnsSwitcher.ApplyAsync(prof); App.Net.ProbeNow(); }, p.ToString());
            }
            Act("Snooze 1 hour", Icons.Clock, () => { App.SnoozeUntil = DateTime.Now.AddHours(1); App.Tray.Refresh(); });
            Act("Run setup again", Icons.Play, () => App.Current.ShowSetup());
            Act("Take the tour", Icons.Play, () => App.Current.ShowWelcome());
            Act("Preview the offline dialog", Icons.WifiOff, Demo.PreviewOffline);
            Act("Show sample toasts", Icons.Clipboard, Demo.TryClipboard);
            Act("Open log folder", Icons.Lines, () => ActionProvider.Shell(Log.Folder));
            Act("Open IP history repository", Icons.Git, () => ActionProvider.Shell(Net.PublicIpMonitor.RepoDir));
            Act("Restart service", Icons.Restart, () => App.Current.Quit(ServiceHost.ExitRestart));
            Act("Uninstall service", Icons.Trash, () => Go("service", "Uninstall"));
            foreach (var sc in s.Shortcuts)
            {
                var x = sc;
                Act(":" + sc.Name, Icons.Code, () => { Go("shortcuts"); }, string.IsNullOrWhiteSpace(sc.Description) ? "Edit this shortcut" : sc.Description, "Shortcuts", !string.IsNullOrWhiteSpace(sc.Description));
            }
            return list;
        }

        // -------------------------------------------------------------- the window

        private sealed class PaletteWindow : Window
        {
            private readonly List<Item> _all;
            private readonly TextBox _query;
            private readonly StackPanel _list;
            private readonly TextBlock _status;
            private readonly Search.SearchField _search;
            public Search.SearchField FindField => _search;
            private List<Item> _shown = new List<Item>();
            private int _sel;
            private FrameworkElement _selInline;   // the switch or unlock button of the selected row; Ctrl+Space operates it from the keyboard
            private readonly Border _card;

            public PaletteWindow(List<Item> items)
            {
                _all = items;
                WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
                ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false; Title = "Command palette";
                ApplySize();

                _search = new Search.SearchField("palette", "Search commands, pages and settings") { Margin = new Thickness(0) };
                _search.SampleSource = () => _all.Select(i => i.Title);
                _query = _search.Box;
                var size = Ui.Button(Settings.Current.PaletteFullWindow ? "Card" : "Full", () =>
                {
                    Settings.Current.PaletteFullWindow = !Settings.Current.PaletteFullWindow;
                    Settings.Save();
                    ApplySize();
                }, "Btn.Ghost");
                size.Height = 34; size.Margin = new Thickness(6, 0, 0, 0); size.ToolTip = "Switch between a card and the full window";

                var top = new Grid();
                top.ColumnDefinitions.Add(new ColumnDefinition());
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                top.Children.Add(_search);
                var flags = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
                flags.Children.Add(size);
                Grid.SetColumn(flags, 1); top.Children.Add(flags);

                _list = new StackPanel();
                _status = new TextBlock { Style = Ui.Style("T.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(4, 10, 0, 0) };
                var scroller = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 0) };
                var root = new DockPanel();
                DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
                DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
                root.Children.Add(scroller);

                _card = new Border
                {
                    Background = Ui.Res("B.Surface"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16), Padding = new Thickness(14), Margin = new Thickness(20),
                    Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Fx.Shadow"], Child = root,
                    RenderTransformOrigin = new Point(0.5, 0), RenderTransform = new ScaleTransform(0.97, 0.97)
                };
                Content = _card;

                _search.Filter += Filter;
                PreviewKeyDown += OnKey;
                Deactivated += (s, e) => { if (IsVisible && !_search.BuilderOpen && !_menuOpen) Close(); };
                // right-click, Shift+F10 or the Menu key: the palette's own actions, and the same menu every other window has
                ElementMenu.Attach(this, _ => PaletteMenu(), new ElementMenu.Options { OnMenu = open => _menuOpen = open });
                Loaded += (s, e) =>
                {
                    if (Ui.Reduced) _card.RenderTransform = Transform.Identity;
                    else
                    {
                        ((ScaleTransform)_card.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = Ui.Spring });
                        ((ScaleTransform)_card.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = Ui.Spring });
                    }
                    _card.Opacity = 0; Ui.Fade(_card, 1, 160);
                    Activate(); _query.Focus();
                };
                Filter();
            }

            private bool _menuOpen;

            private IEnumerable<ElementMenu.Item> PaletteMenu()
            {
                yield return new ElementMenu.Item("Clear the search", null, "", () => { _search.State.SetQuery(""); _query.Focus(); });
                yield return new ElementMenu.Item(Settings.Current.PaletteFullWindow ? "Use the card size" : "Use the full window", null, "", () =>
                {
                    Settings.Current.PaletteFullWindow = !Settings.Current.PaletteFullWindow; Settings.Save(); ApplySize();
                });
                yield return new ElementMenu.Item("Close the palette", null, "Esc", Close);
            }

            private void ApplySize()
            {
                var area = Ui.WorkArea;
                if (Settings.Current.PaletteFullWindow)
                {
                    Core.Appearance.SetDesignSize(this, area.Width, area.Height);
                    Left = area.Left; Top = area.Top; Width = area.Width; Height = area.Height;
                }
                else
                {
                    // grows with the text size, never past the screen; on a short screen the list gives way
                    Core.Appearance.SetDesignSize(this, 680, 560);
                    Left = area.Left + (area.Width - Width) / 2;
                    Top = area.Top + Math.Min(area.Height * 0.12, Math.Max(0, area.Height - Height));
                }
            }

            private void Filter()
            {
                var st = _search.State;
                st.TimedOut = false;
                _status.Foreground = Ui.Res("B.TextMute");
                _shown = _all.Where(i => st.Matches(i.Title + " " + (i.Detail ?? "") + " " + i.Group)).ToList();
                _search.SetCount(_shown.Count, _all.Count, "commands");
                _sel = 0;
                Render();
                _status.Text = st.Error != null
                    ? "Pattern is not valid: " + st.Error
                    : $"{_shown.Count} of {_all.Count} · ↑↓ to move · Enter to go · Esc to close · Alt+B regex builder" + (st.IsRegex ? " · regex" : "");
                if (st.Error != null) _status.Foreground = Ui.Res("B.Amber");
            }

            private void Render()
            {
                _list.Children.Clear();
                _selInline = null;
                string group = null;
                for (int i = 0; i < _shown.Count; i++)
                {
                    var it = _shown[i];
                    if (it.Group != group)
                    {
                        group = it.Group;
                        _list.Children.Add(new TextBlock { Text = group, Style = Ui.Style("T.Eyebrow"), Margin = new Thickness(8, i == 0 ? 0 : 12, 0, 6) });
                    }
                    _list.Children.Add(Row(it, i == _sel, i));
                }
            }

            private FrameworkElement Row(Item it, bool selected, int index)
            {
                var g = new Grid { Margin = new Thickness(8, 0, 8, 0), MinHeight = 44 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(Ui.IconChip(it.Icon ?? Icons.Bolt, Ui.Res("B.Mint"), Ui.Res("B.MintWash"), 28, 14));
                var text = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                text.Children.Add(new TextBlock { Text = it.Title, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
                if (!string.IsNullOrEmpty(it.Detail)) { var detailText = new TextBlock { Text = it.Detail, Style = Ui.Style("T.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis }; if (it.LiteralDetail) Core.Language.SetLiteral(detailText, true); text.Children.Add(detailText); }
                Grid.SetColumn(text, 1); g.Children.Add(text);
                var guard = LockFor(it);
                if (guard != null)
                {
                    // A locked item shows a lock badge and an Unlock… button, and never renders its live control.
                    var badge = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    var lockIcon = Ui.Icon(Icons.Shield, Ui.Res("B.Amber"), 15);
                    lockIcon.Margin = new Thickness(0, 0, 6, 0); lockIcon.VerticalAlignment = VerticalAlignment.Center;
                    badge.Children.Add(lockIcon);
                    badge.Children.Add(new TextBlock { Text = Core.Language.T("Locked"), Foreground = Ui.Res("B.Amber"), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                    var unlock = Ui.Button(Core.Language.T("Unlock…"), () => Unlock(guard), "Btn.Chip");
                    unlock.Height = 26; unlock.Margin = new Thickness(0); unlock.Focusable = false;
                    unlock.ToolTip = Core.Language.T("This item is locked and will not run until it is unlocked.");
                    AutomationProperties.SetName(unlock, Core.Language.T("Unlock…") + " " + it.Title);
                    badge.Children.Add(unlock);
                    if (selected) _selInline = unlock;
                    Grid.SetColumn(badge, 2); g.Children.Add(badge);
                    text.Opacity = 0.65;
                }
                else if (it.Inline != null)
                {
                    var c = it.Inline(); c.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(c, 2); g.Children.Add(c);
                    if (selected) _selInline = c;
                }
                var row = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(0, 4, 0, 4), Background = selected ? Ui.Res("B.Raised") : Brushes.Transparent, Child = g, Cursor = Cursors.Hand };
                AutomationProperties.SetName(row, $"{it.Group}: {it.Title}" + (guard != null ? ", " + Core.Language.T("Locked") : ""));
                row.MouseEnter += (s, e) => { if (_sel != index) { _sel = index; Render(); } };
                row.MouseLeftButtonUp += (s, e) =>
                {
                    if (e.OriginalSource is DependencyObject d && IsInside<ButtonBase>(d)) return; // inline control handled it
                    Choose(it);
                };
                return row;
            }

            private static bool IsInside<T>(DependencyObject d) where T : DependencyObject
            {
                while (d != null) { if (d is T) return true; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d); }
                return false;
            }

            private void Unlock(LockEntry entry)
            {
                Close();
                try { ElementMenu.PromptEntry(entry, null); } catch (Exception ex) { Log.Warn("Palette unlock failed: " + ex.Message); }
            }

            private void Choose(Item it)
            {
                // A locked item refuses to run by any route: it offers the unlock prompt instead.
                var guard = LockFor(it);
                if (guard != null) { Unlock(guard); return; }
                Close();
                try { it.Run?.Invoke(); } catch (Exception ex) { Log.Warn("Palette action failed: " + ex.Message); }
            }

            private void OnKey(object sender, KeyEventArgs e)
            {
                if (_search.BuilderOpen) return;
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
                else if (e.Key == Key.Down && _shown.Count > 0) { e.Handled = true; _sel = (_sel + 1) % _shown.Count; Render(); }
                else if (e.Key == Key.Up && _shown.Count > 0) { e.Handled = true; _sel = (_sel - 1 + _shown.Count) % _shown.Count; Render(); }
                else if (e.Key == Key.Enter && _shown.Count > 0) { e.Handled = true; Choose(_shown[_sel]); }
                else if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && _selInline != null)
                {
                    e.Handled = true;
                    if (_selInline is CheckBox cb) cb.IsChecked = cb.IsChecked != true;
                    else if (_selInline is ButtonBase bb) bb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            }
        }
    }
}
