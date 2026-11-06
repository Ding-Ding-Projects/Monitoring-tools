using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Clipboard settings in one searchable window: automatic lookups with exactly what is sent where, the kinds of copies
    /// that are recognised, the secret guard, extensions, and every clipboard shortcut. Changes save straight away.
    /// </summary>
    internal sealed class ClipboardSettingsDialog : ToolDialogWindow
    {
        internal static ClipboardSettingsDialog Current { get; private set; }
        private readonly SearchField _search = new SearchField("clipboard-settings", "Search clipboard settings", compact: true);
        private readonly List<(FrameworkElement Row, string Words)> _rows = new List<(FrameworkElement, string)>();
        private readonly List<(FrameworkElement Head, List<FrameworkElement> Rows)> _sections = new List<(FrameworkElement, List<FrameworkElement>)>();
        private readonly StackPanel _body = new StackPanel();
        private readonly TextBlock _empty = new TextBlock { Text = "No setting matches.", Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(4, 16, 0, 0), Visibility = Visibility.Collapsed };
        private TextBlock _status;
        private List<FrameworkElement> _current;

        public static ClipboardSettingsDialog Open(string find = null)
        {
            if (Current == null || !Current.IsLoaded)
            {
                Current = new ClipboardSettingsDialog();
                Current.Closed += (s, e) => Current = null;
                Current.Show();
            }
            Current.Activate();
            if (find != null) Current._search.State.SetQuery(find);
            return Current;
        }

        private ClipboardSettingsDialog() : base("Clipboard settings")
        {
            var s = Settings.Current;
            _search.Filter += Apply;

            Section("AUTOMATIC LOOKUPS");
            Switch("Look things up on the internet automatically", "Adds rates, book titles, package versions and address owners to toasts. Off means nothing you copy leaves this PC.", s.AutoLookups, v => s.AutoLookups = v, "lookup internet privacy online");
            Switch("Money amounts", "Sends the currency code to Frankfurter (European Central Bank rates).", s.LookupMoney, v => s.LookupMoney = v, "lookup currency rates");
            Switch("Book numbers and package names", "Sends the ISBN to Open Library, or the package name to npm, NuGet or PyPI.", s.LookupBooksAndPackages, v => s.LookupBooksAndPackages = v, "lookup isbn npm nuget pypi");
            Switch("Public IP addresses", "Sends the address to RDAP, the internet address registries.", s.LookupIps, v => s.LookupIps = v, "lookup rdap owner");
            Note("Never looked up, whatever these say: copies from password managers, anything that looks like a key, token or password, and text from extensions. Each lookup gives up after 4 seconds; answers are kept in memory for a day.", "lookup privacy");
            var cur = new Dropdown("Home currency", Everyday2.Currencies.Select(c => new Choice(c, c)), s.HomeCurrency) { Width = 160 };
            cur.Changed += id => { s.HomeCurrency = id; SmartDetect.HomeCurrency = id; Saved("Home currency " + id + "."); };
            Row(Labelled("Home currency", "Money is shown in this currency first.", cur), "home currency money");

            Section("KINDS OF COPIES");
            Group("developer", "Developer kinds", "Stack traces, hashes, GUIDs, IP addresses, SQL, regular expressions, XML, packages, paths, error codes, cron");
            Group("everyday", "Everyday kinds", "Phone numbers, addresses, parcels, flights, times and zones, books, characters, languages");
            Group("numbers", "Numbers and tables", "Quick conversions, money, sums, tables from spreadsheets");

            Section("SECRETS");
            Switch("Secret guard", "Keys, tokens and bank numbers stay out of history and leave the clipboard after " + s.SecretClearSeconds + " seconds.", s.SecretGuard, v => s.SecretGuard = v, "secret password token key iban privacy");

            Section("EXTENSIONS");
            Switch("Extensions", "Let turned-on extensions add kinds of copies and buttons.", s.ExtensionsEnabled, v => s.ExtensionsEnabled = v, "extensions plugins add-ons");
            Row(Buttons(("Manage extensions", () => ExtensionsDialog.Open())), "extensions manage");

            Section("SNIPPETS, RECIPES AND APP RULES");
            Switch("Snippets", "Typing a snippet's trigger in any app replaces it with the snippet.", s.SnippetsOn, v => { s.SnippetsOn = v; ClipPower.UpdateHook(); }, "snippets triggers text expansion");
            Row(Buttons(("Snippets", () => SnippetsDialog.Open()), ("Recipes", () => RecipesDialog.Open()), ("Per-app rules", () => RulesDialog.Open())), "snippets recipes rules per-app");

            Section("SHORTCUTS");
            Switch("Clipboard shortcuts", "Turn every clipboard shortcut on or off.", s.ClipboardHotkeysOn, v => { s.ClipboardHotkeysOn = v; ClipPower.RegisterAll(); }, "shortcuts hotkeys keys");
            Key("Clipboard history", () => s.HotkeyHistory, v => s.HotkeyHistory = v);
            Key("Paste as plain text", () => s.HotkeyPlainPaste, v => s.HotkeyPlainPaste = v);
            Key("Paste queue", () => s.HotkeyQueue, v => s.HotkeyQueue = v);
            Key("Undo the clipboard", () => s.HotkeyUndo, v => s.HotkeyUndo = v);
            Key("Quick conversions", () => s.HotkeyConversions, v => s.HotkeyConversions = v);
            Key("Pin on screen", () => s.HotkeyPin, v => s.HotkeyPin = v);
            Key("Collect mode", () => s.HotkeyCollect, v => s.HotkeyCollect = v);
            Key("Type it instead of pasting", () => s.HotkeyTypeIt, v => s.HotkeyTypeIt = v);
            if (ClipPower.Taken.Count > 0) Note("Already used by another app, so not active: " + string.Join(", ", ClipPower.Taken) + ".", "shortcuts taken");

            _body.Children.Add(_empty);
            var scroll = new ScrollViewer { Content = _body, Height = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Margin = new Thickness(0, 12, 0, 0) };
            var done = Ui.Button("Done", Close, "Btn.Accent", Icons.Check);
            var more = new List<(string, string, Action)>
            {
                ("Put the shortcuts back to how they were", null, ResetKeys),
                ("Read about smart copies", null, () => SmartActions.Open("https://github.com/Ding-Ding-Projects/Monitoring-tools/blob/main/docs/features/clipboard/smart-copies.md")),
            };
            var root = new StackPanel();
            root.Children.Add(_search); root.Children.Add(scroll);
            root.Children.Add(SmartDialogParts.ActionBar(more, "Changes save straight away.", null, done, "Enter", out _status));
            ShowView("form", root, 820, "Clipboard settings", "What the clipboard recognises, looks up, guards and the keys it uses.", Icons.Clipboard);
            PreviewKeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.Enter && !(Keyboard.FocusedElement is TextBox)) { e.Handled = true; Close(); } };
        }

        // ------------------------------------------------------------------ building rows

        private void Section(string title)
        {
            var t = new TextBlock { Text = title, FontFamily = Ui.Font("F.Mono"), FontSize = 11.5, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(4, 20, 0, 4) };
            _body.Children.Add(t);
            _current = new List<FrameworkElement>();
            _sections.Add((t, _current));
        }

        private void Row(FrameworkElement row, string words)
        {
            _body.Children.Add(row);
            _rows.Add((row, words));
            _current.Add(row);
        }

        private void Switch(string title, string sub, bool on, Action<bool> set, string words)
        {
            var cb = DialogParts.SwitchRow(title, sub, on, out var row);
            cb.Click += (s, e) => { set(cb.IsChecked == true); Saved(title + (cb.IsChecked == true ? " on." : " off.")); };
            Row(row, title + " " + sub + " " + words);
        }

        private void Group(string id, string title, string sub) =>
            Switch(title, sub, !Settings.Current.SmartGroupsOff.Contains(id), v =>
            {
                var off = Settings.Current.SmartGroupsOff;
                off.Remove(id); if (!v) off.Add(id);
            }, "kinds recognise detect " + id);

        private void Note(string text, string words) => Row(DialogParts.Hint(text), text + " " + words);

        private static FrameworkElement Labelled(string title, string sub, FrameworkElement control)
        {
            var g = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = title, FontSize = 14, Foreground = Ui.Res("B.Text") });
            sp.Children.Add(new TextBlock { Text = sub, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap });
            g.Children.Add(sp); Grid.SetColumn(control, 1); g.Children.Add(control);
            return g;
        }

        private static FrameworkElement Buttons(params (string Label, Action Run)[] items)
        {
            var w = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            foreach (var (label, run) in items) { var b = Ui.Button(label, run, "Btn.Ghost"); b.Margin = new Thickness(0, 0, 8, 0); w.Children.Add(b); }
            return w;
        }

        private void Key(string what, Func<string> get, Action<string> set)
        {
            var box = new TextBox { Style = Ui.Style("Input"), Text = get(), Width = 220, Height = 40, FontFamily = Ui.Font("F.Mono"), FontSize = 13, IsReadOnly = true, Cursor = Cursors.Hand };
            AutomationProperties.SetName(box, what + " shortcut. Press the new keys.");
            box.ToolTip = "Click, then press the new keys. Backspace clears it.";
            box.PreviewKeyDown += (s, e) =>
            {
                e.Handled = true;
                var k = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
                if (k == System.Windows.Input.Key.Tab) { e.Handled = false; return; }
                if (k == System.Windows.Input.Key.Back) { set(""); box.Text = ""; ClipPower.RegisterAll(); Saved(what + " has no shortcut now."); return; }
                if (k is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin) return;
                var parts = new List<string>();
                var m = Keyboard.Modifiers;
                if ((m & ModifierKeys.Windows) != 0 || Keyboard.IsKeyDown(System.Windows.Input.Key.LWin) || Keyboard.IsKeyDown(System.Windows.Input.Key.RWin)) parts.Add("Win");
                if ((m & ModifierKeys.Control) != 0) parts.Add("Ctrl");
                if ((m & ModifierKeys.Shift) != 0) parts.Add("Shift");
                if ((m & ModifierKeys.Alt) != 0) parts.Add("Alt");
                var name = k >= System.Windows.Input.Key.D0 && k <= System.Windows.Input.Key.D9 ? ((int)(k - System.Windows.Input.Key.D0)).ToString() : k.ToString();
                parts.Add(name);
                var text = string.Join("+", parts);
                if (!NativeInput.ParseShortcut(text, out _, out _)) { Saved(text + " cannot be a shortcut: use at least one of Ctrl, Alt or Win with a letter, digit or F-key."); return; }
                set(text); box.Text = text;
                ClipPower.RegisterAll();
                Saved(ClipPower.Taken.Any(t => t.StartsWith(text + " ")) ? text + " is already used by another app, so it is not active." : what + " is now " + text + ".");
            };
            Row(Labelled(what, "", box), what + " shortcut hotkey " + get());
        }

        private void ResetKeys()
        {
            var d = new Settings();
            var s = Settings.Current;
            s.HotkeyHistory = d.HotkeyHistory; s.HotkeyPlainPaste = d.HotkeyPlainPaste; s.HotkeyQueue = d.HotkeyQueue; s.HotkeyUndo = d.HotkeyUndo;
            s.HotkeyConversions = d.HotkeyConversions; s.HotkeyPin = d.HotkeyPin; s.HotkeyCollect = d.HotkeyCollect; s.HotkeyTypeIt = d.HotkeyTypeIt;
            Settings.Save(); ClipPower.RegisterAll();
            Close(); Open();
        }

        private void Saved(string what)
        {
            Settings.Save();
            if (_status != null) _status.Text = what + " Saved.";
        }

        private void Apply()
        {
            int shown = 0;
            foreach (var (row, words) in _rows)
            {
                bool on = !_search.State.IsActive || _search.State.Matches(words);
                row.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                if (on) shown++;
            }
            foreach (var (head, rows) in _sections) head.Visibility = rows.Any(r => r.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            _empty.Visibility = shown == 0 ? Visibility.Visible : Visibility.Collapsed;
            _search.SetCount(shown, _rows.Count, "settings");
        }
    }
}
