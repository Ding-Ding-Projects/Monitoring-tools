using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.Core.DocTools;

namespace MonitoringTools.UI.Dialogs
{
    public enum FontState { Ready, AlreadyInstalled, Installed, Unreadable }

    /// <summary>
    /// "Font preview and install" (design/file-tools, board 14). Shows the font at four sizes with editable sample text and
    /// what the file says about itself, and installs it for the current user only (no administrator rights, no system folder).
    /// The preview installs nothing. An already-installed font offers Uninstall; a damaged file says so and changes nothing.
    /// A font collection (.ttc) lists every face, previews the one picked, and installs all of them together.
    /// </summary>
    public sealed class FontInstallDialog : FileToolDialog
    {
        private readonly FontFacts _facts;
        private readonly FontPlan _plan;
        private string _previewDir;
        private FontFamily _family;
        private int _faceIndex;
        private string _previewFile;
        private string _sample = "The five boxing wizards jump quickly";
        private readonly List<TextBlock> _specimens = new List<TextBlock>();

        public FontState State { get; private set; }
        internal FontPlan Plan => _plan;
        internal FontFacts Facts => _facts;
        internal string PreviewDir => _previewDir;
        internal FontFamily PreviewFamily => _family;
        internal int FaceIndex => _faceIndex;
        internal FontFace CurrentFace => _facts.Faces.Count > _faceIndex ? _facts.Faces[_faceIndex] : null;
        /// <summary>True when the preview text blocks really use the font from the file (not a fallback).</summary>
        internal bool PreviewUsesFile => _family != null && _specimens.Count > 0 && _specimens.All(t => ReferenceEquals(t.FontFamily, _family));

        private static readonly List<FontInstallDialog> OpenList = new List<FontInstallDialog>();
        internal static FontInstallDialog Current => OpenList.LastOrDefault();

        public static FontInstallDialog Open(string file)
        {
            if (!File.Exists(file)) { App.Toasts.ShowNotice("Nothing to preview", "The file is no longer there.", App.Toasts.Amber, 4, icon: DocIcons.Font); return null; }
            var d = new FontInstallDialog(file);
            d.Show(); d.Activate();
            return d;
        }

        private FontInstallDialog(string file) : base("Font preview and install", file, DocIcons.Font, 780)
        {
            OpenList.Add(this);
            Closed += (s, e) => { OpenList.Remove(this); CleanPreview(); };
            _facts = FontTools.Read(file);
            if (!_facts.Ok) { ShowUnreadable(); return; }
            _plan = FontTools.Plan(file, _facts);
            PreparePreview(file);
            if (FontTools.IsInstalled(_plan)) ShowAlready(); else ShowReady();
        }

        private void PreparePreview(string file)
        {
            try
            {
                _previewDir = Path.Combine(Path.GetTempPath(), "MonitoringTools-fontpreview", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_previewDir);
                _previewFile = Path.Combine(_previewDir, Path.GetFileName(file));
                File.Copy(file, _previewFile);
                PickFace(0);
            }
            catch (Exception ex) { Log.Warn("Font preview copy failed: " + ex.Message); _family = null; }
        }

        /// <summary>Points the preview at one face of the file (a collection holds several).</summary>
        private void PickFace(int index)
        {
            _faceIndex = Math.Clamp(index, 0, Math.Max(0, _facts.Faces.Count - 1));
            var family = CurrentFace?.Family ?? _facts.Family;
            if (_previewDir == null) return;
            _family = new FontFamily(new Uri(_previewDir + Path.DirectorySeparatorChar, UriKind.Absolute), "./#" + family);
        }

        /// <summary>Shows another face of a collection in the preview (the chips do the same).</summary>
        internal void ShowFace(int index) { PickFace(index); if (State == FontState.Ready) ShowReady(); }

        private void CleanPreview() { try { if (_previewDir != null && Directory.Exists(_previewDir)) Directory.Delete(_previewDir, true); } catch { } }

        private static string Short(string s) { s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim(); return s.Length <= 150 ? s : s[..147] + "..."; }

        private string SummaryLine() => _facts.IsCollection
            ? $"{_facts.Family} · {_facts.Faces.Count} faces · {Ui.Bytes(new FileInfo(FilePath).Length)}"
            : $"{_facts.Family} · {_facts.Face} · {Ui.Bytes(new FileInfo(FilePath).Length)}";

