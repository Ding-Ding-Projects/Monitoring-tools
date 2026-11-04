using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// What is known about a server right now: its recent console lines, the players its last "list" answer named, the
    /// ticks per second Paper reported, memory and uptime. Every value carries the reason it is missing instead of a guess.
    /// </summary>
    public sealed class McLive
    {
        private static readonly Dictionary<string, McLive> All = new Dictionary<string, McLive>();
        private const int Keep = 2000;

        public string ServerId { get; private set; }
        private readonly List<McLogLine> _lines = new List<McLogLine>();
        public bool Starting { get; private set; }
        public DateTime? StartingSince { get; private set; }
        public string LastFailure { get; private set; }
        public DateTime? LastFailureAt { get; private set; }
        public int? PlayersOnline { get; private set; }
        public int? PlayersMax { get; private set; }
        public string[] PlayerNames { get; private set; } = Array.Empty<string>();
        public DateTime? PlayersAt { get; private set; }
        public double? Tps { get; private set; }
        public long? MemoryBytes { get; private set; }
        public string MemoryNote { get; private set; } = "";
        public DateTime? StartedAt { get; private set; }
        public string UptimeNote { get; private set; } = "";
        private DateTime _lastList = DateTime.MinValue, _lastTps = DateTime.MinValue;

        /// <summary>Raised on any thread when a line or a value changes.</summary>
        public static event Action<string> Updated;

        static McLive()
        {
            ServerManager.Output += (id, line) => ForId(id).Add(line);
        }

        public static McLive For(ServerDef s) => ForId(s.Id);

        public static McLive ForId(string id)
        {
            lock (All)
            {
                if (!All.TryGetValue(id, out var l)) All[id] = l = new McLive { ServerId = id };
                return l;
            }
        }

        public List<McLogLine> Lines() { lock (_lines) return _lines.ToList(); }

        public void Add(string raw)
        {
            if (raw == null) return;
            var l = McLogLine.Parse(raw);
            lock (_lines)
            {
                _lines.Add(l);
                if (_lines.Count > Keep) _lines.RemoveRange(0, _lines.Count - Keep);
            }
            Learn(l.Message);
            if (IsReady(l.Message)) { Starting = false; StartingSince = null; }
            if (raw.StartsWith("[server exited with code", StringComparison.Ordinal))
            {
                bool failed = Starting || !raw.Contains("code 0");
                Starting = false; StartingSince = null; PlayersOnline = null; Tps = null;
                if (failed && !raw.Contains("code 0")) Failed("The server stopped on its own: " + raw.Trim('[', ']'));
            }
            Updated?.Invoke(ServerId);
        }

        /// <summary>Replaces the buffer with a fresh tail read from a container or an SSH host, which carries its own history.</summary>
        public void ReplaceWith(string tail)
        {
            var parsed = (tail ?? "").Split('\n').Where(x => x.Trim().Length > 0).Select(McLogLine.Parse).ToList();
            lock (_lines) { _lines.Clear(); _lines.AddRange(parsed.TakeLast(Keep)); }
            foreach (var l in parsed.TakeLast(60)) Learn(l.Message);
            if (parsed.Any(l => IsReady(l.Message))) { Starting = false; StartingSince = null; }
            Updated?.Invoke(ServerId);
        }

        /// <summary>The line a server prints once players can join: "Done (6.2s)!" on Java, "Server started." on Bedrock.</summary>
        public static bool IsReady(string message) => message != null && (message.StartsWith("Done (", StringComparison.Ordinal) || message.StartsWith("Server started.", StringComparison.Ordinal));

        /// <summary>Reads the answer to "list" and "tps" wherever it arrives: a console line or a remote console reply.</summary>
        public void Learn(string text)
        {
            if (McLogLine.TryPlayers(text, out var n, out var max, out var names)) { PlayersOnline = n; PlayersMax = max; PlayerNames = names; PlayersAt = DateTime.Now; }
            if (McLogLine.TryTps(text, out var tps)) Tps = tps;
            var g = GameRuleAnswer.Match(text ?? "");
            if (g.Success) lock (GameRules) GameRules[g.Groups[1].Value] = g.Groups[2].Value.Trim();
        }

        // "Gamerule keepInventory is currently set to: false"
        private static readonly System.Text.RegularExpressions.Regex GameRuleAnswer = new System.Text.RegularExpressions.Regex(@"Gamerule (\S+) is currently set to: (\S+)", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>Game rule values the running server reported in answer to "gamerule &lt;name&gt;".</summary>
        public readonly Dictionary<string, string> GameRules = new Dictionary<string, string>(StringComparer.Ordinal);

        public void MarkStarting() { Starting = true; StartingSince = DateTime.Now; LastFailure = null; Updated?.Invoke(ServerId); }
        public void Failed(string why) { Starting = false; StartingSince = null; LastFailure = why; LastFailureAt = DateTime.Now; Updated?.Invoke(ServerId); }
        public void Stopped() { Starting = false; StartingSince = null; PlayersOnline = null; Tps = null; MemoryBytes = null; StartedAt = null; Updated?.Invoke(ServerId); }

        /// <summary>Sets values directly; used only by the sample servers of the capture run and the self-test.</summary>
        internal void SetSample(int online, int max, string[] names, double? tps, long memory, DateTime started)
        {
            PlayersOnline = online; PlayersMax = max; PlayerNames = names; PlayersAt = DateTime.Now; Tps = tps; MemoryBytes = memory; StartedAt = started;
            MemoryNote = "Java memory in use"; UptimeNote = "since the last start";
        }

        /// <summary>Called while the server runs: asks it for its players and (on Paper and Purpur) its ticks per second, and reads memory and uptime.</summary>
        public async Task PollAsync(ServerDef s)
        {
            if (!s.IsLocal || s.Target == Target.LocalDocker)
            {
                var tail = await ServerManager.LogTailAsync(s, 400);
                if (!string.IsNullOrWhiteSpace(tail)) ReplaceWith(tail);
            }
            if ((DateTime.Now - _lastList).TotalSeconds >= 20)
            {
                _lastList = DateTime.Now;
                Learn(await ServerManager.CommandAsync(s, "list"));
            }
            if (McSoftware.ReportsTps(s.Flavour) && (DateTime.Now - _lastTps).TotalSeconds >= 30)
            {
                _lastTps = DateTime.Now;
                Learn(await ServerManager.CommandAsync(s, "tps"));
            }
            await Task.Run(() => ReadResources(s));
            Updated?.Invoke(ServerId);
        }

        private void ReadResources(ServerDef s)
        {
            try
            {
                switch (s.Target)
                {
                    case Target.LocalProcess:
                    {
                        var p = ServerManager.ProcessOf(s);
                        if (p == null && McDiscovery.ExternalProcess(s) is McProcInfo ext) { try { p = System.Diagnostics.Process.GetProcessById(ext.Pid); } catch { } }
                        if (p == null) { MemoryBytes = null; StartedAt = null; return; }
                        p.Refresh();
                        MemoryBytes = p.WorkingSet64; MemoryNote = s.IsBedrock ? "server process working set" : "Java process working set";
                        StartedAt = p.StartTime; UptimeNote = "since the last start";
                        break;
                    }
                    case Target.LocalDocker:
                    case Target.SshDocker:
                    {
                        var stats = s.Target == Target.LocalDocker
                            ? ServerManager.Run("docker", $"stats --no-stream --format \"{{{{.MemUsage}}}}\" {s.ContainerName}", 20000)
                            : ServerManager.Ssh(s, $"docker stats --no-stream --format '{{{{.MemUsage}}}}' {s.ContainerName}", 30000);
                        MemoryBytes = stats.Code == 0 ? ParseDockerMemory(stats.Out) : null;
                        MemoryNote = MemoryBytes == null ? "Docker did not report memory" : "container memory in use";
                        var started = s.Target == Target.LocalDocker
                            ? ServerManager.Run("docker", $"inspect -f \"{{{{.State.StartedAt}}}}\" {s.ContainerName}", 15000)
                            : ServerManager.Ssh(s, $"docker inspect -f '{{{{.State.StartedAt}}}}' {s.ContainerName}", 30000);
                        StartedAt = started.Code == 0 && DateTime.TryParse(started.Out.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t.ToLocalTime() : null;
                        UptimeNote = StartedAt == null ? "Docker did not report a start time" : "since the container started";
                        break;
                    }
                    case Target.SshProcess:
                    {
                        var r = ServerManager.Ssh(s, $"cd {ServerManager.Sh(s.RemoteFolder)} && ps -o rss=,etimes= -p $(head -n1 server.pid)", 30000);
                        var parts = r.Code == 0 ? r.Out.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
                        if (parts.Length >= 2 && long.TryParse(parts[0], out var rss) && long.TryParse(parts[1], out var secs))
                        { MemoryBytes = rss * 1024; StartedAt = DateTime.Now.AddSeconds(-secs); MemoryNote = "process memory on the host"; UptimeNote = "since the last start"; }
                        else { MemoryBytes = null; StartedAt = null; MemoryNote = UptimeNote = "the host did not report it"; }
                        break;
                    }
                }
            }
            catch (Exception ex) { MemoryNote = ex.Message; }
        }

        /// <summary>"2.1GiB / 4GiB" from docker stats, as bytes used.</summary>
        internal static long? ParseDockerMemory(string text)
        {
            var used = (text ?? "").Split('/')[0].Trim();
            var num = new string(used.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
            if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return null;
            var unit = used[num.Length..].Trim().ToUpperInvariant();
            double mul = unit.StartsWith("G") ? 1024d * 1024 * 1024 : unit.StartsWith("M") ? 1024d * 1024 : unit.StartsWith("K") ? 1024 : 1;
            return (long)(v * mul);
        }
    }
}
