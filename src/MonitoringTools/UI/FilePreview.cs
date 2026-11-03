using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Rich previews for a copied path: shell thumbnails (video frames, PDFs, Office files, album art),
    /// text and code heads, rendered Markdown, folder and zip listings, and media details from ffprobe.
    /// Everything slow runs off the UI thread and fills in when ready.
    /// </summary>
    public static class FilePreview
    {
        private static readonly string[] TextExt =
        { ".txt", ".log", ".md", ".markdown", ".csv", ".json", ".xml", ".yml", ".yaml", ".ini", ".cfg", ".conf", ".toml",
          ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".ps1", ".bat", ".cmd", ".sh", ".html", ".css", ".scss", ".cpp", ".c", ".h",
          ".java", ".go", ".rs", ".rb", ".php", ".sql", ".xaml", ".csproj", ".sln", ".env", ".gitignore", ".dockerfile" };

        /// <summary>Returns a panel that shows a placeholder at once and fills in the preview asynchronously.</summary>
        public static FrameworkElement Build(string path, Action layoutChanged)
        {
            var host = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var kind = ClipItem.Classify(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path);
            bool isText = TextExt.Contains(ext) || name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".") && !Directory.Exists(path);

            if (kind == FileKind.Folder) { _ = FillFolder(host, path, layoutChanged); return host; }
            if (ext == ".zip") { _ = FillZip(host, path, layoutChanged); return host; }
            if (isText) { _ = FillText(host, path, ext, layoutChanged); return host; }

            // Everything else tries a shell thumbnail; media also gets ffprobe details
            _ = FillThumbnail(host, path, kind, layoutChanged);
            return host;
        }

        // -------------------------------------------------------------- text and code

        private static async Task FillText(StackPanel host, string path, string ext, Action changed)
        {
            string head = await Task.Run(() =>
            {
                try
                {
                    var fi = new FileInfo(path);
                    if (fi.Length > 5 * 1024 * 1024) return null;
                    using var r = new StreamReader(path, Encoding.UTF8, true);
                    var buf = new char[4000];
                    int n = r.Read(buf, 0, buf.Length);
                    var s = new string(buf, 0, n);
                    if (s.Count(c => c == '\0') > 2) return null; // binary
                    return s;
                }
                catch { return null; }
            });
            if (string.IsNullOrEmpty(head)) return;

            UIElement view;
            if ((ext == ".md" || ext == ".markdown") && Core.Settings.Current.RenderMarkdown)
            {
                view = Markdown.Render(head, 10);
            }
            else if (ext == ".csv")
            {
                view = CsvTable(head);
            }
            else
            {
                var lines = head.Replace("\r\n", "\n").Split('\n').Take(10).ToArray();
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.Children.Add(new TextBlock
                {
                    Text = string.Join("\n", Enumerable.Range(1, lines.Length)), FontFamily = Ui.Font("F.Mono"), FontSize = 11.5,
                    Foreground = Ui.Res("B.TextMute"), TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 10, 0), Opacity = 0.7
                });
                var code = new TextBlock
                {
                    Text = string.Join("\n", lines.Select(l => l.Length > 90 ? l[..90] + "…" : l)), FontFamily = Ui.Font("F.Mono"), FontSize = 11.5,
                    Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.NoWrap
                };
                Grid.SetColumn(code, 1);
                g.Children.Add(code);
                view = g;
            }
            host.Children.Add(Frame(view));
            Ui.Rise(host, 0, 6, 300);
            changed?.Invoke();
        }

        private static UIElement CsvTable(string head)
        {
            var rows = head.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).Take(6)
                .Select(l => l.Split(l.Count(c => c == ';') > l.Count(c => c == ',') ? ';' : ',').Take(5).ToArray()).ToArray();
            int cols = rows.Max(r => r.Length);
            var g = new Grid();
            for (int c = 0; c < cols; c++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int r = 0; r < rows.Length; r++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int c = 0; c < rows[r].Length; c++)
                {
                    var v = rows[r][c].Trim().Trim('"');
                    var t = new TextBlock
                    {
                        Text = v.Length > 18 ? v[..18] + "…" : v, FontFamily = Ui.Font("F.Mono"), FontSize = 11.5, Margin = new Thickness(0, 0, 14, 3),
                        Foreground = r == 0 ? Ui.Res("B.Mint") : Ui.Res("B.TextSoft"), FontWeight = r == 0 ? FontWeights.SemiBold : FontWeights.Normal
                    };
                    Grid.SetRow(t, r); Grid.SetColumn(t, c);
                    g.Children.Add(t);
                }
            }
            return g;
        }

        // -------------------------------------------------------------- folders and archives

        private static async Task FillFolder(StackPanel host, string path, Action changed)
        {
            var info = await Task.Run(() =>
            {
                try
                {
                    var entries = new DirectoryInfo(path).EnumerateFileSystemInfos().Take(400).ToList();
                    var dirs = entries.OfType<DirectoryInfo>().Count();
                    var files = entries.OfType<FileInfo>().ToList();
                    var top = entries.OrderByDescending(e => e is DirectoryInfo).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Take(6)
                                     .Select(e => (e is DirectoryInfo ? "▸ " : "  ") + e.Name).ToList();
                    var kinds = files.GroupBy(f => f.Extension.ToLowerInvariant()).OrderByDescending(x => x.Count()).Take(3)
                                     .Select(x => $"{x.Count()} {(x.Key.Length == 0 ? "other" : x.Key)}");
                    return (top, $"{dirs} folders · {files.Count} files · {Ui.Bytes(files.Sum(f => f.Length))}" + (files.Count > 0 ? " · " + string.Join(", ", kinds) : ""), entries.Count >= 400);
                }
                catch (Exception ex) { return (new System.Collections.Generic.List<string> { ex.Message }, "", false); }
            });
            var sp = new StackPanel();
            sp.Children.Add(Ui.Mono(string.Join("\n", info.Item1) + (info.Item3 ? "\n  …" : ""), Ui.Res("B.TextSoft"), 11.5));
            host.Children.Add(Frame(sp));
            if (info.Item2.Length > 0) host.Children.Add(Meta(info.Item2));
            Ui.Rise(host, 0, 6, 300);
            changed?.Invoke();
        }

        private static async Task FillZip(StackPanel host, string path, Action changed)
        {
            var info = await Task.Run(() =>
            {
                try
                {
                    using var z = ZipFile.OpenRead(path);
                    var all = z.Entries;
                    var top = all.Where(e => e.FullName.Count(c => c == '/') <= 1).Take(6).Select(e => "  " + e.FullName).ToList();
                    long raw = all.Sum(e => e.Length), packed = all.Sum(e => e.CompressedLength);
                    return (top, $"{all.Count} entries · {Ui.Bytes(raw)} unpacked · {(raw > 0 ? 100 - packed * 100 / raw : 0)}% saved");
                }
                catch (Exception ex) { return (new System.Collections.Generic.List<string> { ex.Message }, ""); }
            });
            host.Children.Add(Frame(Ui.Mono(string.Join("\n", info.Item1), Ui.Res("B.TextSoft"), 11.5)));
            if (info.Item2.Length > 0) host.Children.Add(Meta(info.Item2));
            Ui.Rise(host, 0, 6, 300);
            changed?.Invoke();
        }

        // -------------------------------------------------------------- thumbnails and media details

        private static async Task FillThumbnail(StackPanel host, string path, FileKind kind, Action changed)
        {
            var thumbTask = ShellThumbnailAsync(path, 480);
            var probeTask = kind == FileKind.Video || kind == FileKind.Audio ? ProbeAsync(path) : Task.FromResult<string>(null);
            var thumb = await thumbTask;

            if (thumb != null)
            {
                double ratio = thumb.PixelHeight > 0 ? (double)thumb.PixelWidth / thumb.PixelHeight : 1.6;
                bool square = kind == FileKind.Audio || Math.Abs(ratio - 1) < 0.05;
                double h = square ? 120 : Math.Clamp(364 / ratio, 90, 200);
                var frame = new Border
                {
                    Height = h,
                    Width = square ? 120 : Math.Min(364, h * ratio),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(12),
                    Background = new ImageBrush(thumb) { Stretch = Stretch.UniformToFill },
                    BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1)
                };
                if (kind == FileKind.Video)
                {
                    // play badge over the frame
                    frame.Child = new Border
                    {
                        Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = new SolidColorBrush(Color.FromArgb(0xB0, 0x08, 0x09, 0x0C)),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                        Child = Ui.Icon(Icons.Play, Ui.Res("B.Text"), 14)
                    };
                }
                host.Children.Add(frame);
                Ui.Rise(frame, 0, 8, 360);
                changed?.Invoke();
            }

            var details = await probeTask;
            if (!string.IsNullOrEmpty(details))
            {
                host.Children.Add(Meta(details));
                changed?.Invoke();
            }
        }

        private static Border Frame(UIElement child) => new Border
        {
            Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8, 10, 8),
            BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(1), Child = child, ClipToBounds = true
        };

        private static TextBlock Meta(string s)
        {
            var t = Ui.Mono(s, Ui.Res("B.TextMute"), 11);
            t.Margin = new Thickness(0, 6, 0, 0);
            return t;
        }

        /// <summary>Duration, resolution, frame rate and codecs from ffprobe, when it is installed.</summary>
        private static Task<string> ProbeAsync(string path)
        {
            return Task.Run(() =>
            {
                try
                {
                    var ff = Converter.FindFfmpeg();
                    if (ff == null) return null;
                    var probe = Path.Combine(Path.GetDirectoryName(ff), "ffprobe.exe");
                    if (!File.Exists(probe)) return null;
                    var psi = new ProcessStartInfo(probe, $"-v error -show_entries format=duration,bit_rate:stream=codec_type,codec_name,width,height,avg_frame_rate,sample_rate,channels -of default=nw=1 \"{path}\"")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                    using var p = Process.Start(psi);
                    var o = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(6000)) { try { p.Kill(); } catch { } return null; }

                    string Get(string key, int nth = 0) => Regex.Matches(o, $"^{key}=(.+)$", RegexOptions.Multiline).Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Skip(nth).FirstOrDefault();
                    var parts = new System.Collections.Generic.List<string>();
                    if (double.TryParse(Get("duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur) && dur > 0)
                    {
                        var t = TimeSpan.FromSeconds(dur);
                        parts.Add(t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss"));
                    }
                    var w = Get("width"); var h = Get("height");
                    if (w != null && h != null && w != "N/A") parts.Add($"{w}×{h}");
                    var fr = Get("avg_frame_rate");
                    if (fr != null && fr.Contains('/'))
                    {
                        var f = fr.Split('/');
                        if (double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && b > 0 && a > 0)
                            parts.Add($"{a / b:0.##} fps");
                    }
                    var codecs = Regex.Matches(o, "^codec_name=(.+)$", RegexOptions.Multiline).Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Distinct().Take(3);
                    var c = string.Join(" + ", codecs);
                    if (c.Length > 0) parts.Add(c);
                    var sr = Get("sample_rate");
                    if (sr != null && int.TryParse(sr, out var hz)) parts.Add($"{hz / 1000.0:0.#} kHz");
                    var ch = Get("channels");
                    if (ch == "1") parts.Add("mono"); else if (ch == "2") parts.Add("stereo"); else if (ch != null) parts.Add(ch + " ch");
                    if (long.TryParse(Get("bit_rate"), out var br) && br > 0) parts.Add($"{br / 1000} kb/s");
                    return parts.Count == 0 ? null : string.Join(" · ", parts);
                }
                catch { return null; }
            });
        }

        // -------------------------------------------------------------- Windows shell thumbnails

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr h);

        private const int SIIGBF_BIGGERSIZEOK = 0x1, SIIGBF_THUMBNAILONLY = 0x8;

        /// <summary>Asks Explorer's thumbnail providers for a real preview (no generic file icons).</summary>
        public static Task<BitmapSource> ShellThumbnailAsync(string path, int size)
        {
            var tcs = new TaskCompletionSource<BitmapSource>();
            var t = new Thread(() =>
            {
                IntPtr hbm = IntPtr.Zero;
                try
                {
                    SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var f);
                    int hr = f.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_THUMBNAILONLY | SIIGBF_BIGGERSIZEOK, out hbm);
                    Marshal.ReleaseComObject(f);
                    if (hr != 0 || hbm == IntPtr.Zero) { tcs.TrySetResult(null); return; }
                    var src = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    // shell bitmaps carry premultiplied alpha that WPF reads as fully transparent; drop it
                    var fixedSrc = new FormatConvertedBitmap(src, PixelFormats.Bgr32, null, 0);
                    fixedSrc.Freeze();
                    tcs.TrySetResult(fixedSrc);
                }
                catch { tcs.TrySetResult(null); }
                finally { if (hbm != IntPtr.Zero) DeleteObject(hbm); }
            }) { IsBackground = true, Name = "thumbnail" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            return tcs.Task;
        }
    }
}