        // ------------------------------------------------------------ states

        private UIElement Specimens()
        {
            _specimens.Clear();
            var host = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var rows = new (double Size, Func<string> Text)[]
            {
                (72, () => "Aa Gg 123"), (36, () => _sample.Length > 28 ? _sample[..28] : _sample), (18, () => _sample + " 0123456789"),
                (12, () => "Sphinx of black quartz, judge my vow. Pack my box with five dozen liquor jugs.")
            };
            foreach (var (size, text) in rows)
            {
                var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); g.ColumnDefinitions.Add(new ColumnDefinition());
                var lbl = new TextBlock { Text = size.ToString("0"), FontSize = 11, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4) };
                AutomationProperties.SetName(lbl, size.ToString("0") + " pixels");
                var t = new TextBlock { Text = text(), FontSize = size, Foreground = Ui.Res("B.Text"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Bottom };
                if (_family != null) t.FontFamily = _family;
                if (CurrentFace != null) { t.FontWeight = CurrentFace.WpfWeight; t.FontStyle = CurrentFace.Style; }
                AutomationProperties.SetName(t, size.ToString("0") + " pixel sample");
                t.Tag = text;
                _specimens.Add(t);
                Grid.SetColumn(t, 1); g.Children.Add(lbl); g.Children.Add(t);
                host.Children.Add(new Border { BorderBrush = Ui.Res("B.Line"), BorderThickness = new Thickness(0, 1, 0, 0), Child = g });
            }
            return host;
        }

        private void ShowReady()
        {
            State = FontState.Ready;
            SetSummary(SummaryLine());
            var sample = Field("Sample text", _sample);
            sample.TextChanged += (s, e) => { _sample = sample.Text.Length == 0 ? " " : sample.Text; foreach (var t in _specimens) if (t.Tag is Func<string> f) t.Text = f(); };
            var cf = CurrentFace;
            var weight = (cf?.Weight ?? _facts.Weight) > 0 ? $" ({cf?.Weight ?? _facts.Weight})" : "";
            var rows = new List<(string, string)>
            {
                ("Family", $"{cf?.Family ?? _facts.Family} · {cf?.Face ?? _facts.Face}{weight}"),
                ("Glyphs", $"{cf?.Glyphs ?? _facts.Glyphs:N0}" + (_facts.Scripts.Count > 0 && _faceIndex == 0 ? " · " + string.Join(", ", _facts.Scripts) : "")),
                ("Licence", string.IsNullOrWhiteSpace(_facts.Licence) ? "The font does not say" : Short(_facts.Licence))
            };
            if (!string.IsNullOrWhiteSpace(_facts.Version)) rows.Add(("Version", _facts.Version));
            var sw = new CheckBox { Style = Ui.Style("Switch"), IsChecked = true, IsEnabled = false, Margin = new Thickness(0, 4, 0, 0) };
            var swText = new StackPanel();
            swText.Children.Add(Text("Install for this user only", 14, null, FontWeights.Medium));
            swText.Children.Add(Text("No administrator rights. Other people on this computer do not get it.", 12, Ui.Res("B.TextMute")));
            sw.Content = swText;
            AutomationProperties.SetName(sw, "Install for this user only");
            var cancel = Ui.Button("Cancel", Close, "Btn.Ghost");
            var install = Ui.Button("Install", Install, "Btn.Accent", Icons.Plus);
            if (_facts.IsCollection) rows.Insert(0, ("Faces", (_facts.Faces.Count == 1 ? "1 face" : $"{_facts.Faces.Count} faces") + ": " + string.Join(", ", _facts.Faces.Select(x => x.Display))));
            var parts = new List<UIElement> { sample };
            if (_facts.IsCollection) parts.Add(FacePicker());
            parts.Add(Specimens()); parts.Add(KeyValues(rows)); parts.Add(sw);
            if (_facts.IsCollection) parts.Add(Hint("Install adds every face in this collection, because Windows installs the file as one."));
            SetBody(parts.ToArray());
            SetFooter("The preview does not install anything.", null, new[] { cancel, install }, install);
        }

