using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonitoringTools.Core;
using Activity = MonitoringTools.Core.Activity;
using ActivityKind = MonitoringTools.Core.ActivityKind;

namespace MonitoringTools.Minecraft
{
    /// <summary>
    /// Runs the servers' schedules from the background app. It looks once a minute; a schedule that is due runs once,
    /// its time is recorded in servers.json, and its result goes to the activity log. Nothing runs for a server whose
    /// state cannot be read, and a restart or message is skipped (and recorded as skipped) when the server is not running.
    /// </summary>
    public static class McScheduler
    {
        private static Timer _timer;
        private static int _busy;
        private static readonly HashSet<string> Running = new HashSet<string>();
        /// <summary>The newest build found by an update-check schedule, per server, for the overview's attention list.</summary>
        public static readonly Dictionary<string, McBuild> FoundUpdates = new Dictionary<string, McBuild>();

        public static void Start()
        {
            _timer ??= new Timer(_ => _ = TickAsync(DateTime.Now), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
        }

        public static async Task TickAsync(DateTime now)
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                foreach (var s in ServerManager.Servers.ToList())
                    foreach (var job in s.Schedules.ToList())
                        if (job.Due(now) && Running.Add(s.Id + job.Id))
                        {
                            job.LastRun = now;   // recorded first, so a slow job is never started twice
                            _ = RunAsync(s, job);
                        }
            }
            catch (Exception ex) { Log.Error("Minecraft schedules", ex); }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        private static async Task RunAsync(ServerDef s, McSchedule job)
        {
            string result;
            try { result = await ExecuteAsync(s, job); }
            catch (Exception ex) { result = "Failed: " + ex.Message; }
            finally { Running.Remove(s.Id + job.Id); }
            job.LastResult = result;
            try { ServerManager.Save(); } catch { }
            Activity.Add(ActivityKind.Service, $"Minecraft schedule: {McSchedule.KindLabel(job.Kind)} on {s.Name}", result);
        }

        public static async Task<string> ExecuteAsync(ServerDef s, McSchedule job)
        {
            var st = await McState.ProbeAsync(s);
            bool running = st.State == McRunState.Running;
            if (st.State == McRunState.HostOffline) return "Skipped: the host could not be reached.";
            switch (job.Kind)
            {
                case McScheduleKind.Message:
                    if (!running) return "Skipped: the server was not running.";
                    return await ServerManager.CommandAsync(s, "say " + job.Message.Replace("\n", " "));
                case McScheduleKind.Backup:
                {
                    var b = await McBackups.BackUpAsync(s, running, "Scheduled", "");
                    return "Backed up to " + System.IO.Path.GetFileName(b.Path);
                }
                case McScheduleKind.UpdateCheck:
                {
                    var (newer, unavailable) = await McSoftware.UpdateAsync(s);
                    if (unavailable != null) return unavailable;
                    lock (FoundUpdates) { if (newer != null) FoundUpdates[s.Id] = newer; else FoundUpdates.Remove(s.Id); }
                    return newer == null ? "Up to date." : $"{s.Flavour} build {newer.Id} is available.";
                }
                default:
                {
                    if (!running) return "Skipped: the server was not running.";
                    if (job.WarnMinutes > 0)
                    {
                        await ServerManager.CommandAsync(s, $"say The server restarts in {job.WarnMinutes} minute{(job.WarnMinutes == 1 ? "" : "s")}.");
                        await Task.Delay(TimeSpan.FromMinutes(job.WarnMinutes));
                    }
                    await ServerManager.StopAsync(s);
                    await ServerManager.StartAsync(s, null);
                    return "Restarted.";
                }
            }
        }
    }
}
