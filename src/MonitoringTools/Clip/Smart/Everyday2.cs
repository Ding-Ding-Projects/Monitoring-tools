using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>Money amounts, cron schedules and the language of a copied sentence.</summary>
    public static class Everyday2
    {
        // ------------------------------------------------------------------ money

        public static readonly string[] Currencies = { "USD", "CAD", "EUR", "GBP", "JPY", "HKD", "CNY", "AUD", "CHF", "INR", "MXN", "KRW", "SGD", "NZD", "SEK" };
        private static readonly Dictionary<string, string> Signs = new Dictionary<string, string>
        { ["$"] = "USD", ["US$"] = "USD", ["C$"] = "CAD", ["CA$"] = "CAD", ["€"] = "EUR", ["£"] = "GBP", ["¥"] = "JPY", ["HK$"] = "HKD", ["A$"] = "AUD", ["₹"] = "INR", ["₩"] = "KRW" };
        private static readonly Regex SignFirst = new Regex(@"^(US\$|C\$|CA\$|HK\$|A\$|\$|€|£|¥|₹|₩)\s?(\d[\d,]*(?:\.\d{1,2})?)$", RegexOptions.Compiled);
        private static readonly Regex CodeAfter = new Regex(@"^(\d[\d,]*(?:\.\d{1,2})?)\s?([A-Za-z]{3})$", RegexOptions.Compiled);
        private static readonly Regex CodeBefore = new Regex(@"^([A-Za-z]{3})\s?(\d[\d,]*(?:\.\d{1,2})?)$", RegexOptions.Compiled);

        /// <summary>"$40", "40 USD", "EUR 12.50". The rates arrive from the lookup layer; offline the toast shows the amount.</summary>
        public static SmartFact Money(string t, string homeCurrency = "CAD")
        {
            var s = t.Trim(); string code = null, num = null;
            var m = SignFirst.Match(s);
            if (m.Success) { code = Signs[m.Groups[1].Value]; num = m.Groups[2].Value; }
            else if ((m = CodeAfter.Match(s)).Success) { code = m.Groups[2].Value.ToUpperInvariant(); num = m.Groups[1].Value; }
            else if ((m = CodeBefore.Match(s)).Success) { code = m.Groups[1].Value.ToUpperInvariant(); num = m.Groups[2].Value; }
            if (code == null || !Currencies.Contains(code)) return null;
            if (!double.TryParse(num.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)) return null;
            return new SmartFact("money", "You copied an amount of money", Units.Format(amount) + " " + code)
                .Set("lookup", code).Set("amount", amount.ToString("R", CultureInfo.InvariantCulture)).Set("home", homeCurrency)
                .Value("Amount", Units.Format(amount)).Value("With code", Units.Format(amount) + " " + code);
        }

        /// <summary>Adds converted values once rates are known: rates are "1 base = x other".</summary>
        public static void AddRates(SmartFact f, IDictionary<string, double> ratesFromBase)
        {
            if (!double.TryParse(f.Get("amount"), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)) return;
            var home = f.Get("home");
            foreach (var kv in ratesFromBase.OrderBy(k => k.Key == home ? 0 : 1).ThenBy(k => k.Key))
                f.Values.Add(new SmartValue(kv.Key, Units.Format(Math.Round(amount * kv.Value, 2)) + " " + kv.Key));
            if (home != null && ratesFromBase.TryGetValue(home, out var r)) f.Summary += " → " + Units.Format(Math.Round(amount * r, 2)) + " " + home;
        }

        // ------------------------------------------------------------------ cron

        public static SmartFact Cron(string t, DateTime? fromForTests = null)
        {
            var parts = t.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 || !parts.All(p => Regex.IsMatch(p, @"^[\d*,/\-]+$"))) return null;
            if (!parts.Any(p => p.Contains('*'))) return null; // five plain numbers are not a schedule
            var sets = new HashSet<int>[5];
            int[] lo = { 0, 0, 1, 1, 0 }, hi = { 59, 23, 31, 12, 7 };
            for (int i = 0; i < 5; i++) { sets[i] = Field(parts[i], lo[i], hi[i]); if (sets[i] == null) return null; }
            if (sets[4].Contains(7)) sets[4].Add(0);
            var next = new List<DateTime>();
            var at = (fromForTests ?? DateTime.Now); at = new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0).AddMinutes(1);
            bool domStar = parts[2] == "*", dowStar = parts[4] == "*";
            for (int guard = 0; guard < 600000 && next.Count < 3; guard++, at = at.AddMinutes(1))
            {
                if (!sets[3].Contains(at.Month)) { at = new DateTime(at.Year, at.Month, 1).AddMonths(1).AddMinutes(-1); continue; }
                bool dom = sets[2].Contains(at.Day), dow = sets[4].Contains((int)at.DayOfWeek);
                bool dayOk = domStar ? dow : dowStar ? dom : (dom || dow);
                if (!dayOk) { at = at.Date.AddDays(1).AddMinutes(-1); continue; }
                if (!sets[1].Contains(at.Hour)) { at = at.Date.AddHours(at.Hour + 1).AddMinutes(-1); continue; }
                if (sets[0].Contains(at.Minute)) next.Add(at);
            }
            var words = Describe(parts);
            var f = new SmartFact("cron", "You copied a schedule", words).Value("In words", words);
            for (int i = 0; i < next.Count; i++) f.Value(i == 0 ? "Next run" : "Then", next[i].ToString("ddd d MMM yyyy HH:mm", CultureInfo.InvariantCulture));
            return f;
        }

        private static HashSet<int> Field(string p, int lo, int hi)
        {
            var set = new HashSet<int>();
            foreach (var part in p.Split(','))
            {
                var stepSplit = part.Split('/'); int step = 1;
                if (stepSplit.Length > 2) return null;
                if (stepSplit.Length == 2 && (!int.TryParse(stepSplit[1], out step) || step < 1)) return null;
                int a, b;
                if (stepSplit[0] == "*") { a = lo; b = hi; }
                else if (stepSplit[0].Contains('-')) { var r = stepSplit[0].Split('-'); if (r.Length != 2 || !int.TryParse(r[0], out a) || !int.TryParse(r[1], out b)) return null; }
                else { if (!int.TryParse(stepSplit[0], out a)) return null; b = stepSplit.Length == 2 ? hi : a; }
                if (a < lo || b > hi || a > b) return null;
                for (int v = a; v <= b; v += step) set.Add(v);
            }
            return set;
        }

        private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        private static string Describe(string[] p)
        {
            string time;
            if (Regex.IsMatch(p[0], @"^\d+$") && Regex.IsMatch(p[1], @"^\d+$"))
            {
                var d = new DateTime(2000, 1, 1, int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[0], CultureInfo.InvariantCulture), 0);
                time = "at " + d.ToString(d.Minute == 0 ? "h tt" : "h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();
            }
            else if (p[0].StartsWith("*/") && p[1] == "*") time = "every " + p[0].Substring(2) + " minutes";
            else if (p[0] == "*" && p[1] == "*") time = "every minute";
            else if (Regex.IsMatch(p[0], @"^\d+$") && p[1] == "*") time = "at " + p[0] + " minutes past every hour";
            else if (Regex.IsMatch(p[0], @"^\d+$") && p[1].StartsWith("*/")) time = "every " + p[1].Substring(2) + " hours at minute " + p[0];
            else time = "at minute " + p[0] + ", hour " + p[1];
            string days;
            if (p[4] == "1-5") days = "on weekdays";
            else if (p[4] == "0,6" || p[4] == "6,0") days = "at weekends";
            else if (p[4] != "*" && Regex.IsMatch(p[4], @"^\d$")) days = "every " + DayNames[int.Parse(p[4], CultureInfo.InvariantCulture)];
            else if (p[4] != "*") days = "on days " + p[4] + " of the week";
            else if (p[2] != "*") days = "on day " + p[2] + " of the month";
            else days = "every day";
            var months = p[3] == "*" ? "" : " in month " + p[3];
            return char.ToUpperInvariant(time[0]) + time.Substring(1) + " " + days + months;
        }

        // ------------------------------------------------------------------ language

        private static readonly (string Lang, string Words)[] Stop =
        {
            ("French", " le la les des est et une un pour que dans pas bonjour merci vous avec "),
            ("Spanish", " el los las es y una un para que en por gracias hola usted con muy "),
            ("German", " der die das und ist nicht ein eine mit für danke hallo sie ich auf "),
            ("Italian", " il gli che è e una un per non grazie ciao sono con della "),
            ("Portuguese", " o os as é e uma um para que não obrigado olá você com "),
            ("Dutch", " de het een en is niet van dank hallo jij met voor "),
            ("English", " the and is are of to in that it you for with this was "),
        };

        /// <summary>Works out the language of a sentence from its script or its common small words. English gets nothing.</summary>
        public static SmartFact Language(string t)
        {
            var s = t.Trim();
            if (s.Length < 4 || s.Length > 4000 || Regex.IsMatch(s, @"^\S+$") && s.All(c => c < 128)) return null;
            string lang = Script(s);
            if (lang == null)
            {
                var words = " " + Regex.Replace(s.ToLowerInvariant(), @"[^\p{L}\s]", " ") + " ";
                var tokens = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) return null;
                var best = Stop.Select(x => (x.Lang, Hits: tokens.Count(w => x.Words.Contains(" " + w + " ")))).OrderByDescending(x => x.Hits).First();
                if (best.Hits < 2 && !(tokens.Length <= 3 && best.Hits == 1)) return null;
                lang = best.Lang;
            }
            if (lang == "English") return null;
            return new SmartFact("language", "You copied " + lang + " text", Short(s)).Set("language", lang)
                .Act("Translate with your local AI", SmartDo.Run, "translate", primary: true, tip: "Uses Ollama on this PC when it is set up")
                .Act("Translate on the web", SmartDo.Open, "https://translate.google.com/?sl=auto&tl=en&op=translate&text=" + Uri.EscapeDataString(s.Length > 2000 ? s.Substring(0, 2000) : s));
        }

        private static string Short(string s) => s.Length > 60 ? s.Substring(0, 57) + "…" : s;

        private static string Script(string s)
        {
            int han = 0, kana = 0, hangul = 0, cyr = 0, arab = 0, greek = 0, hebrew = 0, thai = 0, deva = 0, letters = 0;
            foreach (var c in s)
            {
                if (!char.IsLetter(c)) continue; letters++;
                if (c >= 0x4E00 && c <= 0x9FFF) han++;
                else if (c >= 0x3040 && c <= 0x30FF) kana++;
                else if (c >= 0xAC00 && c <= 0xD7AF) hangul++;
                else if (c >= 0x0400 && c <= 0x04FF) cyr++;
                else if (c >= 0x0600 && c <= 0x06FF) arab++;
                else if (c >= 0x0370 && c <= 0x03FF) greek++;
                else if (c >= 0x0590 && c <= 0x05FF) hebrew++;
                else if (c >= 0x0E00 && c <= 0x0E7F) thai++;
                else if (c >= 0x0900 && c <= 0x097F) deva++;
            }
            if (letters == 0) return null;
            double half = letters * 0.4;
            if (kana > 0 && kana + han >= half) return "Japanese";
            if (hangul >= half) return "Korean";
            if (han >= half) return Regex.IsMatch(s, "[嘅咗喺唔佢哋冇嚟]") ? "Cantonese" : "Chinese";
            if (cyr >= half) return "Russian or another Cyrillic language";
            if (arab >= half) return "Arabic";
            if (greek >= half) return "Greek";
            if (hebrew >= half) return "Hebrew";
            if (thai >= half) return "Thai";
            if (deva >= half) return "Hindi";
            return null;
        }
    }
}
