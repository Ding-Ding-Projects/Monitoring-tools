using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>Everyday things people copy: phone numbers, times, sums, addresses, parcels, flights, books,
    /// bank numbers, money, schedules and single characters.</summary>
    public static class Everyday
    {
        // ------------------------------------------------------------------ phone numbers

        private static readonly Regex PhoneRx = new Regex(@"^\+?[\d\s().\-]{7,20}$", RegexOptions.Compiled);

        public static SmartFact Phone(string t)
        {
            if (!PhoneRx.IsMatch(t) || !Regex.IsMatch(t, @"[\s().\-+]")) return null;
            var digits = new string(t.Where(char.IsDigit).ToArray());
            bool plus = t.TrimStart().StartsWith("+");
            if (digits.Length < 7 || digits.Length > 15) return null;
            if (Regex.IsMatch(t, @"^\d{4}-\d{2}-\d{2}$")) return null; // a date
            string pretty, e164, where;
            if (!plus && digits.Length == 10 || plus && digits.Length == 11 && digits[0] == '1' || !plus && digits.Length == 11 && digits[0] == '1')
            {
                var d = digits.Length == 11 ? digits.Substring(1) : digits;
                pretty = $"+1 {d.Substring(0, 3)} {d.Substring(3, 3)} {d.Substring(6)}"; e164 = "+1" + d; where = "North America";
            }
            else if (plus)
            {
                e164 = "+" + digits; pretty = "+" + Group(digits); where = "International";
            }
            else return null;
            return new SmartFact("phone", "You copied a phone number", pretty + " · " + where)
                .Value("Tidy", pretty).Value("International", e164).Value("Digits only", digits)
                .Act("Call", SmartDo.Open, "tel:" + e164, primary: true, tip: "Opens your calling app, such as Phone Link or Teams")
                .Act("Text", SmartDo.Open, "sms:" + e164);
        }

        private static string Group(string digits)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < digits.Length; i++) { if (i > 0 && (digits.Length - i) % 3 == 0 && i != 1) sb.Append(' '); sb.Append(digits[i]); }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ unix timestamps

        public static SmartFact Timestamp(string t, DateTimeOffset? nowForTests = null)
        {
            if (!Regex.IsMatch(t, @"^\d{10}(\d{3})?$")) return null;
            long v = long.Parse(t, CultureInfo.InvariantCulture);
            var at = t.Length == 13 ? DateTimeOffset.FromUnixTimeMilliseconds(v) : DateTimeOffset.FromUnixTimeSeconds(v);
            if (at.Year < 2001 || at.Year > 2100) return null;
            var local = at.ToLocalTime();
            var now = nowForTests ?? DateTimeOffset.Now;
            return new SmartFact("timestamp", "You copied a Unix time", local.ToString("ddd d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " · " + Ago(now - at))
                .Value("Local", local.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture))
                .Value("UTC", at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC")
                .Value("ISO 8601", at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                .Value(t.Length == 13 ? "Seconds" : "Milliseconds", t.Length == 13 ? (v / 1000).ToString(CultureInfo.InvariantCulture) : (v * 1000).ToString(CultureInfo.InvariantCulture));
        }

        public static string Ago(TimeSpan d)
        {
            bool future = d < TimeSpan.Zero; var a = d.Duration();
            string s = a.TotalMinutes < 1 ? "seconds" : a.TotalHours < 1 ? $"{(int)a.TotalMinutes} min" : a.TotalDays < 1 ? $"{(int)a.TotalHours} h" : a.TotalDays < 60 ? $"{(int)a.TotalDays} days" : $"{(int)(a.TotalDays / 30.4)} months";
            return future ? "in " + s : s + " ago";
        }

        // ------------------------------------------------------------------ time zones

        private static readonly Dictionary<string, double> ZoneOffsets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["UTC"] = 0, ["GMT"] = 0, ["Z"] = 0, ["BST"] = 1, ["CET"] = 1, ["CEST"] = 2, ["EET"] = 2, ["EEST"] = 3, ["IST"] = 5.5,
            ["HKT"] = 8, ["SGT"] = 8, ["CST8"] = 8, ["JST"] = 9, ["KST"] = 9, ["AEST"] = 10, ["AEDT"] = 11, ["NZST"] = 12, ["NZDT"] = 13,
            ["EST"] = -5, ["EDT"] = -4, ["ET"] = -5, ["CST"] = -6, ["CDT"] = -5, ["MST"] = -7, ["MDT"] = -6, ["PST"] = -8, ["PDT"] = -7, ["PT"] = -8,
            ["AKST"] = -9, ["HST"] = -10, ["AST"] = -4, ["NST"] = -3.5,
        };
        private static readonly Regex ZoneTimeRx = new Regex(@"^(\d{1,2})(?::(\d{2}))?\s*(am|pm|a\.m\.|p\.m\.)?\s+([A-Za-z]{1,5})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>"3 pm PST", "15:30 UTC", "9:00am EST". Shown in this PC's zone and a few common ones.</summary>
        public static SmartFact ZoneTime(string t, DateTime? todayForTests = null, TimeZoneInfo localForTests = null)
        {
            var m = ZoneTimeRx.Match(t);
            if (!m.Success || !ZoneOffsets.TryGetValue(m.Groups[4].Value, out var off)) return null;
            int h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            var ap = m.Groups[3].Value.ToLowerInvariant().Replace(".", "");
            if (ap.Length == 0 && !m.Groups[2].Success) return null; // "3 PST" is too vague
            if (ap.Length > 0) { if (h < 1 || h > 12) return null; if (ap == "pm" && h != 12) h += 12; if (ap == "am" && h == 12) h = 0; }
            if (h > 23 || min > 59) return null;
            var day = (todayForTests ?? DateTime.Today).Date;
            var at = new DateTimeOffset(day.Year, day.Month, day.Day, h, min, 0, TimeSpan.FromHours(off));
            var local = localForTests ?? TimeZoneInfo.Local;
            var mine = TimeZoneInfo.ConvertTime(at, local);
            var fact = new SmartFact("zonetime", "You copied a time", t.Trim() + " → " + mine.ToString("h:mm tt", CultureInfo.InvariantCulture) + " here");
            fact.Value("Here", mine.ToString("ddd h:mm tt", CultureInfo.InvariantCulture));
            foreach (var (label, id) in new[] { ("UTC", "UTC"), ("Toronto", "America/Toronto"), ("Vancouver", "America/Vancouver"), ("London", "Europe/London"), ("Hong Kong", "Asia/Hong_Kong"), ("Tokyo", "Asia/Tokyo") })
            {
                try { var z = TimeZoneInfo.FindSystemTimeZoneById(id); fact.Value(label, TimeZoneInfo.ConvertTime(at, z).ToString("ddd h:mm tt", CultureInfo.InvariantCulture)); }
                catch { }
            }
            return fact;
        }

        // ------------------------------------------------------------------ maths

        public static SmartFact Maths(string t)
        {
            if (t.Length > 120 || t.Contains('\n') || !Regex.IsMatch(t, @"^[\d\s.+\-*/×÷x^%()=,]+$")) return null;
            if (!Regex.IsMatch(t, @"\d\s*[+\-*/×÷x^%]\s*[\d(]")) return null;
            if (Regex.IsMatch(t, @"^\d{1,4}[-/]\d{1,2}[-/]\d{1,4}$")) return null; // a date like 2026-11-06 or 6/11/2026
            if (Regex.IsMatch(t.Trim(), @"^[\d\s\-()+.]+$") && !Regex.IsMatch(t, @"[*/×÷x^%]") && Regex.IsMatch(t, @"\d{3}[\s\-.]\d{3}")) return null; // a phone number
            var expr = t.Replace("×", "*").Replace("÷", "/").Replace("x", "*").Replace(",", "").TrimEnd('=', ' ');
            double v;
            try { v = new Calc(expr).Run(); } catch { return null; }
            if (double.IsNaN(v) || double.IsInfinity(v)) return null;
            var r = Units.Format(v);
            return new SmartFact("maths", "You copied a sum", expr.Trim() + " = " + r)
                .Value("Result", r).Value("With the sum", expr.Trim() + " = " + r)
                .Act("Copy " + r, SmartDo.Copy, r, primary: true);
        }

        /// <summary>A small calculator: + - * / ^ % and brackets. Percent after a number divides it by 100.</summary>
        private sealed class Calc
        {
            private readonly string s; private int i;
            public Calc(string text) { s = text.Replace(" ", ""); }
            public double Run() { var v = Sum(); if (i != s.Length) throw new FormatException(); return v; }
            private double Sum() { var v = Product(); while (i < s.Length && (s[i] == '+' || s[i] == '-')) { var op = s[i++]; var r = Product(); v = op == '+' ? v + r : v - r; } return v; }
            private double Product() { var v = Power(); while (i < s.Length && (s[i] == '*' || s[i] == '/')) { var op = s[i++]; var r = Power(); v = op == '*' ? v * r : v / r; } return v; }
            private double Power() { var v = Unary(); if (i < s.Length && s[i] == '^') { i++; v = Math.Pow(v, Power()); } return v; }
            private double Unary() { if (i < s.Length && s[i] == '-') { i++; return -Unary(); } if (i < s.Length && s[i] == '+') { i++; return Unary(); } return Atom(); }
            private double Atom()
            {
                double v;
                if (i < s.Length && s[i] == '(') { i++; v = Sum(); if (i >= s.Length || s[i] != ')') throw new FormatException(); i++; }
                else
                {
                    int start = i; while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    if (start == i) throw new FormatException();
                    v = double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
                }
                if (i < s.Length && s[i] == '%') { i++; v /= 100; }
                return v;
            }
        }

        // ------------------------------------------------------------------ addresses

        private static readonly Regex AddressRx = new Regex(@"^\d{1,6}[A-Za-z]?\s+(?:[A-Za-zÀ-ÿ.'\-]+\s+){0,4}(Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Lane|Ln|Drive|Dr|Way|Court|Ct|Crescent|Cres|Place|Pl|Terrace|Highway|Hwy|Parkway|Pkwy|Square|Sq)\b\.?(?:[,\s].{0,80})?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static SmartFact Address(string t)
        {
            var one = Regex.Replace(t.Trim(), @"\s*\r?\n\s*", ", ");
            if (one.Length > 160 || !AddressRx.IsMatch(one)) return null;
            return new SmartFact("address", "You copied an address", one)
                .Value("One line", one).Value("Label", one.Replace(", ", Environment.NewLine))
                .Act("Open in Maps", SmartDo.Open, "https://www.bing.com/maps?q=" + Uri.EscapeDataString(one), primary: true)
                .Act("Directions", SmartDo.Open, "https://www.google.com/maps/dir/?api=1&destination=" + Uri.EscapeDataString(one));
        }

        // ------------------------------------------------------------------ flights

        private static readonly HashSet<string> Airlines = new HashSet<string>(
            "AC WS PD TS AA DL UA WN AS B6 NK F9 BA VS EI AF KL LH LX OS SN IB AZ TP SK AY CX KA HX UO JL NH KE OZ SQ TG MH QF NZ EK QR EY TK SA ET AI CA MU CZ BR CI VN PR".Split(' '));
        private static readonly Regex FlightRx = new Regex(@"^([A-Z0-9]{2})\s?(\d{1,4})$", RegexOptions.Compiled);

        public static SmartFact Flight(string t)
        {
            var m = FlightRx.Match(t.Trim().ToUpperInvariant());
            if (!m.Success || !Airlines.Contains(m.Groups[1].Value) || !Regex.IsMatch(t, "[A-Za-z]")) return null;
            var code = m.Groups[1].Value + " " + m.Groups[2].Value;
            return new SmartFact("flight", "You copied a flight number", code)
                .Value("Flight", code)
                .Act("Flight status", SmartDo.Open, "https://www.bing.com/search?q=" + Uri.EscapeDataString(code.Replace(" ", "") + " flight status"), primary: true);
        }

        // ------------------------------------------------------------------ books

        public static SmartFact Isbn(string t)
        {
            var s = Regex.Replace(t.Trim(), @"^ISBN(?:-1[03])?:?\s*", "", RegexOptions.IgnoreCase);
            if (!Regex.IsMatch(s, @"^[\d\- ]{10,17}[\dXx]$")) return null;
            var d = Regex.Replace(s, @"[\- ]", "").ToUpperInvariant();
            bool ok = d.Length == 13 && (d.StartsWith("978") || d.StartsWith("979")) && Isbn13(d) || d.Length == 10 && Isbn10(d);
            if (!ok) return null;
            var f = new SmartFact("isbn", "You copied a book number", "ISBN " + d).Set("lookup", d)
                .Value("ISBN", d)
                .Act("Open Library", SmartDo.Open, "https://openlibrary.org/isbn/" + d, primary: true);
            if (d.Length == 10) f.Value("ISBN-13", To13(d));
            return f;
        }

        private static bool Isbn13(string d) { int sum = 0; for (int i = 0; i < 13; i++) sum += (d[i] - '0') * (i % 2 == 0 ? 1 : 3); return sum % 10 == 0; }
        private static bool Isbn10(string d)
        {
            int sum = 0;
            for (int i = 0; i < 10; i++) { int v = d[i] == 'X' ? (i == 9 ? 10 : -100) : d[i] - '0'; if (v < 0) return false; sum += v * (10 - i); }
            return sum % 11 == 0;
        }
        private static string To13(string d10)
        {
            var b = "978" + d10.Substring(0, 9); int sum = 0;
            for (int i = 0; i < 12; i++) sum += (b[i] - '0') * (i % 2 == 0 ? 1 : 3);
            return b + ((10 - sum % 10) % 10).ToString(CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ bank IBANs (checked, never stored)

        public static SmartFact Iban(string t)
        {
            var s = Regex.Replace(t.Trim(), @"\s+", "").ToUpperInvariant();
            if (!Regex.IsMatch(s, @"^[A-Z]{2}\d{2}[A-Z0-9]{10,30}$")) return null;
            var moved = s.Substring(4) + s.Substring(0, 4);
            var digits = new StringBuilder();
            foreach (var c in moved) digits.Append(char.IsLetter(c) ? (c - 'A' + 10).ToString(CultureInfo.InvariantCulture) : c.ToString());
            int rem = 0; foreach (var c in digits.ToString()) rem = (rem * 10 + (c - '0')) % 97;
            bool valid = rem == 1;
            var grouped = Regex.Replace(s, ".{4}(?!$)", "$0 ");
            return new SmartFact("iban", "You copied a bank account number (IBAN)", (valid ? "check digits are right" : "check digits are wrong") + " · " + s.Substring(0, 2)) { Sensitive = true }
                .Value("Grouped", grouped).Value("No spaces", s).Set("valid", valid ? "yes" : "no");
        }

        // ------------------------------------------------------------------ parcels

        public static SmartFact Parcel(string t)
        {
            var s = Regex.Replace(t.Trim(), @"\s+", "").ToUpperInvariant();
            string carrier = null, url = null;
            if (Regex.IsMatch(s, @"^1Z[0-9A-Z]{16}$") && UpsCheck(s)) { carrier = "UPS"; url = "https://www.ups.com/track?tracknum=" + s; }
            else if (Regex.IsMatch(s, @"^[A-Z]{2}\d{9}[A-Z]{2}$") && S10Check(s))
            {
                var c = s.Substring(11);
                carrier = c == "CA" ? "Canada Post" : c == "US" ? "USPS" : c == "GB" ? "Royal Mail" : c == "HK" ? "Hongkong Post" : "Post (" + c + ")";
                url = c == "CA" ? "https://www.canadapost-postescanada.ca/track-reperage/en#/search?searchFor=" + s
                    : c == "US" ? "https://tools.usps.com/go/TrackConfirmAction?tLabels=" + s
                    : "https://parcelsapp.com/en/tracking/" + s;
            }
            else if (Regex.IsMatch(s, @"^9[2-5]\d{20}$") && Mod10(s)) { carrier = "USPS"; url = "https://tools.usps.com/go/TrackConfirmAction?tLabels=" + s; }
            if (carrier == null) return null;
            return new SmartFact("parcel", "You copied a tracking number", carrier + " · " + s)
                .Value("Tracking number", s)
                .Act("Track on " + carrier, SmartDo.Open, url, primary: true, tip: "Opens the carrier's own page; live status needs the carrier's sign-in.");
        }

        private static bool UpsCheck(string s)
        {
            int sum = 0; var body = s.Substring(2, 15);
            for (int i = 0; i < 15; i++)
            {
                char c = body[i]; int v = char.IsDigit(c) ? c - '0' : (c - 'A' + 2) % 10;
                sum += i % 2 == 1 ? v * 2 : v;
            }
            return (10 - sum % 10) % 10 == s[17] - '0';
        }

        private static bool S10Check(string s)
        {
            int[] w = { 8, 6, 4, 2, 3, 5, 9, 7 }; int sum = 0;
            for (int i = 0; i < 8; i++) sum += (s[2 + i] - '0') * w[i];
            int c = 11 - sum % 11; if (c == 10) c = 0; if (c == 11) c = 5;
            return c == s[10] - '0';
        }

        private static bool Mod10(string s)
        {
            int sum = 0; var body = s.Substring(0, s.Length - 1);
            for (int i = 0; i < body.Length; i++) { int v = body[body.Length - 1 - i] - '0'; sum += i % 2 == 0 ? v * 3 : v; }
            return (10 - sum % 10) % 10 == s[s.Length - 1] - '0';
        }

        // ------------------------------------------------------------------ single characters and emoji

        public static SmartFact Character(string t)
        {
            var s = t.Trim();
            if (s.Length == 0 || s.Length > 16) return null;
            var e = StringInfo.GetTextElementEnumerator(s); int n = 0; while (e.MoveNext()) n++;
            if (n != 1 || s.All(c => c < 128)) return null;
            var points = new List<int>();
            for (int i = 0; i < s.Length; i++) { int cp = char.ConvertToUtf32(s, i); points.Add(cp); if (char.IsHighSurrogate(s[i])) i++; }
            var code = string.Join(" ", points.Select(p => "U+" + p.ToString("X4", CultureInfo.InvariantCulture)));
            var category = CharUnicodeInfo.GetUnicodeCategory(points[0]);
            return new SmartFact("char", "You copied a character", s + " · " + code + " · " + Friendly(category))
                .Value("Code point", code)
                .Value("HTML", string.Concat(points.Select(p => "&#x" + p.ToString("X", CultureInfo.InvariantCulture) + ";")))
                .Value("C# or JS", string.Concat(points.Select(p => p > 0xFFFF ? "\\u{" + p.ToString("X", CultureInfo.InvariantCulture) + "}" : "\\u" + p.ToString("X4", CultureInfo.InvariantCulture))))
                .Value("UTF-8 bytes", string.Join(" ", Encoding.UTF8.GetBytes(s).Select(b => b.ToString("X2", CultureInfo.InvariantCulture))));
        }

        private static string Friendly(UnicodeCategory c) => c switch
        {
            UnicodeCategory.OtherSymbol => "symbol or emoji",
            UnicodeCategory.MathSymbol => "maths symbol",
            UnicodeCategory.CurrencySymbol => "currency sign",
            UnicodeCategory.OtherLetter => "letter",
            UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter => "letter",
            UnicodeCategory.DashPunctuation or UnicodeCategory.OtherPunctuation or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation => "punctuation",
            UnicodeCategory.SpaceSeparator => "space",
            _ => "character",
        };
    }
}
