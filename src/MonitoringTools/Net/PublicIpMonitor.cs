using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MonitoringTools.Core;

namespace MonitoringTools.Net
{
    public sealed class IpRecord
    {
        public string Ip { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int Times { get; set; } = 1;
        public string Org { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Place => string.Join(", ", new[] { City, Country }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public sealed class IpHistory
    {
        public string Current { get; set; }
        public DateTime LastChecked { get; set; }
        public List<IpRecord> Records { get; set; } = new List<IpRecord>();
    }

    /// <summary>
    /// Watches the public IP address. A change pops a dialog and is committed to a local
    /// git repository (ip-history.md and history.json) with first and last seen times,
    /// then pushed when a remote is configured.
    /// </summary>
    public sealed class PublicIpMonitor : IDisposable
    {
        public event Action<IpRecord, IpRecord> Changed;   // (previous, current)
        public event Action Updated;
        public IpHistory History { get; private set; } = new IpHistory();
        public string LastError { get; private set; }

        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _now = new SemaphoreSlim(0);
        private readonly SemaphoreSlim _repoGate = new SemaphoreSlim(1, 1);

        public static string RepoDir
        {
            get
            {
                var d = string.IsNullOrWhiteSpace(Settings.Current.IpHistoryRepo)
                    ? Path.Combine(Settings.DataDir, "ip-history")
                    : Settings.Current.IpHistoryRepo;
                Directory.CreateDirectory(d);
                return d;
            }
        }

        private static string JsonPath => Path.Combine(RepoDir, "history.json");

        public PublicIpMonitor()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            try { if (File.Exists(JsonPath)) History = JsonSerializer.Deserialize<IpHistory>(File.ReadAllText(JsonPath)) ?? new IpHistory(); }
            catch (Exception ex) { Log.Warn("IP history unreadable: " + ex.Message); }
            NetworkChange.NetworkAddressChanged += (s, e) => CheckSoon();
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = Loop(_cts.Token);
        }

        /// <summary>Re-check shortly, for example after the network comes back.</summary>
        public void CheckSoon() { try { _now.Release(); } catch { } }

        private async Task Loop(CancellationToken ct)
        {
            await Task.Delay(3000, ct).ContinueWith(_ => { });
            while (!ct.IsCancellationRequested)
            {
                if (Settings.Current.PublicIpEnabled) await CheckAsync();
                var wait = TimeSpan.FromMinutes(Math.Max(1, Settings.Current.PublicIpIntervalMinutes));
                await _now.WaitAsync(wait, ct).ContinueWith(_ => { });
                if (!ct.IsCancellationRequested) await Task.Delay(4000).ContinueWith(_ => { }); // let a new route settle
            }
        }

        public async Task CheckAsync()
        {
            var info = await Lookup();
            if (info == null) return;
            var now = DateTime.Now;
            IpRecord previous = History.Records.FirstOrDefault(r => r.Ip == History.Current);
            var current = History.Records.FirstOrDefault(r => r.Ip == info.Ip);
            History.LastChecked = now;

            if (History.Current == info.Ip && current != null)
            {
                current.LastSeen = now;
                current.Org ??= info.Org; current.City ??= info.City; current.Country ??= info.Country;
                await Save(commitMessage: null);
                Raise(null, null);
                return;
            }

            bool first = History.Current == null;
            if (previous != null) previous.LastSeen = now;
            if (current == null)
            {
                current = new IpRecord { Ip = info.Ip, FirstSeen = now, LastSeen = now, Org = info.Org, City = info.City, Country = info.Country };
                History.Records.Insert(0, current);
            }
            else
            {
                current.LastSeen = now;
                current.Times++;
                History.Records.Remove(current);
                History.Records.Insert(0, current);
            }
            History.Current = info.Ip;

            var msg = first ? $"Start public IP history at {info.Ip}" : $"Public IP changed from {previous?.Ip ?? "unknown"} to {info.Ip}";
            await Save(msg);
            Activity.Add(ActivityKind.Network, first ? "Public IP recorded" : "Public IP changed", first ? info.Ip : $"{previous?.Ip} → {info.Ip}{(string.IsNullOrEmpty(current.Org) ? "" : " · " + current.Org)}");
            Raise(first ? null : previous, first ? null : current);
        }

        private void Raise(IpRecord prev, IpRecord cur)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                Updated?.Invoke();
                if (prev != null && cur != null) Changed?.Invoke(prev, cur);
            }));
        }

