using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Command line entry for the regex engine: MonitoringTools.exe --regex "pattern" [--options i,m,s,x,n] [--replace "text"] [--json] file...
    /// Exit code 0 when anything matched, 1 when nothing matched, 2 on an invalid pattern or usage error.
    /// The app is a windowed program, so the console of the parent terminal is attached before any output.
    /// </summary>
    public static class RegexCli
    {
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(int pid);
        [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int which);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        private static TextWriter _out, _err;

        private static void OpenConsole()
        {
            bool outRedirected = IsUsable(GetStdHandle(-11)), errRedirected = IsUsable(GetStdHandle(-12));
            if (!outRedirected || !errRedirected) AttachConsole(-1);
            var utf8 = new UTF8Encoding(false);
            _out = MakeWriter(outRedirected, "CONOUT$", () => Console.OpenStandardOutput(), utf8);
            _err = MakeWriter(errRedirected, "CONOUT$", () => Console.OpenStandardError(), utf8);
        }

        private static bool IsUsable(IntPtr h) => h != IntPtr.Zero && h != new IntPtr(-1);

        private static TextWriter MakeWriter(bool redirected, string console, Func<Stream> std, Encoding enc)
        {
            try
            {
                Stream s;
                if (redirected) s = std();
                else
                {
                    var h = CreateFile(console, 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); // GENERIC_WRITE, share read+write, OPEN_EXISTING
                    if (h.IsInvalid) return TextWriter.Null;
                    s = new FileStream(h, FileAccess.Write);
                }
                return new StreamWriter(s, enc) { AutoFlush = true };
            }
            catch { return TextWriter.Null; }
        }

        public static int Run(string[] args)
        {
            OpenConsole();
            var dataDir = Environment.GetEnvironmentVariable("MT_DATA_DIR");   // lets a test point the library and backups at a throwaway folder
            if (!string.IsNullOrWhiteSpace(dataDir)) Settings.DataDirOverride = dataDir;
            string pattern = null, replace = null, libName = null, requestFile = null;
            bool json = false, help = false, inPlace = false, listLib = false, optionsGiven = false, automation = false;
            var paramValues = new Dictionary<string, string>();
            var extraPatterns = new List<string>();   // --pattern and --patterns-file; when given, every plain argument is a file
            var options = RegexOptions.None;
            var files = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                switch (a)
                {
                    case "--json": json = true; break;
                    case "--help": case "-h": case "/?": help = true; break;
                    case "--in-place": inPlace = true; break;
                    case "--list-library": listLib = true; break;
                    case "--automation": automation = true; break;
                    case "--request":
                        if (i + 1 >= args.Length) return Usage("--request needs the path of a JSON request file.");
                        requestFile = args[++i]; automation = true; break;
                    case "--pattern":
                        if (i + 1 >= args.Length) return Usage("--pattern needs a pattern.");
                        extraPatterns.Add(args[++i]); break;
                    case "--patterns-file":
                    {
                        if (i + 1 >= args.Length) return Usage("--patterns-file needs a file with one pattern per line.");
                        var pf = args[++i];
                        string[] lines;
                        try { lines = RegexGrep.Decode(File.ReadAllBytes(pf)).Text.Replace("\r\n", "\n").Split('\n'); }
                        catch (Exception ex) { _err.WriteLine(pf + ": " + ex.Message); return 2; }
                        // one pattern per line; empty lines and lines starting with "#" are skipped (write \# for a pattern that starts with #)
                        foreach (var line in lines) if (line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal)) extraPatterns.Add(line);
                        if (!extraPatterns.Any()) { _err.WriteLine(pf + ": the file has no patterns."); return 2; }
                        break;
                    }
                    case "--lib":
                        if (i + 1 >= args.Length) return Usage("--lib needs the name of a saved pattern.");
                        libName = args[++i]; break;
                    case "--param":
                    {
                        if (i + 1 >= args.Length || args[i + 1].IndexOf('=') <= 0) return Usage("--param needs name=value.");
                        var kv = args[++i]; int eq = kv.IndexOf('=');
                        paramValues[kv[..eq]] = kv[(eq + 1)..]; break;
                    }
                    case "--replace":
                        if (i + 1 >= args.Length) return Usage("--replace needs a value.");
                        replace = args[++i]; break;
                    case "--options":
                        if (i + 1 >= args.Length) return Usage("--options needs a value such as i,m.");
                        optionsGiven = true;
                        foreach (var o in args[++i].Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            switch (o.Trim().ToLowerInvariant())
                            {
                                case "i": options |= RegexOptions.IgnoreCase; break;
                                case "m": options |= RegexOptions.Multiline; break;
                                case "s": options |= RegexOptions.Singleline; break;
                                case "x": options |= RegexOptions.IgnorePatternWhitespace; break;
                                case "n": options |= RegexOptions.ExplicitCapture; break;
                                default: return Usage("Unknown option '" + o + "'. Use i, m, s, x or n.");
                            }
                        }
                        break;
                    default:
                        if (pattern == null) pattern = a; else files.Add(a);
                        break;
                }
            }
            if (automation)
            {
                string request;
                try
                {
                    if (requestFile != null) request = File.ReadAllText(requestFile);
                    else if (Console.IsInputRedirected) { using var r = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)); request = r.ReadToEnd(); }
                    else { _err.WriteLine("--automation reads a JSON request from standard input, or from the file given with --request."); return 2; }
                }
                catch (Exception ex) { _err.WriteLine("The request could not be read: " + ex.Message); return 2; }
                _out.WriteLine(RegexAutomation.Handle(request, out var code));
                return code;
            }
            if (extraPatterns.Count > 0)
            {
                if (libName != null) return Usage("--lib cannot be combined with --pattern or --patterns-file.");
                if (inPlace && extraPatterns.Count > 1) return Usage("--in-place takes one pattern at a time, so each run has its own backup.");
                if (pattern != null) { files.Insert(0, pattern); pattern = null; }
                if (extraPatterns.Count == 1) pattern = extraPatterns[0];
                else return RunMany(extraPatterns, options, replace, files, json);
            }
            if (listLib)
            {
                Settings.Load();
                foreach (var p in Settings.Current.SavedPatterns) _out.WriteLine(p.Name + "\t" + p.Pattern);
                return 0;
            }
            if (libName != null)
            {
                // A saved pattern supplies the pattern, its options and its replacement; flags given on the command line win. A file argument that
                // would have been read as the pattern is a file instead.
                if (pattern != null) { files.Insert(0, pattern); pattern = null; }
                Settings.Load();
                var saved = Settings.Current.SavedPatterns.FirstOrDefault(p => string.Equals(p.Name, libName, StringComparison.OrdinalIgnoreCase));
                if (saved == null) { _err.WriteLine("No saved pattern is named \"" + libName + "\". Use --list-library to see the names."); return 2; }
                pattern = RegexLibrary.Expand(saved, paramValues, out var unfilled);
                if (unfilled.Count > 0) { _err.WriteLine("The saved pattern needs a value for: " + string.Join(", ", unfilled) + ". Give each as --param name=value."); return 2; }
                if (!optionsGiven) options = (RegexOptions)saved.Options;
                if (replace == null && !string.IsNullOrEmpty(saved.Replacement) && inPlace) replace = saved.Replacement;
            }
            if (help || pattern == null) { _out.WriteLine(HelpText); return help ? 0 : 2; }

            var rx = RegexKit.TryCreate(pattern, options, out var error);
            if (rx == null) { _err.WriteLine("Invalid pattern: " + error); return 2; }

            if (inPlace)
            {
                if (replace == null) return Usage("--in-place needs --replace (or a saved pattern that has a replacement).");
                if (files.Count == 0) return Usage("--in-place needs at least one file.");
                return ReplaceInPlace(rx, replace, files, json);
            }

            var inputs = ReadInputs(files);
            if (inputs == null) return 2;

            bool any = false;
            var jsonRows = new List<object>();
            try
            {
                foreach (var (name, text) in inputs)
                {
                    if (replace != null)
                    {
                        int count = 0;
                        var result = rx.Replace(text, m => { count++; return m.Result(replace); });
                        if (count > 0) any = true;
                        if (json) jsonRows.Add(new { file = name, replacements = count, text = result });
                        else
                        {
                            if (inputs.Count > 1) _out.WriteLine("==> " + name + " <==");
                            _out.Write(result);
                            if (inputs.Count > 1 && !result.EndsWith("\n")) _out.WriteLine();
                        }
                        continue;
                    }
                    var starts = new List<int> { 0 };
                    for (int k = 0; k < text.Length; k++) if (text[k] == '\n') starts.Add(k + 1);
                    foreach (Match m in rx.Matches(text))
                    {
                        any = true;
                        int line = starts.BinarySearch(m.Index);
                        if (line < 0) line = ~line - 1;
                        int col = m.Index - starts[line] + 1;
                        if (json) jsonRows.Add(new { file = name, line = line + 1, column = col, length = m.Length, value = m.Value });
                        else _out.WriteLine($"{name}:{line + 1}:{col}: {m.Value.Replace("\r", "").Replace("\n", "\\n")}");
                    }
                }
            }
            catch (RegexMatchTimeoutException) { _err.WriteLine("Stopped at the 200 ms limit: the pattern backtracks too much on this input."); return 2; }
            catch (RegexParseException ex) { _err.WriteLine("Invalid replacement: " + ex.Message); return 2; }
            catch (ArgumentException ex) { _err.WriteLine("Invalid replacement: " + ex.Message); return 2; }

            if (json) _out.WriteLine(JsonSerializer.Serialize(jsonRows, new JsonSerializerOptions { WriteIndented = true }));
            return any ? 0 : 1;
        }

        /// <summary>The named files (wildcards expanded), or standard input when no file is named. Null after printing why when there is nothing to read.</summary>
        private static List<(string Name, string Text)> ReadInputs(List<string> files)
        {
            var inputs = new List<(string Name, string Text)>();
            if (files.Count == 0)
            {
                if (!Console.IsInputRedirected) { _err.WriteLine("Give at least one file, or pipe text in."); return null; }
                using var r = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
                inputs.Add(("(stdin)", r.ReadToEnd()));
                return inputs;
            }
            foreach (var f in files)
                foreach (var path in Expand(f))
                {
                    try { inputs.Add((path, RegexGrep.Decode(File.ReadAllBytes(path)).Text)); }
                    catch (Exception ex) { _err.WriteLine(path + ": " + ex.Message); }
                }
            return inputs;
        }

        /// <summary>
        /// Several patterns in one command. Matches are listed as file:line:col: [n] text, where n is the pattern's number in the order given;
        /// with --replace each pattern is applied in turn to the result of the one before. Every pattern is checked before anything runs.
        /// </summary>
        private static int RunMany(List<string> patterns, RegexOptions options, string replace, List<string> files, bool json)
        {
            var rxs = new List<Regex>();
            for (int k = 0; k < patterns.Count; k++)
            {
                var rx = RegexKit.TryCreate(patterns[k], options, out var error);
                if (rx == null) { _err.WriteLine("Invalid pattern " + (k + 1) + " (" + patterns[k] + "): " + error); return 2; }
                rxs.Add(rx);
            }
            var inputs = ReadInputs(files);
            if (inputs == null) return 2;
            bool any = false;
            var jsonRows = new List<object>();
            try
            {
                foreach (var (name, text) in inputs)
                {
                    if (replace != null)
                    {
                        var result = text;
                        var counts = new List<int>();
                        foreach (var rx in rxs)
                        {
                            int count = 0;
                            result = rx.Replace(result, m => { count++; return m.Result(replace); });
                            counts.Add(count);
                        }
                        if (counts.Any(c => c > 0)) any = true;
                        if (json) jsonRows.Add(new { file = name, replacements = counts.Sum(), perPattern = counts, text = result });
                        else
                        {
                            if (inputs.Count > 1) _out.WriteLine("==> " + name + " <==");
                            _out.Write(result);
                            if (inputs.Count > 1 && !result.EndsWith("\n")) _out.WriteLine();
                        }
                        continue;
                    }
                    var starts = new List<int> { 0 };
                    for (int k = 0; k < text.Length; k++) if (text[k] == '\n') starts.Add(k + 1);
                    var found = new List<(int Index, int Length, int Pattern, string Value)>();
                    for (int k = 0; k < rxs.Count; k++)
                        foreach (Match m in rxs[k].Matches(text)) found.Add((m.Index, m.Length, k + 1, m.Value));
                    foreach (var h in found.OrderBy(x => x.Index).ThenBy(x => x.Pattern))
                    {
                        any = true;
                        int line = starts.BinarySearch(h.Index);
                        if (line < 0) line = ~line - 1;
                        int col = h.Index - starts[line] + 1;
                        if (json) jsonRows.Add(new { file = name, pattern = h.Pattern, line = line + 1, column = col, length = h.Length, value = h.Value });
                        else _out.WriteLine($"{name}:{line + 1}:{col}: [{h.Pattern}] {h.Value.Replace("\r", "").Replace("\n", "\\n")}");
                    }
                }
            }
            catch (RegexMatchTimeoutException) { _err.WriteLine("Stopped at the 200 ms limit: a pattern backtracks too much on this input."); return 2; }
            catch (RegexParseException ex) { _err.WriteLine("Invalid replacement: " + ex.Message); return 2; }
            catch (ArgumentException ex) { _err.WriteLine("Invalid replacement: " + ex.Message); return 2; }
            if (json) _out.WriteLine(JsonSerializer.Serialize(jsonRows, new JsonSerializerOptions { WriteIndented = true }));
            return any ? 0 : 1;
        }

        /// <summary>
        /// Replaces in the named files. Each changed file is first copied to a timestamped backup (the same folders the Files view restores from),
        /// a file that cannot be backed up is left alone, and the command reports what it changed. Exit code 0 when anything was replaced.
        /// </summary>
        private static int ReplaceInPlace(Regex rx, string replace, List<string> files, bool json)
        {
            var plan = new List<RegexGrep.ReplacePlanItem>();
            foreach (var f in files)
                foreach (var path in Expand(f))
                {
                    try
                    {
                        var info = new FileInfo(path);
                        if (!info.Exists) { _err.WriteLine(path + ": the file does not exist."); continue; }
                        plan.Add(new RegexGrep.ReplacePlanItem { Path = info.FullName, Count = 0, Size = info.Length, Modified = info.LastWriteTimeUtc });
                    }
                    catch (Exception ex) { _err.WriteLine(path + ": " + ex.Message); }
                }
            if (plan.Count == 0) return 1;
            RegexGrep.ReplaceOutcome outcome;
            try { outcome = RegexGrep.Apply(rx, replace, plan, default); }
            catch (RegexParseException ex) { _err.WriteLine("Invalid replacement: " + ex.Message); return 2; }
            catch (ArgumentException ex) { _err.WriteLine("Invalid replacement: " + ex.Message); return 2; }
            foreach (var f in outcome.Failed) _err.WriteLine(f.Path + ": " + f.Reason);
            if (json) _out.WriteLine(JsonSerializer.Serialize(new { filesChanged = outcome.FilesChanged, replacements = outcome.Replacements, backup = outcome.BackupFolder, failed = outcome.Failed.Count }, new JsonSerializerOptions { WriteIndented = true }));
            else _out.WriteLine($"{outcome.Replacements} replacement(s) in {outcome.FilesChanged} file(s)." + (outcome.BackupFolder != null ? " Backup: " + outcome.BackupFolder : ""));
            if (outcome.Failed.Count > 0 && outcome.FilesChanged == 0) return 2;
            return outcome.FilesChanged > 0 ? 0 : 1;
        }

        private static IEnumerable<string> Expand(string spec)
        {
            if (spec.IndexOfAny(new[] { '*', '?' }) < 0) { yield return spec; yield break; }
            var dir = Path.GetDirectoryName(spec);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            string[] found = Array.Empty<string>();
            try { found = Directory.GetFiles(dir, Path.GetFileName(spec)); } catch { }
            foreach (var f in found.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) yield return f;
        }

        private static int Usage(string message)
        {
            _err.WriteLine(message);
            _err.WriteLine("Run MonitoringTools.exe --regex --help for usage.");
            return 2;
        }

        private const string HelpText =
            "MonitoringTools.exe --regex \"<pattern>\" [--options i,m,s,x,n] [--replace \"<text>\"] [--json] <file>...\r\n" +
            "\r\n" +
            "  Prints each match as file:line:col: text, or with --replace prints the replaced text. Files are not changed unless --in-place is given.\r\n" +
            "  --in-place   with --replace, write the replacement into the files after copying each to a timestamped backup\r\n" +
            "  --lib NAME   use a saved pattern from the builder library (its options and, with --in-place, its replacement); --param name=value fills its {{name}} parameters\r\n" +
            "  --list-library  list the saved pattern names\r\n" +
            "  --pattern P  add a pattern; repeat it to run several patterns in one command (every other argument is then a file).\r\n" +
            "               Matches print as file:line:col: [n] text, n being the pattern's number; --replace applies them in order\r\n" +
            "  --patterns-file F  read patterns from F, one per line; empty lines and lines starting with # are skipped\r\n" +
            "  --automation  read one JSON request from standard input (or --request FILE) and print one JSON result:\r\n" +
            "               {\"operation\":\"match|replace|split|validate\",\"patterns\":[{\"pattern\":\"\\\\d+\",\"options\":\"i\"}],\"text\":\"...\",\"files\":[\"...\"]}\r\n" +
            "  --options  i ignore case, m multiline, s singleline, x ignore pattern whitespace, n explicit capture\r\n" +
            "  --json     machine-readable output\r\n" +
            "  Wildcards in a file name are expanded. With no file, text is read from standard input.\r\n" +
            "  Exit code: 0 something matched, 1 nothing matched, 2 invalid pattern or usage.\r\n" +
            "  Every match is limited to 200 ms, the same limit as the builder.";
    }
}
