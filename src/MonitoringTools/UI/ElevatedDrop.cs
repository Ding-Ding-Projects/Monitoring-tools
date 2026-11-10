using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// File drops while the app runs as administrator.
    ///
    /// Windows does not deliver OLE drag and drop from a normal-rights program (File Explorer, the desktop) to a window of an
    /// elevated one: the drop is refused before the app sees it, so every drop zone looks dead. The older WM_DROPFILES route
    /// does work across that boundary once the window allows the message, but only when the window has no OLE drop target.
    ///
    /// So, when elevated, each window: allows WM_DROPFILES, WM_COPYDATA and WM_COPYGLOBALDATA through the message filter,
    /// revokes its OLE drop target, accepts dropped files, and turns each WM_DROPFILES into the same DragEnter, DragOver and
    /// Drop events WPF raises for an OLE drop, aimed at the element under the drop point. Every existing drop handler keeps
    /// working unchanged. Drags inside the app (moving tabs) put the OLE target back for the length of the drag.
    /// Only files arrive this way; text dragged from another program needs the app to run without elevation.
    /// </summary>
    public static class ElevatedDrop
    {
        private const int WM_DROPFILES = 0x0233, WM_COPYDATA = 0x004A, WM_COPYGLOBALDATA = 0x0049, MSGFLT_ALLOW = 1;

        [DllImport("user32.dll", SetLastError = true)] private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, int msg, int action, IntPtr changeInfo);
        [DllImport("shell32.dll")] private static extern void DragAcceptFiles(IntPtr hwnd, bool accept);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder file, int cch);
        [DllImport("shell32.dll")] private static extern bool DragQueryPoint(IntPtr hDrop, out POINT pt);
        [DllImport("shell32.dll")] private static extern void DragFinish(IntPtr hDrop);
        [DllImport("ole32.dll")] private static extern int RevokeDragDrop(IntPtr hwnd);
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

        /// <summary>Turns the bridge on without elevation, for the self-test.</summary>
        public static bool ForceForTest { get; set; }

        public static bool Active => ForceForTest || ServiceHost.IsElevated;

        /// <summary>How many drops the bridge has delivered (for the self-test and the log).</summary>
        public static int Delivered { get; private set; }

        private static readonly HashSet<IntPtr> Bridged = new HashSet<IntPtr>();
        // HwndSource can hold hooks weakly, so each window's hook is kept alive here until the window closes
        private static readonly Dictionary<IntPtr, HwndSourceHook> Hooks = new Dictionary<IntPtr, HwndSourceHook>();

        /// <summary>The last steps the bridge took, newest last (for the self-test and the log).</summary>
        public static readonly List<string> Trace = new List<string>();
        private static void Note(string s) { lock (Trace) { Trace.Add(s); if (Trace.Count > 40) Trace.RemoveAt(0); } }

        private static readonly MethodInfo RegisterTarget = typeof(DragDrop).GetMethod("RegisterDropTarget", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null);
        private static readonly MethodInfo RevokeTarget = typeof(DragDrop).GetMethod("RevokeDropTarget", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null);
        private static readonly ConstructorInfo ArgsCtor = typeof(DragEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(c => c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point) }));

        /// <summary>True when the framework pieces the bridge relies on are present in this .NET version.</summary>
        public static bool FrameworkReady => ArgsCtor != null && RegisterTarget != null && RevokeTarget != null;

        public static void Install()
        {
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) =>
            {
                if (s is Window w && Active)
                    // after WPF has registered its own drop target for the window
                    w.Dispatcher.BeginInvoke(new Action(() => Bridge(w)), System.Windows.Threading.DispatcherPriority.Loaded);
            }));
        }

        /// <summary>Sets one window up for drops from normal-rights programs. Safe to call more than once.</summary>
        public static void Bridge(Window w)
        {
            try
            {
                var hwnd = new WindowInteropHelper(w).Handle;
                if (hwnd == IntPtr.Zero || !Bridged.Add(hwnd)) return;
                foreach (var m in new[] { WM_DROPFILES, WM_COPYDATA, WM_COPYGLOBALDATA }) ChangeWindowMessageFilterEx(hwnd, m, MSGFLT_ALLOW, IntPtr.Zero);
                Revoke(hwnd);
                DragAcceptFiles(hwnd, true);
                HwndSourceHook hook = (IntPtr h, int msg, IntPtr wp, IntPtr lp, ref bool handled) =>
                {
                    if (msg != WM_DROPFILES) return IntPtr.Zero;
                    handled = true;
                    Note("WM_DROPFILES arrived");
                    OnDropFiles(w, wp);
                    return IntPtr.Zero;
                };
                Hooks[hwnd] = hook;
                var src = HwndSource.FromHwnd(hwnd);
                if (src == null) { Note("no HwndSource for the window"); return; }
                src.AddHook(hook);
                Note("bridged " + (w.Title ?? w.GetType().Name));
                w.Closed += (o, e) => { Bridged.Remove(hwnd); Hooks.Remove(hwnd); };
            }
            catch (Exception ex) { Note("bridge failed: " + ex.Message); Log.Warn("Elevated drop bridge not set up: " + ex.Message); }
        }

        /// <summary>
        /// Runs a drag that starts and ends inside the app (DragDrop.DoDragDrop) with the window's OLE drop target back in place,
        /// then takes it away again so files from File Explorer keep arriving.
        /// </summary>
        public static DragDropEffects InternalDrag(DependencyObject source, object data, DragDropEffects allowed)
        {
            var w = Window.GetWindow(source);
            var hwnd = w == null ? IntPtr.Zero : new WindowInteropHelper(w).Handle;
            bool swap = hwnd != IntPtr.Zero && Bridged.Contains(hwnd);
            try
            {
                if (swap) RegisterTarget?.Invoke(null, new object[] { hwnd });
                return DragDrop.DoDragDrop(source, data, allowed);
            }
            finally { if (swap) Revoke(hwnd); }
        }

        private static void Revoke(IntPtr hwnd)
        {
            try { if (RevokeTarget != null) RevokeTarget.Invoke(null, new object[] { hwnd }); else RevokeDragDrop(hwnd); }
            catch { RevokeDragDrop(hwnd); }
        }

        private static void OnDropFiles(Window w, IntPtr hDrop)
        {
            string[] files;
            POINT pt;
            try
            {
                uint n = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                files = new string[n];
                for (uint i = 0; i < n; i++)
                {
                    var len = (int)DragQueryFile(hDrop, i, null, 0) + 1;
                    var sb = new StringBuilder(len);
                    DragQueryFile(hDrop, i, sb, len);
                    files[i] = sb.ToString();
                }
                DragQueryPoint(hDrop, out pt);
            }
            finally { DragFinish(hDrop); }
            Note($"{files.Length} file(s) at {pt.X},{pt.Y}");
            if (files.Length == 0) return;
            w.Activate();
            Deliver(w, files, new Point(pt.X, pt.Y), devicePixels: true);
        }

        /// <summary>Raises the drag and drop events for these files at a point in the window, as an OLE drop would.</summary>
        public static bool Deliver(Window w, string[] files, Point at, bool devicePixels = false)
        {
            if (ArgsCtor == null) { Log.Warn("Elevated drop: this .NET version has no DragEventArgs constructor to use."); return false; }
            var src = PresentationSource.FromVisual(w);
            if (devicePixels && src?.CompositionTarget != null) at = src.CompositionTarget.TransformFromDevice.Transform(at);
            var root = w.Content as UIElement ?? w;
            var hit = w.InputHitTest(at) as DependencyObject ?? root;
            // InputHitTest can land on a text run; climb to something that can raise routed events
            while (hit != null && !(hit is UIElement) && !(hit is ContentElement))
                hit = hit is Visual ? VisualTreeHelper.GetParent(hit) : LogicalTreeHelper.GetParent(hit);
            hit ??= w;
            Note("target " + hit.GetType().Name + " at " + at);
            var data = new DataObject(DataFormats.FileDrop, files);
            const DragDropEffects allowed = DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move;

            DragEventArgs Raise(RoutedEvent preview, RoutedEvent bubble)
            {
                var a = (DragEventArgs)ArgsCtor.Invoke(new object[] { data, DragDropKeyStates.None, allowed, w, at });
                a.RoutedEvent = preview;
                RaiseOn(hit, a);
                if (a.Handled) return a;
                var b = (DragEventArgs)ArgsCtor.Invoke(new object[] { data, DragDropKeyStates.None, allowed, w, at });
                b.RoutedEvent = bubble;   // before anything else: WPF refuses Handled on arguments without an event
                b.Effects = a.Effects;
                RaiseOn(hit, b);
                return b;
            }

            try
            {
                Raise(DragDrop.PreviewDragEnterEvent, DragDrop.DragEnterEvent);
                var over = Raise(DragDrop.PreviewDragOverEvent, DragDrop.DragOverEvent);
                if (over.Effects == DragDropEffects.None)
                {
                    Raise(DragDrop.PreviewDragLeaveEvent, DragDrop.DragLeaveEvent);
                    Note("refused in DragOver");
                    Log.Info($"Elevated drop: the target refused {files.Length} file(s)");
                    return false;
                }
                Raise(DragDrop.PreviewDropEvent, DragDrop.DropEvent);
                Delivered++;
                Log.Info($"Elevated drop: delivered {files.Length} file(s)");
                return true;
            }
            catch (Exception ex) { Note("delivery failed: " + (ex.InnerException ?? ex).Message); Log.Warn("Elevated drop failed: " + ex.Message); return false; }
        }

        private static void RaiseOn(DependencyObject target, RoutedEventArgs a)
        {
            if (target is UIElement u) u.RaiseEvent(a);
            else if (target is ContentElement c) c.RaiseEvent(a);
            else if (target is UIElement3D u3) u3.RaiseEvent(a);
        }
    }
}
