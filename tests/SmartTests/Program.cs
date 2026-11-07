using System;
using System.Collections.Generic;
using System.Linq;
using MonitoringTools.Clip.Smart;

namespace SmartTests
{
    public static partial class Program
    {
        private static int failed, passed;

        private static void Check(string name, bool ok, string detail = "")
        {
            if (ok) passed++; else { failed++; Console.WriteLine("FAIL " + name + (detail.Length > 0 ? " · " + detail : "")); }
        }

        private static void Kind(string text, string kind)
        {
            var f = SmartDetect.Best(text);
            Check("kind of " + text.Replace("\n", "\\n"), (f?.Kind) == kind, "got " + (f?.Kind ?? "nothing"));
        }

        private static string V(SmartFact f, string label) => f?.Values.FirstOrDefault(v => v.Label == label)?.Text;

        public static int Main()
        {
            Kinds();
            Conversions();
            Everyday();
            DeveloperChecks();
            Extensions();
            Power();
            Defaults();
            Console.WriteLine($"{passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
