using System;
using System.IO;

namespace MonitoringTools.Core
{
    /// <summary>
    /// What a copy started by hand (Setup.exe, a downloaded exe) leaves for the service to show once it is installed:
    /// the welcome on a genuine first install, a quiet "updated" notice when it replaced another version, and simply the
    /// control centre when it was the same version or a reinstall. The welcome tour never comes back after an update.
    /// </summary>
    public static class InstallNote
    {
        public enum Kind { FirstRun, Updated, Reopen }

        public const string WelcomeFlag = "welcome.flag";
        public const string OpenFlag = "open.flag";
        public const string UpdatedFlag = "updated.flag";

        /// <summary>Decides from what was on the machine before the copy was replaced.</summary>
        public static Kind Decide(bool installedBefore, bool settingsExisted, Version before, Version now)
        {
            if (!installedBefore && !settingsExisted) return Kind.FirstRun;
            if (installedBefore && before != null && now != null && before != now) return Kind.Updated;
            return Kind.Reopen;
        }

        /// <summary>The updated flag's text: "from&gt;to&gt;how", where how is "manual" or "background".</summary>
        public static string UpdatedText(Version from, Version to, bool manual) =>
            $"{from?.ToString(3) ?? ""}>{to?.ToString(3) ?? ""}>{(manual ? "manual" : "background")}";

        /// <summary>The sentence shown once from an updated flag's text, or null when the text is unusable.</summary>
        public static string UpdatedSentence(string text, Version current)
        {
            if (text == null) return null;
            var parts = text.Split('>');
            var from = parts[0].Trim();
            var to = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : current?.ToString(3) ?? "";
            if (to.Length == 0) return null;
            bool manual = parts.Length > 2 && parts[2].Trim() == "manual";
            var how = manual ? "" : " in the background";
            if (from.Length > 0 && from != to) return $"Updated from {from} to {to}{how}.";
            return $"Updated to {to}{how}.";
        }

        /// <summary>Writes the flag for <paramref name="kind"/> into <paramref name="dataDir"/>.</summary>
        public static void Write(string dataDir, Kind kind, Version before, Version now)
        {
            Directory.CreateDirectory(dataDir);
            switch (kind)
            {
                case Kind.FirstRun: File.WriteAllText(Path.Combine(dataDir, WelcomeFlag), DateTime.Now.ToString("O")); break;
                case Kind.Updated: File.WriteAllText(Path.Combine(dataDir, UpdatedFlag), UpdatedText(before, now, true)); break;
                default: File.WriteAllText(Path.Combine(dataDir, OpenFlag), DateTime.Now.ToString("O")); break;
            }
        }

        /// <summary>Removes and reports a one-shot flag.</summary>
        public static bool Take(string dataDir, string flag)
        {
            var p = Path.Combine(dataDir, flag);
            if (!File.Exists(p)) return false;
            try { File.Delete(p); } catch { }
            return true;
        }
    }
}
