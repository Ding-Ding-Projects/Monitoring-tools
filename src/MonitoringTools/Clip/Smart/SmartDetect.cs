using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>Runs every detector over copied text, most specific first, and returns the best fact plus any
    /// facts from turned-on extensions. Pure, so it is tested on any machine.</summary>
    public static class SmartDetect
    {
        /// <summary>Kinds the person turned off in settings, by detector group ("developer", "everyday", "numbers").</summary>
        public static Func<string, bool> GroupOn = _ => true;

        /// <summary>Extension detectors, set by the extension host. Never called for sensitive text.</summary>
        public static Func<string, IEnumerable<SmartFact>> Extensions;

        /// <summary>The home currency for money amounts.</summary>
        public static string HomeCurrency = "CAD";

        private static readonly (string Group, Func<string, SmartFact> Run)[] Ordered =
        {
            ("secrets", Developer.Secret),
            ("secrets", t => Developer.Jwt(t)),
            ("everyday", Everyday.Iban),
            ("developer", Developer.StackTrace),
            ("numbers", Developer2.Table),
            ("developer", Developer2.Sql),
            ("developer", Developer2.Xml),
            ("developer", Developer.ErrorCode),
            ("numbers", Units.Detect),
            ("numbers", t => Everyday2.Money(t, HomeCurrency)),
            ("everyday", t => Everyday.ZoneTime(t)),
            ("developer", t => Everyday.Timestamp(t)),
            ("developer", Developer.Hash),
            ("developer", Developer.GuidText),
            ("developer", Developer.Ip),
            ("developer", Developer.GitSshUrl),
            ("everyday", Everyday.Parcel),
            ("everyday", Everyday.Isbn),
            ("everyday", Everyday.Flight),
            ("everyday", Everyday.Phone),
            ("numbers", Everyday.Maths),
            ("developer", t => Everyday2.Cron(t)),
            ("developer", Developer2.Package),
            ("developer", Developer2.RegexText),
            ("developer", Developer2.WindowsPath),
            ("everyday", Everyday.Address),
            ("everyday", Everyday.Character),
            ("everyday", Everyday2.Language),
        };

        /// <summary>The single best built-in fact for this text, or null.</summary>
        public static SmartFact Best(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 200_000) return null;
            var t = text.Trim();
            foreach (var (group, run) in Ordered)
            {
                if (group != "secrets" && !GroupOn(group)) continue;
                SmartFact f;
                try { f = run(t); } catch { f = null; }
                if (f != null) { f.Data["group"] = group; return f; }
            }
            return null;
        }

        /// <summary>The best built-in fact first, then any extension facts. Sensitive text never reaches extensions.</summary>
        public static List<SmartFact> All(string text)
        {
            var list = new List<SmartFact>();
            var best = Best(text);
            if (best != null) list.Add(best);
            if (best?.Sensitive == true || Extensions == null || string.IsNullOrWhiteSpace(text)) return list;
            try { list.AddRange(Extensions(text.Trim()).Where(f => f != null)); } catch { }
            return list;
        }

        /// <summary>True when the copy must stay out of history and away from lookups and extensions.</summary>
        public static bool IsSensitive(string text) => Best(text)?.Sensitive == true;
    }
}
