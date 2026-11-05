using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI.Dialogs
{
    /// <summary>
    /// Hash and verify: SHA-256, SHA-1, MD5 and SHA-512 of any file, read off the UI thread with progress, and a check against the value the publisher
    /// gave. The file is never opened, run or moved from here.
    /// </summary>
    internal sealed class HashVerifyDialog : DataToolWindow
    {
        internal static HashVerifyDialog Current { get; private set; }

        private readonly string _path;
        private readonly Dictionary<string, string> _hashes = new Dictionary<string, string>();
        private readonly Choice _algo;
        private readonly BannerBox _banner = new BannerBox();
        private readonly TextBlock _hashLabel = Label("", true);
        private readonly ContentControl _hashHost = new ContentControl { Margin = new Thickness(0, 0, 0, 10) };
        private readonly WrapPanel _copyRow = new WrapPanel();
        private readonly TextBox _expected = new TextBox { Style = Ui.Style("Input"), Height = 42, MaxLength = 4000, AcceptsReturn = false };
        private readonly TextBlock _hint = new TextBlock { Text = "Paste here to check it", FontSize = 13, Foreground = Ui.Res("B.TextMute"), IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        private readonly TextBlock _expectedNote = Note("Spaces, dashes and capitals are ignored. Pasting a whole checksum line works too.");
        private readonly ContentControl _compare = new ContentControl();
        private string _algorithm = HashEngine.Algorithms[0];
        private bool _running, _pendingVerify;
        private DateTime _startedAt;
        private TaskCompletionSource<bool> _ready = new TaskCompletionSource<bool>();
        private string _lastTime = "";

        internal string Algorithm => _algorithm;
        internal BannerBox Banner => _banner;
        internal TextBox ExpectedBox => _expected;
        internal bool Running => _running;
        internal string ShownHash(string algorithm) => _hashes.TryGetValue(algorithm, out var h) ? h : null;
        /// <summary>Completes when the hash being computed is in (tests wait on it).</summary>
        internal Task Ready => _ready.Task;
        internal void ChooseAlgorithm(int i) => _algo.Select(i, notify: true);

        /// <summary>Opens the dialog for one file and starts computing SHA-256 straight away.</summary>
        public static HashVerifyDialog Open(string path)
        {
            if (!File.Exists(path)) { App.Toasts.ShowNotice("Nothing to check", "The file is no longer there.", App.Toasts.Amber, 4, icon: Icons.Bolt); return null; }
            var d = new HashVerifyDialog(path);
            Current = d;
            d.Present();
            d.Begin();
            return d;
        }

        private HashVerifyDialog(string path) : base("Hash and verify", path, 760, DataToolIcons.Hash)
        {
            _path = path;
            long size = 0; try { size = new FileInfo(path).Length; } catch { }
            SetSummary(Ui.Bytes(size));
            _algo = Chips("Algorithm", HashEngine.Algorithms, 0, i => { _algorithm = HashEngine.Algorithms[i]; ClearVerdict(); Begin(); });
            Body.Children.Add(_algo.Element);
            Body.Children.Add(Note("MD5 and SHA-1 are fine for spotting damage but not for proving authenticity."));
            Body.Children.Add(_banner);
            Body.Children.Add(_hashLabel);
            Body.Children.Add(_hashHost);

            var copy = Action("Copy hash", DataToolIcons.Copy, () => { if (Current_Hash() is string h) { ClipboardWatcher.SetText(h); Status("Copied the " + _algorithm + " hash."); } }, "Btn.Ghost");
            var line = Action("Copy as file line", null, () => { if (Current_Hash() is string h) { ClipboardWatcher.SetText(HashEngine.FileLine(h, Path.GetFileName(_path))); Status("Copied a checksum line: hash, a space, then the file name."); } }, "Btn.Ghost");
            copy.ToolTip = "Copies the hash alone."; line.ToolTip = "Copies a line in the format checksum files use: the hash, then * and the file name.";
            _copyRow.Children.Add(copy); _copyRow.Children.Add(line);
            Body.Children.Add(_copyRow);

            Body.Children.Add(Label("Expected value (paste the publisher's checksum)", false, new Thickness(0, 6, 0, 6)));
            var field = new Grid();
            field.Children.Add(_expected); field.Children.Add(_hint);
            var paste = Action("Paste", null, PasteFromClipboard, "Btn.Ghost");
            paste.Margin = new Thickness(8, 0, 0, 0); paste.Height = 42;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(paste, 1); row.Children.Add(field); row.Children.Add(paste);
            Body.Children.Add(row);
            AutomationProperties.SetName(_expected, "Expected checksum");
            _expected.ToolTip = "The checksum the publisher gave. Spaces, dashes and capitals are ignored. A whole line such as 'hash *file name' also works.";
            _expected.TextChanged += (s, e) => OnExpectedChanged();
            _expectedNote.Margin = new Thickness(0, 6, 0, 10);
            Body.Children.Add(_expectedNote);
            Body.Children.Add(_compare);

            SetCancel("Cancel");
            SetPrimary("Verify", DataToolIcons.Check, Verify, false);
            Status("Reading the file…");
            ShowProgress(0, size);
            Loaded += (s, e) => _expected.Focus();
        }

        private string Current_Hash() => _hashes.TryGetValue(_algorithm, out var h) ? h : null;

        private void PasteFromClipboard()
        {
            try { if (Clipboard.ContainsText()) _expected.Text = Clipboard.GetText().Trim(); } catch { }
            _expected.Focus();
        }

        // ------------------------------------------------------------------ computing

        private async void Begin()
        {
            if (_hashes.ContainsKey(_algorithm)) { ShowHash(); RefreshButtons(); if (_pendingVerify) { _pendingVerify = false; Verify(); } return; }
            var token = NewRun();
            _running = true; _startedAt = DateTime.UtcNow;
            _ready = new TaskCompletionSource<bool>();
            var algo = _algorithm;
            long total = 0; try { total = new FileInfo(_path).Length; } catch { }
            _hashLabel.Text = (algo + " of this file").ToUpperInvariant();
            _banner.Visibility = Visibility.Collapsed;
            ShowProgress(0, total);
            Status("Cancel stops reading; nothing is changed.");
            SetCancel("Cancel");
            RefreshButtons();
            var progress = new Progress<(long Done, long Total)>(p => { if (_running && algo == _algorithm) ShowProgress(p.Done, p.Total); });
            try
            {
                var res = await HashEngine.ComputeAsync(_path, new[] { algo }, progress, token);
                _hashes[algo] = res[algo];
                _running = false;
                var secs = Math.Max(0.001, (DateTime.UtcNow - _startedAt).TotalSeconds);
                _lastTime = "Computed in " + Duration(secs) + (total > 0 ? " · " + Math.Round(total / secs / 1048576.0) + " MB/s" : "");
                if (algo == _algorithm) { ShowHash(); Status(_lastTime); }
                RefreshButtons();
                _ready.TrySetResult(true);
                if (_pendingVerify && algo == _algorithm) { _pendingVerify = false; Verify(); }
            }
            catch (OperationCanceledException)
            {
                _running = false;
                if (!IsLoaded) return;
                _hashHost.Content = null; _hashLabel.Text = "";
                _banner.Set(DataBannerKind.Info, "Stopped", "Reading the file was cancelled. Nothing was changed. Choose an algorithm to start again.");
                Status("Stopped. Nothing was changed.");
                RefreshButtons();
                _ready.TrySetResult(false);
            }
            catch (Exception ex)
            {
                _running = false;
                _hashHost.Content = null; _hashLabel.Text = "";
                _banner.Set(DataBannerKind.Error, "The file could not be read", ex.Message + " Nothing was changed.");
                Status("Could not read the file.", true);
                RefreshButtons();
                _ready.TrySetResult(false);
            }
        }

        private static string Duration(double secs) => secs < 1 ? "under 1 s" : secs < 60 ? Math.Round(secs) + " s" : (int)(secs / 60) + " min " + (int)(secs % 60) + " s";

        private ProgressBar _bar; private TextBlock _pct, _detail; private Border _progressCard;

        private void ShowProgress(long done, long total)
        {
            if (_bar == null)
            {
                _bar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 10, 0, 10), Foreground = Ui.Res("B.Mint"), Background = Ui.Res("B.Line"), BorderThickness = new Thickness(0) };
                AutomationProperties.SetName(_bar, "Reading the file");
                _pct = new TextBlock { FontSize = 28, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Bottom };
                _detail = new TextBlock { FontSize = 13, Foreground = Ui.Res("B.TextSoft") };
            }
            double pct = total > 0 ? done * 100.0 / total : 0;
            _bar.Value = pct; _pct.Text = Math.Floor(pct) + "%";
            double secs = Math.Max(0.05, (DateTime.UtcNow - _startedAt).TotalSeconds);
            double rate = done / secs;
            string left = rate > 1 && done > 0 && done < total ? " · about " + Duration((total - done) / rate) + " left" : "";
            _detail.Text = Ui.Bytes(done) + " of " + Ui.Bytes(total) + (done > 0 ? " · " + Math.Round(rate / 1048576.0) + " MB/s" : "") + left;
            if (_progressCard == null)
            {
                var top = new StackPanel { Orientation = Orientation.Horizontal };
                top.Children.Add(_pct);
                top.Children.Add(new TextBlock { Text = "Reading the file", FontSize = 13, Foreground = Ui.Res("B.TextSoft"), Margin = new Thickness(12, 0, 0, 5), VerticalAlignment = VerticalAlignment.Bottom });
                var sp = new StackPanel(); sp.Children.Add(top); sp.Children.Add(_bar); sp.Children.Add(_detail);
                _progressCard = new Border { CornerRadius = new CornerRadius(16), Background = Ui.Res("B.Sunken"), Padding = new Thickness(16, 12, 16, 12), Child = sp };
            }
            _hashHost.Content = _progressCard;
            _hashLabel.Text = (_algorithm + " of this file").ToUpperInvariant();
            _copyRow.Visibility = Visibility.Collapsed;
        }

        private void ShowHash()
        {
            var h = Current_Hash();
            _hashLabel.Text = (_algorithm + " of this file").ToUpperInvariant();
            if (h == null) return;
            _hashHost.Content = HashBlock(Highlight(h, null, false, false));
            _copyRow.Visibility = Visibility.Visible;
        }

        private static Border HashBlock(TextBlock t) => new Border { CornerRadius = new CornerRadius(14), Background = Ui.Res("B.Sunken"), Padding = new Thickness(14, 10, 14, 10), Child = t };

        /// <summary>The hash in groups of eight; with <paramref name="other"/> the groups that differ are marked, with <paramref name="all"/> the whole value is.</summary>
        private static TextBlock Highlight(string hex, string other, bool danger, bool all)
        {
            var t = new TextBlock { FontFamily = Ui.Font("F.Mono"), FontSize = 13, Foreground = Ui.Res("B.Text"), TextWrapping = TextWrapping.Wrap };
            int groups = (hex.Length + 7) / 8;
            for (int i = 0; i < groups; i++)
            {
                var g = hex.Substring(i * 8, Math.Min(8, hex.Length - i * 8));
                var o = other != null && i * 8 < other.Length ? other.Substring(i * 8, Math.Min(8, other.Length - i * 8)) : null;
                var run = new Run(g);
                if (all) { run.Background = Ui.Res("B.MintWash"); run.Foreground = Ui.Res("B.OnPrimaryContainer"); }
                else if (other != null && g != o) { run.Background = danger ? Ui.Res("B.DangerWash") : Ui.Res("B.AmberWash"); run.Foreground = danger ? Ui.Res("B.Danger") : Ui.Res("B.Amber"); }
                t.Inlines.Add(run);
                if (i < groups - 1) t.Inlines.Add(new Run(all ? "  " : "  "));
            }
            return t;
        }

        // ------------------------------------------------------------------ the pasted value

        private void ClearVerdict()
        {
            _compare.Content = null;
            if (!_running) _banner.Visibility = Visibility.Collapsed;
            SetCancel("Cancel");
            SetPrimary("Verify", DataToolIcons.Check, Verify, false);
            _pendingVerify = false;
            _expected.BorderBrush = null;
        }

        private HashEngine.Expected Parsed => HashEngine.ParseExpected(_expected.Text, Path.GetFileName(_path));

        private bool _verdictShown;

        private void OnExpectedChanged()
        {
            _hint.Visibility = _expected.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_verdictShown) { _verdictShown = false; ClearVerdict(); }
            if (_hashes.ContainsKey(_algorithm) && !_running) ShowHash();
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            var p = string.IsNullOrWhiteSpace(_expected.Text) ? null : Parsed;
            if (p == null) { _expectedNote.Text = "Spaces, dashes and capitals are ignored. Pasting a whole checksum line works too."; _expectedNote.Foreground = Ui.Res("B.TextSoft"); }
            else if (p.Hex == null) { _expectedNote.Text = p.Problem; _expectedNote.Foreground = Ui.Res("B.Danger"); }
            else
            {
                var fit = HashEngine.AlgorithmForLength(p.Hex.Length);
                if (fit == null) { _expectedNote.Text = "That is " + p.Hex.Length + " characters. A checksum is 32 (MD5), 40 (SHA-1), 64 (SHA-256) or 128 (SHA-512) characters long."; _expectedNote.Foreground = Ui.Res("B.Danger"); }
                else if (fit != _algorithm) { _expectedNote.Text = "This is " + p.Hex.Length + " characters, which is " + fit + ". Verify will use " + fit + " instead of " + _algorithm + "."; _expectedNote.Foreground = Ui.Res("B.Amber"); }
                else { _expectedNote.Text = "That is " + p.Hex.Length + " characters, the length of " + fit + (p.FileName != null ? ", from the line for " + p.FileName : "") + "."; _expectedNote.Foreground = Ui.Res("B.TextSoft"); }
            }
            bool usable = p != null && p.Hex != null && HashEngine.AlgorithmForLength(p.Hex.Length) != null;
            PrimaryButton.IsEnabled = usable && !_running;
            _copyRow.Visibility = _hashes.ContainsKey(_algorithm) && !_running ? Visibility.Visible : Visibility.Collapsed;
        }

        // ------------------------------------------------------------------ verify

        internal void Verify()
        {
            var p = Parsed;
            if (p.Hex == null) return;
            var fit = HashEngine.AlgorithmForLength(p.Hex.Length);
            if (fit == null) return;
            if (fit != _algorithm)
            {
                _pendingVerify = true;
                _algo.Select(Array.IndexOf(HashEngine.Algorithms, fit), notify: false);
                _algorithm = fit; ClearVerdict(); _pendingVerify = true;
                Begin();
                return;
            }
            if (!_hashes.TryGetValue(_algorithm, out var mine)) { _pendingVerify = true; Begin(); return; }
            var firstDiff = HashEngine.FirstDifference(mine, p.Hex);
            ShowHash();
            if (firstDiff == 0)
            {
                _banner.Set(DataBannerKind.Success, "It matches", "This file is identical to what the publisher published. Algorithm: " + _algorithm + ", all " + mine.Length + " characters equal.");
                _hashHost.Content = HashBlock(Highlight(mine, null, false, true));
                _compare.Content = null;
                Status("Match.");
            }
            else
            {
                _banner.Set(DataBannerKind.Error, "It does not match", "Do not run or install this file. It is damaged or not the file the publisher published. First difference at character " + firstDiff + ".");
                _hashHost.Content = HashBlock(Highlight(mine, p.Hex, true, false));
                var sp = new StackPanel();
                sp.Children.Add(Label("Expected value", true, new Thickness(0, 4, 0, 6)));
                sp.Children.Add(HashBlock(Highlight(p.Hex, mine, true, false)));
                sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 10, 0, 0), Children = { Action("Show in folder", DataToolIcons.Folder, () => ActionProvider.Reveal(_path), "Btn.Ghost") } });
                _compare.Content = sp;
                Status("Mismatch: first difference at character " + firstDiff + ".", true);
            }
            _verdictShown = true;
            SetCancel("Cancel", false);
            SetPrimary("Close", null, Close);
            PrimaryButton.IsEnabled = true;
        }

        protected override void OnCancelButton()
        {
            if (_running) { CancelRun(); return; }
            Close();
        }

        protected override void OnClosedCore() { if (Current == this) Current = null; }
    }
}
