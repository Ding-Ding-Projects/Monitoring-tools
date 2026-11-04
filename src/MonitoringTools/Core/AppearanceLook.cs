using System;
using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Core
{
    /// <summary>
    /// A whole look in five choices: theme, seed colour, contrast, density and text size. The guided "make a look" flow builds
    /// one, shows a review of what changes and applies it through the same settings the single controls use.
    /// </summary>
    public sealed class AppearanceLook
    {
        public string Theme { get; set; } = "system";
        public string Seed { get; set; } = "mint";
        public bool HighContrastOutlines { get; set; }
        public bool Compact { get; set; }
        public int TextPercent { get; set; } = 100;

        public static AppearanceLook FromCurrent() => new AppearanceLook
        {
            Theme = Appearance.ThemeMode, Seed = Appearance.Seed, HighContrastOutlines = Appearance.HighContrastOutlines,
            Compact = Appearance.Compact, TextPercent = Appearance.ClampPercent(Settings.Current.TextScalePercent)
        };

        /// <summary>What to fix, or null when every choice is one the app offers.</summary>
        public string Check()
        {
            if (!new[] { "system", "light", "dark" }.Contains(Theme)) return "Pick light, dark or system for the theme.";
            if (!Appearance.SeedIds.Contains(Seed)) return "Pick one of the seed colours.";
            if (TextPercent < 90 || TextPercent > 150 || TextPercent % 10 != 0) return "Text size is 90 to 150 percent in steps of 10.";
            return null;
        }

        /// <summary>Line by line, what applying this look would change compared with <paramref name="current"/>. Empty when nothing changes.</summary>
        public List<string> Changes(AppearanceLook current)
        {
            var list = new List<string>();
            if (Theme != current.Theme) list.Add($"Theme: {current.Theme} to {Theme}");
            if (Seed != current.Seed) list.Add($"Seed colour: {current.Seed} to {Seed}");
            if (HighContrastOutlines != current.HighContrastOutlines) list.Add($"Contrast outlines: {(current.HighContrastOutlines ? "on" : "off")} to {(HighContrastOutlines ? "on" : "off")}");
            if (Compact != current.Compact) list.Add($"Density: {(current.Compact ? "compact" : "comfortable")} to {(Compact ? "compact" : "comfortable")}");
            if (TextPercent != current.TextPercent) list.Add($"Text size: {current.TextPercent} % to {TextPercent} %");
            return list;
        }

        /// <summary>Applies the look to the saved settings and redraws. Returns the problem, or null on success.</summary>
        public string Apply()
        {
            var err = Check();
            if (err != null) return err;
            var s = Settings.Current;
            s.ThemeMode = Theme; s.SeedColour = Seed; s.HighContrastOutlines = HighContrastOutlines;
            s.CompactDensity = Compact; s.TextScalePercent = TextPercent;
            Settings.Save();
            Appearance.Apply();
            return null;
        }
    }
}
