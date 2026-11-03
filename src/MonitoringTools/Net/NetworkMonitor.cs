using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MonitoringTools.Core;

namespace MonitoringTools.Net
{
    public enum StageResult { Ok, Warn, Fail, Skipped }
    public enum Cause { None, NoAdapter, NoAddress, NoGateway, GatewayDown, DnsFailing, CaptivePortal, Upstream }

    public sealed class Stage
    {
        public string Name;
        public StageResult Result;
        public string Value;
    }

    public sealed class NetworkState
    {
        public bool Online;
        public Cause Cause;
        public string Headline;
        public string Explanation;
        public List<Stage> Stages = new List<Stage>();
        public List<(string Label, string Value)> Details = new List<(string, string)>();
        public DateTime CheckedAt = DateTime.Now;
        public DateTime? DownSince;
        public long GatewayMs = -1;
        public long DnsMs = -1;

        public string DetailsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Monitoring Tools network report, {CheckedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(Headline);
            sb.AppendLine(Explanation);
            foreach (var s in Stages) sb.AppendLine($"  {s.Name}: {s.Result} {s.Value}");
            foreach (var d in Details) sb.AppendLine($"  {d.Label}: {d.Value}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Probes the path to the internet in order (adapter, address, gateway, DNS, internet) so the
    /// dialog can say exactly where it broke. Declares an outage only after N failed rounds in a row.
    /// </summary>
    public sealed class NetworkMonitor : IDisposable
    {
        public event Action<NetworkState> StateChanged;
        public event Action Sampled;
        public NetworkState Current { get; private set; }
        public DateTime StartedAt { get; } = DateTime.Now;
        public int ProbeCount { get; private set; }

        /// <summary>Recent probe results for the latency chart: gateway and DNS milliseconds (-1 when silent or failed).</summary>
        public readonly List<(DateTime At, long Gateway, long Dns, bool Online)> Samples = new List<(DateTime, long, long, bool)>();

        public void Seed(IEnumerable<(DateTime, long, long, bool)> samples)
        {
            lock (Samples) Samples.AddRange(samples);
        }

        private readonly HttpClient _http;
        private CancellationTokenSource _cts;
        private int _failStreak;
        private bool _reportedOffline;
        private DateTime? _downSince;
        private readonly SemaphoreSlim _probeNow = new SemaphoreSlim(0);

        public NetworkMonitor()
        {
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = true }) { Timeout = TimeSpan.FromSeconds(4) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MonitoringTools/1.0");
            NetworkChange.NetworkAvailabilityChanged += (s, e) => ProbeNow();
            NetworkChange.NetworkAddressChanged += (s, e) => ProbeNow();
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = Loop(_cts.Token);
        }

        public void ProbeNow() { try { _probeNow.Release(); } catch { } }

        public int SecondsUntilNextProbe { get; private set; }

        private async Task Loop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NetworkState state;
                try { state = await Probe(); }
                catch (Exception ex) { Log.Warn("Probe crashed: " + ex.Message); state = new NetworkState { Online = true }; }
                ProbeCount++;
                lock (Samples)
                {
                    Samples.Add((DateTime.Now, state.GatewayMs, state.DnsMs, state.Online));
                    while (Samples.Count > 120) Samples.RemoveAt(0);
                }
                Application.Current?.Dispatcher.BeginInvoke(new Action(() => Sampled?.Invoke()));

                if (state.Online)
                {
                    _failStreak = 0;
                    var wasOffline = _reportedOffline;
                    _reportedOffline = false;
                    state.DownSince = _downSince;
                    _downSince = null;
                    Publish(state, force: wasOffline || Current == null);
                }
                else
                {
                    _failStreak++;
                    _downSince ??= DateTime.Now;
                    state.DownSince = _downSince;
                    if (_failStreak >= Settings.Current.FailuresBeforeAlert || _reportedOffline)
                    {
                        _reportedOffline = true;
                        Publish(state, force: true);
                    }
                }

                int interval = Math.Max(1, Settings.Current.ProbeIntervalSeconds);
                for (int i = interval; i > 0 && !ct.IsCancellationRequested; i--)
                {
                    SecondsUntilNextProbe = i;
                    if (await _probeNow.WaitAsync(1000, ct).ContinueWith(t => !t.IsCanceled && t.Result)) break;
                }
            }
        }

        private void Publish(NetworkState s, bool force)
        {
            var changed = Current == null || Current.Online != s.Online || Current.Cause != s.Cause;
            Current = s;
            if (changed || force)
                Application.Current?.Dispatcher.BeginInvoke(new Action(() => StateChanged?.Invoke(s)));
        }

