using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.UI;

namespace MonitoringTools.Core
{
    /// <summary>
    /// The drawer logo and the tray icon beyond a tint: a choice of glyphs from the app's own icon set, or one custom PNG or ICO
    /// file. The image is validated (type, size, real dimensions), copied into the app's data folder and never read again from
    /// where it came from. Reset puts back the default glyph.
    /// </summary>
    public static class LogoKit
    {
        public const long MaxBytes = 256 * 1024;
        public const int MinSide = 16, MaxSide = 512;
        public const string DefaultGlyph = "pulse";

        public static readonly (string Id, string Label, string Data)[] Glyphs =
        {
            ("pulse", "Pulse", Icons.Pulse), ("monitor", "Monitor", Icons.Monitor), ("bolt", "Bolt", Icons.Bolt), ("shield", "Shield", Icons.Shield),
            ("wifi", "Wi-Fi", Icons.Wifi), ("globe", "Globe", Icons.Globe), ("clipboard", "Clipboard", Icons.Clipboard), ("clock", "Clock", Icons.Clock),
            ("box", "Box", Icons.Box), ("code", "Code", Icons.Code), ("game", "Controller", Icons.Game), ("moon", "Moon", Icons.Moon)
        };

        private static Settings S => Settings.Current;

        public static string GlyphId => Glyphs.Any(g => g.Id == S.LogoGlyph) ? S.LogoGlyph : DefaultGlyph;
        public static string GlyphData(string id) => (Glyphs.FirstOrDefault(g => g.Id == id).Data) ?? Glyphs[0].Data;

        /// <summary>The stored copy of the custom image, or null when none is set or the copy has gone.</summary>
        public static string ImagePath
        {
            get
            {
                if (string.IsNullOrEmpty(S.LogoImageFile)) return null;
                var p = Path.Combine(Folder, Path.GetFileName(S.LogoImageFile));
                return File.Exists(p) ? p : null;
            }
        }

        public static bool HasCustomImage => ImagePath != null;
        public static bool IsCustomised => HasCustomImage || GlyphId != DefaultGlyph;

        /// <summary>Changes whenever the drawn logo would change, so the tray knows to redraw.</summary>
        public static string Signature => GlyphId + "|" + (ImagePath == null ? "" : S.LogoImageFile + new FileInfo(ImagePath).LastWriteTimeUtc.Ticks);

        private static string Folder
        {
            get { var d = Path.Combine(Settings.DataDir, "logo"); Directory.CreateDirectory(d); return d; }
        }

        // ------------------------------------------------------------ validation and import

