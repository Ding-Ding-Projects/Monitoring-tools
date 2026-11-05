using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Core;
using MonitoringTools.Media;

namespace MonitoringTools.UI
{
    /// <summary>Test media for the file tool dialog checks: video and audio made by FFmpeg's lavfi sources, and pictures made in code,
    /// including JPEG, PNG and WebP files with hand-built metadata blocks so the clean-up has something real to remove.</summary>
    public static partial class SelfTest
    {
        internal sealed class MediaFixtures
        {
            public string Dir, Ffmpeg;
            public string Video, BigVideo, NoiseVideo, Tone, ToneMp3;
            public List<string> Pictures = new List<string>();
            public string JpegGps, PngGps, WebpGps, JpegClean, BadJpeg, BadMp4, IconSource, IconSmall, Svg;
        }

        private static void Click(ButtonBase b) { if (!b.IsEnabled) throw new InvalidOperationException("the button is disabled"); b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); }

        private static void PressKey(Window w, Key key)
        {
            var src = PresentationSource.FromVisual(w);
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, src, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            w.RaiseEvent(e);
        }

        private static async Task<string> Ff(string ffmpeg, string args)
        {
            var r = await Task.Run(() => RunTool(ffmpeg, "-hide_banner -loglevel error -y " + args, 240000));
            return r.Code == 0 ? null : r.Out;
        }

