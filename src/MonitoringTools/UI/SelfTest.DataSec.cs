using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        private static async Task<bool> DsWait(Task t, int ms = 30000) => await Task.WhenAny(t, Task.Delay(ms)) == t;

        private static async Task<bool> DsUntil(Func<bool> ok, int ms = 15000)
        {
            var end = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < end) { if (ok()) return true; await Task.Delay(40); }
            return ok();
        }

        private static string DsText(Window w)
        {
            w.UpdateLayout();
            var sb = new StringBuilder();
            foreach (var tb in DataToolWindow.Descend<TextBlock>(w)) { sb.AppendLine(tb.Text); sb.AppendLine(string.Concat(tb.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text))); }
            return sb.ToString();
        }

        private static bool DsSame(JsonNode a, JsonNode b)
        {
            if (a is JsonObject oa && b is JsonObject ob) return oa.Count == ob.Count && oa.All(kv => ob.ContainsKey(kv.Key) && DsSame(kv.Value, ob[kv.Key]));
            if (a is JsonArray aa && b is JsonArray ab) return aa.Count == ab.Count && aa.Zip(ab).All(p => DsSame(p.First, p.Second));
            if (a == null || b == null) return a == null && b == null;
            var ea = a.GetValue<JsonElement>(); var eb = b.GetValue<JsonElement>();
            if (ea.ValueKind == JsonValueKind.Number && eb.ValueKind == JsonValueKind.Number) return ea.GetDouble() == eb.GetDouble();
            return ea.ValueKind == eb.ValueKind && ea.GetRawText() == eb.GetRawText();
        }

        private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

        private static void DsClose(params Window[] ws) { foreach (var w in ws) { try { w?.Close(); } catch { } } }

        private static IEnumerable<Window> DsDialogs() => Application.Current.Windows.OfType<Window>().Where(w => w is HashVerifyDialog || w is WhoSignedDialog || w is CertInspectorDialog || w is DataViewerDialog || w is EncodingLinesDialog).ToList();

        private static void DsCloseAll() { foreach (var w in DsDialogs()) { try { w.Close(); } catch { } } }

        private static async Task DataSecChecksAsync(string dir)
        {
            var work = Path.Combine(Settings.DataDir, "datasec-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            DataSecFixtures fx = null;
            try { fx = DataSecFixtures.Create(work, signing: true, big: true); }
            catch (Exception ex) { Results.Add(new Result("datasec: the test files could not be created", false, ex.ToString())); return; }

            await HashChecks(fx);
            await SignatureChecks(fx);
            await CertChecks(fx);
            await DataChecks(fx);
            await EncodingChecks(fx);
            await DsToastChecks(fx);
            await CheckAsync("datasec: every visible string in the data and security dialogs has a Cantonese entry", () => DsLanguageAudit(fx, dir));
            try { Directory.Delete(work, true); } catch { }
        }

        // ================================================================== hash

        private static async Task HashChecks(DataSecFixtures fx)
        {
            var want = new Dictionary<string, string>
            {
                ["SHA-256"] = Hex(SHA256.HashData(fx.HashedBytes)), ["SHA-1"] = Hex(SHA1.HashData(fx.HashedBytes)),
                ["MD5"] = Hex(MD5.HashData(fx.HashedBytes)), ["SHA-512"] = Hex(SHA512.HashData(fx.HashedBytes)),
            };
            Check("hash: the engine's four algorithms match the framework's on known text", () =>
            {
                var p = Path.Combine(fx.Dir, "abc.txt"); File.WriteAllText(p, "abc");
                var r = HashEngine.ComputeAsync(p, HashEngine.Algorithms, null, CancellationToken.None).GetAwaiter().GetResult();
                if (r["SHA-256"] != "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad") return "SHA-256 " + r["SHA-256"];
                if (r["SHA-1"] != "a9993e364706816aba3e25717850c26c9cd0d89d") return "SHA-1 " + r["SHA-1"];
                if (r["MD5"] != "900150983cd24fb0d6963f7d28e17f72") return "MD5 " + r["MD5"];
                return r["SHA-512"].StartsWith("ddaf35a193617aba") ? null : "SHA-512 " + r["SHA-512"];
            });
            Check("hash: a pasted value ignores spaces, dashes, colons and capitals, and reads a whole checksum line", () =>
            {
                var h = want["SHA-256"];
                var spaced = string.Join(" ", Enumerable.Range(0, 8).Select(i => h.Substring(i * 8, 8))).ToUpperInvariant();
                if (HashEngine.ParseExpected(spaced).Hex != h) return "spaced capitals";
                if (HashEngine.ParseExpected(string.Join("-", Enumerable.Range(0, 8).Select(i => h.Substring(i * 8, 8)))).Hex != h) return "dashes";
                if (HashEngine.ParseExpected(string.Join(":", Enumerable.Range(0, 32).Select(i => h.Substring(i * 2, 2)))).Hex != h) return "colons";
                var line = HashEngine.ParseExpected(h + "  ubuntu-sample.iso\r\n"); if (line.Hex != h || line.FileName != "ubuntu-sample.iso") return "whole line";
                var star = HashEngine.ParseExpected(h + " *ubuntu-sample.iso"); if (star.Hex != h) return "star line";
                var multi = HashEngine.ParseExpected("aa" + new string('0', 62) + "  other.iso\n" + h + "  ubuntu-sample.iso", "ubuntu-sample.iso"); if (multi.Hex != h) return "the right line of a checksum file";
                if (HashEngine.ParseExpected("not a checksum!").Hex != null) return "junk accepted";
                if (HashEngine.FirstDifference("abcdef", "abcxef") != 4 || HashEngine.FirstDifference("abc", "abc") != 0) return "first difference";
                return HashEngine.AlgorithmForLength(40) == "SHA-1" && HashEngine.AlgorithmForLength(41) == null ? null : "length guess";
            });

            await CheckAsync("hash dialog: computes every algorithm, shows SHA-256 in groups of eight, and Verify says match or mismatch with the first differing character", async () =>
            {
                var d = HashVerifyDialog.Open(fx.Hashed);
                try
                {
                    if (!await DsWait(d.Ready)) return "SHA-256 never finished";
                    if (d.ShownHash("SHA-256") != want["SHA-256"]) return "SHA-256 differs from the reference";
                    var shown = DsText(d);
                    var grouped = HashEngine.Grouped(want["SHA-256"]).Replace(" ", "  ");
                    if (!shown.Contains(want["SHA-256"].Substring(0, 8)) || !shown.Contains(want["SHA-256"].Substring(8, 8))) return "the hash is not on screen in groups";
                    if (d.PrimaryButton.IsEnabled) return "Verify is on with nothing pasted";
                    foreach (var (i, a) in new[] { (1, "SHA-1"), (2, "MD5"), (3, "SHA-512") })
                    {
                        d.ChooseAlgorithm(i);
                        if (!await DsUntil(() => d.ShownHash(a) != null)) return a + " never finished";
                        if (d.ShownHash(a) != want[a]) return a + " differs from the reference";
                    }
                    d.ChooseAlgorithm(0);
                    await DsUntil(() => !d.Running);
                    // a match: spaces, dashes and capitals ignored
                    d.ExpectedBox.Text = string.Join("-", Enumerable.Range(0, 8).Select(i => want["SHA-256"].Substring(i * 8, 8))).ToUpperInvariant();
                    if (!d.PrimaryButton.IsEnabled) return "Verify stayed off with a valid value pasted";
                    DataToolWindow.Press(d.PrimaryButton);
                    if (d.Banner.Kind != BannerKind.Success || !d.Banner.TitleText.Contains("matches")) return "no match banner: " + d.Banner.TitleText;
                    // a mismatch at a known character
                    var bad = want["SHA-256"].ToCharArray(); bad[8] = bad[8] == 'a' ? 'b' : 'a';
                    d.ExpectedBox.Text = new string(bad);
                    if (d.Banner.Visibility == Visibility.Visible && d.Banner.Kind == BannerKind.Success) return "an old verdict stayed after the value changed";
                    DataToolWindow.Press(d.PrimaryButton);
                    if (d.Banner.Kind != BannerKind.Error || !d.Banner.TitleText.Contains("does not match") || !d.Banner.BodyText.Contains("Do not run or install") || !d.Banner.BodyText.Contains("character 9")) return "no mismatch banner: " + d.Banner.TitleText + " / " + d.Banner.BodyText;
                    // a whole checksum line
                    d.ExpectedBox.Text = want["SHA-256"] + " *ubuntu-sample.iso";
                    DataToolWindow.Press(d.PrimaryButton);
                    if (d.Banner.Kind != BannerKind.Success) return "a whole checksum line did not match";
                    return null;
                }
                finally { DsClose(d); }
            });

            await CheckAsync("hash dialog: a value whose length belongs to another algorithm switches to it, and junk or a wrong length keeps Verify off", async () =>
            {
                var d = HashVerifyDialog.Open(fx.Hashed);
                try
                {
                    await DsWait(d.Ready);
                    d.ExpectedBox.Text = want["SHA-1"];
                    if (!d.PrimaryButton.IsEnabled) return "a SHA-1 value is not accepted while SHA-256 is chosen";
                    if (!DsText(d).Contains("which is SHA-1")) return "no note about the length";
                    DataToolWindow.Press(d.PrimaryButton);
                    if (!await DsUntil(() => d.Algorithm == "SHA-1" && d.Banner.Kind == BannerKind.Success && d.Banner.TitleText.Contains("matches"))) return "it did not switch to SHA-1 and match: " + d.Algorithm + " / " + d.Banner.TitleText + " / running " + d.Running + " / sha1 " + (d.ShownHash("SHA-1") != null) + " / " + d.StatusLine;
                    d.ExpectedBox.Text = "hello world";
                    if (d.PrimaryButton.IsEnabled) return "Verify is on for junk";
                    d.ExpectedBox.Text = new string('a', 41);
                    if (d.PrimaryButton.IsEnabled) return "Verify is on for 41 characters";
                    if (!DsText(d).Contains("41 characters")) return "no note about the length";
                    return null;
                }
                finally { DsClose(d); }
            });

            await CheckAsync("hash: a large file reports progress and Cancel stops reading without a result", async () =>
            {
                int reports = 0; long last = 0, total = 0;
                var res = await HashEngine.ComputeAsync(fx.Big, new[] { "SHA-256", "MD5" }, new Progress<(long Done, long Total)>(p => { reports++; last = p.Done; total = p.Total; }), CancellationToken.None);
                await Task.Delay(200);
                if (reports < 2) return "only " + reports + " progress reports";
                if (res["MD5"].Length != 32) return "no MD5";
                var cts = new CancellationTokenSource(); bool cancelled = false;
                try { await HashEngine.ComputeAsync(fx.Big, new[] { "SHA-512" }, new Progress<(long Done, long Total)>(p => cts.Cancel()), cts.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                return cancelled ? null : "cancelling did not stop the read";
            });

            await CheckAsync("hash dialog: the Cancel button stops a running read, says nothing was changed, and leaves no hash", async () =>
            {
                var d = HashVerifyDialog.Open(fx.Big);
                try
                {
                    if (!d.Running) return "not running right after opening";
                    DataToolWindow.Press(d.CancelButton);
                    if (!await DsWait(d.Ready, 20000)) return "never settled";
                    if (d.ShownHash("SHA-256") != null) return "a hash was kept after cancelling";
                    if (d.Running) return "still running";
                    return d.Banner.TitleText == "Stopped" && d.Banner.BodyText.Contains("Nothing was changed") ? null : "banner: " + d.Banner.TitleText;
                }
                finally { DsClose(d); }
            });
        }

        // ================================================================== who signed this

        private static async Task SignatureChecks(DataSecFixtures fx)
        {
            await CheckAsync("signature: a signed system file is valid, names Microsoft, and its chain ends in a trusted root", async () =>
            {
                if (fx.SystemSigned == null) return "no signed system file found to test with";
                var r = await Task.Run(() => SignatureEngine.Inspect(fx.SystemSigned));
                if (r.State != SignatureState.Valid) return "state " + r.State + " (0x" + r.TrustResult.ToString("X8") + ") " + r.Problem;
                if (r.Publisher == null || !r.Publisher.Contains("Microsoft")) return "publisher " + r.Publisher;
                if (r.Chain.Count < 2) return "chain of " + r.Chain.Count;
                if (r.Chain[^1].Badge != "Trusted root") return "last link " + r.Chain[^1].Badge;
                return r.Thumbprint != null && r.FileSha256 != null ? null : "no thumbprint or file hash";
            });
            await CheckAsync("signature: an unsigned program, a program signed by an untrusted issuer and a program changed after signing give three different verdicts", async () =>
            {
                if (fx.SigningProblem != null) return "the throwaway signing could not run: " + fx.SigningProblem;
                var un = await Task.Run(() => SignatureEngine.Inspect(fx.Unsigned));
                if (un.State != SignatureState.Unsigned) return "unsigned copy: " + un.State + " 0x" + un.TrustResult.ToString("X8");
                var ss = await Task.Run(() => SignatureEngine.Inspect(fx.SelfSigned));
                if (ss.State != SignatureState.Untrusted) return "self-signed copy: " + ss.State + " 0x" + ss.TrustResult.ToString("X8") + " " + ss.Problem;
                if (ss.Publisher == null || !ss.Publisher.Contains("Northwind Traders Test")) return "self-signed publisher " + ss.Publisher;
                if (ss.Chain.Count == 0 || ss.Chain[0].Good) return "the self-signed issuer was shown as trusted";
                var br = await Task.Run(() => SignatureEngine.Inspect(fx.Broken));
                if (br.State != SignatureState.Broken) return "changed copy: " + br.State + " 0x" + br.TrustResult.ToString("X8") + " " + br.Problem;
                return br.Problem != null && br.Problem.Contains("Digest mismatch") ? null : "broken problem text: " + br.Problem;
            });
            await CheckAsync("signature dialog: shows the right banner and actions for signed, unsigned, untrusted and broken files, and opens the certificate inspector", async () =>
            {
                if (fx.SystemSigned == null) return "no signed system file";
                var d = WhoSignedDialog.Open(fx.SystemSigned);
                try
                {
                    if (!await DsWait(d.Ready)) return "never finished";
                    if (d.Banner.Kind != BannerKind.Success || !d.Banner.TitleText.StartsWith("Signed by") || !d.Banner.BodyText.Contains("not that it is safe")) return "signed banner: " + d.Banner.TitleText + " / " + d.Banner.BodyText;
                    var open = d.FindButton("Open certificate"); if (open == null) return "no Open certificate button";
                    DataToolWindow.Press(open);
                    await Task.Delay(300);
                    var ci = CertInspectorDialog.Current; if (ci == null || ci.Details == null || !ci.Details.Subject.Contains("Microsoft")) return "the certificate inspector did not open on the Microsoft certificate";
                    DsClose(ci);
                }
                finally { DsClose(d); }
                if (fx.SigningProblem != null) return "the throwaway signing could not run: " + fx.SigningProblem;
                d = WhoSignedDialog.Open(fx.Unsigned);
                try
                {
                    await DsWait(d.Ready);
                    if (d.Banner.Kind != BannerKind.Warning || d.Banner.TitleText != "This file is not signed") return "unsigned banner: " + d.Banner.TitleText;
                    var hv = d.FindButton("Hash and verify"); if (hv == null || d.FindButton("Show in folder") == null) return "unsigned actions missing";
                    DataToolWindow.Press(hv); await Task.Delay(200);
                    if (HashVerifyDialog.Current == null) return "Hash and verify did not open";
                    DsClose(HashVerifyDialog.Current);
                }
                finally { DsClose(d); }
                d = WhoSignedDialog.Open(fx.SelfSigned);
                try
                {
                    await DsWait(d.Ready);
                    if (d.Banner.Kind != BannerKind.Warning || !d.Banner.TitleText.Contains("root is not trusted")) return "untrusted banner: " + d.Banner.TitleText;
                    if (d.FindButton("Open certificate") == null) return "no Open certificate for an untrusted file";
                }
                finally { DsClose(d); }
                d = WhoSignedDialog.Open(fx.Broken);
                try
                {
                    await DsWait(d.Ready);
                    if (d.Banner.Kind != BannerKind.Error || d.Banner.TitleText != "The signature is broken" || !d.Banner.BodyText.Contains("Do not run it")) return "broken banner: " + d.Banner.TitleText;
                    if (d.FindButton("Delete file") == null || d.FindButton("Show in folder") == null) return "broken actions missing";
                    if (!DsText(d).Contains("Digest mismatch")) return "the problem is not shown";
                    if (!File.Exists(fx.Broken)) return "the dialog deleted the file";
                }
                finally { DsClose(d); }
                return null;
            });
        }

        // ================================================================== certificates

        private static async Task CertChecks(DataSecFixtures fx)
        {
            Check("certificate: details of a self-signed certificate made in code are read exactly", () =>
            {
                var r = CertEngine.Load(fx.CertCer);
                if (r.Details == null) return r.Problem;
                var d = r.Details; var c = fx.Cert;
                if (d.Subject != c.Subject || d.Issuer != c.Issuer) return "subject or issuer";
                if (d.Sha1 != c.Thumbprint) return "thumbprint";
                if (d.Sha256.Replace(" ", "").ToLowerInvariant() != Hex(SHA256.HashData(c.RawData))) return "SHA-256";
                if (!d.Names.SequenceEqual(new[] { "api.example.org", "www.example.org" })) return "names: " + string.Join(",", d.Names);
                if (d.Key != "RSA 2048 bits · signature sha256RSA") return "key: " + d.Key;
                if (!d.Uses.Contains("Digital signature") || !d.Uses.Contains("Key encipherment") || !d.Uses.Contains("Server authentication") || !d.Uses.Contains("Client authentication")) return "uses: " + string.Join(",", d.Uses);
                if (d.Expiry != "214 days left") return "countdown: " + d.Expiry;
                if (d.HasPrivateKey) return "a public file claims a key";
                var pem = CertEngine.Load(fx.CertPem).Details; if (pem == null || pem.Sha1 != c.Thumbprint) return "PEM does not read the same";
                var soon = CertEngine.Load(fx.CertSoon).Details; if (soon.Expiry != "12 days left") return "soon: " + soon.Expiry;
                var old = CertEngine.Load(fx.CertExpired).Details; if (old.Expiry != "Expired 3 days ago") return "expired: " + old.Expiry;
                var withKey = CertEngine.Load(fx.CertPemKey).Details; if (withKey == null || !withKey.HasPrivateKey) return "a PEM with a key is not seen as holding one";
                return null;
            });
            await CheckAsync("certificate dialog: countdown, validity and warnings for valid, expiring and expired certificates", async () =>
            {
                var d = CertInspectorDialog.Open(fx.CertCer);
                try
                {
                    var t = DsText(d);
                    if (!t.Contains("214 days left") || !t.Contains("Digital signature") || !t.Contains("api.example.org")) return "valid certificate text is wrong";
                    if (d.Banner.Visibility == Visibility.Visible) return "a banner shows for a healthy certificate";
                }
                finally { DsClose(d); }
                d = CertInspectorDialog.Open(fx.CertSoon);
                try { if (d.Banner.Kind != BannerKind.Warning || !d.Banner.TitleText.StartsWith("Expires in 12 days")) return "expiring banner: " + d.Banner.TitleText; }
                finally { DsClose(d); }
                d = CertInspectorDialog.Open(fx.CertExpired);
                try { if (d.Banner.Kind != BannerKind.Error || !DsText(d).Contains("Expired 3 days ago")) return "expired state: " + d.Banner.TitleText; }
                finally { DsClose(d); }
                d = CertInspectorDialog.Open(Path.Combine(fx.Dir, "notes.txt"));
                try { if (d.Banner.Kind != BannerKind.Error || d.Details != null) return "a text file was accepted as a certificate"; }
                finally { DsClose(d); }
                return null;
            });
            await CheckAsync("certificate dialog: a password-protected PKCS#12 asks for the password, refuses a wrong one, opens with the right one and clears it", async () =>
            {
                var d = CertInspectorDialog.Open(fx.CertPfx);
                try
                {
                    if (d.ModeName != "Password" || d.PasswordField == null) return "no password step, mode " + d.ModeName;
                    if (!d.Banner.BodyText.Contains("never stored or written to the log")) return "the password promise is missing";
                    d.PasswordField.Password = "wrong";
                    DataToolWindow.Press(d.PrimaryButton);
                    if (d.ModeName != "Password" || d.Banner.Kind != BannerKind.Error) return "a wrong password was not refused: " + d.ModeName;
                    d.PasswordField.Password = fx.PfxPassword;
                    DataToolWindow.Press(d.PrimaryButton);
                    if (d.ModeName != "Details" || d.Details == null || !d.Details.HasPrivateKey) return "the right password did not open it: " + d.ModeName;
                    if (DsText(d).Contains(fx.PfxPassword)) return "the password is on screen";
                    if (d.PasswordField.Password.Length != 0) return "the password was kept";
                    return null;
                }
                finally { DsClose(d); }
            });
            await CheckAsync("certificate dialog: converts between PEM, DER and PKCS#12, and a private key is written only with the switch on and a new password", async () =>
            {
                // a public certificate: DER and PEM copies carry no key, and the key switch is off
                var d = CertInspectorDialog.Open(fx.CertCer);
                string pemOut, derOut;
                try
                {
                    DataToolWindow.Press(d.FindButton("Convert…"));
                    if (d.ModeName != "Convert") return "Convert… did not open the format step";
                    if (d.IncludeKeySwitch.IsEnabled) return "the key switch is on for a public file";
                    d.FormatChoice.Select(0, true);
                    if (d.SaveAsBox.Text != "api.example.org.pem") d.SaveAsBox.Text = "api.example.org.pem";
                    d.SaveAsBox.Text = "converted-public.pem";
                    DataToolWindow.Press(d.PrimaryButton);
                    pemOut = d.Written; if (pemOut == null || !File.Exists(pemOut)) return "no PEM written";
                }
                finally { DsClose(d); }
                var pemText = File.ReadAllText(pemOut);
                if (!pemText.Contains("BEGIN CERTIFICATE") || pemText.Contains("PRIVATE KEY")) return "the PEM is wrong or holds a key";
                d = CertInspectorDialog.Open(fx.CertCer);
                try
                {
                    DataToolWindow.Press(d.FindButton("Convert…"));
                    d.FormatChoice.Select(1, true);
                    d.SaveAsBox.Text = "converted-public.cer";
                    DataToolWindow.Press(d.PrimaryButton);
                    derOut = d.Written;
                }
                finally { DsClose(d); }
                if (new X509Certificate2(File.ReadAllBytes(derOut)).Thumbprint != fx.Cert.Thumbprint) return "the DER copy is a different certificate";
                if (new X509Certificate2(File.ReadAllBytes(pemOut)).Thumbprint != fx.Cert.Thumbprint) return "the PEM copy is a different certificate";
                // a source with a private key
                d = CertInspectorDialog.Open(fx.CertPfx);
                try
                {
                    d.PasswordField.Password = fx.PfxPassword; DataToolWindow.Press(d.PrimaryButton);
                    DataToolWindow.Press(d.FindButton("Convert…"));
                    if (!d.IncludeKeySwitch.IsEnabled) return "the key switch is off for a source with a key";
                    d.FormatChoice.Select(1, true);
                    if (d.IncludeKeySwitch.IsEnabled) return "the key switch is on for DER";
                    d.FormatChoice.Select(2, true);
                    d.SaveAsBox.Text = "no-key.pfx";
                    DataToolWindow.Press(d.PrimaryButton);
                    var noKey = d.Written; if (noKey == null) return "a public-only PKCS#12 was not written";
                    var pub = new X509Certificate2(noKey, (string)null, X509KeyStorageFlags.EphemeralKeySet);
                    if (pub.HasPrivateKey) return "the public-only PKCS#12 holds a key";
                }
                finally { DsClose(d); }
                d = CertInspectorDialog.Open(fx.CertPfx);
                try
                {
                    d.PasswordField.Password = fx.PfxPassword; DataToolWindow.Press(d.PrimaryButton);
                    DataToolWindow.Press(d.FindButton("Convert…"));
                    d.FormatChoice.Select(2, true);
                    d.IncludeKeySwitch.IsChecked = true;
                    d.SaveAsBox.Text = "with-key.pfx";
                    d.NewPasswordField.Password = "abc";
                    DataToolWindow.Press(d.PrimaryButton);
                    if (d.Written != null) return "a key was written with a weak password";
                    d.NewPasswordField.Password = "new-secret-9";
                    DataToolWindow.Press(d.PrimaryButton);
                    var withKey = d.Written; if (withKey == null || !File.Exists(withKey)) return "no PKCS#12 with a key written";
                    var loaded = new X509Certificate2(withKey, "new-secret-9", X509KeyStorageFlags.EphemeralKeySet);
                    if (!loaded.HasPrivateKey) return "the key is missing from the copy";
                    try { new X509Certificate2(withKey, "wrong", X509KeyStorageFlags.EphemeralKeySet); return "the copy opens with a wrong password"; } catch (CryptographicException) { }
                    if (!DsText(d).Contains("contains the private key")) return "the confirmation does not say a key is inside";
                }
                finally { DsClose(d); }
                // PEM with a key is encrypted
                d = CertInspectorDialog.Open(fx.CertPemKey);
                try
                {
                    DataToolWindow.Press(d.FindButton("Convert…"));
                    d.FormatChoice.Select(0, true);
                    d.IncludeKeySwitch.IsChecked = true;
                    d.SaveAsBox.Text = "key-copy.pem";
                    d.NewPasswordField.Password = "pem-secret-7";
                    DataToolWindow.Press(d.PrimaryButton);
                    var text = d.Written == null ? "" : File.ReadAllText(d.Written);
                    return text.Contains("BEGIN ENCRYPTED PRIVATE KEY") && !text.Contains("BEGIN PRIVATE KEY") ? null : "the PEM key is not encrypted";
                }
                finally { DsClose(d); }
            });
        }

        // ================================================================== data

        private static async Task DataChecks(DataSecFixtures fx)
        {
            Check("data: the CSV reader handles quotes, embedded commas and newlines, a byte-order mark and detects the separator", () =>
            {
                var p = DataEngine.Probe(fx.CsvFile);
                if (!p.Valid || p.Format != DataKind.Csv) return "not valid: " + p.ErrorMessage;
                if (p.Rows != 5 || p.Columns != 5 || !p.HeaderFound || p.Header[0] != "id") return $"rows {p.Rows}, columns {p.Columns}, header {p.HeaderFound}";
                if (p.Preview[1][1] != "Ortega, Luis" || p.Preview[3][1] != "Sam \"The Man\" Okafor" || p.Preview[4][1] != "Ines\nRossi") return "quoted fields: " + p.Preview[1][1] + " | " + p.Preview[3][1] + " | " + p.Preview[4][1];
                if (DataEngine.Probe(fx.CsvSemi).Delimiter != ';' || DataEngine.Probe(fx.CsvSemi).Columns != 3) return "semicolon not detected";
                if (DataEngine.Probe(fx.CsvTab).Delimiter != '\t') return "tab not detected";
                var bad = DataEngine.Probe(fx.CsvBad);
                return !bad.Valid && bad.ErrorLine == 2 && bad.ErrorMessage.Contains("never closed") ? null : "an unclosed quote: valid " + bad.Valid + " line " + bad.ErrorLine + " " + bad.ErrorMessage;
            });
            Check("data: invalid JSON and YAML name the line and column of the first error, with three lines of context", () =>
            {
                var j = DataEngine.Probe(fx.JsonBad);
                if (j.Valid || j.ErrorLine != 5 || j.ErrorColumn < 1) return $"JSON: valid {j.Valid} line {j.ErrorLine} column {j.ErrorColumn} {j.ErrorMessage}";
                if (j.Context.Length < 3 || !j.Context.Any(l => l.Contains("\"path\""))) return "no context lines";
                if (!DataEngine.Probe(fx.JsonOk).Valid) return "a valid JSON file was refused";
                var y = DataEngine.Probe(fx.YamlBad);
                if (y.Valid || y.ErrorLine < 1) return $"YAML: valid {y.Valid} line {y.ErrorLine} {y.ErrorMessage}";
                var dup = DataEngine.Probe(fx.YamlDup);
                if (dup.Valid) return "a duplicate YAML key was accepted";
                return DataEngine.Probe(fx.YamlOk).Valid ? null : "a valid YAML file was refused: " + DataEngine.Probe(fx.YamlOk).ErrorMessage;
            });
            Check("data: CSV converts to JSON and YAML with numbers typed by column, text kept as text, and the whole file used", () =>
            {
                var p = DataEngine.Probe(fx.CsvFile);
                var stats = new ConvertStats();
                var jsonPath = DataEngine.ConvertFile(p, DataKind.Json, true, Path.Combine(fx.Dir, "conv-orders.json"), stats);
                var tree = JsonNode.Parse(File.ReadAllText(jsonPath)) as JsonArray;
                if (tree == null || tree.Count != 5 || stats.Items != 5) return "rows: " + stats.Items;
                var first = tree[0] as JsonObject;
                if (first["id"]?.GetValueKind() != JsonValueKind.String || first["id"].GetValue<string>() != "1001") return "id was not kept as text";
                if (first["total"]?.GetValueKind() != JsonValueKind.Number) return "total was not a number";
                if (!File.ReadAllText(jsonPath).Contains("128.50")) return "the number's written digits changed";
                if (tree[4]["name"].GetValue<string>() != "Ines\nRossi" || tree[1]["name"].GetValue<string>() != "Ortega, Luis") return "quoted text lost";
                if (!stats.Numbers.StartsWith("total kept as numbers") || !stats.Numbers.Contains("id")) return "numbers note: " + stats.Numbers;
                var compact = DataEngine.ConvertFile(p, DataKind.Json, false, Path.Combine(fx.Dir, "conv-orders-compact.json"), new ConvertStats());
                if (File.ReadAllText(compact).Contains("\n  ") || !File.ReadAllText(jsonPath).Contains("\n  ")) return "pretty and compact look the same";
                var yamlPath = DataEngine.ConvertFile(p, DataKind.Yaml, true, Path.Combine(fx.Dir, "conv-orders.yaml"), new ConvertStats());
                var y = DataEngine.ParseYaml(File.ReadAllText(yamlPath), out _) as JsonArray;
                if (y == null || y.Count != 5) return "YAML rows";
                if (!DsSame(y, tree)) return "YAML and JSON differ: " + y.ToJsonString() + " vs " + tree.ToJsonString();
                var big = DataEngine.Probe(fx.CsvBig);
                if (big.Rows != 1200 || big.Preview.Count != 500) return $"the big file: rows {big.Rows}, shown {big.Preview.Count}";
                var bigStats = new ConvertStats();
                var bigJson = DataEngine.ConvertFile(big, DataKind.Json, false, Path.Combine(fx.Dir, "conv-big.json"), bigStats);
                return (JsonNode.Parse(File.ReadAllText(bigJson)) as JsonArray)?.Count == 1200 && bigStats.Items == 1200 ? null : "conversion stopped at the preview";
            });
            Check("data: JSON, YAML and CSV convert into each other without losing values, and an unsuitable shape is refused in plain words", () =>
            {
                var j = DataEngine.Probe(fx.JsonList);
                var csv = DataEngine.ConvertFile(j, DataKind.Csv, true, Path.Combine(fx.Dir, "people-from-json.csv"), new ConvertStats());
                var text = File.ReadAllText(csv);
                if (!text.StartsWith("id,name,age,city") || !text.Contains("007,Ava,31,") || !text.Contains("008,Luis,42,Madrid")) return "JSON to CSV: " + text;
                var yaml = DataEngine.ConvertFile(j, DataKind.Yaml, true, Path.Combine(fx.Dir, "people-from-json.yaml"), new ConvertStats());
                var back = DataEngine.Probe(yaml);
                if (!back.Valid || !DsSame(back.Tree, j.Tree)) return "JSON to YAML to JSON lost something: " + File.ReadAllText(yaml);
                if (!File.ReadAllText(yaml).Contains("'007'") && !File.ReadAllText(yaml).Contains("\"007\"")) return "the text value 007 is not quoted in YAML, so it would turn into a number";
                var y = DataEngine.Probe(fx.YamlList);
                var asJson = DataEngine.ConvertFile(y, DataKind.Json, true, Path.Combine(fx.Dir, "people-from-yaml.json"), new ConvertStats());
                var yt = JsonNode.Parse(File.ReadAllText(asJson)) as JsonArray;
                if (yt == null || yt[0]["id"].GetValue<string>() != "007" || yt[1]["active"].GetValue<bool>() != true || yt[0]["age"].GetValue<long>() != 31) return "YAML types: " + File.ReadAllText(asJson);
                try { DataEngine.ConvertFile(DataEngine.Probe(fx.JsonOk), DataKind.Csv, true, Path.Combine(fx.Dir, "bad-shape.csv"), new ConvertStats()); return "an object was written as a table"; }
                catch (InvalidOperationException ex) { if (!ex.Message.Contains("list of records")) return "message: " + ex.Message; }
                if (File.Exists(Path.Combine(fx.Dir, "bad-shape.csv")) || File.Exists(Path.Combine(fx.Dir, "bad-shape.csv.part"))) return "a refused conversion left a file behind";
                return null;
            });
            await CheckAsync("data viewer: a valid CSV shows the first 500 rows, switches to text, changes separator and converts to YAML under a new name", async () =>
            {
                var d = DataViewerDialog.Open(fx.CsvBig);
                try
                {
                    if (!await DsWait(d.Ready)) return "never loaded";
                    if (d.Banner.Kind != BannerKind.Success || d.Banner.TitleText != "Valid CSV" || !d.Banner.BodyText.Contains("first 500 rows")) return "banner: " + d.Banner.TitleText + " / " + d.Banner.BodyText;
                    if (d.RowsShown != 500) return "rows shown " + d.RowsShown;
                    if (!d.SummaryText.Contains("1,200 rows")) return "summary: " + d.SummaryText;
                    // searching narrows the rows
                    d.Search.State.SetQuery("Row 12");
                    var filtered = d.RowsShown; d.Search.State.SetQuery("");
                    if (filtered == 0 || filtered >= 500) return "search left " + filtered + " rows";
                    d.ViewChoice.Select(1, true);
                    if (d.TextBoxView == null || !d.TextBoxView.Text.StartsWith("id,name,amount")) return "text view";
                    d.ViewChoice.Select(0, true);
                    // semicolon separator reparses into one wide column
                    d.ResetReloaded(); DsChip(d, "Semicolon");
                    if (!await DsWait(d.Reloaded)) return "no reload";
                    if (d.Probe.Delimiter != ';' || d.Probe.Columns != 1) return "separator not applied: " + d.Probe.Delimiter + " " + d.Probe.Columns;
                    d.ResetReloaded(); DsChip(d, "Comma"); await DsWait(d.Reloaded);
                    if (d.Probe.Columns != 3) return "comma did not restore 3 columns";
                    d.TargetChoice.Select(1, true);
                    if (d.SaveAsBox.Text != "big-orders.yaml") return "default name " + d.SaveAsBox.Text;
                    d.SaveAsBox.Text = "big-orders-converted.yaml";
                    d.ResetConverted();
                    DataToolWindow.Press(d.PrimaryButton);
                    if (!await DsWait(d.WaitConverted())) return "conversion never finished";
                    if (d.Written == null || !File.Exists(d.Written) || Path.GetFileName(d.Written) != "big-orders-converted.yaml") return "output " + d.Written;
                    var y = DataEngine.Probe(d.Written);
                    if (!y.Valid || (y.Tree as JsonArray)?.Count != 1200) return "the YAML has " + (y.Tree as JsonArray)?.Count + " rows";
                    if (!d.Banner.TitleText.StartsWith("Saved big-orders-converted.yaml") || !d.Banner.BodyText.Contains("1,200 rows became 1,200 objects")) return "done banner: " + d.Banner.TitleText + " / " + d.Banner.BodyText;
                    if (!File.Exists(fx.CsvBig) || File.ReadAllText(fx.CsvBig).Length < 1000) return "the original changed";
                    return null;
                }
                finally { DsClose(d); }
            });
            await CheckAsync("data viewer: an invalid JSON file shows line and column, three lines of context, turns Convert off, and Go to the error selects the line", async () =>
            {
                var d = DataViewerDialog.Open(fx.JsonBad);
                try
                {
                    await DsWait(d.Ready);
                    if (d.Banner.Kind != BannerKind.Error || !d.Banner.TitleText.StartsWith("Invalid JSON at line 5, column")) return "banner: " + d.Banner.TitleText;
                    if (!d.Banner.BodyText.Contains("Nothing can be converted until it is fixed")) return "no stop notice";
                    if (d.PrimaryButton.IsEnabled) return "Convert is on for an invalid file";
                    if (!DsText(d).Contains("\"path\"")) return "the offending line is not shown";
                    var go = d.FindButton("Go to the error"); if (go == null || d.FindButton("Copy message") == null) return "missing actions";
                    DataToolWindow.Press(go);
                    await Task.Delay(100);
                    if (d.TextBoxView == null || !d.TextBoxView.SelectedText.Contains("\"path\"")) return "the error line is not selected: '" + d.TextBoxView?.SelectedText + "'";
                    return null;
                }
                finally { DsClose(d); }
            });
            await CheckAsync("data viewer: YAML opens, shows its text, converts to JSON, and Format in place rewrites JSON after saving a backup", async () =>
            {
                var d = DataViewerDialog.Open(fx.YamlOk);
                try
                {
                    await DsWait(d.Ready);
                    if (d.Banner.TitleText != "Valid YAML" || !d.Banner.BodyText.Contains("2 levels")) return "banner: " + d.Banner.TitleText + " / " + d.Banner.BodyText;
                    d.ViewChoice.Select(1, true);
                    if (!d.TextBoxView.Text.Contains("host: example.org")) return "text view";
                    d.SaveAsBox.Text = "config-converted.json";
                    d.ResetConverted();
                    DataToolWindow.Press(d.PrimaryButton);
                    if (!await DsWait(d.WaitConverted())) return "never finished";
                    var t = JsonNode.Parse(File.ReadAllText(d.Written)) as JsonObject;
                    if (t == null || t["server"]["port"].GetValue<long>() != 8080 || t["server"]["tls"].GetValue<bool>() != true || t["features"].AsArray().Count != 3) return "converted content: " + File.ReadAllText(d.Written);
                }
                finally { DsClose(d); }
                var ugly = Path.Combine(fx.Dir, "ugly.json"); var original = "{\"a\":1,\"b\":[1,2,{\"c\":null}]}";
                File.WriteAllText(ugly, original);
                d = DataViewerDialog.Open(ugly);
                try
                {
                    await DsWait(d.Ready);
                    var backup = d.FormatInPlaceNow();
                    if (backup == null || File.ReadAllText(backup) != original) return "no faithful backup";
                    var now = File.ReadAllText(ugly);
                    return now.Contains("\n  \"a\": 1") && JsonNode.DeepEquals(JsonNode.Parse(now), JsonNode.Parse(original)) ? null : "formatted text: " + now;
                }
                finally { DsClose(d); }
            });
        }

        private static void DsChip(Window w, string label)
        {
            var rb = DataToolWindow.Descend<System.Windows.Controls.RadioButton>(w).FirstOrDefault(r => (r.Content as string) == label);
            if (rb == null) throw new InvalidOperationException("no chip " + label);
            rb.IsChecked = true;
        }

        // ================================================================== encoding

        private static async Task EncodingChecks(DataSecFixtures fx)
        {
            Check("encoding: detects UTF-8, UTF-8 with BOM, UTF-16 with and without BOM, a legacy code page as a guess, and binary", () =>
            {
                var u8 = TextEngine.Analyse(fx.EncUtf8); if (u8.EncodingName != "UTF-8" || u8.Ends != LineEnds.Lf || !u8.Ascii) return "UTF-8: " + u8.EncodingName + " " + u8.Ends;
                var bom = TextEngine.Analyse(fx.EncBom); if (bom.EncodingName != "UTF-8 BOM" || bom.Ends != LineEnds.CrLf || bom.Crlf != 3) return "BOM: " + bom.EncodingName + " " + bom.Ends + " " + bom.Crlf;
                var le = TextEngine.Analyse(fx.EncU16le); if (le.EncodingName != "UTF-16 LE" || !le.Bom || le.Ends != LineEnds.CrLf) return "UTF-16 LE: " + le.EncodingName + " " + le.Ends;
                var be = TextEngine.Analyse(fx.EncU16be); if (be.EncodingName != "UTF-16 BE" || be.Ends != LineEnds.Lf) return "UTF-16 BE: " + be.EncodingName + " " + be.Ends;
                var nb = TextEngine.Analyse(fx.EncU16leNoBom); if (nb.EncodingName != "UTF-16 LE" || nb.Bom || nb.Binary) return "UTF-16 LE without BOM: " + nb.EncodingName + " binary " + nb.Binary;
                var an = TextEngine.Analyse(fx.EncAnsi); if (!an.Guess || !an.EncodingName.Contains("(guess)") || an.Binary || an.Ends != LineEnds.CrLf) return "legacy: " + an.EncodingName;
                var mx = TextEngine.Analyse(fx.EncMixed); if (mx.Ends != LineEnds.Mixed || mx.Crlf != 2 || mx.Lf != 7) return "mixed: " + mx.Ends + " crlf " + mx.Crlf + " lf " + mx.Lf;
                if (!TextEngine.Analyse(fx.Png).Binary) return "a PNG is not binary";
                if (!TextEngine.Analyse(fx.NulText).Binary) return "a .txt full of NUL bytes is not binary";
                var bad = TextEngine.Analyse(fx.EncAnsi, 65001);
                return bad.DecodeError != null && bad.DecodeErrorByte == 0xE9 && bad.DecodeErrorPosition == 4 ? null : "choosing UTF-8 for a Windows-1252 file: error " + bad.DecodeError + " byte " + bad.DecodeErrorByte + " at " + bad.DecodeErrorPosition;
            });
            Check("encoding: conversion decodes strictly and changes only what was asked, with a backup and an undo", () =>
            {
                var p = Path.Combine(fx.Dir, "conv-enc.txt");
                File.Copy(fx.EncAnsi, p, true);
                var before = File.ReadAllBytes(p);
                var info = TextEngine.Analyse(p);
                var r = TextEngine.Convert(info, TargetEncoding.Utf8, TargetEnds.Lf, true);
                if (!r.Changed || r.Failed) return "not converted: " + r.Message;
                var after = File.ReadAllText(p, new UTF8Encoding(false, true));
                if (after.Contains('\r') || !after.Contains("Café") || !after.Contains("€")) return "content: " + after;
                var again = TextEngine.Analyse(p);
                if (again.EncodingName != "UTF-8" || again.Ends != LineEnds.Lf) return "after: " + again.EncodingName;
                if (!File.ReadAllBytes(r.Backup).SequenceEqual(before)) return "the backup differs from the original";
                if (!TextEngine.Restore(p, r.Backup) || !File.ReadAllBytes(p).SequenceEqual(before) || File.Exists(r.Backup)) return "undo did not restore the original";
                var noBackup = TextEngine.Convert(TextEngine.Analyse(p), TargetEncoding.Utf16, TargetEnds.Keep, false);
                if (!noBackup.Changed || noBackup.Backup != null || Directory.GetFiles(fx.Dir, "conv-enc.txt.bak*").Length != 0) return "a backup was made although it was off";
                var u16 = TextEngine.Analyse(p); if (u16.EncodingName != "UTF-16 LE" || !u16.Bom || u16.Ends != LineEnds.CrLf) return "UTF-16 result: " + u16.EncodingName + " " + u16.Ends;
                var skip = TextEngine.Convert(TextEngine.Analyse(fx.EncUtf8), TargetEncoding.Utf8, TargetEnds.Keep, true);
                if (skip.Changed || !skip.Message.Contains("already matches") || File.Exists(fx.EncUtf8 + ".bak")) return "an unchanged file was rewritten";
                var bin = File.ReadAllBytes(fx.Png);
                var br = TextEngine.Convert(TextEngine.Analyse(fx.Png), TargetEncoding.Utf8, TargetEnds.Lf, true);
                return br.Failed && File.ReadAllBytes(fx.Png).SequenceEqual(bin) ? null : "a binary file was touched";
            });
            await CheckAsync("encoding dialog: a batch converts the ticked text files, skips a binary one, and Undo from backups restores every original byte for byte", async () =>
            {
                var names = new[] { "b-ansi.txt", "b-u16.txt", "b-mixed.sh", "b-ok.csv", "b-logo.png" };
                var src = new[] { fx.EncAnsi, fx.EncU16le, fx.EncMixed, fx.EncUtf8, fx.Png };
                var files = names.Select((n, i) => { var p = Path.Combine(fx.Dir, n); File.Copy(src[i], p, true); return p; }).ToArray();
                var originals = files.Select(File.ReadAllBytes).ToArray();
                var d = EncodingLinesDialog.Open(files);
                try
                {
                    var infos = d.Infos;
                    if (infos.Count != 5 || !infos[4].Binary) return "the batch was not analysed";
                    if (!DsText(d).Contains("Mixed ends") || !DsText(d).Contains("Skipped, not text")) return "the result badges are missing";
                    d.SelectRow(2);
                    if (!d.Banner.TitleText.Contains("mixes line endings") || !d.Banner.BodyText.Contains("2 with CRLF")) return "mixed warning: " + d.Banner.BodyText;
                    d.ShowMixed(true);
                    if (!d.PreviewText.Contains("#!/bin/sh") || !d.PreviewText.Contains("run --fast")) return "mixed lines preview: " + d.PreviewText;
                    d.ShowMixed(false);
                    d.SelectRow(0);
                    if (!d.PreviewText.Contains("Café")) return "preview of the legacy file: " + d.PreviewText;
                    d.EncodingChoice.Select(0, true); d.EndsChoice.Select(1, true);
                    var go = d.PrimaryButton;
                    if (!go.IsEnabled || !LabelOfButton(d, go).StartsWith("Convert 3 files")) return "primary button: " + LabelOfButton(d, go);
                    DataToolWindow.Press(go);
                    if (!await DsWait(d.WaitConverted())) return "never finished";
                    if (d.Results.Count != 3 || d.Results.Any(r => !r.Changed)) return "results: " + string.Join(" | ", d.Results.Select(r => r.Message));
                    foreach (var p in files.Take(3))
                    {
                        var i = TextEngine.Analyse(p);
                        if (i.EncodingName != "UTF-8" || i.Bom || (i.Ends != LineEnds.Lf)) return Path.GetFileName(p) + " is " + i.EncodingName + " " + i.Ends;
                        if (!File.Exists(p + ".bak")) return "no backup of " + p;
                    }
                    if (File.Exists(files[3] + ".bak") || !File.ReadAllBytes(files[3]).SequenceEqual(originals[3])) return "an unchanged file was touched";
                    if (!File.ReadAllBytes(files[4]).SequenceEqual(originals[4])) return "the binary file was touched";
                    if (!d.Banner.TitleText.StartsWith("Converted 3 files") || !DsText(d).Contains("Windows-1252")) return "done state: " + d.Banner.TitleText;
                    if (d.UndoBackups() != 3) return "undo restored a different number";
                    for (int i = 0; i < files.Length; i++) if (!File.ReadAllBytes(files[i]).SequenceEqual(originals[i])) return Path.GetFileName(files[i]) + " is not back to its original bytes";
                    if (files.Any(p => File.Exists(p + ".bak"))) return "backups were left behind";
                    return null;
                }
                finally { DsClose(d); }
            });
            await CheckAsync("encoding dialog: bytes that are not valid in the chosen encoding stop that file, write nothing, and choosing the right encoding by hand fixes it", async () =>
            {
                var p = Path.Combine(fx.Dir, "export-legacy-copy.txt");
                File.Copy(fx.EncAnsi, p, true);
                var before = File.ReadAllBytes(p);
                var d = EncodingLinesDialog.Open(new[] { p });
                try
                {
                    d.ReadAs(0, 65001);
                    if (d.Banner.Kind != BannerKind.Error || !d.Banner.TitleText.Contains("Could not convert without losing characters") || !d.Banner.BodyText.Contains("0xE9") || !d.Banner.BodyText.Contains("position 4")) return "error banner: " + d.Banner.TitleText + " / " + d.Banner.BodyText;
                    if (d.PrimaryButton.IsEnabled) return "Convert is on for an undecodable file";
                    if (!DsText(d).Contains("Nothing was written")) return "no note about what was not changed";
                    if (!File.ReadAllBytes(p).SequenceEqual(before)) return "the file changed";
                    d.ReadAs(0, 1252);
                    if ((d.Banner.Visibility == Visibility.Visible && d.Banner.Kind == BannerKind.Error) || !d.PrimaryButton.IsEnabled) return "choosing Windows-1252 did not fix it";
                    return null;
                }
                finally { DsClose(d); }
            });
        }

        private static string LabelOfButton(Window w, System.Windows.Controls.Button b) => DataToolWindow.LabelOf(b);

        // ================================================================== clipboard notice buttons

        private static async Task DsToastChecks(DataSecFixtures fx)
        {
            await CheckAsync("clipboard notice: copied files get the buttons for the right dialogs, and each button opens its dialog", async () =>
            {
                string[] Labels(params string[] files) => ActionProvider.For(new ClipItem { Kind = ClipKind.Files, Files = files }).Select(a => a.Label).ToArray();
                bool Has(string[] l, string s) => l.Contains(s);
                var exe = Labels(fx.Unsigned ?? fx.SystemSigned);
                if (!Has(exe, "Who signed this?") || !Has(exe, "Hash and verify") || Has(exe, "Inspect certificate") || Has(exe, "View data")) return "program: " + string.Join(", ", exe);
                var cer = Labels(fx.CertCer); if (!Has(cer, "Inspect certificate") || !Has(cer, "Hash and verify")) return "certificate: " + string.Join(", ", cer);
                var csv = Labels(fx.CsvFile); if (!Has(csv, "View data") || !Has(csv, "Encoding and line endings")) return "CSV: " + string.Join(", ", csv);
                var yml = Labels(fx.YamlOk); if (!Has(yml, "View data")) return "YAML: " + string.Join(", ", yml);
                var txt = Labels(fx.EncAnsi); if (!Has(txt, "Encoding and line endings") || Has(txt, "View data")) return "text: " + string.Join(", ", txt);
                var many = Labels(fx.EncAnsi, fx.EncUtf8); if (!Has(many, "Encoding and line endings") || Has(many, "Hash and verify")) return "two text files: " + string.Join(", ", many);
                var png = Labels(fx.Png); if (Has(png, "Encoding and line endings") || !Has(png, "Hash and verify")) return "PNG: " + string.Join(", ", png);
                var at = ActionProvider.For(new ClipItem { Kind = ClipKind.Files, Files = new[] { fx.CsvFile } });
                var labels = at.Select(a => a.Label).ToList();
                if (labels.IndexOf("View data") != labels.IndexOf("Show in folder") + 1) return "the buttons are not placed after Show in folder: " + string.Join(", ", labels);
                foreach (var (label, test) in new (string, Func<bool>)[]
                {
                    ("View data", () => DataViewerDialog.Current != null), ("Encoding and line endings", () => EncodingLinesDialog.Current != null), ("Hash and verify", () => HashVerifyDialog.Current != null),
                })
                {
                    at.First(a => a.Label == label).Run();
                    if (!await DsUntil(test, 3000)) return label + " did not open its dialog";
                    DsCloseAll();
                }
                ActionProvider.For(new ClipItem { Kind = ClipKind.Files, Files = new[] { fx.CertCer } }).First(a => a.Label == "Inspect certificate").Run();
                if (!await DsUntil(() => CertInspectorDialog.Current != null, 3000)) return "Inspect certificate did not open";
                DsCloseAll();
                if (fx.Unsigned != null)
                {
                    ActionProvider.For(new ClipItem { Kind = ClipKind.Files, Files = new[] { fx.Unsigned } }).First(a => a.Label == "Who signed this?").Run();
                    if (!await DsUntil(() => WhoSignedDialog.Current != null, 3000)) return "Who signed this? did not open";
                    DsCloseAll();
                }
                return null;
            });
        }

        // ================================================================== Cantonese

        private static async Task<string> DsLanguageAudit(DataSecFixtures fx, string dir)
        {
            var saved = Settings.Current.LanguageMode;
            try
            {
                Settings.Current.LanguageMode = LanguageMode.Cantonese;
                Core.Language.Refresh();
                LanguageAudit.Reset();
                foreach (var (name, open) in DsScenes(fx))
                {
                    var w = open();
                    await Task.Delay(600);
                    if (w is DataViewerDialog dv) await DsWait(dv.Ready);
                    await Task.Delay(300);
                    LanguageAudit.Scan(name, w);
                    DsClose(w);
                }
                var left = LanguageAudit.Findings;
                File.WriteAllLines(Path.Combine(dir, "datasec-untranslated.txt"), left.Select(x => x.Text.Replace("\n", " / ") + "\t" + x.Where));
                return left.Count == 0 ? null : left.Count + " untranslated: " + string.Join(" | ", left.Take(60).Select(x => "\"" + x.Text.Replace("\n", " / ") + "\" (" + x.Where + ")"));
            }
            finally { Settings.Current.LanguageMode = saved; Core.Language.Refresh(); DsCloseAll(); }
        }

        /// <summary>Every dialog in each state the checks and captures use: the same list serves the language audit.</summary>
        internal static IEnumerable<(string Name, Func<Window> Open)> DsScenes(DataSecFixtures fx)
        {
            yield return ("hash ready", () => { var d = HashVerifyDialog.Open(fx.Hashed); return d; });
            if (fx.SystemSigned != null) yield return ("signed", () => WhoSignedDialog.Open(fx.SystemSigned));
            if (fx.Unsigned != null) yield return ("unsigned", () => WhoSignedDialog.Open(fx.Unsigned));
            if (fx.SelfSigned != null) yield return ("untrusted", () => WhoSignedDialog.Open(fx.SelfSigned));
            if (fx.Broken != null) yield return ("broken", () => WhoSignedDialog.Open(fx.Broken));
            yield return ("certificate valid", () => CertInspectorDialog.Open(fx.CertCer));
            yield return ("certificate soon", () => CertInspectorDialog.Open(fx.CertSoon));
            yield return ("certificate expired", () => CertInspectorDialog.Open(fx.CertExpired));
            yield return ("certificate password", () => CertInspectorDialog.Open(fx.CertPfx));
            yield return ("certificate convert", () => { var d = CertInspectorDialog.Open(fx.CertCer); DataToolWindow.Press(d.FindButton("Convert…")); return d; });
            yield return ("data csv", () => DataViewerDialog.Open(fx.CsvFile));
            yield return ("data json error", () => DataViewerDialog.Open(fx.JsonBad));
            yield return ("data yaml", () => { var d = DataViewerDialog.Open(fx.YamlOk); return d; });
            yield return ("data csv error", () => DataViewerDialog.Open(fx.CsvBad));
            yield return ("encoding batch", () => EncodingLinesDialog.Open(new[] { fx.EncAnsi, fx.EncU16le, fx.EncMixed, fx.EncUtf8, fx.Png }));
            yield return ("encoding mixed", () => { var d = EncodingLinesDialog.Open(new[] { fx.EncMixed }); return d; });
            yield return ("encoding cannot decode", () => { var d = EncodingLinesDialog.Open(new[] { fx.EncAnsi }); d.ReadAs(0, 65001); return d; });
        }
    }
}
