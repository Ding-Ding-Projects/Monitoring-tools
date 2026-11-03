using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MonitoringTools.Core
{
    public sealed class Settings
    {
        // Clipboard
        public bool ClipboardEnabled { get; set; } = true;
        public double ToastSeconds { get; set; } = 3.0;
        public bool KeepOpenOnHover { get; set; } = true;
        public bool RenderMarkdown { get; set; } = true;
        public bool IgnoreSensitiveSources { get; set; } = true;
        public int HistorySize { get; set; } = 50;

        // Network
        public bool NetworkEnabled { get; set; } = true;
        public int ProbeIntervalSeconds { get; set; } = 3;
        public int FailuresBeforeAlert { get; set; } = 2;

        // Public IP watch and its git history
        public bool PublicIpEnabled { get; set; } = true;
        public int PublicIpIntervalMinutes { get; set; } = 5;
        public string IpHistoryRepo { get; set; } = "";
        public string IpHistoryRemote { get; set; } = "";

        // Game mode
        public bool GameModeEnabled { get; set; } = true;
        public bool GameModeFullscreen { get; set; } = true;
        public List<string> GameProcesses { get; set; } = new List<string>(GameDefaults);

        // Quick commands and media
        public bool QuickCommandsEnabled { get; set; } = true;
        public string FfmpegPath { get; set; } = "";
        public string MediaSaveFolder { get; set; } = "";

        // Custom shortcuts and git push
        public List<Shortcut> Shortcuts { get; set; } = new List<Shortcut>();
        public string CommitTemplate { get; set; } = "";

        // File tools
        public List<string> RecentFolders { get; set; } = new List<string>();

        // DNS profiles
        public List<DnsProfile> DnsProfiles { get; set; } = DnsProfile.Defaults();

        // First-run setup, palette and sound
        public int OnboardingStep { get; set; } = 0;
        public bool OnboardingDone { get; set; } = false;
        public bool PaletteFullWindow { get; set; } = false;
        public bool SoundsEnabled { get; set; } = true;
        public double SoundVolume { get; set; } = 0.6;

        // Service
        public bool StartWithWindows { get; set; } = true;
        public bool RunElevated { get; set; } = false;

        public static readonly string[] GameDefaults =
        {
            "RobloxPlayerBeta", "RobloxPlayer", "RobloxStudioBeta",
            "Minecraft.Windows", "MinecraftLauncher", "javaw:Minecraft",
            "League of Legends", "LeagueClient", "LeagueClientUx",
            "VALORANT-Win64-Shipping", "FortniteClient-Win64-Shipping",
            "cs2", "dota2", "r5apex", "RocketLeague", "Overwatch", "GTA5", "eldenring"
        };

        public static string DataDir
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitoringTools");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string FilePath => Path.Combine(DataDir, "settings.json");
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true };

        public static Settings Current { get; private set; } = new Settings();
        public static event Action Changed;

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Current = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
            catch (Exception ex)
            {
                Log.Warn("Settings unreadable, using defaults: " + ex.Message);
                Current = new Settings();
            }
            if (Current.ToastSeconds < 1) Current.ToastSeconds = 3;
            if (Current.ProbeIntervalSeconds < 1) Current.ProbeIntervalSeconds = 3;
            if (Current.FailuresBeforeAlert < 1) Current.FailuresBeforeAlert = 2;
            if (Current.GameProcesses == null) Current.GameProcesses = new List<string>(GameDefaults);
            if (Current.Shortcuts == null) Current.Shortcuts = new List<Shortcut>();
            if (Current.DnsProfiles == null || Current.DnsProfiles.Count == 0) Current.DnsProfiles = DnsProfile.Defaults();
            if (Current.CommitTemplate == null) Current.CommitTemplate = "";
        }

        public static void Save()
        {
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, Json)); }
            catch (Exception ex) { Log.Warn("Settings save failed: " + ex.Message); }
            Changed?.Invoke();
        }

        public string ResolvedMediaFolder
        {
            get
            {
                var dir = string.IsNullOrWhiteSpace(MediaSaveFolder)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Monitoring Tools")
                    : MediaSaveFolder;
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }
}
