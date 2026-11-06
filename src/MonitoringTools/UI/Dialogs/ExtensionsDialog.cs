using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// The extensions manager: every extension in the extensions folder, what it notices, the buttons it adds, what it is
    /// allowed to do and its last runs. Turning one on, or adding one, first shows exactly what it asks for. Ctrl+N adds an
    /// extension from a folder, Ctrl+O opens the extensions folder.
    /// </summary>
    internal sealed class ExtensionsDialog : ToolDialogWindow
    {
        internal static ExtensionsDialog Current { get; private set; }

        private readonly ExtensionHost _host;
        private readonly StackPanel _list = new StackPanel();
        private readonly ContentControl _detail = new ContentControl { Focusable = false };
        private readonly ContentControl _banner = new ContentControl { Focusable = false };
        private string _selected;

        public static ExtensionsDialog Open()
        {
            if (App.Extensions == null) App.LoadExtensions();
            if (App.Extensions == null) { App.Toasts.ShowNotice("Extensions are not available", "The extensions folder could not be read.", App.Toasts.Amber, 5, icon: Icons.Bolt); return null; }
            if (Current != null && Current.IsLoaded) { Current.Activate(); return Current; }
            var d = new ExtensionsDialog(App.Extensions);
            Current = d;
            d.Closed += (s, e) => { if (Current == d) Current = null; };
            d.Show();
            return d;
        }

        internal ExtensionsDialog(ExtensionHost host) : base("Extensions")
        {
            _host = host;
            var grid = new Grid { Height = 520 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var listCard = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(22), Padding = new Thickness(8), Child = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false } };
            grid.Children.Add(listCard);
            var detailScroll = new ScrollViewer { Content = _detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
            Grid.SetColumn(detailScroll, 2); grid.Children.Add(detailScroll);

            var more = new List<(string, string, Action)>
            {
                ("Reload all extensions", "F5", Reload),
                ("Open the extensions folder", "Ctrl+O", OpenFolder),
                ("How to write an extension", null, () => SmartActions.Open("https://github.com/Ding-Ding-Projects/Monitoring-tools/blob/main/docs/features/clipboard/extensions.md")),
                ("Turn every extension off", null, () => { Settings.Current.ExtensionsEnabled = false; Settings.Save(); Paint(); }),
            };
            var folder = Ui.Button("Open the folder", OpenFolder, "Btn.Ghost", Icons.Folder);
            var add = Ui.Button("Add an extension", Add, "Btn.Accent", Icons.Plus);
            var body = new StackPanel();
            body.Children.Add(grid);
            body.Children.Add(_banner);
            body.Children.Add(SmartDialogParts.ActionBar(more, "Changes save straight away.", new[] { folder }, add, "Ctrl+N"));
            ShowView("form", body, 900, "Extensions", "", Icons.Wand, Ui.Res("B.Violet"), Ui.Res("B.VioletWash"));

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.N && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; Add(); }
                else if (e.Key == System.Windows.Input.Key.O && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; OpenFolder(); }
                else if (e.Key == System.Windows.Input.Key.F5) { e.Handled = true; Reload(); }
            };
            Paint();
        }

        private void Reload() { _host.Load(); Paint(); Say("Reloaded " + _host.Items.Count + (_host.Items.Count == 1 ? " extension." : " extensions."), "mint"); }

        private void OpenFolder()
        {
            Directory.CreateDirectory(_host.Root);
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _host.Root + "\"") { UseShellExecute = false }); } catch { }
        }

        internal void Paint()
        {
            int on = _host.Items.Count(i => i.Enabled);
            SubText.Text = !Settings.Current.ExtensionsEnabled ? "All extensions are off. Turn them back on from Clipboard settings."
                : _host.Items.Count == 0 ? "None yet. Add one, or put a folder with an extension.json in the extensions folder."
                : $"{on} of {_host.Items.Count} turned on · they add kinds of copies and buttons to toasts";
            _list.Children.Clear();
            if (_selected == null || _host.Items.All(i => KeyOf(i) != _selected)) _selected = _host.Items.Select(KeyOf).FirstOrDefault();
            int n = 0;
            foreach (var it in _host.Items)
            {
                var item = it;
                var name = new TextBlock { Text = it.Name, FontSize = 14.5, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis };
                var state = it.Error != null ? "needs fixing" : it.Enabled ? "on" : it.NeedsApproval ? "waiting for you to allow it" : "off";
                var sub = Ui.Mono((it.Manifest?.Version ?? "") + " · " + state, it.Error != null ? Ui.Res("B.Amber") : Ui.Res("B.TextSoft"), 11);
                var sp = new StackPanel(); sp.Children.Add(name); sp.Children.Add(sub);
                var row = new Button { Content = sp, Style = Ui.Style("Btn.Row"), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 2) };
                if (KeyOf(it) == _selected) row.Background = Ui.Res("B.MintWash");
                AutomationProperties.SetName(row, it.Name + ", " + state);
                row.Click += (s, e) => { _selected = KeyOf(item); Paint(); };
                _list.Children.Add(row);
                Ui.Rise(row, n++ * 30, 6, 280);
            }
            var cur = _host.Items.FirstOrDefault(i => KeyOf(i) == _selected);
            _detail.Content = cur == null ? Empty() : Detail(cur);
        }

        private static string KeyOf(LoadedExtension i) => i.Manifest?.Id ?? i.Folder;

        private FrameworkElement Empty()
        {
            var sp = new StackPanel { Margin = new Thickness(8) };
            sp.Children.Add(Ui.Text("Extensions teach the clipboard new tricks: a pattern to notice, and buttons that run a small program you choose.", "T.Body"));
            sp.Children.Add(DialogParts.Hint("Each extension is a folder with an extension.json and a program or script (.exe, .ps1, .py, .js, .cmd). Try the samples in the samples\\extensions folder of the project."));
            return sp;
        }

        private FrameworkElement Detail(LoadedExtension it)
        {
            var sp = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titles = new StackPanel();
            titles.Children.Add(new TextBlock { Text = it.Name, FontFamily = Ui.Font("F.Display"), FontSize = 20, Foreground = Ui.Res("B.Text") });
            titles.Children.Add(new TextBlock { Text = (it.Manifest?.Author ?? "Local folder") + (it.Manifest != null ? " · version " + it.Manifest.Version : ""), FontSize = 12.5, Foreground = Ui.Res("B.TextSoft") });
            head.Children.Add(titles);
            if (it.Manifest != null && it.Error == null)
            {
                var sw = new CheckBox { Style = Ui.Style("Switch"), IsChecked = it.Enabled, VerticalAlignment = VerticalAlignment.Center };
                AutomationProperties.SetName(sw, "Turn " + it.Name + " on");
                sw.Click += (s, e) =>
                {
                    if (sw.IsChecked == true) { sw.IsChecked = false; Ask(it); }
                    else { _host.TurnOff(it.Manifest.Id); Paint(); Say(it.Name + " is off.", "mint"); }
                };
                Grid.SetColumn(sw, 1); head.Children.Add(sw);
            }
            sp.Children.Add(head);
            if (it.Error != null) { var b = DialogParts.Banner(DialogParts.Warn, it.Error + " The folder is " + it.Folder + ".", "amber"); b.Margin = new Thickness(0, 14, 0, 0); sp.Children.Add(b); }
            if (it.Manifest == null) return sp;
            if (!string.IsNullOrEmpty(it.Manifest.Description)) sp.Children.Add(new TextBlock { Text = it.Manifest.Description, FontSize = 14.5, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) });
            if (it.NeedsApproval && it.Error == null)
            {
                var b = DialogParts.Banner(DialogParts.Info, "This extension is new or asks for something new, so it stays off until you allow it.", "amber");
                b.Margin = new Thickness(0, 12, 0, 0); sp.Children.Add(b);
            }
            sp.Children.Add(Chips("NOTICES THESE COPIES", it.Manifest.Detect.Select(d => d.Label ?? d.Kind), true));
            sp.Children.Add(Chips("ADDS THESE BUTTONS", it.Manifest.Actions.Select(a => a.Label), false));
            sp.Children.Add(Chips("ALLOWED TO", it.Manifest.Permissions.Select(ExtensionPermission.Describe), false));
            var log = it.Log.Count == 0 ? "Not run yet." : string.Join(Environment.NewLine, it.Log);
            var logBox = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(16), Padding = new Thickness(14, 10, 14, 12), Margin = new Thickness(0, 16, 0, 0) };
            var lsp = new StackPanel();
            lsp.Children.Add(Eyebrow("LAST RUNS"));
            var lt = Ui.Mono(log, Ui.Res("B.TextSoft"), 12); Core.Language.SetLiteral(lt, true); lsp.Children.Add(lt);
            logBox.Child = lsp; sp.Children.Add(logBox);
            sp.Children.Add(DialogParts.Hint("Program: " + it.Manifest.Run + " in " + it.Folder));
            return sp;
        }

        private static TextBlock Eyebrow(string t) => new TextBlock { Text = t, FontFamily = Ui.Font("F.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(0, 0, 0, 6) };

        private static FrameworkElement Chips(string title, IEnumerable<string> items, bool mono)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            sp.Children.Add(Eyebrow(title));
            var wrap = new WrapPanel();
            foreach (var t in items)
            {
                var p = Ui.Pill(t, Ui.Res("B.Sunken"), Ui.Res("B.Text"), mono, 12);
                p.Margin = new Thickness(0, 0, 6, 6);
                wrap.Children.Add(p);
            }
            if (wrap.Children.Count == 0) wrap.Children.Add(new TextBlock { Text = "Nothing", Foreground = Ui.Res("B.TextSoft"), FontSize = 12.5 });
            sp.Children.Add(wrap);
            return sp;
        }

        /// <summary>Shows what an extension asks for, with Allow and turn on as the main action.</summary>
        internal void Ask(LoadedExtension it)
        {
            var sp = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
            sp.Children.Add(new TextBlock { Text = "Turn on \"" + it.Name + "\"?", FontFamily = Ui.Font("F.Display"), FontSize = 20, Foreground = Ui.Res("B.Text") });
            sp.Children.Add(new TextBlock { Text = "From " + it.Folder + " · version " + it.Manifest.Version + ". Extensions are not checked or signed, so only allow one if you trust where it came from. It asks to:", FontSize = 13.5, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 10) });
            foreach (var p in it.Manifest.Permissions) sp.Children.Add(new TextBlock { Text = "✓  " + ExtensionPermission.Describe(p), FontSize = 14, Foreground = Ui.Res("B.Text"), Margin = new Thickness(0, 2, 0, 2) });
            var missing = ExtensionPermission.All.Where(p => !it.Manifest.Permissions.Contains(p)).Select(ExtensionPermission.Describe).ToList();
            if (missing.Count > 0) sp.Children.Add(new TextBlock { Text = "✗  Not: " + string.Join(", ", missing).ToLowerInvariant(), FontSize = 13, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            sp.Children.Add(DialogParts.Hint("It never sees copies from password managers or anything that looks like a key, token or password."));
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            var cancel = Ui.Button("Cancel", () => Paint(), "Btn.Ghost");
            var allow = Ui.Button("Allow and turn on", () => { _host.Allow(it.Manifest.Id); Paint(); Say(it.Name + " is on. Copy something it notices to try it.", "mint"); }, "Btn.Accent", Icons.Check);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(cancel); row.Children.Add(allow);
            sp.Children.Add(row);
            _detail.Content = sp;
            Ui.Rise(sp, 0, 8, 300);
            allow.Focus();
        }

        private void Add()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Choose the extension's folder (it has an extension.json)" };
            if (dlg.ShowDialog(this) != true) return;
            var m = _host.Install(dlg.FolderName, false, out var err);
            if (err == "exists")
            {
                var old = _host.Find(m.Id);
                Say("\"" + m.Name + "\" is already here (version " + (old?.Manifest?.Version ?? "?") + "). Press Add again and choose Replace to update it to " + m.Version + ".", "amber");
                var replace = Ui.Button("Replace with version " + m.Version, () =>
                {
                    try { _host.Install(dlg.FolderName, true, out var e2); if (e2 != null) { Say(e2, "amber"); return; } } catch (Exception ex) { Say("Not replaced: " + ex.Message, "amber"); return; }
                    _selected = m.Id; Paint(); var it2 = _host.Find(m.Id); if (it2 != null && it2.NeedsApproval) Ask(it2);
                }, "Btn.Accent");
                _banner.Content = new StackPanel { Children = { (UIElement)_banner.Content, replace } };
                replace.Margin = new Thickness(0, 8, 0, 0); replace.HorizontalAlignment = HorizontalAlignment.Right;
                return;
            }
            if (m == null) { Say("Not added: " + err, "amber"); return; }
            _selected = m.Id; Paint();
            var it = _host.Find(m.Id);
            if (it != null && it.Error == null) Ask(it);
        }

        private void Say(string text, string tone)
        {
            var b = DialogParts.Banner(tone == "amber" ? DialogParts.Warn : Icons.Check, text, tone);
            b.Margin = new Thickness(0, 12, 0, 0);
            _banner.Content = b;
            Ui.Rise(b, 0, 6, 260);
        }
    }
}
