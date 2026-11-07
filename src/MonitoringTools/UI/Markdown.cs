using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A small Markdown renderer for toast previews: headings, bold, italic, strike, inline code,
    /// fenced code, bullet and numbered lists, task lists, quotes, links and bare URLs.
    /// </summary>
    public static class Markdown
    {
        private static readonly Regex Inline = new Regex(
            @"(?<code>`[^`]+`)|(?<bold>\*\*[^*]+\*\*|__[^_]+__)|(?<strike>~~[^~]+~~)|(?<ital>\*[^*\s][^*]*\*|_[^_\s][^_]*_)|(?<link>\[[^\]]+\]\([^)\s]+\))|(?<url>https?://[^\s)]+)",
            RegexOptions.Compiled);

        public static StackPanel Render(string md, int maxLines = 14)
        {
            var root = new StackPanel();
            var lines = md.Replace("\r\n", "\n").Split('\n');
            bool inCode = false;
            TextBlock code = null;
            int shown = 0;

            foreach (var raw in lines)
            {
                if (shown >= maxLines) { root.Children.Add(Para("…", Ui.Res("B.TextMute"))); break; }
                var line = raw.TrimEnd();

                if (line.TrimStart().StartsWith("```"))
                {
                    inCode = !inCode;
                    if (inCode)
                    {
                        code = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap };
                        root.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 2, 0, 6), Child = code });
                    }
                    continue;
                }
                if (inCode) { if (code.Inlines.Count > 0) code.Inlines.Add(new LineBreak()); code.Inlines.Add(new Run(line)); shown++; continue; }
                if (line.Trim().Length == 0) continue;

                var h = Regex.Match(line, @"^\s{0,3}(#{1,6})\s+(.*)$");
                if (h.Success)
                {
                    int level = h.Groups[1].Value.Length;
                    var tb = Para(null, Ui.Res("B.Text"));
                    tb.FontFamily = Ui.Font("F.Display");
                    tb.FontWeight = FontWeights.SemiBold;
                    tb.FontSize = level == 1 ? 22 : level == 2 ? 16 : 14;
                    AddInlines(tb, h.Groups[2].Value);
                    root.Children.Add(tb); shown++; continue;
                }

                var task = Regex.Match(line, @"^\s*[-*+]\s+\[( |x|X)\]\s+(.*)$");
                if (task.Success)
                {
                    root.Children.Add(Bullet(task.Groups[1].Value.Trim().Length > 0 ? "☑" : "☐", task.Groups[2].Value)); shown++; continue;
                }
                var bullet = Regex.Match(line, @"^(\s*)[-*+]\s+(.*)$");
                if (bullet.Success) { root.Children.Add(Bullet("•", bullet.Groups[2].Value, bullet.Groups[1].Value.Length)); shown++; continue; }
                var num = Regex.Match(line, @"^(\s*)(\d+)[.)]\s+(.*)$");
                if (num.Success) { root.Children.Add(Bullet(num.Groups[2].Value + ".", num.Groups[3].Value, num.Groups[1].Value.Length)); shown++; continue; }

                if (line.TrimStart().StartsWith(">"))
                {
                    var q = Para(null, Ui.Res("B.TextMute"));
                    q.FontStyle = FontStyles.Italic;
                    AddInlines(q, line.TrimStart().TrimStart('>').Trim());
                    root.Children.Add(new Border { BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(10, 0, 0, 0), Margin = new Thickness(0, 0, 0, 4), Child = q });
                    shown++; continue;
                }

                if (Regex.IsMatch(line, @"^\s*(-{3,}|\*{3,}|_{3,})\s*$"))
                {
                    root.Children.Add(new Border { Height = 1, Background = Ui.Res("B.Line"), Margin = new Thickness(0, 6, 0, 6) }); continue;
                }

                var p = Para(null, Ui.Res("B.TextSoft"));
                AddInlines(p, line);
                root.Children.Add(p); shown++;
            }
            return root;
        }

        private static TextBlock Para(string text, Brush fg)
        {
            var t = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = 12, Foreground = fg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4), LineHeight = 19 };
            if (text != null) t.Text = text;
            return t;
        }

        private static FrameworkElement Bullet(string mark, string text, int indent = 0)
        {
            var g = new Grid { Margin = new Thickness(Math.Min(indent, 8) * 6, 0, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var m = new TextBlock { Text = mark, Foreground = Ui.Res("B.Mint"), FontFamily = Ui.Font("F.Mono"), FontSize = 12, Margin = new Thickness(0, 1, 0, 0) };
            var t = Para(null, Ui.Res("B.TextSoft"));
            t.Margin = new Thickness(0);
            AddInlines(t, text);
            Grid.SetColumn(t, 1);
            g.Children.Add(m); g.Children.Add(t);
            return g;
        }

        public static void AddInlines(TextBlock tb, string text)
        {
            int last = 0;
            foreach (Match m in Inline.Matches(text))
            {
                if (m.Index > last) tb.Inlines.Add(new Run(text.Substring(last, m.Index - last)));
                var v = m.Value;
                if (m.Groups["code"].Success)
                {
                    // a run, not a box, so a long command wraps with the sentence instead of running out of the page
                    tb.Inlines.Add(new Run("\u2009" + v.Trim('`') + "\u2009") { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.Text"), Background = Ui.Res("B.Raised") });
                }
                else if (m.Groups["bold"].Success) tb.Inlines.Add(new Bold(new Run(v.Substring(2, v.Length - 4))) { Foreground = Ui.Res("B.Text") });
                else if (m.Groups["strike"].Success) tb.Inlines.Add(new Run(v.Substring(2, v.Length - 4)) { TextDecorations = TextDecorations.Strikethrough });
                else if (m.Groups["ital"].Success) tb.Inlines.Add(new Italic(new Run(v.Substring(1, v.Length - 2))));
                else if (m.Groups["link"].Success)
                {
                    var lm = Regex.Match(v, @"^\[([^\]]+)\]\(([^)\s]+)\)$");
                    tb.Inlines.Add(MakeLink(lm.Groups[1].Value, lm.Groups[2].Value));
                }
                else if (m.Groups["url"].Success) tb.Inlines.Add(MakeLink(v, v));
                last = m.Index + m.Length;
            }
            if (last < text.Length) tb.Inlines.Add(new Run(text.Substring(last)));
        }

        public static Hyperlink MakeLink(string label, string url)
        {
            var h = new Hyperlink(new Run(label)) { Foreground = Ui.Res("B.Mint"), Cursor = System.Windows.Input.Cursors.Hand };
            h.Click += (s, e) => Open(url);
            return h;
        }

        public static void Open(string url)
        {
            try
            {
                if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
                if (Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https" || u.Scheme == "mailto" || u.Scheme == "ms-settings"))
                    Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
