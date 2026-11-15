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
            Check("clipboard vhd consumes an exact in-memory selection once", () =>
            {
                var old = Settings.Current.QuickCommandsEnabled;
                int starts = 0, notices = 0;
                string[] received = null;
                void Start(string[] paths) { starts++; received = paths; }
                void Notice(string text) { notices++; }
                var app = App.Current;
                bool Handle(Clip.ClipItem item) => app.HandlePackingClipboard(item, Start, Notice);
                Clip.ClipItem Command(string text) => new() { Kind = Clip.ClipKind.Text, Text = text };
                Clip.ClipItem Selection(params string[] paths) => new() { Kind = Clip.ClipKind.Files, Files = paths, IsPathText = true };
                try
                {
                    Settings.Current.QuickCommandsEnabled = true;
                    var copy = new[] { file, Path.Combine(source, "Empty") };
                    Handle(Selection(copy)); copy[0] = "changed-after-event";
                    if (!Handle(Command("  VhD \r\n")) || starts != 1 || received[0] != file || received.Length != 2) return "exact command or immutable snapshot mismatch";
                    Handle(Command("vhd"));
                    if (starts != 1 || notices != 1) return "selection was consumed more than once";
                    Handle(Selection(file));
                    if (Handle(Command("make vhd"))) return "phrase incorrectly accepted";
                    Handle(Command("vhd"));
                    if (starts != 1) return "unsupported intervening copy retained stale selection";
                    Handle(Selection(file, Path.Combine(root, "missing-input")));
                    Handle(Command("vhd"));
                    if (starts != 1 || notices != 3) return "missing selection was silently reduced";
                    Settings.Current.QuickCommandsEnabled = false;
                    Handle(Selection(file));
                    if (Handle(Command("vhd")) || starts != 1) return "disabled quick commands launched packing";
                    Settings.Current.QuickCommandsEnabled = true;
                    Handle(Command("vhd"));
                    if (starts != 1 || notices != 4) return "disabled snapshot was retained";
                    Handle(Selection(file)); Handle(new Clip.ClipItem { Kind = Clip.ClipKind.Image }); Handle(Command("vhd"));
                    return starts == 1 && notices == 5 ? null : "unsupported selection reused stale files";
                }
                finally { Settings.Current.QuickCommandsEnabled = true; Handle(Command("clear test selection")); Settings.Current.QuickCommandsEnabled = old; }
            });
            await CheckAsync("cancelled live packing view remains visible", async () =>
            {
                var job = new PackingJob(new[] { file }, PackingFormat.Zip);
                job.Cancel(); PackingProgressDialog.Open(job); job.Start(); await job.Completion;
                var window = PackingProgressDialog.For(job);
                try { return job.Cancelled && window?.IsVisible == true ? null : "cancelled view dismissed"; }
                finally { window?.Close(); }
            });
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
            await CheckAsync("each direct packing toast action starts once", async () =>
            {
                foreach (var format in new[] { PackingFormat.Zip, PackingFormat.Vhdx })
                {
                    var actions = new System.Collections.Generic.List<ToastAction>();
                    ArchiveDiskActions.Add(new Clip.ClipItem { Kind = Clip.ClipKind.Files, Files = new[] { file } }, actions);
                    var action = actions.Single(a => a.Label == (format == PackingFormat.Zip ? "Pack to ZIP" : "Pack to VHDX"));
                    int started = 0;
                    void Count() => started++;
                    PackingJob.Started += Count;
                    VhdSession.TestDecline = format == PackingFormat.Vhdx;
                    PackingJob job = null;
                    try
                    {
                        action.Run(); job = PackingJob.Latest; action.Run();
                        if (started != 1 || PackingJob.Latest != job) return "repeated action started another job";
                        await job.Completion;
                        action.Run();
                        if (started != 1 || PackingJob.Latest != job) return "completed action started again";
                        if (format == PackingFormat.Zip && !job.Success) return job.Error ?? "ZIP did not complete";
                        if (format == PackingFormat.Vhdx && (job.Success || job.Output != null)) return "declined consent produced output";
                    }
                    finally
                    {
                        PackingJob.Started -= Count; VhdSession.TestDecline = false;
                        if (job != null) PackingProgressDialog.For(job)?.Close();
                    }
                }
                return null;
            });
            await CheckAsync("completed packing details follow live language switches", async () =>
            {
                var other = Path.Combine(root, "other.txt"); File.WriteAllText(other, "Second folder");
                var job = PackingProgressDialog.Start(new[] { file, other }, PackingFormat.Zip);
                await job.Completion;
                PackingProgressDialog.Open(job); // Completed results reopened by the person remain visible.
                var window = PackingProgressDialog.For(job);
                var old = Settings.Current.LanguageMode;
                try
                {
                    foreach (var mode in new[] { LanguageMode.Cantonese, LanguageMode.Bilingual, LanguageMode.English })
                    {
                        Settings.Current.LanguageMode = mode; Language.Refresh();
                        if (window.StageText != Language.T(job.Stage)) return "stage did not follow " + mode;
                        if (window.DestinationNote != Language.T("Mixed folders: output is saved beside the first copied item.")) return "destination explanation did not follow " + mode;
                        if (!window.ResultText.Contains(PathDisplay.Shown(job.Output))) return "display path did not use the shared privacy presentation";
                        if (window.RateText.Length != 0) return "terminal result still shows a transfer estimate";
                        if (!File.Exists(job.Output)) return "operational output path was altered";
                    }
                    return null;
                }
                finally { Settings.Current.LanguageMode = old; Language.Refresh(); window.Close(); }
            });
            PackingJob finished = null;
            await CheckAsync("direct ZIP starts automatically and verifies real entries", async () =>
            {
                var directParent = Path.Combine(root, "Direct ZIP");
                var directSource = Path.Combine(directParent, "Sources");
                Directory.CreateDirectory(Path.Combine(directSource, "Empty"));
                var directFile = Path.Combine(directSource, "hello.txt");
                File.WriteAllText(directFile, "Isolated direct ZIP fixture.");
                finished = PackingProgressDialog.Start(new[] { directSource, directFile }, PackingFormat.Zip);
                var window = PackingProgressDialog.For(finished);
                window.Hide();
                await finished.Completion;
                if (!finished.Success || finished.Partial) return finished.Error ?? string.Join("\n", finished.Warnings);
                if (PackingProgressDialog.For(finished) != null || window.IsVisible) return "verified success did not dismiss immediately";
                PackingProgressDialog.Open(finished);
                var reopened = PackingProgressDialog.For(finished);
                if (reopened == null || !reopened.IsVisible) return "completed result did not reopen";
                Language.Refresh();
                if (!reopened.IsVisible) return "manually reopened completed result auto-dismissed";
                if (!Activity.Recent(20).Any(e => e.Reopen != null)) return "no reopen activity";
                using var zip = ZipFile.OpenRead(finished.Output);
                if (zip.Entries.Count(e => !e.FullName.EndsWith("/")) != 1 || !zip.Entries.Any(e => e.FullName.EndsWith("Empty/"))) return "ZIP contents incorrect";
                using var reader = new StreamReader(zip.Entries.Single(e => e.FullName.EndsWith("hello.txt")).Open());
                if (reader.ReadToEnd() != File.ReadAllText(directFile)) return "ZIP bytes differ";
                return Directory.GetDirectories(directParent, ".monitoring-pack-*").Length == 0 ? null : "temporary output remains";
            });
            await CheckAsync("missing inputs produce an explicit partial result", async () =>
            {
                var job = new PackingJob(new[] { file, Path.Combine(root, "missing.txt") }, PackingFormat.Zip);
                job.Start(); await job.Completion;
                return !job.Success && job.Output != null && job.Partial && job.Warnings.Any(w => w.Contains("missing.txt")) ? null : "missing selection was silently dropped";
            });
            await CheckAsync("packing diagnostics mask profile paths without changing raw warnings", async () =>
            {
                var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(profile)) return "user profile path unavailable";
                var missing = Path.Combine(profile, "missing-pack-fixture-" + Guid.NewGuid().ToString("N"), "input.txt");
                var job = PackingProgressDialog.Start(new[] { file, missing }, PackingFormat.Zip);
                await job.Completion;
                var window = PackingProgressDialog.For(job);
                try
                {
                    var raw = job.Warnings.Single(w => w.Contains(missing));
                    var shown = window.ResultText;
                    if (shown.Contains(profile, StringComparison.OrdinalIgnoreCase) || !shown.Contains("%USERPROFILE%")) return "displayed warning exposes a profile path";
                    if (!job.Warnings.Contains(raw) || !raw.Contains(missing)) return "raw warning was changed";
                    var embedded = "Read failed: '" + missing.ToUpperInvariant() + "'.";
                    var masked = PackingProgressDialog.DisplayDiagnostic(embedded);
                    return masked.Contains(profile, StringComparison.OrdinalIgnoreCase) || !masked.Contains("%USERPROFILE%") ? "embedded diagnostic path was not masked" : null;
                }
                finally { window.Close(); }
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
            if (Environment.GetEnvironmentVariable("MT_PACK_LIVE_VHDX") == "1")
            {
                await CheckAsync("live VHDX packs verifies and detaches with native consent", async () =>
                {
                    var liveSource = Path.Combine(root, "Live disk sources");
                    Directory.CreateDirectory(Path.Combine(liveSource, "Empty"));
                    File.WriteAllText(Path.Combine(liveSource, "readme.txt"), "Native virtual disk verification fixture.");
                    File.WriteAllBytes(Path.Combine(liveSource, "payload.bin"), System.Security.Cryptography.RandomNumberGenerator.GetBytes(1024 * 1024));
                    string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
                    var originals = Directory.GetFiles(liveSource).ToDictionary(path => path, Hash);
                    var started = DateTimeOffset.UtcNow;
                    var savedLaunch = VhdSession.TestLaunch;
                    var savedDecline = VhdSession.TestDecline;
                    PackingJob live = null;
                    VDiskInfo info = null;
                    string failure = null;
                    bool originalsUnchanged = false, absentFromAttachedList = false;
                    int? attachedQueryError = null;
                    try
                    {
                        // This explicit opt-in always uses the real native helper and its user-owned consent prompt.
                        VhdSession.TestLaunch = null; VhdSession.TestDecline = false;
                        live = PackingProgressDialog.Start(new[] { liveSource }, PackingFormat.Vhdx);
                        await live.Completion;
                        if (!live.Success || live.Output == null || !File.Exists(live.Output)) failure = live.Error ?? "The live disk did not complete.";
                        else
                        {
                            info = VirtDisk.Read(live.Output);
                            var attached = VirtDisk.Attached();
                            attachedQueryError = VirtDisk.LastAttachedError;
                            absentFromAttachedList = attachedQueryError == 0 && !attached.Any(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(live.Output), StringComparison.OrdinalIgnoreCase));
                            originalsUnchanged = originals.All(pair => File.Exists(pair.Key) && Hash(pair.Key) == pair.Value);
                            if (info.Error != null) failure = info.Error;
                            else if (info.Attached || !absentFromAttachedList) failure = "Independent disk detachment could not be verified.";
                            else if (info.Format != "VHDX" || info.Type != "dynamic") failure = "The live disk format or allocation type did not match the defaults.";
                            else if (live.DiskResult == null || !live.DiskResult.Ok || live.DiskResult.Verified != originals.Count) failure = "Not every source file passed SHA-256 verification.";
                            else if (!originalsUnchanged) failure = "An original source file changed.";
                        }
                        return failure;
                    }
                    catch (Exception ex) { failure = ex.Message; return failure; }
                    finally
                    {
                        VhdSession.TestLaunch = savedLaunch; VhdSession.TestDecline = savedDecline;
                        File.WriteAllText(Path.Combine(dir, "quickpack-live-vhdx.json"), System.Text.Json.JsonSerializer.Serialize(new
                        {
                            version = 1, kind = "native-vhdx", consent = "native-user-owned", startedAtUtc = started,
                            finishedAtUtc = DateTimeOffset.UtcNow, success = failure == null && live?.Success == true,
                            failure, stage = live?.Stage, output = live?.Output, retainedPath = live?.RetainedPath,
                            outputExists = live?.Output != null && File.Exists(live.Output), format = info?.Format, allocationType = info?.Type,
                            virtualBytes = info?.VirtualSize, physicalBytes = info?.PhysicalSize, diskReadError = info?.Error,
                            attached = info?.Attached, absentFromAttachedList, attachedQueryError,
                            expectedFiles = originals.Count, verifiedFiles = live?.DiskResult?.Verified, originalsUnchanged
                        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        if (live != null)
                        {
                            if (finished != null) PackingProgressDialog.For(finished)?.Close();
                            finished = live;
                        }
                    }
                });
            }
            // Explicit test-only inspection hold: reopen a real completed result exactly as Activity does.
            // Production live-success auto-dismiss remains enabled; no delayed close or fabricated progress state.
            if (finished != null && int.TryParse(Environment.GetEnvironmentVariable("MT_PACK_HOLD_MS"), out var hold))
            {
                PackingProgressDialog.Open(finished);
                var heldWindow = PackingProgressDialog.For(finished);
                File.WriteAllText(Path.Combine(dir, "quickpack-held-dialog.json"), System.Text.Json.JsonSerializer.Serialize(new
                {
                    version = 1, kind = "actual-packing-result", processId = Environment.ProcessId,
                    hwnd = new System.Windows.Interop.WindowInteropHelper(heldWindow).Handle.ToInt64(),
                    reopenedCompletedResult = true, productionAutoDismiss = true, testHoldOnly = true,
                    format = finished.Format.ToString(), complete = finished.Complete, success = finished.Success,
                    stage = finished.Stage, output = finished.Output, error = finished.Error,
                    heldAtUtc = DateTimeOffset.UtcNow, holdMilliseconds = Math.Clamp(hold, 0, 120000)
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                await Task.Delay(Math.Clamp(hold, 0, 120000));
            }
            if (finished != null) PackingProgressDialog.For(finished)?.Close();
        }
    }
}
