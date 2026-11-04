using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MonitoringTools.UI
{
    // Entry points for the self-test and the capture run. They drive the board's own undo, redo, render and export code, so a
    // check exercises the real paths without a mouse or pen.
    public sealed partial class DrawingWindow
    {
        internal static DrawingWindow CreateBlankForTest()
        {
            var w = new DrawingWindow(null, null, "Blank board", "selftest-" + System.Guid.NewGuid().ToString("N")[..8]);
            w.Show();
            return w;
        }

        internal int StrokeCountForTest => _canvas.Strokes.Count;

        /// <summary>Adds one stroke as if it had just been drawn and commits it to the undo list.</summary>
        internal void DrawStrokeForTest(double x0, double y0, double x1, double y1)
        {
            var pts = new StylusPointCollection();
            for (int i = 0; i <= 12; i++) pts.Add(new StylusPoint(x0 + (x1 - x0) * i / 12.0, y0 + (y1 - y0) * i / 12.0 + (i % 2) * 4));
            var stroke = new Stroke(pts) { DrawingAttributes = { Color = _color, Width = 6, Height = 6 } };
            _canvas.Strokes.Add(stroke);
            CommitChange();
        }

        /// <summary>Changes the width the way the [ and ] keys do. Returns the width now in force.</summary>
        internal double SetWidthForTest(double v) { SetWidth(v); return _width; }

        /// <summary>
        /// Renders one segment of a stroke in progress through the live renderer (the code that draws while the pen is down) and returns the ink
        /// thickness found across the segment, in pixels, so a check can compare the segment before and after a width change.
        /// </summary>
        internal int LiveSegmentThicknessForTest(double x0, double x1, double y)
        {
            var pts = new StylusPointCollection();
            for (int i = 0; i <= 6; i++) pts.Add(new StylusPoint(x0 + (x1 - x0) * i / 6.0, y));
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen()) _live.DrawForTest(dc, pts);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(200, 120, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(dv);
            var px = new byte[200 * 120 * 4];
            rtb.CopyPixels(px, 200 * 4, 0);
            int col = (int)((x0 + x1) / 2), thick = 0;
            for (int r = 0; r < 120; r++) if (px[(r * 200 + col) * 4 + 3] > 128) thick++;
            return thick;
        }

        internal void UndoForTest() => Undo();
        internal void RedoForTest() => Redo();
        internal string SvgForTest() => BuildSvg();

        internal int SavePngForTest(string path)
        {
            var bmp = Render(1);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(path)) enc.Save(fs);
            return bmp.PixelWidth;
        }
    }
}