        /// <summary>Checks a file without storing anything. Returns what is wrong, or null; <paramref name="ext"/> is the lower-case extension kept.</summary>
        public static string Validate(string path, out string ext)
        {
            ext = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "That file does not exist.";
                var e = Path.GetExtension(path).ToLowerInvariant();
                if (e != ".png" && e != ".ico") return "Choose a PNG or ICO file. SVG and other types are not accepted.";
                var info = new FileInfo(path);
                if (info.Length == 0) return "The file is empty.";
                if (info.Length > MaxBytes) return $"The file is {info.Length / 1024} KB. The limit is {MaxBytes / 1024} KB.";
                var bytes = File.ReadAllBytes(path);
                bool png = bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                bool ico = bytes.Length > 6 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1 && bytes[3] == 0;
                if (e == ".png" && !png) return "The file ends in .png but is not a PNG image.";
                if (e == ".ico" && !ico) return "The file ends in .ico but is not an icon file.";
                BitmapSource best;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    if (dec.Frames.Count == 0) return "The image has no picture in it.";
                    best = dec.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();
                }
                catch { return "The image could not be read."; }
                if (best.PixelWidth < MinSide || best.PixelHeight < MinSide) return $"The image is {best.PixelWidth} x {best.PixelHeight}. The smallest side must be at least {MinSide} pixels.";
                if (best.PixelWidth > MaxSide || best.PixelHeight > MaxSide) return $"The image is {best.PixelWidth} x {best.PixelHeight}. The largest side is {MaxSide} pixels.";
                ext = e;
                return null;
            }
            catch (Exception ex) { return "The file could not be checked: " + ex.Message; }
        }

        /// <summary>Validates and stores a custom image, then redraws. Returns the problem, or null on success.</summary>
        public static string Import(string path)
        {
            var err = Validate(path, out var ext);
            if (err != null) return err;
            try
            {
                var name = "custom" + ext;
                foreach (var old in Directory.GetFiles(Folder, "custom.*")) { try { File.Delete(old); } catch { } }
                File.Copy(path, Path.Combine(Folder, name), true);
                S.LogoImageFile = name;
                Settings.Save();
                Appearance.Apply();
                return null;
            }
            catch (Exception ex) { return "The image could not be stored: " + ex.Message; }
        }

        /// <summary>Picks a glyph and drops any custom image, so the choice is never ambiguous.</summary>
        public static string SetGlyph(string id)
        {
            if (!Glyphs.Any(g => g.Id == id)) return "That glyph is not in the icon set.";
            S.LogoGlyph = id;
            RemoveImageFiles();
            Settings.Save();
            Appearance.Apply();
            return null;
        }

        /// <summary>Back to the default glyph with no custom image.</summary>
        public static void Reset()
        {
            S.LogoGlyph = DefaultGlyph;
            RemoveImageFiles();
            Settings.Save();
            Appearance.Apply();
        }

        /// <summary>Removes only the custom image, keeping the chosen glyph.</summary>
        public static void RemoveImage()
        {
            RemoveImageFiles();
            Settings.Save();
            Appearance.Apply();
        }

        private static void RemoveImageFiles()
        {
            S.LogoImageFile = "";
            try { foreach (var f in Directory.GetFiles(Folder, "custom.*")) File.Delete(f); } catch { }
        }

        /// <summary>Reset without redrawing, for <see cref="Appearance.ResetToDefaults"/>, which redraws itself.</summary>
        public static void ResetQuiet()
        {
            S.LogoGlyph = DefaultGlyph;
            RemoveImageFiles();
        }

        // ------------------------------------------------------------ drawing

        public static BitmapSource LoadImage()
        {
            var p = ImagePath;
            if (p == null) return null;
            try
            {
                using var ms = new MemoryStream(File.ReadAllBytes(p));
                var dec = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var f = dec.Frames.OrderByDescending(x => x.PixelWidth * x.PixelHeight).First();
                f.Freeze();
                return f;
            }
            catch { return null; }
        }

        /// <summary>Shows the logo in the drawer header: the custom image when there is one, otherwise the chosen glyph in the tint.</summary>
        public static void ApplyToHeader(System.Windows.Shapes.Path logo, Color tint)
        {
            if (logo?.Parent is not Panel panel) { if (logo != null) logo.Stroke = new SolidColorBrush(tint); return; }
            var img = panel.Children.OfType<Image>().FirstOrDefault(i => (i.Tag as string) == "logo-custom");
            var src = LoadImage();
            if (src != null)
            {
                if (img == null)
                {
                    img = new Image { Tag = "logo-custom", Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0), Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
                    System.Windows.Automation.AutomationProperties.SetName(img, "Logo");
                    panel.Children.Insert(0, img);
                }
                img.Source = src; img.Visibility = Visibility.Visible;
                logo.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (img != null) img.Visibility = Visibility.Collapsed;
                logo.Visibility = Visibility.Visible;
                logo.Data = Geometry.Parse(GlyphData(GlyphId));
                logo.Stroke = new SolidColorBrush(tint);
            }
        }

        /// <summary>The logo as a square bitmap for the tray (without the status dot): the custom image, or the glyph in light strokes.</summary>
        public static System.Drawing.Bitmap TrayBase(int size = 32)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var src = LoadImage();
                if (src != null) dc.DrawImage(src, new Rect(0, 0, size, size));
                else
                {
                    double k = size * 0.78 / 24.0, off = size * 0.11;
                    dc.PushTransform(new TranslateTransform(off, off));
                    dc.PushTransform(new ScaleTransform(k, k));
                    var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xED)), 2.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    dc.DrawGeometry(null, pen, Geometry.Parse(GlyphData(GlyphId)));
                    dc.Pop(); dc.Pop();
                }
            }
            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            enc.Save(ms);
            ms.Position = 0;
            using var tmp = new System.Drawing.Bitmap(ms);
            return new System.Drawing.Bitmap(tmp);
        }
    }
}
