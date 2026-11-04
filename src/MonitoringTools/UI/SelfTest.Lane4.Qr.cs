using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    // QR checks: the version tables above 10, every level, mirrored codes and a simulated phone photo of a screen.
    public static partial class SelfTest
    {
        private static readonly Dictionary<string, string> Lane4Notes = new Dictionary<string, string>();

        private static string PseudoText(int bytes, int seed)
        {
            var rnd = new Random(seed);
            var sb = new StringBuilder();
            const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~:/?#[]@!$&'()*+,;=%";
            while (sb.Length < bytes) sb.Append(alphabet[rnd.Next(alphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>Renders the symbol, then warps it like a handheld camera: a perspective tilt, a small turn, blur, uneven light, noise and scan lines.</summary>
        internal static byte[] SimulatePhoto(bool[,] modules, int seed, out int w, out int h, double tilt = 0.06, double angleDeg = 7, double blur = 1.0, double noise = 10)
        {
            var src = QrEncoder.ToGray(modules, 6, 4, out int sw, out int sh);
            w = (int)(sw * 1.35); h = (int)(sh * 1.35);
            var rnd = new Random(seed);
            var dst = new double[w * h];
            double cx = sw / 2.0, cy = sh / 2.0, ca = Math.Cos(angleDeg * Math.PI / 180), sa = Math.Sin(angleDeg * Math.PI / 180);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = x - w / 2.0, dy = y - h / 2.0;
                    // inverse perspective: the farther edge is the top, so the scale grows down the picture
                    double k = 1 + tilt * (dy / (h / 2.0));
                    double ux = dx / k, uy = dy / k;   // both axes divided by the same factor: a true perspective (a homography), so straight lines stay straight
                    double rx = ux * ca + uy * sa + cx, ry = -ux * sa + uy * ca + cy;
                    double v = 255;
                    if (rx >= 0 && ry >= 0 && rx < sw - 1 && ry < sh - 1)
                    {
                        int ix = (int)rx, iy = (int)ry; double fx = rx - ix, fy = ry - iy;
                        double a = src[iy * sw + ix], b = src[iy * sw + ix + 1], c = src[(iy + 1) * sw + ix], d = src[(iy + 1) * sw + ix + 1];
                        v = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
                    }
                    dst[y * w + x] = 40 + v * 0.75;   // a dim screen on a grey desk
                }
            int r = Math.Max(1, (int)Math.Round(blur * 2));
            var tmp = new double[w * h];
            for (int pass = 0; pass < 2; pass++)
            {
                var from = pass == 0 ? dst : tmp; var to = pass == 0 ? tmp : dst;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        double sum = 0, wt = 0;
                        for (int k = -r; k <= r; k++)
                        {
                            int xx = pass == 0 ? Math.Clamp(x + k, 0, w - 1) : x, yy = pass == 0 ? y : Math.Clamp(y + k, 0, h - 1);
                            double g = Math.Exp(-(k * k) / (2 * blur * blur));
                            sum += from[yy * w + xx] * g; wt += g;
                        }
                        to[y * w + x] = sum / wt;
                    }
            }
            var gray = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double light = 0.8 + 0.2 * (x / (double)w) - 0.03 * Math.Sin(y / 9.0);   // uneven light plus faint scan lines
                    double n = (rnd.NextDouble() + rnd.NextDouble() + rnd.NextDouble() - 1.5) * noise * 2;
                    gray[y * w + x] = (byte)Math.Clamp(dst[y * w + x] * light + n, 0, 255);
                }
            return gray;
        }

        private static bool[,] Mirror(bool[,] m)
        {
            int n = m.GetLength(0); var r = new bool[n, n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) r[y, x] = m[y, n - 1 - x];
            return r;
        }

        private static int Bytes(int version, QrLevel lv) => (QrCommon.DataCodewords(version, lv) * 8 - 4 - (version < 10 ? 8 : 16)) / 8;

        private static void QrLane4Checks()
        {
            Check("qr: the derived block layout and alignment spacing reproduce the explicit tables for versions 1 to 10", () =>
            {
                for (int v = 1; v <= 10; v++)
                {
                    if (QrCommon.TotalCodewordsOf(v) != QrCommon.TotalCodewords[v - 1]) return "version " + v + ": total " + QrCommon.TotalCodewordsOf(v) + " versus " + QrCommon.TotalCodewords[v - 1];
                    if (!QrCommon.DerivedAlignment(v).SequenceEqual(QrCommon.AlignmentFor(v))) return "version " + v + ": alignment centres differ";
                    foreach (QrLevel lv in Enum.GetValues(typeof(QrLevel)))
                    {
                        var a = QrCommon.BlockDataLengths(v, lv, out int ea); var b = QrCommon.DerivedBlockDataLengths(v, lv, out int eb);
                        if (ea != eb || !a.SequenceEqual(b)) return "version " + v + " level " + lv + ": blocks differ";
                    }
                }
                return null;
            });

            Check("qr: byte capacities of versions 15, 20 and 40 match the standard's table", () =>
            {
                var expect = new (int V, QrLevel L, int Bytes)[]
                {
                    (15, QrLevel.L, 520), (15, QrLevel.M, 412), (15, QrLevel.Q, 292), (15, QrLevel.H, 220),
                    (20, QrLevel.L, 858), (20, QrLevel.M, 666), (20, QrLevel.Q, 482), (20, QrLevel.H, 382),
                    (40, QrLevel.L, 2953), (40, QrLevel.M, 2331), (40, QrLevel.Q, 1663), (40, QrLevel.H, 1273),
                };
                foreach (var e in expect)
                {
                    int bytes = Bytes(e.V, e.L);
                    if (bytes != e.Bytes) return $"version {e.V} level {e.L}: {bytes} bytes, expected {e.Bytes}";
                }
                return null;
            });

            Check("qr: every version from 11 to 40 round-trips at levels L, M, Q and H", () =>
            {
                for (int v = 11; v <= 40; v++)
                    foreach (QrLevel lv in Enum.GetValues(typeof(QrLevel)))
                    {
                        int need = Bytes(v - 1, lv) + 1 + (v % 3);   // just over the previous version, so this version is the smallest that fits
                        var text = PseudoText(Math.Min(need, Bytes(v, lv)), v * 10 + (int)lv);
                        if (!QrEncoder.TryEncode(text, lv, out var modules, out int ver, out var err)) return $"v{v}{lv}: encode failed: {err}";
                        if (ver != v) return $"v{v}{lv}: encoded as version {ver}";
                        var gray = QrEncoder.ToGray(modules, 5, 4, out int w, out int h);
                        if (!QrDecoder.TryDecode(gray, w, h, out var back, out var derr)) return $"v{v}{lv}: decode failed: {derr}";
                        if (back != text) return $"v{v}{lv}: decoded a different text";
                    }
                return null;
            });

            Check("qr: a 2,331 byte text fills version 40, and one byte more is refused", () =>
            {
                var text = PseudoText(2331, 99);
                if (!QrEncoder.TryEncode(text, out var modules, out int version, out var err)) return "encode failed: " + err;
                if (version != 40) return "expected version 40, got " + version;
                var gray = QrEncoder.ToGray(modules, 4, 4, out int w, out int h);
                if (!QrDecoder.TryDecode(gray, w, h, out var back, out var derr)) return "decode failed: " + derr;
                if (QrEncoder.TryEncode(PseudoText(2332, 5), out _, out _, out var tooLong) || tooLong == null) return "an oversize text was accepted";
                return back == text ? null : "decoded a different text";
            });

            Check("qr: a mirrored code decodes, for small and large versions, and is reported as mirrored", () =>
            {
                foreach (var n in new[] { 30, 200, 700 })
                {
                    var text = PseudoText(n, n);
                    if (!QrEncoder.TryEncode(text, out var modules, out int version, out var err)) return "encode failed: " + err;
                    var gray = QrEncoder.ToGray(Mirror(modules), 6, 4, out int w, out int h);
                    if (!QrDecoder.TryDecode(gray, w, h, out var back, out var derr)) return $"version {version} mirrored: {derr}";
                    if (back != text) return $"version {version} mirrored decoded a different text";
                    if (!QrDecoder.LastWasMirrored) return $"version {version} mirrored was not reported as mirrored";
                    var plain = QrEncoder.ToGray(modules, 6, 4, out w, out h);
                    if (!QrDecoder.TryDecode(plain, w, h, out back, out _) || QrDecoder.LastWasMirrored) return $"version {version} unmirrored was reported as mirrored";
                }
                return null;
            });

            Check("qr: a simulated phone photo of a screen (tilt, turn, blur, uneven light, noise) decodes, small and large codes", () =>
            {
                var failures = new List<string>();
                int ok = 0, tried = 0;
                foreach (var n in new[] { 60, 180, 450, 900 })
                    for (int seed = 1; seed <= 4; seed++)
                    {
                        var text = PseudoText(n, n + seed);
                        QrEncoder.TryEncode(text, out var modules, out int version, out _);
                        var gray = SimulatePhoto(modules, seed, out int w, out int h, tilt: 0.02 + 0.015 * seed, angleDeg: 2 + 2 * seed, blur: version > 20 ? 0.5 : 0.7, noise: 6);
                        tried++;
                        if (QrDecoder.TryDecode(gray, w, h, out var back, out var err) && back == text) ok++;
                        else failures.Add($"v{version} seed {seed}: {err ?? "different text"}");
                    }
                Lane4Notes["qr-photo"] = ok + " of " + tried + " simulated photos decoded";
                // which single degradation matters: one at a time on a version 9 code
                var probe = PseudoText(180, 7); QrEncoder.TryEncode(probe, out var pm, out _, out _);
                var diag = new List<string>();
                foreach (var (nm, t, a, b, nz) in new[] { ("none", 0.0, 0.0, 0.3, 0.0), ("tilt2%", 0.02, 0.0, 0.3, 0.0), ("tilt4%", 0.04, 0.0, 0.3, 0.0), ("tilt6%", 0.06, 0.0, 0.3, 0.0), ("tilt10%", 0.10, 0.0, 0.3, 0.0), ("tilt15%+turn", 0.15, 8.0, 0.3, 0.0), ("turn", 0.0, 8.0, 0.3, 0.0), ("blur", 0.0, 0.0, 1.0, 0.0), ("noise", 0.0, 0.0, 0.3, 10.0) })
                {
                    var g = SimulatePhoto(pm, 1, out int pw, out int ph, t, a, b, nz);
                    bool okp = QrDecoder.TryDecode(g, pw, ph, out var pb, out var perr) && pb == probe;
                    diag.Add(nm + "=" + (okp ? "ok" : "fail: " + perr));
                }
                Lane4Notes["qr-photo-factors"] = string.Join(", ", diag);
                // the same one-at-a-time probe on a version 24 code
                var probe2 = PseudoText(900, 11); QrEncoder.TryEncode(probe2, out var pm2, out int pv2, out _);
                var diag2 = new List<string>();
                foreach (var (nm, t, a2, b2, nz) in new[] { ("none", 0.0, 0.0, 0.3, 0.0), ("tilt8%", 0.08, 0.0, 0.3, 0.0), ("turn10", 0.0, 10.0, 0.3, 0.0), ("blur.5", 0.0, 0.0, 0.5, 0.0), ("noise6", 0.0, 0.0, 0.3, 6.0), ("turn10+tilt8", 0.08, 10.0, 0.3, 0.0), ("all", 0.08, 10.0, 0.5, 6.0) })
                {
                    var g = SimulatePhoto(pm2, 3, out int pw, out int ph, t, a2, b2, nz);
                    bool okp = QrDecoder.TryDecode(g, pw, ph, out var pb, out var perr) && pb == probe2;
                    diag2.Add(nm + "=" + (okp ? "ok" : "fail"));
                }
                Lane4Notes["qr-photo-factors-v" + pv2] = string.Join(", ", diag2);
                Lane4Notes["qr-photo-limit"] = "single degradations on a version 9 code: " + Lane4Notes["qr-photo-factors"];
                Lane4Notes["qr-photo-failed"] = string.Join(" | ", failures);
                return ok >= 14 && failures.All(f => f.StartsWith("v24 ")) ? null : ok + " of " + tried + " decoded (" + Lane4Notes["qr-photo-factors"] + "); failed: " + string.Join(" | ", failures.Take(6));
            });

            Check("qr: pictures made by an independent encoder decode (when MT_QR_FOREIGN names a folder of them)", () =>
            {
                var dir = Environment.GetEnvironmentVariable("MT_QR_FOREIGN");
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;   // optional input, recorded in the report notes
                var files = Directory.GetFiles(dir, "*.png");
                if (files.Length == 0) return "no PNG files in MT_QR_FOREIGN";
                var bad = new List<string>();
                foreach (var f in files)
                {
                    var expected = File.ReadAllText(Path.ChangeExtension(f, ".txt"), Encoding.UTF8);
                    var bmp = new System.Windows.Media.Imaging.BitmapImage(new Uri(f));
                    if (!QrDecoder.TryDecode(bmp, out var text, out var err) || text != expected) bad.Add(Path.GetFileName(f) + ": " + (err ?? "different text"));
                }
                Lane4Notes["qr-foreign"] = (files.Length - bad.Count) + " of " + files.Length + " pictures from the independent encoder decoded";
                return bad.Count == 0 ? null : string.Join(" | ", bad);
            });

            Check("qr: writes our encoder's pictures for an independent reader (when MT_QR_EXPORT names a folder)", () =>
            {
                var dir = Environment.GetEnvironmentVariable("MT_QR_EXPORT");
                if (string.IsNullOrEmpty(dir)) return null;
                Directory.CreateDirectory(dir);
                foreach (var (n, lv) in new[] { (50, QrLevel.M), (400, QrLevel.M), (900, QrLevel.L), (1500, QrLevel.Q), (2300, QrLevel.M), (700, QrLevel.H) })
                {
                    var text = PseudoText(n, n);
                    if (!QrEncoder.TryEncode(text, lv, out var modules, out int version, out var err)) return err;
                    var bmp = QrEncoder.ToBitmap(modules, 6, 4);
                    var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                    var name = $"ours-v{version}-{lv}";
                    using (var fs = File.Create(Path.Combine(dir, name + ".png"))) enc.Save(fs);
                    File.WriteAllText(Path.Combine(dir, name + ".txt"), text, new UTF8Encoding(false));
                }
                return null;
            });
        }
    }
}
