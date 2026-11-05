using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Certificate inspector: subject, issuer, validity with a countdown, key usage, alternative names and fingerprints of a certificate file
    /// (.cer .crt .pem .pfx .p12 .der), and conversion between PEM, DER and PKCS#12. A PKCS#12 password is used once and never stored or logged;
    /// the private key is never shown, and a copy that holds one is only written with the switch on and a new password.
    /// </summary>
    internal sealed class CertInspectorDialog : DataToolWindow
    {
        internal static CertInspectorDialog Current { get; private set; }
        private enum Mode { Password, Details, Convert, Done }

        private readonly string _path;               // null when the certificate came from another dialog
        private readonly string _folder;
        private CertDetails _d;
        private Mode _mode;
        private PasswordBox _pw;
        private TextBox _pwShown;
        private TextBlock _pwError;
        private CertFormat _format = CertFormat.Pem;
        private bool _includeKey;
        private Choice _formatChoice;
        private CheckBox _keySwitch;
        private TextBox _saveAs;
        private PasswordBox _newPw;
        private string _written;

        internal CertDetails Details => _d;
        internal string Written => _written;
        internal PasswordBox PasswordField => _pw;
        internal PasswordBox NewPasswordField => _newPw;
        internal Choice FormatChoice => _formatChoice;
        internal CheckBox IncludeKeySwitch => _keySwitch;
        internal TextBox SaveAsBox => _saveAs;
        internal string ModeName => _mode.ToString();
        internal BannerBox Banner => _banner;
        private readonly BannerBox _banner = new BannerBox();

        public static CertInspectorDialog Open(string path)
        {
            if (!File.Exists(path)) { App.Toasts.ShowNotice("Nothing to inspect", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var d = new CertInspectorDialog(path, null, null);
            Current = d; d.Present();
            return d;
        }

        /// <summary>Opens the inspector on a certificate read from somewhere else, for example a link in a signature's chain.</summary>
        public static CertInspectorDialog OpenCertificate(X509Certificate2 cert, string label)
        {
            var d = new CertInspectorDialog(null, CertEngine.Describe(new X509Certificate2(cert.RawData)), label);
            Current = d; d.Present();
            return d;
        }

        private CertInspectorDialog(string path, CertDetails details, string label) : base("Certificate inspector", path ?? label, 780, DataToolIcons.Cert)
        {
            _path = path;
            _folder = path != null ? Path.GetDirectoryName(path) : Settings.Current.ResolvedMediaFolder;
            if (details != null) { _d = details; ShowDetails(); }
            else
            {
                var r = CertEngine.Load(path);
                Apply(r);
            }
        }

        private void Apply(CertEngine.LoadResult r)
        {
            if (r.NeedsPassword || r.WrongPassword) { ShowPassword(r.WrongPassword); return; }
            if (r.Details == null)
            {
                Body.Children.Clear();
                SetSummary("not read");
                _banner.Set(DataBannerKind.Error, "This file could not be opened", (r.Problem ?? "It is not a certificate Windows can read.") + " Nothing was changed.");
                Body.Children.Add(_banner);
                SetCancel("Cancel", false); SetPrimary("Close", null, Close);
                Status("Nothing was changed.");
                _mode = Mode.Done;
                return;
            }
            _d = r.Details;
            ShowDetails();
        }

        // ------------------------------------------------------------------ password

        private void ShowPassword(bool wrong)
        {
            _mode = Mode.Password;
            Body.Children.Clear();
            SetSummary("certificate with private key");
            _banner.Set(wrong ? DataBannerKind.Error : DataBannerKind.Info, wrong ? "That password did not open the file" : "This file is protected with a password",
                wrong ? "Try again. The file is not changed." : "It holds a private key. The password is used once to read the certificate and is never stored or written to the log.");
            Body.Children.Add(_banner);
            Body.Children.Add(Label("Password", false));
            _pw = NewPasswordBox(out var frame, out _pwShown);
            AutomationProperties.SetName(_pw, "Password for the certificate file");
            Body.Children.Add(frame);
            Body.Children.Add(Note("Only the public details are shown. The private key is never displayed.", new Thickness(0, 8, 0, 4)));
            SetCancel("Cancel");
            SetPrimary("Open", DataToolIcons.Check, TryOpen);
            Status("");
            Loaded += (s, e) => _pw.Focus();
            if (IsLoaded) _pw.Focus();
        }

        private void TryOpen()
        {
            var pw = _pw.Password;
            var r = CertEngine.Load(_path, pw);
            _pw.Clear(); if (_pwShown != null) _pwShown.Clear();     // the password is used once and dropped
            Apply(r);
        }

        private PasswordBox NewPasswordBox(out Border frame, out TextBox shown)
        {
            var box = new PasswordBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Mint"), FontFamily = Ui.Font("F.Mono"), FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(0), MaxLength = 256 };
            var t = new ControlTemplate(typeof(PasswordBox));
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            host.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            t.VisualTree = host; box.Template = t;
            var plain = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ui.Res("B.Text"), CaretBrush = Ui.Res("B.Mint"), FontFamily = Ui.Font("F.Mono"), FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed, MaxLength = 256 };
            var eye = new Button { Style = Ui.Style("Btn.Icon"), Content = Ui.Icon(DataToolIcons.Eye, Ui.Res("B.TextSoft"), 16, 1.8), Width = 32, Height = 32, Margin = new Thickness(6, 0, 0, 0), ToolTip = "Shows or hides the password you typed." };
            AutomationProperties.SetName(eye, "Show password");
            eye.Click += (s, e) =>
            {
                if (plain.Visibility == Visibility.Collapsed) { plain.Text = box.Password; plain.Visibility = Visibility.Visible; box.Visibility = Visibility.Collapsed; plain.Focus(); }
                else { box.Password = plain.Text; plain.Text = ""; plain.Visibility = Visibility.Collapsed; box.Visibility = Visibility.Visible; box.Focus(); }
            };
            plain.TextChanged += (s, e) => { if (plain.Visibility == Visibility.Visible) box.Password = plain.Text; };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var inner = new Grid(); inner.Children.Add(box); inner.Children.Add(plain);
            Grid.SetColumn(eye, 1); g.Children.Add(inner); g.Children.Add(eye);
            var f = new Border { Height = 42, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.Sunken"), BorderBrush = Ui.Res("B.LineStrong"), BorderThickness = new Thickness(1), Padding = new Thickness(12, 0, 6, 0), Child = g };
            box.GotKeyboardFocus += (s, e) => f.BorderBrush = Ui.Res("B.Mint"); box.LostKeyboardFocus += (s, e) => f.BorderBrush = Ui.Res("B.LineStrong");
            plain.GotKeyboardFocus += (s, e) => f.BorderBrush = Ui.Res("B.Mint"); plain.LostKeyboardFocus += (s, e) => f.BorderBrush = Ui.Res("B.LineStrong");
            frame = f; shown = plain;
            return box;
        }

        // ------------------------------------------------------------------ details

        private void ShowDetails()
        {
            _mode = Mode.Details;
            Body.Children.Clear();
            var d = _d;
            SetSummary(d.Kind + (d.Size > 0 ? " · " + Ui.Bytes(d.Size) : ""));
            bool expired = d.DaysLeft < 0 || d.NotAfter < DateTime.Now;
            bool soon = !expired && d.DaysLeft < 30;
            if (soon) _banner.Set(DataBannerKind.Warning, "Expires in " + (d.DaysLeft == 1 ? "1 day" : d.DaysLeft + " days"), "Renew it before " + d.NotAfter.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + " so clients keep trusting it.");
            else if (expired) _banner.Set(DataBannerKind.Error, "This certificate has expired", "It stopped being valid on " + d.NotAfter.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ". Clients will refuse it.");
            else if (DateTime.Now < d.NotBefore) _banner.Set(DataBannerKind.Warning, "This certificate is not valid yet", "It becomes valid on " + d.NotBefore.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".");
            else _banner.Visibility = Visibility.Collapsed;
            Body.Children.Add(_banner);

            Brush tone = expired ? Ui.Res("B.Danger") : soon ? Ui.Res("B.Amber") : Ui.Res("B.Mint");
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var days = new TextBlock { Text = d.Expiry, FontSize = 28, Foreground = expired ? Ui.Res("B.Danger") : Ui.Res("B.Text"), VerticalAlignment = VerticalAlignment.Bottom };
            AutomationProperties.SetName(days, d.Expiry + ". " + (expired ? "Expired " : "Expires ") + d.NotAfter.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            head.Children.Add(days);
            head.Children.Add(new TextBlock { Text = (expired ? "Expired " : "Expires ") + d.NotAfter.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(12, 0, 0, 5), VerticalAlignment = VerticalAlignment.Bottom });
            Body.Children.Add(head);

            var lab = new Grid();
            lab.ColumnDefinitions.Add(new ColumnDefinition()); lab.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lab.Children.Add(new TextBlock { Text = expired ? "Expired" : soon ? "Valid, ending soon" : "Valid", FontSize = 12, FontWeight = FontWeights.Medium, Foreground = tone });
            var range = new TextBlock { Text = "from " + d.NotBefore.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + " to " + d.NotAfter.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), FontSize = 12, Foreground = Ui.Res("B.TextSoft") };
            Grid.SetColumn(range, 1); lab.Children.Add(range);
            Body.Children.Add(lab);
            double life = Math.Max(1, (d.NotAfter - d.NotBefore).TotalSeconds);
            double frac = Math.Max(0, Math.Min(1, (DateTime.Now - d.NotBefore).TotalSeconds / life));
            var bar = new ProgressBar { Height = 10, Minimum = 0, Maximum = 100, Value = frac * 100, Foreground = tone, Background = Ui.Res("B.Line"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 6, 0, 12) };
            AutomationProperties.SetName(bar, "How much of the validity period has passed");
            Body.Children.Add(bar);

            Body.Children.Add(Card(
                ("Subject", Data(d.Subject)),
                ("Issuer", Data(d.Issuer)),
                ("Alternative names", d.Names.Count == 0 ? Value("none") : Data(string.Join(", ", d.Names))),
                ("Key", Data(d.Key)),
                ("Serial", Value(d.Serial, true)),
                ("SHA-256", Value(d.Sha256, true)),
                ("Thumbprint (SHA-1)", Value(d.Sha1, true))));
            Body.Children.Add(Label("Allowed uses", true));
            var uses = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            if (d.Uses.Count == 0) uses.Children.Add(new TextBlock { Text = "Not restricted by the certificate.", FontSize = 12, Foreground = Ui.Res("B.TextSoft") });
            foreach (var u in d.Uses) uses.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = Ui.Res("B.MintWash"), Padding = new Thickness(10, 3, 10, 4), Margin = new Thickness(0, 0, 6, 6), Child = new TextBlock { Text = u, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.OnPrimaryContainer") } });
            Body.Children.Add(uses);
            if (d.HasPrivateKey) Body.Children.Add(Note("This file holds a private key. It is never shown here.", new Thickness(0, 0, 0, 4)));
            if (d.CertCount > 1) Body.Children.Add(Note("The file holds " + d.CertCount + " certificates. This is the first.", new Thickness(0, 0, 0, 4)));

            FooterSecondary.Children.Clear();
            FooterSecondary.Children.Add(Action("Convert…", DataToolIcons.Refresh, ShowConvert));
            SetCancel("Copy details");
            SetPrimary("Close", null, Close);
            Status(d.Expiry + ".");
        }

        protected override void OnCancelButton()
        {
            if (_mode == Mode.Details && _d != null) { ClipboardWatcher.SetText(CertEngine.AsText(_d)); Status("Copied the details. A private key or password is never copied."); return; }
            if (_mode == Mode.Convert) { ShowDetails(); return; }
            Close();
        }

        // ------------------------------------------------------------------ convert

        private void ShowConvert()
        {
            _mode = Mode.Convert;
            Body.Children.Clear();
            FooterSecondary.Children.Clear();
            _banner.Visibility = Visibility.Collapsed;
            Body.Children.Add(_banner);
            Body.Children.Add(Label("Convert to", true));
            _formatChoice = Segmented("Convert to", new[] { "PEM (.pem)", "DER (.cer)", "PKCS#12 (.pfx)" }, (int)_format, i =>
            {
                _format = (CertFormat)i;
                if (_format == CertFormat.Der) { _includeKey = false; _keySwitch.IsChecked = false; }
                SyncConvert(true);
            });
            Body.Children.Add(_formatChoice.Element);
            _keySwitch = Switch("Include the private key", _d.HasPrivateKey ? "Needs a new password to protect the result." : "Only possible when the source has one.", _includeKey, on => { _includeKey = on; SyncConvert(false); });
            var sw = new Border { CornerRadius = new CornerRadius(20), Background = Ui.Res("B.Sunken"), Padding = new Thickness(16, 12, 16, 8), Margin = new Thickness(0, 0, 0, 12) };
            var sp = new StackPanel(); sp.Children.Add(_keySwitch);
            _keyNote = Note("", new Thickness(0, 0, 0, 6)); sp.Children.Add(_keyNote);
            sw.Child = sp;
            Body.Children.Add(sw);
            _pwHost = new StackPanel { Visibility = Visibility.Collapsed };
            _pwHost.Children.Add(Label("New password for the private key", false));
            _newPw = NewPasswordBox(out var frame, out _);
            AutomationProperties.SetName(_newPw, "New password for the private key");
            _pwHost.Children.Add(frame);
            _pwHost.Children.Add(Note("At least 6 characters. Anyone with the file and this password can use the key, so keep both safe.", new Thickness(0, 6, 0, 10)));
            Body.Children.Add(_pwHost);
            Body.Children.Add(Label("Save as", false));
            _saveAs = new TextBox { Style = Ui.Style("Input"), Height = 42, MaxLength = 200 };
            AutomationProperties.SetName(_saveAs, "File name to save as");
            Body.Children.Add(_saveAs);
            Body.Children.Add(Note("Saved next to the original. A name that exists gets a number added, never an overwrite.", new Thickness(0, 6, 0, 4)));
            SetCancel("Cancel");
            SetPrimary("Convert", DataToolIcons.Refresh, DoConvert);
            SyncConvert(true);
        }

        private TextBlock _keyNote; private StackPanel _pwHost;

        private string DefaultName()
        {
            var src = _path ?? SafeName(FileLabel) + ".cer";
            return CertEngine.DefaultOutputName(src, _format);
        }

        private static string SafeName(string s) { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Trim().Length == 0 ? "certificate" : s.Trim(); }

        private void SyncConvert(bool resetName)
        {
            bool can = CertEngine.CanIncludeKey(_d, _format);
            _keySwitch.IsEnabled = can;
            if (!can && _includeKey) { _includeKey = false; _keySwitch.IsChecked = false; }
            _keyNote.Text = !_d.HasPrivateKey ? "This file holds only the public certificate, so a PEM or DER copy is safe to share."
                : _format == CertFormat.Der ? "A DER file cannot hold a private key."
                : _includeKey ? "The copy will contain the private key, protected by the password below. Do not share it." : "Off: the copy holds the public certificate only and is safe to share.";
            _pwHost.Visibility = _includeKey ? Visibility.Visible : Visibility.Collapsed;
            if (resetName || string.IsNullOrWhiteSpace(_saveAs.Text) || _saveAs.Text == _lastName) { _saveAs.Text = DefaultName(); _lastName = _saveAs.Text; }
            Status(_includeKey ? "Includes the private key, protected by your password." : "Public data only.");
        }

        private string _lastName;

        private void DoConvert()
        {
            try
            {
                var name = _saveAs.Text.Trim();
                if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars().Where(c => c != '\\' && c != '/' && c != ':').ToArray()) >= 0) { Status("Choose a valid file name.", true); return; }
                if (_includeKey && (_newPw.Password.Length < 6)) { Status("Choose a password of at least 6 characters to protect the private key.", true); _newPw.Focus(); return; }
                var full = Path.IsPathRooted(name) ? name : Path.Combine(_folder ?? Settings.Current.ResolvedMediaFolder, name);
                _written = CertEngine.ConvertTo(_d, _format, full, _includeKey, _includeKey ? _newPw.Password : null);
                _newPw.Clear();
                ShowDone();
            }
            catch (Exception ex)
            {
                _banner.Set(DataBannerKind.Error, "Nothing was saved", ex.Message);
                _banner.Visibility = Visibility.Visible;
                Status("Nothing was saved.", true);
            }
        }

        private void ShowDone()
        {
            _mode = Mode.Done;
            Body.Children.Clear();
            FooterSecondary.Children.Clear();
            _banner.Set(DataBannerKind.Success, "Saved " + Path.GetFileName(_written),
                _includeKey ? "This copy contains the private key, protected by the password you set. Share it only with someone who needs the key." : "This copy holds only public data, so it is safe to share.");
            Body.Children.Add(_banner);
            long size = 0; try { size = new FileInfo(_written).Length; } catch { }
            Body.Children.Add(Card(("Format", Value(_format == CertFormat.Pem ? "PEM" : _format == CertFormat.Der ? "DER" : "PKCS#12")), ("Size", Value(Ui.Bytes(size))), ("Private key", Value(_includeKey ? "included, password protected" : "not included"))));
            FooterSecondary.Children.Add(Action("Show in folder", DataToolIcons.Folder, () => ActionProvider.Reveal(_written)));
            SetCancel("Cancel", false);
            SetPrimary("Close", null, Close);
            Status("Saved.");
        }

        protected override void OnClosedCore() { if (Current == this) Current = null; try { _pw?.Clear(); _newPw?.Clear(); } catch { } }
    }
}
