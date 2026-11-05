using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace MonitoringTools.Core
{
    internal enum SignatureState { Valid, Unsigned, Broken, Untrusted, Unreadable }

    /// <summary>One certificate in the chain with what the check found about it.</summary>
    internal sealed class ChainLink
    {
        public string Name;
        public string Badge;      // "Valid", "Trusted", "Trusted root", "Not trusted", "Valid when signed", "Expired"
        public bool Good;
        public X509Certificate2 Certificate;
    }

    internal sealed class SignatureReport
    {
        public SignatureState State;
        public string Path;
        public uint TrustResult;                       // the WinVerifyTrust result code
        public string Problem;                         // plain-words reason for Broken, Untrusted and Unreadable
        public bool ViaCatalog;                        // the signature lives in a security catalog, not in the file
        public string Publisher;                       // subject common name (and place when present)
        public DateTime? SignedOn;                     // from a trusted timestamp when there is one
        public bool Timestamped;
        public DateTime? CertNotAfter;
        public bool CertValidWhenSigned;
        public string Digest;                          // "SHA-256 · file hash ..." text
        public string FileSha256;
        public string Thumbprint;
        public X509Certificate2 Leaf;
        public List<ChainLink> Chain = new List<ChainLink>();
        public long Size;
        public DateTime Modified;
    }

    /// <summary>
    /// Reads who signed an executable: WinVerifyTrust gives the verdict (embedded signatures and catalog-signed system files), and the
    /// signer certificate, its chain and the timestamp are read from the signature itself. Nothing is launched and revocation is not checked
    /// (it would wait for the network), which the dialog says plainly.
    /// </summary>
    internal static class SignatureEngine
    {
        // ------------------------------------------------------------------ WinVerifyTrust

        private static readonly Guid ActionGenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO { public uint cbStruct; public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_CATALOG_INFO
        {
            public uint cbStruct; public uint dwCatalogVersion; public string pcwszCatalogFilePath; public string pcwszMemberTag; public string pcwszMemberFilePath;
            public IntPtr hMemberFile; public IntPtr pbCalculatedFileHash; public uint cbCalculatedFileHash; public IntPtr pcCatalogContext; public IntPtr hCatAdmin;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public uint dwUIChoice; public uint fdwRevocationChecks;
            public uint dwUnionChoice; public IntPtr pInfo; public uint dwStateAction; public IntPtr hWVTStateData; public IntPtr pwszURLReference;
            public uint dwProvFlags; public uint dwUIContext; public IntPtr pSignatureSettings;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CATALOG_INFO { public uint cbStruct; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string wszCatalogFile; }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
        private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, ref WINTRUST_DATA pWVTData);
        [DllImport("wintrust.dll", SetLastError = true)] private static extern bool CryptCATAdminAcquireContext(out IntPtr phCatAdmin, IntPtr pgSubsystem, uint dwFlags);
        [DllImport("wintrust.dll", SetLastError = true)] private static extern bool CryptCATAdminCalcHashFromFileHandle(IntPtr hFile, ref uint pcbHash, byte[] pbHash, uint dwFlags);
        [DllImport("wintrust.dll", SetLastError = true)] private static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr hCatAdmin, byte[] pbHash, uint cbHash, uint dwFlags, ref IntPtr phPrevCatInfo);
        [DllImport("wintrust.dll", SetLastError = true)] private static extern bool CryptCATCatalogInfoFromContext(IntPtr hCatInfo, ref CATALOG_INFO psCatInfo, uint dwFlags);
        [DllImport("wintrust.dll", SetLastError = true)] private static extern bool CryptCATAdminReleaseCatalogContext(IntPtr hCatAdmin, IntPtr hCatInfo, uint dwFlags);
        [DllImport("wintrust.dll", SetLastError = true)] private static extern bool CryptCATAdminReleaseContext(IntPtr hCatAdmin, uint dwFlags);

        private const uint TRUST_E_NOSIGNATURE = 0x800B0100, TRUST_E_PROVIDER_UNKNOWN = 0x800B0001, TRUST_E_SUBJECT_FORM_UNKNOWN = 0x800B0003,
            TRUST_E_BAD_DIGEST = 0x80096010, TRUST_E_SUBJECT_NOT_TRUSTED = 0x800B0004, CERT_E_UNTRUSTEDROOT = 0x800B0109, CERT_E_CHAINING = 0x800B010A,
            CERT_E_EXPIRED = 0x800B0101, TRUST_E_EXPLICIT_DISTRUST = 0x800B0111, CERT_E_UNTRUSTEDTESTROOT = 0x800B010D, CERT_E_REVOKED = 0x800B010C,
            CRYPT_E_SECURITY_SETTINGS = 0x80092026, CERT_E_CN_NO_MATCH = 0x800B010F, CERT_E_PURPOSE = 0x800B0106, TRUST_E_CERT_SIGNATURE = 0x80096004,
            CERT_E_VALIDITYPERIODNESTING = 0x800B0102, CERT_E_UNTRUSTEDCA = 0x800B0112, TRUST_E_TIME_STAMP = 0x80096005, TRUST_E_COUNTER_SIGNER = 0x80096003;

        private static uint Verify(string path, byte[] catalogHash = null, string catalogPath = null)
        {
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = 2,                 // WTD_UI_NONE
                fdwRevocationChecks = 0,        // WTD_REVOKE_NONE
                dwStateAction = 0,              // WTD_STATEACTION_IGNORE
                dwProvFlags = 0x10 | 0x1000,    // no revocation check, cache-only URL retrieval: never wait for the network
            };
            IntPtr info = IntPtr.Zero, hashPtr = IntPtr.Zero;
            try
            {
                if (catalogHash == null)
                {
                    var f = new WINTRUST_FILE_INFO { pcwszFilePath = path };
                    f.cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>();
                    info = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
                    Marshal.StructureToPtr(f, info, false);
                    data.dwUnionChoice = 1;     // WTD_CHOICE_FILE
                }
                else
                {
                    hashPtr = Marshal.AllocHGlobal(catalogHash.Length);
                    Marshal.Copy(catalogHash, 0, hashPtr, catalogHash.Length);
                    var c = new WINTRUST_CATALOG_INFO
                    {
                        pcwszCatalogFilePath = catalogPath, pcwszMemberTag = Convert.ToHexString(catalogHash), pcwszMemberFilePath = path,
                        pbCalculatedFileHash = hashPtr, cbCalculatedFileHash = (uint)catalogHash.Length,
                    };
                    c.cbStruct = (uint)Marshal.SizeOf<WINTRUST_CATALOG_INFO>();
                    info = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_CATALOG_INFO>());
                    Marshal.StructureToPtr(c, info, false);
                    data.dwUnionChoice = 2;     // WTD_CHOICE_CATALOG
                }
                data.pInfo = info;
                return unchecked((uint)WinVerifyTrust(IntPtr.Zero, ActionGenericVerifyV2, ref data));
            }
            finally
            {
                if (info != IntPtr.Zero) { Marshal.DestroyStructure(info, catalogHash == null ? typeof(WINTRUST_FILE_INFO) : typeof(WINTRUST_CATALOG_INFO)); Marshal.FreeHGlobal(info); }
                if (hashPtr != IntPtr.Zero) Marshal.FreeHGlobal(hashPtr);
            }
        }

        /// <summary>Finds the security catalog that vouches for the file's hash, when there is one.</summary>
        private static (byte[] Hash, string Catalog) FindCatalog(string path)
        {
            if (!CryptCATAdminAcquireContext(out var admin, IntPtr.Zero, 0)) return (null, null);
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                uint len = 0;
                CryptCATAdminCalcHashFromFileHandle(fs.SafeFileHandle.DangerousGetHandle(), ref len, null, 0);
                if (len == 0) return (null, null);
                var hash = new byte[len];
                if (!CryptCATAdminCalcHashFromFileHandle(fs.SafeFileHandle.DangerousGetHandle(), ref len, hash, 0)) return (null, null);
                var prev = IntPtr.Zero;
                var catCtx = CryptCATAdminEnumCatalogFromHash(admin, hash, len, 0, ref prev);
                if (catCtx == IntPtr.Zero) return (null, null);
                try
                {
                    var ci = new CATALOG_INFO { cbStruct = (uint)Marshal.SizeOf<CATALOG_INFO>() };
                    if (!CryptCATCatalogInfoFromContext(catCtx, ref ci, 0)) return (null, null);
                    return (hash, ci.wszCatalogFile);
                }
                finally { CryptCATAdminReleaseCatalogContext(admin, catCtx, 0); }
            }
            catch { return (null, null); }
            finally { CryptCATAdminReleaseContext(admin, 0); }
        }

        // ------------------------------------------------------------------ the report

        internal static SignatureReport Inspect(string path)
        {
            var r = new SignatureReport { Path = path };
            try { var fi = new FileInfo(path); r.Size = fi.Length; r.Modified = fi.LastWriteTime; } catch { }
            r.FileSha256 = HashFile(path);

            uint trust;
            try { trust = Verify(path); } catch (Exception ex) { r.State = SignatureState.Unreadable; r.Problem = ex.Message; return r; }

            string catalogPath = null;
            if (trust == TRUST_E_NOSIGNATURE || trust == TRUST_E_PROVIDER_UNKNOWN || trust == TRUST_E_SUBJECT_FORM_UNKNOWN)
            {
                var (hash, catCtx) = FindCatalog(path);
                if (hash != null) { try { trust = Verify(path, hash, catCtx); catalogPath = catCtx; r.ViaCatalog = true; } catch { } }
            }
            r.TrustResult = trust;

            // The signer certificate, the timestamp and the chain are read from the signature itself.
            SignedCms cms = null;
            try { cms = catalogPath != null ? DecodeCms(File.ReadAllBytes(catalogPath)) : ReadEmbedded(path); } catch { }
            X509Certificate2 leaf = null;
            DateTime? stamp = null;
            if (cms != null)
            {
                try
                {
                    var signer = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0] : null;
                    leaf = signer?.Certificate;
                    if (signer != null) stamp = ReadTimestamp(signer);
                }
                catch { }
            }
            if (leaf == null && catalogPath == null)
            {
                try { leaf = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)); } catch { }
            }
            r.Leaf = leaf;
            if (leaf != null) FillFromCertificate(r, leaf, cms, stamp);

            switch (trust)
            {
                case 0:
                    r.State = SignatureState.Valid;
                    break;
                case TRUST_E_NOSIGNATURE:
                case TRUST_E_PROVIDER_UNKNOWN:
                case TRUST_E_SUBJECT_FORM_UNKNOWN:
                    r.State = leaf != null && trust != TRUST_E_NOSIGNATURE ? SignatureState.Unreadable : SignatureState.Unsigned;
                    if (r.State == SignatureState.Unreadable) r.Problem = "Windows cannot read the signature format of this file.";
                    break;
                case TRUST_E_BAD_DIGEST:
                case TRUST_E_CERT_SIGNATURE:
                case TRUST_E_COUNTER_SIGNER:
                case TRUST_E_TIME_STAMP:
                    r.State = SignatureState.Broken;
                    r.Problem = trust == TRUST_E_BAD_DIGEST ? "Digest mismatch: the content no longer matches the signature" : "The signature itself is damaged (code 0x" + trust.ToString("X8") + ")";
                    break;
                case TRUST_E_SUBJECT_NOT_TRUSTED:
                    r.State = SignatureState.Broken;
                    r.Problem = "Windows does not accept this signature (code 0x" + trust.ToString("X8") + ")";
                    break;
                case CERT_E_UNTRUSTEDROOT:
                case CERT_E_CHAINING:
                case CERT_E_UNTRUSTEDTESTROOT:
                case CERT_E_UNTRUSTEDCA:
                case TRUST_E_EXPLICIT_DISTRUST:
                case CERT_E_REVOKED:
                case CERT_E_EXPIRED:
                case CERT_E_PURPOSE:
                case CRYPT_E_SECURITY_SETTINGS:
                case CERT_E_CN_NO_MATCH:
                case CERT_E_VALIDITYPERIODNESTING:
                    r.State = SignatureState.Untrusted;
                    r.Problem = trust switch
                    {
                        CERT_E_EXPIRED => "The certificate had expired and there is no trusted timestamp that shows when the file was signed.",
                        TRUST_E_EXPLICIT_DISTRUST => "The certificate is on this computer's list of distrusted certificates.",
                        CERT_E_REVOKED => "The certificate was revoked by its issuer.",
                        _ => "The issuer is not on this computer's trusted list (code 0x" + trust.ToString("X8") + ").",
                    };
                    break;
                default:
                    r.State = SignatureState.Untrusted;
                    r.Problem = "Windows could not confirm the signature (code 0x" + trust.ToString("X8") + ").";
                    break;
            }
            return r;
        }

        private static string HashFile(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
            }
            catch { return null; }
        }

        private static SignedCms DecodeCms(byte[] pkcs7)
        {
            var cms = new SignedCms();
            cms.Decode(pkcs7);
            return cms;
        }

        /// <summary>The Authenticode block of a PE file: the certificate table entry holds a PKCS#7 signature.</summary>
        private static SignedCms ReadEmbedded(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var br = new BinaryReader(fs);
            if (fs.Length < 0x40 || br.ReadUInt16() != 0x5A4D) return null;
            fs.Position = 0x3C; var pe = br.ReadInt32();
            if (pe <= 0 || pe + 24 > fs.Length) return null;
            fs.Position = pe; if (br.ReadUInt32() != 0x00004550) return null;
            fs.Position = pe + 24; var magic = br.ReadUInt16();
            long dirs = pe + 24 + (magic == 0x20B ? 112 : 96);
            long entry = dirs + 4 * 8;                                 // IMAGE_DIRECTORY_ENTRY_SECURITY
            if (entry + 8 > fs.Length) return null;
            fs.Position = entry; var off = br.ReadUInt32(); var size = br.ReadUInt32();
            if (off == 0 || size < 8 || off + size > fs.Length) return null;
            fs.Position = off; var len = br.ReadUInt32(); br.ReadUInt16(); var type = br.ReadUInt16();
            if (type != 2 || len < 8 || len > size) return null;
            return DecodeCms(br.ReadBytes((int)len - 8));
        }

        private const string RfcTimestampAttribute = "1.3.6.1.4.1.311.3.3.1";

        /// <summary>The time the signature was countersigned: a legacy counter-signature or an RFC 3161 timestamp.</summary>
        private static DateTime? ReadTimestamp(SignerInfo signer)
        {
            foreach (var cs in signer.CounterSignerInfos)
                foreach (var attr in cs.SignedAttributes)
                    if (attr.Oid.Value == "1.2.840.113549.1.9.5") { try { return new Pkcs9SigningTime(attr.Values[0].RawData).SigningTime.ToUniversalTime(); } catch { } }
            foreach (var attr in signer.UnsignedAttributes)
            {
                if (attr.Oid.Value != RfcTimestampAttribute) continue;
                try
                {
                    var tsCms = new SignedCms(); tsCms.Decode(attr.Values[0].RawData);
                    var tst = new AsnReader(tsCms.ContentInfo.Content, AsnEncodingRules.DER).ReadSequence();
                    tst.ReadInteger(); tst.ReadObjectIdentifier(); tst.ReadEncodedValue(); tst.ReadInteger();
                    return tst.ReadGeneralizedTime().UtcDateTime;
                }
                catch { }
            }
            return null;
        }

        private static void FillFromCertificate(SignatureReport r, X509Certificate2 leaf, SignedCms cms, DateTime? stamp)
        {
            r.Publisher = Describe(leaf);
            r.Thumbprint = leaf.Thumbprint;
            r.CertNotAfter = leaf.NotAfter;
            if (stamp != null) { r.SignedOn = stamp; r.Timestamped = true; }
            var when = stamp ?? DateTime.UtcNow;
            r.CertValidWhenSigned = when >= leaf.NotBefore.ToUniversalTime() && when <= leaf.NotAfter.ToUniversalTime();
            r.Digest = r.FileSha256 == null ? null : "SHA-256 · file hash " + Shorten(r.FileSha256);

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.VerificationTime = stamp?.ToLocalTime() ?? DateTime.Now;
            if (cms != null) foreach (var c in cms.Certificates.Cast<X509Certificate2>()) chain.ChainPolicy.ExtraStore.Add(c);
            chain.Build(leaf);
            var elements = chain.ChainElements.Cast<X509ChainElement>().ToList();
            for (int i = 0; i < elements.Count; i++)
            {
                var el = elements[i];
                var flags = el.ChainElementStatus.Select(s => s.Status).Aggregate(X509ChainStatusFlags.NoError, (a, b) => a | b);
                bool root = i == elements.Count - 1 && el.Certificate.Subject == el.Certificate.Issuer;
                bool untrusted = (flags & (X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain)) != 0;
                bool expired = (flags & X509ChainStatusFlags.NotTimeValid) != 0;
                string badge; bool good = true;
                if (untrusted || (i == elements.Count - 1 && !root && flags != X509ChainStatusFlags.NoError)) { badge = "Not trusted"; good = false; }
                else if (expired) { badge = "Expired"; good = false; }
                else if (i == 0) badge = stamp != null && leaf.NotAfter.ToUniversalTime() < DateTime.UtcNow ? "Valid when signed" : "Valid";
                else badge = root ? "Trusted root" : "Trusted";
                r.Chain.Add(new ChainLink { Name = Describe(el.Certificate, false), Badge = badge, Good = good, Certificate = el.Certificate });
            }
        }

        internal static string Describe(X509Certificate2 c, bool withPlace = true)
        {
            var name = c.GetNameInfo(X509NameType.SimpleName, false);
            if (string.IsNullOrWhiteSpace(name)) name = c.Subject;
            if (!withPlace) return name;
            string Part(string key) => c.Subject.Split(',').Select(p => p.Trim()).Where(p => p.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)).Select(p => p[(key.Length + 1)..].Trim('"')).FirstOrDefault();
            var place = new[] { Part("L"), Part("C") }.Where(p => !string.IsNullOrEmpty(p)).ToList();
            return place.Count > 0 ? name + " · " + string.Join(", ", place) : name;
        }

        internal static string Shorten(string hex) => hex.Length <= 14 ? hex.ToUpperInvariant() : hex[..6].ToUpperInvariant() + "…" + hex[^4..].ToUpperInvariant();

        /// <summary>Whether this file name is one the dialog is offered for.</summary>
        internal static bool IsSignable(string path)
        {
            var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext is ".exe" or ".msi" or ".dll" or ".sys" or ".cab" or ".msix" or ".ocx" or ".scr";
        }
    }
}
