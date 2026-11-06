using System;
using System.Collections.Generic;
using System.Linq;
using MonitoringTools.Clip.Smart;

namespace SmartTests
{
    public static partial class Program
    {
        private static void Power()
        {
            var m = new SnippetMatcher();
            var list = new List<Snippet> { new Snippet { Name = "Signature", Trigger = ";sig", Body = "Thanks,\nSam" }, new Snippet { Name = "x", Trigger = "brb", Body = "be right back" } };
            m.Snippets = () => list;
            Snippet hit = null;
            foreach (var c in "hello ;sig") hit = m.Feed(c) ?? hit;
            Check("snippet trigger", hit?.Name == "Signature");
            hit = null; foreach (var c in "xbrb") hit = m.Feed(c) ?? hit;
            Check("word trigger needs a word start", hit == null);
            m.Feed('\0'); hit = null; foreach (var c in "brb") hit = m.Feed(c) ?? hit;
            Check("word trigger at start", hit?.Name == "x");
            hit = null; foreach (var c in ";sx\bi\bg") hit = m.Feed(c) ?? hit;
            Check("backspace is understood", hit == null);
            m.Reset(); hit = null; foreach (var c in ";si") hit = m.Feed(c) ?? hit; hit = m.Feed('g');
            Check("trigger after backspaces", hit?.Name == "Signature");

            var e = SnippetMatcher.Fill("Hi {ask:Name},\n{cursor}\nSource: {clipboard:domain} {date}", new DateTime(2026, 11, 6, 16, 5, 0), "https://www.example.com/report", new Dictionary<string, string> { ["Name"] = "Alex" });
            Check("fill ask", e.Text.StartsWith("Hi Alex,"));
            Check("fill domain", e.Text.Contains("Source: example.com"), e.Text);
            Check("fill asks listed", e.Asks.SequenceEqual(new[] { "Name" }));
            Check("cursor offset", e.Text.Length - e.CursorFromEnd == "Hi Alex,\n".Length, e.CursorFromEnd.ToString());

            var r = new Recipe { Name = "Make a slug", Steps = { "trim", "lower", "accents", "symbols", "dash" } };
            Check("slug recipe", Transforms.Apply(r, "  Night Shift Monitor — Café Édition!  ") == "night-shift-monitor-cafe-edition", Transforms.Apply(r, "  Night Shift Monitor — Café Édition!  "));
            Check("strip tracking", Transforms.StripTracking("https://example.com/a?id=4&utm_source=x&fbclid=y#top") == "https://example.com/a?id=4#top");
            Check("strip all tracking", Transforms.StripTracking("https://example.com/a?utm_medium=x") == "https://example.com/a");
            Check("comma list", Transforms.Find("comma").Run("a\nb\n\nc") == "a, b, c");
            Check("rule lookup", AppRule.For(new[] { new AppRule { App = "msedge", Toast = "quiet" } }, "MSEDGE")?.Toast == "quiet");
        }
    }
}
