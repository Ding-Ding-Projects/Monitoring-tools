using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Real measurements against the .NET regex engine: a comparison of the engine modes that exist in
    /// this runtime, and a benchmark with an input-growth run. Nothing here emulates another engine.
    /// </summary>
    public static class RegexBench
    {
        private const int MaxCompared = 5000;

        // ---------------------------------------------------------------- compare

        public sealed record ModeResult(string Mode, bool Supported, string Note, int Matches, string FirstDifference, double ConstructMs, double MatchMs, bool TimedOut);

        private sealed record Hit(int Index, int Length, string Value);

        public static List<ModeResult> Compare(string pattern, RegexOptions baseOptions, string sample, CancellationToken ct = default)
        {
            pattern ??= "";
            sample ??= "";
            if (sample.Length > RegexKit.MaxSampleLength) sample = sample[..RegexKit.MaxSampleLength];
            var clean = baseOptions & ~(RegexOptions.NonBacktracking | RegexOptions.ECMAScript | RegexOptions.Compiled);
            var modes = new (string Name, RegexOptions Options)[]
            {
                ("Backtracking (default)", clean),
                ("NonBacktracking", clean | RegexOptions.NonBacktracking),
                ("ECMAScript", clean | RegexOptions.ECMAScript),
                ("Compiled", clean | RegexOptions.Compiled),
            };
            var results = new List<ModeResult>();
            List<Hit> baseline = null;
            foreach (var (name, opts) in modes)
            {
                ct.ThrowIfCancellationRequested();
                Regex rx = null;
                var sw = Stopwatch.StartNew();
                try { rx = new Regex(pattern, opts, RegexKit.Timeout); }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
                {
                    results.Add(new ModeResult(name, false, "Not supported for this pattern and options: " + First(ex.Message), 0, null, sw.Elapsed.TotalMilliseconds, 0, false));
                    continue;
                }
                double construct = sw.Elapsed.TotalMilliseconds;
                var hits = new List<Hit>();
                bool timedOut = false;
                sw.Restart();
                try
                {
                    for (var m = rx.Match(sample); m.Success && hits.Count < MaxCompared; m = m.NextMatch())
                        hits.Add(new Hit(m.Index, m.Length, m.Value));
                }
                catch (RegexMatchTimeoutException) { timedOut = true; }
                double ms = sw.Elapsed.TotalMilliseconds;
                string diff;
                if (baseline == null && name.StartsWith("Backtracking", StringComparison.Ordinal)) { baseline = timedOut ? null : hits; diff = timedOut ? "This is the reference run, but it hit the time limit." : "Reference for the other modes."; }
                else if (baseline == null) diff = "No reference run to compare with.";
                else if (timedOut) diff = "Hit the time limit, so the matches are incomplete.";
                else diff = FirstDifference(baseline, hits);
                results.Add(new ModeResult(name, true, timedOut ? "Stopped at the " + RegexKit.Timeout.TotalMilliseconds.ToString("0") + " ms limit" : "Supported", hits.Count, diff, construct, ms, timedOut));
            }
            return results;
        }

        private static string First(string m) => (m ?? "").Split('\n')[0].Trim();

        private static string Show(string v)
        {
            v = (v ?? "").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            return v.Length > 40 ? v[..40] + "..." : v;
        }

        private static string FirstDifference(List<Hit> a, List<Hit> b)
        {
            int n = Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++)
                if (a[i].Index != b[i].Index || a[i].Length != b[i].Length)
                    return $"Match #{i + 1} differs: reference at {a[i].Index} length {a[i].Length} \"{Show(a[i].Value)}\", this mode at {b[i].Index} length {b[i].Length} \"{Show(b[i].Value)}\".";
            if (a.Count != b.Count)
                return a.Count > b.Count ? $"Same for the first {n} match(es); the reference has {a.Count - n} more." : $"Same for the first {n} match(es); this mode has {b.Count - n} more.";
            return "Identical to the reference.";
        }

        // ---------------------------------------------------------------- benchmark

        public static readonly int[] GrowthFactors = { 1, 2, 4, 8, 16 };

        public sealed record SizeTiming(int Factor, int Length, double Ms, bool TimedOut);

        public sealed record SubjectBench(string Label, int Length, bool Matched, double MsPerRun, bool TimedOut, List<SizeTiming> Growth, string Verdict, bool Skipped);

        /// <summary>Mean time per call: one measured run, repeated for up to a short budget when the first run is fast.</summary>
        private static double Time(Regex rx, string s, CancellationToken ct, out bool timedOut, out bool matched)
        {
            timedOut = false; matched = false;
            var sw = Stopwatch.StartNew();
            try { matched = rx.IsMatch(s); }
            catch (RegexMatchTimeoutException) { timedOut = true; return sw.Elapsed.TotalMilliseconds; }
            double first = sw.Elapsed.TotalMilliseconds;
            if (first >= 5) return first;
            int runs = 1;
            sw.Restart();
            try
            {
                while (runs < 2000 && sw.Elapsed.TotalMilliseconds < 30 && !ct.IsCancellationRequested) { rx.IsMatch(s); runs++; }
            }
            catch (RegexMatchTimeoutException) { timedOut = true; }
            return runs <= 1 ? first : sw.Elapsed.TotalMilliseconds / (runs - 1);
        }

        public static SubjectBench Run(Regex rx, string label, string subject, CancellationToken ct, Action<string> progress)
        {
            subject ??= "";
            if (subject.Length == 0)
                return new SubjectBench(label, 0, false, 0, false, new List<SizeTiming>(), "Enter a subject to benchmark.", true);
            if (subject.Length > RegexKit.MaxSampleLength) subject = subject[..RegexKit.MaxSampleLength];

            progress?.Invoke(label + ": timing a single subject");
            double per = Time(rx, subject, ct, out var to, out var matched);
            var growth = new List<SizeTiming>();
            foreach (var f in GrowthFactors)
            {
                ct.ThrowIfCancellationRequested();
                var big = Repeat(subject, f);
                progress?.Invoke($"{label}: growth run at {f}x ({big.Length:N0} characters)");
                var ms = Time(rx, big, ct, out var t, out _);
                growth.Add(new SizeTiming(f, big.Length, ms, t));
                if (t) break; // larger sizes would only hit the same limit
            }
            return new SubjectBench(label, subject.Length, matched, per, to, growth, Verdict(growth), false);
        }

        private static string Repeat(string s, int factor)
        {
            if ((long)s.Length * factor > RegexKit.MaxSampleLength) factor = Math.Max(1, RegexKit.MaxSampleLength / s.Length);
            var sb = new StringBuilder(s.Length * factor);
            for (int i = 0; i < factor; i++) sb.Append(s);
            return sb.ToString();
        }

        public static string Verdict(List<SizeTiming> g)
        {
            if (g.Count == 0) return "No growth run.";
            var hit = g.FirstOrDefault(x => x.TimedOut);
            if (hit != null && hit.Factor == 1)
                return $"Hit the {RegexKit.Timeout.TotalMilliseconds:0} ms limit already on the original subject ({hit.Length:N0} characters), so there is no growth to measure. The pattern backtracks catastrophically here.";
            if (hit != null)
                return $"Hit the {RegexKit.Timeout.TotalMilliseconds:0} ms limit at {hit.Factor}x ({hit.Length:N0} characters). Time grows faster than linearly on this subject, or the pattern backtracks catastrophically.";
            var first = g[0];
            var last = g[^1];
            if (last.Factor <= first.Factor) return "Not enough sizes to judge growth.";
            if (last.Ms < 1.0) return $"Too fast to measure growth: {last.Factor}x the input ({last.Length:N0} characters) still takes under 1 ms.";
            double t1 = Math.Max(first.Ms, 0.005);
            double exp = Math.Log(last.Ms / t1) / Math.Log((double)last.Factor / first.Factor);
            if (exp <= 1.35) return $"Linear: the time grows about in proportion to the input (measured exponent about {exp:0.0}).";
            return $"Faster than linear: the time grows roughly like input size to the power {exp:0.0}. Measurements this short are noisy, so treat the exponent as a rough guide.";
        }
    }
}
