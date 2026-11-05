using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MonitoringTools.Core
{
    internal sealed class CertDetails
    {
        public string Subject, Issuer, Serial, Sha256, Sha1, Key, Kind;
        public DateTime NotBefore, NotAfter;
        public List<string> Names = new List<string>();
        public List<string> Uses = new List<string>();
        public bool HasPrivateKey;
        public int DaysLeft;                     // negative once expired
        public string Expiry;                    // "214 days left", "Expired 3 days ago"
        public long Size;
        public X509Certificate2 Certificate;
        public int CertCount = 1;
    }

    internal enum CertFormat { Pem, Der, Pkcs12 }

    /// <summary>Reads and converts certificates. The password of a PKCS#12 file is used once, to read it, and is never stored or logged.</summary>
    internal static class CertEngine
    {
        internal sealed class LoadResult
        {
            public CertDetails Details;
            public bool NeedsPassword;
            public bool WrongPassword;
            public string Problem;
        }

        internal static bool IsCertificateFile(string path)
        {
            var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext is ".cer" or ".crt" or ".pem" or ".pfx" or ".p12" or ".der";
        }

        internal static bool IsPkcs12(string path) => Path.GetExtension(path ?? "").ToLowerInvariant() is ".pfx" or ".p12";

        internal static LoadResult Load(string path, string password = null)
        {
            var res = new LoadResult();
            try
            {
                var bytes = File.ReadAllBytes(path);
                X509Certificate2 cert = null; int count = 1;
                var text = LooksText(bytes) ? Encoding.UTF8.GetString(bytes) : null;
                if (text != null && text.Contains("-----BEGIN"))
                {
                    if (text.Contains("-----BEGIN ENCRYPTED PRIVATE KEY-----") && !text.Contains("-----BEGIN CERTIFICATE-----"))
                    { res.Problem = "This file holds only an encrypted private key, not a certificate."; return res; }
                    var certs = new X509Certificate2Collection();
                    certs.ImportFromPem(text);
                    if (certs.Count == 0) { res.Problem = "No certificate was found in this file."; return res; }
                    cert = certs[0]; count = certs.Count;
                    if (text.Contains("PRIVATE KEY-----"))
                        try { using var withKey = X509Certificate2.CreateFromPem(text, text); cert = new X509Certificate2(withKey.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet); } catch { }
                }
                else
                {
                    var type = X509Certificate2.GetCertContentType(bytes);
                    if (type == X509ContentType.Pfx)
                    {
                        var col = new X509Certificate2Collection();
                        try { col.Import(bytes, password ?? "", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet); }
                        catch (CryptographicException)
                        {
                            if (string.IsNullOrEmpty(password)) res.NeedsPassword = true; else res.WrongPassword = true;
                            return res;
                        }
                        if (col.Count == 0) { res.Problem = "No certificate was found in this file."; return res; }
                        cert = col.Cast<X509Certificate2>().FirstOrDefault(c => c.HasPrivateKey) ?? col[0];
                        count = col.Count;
                    }
                    else if (type == X509ContentType.Unknown) { res.Problem = "This is not a certificate file Windows can read."; return res; }
                    else
                    {
                        var col = new X509Certificate2Collection();
                        col.Import(bytes);
                        if (col.Count == 0) { res.Problem = "No certificate was found in this file."; return res; }
                        cert = col[0]; count = col.Count;
                    }
                }
                res.Details = Describe(cert, bytes.Length, count);
            }
            catch (Exception ex) { res.Problem = "Could not read this file: " + ex.Message; }
            return res;
        }

        private static bool LooksText(byte[] b) => b.Length > 0 && b.Take(64).All(x => x == 9 || x == 10 || x == 13 || (x >= 32 && x < 127));

        internal static CertDetails Describe(X509Certificate2 c, long size = 0, int count = 1)
        {
            var d = new CertDetails
            {
                Certificate = c, Subject = c.Subject, Issuer = c.Issuer, Serial = SpacedHex(c.SerialNumber), Size = size, CertCount = count,
                Sha256 = SpacedHex(Convert.ToHexString(SHA256.HashData(c.RawData))), Sha1 = c.Thumbprint, NotBefore = c.NotBefore, NotAfter = c.NotAfter,
                HasPrivateKey = c.HasPrivateKey,
            };
            d.Kind = d.HasPrivateKey ? "certificate with private key" : "public certificate";
            d.Key = KeyText(c);
            d.Names = AlternativeNames(c);
            d.Uses = Usages(c);
            var left = c.NotAfter - DateTime.Now;
            d.DaysLeft = (int)Math.Floor(left.TotalDays);
            d.Expiry = left.TotalSeconds >= 0
                ? (d.DaysLeft == 1 ? "1 day left" : d.DaysLeft + " days left")
                : (int)Math.Floor(-left.TotalDays) switch { 0 => "Expired today", 1 => "Expired 1 day ago", var n => "Expired " + n + " days ago" };
            return d;
        }

        private static string SpacedHex(string hex) => string.Join(" ", Enumerable.Range(0, hex.Length / 2).Select(i => hex.Substring(i * 2, 2)));

        private static string KeyText(X509Certificate2 c)
        {
            string key;
            using (var rsa = c.GetRSAPublicKey()) key = rsa != null ? "RSA " + rsa.KeySize + " bits" : null;
            if (key == null) using (var ec = c.GetECDsaPublicKey()) key = ec != null ? "ECDSA " + ec.KeySize + " bits" : null;
            if (key == null) using (var dsa = c.GetDSAPublicKey()) key = dsa != null ? "DSA " + dsa.KeySize + " bits" : null;
            key ??= c.PublicKey.Oid.FriendlyName ?? "unknown key";
            return key + " · signature " + (c.SignatureAlgorithm.FriendlyName ?? c.SignatureAlgorithm.Value);
        }

        internal static List<string> AlternativeNames(X509Certificate2 c)
        {
            var list = new List<string>();
            var ext = c.Extensions.Cast<X509Extension>().FirstOrDefault(e => e.Oid?.Value == "2.5.29.17");
            if (ext == null) return list;
            try
            {
                var r = new AsnReader(ext.RawData, AsnEncodingRules.DER).ReadSequence();
                while (r.HasData)
                {
                    var tag = r.PeekTag();
                    if (tag.TagClass != TagClass.ContextSpecific) { r.ReadEncodedValue(); continue; }
                    switch (tag.TagValue)
                    {
                        case 1: case 2: case 6: list.Add(r.ReadCharacterString(UniversalTagNumber.IA5String, tag)); break;
                        case 7: list.Add(new IPAddress(r.ReadOctetString(tag)).ToString()); break;
                        default: r.ReadEncodedValue(); break;
                    }
                }
            }
            catch { }
            return list;
        }

        internal static List<string> Usages(X509Certificate2 c)
        {
            var uses = new List<string>();
            foreach (var e in c.Extensions)
            {
                if (e is X509KeyUsageExtension ku)
                {
                    var f = ku.KeyUsages;
                    foreach (var (flag, name) in new[]
                    {
                        (X509KeyUsageFlags.DigitalSignature, "Digital signature"), (X509KeyUsageFlags.NonRepudiation, "Non-repudiation"), (X509KeyUsageFlags.KeyEncipherment, "Key encipherment"),
                        (X509KeyUsageFlags.DataEncipherment, "Data encipherment"), (X509KeyUsageFlags.KeyAgreement, "Key agreement"), (X509KeyUsageFlags.KeyCertSign, "Certificate signing"),
                        (X509KeyUsageFlags.CrlSign, "CRL signing"),
                    })
                        if ((f & flag) != 0) uses.Add(name);
                }
                else if (e is X509EnhancedKeyUsageExtension eku)
                {
                    foreach (var oid in eku.EnhancedKeyUsages)
                        uses.Add(oid.Value switch
                        {
                            "1.3.6.1.5.5.7.3.1" => "Server authentication", "1.3.6.1.5.5.7.3.2" => "Client authentication", "1.3.6.1.5.5.7.3.3" => "Code signing",
                            "1.3.6.1.5.5.7.3.4" => "Email protection", "1.3.6.1.5.5.7.3.8" => "Time stamping", "1.3.6.1.5.5.7.3.9" => "OCSP signing",
                            _ => oid.FriendlyName ?? oid.Value,
                        });
                }
                else if (e is X509BasicConstraintsExtension bc && bc.CertificateAuthority) uses.Add("Certificate authority");
            }
            return uses.Distinct().ToList();
        }

        /// <summary>The name of the converted file: the original name with the new extension, never an existing file.</summary>
        internal static string DefaultOutputName(string source, CertFormat format) => Path.GetFileNameWithoutExtension(source) + Extension(format);

        internal static string Extension(CertFormat f) => f switch { CertFormat.Pem => ".pem", CertFormat.Der => ".cer", _ => ".pfx" };

        /// <summary>Whether the file the person opened holds a private key, so the switch may be turned on.</summary>
        internal static bool CanIncludeKey(CertDetails d, CertFormat f) => d.HasPrivateKey && f != CertFormat.Der;

        /// <summary>
        /// Writes the converted certificate to <paramref name="outPath"/>. A private key goes in only when <paramref name="includeKey"/> is set and
        /// a new password protects it; a public-only conversion is always safe to share.
        /// </summary>
        internal static string ConvertTo(CertDetails d, CertFormat format, string outPath, bool includeKey, string newPassword)
        {
            var c = d.Certificate;
            if (includeKey && !d.HasPrivateKey) throw new InvalidOperationException("This file has no private key to include.");
            if (includeKey && format == CertFormat.Der) throw new InvalidOperationException("A DER file cannot hold a private key.");
            if (includeKey && string.IsNullOrEmpty(newPassword)) throw new InvalidOperationException("A password is needed to protect the private key.");
            var target = FileOps.Unique(outPath);
            byte[] data;
            switch (format)
            {
                case CertFormat.Pem:
                    var pem = new StringBuilder(c.ExportCertificatePem()).Append('\n');
                    if (includeKey)
                    {
                        using var rsa = c.GetRSAPrivateKey(); using var ec = rsa == null ? c.GetECDsaPrivateKey() : null;
                        AsymmetricAlgorithm key = (AsymmetricAlgorithm)rsa ?? ec;
                        if (key == null) throw new InvalidOperationException("The private key cannot be exported.");
                        pem.Append(key.ExportEncryptedPkcs8PrivateKeyPem(newPassword, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100000))).Append('\n');
                    }
                    data = new UTF8Encoding(false).GetBytes(pem.ToString());
                    break;
                case CertFormat.Der:
                    data = c.Export(X509ContentType.Cert);
                    break;
                default:
                    if (includeKey) data = c.Export(X509ContentType.Pfx, newPassword);
                    else
                    {
                        using var pub = new X509Certificate2(c.RawData);
                        data = pub.Export(X509ContentType.Pfx, string.IsNullOrEmpty(newPassword) ? null : newPassword);
                    }
                    break;
            }
            File.WriteAllBytes(target, data);
            return target;
        }

        /// <summary>The details as plain text for the Copy details button. Never includes a private key or a password.</summary>
        internal static string AsText(CertDetails d) => string.Join(Environment.NewLine, new[]
        {
            "Subject: " + d.Subject, "Issuer: " + d.Issuer, "Valid from: " + d.NotBefore.ToString("yyyy-MM-dd HH:mm") + " to " + d.NotAfter.ToString("yyyy-MM-dd HH:mm"),
            "Alternative names: " + (d.Names.Count == 0 ? "none" : string.Join(", ", d.Names)), "Key: " + d.Key, "Serial: " + d.Serial, "SHA-256: " + d.Sha256,
            "SHA-1: " + d.Sha1, "Allowed uses: " + (d.Uses.Count == 0 ? "not restricted" : string.Join(", ", d.Uses)),
        });
    }
}
