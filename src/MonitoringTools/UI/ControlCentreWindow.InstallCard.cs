using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        /// <summary>The Clipboard page card for automatic install of copied choco and winget commands (design board 4, card G).
        /// Off by default; the rules the code enforces are written next to the switch.</summary>
        private FrameworkElement InstallCommandsCard()
        {
            var s = Settings.Current;
            var card = new StackPanel();
            card.Children.Add(CardTitle("Install copied commands automatically", "Clipboard"));

            var numbers = new StackPanel { IsEnabled = s.AutoInstallCommands, Opacity = s.AutoInstallCommands ? 1 : 0.45 };
            var sw = Switch("Install without asking first",
                "Off by default. When on, a plain install command starts after a short delay with a Cancel button.",
                s.AutoInstallCommands, v =>
                {
                    s.AutoInstallCommands = v;
                    numbers.IsEnabled = v; numbers.Opacity = v ? 1 : 0.45;
                }, "B.Amber");
            card.Children.Add(sw);

            var warn = new Border
            {
                Background = Ui.Res("B.AmberWash"), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 0, 0, Appearance.Gap(12))
            };
            var warnRow = new StackPanel { Orientation = Orientation.Horizontal };
            warnRow.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Shield, Ui.Res("B.Amber"), 16, 2), Margin = new Thickness(0, 1, 10, 0), VerticalAlignment = VerticalAlignment.Top });
            warnRow.Children.Add(new TextBlock
            {
                Text = "Turn this on only if you trust everything you copy. Anything on the clipboard that matches the rules below will install software.",
                Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap, MaxWidth = 560
            });
            warn.Child = warnRow;
            card.Children.Add(warn);

            numbers.Children.Add(Number("Wait before starting", "How long the Cancel toast shows before an automatic install starts. 3 to 60 seconds. Default: 10.",
                s.AutoInstallDelaySeconds, 3, 60, 1, v => s.AutoInstallDelaySeconds = (int)v, "seconds"));
            numbers.Children.Add(Number("Most packages per command", "A command that names more packages than this is shown as a toast and never starts by itself. 1 to 10. Default: 5.",
                s.AutoInstallMaxPackages, 1, 10, 1, v => s.AutoInstallMaxPackages = (int)v, "packages"));
            card.Children.Add(numbers);

            // The rules the code enforces, written where the switch is.
            var rules = new StackPanel { Margin = new Thickness(0, Appearance.Gap(6), 0, 0) };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            head.Children.Add(new ContentControl { Content = Ui.Icon(Icons.Check, Ui.Res("B.Mint"), 14, 2.4), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(new TextBlock { Text = "Installed automatically only when", Foreground = Ui.Res("B.Mint"), FontSize = 13, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center });
            rules.Children.Add(head);
            int n = 1;
            foreach (var rule in new[]
            {
                "The clipboard is exactly one command: choco install <name> [-y], choco upgrade <name> [-y] or winget install --id <id> [-e].",
                "No pipes, &&, ;, redirects, variables or quotes that run code.",
                "Package names use letters, digits, dots, dashes and underscores only.",
                "The default source is used. A custom source is refused.",
                "Windows shows its consent prompt every time for choco. The app never answers, hides or pre-approves it.",
                "Each run is logged in Activity with the exact command.",
            })
                rules.Children.Add(new TextBlock { Text = (n++) + ". " + rule, Style = Ui.Style("T.Body"), FontSize = 13, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2), MaxWidth = 640 });
            card.Children.Add(rules);

            var refusedHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, Appearance.Gap(12), 0, 6) };
            refusedHead.Children.Add(new ContentControl { Content = Ui.Icon(ToastIcons.Block, Ui.Res("B.Amber"), 14, 2), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            refusedHead.Children.Add(new TextBlock { Text = "Refused flags", Foreground = Ui.Res("B.Amber"), FontSize = 13, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center });
            card.Children.Add(refusedHead);
            var chips = new WrapPanel { MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var f in new[]
            {
                "--source / -s", "--ignore-checksums", "--ignore-package-exit-codes", "--force / -f", "--params / --package-parameters",
                "--install-arguments", "--override", "--pre / --prerelease", "--allow-downgrade", "--scripts",
            })
            {
                var chip = Ui.Pill(f, Ui.Res("B.Sunken"), Ui.Res("B.TextSoft"), mono: true, size: 12);
                Core.Language.SetLiteral(chip, true);
                chips.Children.Add(chip);
            }
            chips.Children.Add(new Border
            {
                Background = Ui.Res("B.Sunken"), CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 3, 9, 4), Margin = new Thickness(0, 0, 6, 4),
                Child = new TextBlock { Text = "any flag not on the allowed list", FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Mono") }
            });
            card.Children.Add(chips);
            card.Children.Add(new TextBlock
            {
                Text = "A refused command still gets a toast that says why, with Copy command. It is never silently ignored.",
                Style = Ui.Style("T.Body"), FontSize = 12, Foreground = Ui.Res("B.TextMute"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), MaxWidth = 640
            });
            return card;
        }
    }
}
