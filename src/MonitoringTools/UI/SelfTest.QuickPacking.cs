using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Core;
using MonitoringTools.Core.Disks;
using MonitoringTools.Core.Packing;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        private static async Task QuickPackingChecksAsync(string dir)
        {
            var root = Path.Combine(dir, "quick-packing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "Sources");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(Path.Combine(source, "Empty"));
            var file = Path.Combine(source, "hello.txt");
            File.WriteAllText(file, "Packing keeps the original intact. 香港文字。\n");
            Check("packing removes duplicate and parent-child overlaps", () =>
            {
                var scan = PackingSources.Scan(new[] { file, source, file }, CancellationToken.None);
                return scan.Items.Length == 1 && scan.Entries.Count(e => !e.Folder) == 1 && scan.Entries.Any(e => e.Relative.EndsWith("Empty")) ? null : "duplicate or empty folder inventory mismatch";
            });
            Check("packing scan cancellation is observed", () =>
            {
                using var cts = new CancellationTokenSource(); cts.Cancel();
                try { PackingSources.Scan(new[] { source }, cts.Token); return "cancelled scan continued"; }
                catch (OperationCanceledException) { return null; }
            });
            await CheckAsync("packing publishes concurrent outputs without overwrite", async () =>
            {
                var first = Path.Combine(root, "one.tmp"); var second = Path.Combine(root, "two.tmp");
                File.WriteAllText(first, "one"); File.WriteAllText(second, "two");
                File.WriteAllText(Path.Combine(root, "collision.zip"), "existing");
                Directory.CreateDirectory(Path.Combine(root, "collision (1).zip"));
                var paths = await Task.WhenAll(Task.Run(() => PackingSources.Publish(first, root, "collision", ".zip")), Task.Run(() => PackingSources.Publish(second, root, "collision", ".zip")));
                return paths.Distinct().Count() == 2 && File.ReadAllText(Path.Combine(root, "collision.zip")) == "existing"
                    && paths.Select(File.ReadAllText).OrderBy(x => x).SequenceEqual(new[] { "one", "two" }) ? null : "collision replaced or lost an output";
            });
            Check("packing entry limit escapes ancestor read-error handling", () =>
            {
                try { PackingSources.Scan(new[] { source }, CancellationToken.None, entryLimit: 2); return "over-limit tree was accepted"; }
                catch (Exception ex) { return ex.Message.Contains("exceeds 250,000 entries") ? null : ex.Message; }
            });
            PackingJob finished = null;
            await CheckAsync("direct ZIP starts automatically and verifies real entries", async () =>
            {
                finished = PackingProgressDialog.Start(new[] { source, file }, PackingFormat.Zip);
                var window = PackingProgressDialog.For(finished);
                window.Hide();
                await finished.Completion;
                if (!finished.Success || finished.Partial) return finished.Error ?? string.Join("\n", finished.Warnings);
                PackingProgressDialog.Open(finished);
                if (PackingProgressDialog.For(finished) != window || !window.IsVisible) return "reopen did not retain the same job window";
                if (!Activity.Recent(20).Any(e => e.Reopen != null)) return "no reopen activity";
                using var zip = ZipFile.OpenRead(finished.Output);
                if (zip.Entries.Count(e => !e.FullName.EndsWith("/")) != 1 || !zip.Entries.Any(e => e.FullName.EndsWith("Empty/"))) return "ZIP contents incorrect";
                using var reader = new StreamReader(zip.Entries.Single(e => e.FullName.EndsWith("hello.txt")).Open());
                if (reader.ReadToEnd() != File.ReadAllText(file)) return "ZIP bytes differ";
                return Directory.GetDirectories(root, ".monitoring-pack-*").Length == 0 ? null : "temporary output remains";
            });
            await CheckAsync("missing inputs produce an explicit partial result", async () =>
            {
                var job = new PackingJob(new[] { file, Path.Combine(root, "missing.txt") }, PackingFormat.Zip);
                job.Start(); await job.Completion;
                return !job.Success && job.Output != null && job.Partial && job.Warnings.Any(w => w.Contains("missing.txt")) ? null : "missing selection was silently dropped";
            });
            await CheckAsync("locked input produces failure without deleting originals", async () =>
            {
                using var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
                var job = new PackingJob(new[] { file }, PackingFormat.Zip);
                job.Start(); await job.Completion;
                return !job.Success && job.Error != null && File.Exists(file) && Directory.GetDirectories(source, ".monitoring-pack-*").Length == 0 ? null : "locked-file failure was not preserved safely";
            });
            await CheckAsync("cancelled packing leaves originals and no published file", async () =>
            {
                var job = new PackingJob(new[] { source }, PackingFormat.Zip); job.Cancel(); job.Start(); await job.Completion;
                return job.Cancelled && job.Output == null && File.Exists(file) ? null : "cancel did not stop safely";
            });
            await CheckAsync("ZIP cancellation during compression removes only owned output", async () =>
            {
                var large = Path.Combine(source, "large.bin");
                File.WriteAllBytes(large, System.Security.Cryptography.RandomNumberGenerator.GetBytes(8 * 1024 * 1024));
                var job = new PackingJob(new[] { large }, PackingFormat.Zip);
                job.Changed += () => { if (job.Progress?.Done > 0 && !job.Complete) job.Cancel(); };
                job.Start(); await job.Completion;
                return job.Cancelled && job.Output == null && File.Exists(large)
                    && Directory.GetDirectories(source, ".monitoring-pack-*").Length == 0 ? null : "active cancellation left an output or changed a source";
            });
            await CheckAsync("VHDX declined consent remains incomplete", async () =>
            {
                VhdSession.TestDecline = true;
                try
                {
                    var job = new PackingJob(new[] { file }, PackingFormat.Vhdx); job.Start(); await job.Completion;
                    return !job.Success && job.Error != null && job.Output == null && File.Exists(file) ? null : "declined consent reported success";
                }
                finally { VhdSession.TestDecline = false; }
            });
            // Optional real-result inspection seam for an isolated desktop driver. No fabricated progress state.
            if (finished != null && int.TryParse(Environment.GetEnvironmentVariable("MT_PACK_HOLD_MS"), out var hold))
            {
                PackingProgressDialog.Open(finished);
                await Task.Delay(Math.Clamp(hold, 0, 120000));
            }
            if (finished != null) PackingProgressDialog.For(finished)?.Close();
        }
    }
}
