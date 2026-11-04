using System.Collections.Generic;

namespace MonitoringTools.Core
{
    public sealed partial class Settings
    {
        /// <summary>Swaps in settings restored from history, repairs missing lists the same way a load does, and saves.</summary>
        public static void ReplaceCurrent(Settings restored)
        {
            if (restored == null) return;
            Current = restored;
            if (Current.ToastSeconds < 1) Current.ToastSeconds = 3;
            if (Current.ProbeIntervalSeconds < 1) Current.ProbeIntervalSeconds = 3;
            if (Current.FailuresBeforeAlert < 1) Current.FailuresBeforeAlert = 2;
            if (Current.GameProcesses == null) Current.GameProcesses = new List<string>(GameDefaults);
            if (Current.Shortcuts == null) Current.Shortcuts = new List<Shortcut>();
            if (Current.DnsProfiles == null || Current.DnsProfiles.Count == 0) Current.DnsProfiles = DnsProfile.Defaults();
            if (Current.CommitTemplate == null) Current.CommitTemplate = "";
            if (Current.SavedPatterns == null) Current.SavedPatterns = new List<SavedPattern>();
            Save();
        }
    }
}
