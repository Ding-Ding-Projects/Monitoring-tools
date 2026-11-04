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
