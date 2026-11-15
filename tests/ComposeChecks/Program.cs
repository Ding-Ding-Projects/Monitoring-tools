using MonitoringTools.Core;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
var rows = ComposeController.ParseServices("[{\"Service\":\"web\",\"Name\":\"web-1\",\"State\":\"running\",\"Health\":\"healthy\",\"ExitCode\":0,\"Publishers\":[]}]");
Check(rows.Count == 1 && rows[0].Ready, "healthy service");
Check(!new ComposeServiceState("a", "a", "running", "starting", 0, "").Ready, "starting is not ready");
Check(new ComposeServiceState("a", "a", "running", "unhealthy", 0, "").Failed, "unhealthy is failure");
Check(!new ComposeServiceState("a", "a", "exited", "", 0, "").Ready, "one-shot is not running");
Check(ComposeDiagnostics.Redact("password=abc") == "[sensitive diagnostic omitted]", "redaction");
Check(!ComposeDiagnostics.Redact("https://x/?q=private").Contains("private"), "query redaction");
Check(ComposeDiagnostics.Redact("CUSTOM_VALUE=example-value") == "[environment assignment omitted]", "environment value redaction");
var fake = new Fake();
var controller = new ComposeController(Path.Combine(Path.GetTempPath(), "compose.yaml"), fake) { HealthTimeout = TimeSpan.FromMilliseconds(1), PollInterval = TimeSpan.FromMilliseconds(1) };
await controller.ExecuteAsync("startup", null, CancellationToken.None);
Check(controller.Result.StartsWith("Services running"), "startup healthy verdict");
Check(fake.Calls.Count == 8, "preflight/build/pull/start/inspect sequence");
Check(fake.Calls[3].Contains("--services") && !fake.Calls[3].Contains("--environment"), "no resolved configuration dump");
Check(fake.Calls[6].Contains("--no-build"), "distinct build stage");
try { controller.Preview("restart", "unknown"); throw new Exception("service validation absent"); } catch (ArgumentException) { }
fake.Fail = true;
await controller.ExecuteAsync("startup", null, CancellationToken.None);
Check(controller.Result.Contains("exited 17"), "nonzero command does not report success");
fake.Fail = false; fake.State = "[]";
await controller.ExecuteAsync("recheck", null, CancellationToken.None);
Check(controller.Result.Contains("timed out"), "missing service is not ready");
fake.State = "{\"Service\":\"web\",\"State\":\"exited\",\"ExitCode\":0}";
await controller.ExecuteAsync("recheck", null, CancellationToken.None);
Check(controller.Result.Contains("one-shot") && controller.Result.Contains("unverified"), "one-shot requires confirmation");
fake.State = "{invalid";
await controller.ExecuteAsync("recheck", null, CancellationToken.None);
Check(!controller.Result.StartsWith("Services running"), "invalid JSON fails closed");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
await controller.ExecuteAsync("recheck", null, cancelled.Token);
Check(controller.Result.StartsWith("Cancelled"), "cancelled observation");
Console.WriteLine("PASS: Compose controller checks (health, preflight order, redaction, scoped repair, nonzero exit, timeout, one-shot, invalid JSON and cancellation)");
sealed class Fake : IComposeProcess
{
    public List<string[]> Calls = new(); public bool Fail;
    public string State = "{\"Service\":\"web\",\"Name\":\"web-1\",\"State\":\"running\",\"Health\":\"healthy\",\"ExitCode\":0}";
    public Task<ComposeCommandResult> RunAsync(string directory, IReadOnlyList<string> args, Action<string> output, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Calls.Add(args.ToArray());
        return Task.FromResult(new ComposeCommandResult(Fail ? 17 : 0, args.Contains("--services") ? "web\n" : args.Contains("ps") ? State : "ok"));
    }
}
