using System.Collections.Generic;
using System.Linq;

namespace MonitoringTools.Core
{
    public static partial class Modes
    {
        public const int MaxScheduleRules = 64;

        /// <summary>Checks the times of a rule. Returns what to fix, or null.</summary>
        public static string CheckRuleTimes(string start, string end)
            => TryParseTime(start, out _) && TryParseTime(end, out _) ? null : "Enter both times as 24-hour HH:mm, for example 08:30.";

        /// <summary>Checks the optional date range of a rule. Returns what to fix, or null.</summary>
        public static string CheckRuleDates(string from, string to)
        {
            if (!TryParseDate(from, out _) || !TryParseDate(to, out _)) return "Enter dates as yyyy-MM-dd, for example 2026-12-24, or leave them empty.";
            return ValidDateRange(from, to) ? null : "The end date is before the start date.";
        }

        /// <summary>Checks the setting and value of a rule. Returns what to fix, or null.</summary>
        public static string CheckRuleValue(string setting, string value)
            => SettingKeys.Contains(setting) && ValidValue(setting, value) ? null : "Pick a setting and a value first.";

        /// <summary>The whole check used by both the single form and the guided form. <paramref name="adding"/> is false when an existing rule is edited.</summary>
        public static string CheckRule(ScheduleRule r, bool adding)
        {
            if (adding && (Settings.Current.ScheduleRules?.Count ?? 0) >= MaxScheduleRules) return $"The limit is {MaxScheduleRules} rules.";
            return CheckRuleValue(r.Setting, r.Value) ?? CheckRuleTimes(r.Start, r.End) ?? CheckRuleDates(r.FromDate, r.ToDate);
        }

        /// <summary>Adds a checked rule to the saved list and re-evaluates. Returns the problem, or null on success.</summary>
        public static string AddRule(ScheduleRule r)
        {
            var err = CheckRule(r, true);
            if (err != null) return err;
            r.Label = (r.Label ?? "").Trim();
            r.Days = (r.Days ?? new List<int>()).Distinct().OrderBy(d => (d + 6) % 7).ToList();
            Settings.Current.ScheduleRules.Add(r);
            Settings.Save();
            Evaluate();
            return null;
        }
    }
}
