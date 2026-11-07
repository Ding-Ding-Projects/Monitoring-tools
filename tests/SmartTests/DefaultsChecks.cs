using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MonitoringTools.Clip.Smart;

namespace SmartTests
{
    public static partial class Program
    {
        private static string Repo => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        // id -> (sample that must match, action, expected text in the answer)
        private static readonly Dictionary<string, (string Sample, string Action, string Expect)> DefaultSamples = new Dictionary<string, (string, string, string)>
        {
            ["case-styles"] = ("userAccountName", "styles", "user_account_name"),
            ["html-tools"] = ("Fish &amp; chips &#x2192; done", "decode", "Fish & chips"),
            ["cidr-calculator"] = ("203.0.113.77/24", "range", "203.0.113.255"),
            ["coordinates"] = ("43.6532, -79.3832", "formats", "79°22"),
            ["roman-numerals"] = ("MMXXVI", "number", "2026"),
            ["iso-durations"] = ("PT1H30M", "read", "5400"),
            ["user-agents"] = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.2849.68", "read", "Microsoft Edge"),
            ["unix-permissions"] = ("-rwxr-x---", "explain", "750"),
            ["mac-addresses"] = ("00:1A:2B:3C:4D:5E", "formats", "001a.2b3c.4d5e"),
            ["hex-text"] = ("48 65 6C 6C 6F 21", "decode", "Hello!"),
            ["morse-code"] = ("... --- ... / .... ..", "decode", "SOS HI"),
        };

        private static readonly string[] Ordinary = { "hello", "Thanks for the review, merging now.", "https://example.com/a", "5 kg", "2026-11-06", "+1 416 555 0134", "{\"a\":1}", "C:\\Users\\Public", "OK", "The meeting is at 3." };

        private static bool HavePowerShell()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh", "-NoProfile -Command exit") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
                p.WaitForExit(30000);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        private static void Defaults()
        {
            var dir = Path.Combine(Repo, "src", "MonitoringTools", "Extensions", "Defaults");
            var folders = Directory.GetDirectories(dir);
            Check("there are built-in extensions", folders.Length >= 10, folders.Length.ToString());
            Check("every built-in has a sample check", folders.All(f => DefaultSamples.ContainsKey(Path.GetFileName(f))));
            var ps = HavePowerShell();
            if (!ps) Console.WriteLine("note: PowerShell not found, so built-in extension runs were skipped");
            foreach (var f in folders)
            {
                var id = Path.GetFileName(f);
                var m = ExtensionManifest.Parse(File.ReadAllText(Path.Combine(f, "extension.json")), f, out var err);
                Check("built-in " + id + " parses", m != null && m.Id == id && File.Exists(m.RunPath) && m.Run.EndsWith(".ps1"), err ?? "");
                if (m == null || !DefaultSamples.TryGetValue(id, out var s)) continue;
                var facts = m.Match(s.Sample).ToList();
                Check("built-in " + id + " notices its sample", facts.Count > 0);
                foreach (var o in Ordinary) Check("built-in " + id + " ignores \"" + o + "\"", !m.Match(o).Any());
                Check("built-in " + id + " script is plain ASCII", File.ReadAllBytes(m.RunPath).All(c => c < 128), "Windows PowerShell reads scripts without a byte order mark as ANSI");
                Check("built-in " + id + " asks only for what it uses", !m.Permissions.Contains(ExtensionPermission.ReadAllText) && !m.Permissions.Contains(ExtensionPermission.Internet));
                if (!ps || facts.Count == 0) continue;
                var kind = facts[0].Get("kind");
                var r = ExtensionHost.RunProgramAsync(m, s.Action, kind, s.Sample).GetAwaiter().GetResult();
                var all = (r.Copy ?? "") + "|" + (r.Open ?? "") + "|" + (r.Toast ?? "") + "|" + string.Join("|", r.Values.Select(v => v.Text));
                Check("built-in " + id + " runs", r.Ok && all.Contains(s.Expect), (r.Error ?? all) + " · " + r.Ms + " ms");
            }

            if (ps)
            {
                var coords = ExtensionManifest.Parse(File.ReadAllText(Path.Combine(dir, "coordinates", "extension.json")), Path.Combine(dir, "coordinates"), out _);
                var dms = ExtensionHost.RunProgramAsync(coords, "formats", "coords", "43\u00B039\u203211.5\u2033N 79\u00B022\u203259.5\u2033W").GetAwaiter().GetResult();
                Check("coordinates reads degrees, minutes, seconds", dms.Ok && dms.Values.Any(v => v.Text.StartsWith("43.65")), dms.Error ?? string.Join("|", dms.Values.Select(v => v.Text)));
            }

            // keeping built-ins current without touching your edits
            var root = Path.Combine(Path.GetTempPath(), "mt-defaults-" + Guid.NewGuid().ToString("N"));
            BundledExtension Bundle(string body) => new BundledExtension { Id = "demo", Files = { ["extension.json"] = System.Text.Encoding.UTF8.GetBytes(Manifest.Replace("ticket-keys", "demo").Replace("run.sh", "demo.ps1").Replace("1.0.0", body)), ["demo.ps1"] = System.Text.Encoding.UTF8.GetBytes("# " + body + "\n") } };
            var host = new ExtensionHost(root);
            var changed = host.SyncDefaults(new[] { Bundle("1.0.0") });
            Check("built-in installed", changed.SequenceEqual(new[] { "demo" }) && File.Exists(Path.Combine(root, "demo", "demo.ps1")));
            Check("built-in starts on", host.Find("demo").Enabled && host.Find("demo").BuiltIn);
            Check("same version does nothing", host.SyncDefaults(new[] { Bundle("1.0.0") }).Count == 0);
            File.WriteAllText(Path.Combine(root, "demo", "demo.ps1"), "# 1.0.0\r\n");
            Check("line endings are not an edit", host.SyncDefaults(new[] { Bundle("1.1.0") }).SequenceEqual(new[] { "demo" }) && File.ReadAllText(Path.Combine(root, "demo", "demo.ps1")) == "# 1.1.0\n");
            host.TurnOff("demo");
            host.SyncDefaults(new[] { Bundle("1.2.0") });
            Check("update keeps it off when you turned it off", !host.Find("demo").Enabled && !host.Find("demo").NeedsApproval);
            File.WriteAllText(Path.Combine(root, "demo", "demo.ps1"), "# mine\n");
            Check("your edits are never overwritten", host.SyncDefaults(new[] { Bundle("1.3.0") }).Count == 0 && File.ReadAllText(Path.Combine(root, "demo", "demo.ps1")) == "# mine\n");
            host.Load();
            Check("an edited built-in is no longer marked built in", !host.Find("demo").BuiltIn);
            Directory.Delete(Path.Combine(root, "demo"), true);
            Check("a deleted built-in is not put back", host.SyncDefaults(new[] { Bundle("1.4.0") }).Count == 0 && !Directory.Exists(Path.Combine(root, "demo")));
            Check("and stays deleted", host.SyncDefaults(new[] { Bundle("1.5.0") }).Count == 0);
            Directory.Delete(root, true);
        }
    }
}
