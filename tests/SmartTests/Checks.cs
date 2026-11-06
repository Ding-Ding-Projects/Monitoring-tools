using System;
using System.Collections.Generic;
using System.Linq;
using MonitoringTools.Clip.Smart;

namespace SmartTests
{
    public static partial class Program
    {
        private static void Kinds()
        {
            Kind("5 kg", "unit"); Kind("5kg", "unit"); Kind("72°F", "unit"); Kind("3 cups", "unit"); Kind("6'2\"", "unit");
            Kind("100 km/h", "unit"); Kind("1.5 GB", "unit"); Kind("30 mpg", "unit"); Kind("0xFF", "unit"); Kind("2 h 15 min", "unit");
            Kind("$40", "money"); Kind("40 USD", "money"); Kind("EUR 12.50", "money");
            Kind("3 pm PST", "zonetime"); Kind("15:30 UTC", "zonetime");
            Kind("1793998800", "timestamp"); Kind("1793998800000", "timestamp");
            Kind("(12.5*4)+18", "maths"); Kind("12 × 4", "maths");
            Kind("+1 416 555 0134", "phone"); Kind("(416) 555-0134", "phone"); Kind("416.555.0134", "phone");
            Kind("42 Example Street, Toronto ON", "address");
            Kind("AC 856", "flight"); Kind("ac856", "flight");
            Kind("978-0-13-468599-1", "isbn");
            Kind("GB82 WEST 1234 5698 7654 32", "iban");
            Kind("1Z999AA10123456784", "parcel");
            Kind("0 9 * * 1-5", "cron");
            Kind("→", "char"); Kind("😀", "char");
            Kind("Bonjour, merci pour le message", "language"); Kind("你好，今日天氣好好", "language");
            Kind("ghp_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789", "secret");
            Kind("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c", "jwt");
            Kind("System.NullReferenceException: Object reference not set\n   at App.Toast.Show() in C:\\src\\Toast.cs:line 42", "trace");
            Kind("Traceback (most recent call last):\n  File \"app.py\", line 7, in <module>\nValueError: bad", "trace");
            Kind("Item\tQty\tPrice\nCable\t2\t12.50\nAdapter\t1\t18.00", "table");
            Kind("SELECT a, b FROM t WHERE a = 1 AND b = 2", "sql");
            Kind("<root><a>1</a></root>", "xml");
            Kind("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "hash");
            Kind("198.51.100.7", "ip"); Kind("2001:db8::1", "ip");
            Kind("git@github.com:Ding-Ding-Projects/Monitoring-tools.git", "git");
            Kind("0x80070005", "hresult"); Kind("-2147024891", "hresult");
            Kind("3f2504e0-4f89-11d3-9a0c-0305e82c3301", "guid");
            Kind("npm install react@18.3.1", "package"); Kind("dotnet add package Serilog", "package");
            Kind("^\\d{3}-\\d{4}$", "regex");
            Kind("D:\\Footage\\missing-folder\\clip.mov", "path");

            // things that must stay plain text
            foreach (var plain in new[] { "hello", "Thanks for the review, merging now.", "2026-11-06", "Room 4B", "version 2", "12", "the bus was late again", "OK", "1234" })
                Kind(plain, null);
        }

        private static void Conversions()
        {
            var f = Units.Detect("5 kg");
            Check("5 kg in pounds", V(f, "pounds") == "11.02 lb", V(f, "pounds"));
            Check("5 kg in grams", V(f, "grams") == "5000 g", V(f, "grams"));
            Check("5 kg skips itself", V(f, "kilograms") == null);
            Check("72F in C", V(Units.Detect("72°F"), "Celsius") == "22.22°C");
            Check("0 C in K", V(Units.Detect("0 c"), "Kelvin") == "273.15 K");
            Check("6'2\" in cm", V(Units.Detect("6'2\""), "centimetres") == "187.96 cm");
            Check("30 mpg in L/100km", V(Units.Detect("30 mpg"), "litres per 100 km") == "7.84 L/100km");
            Check("0xFF decimal", V(Units.Detect("0xFF"), "decimal") == "255");
            Check("0xFF binary", V(Units.Detect("0xFF"), "binary") == "0b11111111");
            Check("2 h 15 min in hours", V(Units.Detect("2 h 15 min"), "hours") == "2.25 h");
            Check("1 GiB in MB", V(Units.Detect("1 GiB"), "megabytes") == "1073.74 MB");
            Check("100 km/h in mph", V(Units.Detect("100 km/h"), "miles per hour") == "62.14 mph");
            Check("1 acre in m2", V(Units.Detect("1 acre"), "square metres") == "4046.86 m²");
            Check("tiny numbers", Units.Format(0.00001) == "1e-5", Units.Format(0.00001));
            Check("every family converts", Units.Families.All(fam => Units.ConvertAll(new Quantity { Number = 1, Family = fam, Unit = fam.Units[0] }).Count == fam.Units.Length));
            Check("primary is all conversions", f.Actions[0].Value == "conversions:5 kg");
        }

        private static void Everyday()
        {
            var m = MonitoringTools.Clip.Smart.Everyday2.Money("$40");
            MonitoringTools.Clip.Smart.Everyday2.AddRates(m, new Dictionary<string, double> { ["CAD"] = 1.39, ["EUR"] = 0.93 });
            Check("money converts", V(m, "CAD") == "55.6 CAD", V(m, "CAD"));
            Check("money summary", m.Summary.EndsWith("→ 55.6 CAD"), m.Summary);

            var c = MonitoringTools.Clip.Smart.Everyday2.Cron("0 9 * * 1-5", new DateTime(2026, 11, 6, 16, 0, 0));
            Check("cron words", c.Summary == "At 9 am on weekdays", c.Summary);
            Check("cron next run", V(c, "Next run") == "Mon 9 Nov 2026 09:00", V(c, "Next run"));
            var c2 = MonitoringTools.Clip.Smart.Everyday2.Cron("*/15 * * * *", new DateTime(2026, 11, 6, 16, 2, 0));
            Check("cron every 15", V(c2, "Next run") == "Fri 6 Nov 2026 16:15", V(c2, "Next run"));

            var z = MonitoringTools.Clip.Smart.Everyday.ZoneTime("3 pm PST", new DateTime(2026, 11, 6), TimeZoneInfo.FindSystemTimeZoneById("America/Toronto"));
            Check("3 pm PST in Toronto", V(z, "Here") == "Fri 6:00 PM", V(z, "Here"));
            Check("maths", V(MonitoringTools.Clip.Smart.Everyday.Maths("(12.5*4)+18"), "Result") == "68");
            Check("maths percent", V(MonitoringTools.Clip.Smart.Everyday.Maths("200 * 15%"), "Result") == "30");
            Check("phone tidy", V(MonitoringTools.Clip.Smart.Everyday.Phone("416.555.0134"), "International") == "+14165550134");
            Check("isbn10 to 13", V(MonitoringTools.Clip.Smart.Everyday.Isbn("0-306-40615-2"), "ISBN-13") == "9780306406157");
            Check("iban valid", MonitoringTools.Clip.Smart.Everyday.Iban("GB82 WEST 1234 5698 7654 32").Get("valid") == "yes");
            Check("iban wrong", MonitoringTools.Clip.Smart.Everyday.Iban("GB83 WEST 1234 5698 7654 32").Get("valid") == "no");
            Check("iban sensitive", MonitoringTools.Clip.Smart.Everyday.Iban("GB82 WEST 1234 5698 7654 32").Sensitive);
            Check("timestamp utc", V(MonitoringTools.Clip.Smart.Everyday.Timestamp("1793998800"), "UTC") == "2026-11-06 21:00:00 UTC", V(MonitoringTools.Clip.Smart.Everyday.Timestamp("1793998800"), "UTC"));
            Check("char code", V(MonitoringTools.Clip.Smart.Everyday.Character("😀"), "Code point") == "U+1F600");
            Check("cantonese", MonitoringTools.Clip.Smart.Everyday2.Language("我哋今日去咗飲茶")?.Get("language") == "Cantonese");
            Check("english is quiet", MonitoringTools.Clip.Smart.Everyday2.Language("This is the plan for the week") == null);
        }

        private static void DeveloperChecks()
        {
            var tr = Developer.StackTrace("System.NullReferenceException: Object reference not set\n   at App.Toast.Show() in C:\\src\\Toast.cs:line 42\n   at App.Main() in C:\\src\\App.cs:line 7");
            Check("trace opens line", tr.Actions[0].Value == "C:\\src\\Toast.cs:42", tr.Actions[0].Value);
            Check("trace summary", tr.Summary == "System.NullReferenceException · 2 frames", tr.Summary);
            var jwt = Developer.Jwt("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwiZXhwIjoxNTE2MjM5MDIyfQ.abc_def-ghi");
            Check("jwt subject", V(jwt, "Subject") == "1234567890");
            Check("jwt expired", jwt.Summary.StartsWith("expired"), jwt.Summary);
            Check("ip private ranges", Developer.IsPrivate(new System.Net.IPAddress(new byte[] { 10, 0, 0, 1 })) && Developer.IsPrivate(new System.Net.IPAddress(new byte[] { 172, 20, 1, 1 })) && !Developer.IsPrivate(new System.Net.IPAddress(new byte[] { 198, 51, 100, 7 })));
            Check("ip public has lookup", Developer.Ip("198.51.100.7").Get("lookup") == "198.51.100.7");
            Check("hresult facility", V(Developer.ErrorCode("0x80070005"), "Facility") == "7 · Win32");
            Check("guid braces", V(Developer.GuidText("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), "Braces") == "{3f2504e0-4f89-11d3-9a0c-0305e82c3301}");
            Check("sql format", Developer2.FormatSql("select a from t where x=1 and y=2") == "SELECT a\nFROM t\nWHERE x=1\n  AND y=2", Developer2.FormatSql("select a from t where x=1 and y=2").Replace("\n", "\\n"));
            Check("in list", Developer2.InList("a\nb\nO'Hare") == "IN ('a', 'b', 'O''Hare')");
            Check("in list numbers", Developer2.InList("1\n2\n3") == "IN (1, 2, 3)");
            Check("yaml", Developer2.JsonToYaml("{\"name\":\"tools\",\"tags\":[\"a\",\"b\"],\"n\":2,\"o\":{\"x\":true}}") == "name: tools\ntags:\n  - a\n  - b\nn: 2\no:\n  x: true", Developer2.JsonToYaml("{\"name\":\"tools\",\"tags\":[\"a\",\"b\"],\"n\":2,\"o\":{\"x\":true}}").Replace("\n", "\\n"));
            var tb = Developer2.Table("Item\tQty\tPrice\nCable\t2\t12.50\nAdapter\t1\t18.00");
            Check("table sum", V(tb, "Price sum") == "30.5", V(tb, "Price sum"));
            Check("table markdown", V(tb, "Markdown").StartsWith("| Item | Qty | Price |"));
            Check("table json dup headers", Developer2.ToJson(new List<string[]> { new[] { "a", "a" }, new[] { "1", "2" } }, true).Contains("a 2"));
            Check("wsl path", V(Developer2.WindowsPath("D:\\Footage\\x.mov"), "WSL") == "/mnt/d/Footage/x.mov");
            Check("package lookup", Developer2.Package("npm i left-pad").Get("lookup") == "npm:left-pad");
            Check("secret never reaches extensions", SmartDetect.All("ghp_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789").Count == 1);
        }
    }
}
