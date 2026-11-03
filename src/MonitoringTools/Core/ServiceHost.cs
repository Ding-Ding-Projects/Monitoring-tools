using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace MonitoringTools.Core
{
    /// <summary>
    /// Makes the app behave like a background service: it installs itself per user,
    /// starts at sign-in, runs under a small supervisor that restarts it after a crash,
    /// can optionally run elevated through a scheduled task, and can uninstall itself.
    /// </summary>
    public static class ServiceHost
    {
        public const string Name = "MonitoringTools";
        public const int ExitQuit = 0;
        public const int ExitRestart = 75;

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string QuitEventName = @"Local\MonitoringTools.Quit";
        public const string ShowEventName = @"Local\MonitoringTools.Show";
        public const string AgentMutexName = @"Local\MonitoringTools.Agent";
        public const string SupervisorMutexName = @"Local\MonitoringTools.Supervisor";

        public static string CurrentExe => Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule.FileName;

        public static string InstallDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MonitoringTools");

        public static string InstalledExe => Path.Combine(InstallDir, "MonitoringTools.exe");

        /// <summary>A framework-dependent or dev build runs in place instead of copying itself.</summary>
        public static bool IsPortableBuild =>
            File.Exists(Path.Combine(Path.GetDirectoryName(CurrentExe) ?? "", "MonitoringTools.dll"));

        public static string ServiceExe => IsPortableBuild ? CurrentExe : InstalledExe;

        public static bool IsRunningInstalled =>
            IsPortableBuild || string.Equals(Path.GetFullPath(CurrentExe), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

        public static bool IsElevated
        {
            get
            {
                try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
                catch { return false; }
            }
        }

        // ---------------------------------------------------------------- install

        /// <summary>Copies the exe into the per-user install folder (replacing a running older copy) and registers autostart.</summary>
        public static void Install()
        {
            if (!IsPortableBuild)
            {
                StopRunningInstance(TimeSpan.FromSeconds(6));
                Directory.CreateDirectory(InstallDir);
                for (int i = 0; i < 10; i++)
                {
                    try { File.Copy(CurrentExe, InstalledExe, true); break; }
                    catch (IOException) when (i < 9) { Thread.Sleep(500); }
                }
            }
            Settings.Load();
            ApplyAutostart();
            Log.Info("Installed to " + ServiceExe);
        }

        public static void StopRunningInstance(TimeSpan wait)
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(QuitEventName, out var quit)) { quit.Set(); quit.Dispose(); }
            }
            catch { }
            var deadline = DateTime.UtcNow + wait;
            while (DateTime.UtcNow < deadline)
            {
                if (!Mutex.TryOpenExisting(AgentMutexName, out var m) && !Mutex.TryOpenExisting(SupervisorMutexName, out m)) return;
                m.Dispose();
                Thread.Sleep(250);
            }
        }

        public static void ApplyAutostart()
        {
            var s = Settings.Current;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
                if (s.StartWithWindows && !s.RunElevated)
                    key.SetValue(Name, $"\"{ServiceExe}\" --supervise");
                else
                    key.DeleteValue(Name, false);
            }
            catch (Exception ex) { Log.Warn("Autostart registry update failed: " + ex.Message); }

            if (s.RunElevated && IsElevated)
                RunSchtasks($"/Change /TN \"{Name}\" /{(s.StartWithWindows ? "ENABLE" : "DISABLE")}");
        }

        public static void LaunchSupervisor()
        {
            Process.Start(new ProcessStartInfo(ServiceExe, "--supervise") { UseShellExecute = false });
        }

        // ---------------------------------------------------------------- supervisor

        /// <summary>Keeps one agent process alive. Quit exits cleanly; a crash is restarted with backoff.</summary>
        public static int Supervise()
        {
            using var own = new Mutex(true, SupervisorMutexName, out bool first);
            if (!first) return 0;
            using var quit = new EventWaitHandle(false, EventResetMode.ManualReset, QuitEventName);

            int failures = 0;
            while (true)
            {
                var started = DateTime.UtcNow;
                Process child;
                try { child = Process.Start(new ProcessStartInfo(CurrentExe, "--agent") { UseShellExecute = false }); }
                catch (Exception ex) { Log.Error("Supervisor could not start agent", ex); return 1; }

                int signalled = WaitHandle.WaitAny(new WaitHandle[] { quit, new ProcessWaitHandle(child) });
                if (signalled == 0)
                {
                    if (!child.WaitForExit(5000)) { try { child.Kill(); } catch { } }
                    return 0;
                }

                int code = child.ExitCode;
                if (code == ExitQuit) return 0;
                if (code == ExitRestart) { failures = 0; continue; }

                if ((DateTime.UtcNow - started).TotalSeconds > 60) failures = 0;
                failures++;
                int delay = Math.Min(30, failures * failures * 2);
                Log.Warn($"Agent exited with {code}; restart #{failures} in {delay}s");
                if (quit.WaitOne(TimeSpan.FromSeconds(delay))) return 0;
            }
        }

        private sealed class ProcessWaitHandle : WaitHandle
        {
            public ProcessWaitHandle(Process p)
            {
                SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(p.Handle, false);
            }
        }

        // ---------------------------------------------------------------- elevation

        /// <summary>Asks for one UAC prompt, then runs elevated at every sign-in until turned off.</summary>
        public static bool RequestElevation(bool enable)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(ServiceExe, enable ? "--elevate-on" : "--elevate-off")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                });
                p?.WaitForExit();
                return p != null && p.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Log.Info("Elevation prompt was declined");
                return false;
            }
        }

        /// <summary>Runs inside the elevated helper process.</summary>
        public static int ElevateHelper(bool enable)
        {
            Settings.Load();
            if (enable)
            {
                var user = WindowsIdentity.GetCurrent().Name;
                var xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo><Description>Monitoring Tools background service (elevated)</Description></RegistrationInfo>
  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{Escape(user)}</UserId></LogonTrigger></Triggers>
  <Principals><Principal id=""Author""><UserId>{Escape(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <Enabled>{(Settings.Current.StartWithWindows ? "true" : "false")}</Enabled>
  </Settings>
  <Actions Context=""Author""><Exec><Command>{Escape(ServiceExe)}</Command><Arguments>--supervise</Arguments></Exec></Actions>
</Task>";
                var file = Path.Combine(Path.GetTempPath(), "MonitoringTools.task.xml");
                File.WriteAllText(file, xml, Encoding.Unicode);
                int rc = RunSchtasks($"/Create /TN \"{Name}\" /XML \"{file}\" /F");
                try { File.Delete(file); } catch { }
                if (rc != 0) return rc;
                Settings.Current.RunElevated = true;
            }
            else
            {
                RunSchtasks($"/Delete /TN \"{Name}\" /F");
                Settings.Current.RunElevated = false;
            }
            Settings.Save();
            ApplyAutostart();
            return 0;
        }

        /// <summary>After the elevation mode flips, restart the service in the matching mode.</summary>
        public static void RelaunchForElevationChange()
        {
            if (Settings.Current.RunElevated)
                RunSchtasksDetached($"/Run /TN \"{Name}\"");
            else
                // explorer.exe starts programs at normal integrity, which drops elevation
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ServiceExe}\"") { UseShellExecute = false });
        }

        public static bool ElevatedTaskExists() => RunSchtasks($"/Query /TN \"{Name}\"") == 0;

        private static string Escape(string s) => System.Security.SecurityElement.Escape(s);

        public static int RunSchtasks(string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo("schtasks.exe", args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                });
                p.WaitForExit(15000);
                return p.HasExited ? p.ExitCode : -1;
            }
            catch (Exception ex) { Log.Warn("schtasks failed: " + ex.Message); return -1; }
        }

        private static void RunSchtasksDetached(string args)
        {
            try
            {
                // Give the current instance time to exit so the task's IgnoreNew policy does not drop the start.
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 3 /nobreak >nul & schtasks.exe {args}")
                { UseShellExecute = false, CreateNoWindow = true });
            }
            catch { }
        }

        // ---------------------------------------------------------------- uninstall

        public static void Uninstall(bool removeData)
        {
            Log.Info("Uninstalling");
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                key?.DeleteValue(Name, false);
            }
            catch { }

            if (ElevatedTaskExists())
            {
                if (IsElevated) RunSchtasks($"/Delete /TN \"{Name}\" /F");
                else RequestElevation(false);
            }

            var targets = new StringBuilder("/c timeout /t 4 /nobreak >nul");
            if (!IsPortableBuild) targets.Append($" & rmdir /s /q \"{InstallDir}\"");
            if (removeData) targets.Append($" & rmdir /s /q \"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitoringTools")}\"");
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", targets.ToString())
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() });
            }
            catch { }

            try
            {
                if (EventWaitHandle.TryOpenExisting(QuitEventName, out var quit)) quit.Set();
            }
            catch { }
        }

        public static string[] Args => Environment.GetCommandLineArgs().Skip(1).ToArray();
    }
}