        private sealed class LookupResult { public string Ip, Org, City, Country; }

        private async Task<LookupResult> Lookup()
        {
            try
            {
                var json = await _http.GetStringAsync("https://ipinfo.io/json");
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                string Get(string k) => r.TryGetProperty(k, out var v) ? v.GetString() : null;
                var ip = Get("ip");
                if (System.Net.IPAddress.TryParse(ip, out _))
                {
                    LastError = null;
                    return new LookupResult { Ip = ip, Org = Get("org"), City = Get("city"), Country = Get("country") };
                }
            }
            catch (Exception ex) { LastError = ex.Message; }
            try
            {
                var ip = (await _http.GetStringAsync("https://api.ipify.org")).Trim();
                if (System.Net.IPAddress.TryParse(ip, out _)) { LastError = null; return new LookupResult { Ip = ip }; }
            }
            catch (Exception ex) { LastError = ex.Message; }
            return null;
        }

        // -------------------------------------------------------------- the history repository

        private async Task Save(string commitMessage)
        {
            await _repoGate.WaitAsync();
            try
            {
                File.WriteAllText(JsonPath, JsonSerializer.Serialize(History, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(Path.Combine(RepoDir, "ip-history.md"), Markdown(), new UTF8Encoding(false));
                if (commitMessage == null) return;   // last-seen refreshes are written, and land with the next change
                await Task.Run(() => Commit(commitMessage));
            }
            catch (Exception ex) { Log.Warn("IP history save failed: " + ex.Message); }
            finally { _repoGate.Release(); }
        }

        private string Markdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Public IP history");
            sb.AppendLine();
            sb.AppendLine($"Current: `{History.Current}` · last checked {History.LastChecked:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine("| IP | First seen | Last seen | Times | Network | Place |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in History.Records)
                sb.AppendLine($"| `{r.Ip}` | {r.FirstSeen:yyyy-MM-dd HH:mm} | {r.LastSeen:yyyy-MM-dd HH:mm} | {r.Times} | {r.Org} | {r.Place} |");
            return sb.ToString();
        }

        private static void Commit(string message)
        {
            var dir = RepoDir;
            if (!Directory.Exists(Path.Combine(dir, ".git")))
            {
                GitPush.Git(dir, "init -b main");
                GitPush.Git(dir, "config user.name \"Monitoring Tools\"");
                GitPush.Git(dir, "config user.email \"monitoring-tools@localhost\"");
            }
            var remote = Settings.Current.IpHistoryRemote?.Trim();
            if (!string.IsNullOrEmpty(remote))
            {
                if (GitPush.Git(dir, "remote get-url origin").Code != 0) GitPush.Git(dir, $"remote add origin \"{remote}\"");
                else GitPush.Git(dir, $"remote set-url origin \"{remote}\"");
            }
            GitPush.Git(dir, "add -A");
            var msgFile = Path.Combine(Path.GetTempPath(), $"mt-ip-{Guid.NewGuid():N}.txt");
            File.WriteAllText(msgFile, message, new UTF8Encoding(false));
            var c = GitPush.Git(dir, $"commit -F \"{msgFile}\"");
            try { File.Delete(msgFile); } catch { }
            if (c.Code != 0) { Log.Warn("IP history commit: " + c.All); return; }
            if (!string.IsNullOrEmpty(remote))
            {
                var p = GitPush.Git(dir, "push -u origin main");
                if (p.Code != 0) Log.Warn("IP history push failed: " + p.All);
            }
        }

        public static Task<(int Code, string All)> PushNowAsync() => Task.Run(() =>
        {
            var r = GitPush.Git(RepoDir, "push -u origin main");
            return (r.Code, r.All);
        });

        public void Dispose()
        {
            _cts?.Cancel();
            _http.Dispose();
        }
    }
}
