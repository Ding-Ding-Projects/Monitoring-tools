namespace MonitoringTools.Core
{
    // Appearance choices. Stored values are plain strings and numbers so settings.json stays readable.
    public sealed partial class Settings
    {
        /// <summary>"system", "light" or "dark".</summary>
        public string ThemeMode { get; set; } = "system";

        /// <summary>The look: "graphite" (default), "instrument", "blueprint" or "pop". Changes type, shapes, depth and the
        /// neutral colours everywhere; see <see cref="Looks"/>.</summary>
        public string Look { get; set; } = "graphite";

        /// <summary>"mint", "violet", "amber", "ocean" or "rose".</summary>
        public string SeedColour { get; set; } = "mint";

        /// <summary>Text size as a percentage, 90 to 150 in steps of 10.</summary>
        public int TextScalePercent { get; set; } = 100;

        /// <summary>True for the compact density.</summary>
        public bool CompactDensity { get; set; } = false;

        /// <summary>"follow" (use Windows), "on" (reduce motion) or "off" (always animate).</summary>
        public string MotionMode { get; set; } = "follow";

        /// <summary>True to draw dividers and borders with the stronger outline colour.</summary>
        public bool HighContrastOutlines { get; set; } = false;

        /// <summary>The name shown for the app in headers and the tray.</summary>
        public string DisplayName { get; set; } = "Monitoring Tools";

        /// <summary>"seed" follows the seed colour; "custom" uses <see cref="LogoCustomHex"/>.</summary>
        public string LogoSource { get; set; } = "seed";

        /// <summary>A custom logo tint as #RRGGBB.</summary>
        public string LogoCustomHex { get; set; } = "";

        /// <summary>The glyph id from the app's icon set shown as the logo. Default "pulse".</summary>
        public string LogoGlyph { get; set; } = "pulse";

        /// <summary>File name (inside the app's logo folder) of a custom PNG or ICO logo. Empty means none.</summary>
        public string LogoImageFile { get; set; } = "";
    }
}
