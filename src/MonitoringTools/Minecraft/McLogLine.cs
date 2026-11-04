using System;
using System.Text.RegularExpressions;

namespace MonitoringTools.Minecraft
{
    public enum McLogLevel { Info, Warn, Chat, Command }

    /// <summary>One console line split into its time, level and message, the way the console and the overview show it.</summary>
    public sealed class McLogLine
    {
        public string Time = "", Message = "", Raw = "";
        public McLogLevel Level;

        // Vanilla: "[14:02:11] [Server thread/INFO]: Done". Paper and itzg images: "[14:02:11 INFO]: Done".
        private static readonly Regex Vanilla = new Regex(@"^\[(?<t>\d{1,2}:\d{2}:\d{2})\]\s*\[(?<th>[^\]/]*)/(?<l>[A-Z]+)\][^:]*:\s?(?<m>.*)$", RegexOptions.Compiled);
        private static readonly Regex Paper = new Regex(@"^\[(?<t>\d{1,2}:\d{2}:\d{2})\s+(?<l>[A-Z]+)\]:?\s?(?<m>.*)$", RegexOptions.Compiled);
        // Bedrock: "[2026-11-04 10:00:00:123 INFO] Server started."
        private static readonly Regex Bedrock = new Regex(@"^\[\d{4}-\d{2}-\d{2} (?<t>\d{1,2}:\d{2}:\d{2})(:\d+)?\s+(?<l>[A-Z]+)\]\s?(?<m>.*)$", RegexOptions.Compiled);
        private static readonly Regex ChatMsg = new Regex(@"^(\[Not Secure\]\s*)?<[^>]{1,32}>\s", RegexOptions.Compiled);
        private static readonly Regex Colour = new Regex("§.", RegexOptions.Compiled);

        public static McLogLine Parse(string raw)
        {
            raw = Colour.Replace(raw ?? "", "").TrimEnd('\r');
            var l = new McLogLine { Raw = raw, Message = raw };
            var m = Vanilla.Match(raw);
            if (!m.Success) m = Paper.Match(raw);
            if (!m.Success) m = Bedrock.Match(raw);
            string level = "INFO";
            if (m.Success) { l.Time = m.Groups["t"].Value; l.Message = m.Groups["m"].Value; level = m.Groups["l"].Value; }
            if (level == "WARN" || level == "ERROR" || level == "FATAL") l.Level = McLogLevel.Warn;
            else if (ChatMsg.IsMatch(l.Message)) l.Level = McLogLevel.Chat;
            else if (l.Message.Contains("issued server command:", StringComparison.Ordinal) || raw.StartsWith("> ", StringComparison.Ordinal) || raw.StartsWith("[command] ", StringComparison.Ordinal)) l.Level = McLogLevel.Command;
            else l.Level = McLogLevel.Info;
            return l;
        }

        public static string LevelLabel(McLogLevel l) => l switch { McLogLevel.Warn => "WARN", McLogLevel.Chat => "CHAT", McLogLevel.Command => "CMD", _ => "INFO" };

        // ---- answers to the commands the overview sends

        // 1.13 and newer: "There are 3 of a max of 20 players online: Ava, Ben, Chloe". Older: "There are 3/20 players online:".
        private static readonly Regex ListNew = new Regex(@"There are (?<n>\d+) of a max(?: of)? (?<max>\d+) players online:?\s*(?<names>.*)$", RegexOptions.Compiled);
        private static readonly Regex ListOld = new Regex(@"There are (?<n>\d+)/(?<max>\d+) players online:?\s*(?<names>.*)$", RegexOptions.Compiled);
        // Paper: "TPS from last 1m, 5m, 15m: 20.0, 20.0, 20.0" (a leading * marks a value capped at 20).
        private static readonly Regex Tps = new Regex(@"TPS from last 1m, 5m, 15m:\s*\*?(?<a>\d+(\.\d+)?)", RegexOptions.Compiled);

        public static bool TryPlayers(string text, out int online, out int max, out string[] names)
        {
            online = max = 0; names = Array.Empty<string>();
            text = Colour.Replace(text ?? "", "");
            var m = ListNew.Match(text);
            if (!m.Success) m = ListOld.Match(text);
            if (!m.Success) return false;
            online = int.Parse(m.Groups["n"].Value); max = int.Parse(m.Groups["max"].Value);
            names = m.Groups["names"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return true;
        }

        public static bool TryTps(string text, out double tps)
        {
            tps = 0;
            var m = Tps.Match(Colour.Replace(text ?? "", ""));
            return m.Success && double.TryParse(m.Groups["a"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tps);
        }
    }
}
