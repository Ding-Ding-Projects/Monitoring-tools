using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
        /// <summary>The parts of the Ollama page that go beyond installing, pulling and chatting: catalogue, cart, details and the troubleshooter.</summary>
        private sealed class OllamaSuite
        {
            public FrameworkElement Catalogue, Cart, Details, Troubleshooter;
            public Action<string> ShowDetails;
        }

        private static string FitBrush(FitLevel l) => l switch { FitLevel.Good => "B.Mint", FitLevel.Ok => "B.TextSoft", FitLevel.Tight => "B.Amber", _ => "B.Danger" };

        private OllamaSuite BuildOllamaSuite(Func<Task> refreshModels)
        {
            var suite = new OllamaSuite();
            HardwareFacts hw = null;
            var liveCache = new Dictionary<string, OllamaCatalogueEntry>(StringComparer.OrdinalIgnoreCase);
            var cart = Settings.Current.OllamaCart ??= new List<string>();

            OllamaCatalogueEntry Lookup(string model) =>
                OllamaCatalogue.Snapshot.FirstOrDefault(e => e.Model.Equals(model, StringComparison.OrdinalIgnoreCase))
                ?? (liveCache.TryGetValue(model, out var l) ? l : null);

            Action paintCart = null;
            Action paintCatalogue = null;

            // ================================================================ catalogue
            var catBox = new StackPanel();
            catBox.Children.Add(CardTitle("Model catalogue",
                $"A built-in list of well-known Ollama models, written on {OllamaCatalogue.SnapshotDate}. Sizes are typical downloads for the default 4-bit version, not measurements, and newer models exist. Use the online search for the current library."));
            var hwLine = Ui.Mono("Reading this computer's memory, graphics and disk…", Ui.Res("B.TextMute"), 11);
            hwLine.TextWrapping = TextWrapping.Wrap; hwLine.Margin = new Thickness(0, 0, 0, 8);
            catBox.Children.Add(hwLine);
            var catFilter = new SearchField("ollama:catalogue", "Search the catalogue") { Margin = new Thickness(0, 0, 0, 8) };
            catBox.Children.Add(catFilter);
            var modeLine = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            modeLine.TextWrapping = TextWrapping.Wrap; modeLine.Margin = new Thickness(0, 0, 0, 6);
            var catRows = new StackPanel();
            List<OllamaCatalogueEntry> shownSource = OllamaCatalogue.Snapshot.ToList();
            bool liveMode = false;

            var liveBtn = Ui.Button("Search the online library", null, "Btn.Ghost", Icons.Globe);
            liveBtn.ToolTip = "Sends your search words to ollama.com/search and reads the result page. This is the only action here that contacts the Ollama website. Default: not used unless you press it.";
            var snapBtn = Ui.Button("Show the built-in list", () => { liveMode = false; shownSource = OllamaCatalogue.Snapshot.ToList(); paintCatalogue(); }, "Btn.Ghost", Icons.Lines);
            snapBtn.ToolTip = "Goes back to the dated built-in list.";
            var hwBtn = Ui.Button("Re-read hardware", null, "Btn.Ghost", Icons.Restart);
            hwBtn.ToolTip = "Reads memory, graphics adapters and free disk again, for example after closing programs.";
            var catActions = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            catActions.Children.Add(liveBtn); catActions.Children.Add(snapBtn); catActions.Children.Add(hwBtn);
            catBox.Children.Add(catActions);
            catBox.Children.Add(modeLine);
            catBox.Children.Add(catRows);

            async Task ReadHw()
            {
                hwLine.Text = "Reading this computer's memory, graphics and disk…"; hwLine.Foreground = Ui.Res("B.TextMute");
                hw = await Task.Run(OllamaCatalogue.ReadHardware);
                var gpu = hw.Gpus.Count == 0 ? (hw.GpuError ?? "no graphics adapter reported")
                    : string.Join("; ", hw.Gpus.Select(g => g.VramBytes > 0 ? $"{g.Name} ({g.VramBytes / 1073741824.0:0.0} GB{(g.VramFromAdapterRam ? ", Windows may under-report above 4 GB" : "")})" : $"{g.Name} (memory not reported)"));
                hwLine.Text = $"RAM {hw.FreeRam / 1073741824.0:0.0} GB free of {hw.TotalRam / 1073741824.0:0.0} GB · GPU: {gpu} · Disk {hw.FreeDisk / 1073741824.0:0.0} GB free on {hw.DiskRoot ?? "?"} (models folder {hw.ModelsFolder})";
                hwLine.Foreground = Ui.Res("B.TextSoft");
                paintCatalogue(); paintCart();
            }
            hwBtn.Click += async (o, e) => await ReadHw();

            paintCatalogue = () =>
            {
                catRows.Children.Clear();
                catFilter.SampleSource = () => shownSource.Select(x => x.Model);
                var hits = shownSource.Where(m => catFilter.State.Matches($"{m.Model} {m.Family} {m.Parameters} {m.Note}")).ToList();
                modeLine.Text = liveMode
                    ? "Showing results read from ollama.com. Sizes are estimates from the parameter count (about 0.6 GB per billion, 4-bit); the real download is shown by the server when you pull."
                    : $"Showing the built-in snapshot of {OllamaCatalogue.Snapshot.Count} models from {OllamaCatalogue.SnapshotDate}.";
                foreach (var m in hits.Take(60))
                {
                    var g = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var info = new StackPanel();
                    info.Children.Add(new TextBlock { Text = m.Model, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
                    var meta = string.Join(" · ", new[]
                    {
                        m.Parameters, m.SizeGb > 0 ? (m.SizeEstimated ? "about " : "") + m.SizeGb.ToString("0.0") + " GB" : "size unknown",
                        m.ContextK > 0 ? m.ContextK + "K context" : null, m.Family
                    }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    info.Children.Add(Ui.Mono(meta, Ui.Res("B.TextMute"), 11));
                    if (!string.IsNullOrWhiteSpace(m.Note)) { var n = Ui.Text(m.Note); info.Children.Add(n); }
                    if (hw != null)
                    {
                        var v = OllamaCatalogue.Fit(m.SizeBytes, hw);
                        var vt = Ui.Mono(v.Summary + ". " + v.Reason, Ui.Res(FitBrush(v.Level)), 11);
                        vt.TextWrapping = TextWrapping.Wrap; vt.Margin = new Thickness(0, 2, 12, 0);
                        info.Children.Add(vt);
                    }
                    g.Children.Add(info);
                    var name = m.Model;
                    bool inCart = cart.Contains(name, StringComparer.OrdinalIgnoreCase);
                    var add = Ui.Button(inCart ? "In cart" : "Add to cart", () =>
                    {
                        if (!cart.Contains(name, StringComparer.OrdinalIgnoreCase)) { cart.Add(name); if (m.Live) liveCache[name] = m; Settings.Save(); }
                        paintCart(); paintCatalogue();
                    }, inCart ? "Btn.Chip" : "Btn.Ghost", inCart ? Icons.Check : Icons.Plus);
                    add.IsEnabled = !inCart;
                    add.ToolTip = "Queues " + name + " in the pull cart below. Nothing is downloaded until you press Pull all.";
                    AutomationProperties.SetName(add, "Add " + name + " to the pull cart");
                    Grid.SetColumn(add, 1); g.Children.Add(add);
                    SearchScope.SetItem(g, "model catalogue " + m.Model + " " + m.Family);
                    catRows.Children.Add(g);
                }
                if (hits.Count > 60) catRows.Children.Add(Ui.Mono($"Showing the first 60 of {hits.Count}. Narrow the search to see the rest.", Ui.Res("B.TextMute"), 11));
                if (hits.Count == 0) catRows.Children.Add(Ui.Text(liveMode ? "No online results match the filter." : "No catalogue entry matches. Try the online search for models newer than this list."));
                catFilter.SetCount(Math.Min(hits.Count, 60), shownSource.Count, "models");
            };
            catFilter.Filter += () => paintCatalogue();

            CancellationTokenSource liveCts = null;
            liveBtn.Click += async (o, e) =>
            {
                var q = catFilter.Box.Text.Trim();
                if (q.Length == 0) { modeLine.Text = "Type a word to look for first, for example qwen or coder."; modeLine.Foreground = Ui.Res("B.Amber"); return; }
                liveBtn.IsEnabled = false; modeLine.Foreground = Ui.Res("B.TextMute"); modeLine.Text = "Asking ollama.com for “" + q + "”…";
                liveCts?.Cancel(); liveCts = new CancellationTokenSource();
                try
                {
                    var res = await OllamaCatalogue.LiveSearchAsync(q, liveCts.Token);
                    foreach (var r in res) liveCache[r.Model] = r;
                    liveMode = true; shownSource = res;
                    catFilter.Box.Text = "";
                    paintCatalogue();
                }
                catch (Exception ex)
                {
                    modeLine.Text = "The online search is not available: " + ex.Message + " The built-in list still works.";
                    modeLine.Foreground = Ui.Res("B.Amber");
                }
                liveBtn.IsEnabled = true;
            };
            suite.Catalogue = Card(catBox);

            // ================================================================ pull cart
            var cartBox = new StackPanel();
            cartBox.Children.Add(CardTitle("Pull cart", "Queue several models, see the combined download size against your free disk, then pull them one after another through the local server."));
            var cartRows = new StackPanel();
            var cartTotal = Ui.Mono("", Ui.Res("B.TextSoft"), 12);
            cartTotal.TextWrapping = TextWrapping.Wrap; cartTotal.Margin = new Thickness(0, 4, 0, 6);
            var cartBar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 6, 0, 6), Foreground = Ui.Res("B.Mint"), Background = Ui.Res("B.Sunken"), Visibility = Visibility.Collapsed };
            var cartStatus = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            cartStatus.TextWrapping = TextWrapping.Wrap;
            CancellationTokenSource cartCts = null;
            var pullAll = Ui.Button("Pull all", null, "Btn.Accent", Icons.Play);
            var stopCart = Ui.Button("Cancel", () => cartCts?.Cancel(), "Btn.Ghost", Icons.Close);
            stopCart.IsEnabled = false;
            var clearCart = Ui.Button("Empty the cart", () => { if (cartCts != null) return; cart.Clear(); Settings.Save(); paintCart(); paintCatalogue(); }, "Btn.Ghost", Icons.Trash);
            clearCart.ToolTip = "Removes every queued name. Nothing on disk changes.";
            pullAll.ToolTip = "Downloads each queued model in turn through the local server. A finished model leaves the cart; a failed one stays.";
            stopCart.ToolTip = "Stops after the current download is interrupted. Layers already fetched are kept.";
            var cartActions = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            cartActions.Children.Add(pullAll); cartActions.Children.Add(stopCart); cartActions.Children.Add(clearCart);
            cartBox.Children.Add(cartRows); cartBox.Children.Add(cartTotal); cartBox.Children.Add(cartActions); cartBox.Children.Add(cartBar); cartBox.Children.Add(cartStatus);

            paintCart = () =>
            {
                cartRows.Children.Clear();
                if (cart.Count == 0) cartRows.Children.Add(Ui.Text("The cart is empty. Use Add to cart in the catalogue above."));
                double gb = 0; int unknown = 0;
                foreach (var name in cart.ToList())
                {
                    var entry = Lookup(name);
                    if (entry == null || entry.SizeGb <= 0) unknown++; else gb += entry.SizeGb;
                    var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var info = new StackPanel();
                    info.Children.Add(new TextBlock { Text = name, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
                    info.Children.Add(Ui.Mono(entry != null && entry.SizeGb > 0 ? (entry.SizeEstimated ? "about " : "") + entry.SizeGb.ToString("0.0") + " GB" : "size unknown (not in the catalogue)", Ui.Res("B.TextMute"), 11));
                    row.Children.Add(info);
                    var rm = Ui.Button("Remove", () => { if (cartCts != null) return; cart.RemoveAll(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)); Settings.Save(); paintCart(); paintCatalogue(); }, "Btn.Ghost", Icons.Close);
                    rm.ToolTip = "Takes " + name + " out of the cart.";
                    AutomationProperties.SetName(rm, "Remove " + name + " from the cart");
                    Grid.SetColumn(rm, 1); row.Children.Add(rm);
                    SearchScope.SetItem(row, "pull cart " + name);
                    cartRows.Children.Add(row);
                }
                if (cart.Count == 0) { cartTotal.Text = ""; pullAll.IsEnabled = false; return; }
                pullAll.IsEnabled = cartCts == null;
                var text = $"{cart.Count} model(s), combined download about {gb:0.0} GB" + (unknown > 0 ? $" plus {unknown} of unknown size" : "") + ".";
                if (hw != null && hw.FreeDisk > 0)
                {
                    bool fits = gb * 1073741824.0 <= hw.FreeDisk;
                    text += fits ? $" Free disk: {hw.FreeDisk / 1073741824.0:0.0} GB, enough." : $" Free disk: {hw.FreeDisk / 1073741824.0:0.0} GB, which is not enough for the whole cart.";
                    cartTotal.Foreground = Ui.Res(fits ? "B.TextSoft" : "B.Danger");
                }
                else cartTotal.Foreground = Ui.Res("B.TextSoft");
                cartTotal.Text = text;
            };

            pullAll.Click += async (o, e) =>
            {
                if (cart.Count == 0) return;
                if (await Ollama.VersionAsync() == null) { cartStatus.Text = "The Ollama server is not running. Start it at the top of this page first."; cartStatus.Foreground = Ui.Res("B.Amber"); return; }
                cartCts = new CancellationTokenSource();
                pullAll.IsEnabled = false; stopCart.IsEnabled = true; cartBar.Visibility = Visibility.Visible;
                int done = 0, failed = 0, total = cart.Count;
                foreach (var name in cart.ToList())
                {
                    if (cartCts.IsCancellationRequested) break;
                    cartBar.IsIndeterminate = true;
                    try
                    {
                        await Ollama.PullAsync(name, (s, f) =>
                        {
                            cartStatus.Foreground = Ui.Res("B.TextMute");
                            cartStatus.Text = $"[{done + failed + 1}/{total}] {name}: " + (f >= 0 ? $"{s} · {f * 100:0}%" : s);
                            if (f >= 0) { cartBar.IsIndeterminate = false; cartBar.Value = f; } else cartBar.IsIndeterminate = true;
                        }, cartCts.Token);
                        done++;
                        cart.RemoveAll(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)); Settings.Save();
                        paintCart();
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { failed++; cartStatus.Text = $"{name} failed: {ex.Message}"; cartStatus.Foreground = Ui.Res("B.Amber"); await Task.Delay(1200); }
                }
                bool cancelled = cartCts.IsCancellationRequested;
                cartCts = null;
                cartBar.Visibility = Visibility.Collapsed; stopCart.IsEnabled = false;
                cartStatus.Text = (cancelled ? "Cancelled. " : "") + $"{done} pulled, {failed} failed, {cart.Count} still in the cart.";
                cartStatus.Foreground = Ui.Res(failed > 0 ? "B.Amber" : "B.Mint");
                if (done > 0 && !cancelled) Sounds.Play(Cue.Complete);
                paintCart(); paintCatalogue();
                await refreshModels();
            };
            suite.Cart = Card(cartBox);

            // ================================================================ model details
            var detBox = new StackPanel();
            detBox.Children.Add(CardTitle("Model details", "What the server reports for an installed model: parameters, template, context length, capabilities and licence. Press Details on an installed model."));
            var detHost = new StackPanel();
            detHost.Children.Add(Ui.Text("No model selected yet."));
            detBox.Children.Add(detHost);
            suite.Details = Card(detBox);
            suite.ShowDetails = async model =>
            {
                detHost.Children.Clear();
                detHost.Children.Add(Ui.Mono("Reading details of " + model + "…", Ui.Res("B.TextMute"), 11));
                try
                {
                    var d = await Ollama.ShowAsync(model);
                    detHost.Children.Clear();
                    detHost.Children.Add(new TextBlock { Text = model, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
                    void Line(string k, string v) { if (!string.IsNullOrWhiteSpace(v)) detHost.Children.Add(Ui.Mono($"{k}: {v}", Ui.Res("B.TextSoft"), 12)); }
                    Line("Family", d.Family); Line("Parameters", d.ParameterSize); Line("Quantisation", d.Quantization); Line("Format", d.Format);
                    Line("Context length", d.ContextLength > 0 ? d.ContextLength.ToString("N0") + " tokens" : "not reported");
                    Line("Capabilities", d.Capabilities.Count > 0 ? string.Join(", ", d.Capabilities) : "not reported by this server version");
                    Line("Licence", d.LicenceKind);
                    var lic = Ui.Text(d.LicenceSummary + "  (An automatic summary of the licence text. It is not legal advice; read the full text before relying on it.)");
                    detHost.Children.Add(lic);
                    void Block(string title, string body)
                    {
                        if (string.IsNullOrWhiteSpace(body)) return;
                        var exp = new Expander { Header = title, Margin = new Thickness(0, 6, 0, 0), Foreground = Ui.Res("B.TextSoft") };
                        exp.Content = new TextBlock { Text = body.Trim(), Style = Ui.Style("T.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
                        detHost.Children.Add(exp);
                    }
                    Block("Default parameters", d.Parameters);
                    Block("Prompt template", d.Template);
                }
                catch (Exception ex)
                {
                    detHost.Children.Clear();
                    detHost.Children.Add(Ui.Mono("Could not read details: " + ex.Message, Ui.Res("B.Amber"), 11));
                }
            };

            // ================================================================ troubleshooter
            var trBox = new StackPanel();
            trBox.Children.Add(CardTitle("Troubleshooter", "Works offline from local facts: whether Ollama is installed and answering, whether the port is taken and by what, the models folder and the log. Nothing is sent anywhere."));
            var trRows = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            var logOut = new TextBlock { Style = Ui.Style("T.Mono"), FontSize = 11, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap };
            var logScroll = new ScrollViewer { Content = logOut, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
            var trNote = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            trNote.TextWrapping = TextWrapping.Wrap;

            async Task RunChecks()
            {
                trRows.Children.Clear();
                trRows.Children.Add(Ui.Mono("Checking…", Ui.Res("B.TextMute"), 11));
                var checks = await OllamaDiagnostics.RunAsync();
                trRows.Children.Clear();
                foreach (var c in checks)
                {
                    var glyph = c.State switch { CheckState.Ok => "✓", CheckState.Warn => "!", CheckState.Fail => "✗", _ => "i" };
                    var brush = c.State switch { CheckState.Ok => "B.Mint", CheckState.Warn => "B.Amber", CheckState.Fail => "B.Danger", _ => "B.TextMute" };
                    var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                    row.Children.Add(new TextBlock { Text = glyph + "  " + c.Title, Foreground = Ui.Res(brush), FontSize = 12, FontFamily = Ui.Font("F.Body") });
                    var d = Ui.Text(c.Detail); d.Margin = new Thickness(18, 0, 0, 0); row.Children.Add(d);
                    SearchScope.SetItem(row, "troubleshooter " + c.Title);
                    trRows.Children.Add(row);
                }
            }

            var runBtn = Ui.Button("Run checks", async () => await RunChecks(), "Btn.Accent", Icons.Pulse);
            runBtn.ToolTip = "Runs every local check again.";
            var logsBtn = Ui.Button("Open logs folder", () => OpenFolder(OllamaDiagnostics.LogsFolder(), trNote), "Btn.Ghost", Icons.Folder);
            logsBtn.ToolTip = "Opens the folder that holds Ollama's server.log and app.log.";
            var modelsBtn = Ui.Button("Open models folder", () => OpenFolder(OllamaCatalogue.ModelsFolder(), trNote), "Btn.Ghost", Icons.Folder);
            modelsBtn.ToolTip = "Opens the folder where downloaded models are stored.";
            var logBtn = Ui.Button("Show log", () =>
            {
                var t = OllamaDiagnostics.TailServerLog();
                logScroll.Visibility = Visibility.Visible;
                logOut.Text = t ?? "There is no server.log at " + OllamaDiagnostics.ServerLog() + " yet.";
                logScroll.ScrollToEnd();
            }, "Btn.Ghost", Icons.Lines);
            logBtn.ToolTip = "Shows the last lines of server.log, where start-up errors are recorded.";
            var startBtn = Ui.Button("Start server", async () =>
            {
                trNote.Text = "Starting the local server…"; trNote.Foreground = Ui.Res("B.TextMute");
                try { await Ollama.StartAsync(); trNote.Text = "Server started."; trNote.Foreground = Ui.Res("B.Mint"); }
                catch (Exception ex) { trNote.Text = ex.Message; trNote.Foreground = Ui.Res("B.Amber"); }
                await RunChecks(); await refreshModels();
            }, "Btn.Ghost", Icons.Play);
            startBtn.ToolTip = "Starts 'ollama serve' hidden on 127.0.0.1, then re-checks.";
            var portBtn = Ui.Button("Find a free port", () =>
            {
                int p = Enumerable.Range(11435, 200).FirstOrDefault(ServerManager.PortFree);
                trNote.Text = p == 0 ? "No free port was found between 11435 and 11634." : $"Port {p} is free. Type it into the Port box at the top of this page, then press Start server.";
                trNote.Foreground = Ui.Res(p == 0 ? "B.Amber" : "B.TextSoft");
            }, "Btn.Ghost", Icons.Search);
            portBtn.ToolTip = "Suggests an unused port above 11434 for when the default is taken. It does not change any setting.";
            var trActions = new WrapPanel();
            foreach (var b in new[] { runBtn, startBtn, logBtn, logsBtn, modelsBtn, portBtn }) trActions.Children.Add(b);
            trBox.Children.Add(trActions); trBox.Children.Add(trRows); trBox.Children.Add(logScroll); trBox.Children.Add(trNote);
            suite.Troubleshooter = Card(trBox);

            // ================================================================ initial paint
            paintCatalogue(); paintCart();
            suite.Catalogue.Loaded += async (o, e) => { if (hw == null) await ReadHw(); };
            suite.Troubleshooter.Loaded += async (o, e) => { if (trRows.Children.Count == 0) await RunChecks(); };
            return suite;
        }

        private static void OpenFolder(string path, TextBlock note)
        {
            try
            {
                if (!Directory.Exists(path)) { note.Text = path + " does not exist yet."; note.Foreground = Ui.Res("B.Amber"); return; }
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { note.Text = ex.Message; note.Foreground = Ui.Res("B.Amber"); }
        }
    }
}
