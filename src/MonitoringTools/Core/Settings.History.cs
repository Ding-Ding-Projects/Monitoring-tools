namespace MonitoringTools.Core
{
    public sealed partial class Settings
    {
        // Local history: how saved versions of scripts and templates are protected and how long they are kept.

        /// <summary>"none", "windows" (key protected for this Windows account) or "password" (key derived from a password). See HistoryVault.</summary>
        public string HistoryProtection { get; set; } = "none";

        /// <summary>The most versions kept per item, 5 to 500.</summary>
        public int HistoryKeepCount { get; set; } = 50;

        /// <summary>Versions older than this many days are dropped (the newest always stays). 0 keeps them for ever.</summary>
        public int HistoryKeepDays { get; set; } = 0;
    }
}
