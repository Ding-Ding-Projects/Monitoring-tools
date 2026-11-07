using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonitoringTools.Core
{
    /// <summary>The text rules of the Markdown renderer that need no window: emoji shortcodes and link resolution.</summary>
    public static class MarkdownText
    {
        /// <summary>GitHub-style shortcodes, as release notes and pull requests write them.</summary>
        private static readonly Dictionary<string, string> Emoji = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rocket"] = "🚀", ["tada"] = "🎉", ["sparkles"] = "✨", ["bug"] = "🐛", ["fire"] = "🔥", ["star"] = "⭐", ["star2"] = "🌟",
            ["white_check_mark"] = "✅", ["heavy_check_mark"] = "✔️", ["check"] = "✔️", ["x"] = "❌", ["warning"] = "⚠️", ["no_entry"] = "⛔",
            ["construction"] = "🚧", ["lock"] = "🔒", ["unlock"] = "🔓", ["memo"] = "📝", ["pencil"] = "📝", ["pencil2"] = "✏️",
            ["book"] = "📖", ["books"] = "📚", ["package"] = "📦", ["wrench"] = "🔧", ["hammer"] = "🔨", ["gear"] = "⚙️", ["zap"] = "⚡",
            ["art"] = "🎨", ["lipstick"] = "💄", ["recycle"] = "♻️", ["fire_engine"] = "🚒", ["ambulance"] = "🚑", ["arrow_up"] = "⬆️",
            ["arrow_down"] = "⬇️", ["arrow_right"] = "➡️", ["arrow_left"] = "⬅️", ["heart"] = "❤️", ["green_heart"] = "💚", ["thumbsup"] = "👍",
            ["+1"] = "👍", ["thumbsdown"] = "👎", ["-1"] = "👎", ["eyes"] = "👀", ["bulb"] = "💡", ["mag"] = "🔍", ["link"] = "🔗",
            ["calendar"] = "📅", ["clock"] = "🕒", ["hourglass"] = "⌛", ["chart_with_upwards_trend"] = "📈", ["bar_chart"] = "📊",
            ["globe_with_meridians"] = "🌐", ["computer"] = "💻", ["iphone"] = "📱", ["bell"] = "🔔", ["mute"] = "🔇", ["speaker"] = "🔈",
            ["camera"] = "📷", ["movie_camera"] = "🎥", ["musical_note"] = "🎵", ["floppy_disk"] = "💾", ["cd"] = "💿", ["dvd"] = "📀",
            ["file_folder"] = "📁", ["open_file_folder"] = "📂", ["page_facing_up"] = "📄", ["clipboard"] = "📋", ["pushpin"] = "📌",
            ["shield"] = "🛡️", ["test_tube"] = "🧪", ["boom"] = "💥", ["pray"] = "🙏", ["clap"] = "👏", ["wave"] = "👋",
            ["smile"] = "😄", ["joy"] = "😂", ["thinking"] = "🤔", ["sweat_smile"] = "😅", ["dumpling"] = "🥟", ["tea"] = "🍵",
            ["information_source"] = "ℹ️", ["exclamation"] = "❗", ["new"] = "🆕", ["up"] = "🆙", ["ok"] = "🆗",
            ["heavy_plus_sign"] = "➕", ["heavy_minus_sign"] = "➖", ["truck"] = "🚚", ["twisted_rightwards_arrows"] = "🔀", ["rewind"] = "⏪",
            ["globe"] = "🌐", ["earth_asia"] = "🌏", ["moon"] = "🌙", ["sunny"] = "☀️", ["umbrella"] = "☔", ["snowflake"] = "❄️",
        };

        private static readonly Regex Shortcode = new Regex(@":([a-z0-9_+\-]{1,40}):", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Text with every known :shortcode: replaced by its emoji. Unknown ones, and times such as 12:30:45, stay as typed.</summary>
        public static string WithEmoji(string text) =>
            string.IsNullOrEmpty(text) || text.IndexOf(':') < 0 ? text : Shortcode.Replace(text, m => Emoji.TryGetValue(m.Groups[1].Value, out var e) ? e : m.Value);

        /// <summary>An absolute link as it is, a relative one against the base address, or null when there is nothing to point at.</summary>
        public static string Resolve(string href, Uri baseUri)
        {
            if (string.IsNullOrWhiteSpace(href)) return null;
            if (Uri.TryCreate(href, UriKind.Absolute, out var abs) && abs.Scheme != "file") return href;
            if (href.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) return href;
            if (baseUri == null) return null;
            return Uri.TryCreate(baseUri, href, out var rel) ? rel.AbsoluteUri : null;
        }
    }
}
