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
    public sealed record ComposeCommandResult(int ExitCode, string Output, bool Truncated = false);
    public interface IComposeProcess
    {
        Task<ComposeCommandResult> RunAsync(string directory, IReadOnlyList<string> arguments, Action<string> output, CancellationToken cancellation);
    }

    /// <summary>Owns only the CLI child, never containers. Output and retained diagnostics are bounded.</summary>
    public sealed class ComposeProcess : IComposeProcess
    {
        public async Task<ComposeCommandResult> RunAsync(string directory, IReadOnlyList<string> arguments, Action<string> output, CancellationToken cancellation)
        {
            var start = new ProcessStartInfo("docker") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in arguments) start.ArgumentList.Add(arg);
            using var process = new Process { StartInfo = start };
            process.Start();
            using var registration = cancellation.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
            var retained = new StringBuilder(); bool truncated = false;
            async Task Read(StreamReader reader, bool stdout)
            {
                var buffer = new char[1024]; var line = new StringBuilder(); bool oversized = false;
                async Task Publish()
                {
                    var safe = ComposeDiagnostics.Redact(line.ToString()); line.Clear();
                    if (stdout) lock (retained) { if (retained.Length + safe.Length < 262144) retained.AppendLine(safe); else truncated = true; }
                    output?.Invoke(safe);
                    await Task.CompletedTask;
                }
                int count;
                while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation)) > 0)
                    for (int i = 0; i < count; i++)
                    {
                        if (buffer[i] is '\n' or '\r') { if (oversized) { line.Clear(); line.Append("[oversized diagnostic omitted]"); oversized = false; } if (line.Length > 0) await Publish(); }
                        else if (oversized) continue;
                        else if (line.Length < 8192) line.Append(buffer[i]);
                        // Suppress oversized records entirely: splitting could expose a credential continuation.
                        else { line.Clear(); oversized = true; }
                    }
                if (oversized) line.Append("[oversized diagnostic omitted]");
                if (line.Length > 0) await Publish();
            }
            var reads = Task.WhenAll(Read(process.StandardOutput, true), Read(process.StandardError, false));
            try { await Task.WhenAll(process.WaitForExitAsync(cancellation), reads); }
            finally { if (!process.HasExited) { try { process.Kill(true); } catch (InvalidOperationException) { } } }
            cancellation.ThrowIfCancellationRequested();
            return new ComposeCommandResult(process.ExitCode, retained.ToString(), truncated);
        }
    }

    public static class ComposeDiagnostics
    {
        public static string Redact(string text)
        {
            text = Regex.Replace(text ?? "", @"\x1B\[[0-?]*[ -/]*[@-~]", "");
            if (Regex.IsMatch(text, @"(?i)password|passwd|secret|token|authorization|api[_-]?key|private[_ -]?key|credential|://[^\s/]+:[^\s/]+@"))
                return "[sensitive diagnostic omitted]";
            return Regex.Replace(text, @"(https?://[^\s?]+)\?[^\s]+", "$1?[query omitted]", RegexOptions.IgnoreCase);
        }
    }

    public sealed record ComposeServiceState(string Service, string Name, string State, string Health, int? ExitCode, string Ports)
    {
        public string Verdict => State == "running" ? (Health == "healthy" ? "healthy" : Health == "unhealthy" ? "unhealthy" : Health == "starting" ? "waiting" : "running (no health check)")
            : State == "exited" && ExitCode == 0 ? "completed (exit 0; possible one-shot)" : State is "exited" or "dead" ? "failed" : "waiting";
        public bool Ready => State == "running" && (Health == "healthy" || string.IsNullOrEmpty(Health));
        public bool Failed => Health == "unhealthy" || State == "dead" || (State == "exited" && ExitCode != 0);
    }

    public sealed class ComposeController
    {
        private readonly IComposeProcess _process;
        public string FilePath { get; }
        public string DirectoryPath => Path.GetDirectoryName(FilePath);
        public string Stage { get; private set; } = "Ready";
        public string Result { get; private set; } = "Not started";
        public string[] ExpectedServices { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<ComposeServiceState> Services { get; private set; } = Array.Empty<ComposeServiceState>();
        public event Action Changed;
        public event Action<string> Output;
        public TimeSpan HealthTimeout { get; set; } = TimeSpan.FromSeconds(120);
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
        public ComposeController(string file, IComposeProcess process = null) { FilePath = Path.GetFullPath(file); _process = process ?? new ComposeProcess(); }
        public string Preview(string operation, string service = null) => "docker " + string.Join(" ", Arguments(operation, service).Select(a => "\"" + a.Replace("\"", "\\\"") + "\""));
        private string[] Arguments(string operation, string service)
        {
            var args = new List<string> { "compose", "--ansi", "never", "-f", FilePath };
            switch (operation)
            {
                case "build": args.Add("build"); break;
                case "pull": args.AddRange(new[] { "pull", "--ignore-buildable" }); break;
                case "start": args.AddRange(new[] { "up", "-d", "--no-build", "--pull", "never" }); break;
                case "inspect": args.AddRange(new[] { "ps", "--all", "--format", "json" }); break;
                case "config": args.AddRange(new[] { "config", "--services" }); break;
                case "logs": args.AddRange(new[] { "logs", "--no-color", "--tail", "100" }); break;
                case "restart": args.Add("restart"); break;
                default: throw new ArgumentException("Unsupported operation", nameof(operation));
            }
            if (service != null)
            {
                if (!ExpectedServices.Contains(service) || service.StartsWith("-")) throw new ArgumentException("Select a discovered service.");
                args.Add("--"); args.Add(service);
            }
            else if (operation == "restart" || operation == "logs") throw new ArgumentException("Select a discovered service.");
            return args.ToArray();
        }
        private async Task<ComposeCommandResult> Run(string stage, IReadOnlyList<string> args, CancellationToken cancellation, bool display = true)
        {
            Stage = stage; Changed?.Invoke();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(stage is "Build" or "Pull" ? TimeSpan.FromMinutes(20) : TimeSpan.FromSeconds(30));
            var result = await _process.RunAsync(DirectoryPath, args, display ? line => Output?.Invoke(line) : null, timeout.Token);
            if (result.ExitCode != 0) throw new InvalidOperationException(stage + " command exited " + result.ExitCode + ". See diagnostics; no automatic repair was applied.");
            if (result.Truncated && !display) throw new InvalidOperationException(stage + " response exceeded the safe limit; readiness is unverified.");
            return result;
        }
        public async Task ExecuteAsync(string operation, string service, CancellationToken cancellation)
        {
            try
            {
                Result = "In progress";
                if (operation == "startup")
                {
                    await Run("CLI", new[] { "--version" }, cancellation);
                    await Run("Compose", new[] { "compose", "version" }, cancellation);
                    await Run("Engine", new[] { "info", "--format", "{{.ServerVersion}}" }, cancellation);
                    var config = await Run("Configuration", Arguments("config", null), cancellation, false);
                    ExpectedServices = config.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
                    if (ExpectedServices.Length == 0) throw new InvalidOperationException("No active services were discovered.");
                    await Run("Build", Arguments("build", null), cancellation);
                    await Run("Pull", Arguments("pull", null), cancellation);
                    await Run("Start", Arguments("start", null), cancellation);
                }
                else if (operation == "logs") { await Run("Logs", Arguments("logs", service), cancellation); Result = "Logs retrieved; readiness unchanged"; return; }
                else if (operation == "restart") await Run("Restart", Arguments("restart", service), cancellation);
                else if (operation != "recheck") throw new ArgumentException("Unsupported operation");
                if (ExpectedServices.Length == 0) throw new InvalidOperationException("Run startup preflight before checking services.");
                var watch = Stopwatch.StartNew();
                do
                {
                    cancellation.ThrowIfCancellationRequested();
                    var response = await Run("Health observation", Arguments("inspect", null), cancellation, false);
                    Services = ParseServices(response.Output); Changed?.Invoke();
                    bool complete = ExpectedServices.All(s => Services.Any(c => c.Service == s));
                    if (Services.Any(s => s.Failed)) { Result = "Service failure observed; inspect the selected service logs"; return; }
                    if (complete && Services.All(s => s.Ready)) { Result = "Services running; health checks passed where configured. URLs are unverified."; return; }
                    if (complete && Services.All(s => s.Ready || (s.State == "exited" && s.ExitCode == 0)))
                    { Result = "Exited service observed with code 0. Confirm intended one-shot behavior; stack readiness is unverified."; return; }
                    await Task.Delay(PollInterval, cancellation);
                } while (watch.Elapsed < HealthTimeout);
                Result = "Observation timed out; services may still be starting. Inspect logs or recheck.";
            }
            catch (OperationCanceledException) { Result = cancellation.IsCancellationRequested ? "Cancelled. Owned CLI/observation stopped; containers may remain running. No rollback was performed." : "Command timed out. Containers may remain running; recheck before retrying."; }
            catch (Exception ex) { Result = ComposeDiagnostics.Redact(ex.Message); }
            finally { Stage = "Stopped"; Changed?.Invoke(); }
        }
        public static IReadOnlyList<ComposeServiceState> ParseServices(string json)
        {
            var values = new List<ComposeServiceState>();
            if (string.IsNullOrWhiteSpace(json)) return values;
            void Add(JsonElement item)
            {
                string Get(string key) => item.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";
                int? code = int.TryParse(Get("ExitCode"), out int n) ? n : null;
                var ports = new List<string>();
                if (item.TryGetProperty("Publishers", out var publishers) && publishers.ValueKind == JsonValueKind.Array)
                    foreach (var p in publishers.EnumerateArray())
                        ports.Add($"{p.GetProperty("PublishedPort")} → {p.GetProperty("TargetPort")}/{p.GetProperty("Protocol")} (URL unverified)");
                values.Add(new ComposeServiceState(Get("Service"), Get("Name"), Get("State").ToLowerInvariant(), Get("Health").ToLowerInvariant(), code, string.Join(", ", ports)));
            }
            if (json.TrimStart().StartsWith("[")) { using var doc = JsonDocument.Parse(json); foreach (var item in doc.RootElement.EnumerateArray()) Add(item); }
            else foreach (var line in json.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l))) { using var doc = JsonDocument.Parse(line); Add(doc.RootElement); }
            return values;
        }
    }
}
