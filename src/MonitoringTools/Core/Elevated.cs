using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    /// <summary>Runs admin-only commands: directly when already elevated, otherwise behind one UAC prompt.</summary>
    public static class Elevated
    {
        public static Task<int> RunAsync(params string[] commands)
        {
            return Task.Run(() =>
            {
                var line = string.Join(" & ", commands);
                try
                {
                    ProcessStartInfo psi;
                    if (ServiceHost.IsElevated)
                        psi = new ProcessStartInfo("cmd.exe", "/c " + line) { UseShellExecute = false, CreateNoWindow = true };
                    else
                        psi = new ProcessStartInfo("cmd.exe", "/c " + line) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                    using var p = Process.Start(psi);
                    p.WaitForExit(120000);
                    return p.HasExited ? p.ExitCode : -1;
                }
                catch (System.ComponentModel.Win32Exception) { return -2; } // prompt declined
                catch (Exception ex) { Log.Warn("Elevated command failed: " + ex.Message); return -1; }
            });
        }
    }
}
