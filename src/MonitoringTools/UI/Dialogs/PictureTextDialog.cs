using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Text and codes in a picture, read on this PC with Windows' own text recognition: every line it finds is outlined on
    /// the picture, tap lines to choose them, and copy. A QR code in the picture is decoded too, with Join for Wi-Fi codes.
    /// Nothing is uploaded.
    /// </summary>
    internal sealed class PictureTextDialog : ToolDialogWindow
    {
        private readonly BitmapSource _img;
        private readonly Canvas _overlay = new Canvas();
        private readonly TextBox _text = new TextBox { Style = Ui.Style("Input.Multi"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = Ui.Font("F.Mono"), FontSize = 13.5, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private readonly ContentControl _qr = new ContentControl { Focusable = false };
        private readonly List<(string Text, Rect Box, Border Mark)> _lines = new List<(string, Rect, Border)>();
        private readonly HashSet<int> _chosen = new HashSet<int>();
        private readonly Button _primary;
        private TextBlock _status;
        private string _qrText;

        public static PictureTextDialog Open(BitmapSource img)
        {
            var d = new PictureTextDialog(img);
            d.Show();
            _ = d.ReadAsync();
            return d;
        }

        private PictureTextDialog(BitmapSource img) : base("Text and codes in this picture")
        {
            _img = img;
            AutomationProperties.SetName(_text, "Found text");
            var pic = new Grid { Width = 420, Height = 420 * img.PixelHeight / Math.Max(1.0, img.PixelWidth) };
            if (pic.Height > 440) { pic.Height = 440; pic.Width = 440 * img.PixelWidth / Math.Max(1.0, img.PixelHeight); }
            pic.Children.Add(new Image { Source = img, Stretch = Stretch.Fill });
            pic.Children.Add(_overlay);
            var picCard = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(20), Padding = new Thickness(12), Child = pic, VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(picCard, "The picture, with the text it found outlined");

            var right = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
            right.Children.Add(DialogParts.Label("Found text · tap lines in the picture to choose"));
            _text.Height = 220;
            right.Children.Add(_text);
            right.Children.Add(_qr);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(right, 1); g.Children.Add(picCard); g.Children.Add(right);

            _primary = Ui.Button("Copy the text", OnPrimary, "Btn.Accent", Icons.Clipboard);
            var pin = Ui.Button("Pin on screen", () => PinWindow.Show(ClipItem.FromImage(_img)), "Btn.Ghost", Icons.Image);
            var more = new List<(string, string, Action)>
            {
                ("Choose every line", null, () => { for (int i = 0; i < _lines.Count; i++) _chosen.Add(i); Repaint(); }),
                ("Search the web for the text", null, () => SmartActions.Open("https://www.bing.com/search?q=" + Uri.EscapeDataString(_text.Text.Length > 300 ? _text.Text.Substring(0, 300) : _text.Text))),
                ("Blur emails, numbers and faces, then copy", "Ctrl+B", () => _ = BlurAndCopy()),
                ("Save the picture", null, SavePicture),
                ("Read again", null, () => _ = ReadAsync()),
            };
            var body = new StackPanel();
            body.Children.Add(g);
            body.Children.Add(SmartDialogParts.ActionBar(more, "Reading…", new[] { pin }, _primary, "Ctrl+Enter", out _status));
            PreviewKeyDown += (o, e) => { if (e.Key == System.Windows.Input.Key.B && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) { e.Handled = true; _ = BlurAndCopy(); } };
            ShowView("form", body, 900, "Text and codes in this picture", img.PixelWidth + " × " + img.PixelHeight + " · read on this PC, nothing uploaded", Icons.Image, Ui.Res("B.Mint"), Ui.Res("B.MintWash"));
        }

        protected override void OnPrimary()
        {
            if (_text.Text.Trim().Length == 0) { _status.Text = "No text to copy."; return; }
            ClipboardWatcher.SetText(_text.Text);
            var n = _text.Text.Split('\n').Length;
            _status.Text = "Copied " + n + (n == 1 ? " line." : " lines.");
        }

        internal async Task ReadAsync()
        {
            _status.Text = "Reading…";
            try
            {
                var lines = await Ocr(_img);
                _lines.Clear(); _overlay.Children.Clear(); _chosen.Clear();
                double sx = ((FrameworkElement)_overlay.Parent).Width / _img.PixelWidth, sy = ((FrameworkElement)_overlay.Parent).Height / _img.PixelHeight;
                for (int i = 0; i < lines.Count; i++)
                {
                    int idx = i;
                    var (text, box) = lines[i];
                    var mark = new Border { Width = box.Width * sx + 6, Height = box.Height * sy + 4, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1.5), Cursor = System.Windows.Input.Cursors.Hand, Background = Brushes.Transparent, ToolTip = text };
                    Canvas.SetLeft(mark, box.X * sx - 3); Canvas.SetTop(mark, box.Y * sy - 2);
                    AutomationProperties.SetName(mark, "Line: " + text);
                    mark.MouseLeftButtonUp += (s, e) => { if (!_chosen.Remove(idx)) _chosen.Add(idx); Repaint(); };
                    _overlay.Children.Add(mark);
                    _lines.Add((text, box, mark));
                    _chosen.Add(i);
                }
                Repaint();
                _status.Text = lines.Count == 0 ? "No text found." : lines.Count + (lines.Count == 1 ? " line found." : " lines found.");
            }
            catch (Exception ex) { _status.Text = "Text recognition is not available: " + ex.Message; }
            ReadQr();
        }

        private void Repaint()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                bool on = _chosen.Contains(i);
                _lines[i].Mark.BorderBrush = on ? Ui.Res("B.Mint") : new SolidColorBrush(Color.FromArgb(0x80, 0x6F, 0xDB, 0xB4));
                _lines[i].Mark.Background = on ? new SolidColorBrush(Color.FromArgb(0x40, 0x6F, 0xDB, 0xB4)) : Brushes.Transparent;
            }
            _text.Text = string.Join(Environment.NewLine, _lines.Where((l, i) => _chosen.Contains(i)).Select(l => l.Text));
            _primary.Content = _chosen.Count == 0 ? "Copy the text" : "Copy " + _chosen.Count + (_chosen.Count == 1 ? " line" : " lines");
        }

        private void ReadQr()
        {
            if (!QrDecoder.TryDecode(_img, out var text, out _)) return;
            _qrText = text;
            var sp = new StackPanel();
            var wifi = Regex.Match(text, @"^WIFI:(?:.*?;)?S:(?<s>(?:\\.|[^;])*);", RegexOptions.IgnoreCase);
            sp.Children.Add(Ui.Mono(wifi.Success ? "QR CODE · WI-FI NETWORK" : "QR code", Ui.Res("B.TextSoft"), 11));
            sp.Children.Add(new TextBlock { Text = wifi.Success ? wifi.Groups["s"].Value.Replace("\\", "") : text, FontSize = 17, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            var row = new WrapPanel();
            var copy = Ui.Button("Copy what it says", () => { ClipboardWatcher.SetText(_qrText); _status.Text = "Copied the QR code's text."; }, "Btn.Ghost", Icons.Clipboard);
            row.Children.Add(copy);
            if (Uri.TryCreate(text, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
            { var open = Ui.Button("Open the link", () => SmartActions.Open(text), "Btn.Ghost", Icons.Link); open.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(open); }
            sp.Children.Add(row);
            _qr.Content = new Border { Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 12, 16, 14), Margin = new Thickness(0, 12, 0, 0), Child = sp };
        }

        /// <summary>Copies a version of the picture with email addresses, long numbers, phone numbers and faces blurred,
        /// ready to share. Worked out on this PC.</summary>
        internal async Task BlurAndCopy()
        {
            _status.Text = "Blurring…";
            var boxes = new List<Rect>();
            try
            {
                foreach (var (text, box) in await OcrWords(_img))
                    if (Regex.IsMatch(text, @"@[\w-]+\.|\d{3,}[\d\s\-().]{4,}|\b\d{6,}\b|[A-Z]{2}\d{2}[A-Z0-9]{10,}")) boxes.Add(box);
            }
            catch { }
            try { boxes.AddRange(await Faces(_img)); } catch { }
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(_img, new Rect(0, 0, _img.PixelWidth, _img.PixelHeight));
                foreach (var b in boxes)
                {
                    var r = Rect.Inflate(b, 4, 4);
                    r.Intersect(new Rect(0, 0, _img.PixelWidth, _img.PixelHeight));
                    if (r.IsEmpty) continue;
                    var crop = new CroppedBitmap(_img, new Int32Rect((int)r.X, (int)r.Y, Math.Max(1, (int)r.Width), Math.Max(1, (int)r.Height)));
                    var tiny = new TransformedBitmap(crop, new ScaleTransform(Math.Min(1, 6 / Math.Max(1, r.Width)), Math.Min(1, 6 / Math.Max(1, r.Height))));
                    var group = new DrawingGroup(); RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.NearestNeighbor);
                    group.Children.Add(new ImageDrawing(tiny, r));
                    dc.DrawDrawing(group);
                }
            }
            var rtb = new RenderTargetBitmap(_img.PixelWidth, _img.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv); rtb.Freeze();
            ClipboardWatcher.SetImage(rtb);
            _status.Text = boxes.Count == 0 ? "Nothing private found, so the picture was copied as it is." : "Blurred " + boxes.Count + (boxes.Count == 1 ? " area" : " areas") + " and copied the picture. Check it before you share it.";
        }

        private static async Task<List<(string, Rect)>> OcrWords(BitmapSource img)
        {
            var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new InvalidOperationException("no recognition language");
            var conv = new FormatConvertedBitmap(img, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight; var px = new byte[w * 4 * h]; conv.CopyPixels(px, w * 4, 0);
            var buffer = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(px);
            using var bmp = Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(buffer, Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, w, h, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
            var res = await engine.RecognizeAsync(bmp);
            var list = new List<(string, Rect)>();
            foreach (var line in res.Lines)
            {
                list.Add((line.Text, Union(line.Words.Select(x => x.BoundingRect))));
                foreach (var word in line.Words) list.Add((word.Text, new Rect(word.BoundingRect.X, word.BoundingRect.Y, word.BoundingRect.Width, word.BoundingRect.Height)));
            }
            return list;
        }

        private static Rect Union(IEnumerable<Windows.Foundation.Rect> rs)
        {
            Rect u = Rect.Empty;
            foreach (var r in rs) u.Union(new Rect(r.X, r.Y, r.Width, r.Height));
            return u.IsEmpty ? new Rect(0, 0, 1, 1) : u;
        }

        private static async Task<List<Rect>> Faces(BitmapSource img)
        {
            if (!Windows.Media.FaceAnalysis.FaceDetector.IsSupported) return new List<Rect>();
            var detector = await Windows.Media.FaceAnalysis.FaceDetector.CreateAsync();
            var conv = new FormatConvertedBitmap(img, PixelFormats.Gray8, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight; var px = new byte[w * h]; conv.CopyPixels(px, w, 0);
            var buffer = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(px);
            using var bmp = Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(buffer, Windows.Graphics.Imaging.BitmapPixelFormat.Gray8, w, h);
            var faces = await detector.DetectFacesAsync(bmp);
            return faces.Select(f => new Rect(f.FaceBox.X, f.FaceBox.Y, f.FaceBox.Width, f.FaceBox.Height)).ToList();
        }

        private void SavePicture()
        {
            var path = Path.Combine(Settings.Current.ResolvedMediaFolder, "Picture " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".png");
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(_img));
            using (var fs = File.Create(path)) enc.Save(fs);
            ActionProvider.Reveal(path);
            _status.Text = "Saved " + Path.GetFileName(path) + ".";
        }

        /// <summary>Windows' built-in text recognition, in the language of your Windows. Returns lines with their boxes in pixels.</summary>
        public static async Task<List<(string Text, Rect Box)>> Ocr(BitmapSource img)
        {
            var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new InvalidOperationException("no recognition language is installed in Windows");
            var conv = new FormatConvertedBitmap(img, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            conv.CopyPixels(px, stride, 0);
            return await Recognise(engine, px, w, h);
        }

        private static async Task<List<(string, Rect)>> Recognise(Windows.Media.Ocr.OcrEngine engine, byte[] px, int w, int h)
        {
            var buffer = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(px);
            using var bmp = Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(buffer, Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, w, h, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
            var res = await engine.RecognizeAsync(bmp);
            var list = new List<(string, Rect)>();
            foreach (var line in res.Lines)
            {
                double x1 = double.MaxValue, y1 = double.MaxValue, x2 = 0, y2 = 0;
                foreach (var word in line.Words) { var r = word.BoundingRect; x1 = Math.Min(x1, r.X); y1 = Math.Min(y1, r.Y); x2 = Math.Max(x2, r.X + r.Width); y2 = Math.Max(y2, r.Y + r.Height); }
                list.Add((line.Text, new Rect(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1))));
            }
            return list;
        }
    }
}
