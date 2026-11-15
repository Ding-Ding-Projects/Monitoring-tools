using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Core.Archives;
using MonitoringTools.Core.Disks;

namespace MonitoringTools.Core.Packing
{
    public enum PackingFormat { Zip, Vhdx }

    /// <summary>Owns a packing operation independently of any window, with observable progress and retained results.</summary>
    public sealed class PackingJob
    {
        public static PackingJob Latest { get; private set; }
        public static event Action Started;
        public event Action Changed;
        public PackingFormat Format { get; }
        public string[] Items { get; }
        public PackingSources Sources { get; private set; }
        public string Stage { get; private set; } = "Scanning";
        public string Output { get; private set; }
        public string Error { get; private set; }
        public string RetainedPath { get; private set; }
        public bool Complete { get; private set; }
        public bool Cancelled { get; private set; }
        public bool Partial => Warnings.Count > 0;
        public bool Success => Complete && Error == null && !Cancelled && !Partial && Output != null;
        public bool CancellationRequested => _cancel.IsCancellationRequested;
        public readonly List<string> Warnings = new();
        public PackProgress Progress { get; private set; }
        public PackResult DiskResult { get; private set; }
        public Task Completion { get; private set; }
        public bool Fixed, CopyPermissions, FitTightly;
        public string FileSystem = "NTFS";
        private readonly CancellationTokenSource _cancel = new();
        private string _staging;
        private VhdSession _session;
        private bool _detached;
        public PackingJob(string[] items, PackingFormat format) { Items = (string[])items.Clone(); Format = format; }
        public void Start()
        {
            if (Completion != null) return;
            Latest = this;
            Completion = Run();
            Started?.Invoke();
        }
        public void Cancel() => _cancel.Cancel();
        private void SetStage(string stage) { Stage = stage; Changed?.Invoke(); }
        private void Report(PackProgress p) { Progress = p; Changed?.Invoke(); }

