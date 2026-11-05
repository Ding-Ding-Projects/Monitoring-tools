using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The help page: an offline reading experience over the Markdown articles embedded in the program.
        // Its content is built in this file only.

        private sealed class HelpView { public string Id; public string Anchor; public bool Results; }

        private HelpView _helpView = new HelpView();
        private readonly Stack<HelpView> _helpBack = new Stack<HelpView>();
        private readonly Dictionary<string, FrameworkElement> _helpAnchors = new Dictionary<string, FrameworkElement>();
        private StackPanel _helpHost;
        private SearchField _helpSearch;
        private DocArticle _helpArticle;

        private static readonly (string Title, string Icon, string Article, string Anchor)[] HelpTasks =
        {
            ("Convert a video", Icons.Video, "commands/convert-media", "convert-a-video"),
            ("Fix a broken connection", Icons.WifiOff, "network/internet-watch", "fix-a-broken-connection"),
            ("Push a repo from the clipboard", Icons.Git, "commands/push-a-repo", null),
            ("Make your own command", Icons.Braces, "commands/custom-shortcuts", "make-your-own-command"),
            ("Find something you copied", Icons.Clipboard, "clipboard/history", "find-something-you-copied"),
            ("Build a regex", Icons.Wand, "search/regex-builder", "views"),
            ("Keep popups quiet while gaming", Icons.Game, "game/game-mode", null),
            ("Run a Minecraft server", Icons.Box, "minecraft/servers", "create-a-server"),
        };

        private static string HelpCategoryIcon(string id) => id switch
        {
            "clipboard" => Icons.Clipboard,
            "commands" => Icons.Bolt,
            "network" => Icons.Wifi,
            "game" => Icons.Game,
            "files" => Icons.Folder,
            "minecraft" => Icons.Box,
            "search" => Icons.Search,
            "app" => Icons.Monitor,
            _ => Icons.Text,
        };

        private FrameworkElement HelpPage()
        {
            var root = PageRoot("Help", "Help and docs", "Every feature explained, offline. Pick a task, browse a topic, or search.");
            _helpSearch = new SearchField("help:articles", "Search the docs") { MinWidth = 640 };
            _helpSearch.SampleSource = () => Docs.All.Select(a => a.Title + ". " + a.Summary);
            _helpHost = new StackPanel { MaxWidth = 780, HorizontalAlignment = HorizontalAlignment.Left };
            root.Children.Add(_helpSearch);
            root.Children.Add(_helpHost);

            var field = _helpSearch;
            field.Filter += () => { if (field == _helpSearch) HelpOnSearch(); };
            if (field.State.IsActive && _helpView.Id == null) _helpView = new HelpView { Results = true };
            HelpShow(false);
            return new Grid { Children = { root } };
        }

        // -------------------------------------------------------------- navigation inside the page

        private void HelpOnSearch()
        {
            var st = _helpSearch.State;
            if (st.IsActive)
            {
                if (!_helpView.Results)
                {
                    if (_helpView.Id != null) HelpPush();
                    _helpView = new HelpView { Results = true };
                }
            }
            else if (_helpView.Results) _helpView = new HelpView();
            HelpShow(false);
        }

        private void HelpPush()
        {
            _helpBack.Push(_helpView);
            if (_helpBack.Count > 50)
            {
                var top = _helpBack.ToArray();   // most recent first
                _helpBack.Clear();
                for (int i = 49; i >= 0; i--) _helpBack.Push(top[i]);
            }
        }

        private void HelpOpen(string id, string anchor)
        {
            HelpPush();
            _helpView = new HelpView { Id = id, Anchor = anchor };
            HelpShow(true);
        }

        private void HelpGoBack()
        {
            _helpView = _helpBack.Count > 0 ? _helpBack.Pop() : new HelpView();
            HelpShow(true);
        }

        private void HelpHome()
        {
            _helpBack.Clear();
            _helpSearch.State.SetQuery("", _helpSearch);
            _helpView = new HelpView();
            HelpShow(true);
        }

        private void HelpShow(bool scroll)
        {
            if (_helpHost == null) return;
            _helpHost.Children.Clear();
            _helpAnchors.Clear();
            _helpArticle = null;
            var st = _helpSearch.State;
            var view = _helpView;
            if (view.Results && !st.IsActive) view = _helpView = new HelpView();

            if (Docs.All.Count == 0)
            {
                _helpSearch.SetCount(0, 0, "articles");
                _helpHost.Children.Add(Card(Ui.Text("The documentation bundled with this build could not be loaded, so there is nothing to show here. Reinstalling the program restores it.")));
                return;
            }

            if (view.Results) HelpBuildResults();
            else
            {
                _helpSearch.SetCount(Docs.All.Count, Docs.All.Count, "articles");
                var a = view.Id == null ? null : Docs.Find(view.Id);
                if (a == null) HelpBuildLanding(); else HelpBuildArticle(a, view.Anchor);
            }

            if (scroll)
            {
                if (view.Anchor != null && _helpArticle != null) HelpScrollTo(view.Anchor);
                else Scroller.ScrollToTop();
            }
        }

        private void HelpScrollTo(string anchor)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (anchor != null && _helpAnchors.TryGetValue(anchor, out var el))
                {
                    el.BringIntoView(new Rect(0, 0, Math.Max(el.ActualWidth, 1), Math.Max(el.ActualHeight, 420)));
                    if (el is TextBlock tb) tb.Foreground = Ui.Res("B.Mint");
                }
                else Scroller.ScrollToTop();
            }), DispatcherPriority.Loaded);
        }

        private void HelpLink(DocArticle from, string href)
        {
            if (!Docs.TryResolve(from, href, out var target, out var anchor)) { Markdown.Open(href); return; }
            if (target == null) return;
            if (target == from)
            {
                if (anchor != null) HelpScrollTo(anchor);
                return;
            }
            HelpOpen(target.Id, anchor);
        }

        // -------------------------------------------------------------- landing

        private void HelpBuildLanding()
        {
            var h = _helpHost;
            h.Children.Add(HelpSectionTitle("What do you want to do?", "Each one opens the article at the right step."));
            var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -14, 0) };
            foreach (var (title, icon, id, anchor) in HelpTasks)
            {
                var art = Docs.Find(id);
                if (art == null) continue;
                var sp = new StackPanel();
                var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                top.Children.Add(Ui.IconChip(icon, Ui.Res("B.Mint"), Ui.Res("B.MintWash")));
                top.Children.Add(new TextBlock { Text = title, Style = Ui.Style("T.Title"), FontSize = 14, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(top);
                sp.Children.Add(new TextBlock { Text = art.Summary, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), MaxHeight = 52, TextTrimming = TextTrimming.CharacterEllipsis });
                sp.Children.Add(Ui.Mono("Opens: " + art.Title, Ui.Res("B.TextMute"), 11));
                ((TextBlock)sp.Children[2]).Margin = new Thickness(0, 8, 0, 0);
                var card = Card(sp, null, 16);
                card.Margin = new Thickness(0, 0, 14, 14);
                var aid = art.Id; var aan = anchor;
                HelpClickable(card, "Task: " + title, () => HelpOpen(aid, aan));
                SearchScope.SetItem(card, title + " " + art.Title + " " + art.Summary);
                grid.Children.Add(card);
            }
            h.Children.Add(grid);

            h.Children.Add(HelpSectionTitle("Browse by topic", "Every article, grouped by what it is about."));
            var categories = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -14, 0) };
            foreach (var c in Docs.Categories)
            {
                var sp = new StackPanel();
                var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                top.Children.Add(Ui.IconChip(HelpCategoryIcon(c.Id), Ui.Res("B.Violet"), Ui.Res("B.VioletWash")));
                top.Children.Add(new TextBlock { Text = c.Title, Style = Ui.Style("T.Title"), FontSize = 14, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(top);
                sp.Children.Add(new TextBlock { Text = c.Intro, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), MaxHeight = 52, TextTrimming = TextTrimming.CharacterEllipsis });
                var count = c.Articles.Count;
                var cnt = Ui.Mono(count + (count == 1 ? " article" : " articles"), Ui.Res("B.TextMute"), 11);
                cnt.Margin = new Thickness(0, 8, 0, 0);
                sp.Children.Add(cnt);
                var card = Card(sp, null, 16);
                card.Margin = new Thickness(0, 0, 14, 14);
                var cid = c.Id;
                HelpClickable(card, "Topic: " + c.Title, () => HelpOpen(cid, null));
                SearchScope.SetItem(card, c.Title + " " + c.Intro);
                categories.Children.Add(card);
            }
            h.Children.Add(categories);

            var readme = Docs.Find("readme");
            var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            if (readme != null) foot.Children.Add(Ui.Button("Read the project overview", () => HelpOpen("readme", null), "Btn.Ghost", Icons.Text));
            h.Children.Add(foot);
            h.Children.Add(new TextBlock
            {
                Text = "These pages are documentation. They describe what the program does; nothing on this page runs any of it, and the screenshots inside the articles are from the real program.",
                Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 4, 0, 0)
            });
        }

        private static FrameworkElement HelpSectionTitle(string title, string sub)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
            sp.Children.Add(new TextBlock { Text = title, Style = Ui.Style("T.Title"), FontSize = 16 });
            if (sub != null) sp.Children.Add(new TextBlock { Text = sub, Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 3, 0, 0) });
            return sp;
        }

        // -------------------------------------------------------------- search results

        private void HelpBuildResults()
        {
            var st = _helpSearch.State;
            st.TimedOut = false;
            var hits = new List<(DocArticle A, int Score)>();
            foreach (var a in Docs.All)
            {
                bool t = st.Matches(a.Title);
                bool body = t || st.Matches(a.SearchText);
                if (!body) continue;
                hits.Add((a, (t ? 2 : 0) + (a.IsIndex ? 0 : 1)));
            }
            var ordered = hits.OrderByDescending(x => x.Score).ThenBy(x => x.A.Title, StringComparer.OrdinalIgnoreCase).ToList();
            _helpSearch.SetCount(ordered.Count, Docs.All.Count, "articles");

            var head = new TextBlock
            {
                Text = st.Error != null ? "The pattern is not valid, so every article is listed." : ordered.Count == 1 ? "1 article matches" : ordered.Count + " articles match",
                Style = Ui.Style("T.Title"), FontSize = 16, Margin = new Thickness(0, 0, 0, 12)
            };
            _helpHost.Children.Add(head);

            if (ordered.Count == 0)
            {
                var none = new StackPanel();
                none.Children.Add(Ui.Text("Nothing in the docs matches that. Try fewer words, or switch the search to a pattern with the regex button."));
                none.Children.Add(Ui.Button("Back to all docs", HelpHome, "Btn.Ghost", Icons.Restart));
                _helpHost.Children.Add(Card(none));
                return;
            }

            foreach (var (a, _) in ordered)
            {
                var sp = new StackPanel();
                var title = new TextBlock { Style = Ui.Style("T.Title"), FontSize = 14 };
                HelpHighlight(title, a.Title, HelpRanges(a.Title, st));
                sp.Children.Add(title);
                var category = Docs.CategoryOf(a);
                var crumb = Ui.Mono(a.IsIndex ? "Topic" : (category?.Title ?? "Docs"), Ui.Res("B.TextMute"), 11);
                crumb.Margin = new Thickness(0, 2, 0, 6);
                sp.Children.Add(crumb);
                var snip = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12 };
                var text = HelpSnippet(a, st);
                HelpHighlight(snip, text, HelpRanges(text, st));
                sp.Children.Add(snip);
                var card = Card(sp, null, 16);
                var id = a.Id;
                HelpClickable(card, "Article: " + a.Title, () => HelpOpen(id, null));
                _helpHost.Children.Add(card);
            }
        }

        private static string HelpSnippet(DocArticle a, SearchState st)
        {
            var body = string.Join(" ", a.SearchText.Split('\n').Skip(1 + Math.Max(1, a.Headings.Count)));
            body = Regex.Replace(body, @"\s+", " ").Trim();
            var r = HelpRanges(body, st);
            if (r.Count == 0)
                return a.Summary.Length > 180 ? a.Summary.Substring(0, 180).TrimEnd() + "…" : a.Summary;
            int start = Math.Max(0, r[0].Start - 70);
            if (start > 0)
            {
                var sp = body.IndexOf(' ', start);
                if (sp >= 0 && sp < r[0].Start) start = sp + 1;
            }
            int end = Math.Min(body.Length, r[0].Start + r[0].Length + 130);
            var s = body.Substring(start, end - start).Trim();
            return (start > 0 ? "…" : "") + s + (end < body.Length ? "…" : "");
        }

        /// <summary>The spans of <paramref name="text"/> the current search matches, merged and in order.</summary>
        private static List<(int Start, int Length)> HelpRanges(string text, SearchState st)
        {
            var raw = new List<(int Start, int Length)>();
            if (string.IsNullOrEmpty(text) || !st.IsActive) return raw;
            if (st.IsRegex)
            {
                if (st.Regex != null)
                {
                    try
                    {
                        foreach (Match m in st.Regex.Matches(text))
                        {
                            if (m.Length > 0) raw.Add((m.Index, m.Length));
                            if (raw.Count >= 40) break;
                        }
                    }
                    catch (RegexMatchTimeoutException) { }
                }
            }
            else
            {
                var cmp = st.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                foreach (var w in st.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    int i = 0;
                    while (raw.Count < 80 && (i = text.IndexOf(w, i, cmp)) >= 0) { raw.Add((i, w.Length)); i += w.Length; }
                }
            }
            raw.Sort((x, y) => x.Start.CompareTo(y.Start));
            var merged = new List<(int Start, int Length)>();
            foreach (var r in raw)
            {
                if (merged.Count > 0 && r.Start <= merged[^1].Start + merged[^1].Length)
                {
                    var last = merged[^1];
                    merged[^1] = (last.Start, Math.Max(last.Start + last.Length, r.Start + r.Length) - last.Start);
                }
                else merged.Add(r);
            }
            return merged;
        }

        private static void HelpHighlight(TextBlock tb, string text, List<(int Start, int Length)> ranges)
        {
            int last = 0;
            foreach (var (s, l) in ranges)
            {
                if (s > last) tb.Inlines.Add(new Run(text.Substring(last, s - last)));
                tb.Inlines.Add(new Run(text.Substring(s, l)) { Background = Ui.Res("B.MintWash"), Foreground = Ui.Res("B.Mint"), FontWeight = FontWeights.SemiBold });
                last = s + l;
            }
            if (last < text.Length) tb.Inlines.Add(new Run(text.Substring(last)));
        }

        private static void HelpClickable(Border b, string name, Action act)
        {
            b.Cursor = Cursors.Hand;
            b.Focusable = true;
            b.FocusVisualStyle = null;
            AutomationProperties.SetName(b, name);
            b.MouseLeftButtonUp += (s, e) => { act(); e.Handled = true; };
            b.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { act(); e.Handled = true; } };
            b.GotKeyboardFocus += (s, e) => b.BorderBrush = Ui.Res("B.Mint");
            b.LostKeyboardFocus += (s, e) => b.BorderBrush = Ui.Res("B.Line");
        }

        // -------------------------------------------------------------- article reader

        private void HelpBuildArticle(DocArticle a, string anchor)
        {
            _helpArticle = a;
            var h = _helpHost;
            var category = Docs.CategoryOf(a);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            bar.Children.Add(Ui.Button("Back", HelpGoBack, "Btn.Ghost", Icons.Restart));
            bar.Children.Add(Ui.Button("All docs", HelpHome, "Btn.Ghost", Icons.Text));
            h.Children.Add(bar);

            var crumb = a.Id == "readme" ? "Project overview" : a.IsIndex ? "Topic" : (category?.Title ?? "Docs");
            h.Children.Add(new TextBlock { Text = crumb.ToUpperInvariant(), Style = Ui.Style("T.Eyebrow"), FontSize = 12 });
            h.Children.Add(new TextBlock { Text = a.Title, Style = Ui.Style("T.Title"), FontSize = 28, Margin = new Thickness(0, 4, 0, 14), TextWrapping = TextWrapping.Wrap });

            var body = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var related = HelpRender(a, body);
            h.Children.Add(body);

            if (a.IsIndex)
            {
                var cc = Docs.Categories.FirstOrDefault(c => c.Id == a.Category);
                if (cc != null && cc.Articles.Count > 0)
                {
                    h.Children.Add(HelpSectionTitle("Articles in this topic", null));
                    foreach (var x in cc.Articles) h.Children.Add(HelpNextCard(x));
                }
            }

            var rel = related.Select(r => Docs.TryResolve(a, r, out var t, out _) ? t : null).Where(t => t != null && t != a).Distinct().ToList();
            if (rel.Count > 0)
            {
                h.Children.Add(HelpSectionTitle("Related next steps", null));
                foreach (var t in rel) h.Children.Add(HelpNextCard(t));
            }

            var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            foot.Children.Add(Ui.Button("Open on GitHub", () => Markdown.Open(Docs.SourceBase + a.SourcePath), "Btn.Ghost", Icons.Globe));
            h.Children.Add(foot);
            h.Children.Add(new TextBlock
            {
                Text = "Optional source link. This page is the full article, available offline; the link needs an internet connection.",
                Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute")
            });
        }

        private FrameworkElement HelpNextCard(DocArticle t)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = t.Title, Style = Ui.Style("T.Title"), FontSize = 14 });
            sp.Children.Add(new TextBlock { Text = t.Summary, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(0, 3, 0, 0), MaxHeight = 40, TextTrimming = TextTrimming.CharacterEllipsis });
            var card = Card(sp, null, 14);
            card.Margin = new Thickness(0, 0, 0, 10);
            var id = t.Id;
            HelpClickable(card, "Open " + t.Title, () => HelpOpen(id, null));
            return card;
        }

        private static readonly Regex HelpInline = new Regex(
            @"(?<code>`[^`]+`)|(?<bold>\*\*[^*]+\*\*|__[^_]+__)|(?<strike>~~[^~]+~~)|(?<ital>\*[^*\s][^*]*\*|_[^_\s][^_]*_)|(?<link>\[[^\]]+\]\([^)\s]+\))|(?<url>https?://[^\s)]+)",
            RegexOptions.Compiled);

        private void HelpInlines(TextBlock tb, string text, DocArticle from)
        {
            int last = 0;
            foreach (Match m in HelpInline.Matches(text))
            {
                if (m.Index > last) tb.Inlines.Add(new Run(text.Substring(last, m.Index - last)));
                var v = m.Value;
                if (m.Groups["code"].Success)
                {
                    tb.Inlines.Add(new InlineUIContainer(new Border
                    {
                        Background = Ui.Res("B.Raised"), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 0, 5, 1),
                        Child = new TextBlock { Text = v.Trim('`'), FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.Text") }
                    }) { BaselineAlignment = BaselineAlignment.Center });
                }
                else if (m.Groups["bold"].Success) tb.Inlines.Add(new Bold(new Run(v.Substring(2, v.Length - 4))) { Foreground = Ui.Res("B.Text") });
                else if (m.Groups["strike"].Success) tb.Inlines.Add(new Run(v.Substring(2, v.Length - 4)) { TextDecorations = TextDecorations.Strikethrough });
                else if (m.Groups["ital"].Success) tb.Inlines.Add(new Italic(new Run(v.Substring(1, v.Length - 2))));
                else if (m.Groups["link"].Success)
                {
                    var lm = Regex.Match(v, @"^\[([^\]]+)\]\(([^)\s]+)\)$");
                    var href = lm.Groups[2].Value;
                    var link = new Hyperlink(new Run(lm.Groups[1].Value)) { Foreground = Ui.Res("B.Mint"), Cursor = Cursors.Hand };
                    link.Click += (s, e) => HelpLink(from, href);
                    tb.Inlines.Add(link);
                }
                else if (m.Groups["url"].Success) tb.Inlines.Add(Markdown.MakeLink(v, v));
                last = m.Index + m.Length;
            }
            if (last < text.Length) tb.Inlines.Add(new Run(text.Substring(last)));
        }

        private TextBlock HelpPara(string text, DocArticle from, double size = 14, Brush fg = null)
        {
            var t = new TextBlock { FontFamily = Ui.Font("F.Body"), FontSize = size, Foreground = fg ?? Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, LineHeight = size + 8, Margin = new Thickness(0, 0, 0, 10) };
            HelpInlines(t, text, from);
            return t;
        }

        private static bool HelpIsBlockStart(string line) =>
            line.TrimStart().StartsWith("```") || Regex.IsMatch(line, @"^\s{0,3}#{1,6}\s") || line.TrimStart().StartsWith("|") ||
            Regex.IsMatch(line, @"^\s*([-*+]|\d+[.)])\s+") || line.TrimStart().StartsWith(">") || Regex.IsMatch(line, @"^\s*!\[[^\]]*\]\([^)]+\)\s*$") ||
            Regex.IsMatch(line, @"^\s*(-{3,}|\*{3,}|_{3,})\s*$");

        /// <summary>Renders an article body into <paramref name="into"/> and returns the links found under its "Related" heading.</summary>
        private List<string> HelpRender(DocArticle a, StackPanel into)
        {
            var related = new List<string>();
            var lines = a.Markdown.Split('\n');
            bool titleSeen = false, inRelated = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd();
                if (line.Trim().Length == 0) continue;

                if (line.TrimStart().StartsWith("```"))
                {
                    var code = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap };
                    var sb = new List<string>();
                    for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith("```"); i++) sb.Add(lines[i].TrimEnd());
                    code.Text = string.Join("\n", sb);
                    var copy = Ui.Button("Copy", () => { try { Clipboard.SetText(string.Join("\n", sb)); } catch { } }, "Btn.Chip");
                    copy.Margin = new Thickness(0, 4, 4, 0);
                    copy.HorizontalAlignment = HorizontalAlignment.Right;
                    copy.VerticalAlignment = VerticalAlignment.Top;
                    AutomationProperties.SetName(copy, "Copy this example text");
                    var g = new Grid();
                    g.Children.Add(code);
                    g.Children.Add(copy);
                    into.Children.Add(new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 2, 0, 12), Child = g });
                    continue;
                }

                var hm = Regex.Match(line, @"^\s{0,3}(#{1,6})\s+(.*)$");
                if (hm.Success)
                {
                    int level = hm.Groups[1].Length;
                    var text = hm.Groups[2].Value;
                    if (level == 1 && !titleSeen) { titleSeen = true; continue; }
                    inRelated = level == 2 && Docs.StripInline(text).Trim().Equals("Related", StringComparison.OrdinalIgnoreCase);
                    if (inRelated) continue;
                    var tb = new TextBlock
                    {
                        FontFamily = Ui.Font("F.Display"), FontWeight = FontWeights.SemiBold, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap,
                        FontSize = level <= 2 ? 20 : level == 3 ? 16 : 14, Margin = new Thickness(0, level <= 2 ? 18 : 12, 0, 8)
                    };
                    HelpInlines(tb, text, a);
                    AutomationProperties.SetHeadingLevel(tb, level <= 2 ? AutomationHeadingLevel.Level2 : AutomationHeadingLevel.Level3);
                    var slug = Docs.Slug(text);
                    if (!_helpAnchors.ContainsKey(slug)) _helpAnchors[slug] = tb;
                    into.Children.Add(tb);
                    continue;
                }

                if (inRelated)
                {
                    foreach (Match lm in Regex.Matches(line, @"\[[^\]]+\]\(([^)\s]+)\)")) related.Add(lm.Groups[1].Value);
                    continue;
                }

                if (line.TrimStart().StartsWith("|") && i + 1 < lines.Length && Regex.IsMatch(lines[i + 1], @"^\s*\|?\s*:?-{2,}"))
                {
                    var rows = new List<string[]>();
                    rows.Add(HelpCells(line));
                    i += 2;
                    for (; i < lines.Length && lines[i].TrimStart().StartsWith("|"); i++) rows.Add(HelpCells(lines[i]));
                    i--;
                    into.Children.Add(HelpTable(rows, a));
                    continue;
                }

                var im = Regex.Match(line, @"^\s*!\[([^\]]*)\]\(([^)]+)\)\s*$");
                if (im.Success)
                {
                    var img = HelpImage(im.Groups[1].Value, im.Groups[2].Value);
                    if (img != null) into.Children.Add(img);
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*(-{3,}|\*{3,}|_{3,})\s*$"))
                {
                    into.Children.Add(new Border { Height = 1, Background = Ui.Res("B.Line"), Margin = new Thickness(0, 8, 0, 12) });
                    continue;
                }

                var li = Regex.Match(line, @"^(\s*)([-*+]|\d+[.)])\s+(.*)$");
                if (li.Success)
                {
                    var mark = Char.IsDigit(li.Groups[2].Value[0]) ? li.Groups[2].Value.TrimEnd(')') : "•";
                    if (mark != "•" && !mark.EndsWith(".")) mark += ".";
                    var text = li.Groups[3].Value;
                    var task = Regex.Match(text, @"^\[( |x|X)\]\s+(.*)$");
                    if (task.Success) { mark = task.Groups[1].Value.Trim().Length > 0 ? "☑" : "☐"; text = task.Groups[2].Value; }
                    var gr = new Grid { Margin = new Thickness(Math.Min(li.Groups[1].Length / 2, 6) * 18, 0, 0, 4) };
                    gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                    gr.ColumnDefinitions.Add(new ColumnDefinition());
                    var m = new TextBlock { Text = mark, Foreground = Ui.Res("B.Mint"), FontFamily = Ui.Font("F.Mono"), FontSize = 12, Margin = new Thickness(0, 2, 0, 0) };
                    var t = HelpPara(text, a);
                    t.Margin = new Thickness(0);
                    Grid.SetColumn(t, 1);
                    gr.Children.Add(m); gr.Children.Add(t);
                    into.Children.Add(gr);
                    continue;
                }

                if (line.TrimStart().StartsWith(">"))
                {
                    var q = HelpPara(line.TrimStart().TrimStart('>').Trim(), a, 14, Ui.Res("B.TextMute"));
                    q.FontStyle = FontStyles.Italic;
                    into.Children.Add(new Border { BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 0, 0, 0), Margin = new Thickness(0, 0, 0, 10), Child = q });
                    continue;
                }

                var para = new List<string> { line.Trim() };
                while (i + 1 < lines.Length && lines[i + 1].Trim().Length > 0 && !HelpIsBlockStart(lines[i + 1])) para.Add(lines[++i].Trim());
                into.Children.Add(HelpPara(string.Join(" ", para), a));
            }
            return related;
        }

        private static string[] HelpCells(string line)
        {
            var t = line.Trim();
            if (t.StartsWith("|")) t = t.Substring(1);
            if (t.EndsWith("|")) t = t.Substring(0, t.Length - 1);
            return t.Split('|').Select(c => c.Trim()).ToArray();
        }

        private FrameworkElement HelpTable(List<string[]> rows, DocArticle a)
        {
            int cols = rows.Max(r => r.Length);
            var g = new Grid();
            for (int c = 0; c < cols; c++)
            {
                int longest = rows.Max(r => c < r.Length ? Docs.StripInline(r[c]).Length : 0);
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Min(Math.Max(longest, 8), 60), GridUnitType.Star) });
            }
            for (int r = 0; r < rows.Count; r++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int c = 0; c < cols; c++)
                {
                    var text = c < rows[r].Length ? rows[r][c] : "";
                    var tb = new TextBlock
                    {
                        FontFamily = Ui.Font("F.Body"), FontSize = 12, TextWrapping = TextWrapping.Wrap,
                        Foreground = r == 0 ? Ui.Res("B.Text") : Ui.Res("B.TextSoft"), FontWeight = r == 0 ? FontWeights.SemiBold : FontWeights.Normal
                    };
                    HelpInlines(tb, text, a);
                    var cell = new Border
                    {
                        Padding = new Thickness(10, 7, 10, 7), Child = tb,
                        Background = r == 0 ? Ui.Res("B.Raised") : Brushes.Transparent,
                        BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 0, 0, 1)
                    };
                    Grid.SetRow(cell, r); Grid.SetColumn(cell, c);
                    g.Children.Add(cell);
                }
            }
            return new Border { Background = Ui.Res("B.Surface"), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), ClipToBounds = true, Margin = new Thickness(0, 2, 0, 14), Child = g };
        }

        private static FrameworkElement HelpImage(string alt, string src)
        {
            var bytes = Docs.Image(src);
            if (bytes == null) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                var img = new Image { Source = bmp, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = 740, HorizontalAlignment = HorizontalAlignment.Left };
                AutomationProperties.SetName(img, alt);
                var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 14) };
                sp.Children.Add(new Border { CornerRadius = new CornerRadius(12), BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = true, Child = img });
                if (alt.Length > 0) sp.Children.Add(new TextBlock { Text = alt, Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(2, 5, 0, 0) });
                return sp;
            }
            catch { return null; }
        }
    }
}
