using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Who signed this?: the Authenticode verdict (WinVerifyTrust, including catalog-signed system files), the publisher, the certificate chain and the
    /// timestamp of a program, installer or library. It says who made the file and that it was not changed; it never says the file is safe and never
    /// runs it.
    /// </summary>
    internal sealed class WhoSignedDialog : DataToolWindow
    {
        internal static WhoSignedDialog Current { get; private set; }

        private readonly string _path;
        private readonly BannerBox _banner = new BannerBox();
        private readonly ContentControl _content = new ContentControl();
        private readonly TaskCompletionSource<bool> _ready = new TaskCompletionSource<bool>();
        private SignatureReport _report;
        private int _selectedLink;

        internal Task Ready => _ready.Task;
        internal SignatureReport Report => _report;
        internal BannerBox Banner => _banner;

        public static WhoSignedDialog Open(string path)
        {
            if (!File.Exists(path)) { App.Toasts.ShowNotice("Nothing to check", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var d = new WhoSignedDialog(path);
            Current = d;
            d.Present();
            d.Begin();
            return d;
        }

        private static string Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".msi" => "installer", ".dll" or ".ocx" => "library", ".sys" => "driver", ".cab" => "cabinet", ".msix" => "package", _ => "program",
        };

        private WhoSignedDialog(string path) : base("Who signed this?", path, 780, DataToolIcons.Stamp)
        {
            _path = path;
            long size = 0; try { size = new FileInfo(path).Length; } catch { }
            SetSummary(Kind(path) + " · " + Ui.Bytes(size));
            Body.Children.Add(_banner);
            Body.Children.Add(_content);
            _banner.Set(BannerKind.Info, "Checking the signature…", "Reading the file. It is not run.");
            SetCancel("Copy details"); CancelButton.IsEnabled = false;
            SetPrimary("Close", null, Close);
            Status("Revocation is not checked, so this works offline.");
        }

        protected override void OnCancelButton()
        {
            if (_report == null) { Close(); return; }
            ClipboardWatcher.SetText(AsText(_report));
            Status("Copied the details. They hold no file contents.");
        }

        private async void Begin()
        {
            try
            {
                _report = await Task.Run(() => SignatureEngine.Inspect(_path));
                Render();
                _ready.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _banner.Set(BannerKind.Error, "The check could not run", ex.Message);
                _ready.TrySetResult(false);
            }
        }

        internal static string AsText(SignatureReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("File: " + Path.GetFileName(r.Path));
            sb.AppendLine("Verdict: " + r.State + (r.Problem != null ? " (" + r.Problem + ")" : "") + " [trust result 0x" + r.TrustResult.ToString("X8") + "]");
            if (r.Publisher != null) sb.AppendLine("Publisher: " + r.Publisher);
            if (r.SignedOn != null) sb.AppendLine("Signed on: " + r.SignedOn.Value.ToString("yyyy-MM-dd HH:mm") + " UTC (timestamp)");
            if (r.Thumbprint != null) sb.AppendLine("Thumbprint: " + r.Thumbprint);
            if (r.FileSha256 != null) sb.AppendLine("SHA-256: " + r.FileSha256);
            foreach (var l in r.Chain) sb.AppendLine("Chain: " + l.Name + " [" + l.Badge + "]");
            return sb.ToString().TrimEnd();
        }

        private static string When(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " UTC";

        private void Render()
        {
            var r = _report;
            var panel = new StackPanel();
            FooterSecondary.Children.Clear();
            CancelButton.IsEnabled = true;
            string pub = r.Leaf != null ? SignatureEngine.Describe(r.Leaf, false) : null;
            switch (r.State)
            {
                case SignatureState.Valid:
                    _banner.Set(BannerKind.Success, "Signed by " + pub + ", and the file is intact", "The signature checks out and the certificate chains to a trusted root. This tells you who made it and that it was not altered, not that it is safe.");
                    break;
                case SignatureState.Unsigned:
                    _banner.Set(BannerKind.Warning, "This file is not signed", "Nobody vouches for who made it. That is common for small or older tools and is not proof of a problem. Check where you got it.");
                    break;
                case SignatureState.Broken:
                    _banner.Set(BannerKind.Error, "The signature is broken", "The file was changed after it was signed. Do not run it. Delete it and get a fresh copy from the publisher.");
                    break;
                case SignatureState.Untrusted:
                    _banner.Set(BannerKind.Warning, "Signed, but the root is not trusted on this computer",
                        r.Problem != null && !r.Problem.StartsWith("The issuer")
                            ? r.Problem
                            : "The signature is intact" + (r.CertValidWhenSigned ? " and the certificate was valid when signed" + (r.Timestamped ? " (timestamped " + r.SignedOn.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ")" : "") : "") + ", but its issuer is not in your trusted list.");
                    break;
                default:
                    _banner.Set(BannerKind.Error, "The signature could not be read", r.Problem ?? "Windows could not read this file's signature.");
                    break;
            }

            if (r.State == SignatureState.Unsigned || r.State == SignatureState.Unreadable)
            {
                panel.Children.Add(Card(
                    ("SHA-256", Value(r.FileSha256 == null ? "unavailable" : SignatureEngine.Shorten(r.FileSha256), true)),
                    ("Size", Value(Ui.Bytes(r.Size))),
                    ("Modified", Value(r.Modified.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)))));
                FooterSecondary.Children.Add(Action("Hash and verify", DataToolIcons.Hash, () => HashVerifyDialog.Open(_path)));
                FooterSecondary.Children.Add(Action("Show in folder", DataToolIcons.Folder, () => ActionProvider.Reveal(_path)));
            }
            else if (r.State == SignatureState.Broken)
            {
                var rows = new List<(string, UIElement)>();
                rows.Add(("Claims to be from", Data(r.Publisher ?? "unknown")));
                if (r.SignedOn != null) rows.Add(("Signed on", Value(When(r.SignedOn.Value))));
                rows.Add(("Problem", Value(r.Problem ?? "")));
                panel.Children.Add(Card(rows.ToArray()));
                FooterSecondary.Children.Add(Action("Show in folder", DataToolIcons.Folder, () => ActionProvider.Reveal(_path)));
                FooterSecondary.Children.Add(Action("Delete file", DataToolIcons.Trash, () => FileToolsWindow.ConfirmRecycle(new[] { _path }), "Btn.Danger"));
            }
            else
            {
                var rows = new List<(string, UIElement)> { ("Publisher", Data(r.Publisher ?? "unknown")) };
                rows.Add(("Signed on", Value(r.Timestamped ? When(r.SignedOn.Value) + " (trusted timestamp)" : "Not timestamped, so the date of signing is not proven")));
                if (r.CertNotAfter != null)
                    rows.Add(("Certificate valid", Value("to " + r.CertNotAfter.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + (r.CertValidWhenSigned ? (r.Timestamped ? ", was valid when signed" : ", valid now") : ", was not valid when signed"))));
                rows.Add(("Signature kept", Value(r.ViaCatalog ? "In a Windows security catalog (a system file)" : "Inside the file")));
                if (r.Digest != null) rows.Add(("Digest", Value(r.Digest, true)));
                if (r.Thumbprint != null) rows.Add(("Thumbprint", Value(r.Thumbprint, true)));
                panel.Children.Add(Card(rows.ToArray()));
                if (r.Chain.Count > 0)
                {
                    panel.Children.Add(Label("Certificate chain", true));
                    panel.Children.Add(ChainList(r));
                }
                if (r.Leaf != null) FooterSecondary.Children.Add(Action("Open certificate", DataToolIcons.Cert, OpenSelected));
            }
            if (r.State == SignatureState.Valid || r.State == SignatureState.Untrusted)
                panel.Children.Add(Note("The check does not ask the issuer whether a certificate was revoked, so it works offline. A signature never says anything about what a program does.", new Thickness(0, 0, 0, 4)));
            _content.Content = panel;
            Status(r.State == SignatureState.Valid ? "Verified against the Windows trusted list." : r.State == SignatureState.Unsigned ? "No signature found." : "Do not rely on this file until the problem is understood.", r.State != SignatureState.Valid);
        }

        private FrameworkElement ChainList(SignatureReport r)
        {
            var list = new StackPanel();
            for (int i = 0; i < r.Chain.Count; i++)
            {
                int idx = i; var link = r.Chain[i];
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(i * 24, 0, 0, 0) };
                left.Children.Add(new ContentControl { Content = Ui.Icon(i == 0 ? DataToolIcons.Stamp : DataToolIcons.Cert, Ui.Res("B.TextSoft"), 16, 1.8), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                left.Children.Add(Data(link.Name));
                ((TextBlock)left.Children[1]).FontWeight = FontWeights.Medium; ((TextBlock)left.Children[1]).VerticalAlignment = VerticalAlignment.Center; ((TextBlock)left.Children[1]).TextWrapping = TextWrapping.NoWrap; ((TextBlock)left.Children[1]).TextTrimming = TextTrimming.CharacterEllipsis; ((TextBlock)left.Children[1]).Margin = new Thickness(0);
                var badge = Badge(link.Badge, link.Good, link.Badge == "Valid when signed");
                Grid.SetColumn(badge, 1);
                g.Children.Add(left); g.Children.Add(badge);
                var b = new Button { Style = Ui.Style("Btn.Row"), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 9, 12, 9), Height = double.NaN, Content = g };
                AutomationProperties.SetName(b, link.Name + ", " + link.Badge);
                b.Click += (s, e) => { _selectedLink = idx; Status("Selected " + link.Name + ". Open certificate shows its details."); };
                b.MouseDoubleClick += (s, e) => { _selectedLink = idx; OpenSelected(); };
                list.Children.Add(b);
            }
            return new Border { CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Sunken"), Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(4), Child = list };
        }

        private void OpenSelected()
        {
            var r = _report;
            if (r == null) return;
            var cert = r.Chain.Count > _selectedLink ? r.Chain[_selectedLink].Certificate : r.Leaf;
            if (cert != null) CertInspectorDialog.OpenCertificate(cert, r.Chain.Count > _selectedLink ? r.Chain[_selectedLink].Name : Path.GetFileName(_path));
        }

        internal void OpenCertificateForTest() => OpenSelected();

        protected override void OnClosedCore() { if (Current == this) Current = null; }
    }
}
