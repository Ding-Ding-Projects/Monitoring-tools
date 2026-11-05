using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MonitoringTools.Media
{
    /// <summary>One tag of a TIFF-style directory, with its value kept as the raw bytes of the source byte order.</summary>
    public sealed class ExifEntry
    {
        public ushort Tag, Type;
        public uint Count;
        public byte[] Value;
    }

    /// <summary>The metadata block of a JPEG, PNG or WebP picture (all three carry a TIFF stream): the main directory, the Exif
    /// directory, the GPS directory and the thumbnail directory, parsed so that groups of tags can be dropped and the rest written back.</summary>
    public sealed class ExifData
    {
        public bool BigEndian;
        public List<ExifEntry> Ifd0 = new List<ExifEntry>(), Exif = new List<ExifEntry>(), Gps = new List<ExifEntry>(), Ifd1 = new List<ExifEntry>();
        public byte[] Thumbnail;

        public const ushort TagExifPtr = 0x8769, TagGpsPtr = 0x8825, TagInterop = 0xA005, TagThumbOffset = 0x0201, TagThumbLength = 0x0202;
        public const ushort TagOrientation = 0x0112;

        private static readonly int[] TypeSize = { 0, 1, 1, 2, 4, 8, 1, 1, 2, 4, 8, 4, 8 };
        internal static int SizeOf(ushort type) => type >= 1 && type < TypeSize.Length ? TypeSize[type] : 0;

        // ------------------------------------------------------------ reading

        public static ExifData Parse(byte[] tiff, int start = 0, int length = -1)
        {
            try
            {
                if (length < 0) length = tiff.Length - start;
                if (length < 8) return null;
                var d = new ExifData();
                if (tiff[start] == 'M' && tiff[start + 1] == 'M') d.BigEndian = true;
                else if (!(tiff[start] == 'I' && tiff[start + 1] == 'I')) return null;
                if (U16(tiff, start + 2, d.BigEndian) != 42) return null;
                long ifd0 = U32(tiff, start + 4, d.BigEndian);
                long next0 = ReadIfd(tiff, start, length, ifd0, d.BigEndian, d.Ifd0);
                var ptr = d.Ifd0.FirstOrDefault(e => e.Tag == TagExifPtr);
                if (ptr != null) { ReadIfd(tiff, start, length, PtrValue(ptr, d.BigEndian), d.BigEndian, d.Exif); d.Ifd0.Remove(ptr); }
                var gps = d.Ifd0.FirstOrDefault(e => e.Tag == TagGpsPtr);
                if (gps != null) { ReadIfd(tiff, start, length, PtrValue(gps, d.BigEndian), d.BigEndian, d.Gps); d.Ifd0.Remove(gps); }
                d.Exif.RemoveAll(e => e.Tag == TagInterop);
                if (next0 > 0) ReadIfd(tiff, start, length, next0, d.BigEndian, d.Ifd1);
                var to = d.Ifd1.FirstOrDefault(e => e.Tag == TagThumbOffset); var tl = d.Ifd1.FirstOrDefault(e => e.Tag == TagThumbLength);
                if (to != null && tl != null)
                {
                    long off = PtrValue(to, d.BigEndian), len = PtrValue(tl, d.BigEndian);
                    if (off > 0 && len > 0 && off + len <= length) { d.Thumbnail = new byte[len]; Array.Copy(tiff, start + off, d.Thumbnail, 0, len); }
                    d.Ifd1.Remove(to); d.Ifd1.Remove(tl);
                }
                return d;
            }
            catch { return null; }
        }

        private static long ReadIfd(byte[] b, int start, int length, long off, bool be, List<ExifEntry> into)
        {
            if (off < 8 || off + 2 > length) return 0;
            int n = U16(b, start + (int)off, be);
            if (n > 2000 || off + 2 + n * 12 > length) return 0;
            for (int i = 0; i < n; i++)
            {
                int p = start + (int)off + 2 + i * 12;
                var e = new ExifEntry { Tag = U16(b, p, be), Type = U16(b, p + 2, be), Count = U32(b, p + 4, be) };
                long size = (long)SizeOf(e.Type) * e.Count;
                if (SizeOf(e.Type) == 0 || size > length) continue;
                if (size <= 4) { e.Value = new byte[size]; Array.Copy(b, p + 8, e.Value, 0, size); }
                else
                {
                    long vo = U32(b, p + 8, be);
                    if (vo + size > length) continue;
                    e.Value = new byte[size]; Array.Copy(b, start + vo, e.Value, 0, size);
                }
                into.Add(e);
            }
            long nextPos = start + off + 2 + n * 12L;
            return nextPos + 4 <= start + length ? U32(b, (int)nextPos, be) : 0;
        }

        internal static ushort U16(byte[] b, int p, bool be) => be ? (ushort)((b[p] << 8) | b[p + 1]) : (ushort)(b[p] | (b[p + 1] << 8));
        internal static uint U32(byte[] b, int p, bool be) => be ? (uint)((b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3]) : (uint)(b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24));
        private static long PtrValue(ExifEntry e, bool be) => e.Value.Length >= 4 ? U32(e.Value, 0, be) : e.Value.Length >= 2 ? U16(e.Value, 0, be) : 0;

        public string Ascii(List<ExifEntry> ifd, ushort tag)
        {
            var e = ifd.FirstOrDefault(x => x.Tag == tag && x.Value != null);
            if (e == null) return null;
            int len = e.Value.Length;
            while (len > 0 && e.Value[len - 1] == 0) len--;
            return len == 0 ? null : Encoding.UTF8.GetString(e.Value, 0, len).Trim();
        }

        public double[] Rationals(List<ExifEntry> ifd, ushort tag)
        {
            var e = ifd.FirstOrDefault(x => x.Tag == tag && (x.Type == 5 || x.Type == 10) && x.Value != null);
            if (e == null) return null;
            var r = new double[e.Value.Length / 8];
            for (int i = 0; i < r.Length; i++)
            {
                double num = U32(e.Value, i * 8, BigEndian), den = U32(e.Value, i * 8 + 4, BigEndian);
                r[i] = den == 0 ? 0 : num / den;
            }
            return r;
        }

        public int? Short(List<ExifEntry> ifd, ushort tag)
        {
            var e = ifd.FirstOrDefault(x => x.Tag == tag && x.Type == 3 && x.Value != null && x.Value.Length >= 2);
            return e == null ? (int?)null : U16(e.Value, 0, BigEndian);
        }

        // ------------------------------------------------------------ groups

        public const string Location = "location", Camera = "camera", DateTimeGroup = "datetime", Author = "author", Colour = "colour", Thumb = "thumbnail";

        public static string GroupOf(string ifd, ushort tag)
        {
            if (ifd == "gps") return Location;
            if (ifd == "ifd1") return Thumb;
            switch (tag)
            {
                case 0x010F: case 0x0110: return Camera;                                   // make, model
                case 0x0132: return DateTimeGroup;                                          // modify date
                case 0x0131: case 0x013B: case 0x8298: case 0x010E: return Author;          // software, artist, copyright, description
                case 0x9C9B: case 0x9C9C: case 0x9C9D: case 0x9C9E: case 0x9C9F: return Author;   // Windows XP title, comment, author, keywords, subject
                case 0x9003: case 0x9004: case 0x9290: case 0x9291: case 0x9292: case 0x9010: case 0x9011: case 0x9012: return DateTimeGroup;
                case 0xA430: case 0xA431: case 0xA432: case 0xA433: case 0xA434: case 0xA435: case 0x927C: case 0xA420: return Camera;
                case 0x9286: return Author;                                                 // user comment
            }
            return null;   // layout and exposure tags that identify nothing
        }

        /// <summary>How many tags belong to a group.</summary>
        public int CountIn(string group)
        {
            int n = 0;
            foreach (var e in Ifd0) if (GroupOf("ifd0", e.Tag) == group) n++;
            foreach (var e in Exif) if (GroupOf("exif", e.Tag) == group) n++;
            if (group == Location) n += Gps.Count;
            if (group == Thumb && (Thumbnail != null || Ifd1.Count > 0)) n += 1;
            return n;
        }

        /// <summary>A copy that keeps everything except the groups to remove. The Orientation tag is always kept when keepOrientation is true.</summary>
        public ExifData Without(ISet<string> remove)
        {
            var d = new ExifData { BigEndian = BigEndian };
            d.Ifd0.AddRange(Ifd0.Where(e => !remove.Contains(GroupOf("ifd0", e.Tag) ?? "")));
            d.Exif.AddRange(Exif.Where(e => !remove.Contains(GroupOf("exif", e.Tag) ?? "")));
            if (!remove.Contains(Location)) d.Gps.AddRange(Gps);
            if (!remove.Contains(Thumb)) { d.Ifd1.AddRange(Ifd1); d.Thumbnail = Thumbnail; }
            return d;
        }

        public bool IsEmpty => Ifd0.Count == 0 && Exif.Count == 0 && Gps.Count == 0 && Thumbnail == null && Ifd1.Count == 0;

        // ------------------------------------------------------------ writing

        /// <summary>Serialises to a TIFF stream in the source byte order. Returns null when nothing is left to write.</summary>
        public byte[] ToTiff()
        {
            if (IsEmpty) return null;
            bool be = BigEndian;
            var ifd0 = Ifd0.OrderBy(e => e.Tag).ToList();
            var exif = Exif.OrderBy(e => e.Tag).ToList();
            var gps = Gps.OrderBy(e => e.Tag).ToList();
            var ifd1 = Ifd1.OrderBy(e => e.Tag).ToList();
            bool hasThumb = Thumbnail != null && Thumbnail.Length > 0;
            // pointer entries are placeholders until the layout is known
            ExifEntry exifPtr = null, gpsPtr = null, thumbOff = null, thumbLen = null;
            if (exif.Count > 0) { exifPtr = new ExifEntry { Tag = TagExifPtr, Type = 4, Count = 1, Value = new byte[4] }; ifd0.Add(exifPtr); }
            if (gps.Count > 0) { gpsPtr = new ExifEntry { Tag = TagGpsPtr, Type = 4, Count = 1, Value = new byte[4] }; ifd0.Add(gpsPtr); }
            if (hasThumb) { thumbOff = new ExifEntry { Tag = TagThumbOffset, Type = 4, Count = 1, Value = new byte[4] }; thumbLen = new ExifEntry { Tag = TagThumbLength, Type = 4, Count = 1, Value = new byte[4] }; ifd1.Add(thumbOff); ifd1.Add(thumbLen); }
            ifd0 = ifd0.OrderBy(e => e.Tag).ToList(); ifd1 = ifd1.OrderBy(e => e.Tag).ToList();
            bool writeIfd1 = ifd1.Count > 0;

            long off0 = 8;
            long offExif = off0 + IfdSize(ifd0);
            long offGps = offExif + (exif.Count > 0 ? IfdSize(exif) : 0);
            long off1 = offGps + (gps.Count > 0 ? IfdSize(gps) : 0);
            long offThumb = off1 + (writeIfd1 ? IfdSize(ifd1) : 0);
            if (exifPtr != null) Put32(exifPtr.Value, 0, (uint)offExif, be);
            if (gpsPtr != null) Put32(gpsPtr.Value, 0, (uint)offGps, be);
            if (hasThumb) { Put32(thumbOff.Value, 0, (uint)offThumb, be); Put32(thumbLen.Value, 0, (uint)Thumbnail.Length, be); }
            // the thumbnail's own directory is rewritten once the offsets are in place
            long total = offThumb + (hasThumb ? Thumbnail.Length : 0);
            var buf = new byte[total];
            buf[0] = buf[1] = (byte)(be ? 'M' : 'I');
            Put16(buf, 2, 42, be); Put32(buf, 4, 8, be);
            WriteIfd(buf, off0, ifd0, writeIfd1 ? off1 : 0, be);
            if (exif.Count > 0) WriteIfd(buf, offExif, exif, 0, be);
            if (gps.Count > 0) WriteIfd(buf, offGps, gps, 0, be);
            if (writeIfd1) WriteIfd(buf, off1, ifd1, 0, be);
            if (hasThumb) Array.Copy(Thumbnail, 0, buf, offThumb, Thumbnail.Length);
            return buf;
        }

        private static long IfdSize(List<ExifEntry> e)
        {
            long s = 2 + 12L * e.Count + 4;
            foreach (var x in e) if (x.Value.Length > 4) s += (x.Value.Length + 1) & ~1;
            return s;
        }

        private static void WriteIfd(byte[] buf, long off, List<ExifEntry> entries, long nextIfd, bool be)
        {
            Put16(buf, (int)off, (ushort)entries.Count, be);
            long valuePos = off + 2 + 12L * entries.Count + 4;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                int p = (int)off + 2 + i * 12;
                Put16(buf, p, e.Tag, be); Put16(buf, p + 2, e.Type, be); Put32(buf, p + 4, e.Count, be);
                if (e.Value.Length <= 4) Array.Copy(e.Value, 0, buf, p + 8, e.Value.Length);
                else
                {
                    Put32(buf, p + 8, (uint)valuePos, be);
                    Array.Copy(e.Value, 0, buf, valuePos, e.Value.Length);
                    valuePos += (e.Value.Length + 1) & ~1;
                }
            }
            Put32(buf, (int)(off + 2 + 12L * entries.Count), (uint)nextIfd, be);
        }

        internal static void Put16(byte[] b, int p, ushort v, bool be) { if (be) { b[p] = (byte)(v >> 8); b[p + 1] = (byte)v; } else { b[p] = (byte)v; b[p + 1] = (byte)(v >> 8); } }
        internal static void Put32(byte[] b, int p, uint v, bool be)
        {
            if (be) { b[p] = (byte)(v >> 24); b[p + 1] = (byte)(v >> 16); b[p + 2] = (byte)(v >> 8); b[p + 3] = (byte)v; }
            else { b[p] = (byte)v; b[p + 1] = (byte)(v >> 8); b[p + 2] = (byte)(v >> 16); b[p + 3] = (byte)(v >> 24); }
        }

        // ------------------------------------------------------------ reading summaries

        public static string FormatCoordinate(double deg, bool latitude)
        {
            string hemi = latitude ? (deg < 0 ? "S" : "N") : (deg < 0 ? "W" : "E");
            return Math.Abs(deg).ToString("0.0000", CultureInfo.InvariantCulture) + "° " + hemi;
        }

        public bool TryGps(out double lat, out double lon, out double? alt, out double? accuracy)
        {
            lat = lon = 0; alt = null; accuracy = null;
            var la = Rationals(Gps, 2); var lo = Rationals(Gps, 4);
            if (la == null || lo == null || la.Length < 3 || lo.Length < 3) return false;
            lat = la[0] + la[1] / 60 + la[2] / 3600; lon = lo[0] + lo[1] / 60 + lo[2] / 3600;
            if ((Ascii(Gps, 1) ?? "N").StartsWith("S", StringComparison.OrdinalIgnoreCase)) lat = -lat;
            if ((Ascii(Gps, 3) ?? "E").StartsWith("W", StringComparison.OrdinalIgnoreCase)) lon = -lon;
            var a = Rationals(Gps, 6); if (a != null && a.Length > 0) alt = a[0];
            var h = Rationals(Gps, 0x1F); if (h != null && h.Length > 0) accuracy = h[0];
            return true;
        }
    }
}
