using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;

namespace MonitoringTools.Media
{
    public sealed class PrivacyGroup
    {
        public string Id, Title, Detail;
        public int Tags;
        public bool Found;
    }

    /// <summary>What one picture carries besides its pixels.</summary>
    public sealed class PrivacyReport
    {
        public string Path, Kind, Reason;
        public bool Supported = true;
        public long Bytes;
        public List<PrivacyGroup> Groups = new List<PrivacyGroup>();
        public double? Lat, Lon, Altitude, Accuracy;
        public string Serial;
        public bool HasOrientation;
        public int Orientation = 1;
        public PrivacyGroup Group(string id) => Groups.FirstOrDefault(g => g.Id == id);
        /// <summary>Anything that identifies the person or the place: everything except the colour profile.</summary>
        public bool HasHidden => Groups.Any(g => g.Found && g.Id != ExifData.Colour);
        public bool HasLocation => Group(ExifData.Location)?.Found == true;
        public int TagTotal => Groups.Where(g => g.Found).Sum(g => g.Tags);
        public string Summary => !Supported ? Reason : (!HasHidden ? "no hidden data" : HasLocation ? "GPS · " + (TagTotal - Group(ExifData.Location).Tags) + " more" : TagTotal + " hidden items");
    }

    public sealed class CleanResult
    {
        public bool Ok;
        public string Source, Output, Error, Method;
        public int Removed;
        public List<string> RemovedGroups = new List<string>(), Kept = new List<string>();
        public bool Verified;
    }

    /// <summary>Reads and removes picture metadata. JPEG, PNG and WebP are edited at the byte level so the picture data is not
    /// re-compressed; TIFF is saved again losslessly. Nothing is uploaded, and coordinates only ever live in memory for this dialog.</summary>
    public static class ImagePrivacyEngine
    {
        public static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp", ".heic" };

        public static readonly (string Id, string Title, string Sub)[] GroupInfo =
        {
            (ExifData.Location, "Location", "GPS position, altitude, direction"),
            (ExifData.Camera, "Camera and lens", "Make, model, serial numbers, lens"),
            (ExifData.DateTimeGroup, "Date and time", "When the picture was taken or changed"),
            (ExifData.Author, "Author and notes", "Author, copyright, software, comments, XMP and IPTC"),
            (ExifData.Colour, "Colour profile", "Needed so colours look right; keep it"),
            (ExifData.Thumb, "Embedded thumbnail", "A small copy that can show the uncropped original"),
        };

        private static string Kind(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".jpg": case ".jpeg": return "jpeg";
                case ".png": return "png";
                case ".webp": return "webp";
                case ".tif": case ".tiff": return "tiff";
                case ".heic": return "heic";
                default: return "other";
            }
        }

        private static readonly byte[] ExifHead = { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0 };
        private static readonly byte[] XmpHead = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");
        private static readonly byte[] IccHead = Encoding.ASCII.GetBytes("ICC_PROFILE\0");

        private static bool StartsWith(byte[] b, int pos, byte[] head)
        {
            if (pos + head.Length > b.Length) return false;
            for (int i = 0; i < head.Length; i++) if (b[pos + i] != head[i]) return false;
            return true;
        }

        // ------------------------------------------------------------ reading

