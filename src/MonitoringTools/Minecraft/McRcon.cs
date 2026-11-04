using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// The Source RCON protocol a Java server speaks when enable-rcon is on. Used for servers this app did not start, whose
    /// console input belongs to another program: an adopted process takes commands, including a graceful stop, only this way.
    /// </summary>
    public static class McRcon
    {
        public sealed class Endpoint { public int Port; public string Password; }

        /// <summary>The RCON port and password from the server's own server.properties, or null when RCON is off or has no password.</summary>
        public static Endpoint From(ServerDef s)
        {
            var p = ServerManager.ReadProperties(s);
            if (!p.TryGetValue("enable-rcon", out var on) || !string.Equals(on, "true", StringComparison.OrdinalIgnoreCase)) return null;
            if (!p.TryGetValue("rcon.password", out var pw) || string.IsNullOrEmpty(pw)) return null;
            int port = p.TryGetValue("rcon.port", out var ps) && int.TryParse(ps, out var n) ? n : 25575;
            return new Endpoint { Port = port, Password = pw };
        }

        public static async Task<string> SendAsync(int port, string password, string command, int timeoutMs = 6000)
        {
            using var tcp = new TcpClient();
            var connect = tcp.ConnectAsync("127.0.0.1", port);
            if (await Task.WhenAny(connect, Task.Delay(timeoutMs)) != connect) throw new TimeoutException("RCON did not answer on port " + port + ".");
            await connect;
            tcp.ReceiveTimeout = timeoutMs; tcp.SendTimeout = timeoutMs;
            var stream = tcp.GetStream();
            await Write(stream, 1, 3, password);
            var (id, _, _) = await Read(stream);
            if (id == -1) throw new UnauthorizedAccessException("The RCON password in server.properties was refused.");
            await Write(stream, 2, 2, command);
            var (_, _, body) = await Read(stream);
            return body;
        }

        private static async Task Write(Stream s, int id, int type, string body)
        {
            var b = Encoding.UTF8.GetBytes(body);
            var packet = new byte[14 + b.Length];
            BitConverter.GetBytes(10 + b.Length).CopyTo(packet, 0);
            BitConverter.GetBytes(id).CopyTo(packet, 4);
            BitConverter.GetBytes(type).CopyTo(packet, 8);
            b.CopyTo(packet, 12);
            await s.WriteAsync(packet);
        }

        private static async Task<(int Id, int Type, string Body)> Read(Stream s)
        {
            var len = BitConverter.ToInt32(await Exactly(s, 4), 0);
            if (len < 10 || len > 1 << 20) throw new InvalidDataException("RCON sent a packet of an impossible size.");
            var rest = await Exactly(s, len);
            return (BitConverter.ToInt32(rest, 0), BitConverter.ToInt32(rest, 4), Encoding.UTF8.GetString(rest, 8, len - 10));
        }

        private static async Task<byte[]> Exactly(Stream s, int n)
        {
            var buf = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = await s.ReadAsync(buf.AsMemory(got, n - got));
                if (r == 0) throw new EndOfStreamException("RCON closed the connection.");
                got += r;
            }
            return buf;
        }
    }
}
