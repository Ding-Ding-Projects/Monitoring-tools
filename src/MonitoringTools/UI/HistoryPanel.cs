using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The version list for one saved text: when each copy was kept and how large it is, a line comparison against the text
    /// as it is now, and a Restore that asks for the two-key confirmation. Nothing here is uploaded anywhere.
    /// </summary>
    public static class HistoryPanel
    {
        public static FrameworkElement Build(string key, Func<string> current, Action<string> restore, string noun)
        {
            var root = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            root.Children.Add(new TextBlock
            {
                Text = $"History of this {noun}",
                Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 2)
            });
            root.Children.Add(new TextBlock
            {
                Text = $"Every save keeps a copy on this computer, up to the limit set on the Records page; the oldest is dropped first. Pick a version to compare it with the text as it is now. Restore puts that version in the editor and saves it, after you confirm. The text you are replacing is kept in the history first.",
                Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
            });

            var search = new SearchField("history:" + key, "Search saved versions") { Margin = new Thickness(0, 0, 0, 8) };
            var rows = new StackPanel();
            var diffBox = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, TextWrapping = TextWrapping.NoWrap, Foreground = Ui.Res("B.TextSoft") };
            var diffScroll = new ScrollViewer { Content = diffBox, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10) };
            var diffFrame = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(12), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), Child = diffScroll, Margin = new Thickness(0, 8, 0, 8), Visibility = Visibility.Collapsed };
            AutomationProperties.SetName(diffFrame, "Comparison with the current text");
            var legend = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            var lockedNote = new TextBlock { Text = "The history is locked. Unlock it on the Records page to see and restore earlier versions.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
            var empty = new TextBlock { Text = "No earlier versions yet. The first save creates one.", Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 0, 0, 4) };

            List<(HistoryVersion V, string Text)> versions = new List<(HistoryVersion, string)>();
            HistoryVersion selected = null;
            Button restoreBtn = null;

            void Show(HistoryVersion v, string text)
            {
                selected = v;
                restoreBtn.IsEnabled = true;
                var lines = ScriptHistory.Diff(current(), text);
                diffBox.Inlines.Clear();
                int plus = lines.Count(l => l.Op == '+'), minus = lines.Count(l => l.Op == '-');
                foreach (var l in lines)
                {
                    var run = new Run((l.Op == ' ' ? "  " : l.Op + " ") + l.Text + "\n")
                    {
                        Foreground = l.Op == '+' ? Ui.Res("B.Mint") : l.Op == '-' ? Ui.Res("B.Danger") : Ui.Res("B.TextSoft")
                    };
                    diffBox.Inlines.Add(run);
                }
                diffFrame.Visibility = Visibility.Visible;
                legend.Visibility = Visibility.Visible;
                legend.Text = plus == 0 && minus == 0
                    ? "This version is identical to the text in the editor."
                    : $"Restoring this version would add the {plus} line{(plus == 1 ? "" : "s")} marked + and remove the {minus} line{(minus == 1 ? "" : "s")} marked -.";
                Render();
            }

            void Render()
            {
                rows.Children.Clear();
                int shown = 0;
                foreach (var (v, text) in versions)
                {
                    if (search.State.IsActive && !search.State.Matches(v.Time.ToString("f") + " " + text)) continue;
                    shown++;
                    var vv = v; var tt = text;
                    var label = $"{v.Time:yyyy-MM-dd HH:mm:ss}   ·   {FormatSize(v.Bytes)}";
                    var b = Ui.Button(label, () => Show(vv, tt), "Btn.Row");
                    b.Height = 40; b.Margin = new Thickness(0, 1, 0, 1);
                    if (selected != null && selected.Id == v.Id) b.Background = Ui.Res("B.SecondaryContainer");
                    AutomationProperties.SetName(b, "Version saved " + v.Time.ToString("f") + ", " + FormatSize(v.Bytes));
                    rows.Children.Add(SearchScope.SetItem(b, v.Time.ToString("f") + " " + text));
                }
                search.SetCount(shown, versions.Count, "versions");
                lockedNote.Visibility = ScriptHistory.IsLocked ? Visibility.Visible : Visibility.Collapsed;
                empty.Visibility = versions.Count == 0 && !ScriptHistory.IsLocked ? Visibility.Visible : Visibility.Collapsed;
                search.Visibility = versions.Count == 0 || ScriptHistory.IsLocked ? Visibility.Collapsed : Visibility.Visible;
            }

            void Reload()
            {
                versions = ScriptHistory.List(key).Select(v => (v, ScriptHistory.Read(key, v.Id) ?? "")).ToList();
                selected = null;
                restoreBtn.IsEnabled = false;
                diffFrame.Visibility = Visibility.Collapsed;
                legend.Visibility = Visibility.Collapsed;
                Render();
            }

            restoreBtn = Ui.Button("Restore this version", () =>
            {
                if (selected == null) return;
                var pick = selected;
                var text = ScriptHistory.Read(key, pick.Id);
                if (text == null) return;
                var dlg = new ConfirmWindow($"Restore the version from {pick.Time:yyyy-MM-dd HH:mm}?",
                    $"The {noun} in the editor is replaced by this saved version and saved. The text you are replacing is kept in the history, so you can come back to it.",
                    "Restore", danger: true);
                dlg.Confirmed += _ =>
                {
                    ScriptHistory.Save(key, current());   // keep what is being replaced
                    restore(text);
                    Reload();
                };
                dlg.Show();
            }, "Btn.Accent", Icons.Restart);
            restoreBtn.IsEnabled = false;
            restoreBtn.ToolTip = "Replaces the text in the editor with the selected version, after a two-key confirmation. Off until you pick a version.";

            search.Filter += Render;
            root.Children.Add(lockedNote);
            root.Children.Add(empty);
            root.Children.Add(search);
            root.Children.Add(rows);
            root.Children.Add(legend);
            root.Children.Add(diffFrame);
            root.Children.Add(restoreBtn);
            root.Loaded += (s, e) => Reload();
            return root;
        }

        public static string FormatSize(long bytes) => bytes < 1024 ? bytes + " B" : (bytes / 1024.0).ToString("0.0") + " KB";
    }
}
