using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The files the data and security checks and captures work on, all created in a throwaway folder: a self-signed certificate made in code,
    /// a signed system file, an unsigned copy, a copy signed with a throwaway certificate, a copy whose content was changed after signing, and
    /// CSV, JSON and YAML samples (one with an error each) and text files in every encoding.
    /// </summary>
    internal sealed class DataSecFixtures
    {
        public string Dir;
        public string Hashed, Big;
        public string SystemSigned, Unsigned, SelfSigned, Broken;
        public string SigningProblem;                       // set when the throwaway signing could not run
        public string CertCer, CertPem, CertPfx, CertPemKey, CertExpired, CertSoon;
        public string PfxPassword = "test-password-1";
        public X509Certificate2 Cert;
        public string CsvFile, CsvSemi, CsvTab, CsvBig, CsvBad, JsonOk, JsonBad, JsonList, YamlOk, YamlBad, YamlDup, YamlList;
        public string EncUtf8, EncBom, EncU16le, EncU16be, EncU16leNoBom, EncAnsi, EncMixed, EncSjis, Png, NulText, EncCrlf;
        public byte[] HashedBytes;

        public static readonly string[] SystemSignedCandidates =
        {
            @"C:\Windows\System32\notepad.exe", @"C:\Windows\System32\cmd.exe", @"C:\Windows\System32\kernel32.dll", @"C:\Windows\explorer.exe",
        };

        public static DataSecFixtures Create(string dir, bool signing = true, bool big = false)
        {
            Directory.CreateDirectory(dir);
            var f = new DataSecFixtures { Dir = dir };
            f.Hashed = Path.Combine(dir, "ubuntu-sample.iso");
            f.HashedBytes = new byte[3 * 1024 * 1024 + 123];
            new Random(42).NextBytes(f.HashedBytes);
            File.WriteAllBytes(f.Hashed, f.HashedBytes);
            if (big)
            {
                f.Big = Path.Combine(dir, "big-sample.bin");
                using var fs = new FileStream(f.Big, FileMode.Create);
                var chunk = new byte[1 << 20]; new Random(7).NextBytes(chunk);
                for (int i = 0; i < 300; i++) fs.Write(chunk, 0, chunk.Length);
            }
            f.SystemSigned = SystemSignedCandidates.FirstOrDefault(File.Exists);
            f.MakeCerts();
            f.MakeData();
            f.MakeText();
            if (signing) f.MakeSigned();
            return f;
        }

        // ------------------------------------------------------------------ certificates

        private string LastKeyPem;

        private X509Certificate2 NewCert(string name, DateTime from, DateTime to, bool codeSigning = false, string[] dns = null)
        {
            using var rsa = RSA.Create(2048);
            LastKeyPem = rsa.ExportPkcs8PrivateKeyPem();
            var req = new CertificateRequest("CN=" + name + ", O=Example Org", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | (codeSigning ? 0 : X509KeyUsageFlags.KeyEncipherment), true));
            var eku = new OidCollection { new Oid(codeSigning ? "1.3.6.1.5.5.7.3.3" : "1.3.6.1.5.5.7.3.1"), };
            if (!codeSigning) eku.Add(new Oid("1.3.6.1.5.5.7.3.2"));
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));
            if (dns != null)
            {
                var san = new SubjectAlternativeNameBuilder();
                foreach (var d in dns) san.AddDnsName(d);
                req.CertificateExtensions.Add(san.Build());
            }
            using var made = req.CreateSelfSigned(new DateTimeOffset(from), new DateTimeOffset(to));
            return new X509Certificate2(made.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }

        private void MakeCerts()
        {
            Cert = NewCert("api.example.org", DateTime.Now.AddDays(-150), DateTime.Now.AddDays(214).AddHours(12), false, new[] { "api.example.org", "www.example.org" });
            var keyPem = LastKeyPem;
            CertCer = Path.Combine(Dir, "api.example.org.cer"); File.WriteAllBytes(CertCer, Cert.Export(X509ContentType.Cert));
            CertPem = Path.Combine(Dir, "api.example.org.pem"); File.WriteAllText(CertPem, Cert.ExportCertificatePem() + "\n");
            CertPfx = Path.Combine(Dir, "client-auth.pfx"); File.WriteAllBytes(CertPfx, Cert.Export(X509ContentType.Pfx, PfxPassword));
            File.WriteAllText(CertPemKey = Path.Combine(Dir, "with-key.pem"), Cert.ExportCertificatePem() + "\n" + keyPem + "\n");
            using var soon = NewCert("mail.example.org", DateTime.Now.AddDays(-353), DateTime.Now.AddDays(12).AddHours(6), false, new[] { "mail.example.org" });
            CertSoon = Path.Combine(Dir, "mail.example.org.cer"); File.WriteAllBytes(CertSoon, soon.Export(X509ContentType.Cert));
            using var old = NewCert("old.example.org", DateTime.Now.AddDays(-400), DateTime.Now.AddDays(-3).AddHours(-2), false, new[] { "old.example.org" });
            CertExpired = Path.Combine(Dir, "old.example.org.crt"); File.WriteAllBytes(CertExpired, old.Export(X509ContentType.Cert));
        }

        // ------------------------------------------------------------------ signing

        private void MakeSigned()
        {
            try
            {
                var src = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "find.exe");
                if (!File.Exists(src)) { SigningProblem = "find.exe is not available to copy"; return; }
                Unsigned = Path.Combine(Dir, "notes-tool.exe");
                File.Copy(src, Unsigned, true);
                using (var fs = new FileStream(Unsigned, FileMode.Append)) fs.Write(Encoding.ASCII.GetBytes("unsigned-test-tail"), 0, 18);   // no catalog knows this hash
                SelfSigned = Path.Combine(Dir, "legacy-tool.exe");
                File.Copy(Unsigned, SelfSigned, true);
                using var cert = NewCert("Northwind Traders Test", DateTime.Now.AddDays(-30), DateTime.Now.AddDays(300), true);
                var pfx = Path.Combine(Dir, "code-signing.pfx");
                File.WriteAllBytes(pfx, cert.Export(X509ContentType.Pfx, PfxPassword));
                var ps = "$ErrorActionPreference='Stop'; $c = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('" + pfx + "','" + PfxPassword
                    + "',[System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet); $r = Set-AuthenticodeSignature -FilePath '" + SelfSigned + "' -Certificate $c -HashAlgorithm SHA256; Write-Output $r.Status";
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + ps.Replace("\"", "\\\"") + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(120000)) { try { p.Kill(true); } catch { } SigningProblem = "signing timed out"; return; }
                File.Delete(pfx);
                var status = o.Result.Trim();
                if (p.ExitCode != 0 || !(status.Contains("Valid") || status.Contains("UnknownError") || status.Contains("NotTrusted")))
                { SigningProblem = "Set-AuthenticodeSignature said: " + status + " " + e.Result.Trim(); }
                if (new FileInfo(SelfSigned).Length <= new FileInfo(Unsigned).Length) { SigningProblem = "the signature was not written. " + status + " " + e.Result.Trim(); return; }
                Broken = Path.Combine(Dir, "driver-pack.exe");
                File.Copy(SelfSigned, Broken, true);
                var bytes = File.ReadAllBytes(Broken);
                // flip a byte inside the first section's raw data, away from the headers and the certificate table
                int pe = BitConverter.ToInt32(bytes, 0x3C);
                int sections = BitConverter.ToUInt16(bytes, pe + 6), optSize = BitConverter.ToUInt16(bytes, pe + 20);
                int sec = pe + 24 + optSize;
                int rawPtr = BitConverter.ToInt32(bytes, sec + 20), rawSize = BitConverter.ToInt32(bytes, sec + 16);
                int at = rawPtr + Math.Min(rawSize - 1, 0x200);
                bytes[at] ^= 0xFF;
                File.WriteAllBytes(Broken, bytes);
            }
            catch (Exception ex) { SigningProblem = ex.GetType().Name + ": " + ex.Message; }
        }

        // ------------------------------------------------------------------ data

        private void MakeData()
        {
            string W(string name, string text, Encoding enc = null) { var p = Path.Combine(Dir, name); File.WriteAllText(p, text, enc ?? new UTF8Encoding(false)); return p; }
            CsvFile = W("orders-march.csv",
                "id,name,city,total,date\r\n1001,Ava Chen,Toronto,128.50,2025-03-02\r\n1002,\"Ortega, Luis\",Madrid,64.00,2025-03-02\r\n1003,Mei Tanaka,Osaka,212.75,2025-03-03\r\n1004,\"Sam \"\"The Man\"\" Okafor\",Lagos,39.90,2025-03-04\r\n1005,\"Ines\nRossi\",Milan,305.00,2025-03-04\r\n",
                new UTF8Encoding(true));
            CsvSemi = W("orders-semicolon.csv", "id;name;total\n1;Ava;1,5\n2;Luis;2,5\n3;Mei;3,5\n");
            CsvTab = W("orders.tsv", "id\tname\ttotal\n1\tAva\t1.5\n2\tLuis\t2.5\n");
            var sb = new StringBuilder("id,name,amount\r\n");
            for (int i = 1; i <= 1200; i++) sb.Append(i).Append(",Row ").Append(i).Append(',').Append((i * 1.25).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
            CsvBig = W("big-orders.csv", sb.ToString());
            CsvBad = W("broken-quote.csv", "id,name\n1,\"Ava\n2,Luis\n");
            JsonOk = W("settings-ok.json", "{\n  \"name\": \"demo\",\n  \"retries\": 3,\n  \"mode\": \"fast\",\n  \"nested\": { \"a\": [1, 2, 3], \"b\": null }\n}\n");
            JsonBad = W("settings.json", "{\n  \"name\": \"demo\",\n  \"retries\": 3,\n  \"mode\": \"fast\"\n  \"path\": \"D:\\\\data\"\n}\n");
            JsonList = W("people.json", "[{\"id\":\"007\",\"name\":\"Ava\",\"age\":31},{\"id\":\"008\",\"name\":\"Luis\",\"age\":42,\"city\":\"Madrid\"}]");
            YamlOk = W("config.yaml", "# the server\nserver:\n  host: example.org\n  port: 8080\n  tls: true\nfeatures:\n  - search\n  - export\n  - notifications\n");
            YamlBad = W("config-bad.yaml", "server:\n  host: example.org\n   port: 8080\nfeatures: [a, b\n");
            YamlDup = W("config-dup.yaml", "a: 1\nb: 2\na: 3\n");
            YamlList = W("people.yaml", "- id: \"007\"\n  name: Ava\n  age: 31\n- id: \"008\"\n  name: Luis\n  age: 42\n  active: true\n");
        }

        // ------------------------------------------------------------------ text in every encoding

        private void MakeText()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            const string sample = "Caf\u00E9 menu for Friday\nPrices in \u20AC, not $\nThree lines in all\n";
            string Crlf(string s) => s.Replace("\n", "\r\n");
            EncUtf8 = Path.Combine(Dir, "plain-utf8.txt"); File.WriteAllText(EncUtf8, "Hello menu\nPrices in dollars\n", new UTF8Encoding(false));
            EncBom = Path.Combine(Dir, "bom-utf8.txt"); File.WriteAllText(EncBom, Crlf(sample), new UTF8Encoding(true));
            EncU16le = Path.Combine(Dir, "readme-old.txt"); File.WriteAllText(EncU16le, Crlf(sample), new UnicodeEncoding(false, true));
            EncU16be = Path.Combine(Dir, "u16be.txt"); File.WriteAllText(EncU16be, sample, new UnicodeEncoding(true, true));
            EncU16leNoBom = Path.Combine(Dir, "u16le-nobom.txt"); File.WriteAllText(EncU16leNoBom, Crlf(sample), new UnicodeEncoding(false, false));
            EncAnsi = Path.Combine(Dir, "notes.txt"); File.WriteAllBytes(EncAnsi, Encoding.GetEncoding(1252).GetBytes(Crlf("Caf\u00E9 menu for Friday\nPrices in \u20AC, not $\n(Windows-1252 bytes E9 and 80)\n")));
            EncCrlf = Path.Combine(Dir, "data.csv"); File.WriteAllText(EncCrlf, "a,b\r\n1,2\r\n", new UTF8Encoding(false));
            EncMixed = Path.Combine(Dir, "script.sh");
            var mixed = new StringBuilder("#!/bin/sh\r\necho start\n");
            for (int i = 0; i < 6; i++) mixed.Append("echo line ").Append(i).Append('\n');
            mixed.Append("run --fast\r\n");
            File.WriteAllText(EncMixed, mixed.ToString(), new UTF8Encoding(false));
            EncSjis = Path.Combine(Dir, "export-legacy.txt"); File.WriteAllBytes(EncSjis, Encoding.GetEncoding(932).GetBytes("\u3053\u3093\u306B\u3061\u306F\r\n\u30C6\u30B9\u30C8\r\n"));
            Png = Path.Combine(Dir, "logo.png");
            File.WriteAllBytes(Png, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 0x1F, 0x15, 0xC4, 0x89 });
            NulText = Path.Combine(Dir, "looks-text.txt"); File.WriteAllBytes(NulText, new byte[] { 0x41, 0, 0, 0x42, 0x43, 0, 0, 0, 0x44, 0x45, 0, 0, 1, 2, 3, 0 });
        }
    }
}