        public static PrivacyReport Scan(string path)
        {
            var rep = new PrivacyReport { Path = path, Kind = Kind(path) };
            try { rep.Bytes = new FileInfo(path).Length; } catch { }
            ExifData exif = null; bool icc = false; int notes = 0;
            try
            {
                var b = File.ReadAllBytes(path);
                switch (rep.Kind)
                {
                    case "jpeg": ScanJpeg(b, out exif, out icc, out notes); break;
                    case "png": ScanPng(b, out exif, out icc, out notes, out var dateTags); notes += 0; if (dateTags > 0) rep.Groups.Add(new PrivacyGroup { Id = "__png_time", Tags = dateTags }); break;
                    case "webp": ScanWebp(b, out exif, out icc, out notes); break;
                    case "tiff": exif = ExifData.Parse(b); break;
                    case "heic":
                        rep.Supported = false; rep.Reason = "HEIC pictures cannot be read or cleaned in this version";
                        return rep;
                    default: rep.Supported = false; rep.Reason = "This picture type is not supported"; return rep;
                }
            }
            catch (Exception ex) { rep.Supported = false; rep.Reason = "Could not read this picture: " + ex.Message; return rep; }

            int pngTime = rep.Groups.FirstOrDefault(g => g.Id == "__png_time")?.Tags ?? 0;
            rep.Groups.RemoveAll(g => g.Id == "__png_time");
            if (exif != null)
            {
                var o = exif.Short(exif.Ifd0, ExifData.TagOrientation);
                if (o.HasValue) { rep.HasOrientation = true; rep.Orientation = o.Value; }
                if (exif.TryGps(out var la, out var lo, out var alt, out var acc)) { rep.Lat = la; rep.Lon = lo; rep.Altitude = alt; rep.Accuracy = acc; }
            }
            foreach (var (id, title, sub) in GroupInfo)
            {
                var g = new PrivacyGroup { Id = id, Title = title };
                switch (id)
                {
                    case ExifData.Location:
                        g.Tags = exif?.CountIn(id) ?? 0; g.Found = g.Tags > 0;
                        g.Detail = rep.Lat.HasValue ? ExifData.FormatCoordinate(rep.Lat.Value, true) + ", " + ExifData.FormatCoordinate(rep.Lon.Value, false) : sub;
                        break;
                    case ExifData.Camera:
                        g.Tags = exif?.CountIn(id) ?? 0; g.Found = g.Tags > 0;
                        if (exif != null && g.Found)
                        {
                            var make = exif.Ascii(exif.Ifd0, 0x010F); var model = exif.Ascii(exif.Ifd0, 0x0110);
                            var serial = exif.Ascii(exif.Exif, 0xA431); var lens = exif.Ascii(exif.Exif, 0xA434);
                            rep.Serial = serial;
                            var parts = new List<string>();
                            var cam = string.Join(" ", new[] { make, model }.Where(s => !string.IsNullOrEmpty(s)));
                            if (cam.Length > 0) parts.Add(cam);
                            if (!string.IsNullOrEmpty(serial)) parts.Add("serial " + serial);
                            if (!string.IsNullOrEmpty(lens)) parts.Add("lens " + lens);
                            g.Detail = parts.Count > 0 ? string.Join(", ", parts) : sub;
                        }
                        else g.Detail = sub;
                        break;
                    case ExifData.DateTimeGroup:
                        g.Tags = (exif?.CountIn(id) ?? 0) + pngTime; g.Found = g.Tags > 0;
                        var taken = exif?.Ascii(exif.Exif, 0x9003) ?? exif?.Ascii(exif.Ifd0, 0x0132);
                        g.Detail = g.Found ? (string.IsNullOrEmpty(taken) ? "A date is stored in the file" : PrettyDate(taken)) : sub;
                        break;
                    case ExifData.Author:
                        g.Tags = (exif?.CountIn(id) ?? 0) + notes; g.Found = g.Tags > 0;
                        g.Detail = g.Found ? "Author, software or comments are stored in the file" : sub;
                        break;
                    case ExifData.Colour:
                        g.Tags = icc ? 1 : 0; g.Found = icc; g.Detail = sub;
                        break;
                    case ExifData.Thumb:
                        g.Tags = exif?.CountIn(id) ?? 0; g.Found = g.Tags > 0; g.Detail = sub;
                        break;
                }
                rep.Groups.Add(g);
            }
            return rep;
        }

