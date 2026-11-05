using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Other regular expression engines that this computer can really run, and translation of a .NET
    /// pattern into the syntax of other flavours. Nothing here emulates an engine: an engine that is not
    /// installed is reported as unavailable, and a construct that has no equivalent is listed as a limit
    /// instead of being rewritten into something that merely looks similar.
    ///
    /// Engines run offline: .NET in this app, the Windows JScript engine (VBScript.RegExp, an ECMAScript
    /// 3 flavour), Windows PowerShell 5.1 (the .NET Framework engine), and Python's re module or Node.js
    /// (V8) when those are installed. Each outside engine runs in a short-lived child process or a
    /// bounded worker, receives the pattern and subject as data (never as program text) and is stopped
    /// after a few seconds.
    /// </summary>
    public static class RegexFlavours
    {
        public const int ExternalTimeoutMs = 6000;
        public const int MaxHits = 500;
        public const int MaxSubject = 64 * 1024;

        public sealed record Hit(int Index, int Length);

        public sealed record EngineResult(string Engine, string Version, bool Available, string Note, string Error, List<Hit> Hits, double Ms, bool TimedOut, string Converted)
        {
            public int Matches => Hits?.Count ?? 0;
        }

        // ---------------------------------------------------------------- translation

        public static readonly string[] Targets = { "Python", "JavaScript", "Java" };

        public sealed record Conversion(string Language, string Pattern, string Flags, List<string> Limits);

        /// <summary>
        /// Translates a .NET pattern for another flavour. The limits list names every construct in the
        /// pattern that the target cannot express; the returned pattern is then left as it was for that
        /// construct, so the target engine's own error is what the user sees.
        /// </summary>
        public static Conversion Convert(string language, string pattern, RegexOptions options)
        {
            pattern ??= "";
            var limits = new List<string>();
            var tree = RegexTree.Parse(pattern, options & ~(RegexOptions.NonBacktracking | RegexOptions.RightToLeft | RegexOptions.ECMAScript));
            var nodes = tree.Root == null ? new List<RegexTree.Node>() : RegexTree.Walk(tree.Root).ToList();
            bool Has(Func<RegexTree.Node, bool> f) => nodes.Any(f);
            void Limit(bool present, string text) { if (present && !limits.Contains(text)) limits.Add(text); }

            Limit(Has(n => n.Kind == RegexTree.NodeKind.Group && n.Group == RegexTree.GroupKind.Balancing), "Balancing groups exist only in .NET.");
            Limit(Has(n => n.Kind == RegexTree.NodeKind.Subtraction), language == "Java" ? "Character class subtraction needs the Java form [a-z&&[^aeiou]]; it was left unchanged." : "Character class subtraction exists only in .NET and Java.");
            Limit(Has(n => n.Kind == RegexTree.NodeKind.Anchor && n.Text(pattern) == "\\G"), language == "Java" ? "" : "\\G (continue from the previous match) has no equivalent.");
            Limit(language == "JavaScript" && Has(n => n.Kind == RegexTree.NodeKind.Group && n.Group == RegexTree.GroupKind.Atomic), "Atomic groups are not part of JavaScript.");
            Limit(language == "JavaScript" && Has(n => n.Kind == RegexTree.NodeKind.InlineOptions || n.Kind == RegexTree.NodeKind.Group && n.Group == RegexTree.GroupKind.Options), "Inline options such as (?i) are not part of JavaScript; use the flags instead.");
            Limit(language != "Python" && Has(n => n.Kind == RegexTree.NodeKind.Conditional), language == "JavaScript" ? "Conditionals are not part of JavaScript." : "Conditionals are not part of Java.");
            Limit(language == "Python" && Has(n => n.Kind == RegexTree.NodeKind.Property), "Python's re module has no \\p{...} Unicode properties.");
            Limit(language == "JavaScript" && Has(n => n.Kind == RegexTree.NodeKind.Property), "\\p{...} needs the u flag in JavaScript and uses different property names; it was left unchanged.");
            Limit((options & RegexOptions.RightToLeft) != 0, "Right to left has no equivalent.");
            Limit((options & RegexOptions.NonBacktracking) != 0, "Non-backtracking has no equivalent; the normal engine of the target is used.");
            Limit((options & RegexOptions.ExplicitCapture) != 0, "Explicit capture has no equivalent; unnamed groups still capture.");
            Limit((options & RegexOptions.IgnorePatternWhitespace) != 0 && language == "JavaScript", "Ignore whitespace has no equivalent in JavaScript.");
            limits.RemoveAll(string.IsNullOrEmpty);

            string p = pattern;
            // Anchors that differ between flavours.
            if (language == "JavaScript")
            {
                p = Regex.Replace(p, @"(?<!\\)((?:\\\\)*)\\A", "$1(?<![\\s\\S])");
                p = Regex.Replace(p, @"(?<!\\)((?:\\\\)*)\\z", "$1(?![\\s\\S])");
                p = Regex.Replace(p, @"(?<!\\)((?:\\\\)*)\\Z", "$1(?=\\n?(?![\\s\\S]))");
            }
            else if (language == "Python")
            {
                p = Regex.Replace(p, @"(?<!\\)((?:\\\\)*)\\Z", "$1(?=\\n?\\Z)");
                p = Regex.Replace(p, @"(?<!\\)((?:\\\\)*)\\z", "$1\\Z");
                p = Regex.Replace(p, @"\(\?<([A-Za-z_]\w*)>", "(?P<$1>");
                p = Regex.Replace(p, @"\\k<([A-Za-z_]\w*)>", "(?P=$1)");
            }
            var flags = new StringBuilder();
            if ((options & RegexOptions.IgnoreCase) != 0) flags.Append('i');
            if ((options & RegexOptions.Multiline) != 0) flags.Append('m');
            if ((options & RegexOptions.Singleline) != 0) flags.Append('s');
            if ((options & RegexOptions.IgnorePatternWhitespace) != 0 && language != "JavaScript") flags.Append('x');
            return new Conversion(language, p, flags.ToString(), limits);
        }

        /// <summary>Translates a .NET replacement string for another flavour. Tokens with no equivalent are reported through limits.</summary>
        public static string ConvertReplacement(string language, string replacement, List<string> limits)
        {
            replacement ??= "";
            var sb = new StringBuilder();
            for (int i = 0; i < replacement.Length; i++)
            {
                char c = replacement[i];
                if (c != '$' || i + 1 >= replacement.Length) { sb.Append(Lit(language, c)); continue; }
                char n = replacement[i + 1];
                if (n == '$') { sb.Append(language == "Python" ? "$" : language == "Java" ? "\\$" : "$$"); i++; continue; }
                if (char.IsDigit(n))
                {
                    int j = i + 1;
                    while (j < replacement.Length && char.IsDigit(replacement[j]) && j - i <= 2) j++;
                    var num = replacement.Substring(i + 1, j - i - 1);
                    sb.Append(language == "Python" ? "\\g<" + num + ">" : "$" + num);
                    i = j - 1; continue;
                }
                if (n == '{')
                {
                    int close = replacement.IndexOf('}', i + 2);
                    if (close > 0)
                    {
                        var name = replacement.Substring(i + 2, close - i - 2);
                        sb.Append(language == "Python" ? "\\g<" + name + ">" : language == "JavaScript" ? (name.All(char.IsDigit) ? "$" + name : "$<" + name + ">") : "${" + name + "}");
                        i = close; continue;
                    }
                }
                if (n == '&') { sb.Append(language == "Python" ? "\\g<0>" : language == "JavaScript" ? "$&" : "$0"); i++; continue; }
                if (n == '`' || n == '\'' || n == '+' || n == '_')
                {
                    var l = "The replacement token $" + n + " has no equivalent" + (language == "JavaScript" && (n == '`' || n == '\'') ? "" : "") + ".";
                    if (language == "JavaScript" && (n == '`' || n == '\'')) { sb.Append('$').Append(n); i++; continue; }
                    if (!limits.Contains(l)) limits.Add(l);
                    sb.Append('$').Append(n); i++; continue;
                }
                sb.Append(Lit(language, c));
            }
            return sb.ToString();
        }

        private static string Lit(string language, char c) => language == "Python" && c == '\\' ? "\\\\" : c.ToString();

        // ---------------------------------------------------------------- running engines

        /// <summary>Runs the same pattern text in every engine that exists on this computer.</summary>
        public static List<EngineResult> RunAll(string pattern, RegexOptions options, string subject, bool includeExternal = true)
        {
            pattern ??= ""; subject ??= "";
            if (subject.Length > MaxSubject) subject = subject[..MaxSubject];
            var jobs = new List<Task<EngineResult>>
            {
                Task.Run(() => Dotnet(pattern, options, subject)),
                Task.Run(() => Jscript(pattern, options, subject)),
            };
            if (includeExternal)
            {
                jobs.Add(Task.Run(() => WindowsPowerShell(pattern, options, subject)));
                jobs.Add(Task.Run(() => Python(pattern, options, subject)));
                jobs.Add(Task.Run(() => Node(pattern, options, subject)));
            }
            Task.WaitAll(jobs.ToArray());
            return jobs.Select(j => j.Result).ToList();
        }

        public static EngineResult Dotnet(string pattern, RegexOptions options, string subject)
        {
            var o = options & ~(RegexOptions.NonBacktracking | RegexOptions.ECMAScript | RegexOptions.RightToLeft | RegexOptions.Compiled);
            var sw = Stopwatch.StartNew();
            var hits = new List<Hit>();
            try
            {
                var rx = new Regex(pattern, o, RegexKit.Timeout);
                for (var m = rx.Match(subject); m.Success && hits.Count < MaxHits; m = m.NextMatch()) hits.Add(new Hit(m.Index, m.Length));
                return new EngineResult(".NET " + Environment.Version.ToString(2) + " (this app)", Environment.Version.ToString(), true, "Backtracking engine in this app", null, hits, sw.Elapsed.TotalMilliseconds, false, pattern);
            }
            catch (RegexMatchTimeoutException) { return new EngineResult(".NET " + Environment.Version.ToString(2) + " (this app)", Environment.Version.ToString(), true, "Stopped at the time limit", null, hits, sw.Elapsed.TotalMilliseconds, true, pattern); }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException) { return new EngineResult(".NET " + Environment.Version.ToString(2) + " (this app)", Environment.Version.ToString(), true, "", FirstLine(ex.Message), hits, sw.Elapsed.TotalMilliseconds, false, pattern); }
        }

        /// <summary>The Windows JScript regular expression object (ECMAScript 3 flavour), reached through COM. No lookbehind, named groups, \A or \z.</summary>
        public static EngineResult Jscript(string pattern, RegexOptions options, string subject)
        {
            const string name = "JScript RegExp (Windows, ECMAScript 3)";
            var type = Type.GetTypeFromProgID("VBScript.RegExp");
            if (type == null) return new EngineResult(name, "", false, "Not available: the VBScript.RegExp component is not installed on this computer.", null, null, 0, false, pattern);
            var result = new List<Hit>();
            string error = null;
            var sw = Stopwatch.StartNew();
            var worker = new Thread(() =>
            {
                try
                {
                    dynamic re = Activator.CreateInstance(type);
                    re.Pattern = pattern;
                    re.Global = true;
                    re.IgnoreCase = (options & RegexOptions.IgnoreCase) != 0;
                    re.Multiline = (options & RegexOptions.Multiline) != 0;
                    dynamic ms = re.Execute(subject);
                    int n = ms.Count;
                    for (int i = 0; i < n && result.Count < MaxHits; i++) { dynamic m = ms[i]; result.Add(new Hit((int)m.FirstIndex, (int)m.Length)); }
                }
                catch (Exception ex) { error = FirstLine(ex.InnerException?.Message ?? ex.Message); }
            });
            worker.SetApartmentState(ApartmentState.STA);
            worker.IsBackground = true;
            worker.Start();
            if (!worker.Join(3000)) return new EngineResult(name, "5.8", true, "Stopped after 3 seconds", null, result, sw.Elapsed.TotalMilliseconds, true, pattern);
            return new EngineResult(name, "5.8", true, "Run through COM; ignore case and multiline only", error, result, sw.Elapsed.TotalMilliseconds, false, pattern);
        }

        public static EngineResult WindowsPowerShell(string pattern, RegexOptions options, string subject)
        {
            const string name = ".NET Framework 4 (Windows PowerShell 5.1)";
            var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(exe)) return new EngineResult(name, "", false, "Not available: Windows PowerShell 5.1 was not found.", null, null, 0, false, pattern);
            var o = options & ~(RegexOptions.NonBacktracking | RegexOptions.ECMAScript | RegexOptions.RightToLeft | RegexOptions.Compiled);
            var script = "$d = ConvertFrom-Json $env:MT_RX_JSON\n" +
                "try { $rx = New-Object System.Text.RegularExpressions.Regex($d.p, [int]$d.o, [TimeSpan]::FromMilliseconds(200)) } catch { Write-Output (ConvertTo-Json -Compress @{error=$_.Exception.Message; ver=$PSVersionTable.CLRVersion.ToString()}); exit 0 }\n" +
                "$h = @(); try { foreach ($m in $rx.Matches($d.t)) { if ($h.Count -ge 500) { break }; $h += ,@($m.Index, $m.Length) } } catch { Write-Output (ConvertTo-Json -Compress @{error=$_.Exception.Message; ver=$PSVersionTable.CLRVersion.ToString()}); exit 0 }\n" +
                "Write-Output (ConvertTo-Json -Compress -Depth 4 @{hits=$h; ver=$PSVersionTable.CLRVersion.ToString()})";
            var args = new List<string> { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", System.Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) };
            return RunProcess(name, exe, args, JsonSerializer.Serialize(new { p = pattern, t = subject, o = (int)o }), null, "MT_RX_JSON", pattern);
        }

        public static EngineResult Python(string pattern, RegexOptions options, string subject)
        {
            const string name = "Python re";
            var exe = FindOnPath("python.exe") ?? FindOnPath("python3.exe");
            if (exe == null || exe.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) >= 0 && !PythonWorks(exe)) return new EngineResult(name, "", false, "Not available: Python was not found on this computer.", null, null, 0, false, pattern);
            var conv = Convert("Python", pattern, options);
            var code = "import sys, json, re\nd = json.load(sys.stdin)\nfl = 0\nfor c in d['f']:\n    fl |= {'i': re.I, 'm': re.M, 's': re.S, 'x': re.X}[c]\n" +
                "try:\n    rx = re.compile(d['p'], fl)\nexcept Exception as e:\n    print(json.dumps({'error': str(e), 'ver': sys.version.split()[0]})); sys.exit(0)\n" +
                "h = []\nfor m in rx.finditer(d['t']):\n    h.append([m.start(), m.end() - m.start()])\n    if len(h) >= 500: break\nprint(json.dumps({'hits': h, 'ver': sys.version.split()[0]}))";
            var r = RunProcess(name, exe, new List<string> { "-I", "-c", code }, JsonSerializer.Serialize(new { p = conv.Pattern, t = subject, f = conv.Flags }), conv, null, pattern);
            return FixCodePoints(r, subject);
        }

        public static EngineResult Node(string pattern, RegexOptions options, string subject)
        {
            const string name = "JavaScript V8 (Node.js)";
            var exe = FindOnPath("node.exe");
            if (exe == null) return new EngineResult(name, "", false, "Not available: Node.js was not found on this computer.", null, null, 0, false, pattern);
            var conv = Convert("JavaScript", pattern, options);
            var code = "let s='';process.stdin.on('data',c=>s+=c).on('end',()=>{const d=JSON.parse(s);let rx;try{rx=new RegExp(d.p,d.f+'g');}catch(e){console.log(JSON.stringify({error:e.message,ver:process.versions.v8}));return;}" +
                "const h=[];for(const m of d.t.matchAll(rx)){h.push([m.index,m[0].length]);if(h.length>=500)break;}console.log(JSON.stringify({hits:h,ver:process.versions.v8}));});";
            return RunProcess(name, exe, new List<string> { "-e", code }, JsonSerializer.Serialize(new { p = conv.Pattern, t = subject, f = conv.Flags }), conv, null, pattern);
        }

        private static bool PythonWorks(string exe)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, "-c \"print(1)\"") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (!p.WaitForExit(3000)) { try { p.Kill(true); } catch { } return false; }
                return p.StandardOutput.ReadToEnd().Trim() == "1";
            }
            catch { return false; }
        }

        /// <summary>Python counts code points and .NET counts UTF-16 units; map the positions when the subject has pairs.</summary>
        private static EngineResult FixCodePoints(EngineResult r, string subject)
        {
            if (r.Hits == null || !subject.Any(char.IsSurrogate)) return r;
            var map = new List<int>();
            for (int i = 0; i < subject.Length; i++) { map.Add(i); if (char.IsHighSurrogate(subject[i]) && i + 1 < subject.Length) i++; }
            map.Add(subject.Length);
            int At(int cp) => cp < map.Count ? map[cp] : subject.Length;
            return r with { Hits = r.Hits.Select(h => new Hit(At(h.Index), At(h.Index + h.Length) - At(h.Index))).ToList() };
        }

        private static string FindOnPath(string file)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    var f = Path.Combine(dir.Trim(), file);
                    if (File.Exists(f)) return f;
                }
                catch { }
            }
            return null;
        }

        private static EngineResult RunProcess(string name, string exe, List<string> args, string stdin, Conversion conv, string jsonEnv, string original)
        {
            var sw = Stopwatch.StartNew();
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardInput = jsonEnv == null, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            if (jsonEnv != null) psi.Environment[jsonEnv] = stdin;
            string converted = conv?.Pattern ?? original;
            string note = conv != null && conv.Limits.Count > 0 ? "Limits: " + string.Join(" ", conv.Limits) : (conv != null && conv.Pattern != original ? "Pattern translated for this flavour" : "Same pattern text");
            try
            {
                using var p = Process.Start(psi);
                if (jsonEnv == null) { p.StandardInput.Write(stdin); p.StandardInput.Close(); }
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(ExternalTimeoutMs))
                {
                    try { p.Kill(true); } catch { }
                    return new EngineResult(name, "", true, "Stopped after " + ExternalTimeoutMs / 1000 + " seconds", null, new List<Hit>(), sw.Elapsed.TotalMilliseconds, true, converted);
                }
                var output = outTask.Result.Trim();
                if (output.Length == 0) return new EngineResult(name, "", true, note, "No result: " + FirstLine(errTask.Result), new List<Hit>(), sw.Elapsed.TotalMilliseconds, false, converted);
                using var doc = JsonDocument.Parse(output.Split('\n').Last().Trim());
                var root = doc.RootElement;
                var ver = root.TryGetProperty("ver", out var v) ? v.ToString() : "";
                if (root.TryGetProperty("error", out var err)) return new EngineResult(name, ver, true, note, FirstLine(err.GetString()), new List<Hit>(), sw.Elapsed.TotalMilliseconds, false, converted);
                var hits = new List<Hit>();
                foreach (var h in root.GetProperty("hits").EnumerateArray()) hits.Add(new Hit(h[0].GetInt32(), h[1].GetInt32()));
                return new EngineResult(name, ver, true, note, null, hits, sw.Elapsed.TotalMilliseconds, false, converted);
            }
            catch (Exception ex) { return new EngineResult(name, "", false, "Not available: " + FirstLine(ex.Message), null, null, sw.Elapsed.TotalMilliseconds, false, converted); }
        }

        private static string FirstLine(string m) => (m ?? "").Replace("\r", "").Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";

        /// <summary>Plain description of how an engine's matches differ from the reference list, or a statement that they agree.</summary>
        public static string Difference(List<Hit> reference, List<Hit> other)
        {
            if (reference == null || other == null) return "";
            int n = Math.Min(reference.Count, other.Count);
            for (int i = 0; i < n; i++)
                if (reference[i].Index != other[i].Index || reference[i].Length != other[i].Length)
                    return $"Match #{i + 1} differs: .NET at {reference[i].Index} length {reference[i].Length}, this engine at {other[i].Index} length {other[i].Length}.";
            if (reference.Count != other.Count) return reference.Count > other.Count ? $"Same for {n} match(es); .NET finds {reference.Count - n} more." : $"Same for {n} match(es); this engine finds {other.Count - n} more.";
            return "Identical matches.";
        }
    }
}
