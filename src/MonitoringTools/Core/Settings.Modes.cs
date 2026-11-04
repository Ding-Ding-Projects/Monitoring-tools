using System;
using System.Collections.Generic;

namespace MonitoringTools.Core
{
    /// <summary>Settings owned by the Modes page: School mode attempts, ADHD modes and scheduled settings.</summary>
    public sealed partial class Settings
    {
        // School mode. The mode itself, its name and its PIN hash live in the shared record (see Modes);
        // only the wrong-attempt counter for this install is kept here.
        public int SchoolFailures { get; set; } = 0;
        public DateTime SchoolLockUntilUtc { get; set; } = DateTime.MinValue;

        // ADHD modes, each independent and off by default.
        public bool AdhdFocus { get; set; } = false;
        public bool AdhdLowStimulation { get; set; } = false;
        public bool AdhdTimeAwareness { get; set; } = false;
        public bool AdhdOneThing { get; set; } = false;
        public bool AdhdMomentum { get; set; } = false;
        public int TimeReminderMinutes { get; set; } = 30;
        public string LastPage { get; set; } = "";

        // Scheduled settings and the external settings file
        public List<ScheduleRule> ScheduleRules { get; set; } = new List<ScheduleRule>();
        public string ExternalSettingsPath { get; set; } = "";
        public bool ExternalFollowing { get; set; } = false;
    }

    /// <summary>One scheduled rule: between two local times on chosen weekdays, set one value.</summary>
    public sealed class ScheduleRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Label { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public string Start { get; set; } = "09:00";
        public string End { get; set; } = "17:00";
        /// <summary>DayOfWeek values (0 = Sunday). Empty means every day.</summary>
        public List<int> Days { get; set; } = new List<int>();
        /// <summary>language, theme, sounds, toasts or gamemode.</summary>
        public string Setting { get; set; } = "sounds";
        public string Value { get; set; } = "off";
    }
}
