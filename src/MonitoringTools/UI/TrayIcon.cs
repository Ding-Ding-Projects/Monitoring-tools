using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>The notification-area icon. Its dot shows state: mint online, amber offline, slate paused, violet game mode.</summary>
    public sealed class TrayIcon : IDisposable
    {
        private readonly System.Windows.Forms.NotifyIcon _icon;
        private IntPtr _hicon = IntPtr.Zero;
        private TrayMenuWindow _menu;
        private string _lastKey;

        public TrayIcon()
        {
            _icon = new System.Windows.Forms.NotifyIcon { Visible = true, Text = "Monitoring Tools" };
            _icon.MouseUp += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Right || e.Button == System.Windows.Forms.MouseButtons.Left)
                    App.Current.Dispatcher.BeginInvoke(new Action(ShowMenu));
            };
            _icon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    App.Current.Dispatcher.BeginInvoke(new Action(() => { _menu?.CloseAnimated(); App.Current.OpenControlCentre(null); }));
            };
        }

        public void ShowMenu()
        {
            if (_menu != null) { _menu.CloseAnimated(); _menu = null; return; }
            _menu = new TrayMenuWindow();
            _menu.Closed += (s, e) => _menu = null;
            _menu.ShowAtCursor();
        }

        public (Color dot, string status) State()
        {
            var s = Settings.Current;
            if (App.Net?.Current != null && !App.Net.Current.Online) return (Color.FromArgb(0xFF, 0xB5, 0x47), "offline · " + (App.Net.Current.Headline ?? ""));
            if (App.IsSnoozed) return (Color.FromArgb(0x9A, 0xA1, 0xAD), $"snoozed until {App.SnoozeUntil:HH:mm}");
            if (s.GameModeEnabled && App.Games != null && App.Games.IsGaming) return (Color.FromArgb(0xB9, 0xA2, 0xFF), "game mode · " + App.Games.ActiveGame);
            if (!s.ClipboardEnabled && !s.NetworkEnabled) return (Color.FromArgb(0x9A, 0xA1, 0xAD), "paused");
            return (Color.FromArgb(0x7C, 0xF2, 0xC2), "online · watching clipboard");
        }

        public void Refresh()
        {
            var (dot, status) = State();
            var key = dot.ToArgb() + status;
            var text = ("Monitoring Tools · " + status);
            _icon.Text = text.Length > 120 ? text[..120] : text;
            if (key == _lastKey) return;
            _lastKey = key;

            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var path = Rounded(new RectangleF(1, 1, 30, 30), 8))
                using (var bg = new SolidBrush(Color.FromArgb(0x14, 0x16, 0x1B)))
                using (var edge = new Pen(Color.FromArgb(0x3A, 0x40, 0x4C), 1.2f))
                {
                    g.FillPath(bg, path);
                    g.DrawPath(edge, path);
                }
                using (var line = new Pen(Color.FromArgb(0xE8, 0xEA, 0xED), 2.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLines(line, new[]
                    {
                        new PointF(5, 16), new PointF(10, 16), new PointF(13.5f, 8), new PointF(18, 24), new PointF(21, 16), new PointF(25, 16)
                    });
                }
                using (var ring = new SolidBrush(Color.FromArgb(0x14, 0x16, 0x1B)))
                using (var fill = new SolidBrush(dot))
                {
                    g.FillEllipse(ring, 19, 19, 13, 13);
                    g.FillEllipse(fill, 21, 21, 9, 9);
                }
            }
            var h = bmp.GetHicon();
            var old = _icon.Icon;
            _icon.Icon = Icon.FromHandle(h);
            if (_hicon != IntPtr.Zero) Native.DestroyIcon(_hicon);
            old?.Dispose();
            _hicon = h;
        }

        private static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            if (_hicon != IntPtr.Zero) Native.DestroyIcon(_hicon);
        }
    }
}
