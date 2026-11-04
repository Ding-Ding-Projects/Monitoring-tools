using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// Sends a command built by the command builder and reads the server's reply. Docker and RCON give the reply back
    /// directly; a server this app runs as a process answers in its console, so the new console lines that follow the
    /// command are read as its reply.
    /// </summary>
    public static class McCommandRun
    {
        /// <summary>Command words that change who may play or stop the server; they ask for the two-key confirmation first.</summary>
        public static readonly string[] Confirmed = { "stop", "ban", "ban-ip", "op", "deop" };

        public static bool NeedsConfirmation(string line)
        {
            var first = (line ?? "").Trim().TrimStart('/').Split(' ')[0];
            return Confirmed.Contains(first);
        }

        /// <summary>Sends the line and returns the reply, or the reason nothing was sent.</summary>
        public static async Task<string> SendAsync(ServerDef s, string line, TimeSpan? wait = null)
        {
            line = (line ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (line.Length == 0) return "There is no command to send.";
            if (McDemo.IsSample(s)) return "Sample server: nothing was sent.";
            var live = McLive.For(s);
            int before = live.Lines().Count;
            var answer = await ServerManager.CommandAsync(s, line);
            if (answer != "Sent." || s.Target == Target.SshDocker || s.Target == Target.LocalDocker) return answer;
            // The reply is in the console: wait for the lines that follow the command.
            var until = DateTime.Now + (wait ?? TimeSpan.FromSeconds(3));
            List<McLogLine> fresh = new List<McLogLine>();
            while (DateTime.Now < until)
            {
                await Task.Delay(150);
                var all = live.Lines();
                if (all.Count < before) before = 0;
                fresh = all.Skip(before).Where(l => !l.Message.Contains("issued server command", StringComparison.Ordinal)).ToList();
                if (fresh.Count > 0) { await Task.Delay(200); fresh = live.Lines().Skip(before).ToList(); break; }
            }
            return fresh.Count == 0 ? "Sent. The server did not answer in the console." : string.Join(" · ", fresh.Take(4).Select(l => l.Message));
        }

        /// <summary>Reads a player's position with data get entity, as numbers; null with the reason when it cannot be read.</summary>
        public static async Task<(double[] Pos, string Problem)> PositionAsync(ServerDef s, string player)
        {
            if (McDemo.IsSample(s)) return (null, "Sample server: positions are not read from a sample.");
            var st = McState.Cached(s).State;
            if (st != McRunState.Running) return (null, "The server is not running, so a player's position cannot be read.");
            var reply = await SendAsync(s, "data get entity " + player + " Pos");
            var pos = McPosition.FromDataReply(reply);
            return pos == null ? (null, "The server did not give a position: " + reply) : (pos, null);
        }
    }
}
