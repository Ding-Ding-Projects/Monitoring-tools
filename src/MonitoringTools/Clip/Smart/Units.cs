using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MonitoringTools.Clip.Smart
{
    /// <summary>One unit: its name, the symbol written after a number, how many base units it is, and the
    /// spellings people copy (lower case, compared without spaces).</summary>
    public sealed class Unit
    {
        public string Name, Symbol;
        public double Factor;
        public string[] Spellings;
        public Unit(string name, string symbol, double factor, string spellings)
        { Name = name; Symbol = symbol; Factor = factor; Spellings = spellings.Split(' ', StringSplitOptions.RemoveEmptyEntries); }
    }

    public sealed class UnitFamily
    {
        public string Id, Name;
        public Unit[] Units;
        /// <summary>Temperature, fuel use and number bases do not scale by a factor.</summary>
        public bool Special;
        public UnitFamily(string id, string name, bool special, params Unit[] units) { Id = id; Name = name; Special = special; Units = units; }
    }

    /// <summary>A parsed amount: the number, its family and the unit it was written in.</summary>
    public sealed class Quantity
    {
        public double Number;
        public UnitFamily Family;
        public Unit Unit;
        public string Source;
    }

    public sealed class Converted
    {
        public Unit Unit;
        public double Number;
        public string Text;
        public bool IsSource;
    }

    /// <summary>Quick conversions: every common unit of a family at once, worked out on this PC.
    /// Money is listed for parsing only; its rates come from the lookup layer.</summary>
    public static class Units
    {
        public static readonly UnitFamily[] Families =
        {
            new UnitFamily("mass", "Weight", false,
                new Unit("kilograms", "kg", 1, "kg kgs kilo kilos kilogram kilograms"),
                new Unit("grams", "g", .001, "g gr gram grams gramme grammes"),
                new Unit("milligrams", "mg", 1e-6, "mg milligram milligrams"),
                new Unit("pounds", "lb", .45359237, "lb lbs pound pounds"),
                new Unit("ounces", "oz", .028349523125, "oz ounce ounces"),
                new Unit("stone", "st", 6.35029318, "st stone stones"),
                new Unit("metric tonnes", "t", 1000, "t tonne tonnes"),
                new Unit("carats", "ct", .0002, "ct carat carats")),
            new UnitFamily("length", "Length", false,
                new Unit("metres", "m", 1, "m metre metres meter meters"),
                new Unit("centimetres", "cm", .01, "cm centimetre centimetres centimeter centimeters"),
                new Unit("millimetres", "mm", .001, "mm millimetre millimetres millimeter millimeters"),
                new Unit("kilometres", "km", 1000, "km kilometre kilometres kilometer kilometers"),
                new Unit("inches", "in", .0254, "in inch inches \""),
                new Unit("feet", "ft", .3048, "ft foot feet '"),
                new Unit("yards", "yd", .9144, "yd yds yard yards"),
                new Unit("miles", "mi", 1609.344, "mi mile miles"),
                new Unit("nautical miles", "nmi", 1852, "nmi")),
            new UnitFamily("volume", "Volume", false,
                new Unit("millilitres", "ml", .001, "ml millilitre millilitres milliliter milliliters"),
                new Unit("litres", "L", 1, "l litre litres liter liters"),
                new Unit("US cups", "cups", .2365882365, "cup cups"),
                new Unit("tablespoons", "tbsp", .01478676478125, "tbsp tablespoon tablespoons"),
                new Unit("teaspoons", "tsp", .00492892159375, "tsp teaspoon teaspoons"),
                new Unit("US fluid ounces", "fl oz", .0295735295625, "floz"),
                new Unit("US gallons", "gal", 3.785411784, "gal gallon gallons"),
                new Unit("UK pints", "pt", .56826125, "pt pint pints"),
                new Unit("cubic metres", "m³", 1000, "m3 m³")),
            new UnitFamily("temp", "Temperature", true,
                new Unit("Celsius", "°C", 0, "°c c celsius"),
                new Unit("Fahrenheit", "°F", 0, "°f f fahrenheit"),
                new Unit("Kelvin", "K", 0, "k kelvin")),
            new UnitFamily("area", "Area", false,
                new Unit("square metres", "m²", 1, "m2 m² sqm"),
                new Unit("square feet", "ft²", .09290304, "ft2 ft² sqft"),
                new Unit("acres", "ac", 4046.8564224, "ac acre acres"),
                new Unit("hectares", "ha", 10000, "ha hectare hectares"),
                new Unit("square kilometres", "km²", 1e6, "km2 km²"),
                new Unit("square miles", "mi²", 2589988.110336, "mi2 mi²")),
            new UnitFamily("speed", "Speed", false,
                new Unit("kilometres per hour", "km/h", 1 / 3.6, "km/h kmh kph"),
                new Unit("miles per hour", "mph", .44704, "mph"),
                new Unit("metres per second", "m/s", 1, "m/s"),
                new Unit("knots", "kn", .514444, "kn knot knots"),
                new Unit("feet per second", "ft/s", .3048, "ft/s")),
            new UnitFamily("data", "Data size", false,
                new Unit("bytes", "B", 1, "b byte bytes"),
                new Unit("kilobytes", "KB", 1e3, "kb"),
                new Unit("megabytes", "MB", 1e6, "mb"),
                new Unit("gigabytes", "GB", 1e9, "gb"),
                new Unit("terabytes", "TB", 1e12, "tb"),
                new Unit("kibibytes", "KiB", 1024, "kib"),
                new Unit("mebibytes", "MiB", 1048576, "mib"),
                new Unit("gibibytes", "GiB", 1073741824, "gib"),
                new Unit("bits", "bit", .125, "bit bits")),
            new UnitFamily("time", "Time", false,
                new Unit("seconds", "s", 1, "s sec secs second seconds"),
                new Unit("minutes", "min", 60, "min mins minute minutes"),
                new Unit("hours", "h", 3600, "h hr hrs hour hours"),
                new Unit("days", "d", 86400, "d day days"),
                new Unit("weeks", "wk", 604800, "wk week weeks"),
                new Unit("milliseconds", "ms", .001, "ms millisecond milliseconds")),
            new UnitFamily("energy", "Energy", false,
                new Unit("kilocalories", "kcal", 4184, "kcal cal calories"),
                new Unit("kilojoules", "kJ", 1000, "kj"),
                new Unit("joules", "J", 1, "j joule joules"),
                new Unit("kilowatt hours", "kWh", 3.6e6, "kwh"),
                new Unit("BTU", "BTU", 1055.06, "btu")),
            new UnitFamily("pressure", "Pressure", false,
                new Unit("kilopascals", "kPa", 1000, "kpa"),
                new Unit("PSI", "psi", 6894.757, "psi"),
                new Unit("bar", "bar", 1e5, "bar"),
                new Unit("atmospheres", "atm", 101325, "atm"),
                new Unit("millimetres of mercury", "mmHg", 133.322, "mmhg")),
            new UnitFamily("fuel", "Fuel use", true,
                new Unit("litres per 100 km", "L/100km", 0, "l/100km"),
                new Unit("US mpg", "mpg", 0, "mpg"),
                new Unit("UK mpg", "mpg (UK)", 0, "mpguk")),
            new UnitFamily("angle", "Angle", false,
                new Unit("degrees", "°", Math.PI / 180, "deg degree degrees °"),
                new Unit("radians", "rad", 1, "rad radian radians"),
                new Unit("turns", "turns", 2 * Math.PI, "turn turns")),
            new UnitFamily("base", "Number bases", true,
                new Unit("decimal", "", 0, ""),
                new Unit("hexadecimal", "0x", 0, ""),
                new Unit("binary", "0b", 0, ""),
                new Unit("octal", "0o", 0, "")),
        };

        public static UnitFamily Family(string id) => Families.FirstOrDefault(f => f.Id == id);

        private static readonly Regex BaseRx = new Regex(@"^(0x[0-9a-f]+|0b[01]+|0o[0-7]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HoursMinRx = new Regex(@"^(\d+)\s*h(?:ours?|rs?)?\s*(\d+)\s*m(?:in(?:utes?|s)?)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FeetInchRx = new Regex(@"^(\d+)\s*(?:'|ft)\s*(\d+(?:\.\d+)?)\s*(?:""|in)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AmountRx = new Regex(@"^(-?\d[\d,]*(?:\.\d+)?|-?\.\d+)\s*(.+)$", RegexOptions.Compiled);

        /// <summary>Reads "5 kg", "72°F", "6'2"", "2 h 15 min", "0xFF", "30 mpg". Plain numbers without a unit are not
        /// amounts, except in the number-base forms.</summary>
        public static Quantity Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var raw = text.Trim();
            if (raw.Length > 40 || raw.Contains('\n')) return null;

            var b = BaseRx.Match(raw);
            if (b.Success)
            {
                var s = raw.Substring(2);
                int radix = char.ToLowerInvariant(raw[1]) == 'x' ? 16 : char.ToLowerInvariant(raw[1]) == 'b' ? 2 : 8;
                try { return new Quantity { Number = Convert.ToInt64(s, radix), Family = Family("base"), Unit = Family("base").Units[radix == 16 ? 1 : radix == 2 ? 2 : 3], Source = raw }; }
                catch { return null; }
            }
            var hm = HoursMinRx.Match(raw);
            if (hm.Success)
            {
                var time = Family("time");
                return new Quantity { Number = double.Parse(hm.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + double.Parse(hm.Groups[2].Value, CultureInfo.InvariantCulture), Family = time, Unit = time.Units[1], Source = raw };
            }
            var fi = FeetInchRx.Match(raw);
            if (fi.Success)
            {
                var len = Family("length");
                return new Quantity { Number = double.Parse(fi.Groups[1].Value, CultureInfo.InvariantCulture) * 12 + double.Parse(fi.Groups[2].Value, CultureInfo.InvariantCulture), Family = len, Unit = len.Units[4], Source = raw };
            }

            var m = AmountRx.Match(raw);
            if (!m.Success) return null;
            if (!double.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return null;
            var word = m.Groups[2].Value.Trim().ToLowerInvariant();
            word = Regex.Replace(word, @"\s+", " ");
            word = word.Replace("fl oz", "floz").Replace("fl. oz", "floz").Replace("mpg (uk)", "mpguk").Replace("mpg uk", "mpguk").Replace("º", "°")
                       .Replace("sq ft", "sqft").Replace("sq m", "sqm").Replace("l/100 km", "l/100km").Replace("km / h", "km/h");
            if (word.EndsWith(".")) word = word.TrimEnd('.');
            foreach (var f in Families)
                foreach (var u in f.Units)
                    if (u.Spellings.Contains(word))
                        return new Quantity { Number = n, Family = f, Unit = u, Source = raw };
            return null;
        }

        /// <summary>The amount in every unit of its family, or of another family when the dialog asks for one.</summary>
        public static List<Converted> ConvertAll(Quantity q)
        {
            var f = q.Family;
            var list = new List<Converted>();
            if (f.Id == "temp")
            {
                double c = q.Unit.Name == "Celsius" ? q.Number : q.Unit.Name == "Fahrenheit" ? (q.Number - 32) * 5 / 9 : q.Number - 273.15;
                Add(list, f.Units[0], c, q); Add(list, f.Units[1], c * 9 / 5 + 32, q); Add(list, f.Units[2], c + 273.15, q);
            }
            else if (f.Id == "fuel")
            {
                if (q.Number <= 0) return list;
                double l = q.Unit.Name.StartsWith("litres") ? q.Number : q.Unit.Name == "US mpg" ? 235.214583 / q.Number : 282.480936 / q.Number;
                Add(list, f.Units[0], l, q); Add(list, f.Units[1], 235.214583 / l, q); Add(list, f.Units[2], 282.480936 / l, q);
            }
            else if (f.Id == "base")
            {
                long i = (long)Math.Truncate(q.Number);
                string[] texts = { i.ToString(CultureInfo.InvariantCulture), "0x" + Convert.ToString(i, 16).ToUpperInvariant(), "0b" + Convert.ToString(i, 2), "0o" + Convert.ToString(i, 8) };
                for (int k = 0; k < 4; k++) list.Add(new Converted { Unit = f.Units[k], Number = i, Text = texts[k], IsSource = f.Units[k] == q.Unit });
            }
            else
            {
                double baseValue = q.Number * q.Unit.Factor;
                foreach (var u in f.Units) Add(list, u, baseValue / u.Factor, q);
            }
            return list;
        }

        private static void Add(List<Converted> list, Unit u, double v, Quantity q) =>
            list.Add(new Converted { Unit = u, Number = v, Text = WithSymbol(Format(v), u), IsSource = u == q.Unit });

        public static string WithSymbol(string number, Unit u) =>
            string.IsNullOrEmpty(u.Symbol) ? number : u.Symbol.StartsWith("°") && u.Symbol.Length > 1 ? number + u.Symbol : number + " " + u.Symbol;

        /// <summary>Up to four decimals below 1 and two above, no grouping, so the result pastes into any calculator.</summary>
        public static string Format(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
            var a = Math.Abs(v);
            if (a != 0 && (a >= 1e12 || a < 1e-4)) return v.ToString("0.###e+0", CultureInfo.InvariantCulture);
            var s = Math.Round(v, a < 1 ? 4 : 2).ToString("0.####", CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }

        /// <summary>The toast fact: the source amount and every other unit as a copyable value.</summary>
        public static SmartFact Detect(string text)
        {
            var q = Parse(text);
            if (q == null) return null;
            var all = ConvertAll(q);
            if (all.Count < 2) return null;
            var fact = new SmartFact("unit", "You copied an amount", q.Source + " · " + q.Family.Name.ToLowerInvariant())
                .Set("family", q.Family.Id).Set("unit", q.Unit.Name).Set("number", q.Number.ToString("R", CultureInfo.InvariantCulture));
            foreach (var c in all.Where(c => !c.IsSource)) fact.Value(c.Unit.Name, c.Text);
            fact.Act("All conversions", SmartDo.Run, "conversions:" + q.Source, primary: true, tip: "Every unit at once, each one copyable");
            return fact;
        }
    }
}
