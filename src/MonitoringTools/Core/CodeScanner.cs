using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace MonitoringTools.Core
{
    /// <summary>One code read from a picture: its kind in plain words and the text it holds.</summary>
    public sealed record ScannedCode(string Kind, string Text, bool IsQr);

    /// <summary>
    /// Reads every scannable code in a picture: QR and Micro QR, Data Matrix, Aztec, PDF417, MaxiCode and the 1D barcodes
    /// (EAN and UPC, Code 39, 93 and 128, ITF, Codabar, GS1 DataBar). Uses ZXing.Net, with the app's own QR reader as a
    /// second try for mirrored and photographed QR codes. Large pictures are scaled down first so a screenshot never
    /// stalls the toast, and each picture is read once.
    /// </summary>
    public static class CodeScanner
    {
        private const long MaxPixels = 4_000_000;
        private static readonly ConditionalWeakTable<BitmapSource, IReadOnlyList<ScannedCode>> Cache = new ConditionalWeakTable<BitmapSource, IReadOnlyList<ScannedCode>>();

        public static IReadOnlyList<ScannedCode> Scan(BitmapSource img)
        {
            if (img == null) return Array.Empty<ScannedCode>();
            lock (Cache)
            {
                if (Cache.TryGetValue(img, out var hit)) return hit;
                var found = ScanUncached(img);
                Cache.AddOrUpdate(img, found);
                return found;
            }
        }

        private static IReadOnlyList<ScannedCode> ScanUncached(BitmapSource img)
        {
            var list = new List<ScannedCode>();
            try
            {
                BitmapSource src = img;
                long px = (long)img.PixelWidth * img.PixelHeight;
                if (px > MaxPixels)
                {
                    var s = Math.Sqrt((double)MaxPixels / px);
                    src = new TransformedBitmap(img, new ScaleTransform(s, s));
                }
                var bgra = src.Format == PixelFormats.Bgra32 ? src : new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
                int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
                var bytes = new byte[stride * h];
                bgra.CopyPixels(bytes, stride, 0);
                var reader = new BarcodeReaderGeneric
                {
                    AutoRotate = true,
                    Options = new DecodingOptions { TryHarder = true, TryInverted = true, PossibleFormats = null },
                };
                var results = reader.DecodeMultiple(new RGBLuminanceSource(bytes, w, h, RGBLuminanceSource.BitmapFormat.BGRA32));
                if (results != null)
                    foreach (var r in results)
                        if (!string.IsNullOrEmpty(r.Text) && !list.Any(c => c.Text == r.Text))
                            list.Add(new ScannedCode(Describe(r.BarcodeFormat), r.Text, r.BarcodeFormat == BarcodeFormat.QR_CODE));
            }
            catch (Exception ex) { Log.Warn("Code scan failed: " + ex.GetType().Name); }

            // The app's own reader also handles mirrored and photographed QR codes.
            if (!list.Any(c => c.IsQr) && (long)img.PixelWidth * img.PixelHeight <= 8_000_000)
            {
                try { if (QrDecoder.TryDecode(img, out var text, out _) && !string.IsNullOrEmpty(text) && !list.Any(c => c.Text == text)) list.Add(new ScannedCode("QR code", text, true)); }
                catch { }
            }
            return list;
        }

        private static string Describe(BarcodeFormat f) => f switch
        {
            BarcodeFormat.QR_CODE => "QR code",
            BarcodeFormat.DATA_MATRIX => "Data Matrix code",
            BarcodeFormat.AZTEC => "Aztec code",
            BarcodeFormat.PDF_417 => "PDF417 code",
            BarcodeFormat.MAXICODE => "MaxiCode",
            BarcodeFormat.EAN_13 => "EAN-13 barcode",
            BarcodeFormat.EAN_8 => "EAN-8 barcode",
            BarcodeFormat.UPC_A => "UPC-A barcode",
            BarcodeFormat.UPC_E => "UPC-E barcode",
            BarcodeFormat.CODE_39 => "Code 39 barcode",
            BarcodeFormat.CODE_93 => "Code 93 barcode",
            BarcodeFormat.CODE_128 => "Code 128 barcode",
            BarcodeFormat.ITF => "ITF barcode",
            BarcodeFormat.CODABAR => "Codabar barcode",
            BarcodeFormat.RSS_14 or BarcodeFormat.RSS_EXPANDED => "GS1 DataBar barcode",
            _ => "Barcode",
        };
    }
}
