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
            var liveFile = Environment.GetEnvironmentVariable("MT_COMPOSE_LIVE_FILE");
            if (!string.IsNullOrWhiteSpace(liveFile))
            {
                await CheckAsync("Compose live: explicitly selected task fixture reaches observed service readiness", async () =>
                {
                    var selected = Path.GetFullPath(liveFile);
                    if (!File.Exists(selected)) return "Explicit live Compose file does not exist";
                    var live = new ComposeDialog(selected);
                    live.Show();
                    try
                    {
                        await live.RunForTest();
                        bool verified = live.Outcome.StartsWith("Services running;", StringComparison.Ordinal);
                        File.WriteAllText(Path.Combine(dir, "compose-live-ready.json"), System.Text.Json.JsonSerializer.Serialize(new {
                            kind = "built-dialog-with-real-docker-process", dockerRuntimeVerified = verified,
                            source = typeof(ComposeDialog).Assembly.GetName().Version?.ToString(), title = live.Title,
                            atUtc = DateTime.UtcNow, outcome = live.Outcome,
                            cleanup = "Caller owns fixture teardown; closing this dialog does not stop containers." }));
                        if (int.TryParse(Environment.GetEnvironmentVariable("MT_COMPOSE_CAPTURE_WAIT_SECONDS"), out var hold))
                            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(hold, 0, 90)));
                        return verified ? null : live.Outcome;
                    }
                    finally { live.Close(); }
                });
                return;
            }
            await CheckAsync("Compose dialog: Cantonese opening switches live to English and bilingual", async () =>
            {
                var saved = Settings.Current.LanguageMode;
                ComposeDialog dialog = null;
                try
                {
                    Settings.Current.LanguageMode = LanguageMode.Cantonese; Language.Refresh();
                    dialog = new ComposeDialog(Path.Combine(dir, "compose-language.yaml"), new ComposeFixture());
                    dialog.Show(); await Task.Delay(200); Language.Refresh(dialog);
                    if (!TextOf(dialog).Contains("重試啟動") || !dialog.Title.Contains("啟動")) return "Cantonese opening failed";
                    Settings.Current.LanguageMode = LanguageMode.English; Language.Refresh(); await Task.Delay(200);
                    var english = TextOf(dialog);
                    if (!english.Contains("Retry startup") || !english.Contains("Run previewed action") || dialog.Title != "Compose startup" || english.Contains("重試啟動")) return "English restoration failed";
                    Settings.Current.LanguageMode = LanguageMode.Bilingual; Language.Refresh(); await Task.Delay(200);
                    var bilingual = TextOf(dialog);
                    return bilingual.Contains("Retry startup") && bilingual.Contains("重試啟動") && dialog.Title.Contains("Compose startup") && dialog.Title.Contains("啟動") ? null : "Bilingual restoration failed";
                }
                finally { dialog?.Close(); Settings.Current.LanguageMode = saved; Language.Refresh(); }
            });
            await CheckAsync("Compose dialog: real controls with deterministic CLI fixture, not Docker runtime proof", async () =>
            {
                var process = new ComposeFixture();
                var dialog = new ComposeDialog(Path.Combine(dir, "compose-fixture.yaml"), process);
                dialog.Show();
                try
                {
                    await dialog.RunForTest();
                    if (!dialog.Outcome.StartsWith("Services running")) return dialog.Outcome;
                    if (dialog.SelectedServiceForTest != "web") return "Discovered service selection is blank";
                    if (!dialog.FooterOutsideScrollForTest) return "Primary actions remain inside scrolling content";
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
