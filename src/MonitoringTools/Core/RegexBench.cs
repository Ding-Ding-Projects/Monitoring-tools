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

        // ---------------------------------------------------------------- two patterns side by side

        public sealed record PatternRun(string Label, string Pattern, string Error, int Matches, double Ms, bool TimedOut, List<(int Index, int Length)> Hits);

        public sealed record PairResult(PatternRun A, PatternRun B, string Verdict);

        /// <summary>Runs two patterns on the same subject with the same options and says which finds what and which is faster.</summary>
        public static PairResult ComparePatterns(string patternA, string patternB, RegexOptions options, string subject)
        {
            subject ??= "";
            if (subject.Length > RegexKit.MaxSampleLength) subject = subject[..RegexKit.MaxSampleLength];
            PatternRun One(string label, string p)
            {
                var rx = RegexKit.TryCreate(p, options, out var err);
                if (rx == null) return new PatternRun(label, p, err, 0, 0, false, new List<(int, int)>());
                var hits = new List<(int, int)>();
                var sw = Stopwatch.StartNew();
                bool to = false;
                try { for (var m = rx.Match(subject); m.Success && hits.Count < MaxCompared; m = m.NextMatch()) hits.Add((m.Index, m.Length)); }
                catch (RegexMatchTimeoutException) { to = true; }
                double first = sw.Elapsed.TotalMilliseconds;
                // a fast pattern is timed over a few repeats so the number is not just one noisy sample
                if (!to && first < 5)
                {
                    int runs = 1; sw.Restart();
                    try { while (runs < 500 && sw.Elapsed.TotalMilliseconds < 25) { rx.IsMatch(subject); runs++; } }
                    catch (RegexMatchTimeoutException) { to = true; }
                    if (!to && runs > 1) first = sw.Elapsed.TotalMilliseconds / runs;
                }
                return new PatternRun(label, p, null, hits.Count, first, to, hits);
            }
            var a = One("A", patternA ?? ""); var b = One("B", patternB ?? "");
            string verdict;
            if (a.Error != null || b.Error != null) verdict = "At least one pattern is not valid, so there is nothing to compare.";
            else if (a.TimedOut || b.TimedOut) verdict = (a.TimedOut ? "A" : "B") + " hit the time limit" + (a.TimedOut && b.TimedOut ? " and so did B" : "") + ", so its matches are incomplete.";
            else
            {
                var same = a.Hits.SequenceEqual(b.Hits);
                var speed = a.Ms <= 0 || b.Ms <= 0 ? "" : a.Ms < b.Ms * 0.8 ? $" A is about {b.Ms / Math.Max(a.Ms, 0.0001):0.0} times faster." : b.Ms < a.Ms * 0.8 ? $" B is about {a.Ms / Math.Max(b.Ms, 0.0001):0.0} times faster." : " The times are within noise of each other.";
                verdict = (same ? $"Both find the same {a.Hits.Count} match(es)." : $"They differ: A finds {a.Matches}, B finds {b.Matches}; " + FirstPairDifference(a.Hits, b.Hits) + ".") + speed;
            }
            return new PairResult(a, b, verdict);
        }

        private static string FirstPairDifference(List<(int Index, int Length)> a, List<(int Index, int Length)> b)
        {
            int n = Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++) if (a[i] != b[i]) return $"the first difference is match #{i + 1} (A at {a[i].Index} length {a[i].Length}, B at {b[i].Index} length {b[i].Length})";
            return a.Count > b.Count ? $"A has {a.Count - n} more after the shared ones" : $"B has {b.Count - n} more after the shared ones";
        }

        // ---------------------------------------------------------------- per-token profile

        public sealed record TokenTime(string Text, int Start, int End, double PrefixMs, double DeltaMs, int PrefixMatches, bool TimedOut, bool Skipped, string Note);

        /// <summary>
        /// Times the pattern one top-level piece at a time: the first piece alone, then the first two, and so on. The difference between
        /// two neighbours is the cost that piece added on this subject. Only pieces that form a valid pattern on their own are timed;
        /// the others are listed with the reason. This is a cost estimate from the real engine, not an internal profile of it.
        /// </summary>
        public static List<TokenTime> Profile(string pattern, RegexOptions options, string subject, CancellationToken ct = default)
        {
            var list = new List<TokenTime>();
            pattern ??= ""; subject ??= "";
            if (subject.Length > 20000) subject = subject[..20000];
            var tree = RegexTree.Parse(pattern, options & ~(RegexOptions.NonBacktracking | RegexOptions.RightToLeft | RegexOptions.ECMAScript));
            if (!tree.Valid || tree.Root == null) { list.Add(new TokenTime(pattern, 0, pattern.Length, 0, 0, 0, false, true, "The pattern is not valid: " + tree.Message)); return list; }
            var root = tree.Root;
            var parts = new List<RegexTree.Node>();
            foreach (var c in root.Children)
            {
                if (c.Kind == RegexTree.NodeKind.Alternation)
                {
                    foreach (var br in c.Children) parts.Add(br);
                    list.Add(new TokenTime(c.Text(pattern), c.Start, c.End, 0, 0, 0, false, true, "Alternation: each branch is timed on its own below."));
                    break;
                }
                parts.Add(c);
            }
            bool alternation = root.Children.Any(c => c.Kind == RegexTree.NodeKind.Alternation);
            double previous = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var node = parts[i];
                var text = alternation ? node.Text(pattern) : pattern.Substring(parts[0].Start, node.End - parts[0].Start);
                var piece = node.Text(pattern);
                if (piece.Length == 0) continue;
                var rx = RegexKit.TryCreate(text, options, out var err);
                if (rx == null) { list.Add(new TokenTime(piece, node.Start, node.End, 0, 0, 0, false, true, "Not a valid pattern on its own: " + err)); continue; }
                int count = 0; bool to = false;
                double ms;
                var sw = Stopwatch.StartNew();
                try { foreach (Match m in rx.Matches(subject)) { count++; if (count >= MaxCompared) break; } }
                catch (RegexMatchTimeoutException) { to = true; }
                ms = sw.Elapsed.TotalMilliseconds;
                if (!to && ms < 10)
                {
                    int runs = 1; sw.Restart();
                    try { while (runs < 300 && sw.Elapsed.TotalMilliseconds < 20) { rx.Matches(subject).Count.ToString(); runs++; } }
                    catch (RegexMatchTimeoutException) { to = true; }
                    if (!to && runs > 1) ms = sw.Elapsed.TotalMilliseconds / runs;
                }
                var delta = alternation ? ms : Math.Max(0, ms - previous);
                list.Add(new TokenTime(piece, node.Start, node.End, ms, delta, count, to, false, to ? "Stopped at the time limit" : ""));
                if (!alternation) previous = ms;
                if (to) break;
            }
            return list;
        }

        // ---------------------------------------------------------------- saved history

        public const int MaxHistory = 30;

        /// <summary>Adds a record at the front of the saved history and trims it. Returns the record.</summary>
        public static RegexBenchRecord Remember(string pattern, RegexOptions options, SubjectBench bench)
        {
            var rec = new RegexBenchRecord { When = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Pattern = pattern ?? "", Options = (int)options, SubjectLength = bench.Length, MsPerRun = Math.Round(bench.MsPerRun, 4), TimedOut = bench.TimedOut, Verdict = bench.Verdict ?? "" };
            var s = Settings.Current;
            if (s == null) return rec;
            s.RegexBenchHistory ??= new List<RegexBenchRecord>();
            s.RegexBenchHistory.Insert(0, rec);
            if (s.RegexBenchHistory.Count > MaxHistory) s.RegexBenchHistory.RemoveRange(MaxHistory, s.RegexBenchHistory.Count - MaxHistory);
            return rec;
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
