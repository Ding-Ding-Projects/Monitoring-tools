using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    public sealed class OpResult
    {
        public string Source;
        public string Output;
        public bool Ok;
        public string Error;
    }

    public static class FileOps
    {
        // -------------------------------------------------------------- default app

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int AssocQueryString(int flags, int str, string assoc, string extra, StringBuilder outBuf, ref uint outLen);
        private const int ASSOCSTR_FRIENDLYAPPNAME = 4;

        /// <summary>The friendly name of the app that opens this file type, such as "Photos" or "VLC media player".</summary>
        public static string DefaultAppName(string path)
        {
            try
            {
                var ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext) || Directory.Exists(path)) return null;
                uint len = 512;
                var sb = new StringBuilder((int)len);
                if (AssocQueryString(0, ASSOCSTR_FRIENDLYAPPNAME, ext, null, sb, ref len) != 0) return null;
                var name = sb.ToString().Trim();
                return name.Length == 0 || name.Length > 40 ? null : name;
            }
            catch { return null; }
        }

        public static void OpenWithPicker(string path)
        {
            try { Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {path}") { UseShellExecute = false }); }
            catch (Exception ex) { Log.Warn("Open with failed: " + ex.Message); }
        }

        // -------------------------------------------------------------- move, copy, delete

        public static string Unique(string target)
        {
            if (!File.Exists(target) && !Directory.Exists(target)) return target;
            var dir = Path.GetDirectoryName(target); var name = Path.GetFileNameWithoutExtension(target); var ext = Path.GetExtension(target);
            int n = 1; string p;
            do { p = Path.Combine(dir, $"{name} ({n++}){ext}"); } while (File.Exists(p) || Directory.Exists(p));
            return p;
        }

        public static Task<List<OpResult>> MoveOrCopyAsync(IEnumerable<string> files, string folder, bool move, Action<int> progress = null)
        {
            var list = files.ToList();
            return Task.Run(() =>
            {
                var res = new List<OpResult>();
                int i = 0;
                foreach (var src in list)
                {
                    var r = new OpResult { Source = src };
                    try
                    {
                        var dest = Unique(Path.Combine(folder, Path.GetFileName(src.TrimEnd('\\'))));
                        if (Directory.Exists(src))
                        {
                            if (move)
                            {
                                if (string.Equals(Path.GetPathRoot(src), Path.GetPathRoot(dest), StringComparison.OrdinalIgnoreCase)) Directory.Move(src, dest);
                                else { CopyDir(src, dest); Directory.Delete(src, true); }
                            }
                            else CopyDir(src, dest);
                        }
                        else if (move) File.Move(src, dest);
                        else File.Copy(src, dest);
                        r.Output = dest; r.Ok = true;
                    }
                    catch (Exception ex) { r.Error = ex.Message; }
                    res.Add(r);
                    progress?.Invoke(++i);
                }
                return res;
            });
        }

        private static void CopyDir(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)));
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dest, Path.GetFileName(d)));
        }

        /// <summary>Sends files and folders to the Recycle Bin, never a hard delete.</summary>
        public static Task<List<OpResult>> RecycleAsync(IEnumerable<string> files)
        {
            var list = files.ToList();
            return Task.Run(() =>
            {
                var res = new List<OpResult>();
                foreach (var src in list)
                {
                    var r = new OpResult { Source = src };
                    try
                    {
                        if (Directory.Exists(src))
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(src, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        else
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(src, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        r.Ok = true;
                    }
                    catch (Exception ex) { r.Error = ex.Message; }
                    res.Add(r);
                }
                return res;
            });
        }

        // -------------------------------------------------------------- base64

        private static readonly Regex B64 = new Regex(@"^[A-Za-z0-9+/\r\n]+={0,2}$|^[A-Za-z0-9_\-\r\n]+$", RegexOptions.Compiled);

        /// <summary>Plausible base64: long enough, right alphabet, decodes cleanly. Data URIs count too.</summary>
        public static bool LooksBase64(string text)
        {
            var t = (text ?? "").Trim();
            if (t.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && t.Contains(";base64,")) return true;
            if (t.Length < 20 || t.Contains(' ')) return false;
            if (!B64.IsMatch(t)) return false;
            // ordinary words also match the alphabet; real base64 mixes cases and digits or symbols
            bool mixed = t.Any(char.IsUpper) && t.Any(char.IsLower) && (t.Any(char.IsDigit) || t.IndexOfAny(new[] { '+', '/', '=' }) >= 0);
            if (!mixed) return false;
            return TryDecode(t, out _);
        }

        public static bool TryDecode(string text, out byte[] bytes)
        {
            bytes = null;
            var t = (text ?? "").Trim();
            var comma = t.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
            if (t.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0) t = t[(comma + 8)..];
            t = Regex.Replace(t, @"\s+", "").Replace('-', '+').Replace('_', '/');
            while (t.Length % 4 != 0) t += "=";
            try { bytes = Convert.FromBase64String(t); return bytes.Length > 0; } catch { return false; }
        }

        public static string EncodeText(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? ""));

        public static string DecodeToText(string text)
        {
            if (!TryDecode(text, out var b)) return null;
            var s = new UTF8Encoding(false, true);
            try { return s.GetString(b); } catch { return null; } // not text: caller saves as a file instead
        }

        public static Task<List<OpResult>> Base64EncodeFilesAsync(IEnumerable<string> files)
        {
            var list = files.Where(File.Exists).ToList();
            return Task.Run(() => list.Select(src =>
            {
                var r = new OpResult { Source = src };
                try
                {
                    var outPath = Unique(src + ".b64");
                    using (var input = File.OpenRead(src))
                    using (var output = File.Create(outPath))
                    using (var cs = new CryptoStream(output, new ToBase64Transform(), CryptoStreamMode.Write))
                        input.CopyTo(cs);
                    r.Output = outPath; r.Ok = true;
                }
                catch (Exception ex) { r.Error = ex.Message; }
                return r;
            }).ToList());
        }

        public static Task<List<OpResult>> Base64DecodeFilesAsync(IEnumerable<string> files)
        {
            var list = files.Where(File.Exists).ToList();
            return Task.Run(() => list.Select(src =>
            {
                var r = new OpResult { Source = src };
                try
                {
                    var target = src.EndsWith(".b64", StringComparison.OrdinalIgnoreCase) || src.EndsWith(".base64", StringComparison.OrdinalIgnoreCase)
                        ? src[..src.LastIndexOf('.')]
                        : src + ".bin";
                    var outPath = Unique(target);
                    var text = File.ReadAllText(src);
                    if (!TryDecode(text, out var bytes)) throw new InvalidDataException("Not valid base64.");
                    File.WriteAllBytes(outPath, bytes);
                    r.Output = outPath; r.Ok = true;
                }
                catch (Exception ex) { r.Error = ex.Message; }
                return r;
            }).ToList());
        }

        // -------------------------------------------------------------- encryption

        // File format (.mtenc): "MTENC" 0x01 | iterations u32 | salt[16] | nonce[12] | chunk u32
        // then chunks: final u8 | length u32 | ciphertext | tag[16]
        // AES-256-GCM per chunk, key from PBKDF2-SHA256. Each chunk's nonce is the base nonce
        // XOR its index; the header, index and final flag are authenticated, so chunks cannot be
        // reordered, dropped or truncated without the tag check failing.
        private static readonly byte[] Magic = { (byte)'M', (byte)'T', (byte)'E', (byte)'N', (byte)'C', 1 };
        private const int Iterations = 600_000;
        private const int Chunk = 1 << 20;
        public const string EncryptedExt = ".mtenc";

        public static bool IsEncrypted(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                using var f = File.OpenRead(path);
                var b = new byte[6];
                return f.Read(b, 0, 6) == 6 && b.SequenceEqual(Magic);
            }
            catch { return false; }
        }

        public static Task<List<OpResult>> EncryptAsync(IEnumerable<string> files, string password, bool deleteOriginals, Action<int> progress = null)
        {
            var list = files.Where(File.Exists).ToList();
            return Task.Run(() =>
            {
                var res = new List<OpResult>();
                int i = 0;
                foreach (var src in list)
                {
                    var r = new OpResult { Source = src };
                    var outPath = Unique(src + EncryptedExt);
                    try
                    {
                        var salt = RandomNumberGenerator.GetBytes(16);
                        var nonce = RandomNumberGenerator.GetBytes(12);
                        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);

                        var header = new byte[6 + 4 + 16 + 12 + 4];
                        Magic.CopyTo(header, 0);
                        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(6), Iterations);
                        salt.CopyTo(header, 10);
                        nonce.CopyTo(header, 26);
                        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(38), Chunk);

                        using (var aes = new AesGcm(key, 16))
                        using (var input = File.OpenRead(src))
                        using (var output = File.Create(outPath))
                        {
                            output.Write(header);
                            var buf = new byte[Chunk];
                            var cipher = new byte[Chunk];
                            var tag = new byte[16];
                            var lenBuf = new byte[4];
                            long index = 0;
                            while (true)
                            {
                                int n = ReadFull(input, buf);
                                bool final = input.Position >= input.Length;
                                var aad = Aad(header, index, final);
                                aes.Encrypt(ChunkNonce(nonce, index), buf.AsSpan(0, n), cipher.AsSpan(0, n), tag, aad);
                                output.WriteByte(final ? (byte)1 : (byte)0);
                                BinaryPrimitives.WriteUInt32LittleEndian(lenBuf, (uint)n);
                                output.Write(lenBuf, 0, 4);
                                output.Write(cipher, 0, n);
                                output.Write(tag);
                                index++;
                                if (final) break;
                            }
                        }
                        CryptographicOperations.ZeroMemory(key);
                        if (deleteOriginals) File.Delete(src);
                        r.Output = outPath; r.Ok = true;
                    }
                    catch (Exception ex)
                    {
                        r.Error = ex.Message;
                        try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
                    }
                    res.Add(r);
                    progress?.Invoke(++i);
                }
                return res;
            });
        }

        public static Task<List<OpResult>> DecryptAsync(IEnumerable<string> files, string password, Action<int> progress = null)
        {
            var list = files.Where(File.Exists).ToList();
            return Task.Run(() =>
            {
                var res = new List<OpResult>();
                int i = 0;
                foreach (var src in list)
                {
                    var r = new OpResult { Source = src };
                    var target = src.EndsWith(EncryptedExt, StringComparison.OrdinalIgnoreCase) ? src[..^EncryptedExt.Length] : src + ".decrypted";
                    var outPath = Unique(target);
                    try
                    {
                        using var input = File.OpenRead(src);
                        var header = new byte[42];
                        if (ReadFull(input, header) != 42 || !header.AsSpan(0, 6).SequenceEqual(Magic)) throw new InvalidDataException("Not a Monitoring Tools encrypted file.");
                        int iters = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(6));
                        var salt = header.AsSpan(10, 16).ToArray();
                        var nonce = header.AsSpan(26, 12).ToArray();
                        int chunk = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(38));
                        if (chunk <= 0 || chunk > 64 << 20 || iters < 1000) throw new InvalidDataException("Damaged header.");
                        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iters, HashAlgorithmName.SHA256, 32);

                        using (var aes = new AesGcm(key, 16))
                        using (var output = File.Create(outPath))
                        {
                            var cipher = new byte[chunk];
                            var plain = new byte[chunk];
                            var tag = new byte[16];
                            var lenBuf = new byte[4];
                            long index = 0;
                            bool sawFinal = false;
                            while (!sawFinal)
                            {
                                int flag = input.ReadByte();
                                if (flag < 0) throw new InvalidDataException("The file is truncated.");
                                bool final = flag == 1;
                                if (ReadFull(input, lenBuf) != 4) throw new InvalidDataException("The file is truncated.");
                                int n = (int)BinaryPrimitives.ReadUInt32LittleEndian(lenBuf);
                                if (n < 0 || n > chunk) throw new InvalidDataException("Damaged chunk.");
                                if (ReadFull(input, cipher.AsSpan(0, n)) != n || ReadFull(input, tag) != 16) throw new InvalidDataException("The file is truncated.");
                                aes.Decrypt(ChunkNonce(nonce, index), cipher.AsSpan(0, n), tag, plain.AsSpan(0, n), Aad(header, index, final));
                                output.Write(plain, 0, n);
                                index++;
                                sawFinal = final;
                            }
                        }
                        CryptographicOperations.ZeroMemory(key);
                        r.Output = outPath; r.Ok = true;
                    }
                    catch (Exception ex)
                    {
                        r.Error = ex is AuthenticationTagMismatchException || ex is CryptographicException ? "Wrong password, or the file was changed." : ex.Message;
                        try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
                    }
                    res.Add(r);
                    progress?.Invoke(++i);
                }
                return res;
            });
        }

        private static byte[] ChunkNonce(byte[] baseNonce, long index)
        {
            var n = (byte[])baseNonce.Clone();
            Span<byte> idx = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(idx, index);
            for (int i = 0; i < 8; i++) n[4 + i] ^= idx[i];
            return n;
        }

        private static byte[] Aad(byte[] header, long index, bool final)
        {
            var a = new byte[header.Length + 9];
            header.CopyTo(a, 0);
            BinaryPrimitives.WriteInt64LittleEndian(a.AsSpan(header.Length), index);
            a[^1] = final ? (byte)1 : (byte)0;
            return a;
        }

        private static int ReadFull(Stream s, Span<byte> buf)
        {
            int total = 0;
            while (total < buf.Length)
            {
                int n = s.Read(buf[total..]);
                if (n == 0) break;
                total += n;
            }
            return total;
        }

        private static int ReadFull(Stream s, byte[] buf) => ReadFull(s, buf.AsSpan());

        /// <summary>0 to 4, for the strength meter.</summary>
        public static int PasswordScore(string p)
        {
            if (string.IsNullOrEmpty(p)) return 0;
            int classes = (Regex.IsMatch(p, "[a-z]") ? 1 : 0) + (Regex.IsMatch(p, "[A-Z]") ? 1 : 0) + (Regex.IsMatch(p, "[0-9]") ? 1 : 0) + (Regex.IsMatch(p, "[^A-Za-z0-9]") ? 1 : 0);
            if (p.Length < 8) return 1;
            if (p.Length >= 16 && classes >= 2) return 4;
            if (p.Length >= 12 && classes >= 3) return 4;
            if (p.Length >= 10 && classes >= 2) return 3;
            return 2;
        }
    }
}
