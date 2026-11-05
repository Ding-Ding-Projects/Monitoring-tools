using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace MonitoringTools.Media
{
    public enum IconShape { Keep, RoundedSquare, Circle }

    public sealed class IconOptions
    {
        public List<int> Sizes = new List<int>(IconMaker.AllSizes);
        public bool KeepTransparency = true;
        public double Padding;          // 0 to 0.4, each side
        public IconShape Shape = IconShape.Keep;
    }

    /// <summary>Builds a multi-size .ico entirely in .NET: each size is rendered with a high-quality filter, stored as PNG
    /// (Windows Vista and later), and written into the ICONDIR and ICONDIRENTRY records by hand. SVG sources are drawn by a small built-in
    /// renderer (shapes, paths, fills, strokes, transforms) and rasterised at 512 first.</summary>
    public static class IconMaker
    {
        public static readonly int[] AllSizes = { 16, 24, 32, 48, 64, 128, 256 };
        public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".svg" };

        public static BitmapSource Load(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".svg") return SvgLite.Rasterise(File.ReadAllText(path), 512);
            if (ext == ".webp")
            {
                var r = MediaProc.RunBytesAsync($"-i {MediaProc.Quote(path)} -frames:v 1 -f image2pipe -vcodec png -", default).GetAwaiter().GetResult();
                if (!r.Ok) throw new InvalidDataException("FFmpeg could not read this WebP picture.");
                return MediaProc.Decode(r.Data);
            }
            var dec = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var f = dec.Frames[0]; f.Freeze();
            return f;
        }

        public static bool HasAlpha(BitmapSource s) => s.Format == PixelFormats.Bgra32 || s.Format == PixelFormats.Pbgra32 || s.Format == PixelFormats.Prgba64 || s.Format == PixelFormats.Rgba64 || s.Format == PixelFormats.Rgba128Float || s.Format == PixelFormats.Prgba128Float;

        /// <summary>One icon size as a picture on a transparent (or white) square.</summary>
        public static BitmapSource Render(BitmapSource src, int size, IconOptions o)
        {
            var dv = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.Fant);
            using (var dc = dv.RenderOpen())
            {
                var full = new Rect(0, 0, size, size);
                Geometry clip = null;
                if (o.Shape == IconShape.Circle) clip = new EllipseGeometry(full);
                else if (o.Shape == IconShape.RoundedSquare) clip = new RectangleGeometry(full, size * 0.22, size * 0.22);
                if (!o.KeepTransparency) dc.DrawRectangle(Brushes.White, null, full);
                if (clip != null) { clip.Freeze(); dc.PushClip(clip); }
                double pad = Math.Clamp(o.Padding, 0, 0.4) * size;
                double box = size - 2 * pad;
                double scale = Math.Min(box / src.PixelWidth, box / src.PixelHeight);
                double w = src.PixelWidth * scale, h = src.PixelHeight * scale;
                dc.DrawImage(src, new Rect((size - w) / 2, (size - h) / 2, w, h));
                if (clip != null) dc.Pop();
            }
            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        private static byte[] Png(BitmapSource b)
        {
            var e = new PngBitmapEncoder(); e.Frames.Add(BitmapFrame.Create(b));
            using var ms = new MemoryStream(); e.Save(ms); return ms.ToArray();
        }

        /// <summary>The finished .ico bytes for the chosen sizes.</summary>
        public static byte[] Build(BitmapSource src, IconOptions o)
        {
            var sizes = o.Sizes.Distinct().Where(s => s >= 1 && s <= 256).OrderBy(s => s).ToList();
            if (sizes.Count == 0) throw new ArgumentException("Choose at least one size.");
            var images = sizes.Select(s => Png(Render(src, s, o))).ToList();
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Count);
            int offset = 6 + 16 * sizes.Count;
            for (int i = 0; i < sizes.Count; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((ushort)1); w.Write((ushort)32);
                w.Write((uint)images[i].Length); w.Write((uint)offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
            w.Flush();
            return ms.ToArray();
        }

        /// <summary>The sizes recorded in an .ico file's directory (256 is stored as 0).</summary>
        public static List<int> ReadSizes(byte[] ico)
        {
            var l = new List<int>();
            if (ico.Length < 6 || ico[2] != 1 || ico[3] != 0) return l;
            int n = ico[4] | (ico[5] << 8);
            for (int i = 0; i < n && 6 + i * 16 + 16 <= ico.Length; i++) l.Add(ico[6 + i * 16] == 0 ? 256 : ico[6 + i * 16]);
            return l;
        }
    }

    /// <summary>A small SVG renderer: rect, circle, ellipse, line, polyline, polygon and path, with fill, stroke, stroke width,
    /// opacity and the common transforms, nested groups and a viewBox. It does not do gradients (the first stop colour is used),
    /// filters, clip paths, masks, text, images or CSS classes. Anything it cannot draw is skipped, never guessed.</summary>
    public static class SvgLite
    {
        public static BitmapSource Rasterise(string svg, int size)
        {
            var doc = XDocument.Parse(svg);
            var root = doc.Root;
            if (root == null || root.Name.LocalName != "svg") throw new InvalidDataException("This file is not an SVG picture.");
            double vx = 0, vy = 0, vw = 0, vh = 0;
            var vb = (string)root.Attribute("viewBox");
            if (vb != null)
            {
                var p = Regex.Split(vb.Trim(), @"[\s,]+").Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                if (p.Length == 4) { vx = p[0]; vy = p[1]; vw = p[2]; vh = p[3]; }
            }
            if (vw <= 0) { vw = Len((string)root.Attribute("width"), 100); vh = Len((string)root.Attribute("height"), 100); }
            if (vh <= 0) vh = vw;
            double scale = size / Math.Max(vw, vh);
            int pw = Math.Max(1, (int)Math.Round(vw * scale)), ph = Math.Max(1, (int)Math.Round(vh * scale));
            var gradients = root.Descendants().Where(e => (e.Name.LocalName == "linearGradient" || e.Name.LocalName == "radialGradient") && e.Attribute("id") != null)
                .ToDictionary(e => (string)e.Attribute("id"), e => e);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, -vx * scale, -vy * scale));
                Draw(dc, root, gradients, 1.0, null, null);
                dc.Pop();
            }
            var rtb = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        private static double Len(string s, double fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            var m = Regex.Match(s, @"^\s*(-?[0-9.]+)");
            return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;
        }

        private static double Num(XElement e, string name, double fallback = 0) => Len((string)e.Attribute(name), fallback);

        private static string Prop(XElement e, string name)
        {
            var v = (string)e.Attribute(name);
            var style = (string)e.Attribute("style");
            if (style != null)
                foreach (var part in style.Split(';'))
                {
                    var kv = part.Split(':', 2);
                    if (kv.Length == 2 && kv[0].Trim() == name) return kv[1].Trim();
                }
            return v;
        }

        private static Brush Paint(string v, Dictionary<string, XElement> grads, double opacity)
        {
            if (string.IsNullOrWhiteSpace(v) || v == "none") return null;
            v = v.Trim();
            if (v.StartsWith("url(#"))
            {
                var id = v.Substring(5).TrimEnd(')', '\'', '"', ' ');
                if (grads.TryGetValue(id, out var g))
                {
                    var stop = g.Elements().FirstOrDefault(x => x.Name.LocalName == "stop");
                    var sc = stop == null ? null : Prop(stop, "stop-color");
                    return Paint(sc ?? "#808080", grads, opacity);
                }
                return null;
            }
            try
            {
                Color c;
                if (v.StartsWith("rgb"))
                {
                    var n = Regex.Matches(v, @"[0-9.]+").Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                    c = Color.FromRgb((byte)n[0], (byte)n[1], (byte)n[2]);
                }
                else c = (Color)ColorConverter.ConvertFromString(v.Length == 4 && v[0] == '#' ? "#" + v[1] + v[1] + v[2] + v[2] + v[3] + v[3] : v);
                c.A = (byte)Math.Clamp(c.A * opacity, 0, 255);
                var b = new SolidColorBrush(c); b.Freeze(); return b;
            }
            catch { return null; }
        }

        private static Transform Xf(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return null;
            var g = new TransformGroup();
            foreach (Match m in Regex.Matches(t, @"(matrix|translate|scale|rotate)\s*\(([^)]*)\)"))
            {
                var n = Regex.Split(m.Groups[2].Value.Trim(), @"[\s,]+").Where(x => x.Length > 0).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                switch (m.Groups[1].Value)
                {
                    case "matrix" when n.Length == 6: g.Children.Add(new MatrixTransform(n[0], n[1], n[2], n[3], n[4], n[5])); break;
                    case "translate": g.Children.Add(new TranslateTransform(n[0], n.Length > 1 ? n[1] : 0)); break;
                    case "scale": g.Children.Add(new ScaleTransform(n[0], n.Length > 1 ? n[1] : n[0])); break;
                    case "rotate": g.Children.Add(n.Length >= 3 ? new RotateTransform(n[0], n[1], n[2]) : new RotateTransform(n[0])); break;
                }
            }
            return g.Children.Count == 0 ? null : g;
        }

        private static void Draw(DrawingContext dc, XElement e, Dictionary<string, XElement> grads, double inheritedOpacity, string inheritedFill, string inheritedStroke)
        {
            var name = e.Name.LocalName;
            if (name == "defs" || name == "clipPath" || name == "mask" || name == "linearGradient" || name == "radialGradient" || name == "style" || name == "title" || name == "desc") return;
            double opacity = inheritedOpacity * (double.TryParse(Prop(e, "opacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out var op) ? op : 1);
            var xf = Xf((string)e.Attribute("transform"));
            if (xf != null) dc.PushTransform(xf);
            var fillV = Prop(e, "fill") ?? inheritedFill ?? (name == "svg" || name == "g" ? null : "#000000");
            var strokeV = Prop(e, "stroke") ?? inheritedStroke;
            if (name == "svg" || name == "g" || name == "a")
            {
                foreach (var c in e.Elements()) Draw(dc, c, grads, opacity, Prop(e, "fill") ?? inheritedFill, Prop(e, "stroke") ?? inheritedStroke);
            }
            else
            {
                Geometry geo = null;
                try
                {
                    switch (name)
                    {
                        case "rect":
                            geo = new RectangleGeometry(new Rect(Num(e, "x"), Num(e, "y"), Num(e, "width"), Num(e, "height")), Num(e, "rx", Num(e, "ry")), Num(e, "ry", Num(e, "rx"))); break;
                        case "circle": geo = new EllipseGeometry(new Point(Num(e, "cx"), Num(e, "cy")), Num(e, "r"), Num(e, "r")); break;
                        case "ellipse": geo = new EllipseGeometry(new Point(Num(e, "cx"), Num(e, "cy")), Num(e, "rx"), Num(e, "ry")); break;
                        case "line": geo = new LineGeometry(new Point(Num(e, "x1"), Num(e, "y1")), new Point(Num(e, "x2"), Num(e, "y2"))); break;
                        case "polyline": case "polygon":
                            {
                                var n = Regex.Split(((string)e.Attribute("points") ?? "").Trim(), @"[\s,]+").Where(x => x.Length > 0).Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                                var pts = new List<Point>(); for (int i = 0; i + 1 < n.Length; i += 2) pts.Add(new Point(n[i], n[i + 1]));
                                if (pts.Count > 1)
                                {
                                    var sg = new StreamGeometry(); using (var c = sg.Open()) { c.BeginFigure(pts[0], name == "polygon", name == "polygon"); c.PolyLineTo(pts.Skip(1).ToList(), true, false); }
                                    geo = sg;
                                }
                                break;
                            }
                        case "path": geo = Geometry.Parse((string)e.Attribute("d") ?? ""); break;
                    }
                }
                catch { geo = null; }
                if (geo != null)
                {
                    if ((Prop(e, "fill-rule") ?? "").ToLowerInvariant() == "evenodd" && geo is PathGeometry pg) pg.FillRule = FillRule.EvenOdd;
                    double fo = double.TryParse(Prop(e, "fill-opacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out var fov) ? fov : 1;
                    double so = double.TryParse(Prop(e, "stroke-opacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sov) ? sov : 1;
                    var fill = name == "line" ? null : Paint(fillV, grads, opacity * fo);
                    var sb = Paint(strokeV, grads, opacity * so);
                    Pen pen = null;
                    if (sb != null)
                    {
                        pen = new Pen(sb, Len(Prop(e, "stroke-width"), 1)) { LineJoin = PenLineJoin.Round };
                        var cap = Prop(e, "stroke-linecap"); if (cap == "round") { pen.StartLineCap = pen.EndLineCap = PenLineCap.Round; } else if (cap == "square") { pen.StartLineCap = pen.EndLineCap = PenLineCap.Square; }
                    }
                    if (fill != null || pen != null) dc.DrawGeometry(fill, pen, geo);
                }
            }
            if (xf != null) dc.Pop();
        }
    }
}
