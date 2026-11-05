using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Builds small RAR archives byte by byte for the self-test and the captures, because no RAR can be created without WinRAR.
    /// Every file is stored (no compression), which both formats document fully:
    ///   RAR 4.x: marker block 52 61 72 21 1A 07 00, archive header (type 0x73), one file header (type 0x74, method 0x30 = store)
    ///   per file followed by its bytes, and the end block (type 0x7B). Each header CRC is the low 16 bits of the CRC32 of the
    ///   header from the type byte on; the file CRC is the CRC32 of the data. Paths use backslashes.
    ///   RAR 5.0: signature 52 61 72 21 1A 07 01 00, then headers of the form CRC32, size (vint), type (vint), flags (vint), ...:
    ///   main archive header (type 1), one file header (type 2) per file followed by its data, and the end header (type 5).
    ///   Paths use forward slashes. A file can be encrypted with AES-256 in CBC mode under a key from PBKDF2-HMAC-SHA256,
    ///   described by a file encryption record (type 1) in the header's extra area, including the password check value.
    /// </summary>
    internal static class RarFixtures
    {
        public static byte[] Rar4(params (string Name, byte[] Data)[] files)
        {
            var o = new MemoryStream();
            o.Write(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 });
            Rar4Block(o, 0x73, 0, new byte[6]);
            foreach (var (name, data) in files)
            {
                var nb = Encoding.ASCII.GetBytes(name.Replace('/', '\\'));
                var b = new MemoryStream();
                var w = new BinaryWriter(b);
                w.Write((uint)data.Length);           // packed size
                w.Write((uint)data.Length);           // unpacked size
                w.Write((byte)2);                     // host OS: Win32
                w.Write(Crc32(data));                 // file CRC
                w.Write(0x5A210000u | (1u << 5));     // DOS date and time: 2025-01-01 00:01
                w.Write((byte)29);                    // version needed to extract
                w.Write((byte)0x30);                  // method: store
                w.Write((ushort)nb.Length);
                w.Write(0x20u);                       // attributes: archive
                w.Write(nb);
                Rar4Block(o, 0x74, 0x8000, b.ToArray());   // 0x8000: the packed size follows the header
                o.Write(data);
            }
            Rar4Block(o, 0x7B, 0x4000, Array.Empty<byte>());
            return o.ToArray();
        }

        private static void Rar4Block(Stream o, byte type, ushort flags, byte[] body)
        {
            var tail = new byte[5 + body.Length];
            tail[0] = type;
            BitConverter.GetBytes(flags).CopyTo(tail, 1);
            BitConverter.GetBytes((ushort)(7 + body.Length)).CopyTo(tail, 3);
            body.CopyTo(tail, 5);
            o.Write(BitConverter.GetBytes((ushort)(Crc32(tail) & 0xFFFF)));
            o.Write(tail);
        }

        public static byte[] Rar5(params (string Name, byte[] Data)[] files) => Rar5(null, files);

        /// <summary>A RAR 5.0 archive; with a password, every file's data is encrypted (the names stay visible).</summary>
        public static byte[] Rar5(string password, params (string Name, byte[] Data)[] files) => Rar5(password, false, files);

        /// <summary>
        /// A RAR 5.0 archive; with a password, every file's data is encrypted. With <paramref name="encryptNames"/> the headers are
        /// encrypted too (an archive encryption header, type 4, then each header behind its own IV), so not even the names can be
        /// listed without the password.
        /// </summary>
        public static byte[] Rar5(string password, bool encryptNames, params (string Name, byte[] Data)[] files)
        {
            var o = new MemoryStream();
            o.Write(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 });
            byte[] headerKey = null;
            if (password != null && encryptNames)
            {
                var salt = new byte[16];
                for (int i = 0; i < 16; i++) salt[i] = (byte)(0x40 + i);
                var (key, check) = Derive(password, salt);
                var h = new MemoryStream();
                h.Write(Vint(0)); h.Write(Vint(1)); h.WriteByte(Lg2Count); h.Write(salt); h.Write(check);   // version 0, flags: password check present
                Rar5Header(o, 4, h.ToArray(), null, null);
                headerKey = key;
            }
            int ivSeed = 0;
            void Header(ulong type, byte[] fields, byte[] extra, ulong? dataSize)
            {
                if (headerKey == null) { Rar5Header(o, type, fields, extra, dataSize); return; }
                var plain = new MemoryStream();
                Rar5Header(plain, type, fields, extra, dataSize);
                var p = plain.ToArray();
                var padded = new byte[(p.Length + 15) / 16 * 16];
                p.CopyTo(padded, 0);
                var iv = new byte[16];
                for (int i = 0; i < 16; i++) iv[i] = (byte)(0xC0 + i + ivSeed);
                ivSeed++;
                using var aes = Aes.Create();
                aes.Key = headerKey;
                o.Write(iv);
                o.Write(aes.EncryptCbc(padded, iv, PaddingMode.None));
            }
            Header(1, Vint(0), null, null);
            int n = 0;
            foreach (var (name, data) in files)
            {
                var nb = Encoding.UTF8.GetBytes(name.Replace('\\', '/'));
                byte[] extra = null, payload = data;
                if (password != null)
                {
                    var salt = new byte[16]; var iv = new byte[16];
                    for (int i = 0; i < 16; i++) { salt[i] = (byte)(i + 16 * n); iv[i] = (byte)(0x80 + i + n); }
                    var (key, check) = Derive(password, salt);
                    var rec = new MemoryStream();
                    rec.Write(Vint(1)); rec.Write(Vint(0)); rec.Write(Vint(1));   // type: encryption, version 0, flags: password check present
                    rec.WriteByte(Lg2Count); rec.Write(salt); rec.Write(iv); rec.Write(check);
                    var r = rec.ToArray();
                    extra = Concat(Vint((ulong)r.Length), r);
                    var padded = new byte[(data.Length + 15) / 16 * 16];
                    data.CopyTo(padded, 0);
                    using var aes = Aes.Create();
                    aes.Key = key;
                    payload = aes.EncryptCbc(padded, iv, PaddingMode.None);
                }
                var f = new MemoryStream();
                f.Write(Vint(0x0004 | 0x0002));                // file flags: time and CRC32 present
                f.Write(Vint((ulong)data.Length));             // unpacked size
                f.Write(Vint(0x20));                           // attributes: archive
                f.Write(BitConverter.GetBytes(1735689660u));   // modified: 2025-01-01 00:01 UTC
                f.Write(BitConverter.GetBytes(Crc32(data)));
                f.Write(Vint(0));                              // compression: version 0, method 0 (store)
                f.Write(Vint(0));                              // host OS: Windows
                f.Write(Vint((ulong)nb.Length)); f.Write(nb);
                Header(2, f.ToArray(), extra, (ulong)payload.Length);
                o.Write(payload);
                n++;
            }
            Header(5, Vint(0), null, null);
            return o.ToArray();
        }

        /// <summary>log2 of the PBKDF2 iteration count (2^15), the value WinRAR uses.</summary>
        private const byte Lg2Count = 15;

        /// <summary>
        /// The AES-256 key (PBKDF2-HMAC-SHA256 after 2^15 iterations) and the 12-byte password check value: the result after
        /// 2^15 + 32 iterations folded to 8 bytes by XOR, followed by the first 4 bytes of its SHA-256.
        /// </summary>
        private static (byte[] Key, byte[] Check) Derive(string password, byte[] salt)
        {
            var pw = Encoding.UTF8.GetBytes(password);
            var key = Rfc2898DeriveBytes.Pbkdf2(pw, salt, 1 << Lg2Count, HashAlgorithmName.SHA256, 32);
            var v2 = Rfc2898DeriveBytes.Pbkdf2(pw, salt, (1 << Lg2Count) + 32, HashAlgorithmName.SHA256, 32);
            var fold = new byte[8];
            for (int i = 0; i < v2.Length; i++) fold[i % 8] ^= v2[i];
            return (key, Concat(fold, SHA256.HashData(fold)[..4]));
        }

        private static void Rar5Header(Stream o, ulong type, byte[] fields, byte[] extra, ulong? dataSize)
        {
            ulong flags = (extra != null ? 1u : 0u) | (dataSize != null ? 2u : 0u);
            var b = new MemoryStream();
            b.Write(Vint(type)); b.Write(Vint(flags));
            if (extra != null) b.Write(Vint((ulong)extra.Length));
            if (dataSize != null) b.Write(Vint(dataSize.Value));
            b.Write(fields);
            if (extra != null) b.Write(extra);
            var body = b.ToArray();
            var sized = Concat(Vint((ulong)body.Length), body);
            o.Write(BitConverter.GetBytes(Crc32(sized)));
            o.Write(sized);
        }

        private static byte[] Vint(ulong v)
        {
            var l = new List<byte>();
            do { byte x = (byte)(v & 0x7F); v >>= 7; l.Add(v != 0 ? (byte)(x | 0x80) : x); } while (v != 0);
            return l.ToArray();
        }

        private static byte[] Concat(byte[] a, byte[] b) { var r = new byte[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }

        private static readonly uint[] Table = MakeTable();
        private static uint[] MakeTable()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; t[i] = c; }
            return t;
        }

        public static uint Crc32(byte[] data)
        {
            uint c = 0xFFFFFFFF;
            foreach (var x in data) c = Table[(c ^ x) & 0xFF] ^ (c >> 8);
            return ~c;
        }
    }
}
