using System;
using System.Threading.Tasks;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // Drawing: the width changes while a stroke is in progress.
    public static partial class SelfTest
    {
        private static async Task DrawLane4ChecksAsync()
        {
            await CheckAsync("drawing: a width change made while a stroke is in progress shows in its very next segment", async () =>
            {
                var w = DrawingWindow.CreateBlankForTest();
                var saved = Settings.Current.DrawWidth;
                try
                {
                    await Task.Delay(500);
                    w.SetWidthForTest(4);
                    int thin = w.LiveSegmentThicknessForTest(20, 80, 40);
                    double now = w.SetWidthForTest(20);
                    int thick = w.LiveSegmentThicknessForTest(100, 160, 40);   // the next segment of the same stroke
                    w.SetWidthForTest(10);
                    int mid = w.LiveSegmentThicknessForTest(20, 80, 80);
                    if (now != 20) return "the width in force was " + now;
                    if (thin < 3 || thin > 7) return "a width 4 segment measured " + thin + " px";
                    if (thick < 17 || thick > 24) return "a width 20 segment measured " + thick + " px";
                    if (mid < 8 || mid > 13) return "a width 10 segment measured " + mid + " px";
                    return thick > 2 * thin ? null : "the later segment is not clearly thicker";
                }
                finally { Settings.Current.DrawWidth = saved; w.Close(); }
            });
        }
    }
}
