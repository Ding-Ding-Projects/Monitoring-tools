using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MonitoringTools.Core
{
    /// <summary>
    /// A QR Code reader written from the specification, with no library. It binarizes the picture with an adaptive
    /// threshold, finds the three finder patterns, estimates the version (1 to 10), samples the grid through a perspective
    /// transform anchored on the alignment pattern, reads the format information, undoes the mask, reads the codewords in
    /// zigzag order, repairs them with Reed-Solomon error correction (levels L, M, Q and H, interleaved blocks) and decodes
    /// numeric, alphanumeric and byte segments. Pictures turned by 90, 180 and 270 degrees and mildly tilted or noisy
    /// pictures work. Mirrored codes, version 11 and above, and Kanji segments are not supported, and it says so.
    /// </summary>
    public static class QrDecoder
    {
        // ------------------------------------------------------------ entry points

        /// <summary>Reads the grey values of a picture on the calling thread, so the decode itself can run on another one. Transparent areas count as white.</summary>
        public static byte[] ToGray(BitmapSource src, out int width, out int height)
        {
            var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            width = conv.PixelWidth; height = conv.PixelHeight;
            var raw = new byte[width * height * 4];
            conv.CopyPixels(raw, width * 4, 0);
            var gray = new byte[width * height];
            for (int i = 0; i < gray.Length; i++)
            {
                int b = raw[i * 4], g = raw[i * 4 + 1], r = raw[i * 4 + 2], a = raw[i * 4 + 3];
                int luma = (r * 299 + g * 587 + b * 114) / 1000;
                gray[i] = (byte)((luma * a + 255 * (255 - a)) / 255);
            }
            return gray;
        }

        public static bool TryDecode(BitmapSource src, out string text, out string error)
        {
            var gray = ToGray(src, out int w, out int h);
            return TryDecode(gray, w, h, out text, out error);
        }

        /// <summary>Decodes a QR code from 8-bit grey pixels (0 black, 255 white), row by row. On failure the error says why in plain words.</summary>
        public static bool TryDecode(byte[] gray, int width, int height, out string text, out string error)
        {
            text = null;
            if (gray == null || width < 21 || height < 21 || gray.Length < width * height) { error = "The picture is too small to hold a QR code."; return false; }
            var reasons = new List<(int Rank, string Message)>();
            foreach (bool invert in new[] { false, true })
            {
                var bw = Binarize(gray, width, height, invert);
                if (TryDecodeBinary(bw, width, height, out text, reasons)) { error = null; return true; }
            }
            error = reasons.Count == 0
                ? "No QR code was found in the picture: there are no finder patterns (the three large squares in the corners)."
                : reasons.OrderByDescending(r => r.Rank).First().Message;
            return false;
        }

        // ------------------------------------------------------------ binarization

        private static bool[] Binarize(byte[] gray, int w, int h, bool invert)
        {
            // global Otsu threshold, used as a floor for large flat dark areas where the local mean alone would call them white
            var hist = new int[256];
            for (int i = 0; i < w * h; i++) hist[gray[i]]++;
            int total = w * h; long sumAll = 0;
            for (int i = 0; i < 256; i++) sumAll += (long)i * hist[i];
            long sumB = 0; int wB = 0; double maxVar = -1; int otsu = 128;
            for (int t = 0; t < 256; t++)
            {
                wB += hist[t]; if (wB == 0) continue;
                int wF = total - wB; if (wF == 0) break;
                sumB += (long)t * hist[t];
                double mB = (double)sumB / wB, mF = (double)(sumAll - sumB) / wF;
                double between = (double)wB * wF * (mB - mF) * (mB - mF);
                if (between > maxVar) { maxVar = between; otsu = t; }
            }

            // integral image for the local mean
            var integral = new long[(w + 1) * (h + 1)];
            for (int y = 0; y < h; y++)
            {
                long row = 0;
                for (int x = 0; x < w; x++)
                {
                    int v = invert ? 255 - gray[y * w + x] : gray[y * w + x];
                    row += v;
                    integral[(y + 1) * (w + 1) + x + 1] = integral[y * (w + 1) + x + 1] + row;
                }
            }
            int half = Math.Max(8, Math.Max(w, h) / 16);
            var bw = new bool[w * h];
            double floor = (invert ? 255 - otsu : otsu) * 0.5;
            for (int y = 0; y < h; y++)
            {
                int y0 = Math.Max(0, y - half), y1 = Math.Min(h, y + half + 1);
                for (int x = 0; x < w; x++)
                {
                    int x0 = Math.Max(0, x - half), x1 = Math.Min(w, x + half + 1);
                    long sum = integral[y1 * (w + 1) + x1] - integral[y0 * (w + 1) + x1] - integral[y1 * (w + 1) + x0] + integral[y0 * (w + 1) + x0];
                    double mean = (double)sum / ((x1 - x0) * (y1 - y0));
                    int v = invert ? 255 - gray[y * w + x] : gray[y * w + x];
                    bw[y * w + x] = v < Math.Max(mean * 0.85, floor);
                }
            }
            return bw; // true is dark
        }

        // ------------------------------------------------------------ finder patterns

        private sealed class Finder { public double X, Y, Size; public int Count; }

        private static bool LooksLikeFinder(int[] c)
        {
            int total = 0;
            for (int i = 0; i < 5; i++) { if (c[i] == 0) return false; total += c[i]; }
            if (total < 7) return false;
            double ms = total / 7.0, tol = ms * 0.6;
            return Math.Abs(ms - c[0]) < tol && Math.Abs(ms - c[1]) < tol && Math.Abs(3 * ms - c[2]) < 3 * tol && Math.Abs(ms - c[3]) < tol && Math.Abs(ms - c[4]) < tol;
        }

        private static double CenterFromEnd(int[] c, int end) => end - c[4] - c[3] - c[2] / 2.0;

        /// <summary>Walks outward from a centre along one axis and checks the 1:1:3:1:1 run lengths. Returns the centre along that axis, or NaN.</summary>
        private static double CrossCheck(bool[] bw, int w, int h, int cx, int cy, bool vertical, int maxCount, int originalTotal)
        {
            int len = vertical ? h : w, start = vertical ? cy : cx;
            bool Dark(int i) => vertical ? bw[i * w + cx] : bw[cy * w + i];
            var c = new int[5];
            int p = start;
            while (p >= 0 && Dark(p)) { c[2]++; p--; }
            if (p < 0) return double.NaN;
            while (p >= 0 && !Dark(p) && c[1] <= maxCount) { c[1]++; p--; }
            if (p < 0 || c[1] > maxCount) return double.NaN;
            while (p >= 0 && Dark(p) && c[0] <= maxCount) { c[0]++; p--; }
            if (c[0] > maxCount) return double.NaN;
            p = start + 1;
            while (p < len && Dark(p)) { c[2]++; p++; }
            if (p >= len) return double.NaN;
            while (p < len && !Dark(p) && c[3] < maxCount) { c[3]++; p++; }
            if (p >= len || c[3] >= maxCount) return double.NaN;
            while (p < len && Dark(p) && c[4] < maxCount) { c[4]++; p++; }
            if (c[4] >= maxCount) return double.NaN;
            int tot = c[0] + c[1] + c[2] + c[3] + c[4];
            if (5 * Math.Abs(tot - originalTotal) >= 2 * originalTotal) return double.NaN;
            return LooksLikeFinder(c) ? CenterFromEnd(c, p) : double.NaN;
        }

        private static List<Finder> FindFinders(bool[] bw, int w, int h)
        {
            var found = new List<Finder>();
            var sc = new int[5];
            for (int y = 0; y < h; y++)
            {
                Array.Clear(sc, 0, 5);
                int state = 0;
                for (int x = 0; x <= w; x++)
                {
                    bool dark = x < w && bw[y * w + x];
                    if (dark)
                    {
                        if ((state & 1) == 1) state++;
                        sc[state]++;
                    }
                    else if ((state & 1) == 0)
                    {
                        if (state == 4)
                        {
                            if (LooksLikeFinder(sc) && Handle(bw, w, h, sc, y, x, found)) { Array.Clear(sc, 0, 5); state = 0; }
                            else { sc[0] = sc[2]; sc[1] = sc[3]; sc[2] = sc[4]; sc[3] = 1; sc[4] = 0; state = 3; }
                        }
                        else sc[++state]++;
                    }
                    else sc[state]++;
                }
            }
            return found;
        }

        private static bool Handle(bool[] bw, int w, int h, int[] sc, int y, int xEnd, List<Finder> found)
        {
            int total = sc[0] + sc[1] + sc[2] + sc[3] + sc[4];
            double cx = CenterFromEnd(sc, xEnd);
            double cy = CrossCheck(bw, w, h, (int)cx, y, true, sc[2], total);
            if (double.IsNaN(cy)) return false;
            cx = CrossCheck(bw, w, h, (int)cx, (int)cy, false, sc[2], total);
            if (double.IsNaN(cx)) return false;
            double ms = total / 7.0;
            foreach (var f in found)
                if (Math.Abs(f.X - cx) <= ms && Math.Abs(f.Y - cy) <= ms && (Math.Abs(f.Size - ms) <= 1 || Math.Abs(f.Size - ms) <= f.Size))
                {
                    f.X = (f.X * f.Count + cx) / (f.Count + 1); f.Y = (f.Y * f.Count + cy) / (f.Count + 1); f.Size = (f.Size * f.Count + ms) / (f.Count + 1); f.Count++;
                    return true;
                }
            found.Add(new Finder { X = cx, Y = cy, Size = ms, Count = 1 });
            return true;
        }

        /// <summary>Picks the three finders that form a right isosceles triangle, and names them top-left, top-right and bottom-left in the code's own frame.</summary>
        private static bool PickTriple(List<Finder> all, out Finder tl, out Finder tr, out Finder bl)
        {
            tl = tr = bl = null;
            var c = all.Where(f => f.Count >= 2).OrderByDescending(f => f.Count).Take(12).ToList();
            double best = double.MaxValue;
            for (int i = 0; i < c.Count; i++)
                for (int j = i + 1; j < c.Count; j++)
                    for (int k = j + 1; k < c.Count; k++)
                    {
                        var t = new[] { c[i], c[j], c[k] };
                        double minS = t.Min(f => f.Size), maxS = t.Max(f => f.Size);
                        if (maxS / minS > 1.7) continue;
                        double d01 = Dist(t[0], t[1]), d02 = Dist(t[0], t[2]), d12 = Dist(t[1], t[2]);
                        // the corner is the vertex opposite the longest side
                        Finder corner, a, b; double hyp, la, lb;
                        if (d12 >= d01 && d12 >= d02) { corner = t[0]; a = t[1]; b = t[2]; hyp = d12; la = d01; lb = d02; }
                        else if (d02 >= d01) { corner = t[1]; a = t[0]; b = t[2]; hyp = d02; la = d01; lb = d12; }
                        else { corner = t[2]; a = t[0]; b = t[1]; hyp = d01; la = d02; lb = d12; }
                        double legMax = Math.Max(la, lb), legMin = Math.Min(la, lb);
                        if (legMin / legMax < 0.7) continue;
                        double expect = Math.Sqrt(la * la + lb * lb);
                        if (Math.Abs(hyp - expect) / hyp > 0.2) continue;
                        double score = (legMax - legMin) / legMax + Math.Abs(hyp - expect) / hyp;
                        if (score >= best) continue;
                        best = score;
                        // in image coordinates (y down) the top-right to bottom-left order has a positive cross product
                        double cross = (a.X - corner.X) * (b.Y - corner.Y) - (a.Y - corner.Y) * (b.X - corner.X);
                        tl = corner;
                        if (cross > 0) { tr = a; bl = b; } else { tr = b; bl = a; }
                    }
            return tl != null;
        }

        private static double Dist(Finder a, Finder b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        // ------------------------------------------------------------ geometry

        /// <summary>A projective map from module coordinates to picture coordinates, solved from four point pairs.</summary>
        private sealed class Perspective
        {
            private readonly double a, b, c, d, e, f, g, h;
            public Perspective(double[] mx, double[] my, double[] ix, double[] iy)
            {
                var m = new double[8, 9];
                for (int i = 0; i < 4; i++)
                {
                    int r = i * 2;
                    m[r, 0] = mx[i]; m[r, 1] = my[i]; m[r, 2] = 1; m[r, 6] = -mx[i] * ix[i]; m[r, 7] = -my[i] * ix[i]; m[r, 8] = ix[i];
                    m[r + 1, 3] = mx[i]; m[r + 1, 4] = my[i]; m[r + 1, 5] = 1; m[r + 1, 6] = -mx[i] * iy[i]; m[r + 1, 7] = -my[i] * iy[i]; m[r + 1, 8] = iy[i];
                }
                for (int col = 0; col < 8; col++)
                {
                    int piv = col;
                    for (int r = col + 1; r < 8; r++) if (Math.Abs(m[r, col]) > Math.Abs(m[piv, col])) piv = r;
                    if (Math.Abs(m[piv, col]) < 1e-12) { Valid = false; return; }
                    if (piv != col) for (int k = 0; k < 9; k++) (m[col, k], m[piv, k]) = (m[piv, k], m[col, k]);
                    for (int r = 0; r < 8; r++)
                    {
                        if (r == col) continue;
                        double factor = m[r, col] / m[col, col];
                        for (int k = col; k < 9; k++) m[r, k] -= factor * m[col, k];
                    }
                }
                a = m[0, 8] / m[0, 0]; b = m[1, 8] / m[1, 1]; c = m[2, 8] / m[2, 2]; d = m[3, 8] / m[3, 3];
                e = m[4, 8] / m[4, 4]; f = m[5, 8] / m[5, 5]; g = m[6, 8] / m[6, 6]; h = m[7, 8] / m[7, 7];
                Valid = true;
            }
            public bool Valid { get; private set; }
            public (double X, double Y) Map(double x, double y)
            {
                double den = g * x + h * y + 1;
                return ((a * x + b * y + c) / den, (d * x + e * y + f) / den);
            }
        }

        private static bool Dark(bool[] bw, int w, int h, double x, double y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            return ix >= 0 && iy >= 0 && ix < w && iy < h && bw[iy * w + ix];
        }

        private static bool InPicture(int w, int h, double x, double y) => x >= 0 && y >= 0 && x < w && y < h;

        /// <summary>Looks near the predicted spot for the alignment pattern (dark centre, light ring, dark ring) and returns its centre.</summary>
        private static bool FindAlignment(bool[] bw, int w, int h, double px, double py, double exX, double exY, double eyX, double eyY, double radiusPx, out double ax, out double ay)
        {
            ax = px; ay = py;
            double bestScore = 0; double bx = px, by = py;
            double ms = Math.Sqrt(exX * exX + exY * exY);
            double step = Math.Max(0.5, ms / 3);
            for (double oy = -radiusPx; oy <= radiusPx; oy += step)
                for (double ox = -radiusPx; ox <= radiusPx; ox += step)
                {
                    double cx = px + ox, cy = py + oy;
                    int score = 0;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            bool expect = Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1;
                            double sx = cx + dx * exX + dy * eyX, sy = cy + dx * exY + dy * eyY;
                            if (Dark(bw, w, h, sx, sy) == expect) score++;
                        }
                    // prefer the closest of equally good spots
                    double penalty = (ox * ox + oy * oy) / (radiusPx * radiusPx + 1) * 0.5;
                    if (score - penalty > bestScore) { bestScore = score - penalty; bx = cx; by = cy; }
                }
            if (bestScore < 21) return false;
            ax = bx; ay = by;
            return true;
        }

        // ------------------------------------------------------------ the whole pipeline on a binary picture

        private static bool TryDecodeBinary(bool[] bw, int w, int h, out string text, List<(int Rank, string Message)> reasons)
        {
            text = null;
            var finders = FindFinders(bw, w, h);
            if (!PickTriple(finders, out var tl, out var tr, out var bl))
            {
                reasons.Add((0, finders.Count(f => f.Count >= 2) > 0
                    ? "Finder patterns were seen, but not three that form the corners of one QR code (the picture may be cropped, too small or mirrored)."
                    : "No QR code was found in the picture: there are no finder patterns (the three large squares in the corners)."));
                return false;
            }
            double ms = (tl.Size + tr.Size + bl.Size) / 3;
            double dTr = Dist(tl, tr), dBl = Dist(tl, bl);
            int dimEst = (int)Math.Round((dTr + dBl) / 2 / ms + 7);
            int vEst = (int)Math.Round((dimEst - 17) / 4.0);
            if (vEst > QrCommon.MaxVersion + 1)
            {
                reasons.Add((3, "Unsupported version: this QR code looks like version " + vEst + ". Versions 1 to " + QrCommon.MaxVersion + " (up to about 200 characters) are supported."));
                return false;
            }
            var tries = new List<int>();
            foreach (var d in new[] { 0, -1, 1, -2, 2 }) { int v = vEst + d; if (v >= 1 && v <= QrCommon.MaxVersion && !tries.Contains(v)) tries.Add(v); }
            if (tries.Count == 0) { reasons.Add((3, "Unsupported version: the code is too small to measure.")); return false; }
            foreach (var v in tries)
                if (TryVersion(bw, w, h, tl, tr, bl, v, out text, reasons)) return true;
            if (vEst > QrCommon.MaxVersion) reasons.Add((3, "Unsupported version: this QR code looks like version " + vEst + ". Versions 1 to " + QrCommon.MaxVersion + " are supported."));
            return false;
        }

        private static bool TryVersion(bool[] bw, int w, int h, Finder tl, Finder tr, Finder bl, int version, out string text, List<(int Rank, string Message)> reasons)
        {
            text = null;
            int dim = QrCommon.Size(version);
            double span = dim - 7;
            double exX = (tr.X - tl.X) / span, exY = (tr.Y - tl.Y) / span, eyX = (bl.X - tl.X) / span, eyY = (bl.Y - tl.Y) / span;
            double brX = tr.X + bl.X - tl.X, brY = tr.Y + bl.Y - tl.Y;

            // First choice: anchor the fourth corner on the alignment pattern, which copes with perspective. Second choice: assume a parallelogram.
            var anchors = new List<(double[] Mx, double[] My, double[] Ix, double[] Iy)>();
            if (version >= 2)
            {
                double off = dim - 10; // from the top-left finder centre (3.5) to the alignment centre (dim - 6.5)
                double px = tl.X + exX * off + eyX * off, py = tl.Y + exY * off + eyY * off;
                double radius = 4 * Math.Sqrt(exX * exX + exY * exY) + 2;
                if (FindAlignment(bw, w, h, px, py, exX, exY, eyX, eyY, radius, out double ax, out double ay))
                    anchors.Add((new[] { 3.5, dim - 3.5, 3.5, dim - 6.5 }, new[] { 3.5, 3.5, dim - 3.5, dim - 6.5 }, new[] { tl.X, tr.X, bl.X, ax }, new[] { tl.Y, tr.Y, bl.Y, ay }));
            }
            anchors.Add((new[] { 3.5, dim - 3.5, 3.5, dim - 3.5 }, new[] { 3.5, 3.5, dim - 3.5, dim - 3.5 }, new[] { tl.X, tr.X, bl.X, brX }, new[] { tl.Y, tr.Y, bl.Y, brY }));

            foreach (var an in anchors)
            {
                var tf = new Perspective(an.Mx, an.My, an.Ix, an.Iy);
                if (!tf.Valid) continue;
                var grid = new bool[dim, dim];
                double[] offs = { 0, -0.22, 0.22 };
                bool off = false;
                for (int r = 0; r < dim && !off; r++)
                    for (int c = 0; c < dim; c++)
                    {
                        int votes = 0, seen = 0;
                        foreach (var oy in offs)
                            foreach (var ox in offs)
                            {
                                if ((ox != 0 || oy != 0) && ox != oy && ox != -oy) continue; // the centre and the four diagonals
                                var p = tf.Map(c + 0.5 + ox, r + 0.5 + oy);
                                if (!InPicture(w, h, p.X, p.Y)) continue;
                                seen++; if (Dark(bw, w, h, p.X, p.Y)) votes++;
                            }
                        if (seen == 0) { off = true; break; }
                        grid[r, c] = votes * 2 > seen;
                    }
                if (off) { reasons.Add((1, "The code runs off the edge of the picture.")); continue; }
                if (DecodeGrid(grid, version, out text, reasons)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------ reading the symbol

        private static int Hamming(int a, int b) { int x = a ^ b, n = 0; while (x != 0) { n += x & 1; x >>= 1; } return n; }

        private static bool ReadFormat(bool[,] g, int size, out QrLevel level, out int mask)
        {
            level = QrLevel.M; mask = 0;
            int w1 = 0, w2 = 0;
            void Bit(ref int word, int i, bool dark) { if (dark) word |= 1 << i; }
            for (int i = 0; i <= 5; i++) Bit(ref w1, i, g[i, 8]);
            Bit(ref w1, 6, g[7, 8]); Bit(ref w1, 7, g[8, 8]); Bit(ref w1, 8, g[8, 7]);
            for (int i = 9; i < 15; i++) Bit(ref w1, i, g[8, 14 - i]);
            for (int i = 0; i < 8; i++) Bit(ref w2, i, g[8, size - 1 - i]);
            for (int i = 8; i < 15; i++) Bit(ref w2, i, g[size - 15 + i, 8]);
            int bestData = -1, bestDist = 4;
            for (int data = 0; data < 32; data++)
            {
                int cand = QrCommon.FormatWord(QrCommon.LevelFromBits(data >> 3), data & 7);
                int dist = Math.Min(Hamming(cand, w1), Hamming(cand, w2));
                if (dist < bestDist) { bestDist = dist; bestData = data; }
            }
            if (bestData < 0) return false;
            level = QrCommon.LevelFromBits(bestData >> 3); mask = bestData & 7;
            return true;
        }

        private static bool DecodeGrid(bool[,] g, int version, out string text, List<(int Rank, string Message)> reasons)
        {
            text = null;
            int size = QrCommon.Size(version);
            if (!ReadFormat(g, size, out var level, out int mask))
            {
                reasons.Add((1, "The format information (next to the corner squares) could not be read, so the code is too damaged or blurred to decode."));
                return false;
            }
            var tmpl = new QrCommon.Grid(version);
            var bits = new List<bool>();
            foreach (var (x, y) in QrCommon.DataPath(tmpl.Function, size))
                bits.Add(g[y, x] ^ QrCommon.MaskApplies(mask, x, y));
            int total = QrCommon.TotalCodewords[version - 1];
            var words = new byte[total];
            for (int i = 0; i < total; i++)
            {
                int v = 0; for (int j = 0; j < 8; j++) v = (v << 1) | (bits[i * 8 + j] ? 1 : 0);
                words[i] = (byte)v;
            }

            var lens = QrCommon.BlockDataLengths(version, level, out int ecc);
            int nb = lens.Length, maxLen = lens.Max();
            var blocks = new byte[nb][];
            for (int k = 0; k < nb; k++) blocks[k] = new byte[lens[k] + ecc];
            int at = 0;
            for (int i = 0; i < maxLen; i++)
                for (int k = 0; k < nb; k++) if (i < lens[k]) blocks[k][i] = words[at++];
            for (int i = 0; i < ecc; i++)
                for (int k = 0; k < nb; k++) blocks[k][lens[k] + i] = words[at++];

            var data = new List<byte>();
            for (int k = 0; k < nb; k++)
            {
                if (!QrCommon.RsCorrect(blocks[k], ecc))
                {
                    reasons.Add((2, "Too many errors: the code is damaged or blurred beyond what its error correction (level " + level + ") can repair."));
                    return false;
                }
                for (int i = 0; i < lens[k]; i++) data.Add(blocks[k][i]);
            }
            return ParseSegments(data.ToArray(), version, out text, reasons);
        }

        private const string Alnum = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

        private static bool ParseSegments(byte[] data, int version, out string text, List<(int Rank, string Message)> reasons)
        {
            text = null;
            int pos = 0, total = data.Length * 8;
            int Read(int n)
            {
                if (pos + n > total) throw new FormatException();
                int v = 0;
                for (int i = 0; i < n; i++) { v = (v << 1) | ((data[pos >> 3] >> (7 - (pos & 7))) & 1); pos++; }
                return v;
            }
            var sb = new StringBuilder();
            var bytes = new List<byte>();
            void FlushBytes()
            {
                if (bytes.Count == 0) return;
                var arr = bytes.ToArray(); bytes.Clear();
                try { sb.Append(new UTF8Encoding(false, true).GetString(arr)); }
                catch (ArgumentException) { sb.Append(Encoding.Latin1.GetString(arr)); }
            }
            try
            {
                while (total - pos >= 4)
                {
                    int mode = Read(4);
                    if (mode == 0) break;
                    switch (mode)
                    {
                        case 1:
                        {
                            FlushBytes();
                            int n = Read(version < 10 ? 10 : 12);
                            while (n >= 3) { sb.Append(Read(10).ToString("000")); n -= 3; }
                            if (n == 2) sb.Append(Read(7).ToString("00"));
                            else if (n == 1) sb.Append(Read(4).ToString());
                            break;
                        }
                        case 2:
                        {
                            FlushBytes();
                            int n = Read(version < 10 ? 9 : 11);
                            while (n >= 2) { int v = Read(11); sb.Append(Alnum[v / 45]).Append(Alnum[v % 45]); n -= 2; }
                            if (n == 1) sb.Append(Alnum[Read(6)]);
                            break;
                        }
                        case 4:
                        {
                            int n = Read(version < 10 ? 8 : 16);
                            for (int i = 0; i < n; i++) bytes.Add((byte)Read(8));
                            break;
                        }
                        case 7: // extended channel interpretation: skip the designator, the bytes that follow are still read as UTF-8
                        {
                            int first = Read(8);
                            if ((first & 0x80) == 0) { }
                            else if ((first & 0xC0) == 0x80) Read(8);
                            else Read(16);
                            break;
                        }
                        default:
                            reasons.Add((2, mode == 8 ? "Unsupported content: this QR code uses Kanji mode, which is not supported." : "Unsupported content: this QR code uses a segment type (" + mode + ") that is not supported."));
                            return false;
                    }
                }
            }
            catch (FormatException)
            {
                reasons.Add((2, "The code was corrected but its content is cut short, so it cannot be read."));
                return false;
            }
            FlushBytes();
            text = sb.ToString();
            return true;
        }
    }
}
