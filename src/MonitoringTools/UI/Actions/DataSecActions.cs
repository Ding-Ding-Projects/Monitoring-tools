using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Dialogs;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The clipboard notice buttons that open the data and security dialogs: Who signed this? for programs and installers, Certificate inspector for
    /// certificate files, Data viewer and converter for CSV, JSON and YAML, Encoding and line endings for text files, and Hash and verify for any single file.
    /// </summary>
    internal static class DataSecActions
    {
        /// <summary>Which of these dialogs a set of copied files can open, in the order the buttons appear.</summary>
        internal static List<string> Kinds(string[] files)
        {
            var kinds = new List<string>();
            files = (files ?? Array.Empty<string>()).Where(f => File.Exists(f)).ToArray();
            if (files.Length == 0) return kinds;
            if (files.Length == 1)
            {
                var f = files[0];
                if (SignatureEngine.IsSignable(f)) kinds.Add("signed");
                if (CertEngine.IsCertificateFile(f)) kinds.Add("cert");
                if (DataEngine.IsDataFile(f)) kinds.Add("data");
                if (TextEngine.IsTextName(f)) kinds.Add("encoding");
                kinds.Add("hash");
            }
            else if (files.All(TextEngine.IsTextName)) kinds.Add("encoding");
            return kinds;
        }

        internal static void Add(ClipItem item, List<ToastAction> l)
        {
            if (item?.Files == null || item.Files.Length == 0 || item.FirstFileKind == FileKind.Folder) return;
            var files = item.Files;
            var actions = new List<ToastAction>();
            foreach (var kind in Kinds(files))
            {
                var f = files[0];
                switch (kind)
                {
                    case "signed":
                        actions.Add(new ToastAction { Label = "Who signed this?", Icon = DataToolIcons.Stamp, HandsOff = true, Tip = "Shows who signed this program or installer, the certificate chain and whether the file was changed since. Nothing is run.", Run = () => WhoSignedDialog.Open(f) });
                        break;
                    case "cert":
                        actions.Add(new ToastAction { Label = "Inspect certificate", Icon = DataToolIcons.Cert, HandsOff = true, Tip = "Subject, issuer, expiry countdown, allowed uses and fingerprints. Convert between PEM, DER and PKCS#12.", Run = () => CertInspectorDialog.Open(f) });
                        break;
                    case "data":
                        actions.Add(new ToastAction { Label = "View data", Icon = DataToolIcons.Table, HandsOff = true, Tip = "Table view, validity check with the line and column of an error, and conversion between CSV, JSON and YAML.", Run = () => DataViewerDialog.Open(f) });
                        break;
                    case "encoding":
                        var set = files.Where(TextEngine.IsTextName).ToArray();
                        actions.Add(new ToastAction { Label = "Encoding and line endings", Icon = DataToolIcons.Lines, HandsOff = true, Tip = "Finds the text encoding and line endings and converts them, with a backup. Binary files are never touched.", Run = () => EncodingLinesDialog.Open(set) });
                        break;
                    case "hash":
                        actions.Add(new ToastAction { Label = "Hash and verify", Icon = DataToolIcons.Hash, HandsOff = true, Tip = "SHA-256, SHA-1, MD5 or SHA-512 of this file, and a check against the checksum the publisher gave.", Run = () => HashVerifyDialog.Open(f) });
                        break;
                }
            }
            if (actions.Count == 0) return;
            int at = l.FindIndex(a => a.Label == "Show in folder");
            if (at >= 0) l.InsertRange(at + 1, actions); else l.AddRange(actions);
        }
    }
}
