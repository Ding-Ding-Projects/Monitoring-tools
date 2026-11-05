using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // Entry point for the checks of: QR versions and photos, encrypted local history, the appearance file dialogs, the
    // eyedropper, the external settings file, the other ADHD modes, the palette menu and the settings walk on other windows.
    public static partial class SelfTest
    {
        internal static void NoteForReport(string key, string value) => Lane4Notes[key] = value;

        private static async Task Lane4ChecksAsync(string dir)
        {
            QrLane4Checks();
            await HistoryLane4ChecksAsync();
            await AdhdLane4ChecksAsync();
            await ExternalLane4ChecksAsync(dir);
            await PickerLane4ChecksAsync(dir);
            await DrawLane4ChecksAsync();
            await LocksLane4ChecksAsync();
            await LockWalkChecksAsync();
            await Task.CompletedTask;
        }
    }
}
