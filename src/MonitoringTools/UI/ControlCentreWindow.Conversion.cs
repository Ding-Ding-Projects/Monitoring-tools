using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MonitoringTools.Core;
using MonitoringTools.Media;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        private string _formatKind = "All";

        /// <summary>
        /// Every conversion format as one searchable list: a kind badge, the :command, the name and extension,
        /// when to pick it, what it can be made from, and a Convert files button that opens a picker for compatible files.
        /// Design: design/motion-and-pages/Conversion.dc.html.
        /// </summary>
        private FrameworkElement FormatListCard()
        {
            var listed = Formats.Listed.ToList();
            var sp = new StackPanel();
            sp.Children.Add(CardTitle("Formats", $"{listed.Count} formats. Copy a path with :format on the end, or pick one here and choose files."));

            var search = new SearchField("conversion:formats", "Search formats, for example webm or lossless") { Margin = new Thickness(0, 0, 0, 10) };
            search.SampleSource = () => listed.Select(f => f.Id);
            sp.Children.Add(search);

            var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            sp.Children.Add(chips);
            var rows = new StackPanel();
            sp.Children.Add(rows);
            sp.Children.Add(Ui.Text("An existing file is never overwritten: the new one gets a (1), (2) suffix.", "T.Body"));

            var kinds = new[] { ("All", (TargetKind?)null), ("Video", TargetKind.Video), ("Audio", TargetKind.Audio), ("Image", TargetKind.Image) };

            void PaintChips()
            {
                chips.Children.Clear();
                foreach (var (label, kind) in kinds)
                {
                    int n = kind == null ? listed.Count : listed.Count(f => f.Kind == kind);
                    var b = Ui.Button($"{label} {n}", null, "Btn.Chip");
                    b.Height = 32; b.Margin = new Thickness(0, 0, 6, 6);
                    bool on = _formatKind == label;
                    b.Background = Ui.Res(on ? "B.SecondaryContainer" : "B.Raised");
                    b.Foreground = Ui.Res(on ? "B.OnSecondaryContainer" : "B.TextSoft");
                    AutomationProperties.SetName(b, $"Show {label.ToLowerInvariant()} formats");
                    var pick = label;
                    b.Click += (o, e) => { _formatKind = pick; PaintChips(); PaintRows(true); };
                    chips.Children.Add(b);
                }
            }

            void PaintRows(bool animate)
            {
                rows.Children.Clear();
                var hits = listed.Where(f => (_formatKind == "All" || f.Kind.ToString() == _formatKind)
                    && search.State.Matches($"{f.Id} {f.Label} {f.Extension} {f.Note} {f.Kind}")).ToList();
                search.SetCount(hits.Count, listed.Count, "formats");
                foreach (var f in hits) rows.Children.Add(FormatRow(f));
                if (hits.Count == 0)
                    rows.Children.Add(new TextBlock { Text = "No format matches. Try a file extension, a codec or a word like lossless.", Style = Ui.Style("T.Body"), Foreground = Ui.Res("B.TextMute"), Margin = new Thickness(4, 8, 0, 12) });
                if (animate) Ui.RiseAll(rows.Children, 22);
            }

            search.Filter += () => PaintRows(false);
            PaintChips();
            PaintRows(false);
            return Card(sp);
        }

        private FrameworkElement FormatRow(FormatSpec f)
        {
            var (wash, ink, badge) = f.Kind switch
            {
                TargetKind.Video => ("B.TertiaryContainer", "B.OnTertiaryContainer", "VID"),
                TargetKind.Audio => ("B.VioletWash", "B.Violet", "AUD"),
                _ => ("B.AmberWash", "B.Amber", "IMG"),
            };
            var g = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chip = new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res(wash), Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock { Text = badge, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Ui.Res(ink), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontFamily = Ui.Font("F.Body") }
            };
            g.Children.Add(chip);

            var cmd = Ui.Pill(":" + f.Id, Ui.Res("B.MintWash"), Ui.Res("B.Mint"), mono: true, size: 12.5);
            cmd.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(cmd, 1); g.Children.Add(cmd);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
            var title = new TextBlock { FontSize = 14.5, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis };
            title.Inlines.Add(new System.Windows.Documents.Run(f.Label));
            title.Inlines.Add(new System.Windows.Documents.Run("  " + f.Extension) { FontFamily = Ui.Font("F.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextMute") });
            text.Children.Add(title);
            text.Children.Add(new TextBlock { Text = f.Note + " · from " + f.From, FontSize = 12, Foreground = Ui.Res("B.TextSoft"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(text, 2); g.Children.Add(text);

            var go = Ui.Button("Convert files…", () => PickAndConvert(f), "Btn.Ghost", Icons.Bolt);
            go.Height = 36; go.Margin = new Thickness(0);
            go.ToolTip = $"Choose files to convert to {f.Label}. Each one is saved next to its original.";
            AutomationProperties.SetName(go, "Convert files to " + f.Label);
            Grid.SetColumn(go, 3); g.Children.Add(go);

            // A row with a soft tonal hover, like the design's list rows.
            var tint = new SolidColorBrush(Colors.Transparent);
            var row = new Border { Background = tint, CornerRadius = new CornerRadius(18), Padding = new Thickness(10, 8, 10, 8), Child = g };
            row.MouseEnter += (o, e) => Ui.ColorTo(tint, ((SolidColorBrush)Ui.Res("B.Raised")).Color, Ui.Reduced ? 1 : 180);
            row.MouseLeave += (o, e) => Ui.ColorTo(tint, Colors.Transparent, Ui.Reduced ? 1 : 240);
            SearchScope.SetItem(row, $"format {f.Id} {f.Label} {f.Extension} {f.Note}");
            return row;
        }

        /// <summary>Opens a picker filtered to files this format can be made from, then converts one file or a batch.</summary>
        private void PickAndConvert(FormatSpec f)
        {
            var exts = new List<string>();
            if (f.Kind == TargetKind.Video) { exts.AddRange(MonitoringTools.Clip.ClipItem.VideoExt); if (f.Id == "mp4") exts.AddRange(MonitoringTools.Clip.ClipItem.AudioExt); }
            else if (f.Kind == TargetKind.Audio) { exts.AddRange(MonitoringTools.Clip.ClipItem.AudioExt); exts.AddRange(MonitoringTools.Clip.ClipItem.VideoExt); }
            else { exts.AddRange(MonitoringTools.Clip.ClipItem.ImageExt); if (f.Id != "ico") exts.AddRange(MonitoringTools.Clip.ClipItem.VideoExt); }
            var pattern = string.Join(";", exts.Distinct(StringComparer.OrdinalIgnoreCase).Select(e => "*" + e));
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Choose files to convert to {f.Label}",
                Filter = $"Files that convert to {f.Label}|{pattern}|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) != true || dlg.FileNames.Length == 0) return;
            var files = dlg.FileNames.Where(File.Exists).ToList();
            if (files.Count == 1) Converter.Start(files[0], f.Id);
            else Converter.StartBatch(files, f.Id, Path.GetDirectoryName(files[0]));
        }
    }
}
