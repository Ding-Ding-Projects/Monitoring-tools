using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MonitoringTools.Core;

namespace MonitoringTools.Clip
{
    /// <summary>Listens for WM_CLIPBOARDUPDATE on a message-only window and turns changes into ClipItems.</summary>
    public sealed class ClipboardWatcher : IDisposable
    {
        public event Action<ClipItem> Changed;

        private HwndSource _source;
        private readonly DispatcherTimer _debounce;
        private string _lastSignature;
        private DateTime _lastAt;
        private static DateTime _ignoreUntil = DateTime.MinValue;

        public ClipboardWatcher()
        {
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _debounce.Tick += (s, e) => { _debounce.Stop(); Read(); };
        }

        public void Start()
        {
            var p = new HwndSourceParameters("MonitoringTools.Clipboard") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0 };
            _source = new HwndSource(p);
            _source.AddHook(Hook);
            if (!Native.AddClipboardFormatListener(_source.Handle))
                Log.Warn("AddClipboardFormatListener failed: " + Marshal.GetLastWin32Error());
        }

        /// <summary>True while the watcher is set to ignore the app's own write. For the self-test.</summary>
        internal static bool IgnoringForTest => DateTime.UtcNow < _ignoreUntil;
        /// <summary>How many times the watcher has been told to ignore the app's own write. For the self-test, which cannot rely on the 900 ms window still being open.</summary>
        internal static int IgnoreCallsForTest { get; private set; }

        /// <summary>Call before the app itself writes the clipboard so its own copy does not pop a toast.</summary>
        public static void IgnoreNextChange() { _ignoreUntil = DateTime.UtcNow.AddMilliseconds(900); IgnoreCallsForTest++; }

        /// <summary>Self-test seam: when set, SetText hands the text here instead of touching the real clipboard.</summary>
        internal static Action<string> TextSinkForTest;

        public static void SetText(string text)
        {
            if (TextSinkForTest != null) { IgnoreNextChange(); TextSinkForTest(text); return; }
            IgnoreNextChange();
            Retry(() => Clipboard.SetText(text));
        }

        public static void SetFiles(params string[] files)
        {
            IgnoreNextChange();
            var col = new StringCollection();
            col.AddRange(files);
            Retry(() => Clipboard.SetFileDropList(col));
        }

        public static void SetImage(BitmapSource img)
        {
            IgnoreNextChange();
            Retry(() => Clipboard.SetImage(img));
        }

        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg == Native.WM_CLIPBOARDUPDATE)
            {
                _debounce.Stop();
                _debounce.Start();
            }
            return IntPtr.Zero;
        }

        private void Read()
        {
            if (DateTime.UtcNow < _ignoreUntil) return;
            ClipItem item = null;
            try
            {
                IDataObject data = null;
                Retry(() => data = Clipboard.GetDataObject());
                if (data == null) return;

                if (Settings.Current.IgnoreSensitiveSources && IsSensitive(data)) return;

                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Length > 0) item = ClipItem.FromFiles(files);
                }
                if (item == null && data.GetDataPresent(DataFormats.UnicodeText))
                {
                    var text = data.GetData(DataFormats.UnicodeText) as string;
                    if (!string.IsNullOrEmpty(text)) item = ClipItem.FromText(text);
                }
                if (item == null && (data.GetDataPresent(DataFormats.Bitmap) || data.GetDataPresent(DataFormats.Dib)))
                {
                    BitmapSource img = null;
                    Retry(() => img = Clipboard.GetImage());
                    if (img != null)
                    {
                        var copy = new WriteableBitmap(img);
                        copy.Freeze();
                        item = ClipItem.FromImage(copy);
                    }
                }
                if (item == null && data.GetDataPresent(DataFormats.WaveAudio))
                {
                    if (data.GetData(DataFormats.WaveAudio) is Stream s)
                    {
                        using var ms = new MemoryStream();
                        s.CopyTo(ms);
                        item = ClipItem.FromAudio(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Clipboard read failed: " + ex.Message);
                return;
            }

            if (item == null) return;
            try { item.SourceApp = NativeInput.ClipboardApp(); } catch { }
            if (item.Signature == _lastSignature && (DateTime.UtcNow - _lastAt).TotalSeconds < 1.5) return;
            _lastSignature = item.Signature;
            _lastAt = DateTime.UtcNow;
            Changed?.Invoke(item);
        }

        /// <summary>Password managers mark their copies so viewers and history skip them.</summary>
        private static bool IsSensitive(IDataObject data)
        {
            var formats = data.GetFormats(false);
            if (formats.Contains("ExcludeClipboardContentFromMonitorProcessing")) return true;
            if (formats.Contains("Clipboard Viewer Ignore")) return true;
            foreach (var f in new[] { "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" })
            {
                if (!formats.Contains(f)) continue;
                try
                {
                    if (data.GetData(f) is MemoryStream ms && ms.Length >= 4)
                    {
                        var b = ms.ToArray();
                        if (BitConverter.ToInt32(b, 0) == 0) return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private static void Retry(Action a)
        {
            for (int i = 0; i < 6; i++)
            {
                try { a(); return; }
                catch (COMException) when (i < 5) { Thread.Sleep(40 + i * 30); }
            }
        }

        public void Dispose()
        {
            if (_source != null)
            {
                Native.RemoveClipboardFormatListener(_source.Handle);
                _source.Dispose();
                _source = null;
            }
        }
    }
}
