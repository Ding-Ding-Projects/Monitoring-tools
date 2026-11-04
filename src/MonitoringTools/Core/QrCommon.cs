using System;
using System.Collections.Generic;

namespace MonitoringTools.Core
{
    /// <summary>QR error-correction level. The values are the order used by the tables below.</summary>
    internal enum QrLevel { L = 0, M = 1, Q = 2, H = 3 }

    /// <summary>
    /// Shared QR Code (ISO/IEC 18004 Model 2) tables and arithmetic for versions 1 to 10: block structure, function
    /// patterns, format information and Reed-Solomon coding over GF(256). The encoder and the decoder both use it,
    /// so a mistake here cannot be hidden by the two halves agreeing on a private convention.
    /// </summary>
    internal static class QrCommon
    {
        public const int MaxVersion = 10;

        public static int Size(int version) => 17 + 4 * version;

        // Per version and level (L, M, Q, H): error-correction codewords per block, then (blocks, data codewords) for
        // group 1 and for group 2.
        private static readonly int[][][] Blocks =
        {
            new[] { new[] { 7, 1, 19, 0, 0 }, new[] { 10, 1, 16, 0, 0 }, new[] { 13, 1, 13, 0, 0 }, new[] { 17, 1, 9, 0, 0 } },
            new[] { new[] { 10, 1, 34, 0, 0 }, new[] { 16, 1, 28, 0, 0 }, new[] { 22, 1, 22, 0, 0 }, new[] { 28, 1, 16, 0, 0 } },
            new[] { new[] { 15, 1, 55, 0, 0 }, new[] { 26, 1, 44, 0, 0 }, new[] { 18, 2, 17, 0, 0 }, new[] { 22, 2, 13, 0, 0 } },
            new[] { new[] { 20, 1, 80, 0, 0 }, new[] { 18, 2, 32, 0, 0 }, new[] { 26, 2, 24, 0, 0 }, new[] { 16, 4, 9, 0, 0 } },
            new[] { new[] { 26, 1, 108, 0, 0 }, new[] { 24, 2, 43, 0, 0 }, new[] { 18, 2, 15, 2, 16 }, new[] { 22, 2, 11, 2, 12 } },
            new[] { new[] { 18, 2, 68, 0, 0 }, new[] { 16, 4, 27, 0, 0 }, new[] { 24, 4, 19, 0, 0 }, new[] { 28, 4, 15, 0, 0 } },
            new[] { new[] { 20, 2, 78, 0, 0 }, new[] { 18, 4, 31, 0, 0 }, new[] { 18, 2, 14, 4, 15 }, new[] { 26, 4, 13, 1, 14 } },
            new[] { new[] { 24, 2, 97, 0, 0 }, new[] { 22, 2, 38, 2, 39 }, new[] { 22, 4, 18, 2, 19 }, new[] { 26, 4, 14, 2, 15 } },
            new[] { new[] { 30, 2, 116, 0, 0 }, new[] { 22, 3, 36, 2, 37 }, new[] { 20, 4, 16, 4, 17 }, new[] { 24, 4, 12, 4, 13 } },
            new[] { new[] { 18, 2, 68, 2, 69 }, new[] { 26, 4, 43, 1, 44 }, new[] { 24, 6, 19, 2, 20 }, new[] { 28, 6, 15, 2, 16 } },
        };

        public static readonly int[] TotalCodewords = { 26, 44, 70, 100, 134, 172, 196, 242, 292, 346 };

        private static readonly int[][] AlignmentCenters =
        {
            new int[0], new[] { 6, 18 }, new[] { 6, 22 }, new[] { 6, 26 }, new[] { 6, 30 },
            new[] { 6, 34 }, new[] { 6, 22, 38 }, new[] { 6, 24, 42 }, new[] { 6, 26, 46 }, new[] { 6, 28, 50 },
        };

        /// <summary>The data-block lengths of a version and level, in codeword order, plus the error-correction length per block.</summary>
        public static int[] BlockDataLengths(int version, QrLevel level, out int eccPerBlock)
        {
            var t = Blocks[version - 1][(int)level];
            eccPerBlock = t[0];
            var list = new List<int>();
            for (int i = 0; i < t[1]; i++) list.Add(t[2]);
            for (int i = 0; i < t[3]; i++) list.Add(t[4]);
            return list.ToArray();
        }

        public static int DataCodewords(int version, QrLevel level)
        {
            var lens = BlockDataLengths(version, level, out _);
            int n = 0; foreach (var l in lens) n += l;
            return n;
        }

        // ------------------------------------------------------------ GF(256), primitive polynomial 0x11D

        private static readonly byte[] Exp = new byte[512];
        private static readonly int[] Log = new int[256];

