using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MonitoringTools.Core;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    public static partial class ScreenshotRunner
    {
        /// <summary>The hash, signature, certificate, data and encoding dialogs, each in every state the design shows, on files made for the capture.</summary>
        private static async Task DataSecScenes()
        {
            var work = Path.Combine(Settings.DataDir, "datasec-capture");
            var fx = DataSecFixtures.Create(work, signing: true, big: false);
            var sparse = Path.Combine(work, "ubuntu-24.04.iso");
            using (var fs = new FileStream(sparse, FileMode.Create)) fs.SetLength(3L * 1024 * 1024 * 1024);

            async Task<T> Wait<T>(T w, Task ready) where T : Window { await Task.WhenAny(ready, Task.Delay(20000)); return w; }

            // ---- hash and verify
            var h = HashVerifyDialog.Open(sparse);
            await Shot(h, "datasec-hash-computing", "Hash and verify: reading a 3 GB file, with progress and Cancel", 900);
            await Task.WhenAny(h.Ready, Task.Delay(60000));
            await Shot(h, "datasec-hash-ready", "Hash and verify: the SHA-256 in groups of eight, ready for a pasted checksum", 400);
            var want = h.ShownHash("SHA-256");
            h.ExpectedBox.Text = want.ToUpperInvariant();
            DataToolWindow.Press(h.PrimaryButton);
            await Shot(h, "datasec-hash-match", "Hash and verify: it matches the publisher's checksum", 400);
            var bad = want.ToCharArray(); bad[8] = bad[8] == 'a' ? 'b' : 'a';
            h.ExpectedBox.Text = new string(bad);
            DataToolWindow.Press(h.PrimaryButton);
            await Shot(h, "datasec-hash-mismatch", "Hash and verify: it does not match, with the first differing group marked", 400);
            h.Close(); await Task.Delay(300);

            // ---- who signed this
            async Task Sig(string name, string caption, string file)
            {
                if (file == null) return;
                var d = WhoSignedDialog.Open(file);
                await Wait(d, d.Ready);
                await Shot(d, name, caption, 500);
                d.Close(); await Task.Delay(250);
            }
            await Sig("datasec-signed-valid", "Who signed this?: a signed system file, valid, with its certificate chain", fx.SystemSigned);
            await Sig("datasec-signed-none", "Who signed this?: a file that is not signed", fx.Unsigned);
            await Sig("datasec-signed-broken", "Who signed this?: the file changed after it was signed", fx.Broken);
            await Sig("datasec-signed-untrusted", "Who signed this?: signed, but the issuer is not trusted on this computer", fx.SelfSigned);

            // ---- certificate inspector
            var c = CertInspectorDialog.Open(fx.CertCer); await Shot(c, "datasec-cert-valid", "Certificate inspector: a valid certificate with its countdown", 500); c.Close(); await Task.Delay(250);
            c = CertInspectorDialog.Open(fx.CertSoon); await Shot(c, "datasec-cert-expiring", "Certificate inspector: expiring soon", 500); c.Close(); await Task.Delay(250);
            c = CertInspectorDialog.Open(fx.CertExpired); await Shot(c, "datasec-cert-expired", "Certificate inspector: expired", 500); c.Close(); await Task.Delay(250);
            c = CertInspectorDialog.Open(fx.CertPfx); c.PasswordField.Password = "wrong"; await Shot(c, "datasec-cert-password", "Certificate inspector: a PKCS#12 file asks for its password", 500);
            c.PasswordField.Password = fx.PfxPassword; DataToolWindow.Press(c.PrimaryButton); await Task.Delay(300);
            DataToolWindow.Press(c.FindButton("Convert…")); c.FormatChoice.Select(2, true); c.IncludeKeySwitch.IsChecked = true; c.NewPasswordField.Password = "capture-secret";
            await Shot(c, "datasec-cert-convert", "Certificate inspector: convert to PKCS#12 with the private key and a new password", 500);
            c.Close(); await Task.Delay(250);

            // ---- data viewer and converter
            var v = DataViewerDialog.Open(fx.CsvFile); await Wait(v, v.Ready); await Shot(v, "datasec-data-table", "Data viewer: a valid CSV as a table with the conversion options", 500);
            v.SaveAsBox.Text = "orders-march-converted.json";
            v.ResetConverted(); DataToolWindow.Press(v.PrimaryButton); await Task.WhenAny(v.WaitConverted(), Task.Delay(10000));
            await Shot(v, "datasec-data-done", "Data viewer: converted, with the size and how numbers were typed", 400); v.Close(); await Task.Delay(250);
            v = DataViewerDialog.Open(fx.JsonBad); await Wait(v, v.Ready); await Shot(v, "datasec-data-error", "Data viewer: invalid JSON with line, column and context", 500); v.Close(); await Task.Delay(250);
            v = DataViewerDialog.Open(fx.YamlOk); await Wait(v, v.Ready); v.ViewChoice.Select(1, true); await Shot(v, "datasec-data-text", "Data viewer: YAML in the text view", 500); v.Close(); await Task.Delay(250);

            // ---- encoding and line endings
            var batch = new[] { fx.EncAnsi, fx.EncU16le, fx.EncMixed, fx.EncCrlf };
            var e = EncodingLinesDialog.Open(batch); await Shot(e, "datasec-encoding-batch", "Encoding and line endings: a batch of four, with what each file is and will become", 500);
            e.SelectRow(2); await Shot(e, "datasec-encoding-mixed", "Encoding and line endings: a file that mixes line endings", 400);
            e.SelectRow(0); DataToolWindow.Press(e.PrimaryButton); await Task.WhenAny(e.WaitConverted(), Task.Delay(10000));
            await Shot(e, "datasec-encoding-done", "Encoding and line endings: converted, with Undo from backups", 400); e.Close(); await Task.Delay(250);
            e = EncodingLinesDialog.Open(new[] { fx.EncAnsi }); e.ReadAs(0, 65001);
            await Shot(e, "datasec-encoding-error", "Encoding and line endings: a byte that is not valid stops the file, nothing is written", 400); e.Close();
            try { Directory.Delete(work, true); } catch { }
        }
    }
}
