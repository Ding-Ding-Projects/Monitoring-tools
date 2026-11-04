using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MonitoringTools.Core
{
    /// <summary>When the running build was made, read from metadata the build stamps into the assembly.</summary>
    public static class BuildInfo
    {
        public static DateTime? BuiltUtc
        {
            get
            {
                var raw = typeof(BuildInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildTimeUtc")?.Value;
                return DateTime.TryParseExact(raw, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null;
            }
        }

        /// <summary>"updated 2026-11-04 05:10:32 (UTC-05:00)" in local time, or an honest unavailable label.</summary>
        public static string UpdatedLabel
        {
            get
            {
                var t = BuiltUtc;
                if (t == null) return "updated: build time unavailable";
                var local = t.Value.ToLocalTime();
                var off = TimeZoneInfo.Local.GetUtcOffset(local);
                return $"updated {local:yyyy-MM-dd HH:mm:ss} (UTC{(off < TimeSpan.Zero ? "-" : "+")}{off.ToString(@"hh\:mm", CultureInfo.InvariantCulture)})";
            }
        }
    }
}
