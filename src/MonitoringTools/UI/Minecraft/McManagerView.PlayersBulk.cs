using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Minecraft
{
    // The player list as a real multi-select ListBox with the shared search field and its regex builder, and the bulk actions
    // for the selection. Java and Bedrock share it; each supplies how a row looks and what to reload after a change.
    internal sealed partial class McManagerView
    {
        internal sealed class PlayerBox
        {
            public StackPanel Root = new StackPanel();
            public SearchField Search;
            public ListBox List;
            public TextBlock Note, Selected, Result;
            public List<McPlayer> All = new List<McPlayer>();
            public Func<McPlayer, FrameworkElement> Row;
            public Action Changed;
            public Button[] Bulk = Array.Empty<Button>();
            public TextBox Reason, Target, Item, Count;
            public string Mode = "survival";
            public Action<string, string> Say;
            public IEnumerable<McPlayer> Chosen => List.SelectedItems.OfType<ListBoxItem>().Select(i => (McPlayer)i.Tag);
            public Action<List<McPlayer>, string> Show;
        }

        private static Style _playerItem;
        private static Style PlayerItemStyle()
        {
            if (_playerItem != null) return _playerItem;
            var st = new Style(typeof(ListBoxItem));
            var tpl = new ControlTemplate(typeof(ListBoxItem));
            var bd = new FrameworkElementFactory(typeof(Border), "Bd");
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            bd.SetValue(Border.PaddingProperty, new Thickness(8, 2, 8, 2));
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            bd.SetValue(Border.BorderBrushProperty, System.Windows.Media.Brushes.Transparent);
            bd.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            bd.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            tpl.VisualTree = bd;
            var sel = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            sel.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Res("B.SecondaryContainer"), "Bd"));
            tpl.Triggers.Add(sel);
            var foc = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            foc.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Res("B.Mint"), "Bd"));
            tpl.Triggers.Add(foc);
            st.Setters.Add(new Setter(Control.TemplateProperty, tpl));
            st.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
            return _playerItem = st;
        }

        private static string Short(IEnumerable<string> names, int cap)
        {
            var l = names.ToList();
            return string.Join(", ", l.Take(cap)) + (l.Count > cap ? $" and {l.Count - cap} more" : "");
        }

        /// <summary>Builds the search field, the list and the bulk toolbar. <paramref name="changed"/> runs after an action so the caller reloads its files.</summary>
        private PlayerBox MakePlayerBox(ServerDef s, string searchId, Func<McPlayer, FrameworkElement> rowFor, Action changed)
        {
            var b = new PlayerBox { Row = rowFor, Changed = changed };
            b.Search = new SearchField(searchId, "Search players", compact: true) { Margin = new Thickness(0, 0, 0, 8) };
            b.Search.SampleSource = () => b.All.Select(p => p.Name);
            b.Root.Children.Add(b.Search);
            b.Note = McUi.Body("", "B.TextMute", 12);
            b.Root.Children.Add(b.Note);
            b.List = new ListBox
            {
                SelectionMode = SelectionMode.Extended, BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent, Padding = new Thickness(0),
                ItemContainerStyle = PlayerItemStyle(),
            };
            ScrollViewer.SetVerticalScrollBarVisibility(b.List, ScrollBarVisibility.Disabled);   // the page scrolls, not the list
            System.Windows.Input.KeyboardNavigation.SetTabNavigation(b.List, System.Windows.Input.KeyboardNavigationMode.Continue);   // the switches inside each row are reached with Tab
            ScrollViewer.SetHorizontalScrollBarVisibility(b.List, ScrollBarVisibility.Disabled);
            AutomationProperties.SetName(b.List, "Players");
            AutomationProperties.SetHelpText(b.List, "Select several players with Ctrl or Shift and a click or the arrow keys, or press Ctrl+A for all shown.");
            b.Root.Children.Add(b.List);

            b.Selected = McUi.Body("", "B.TextSoft", 12);
            b.Selected.Margin = new Thickness(0, 8, 0, 4);
            AutomationProperties.SetLiveSetting(b.Selected, AutomationLiveSetting.Polite);
            b.Root.Children.Add(b.Selected);

            b.Result = McUi.Body("", "B.TextMute", 12);
            AutomationProperties.SetLiveSetting(b.Result, AutomationLiveSetting.Polite);
            b.Say = (t, fg) => { b.Result.Text = t; b.Result.Foreground = Ui.Res(fg); Core.Language.Refresh(b.Result); };

            StackPanel Labelled(string label, TextBox box) { var c = new StackPanel { Margin = new Thickness(0, 0, 12, 4) }; c.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 4) }); c.Children.Add(box); return c; }
            b.Reason = McUi.Field("", "Reason for kick or ban", 220, "Optional. Sent with the kick or ban to every selected player.");
            b.Target = McUi.Field("", "Teleport to", 200, "A player's name, or three coordinates such as 10 64 -20. A ~ means relative to the player.");
            b.Item = McUi.Field("", "Item to give", 200, "An item id such as minecraft:diamond.");
            b.Count = McUi.Field("1", "Count", 70, "1 to 6400.");
            var inputs = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            inputs.Children.Add(Labelled("Reason for kick or ban", b.Reason));
            inputs.Children.Add(Labelled("Teleport to", b.Target));
            inputs.Children.Add(Labelled("Item to give", b.Item));
            inputs.Children.Add(Labelled("Count", b.Count));
            var bar = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            AutomationProperties.SetName(bar, "Actions on the selected players");

            Button Add(BulkVerb v, string label, string icon, string style = "Btn.Ghost")
            {
                var btn = Ui.Button(label, () => RunBulk(s, b, v), style, icon);
                AutomationProperties.SetName(btn, label);
                bar.Children.Add(btn);
                return btn;
            }
            var buttons = new List<Button>
            {
                Add(BulkVerb.Kick, "Kick…", Icons.Close),
            };
            if (McBulk.Supported(s, BulkVerb.Ban)) { buttons.Add(Add(BulkVerb.Ban, "Ban…", Icons.Shield)); buttons.Add(Add(BulkVerb.Pardon, "Pardon", Icons.Check)); }
            buttons.Add(Add(BulkVerb.Op, "Make operators…", Icons.Shield));
            buttons.Add(Add(BulkVerb.Deop, "Remove operators", Icons.Restart));
            buttons.Add(Add(BulkVerb.AllowAdd, s.IsBedrock ? "Add to allow list" : "Add to whitelist", Icons.Plus));
            buttons.Add(Add(BulkVerb.AllowRemove, s.IsBedrock ? "Remove from allow list" : "Remove from whitelist", Icons.Trash));
            Button mode = null;
            mode = Ui.Button("Game mode", () =>
            {
                var items = McBulk.Modes.Select(m => new ElementMenu.Item("Game mode: " + m, Icons.Game, null, () => { b.Mode = m; RunBulk(s, b, BulkVerb.GameMode); })).ToList();
                var w = Window.GetWindow(mode);
                if (w != null) ElementMenu.OpenItems(w, mode, items, mode.TranslatePoint(new Point(0, mode.ActualHeight), w));
            }, "Btn.Ghost", Icons.Game);
            AutomationProperties.SetName(mode, "Game mode");
            bar.Children.Add(mode); buttons.Add(mode);
            buttons.Add(Add(BulkVerb.Teleport, "Teleport", Icons.Link));
            buttons.Add(Add(BulkVerb.Give, "Give", Icons.Box));
            if (!s.IsBedrock)
            {
                var builder = Ui.Button("Give in the command builder…", () => GiveInBuilder(s, b), "Btn.Ghost", Icons.Wand);
                AutomationProperties.SetName(builder, "Give in the command builder…");
                bar.Children.Add(builder); buttons.Add(builder);
            }
            var all = Ui.Button("Select all shown", () => { b.List.SelectAll(); b.List.Focus(); }, "Btn.Ghost", Icons.Check);
            var clear = Ui.Button("Clear selection", () => b.List.UnselectAll(), "Btn.Ghost", Icons.Close);
            AutomationProperties.SetName(all, "Select all shown"); AutomationProperties.SetName(clear, "Clear selection");
            bar.Children.Add(all); bar.Children.Add(clear);
            b.Bulk = buttons.ToArray();
            b.Root.Children.Add(bar);
            b.Root.Children.Add(inputs);
            b.Root.Children.Add(b.Result);

            void UpdateSelection()
            {
                var names = b.Chosen.Select(p => p.Name).ToList();
                b.Selected.Text = names.Count == 0 ? "Nothing selected. Select players to act on several at once." : $"{names.Count} selected: {Short(names, 8)}";
                foreach (var x in b.Bulk) x.IsEnabled = names.Count > 0;
                clear.IsEnabled = names.Count > 0;
                Core.Language.Refresh(b.Selected);
            }
            b.List.SelectionChanged += (o, e) => UpdateSelection();

            void Apply()
            {
                var keep = new HashSet<string>(b.Chosen.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                var shown = b.All.Where(p => b.Search.State.Matches(p.Name)).ToList();
                b.List.Items.Clear();
                foreach (var p in shown)
                {
                    var item = new ListBoxItem { Content = b.Row(p), Tag = p, Focusable = true };
                    AutomationProperties.SetName(item, p.Name + (p.Online ? ", online" : ", offline") + (p.Banned ? ", banned" : "") + (p.Operator ? ", operator" : ""));
                    b.List.Items.Add(item);
                    if (keep.Contains(p.Name)) item.IsSelected = true;
                }
                b.Search.SetCount(shown.Count, b.All.Count, "players");
                UpdateSelection();
                Core.Language.Refresh(b.List);
            }
            b.Search.Filter += Apply;
            b.Show = (players, note) =>
            {
                b.All = players ?? new List<McPlayer>();
                b.Note.Text = note ?? ""; b.Note.Visibility = string.IsNullOrEmpty(note) ? Visibility.Collapsed : Visibility.Visible;
                Apply();
            };
            UpdateSelection();
            return b;
        }

        /// <summary>
        /// Opens the command builder's give form with the item and count already typed and the first selected player as the target.
        /// The builder's Who control takes one name or one selector, so with several players selected it opens on the first and
        /// the status line says so; the plain Give button above sends to every selected player.
        /// </summary>
        private void GiveInBuilder(ServerDef s, PlayerBox b)
        {
            var names = b.Chosen.Select(p => p.Name).ToList();
            if (names.Count == 0) { b.Say("Select players first.", "B.Amber"); return; }
            var v = ValuesFor(s, "give");
            v["targets"] = names[0];
            if (!string.IsNullOrWhiteSpace(b.Item.Text)) v["item"] = b.Item.Text.Trim();
            if (int.TryParse(b.Count.Text.Trim(), out var c) && c > 0) v["count"] = c.ToString();
            OpenCommandTask("give");
            SetStatus(names.Count == 1 ? $"The give form is open for {names[0]}."
                : $"The give form is open for {names[0]}. It takes one target at a time; use Give in the player list to give to all {names.Count} selected players.", "B.TextSoft");
        }

        private void RunBulk(ServerDef s, PlayerBox b, BulkVerb v)
        {
            var names = b.Chosen.Select(p => p.Name).ToList();
            if (names.Count == 0) { b.Say("Select players first.", "B.Amber"); return; }
            int.TryParse(b.Count.Text.Trim(), out var count);
            var a = new BulkArgs { Reason = b.Reason.Text, Mode = b.Mode, Target = b.Target.Text, Item = b.Item.Text, Count = count };
            var problem = McBulk.Problem(s, v, a);
            if (problem != null) { b.Say(problem, "B.Amber"); return; }
            async Task Go()
            {
                b.Say("Working…", "B.TextMute");
                bool up = IsUp(s);
                var results = await McBulk.RunAsync(s, up, v, names, a);
                b.Say(McBulk.Summary(v, results), results.All(r => r.Ok) ? "B.Mint" : "B.Amber");
                await Task.Delay(up && !McDemo.IsSample(s) ? 900 : 10);   // a running server saves its files a moment after the commands
                b.Changed?.Invoke();
            }
            if (!McBulk.NeedsConfirm(v)) { _ = Go(); return; }
            var verb = v == BulkVerb.Ban ? "Ban" : v == BulkVerb.Op ? "Make operators of" : "Kick";
            var body = (v == BulkVerb.Op ? "Operators can run every command on this server, including stopping it. " : v == BulkVerb.Ban ? "They are disconnected and cannot join again until they are pardoned. " : "They are disconnected now and can join again. ")
                + $"These {names.Count} players: {Short(names, 15)}.";
            var dlg = new ConfirmWindow($"{verb} {names.Count} player{(names.Count == 1 ? "" : "s")}?", body, v == BulkVerb.Op ? "Make operators" : verb, danger: true);
            dlg.Confirmed += ok => _ = Go();
            dlg.Show();
        }
    }
}
