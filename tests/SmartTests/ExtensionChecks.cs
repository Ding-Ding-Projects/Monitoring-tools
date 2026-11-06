using System;
using System.IO;
using System.Linq;
using MonitoringTools.Clip.Smart;

namespace SmartTests
{
    public static partial class Program
    {
        private const string Manifest = @"{
  // comments are fine
  ""id"": ""ticket-keys"", ""name"": ""Ticket keys"", ""version"": ""1.0.0"", ""api"": 1,
  ""detect"": [{ ""kind"": ""ticket"", ""label"": ""Ticket"", ""pattern"": ""^[A-Z][A-Z0-9]+-\\d+$"" }],
  ""actions"": [{ ""id"": ""open"", ""label"": ""Open ticket"", ""for"": [""ticket""], ""primary"": true },
                { ""id"": ""copy"", ""label"": ""Copy link"" }],
  ""run"": ""run.sh"",
  ""permissions"": [""read-text"", ""open-links"", ""change-clipboard""]
}";

        private static void Extensions()
        {
            var m = ExtensionManifest.Parse(Manifest, null, out var err);
            Check("manifest parses", m != null, err ?? "");
            Check("manifest matches", m.Match("ABC-1234").Single().Actions.Count == 2);
            Check("manifest ignores", !m.Match("hello").Any());
            Check("action value", m.Match("ABC-1234").Single().Actions[0].Value == "ext:ticket-keys:open:ticket");

            ExtensionManifest.Parse(Manifest.Replace(@"""api"": 1", @"""api"": 2"), null, out err);
            Check("wrong api refused", err != null && err.Contains("api"));
            ExtensionManifest.Parse(Manifest.Replace(@"^[A-Z][A-Z0-9]+-\\d+$", ".*"), null, out err);
            Check("match-everything refused", err != null && err.Contains("read-all-text"), err ?? "");
            ExtensionManifest.Parse(Manifest.Replace(@"""run.sh""", @"""../evil.sh"""), Path.GetTempPath(), out err);
            Check("run outside folder refused", err != null && err.Contains("inside"), err ?? "");
            ExtensionManifest.Parse(Manifest.Replace(@"""read-text"",", @"""read-text"", ""root"","), null, out err);
            Check("unknown permission refused", err != null && err.Contains("root"));

            var noLinks = ExtensionManifest.Parse(Manifest.Replace(@"""open-links"", ", ""), null, out _);
            var r1 = new ExtensionResult(); ExtensionHost.Accept(noLinks, "{\"open\":\"https://example.com\"}", r1);
            Check("open without permission dropped", r1.Open == null && r1.Error != null);
            var r2 = new ExtensionResult(); ExtensionHost.Accept(m, "{\"open\":\"file:///C:/x\"}", r2);
            Check("non-web link dropped", r2.Open == null && r2.Error != null);
            var r3 = new ExtensionResult(); ExtensionHost.Accept(m, "{\"copy\":\"x\",\"values\":[{\"label\":\"a\",\"text\":\"b\"}]}", r3);
            Check("copy and values kept", r3.Copy == "x" && r3.Values.Count == 1 && r3.Ok);

            if (OperatingSystem.IsWindows()) return;
            var root = Path.Combine(Path.GetTempPath(), "mt-ext-" + Guid.NewGuid().ToString("N"));
            var src = Path.Combine(root, "src");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "extension.json"), Manifest);
            File.WriteAllText(Path.Combine(src, "run.sh"), "#!/bin/sh\nread line\nkey=$(echo \"$line\" | sed 's/.*\"text\":\"\\([^\"]*\\)\".*/\\1/')\necho \"{\\\"open\\\":\\\"https://tracker.example/$key\\\"}\"\n");
            File.SetUnixFileMode(Path.Combine(src, "run.sh"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.WriteAllText(Path.Combine(src, "slow.sh"), "#!/bin/sh\nsleep 30\n");
            File.SetUnixFileMode(Path.Combine(src, "slow.sh"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var host = new ExtensionHost(Path.Combine(root, "installed"));
            host.Load();
            var inst = host.Install(src, false, out err);
            Check("install", inst != null && err == null, err ?? "");
            Check("new extension waits for approval", host.Find("ticket-keys").NeedsApproval && !host.Find("ticket-keys").Enabled);
            Check("off extension does not match", !host.Detect("ABC-1234").Any());
            host.Allow("ticket-keys");
            Check("allowed extension matches", host.Detect("ABC-1234").Count() == 1);
            var res = host.RunAsync("ext:ticket-keys:open:ticket", "ABC-1234").GetAwaiter().GetResult();
            Check("extension runs", res.Ok && res.Open == "https://tracker.example/ABC-1234", res.Error ?? res.Open ?? "");
            Check("run is logged", host.Find("ticket-keys").Log.Count == 1);

            var again = new ExtensionHost(host.Root); again.Load();
            Check("approval remembered", again.Find("ticket-keys").Enabled);

            File.WriteAllText(Path.Combine(host.Root, "ticket-keys", "extension.json"), Manifest.Replace(@"""read-text"",", @"""read-text"", ""internet"","));
            again.Load();
            Check("new permission asks again", again.Find("ticket-keys").NeedsApproval && !again.Find("ticket-keys").Enabled);

            host.Install(src, false, out err);
            Check("same id needs replace", err == "exists");

            var slow = ExtensionManifest.Parse(Manifest.Replace("run.sh", "slow.sh"), src, out _);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sr = ExtensionHost.RunProgramAsync(slow, "open", "ticket", "ABC-1").GetAwaiter().GetResult();
            Check("slow extension stopped", !sr.Ok && sw.Elapsed.TotalSeconds < 9, sr.Error ?? "");

            SmartDetect.Extensions = host.Detect;
            Check("detect includes extensions", SmartDetect.All("ABC-1234").Any(f => f.FromExtension == "ticket-keys"));
            SmartDetect.Extensions = null;
            Directory.Delete(root, true);
        }
    }
}