        private async Task Run()
        {
            try
            {
                Sources = await Task.Run(() => PackingSources.Scan(Items, _cancel.Token));
                Warnings.AddRange(Sources.Warnings);
                _cancel.Token.ThrowIfCancellationRequested();
                _staging = Path.Combine(Sources.Folder, ".monitoring-pack-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_staging);
                var staged = Format == PackingFormat.Zip ? await Zip() : await Disk();
                _cancel.Token.ThrowIfCancellationRequested();
                SetStage("Finishing");
                Output = PackingSources.Publish(staged, Sources.Folder, Sources.Name, Format == PackingFormat.Zip ? ".zip" : ".vhdx");
                SetStage(Partial ? "Packed with skipped items" : "Packed and checked");
            }
            catch (OperationCanceledException) { Cancelled = true; SetStage("Cancelled"); }
            catch (Exception ex) { Error = ex.Message; SetStage("Stopped"); }
            finally
            {
                if (_session != null && !_detached)
                {
                    VhdStatus cleanup = null;
                    try { cleanup = await _session.DetachAsync(delete: true); }
                    catch (Exception ex) { Warnings.Add("Disk cleanup did not finish: " + ex.Message); }
                    _detached = cleanup?.Phase == "deleted" || cleanup?.Phase == "detached";
                    if (!_detached && (File.Exists(_session.Request.File) || _session.HelperAlive))
                    {
                        RetainedPath = _session.Request.File;
                        Error = (Error == null ? "" : Error + "\n") + "Disk cleanup was not confirmed. The temporary disk is retained: " + RetainedPath;
                        Stage = "Cleanup needs attention";
                    }
                }
                if (_staging != null && RetainedPath == null)
                {
                    try { Directory.Delete(_staging, true); }
                    catch (Exception ex) { RetainedPath = _staging; Stage = "Cleanup needs attention"; Warnings.Add("Temporary output could not be removed: " + ex.Message); }
                }
                if (_session != null && !_session.HelperAlive) _session.Cleanup();
                Complete = true;
                Changed?.Invoke();
                Activity.Add(ActivityKind.Files, Stage, Output ?? Error ?? "Packing cancelled", Success && !Partial);
            }
        }

        private async Task<string> Zip()
        {
            var scan = new ArchiveScan { Root = Sources.Folder, RootName = "" };
            foreach (var e in Sources.Entries)
                scan.Entries.Add(new ScanEntry { Full = e.Source, Relative = e.Relative.Replace('\\', '/'), Size = e.Size, IsEmptyDirectory = e.Folder, Modified = File.GetLastWriteTime(e.Source) });
            var options = new ArchiveOptions { Items = Sources.Items, Name = "output", OutFolder = _staging, Format = ArchiveFormat.Zip, Method = "Deflate", Level = 3, TestAfter = true, IncludeParent = false };
            var progress = new Progress<ArchiveProgress>(p =>
            {
                if (Complete) return;
                Stage = p.Step == "Test" ? "Verifying" : p.Step == "Finish" ? "Finishing" : "Compressing";
                Report(new PackProgress { Done = p.Done, Total = p.Total, BytesPerSecond = p.BytesPerSecond, Left = p.Remaining, Current = p.CurrentFile,
                    FilesTotal = p.FilesTotal, FilesDone = p.FilesDone });
            });
            var result = await ArchiveEngine.RunAsync(scan, options, progress, _cancel.Token);
            if (result.Cancelled) throw new OperationCanceledException();
            if (!result.Ok || !result.TestOk) throw new IOException(result.Error ?? "Archive verification did not finish.");
            return result.Outputs.Single();
        }

        private async Task<string> Disk()
        {
            var plan = QuickPackPlan.For(Sources.Items, entries: Sources.Entries.Select(e => (e.Size, e.Folder)));
            var letters = VhdEngine.FreeLetters();
            if (letters.Count == 0) throw new IOException("No drive letter is available for the temporary disk.");
            var request = new VhdRequest { File = Path.Combine(_staging, "output.vhdx"), SizeMb = Math.Max(VhdEngine.MinMb, plan.StartBytes / QuickPackPlan.Mb),
                Fixed = Fixed, FileSystem = FileSystem, Label = QuickPackPlan.LabelFor(Sources.Name), MountMode = "letter", Letter = letters[0] };
            var invalid = VhdEngine.Validate(request, checkFolderState: false);
            if (invalid != null) throw new IOException(invalid);
            _session = new VhdSession(request);
            SetStage("Waiting for Windows permission");
            if (!await _session.StartAsync()) throw new IOException(_session.LaunchError ?? "The disk helper did not start.");
            SetStage("Creating and mounting");
            var ready = await _session.WaitForAsync(s => s.Phase == "ready" || s.Phase == "error", TimeSpan.FromMinutes(10), CancellationToken.None);
            if (ready?.Phase != "ready") throw new IOException(ready?.Error ?? "Disk creation did not finish.");
            _cancel.Token.ThrowIfCancellationRequested();
            var copy = new PackCopy { CopyPermissions = CopyPermissions };
            var growths = new List<(long From, long To)>();
            long size = request.SizeMb * QuickPackPlan.Mb;
            copy.Grow = async left =>
            {
                _cancel.Token.ThrowIfCancellationRequested();
                var next = QuickPackPlan.GrowTo(size, left);
                if (next <= size) return "The disk cannot grow any further.";
                SetStage("Growing disk");
                _session.Send("grow " + next / QuickPackPlan.Mb);
                var grown = await _session.WaitForAsync(s => s.Phase == "grown" || s.Phase == "grow-failed" || s.Phase == "error", TimeSpan.FromMinutes(10), CancellationToken.None);
                if (grown?.Phase != "grown") return grown?.Error ?? "Disk growth did not finish.";
                growths.Add((size, next)); size = next; SetStage("Copying and verifying"); return null;
            };
            SetStage("Copying and verifying");
            DiskResult = await copy.RunAsync(Sources.Entries, VhdEngine.MountRoot(request), new Progress<PackProgress>(Report), _cancel.Token);
            DiskResult.Growths.AddRange(growths);
            Warnings.AddRange(DiskResult.Skipped.Select(s => s.Path + ": " + s.Reason));
            if (DiskResult.Cancelled) throw new OperationCanceledException();
            if (DiskResult.Error != null) throw new IOException(DiskResult.Error);
            if (FitTightly)
            {
                SetStage("Fitting disk");
                _session.Send("fit");
                var fitted = await _session.WaitForAsync(s => s.Phase == "fitted" || s.Phase == "fit-failed" || s.Phase == "error", TimeSpan.FromMinutes(10), CancellationToken.None);
                if (fitted?.Phase != "fitted") Warnings.Add("Could not fit the disk tightly: " + (fitted?.Error ?? "no answer"));
            }
            SetStage("Detaching");
            var detached = await _session.DetachAsync(delete: false);
            _detached = detached?.Phase == "detached";
            if (!_detached) throw new IOException(detached?.Error ?? "Disk detachment was not confirmed.");
            return request.File;
        }
    }
}
