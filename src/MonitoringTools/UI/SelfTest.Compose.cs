using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Core;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        private static async Task ComposeChecksAsync(string dir)
        {
            await CheckAsync("Compose dialog: real controls with deterministic CLI fixture, not Docker runtime proof", async () =>
            {
                var process = new ComposeFixture();
                var dialog = new ComposeDialog(Path.Combine(dir, "compose-fixture.yaml"), process);
                dialog.Show();
                try
                {
                    await dialog.RunForTest();
                    if (!dialog.Outcome.StartsWith("Services running")) return dialog.Outcome;
                    // A real displayed dialog is available to the external native capture route.
                    // The explicit receipt prevents fixture pixels being mistaken for live Docker evidence.
                    File.WriteAllText(Path.Combine(dir, "compose-dialog-ready.json"), System.Text.Json.JsonSerializer.Serialize(new {
                        kind = "built-dialog-with-deterministic-process-fixture", dockerRuntimeVerified = false,
                        source = typeof(ComposeDialog).Assembly.GetName().Version?.ToString(), title = dialog.Title,
                        atUtc = DateTime.UtcNow, outcome = dialog.Outcome }));
                    if (int.TryParse(Environment.GetEnvironmentVariable("MT_COMPOSE_CAPTURE_WAIT_SECONDS"), out var seconds))
                        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 120)));
                    process.Wait = true;
                    var running = dialog.RunForTest();
                    await Task.Delay(50); dialog.CancelForTest(); await running;
                    return dialog.Outcome.StartsWith("Cancelled") ? null : "Cancellation was not reported";
                }
                finally { dialog.Close(); }
            });
            Check("Compose diagnostic redaction: credential lines and URL queries never displayed", () =>
                ComposeDiagnostics.Redact("Authorization: bearer example-value") == "[sensitive diagnostic omitted]" &&
                !ComposeDiagnostics.Redact("https://example.test/?key=example-value").Contains("example-value") ? null : "Redaction failed");
            Check("Compose exited one-shot never claims health", () =>
                !new ComposeServiceState("job", "job-1", "exited", "", 0, "").Ready ? null : "Exited service reported ready");
        }
        private sealed class ComposeFixture : IComposeProcess
        {
            internal bool Wait;
            public async Task<ComposeCommandResult> RunAsync(string directory, IReadOnlyList<string> args, Action<string> output, CancellationToken cancellation)
            {
                if (Wait) await Task.Delay(Timeout.Infinite, cancellation);
                output?.Invoke("Deterministic CLI fixture; no Docker command executed.");
                return new ComposeCommandResult(0, args.Contains("--services") ? "web\n" : args.Contains("ps")
                    ? "{\"Service\":\"web\",\"Name\":\"fixture-web-1\",\"State\":\"running\",\"Health\":\"healthy\",\"ExitCode\":0,\"Publishers\":[]}" : "fixture");
            }
        }
    }
}
