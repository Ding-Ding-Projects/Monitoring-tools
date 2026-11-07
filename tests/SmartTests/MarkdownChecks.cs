using System;
using MonitoringTools.Core;

namespace SmartTests
{
    public static partial class Program
    {
        /// <summary>The Markdown renderer's text rules: emoji shortcodes and links relative to a base address.</summary>
        private static void MarkdownChecks()
        {
            Check("markdown emoji shortcode", MarkdownText.WithEmoji("Shipped :rocket: today :tada:") == "Shipped 🚀 today 🎉");
            Check("markdown unknown shortcode stays", MarkdownText.WithEmoji("a :not_a_real_one: b") == "a :not_a_real_one: b");
            Check("markdown times are not emoji", MarkdownText.WithEmoji("at 12:30:45") == "at 12:30:45");
            Check("markdown quick commands are not emoji", MarkdownText.WithEmoji("copy C:\\video.mov:mp4") == "copy C:\\video.mov:mp4");
            var b = new Uri("https://github.com/Ding-Ding-Projects/Monitoring-tools/blob/main/");
            Check("markdown relative link uses the base", MarkdownText.Resolve("docs/features/README.md", b) == "https://github.com/Ding-Ding-Projects/Monitoring-tools/blob/main/docs/features/README.md");
            Check("markdown parent link uses the base", MarkdownText.Resolve("../main/HANDOFF.md", b) == "https://github.com/Ding-Ding-Projects/Monitoring-tools/blob/main/HANDOFF.md");
            Check("markdown absolute link stays", MarkdownText.Resolve("https://example.com/a", b) == "https://example.com/a");
            Check("markdown relative link without a base is not a link", MarkdownText.Resolve("docs/a.md", null) == null);
        }
    }
}
