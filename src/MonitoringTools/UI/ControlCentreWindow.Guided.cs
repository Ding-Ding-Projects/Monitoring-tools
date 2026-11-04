using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Net;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // Guided forms for the DNS page. The Minecraft server guide lives beside its page in ControlCentreWindow.Minecraft.cs.

        private bool _dnsGuiding;
        private DnsProfile _dnsOffer;
        private string _dnsName = "", _dnsPrimary = "", _dnsSecondary = "";

        private static FrameworkElement Labeled(string caption, TextBox box, string name)
        {
            AutomationProperties.SetName(box, name);
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            sp.Children.Add(Ui.Mono(caption.ToUpperInvariant(), Ui.Res("B.TextMute"), 11));
            sp.Children.Add(box);
            return sp;
        }

        /// <summary>Switches the computer to a profile. One admin prompt, or none when the service already runs elevated.</summary>
        private async void UseDnsProfile(DnsProfile prof)
        {
            var rc = await DnsSwitcher.ApplyAsync(prof);
            if (rc == 0) App.Toasts.ShowNotice($"DNS is now {prof.Name}", prof.ToString(), App.Toasts.Mint, 4, icon: Icons.Globe);
            else App.Toasts.ShowNotice("DNS was not changed", rc == -2 ? "The prompt was cancelled." : $"The change finished with code {rc}.", App.Toasts.Amber, 4, icon: Icons.Globe);
            App.Net.ProbeNow();
            _dnsOffer = null;
            Navigate("network", animate: false);
        }

        /// <summary>Shown right after a profile is saved: switch to it now, or leave it in the list for later.</summary>
        private FrameworkElement DnsSavedOffer()
        {
            var prof = _dnsOffer;
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 12) };
            sp.Children.Add(new TextBlock { Text = $"Saved “{prof.Name}” ({prof}).", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(Ui.Text("Switch this computer to it now? Nothing changes until you choose. Switching needs administrator rights: one prompt, or none when the service runs elevated."));
            var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(Ui.Button("Use now", () => UseDnsProfile(prof), "Btn.Accent", Icons.Globe));
            row.Children.Add(Ui.Button("Not now", () => { _dnsOffer = null; Navigate("network", animate: false); }, "Btn.Ghost"));
            sp.Children.Add(row);
            var b = new System.Windows.Controls.Border { Background = Ui.Res("B.MintWash"), CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 12, 16, 6), Child = sp };
            AutomationProperties.SetName(b, "Profile saved");
            return b;
        }

        private FrameworkElement DnsGuide()
        {
            var s = Settings.Current;
            var name = Input(_dnsName, 260, "A short label, for example Work or Family filter");
            var p1 = Input(_dnsPrimary, 200, "Primary server, for example 1.1.1.1");
            var p2 = Input(_dnsSecondary, 200, "Secondary server (optional), for example 1.0.0.1");
            name.TextChanged += (o, e) => _dnsName = name.Text;
            p1.TextChanged += (o, e) => _dnsPrimary = p1.Text;
            p2.TextChanged += (o, e) => _dnsSecondary = p2.Text;
            var hint = new TextBlock { Style = Ui.Style("T.Body"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            AutomationProperties.SetLiveSetting(hint, AutomationLiveSetting.Polite);
            void ShowHint()
            {
                var err = ListActions.ValidateDnsServers(p1.Text, p2.Text);
                bool empty = string.IsNullOrWhiteSpace(p1.Text) && string.IsNullOrWhiteSpace(p2.Text);
                hint.Text = empty ? "IPv4 addresses only, written like 1.1.1.1." : err ?? "Both addresses look valid.";
                hint.Foreground = Ui.Res(empty ? "B.TextMute" : err == null ? "B.Mint" : "B.Amber");
            }
            p1.TextChanged += (o, e) => ShowHint(); p2.TextChanged += (o, e) => ShowHint();
            ShowHint();
            var review = new TextBlock { Style = Ui.Style("T.Body"), TextWrapping = TextWrapping.Wrap, Foreground = Ui.Res("B.Text") };

            var steps = new List<GuidedStep>
            {
                new GuidedStep
                {
                    Title = "Name the profile", Help = "A short label that appears in the list and in the notice when you switch. A profile with the same name is replaced.",
                    Build = () => Labeled("Name", name, "Profile name"),
                    Validate = () => ListActions.ValidateDnsName(name.Text)
                },
                new GuidedStep
                {
                    Title = "Enter the servers", Help = "The primary server is used first. The secondary is a fallback and may be left empty. Only IPv4 addresses are supported.",
                    Build = () => { var sp = new StackPanel(); sp.Children.Add(Labeled("Primary", p1, "Primary DNS server")); sp.Children.Add(Labeled("Secondary (optional)", p2, "Secondary DNS server")); sp.Children.Add(hint); return sp; },
                    Validate = () => ListActions.ValidateDnsServers(p1.Text, p2.Text)
                },
                new GuidedStep
                {
                    Title = "Review and save", Help = "Saving only adds it to the list. You are then asked whether to switch to it right away.",
                    Build = () => review,
                    Entered = () =>
                    {
                        var replaces = s.DnsProfiles.Any(x => x.Name.Equals(name.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                        review.Text = $"Name: {name.Text.Trim()}\nPrimary: {p1.Text.Trim()}\nSecondary: {(string.IsNullOrWhiteSpace(p2.Text) ? "none" : p2.Text.Trim())}"
                            + (replaces ? "\nA profile with this name exists and will be replaced." : "");
                    }
                },
            };
            var form = new GuidedForm("Add a DNS profile", steps, "Save profile", () =>
            {
                var err = ListActions.ValidateDnsName(name.Text) ?? ListActions.ValidateDnsServers(p1.Text, p2.Text);
                if (err != null) return err;
                var prof = new DnsProfile { Name = name.Text.Trim(), Primary = p1.Text.Trim(), Secondary = p2.Text.Trim() };
                s.DnsProfiles.RemoveAll(x => x.Name.Equals(prof.Name, StringComparison.OrdinalIgnoreCase));
                s.DnsProfiles.Add(prof);
                Settings.Save();
                _dnsOffer = prof;
                return null;
            }, () => { _dnsGuiding = false; Navigate("network", animate: false); });
            form.Finished += () => { _dnsGuiding = false; _dnsName = _dnsPrimary = _dnsSecondary = ""; Navigate("network", animate: false); };
            return form;
        }
    }
}
