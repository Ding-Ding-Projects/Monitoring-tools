namespace MonitoringTools.Core
{
    /// <summary>Settings for copied install commands (choco and winget). Automatic mode is opt-in and off by default.</summary>
    public sealed partial class Settings
    {
        /// <summary>Run a copied plain install command by itself after a short delay with a Cancel button. Off by default.</summary>
        public bool AutoInstallCommands { get; set; } = false;
        /// <summary>Seconds shown on the Cancel toast before an automatic install starts. 3 to 60.</summary>
        public int AutoInstallDelaySeconds { get; set; } = 10;
        /// <summary>Most packages one automatically installed command may name. 1 to 10.</summary>
        public int AutoInstallMaxPackages { get; set; } = 5;
    }
}
