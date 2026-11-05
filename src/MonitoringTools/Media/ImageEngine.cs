using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MonitoringTools.Media
{
    public sealed class ImageOptions
    {
        public int Width, Height;          // 0 means not set
        public bool KeepProportions = true;
        public string Crop = "none";        // none, 1:1, 4:3, 16:9, free
        public bool UseTarget;              // aim for a file size instead of a fixed quality
        public int Quality = 82;
        public long TargetBytes;
        public string Folder;               // null: next to the original
    }

    public sealed class ImageResult
    {
        public bool Ok;
        public string Source, Output, Error;
        public long SourceBytes, OutputBytes;
        public int Width, Height, Quality;
        public bool TargetMissed;           // the quality floor was reached before the target size
    }

    /// <summary>Resize, crop and compress for the Images dialog. WPF imaging does the work, WebP goes through FFmpeg. The original
    /// is never touched: copies get the suffix -small (or go to the chosen folder).</summary>
    public static class ImageEngine
    {
        public const int QualityFloor = 40;

        public static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".heic" };

        public static bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        public static string OutputExtension(string src)
        {
            switch (Path.GetExtension(src).ToLowerInvariant())
            {
                case ".jpg": case ".jpeg": case ".heic": return ".jpg";
                case ".tif": case ".tiff": return ".tif";
                default: return Path.GetExtension(src).ToLowerInvariant();
            }
        }

        public static string OutputPath(string src, string folder) =>
            MediaProc.UniquePath(folder ?? Path.GetDirectoryName(src), Path.GetFileNameWithoutExtension(src) + "-small", OutputExtension(src), src);

        public static (int Width, int Height) PixelSize(string path)
        {
            try
            {
                if (Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    var mi = MediaProc.Probe(path);
                    return (mi.Width, mi.Height);
                }
                var f = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                return (f.PixelWidth, f.PixelHeight);
            }
            catch { return (0, 0); }
        }

        /// <summary>Decodes a picture fully. WebP goes through FFmpeg because Windows has no WebP decoder in every install.</summary>
        public static async Task<BitmapSource> LoadAsync(string path, CancellationToken ct, int decodeWidth = 0)
        {
            if (Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                var r = await MediaProc.RunBytesAsync($"-i {MediaProc.Quote(path)} -frames:v 1 -f image2pipe -vcodec png -", ct);
                if (!r.Ok) throw new InvalidDataException("FFmpeg could not read this WebP picture: " + MediaProc.Plain(r.Error));
                return MediaProc.Decode(r.Data);
            }
            return await Task.Run(() =>
            {
                var dec = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (dec.Frames.Count > 1 && Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("This GIF is animated. Only the first picture would be kept, so it was not changed.");
                var fr = dec.Frames[0];
                if (decodeWidth > 0 && fr.PixelWidth > decodeWidth)
                {
                    var bi = new BitmapImage();
                    bi.BeginInit(); bi.UriSource = new Uri(path); bi.DecodePixelWidth = decodeWidth; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
                    return (BitmapSource)bi;
                }
                fr.Freeze();
                return (BitmapSource)fr;
            }, ct);
        }

        public static Int32Rect CropRect(int w, int h, string crop, double freeAspect)
        {
            double aspect = crop switch { "1:1" => 1.0, "4:3" => 4.0 / 3, "16:9" => 16.0 / 9, "free" => freeAspect > 0 ? freeAspect : 0, _ => 0 };
            if (aspect <= 0) return new Int32Rect(0, 0, w, h);
            int cw = w, ch = h;
            if ((double)w / h > aspect) cw = Math.Max(1, (int)Math.Round(h * aspect)); else ch = Math.Max(1, (int)Math.Round(w / aspect));
            return new Int32Rect((w - cw) / 2, (h - ch) / 2, cw, ch);
        }

        /// <summary>The size a picture ends up at after the crop and the resize.</summary>
        public static (int W, int H) OutputSize(int srcW, int srcH, ImageOptions o)
        {
            double freeAspect = o.Width > 0 && o.Height > 0 ? (double)o.Width / o.Height : 0;
            var c = CropRect(srcW, srcH, o.Crop, freeAspect);
            int w = c.Width, h = c.Height;
            if (o.Width <= 0 && o.Height <= 0) return (w, h);
            if (o.KeepProportions)
            {
                double sx = o.Width > 0 ? (double)o.Width / w : double.MaxValue;
                double sy = o.Height > 0 ? (double)o.Height / h : double.MaxValue;
                double s = Math.Min(Math.Min(sx, sy), 1.0);   // fit inside the box and never enlarge
                return (Math.Max(1, (int)Math.Round(w * s)), Math.Max(1, (int)Math.Round(h * s)));
            }
            return (o.Width > 0 ? o.Width : w, o.Height > 0 ? o.Height : h);
        }

        public static BitmapSource Render(BitmapSource src, ImageOptions o)
        {
            double freeAspect = o.Width > 0 && o.Height > 0 ? (double)o.Width / o.Height : 0;
            var rect = CropRect(src.PixelWidth, src.PixelHeight, o.Crop, freeAspect);
            BitmapSource cropped = rect.Width == src.PixelWidth && rect.Height == src.PixelHeight ? src : new CroppedBitmap(src, rect);
            var (w, h) = OutputSize(src.PixelWidth, src.PixelHeight, o);
            if (w == cropped.PixelWidth && h == cropped.PixelHeight) { cropped.Freeze(); return cropped; }
            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.Fant);
            RenderOptions.SetEdgeMode(dv, EdgeMode.Aliased);
            using (var dc = dv.RenderOpen()) dc.DrawImage(cropped, new Rect(0, 0, w, h));
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        private static byte[] EncodeJpeg(BitmapSource img, int quality, BitmapMetadata meta)
        {
            BitmapSource src = img;
            if (img.Format.BitsPerPixel == 32 && (img.Format == PixelFormats.Pbgra32 || img.Format == PixelFormats.Bgra32))
                src = new FormatConvertedBitmap(FlattenOnWhite(img), PixelFormats.Bgr24, null, 0);
            var enc = new JpegBitmapEncoder { QualityLevel = quality };
            try { enc.Frames.Add(meta != null ? BitmapFrame.Create(src, null, meta, null) : BitmapFrame.Create(src)); }
            catch { enc = new JpegBitmapEncoder { QualityLevel = quality }; enc.Frames.Add(BitmapFrame.Create(src)); }
            using var ms = new MemoryStream();
            try { enc.Save(ms); }
            catch when (meta != null) { return EncodeJpeg(img, quality, null); }   // some metadata blocks cannot be carried over: keep the picture, not the block
            return ms.ToArray();
        }

        private static BitmapSource FlattenOnWhite(BitmapSource img)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, img.PixelWidth, img.PixelHeight)); dc.DrawImage(img, new Rect(0, 0, img.PixelWidth, img.PixelHeight)); }
            var rtb = new RenderTargetBitmap(img.PixelWidth, img.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            return rtb;
        }

        private static byte[] EncodeOther(BitmapSource img, string ext, BitmapMetadata meta)
        {
            BitmapEncoder enc = ext switch { ".png" => new PngBitmapEncoder(), ".bmp" => new BmpBitmapEncoder(), ".gif" => new GifBitmapEncoder(), ".tif" => new TiffBitmapEncoder(), _ => new PngBitmapEncoder() };
            try { enc.Frames.Add(meta != null && ext != ".bmp" && ext != ".gif" ? BitmapFrame.Create(img, null, meta, null) : BitmapFrame.Create(img)); }
            catch { enc = enc is TiffBitmapEncoder ? new TiffBitmapEncoder() : (BitmapEncoder)new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(img)); }
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }

        private static BitmapMetadata MetaOf(string path)
        {
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".webp" || ext == ".heic" || ext == ".bmp" || ext == ".gif") return null;
                var dec = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                return dec.Frames[0].Metadata is BitmapMetadata m ? m.Clone() : null;
            }
            catch { return null; }
        }

        public static async Task<ImageResult> ProcessAsync(string src, string dest, ImageOptions o, CancellationToken ct)
        {
            var res = new ImageResult { Source = src, Output = dest };
            var part = dest + ".part";
            try
            {
                res.SourceBytes = new FileInfo(src).Length;
                var bmp = await LoadAsync(src, ct);
                ct.ThrowIfCancellationRequested();
                var img = await Task.Run(() => Render(bmp, o), ct);
                res.Width = img.PixelWidth; res.Height = img.PixelHeight;
                var ext = Path.GetExtension(dest).ToLowerInvariant();
                byte[] data;
                if (ext == ".webp")
                {
                    data = await EncodeWebpAsync(img, o, res, ct);
                }
                else if (ext == ".jpg")
                {
                    var meta = MetaOf(src);
                    if (o.UseTarget && o.TargetBytes > 0)
                    {
                        int lo = QualityFloor, hi = Math.Clamp(o.Quality, QualityFloor, 100);
                        data = await Task.Run(() => EncodeJpeg(img, lo, meta), ct);
                        int best = lo;
                        if (data.Length <= o.TargetBytes)
                        {
                            // highest quality that still fits
                            var fits = data;
                            while (lo < hi)
                            {
                                int mid = (lo + hi + 1) / 2;
                                var t = await Task.Run(() => EncodeJpeg(img, mid, meta), ct);
                                if (t.Length <= o.TargetBytes) { lo = mid; fits = t; } else hi = mid - 1;
                            }
                            data = fits; best = lo;
                        }
                        else res.TargetMissed = true;
                        res.Quality = best;
                    }
                    else { data = await Task.Run(() => EncodeJpeg(img, Math.Clamp(o.Quality, 1, 100), meta), ct); res.Quality = o.Quality; }
                }
                else data = await Task.Run(() => EncodeOther(img, ext, ext == ".png" || ext == ".tif" ? MetaOf(src) : null), ct);
                File.WriteAllBytes(part, data);
                File.Move(part, dest);
                res.OutputBytes = data.Length;
                res.Ok = true;
            }
            catch (OperationCanceledException) { res.Error = "Cancelled"; }
            catch (NotSupportedException ex) { res.Error = "unsupported: " + ex.Message; }
            catch (Exception ex) when (ex.GetType().Name.Contains("Codec") || ex.Message.Contains("codec", StringComparison.OrdinalIgnoreCase)) { res.Error = "unsupported compression or format: " + ex.Message; }
            catch (Exception ex) { res.Error = ex.Message; }
            finally { try { if (File.Exists(part)) File.Delete(part); } catch { } }
            return res;
        }

        private static async Task<byte[]> EncodeWebpAsync(BitmapSource img, ImageOptions o, ImageResult res, CancellationToken ct)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "mt-img-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                var png = Path.Combine(tmp, "in.png");
                using (var fs = File.Create(png)) { var e = new PngBitmapEncoder(); e.Frames.Add(BitmapFrame.Create(img)); e.Save(fs); }
                async Task<byte[]> Enc(int q)
                {
                    var outFile = Path.Combine(tmp, "out" + q + ".webp");
                    var r = await MediaProc.RunFfmpegAsync($"-i {MediaProc.Quote(png)} -c:v libwebp -q:v {q} -f webp {MediaProc.Quote(outFile)}", 0, null, ct);
                    if (!r.Ok) throw new IOException(MediaProc.Plain(r.Error));
                    return File.ReadAllBytes(outFile);
                }
                if (o.UseTarget && o.TargetBytes > 0)
                {
                    int lo = QualityFloor, hi = Math.Clamp(o.Quality, QualityFloor, 100);
                    var data = await Enc(lo);
                    if (data.Length > o.TargetBytes) { res.TargetMissed = true; res.Quality = lo; return data; }
                    var fits = data; int best = lo;
                    while (lo < hi) { int mid = (lo + hi + 1) / 2; var t = await Enc(mid); if (t.Length <= o.TargetBytes) { lo = mid; fits = t; best = mid; } else hi = mid - 1; }
                    res.Quality = best; return fits;
                }
                res.Quality = o.Quality;
                return await Enc(Math.Clamp(o.Quality, 1, 100));
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }
        }

        /// <summary>A rough size for the list before anything is saved: pixel area and quality scale the original.</summary>
        public static long Estimate(long srcBytes, int srcW, int srcH, ImageOptions o, string srcExt)
        {
            if (srcW <= 0 || srcH <= 0) return srcBytes;
            var (w, h) = OutputSize(srcW, srcH, o);
            double area = (double)w * h / ((double)srcW * srcH);
            bool lossy = srcExt.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || srcExt.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) || srcExt.Equals(".webp", StringComparison.OrdinalIgnoreCase) || srcExt.Equals(".heic", StringComparison.OrdinalIgnoreCase);
            double q = lossy ? Math.Pow(Math.Clamp(o.Quality, 40, 100) / 90.0, 1.6) : 1.0;
            long est = (long)(srcBytes * area * q);
            if (lossy && o.UseTarget && o.TargetBytes > 0) est = Math.Min(est, o.TargetBytes);
            return Math.Max(1024, est);
        }
    }
}
