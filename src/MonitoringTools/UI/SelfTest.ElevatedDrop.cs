using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr h);
        [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr h);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp);

        /// <summary>Builds a real HDROP (DROPFILES + wide, double-null-terminated paths), as File Explorer sends with WM_DROPFILES.</summary>
        private static IntPtr MakeHDrop(string[] files, int x, int y)
        {
            var list = string.Join("\0", files) + "\0\0";
            var bytes = Encoding.Unicode.GetBytes(list);
            var h = GlobalAlloc(0x0042 /* GMEM_MOVEABLE | GMEM_ZEROINIT */, (UIntPtr)(20 + bytes.Length));
            var p = GlobalLock(h);
            Marshal.WriteInt32(p, 0, 20);   // pFiles: offset of the list
            Marshal.WriteInt32(p, 4, x);    // pt.x (client)
            Marshal.WriteInt32(p, 8, y);    // pt.y
            Marshal.WriteInt32(p, 12, 0);   // fNC
            Marshal.WriteInt32(p, 16, 1);   // fWide
            Marshal.Copy(bytes, 0, p + 20, bytes.Length);
            GlobalUnlock(h);
            return h;
        }

        /// <summary>Drops from File Explorer while the app runs as administrator: WM_DROPFILES reaches the drop zone under the point,
        /// a zone that refuses gets no Drop, and tab drags inside the app still have a drop target. Run on their own (MT_ONLY=elevateddrop).</summary>
        private static async Task ElevatedDropChecksAsync()
        {
            Check("elevated drop: this .NET has the pieces the bridge uses", () => ElevatedDrop.FrameworkReady ? null : "DragEventArgs constructor or DragDrop.RegisterDropTarget/RevokeDropTarget not found");

            ElevatedDrop.ForceForTest = true;
            var w = new Window { Width = 600, Height = 300, Title = "Elevated drop check", WindowStartupLocation = WindowStartupLocation.CenterScreen };
            try
            {
                string[] got = null; bool refusedDropped = false; bool lit = false;
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
                var zone = new Border { Background = Brushes.LightGreen, AllowDrop = true };
                zone.DragEnter += (s, e) => { lit = true; e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
                zone.DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
                zone.Drop += (s, e) => { got = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Handled = true; };
                var refuse = new Border { Background = Brushes.LightPink, AllowDrop = true };
                refuse.DragOver += (s, e) => { e.Effects = DragDropEffects.None; e.Handled = true; };
                refuse.Drop += (s, e) => refusedDropped = true;
                Grid.SetColumn(refuse, 1);
                grid.Children.Add(zone); grid.Children.Add(refuse);
                w.Content = grid;
                w.Show();
                await Task.Delay(600);
                var hwnd = new WindowInteropHelper(w).Handle;
                var scale = PresentationSource.FromVisual(w)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                var files = new[] { @"C:\Users\Sam\Pictures\holiday photo.png", @"C:\Users\Sam\Documents\notes.txt" };

                await CheckAsync("elevated drop: the events reach the drop zone under a point (without the message)", async () =>
                {
                    var pt = zone.TranslatePoint(new Point(zone.ActualWidth / 2, zone.ActualHeight / 2), w);
                    ElevatedDrop.Deliver(w, files, pt);
                    await Task.Delay(100);
                    var r = got == null ? "the zone got no Drop; bridge trace: " + string.Join(" › ", ElevatedDrop.Trace) : null;
                    got = null; lit = false;
                    return r;
                });

                await CheckAsync("elevated drop: a WM_DROPFILES from File Explorer reaches the drop zone under the point", async () =>
                {
                    int before = ElevatedDrop.Delivered;
                    var pt = zone.TranslatePoint(new Point(zone.ActualWidth / 2, zone.ActualHeight / 2), w);
                    PostMessage(hwnd, 0x0233, MakeHDrop(files, (int)(pt.X * scale), (int)(pt.Y * scale)), IntPtr.Zero);
                    for (int i = 0; i < 20 && got == null; i++) await Task.Delay(100);
                    if (got == null) return "the zone got no Drop; bridge trace: " + string.Join(" › ", ElevatedDrop.Trace);
                    if (got.Length != 2 || got[0] != files[0] || got[1] != files[1]) return "the zone got: " + string.Join(" | ", got);
                    if (!lit) return "the zone got no DragEnter, so it could not light up";
                    if (ElevatedDrop.Delivered != before + 1) return "the bridge did not count the drop";
                    return null;
                });

                await CheckAsync("elevated drop: a zone that refuses the files in DragOver gets no Drop", async () =>
                {
                    var pt = refuse.TranslatePoint(new Point(refuse.ActualWidth / 2, refuse.ActualHeight / 2), w);
                    PostMessage(hwnd, 0x0233, MakeHDrop(files, (int)(pt.X * scale), (int)(pt.Y * scale)), IntPtr.Zero);
                    await Task.Delay(800);
                    return refusedDropped ? "the refusing zone still got Drop" : null;
                });

                Check("elevated drop: the window's OLE drop target can come back for a drag inside the app", () =>
                {
                    // the swap InternalDrag does around DoDragDrop, without starting a modal drag
                    var reg = typeof(DragDrop).GetMethod("RegisterDropTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var rev = typeof(DragDrop).GetMethod("RevokeDropTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    try { reg.Invoke(null, new object[] { hwnd }); rev.Invoke(null, new object[] { hwnd }); }
                    catch (Exception ex) { return "register and revoke threw: " + (ex.InnerException ?? ex).Message; }
                    return null;
                });
            }
            finally { w.Close(); ElevatedDrop.ForceForTest = false; }
        }
    }
}