        /// <summary>One chip for each face of a collection; the chosen one is filled.</summary>
        private UIElement FacePicker()
        {
            var host = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            host.Children.Add(FieldLabel("Face"));
            var wrap = new WrapPanel();
            foreach (var face in _facts.Faces)
            {
                var i = face.Index;
                var b = Ui.Button(face.Display, () => ShowFace(i), "Btn.Chip");
                b.Height = 28; b.Margin = new Thickness(0, 0, 6, 6);
                if (i == _faceIndex) { b.Background = Ui.Res("B.MintWash"); b.BorderBrush = Ui.Res("B.Mint"); b.Foreground = Ui.Res("B.Text"); }
                else { b.Background = Ui.Res("B.Raised"); b.BorderBrush = Ui.Res("B.Line"); b.Foreground = Ui.Res("B.TextSoft"); }
                AutomationProperties.SetName(b, "Preview the face " + face.Display);
                AutomationProperties.SetItemStatus(b, i == _faceIndex ? "selected" : "");
                wrap.Children.Add(b);
            }
            host.Children.Add(wrap);
            return host;
        }

        private void ShowAlready()
        {
            State = FontState.AlreadyInstalled;
            SetSummary("already installed");
            var un = Ui.Button("Uninstall", Uninstall, "Btn.Danger", Icons.Trash);
            SetBody(Banner(BannerKind.Info, "This font is already installed", "The same file is installed for you. Installing again would change nothing."), WrapOne(un));
            SetFooterClose("Uninstall removes the copy in your user fonts folder.");
        }

        private void ShowInstalled()
        {
            State = FontState.Installed;
            SetSummary("installed");
            var un = Ui.Button("Uninstall", Uninstall, "Btn.Danger", Icons.Trash);
            var settings = Ui.Button("Open fonts settings", () => ActionProvider.Shell("ms-settings:fonts"), "Btn.Chip");
            settings.Margin = new Thickness(0, 0, 0, 8);
            var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(un); row.Children.Add(settings);
            SetBody(Banner(BannerKind.Ok, _facts.IsCollection ? $"All {_facts.Faces.Count} faces are installed for you" : $"{_facts.Family} is installed for you", "Apps that are already open may need a restart to list it."),
                    KeyValues(new[] { ("Location", "Your user fonts folder"), ("Removal", "Open this tool again and choose Uninstall") }), row);
            SetFooterClose("Installed for this user only.");
        }

        private void ShowUnreadable()
        {
            State = FontState.Unreadable;
            SetSummary("could not read");
            var reason = _facts.Error;
            var copy = Ui.Button("Copy details", () => { try { Clipboard.SetText(reason); } catch { } }, "Btn.Ghost", DocIcons.Copy);
            SetBody(Banner(BannerKind.Bad, "This is not a usable font file", reason),
                    Card(new StackPanel { Children = { Text("What was not changed", 12, Ui.Res("B.Mint"), FontWeights.Medium), Text("Nothing was installed and the file was not changed.", 13) } }));
            SetFooterClose("Fonts open here as TTF, OTF or TTC.", copy);
        }

        private static UIElement WrapOne(Button b) { var p = new StackPanel { Orientation = Orientation.Horizontal }; p.Children.Add(b); return p; }

        // ------------------------------------------------------------ actions

        private void Install()
        {
            var err = FontTools.Install(_plan);
            if (err != null)
            {
                var copy = Ui.Button("Copy details", () => { try { Clipboard.SetText(err); } catch { } }, "Btn.Ghost", DocIcons.Copy);
                State = FontState.Unreadable; SetSummary("could not install");
                SetBody(Banner(BannerKind.Bad, "The font could not be installed", err),
                        Card(new StackPanel { Children = { Text("What was not changed", 12, Ui.Res("B.Mint"), FontWeights.Medium), Text("Nothing was installed and the file was not changed.", 13) } }));
                SetFooterClose("Windows refused the install.", copy);
                return;
            }
            ShowInstalled();
        }

        private void Uninstall()
        {
            var err = FontTools.Uninstall(_plan);
            if (err != null) { SetStatus("Could not uninstall: " + err); return; }
            SetSummary("removed");
            SetBody(Banner(BannerKind.Ok, $"{_facts.Family} was removed", "It is gone from your user fonts. Apps that are open may keep showing it until they restart."));
            SetFooterClose("Removed for this user.");
            State = FontState.Ready;
        }

        // ------------------------------------------------------------ for checks and captures

        internal void SetSampleForTest(string text) { var tb = Drive.Find<TextBox>(this, "Sample text"); if (tb != null) tb.Text = text; }
        internal IReadOnlyList<TextBlock> SpecimenBlocks => _specimens;
    }
}
