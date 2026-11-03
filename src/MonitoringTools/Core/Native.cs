using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MonitoringTools.Core
{
    internal static class Native
    {
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOPMOST = 0x00000008;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetClipboardOwner();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr value);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out int state);

        // QUERY_USER_NOTIFICATION_STATE values
        public const int QUNS_BUSY = 2;
        public const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
        public const int QUNS_PRESENTATION_MODE = 4;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public static void MakeToolWindow(Window w, bool noActivate)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            long ex = GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64();
            ex |= WS_EX_TOOLWINDOW;
            if (noActivate) ex |= WS_EX_NOACTIVATE;
            SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(ex));
        }

        public static void ForceTopmost(Window w)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public static void ApplyDarkChrome(Window w)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int on = 1; DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4);
                int round = 2; DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
            }
            catch { }
        }

        public static Point CursorDip(Visual v)
        {
            GetCursorPos(out var p);
            var src = PresentationSource.FromVisual(v);
            if (src?.CompositionTarget == null) return new Point(p.X, p.Y);
            return src.CompositionTarget.TransformFromDevice.Transform(new Point(p.X, p.Y));
        }
    }
}