        private static string Sha(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(s)); }

        internal static async Task<MediaFixtures> MakeMediaFixturesAsync(string root)
        {
            var f = new MediaFixtures { Dir = root };
            Directory.CreateDirectory(root);
            var ffmpeg = Converter.FindFfmpeg();
            var given = Environment.GetEnvironmentVariable("MT_FFMPEG");
            if (ffmpeg == null && !string.IsNullOrEmpty(given) && File.Exists(given)) { Settings.Current.FfmpegPath = given; ffmpeg = Converter.FindFfmpeg(); }
            f.Ffmpeg = ffmpeg;
            if (ffmpeg == null) return f;
            f.Video = Path.Combine(root, "holiday.mp4");
            f.BigVideo = Path.Combine(root, "big.mp4");
            f.NoiseVideo = Path.Combine(root, "noise.mp4");
            f.Tone = Path.Combine(root, "tone.wav");
            f.ToneMp3 = Path.Combine(root, "tone2.wav");
            // a key frame every 2 seconds so that "on a key frame" can be told from "between key frames"
            var jobs = new List<Task<string>>
            {
                Ff(ffmpeg, $"-f lavfi -i testsrc2=size=320x240:rate=25 -f lavfi -i sine=frequency=440:sample_rate=44100 -t 20 -c:v libx264 -preset ultrafast -g 50 -keyint_min 50 -sc_threshold 0 -pix_fmt yuv420p -c:a aac -shortest \"{f.Video}\""),
                Ff(ffmpeg, $"-f lavfi -i testsrc2=size=1280x720:rate=25 -f lavfi -i sine=frequency=330:sample_rate=44100 -t 30 -c:v libx264 -preset ultrafast -g 50 -pix_fmt yuv420p -c:a aac -shortest \"{f.BigVideo}\""),
                Ff(ffmpeg, $"-f lavfi -i testsrc2=size=640x360:rate=25 -t 20 -vf noise=alls=60:allf=t+u -c:v libx264 -preset ultrafast -crf 14 -g 25 -pix_fmt yuv420p \"{f.NoiseVideo}\""),
                // a quiet tone so that raising it to a loudness target is a real change
                Ff(ffmpeg, $"-f lavfi -i sine=frequency=440:sample_rate=44100:duration=12 -af volume=0.05 -ac 2 -c:a pcm_s16le \"{f.Tone}\""),
            };
            var errs = await Task.WhenAll(jobs);
            if (errs.Any(e => e != null)) { f.Ffmpeg = null; Results.Add(new Result("media fixtures: FFmpeg could not make the test media", false, string.Join(" | ", errs.Where(e => e != null)))); return f; }

            for (int i = 0; i < 4; i++)
            {
                var p = Path.Combine(root, $"IMG_{4821 + i}.jpg");
                File.WriteAllBytes(p, MakeJpeg(2000, 1500, i, 92, noise: false));
                f.Pictures.Add(p);
            }
            var png = Path.Combine(root, "scan.png"); File.WriteAllBytes(png, MakePng(1200, 900, 5)); f.Pictures.Add(png);
            var bmp = Path.Combine(root, "paint.bmp"); File.WriteAllBytes(bmp, EncodeBitmap(Gradient(900, 600, 6), new BmpBitmapEncoder())); f.Pictures.Add(bmp);
            var webp = Path.Combine(root, "web.webp");
            var pngForWebp = Path.Combine(root, "web-src.png"); File.WriteAllBytes(pngForWebp, MakePng(1000, 700, 7));
            if (await Ff(ffmpeg, $"-i \"{pngForWebp}\" -c:v libwebp -q:v 80 \"{webp}\"") == null) f.Pictures.Add(webp);

            // pictures that carry hidden data
            f.JpegGps = Path.Combine(root, "trip-1.jpg"); File.WriteAllBytes(f.JpegGps, WithExif(MakeJpeg(1600, 1200, 11, 90, false), SampleExif(true), true));
            f.JpegClean = Path.Combine(root, "plain.jpg"); File.WriteAllBytes(f.JpegClean, MakeJpeg(800, 600, 12, 90, false));
            f.PngGps = Path.Combine(root, "trip-2.png"); File.WriteAllBytes(f.PngGps, PngWithMetadata(MakePng(900, 600, 13)));
            var plainPng = Path.Combine(root, "plain-for-webp.png"); File.WriteAllBytes(plainPng, MakePng(640, 480, 14));
            var losslessWebp = Path.Combine(root, "plain-lossless.webp");
            if (await Ff(ffmpeg, $"-i \"{plainPng}\" -c:v libwebp -lossless 1 \"{losslessWebp}\"") == null)
            {
                f.WebpGps = Path.Combine(root, "trip-3.webp");
                File.WriteAllBytes(f.WebpGps, WebpWithExif(File.ReadAllBytes(losslessWebp), 640, 480, SampleExif(true)));
            }
            f.BadJpeg = Path.Combine(root, "broken.jpg"); File.WriteAllBytes(f.BadJpeg, Enumerable.Range(0, 3000).Select(i => (byte)(i * 31 % 251)).ToArray());
            f.BadMp4 = Path.Combine(root, "broken.mp4"); File.WriteAllBytes(f.BadMp4, Enumerable.Range(0, 5000).Select(i => (byte)(i * 17 % 253)).ToArray());
            f.IconSource = Path.Combine(root, "logo.png"); File.WriteAllBytes(f.IconSource, EncodeBitmap(Disc(512, 512), new PngBitmapEncoder()));
            f.IconSmall = Path.Combine(root, "small-logo.png"); File.WriteAllBytes(f.IconSmall, EncodeBitmap(Disc(120, 120), new PngBitmapEncoder()));
            f.Svg = Path.Combine(root, "mark.svg");
            File.WriteAllText(f.Svg, "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect x='10' y='10' width='80' height='80' rx='16' fill='#e33'/><circle cx='50' cy='50' r='22' fill='#fff'/></svg>");
            return f;
        }

        // ------------------------------------------------------------ pictures in code

        private static BitmapSource Gradient(int w, int h, int seed, bool noise = false)
        {
            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr24, null);
            var px = new byte[w * h * 3];
            var rnd = new Random(seed);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 3;
                    px[i] = (byte)(40 + 150 * x / w + (noise ? rnd.Next(-25, 25) : 0));
                    px[i + 1] = (byte)(60 + 120 * y / h + ((x / 40 + y / 40) % 2 == 0 ? 25 : 0) + seed * 7 % 40);
                    px[i + 2] = (byte)(90 + 100 * (x + y) / (w + h) + (noise ? rnd.Next(-25, 25) : 0));
                }
            wb.WritePixels(new Int32Rect(0, 0, w, h), px, w * 3, 0);
            wb.Freeze();
            return wb;
        }

        private static BitmapSource Disc(int w, int h)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x3A, 0xB8, 0x8F)), null, new Point(w / 2.0, h / 2.0), w * 0.42, h * 0.42);
                dc.DrawEllipse(Brushes.White, null, new Point(w / 2.0, h / 2.0), w * 0.16, h * 0.16);
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv); rtb.Freeze();
            return rtb;
        }

        private static byte[] EncodeBitmap(BitmapSource s, BitmapEncoder enc)
        {
            enc.Frames.Add(BitmapFrame.Create(s));
            using var ms = new MemoryStream(); enc.Save(ms); return ms.ToArray();
        }

        internal static byte[] MakeJpeg(int w, int h, int seed, int quality, bool noise) => EncodeBitmap(Gradient(w, h, seed, noise), new JpegBitmapEncoder { QualityLevel = quality });
        internal static byte[] MakePng(int w, int h, int seed) => EncodeBitmap(Gradient(w, h, seed), new PngBitmapEncoder());

        // ------------------------------------------------------------ hand-built metadata

        private static ExifEntry Asc(ushort tag, string s) { var b = Encoding.ASCII.GetBytes(s + "\0"); return new ExifEntry { Tag = tag, Type = 2, Count = (uint)b.Length, Value = b }; }
        private static ExifEntry Sh(ushort tag, ushort v) => new ExifEntry { Tag = tag, Type = 3, Count = 1, Value = BitConverter.GetBytes(v) };
        private static ExifEntry Rat(ushort tag, params (uint n, uint d)[] v)
        {
            var b = new byte[v.Length * 8];
            for (int i = 0; i < v.Length; i++) { BitConverter.GetBytes(v[i].n).CopyTo(b, i * 8); BitConverter.GetBytes(v[i].d).CopyTo(b, i * 8 + 4); }
            return new ExifEntry { Tag = tag, Type = 5, Count = (uint)v.Length, Value = b };
        }

        internal static ExifData SampleExif(bool withThumb)
        {
            var d = new ExifData { BigEndian = false };
            d.Ifd0.AddRange(new[] { Asc(0x010F, "TestCam Pro"), Asc(0x0110, "TC-9000"), Sh(0x0112, 6), Asc(0x0131, "TestSoft 1.0"), Asc(0x013B, "Alice Example"), Asc(0x0132, "2025:06:12 16:42:09"), Rat(0x011A, (72, 1)), Rat(0x011B, (72, 1)), Sh(0x0128, 2) });
            d.Exif.AddRange(new[]
            {
                Asc(0x9003, "2025:06:12 16:42:08"), Rat(0x829A, (1, 125)), Rat(0x829D, (28, 10)), Sh(0x8827, 400), Asc(0xA431, "SN0012345"), Asc(0xA434, "EF 24-70mm f/2.8"),
                new ExifEntry { Tag = 0x927C, Type = 7, Count = 16, Value = Encoding.ASCII.GetBytes("MAKERNOTE-SECRET") },
            });
            d.Gps.AddRange(new[]
            {
                new ExifEntry { Tag = 0, Type = 1, Count = 4, Value = new byte[] { 2, 3, 0, 0 } },
                Asc(1, "N"), Rat(2, (51, 1), (30, 1), (252, 100)), Asc(3, "W"), Rat(4, (0, 1), (7, 1), (2856, 100)), Rat(6, (11, 1)), Rat(0x1F, (5, 1)),
            });
            if (withThumb)
            {
                d.Thumbnail = MakeJpeg(32, 24, 3, 70, false);
                d.Ifd1.Add(Sh(0x0103, 6));
            }
            return d;
        }

        private static byte[] WithExif(byte[] jpeg, ExifData exif, bool afterApp0)
        {
            var tiff = exif.ToTiff();
            int insertAt = 2;
            if (afterApp0 && jpeg.Length > 6 && jpeg[2] == 0xFF && jpeg[3] == 0xE0) insertAt = 4 + ((jpeg[4] << 8) | jpeg[5]);
            using var ms = new MemoryStream();
            ms.Write(jpeg, 0, insertAt);
            int len = 2 + 6 + tiff.Length;
            ms.WriteByte(0xFF); ms.WriteByte(0xE1); ms.WriteByte((byte)(len >> 8)); ms.WriteByte((byte)len);
            ms.Write(new byte[] { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0 }); ms.Write(tiff);
            ms.Write(jpeg, insertAt, jpeg.Length - insertAt);
            return ms.ToArray();
        }

        private static byte[] PngChunk(string type, byte[] data)
        {
            var b = new byte[12 + data.Length];
            b[0] = (byte)(data.Length >> 24); b[1] = (byte)(data.Length >> 16); b[2] = (byte)(data.Length >> 8); b[3] = (byte)data.Length;
            Encoding.ASCII.GetBytes(type).CopyTo(b, 4); data.CopyTo(b, 8);
            var crcIn = new byte[4 + data.Length]; Array.Copy(b, 4, crcIn, 0, crcIn.Length);
            uint c = ImagePrivacyEngine.Crc(crcIn, 0, crcIn.Length);
            b[8 + data.Length] = (byte)(c >> 24); b[9 + data.Length] = (byte)(c >> 16); b[10 + data.Length] = (byte)(c >> 8); b[11 + data.Length] = (byte)c;
            return b;
        }

        private static byte[] PngWithMetadata(byte[] png)
        {
            var exif = SampleExif(false);
            using var ms = new MemoryStream();
            ms.Write(png, 0, 8 + 25);   // signature and IHDR
            ms.Write(PngChunk("tEXt", Encoding.Latin1.GetBytes("Author\0Alice Example")));
            ms.Write(PngChunk("tEXt", Encoding.Latin1.GetBytes("Creation Time\02025-06-12T16:42:08")));
            ms.Write(PngChunk("tIME", new byte[] { 0x07, 0xE9, 6, 12, 16, 42, 8 }));
            ms.Write(PngChunk("eXIf", exif.ToTiff()));
            ms.Write(png, 8 + 25, png.Length - 8 - 25);
            return ms.ToArray();
        }

        private static byte[] WebpWithExif(byte[] webp, int w, int h, ExifData exif)
        {
            // RIFF size WEBP, then a VP8X chunk (flags, canvas size), the original image chunk, then EXIF
            var body = new MemoryStream();
            var vp8x = new byte[10]; vp8x[0] = 0x08; vp8x[4] = (byte)(w - 1); vp8x[5] = (byte)((w - 1) >> 8); vp8x[6] = (byte)((w - 1) >> 16); vp8x[7] = (byte)(h - 1); vp8x[8] = (byte)((h - 1) >> 8); vp8x[9] = (byte)((h - 1) >> 16);
            void Chunk(string type, byte[] data) { body.Write(Encoding.ASCII.GetBytes(type)); body.Write(BitConverter.GetBytes((uint)data.Length)); body.Write(data); if ((data.Length & 1) != 0) body.WriteByte(0); }
            Chunk("VP8X", vp8x);
            body.Write(webp, 12, webp.Length - 12);
            Chunk("EXIF", exif.ToTiff());
            var all = new MemoryStream();
            all.Write(Encoding.ASCII.GetBytes("RIFF")); all.Write(BitConverter.GetBytes((uint)(4 + body.Length))); all.Write(Encoding.ASCII.GetBytes("WEBP")); all.Write(body.ToArray());
            return all.ToArray();
        }

        // ------------------------------------------------------------ reading back with something that is not the cleaner

        internal static string WpfQuery(string path, string query)
        {
            try
            {
                var dec = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var m = dec.Frames[0].Metadata as BitmapMetadata;
                var v = m?.GetQuery(query);
                return v?.ToString();
            }
            catch { return null; }
        }

        private static byte[] PixelsOf(string path)
        {
            var dec = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var f = dec.Frames[0];
            var fc = new FormatConvertedBitmap(f, PixelFormats.Bgra32, null, 0);
            var px = new byte[fc.PixelWidth * fc.PixelHeight * 4];
            fc.CopyPixels(px, fc.PixelWidth * 4, 0);
            return px;
        }

        private static bool Contains(byte[] hay, string needle)
        {
            var n = Encoding.ASCII.GetBytes(needle);
            for (int i = 0; i + n.Length <= hay.Length; i++)
            {
                int k = 0; while (k < n.Length && hay[i + k] == n[k]) k++;
                if (k == n.Length) return true;
            }
            return false;
        }
    }
}