        private static string PrettyDate(string exifDate)
        {
            if (DateTime.TryParseExact(exifDate, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.ToString("d MMM yyyy, HH:mm:ss", CultureInfo.InvariantCulture) + " (local)";
            return exifDate;
        }

        private static IEnumerable<(byte Marker, int Start, int Length)> JpegSegments(byte[] b, out int scanStart)
        {
            var list = new List<(byte, int, int)>();
            scanStart = b.Length;
            if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) throw new InvalidDataException("This file is not a JPEG picture.");
            int i = 2;
            while (i + 3 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                byte m = b[i + 1];
                if (m == 0xFF) { i++; continue; }
                if (m == 0xDA) { scanStart = i; break; }
                if (m == 0x01 || (m >= 0xD0 && m <= 0xD8)) { i += 2; continue; }
                int len = (b[i + 2] << 8) | b[i + 3];
                if (len < 2 || i + 2 + len > b.Length) throw new InvalidDataException("The JPEG segments are damaged.");
                list.Add((m, i, 2 + len));
                i += 2 + len;
            }
            return list;
        }

        private static void ScanJpeg(byte[] b, out ExifData exif, out bool icc, out int notes)
        {
            exif = null; icc = false; notes = 0;
            foreach (var (m, start, len) in JpegSegments(b, out _))
            {
                int data = start + 4;
                if (m == 0xE1 && StartsWith(b, data, ExifHead)) exif ??= ExifData.Parse(b, data + 6, len - 4 - 6);
                else if (m == 0xE1 && StartsWith(b, data, XmpHead)) notes++;
                else if (m == 0xE2 && StartsWith(b, data, IccHead)) icc = true;
                else if (m == 0xED || m == 0xFE) notes++;
            }
        }

        // ------------------------------------------------------------ PNG

        private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static IEnumerable<(string Type, int Start, int DataLen)> PngChunks(byte[] b)
        {
            if (b.Length < 8 || !StartsWith(b, 0, PngSig)) throw new InvalidDataException("This file is not a PNG picture.");
            int i = 8;
            while (i + 12 <= b.Length)
            {
                int len = (int)ExifData.U32(b, i, true);
                if (len < 0 || i + 12 + len > b.Length) throw new InvalidDataException("The PNG chunks are damaged.");
                yield return (Encoding.ASCII.GetString(b, i + 4, 4), i, len);
                i += 12 + len;
            }
        }

        private static void ScanPng(byte[] b, out ExifData exif, out bool icc, out int notes, out int dateTags)
        {
            exif = null; icc = false; notes = 0; dateTags = 0;
            foreach (var (type, start, len) in PngChunks(b))
            {
                switch (type)
                {
                    case "eXIf": exif ??= ExifData.Parse(b, start + 8, len); break;
                    case "iCCP": icc = true; break;
                    case "tIME": dateTags++; break;
                    case "tEXt": case "zTXt": case "iTXt":
                        var kw = Encoding.Latin1.GetString(b, start + 8, Math.Min(len, 79)).Split('\0')[0];
                        if (kw.Equals("Creation Time", StringComparison.OrdinalIgnoreCase)) dateTags++; else notes++;
                        break;
                }
            }
        }

        private static readonly uint[] CrcTable = BuildCrc();
        private static uint[] BuildCrc()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; t[n] = c; }
            return t;
        }
        internal static uint Crc(byte[] b, int start, int len)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = 0; i < len; i++) c = CrcTable[(c ^ b[start + i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        // ------------------------------------------------------------ WebP

        private static IEnumerable<(string Type, int Start, int DataLen)> WebpChunks(byte[] b)
        {
            if (b.Length < 20 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WEBP") throw new InvalidDataException("This file is not a WebP picture.");
            int i = 12;
            while (i + 8 <= b.Length)
            {
                int len = (int)ExifData.U32(b, i + 4, false);
                if (len < 0 || i + 8 + len > b.Length) throw new InvalidDataException("The WebP chunks are damaged.");
                yield return (Encoding.ASCII.GetString(b, i, 4), i, len);
                i += 8 + len + (len & 1);
            }
        }

        private static void ScanWebp(byte[] b, out ExifData exif, out bool icc, out int notes)
        {
            exif = null; icc = false; notes = 0;
            foreach (var (type, start, len) in WebpChunks(b))
            {
                if (type == "EXIF") { int off = len >= 6 && StartsWith(b, start + 8, ExifHead) ? 6 : 0; exif ??= ExifData.Parse(b, start + 8 + off, len - off); }
                else if (type == "XMP ") notes++;
                else if (type == "ICCP") icc = true;
            }
        }

        // ------------------------------------------------------------ cleaning

        /// <summary>Writes a cleaned copy of src to dest. remove holds the group ids to drop; orientation is kept when keepOrientation is set.</summary>
        public static CleanResult Clean(string src, string dest, PrivacyReport rep, ISet<string> remove, bool keepOrientation = true)
        {
            var res = new CleanResult { Source = src, Output = dest };
            var part = dest + ".part";
            try
            {
                var b = File.ReadAllBytes(src);
                byte[] outBytes;
                switch (rep.Kind)
                {
                    case "jpeg": outBytes = CleanJpeg(b, remove, keepOrientation, res); res.Method = "Removed in the file, picture data not re-compressed"; break;
                    case "png": outBytes = CleanPng(b, remove, keepOrientation, res); res.Method = "Removed in the file, picture data not re-compressed"; break;
                    case "webp": outBytes = CleanWebp(b, remove, keepOrientation, res); res.Method = "Removed in the file, picture data not re-compressed"; break;
                    case "tiff": outBytes = CleanTiff(src, remove, keepOrientation, res, rep); res.Method = "Saved again without the metadata, lossless"; break;
                    default: res.Error = rep.Reason ?? "This picture type is not supported"; return res;
                }
                File.WriteAllBytes(part, outBytes);
                File.Move(part, dest);
                // The proof: read the new file back and check that every chosen group is really gone.
                var after = Scan(dest);
                res.Verified = after.Supported && remove.All(id => after.Group(id) == null || !after.Group(id).Found);
                foreach (var g in rep.Groups.Where(g => g.Found))
                    if (remove.Contains(g.Id)) res.RemovedGroups.Add(g.Title); else res.Kept.Add(g.Title);
                if (keepOrientation && rep.HasOrientation && rep.Orientation != 1) res.Kept.Add("Orientation");
                res.Ok = res.Verified;
                if (!res.Verified) res.Error = "The chosen data was still found in the new file, so it was not kept as a clean copy.";
                if (!res.Ok) { try { File.Delete(dest); } catch { } }
                return res;
            }
            catch (Exception ex) { res.Error = ex.Message; return res; }
            finally { try { if (File.Exists(part)) File.Delete(part); } catch { } }
        }

        private static ExifData Rebuild(ExifData exif, ISet<string> remove, bool keepOrientation, CleanResult res, out byte[] tiff)
        {
            tiff = null;
            if (exif == null) return null;
            res.Removed += remove.Sum(id => exif.CountIn(id));
            var kept = exif.Without(remove);
            // keep only what cannot identify anyone: layout tags, exposure settings and the orientation
            if (!keepOrientation) { kept.Ifd0.RemoveAll(e => e.Tag == ExifData.TagOrientation); }
            tiff = kept.ToTiff();
            return kept;
        }

        private static byte[] CleanJpeg(byte[] b, ISet<string> remove, bool keepOrientation, CleanResult res)
        {
            var segs = JpegSegments(b, out int scan).ToList();
            ExifData exif = null;
            foreach (var (m, start, len) in segs) if (m == 0xE1 && StartsWith(b, start + 4, ExifHead)) { exif = ExifData.Parse(b, start + 10, len - 10); break; }
            Rebuild(exif, remove, keepOrientation, res, out var tiff);
            using var ms = new MemoryStream();
            ms.WriteByte(0xFF); ms.WriteByte(0xD8);
            bool wrote = false;
            void WriteExif()
            {
                if (wrote) return; wrote = true;
                if (tiff == null || tiff.Length + 8 > 65535) return;
                int len = 2 + 6 + tiff.Length;
                ms.WriteByte(0xFF); ms.WriteByte(0xE1); ms.WriteByte((byte)(len >> 8)); ms.WriteByte((byte)len);
                ms.Write(ExifHead); ms.Write(tiff);
            }
            // a JFIF APP0 must come first; the rebuilt Exif follows it
            foreach (var (m, start, len) in segs)
            {
                int data = start + 4;
                bool isExif = m == 0xE1 && StartsWith(b, data, ExifHead);
                bool isXmp = m == 0xE1 && StartsWith(b, data, XmpHead);
                bool isIcc = m == 0xE2 && StartsWith(b, data, IccHead);
                bool isNote = m == 0xED || m == 0xFE;
                if (isExif) { WriteExif(); continue; }
                if (isXmp || isNote) { if (remove.Contains(ExifData.Author)) { res.Removed++; continue; } }
                if (isIcc && remove.Contains(ExifData.Colour)) { res.Removed++; continue; }
                ms.Write(b, start, len);
            }
            if (!wrote) WriteExif();
            ms.Write(b, scan, b.Length - scan);
            return ms.ToArray();
        }

        private static byte[] CleanPng(byte[] b, ISet<string> remove, bool keepOrientation, CleanResult res)
        {
            using var ms = new MemoryStream();
            ms.Write(PngSig);
            ExifData exif = null;
            foreach (var (type, start, len) in PngChunks(b)) if (type == "eXIf") { exif = ExifData.Parse(b, start + 8, len); break; }
            Rebuild(exif, remove, keepOrientation, res, out var tiff);
            bool exifWritten = false;
            foreach (var (type, start, len) in PngChunks(b))
            {
                bool drop = false;
                switch (type)
                {
                    case "eXIf":
                        if (tiff != null && !exifWritten) { WriteChunk(ms, "eXIf", tiff); }
                        exifWritten = true; continue;
                    case "iCCP": drop = remove.Contains(ExifData.Colour); break;
                    case "tIME": drop = remove.Contains(ExifData.DateTimeGroup); break;
                    case "tEXt": case "zTXt": case "iTXt":
                        var kw = Encoding.Latin1.GetString(b, start + 8, Math.Min(len, 79)).Split('\0')[0];
                        drop = kw.Equals("Creation Time", StringComparison.OrdinalIgnoreCase) ? remove.Contains(ExifData.DateTimeGroup) : remove.Contains(ExifData.Author);
                        break;
                }
                if (drop) { res.Removed++; continue; }
                ms.Write(b, start, 12 + len);
            }
            return ms.ToArray();
        }

        private static void WriteChunk(Stream s, string type, byte[] data)
        {
            var head = new byte[8];
            ExifData.Put32(head, 0, (uint)data.Length, true);
            Encoding.ASCII.GetBytes(type).CopyTo(head, 4);
            s.Write(head);
            s.Write(data);
            var crcBuf = new byte[4 + data.Length];
            Array.Copy(head, 4, crcBuf, 0, 4); Array.Copy(data, 0, crcBuf, 4, data.Length);
            var c = new byte[4]; ExifData.Put32(c, 0, Crc(crcBuf, 0, crcBuf.Length), true);
            s.Write(c);
        }

        private static byte[] CleanWebp(byte[] b, ISet<string> remove, bool keepOrientation, CleanResult res)
        {
            ExifData exif = null; bool hadExif = false;
            foreach (var (type, start, len) in WebpChunks(b)) if (type == "EXIF") { hadExif = true; int off = len >= 6 && StartsWith(b, start + 8, ExifHead) ? 6 : 0; exif = ExifData.Parse(b, start + 8 + off, len - off); break; }
            Rebuild(exif, remove, keepOrientation, res, out var tiff);
            using var body = new MemoryStream();
            int vp8xFlagsPos = -1; bool wroteExif = false;
            foreach (var (type, start, len) in WebpChunks(b))
            {
                if (type == "EXIF") { if (tiff != null && !wroteExif) { WriteWebpChunk(body, "EXIF", tiff); wroteExif = true; } continue; }
                if (type == "XMP " && remove.Contains(ExifData.Author)) { res.Removed++; continue; }
                if (type == "ICCP" && remove.Contains(ExifData.Colour)) { res.Removed++; continue; }
                if (type == "VP8X") vp8xFlagsPos = (int)body.Position + 8;
                body.Write(b, start, 8 + len + (len & 1));
            }
            if (tiff != null && !wroteExif && vp8xFlagsPos >= 0) { WriteWebpChunk(body, "EXIF", tiff); wroteExif = true; }
            var data = body.ToArray();
            if (vp8xFlagsPos >= 0)
            {
                byte f = data[vp8xFlagsPos];
                if (remove.Contains(ExifData.Author)) f &= 0xFB;                // XMP
                if (remove.Contains(ExifData.Colour)) f &= 0xDF;                // ICC
                if (wroteExif) f |= 0x08; else f &= 0xF7;                       // EXIF
                data[vp8xFlagsPos] = f;
            }
            var outBytes = new byte[12 + data.Length];
            Encoding.ASCII.GetBytes("RIFF").CopyTo(outBytes, 0);
            ExifData.Put32(outBytes, 4, (uint)(4 + data.Length), false);
            Encoding.ASCII.GetBytes("WEBP").CopyTo(outBytes, 8);
            Array.Copy(data, 0, outBytes, 12, data.Length);
            return outBytes;
        }

        private static void WriteWebpChunk(Stream s, string type, byte[] data)
        {
            var head = new byte[8];
            Encoding.ASCII.GetBytes(type).CopyTo(head, 0);
            ExifData.Put32(head, 4, (uint)data.Length, false);
            s.Write(head); s.Write(data);
            if ((data.Length & 1) != 0) s.WriteByte(0);
        }

        private static byte[] CleanTiff(string src, ISet<string> remove, bool keepOrientation, CleanResult res, PrivacyReport rep)
        {
            var dec = BitmapDecoder.Create(new Uri(src), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (dec.Frames.Count > 1) throw new InvalidDataException("This TIFF has several pages. Only one would be kept, so it was not changed.");
            res.Removed += rep.Groups.Where(g => g.Found && remove.Contains(g.Id)).Sum(g => g.Tags);
            var enc = new TiffBitmapEncoder { Compression = TiffCompressOption.Zip };
            var frame = BitmapFrame.Create(dec.Frames[0]);
            BitmapMetadata meta = null;
            if (keepOrientation && rep.HasOrientation)
            {
                try { meta = new BitmapMetadata("tiff"); meta.SetQuery("/ifd/{ushort=274}", (ushort)rep.Orientation); } catch { meta = null; }
            }
            try { enc.Frames.Add(meta != null ? BitmapFrame.Create(dec.Frames[0], null, meta, null) : frame); }
            catch { enc = new TiffBitmapEncoder { Compression = TiffCompressOption.Zip }; enc.Frames.Add(frame); }
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }
    }
}
