using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MonitoringTools.Core;
using MonitoringTools.Minecraft;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        private sealed class WorldgenEditorUi
        {
            public StackPanel Panel;
            public Action Refresh;
        }

        private WorldgenSource _wgSource;
        private string _wgSourceVersion;

        /// <summary>
        /// Editor for a version's noise settings and dimension types. It lists the values of the jar's own default file, lets you change single values,
        /// and records only those changes; the generator then writes a data pack holding just the changed files. Nothing here is invented: every value
        /// comes from the version's own default file, and an unreadable jar shows an unavailable state.
        /// </summary>
        private WorldgenEditorUi BuildWorldgenEditor(WorldGenOptions o, Func<WorldGenerator.Support> support, Func<JarFacts> facts)
        {
            var ui = new WorldgenEditorUi { Panel = new StackPanel { Margin = new Thickness(0, 8, 0, 6) } };
            var p = ui.Panel;
            p.Children.Add(Ui.Mono("NOISE SETTINGS AND DIMENSION TYPES (advanced, only the values you change are written)", Ui.Res("B.TextMute"), 11));
            var intro = Ui.Mono("Reads the version's own default files from its server jar and lets you change single values, such as the sea level, the world height or the ambient light. A data pack file replaces a whole entry, so each file written is the jar's default with only your values different, and files you did not touch are not written. Whether the game accepts a combination is decided by the game; this tool checks value types and the documented height limits.", Ui.Res("B.TextMute"), 11);
            intro.TextWrapping = TextWrapping.Wrap; intro.Margin = new Thickness(0, 2, 0, 6);
            p.Children.Add(intro);

            var status = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(0, 4, 0, 4);
            var kind = WorldgenKind.NoiseSettings;
            string selectedId = null;
            string defaultJson = null;
            List<WorldgenLeaf> leaves = new List<WorldgenLeaf>();
            var idHost = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            var rowsHost = new StackPanel();
            var filter = new SearchField("minecraft:worldgen-values", "Search values", compact: true) { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 6) };
            var summary = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            summary.TextWrapping = TextWrapping.Wrap; summary.Margin = new Thickness(0, 4, 0, 4);
            var check = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            check.TextWrapping = TextWrapping.Wrap; check.Margin = new Thickness(0, 2, 0, 4);
            var kindHost = new StackPanel();
            var body = new StackPanel();   // everything below the Read button, shown only once the files are read

            static string Norm(WorldgenLeaf l, string text)
            {
                text = (text ?? "").Trim();
                if (l.Type is "integer" or "number" && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d.ToString("R", CultureInfo.InvariantCulture);
                return text;
            }

            WorldgenEdit FindEdit(string path) => o.WorldgenEdits.FirstOrDefault(e => e.Kind == kind && e.Id == selectedId && e.Path == path);
            Dictionary<string, string> ChangesHere() => o.WorldgenEdits.Where(e => e.Kind == kind && e.Id == selectedId).ToDictionary(e => e.Path, e => e.Value, StringComparer.Ordinal);

            void Summarise()
            {
                int n = o.WorldgenEdits.Count, files = o.WorldgenEdits.Select(e => (e.Kind, e.Id)).Distinct().Count();
                summary.Text = n == 0 ? "No changes: the jar's defaults are used and no data pack is written."
                    : $"{n} changed value{(n == 1 ? "" : "s")} in {files} file{(files == 1 ? "" : "s")}: " + string.Join(", ", o.WorldgenEdits.Select(e => (e.Kind == WorldgenKind.NoiseSettings ? "noise " : "dimension ") + e.Id.Replace("minecraft:", "") + " " + e.Path).Take(6)) + (n > 6 ? ", …" : "") + ". They are written as a data pack when you generate or write to a server.";
                summary.Foreground = Ui.Res(n == 0 ? "B.TextMute" : "B.Mint");
            }

            void Recheck()
            {
                Summarise();
                if (selectedId == null || defaultJson == null) { check.Text = ""; return; }
                var changes = ChangesHere();
                if (changes.Count == 0) { check.Text = ""; return; }
                var bad = WorldgenEditor.Validate(kind, defaultJson, changes, facts()?.Available == true ? facts().Blocks : null);
                if (bad.Count == 0)
                {
                    check.Text = "These changes pass the checks: every value has the type of the jar's own default" + (facts()?.Available == true ? ", block ids exist in the jar" : "") + ", and the documented height limits hold.";
                    check.Foreground = Ui.Res("B.Mint");
                }
                else { check.Text = string.Join("\n", bad.Take(5)) + (bad.Count > 5 ? $"\n…and {bad.Count - 5} more." : ""); check.Foreground = Ui.Res("B.Amber"); }
            }

            FrameworkElement Row(WorldgenLeaf leaf)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new StackPanel();
                var marker = new TextBlock { FontSize = 13, FontFamily = Ui.Font("F.Body"), VerticalAlignment = VerticalAlignment.Center };
                void Mark()
                {
                    var changed = FindEdit(leaf.Path) != null;
                    marker.Text = leaf.Path + (changed ? "  · changed" : "");
                    marker.Foreground = Ui.Res(changed ? "B.Mint" : "B.Text");
                }
                Mark();
                left.Children.Add(marker);
                left.Children.Add(Ui.Mono("default " + leaf.Default + " · " + leaf.Type, Ui.Res("B.TextMute"), 11));
                row.Children.Add(left);

                void Commit(string text)
                {
                    var existing = FindEdit(leaf.Path);
                    if (string.IsNullOrWhiteSpace(text) || Norm(leaf, text) == Norm(leaf, leaf.Default))
                    {
                        if (existing != null) o.WorldgenEdits.Remove(existing);
                    }
                    else
                    {
                        var (_, err) = WorldgenEditor.ParseValue(leaf, text);
                        if (err != null) { check.Text = err; check.Foreground = Ui.Res("B.Amber"); return; }
                        if (existing != null) existing.Value = text.Trim();
                        else o.WorldgenEdits.Add(new WorldgenEdit { Kind = kind, Id = selectedId, Path = leaf.Path, Value = text.Trim() });
                    }
                    Mark(); Recheck();
                }

                FrameworkElement ctl;
                if (leaf.Type == "boolean")
                {
                    var chips = new WrapPanel();
                    var buttons = new List<(Button B, string V)>();
                    void Paint()
                    {
                        var cur = FindEdit(leaf.Path)?.Value;
                        foreach (var (b, v) in buttons)
                        {
                            bool on = v == null ? cur == null : cur == v;
                            b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                        }
                    }
                    foreach (var (label, val) in new[] { ("Default (" + leaf.Default + ")", (string)null), ("true", "true"), ("false", "false") })
                    {
                        var v = val;
                        var b = Ui.Button(label, () => { Commit(v ?? ""); Paint(); }, "Btn.Chip");
                        b.Height = 26; b.Margin = new Thickness(0, 0, 4, 0);
                        b.ToolTip = v == null ? "Keep the jar's default for " + leaf.Path + "." : "Set " + leaf.Path + " to " + v + ".";
                        AutomationProperties.SetName(b, leaf.Path + " " + label);
                        buttons.Add((b, v)); chips.Children.Add(b);
                    }
                    Paint();
                    ctl = chips;
                }
                else
                {
                    var box = Input(FindEdit(leaf.Path)?.Value ?? "", 170, "Empty keeps the jar's default (" + leaf.Default + "). Type " + (leaf.Type == "integer" ? "a whole number" : leaf.Type == "number" ? "a number" : "text") + ".");
                    AutomationProperties.SetName(box, leaf.Path + " value");
                    box.LostFocus += (a, b) => Commit(box.Text);
                    ctl = box;
                }
                Grid.SetColumn(ctl, 1); row.Children.Add(ctl);
                SearchScope.SetItem(row, "worldgen " + WorldgenEditor.KindLabel(kind) + " " + selectedId + " " + leaf.Path);
                return row;
            }

            void PaintRows()
            {
                rowsHost.Children.Clear();
                if (selectedId == null) { rowsHost.Children.Add(Ui.Text("Choose a file above to list its values.")); filter.SetCount(0, 0, "values"); return; }
                filter.SampleSource = () => leaves.Select(l => l.Path);
                var hits = leaves.Where(l => filter.State.Matches(l.Path + " " + l.Default)).ToList();
                if (leaves.Count == 0) rowsHost.Children.Add(Ui.Text("This file has no single values this editor can change."));
                foreach (var l in hits.Take(250)) rowsHost.Children.Add(Row(l));
                if (hits.Count > 250) rowsHost.Children.Add(Ui.Mono($"Showing the first 250 of {hits.Count}. Narrow the search to see the rest.", Ui.Res("B.TextMute"), 11));
                filter.SetCount(hits.Count, leaves.Count, "values");
            }
            filter.Filter += PaintRows;

            void SelectFile(string id)
            {
                var text = _wgSource?.Read(kind, id);
                if (text == null) { status.Text = "This version has no default file for " + id + "."; status.Foreground = Ui.Res("B.Amber"); return; }
                try { leaves = WorldgenEditor.Leaves(text); }
                catch (Exception ex) { status.Text = id + " could not be read as JSON: " + ex.Message; status.Foreground = Ui.Res("B.Amber"); return; }
                selectedId = id; defaultJson = text;
                status.Text = $"{id}: {leaves.Count} values from {_wgSource.Origin}. " + (kind == WorldgenKind.NoiseSettings ? "Surface rules, the noise router and spawn targets are kept exactly as the jar defines them and are not listed." : "");
                status.Foreground = Ui.Res("B.TextMute");
                PaintIds(); PaintRows(); Recheck();
            }

            void PaintIds()
            {
                idHost.Children.Clear();
                var ids = _wgSource?.Ids(kind) ?? new List<string>();
                if (ids.Count == 0) idHost.Children.Add(Ui.Mono("This version has no " + WorldgenEditor.KindLabel(kind) + " files to edit.", Ui.Res("B.Amber"), 11));
                foreach (var id in ids)
                {
                    var name = id;
                    var changedCount = o.WorldgenEdits.Count(e => e.Kind == kind && e.Id == name);
                    var b = Ui.Button(id.Replace("minecraft:", "") + (changedCount > 0 ? $" ({changedCount})" : ""), () => SelectFile(name), "Btn.Chip");
                    b.Height = 28; b.Margin = new Thickness(0, 0, 4, 4);
                    b.ToolTip = "List the values of " + id + (changedCount > 0 ? ". " + changedCount + " changed." : ".");
                    bool on = name == selectedId;
                    b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                    idHost.Children.Add(b);
                }
            }

            void PaintKind()
            {
                kindHost.Children.Clear();
                kindHost.Children.Add(Choice("File type (default noise settings)", new[] { ("Noise settings", WorldgenKind.NoiseSettings), ("Dimension type", WorldgenKind.DimensionType) }, kind, v =>
                {
                    kind = v; selectedId = null; defaultJson = null; leaves = new List<WorldgenLeaf>();
                    status.Text = ""; PaintIds(); PaintRows(); Recheck();
                }, "Which kind of world-generation file the editor below changes. Default: noise settings."));
            }

            var read = Ui.Button("Read this version's worldgen files", null, "Btn.Ghost", Icons.Folder);
            read.ToolTip = "Downloads this version's server jar once (checked against Mojang's SHA-1) and reads its default noise settings and dimension type files. When the jar holds none, its data generator is run once, without starting a server, and its output is cached.";
            read.Click += async (a, b) =>
            {
                var ver = o.Version;
                if (string.IsNullOrWhiteSpace(ver)) { status.Text = "Choose a version first."; status.Foreground = Ui.Res("B.Amber"); return; }
                read.IsEnabled = false; status.Foreground = Ui.Res("B.TextMute"); status.Text = "Reading…";
                try
                {
                    var src = await WorldgenEditor.OpenAsync(ver, null, t => Dispatcher.BeginInvoke(new Action(() => status.Text = t)));
                    if (ver == o.Version) { _wgSource = src; _wgSourceVersion = ver; }
                    var nn = src.Ids(WorldgenKind.NoiseSettings).Count; var dd = src.Ids(WorldgenKind.DimensionType).Count;
                    status.Text = $"{ver}: {nn} noise settings and {dd} dimension types, read from {src.Origin}.";
                    status.Foreground = Ui.Res(nn + dd > 0 ? "B.Mint" : "B.Amber");
                    selectedId = null; defaultJson = null; leaves = new List<WorldgenLeaf>();
                }
                catch (Exception ex) { status.Text = "Unavailable: " + ex.Message; status.Foreground = Ui.Res("B.Amber"); }
                read.IsEnabled = true;
                Refresh2();
            };

            var resetFile = Ui.Button("Reset this file", () =>
            {
                o.WorldgenEdits.RemoveAll(e => e.Kind == kind && e.Id == selectedId);
                PaintIds(); PaintRows(); Recheck();
            }, "Btn.Ghost", Icons.Restart);
            resetFile.ToolTip = "Forgets the changes to the file shown. Nothing is written until you generate.";
            var resetAll = Ui.Button("Reset all worldgen changes", () => { o.WorldgenEdits.Clear(); PaintIds(); PaintRows(); Recheck(); }, "Btn.Ghost", Icons.Close);
            resetAll.ToolTip = "Forgets every noise settings and dimension type change, so the jar's defaults are used.";
            var resetRow = new WrapPanel(); resetRow.Children.Add(resetFile); resetRow.Children.Add(resetAll);

            p.Children.Add(read);
            p.Children.Add(status);
            body.Children.Add(kindHost);
            body.Children.Add(idHost);
            body.Children.Add(filter);
            body.Children.Add(new ScrollViewer { Content = rowsHost, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            body.Children.Add(check);
            body.Children.Add(summary);
            body.Children.Add(resetRow);
            p.Children.Add(body);

            // Shows the unavailable states when the version cannot use this editor, and the editor once files are read.
            void Refresh2()
            {
                var sup = support();
                if (_wgSourceVersion != o.Version && _wgSource != null) { _wgSource = null; _wgSourceVersion = null; selectedId = null; defaultJson = null; leaves = new List<WorldgenLeaf>(); status.Text = ""; }
                bool dataDriven = sup?.DataDriven == true;
                read.IsEnabled = dataDriven && !string.IsNullOrWhiteSpace(o.Version);
                if (string.IsNullOrWhiteSpace(o.Version)) { status.Text = "Unavailable: choose a version first."; status.Foreground = Ui.Res("B.Amber"); }
                else if (sup != null && !dataDriven) { status.Text = "Unavailable: noise settings and dimension types are only data pack files from Java 1.19 on. Older versions keep them inside the game."; status.Foreground = Ui.Res("B.Amber"); }
                else if (_wgSource == null && status.Text.Length == 0) { status.Text = "Press “Read this version's worldgen files” to list them."; status.Foreground = Ui.Res("B.TextMute"); }
                body.Visibility = _wgSource != null && dataDriven ? Visibility.Visible : Visibility.Collapsed;
                if (_wgSource != null) { PaintKind(); PaintIds(); PaintRows(); Recheck(); }
            }
            ui.Refresh = Refresh2;
            Refresh2();
            return ui;
        }
    }
}
