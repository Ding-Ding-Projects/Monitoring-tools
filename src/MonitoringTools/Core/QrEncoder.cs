using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MonitoringTools.Core
{
    /// <summary>
    /// A small QR Code encoder written from the specification: byte mode, error-correction level M, versions 1 to 10
    /// (up to about 210 bytes of text), all eight masks tried and the lowest penalty kept. Nothing is sent anywhere
    /// and no library is used. It exists so an authenticator account can be moved to a phone as a picture.
    /// </summary>
    public static class QrEncoder
    {
        /// <summary>The largest text that fits, in UTF-8 bytes, at level M in version 10.</summary>
        public static int MaxBytes => (QrCommon.DataCodewords(QrCommon.MaxVersion, QrLevel.M) * 8 - 4 - 16) / 8;

        /// <summary>Encodes text. Returns false with a plain explanation when the text is too long for version 10.</summary>
        public static bool TryEncode(string text, out bool[,] modules, out int version, out string error) =>
            TryEncode(text, QrLevel.M, out modules, out version, out error);

        internal static bool TryEncode(string text, QrLevel level, out bool[,] modules, out int version, out string error)
        {
            modules = null; version = 0; error = null;
            var data = Encoding.UTF8.GetBytes(text ?? "");
            if (data.Length == 0) { error = "There is nothing to encode."; return false; }
            for (int v = 1; v <= QrCommon.MaxVersion; v++)
            {
                int capacityBits = QrCommon.DataCodewords(v, level) * 8;
                int countBits = v < 10 ? 8 : 16;
                if (4 + countBits + 8 * data.Length > capacityBits) continue;
                version = v;
                modules = Build(data, v, level);
                return true;
            }
            error = "That text is " + data.Length + " bytes, which is more than a version 10 QR code can hold (" + MaxBytes + " bytes at level M).";
            return false;
        }

        private static bool[,] Build(byte[] data, int version, QrLevel level)
        {
            int capacity = QrCommon.DataCodewords(version, level);

            // bit stream: mode 0100, character count, bytes, terminator, padding
            var bits = new List<bool>();
            void Put(int value, int count) { for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0); }
            Put(0x4, 4);
            Put(data.Length, version < 10 ? 8 : 16);
            foreach (var b in data) Put(b, 8);
            int cap = capacity * 8;
            for (int i = 0; i < 4 && bits.Count < cap; i++) bits.Add(false);
            while (bits.Count % 8 != 0) bits.Add(false);
            var cw = new List<byte>();
            for (int i = 0; i < bits.Count; i += 8)
            {
                int v = 0; for (int j = 0; j < 8; j++) v = (v << 1) | (bits[i + j] ? 1 : 0);
                cw.Add((byte)v);
            }
            for (int pad = 0xEC; cw.Count < capacity; pad ^= 0xEC ^ 0x11) cw.Add((byte)pad);

            return FromCodewords(cw.ToArray(), version, level);
        }

        /// <summary>Takes the finished data codewords of a version and level, adds error correction, interleaves, places and masks them.</summary>
        internal static bool[,] FromCodewords(byte[] cw, int version, QrLevel level)
        {
            // split into blocks, add error correction, interleave
            var lens = QrCommon.BlockDataLengths(version, level, out int ecc);
            var blocks = new List<byte[]>(); var eccs = new List<byte[]>();
            int at = 0;
            foreach (var len in lens)
            {
                var d = new byte[len]; Array.Copy(cw, at, d, 0, len); at += len;
                blocks.Add(d); eccs.Add(QrCommon.RsEncode(d, ecc));
            }
            var all = new List<byte>();
            int maxLen = 0; foreach (var l in lens) maxLen = Math.Max(maxLen, l);
            for (int i = 0; i < maxLen; i++)
                for (int k = 0; k < blocks.Count; k++) if (i < blocks[k].Length) all.Add(blocks[k][i]);
            for (int i = 0; i < ecc; i++)
                for (int k = 0; k < eccs.Count; k++) all.Add(eccs[k][i]);

            // place the codewords, then pick the mask with the lowest penalty
            bool[,] best = null; int bestScore = int.MaxValue;
            for (int mask = 0; mask < 8; mask++)
            {
                var g = new QrCommon.Grid(version);
                int i = 0;
                foreach (var (x, y) in QrCommon.DataPath(g.Function, g.Size))
                {
                    bool dark = i < all.Count * 8 && ((all[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                    g.Modules[y, x] = dark ^ QrCommon.MaskApplies(mask, x, y);
                    i++;
                }
                g.DrawFormat(level, mask);
                int score = Penalty(g.Modules, g.Size);
                if (score < bestScore) { bestScore = score; best = g.Modules; }
            }
            return best;
        }

        private static int Penalty(bool[,] m, int size)
        {
            int result = 0;
            // runs of five or more, in rows and in columns
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < size; a++)
                {
                    int run = 1;
                    for (int b = 1; b < size; b++)
                    {
                        bool cur = pass == 0 ? m[a, b] : m[b, a], prev = pass == 0 ? m[a, b - 1] : m[b - 1, a];
                        if (cur == prev) { run++; if (run == 5) result += 3; else if (run > 5) result++; }
                        else run = 1;
                    }
                }
            // 2 x 2 blocks of one colour
            for (int y = 0; y < size - 1; y++)
                for (int x = 0; x < size - 1; x++)
                    if (m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1]) result += 3;
            // finder-like 1:1:3:1:1 patterns with a light margin
            bool[] p1 = { true, false, true, true, true, false, true, false, false, false, false };
            bool[] p2 = { false, false, false, false, true, false, true, true, true, false, true };
            for (int pass = 0; pass < 2; pass++)
                for (int a = 0; a < size; a++)
                    for (int b = 0; b + 11 <= size; b++)
                    {
                        bool m1 = true, m2 = true;
                        for (int k = 0; k < 11 && (m1 || m2); k++)
                        {
                            bool v = pass == 0 ? m[a, b + k] : m[b + k, a];
                            if (v != p1[k]) m1 = false;
                            if (v != p2[k]) m2 = false;
                        }
                        if (m1) result += 40;
                        if (m2) result += 40;
                    }
            // balance of dark and light
            int dark = 0;
            foreach (var d in m) if (d) dark++;
            int total = size * size;
            int k2 = (int)Math.Ceiling(Math.Abs(dark * 20 - total * 10) / (double)total) - 1;
            if (k2 > 0) result += k2 * 10;
            return result;
        }

        /// <summary>The symbol as an 8-bit grey image with a quiet zone, scale pixels per module (dark is 0, light is 255).</summary>
        public static byte[] ToGray(bool[,] modules, int scale, int quiet, out int width, out int height)
        {
            int size = modules.GetLength(0);
            width = height = (size + 2 * quiet) * scale;
            var px = new byte[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = 255;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (!modules[y, x]) continue;
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                            px[((y + quiet) * scale + dy) * width + (x + quiet) * scale + dx] = 0;
                }
            return px;
        }

        /// <summary>The symbol as a frozen black-on-white bitmap, ready to show or save. The quiet zone is four modules, as the standard asks.</summary>
        public static BitmapSource ToBitmap(bool[,] modules, int scale = 8, int quiet = 4)
        {
            var px = ToGray(modules, scale, quiet, out int w, out int h);
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, px, w);
            bmp.Freeze();
            return bmp;
        }
    }
}
