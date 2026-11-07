using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>
    /// The wizard that locks one element. It opens beside that element, picks one of six credential mixes, creates credentials for this
    /// lock only, and says plainly that the lock is just for fun and how to recover from forgetting it.
    /// </summary>
    public sealed class LockWizardWindow : AnchoredCard
    {
        private readonly string _pageId, _key, _label;
        private readonly Action _created;
        private LockPolicy _policy = LockPolicy.Pin;
        private int _minutes;
        private readonly string _secret = Totp.NewSecret();
        private readonly StackPanel _fields = new StackPanel();
        private readonly TextBlock _msg = SafetyUi.Live("Lock wizard status");
        private PasswordBox _pin, _pin2, _pass, _pass2;
        private TextBox _code;
        private TextBlock _secretText;

        public LockWizardWindow(FrameworkElement origin, string pageId, string key, string label, Action created) : base(origin, "Lock " + label)
        {
            _pageId = pageId; _key = key; _label = label; _created = created;
            _minutes = Math.Max(0, Settings.Current.LockUnlockMinutes);
            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = "Lock this element", Style = Ui.Style("T.Eyebrow"), FontSize = 11 });
            var shown = label.Length > 70 ? label.Substring(0, 70) + "…" : label;
            body.Children.Add(new TextBlock { Text = shown, Style = Ui.Style("T.Title"), FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) });
            body.Children.Add(SafetyUi.Text("Choose what it takes to unlock. This lock has its own credentials, separate from every other lock. Default: PIN.", 12, "B.TextMute"));

            var policies = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
            var chips = new List<(Button B, LockPolicy P)>();
            void Paint()
            {
                foreach (var (b, p) in chips)
                {
                    bool on = p == _policy;
                    b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                    b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                    b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                }
            }
            foreach (LockPolicy p in Enum.GetValues(typeof(LockPolicy)))
            {
                var pol = p;
                var b = Ui.Button(Locks.Describe(p), () => { _policy = pol; Paint(); BuildFields(); }, "Btn.Chip");
                b.ToolTip = "Unlock with: " + Locks.Describe(p);
                chips.Add((b, p)); policies.Children.Add(b);
            }
            Paint();
            body.Children.Add(policies);
            body.Children.Add(_fields);

            body.Children.Add(new NumberField("Stay unlocked for", "How long this lock stays open after you unlock it. 0 keeps it open until the app closes. Default: 5 min.",
                _minutes, 0, 1440, 1, v => _minutes = (int)v, "min"));

            body.Children.Add(SafetyUi.Text(Locks.Disclosure, 12, "B.TextSoft", null, 6));
            body.Children.Add(SafetyUi.Text(Locks.RecoveryLine, 12, "B.TextMute", null, 4));
            body.Children.Add(_msg);
            var row = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            row.Children.Add(Ui.Button("Lock it", Create, "Btn.Accent", Icons.Shield));
            row.Children.Add(Ui.Button("Cancel", Close, "Btn.Ghost"));
            body.Children.Add(row);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body, MaxHeight = Math.Max(300, Ui.WorkArea.Height - 140) };
            SetBody(scroll);
            BuildFields();
        }

        private PasswordBox Field(string name, StackPanel host)
        {
            var (box, frame) = SafetyUi.Pass(name);
            host.Children.Add(SafetyUi.Text(name, 12, "B.TextSoft", null, 4));
            host.Children.Add(frame);
            return box;
        }

        private void BuildFields()
        {
            _fields.Children.Clear(); _msg.Text = "";
            var f = Locks.Factors(_policy);
            if (f.Contains(LockFactor.Pin)) { _pin = Field("PIN, 4 to 8 digits", _fields); _pin2 = Field("Type the PIN again", _fields); }
            if (f.Contains(LockFactor.Password)) { _pass = Field("Password, at least 4 characters", _fields); _pass2 = Field("Type the password again", _fields); }
            if (f.Contains(LockFactor.Totp))
            {
                _fields.Children.Add(SafetyUi.Text("Authenticator code", 12, "B.TextSoft", null, 8));
                _fields.Children.Add(SafetyUi.Text("A secret was made on this computer for this lock only. Add it to an authenticator, then type the current 6-digit code below to prove it works. SHA-1, 6 digits, 30 seconds.", 12, "B.TextMute"));
                _secretText = SafetyUi.Text(Base32.Group(_secret), 14, "B.Text", null, 6);
                _secretText.FontFamily = Ui.Font("F.Mono"); _secretText.Visibility = Visibility.Collapsed;
                AutomationProperties.SetName(_secretText, "Authenticator secret");
                var uri = Totp.BuildUri(new OtpParams { Issuer = "Monitoring Tools lock", Account = _label.Length > 40 ? _label.Substring(0, 40) : _label, Secret = _secret });
                var rowBtn = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
                var reveal = Ui.Button("Show secret", null, "Btn.Ghost", Icons.Shield);
                reveal.Click += (s, e) => { _secretText.Visibility = _secretText.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; };
                reveal.ToolTip = "Reveal or hide the secret. It is hidden by default.";
                rowBtn.Children.Add(reveal);
                rowBtn.Children.Add(Ui.Button("Copy otpauth link", () => ClipboardWatcher.SetText(uri), "Btn.Ghost", Icons.Clipboard));
                var addOwn = Ui.Button("Add to this app's authenticator", () =>
                {
                    var err = Authenticator.Add(new OtpParams { Issuer = "Monitoring Tools lock", Account = _label.Length > 40 ? _label.Substring(0, 40) : _label, Secret = _secret });
                    _msg.Text = err ?? "Added. Note that the key now sits inside the box it opens, so this lock is ornamental. You can still do it, it is a for-fun lock.";
                }, "Btn.Ghost", Icons.Plus);
                addOwn.ToolTip = "Stores the secret in the built-in authenticator on the Safety page";
                rowBtn.Children.Add(addOwn);
                _fields.Children.Add(_secretText);
                _fields.Children.Add(rowBtn);
                _code = new TextBox { Style = Ui.Style("Input"), MaxLength = 6, Margin = new Thickness(0, 6, 0, 0) };
                AutomationProperties.SetName(_code, "Current authenticator code to confirm pairing");
                _fields.Children.Add(_code);
            }
        }

        private void Create()
        {
            var f = Locks.Factors(_policy);
            string pin = null, pw = null;
            if (f.Contains(LockFactor.Pin))
            {
                if (_pin.Password != _pin2.Password) { _msg.Text = "The two PINs do not match."; return; }
                pin = _pin.Password;
            }
            if (f.Contains(LockFactor.Password))
            {
                if (_pass.Password != _pass2.Password) { _msg.Text = "The two passwords do not match."; return; }
                pw = _pass.Password;
            }
            if (f.Contains(LockFactor.Totp) && !Totp.Verify(new OtpParams { Secret = _secret }, _code?.Text ?? "", 1))
            {
                _msg.Text = "That code does not match yet. Add the secret to an authenticator and type its current code. This confirms the pairing before the lock arms.";
                return;
            }
            var err = Locks.Create(_pageId, _key, _label, _policy, _minutes, pin, pw, f.Contains(LockFactor.Totp) ? _secret : null);
            if (err != null) { _msg.Text = err; return; }
            Close();
            _created?.Invoke();
        }
    }
}