        static QrCommon()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                Exp[i] = (byte)x; Log[x] = i;
                x <<= 1; if (x >= 256) x ^= 0x11D;
            }
            for (int i = 255; i < 512; i++) Exp[i] = Exp[i - 255];
        }

        public static int Mul(int a, int b) => a == 0 || b == 0 ? 0 : Exp[Log[a] + Log[b]];
        public static int Div(int a, int b) => a == 0 ? 0 : Exp[(Log[a] + 255 - Log[b]) % 255];
        public static int Pow(int exponent) => Exp[((exponent % 255) + 255) % 255];

        private static readonly Dictionary<int, byte[]> Generators = new Dictionary<int, byte[]>();

        /// <summary>The generator polynomial for n error-correction codewords, highest degree first without the leading 1.</summary>
        private static byte[] Generator(int n)
        {
            lock (Generators)
            {
                if (Generators.TryGetValue(n, out var g)) return g;
                var poly = new byte[n]; poly[n - 1] = 1; // coefficients of x^(n-1) .. x^0, leading 1 implied
                int root = 1;
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        poly[j] = (byte)Mul(poly[j], root);
                        if (j + 1 < n) poly[j] ^= poly[j + 1];
                    }
                    root = Mul(root, 2);
                }
                Generators[n] = poly;
                return poly;
            }
        }

        /// <summary>The Reed-Solomon remainder of a data block, which is its error-correction codewords.</summary>
        public static byte[] RsEncode(byte[] data, int eccLen)
        {
            var gen = Generator(eccLen);
            var result = new byte[eccLen];
            foreach (var b in data)
            {
                int factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, eccLen - 1);
                result[eccLen - 1] = 0;
                for (int i = 0; i < eccLen; i++) result[i] ^= (byte)Mul(gen[i], factor);
            }
            return result;
        }

        /// <summary>
        /// Corrects a codeword block (data followed by error correction) in place. Returns false when there are more errors
        /// than the code can repair, or when the corrected block still fails its check.
        /// </summary>
        public static bool RsCorrect(byte[] msg, int nsym)
        {
            int n = msg.Length;
            var synd = new int[nsym];
            bool clean = true;
            for (int j = 0; j < nsym; j++)
            {
                int s = 0, a = Pow(j);
                for (int i = 0; i < n; i++) s = Mul(s, a) ^ msg[i];
                synd[j] = s;
                if (s != 0) clean = false;
            }
            if (clean) return true;

            // Berlekamp-Massey: the error-locator polynomial, lowest degree first.
            var sigma = new List<int> { 1 };
            var prev = new List<int> { 1 };
            int L = 0, m = 1, b = 1;
            for (int i = 0; i < nsym; i++)
            {
                int d = synd[i];
                for (int k = 1; k <= L && k < sigma.Count; k++) d ^= Mul(sigma[k], synd[i - k]);
                if (d == 0) { m++; continue; }
                var t = new List<int>(sigma);
                int coef = Div(d, b);
                while (sigma.Count < prev.Count + m) sigma.Add(0);
                for (int k = 0; k < prev.Count; k++) sigma[k + m] ^= Mul(coef, prev[k]);
                if (2 * L <= i) { L = i + 1 - L; prev = t; b = d; m = 1; }
                else m++;
            }
            while (sigma.Count > 1 && sigma[sigma.Count - 1] == 0) sigma.RemoveAt(sigma.Count - 1);
            if (L * 2 > nsym || sigma.Count - 1 != L) return false;

            // Chien search: positions whose locator root is found.
            var positions = new List<int>(); // power of x
            for (int i = 0; i < n; i++)
            {
                int xinv = Pow(-i), v = 0, xp = 1;
                for (int k = 0; k < sigma.Count; k++) { v ^= Mul(sigma[k], xp); xp = Mul(xp, xinv); }
                if (v == 0) positions.Add(i);
            }
            if (positions.Count != L) return false;

            // Forney: the error values.
            var omega = new int[nsym];
            for (int k = 0; k < nsym; k++)
            {
                int v = 0;
                for (int i = 0; i <= k && i < sigma.Count; i++) v ^= Mul(sigma[i], synd[k - i]);
                omega[k] = v;
            }
            foreach (var pos in positions)
            {
                int xinv = Pow(-pos), x = Pow(pos);
                int num = 0, xp = 1;
                for (int k = 0; k < nsym; k++) { num ^= Mul(omega[k], xp); xp = Mul(xp, xinv); }
                int den = 0;
                for (int k = 1; k < sigma.Count; k += 2) den ^= Mul(sigma[k], Pow(-pos * (k - 1)));
                if (den == 0) return false;
                msg[n - 1 - pos] ^= (byte)Mul(x, Div(num, den));
            }

            for (int j = 0; j < nsym; j++)
            {
                int s = 0, a = Pow(j);
                for (int i = 0; i < n; i++) s = Mul(s, a) ^ msg[i];
                if (s != 0) return false;
            }
            return true;
        }

        // ------------------------------------------------------------ the symbol grid

        /// <summary>A symbol under construction: its modules and which of them belong to function patterns rather than data.</summary>
        public sealed class Grid
        {
            public readonly int Size;
            public readonly bool[,] Modules; // [y, x], true is dark
            public readonly bool[,] Function;

            public Grid(int version)
            {
                Size = QrCommon.Size(version);
                Modules = new bool[Size, Size];
                Function = new bool[Size, Size];
                DrawFunctionPatterns(version);
            }

            public void SetFunction(int x, int y, bool dark) { Modules[y, x] = dark; Function[y, x] = true; }

            private void DrawFunctionPatterns(int version)
            {
                for (int i = 0; i < Size; i++) { SetFunction(6, i, i % 2 == 0); SetFunction(i, 6, i % 2 == 0); }
                DrawFinder(3, 3); DrawFinder(Size - 4, 3); DrawFinder(3, Size - 4);
                var pos = AlignmentCenters[version - 1];
                int last = pos.Length - 1;
                for (int i = 0; i <= last; i++)
                    for (int j = 0; j <= last; j++)
                    {
                        if ((i == 0 && j == 0) || (i == 0 && j == last) || (i == last && j == 0)) continue;
                        DrawAlignment(pos[i], pos[j]);
                    }
                DrawFormat(QrLevel.M, 0); // reserves the format areas; the real bits are written once the mask is known
                if (version >= 7) DrawVersion(version);
            }

            private void DrawFinder(int cx, int cy)
            {
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        int xx = cx + dx, yy = cy + dy;
                        if (xx >= 0 && xx < Size && yy >= 0 && yy < Size) SetFunction(xx, yy, dist != 2 && dist != 4);
                    }
            }

            private void DrawAlignment(int cx, int cy)
            {
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        SetFunction(cx + dx, cy + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }

            private void DrawVersion(int version)
            {
                int rem = version;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int bits = (version << 12) | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool bit = ((bits >> i) & 1) != 0;
                    int a = Size - 11 + i % 3, b = i / 3;
                    SetFunction(a, b, bit); SetFunction(b, a, bit);
                }
            }

            public void DrawFormat(QrLevel level, int mask)
            {
                int bits = FormatWord(level, mask);
                for (int i = 0; i <= 5; i++) SetFunction(8, i, GetBit(bits, i));
                SetFunction(8, 7, GetBit(bits, 6));
                SetFunction(8, 8, GetBit(bits, 7));
                SetFunction(7, 8, GetBit(bits, 8));
                for (int i = 9; i < 15; i++) SetFunction(14 - i, 8, GetBit(bits, i));
                for (int i = 0; i < 8; i++) SetFunction(Size - 1 - i, 8, GetBit(bits, i));
                for (int i = 8; i < 15; i++) SetFunction(8, Size - 15 + i, GetBit(bits, i));
                SetFunction(8, Size - 8, true); // the always-dark module
            }
        }

        private static bool GetBit(int value, int index) => ((value >> index) & 1) != 0;

        /// <summary>The 2-bit error-correction indicator that goes into the format word.</summary>
        public static int LevelBits(QrLevel level) => level switch { QrLevel.L => 1, QrLevel.M => 0, QrLevel.Q => 3, _ => 2 };
        public static QrLevel LevelFromBits(int bits) => bits switch { 1 => QrLevel.L, 0 => QrLevel.M, 3 => QrLevel.Q, _ => QrLevel.H };

        /// <summary>The 15-bit format word: level and mask, BCH protected and masked with 0x5412.</summary>
        public static int FormatWord(QrLevel level, int mask)
        {
            int data = (LevelBits(level) << 3) | mask;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            return ((data << 10) | rem) ^ 0x5412;
        }

        public static bool MaskApplies(int mask, int x, int y)
        {
            switch (mask)
            {
                case 0: return (x + y) % 2 == 0;
                case 1: return y % 2 == 0;
                case 2: return x % 3 == 0;
                case 3: return (x + y) % 3 == 0;
                case 4: return (x / 3 + y / 2) % 2 == 0;
                case 5: return x * y % 2 + x * y % 3 == 0;
                case 6: return (x * y % 2 + x * y % 3) % 2 == 0;
                default: return ((x + y) % 2 + x * y % 3) % 2 == 0;
            }
        }

        /// <summary>The positions of the data modules in reading order: columns in pairs from the right, zigzag, skipping the timing column.</summary>
        public static IEnumerable<(int X, int Y)> DataPath(bool[,] function, int size)
        {
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? size - 1 - vert : vert;
                        if (!function[y, x]) yield return (x, y);
                    }
            }
        }
    }
}
