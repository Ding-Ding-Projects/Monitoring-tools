using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    public enum McRunState { Checking, Running, Starting, Stopped, HostOffline, Missing }

    public sealed class McStatus
    {
        public McRunState State;
        public string Detail = "";
        public DateTime At = DateTime.Now;
        public string Label => McState.Label(State);
    }

    /// <summary>
    /// Reads whether a server is running from the place it runs. An SSH host that cannot be reached is reported as
    /// "Host offline", never as stopped: ssh exits with 255 when the connection itself fails, and only an answer from
    /// the host can say the server is stopped.
    /// </summary>
    public static class McState
    {
        private static readonly Dictionary<string, McStatus> Last = new Dictionary<string, McStatus>();
        public static event Action<string> Changed;

        public static string Label(McRunState s) => s switch
        {
            McRunState.Running => "Running", McRunState.Starting => "Starting", McRunState.Stopped => "Stopped",
            McRunState.HostOffline => "Host offline", McRunState.Missing => "Missing", _ => "Checking",
        };

        /// <summary>The last known state, without asking the host again.</summary>
        public static McStatus Cached(ServerDef s)
        {
            lock (Last) return Last.TryGetValue(s.Id, out var st) ? st : new McStatus { State = McRunState.Checking, Detail = "Checking…" };
        }

        /// <summary>Sets a known state, used for the sample servers of the capture run and after the app's own start and stop.</summary>
        public static void Set(ServerDef s, McRunState state, string detail = "")
        {
            lock (Last) Last[s.Id] = new McStatus { State = state, Detail = detail };
            Changed?.Invoke(s.Id);
        }

        public static async Task<McStatus> ProbeAsync(ServerDef s)
        {
            if (McDemo.IsSample(s)) return Cached(s);
            var st = await Task.Run(() => Probe(s));
            if (st.State == McRunState.Running && McLive.For(s).Starting) st.State = McRunState.Starting;
            if (st.State != McRunState.Running && st.State != McRunState.Starting && McLive.For(s).Starting && (DateTime.Now - (McLive.For(s).StartingSince ?? DateTime.Now)).TotalSeconds < 90 && st.State != McRunState.HostOffline)
                st.State = McRunState.Starting;   // the process or container is still coming up
            lock (Last) Last[s.Id] = st;
            Changed?.Invoke(s.Id);
            return st;
        }

        private static McStatus Probe(ServerDef s)
        {
            switch (s.Target)
            {
                case Target.LocalProcess:
                    return new McStatus { State = ServerManager.ProcessOf(s) != null ? McRunState.Running : McRunState.Stopped, Detail = "a process on this computer" };
                case Target.LocalDocker:
                {
                    var info = ServerManager.Run("docker", "info --format \"{{.ServerVersion}}\"", 15000);
                    if (info.Code != 0) return new McStatus { State = McRunState.HostOffline, Detail = "Docker is not running on this computer." };
                    var r = ServerManager.Run("docker", $"inspect -f \"{{{{.State.Status}}}}\" {s.ContainerName}", 15000);
                    if (r.Code != 0) return new McStatus { State = McRunState.Missing, Detail = "The container is not on this computer any more." };
                    return FromDocker(r.Out.Trim());
                }
                case Target.SshProcess:
                {
                    var r = ServerManager.Ssh(s, $"cd {ServerManager.Sh(s.RemoteFolder)} 2>/dev/null && test -f server.pid && kill -0 $(head -n1 server.pid) 2>/dev/null && echo running || echo stopped", 25000);
                    if (r.Code == 255 || r.Code == -1) return new McStatus { State = McRunState.HostOffline, Detail = "The SSH host could not be reached: " + ServerManager.ActionLine(r.Out) };
                    return new McStatus { State = r.Out.Contains("running") ? McRunState.Running : McRunState.Stopped, Detail = "a process on " + s.SshHost };
                }
                case Target.SshDocker:
                {
                    var r = ServerManager.Ssh(s, $"docker inspect -f '{{{{.State.Status}}}}' {s.ContainerName}", 25000);
                    if (r.Code == 255 || r.Code == -1) return new McStatus { State = McRunState.HostOffline, Detail = "The SSH host could not be reached: " + ServerManager.ActionLine(r.Out) };
                    if (r.Code != 0) return new McStatus { State = McRunState.Missing, Detail = "The container is not on the host any more." };
                    return FromDocker(r.Out.Trim());
                }
            }
            return new McStatus { State = McRunState.Checking };
        }

        private static McStatus FromDocker(string status) => status switch
        {
            "running" => new McStatus { State = McRunState.Running, Detail = "container running" },
            "restarting" => new McStatus { State = McRunState.Starting, Detail = "container restarting" },
            _ => new McStatus { State = McRunState.Stopped, Detail = "container " + status },
        };
    }
}
