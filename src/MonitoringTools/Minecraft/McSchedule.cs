using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MonitoringTools.Minecraft
{
    public enum McScheduleKind { Restart, Backup, Message, UpdateCheck }
    public enum McRepeat { Daily, Weekly, Every }

    /// <summary>
    /// One scheduled job for one server, stored in servers.json. Times are this computer's local time. A run that was missed
    /// (the computer was asleep or the app was closed) happens once when the app is next running, never once per missed time.
    /// </summary>
    public sealed class McSchedule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public McScheduleKind Kind { get; set; }
        public bool Enabled { get; set; } = true;
        public McRepeat Repeat { get; set; } = McRepeat.Daily;
        /// <summary>"HH:mm", for daily and weekly schedules.</summary>
        public string Time { get; set; } = "04:00";
        public List<DayOfWeek> Days { get; set; } = new List<DayOfWeek>();
        /// <summary>For "every" schedules: the gap between runs.</summary>
        public int EveryMinutes { get; set; } = 240;
        /// <summary>The chat line for a message schedule.</summary>
        public string Message { get; set; } = "";
        /// <summary>For a restart: how many minutes before it players are warned in chat. 0 restarts without a warning.</summary>
        public int WarnMinutes { get; set; } = 5;
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime? LastRun { get; set; }
        public string LastResult { get; set; } = "";

        private TimeSpan TimeOfDay => TimeSpan.TryParseExact(Time ?? "", @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t : new TimeSpan(4, 0, 0);
        private bool DayOk(DateTime d) => Repeat != McRepeat.Weekly || Days == null || Days.Count == 0 || Days.Contains(d.DayOfWeek);

        /// <summary>The latest planned time at or before <paramref name="now"/>, or null when none has come yet.</summary>
        public DateTime? Previous(DateTime now)
        {
            if (Repeat == McRepeat.Every)
            {
                if (EveryMinutes < 1) return null;
                var gap = TimeSpan.FromMinutes(EveryMinutes);
                var anchor = Created;
                if (now < anchor + gap) return null;
                long k = (now - anchor).Ticks / gap.Ticks;
                return anchor + TimeSpan.FromTicks(gap.Ticks * k);
            }
            for (int back = 0; back <= 7; back++)
            {
                var day = now.Date.AddDays(-back);
                var at = day + TimeOfDay;
                if (at <= now && DayOk(day)) return at;
            }
            return null;
        }

        /// <summary>The next planned time after <paramref name="now"/>.</summary>
        public DateTime? Next(DateTime now)
        {
            if (Repeat == McRepeat.Every)
            {
                if (EveryMinutes < 1) return null;
                var gap = TimeSpan.FromMinutes(EveryMinutes);
                var prev = Previous(now) ?? Created;
                return prev + gap;
            }
            for (int ahead = 0; ahead <= 8; ahead++)
            {
                var day = now.Date.AddDays(ahead);
                var at = day + TimeOfDay;
                if (at > now && DayOk(day)) return at;
            }
            return null;
        }

        /// <summary>True when a planned time has passed since the last run (or since the schedule was made). However many were missed, this is one run.</summary>
        public bool Due(DateTime now)
        {
            if (!Enabled) return false;
            var prev = Previous(now);
            if (prev == null) return false;
            var since = LastRun ?? Created;
            return prev > since;
        }

        public string Describe()
        {
            string when = Repeat switch
            {
                McRepeat.Every => EveryMinutes % 60 == 0 ? $"Every {EveryMinutes / 60} h" : $"Every {EveryMinutes} min",
                McRepeat.Weekly => (Days.Count == 0 ? "Every day" : string.Join(", ", Days.Select(d => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(d)))) + " at " + Time,
                _ => "Every day at " + Time,
            };
            return Kind switch
            {
                McScheduleKind.Restart => when + (WarnMinutes > 0 ? $" · players get a {WarnMinutes}-minute warning in chat" : " · no warning"),
                McScheduleKind.Message => when + ": " + Message,
                McScheduleKind.UpdateCheck => when + " · notify only, never installs by itself",
                _ => when,
            };
        }

        public static string KindLabel(McScheduleKind k) => k switch
        {
            McScheduleKind.Restart => "Restart", McScheduleKind.Backup => "Backup", McScheduleKind.Message => "Message", _ => "Check for updates",
        };
    }
}
