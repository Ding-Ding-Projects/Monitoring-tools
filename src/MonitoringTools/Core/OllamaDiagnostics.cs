using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MonitoringTools.Minecraft;

namespace MonitoringTools.Core
{
    public enum CheckState { Ok, Warn, Fail, Info }

    public sealed class DiagnosticCheck
    {
        public CheckState State;
        public string Title, Detail;
    }

    /// <summary>Works out why Ollama is not answering, from local facts only. It never contacts the internet.</summary>
    public static class OllamaDiagnostics
    {
        public static string LogsFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ollama");

        public static string ServerLog() => Path.Combine(LogsFolder(), "server.log");

        /// <summary>The last lines of server.log, or null when there is no log. The file is opened shared so a running server is not disturbed.</summary>
        public static string TailServerLog(int lines = 30)
        {
            try
            {
                var p = ServerLog();
                if (!File.Exists(p)) return null;
                using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                long take = Math.Min(fs.Length, 64 * 1024);
                fs.Seek(-take, SeekOrigin.End);
                using var rd = new StreamReader(fs);
                return string.Join("\n", rd.ReadToEnd().Split('\n').Where(l => l.Trim().Length > 0).TakeLast(lines));
            }
            catch (Exception ex) { return "The log could not be read: " + ex.Message; }
        }

        /// <summary>The program that is listening on a port, from netstat and the process list; null when nothing is or it cannot be told.</summary>
        public static string PortOwner(int port)
        {
            var r = ServerManager.Run("netstat.exe", "-ano -p tcp", 8000);
            if (r.Code != 0) return null;
            foreach (var line in r.Out.Split('\n'))
            {
                var m = Regex.Match(line, @"^\s*TCP\s+\S+:(?<p>\d+)\s+\S+\s+LISTENING\s+(?<pid>\d+)");
                if (!m.Success || m.Groups["p"].Value != port.ToString()) continue;
                try { using var pr = Process.GetProcessById(int.Parse(m.Groups["pid"].Value)); return $"{pr.ProcessName} (process {pr.Id})"; }
                catch { return "process " + m.Groups["pid"].Value; }
            }
            return null;
        }

        /// <summary>Runs every check. Safe to call off the UI thread.</summary>
        public static async Task<List<DiagnosticCheck>> RunAsync()
        {
            var list = new List<DiagnosticCheck>();
            void Add(CheckState s, string t, string d) => list.Add(new DiagnosticCheck { State = s, Title = t, Detail = d });
            int port = Ollama.Port;

            var exe = Ollama.FindExe();
            if (exe != null) Add(CheckState.Ok, "Ollama is installed", exe);
            else Add(CheckState.Fail, "Ollama is not installed", "No ollama.exe was found in the usual folders or on PATH. Install it with winget or from the official download page.");

            var version = await Ollama.VersionAsync();
            var procs = Process.GetProcessesByName("ollama");
            int count = procs.Length; foreach (var p in procs) p.Dispose();
            if (version != null) Add(CheckState.Ok, "The server is answering", $"Version {version} on 127.0.0.1:{port}.");
            else if (count > 0) Add(CheckState.Warn, "An Ollama process exists but does not answer on this port", $"{count} ollama process(es) are running, yet nothing replied on 127.0.0.1:{port}. It may still be starting, or it may be using a different port (check OLLAMA_HOST below).");
            else Add(exe != null ? CheckState.Fail : CheckState.Info, "The server is not running", exe != null ? "Press Start server above. If it exits straight away, read the log below." : "Nothing to start until Ollama is installed.");

            var host = Environment.GetEnvironmentVariable("OLLAMA_HOST");
            if (!string.IsNullOrWhiteSpace(host))
            {
                bool loopbackOnly = host.StartsWith("127.", StringComparison.Ordinal) || host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || host.StartsWith("[::1]", StringComparison.Ordinal);
                Add(loopbackOnly ? CheckState.Info : CheckState.Warn, "OLLAMA_HOST is set to " + host,
                    loopbackOnly ? "Loopback only, which is what this tool expects. A different port here would not match the port set above."
                                 : "This makes Ollama listen on other network interfaces too, which exposes it to your network. This tool only talks to 127.0.0.1. If other computers must reach it, Windows Firewall needs an inbound rule for that port; on this computer alone no firewall rule is needed.");
            }

            if (version == null)
            {
                bool free = ServerManager.PortFree(port);
                if (free) Add(CheckState.Info, $"Port {port} is free", "Nothing is listening there, so a server can start. If a server keeps failing to start, read the log below.");
                else
                {
                    var owner = await Task.Run(() => PortOwner(port));
                    Add(CheckState.Fail, $"Port {port} is in use by another program", (owner != null ? "The listener is " + owner + ". " : "The owning program could not be identified. ") + "Close it, or choose a different port in the Port box above. Ollama's own default is 11434.");
                }
            }

            var models = OllamaCatalogue.ModelsFolder();
            if (Directory.Exists(models))
            {
                string disk = "";
                try { var d = new DriveInfo(Path.GetPathRoot(models)); disk = $" {d.AvailableFreeSpace / 1073741824.0:0.0} GB free on {d.Name}."; } catch { }
                Add(CheckState.Ok, "Models folder exists", models + "." + disk);
            }
            else Add(CheckState.Info, "Models folder does not exist yet", models + " is created when the first model is pulled.");

            var log = ServerLog();
            if (File.Exists(log)) Add(CheckState.Info, "Server log found", log + ". The last lines are shown below when you press Show log.");
            else Add(CheckState.Info, "No server log found", "Expected at " + log + ". The log appears after the first start.");

            Add(CheckState.Info, "Firewall note", "Requests from this tool go to 127.0.0.1, which Windows Firewall does not filter. A firewall is only relevant if you made Ollama listen on the network.");
            return list;
        }
    }
}
