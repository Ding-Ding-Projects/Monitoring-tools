using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Media.Imaging;
using MonitoringTools.Core;

namespace MonitoringTools.Clip
{
    /// <summary>
    /// The clipboard history kept as a local Git repository, with no limit on the number of copies.
    /// Every copy is written as its own file under entries/yyyy/MM/dd/ and committed at once, so the Git log is the history
    /// and every edit, pin and removal is a commit too. The repository never has a remote unless the person adds one.
    ///
    /// Privacy: copies marked as secrets are not kept unless "Keep secrets" is on; when the history protection
    /// (Settings › Safety) is Windows or a password, every file is written encrypted with AES-256-GCM, and commit
    /// messages never contain the copied text, only its kind and size.
    /// </summary>
    public static class ClipRepo
    {
        public sealed class Entry
        {
            public string Id { get; set; }
            public DateTime At { get; set; }
            public string Kind { get; set; }
            /// <summary>Path of the entry's file, relative to the repository, with forward slashes.</summary>
            public string File { get; set; }
            /// <summary>The first 300 characters (plain), or the same sealed and base64-encoded when the history is encrypted.</summary>
            public string Preview { get; set; }
            public bool SealedPreview { get; set; }
            public int Chars { get; set; }
            public string Source { get; set; }
            public bool Pinned { get; set; }
            /// <summary>How many times the copy was edited after it was taken (each edit is a commit).</summary>
            public int Edits { get; set; }

            [System.Text.Json.Serialization.JsonIgnore] public ClipItem Live { get; set; }
            [System.Text.Json.Serialization.JsonIgnore] public string ShownPreview => SealedPreview ? (Open(Preview) ?? "[locked: type the history password to read]") : Preview;
        }

        private sealed class Job
        {
            public string Kind;            // "add", "edit", "rewrite"
            public Entry Entry;
            public byte[] Content;         // the file's bytes before sealing
            public string Message;
            public List<string> Remove = new List<string>();
        }

        /// <summary>Set by the self-test lane that checks the repository; tests and screenshots otherwise keep history in memory only.</summary>
        public static bool ForceForTest { get; set; }

        private static readonly BlockingCollection<Job> Queue = new BlockingCollection<Job>();
        private static readonly object Gate = new object();
        private static List<Entry> _index;           // newest first
        private static Thread _worker;
        private static int _pending;
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = false };

        public static event Action Changed;
        public static string LastError { get; private set; }
        public static int Pending => _pending;
        public static int Commits { get; private set; }
        public static DateTime? LastCommit { get; private set; }

        /// <summary>True when copies are written to the repository right now.</summary>
        public static bool Recording =>
            Settings.Current.ClipRepoEnabled && !Settings.Current.ClipRepoPaused &&
            (ForceForTest || (App.SelfTestDir == null && App.ScreenshotDir == null));

        /// <summary>True when the History page should show the repository rather than the in-memory list.</summary>
        public static bool InUse => Settings.Current.ClipRepoEnabled && (ForceForTest || (App.SelfTestDir == null && App.ScreenshotDir == null));

        public static string Dir
        {
            get
            {
                var p = Settings.Current.ClipRepoPath;
                return string.IsNullOrWhiteSpace(p) ? Path.Combine(Settings.DataDir, "clipboard-history") : Environment.ExpandEnvironmentVariables(p.Trim());
            }
        }

        private static string IndexPath => Path.Combine(Dir, "index.jsonl");

        // ------------------------------------------------------------------ recording

