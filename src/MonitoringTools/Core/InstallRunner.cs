using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public sealed class InstallResult
    {
        public bool Ok;
        public bool Declined;       // the Windows consent prompt was declined or timed out
        public bool Cancelled;
        public int ExitCode;
        public string Message;
        public string Output = "";
        public Dictionary<string, string> Versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public TimeSpan Took;
        public string LogPath;
    }

    /// <summary>
    /// Runs a command that <see cref="InstallCommands"/> accepted. Chocolatey runs elevated through the Windows consent prompt,
    /// which only the person answers; winget runs as the signed-in user. No shell is involved: the program is started with
    /// the validated arguments, never a command line built from the copied text.
    /// </summary>
    public static class InstallRunner
    {
        public const int ConsentDeclined = 1223;   // ERROR_CANCELLED

        /// <summary>Self-test seam: replaces the real run so no software is ever installed by a check.</summary>
        internal static Func<InstallCommand, Action<string>, CancellationToken, Task<InstallResult>> RunnerForTest;
        /// <summary>Self-test seam: pretend the package managers are (or are not) installed. Null means look on the computer.</summary>
        internal static Func<InstallManager, string> PathForTest;

        public static string PathOf(InstallManager m)
        {
            if (PathForTest != null) return PathForTest(m);
            if (m == InstallManager.Chocolatey)
            {
                var home = Environment.GetEnvironmentVariable("ChocolateyInstall");
                if (!string.IsNullOrEmpty(home))
                {
                    var p = Path.Combine(home, "bin", "choco.exe");
                    if (File.Exists(p)) return p;
                }
                var pd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", "choco.exe");
                return File.Exists(pd) ? pd : Which("choco.exe");
            }
            var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
            return File.Exists(alias) ? alias : Which("winget.exe");
        }

        private static string Which(string exe)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try { var p = Path.Combine(dir.Trim(), exe); if (File.Exists(p)) return p; } catch { }
            }
            return null;
        }

        public static string LogFolder
        {
            get { var d = Path.Combine(Settings.DataDir, "install-logs"); Directory.CreateDirectory(d); return d; }
        }

        /// <param name="step">Called with "check", "consent" or "install" as each step begins.</param>
        public static async Task<InstallResult> RunAsync(InstallCommand c, Action<string> step, CancellationToken ct)
        {
            if (RunnerForTest != null) return await RunnerForTest(c, step, ct);
            var began = DateTime.Now;
            var r = new InstallResult();
            var log = new StringBuilder();
            log.AppendLine($"Command: {c.Display}");
            log.AppendLine($"Started: {began:yyyy-MM-dd HH:mm:ss}");
            try
            {
                step?.Invoke("check");
                var exe = PathOf(c.Manager);
                if (exe == null) { r.Message = c.Manager == InstallManager.Chocolatey ? "Chocolatey is not installed on this computer." : "winget is not installed on this computer."; r.ExitCode = -1; return Done(); }
                log.AppendLine($"Program: {exe}");
                if (c.Manager == InstallManager.Chocolatey) await RunChoco(exe, c, step, ct, r, log);
                else await RunWinget(exe, c, step, ct, r, log);
            }
            catch (OperationCanceledException) { r.Cancelled = true; r.Message = "Cancelled."; }
            catch (Exception ex) { r.Ok = false; r.ExitCode = -1; r.Message = ex.Message; log.AppendLine("Error: " + ex); }
            return Done();

            InstallResult Done()
            {
                r.Took = DateTime.Now - began;
                log.AppendLine($"Exit code: {r.ExitCode}");
                log.AppendLine($"Result: {(r.Ok ? "installed" : r.Declined ? "consent declined" : r.Cancelled ? "cancelled" : "failed")}");
                foreach (var kv in r.Versions) log.AppendLine($"Version: {kv.Key} {kv.Value}");
                if (!string.IsNullOrWhiteSpace(r.Output)) { log.AppendLine("---- output ----"); log.AppendLine(r.Output.TrimEnd()); }
                try { r.LogPath = Path.Combine(LogFolder, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + c.Exe + ".log"); File.WriteAllText(r.LogPath, log.ToString()); }
                catch (Exception ex) { Log.Warn("Install log not written: " + ex.Message); }
                return r;
            }
        }

        private static async Task RunChoco(string exe, InstallCommand c, Action<string> step, CancellationToken ct, InstallResult r, StringBuilder log)
        {
            var elevated = ServiceHost.IsElevated;
            ProcessStartInfo psi = elevated
                ? new ProcessStartInfo(exe, c.ArgumentString) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }
                // The consent prompt is Windows' own. The app never answers, hides or pre-approves it.
                : new ProcessStartInfo(exe, c.ArgumentString) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            if (!elevated) step?.Invoke("consent"); else step?.Invoke("install");
            Process p;
            try { p = Process.Start(psi); }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ConsentDeclined)
            {
                r.Declined = true; r.ExitCode = ConsentDeclined;
                r.Message = "The Windows consent prompt was declined or timed out. Nothing was installed.";
                return;
            }
            if (p == null) { r.ExitCode = -1; r.Message = "The install did not start."; return; }
            using (p)
            {
                if (!elevated) step?.Invoke("install");
                var outTask = elevated ? p.StandardOutput.ReadToEndAsync() : Task.FromResult("");
                var errTask = elevated ? p.StandardError.ReadToEndAsync() : Task.FromResult("");
                try { await p.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMinutes(40), ct); }
                catch (OperationCanceledException)
                {
                    // An elevated process cannot be stopped from here; say so instead of pretending it was.
                    bool killed = false;
                    try { p.Kill(true); killed = true; } catch { }
                    r.Cancelled = true;
                    r.Message = killed ? "Cancelled. The install was stopped." : "Stopped waiting. The elevated install may still finish on its own.";
                    return;
                }
                catch (TimeoutException) { r.ExitCode = -1; r.Message = "The install did not finish in 40 minutes. It may still be running."; return; }
                r.ExitCode = p.ExitCode;
                r.Output = elevated ? (await outTask + await errTask) : "Output is not captured for an elevated run; the package manager's own log is in its logs folder.";
            }
            // 1641 and 3010 mean the install worked and Windows wants a restart.
            r.Ok = r.ExitCode == 0 || r.ExitCode == 1641 || r.ExitCode == 3010;
            r.Message = r.Ok ? (r.ExitCode == 0 ? "Installed." : "Installed. Windows asks for a restart to finish.") : $"The install ended with exit code {r.ExitCode}.";
            if (r.Ok) await ReadChocoVersions(exe, c, r);
        }

        private static async Task ReadChocoVersions(string exe, InstallCommand c, InstallResult r)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, "list --local-only --limit-output") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                using var p = Process.Start(psi);
                var text = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                foreach (var line in text.Split('\n'))
                {
                    var parts = line.Trim().Split('|');
                    if (parts.Length == 2 && c.Packages.Any(n => n.Equals(parts[0], StringComparison.OrdinalIgnoreCase))) r.Versions[parts[0]] = parts[1];
                }
            }
            catch (Exception ex) { Log.Warn("Installed versions not read: " + ex.Message); }
        }

        private static async Task RunWinget(string exe, InstallCommand c, Action<string> step, CancellationToken ct, InstallResult r, StringBuilder log)
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            foreach (var a in c.Arguments()) psi.ArgumentList.Add(a);
            step?.Invoke("install");
            using var p = Process.Start(psi);
            if (p == null) { r.ExitCode = -1; r.Message = "The install did not start."; return; }
            p.StandardInput.Close();   // nothing is typed into winget; a question it asks ends the run instead of hanging
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            try { await p.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMinutes(40), ct); }
            catch (OperationCanceledException) { try { p.Kill(true); } catch { } r.Cancelled = true; r.Message = "Cancelled. The install was stopped."; return; }
            catch (TimeoutException) { try { p.Kill(true); } catch { } r.ExitCode = -1; r.Message = "The install did not finish in 40 minutes and was stopped."; return; }
            r.ExitCode = p.ExitCode;
            r.Output = Regex.Replace(await outTask + await errTask, @"[\u0008\r]+[-\\|/]?\s*", "\n").Trim();
            r.Ok = r.ExitCode == 0;
            r.Message = r.Ok ? "Installed." : $"winget ended with exit code {r.ExitCode}.";
            var v = Regex.Match(r.Output, @"Found .*? Version (?<v>\S+)");
            if (r.Ok && v.Success) r.Versions[c.Packages[0]] = v.Groups["v"].Value;
        }
    }
}
