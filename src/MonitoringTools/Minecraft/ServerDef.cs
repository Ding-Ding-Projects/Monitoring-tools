using System;
using System.Collections.Generic;
using System.IO;

namespace MonitoringTools.Minecraft
{
    public enum Target { LocalProcess, LocalDocker, SshProcess, SshDocker }

    // Stored as numbers in servers.json, so new members are only ever appended.
    public enum Flavour { Vanilla, Paper, Fabric, Purpur, Forge, NeoForge }

    /// <summary>
    /// One server the app created. servers.json keeps a list of these; every field added after the first release has a
    /// default, so an older file loads unchanged and gains the new fields the next time it is saved.
    /// </summary>
    public sealed class ServerDef
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        public string Name { get; set; } = "My server";
        public string Version { get; set; }
        public Flavour Flavour { get; set; } = Flavour.Vanilla;
        public Target Target { get; set; } = Target.LocalProcess;
        public int Port { get; set; } = 25565;
        public int MemoryMb { get; set; } = 2048;
        public bool EulaAccepted { get; set; }
        public string SshHost { get; set; } = "";       // user@host[:port]
        public string RemoteDir { get; set; } = "";     // folder on the SSH host
        public DateTime Created { get; set; } = DateTime.Now;

        // ---- added with the server manager remake
        /// <summary>The software build that was installed: a Paper or Purpur build number, a Fabric loader, a Forge or NeoForge version. Empty for Vanilla.</summary>
        public string Build { get; set; } = "";
        /// <summary>Where backups go. Empty means a folder for this server under the app's "minecraft/backups" folder.</summary>
        public string BackupDir { get; set; } = "";
        /// <summary>How many of the newest backups are kept; older ones are removed after each new backup.</summary>
        public int KeepBackups { get; set; } = 14;
        public List<McSchedule> Schedules { get; set; } = new List<McSchedule>();
        /// <summary>The last commands sent from the command builder, newest first.</summary>
        public List<string> RecentCommands { get; set; } = new List<string>();

        public string Folder => Path.Combine(ServerManager.Root, Id);
        public string ContainerName => "monitoring-tools-mc-" + Id;
        public string RemoteFolder => string.IsNullOrWhiteSpace(RemoteDir) ? $"monitoring-tools-mc/{Id}" : RemoteDir.Trim();
        public bool IsLocal => Target == Target.LocalProcess || Target == Target.LocalDocker;
        public bool IsSsh => Target == Target.SshProcess || Target == Target.SshDocker;
        public bool IsDocker => Target == Target.LocalDocker || Target == Target.SshDocker;
        /// <summary>Paper and Purpur take Bukkit plugins; Fabric, Forge and NeoForge take mods; Vanilla takes neither.</summary>
        public string ContentFolder => Flavour switch { Flavour.Paper or Flavour.Purpur => "plugins", Flavour.Fabric or Flavour.Forge or Flavour.NeoForge => "mods", _ => null };
        public string BackupFolder => string.IsNullOrWhiteSpace(BackupDir) ? Path.Combine(ServerManager.Root, "backups", Id) : BackupDir.Trim();
    }

    public sealed class McVersion
    {
        public string Id, Type, Url, Sha1;
        public DateTime Released;
    }
}
