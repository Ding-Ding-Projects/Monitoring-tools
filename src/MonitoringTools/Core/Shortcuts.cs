using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public enum ScriptShell { Batch, PowerShell }

    /// <summary>A user-defined ":name" command backed by a batch or PowerShell script.</summary>
    public sealed class Shortcut
    {
        public string Name { get; set; } = "";
        public ScriptShell Shell { get; set; } = ScriptShell.PowerShell;
        public bool ShowWindow { get; set; } = false;
        public bool RunElevated { get; set; } = false;
        public string Description { get; set; } = "";

        public string ScriptPath => Path.Combine(Shortcuts.Folder, Name + (Shell == ScriptShell.Batch ? ".bat" : ".ps1"));
    }

    public sealed class ShortcutResult
    {
        public int ExitCode;
        public string Output;
        public TimeSpan Took;
    }

    public static class Shortcuts
    {
        public static string Folder
        {
            get
            {
                var d = Path.Combine(Settings.DataDir, "scripts");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static readonly string[] Reserved = { "push" };

        public static Shortcut Find(string name) =>
            Settings.Current.Shortcuts.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        public static bool IsValidName(string name) =>
            !string.IsNullOrWhiteSpace(name) &&
            System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_-]{1,31}$");

        public static string Template(ScriptShell shell) => shell == ScriptShell.Batch
            ? "@echo off\r\nrem %~1 is whatever was before the colon, for example a file path.\r\nrem The same value is in %MT_INPUT%.\r\necho Running on: %~1\r\n"
            : "# $Target is whatever was before the colon, for example a file path.\r\n# The same value is in $env:MT_INPUT.\r\nparam([string]$Target = $env:MT_INPUT)\r\nWrite-Output \"Running on: $Target\"\r\n";

        public static string ReadScript(Shortcut s) => File.Exists(s.ScriptPath) ? File.ReadAllText(s.ScriptPath) : Template(s.Shell);

        public static void WriteScript(Shortcut s, string body)
        {
            // Batch files read best as the console code page; UTF-8 without BOM keeps ASCII scripts identical.
            File.WriteAllText(s.ScriptPath, body, s.Shell == ScriptShell.PowerShell ? new UTF8Encoding(true) : new UTF8Encoding(false));
            ScriptHistory.Save(ScriptHistory.ScriptKey(s.Name), body);   // every save keeps a versioned copy
        }

        public static Task<ShortcutResult> RunAsync(Shortcut s, string input)
        {
            return Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                if (!File.Exists(s.ScriptPath)) WriteScript(s, Template(s.Shell));

                var arg = (input ?? "").Replace("\"", "\\\"");
                ProcessStartInfo psi = s.Shell == ScriptShell.Batch
                    ? new ProcessStartInfo("cmd.exe", $"/c \"\"{s.ScriptPath}\" \"{arg}\"\"")
                    : new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{s.ScriptPath}\" \"{arg}\"");

                psi.WorkingDirectory = WorkingDirFor(input);

                if (s.RunElevated && !ServiceHost.IsElevated)
                {
                    // Environment variables do not pass through the UAC hop; the argument still does.
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    psi.WindowStyle = s.ShowWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;
                    try
                    {
                        var pe = Process.Start(psi);
                        pe.WaitForExit();
                        return new ShortcutResult { ExitCode = pe.ExitCode, Output = "(ran elevated in its own window)", Took = sw.Elapsed };
                    }
                    catch (Exception ex) { return new ShortcutResult { ExitCode = -1, Output = ex.Message, Took = sw.Elapsed }; }
                }

                psi.UseShellExecute = false;
                psi.EnvironmentVariables["MT_INPUT"] = input ?? "";
                psi.CreateNoWindow = !s.ShowWindow;
                psi.RedirectStandardOutput = !s.ShowWindow;
                psi.RedirectStandardError = !s.ShowWindow;
                var output = new StringBuilder();
                try
                {
                    using var p = new Process { StartInfo = psi };
                    if (!s.ShowWindow)
                    {
                        p.OutputDataReceived += (o, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                        p.ErrorDataReceived += (o, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    }
                    p.Start();
                    if (!s.ShowWindow) { p.BeginOutputReadLine(); p.BeginErrorReadLine(); }
                    p.WaitForExit();
                    return new ShortcutResult { ExitCode = p.ExitCode, Output = output.ToString().TrimEnd(), Took = sw.Elapsed };
                }
                catch (Exception ex) { return new ShortcutResult { ExitCode = -1, Output = ex.Message, Took = sw.Elapsed }; }
            });
        }

        private static string WorkingDirFor(string input)
        {
            try
            {
                if (!string.IsNullOrEmpty(input))
                {
                    if (Directory.Exists(input)) return input;
                    if (File.Exists(input)) return Path.GetDirectoryName(input);
                }
            }
            catch { }
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
    }
}
