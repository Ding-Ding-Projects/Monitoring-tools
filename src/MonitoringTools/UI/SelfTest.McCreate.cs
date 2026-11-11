using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MonitoringTools.Minecraft;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>Creating a Minecraft server keeps the app responsive and shows its progress. Run on their own (MT_ONLY=mccreate).</summary>
        private static async Task McCreateChecksAsync()
        {
            await CheckAsync("minecraft create: the slow steps run off the UI thread, so the app keeps answering", async () =>
            {
                int uiThread = Thread.CurrentThread.ManagedThreadId, workThread = -1, ticks = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                timer.Tick += (s, e) => ticks++;
                ServerManager.SlowStepForTest = (s, report) =>
                {
                    workThread = Thread.CurrentThread.ManagedThreadId;
                    report("Downloading 50% of 40 MB");
                    Thread.Sleep(2000);   // a synchronous step, like a checksum or docker pull
                    throw new OperationCanceledException("stopped by the check");
                };
                var before = ServerManager.Servers.Count;
                timer.Start();
                try
                {
                    var def = new ServerDef { Name = "Responsiveness check", Version = "1.21.1", Flavour = Flavour.Vanilla, Target = Target.LocalProcess, Port = 25599, MemoryMb = 1024, EulaAccepted = true };
                    try { await ServerManager.CreateAsync(def, _ => { }, new Dictionary<string, string>()); return "the stand-in step did not stop the creation"; }
                    catch (OperationCanceledException) { }
                }
                finally { timer.Stop(); ServerManager.SlowStepForTest = null; }
                if (workThread == uiThread) return "the work ran on the UI thread";
                if (ticks < 12) return $"the UI thread answered only {ticks} times in 2 seconds";
                if (ServerManager.Servers.Count != before) return "a failed creation still added a server";
                return null;
            });

            await CheckAsync("minecraft create: the form shows a progress bar that follows the reported percentage", async () =>
            {
                var gate = new TaskCompletionSource<bool>();
                GuidedForm form = null;
                var steps = new List<GuidedStep> { new GuidedStep { Title = "Review", Build = () => new System.Windows.Controls.TextBlock { Text = "Ready" } } };
                form = new GuidedForm("New server", steps, "Create server", null, () => { });
                form.FinishAsync = async () =>
                {
                    await Task.Run(() => Thread.Sleep(200));
                    form.Report("Pulling the server image…");
                    await Task.Delay(300);
                    form.Report("Downloading 40% of 50 MB");
                    await gate.Task;
                    return null;
                };
                var w = new System.Windows.Window { Width = 700, Height = 500, Content = form };
                try
                {
                    w.Show(); await Task.Delay(400);
                    if (!Press(w, "Create server")) return "no Create server button";
                    await Task.Delay(300);
                    if (!form.Working) return "the form does not show it is working";
                    if (form.WorkPercent != null) return "the bar should be indeterminate before a percentage arrives";
                    await Task.Delay(600);
                    if (form.WorkPercent == null) return "the bar did not take the 40% report";
                    await Task.Delay(500);
                    if (Math.Abs(form.WorkPercent.Value - 40) > 1) return "the bar shows " + form.WorkPercent;
                    gate.SetResult(true);
                    await Task.Delay(300);
                    if (form.Working) return "the form still shows work after it finished";
                    return null;
                }
                finally { gate.TrySetResult(true); w.Close(); }
            });
        }
    }
}
