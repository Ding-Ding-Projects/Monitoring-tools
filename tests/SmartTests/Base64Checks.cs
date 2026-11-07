using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MonitoringTools.Core;

namespace SmartTests
{
    public static partial class Program
    {
        /// <summary>The Base64 drop: written beside the file, never overwriting, and proved by a round trip.</summary>
        private static void Base64Checks()
        {
            var dir = Path.Combine(Path.GetTempPath(), "mt-b64-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            try
            {
                var rnd = new Random(7);
                var data = new byte[3 * 64 * 1024 * 2 + 17];   // more than two read chunks, and not a multiple of 3
                rnd.NextBytes(data);
                var src = Path.Combine(dir, "report.pdf");
                File.WriteAllBytes(src, data);

                var enc = Base64Files.Encode(src);
                Check("base64 encode succeeds", enc.Ok && enc.Verified, enc.Error ?? "");
                Check("base64 text goes beside the file as name.ext.base64.txt", enc.Output == src + ".base64.txt", enc.Output ?? "");
                Check("base64 text is the standard encoding", File.ReadAllText(enc.Output) == Convert.ToBase64String(data));
                Check("base64 leaves no temporary file", !Directory.GetFiles(dir, "*.partial").Any());

                var again = Base64Files.Encode(src);
                Check("base64 never overwrites: a second encode is numbered", again.Ok && Path.GetFileName(again.Output) == "report.pdf.base64 (1).txt", again.Output ?? again.Error);
                Check("base64 first output is untouched by the second", File.ReadAllText(enc.Output) == Convert.ToBase64String(data));

                var dec = Base64Files.Decode(enc.Output);
                Check("base64 decode succeeds", dec.Ok && dec.Verified, dec.Error ?? "");
                Check("base64 decode never overwrites the original", Path.GetFileName(dec.Output) == "report (1).pdf", dec.Output ?? "");
                Check("base64 decode gives back the same bytes", File.ReadAllBytes(dec.Output).SequenceEqual(data));

                // URL-safe alphabet, line breaks, missing padding and a data: URL prefix all decode
                var small = Encoding.UTF8.GetBytes("Hello, 世界! ~~~???>>>");
                var urlSafe = Convert.ToBase64String(small).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                var wrapped = string.Join("\r\n", Enumerable.Range(0, (urlSafe.Length + 9) / 10).Select(i => urlSafe.Substring(i * 10, Math.Min(10, urlSafe.Length - i * 10))));
                var loose = Path.Combine(dir, "note.txt.base64.txt");
                File.WriteAllText(loose, "data:text/plain;base64," + wrapped + "\n");
                var looseDec = Base64Files.Decode(loose);
                Check("base64 lenient decode of URL-safe, wrapped, unpadded data URL", looseDec.Ok && File.ReadAllBytes(looseDec.Output).SequenceEqual(small), looseDec.Error ?? "");
                Check("base64 decode restores the extension", Path.GetFileName(looseDec.Output) == "note.txt", looseDec.Output ?? "");

                // a stray character stops with its exact byte offset, and nothing is written
                var broken = Path.Combine(dir, "broken.bin.base64.txt");
                File.WriteAllText(broken, "QUJD*REVG");
                var brokenDec = Base64Files.Decode(broken);
                Check("base64 rejects a stray character", !brokenDec.Ok && brokenDec.Error != null && brokenDec.Error.Contains("Byte 4"), brokenDec.Error ?? "");
                Check("base64 writes nothing for bad text", !File.Exists(Path.Combine(dir, "broken.bin")) && !Directory.GetFiles(dir, "*.partial").Any());

                // cancelling leaves no new file and the source untouched
                using (var cts = new CancellationTokenSource())
                {
                    cts.Cancel();
                    var before = Directory.GetFiles(dir).Length;
                    var cancelled = Base64Files.Encode(src, cts.Token);
                    Check("base64 cancel reports cancelled", cancelled.Cancelled && !cancelled.Ok);
                    Check("base64 cancel leaves no new file", Directory.GetFiles(dir).Length == before);
                    Check("base64 cancel leaves the source untouched", File.ReadAllBytes(src).SequenceEqual(data));
                }

                Check("base64 knows its text files", Base64Files.IsEncoded("a.png.base64.txt") && Base64Files.IsEncoded("a.b64") && !Base64Files.IsEncoded("a.txt"));
                Check("base64 decoded name drops only the ending", Base64Files.DecodedName(@"C:\x\photo.jpg.base64.txt") == @"C:\x\photo.jpg");
                Check("base64 sniffs a PDF", Base64Files.Sniff(WriteBytes(dir, "sniff", Encoding.ASCII.GetBytes("%PDF-1.7\n"))) == "PDF document");
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        private static string WriteBytes(string dir, string name, byte[] bytes)
        {
            var p = Path.Combine(dir, name);
            File.WriteAllBytes(p, bytes);
            return p;
        }
    }
}
