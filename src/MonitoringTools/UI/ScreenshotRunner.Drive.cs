using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        private sealed class Wave6Snap : IWave6Snap
        {
            public Task Window(Window w, string name, string caption) => Shot(w, name, caption, 350);

            public async Task Popup(Window w, FrameworkElement card, FrameworkElement anchor, string name, string caption)
            {
                await Task.Delay(350);
                w.UpdateLayout();
                if (card == null || card.ActualWidth < 1) { await Shot(w, name, caption, 0); return; }
                card.UpdateLayout();
                Point at;
                try { at = anchor.TranslatePoint(new Point(0, anchor.ActualHeight + 8), w); } catch { at = new Point(16, 16); }
                if (at.Y + card.ActualHeight + 24 > w.ActualHeight) at.Y = Math.Max(8, w.ActualHeight - card.ActualHeight - 28);
                double width = Math.Max(w.ActualWidth, at.X + card.ActualWidth + 24), height = Math.Max(w.ActualHeight, at.Y + card.ActualHeight + 24);
                const double scale = 2;
                int pw = (int)Math.Ceiling(width * scale), ph = (int)Math.Ceiling(height * scale);
                var win = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth * scale), (int)Math.Ceiling(w.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                win.Render(w);
                var pop = new RenderTargetBitmap((int)Math.Ceiling((card.ActualWidth + 24) * scale), (int)Math.Ceiling((card.ActualHeight + 24) * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                pop.Render(card);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x09, 0x0C)), null, new Rect(0, 0, width, height));
                    dc.DrawImage(win, new Rect(0, 0, w.ActualWidth, w.ActualHeight));
                    dc.DrawImage(pop, new Rect(at.X, at.Y, card.ActualWidth + 24, card.ActualHeight + 24));
                }
                var bmp = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bmp.Render(dv);
                var file = name + ".png";
                Demo.SavePng(bmp, Path.Combine(_dir, file));
                Index.Add(new { file, caption, width = bmp.PixelWidth, height = bmp.PixelHeight });
            }
        }

        /// <summary>
        /// Drives features through their real controls and photographs the result: element menus, a scheduled rule being edited,
        /// select mode on four lists, each guided-form step, the Focus spotlight, School mode, the appearance editor, the colour
        /// picker and the history panel. A flow that fails is written to wave6-errors.txt and the run carries on.
        /// </summary>
        private static async Task Wave6ScenesAsync()
        {
            var snap = new Wave6Snap();
            var errors = new System.Text.StringBuilder();
            async Task Run(string name, Func<IWave6Snap, Task<string>> flow)
            {
                try { var err = await flow(snap); if (err != null) errors.AppendLine(name + ": " + err); }
                catch (Exception ex) { errors.AppendLine(name + ": " + ex.GetType().Name + ": " + ex.Message); }
            }
            await Run("menus", Wave6Flows.Menus);
            await Run("schedule", Wave6Flows.Schedule);
            await Run("bulk", Wave6Flows.Bulk);
            await Run("guided", Wave6Flows.Guided);
            await Run("spotlight", Wave6Flows.Spotlight);
            await Run("school", Wave6Flows.School);
            await Run("appearance", Wave6Flows.Appearance);
            await Run("picker", Wave6Flows.Picker);
            await Run("history", Wave6Flows.History);
            await Run("number-clipboard", NumberFieldFlows.HistorySize);
            await Run("number-pages", async sn => { await NumberFieldFlows.Capture(sn); return null; });
            await Run("lock-menu-item", Wave6Flows.LockedMenuItem);
            if (errors.Length > 0) File.WriteAllText(Path.Combine(_dir, "wave6-errors.txt"), errors.ToString());
        }
    }
}
