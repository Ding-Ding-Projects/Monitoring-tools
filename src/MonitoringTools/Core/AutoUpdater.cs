using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Keeps the installed service current without asking anyone to download anything. The agent looks for a
    /// newer GitHub release a little after start and every hour, downloads the installer in the background,
    /// checks its SHA-256 against the digest GitHub publishes and its own version against the release, and then,
    /// at a quiet moment (no conversion, no Minecraft server started here, no game), runs it with
    /// <c>--update-install</c>. That process replaces the installed exe and starts the service again by itself,
    /// so the swap takes a few seconds and needs no restart, sign-out or click. If the copy fails, the previous
    /// version is started again.
    /// </summary>
    public static class AutoUpdater
    {
        public const string InstallArg = "--update-install";
        private const string LatestUrl = "https://api.github.com/repos/Ding-Ding-Projects/Monitoring-tools/releases/latest";
        private const string AssetName = "MonitoringTools-Setup.exe";
        private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan Every = TimeSpan.FromHours(1);
        private static readonly TimeSpan RetryWhenBusy = TimeSpan.FromMinutes(10);

        public sealed record Release(Version Version, string Url, string Sha256);

        public enum Phase { Idle, Checking, Downloading, Waiting, Installing, UpToDate, Failed }

        public static Phase State { get; private set; } = Phase.Idle;
        /// <summary>One plain sentence about the last thing the updater did, shown on the About page.</summary>
        public static string Status { get; private set; } = "Updates install themselves in the background.";
        public static DateTime? LastChecked { get; private set; }
        public static event Action Changed;

        /// <summary>Test hooks. The app itself never sets them.</summary>
        internal static Func<Task<Release>> SourceForTest;
        internal static Action<string> LaunchForTest;
        internal static Func<string> BlockedForTest;
        internal static string FolderForTest;

        private static string _ready;          // a verified installer waiting for a quiet moment
        private static Version _readyVersion;
        /// <summary>The newer version that is downloaded and checked, or null.</summary>
        public static Version ReadyVersion => _ready != null ? _readyVersion : null;
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static CancellationTokenSource _loop;

        internal static Version CurrentForTest;
        public static Version Current => CurrentForTest ?? (typeof(AutoUpdater).Assembly.GetName().Version is Version v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(1, 0, 0));

        public static string Folder => FolderForTest ?? Path.Combine(Settings.DataDir, "updates");

        private static void Set(Phase p, string text)
        {
            State = p; Status = text;
            Log.Info("Update: " + text);
            try { Changed?.Invoke(); } catch { }
        }

        /// <summary>Starts the background loop in the agent. A development build never installs over itself.</summary>
        public static void Start()
        {
            _loop?.Cancel();
            _loop = new CancellationTokenSource();
            var token = _loop.Token;
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(FirstCheck, token); } catch { return; }
                while (!token.IsCancellationRequested)
                {
                    var wait = Every;
                    try
                    {
                        if (Settings.Current.AutoUpdate)
                        {
                            await CheckAsync(installWhenQuiet: true);
                            if (_ready != null && State == Phase.Waiting) wait = RetryWhenBusy;
                        }
                    }
                    catch (Exception ex) { Log.Warn("Update loop: " + ex.Message); }
                    try { await Task.Delay(wait, token); } catch { return; }
                }
            });
        }

        /// <summary>Why installing right now would interrupt something, or null when it is a quiet moment.</summary>
        public static string BlockedReason()
        {
            if (BlockedForTest != null) return BlockedForTest();
            if (ServiceHost.IsPortableBuild) return "this is a development build, which never installs over itself";
            if (Media.Converter.Busy) return "a conversion is running";
            if (Minecraft.ServerManager.AnyLocalProcessRunning) return "a Minecraft server started here is running";
            if (App.Games?.IsGaming == true && IsPlaying(Native.IdleFor())) return "a game is running";
            return null;
        }

        /// <summary>How long without keyboard or mouse input counts as not playing, even with a game left open.</summary>
        public static readonly TimeSpan AwayAfter = TimeSpan.FromMinutes(10);

        /// <summary>A game only holds the update back while someone is using the computer: a game client left open
        /// all day would otherwise stop every update. Unknown idle time counts as playing.</summary>
        internal static bool IsPlaying(TimeSpan? idle) => idle == null || idle.Value < AwayAfter;

        /// <summary>Looks for a newer release, downloads and verifies it, and installs it when nothing would be interrupted.</summary>
        public static async Task CheckAsync(bool installWhenQuiet)
        {
            if (!await Gate.WaitAsync(0)) return;   // one check at a time
            try
            {
                if (_ready == null || !File.Exists(_ready))
                {
                    _ready = null;
                    Set(Phase.Checking, "Looking for a newer version…");
                    var rel = await LatestAsync();
                    LastChecked = DateTime.Now;
                    if (rel == null) { Set(Phase.Failed, "Could not read the latest release."); return; }
                    if (rel.Version <= Current) { Set(Phase.UpToDate, $"Up to date ({Current.ToString(3)}). Checked {LastChecked:HH:mm}."); return; }
                    var file = await DownloadAsync(rel);
                    if (file == null) return;
                    _ready = file; _readyVersion = rel.Version;
                }
                if (!installWhenQuiet) { Set(Phase.Waiting, $"Version {_readyVersion.ToString(3)} is downloaded and checked."); return; }
                var why = BlockedReason();
                if (why != null) { Set(Phase.Waiting, $"Version {_readyVersion.ToString(3)} is downloaded and checked. It installs by itself when {Quiet(why)}."); return; }
                Install(_ready, _readyVersion);
            }
            catch (Exception ex) { Set(Phase.Failed, "Update check failed: " + ex.Message); }
            finally { Gate.Release(); }
        }

        private static string Quiet(string why) => why switch
        {
            "a conversion is running" => "the conversion finishes",
            "a Minecraft server started here is running" => "that Minecraft server stops",
            "a game is running" => "you stop playing or step away for 10 minutes",
            _ => why.StartsWith("this is a development build") ? "it runs as the installed copy" : "nothing is running",
        };

        private static async Task<Release> LatestAsync()
        {
            if (SourceForTest != null) return await SourceForTest();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/" + Current.ToString(3));
            using var doc = JsonDocument.Parse(await http.GetStringAsync(LatestUrl));
            var root = doc.RootElement;
            if (root.TryGetProperty("draft", out var d) && d.GetBoolean()) return null;
            if (root.TryGetProperty("prerelease", out var pr) && pr.GetBoolean()) return null;
            if (!Version.TryParse((root.GetProperty("tag_name").GetString() ?? "").TrimStart('v'), out var v)) return null;
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                if (a.GetProperty("name").GetString() != AssetName) continue;
                var digest = a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String ? dg.GetString() : null;
                var sha = digest != null && digest.StartsWith("sha256:") ? digest.Substring(7) : null;
                return new Release(v, a.GetProperty("browser_download_url").GetString(), sha);
            }
            return null;
        }

        /// <summary>Downloads to a .part file, then keeps it only when its SHA-256 and its own version match the release.</summary>
        private static async Task<string> DownloadAsync(Release rel)
        {
            if (string.IsNullOrEmpty(rel.Sha256)) { Set(Phase.Failed, $"Version {rel.Version.ToString(3)} has no published checksum, so it was not downloaded."); return null; }
            Directory.CreateDirectory(Folder);
            var target = Path.Combine(Folder, $"MonitoringTools-{rel.Version.ToString(3)}.exe");
            var part = target + ".part";
            Set(Phase.Downloading, $"Downloading version {rel.Version.ToString(3)} in the background…");
            if (rel.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) File.Copy(new Uri(rel.Url).LocalPath, part, true);
            else
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/" + Current.ToString(3));
                using var resp = await http.GetAsync(rel.Url, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
                await using var src = await resp.Content.ReadAsStreamAsync();
                await using var dst = File.Create(part);
                await src.CopyToAsync(dst);
            }
            string hash;
            await using (var fs = File.OpenRead(part)) hash = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();
            if (!string.Equals(hash, rel.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(part);
                Set(Phase.Failed, $"The download of {rel.Version.ToString(3)} did not match its published checksum, so it was thrown away.");
                return null;
            }
            var own = OwnVersion(part);
            if (own != rel.Version)
            {
                TryDelete(part);
                Set(Phase.Failed, $"The download says it is version {own?.ToString(3) ?? "unknown"}, not {rel.Version.ToString(3)}, so it was thrown away.");
                return null;
            }
            if (File.Exists(target)) TryDelete(target);
            File.Move(part, target);
            foreach (var old in Directory.GetFiles(Folder, "MonitoringTools-*.exe*").Where(f => !string.Equals(f, target, StringComparison.OrdinalIgnoreCase)))
                TryDelete(old);
            return target;
        }

        internal static Version OwnVersion(string exe)
        {
            try
            {
                var pv = FileVersionInfo.GetVersionInfo(exe).ProductVersion ?? "";
                var core = pv.Split('+')[0];
                return Version.TryParse(core, out var v) ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : null;
            }
            catch { return null; }
        }

        private static void Install(string file, Version v)
        {
            Set(Phase.Installing, $"Installing version {v.ToString(3)}. The service is back in a few seconds.");
            if (LaunchForTest != null) { LaunchForTest(file); return; }
            Process.Start(new ProcessStartInfo(file, InstallArg) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(file) });
        }

        private static void TryDelete(string f) { try { File.Delete(f); } catch { } }

        internal static void ResetForTest()
        {
            _ready = null; _readyVersion = null; State = Phase.Idle; Status = "";
        }

        // ------------------------------------------------------------- the installing process

        private static string FlagPath => Path.Combine(Settings.DataDir, "updated.flag");

        /// <summary>Runs in the downloaded installer: replace the installed copy, then start the service again.
        /// The previous version keeps running from the same folder if the copy fails.</summary>
        public static int RunInstall()
        {
            var from = ServiceHost.IsRunningInstalled ? "" : (OwnVersion(ServiceHost.InstalledExe)?.ToString(3) ?? "");
            try
            {
                ServiceHost.Install();
                File.WriteAllText(FlagPath, $"{from}>{Current.ToString(3)}");
            }
            catch (Exception ex) { Log.Error("Background update could not replace the installed copy; starting the previous version", ex); }
            try
            {
                if (Settings.Current.RunElevated) ServiceHost.RelaunchForElevationChange();
                else ServiceHost.LaunchSupervisor();
            }
            catch (Exception ex) { Log.Error("Background update could not start the service again", ex); return 1; }
            return 0;
        }

        /// <summary>In the new agent: the sentence to show once after a background update, or null.</summary>
        public static string TakeUpdatedNote()
        {
            try
            {
                if (!File.Exists(FlagPath)) return null;
                var parts = File.ReadAllText(FlagPath).Split('>');
                File.Delete(FlagPath);
                var to = parts.Length > 1 ? parts[1] : Current.ToString(3);
                var from = parts[0];
                return from.Length > 0 ? $"Updated from {from} to {to} in the background." : $"Updated to {to} in the background.";
            }
            catch { return null; }
        }
    }
}
