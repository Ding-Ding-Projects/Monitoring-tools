using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A chunk grid on which the pre-generation square is drawn. Chunks are 16 x 16 blocks. The view shows chunk and block
    /// coordinates and the 32-chunk region boundaries; it does not draw terrain, because terrain cannot be known without running the game.
    /// Drag to draw a square, hold the right or middle button (or Shift) and drag to pan, use the wheel to zoom.
    /// With the keyboard: arrows move the square (Shift moves 8 chunks), plus and minus resize it, Home returns the view to 0, 0.
    /// </summary>
    public sealed class ChunkAreaPicker : FrameworkElement
    {
        public const int MaxRadius = 100;

        private double _cell = 10;                // pixels per chunk
        private double _viewX, _viewZ;            // chunk coordinate at the centre of the view
        private bool _drawing, _panning;
        private (int X, int Z) _anchor;
        private Point _panStart; private double _panViewX, _panViewZ;
        private (int X, int Z)? _hover;

        public int CenterX { get; private set; }
        public int CenterZ { get; private set; }
        public int Radius { get; private set; }

        /// <summary>Raised when the user changes the square: centre X, centre Z (chunks) and radius (chunks; the square is 2 x radius chunks wide).</summary>
        public event Action<int, int, int> SelectionChanged;

        public ChunkAreaPicker()
        {
            Height = 300; MinWidth = 220;
            Focusable = true; ClipToBounds = true; Cursor = Cursors.Cross;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetName(this, "Pre-generation area picker. Drag to draw a square of chunks; arrow keys move it, plus and minus resize it.");
            ToolTip = "Draw the square to pre-generate: drag across the grid. Right-drag or Shift-drag pans, the wheel zooms, arrow keys move the square, + and - resize it, Home returns to 0, 0.";
        }

        /// <summary>Sets the square without raising the event (used when a text box changed it). Radius 0 means no square.</summary>
        public void SetSelection(int centerX, int centerZ, int radius)
        {
            CenterX = centerX; CenterZ = centerZ; Radius = Math.Clamp(radius, 0, MaxRadius);
            InvalidateVisual();
        }

        public void ResetView() { _viewX = 0; _viewZ = 0; InvalidateVisual(); }

        private void Raise() { SelectionChanged?.Invoke(CenterX, CenterZ, Radius); InvalidateVisual(); }

        // ------------------------------------------------------------ geometry

        private (int X, int Z) CellAt(Point p) =>
            ((int)Math.Floor(_viewX + (p.X - ActualWidth / 2) / _cell), (int)Math.Floor(_viewZ + (p.Y - ActualHeight / 2) / _cell));

        private Point ScreenOf(double chunkX, double chunkZ) =>
            new Point((chunkX - _viewX) * _cell + ActualWidth / 2, (chunkZ - _viewZ) * _cell + ActualHeight / 2);

        /// <summary>The square covering two corner chunks, as centre and radius. The side is rounded up to an even number so it stays centred on a chunk edge.</summary>
        public static (int CenterX, int CenterZ, int Radius) SquareFromCorners((int X, int Z) a, (int X, int Z) b)
        {
            int dx = b.X - a.X, dz = b.Z - a.Z;
            int side = Math.Max(Math.Abs(dx), Math.Abs(dz)) + 1;
            int r = Math.Clamp((side + 1) / 2, 1, MaxRadius);
            int minX = dx >= 0 ? a.X : a.X - side + 1;
            int minZ = dz >= 0 ? a.Z : a.Z - side + 1;
            return (minX + r, minZ + r, r);
        }

        // ------------------------------------------------------------ input

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            Focus();
            var p = e.GetPosition(this);
            bool pan = e.ChangedButton != MouseButton.Left || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if (pan) { _panning = true; _panStart = p; _panViewX = _viewX; _panViewZ = _viewZ; Cursor = Cursors.SizeAll; }
            else { _drawing = true; _anchor = CellAt(p); var s = SquareFromCorners(_anchor, _anchor); CenterX = s.CenterX; CenterZ = s.CenterZ; Radius = s.Radius; Raise(); }
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            _hover = CellAt(p);
            if (_panning) { _viewX = _panViewX - (p.X - _panStart.X) / _cell; _viewZ = _panViewZ - (p.Y - _panStart.Y) / _cell; }
            else if (_drawing) { var s = SquareFromCorners(_anchor, CellAt(p)); CenterX = s.CenterX; CenterZ = s.CenterZ; Radius = s.Radius; Raise(); return; }
            InvalidateVisual();
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_drawing || _panning) { _drawing = _panning = false; Cursor = Cursors.Cross; ReleaseMouseCapture(); e.Handled = true; }
        }

        protected override void OnMouseLeave(MouseEventArgs e) { _hover = null; InvalidateVisual(); }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            var before = CellAt(e.GetPosition(this));
            _cell = Math.Clamp(_cell * (e.Delta > 0 ? 1.25 : 0.8), 3, 48);
            // keep the chunk under the pointer where it was
            var p = e.GetPosition(this);
            _viewX = before.X + 0.5 - (p.X - ActualWidth / 2) / _cell;
            _viewZ = before.Z + 0.5 - (p.Y - ActualHeight / 2) / _cell;
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 8 : 1;
            bool changed = true;
            switch (e.Key)
            {
                case Key.Left: CenterX -= step; break;
                case Key.Right: CenterX += step; break;
                case Key.Up: CenterZ -= step; break;
                case Key.Down: CenterZ += step; break;
                case Key.Add: case Key.OemPlus: Radius = Math.Min(MaxRadius, Radius + step); break;
                case Key.Subtract: case Key.OemMinus: Radius = Math.Max(0, Radius - step); break;
                case Key.Home: ResetView(); changed = false; break;
                default: return;
            }
            e.Handled = true;
            if (changed) Raise();
        }

        // ------------------------------------------------------------ drawing

        private static Brush B(string key, Brush fallback) => Application.Current?.TryFindResource(key) as Brush ?? fallback;

        private FormattedText Text(string s, Brush b, double size = 10)
        {
            return new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            var bg = B("B.Sunken", Brushes.Black);
            var mute = B("B.TextMute", Brushes.Gray);
            var accent = B("B.Mint", Brushes.SeaGreen);
            dc.DrawRoundedRectangle(bg, null, new Rect(0, 0, w, h), 12, 12);
            var minor = new Pen(B("B.Line", Brushes.DimGray), 0.5);
            var major = new Pen(B("B.LineStrong", Brushes.Gray), 1);

            // line spacing: single chunks when they are wide enough, otherwise every 8 chunks
            int every = _cell >= 8 ? 1 : _cell >= 4 ? 4 : 8;
            double halfW = w / 2 / _cell, halfH = h / 2 / _cell;
            int x0 = (int)Math.Floor(_viewX - halfW) - 1, x1 = (int)Math.Ceiling(_viewX + halfW) + 1;
            int z0 = (int)Math.Floor(_viewZ - halfH) - 1, z1 = (int)Math.Ceiling(_viewZ + halfH) + 1;
            for (int x = x0 - Mod(x0, every); x <= x1; x += every)
            {
                bool region = Mod(x, 32) == 0;
                var sx = ScreenOf(x, 0).X;
                dc.DrawLine(region ? major : minor, new Point(sx, 0), new Point(sx, h));
            }
            for (int z = z0 - Mod(z0, every); z <= z1; z += every)
            {
                bool region = Mod(z, 32) == 0;
                var sz = ScreenOf(0, z).Y;
                dc.DrawLine(region ? major : minor, new Point(0, sz), new Point(w, sz));
            }

            // the selected square
            if (Radius > 0)
            {
                var tl = ScreenOf(CenterX - Radius, CenterZ - Radius);
                var br = ScreenOf(CenterX + Radius, CenterZ + Radius);
                var fill = accent.Clone(); fill.Opacity = 0.25; fill.Freeze();
                dc.DrawRectangle(fill, new Pen(accent, 2), new Rect(tl, br));
            }

            // the origin
            var o = ScreenOf(0, 0);
            var axis = new Pen(accent, 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(axis, new Point(o.X, 0), new Point(o.X, h));
            dc.DrawLine(axis, new Point(0, o.Y), new Point(w, o.Y));

            // coordinate labels on the region boundaries (every 32 chunks = 512 blocks)
            int labelEvery = _cell >= 14 ? 8 : _cell >= 6 ? 32 : 64;
            for (int x = x0 - Mod(x0, labelEvery); x <= x1; x += labelEvery)
                dc.DrawText(Text($"{x}", mute), new Point(ScreenOf(x, 0).X + 3, 2));
            for (int z = z0 - Mod(z0, labelEvery); z <= z1; z += labelEvery)
                dc.DrawText(Text($"{z}", mute), new Point(3, ScreenOf(0, z).Y + 2));

            // readout
            string readout = Radius > 0
                ? $"Square {Radius * 2} x {Radius * 2} chunks, chunks {CenterX - Radius},{CenterZ - Radius} to {CenterX + Radius - 1},{CenterZ + Radius - 1}  ·  blocks {(CenterX - Radius) * 16},{(CenterZ - Radius) * 16} to {(CenterX + Radius) * 16 - 1},{(CenterZ + Radius) * 16 - 1}"
                : "No square drawn: nothing will be pre-generated.";
            if (_hover is { } hv) readout += $"  ·  pointer: chunk {hv.X},{hv.Z} (blocks {hv.X * 16}..{hv.X * 16 + 15}, {hv.Z * 16}..{hv.Z * 16 + 15})";
            var ft = Text(readout, B("B.Text", Brushes.White), 11);
            ft.MaxTextWidth = Math.Max(10, w - 12); ft.MaxLineCount = 2; ft.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawRoundedRectangle(bg, null, new Rect(4, h - ft.Height - 8, Math.Min(w - 8, ft.Width + 12), ft.Height + 6), 6, 6);
            dc.DrawText(ft, new Point(10, h - ft.Height - 5));

            if (IsKeyboardFocused) dc.DrawRoundedRectangle(null, new Pen(accent, 2), new Rect(1, 1, w - 2, h - 2), 12, 12);
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) => InvalidateVisual();
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) => InvalidateVisual();

        private static int Mod(int a, int m) => ((a % m) + m) % m;
    }
}