        /// <summary>Records one copy. Called for every copy that reaches the history; returns at once (the commit runs on a worker).</summary>
        public static void Record(ClipItem item)
        {
            if (item == null || !Recording) return;
            if ((item.Sensitive || (item.BestFact?.Sensitive ?? false)) && !Settings.Current.ClipRepoKeepSecrets) return;
            try
            {
                var (bytes, ext) = Content(item);
                if (bytes == null) return;
                var at = item.At == default ? DateTime.Now : item.At;
                var id = at.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Interlocked.Increment(ref _seq).ToString("x");
                var kind = item.Kind.ToString().ToLowerInvariant();
                var text = Preview(item);
                var e = new Entry
                {
                    Id = id, At = at, Kind = kind,
                    File = $"entries/{at:yyyy}/{at:MM}/{at:dd}/{id}-{kind}{ext}" + (Encrypts ? ".enc" : ""),
                    Chars = item.Text?.Length ?? (item.Files?.Length ?? 0),
                    Source = item.SourceApp,
                    Live = item
                };
                SetPreview(e, text);
                item.RepoId = id;
                Enqueue(new Job { Kind = "add", Entry = e, Content = bytes, Message = $"Copy {kind}{Size(item)}{(string.IsNullOrEmpty(item.SourceApp) ? "" : " from " + item.SourceApp)}" });
            }
            catch (Exception ex) { Fail("A copy could not be recorded: " + ex.Message); }
        }

        /// <summary>A copy was edited in place: write the new text over its file, so the old text stays one commit back.</summary>
        public static void RecordEdit(ClipItem item)
        {
            if (item?.RepoId == null || !Recording) return;
            var e = Find(item.RepoId);
            if (e == null) return;
            var (bytes, _) = Content(item);
            if (bytes == null) return;
            e.Edits++;
            e.Chars = item.Text?.Length ?? e.Chars;
            SetPreview(e, Preview(item));
            Enqueue(new Job { Kind = "edit", Entry = e, Content = bytes, Message = $"Edit copy {e.Id} ({e.Chars} characters)" });
        }

        public static void SetPinned(IEnumerable<Entry> entries, bool pinned)
        {
            var list = entries.Where(e => e.Pinned != pinned).ToList();
            if (list.Count == 0) return;
            foreach (var e in list) e.Pinned = pinned;
            Enqueue(new Job { Kind = "rewrite", Message = $"{(pinned ? "Pin" : "Unpin")} {Count(list.Count)}" });
        }

        /// <summary>Removes copies from the history. Their files leave the current tree in one commit; Git still holds them in earlier commits
        /// until "Forget removed copies for good" rewrites the history.</summary>
        public static void Remove(IEnumerable<Entry> entries)
        {
            var list = entries.ToList();
            if (list.Count == 0) return;
            lock (Gate) { Load(); foreach (var e in list) _index.Remove(e); }
            var job = new Job { Kind = "rewrite", Message = $"Remove {Count(list.Count)}" };
            job.Remove.AddRange(list.Select(e => e.File));
            Enqueue(job);
            Changed?.Invoke();
        }

        /// <summary>
        /// Rewrites the repository so removed copies are gone from every commit: one fresh commit holds what is kept today,
        /// and the old commits are dropped and garbage-collected. Returns an error sentence or null.
        /// </summary>
        public static string ForgetRemoved()
        {
            Drain();
            lock (Gate)
            {
                try
                {
                    var dir = Dir;
                    if (!Directory.Exists(Path.Combine(dir, ".git"))) return null;
                    var r = GitPush.Git(dir, "checkout -q --orphan compacted");
                    if (r.Code != 0) return r.All;
                    GitPush.Git(dir, "add -A");
                    GitPush.Git(dir, $"commit -q -m \"Start again with the {Count(_index?.Count ?? 0)} kept today\"");
                    GitPush.Git(dir, "branch -D main");
                    GitPush.Git(dir, "branch -m main");
                    GitPush.Git(dir, "reflog expire --expire=now --all");
                    GitPush.Git(dir, "gc -q --prune=now");
                    Count();
                    return null;
                }
                catch (Exception ex) { return ex.Message; }
                finally { Changed?.Invoke(); }
            }
        }

        // ------------------------------------------------------------------ reading

        /// <summary>Every copy, newest first. The list is shared; do not change it.</summary>
        public static IReadOnlyList<Entry> All()
        {
            lock (Gate) { Load(); return _index.ToList(); }
        }

