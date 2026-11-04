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
        private static async Task Lane4ChecksAsync(string dir)
        {
            QrLane4Checks();
            await Task.CompletedTask;
        }
    }
}