        // -------------------------------------------------------------- the ladder

        public async Task<NetworkState> Probe()
        {
            var st = new NetworkState();
            var all = NetworkInterface.GetAllNetworkInterfaces();
            var up = all.Where(n => n.OperationalStatus == OperationalStatus.Up
                                    && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                    && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel).ToList();

            // Stage 1: adapter
            if (up.Count == 0)
            {
                st.Stages.Add(new Stage { Name = "Adapter", Result = StageResult.Fail, Value = "down" });
                AddSkipped(st, "Gateway", "DNS", "Internet");
                return Diagnose(st, Cause.NoAdapter, "No network connection",
                    "Wi-Fi is off or disconnected, the cable is unplugged, or airplane mode is on.", null);
            }

            var primary = up.FirstOrDefault(n => n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork)) ?? up[0];
            var ip = primary.GetIPProperties();
            var v4 = ip.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
            var gw = ip.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))?.Address;
            st.Stages.Add(new Stage { Name = "Adapter", Result = StageResult.Ok, Value = "up" });

            // Stage 2: address and gateway
            if (v4 == null || v4.ToString().StartsWith("169.254."))
            {
                AddDetails(st, primary, v4, gw);
                st.Stages.Add(new Stage { Name = "Gateway", Result = StageResult.Fail, Value = "no IP" });
                AddSkipped(st, "DNS", "Internet");
                return Diagnose(st, Cause.NoAddress, "No IP address from the router",
                    "The adapter is connected but DHCP did not hand out an address. Release and renew usually fixes this.", primary);
            }
            if (gw == null)
            {
                AddDetails(st, primary, v4, null);
                st.Stages.Add(new Stage { Name = "Gateway", Result = StageResult.Fail, Value = "none" });
                AddSkipped(st, "DNS", "Internet");
                return Diagnose(st, Cause.NoGateway, "No route to the internet",
                    "The adapter has an address but no default gateway, so traffic has nowhere to go.", primary);
            }

            var gwMs = await PingMs(gw, 900);
            st.GatewayMs = gwMs;
            // Many routers drop pings, so a silent gateway is a warning, not a verdict.
            st.Stages.Add(new Stage { Name = "Gateway", Result = gwMs >= 0 ? StageResult.Ok : StageResult.Warn, Value = gwMs >= 0 ? $"{gwMs} ms" : "silent" });

            // Stage 3: DNS
            var dnsSw = Stopwatch.StartNew();
            bool dnsOk = await Resolves("www.msftconnecttest.com", 2500) || await Resolves("one.one.one.one", 2000);
            st.DnsMs = dnsOk ? dnsSw.ElapsedMilliseconds : -1;

            // Stage 4: internet by raw IP, independent of DNS
            bool ipReach = await TcpReach(new IPEndPoint(IPAddress.Parse("1.1.1.1"), 443), 2000)
                           || await TcpReach(new IPEndPoint(IPAddress.Parse("8.8.8.8"), 443), 2000);

            string http = dnsOk ? await HttpCheck() : "skipped";

            st.Stages.Add(new Stage { Name = "DNS", Result = dnsOk ? StageResult.Ok : StageResult.Fail, Value = dnsOk ? $"{st.DnsMs} ms" : "timeout" });
            AddDetails(st, primary, v4, gw);

            if (http == "portal")
            {
                st.Stages.Add(new Stage { Name = "Internet", Result = StageResult.Warn, Value = "sign-in" });
                return Diagnose(st, Cause.CaptivePortal, "Sign-in required",
                    "This network shows a sign-in page before it lets traffic through (hotel, airport, café Wi-Fi).", primary);
            }
            if (http == "ok" || (dnsOk && ipReach))
            {
                st.Stages.Add(new Stage { Name = "Internet", Result = StageResult.Ok, Value = "reachable" });
                st.Online = true;
                st.Headline = "Online";
                return st;
            }
            if (!dnsOk && ipReach)
            {
                st.Stages.Add(new Stage { Name = "Internet", Result = StageResult.Ok, Value = "by IP" });
                return Diagnose(st, Cause.DnsFailing, "Internet connection lost",
                    "The line is up, but DNS lookups are failing, so websites by name will not load. Switching DNS usually fixes this.", primary);
            }
            st.Stages.Add(new Stage { Name = "Internet", Result = StageResult.Fail, Value = "unreachable" });
            if (gwMs < 0)
                return Diagnose(st, Cause.GatewayDown, "Router is not responding",
                    "Your computer is connected, but the router does not answer and nothing gets past it. Try restarting the router.", primary);
            return Diagnose(st, Cause.Upstream, "Internet connection lost",
                "Your router answers, but nothing beyond it does. This is usually an ISP or modem outage.", primary);
        }

        private static NetworkState Diagnose(NetworkState st, Cause c, string head, string why, NetworkInterface nic)
        {
            st.Online = false; st.Cause = c; st.Headline = head; st.Explanation = why;
            if (nic != null && st.Details.Count == 0) AddDetails(st, nic, null, null);
            return st;
        }

        private static void AddSkipped(NetworkState st, params string[] names)
        {
            foreach (var n in names) st.Stages.Add(new Stage { Name = n, Result = StageResult.Skipped, Value = "skipped" });
        }

        private static void AddDetails(NetworkState st, NetworkInterface nic, IPAddress v4, IPAddress gw)
        {
            st.Details.Clear();
            var ip = nic.GetIPProperties();
            string kind = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" :
                          nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? "Ethernet" : nic.NetworkInterfaceType.ToString();
            st.Details.Add(("Adapter", $"{kind} · {nic.Description}"));
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                st.Details.Add(("Network", WifiName() ?? "unknown"));
            else
                st.Details.Add(("Link speed", nic.Speed > 0 ? $"{nic.Speed / 1_000_000} Mbps" : "unknown"));
            v4 ??= ip.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
            st.Details.Add(("IPv4", v4?.ToString() ?? "none"));
            st.Details.Add(("Gateway", gw?.ToString() ?? "none"));
            var dns = ip.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).Select(d => d.ToString()).ToList();
            st.Details.Add(("DNS servers", dns.Count == 0 ? "none" : string.Join(", ", dns)));
            var vpn = NetworkInterface.GetAllNetworkInterfaces().Any(n => n.OperationalStatus == OperationalStatus.Up &&
                        (n.NetworkInterfaceType == NetworkInterfaceType.Ppp || n.Description.IndexOf("VPN", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.Description.IndexOf("WireGuard", StringComparison.OrdinalIgnoreCase) >= 0 || n.Description.IndexOf("TAP", StringComparison.OrdinalIgnoreCase) >= 0));
            if (vpn) st.Details.Add(("VPN", "active"));
        }

        private static string _wifiCache;
        private static DateTime _wifiAt;

        private static string WifiName()
        {
            if ((DateTime.UtcNow - _wifiAt).TotalSeconds < 20) return _wifiCache;
            _wifiAt = DateTime.UtcNow;
            try
            {
                var p = Process.Start(new ProcessStartInfo("netsh", "wlan show interfaces")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
                var text = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                var m = Regex.Match(text, @"^\s*SSID\s*:\s*(.+)$", RegexOptions.Multiline);
                _wifiCache = m.Success ? m.Groups[1].Value.Trim() : null;
            }
            catch { _wifiCache = null; }
            return _wifiCache;
        }

        private static async Task<long> PingMs(IPAddress a, int timeout)
        {
            try
            {
                using var p = new Ping();
                var r = await p.SendPingAsync(a, timeout);
                return r.Status == IPStatus.Success ? r.RoundtripTime : -1;
            }
            catch { return -1; }
        }

        private static async Task<bool> Resolves(string host, int timeoutMs)
        {
            try
            {
                var t = Dns.GetHostAddressesAsync(host);
                var done = await Task.WhenAny(t, Task.Delay(timeoutMs));
                return done == t && t.Status == TaskStatus.RanToCompletion && t.Result.Length > 0;
            }
            catch { return false; }
        }

        private static async Task<bool> TcpReach(IPEndPoint ep, int timeoutMs)
        {
            try
            {
                using var c = new TcpClient();
                var t = c.ConnectAsync(ep.Address, ep.Port);
                var done = await Task.WhenAny(t, Task.Delay(timeoutMs));
                return done == t && c.Connected;
            }
            catch { return false; }
        }

        /// <summary>"ok", "portal" or "fail". Windows' own probe URL returns a fixed body.</summary>
        private async Task<string> HttpCheck()
        {
            try
            {
                var r = await _http.GetAsync("http://www.msftconnecttest.com/connecttest.txt");
                var body = await r.Content.ReadAsStringAsync();
                if (r.IsSuccessStatusCode && body.StartsWith("Microsoft Connect Test")) return "ok";
                return (int)r.StatusCode >= 300 && (int)r.StatusCode < 400 || r.IsSuccessStatusCode ? "portal" : "fail";
            }
            catch { return "fail"; }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _http.Dispose();
        }
    }
}