        public static Entry Find(string id) { lock (Gate) { Load(); return _index.FirstOrDefault(e => e.Id == id); } }

        /// <summary>The full text of a copy (a file list for files, a short label for an image or a sound). Null when it cannot be read.</summary>
        public static string ReadText(Entry e)
        {
            if (e.Live?.Text != null) return e.Live.Text;
            var bytes = ReadBytes(e);
            if (bytes == null) return null;
            if (e.Kind == "image") return "[image]";
            if (e.Kind == "audio") return "[sound]";
            return new UTF8Encoding(false).GetString(bytes);
        }

        public static BitmapSource ReadImage(Entry e)
        {
            if (e.Live?.Image != null) return e.Live.Image;
            if (e.Kind != "image") return null;
            var bytes = ReadBytes(e);
            if (bytes == null) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = new MemoryStream(bytes); bmp.EndInit(); bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private static byte[] ReadBytes(Entry e)
        {
            try
            {
                var path = Path.Combine(Dir, e.File.Replace('/', Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(path)) return null;
                if (!e.File.EndsWith(".enc", StringComparison.Ordinal)) return System.IO.File.ReadAllBytes(path);
                // a sealed file is the base64 of "MTH1" + nonce + tag + ciphertext, so Git can still show it as text
                var b64 = HistoryVault.Decrypt(Convert.FromBase64String(System.IO.File.ReadAllText(path).Trim()));
                return b64 == null ? null : Convert.FromBase64String(b64);
            }
            catch { return null; }
        }

        /// <summary>The versions of one copy, newest first: (commit, time, message).</summary>
        public static List<(string Sha, DateTime At, string Message)> Versions(Entry e) => LogFor("-- \"" + e.File + "\"", 50);

        /// <summary>The newest commits of the whole history.</summary>
        public static List<(string Sha, DateTime At, string Message)> Recent(int n = 10) => LogFor("", n);

        /// <summary>The text of a copy as it was at one commit.</summary>
        public static string TextAt(Entry e, string sha)
        {
            var r = GitPush.Git(Dir, $"show {sha}:\"{e.File}\"");
            if (r.Code != 0) return null;
            if (!e.File.EndsWith(".enc", StringComparison.Ordinal)) return r.Out;
            try
            {
                var b64 = HistoryVault.Decrypt(Convert.FromBase64String(r.Out.Trim()));
                return b64 == null ? null : new UTF8Encoding(false).GetString(Convert.FromBase64String(b64));
            }
            catch { return null; }
        }

        private static List<(string, DateTime, string)> LogFor(string tail, int n)
        {
            var list = new List<(string, DateTime, string)>();
            if (!Directory.Exists(Path.Combine(Dir, ".git"))) return list;
            var r = GitPush.Git(Dir, $"log -n {n} --format=%h%x1f%cI%x1f%s {tail}");
            if (r.Code != 0) return list;
            foreach (var line in r.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split('\x1f');
                if (p.Length < 3) continue;
                DateTime.TryParse(p[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at);
                list.Add((p[0], at.ToLocalTime(), p[2]));
            }
            return list;
        }

        /// <summary>Size of the repository folder on disk, in bytes.</summary>
        public static long DiskBytes()
        {
            try { return Directory.Exists(Dir) ? new DirectoryInfo(Dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0; }
            catch { return 0; }
        }

        /// <summary>Waits until every queued copy is committed (up to the given time). Used by checks and before a rewrite.</summary>
        public static bool Drain(int ms = 30000)
        {
            var until = DateTime.UtcNow.AddMilliseconds(ms);
            while (_pending > 0 && DateTime.UtcNow < until) Thread.Sleep(50);
            return _pending == 0;
        }

        /// <summary>Forgets the loaded index so the next read comes from disk (after the folder changes).</summary>
        public static void Reload() { lock (Gate) _index = null; Changed?.Invoke(); }

        // ------------------------------------------------------------------ worker

        private static int _seq;

        private static void Enqueue(Job j)
        {
            lock (Gate)
            {
                Load();
                if (j.Kind == "add") _index.Insert(0, j.Entry);
                if (_worker == null)
                {
                    _worker = new Thread(Work) { IsBackground = true, Name = "Clipboard history repository", Priority = ThreadPriority.BelowNormal };
                    _worker.Start();
                }
            }
            Interlocked.Increment(ref _pending);
            Queue.Add(j);
            Changed?.Invoke();
        }

        private static void Work()
        {
            foreach (var j in Queue.GetConsumingEnumerable())
            {
                try
                {
                    // a password-protected history that is locked waits until it is unlocked
                    while (Encrypts && HistoryVault.IsLocked) Thread.Sleep(2000);
                    Commit(j);
                    LastError = null;
                }
                catch (Exception ex) { Fail(ex.Message); }
                finally { Interlocked.Decrement(ref _pending); Changed?.Invoke(); }
            }
        }

        private static void Commit(Job j)
        {
            var dir = Dir;
            EnsureRepo(dir);
            var paths = new List<string>();
            if (j.Entry != null && j.Content != null)
            {
                var full = Path.Combine(dir, j.Entry.File.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                var bytes = j.Content;
                if (j.Entry.File.EndsWith(".enc", StringComparison.Ordinal))
                {
                    var sealedBytes = HistoryVault.Encrypt(Convert.ToBase64String(j.Content));
                    if (sealedBytes == null) throw new InvalidOperationException("The history key is not available, so the copy was not written.");
                    bytes = Encoding.ASCII.GetBytes(Convert.ToBase64String(sealedBytes) + "\n");
                }
                System.IO.File.WriteAllBytes(full, bytes);
                paths.Add(j.Entry.File);
            }
            foreach (var r in j.Remove)
            {
                var full = Path.Combine(dir, r.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                paths.Add(r);
            }
            WriteIndex(dir);
            paths.Add("index.jsonl");
            var add = GitPush.Git(dir, "add -A -- " + string.Join(" ", paths.Select(p => "\"" + p + "\"")));
            if (add.Code != 0) throw new InvalidOperationException("git add: " + add.All);
            var msg = Path.Combine(dir, ".git", "MT_COMMIT_MSG");
            System.IO.File.WriteAllText(msg, j.Message + "\n", new UTF8Encoding(false));
            var c = GitPush.Git(dir, $"commit -q --no-verify -F \"{msg}\"");
            if (c.Code != 0 && !c.All.Contains("nothing to commit")) throw new InvalidOperationException("git commit: " + c.All);
            Commits++;
            LastCommit = DateTime.Now;
        }

        private static void EnsureRepo(string dir)
        {
            if (Directory.Exists(Path.Combine(dir, ".git"))) { if (Commits == 0) Count(); return; }
            Directory.CreateDirectory(dir);
            var r = GitPush.Git(dir, "init -q -b main");
            if (r.Code != 0) throw new InvalidOperationException("git init: " + r.All);
            GitPush.Git(dir, "config user.name \"Monitoring Tools\"");
            GitPush.Git(dir, "config user.email \"monitoring-tools@localhost\"");
            GitPush.Git(dir, "config core.autocrlf false");
            GitPush.Git(dir, "config commit.gpgsign false");
            System.IO.File.WriteAllText(Path.Combine(dir, ".gitattributes"), "*.enc -diff\n*.png binary\n*.wav binary\n", new UTF8Encoding(false));
            System.IO.File.WriteAllText(Path.Combine(dir, "README.md"),
                "# Clipboard history\n\nKept by Monitoring Tools. Every copy is a file under `entries/` and every copy, edit, pin and removal is a commit.\n" +
                "`index.jsonl` lists the copies, one per line. This repository has no remote unless you add one; it holds what you copied, so think before you push it anywhere.\n",
                new UTF8Encoding(false));
            System.IO.File.WriteAllText(IndexPath, "", new UTF8Encoding(false));
            GitPush.Git(dir, "add -A");
            GitPush.Git(dir, "commit -q --no-verify -m \"Start the clipboard history\"");
            Commits = 1;
            Log.Info("Started the clipboard history repository in " + dir);
        }

        private static void Count()
        {
            var r = GitPush.Git(Dir, "rev-list --count HEAD");
            if (r.Code == 0 && int.TryParse(r.Out.Trim(), out var n)) Commits = n;
            var l = LogFor("", 1);
            if (l.Count > 0) LastCommit = l[0].Item2;
        }

        // ------------------------------------------------------------------ index

        private static void Load()
        {
            if (_index != null) return;
            _index = new List<Entry>();
            try
            {
                if (!System.IO.File.Exists(IndexPath)) return;
                foreach (var line in System.IO.File.ReadLines(IndexPath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try { var e = JsonSerializer.Deserialize<Entry>(line); if (e?.Id != null) _index.Add(e); } catch { }
                }
                _index.Reverse();   // the file is oldest first, so new lines are appended
                Count();
            }
            catch (Exception ex) { Fail("The history index could not be read: " + ex.Message); }
        }

        private static void WriteIndex(string dir)
        {
            List<Entry> copy;
            lock (Gate) { Load(); copy = _index.ToList(); }
            copy.Reverse();
            var sb = new StringBuilder();
            foreach (var e in copy) sb.Append(JsonSerializer.Serialize(e, Json)).Append('\n');
            var tmp = IndexPath + ".tmp";
            System.IO.File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            System.IO.File.Move(tmp, IndexPath, true);
        }

        // ------------------------------------------------------------------ helpers

        private static bool Encrypts => HistoryVault.Encrypts;

        private static void SetPreview(Entry e, string text)
        {
            text ??= "";
            if (text.Length > 300) text = text.Substring(0, 300);
            if (Encrypts)
            {
                var sealedBytes = HistoryVault.Encrypt(text);
                if (sealedBytes != null) { e.Preview = Convert.ToBase64String(sealedBytes); e.SealedPreview = true; return; }
                e.Preview = ""; e.SealedPreview = false; return;
            }
            e.Preview = text; e.SealedPreview = false;
        }

        private static string Open(string sealedB64)
        {
            try { return HistoryVault.Decrypt(Convert.FromBase64String(sealedB64)); } catch { return null; }
        }

        private static string Preview(ClipItem item) =>
            item.Text ?? (item.Files != null && item.Files.Length > 0 ? string.Join("\n", item.Files) : item.Kind == ClipKind.Image ? "[image]" : item.Kind == ClipKind.Audio ? "[sound]" : "");

        private static (byte[] Bytes, string Ext) Content(ClipItem item)
        {
            var utf8 = new UTF8Encoding(false);
            switch (item.Kind)
            {
                case ClipKind.Image:
                    if (item.Image == null) return (null, null);
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(item.Image));
                    using (var ms = new MemoryStream()) { enc.Save(ms); return (ms.ToArray(), ".png"); }
                case ClipKind.Audio:
                    return item.Audio == null ? (null, null) : (item.Audio, ".wav");
                case ClipKind.Files:
                    return (utf8.GetBytes(string.Join("\n", item.Files ?? Array.Empty<string>()) + "\n"), ".files.txt");
                case ClipKind.Json: return (utf8.GetBytes(item.Text ?? ""), ".json");
                case ClipKind.Markdown: return (utf8.GetBytes(item.Text ?? ""), ".md");
                default:
                    if (item.Text == null) return (null, null);
                    return (utf8.GetBytes(item.Text), ".txt");
            }
        }

        private static string Size(ClipItem item) =>
            item.Text != null ? $", {item.Text.Length} character{(item.Text.Length == 1 ? "" : "s")}"
            : item.Files != null && item.Files.Length > 0 ? $", {item.Files.Length} file{(item.Files.Length == 1 ? "" : "s")}" : "";

        private static string Count(int n) => n == 1 ? "1 copy" : $"{n} copies";

        private static void Fail(string why)
        {
            LastError = why;
            Log.Warn("Clipboard history repository: " + why);
        }
    }
}
