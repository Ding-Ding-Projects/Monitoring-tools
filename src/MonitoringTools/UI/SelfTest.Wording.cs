using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        // A sample personal wording file. The words are ordinary interface words, so every page has some to replace.
        private static readonly Dictionary<string, string> WordingSample = new Dictionary<string, string>
        {
            ["Clipboard"] = "Copy pile", ["Settings"] = "Knobs", ["Search"] = "Hunt", ["Version"] = "Edition",
            ["Overview"] = "Front room", ["Network"] = "Wires", ["Shortcuts"] = "Speed keys", ["Default"] = "Stock",
        };

        /// <summary>Every piece of interface text in a tree that still carries a phrase the wording file replaces.</summary>
        private static List<string> WordingLeftovers(DependencyObject root)
        {
            var rx = new Regex(@"(?<![\p{L}\p{N}])(" + string.Join("|", WordingSample.Keys.Select(Regex.Escape)) + @")(?![\p{L}\p{N}])", RegexOptions.IgnoreCase);
            var found = new List<string>();
            void Look(string kind, string text, DependencyObject at)
            {
                if (!string.IsNullOrEmpty(text) && rx.IsMatch(text)) found.Add(kind + ": " + text.Replace("\n", " / ") + " (" + at.GetType().Name + ")");
            }
            void Walk(DependencyObject d)
            {
                if (d is TextBox || d is PasswordBox) return;   // what is typed is the person's own text
                if (d is UIElement ui && !ui.IsVisible) return;
                if (Language.GetLiteral(d)) return;
                if (d is TextBlock tb) foreach (var r in tb.Inlines.OfType<Run>()) if (!Language.GetLiteral(r) && r.Parent is not Hyperlink) Look("text", r.Text, tb);
                if (d is FrameworkElement fe && fe.ToolTip is string tip) Look("tooltip", tip, fe);
                if (d is UIElement) Look("accessible name", AutomationProperties.GetName(d), d);
                int n = d is Visual ? VisualTreeHelper.GetChildrenCount(d) : 0;
                for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(d, i));
            }
            Walk(root);
            if (root is Window w) Look("window title", w.Title, w);
            return found;
        }

        private static async Task WordingChecksAsync(string dir)
        {
            await CheckAsync("wording: a loaded file reaches every label, survives tab switches and comes back after a restart", async () =>
            {
                if (Modes.SchoolActive) return "School mode was left on by an earlier check, and it keeps personal wording out";
                var file = Path.Combine(Settings.DataDir, "wording-sample.json");
                File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, entries = WordingSample }));
                var saved = Settings.Current.LanguageMode;
                Settings.Current.LanguageMode = LanguageMode.English;
                var cc = new ControlCentreWindow { Width = 1180, Height = 820 };
                try
                {
                    if (!PersonalWording.TryApply(file, out var msg)) return "the sample file was refused: " + msg;
                    cc.Show();
                    await Task.Delay(700);
                    var problems = new List<string>();
                    async Task Visit(string page, string label)
                    {
                        cc.Navigate(page, animate: false);
                        await Task.Delay(500);
                        foreach (var x in WordingLeftovers(cc).Distinct().Take(60)) problems.Add(label + " " + page + " · " + x);
                    }
                    foreach (var p in new[] { "overview", "clipboard", "network", "shortcuts", "service", "modes" }) await Visit(p, "first visit");
                    foreach (var p in new[] { "clipboard", "overview" }) await Visit(p, "after switching tabs");

                    // A restart: the app forgets the words in memory and loads them again from its own saved copy.
                    PersonalWording.ForgetForTest();
                    if (PersonalWording.Count != 0) return "the forget step did not forget";
                    PersonalWording.LoadCache();
                    if (PersonalWording.Count != WordingSample.Count || !PersonalWording.Active) return $"after a restart the file was not loaded again: {PersonalWording.Count} phrases, active {PersonalWording.Active}";
                    await Visit("network", "after a restart");
                    Language.Refresh();   // text the app writes again later must keep the wording too
                    await Task.Delay(200);
                    foreach (var x in WordingLeftovers(cc).Distinct().Take(60)) problems.Add("after a refresh network · " + x);

                    File.WriteAllLines(Path.Combine(dir, "wording-leftovers.txt"), problems);
                    return problems.Count == 0 ? null : problems.Count + " places still show the original wording: " + string.Join(" | ", problems.Take(25));
                }
                finally { PersonalWording.Clear(); Settings.Current.LanguageMode = saved; cc.Close(); }
            });
        }
    }
}
