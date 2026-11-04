using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MonitoringTools.Clip;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        // The safety page: toy locks, the built-in authenticator, the local recovery desk and the change history.
        // Register it as the section id "safety" (see the report for the exact line).
        private FrameworkElement SafetyPage()
        {
            Locks.Start();
            var root = PageRoot("Safety", "Locks, authenticator and recovery",
                "Fun locks for elements, a local authenticator, and a make-believe support desk that tells you how to get unstuck. Everything stays on this computer.");
            root.Children.Add(SafetyLocksCard());
            root.Children.Add(SafetyAuthenticatorCard());
            root.Children.Add(SafetyTicketsCard());
            root.Children.Add(SafetyHistoryCard());
            return root;
        }

        // -------------------------------------------------------------- shared bits

        private static FrameworkElement SafetyChips<T>(string label, IEnumerable<(string Label, T Value)> options, T current, Action<T> set, string tip)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), Margin = new Thickness(0, 0, 0, 8) });
            var wrap = new WrapPanel();
            var buttons = new List<(Button B, T V)>();
            void Paint()
            {
                foreach (var (b, v) in buttons)
                {
                    bool on = EqualityComparer<T>.Default.Equals(v, current);
                    b.Background = on ? Ui.Res("B.Mint") : Ui.Res("B.Raised");
                    b.Foreground = on ? Ui.Res("B.MintInk") : Ui.Res("B.TextSoft");
                    b.BorderBrush = on ? Ui.Res("B.Mint") : Ui.Res("B.LineStrong");
                }
            }
            foreach (var (l, v) in options)
            {
                var val = v;
                var b = Ui.Button(l, null, "Btn.Chip");
                b.ToolTip = tip + ": " + l;
                b.Click += (s, e) => { current = val; set(val); Paint(); };
                buttons.Add((b, v));
                wrap.Children.Add(b);
            }
            Paint();
            sp.Children.Add(wrap);
            return sp;
        }

        private static TextBlock SafetyLine(string text, string brush = "B.TextMute", double size = 12, double top = 0) =>
            new TextBlock { Text = text, Style = Ui.Style("T.Body"), FontSize = size, Foreground = Ui.Res(brush), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0) };

        private static void SafetyToast(string title, string body) => App.Toasts.ShowNotice(title, body, App.Toasts.Amber, 5);

        private static string SafetyWhen(DateTime t) => t.ToString("yyyy-MM-dd HH:mm");

        /// <summary>Wires a rebuild to a store's change event for as long as the content is on screen.</summary>
        private static void SafetyLive(FrameworkElement host, Action<Action> subscribe, Action<Action> unsubscribe, Action rebuild)
        {
            Action handler = () => RunOnUi(rebuild);
            host.Loaded += (s, e) => { unsubscribe(handler); subscribe(handler); };
            host.Unloaded += (s, e) => unsubscribe(handler);
        }

        private static string SafetySave(string suggested, string filter, string text)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = suggested, Filter = filter };
            if (dlg.ShowDialog() != true) return null;
            try { File.WriteAllText(dlg.FileName, text, new UTF8Encoding(false)); return dlg.FileName; }
            catch (Exception ex) { return "Could not save: " + ex.Message; }
        }

        // -------------------------------------------------------------- locks

        private FrameworkElement SafetyLocksCard()
        {
            var box = new StackPanel();
            box.Children.Add(CardTitle("Element locks", "Right-click any button, setting or card and choose Lock this element. A locked element is dimmed and cannot be used until you unlock it here or from its menu."));
            box.Children.Add(SafetyLine(Locks.Disclosure, "B.TextSoft", 13));
            box.Children.Add(SafetyLine(Locks.RecoveryLine, "B.TextMute", 12, 4));
            if (Locks.LoadError != null) box.Children.Add(SafetyLine("The lock store could not be read, so no locks are applied: " + Locks.LoadError, "B.Amber", 13, 6));

            var s = Settings.Current;
            box.Children.Add(SafetyChips("New locks stay unlocked for (default 5 minutes)",
                new[] { ("Until the app closes", 0), ("5 minutes", 5), ("15 minutes", 15), ("1 hour", 60) }, s.LockUnlockMinutes,
                v => { s.LockUnlockMinutes = v; Settings.Save(); }, "Default unlock time for the next lock you make"));
            box.Children.Add(Switch("Hide locked elements from page search", "Off by default, so locked items are shown and marked as locked instead of quietly disappearing.", s.LocksHiddenInSearch, v => s.LocksHiddenInSearch = v));

            var search = new SearchField("safety:locks", "Search locks") { Margin = new Thickness(0, 10, 0, 10) };
            box.Children.Add(search);
            var list = new StackPanel();
            box.Children.Add(list);

            void Rebuild()
            {
                var all = Locks.All;
                var hits = all.Where(l => search.State.Matches(l.Label + " " + l.PageId + " " + Locks.Describe(l.Policy))).ToList();
                list.Children.Clear();
                if (all.Count == 0)
                    list.Children.Add(SafetyLine("No locks yet. Lock something from its right-click menu and it will be listed here.", "B.TextMute", 13));
                foreach (var l in hits)
                {
                    var open = Locks.IsOpen(l);
                    var g = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var text = new StackPanel();
                    text.Children.Add(new TextBlock { Text = l.Label, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                    var wait = Locks.WaitSeconds(l.Id);
                    text.Children.Add(SafetyLine($"{(open ? "Unlocked" : "Locked")} · {Locks.Describe(l.Policy)} · page {l.PageId} · made {SafetyWhen(l.CreatedAt)}" + (wait > 0 ? $" · waiting {SafetyUi.Clock(wait)}" : "")));
                    g.Children.Add(text);
                    var btns = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(btns, 1);
                    var lk = l;
                    if (open)
                    {
                        btns.Children.Add(Ui.Button("Lock again", () => Locks.Relock(lk.Id), "Btn.Ghost", Icons.Shield));
                        btns.Children.Add(Ui.Button("Remove", () =>
                        {
                            var err = Locks.Remove(lk.Id);
                            if (err != null) SafetyToast("Lock not removed", err);
                        }, "Btn.Danger", Icons.Trash));
                    }
                    else
                    {
                        var unlock = Ui.Button("Unlock", null, "Btn.Accent", Icons.Shield);
                        unlock.Click += (a, b) => ElementMenu.PromptEntry(lk, unlock);
                        unlock.ToolTip = "Opens the unlock prompt for this lock";
                        btns.Children.Add(unlock);
                    }
                    g.Children.Add(btns);
                    SearchScope.SetItem(g, "lock " + Locks.Describe(l.Policy));
                    list.Children.Add(g);
                }
                search.SetCount(hits.Count, all.Count, "locks");
            }

            search.Filter += Rebuild;
            SafetyLive(box, a => Locks.Changed += a, a => Locks.Changed -= a, Rebuild);
            Rebuild();
            return SearchScope.SetItem(Card(box), "element locks toy lock pin password");
        }

        // -------------------------------------------------------------- authenticator

        private FrameworkElement SafetyAuthenticatorCard()
        {
            var box = new StackPanel();
            Totp.SelfTest(out var vectors);
            box.Children.Add(CardTitle("Authenticator", "Time-based one-time codes (RFC 6238) for any account, calculated here. Secrets are kept in a Windows-protected file and never in settings, logs or history."));
            box.Children.Add(SafetyLine("Self-check: " + vectors + ".", Totp.SelfTest(out _) ? "B.Mint" : "B.Danger", 12));
            if (Authenticator.LoadError != null) box.Children.Add(SafetyLine("The account store could not be read, so changes are blocked: " + Authenticator.LoadError, "B.Amber", 13, 6));

            // adding
            var addBox = new StackPanel { Margin = new Thickness(0, 10, 0, 6) };
            addBox.Children.Add(new TextBlock { Text = "Add an account", Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
            addBox.Children.Add(SafetyLine("Paste an otpauth:// link, or just a base32 secret. A link brings its issuer, algorithm, digits and period with it. Default for a bare secret: SHA-1, 6 digits, 30 seconds.", "B.TextMute", 12, 2));
            var secretIn = Input("", 560, "otpauth:// link or base32 secret");
            AutomationProperties.SetName(secretIn, "otpauth link or base32 secret");
            addBox.Children.Add(secretIn);
            var issuerIn = Input("", 200, "Issuer, for example the website"); AutomationProperties.SetName(issuerIn, "Issuer");
            var accountIn = Input("", 240, "Account, for example your email"); AutomationProperties.SetName(accountIn, "Account");
            var nameRow = new WrapPanel(); nameRow.Children.Add(issuerIn); nameRow.Children.Add(accountIn);
            addBox.Children.Add(nameRow);
            string algo = "SHA1"; int digits = 6; int period = 30;
            addBox.Children.Add(SafetyChips("Algorithm for a bare secret", Totp.Algorithms.Select(a => (a, a)), algo, v => algo = v, "Algorithm"));
            addBox.Children.Add(SafetyChips("Digits", new[] { ("6", 6), ("7", 7), ("8", 8) }, digits, v => digits = v, "Digits"));
            addBox.Children.Add(SafetyChips("Period", new[] { ("30 s", 30), ("60 s", 60), ("15 s", 15) }, period, v => period = v, "Period in seconds"));
            var addMsg = SafetyLine("", "B.TextSoft", 13, 4);
            AutomationProperties.SetLiveSetting(addMsg, AutomationLiveSetting.Polite);
            AutomationProperties.SetName(addMsg, "Add account status");

            var addRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            addRow.Children.Add(Ui.Button("Add account", () =>
            {
                var input = secretIn.Text.Trim();
                if (input.Length == 0) { addMsg.Text = "Paste an otpauth link or a secret first."; return; }
                OtpParams p;
                if (input.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Totp.TryParseUri(input, out p, out var perr)) { addMsg.Text = perr; return; }
                    if (issuerIn.Text.Trim().Length > 0) p.Issuer = issuerIn.Text.Trim();
                    if (accountIn.Text.Trim().Length > 0) p.Account = accountIn.Text.Trim();
                }
                else
                {
                    if (!Base32.TryDecode(input, out _)) { addMsg.Text = "That is neither an otpauth link nor a valid base32 secret."; return; }
                    p = new OtpParams { Issuer = issuerIn.Text.Trim(), Account = accountIn.Text.Trim(), Secret = Base32.Normalize(input), Algorithm = algo, Digits = digits, Period = period };
                    if (p.Account.Length == 0 && p.Issuer.Length == 0) { addMsg.Text = "Give it a name: an issuer or an account."; return; }
                }
                var err = Authenticator.Add(p);
                addMsg.Text = err ?? "Added. The secret is now in the protected file and is not shown again.";
                if (err == null) { secretIn.Text = ""; issuerIn.Text = ""; accountIn.Text = ""; }
            }, "Btn.Accent", Icons.Plus));
            addRow.Children.Add(Ui.Button("Paste from clipboard", () =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText()) { secretIn.Text = System.Windows.Clipboard.GetText().Trim(); addMsg.Text = "Pasted. Check it, then press Add account."; }
                    else if (System.Windows.Clipboard.ContainsImage()) addMsg.Text = "There is a picture on the clipboard, but this build cannot read QR codes from pictures. See the note below.";
                    else addMsg.Text = "The clipboard has nothing to paste.";
                }
                catch (Exception ex) { addMsg.Text = "The clipboard could not be read: " + ex.Message; }
            }, "Btn.Ghost", Icons.Clipboard));
            var qr = Ui.Button("Read a QR image", () =>
            {
                addMsg.Text = "QR image reading is not available in this build. Windows has no built-in QR decoder and no decoder library is included. Use the otpauth link instead: most sign-in pages show it under a link such as \"Can't scan?\", or a phone's QR scanner shows it as text you can copy.";
            }, "Btn.Ghost", Icons.Image);
            qr.ToolTip = "Not available: this build has no QR decoder. Shows what to do instead.";
            addRow.Children.Add(qr);
            addBox.Children.Add(addRow);
            addBox.Children.Add(addMsg);
            box.Children.Add(addBox);

            box.Children.Add(Switch("Show the next code too", "A small peek at the code that follows, so you do not type one with two seconds left. Default: on.", Settings.Current.AuthenticatorShowNext, v => Settings.Current.AuthenticatorShowNext = v));
            box.Children.Add(SafetyLine("Codes come from this computer's clock. This app is offline, so it cannot tell if the clock is wrong. If a site rejects a fresh code, sync the clock in Windows Settings, Time and language.", "B.TextMute", 12, 0));

            // list
            var search = new SearchField("safety:auth", "Search accounts") { Margin = new Thickness(0, 12, 0, 10) };
            box.Children.Add(search);
            var list = new StackPanel();
            box.Children.Add(list);
            var tickers = new List<Action>();

            void Rebuild()
            {
                var all = Authenticator.All;
                var hits = all.Where(a => search.State.Matches(a.Issuer + " " + a.Account)).ToList();
                list.Children.Clear(); tickers.Clear();
                if (all.Count == 0) list.Children.Add(SafetyLine("No accounts yet. Add one above.", "B.TextMute", 13));
                foreach (var a in hits) list.Children.Add(SafetyAccountRow(a, tickers));
                search.SetCount(hits.Count, all.Count, "accounts");
            }
            search.Filter += Rebuild;

            var exportRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var exp = Ui.Button("Export list without secrets", () =>
            {
                var sb = new StringBuilder("# Authenticator accounts\n\nSecrets are left out on purpose. This list has issuers, accounts and settings only.\n\n");
                foreach (var a in Authenticator.All) sb.Append($"- {a.Title} ({a.Algorithm}, {a.Digits} digits, {a.Period} s)\n");
                var r = SafetySave("authenticator-accounts.md", "Markdown (*.md)|*.md", sb.ToString());
                if (r != null) SafetyToast("Export", r.StartsWith("Could not") ? r : "Saved without secrets: " + r);
            }, "Btn.Ghost", Icons.Archive);
            exp.ToolTip = "Writes issuers, accounts and settings. Secrets are always left out, and the file says so.";
            exportRow.Children.Add(exp);
            exportRow.Children.Add(SafetyLine("A secrets export is not offered. Any export here leaves secrets out and says so.", "B.TextMute", 12));
            box.Children.Add(exportRow);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => { foreach (var t in tickers.ToList()) t(); };
            box.Loaded += (s, e) => timer.Start();
            box.Unloaded += (s, e) => timer.Stop();
            SafetyLive(box, a => Authenticator.Changed += a, a => Authenticator.Changed -= a, Rebuild);
            Rebuild();
            return SearchScope.SetItem(Card(box), "authenticator totp one-time codes qr otpauth");
        }

        private FrameworkElement SafetyAccountRow(AuthAccount a, List<Action> tickers)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = a.Title, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
            var p = a.ToParams();
            var code = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 28, Foreground = Ui.Res("B.Mint"), Margin = new Thickness(0, 2, 0, 0) };
            AutomationProperties.SetLiveSetting(code, AutomationLiveSetting.Polite);
            AutomationProperties.SetName(code, "Current code for " + a.Title);
            var next = SafetyLine("", "B.TextMute", 12);
            var secs = SafetyLine("", "B.TextSoft", 12);
            left.Children.Add(code); left.Children.Add(next);
            left.Children.Add(SafetyLine($"{a.Algorithm} · {a.Digits} digits · {a.Period} s", "B.TextMute", 11));

            // countdown ring with the seconds in the middle, so it is never colour only
            const double D = 40, T = 4;
            double circ = Math.PI * (D - T) / T;
            var ring = new Ellipse { Width = D, Height = D, Stroke = Ui.Res("B.Mint"), StrokeThickness = T, StrokeDashCap = PenLineCap.Round, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(-90) };
            var track = new Ellipse { Width = D, Height = D, Stroke = Ui.Res("B.Line"), StrokeThickness = T };
            var ringBox = new Grid { Width = D, Height = D, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            ringBox.Children.Add(track); ringBox.Children.Add(ring);
            ringBox.Children.Add(new TextBlock { Text = "", Foreground = Ui.Res("B.Text"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var secLabel = (TextBlock)ringBox.Children[2];
            AutomationProperties.SetName(ringBox, "Seconds left for " + a.Title);

            string lastCode = null;
            void Tick()
            {
                long now = Totp.NowUnix();
                var c = Totp.CodeFor(p, now);
                int left2 = Totp.SecondsLeft(p, now);
                if (c == null) { code.Text = "unreadable"; return; }
                if (c != lastCode)
                {
                    lastCode = c;
                    code.Text = c.Length == 6 ? c.Substring(0, 3) + " " + c.Substring(3) : c.Length == 8 ? c.Substring(0, 4) + " " + c.Substring(4) : c;
                }
                secLabel.Text = left2.ToString();
                ring.StrokeDashArray = new DoubleCollection { circ * left2 / p.Period, circ };
                secs.Text = left2 + " seconds left";
                var n = Settings.Current.AuthenticatorShowNext ? Totp.CodeFor(p, now, 1) : null;
                next.Text = n == null ? "" : "Next: " + (n.Length == 6 ? n.Substring(0, 3) + " " + n.Substring(3) : n);
                next.Visibility = n == null ? Visibility.Collapsed : Visibility.Visible;
            }
            tickers.Add(Tick);
            Tick();

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(right, 1);
            right.Children.Add(ringBox);
            var btns = new WrapPanel { MaxWidth = 250, VerticalAlignment = VerticalAlignment.Center };
            var copy = Ui.Button("Copy", () => { var c = Totp.CodeFor(p, Totp.NowUnix()); if (c != null) ClipboardWatcher.SetText(c); }, "Btn.Ghost", Icons.Clipboard);
            copy.ToolTip = "Copy the current code";
            btns.Children.Add(copy);
            var up = Ui.Button("↑", () => Authenticator.Move(a.Id, -1), "Btn.Icon"); up.ToolTip = "Move up"; AutomationProperties.SetName(up, "Move " + a.Title + " up");
            var dn = Ui.Button("↓", () => Authenticator.Move(a.Id, 1), "Btn.Icon"); dn.ToolTip = "Move down"; AutomationProperties.SetName(dn, "Move " + a.Title + " down");
            btns.Children.Add(up); btns.Children.Add(dn);
            btns.Children.Add(Ui.Button("Edit label", () => SafetyEditLabel(a), "Btn.Ghost"));
            var rm = Ui.Button("Remove", () =>
            {
                var dlg = new ConfirmWindow("Remove this account?", "Its secret is deleted from this computer. Make sure you can still sign in to " + a.Title + " another way first.", "Remove", danger: true);
                dlg.Confirmed += _ => { var err = Authenticator.Remove(a.Id); if (err != null) SafetyToast("Authenticator", err); };
                dlg.Show();
            }, "Btn.Danger", Icons.Trash);
            btns.Children.Add(rm);
            right.Children.Add(btns);

            left.Children.Add(secs);
            g.Children.Add(left); g.Children.Add(right);
            return SearchScope.SetItem(g, "authenticator account " + a.Issuer + " " + a.Account);
        }

        private void SafetyEditLabel(AuthAccount a)
        {
            var win = new Window
            {
                Title = "Edit label", Width = 420, SizeToContent = SizeToContent.Height, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize, Background = Ui.Res("B.Raised"), ShowInTaskbar = false
            };
            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(SafetyLine("The label changes. The secret is never shown or changed.", "B.TextSoft", 13));
            var issuer = Input(a.Issuer, double.NaN, "Issuer"); issuer.Margin = new Thickness(0, 10, 0, 6); AutomationProperties.SetName(issuer, "Issuer");
            var account = Input(a.Account, double.NaN, "Account"); account.Margin = new Thickness(0, 0, 0, 6); AutomationProperties.SetName(account, "Account");
            sp.Children.Add(issuer); sp.Children.Add(account);
            var msg = SafetyLine("", "B.Amber", 12);
            sp.Children.Add(msg);
            var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(Ui.Button("Save", () =>
            {
                var err = Authenticator.Rename(a.Id, issuer.Text.Trim(), account.Text.Trim());
                if (err != null) msg.Text = err; else win.Close();
            }, "Btn.Accent", Icons.Check));
            row.Children.Add(Ui.Button("Cancel", win.Close, "Btn.Ghost"));
            sp.Children.Add(row);
            win.Content = sp;
            win.Show();
        }

        // -------------------------------------------------------------- support tickets

        private FrameworkElement SafetyTicketsCard()
        {
            var box = new StackPanel();
            box.Children.Add(CardTitle("Support Tickets", "A local recovery desk for when a lock or an authenticator gets you stuck."));
            // plain, unstyled, always shown
            var notice = SafetyLine(TicketDesk.Disclosure, "B.Text", 13);
            notice.FontWeight = FontWeights.SemiBold;
            AutomationProperties.SetName(notice, "Support desk notice");
            box.Children.Add(notice);

            string category = TicketDesk.Categories[0], severity = TicketDesk.Severities[1];
            box.Children.Add(SafetyChips("What happened", TicketDesk.Categories.Select(c => (c, c)), category, v => category = v, "Category"));
            box.Children.Add(SafetyChips("Severity (nobody will honour it)", TicketDesk.Severities.Select(c => (c, c)), severity, v => severity = v, "Severity"));
            var desc = new TextBox { Style = Ui.Style("Input"), Height = 80, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(0, 8, 0, 8), Margin = new Thickness(0, 6, 0, 6), MaxLength = 1000 };
            AutomationProperties.SetName(desc, "Describe what happened");
            desc.ToolTip = "Describe what you were locked out of";
            box.Children.Add(desc);
            var msg = SafetyLine("", "B.TextSoft", 13);
            AutomationProperties.SetLiveSetting(msg, AutomationLiveSetting.Polite);
            box.Children.Add(Ui.Button("File a ticket", () =>
            {
                if (desc.Text.Trim().Length < 5) { msg.Text = "Describe what happened first, in a few words."; return; }
                var t = TicketDesk.Submit(category, severity, desc.Text);
                desc.Text = "";
                msg.Text = "Ticket " + t.Number + " filed on this computer.";
            }, "Btn.Accent", Icons.Plus));
            box.Children.Add(msg);

            var folder = SafetyLine("The app data folder is: " + Settings.DataDir, "B.TextSoft", 12, 6);
            folder.FontFamily = Ui.Font("F.Mono");
            box.Children.Add(folder);
            var copyPath = Ui.Button("Copy folder path", () => ClipboardWatcher.SetText(Settings.DataDir), "Btn.Chip", Icons.Clipboard);
            box.Children.Add(copyPath);

            var search = new SearchField("safety:tickets", "Search tickets") { Margin = new Thickness(0, 10, 0, 10) };
            box.Children.Add(search);
            var list = new StackPanel();
            box.Children.Add(list);

            void Rebuild()
            {
                var all = TicketDesk.All();
                var hits = all.Where(t => search.State.Matches(t.Number + " " + t.Category + " " + t.Description + " " + TicketDesk.Status(t))).ToList();
                list.Children.Clear();
                if (all.Count == 0) list.Children.Add(SafetyLine("No tickets. Good for you.", "B.TextMute", 13));
                foreach (var t in hits)
                {
                    var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
                    sp.Children.Add(new TextBlock { Text = t.Number + " · " + t.Category, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                    sp.Children.Add(SafetyLine($"{TicketDesk.Status(t)} · severity {t.Severity} · filed {SafetyWhen(t.FiledAt)}", "B.TextMute", 12));
                    sp.Children.Add(SafetyLine(t.Description, "B.TextSoft", 13, 4));
                    if (TicketDesk.Status(t) != "New") sp.Children.Add(SafetyLine(TicketDesk.FirstResponse(t), "B.TextSoft", 12, 4));
                    if (TicketDesk.ResolutionReady(t))
                    {
                        var tt = t;
                        var open = Ui.Button("Open the app data folder", () =>
                        {
                            var err = TicketDesk.OpenFolder(tt);
                            msg.Text = err ?? "Explorer opened the folder. Deleting anything in it is up to you.";
                        }, "Btn.Accent", Icons.Folder);
                        open.ToolTip = "Opens " + Settings.DataDir + " in Windows Explorer. The app deletes nothing.";
                        sp.Children.Add(open);
                    }
                    SearchScope.SetItem(sp, "support ticket " + t.Number);
                    list.Children.Add(sp);
                }
                search.SetCount(hits.Count, all.Count, "tickets");
            }
            search.Filter += Rebuild;

            var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            row.Children.Add(Ui.Button("Export tickets", () =>
            {
                var r = SafetySave("support-tickets.md", "Markdown (*.md)|*.md", TicketDesk.ExportMarkdown());
                if (r != null) SafetyToast("Export", r);
            }, "Btn.Ghost", Icons.Archive));
            row.Children.Add(Ui.Button("Delete all tickets", () =>
            {
                var dlg = new ConfirmWindow("Delete every ticket?", "This removes the local ticket list. It cannot be undone, and no copy exists anywhere else.", "Delete all", danger: true);
                dlg.Confirmed += _ => TicketDesk.Remove(TicketDesk.All());
                dlg.Show();
            }, "Btn.Danger", Icons.Trash));
            box.Children.Add(row);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) => { if (!SearchField.AnyActive && !desc.IsKeyboardFocusWithin) Rebuild(); };
            box.Loaded += (s, e) => timer.Start();
            box.Unloaded += (s, e) => timer.Stop();
            SafetyLive(box, a => TicketDesk.Changed += a, a => TicketDesk.Changed -= a, () => { if (!desc.IsKeyboardFocusWithin) Rebuild(); });
            Rebuild();
            return SearchScope.SetItem(Card(box), "support tickets recovery desk lockout forgotten password");
        }

        // -------------------------------------------------------------- history

        private FrameworkElement SafetyHistoryCard()
        {
            var box = new StackPanel();
            box.Children.Add(CardTitle("Change history", "What changed in locks, authenticator accounts, tickets and the display name, and when. It records actions and labels only. No secret, PIN, password or code is ever written here."));
            var search = new SearchField("safety:history", "Search history") { Margin = new Thickness(0, 0, 0, 10) };
            box.Children.Add(search);
            var list = new StackPanel();
            box.Children.Add(list);
            const int Max = 100;

            void Rebuild()
            {
                var all = SafetyHistory.All();
                var hits = all.Where(h => search.State.Matches(h.Action + " " + h.Detail)).ToList();
                list.Children.Clear();
                if (all.Count == 0) list.Children.Add(SafetyLine("Nothing yet. Locks, accounts and display-name changes will be listed as they happen.", "B.TextMute", 13));
                foreach (var h in hits.Take(Max))
                {
                    var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.Children.Add(Ui.Mono(SafetyWhen(h.At), Ui.Res("B.TextMute")));
                    var tx = new StackPanel(); Grid.SetColumn(tx, 1);
                    tx.Children.Add(new TextBlock { Text = h.Action, Foreground = Ui.Res("B.Text"), FontSize = 13, FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                    if (h.Detail.Length > 0) tx.Children.Add(SafetyLine(h.Detail, "B.TextMute", 12));
                    g.Children.Add(tx);
                    SearchScope.SetItem(g, "safety history");
                    list.Children.Add(g);
                }
                if (hits.Count > Max) list.Children.Add(SafetyLine($"Showing the newest {Max} of {hits.Count}. Narrow the search to see others.", "B.TextMute", 12));
                search.SetCount(hits.Count, all.Count, "entries");
            }
            search.Filter += Rebuild;
            SafetyLive(box, a => SafetyHistory.Changed += a, a => SafetyHistory.Changed -= a, () => { if (!SearchField.AnyActive) Rebuild(); });
            Rebuild();
            return SearchScope.SetItem(Card(box), "safety history log");
        }
    }
}
