using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public sealed class DnsProfile
    {
        public string Name { get; set; } = "";
        public string Primary { get; set; } = "";
        public string Secondary { get; set; } = "";
        public bool IsAutomatic => string.IsNullOrWhiteSpace(Primary);

        public static List<DnsProfile> Defaults() => new List<DnsProfile>
        {
            new DnsProfile { Name = "Automatic" },
            new DnsProfile { Name = "Google", Primary = "8.8.8.8", Secondary = "8.8.4.4" },
            new DnsProfile { Name = "Cloudflare", Primary = "1.1.1.1", Secondary = "1.0.0.1" },
            new DnsProfile { Name = "Quad9", Primary = "9.9.9.9", Secondary = "149.112.112.112" },
            new DnsProfile { Name = "OpenDNS", Primary = "208.67.222.222", Secondary = "208.67.220.220" },
        };

        public override string ToString() => IsAutomatic ? "Automatic (from router)" : $"{Primary}{(string.IsNullOrWhiteSpace(Secondary) ? "" : " · " + Secondary)}";
    }

    public static class DnsSwitcher
    {
        /// <summary>The adapters that currently carry traffic: up, not loopback or tunnel, with a gateway.</summary>
        public static List<NetworkInterface> ActiveAdapters() =>
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                            && n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                .ToList();

        public static string CurrentServers()
        {
            var a = ActiveAdapters().FirstOrDefault();
            if (a == null) return "none";
            var list = a.GetIPProperties().DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).Select(d => d.ToString()).ToList();
            return list.Count == 0 ? "none" : string.Join(" · ", list);
        }

        public static string ActiveProfileName()
        {
            var a = ActiveAdapters().FirstOrDefault();
            if (a == null) return null;
            var servers = a.GetIPProperties().DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).Select(d => d.ToString()).ToList();
            foreach (var p in Settings.Current.DnsProfiles.Where(p => !p.IsAutomatic))
                if (servers.Count > 0 && servers[0] == p.Primary) return p.Name;
            return "Automatic";
        }

        /// <summary>Applies a profile to every active adapter. Needs admin, so it prompts once when not elevated.</summary>
        public static Task<int> ApplyAsync(DnsProfile profile)
        {
            var adapters = ActiveAdapters();
            if (adapters.Count == 0)
            {
                // Offline with no gateway: apply to every adapter that is up
                adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToList();
            }
            var cmds = new List<string>();
            foreach (var a in adapters)
            {
                var name = a.Name.Replace("\"", "");
                if (profile.IsAutomatic)
                    cmds.Add($"netsh interface ipv4 set dnsservers name=\"{name}\" source=dhcp");
                else
                {
                    cmds.Add($"netsh interface ipv4 set dnsservers name=\"{name}\" static {profile.Primary} primary validate=no");
                    if (!string.IsNullOrWhiteSpace(profile.Secondary))
                        cmds.Add($"netsh interface ipv4 add dnsservers name=\"{name}\" {profile.Secondary} index=2 validate=no");
                }
            }
            cmds.Add("ipconfig /flushdns");
            Log.Info("Applying DNS profile " + profile.Name);
            return Elevated.RunAsync(cmds.ToArray());
        }
    }
}
