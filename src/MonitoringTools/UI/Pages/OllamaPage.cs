using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonitoringTools.Core;
using MonitoringTools.UI.Search;

namespace MonitoringTools.UI
{
    public partial class ControlCentreWindow
    {
        private const string OllamaDownloadPage = "https://ollama.com/download";

        /// <summary>The local Ollama manager: install, start and stop, installed and running models, pull, delete and a quick chat test. Localhost only.</summary>
        private FrameworkElement OllamaPage()
        {
            var root = PageRoot("Ollama", "Local models",
                "Run language models on this computer with Ollama. Everything here talks only to 127.0.0.1; nothing is sent to a cloud service.");

            // ---- shared state for the page
            var st = new Settings_Ollama_State();
            List<OllamaModel> models = new List<OllamaModel>();
            var statusLine = Ui.Mono("Checking for Ollama…", Ui.Res("B.TextMute"), 12);
            var detail = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            var notice = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            notice.TextWrapping = TextWrapping.Wrap;
            var modelsHost = new StackPanel();
            // Models are matched by name, because the list is re-read from the server and its objects are replaced.
            Action paintModels = null;
            var bulk = new BulkSelect<OllamaModel>(() => paintModels?.Invoke(), "models", new ModelNameComparer());
            var runningHost = new StackPanel();
            SearchField filter = new SearchField("ollama:models", "Search installed models");
            var chatModelChips = new WrapPanel();
            Button installBtn = null, startBtn = null, stopBtn = null;
            OllamaSuite suite = null;
            OllamaChatUi chatUi = null;
            Border heroCard = null;

            void Say(string text, string brush = "B.TextMute") { notice.Text = text; notice.Foreground = Ui.Res(brush); }

            // ---- status hero: a live dot that breathes while the server runs, the status in large type, then three numbers.
            // Design: design/motion-and-pages/Main.dc.html.
            var statusBox = new StackPanel();
            var (heroDot, setHeroDot) = Ui.LiveDot(Ui.Res("B.TextMute"), false, 14);
            statusLine.FontSize = 22; statusLine.FontFamily = Ui.Font("F.Body"); statusLine.FontWeight = FontWeights.Medium;
            var heroHead = new StackPanel { Orientation = Orientation.Horizontal };
            heroHead.Children.Add(heroDot);
            var heroText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            heroText.Children.Add(statusLine);
            heroText.Children.Add(detail);
            heroHead.Children.Add(heroText);
            statusBox.Children.Add(heroHead);
            var tiles = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 18, 0, 0) };
            TextBlock Tile(string label)
            {
                var n = new TextBlock { Text = "–", FontSize = 28, FontWeight = FontWeights.Medium, Foreground = Ui.Res("B.Text"), FontFamily = Ui.Font("F.Display") };
                var sp = new StackPanel();
                sp.Children.Add(n);
                sp.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Ui.Res("B.TextMute"), FontFamily = Ui.Font("F.Body"), TextWrapping = TextWrapping.Wrap });
                tiles.Children.Add(new Border { Background = Ui.Res("B.Window"), CornerRadius = new CornerRadius(20), Padding = new Thickness(16, 12, 16, 14), Margin = new Thickness(0, 0, 10, 0), Child = sp });
                return n;
            }
            var tileInstalled = Tile("installed models");
            var tileDisk = Tile("on disk");
            var tileLoaded = Tile("loaded in memory");
            statusBox.Children.Add(tiles);

            var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            installBtn = Ui.Button("Install with winget", () => ConfirmInstall(), "Btn.Accent", Icons.Plus);
            installBtn.ToolTip = "Runs: winget install -e --id Ollama.Ollama. Asks for your consent first. Default: not run unless you confirm.";
            startBtn = Ui.Button("Start server", async () =>
            {
                Say("Starting the local server…");
                try { await Ollama.StartAsync(); Say("Server started on 127.0.0.1:" + Ollama.Port + ".", "B.Mint"); }
                catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                await RefreshAll();
            }, "Btn.Ghost", Icons.Play);
            startBtn.ToolTip = "Starts 'ollama serve' hidden and bound to 127.0.0.1 only.";
            stopBtn = Ui.Button("Stop server", () =>
            {
                var dlg = new ConfirmWindow("Stop Ollama?", "This ends every Ollama server and tray process owned by you, and unloads running models. Downloaded models stay on disk.", "Stop Ollama", danger: false);
                dlg.Confirmed += async _ =>
                {
                    int n = await Task.Run(Ollama.StopAll);
                    Say(n == 0 ? "No Ollama process was running." : $"Stopped {n} Ollama process(es).", n == 0 ? "B.TextMute" : "B.Mint");
                    await Task.Delay(500);
                    await RefreshAll();
                };
                dlg.Show();
            }, "Btn.Ghost", Icons.Pause);
            stopBtn.ToolTip = "Ends the Ollama processes you own. Models stay on disk.";
            var refreshBtn = Ui.Button("Refresh", async () => await RefreshAll(), "Btn.Ghost", Icons.Restart);
            refreshBtn.ToolTip = "Reads the server state again.";
            var downloadBtn = Ui.Button("Official download page", () =>
            {
                try { Process.Start(new ProcessStartInfo(OllamaDownloadPage) { UseShellExecute = true }); } catch (Exception ex) { Say(ex.Message, "B.Amber"); }
            }, "Btn.Ghost", Icons.Link);
            downloadBtn.ToolTip = "Opens ollama.com/download in your browser, for when winget is not available.";
            foreach (var b in new[] { installBtn, startBtn, stopBtn, refreshBtn, downloadBtn }) actions.Children.Add(b);
            statusBox.Children.Add(actions);

            var portRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            var portBox = Input(Settings.Current.OllamaPort.ToString(), 100, "Port of the local Ollama server, 1024 to 65535. Default 11434.");
            portBox.LostFocus += async (o, e) =>
            {
                if (int.TryParse(portBox.Text, out var p) && p >= 1024 && p <= 65535) { Settings.Current.OllamaPort = p; Settings.Save(); await RefreshAll(); }
                else portBox.Text = Settings.Current.OllamaPort.ToString();
            };
            AutomationProperties.SetName(portBox, "Ollama server port");
            portRow.Children.Add(new StackPanel { Children = { Ui.Mono("PORT (default 11434)", Ui.Res("B.TextMute"), 11), portBox } });
            statusBox.Children.Add(portRow);
            statusBox.Children.Add(notice);
            heroCard = Card(statusBox);
            heroCard.CornerRadius = new CornerRadius(28);
            root.Children.Add(heroCard);
            void PaintHero(bool up, bool installed)
            {
                // Primary container while the server runs; amber wash when it is installed but stopped or missing.
                heroCard.Background = Ui.Res(up ? "B.MintWash" : "B.AmberWash");
                setHeroDot(Ui.Res(up ? "B.Mint" : "B.Amber"), up);
                statusLine.Foreground = Ui.Res(up ? "B.OnPrimaryContainer" : "B.Amber");
            }

            void ConfirmInstall()
            {
                if (Ollama.FindExe() != null) { Say("Ollama is already installed at " + Ollama.FindExe() + ". Nothing was changed.", "B.TextMute"); return; }
                if (!Ollama.WingetAvailable()) { Say("The Windows Package Manager (winget) is not available here. Use the official download page instead.", "B.Amber"); return; }
                var dlg = new ConfirmWindow("Install Ollama?",
                    "This runs the Windows Package Manager: winget install -e --id Ollama.Ollama. It downloads the official Ollama installer through winget's own source and installs it for your user account. Nothing is installed until you confirm.",
                    "Install", danger: false);
                dlg.Confirmed += async _ =>
                {
                    installBtn.IsEnabled = false;
                    Say("Installing with winget…");
                    var cts = new CancellationTokenSource();
                    try
                    {
                        int code = await Ollama.InstallWithWingetAsync(l => Dispatcher.BeginInvoke(new Action(() => Say(l))), cts.Token);
                        Say(code == 0 ? "✓ winget finished. Press Start server." : $"winget exited with code {code}. Ollama may not be installed; check the line above.", code == 0 ? "B.Mint" : "B.Amber");
                    }
                    catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                    installBtn.IsEnabled = true;
                    await RefreshAll();
                };
                dlg.Show();
            }

            // ---- installed models card
            var modelsBox = new StackPanel();
            modelsBox.Children.Add(CardTitle("Installed models", "Size and family come from the server's tag list. Delete asks twice and cannot be undone."));
            filter.Margin = new Thickness(0, 0, 0, 10);
            modelsBox.Children.Add(filter);
            modelsBox.Children.Add(modelsHost);
            root.Children.Add(Card(modelsBox));
            filter.Filter += () => PaintModels();

            // ---- pull card
            var pullBox = new StackPanel();
            pullBox.Children.Add(CardTitle("Pull a model", "Type a model name and tag, for example llama3.2 or qwen2.5:7b. The local server downloads it and reports progress here."));
            var pullName = Input("", 280, "Model name with an optional tag, for example llama3.2:3b");
            AutomationProperties.SetName(pullName, "Model to pull");
            var pullBar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 6, 0, 6), Foreground = Ui.Res("B.Mint"), Background = Ui.Res("B.Sunken"), Visibility = Visibility.Collapsed };
            var pullText = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            CancellationTokenSource pullCts = null;
            var pullBtn = Ui.Button("Pull", null, "Btn.Accent", Icons.Plus);
            var cancelPull = Ui.Button("Cancel", () => pullCts?.Cancel(), "Btn.Ghost", Icons.Close);
            cancelPull.IsEnabled = false;
            pullBtn.Click += async (o, e) =>
            {
                var name = pullName.Text.Trim();
                if (name.Length == 0) { pullText.Text = "Type a model name first."; pullText.Foreground = Ui.Res("B.Amber"); return; }
                if (await Ollama.VersionAsync() == null) { pullText.Text = "The Ollama server is not running. Start it above first."; pullText.Foreground = Ui.Res("B.Amber"); return; }
                pullBtn.IsEnabled = false; cancelPull.IsEnabled = true;
                pullBar.Visibility = Visibility.Visible; pullBar.IsIndeterminate = true; pullBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null); pullBar.Value = 0;
                Ui.Expand(pullBar);
                pullText.Text = "Contacting the server…"; pullText.Foreground = Ui.Res("B.TextMute");
                pullCts = new CancellationTokenSource();
                try
                {
                    await Ollama.PullAsync(name, (s, f) =>
                    {
                        pullText.Text = f >= 0 ? $"{s} · {f * 100:0}%" : s;
                        if (f >= 0) { pullBar.IsIndeterminate = false; Ui.Glide(pullBar, f); } else pullBar.IsIndeterminate = true;
                    }, pullCts.Token);
                    pullText.Text = $"✓ {name} is ready."; pullText.Foreground = Ui.Res("B.Mint"); Sounds.Play(Cue.Complete);
                }
                catch (OperationCanceledException) { pullText.Text = "Pull cancelled. Finished layers are kept and resume next time."; pullText.Foreground = Ui.Res("B.TextMute"); }
                catch (Exception ex) { pullText.Text = ex.Message; pullText.Foreground = Ui.Res("B.Amber"); Sounds.Play(Cue.Warning); }
                pullBar.Visibility = Visibility.Collapsed; pullBtn.IsEnabled = true; cancelPull.IsEnabled = false;
                await RefreshModels();
            };
            pullBtn.ToolTip = "Downloads the named model through the local server.";
            cancelPull.ToolTip = "Stops the download. Layers already fetched are kept.";
            var pullRow = new WrapPanel(); pullRow.Children.Add(pullName); pullRow.Children.Add(pullBtn); pullRow.Children.Add(cancelPull);
            pullBox.Children.Add(pullRow); pullBox.Children.Add(pullBar); pullBox.Children.Add(pullText);
            root.Children.Add(Card(pullBox));

            // ---- catalogue, cart, model details and troubleshooter (OllamaPage.Suite.cs)
            suite = BuildOllamaSuite(() => RefreshModels());
            root.Children.Add(suite.Catalogue);
            root.Children.Add(suite.Cart);
            root.Children.Add(suite.Details);

            // ---- running models card
            var runBox = new StackPanel();
            runBox.Children.Add(CardTitle("Loaded in memory", "From the server's process list. A model unloads by itself after its keep-alive time."));
            runBox.Children.Add(runningHost);
            root.Children.Add(Card(runBox));

            // ---- chat card (attachments, saved chats, profiles, parameters, capability badge)
            chatUi = BuildOllamaChat(chatModelChips, () => st.ServerUp, () => models.Select(m => m.Name),
                name => { Settings.Current.OllamaLastModel = name; Settings.Save(); PaintModels(); },
                () => RefreshRunning());
            root.Children.Add(chatUi.Card);

            // ---- what is not here
            var scope = new StackPanel();
            scope.Children.Add(CardTitle("Not included yet", "Stated plainly so nothing here is mistaken for more than it is."));
            scope.Children.Add(Ui.Text("The built-in catalogue is a dated snapshot, not the full library, and its sizes are typical figures. Ollama publishes no documented catalogue API, so the online search reads the public search page and can break if that page changes. Hardware verdicts are a rule of thumb. Chat attachments are limited to images and text files, saved chats keep no image bytes, and profiles hold a system prompt and parameters only (no tool-calling definitions). There is no launching of other local tools from here. See docs/features/ollama for the full list."));
            root.Children.Add(Card(scope));
            root.Children.Add(suite.Troubleshooter);

            // ---------------------------------------------------------------- refresh logic
            void PaintModels(bool animate = false)
            {
                modelsHost.Children.Clear();
                bulk.Begin();
                bulk.Prune(models);
                var hits = models.Where(m => filter.State.Matches($"{m.Name} {m.Family} {m.ParameterSize} {m.Quantization}")).ToList();
                filter.SampleSource = () => models.Select(m => m.Name);
                if (!st.ServerUp)
                    modelsHost.Children.Add(Ui.Text("The server is not running, so the model list cannot be read. Start it above."));
                else if (models.Count == 0)
                    modelsHost.Children.Add(Ui.Text("No models are installed yet. Pull one below."));
                if (models.Count > 0)
                    modelsHost.Children.Add(bulk.Bar(new[]
                    {
                        new BulkAction<OllamaModel>
                        {
                            Label = "Delete selected…", Danger = true, Hint = "Delete the chosen models from disk, after a two-key confirmation. You would need to pull them again to use them.",
                            Run = items =>
                            {
                                var chosen = items.ToList();
                                long total = chosen.Sum(x => x.Size);
                                bulk.ConfirmThen($"Delete {chosen.Count} {(chosen.Count == 1 ? "model" : "models")}?", $"This removes {string.Join(", ", chosen.Take(5).Select(x => x.Name))}{(chosen.Count > 5 ? " and " + (chosen.Count - 5) + " more" : "")} from disk ({Ollama.Bytes(total)}). You would need to pull them again to use them.", "Delete", () => _ = DeleteChosen(chosen));
                            }
                        },
                    }));
                foreach (var m in hits)
                {
                    var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    // avatar tile with the model's initial, its name, then size, family, parameters and quantisation as chips
                    var info = new StackPanel { Orientation = Orientation.Horizontal };
                    var initial = string.IsNullOrEmpty(m.Name) ? "?" : m.Name.Substring(0, 1).ToUpperInvariant();
                    info.Children.Add(new Border
                    {
                        Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Ui.Res("B.TertiaryContainer"), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = initial, FontWeight = FontWeights.Bold, FontSize = 14, Foreground = Ui.Res("B.OnTertiaryContainer"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontFamily = Ui.Font("F.Body") }
                    });
                    var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    words.Children.Add(new TextBlock { Text = m.Name, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
                    var chipsRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                    foreach (var x in new[] { Ollama.Bytes(m.Size), m.Family, m.ParameterSize, m.Quantization }.Where(x => !string.IsNullOrWhiteSpace(x)))
                        chipsRow.Children.Add(Ui.Pill(x));
                    words.Children.Add(chipsRow);
                    info.Children.Add(words);
                    g.Children.Add(info);
                    var detailBtn = Ui.Button("Details", () => suite.ShowDetails(m.Name), "Btn.Ghost", Icons.Lines);
                    detailBtn.ToolTip = "Show parameters, template, context length and licence for " + m.Name + ".";
                    AutomationProperties.SetName(detailBtn, "Details of " + m.Name);
                    var del = Ui.Button("Delete", () =>
                    {
                        var dlg = new ConfirmWindow($"Delete {m.Name}?", $"This removes the model from disk ({Ollama.Bytes(m.Size)}). You would need to pull it again to use it.", "Delete", danger: true);
                        dlg.Confirmed += async _ =>
                        {
                            try { await Ollama.DeleteAsync(m.Name); Say($"Deleted {m.Name}.", "B.Mint"); }
                            catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                            await RefreshModels();
                        };
                        dlg.Show();
                    }, "Btn.Danger", Icons.Trash);
                    del.ToolTip = "Delete " + m.Name + " from disk (asks twice).";
                    AutomationProperties.SetName(del, "Delete " + m.Name);
                    var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
                    btnRow.Children.Add(detailBtn); btnRow.Children.Add(del);
                    Grid.SetColumn(btnRow, 1); g.Children.Add(btnRow);
                    SearchScope.SetItem(g, "model " + m.Name + " " + m.Family);
                    modelsHost.Children.Add(bulk.Wrap(m, g, m.Name));
                }
                filter.SetCount(hits.Count, models.Count, "models");
                if (animate) Ui.RiseAll(modelsHost.Children);

                chatModelChips.Children.Clear();
                if (models.Count == 0) chatModelChips.Children.Add(Ui.Mono(st.ServerUp ? "No installed models." : "Server not running.", Ui.Res("B.TextMute"), 11));
                if (models.Count > 0 && !models.Any(m => m.Name == Settings.Current.OllamaLastModel)) { Settings.Current.OllamaLastModel = models[0].Name; Settings.Save(); }
                foreach (var m in models)
                {
                    var name = m.Name;
                    var b = Ui.Button(name, null, "Btn.Chip");
                    b.Height = 28; b.Margin = new Thickness(0, 0, 4, 4);
                    b.ToolTip = "Use " + name + " for the chat test";
                    b.Click += (o, e) => { Settings.Current.OllamaLastModel = name; Settings.Save(); PaintModels(); };
                    bool on = name == Settings.Current.OllamaLastModel;
                    b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                    chatModelChips.Children.Add(b);
                }
                chatUi?.ModelChanged?.Invoke();
            }

            async Task DeleteChosen(List<OllamaModel> chosen)
            {
                int ok = 0; string failed = null;
                foreach (var m in chosen)
                {
                    try { await Ollama.DeleteAsync(m.Name); ok++; }
                    catch (Exception ex) { failed = $"{m.Name}: {ex.Message}"; break; }
                }
                if (failed == null) Say($"Deleted {ok} {(ok == 1 ? "model" : "models")}.", "B.Mint");
                else Say($"Deleted {ok} of {chosen.Count}. Stopped at {failed}", "B.Amber");
                await RefreshModels();
            }

            async Task RefreshModels()
            {
                try { models = st.ServerUp ? await Ollama.ModelsAsync() : new List<OllamaModel>(); }
                catch (Exception ex) { models = new List<OllamaModel>(); Say("Could not read the model list: " + ex.Message, "B.Amber"); }
                PaintModels(animate: true);
                tileInstalled.Text = st.ServerUp ? models.Count.ToString() : "–";
                tileDisk.Text = st.ServerUp ? Ollama.Bytes(models.Sum(m => m.Size)) : "–";
                await RefreshRunning();
            }

            async Task RefreshRunning()
            {
                runningHost.Children.Clear();
                if (!st.ServerUp) { tileLoaded.Text = "–"; runningHost.Children.Add(Ui.Text("The server is not running.")); return; }
                try
                {
                    var running = await Ollama.RunningAsync();
                    tileLoaded.Text = running.Count.ToString();
                    if (running.Count == 0) runningHost.Children.Add(Ui.Text("No model is loaded right now."));
                    foreach (var r in running)
                    {
                        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                        g.ColumnDefinitions.Add(new ColumnDefinition());
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var info = new StackPanel();
                        info.Children.Add(new TextBlock { Text = r.Name, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
                        var where = r.SizeVram >= r.Size && r.Size > 0 ? "fully on the GPU" : r.SizeVram > 0 ? $"{Ollama.Bytes(r.SizeVram)} on the GPU" : "on the CPU";
                        info.Children.Add(Ui.Mono($"{Ollama.Bytes(r.Size)} in memory · {where}" + (r.Expires > DateTime.Now ? $" · unloads at {r.Expires:HH:mm}" : ""), Ui.Res("B.TextMute"), 11));
                        // GPU and CPU share as one split bar: mint for the GPU part, amber for the rest.
                        double gpu = r.Size > 0 ? Math.Clamp((double)r.SizeVram / r.Size, 0, 1) : 0;
                        var split = new Grid { Height = 8, Margin = new Thickness(0, 8, 16, 0) };
                        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(gpu, 0.0001), GridUnitType.Star) });
                        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - gpu, 0.0001), GridUnitType.Star) });
                        var gpuPart = new Border { Background = Ui.Res("B.Mint"), CornerRadius = new CornerRadius(4, 0, 0, 4) };
                        var cpuPart = new Border { Background = Ui.Res("B.Amber"), CornerRadius = new CornerRadius(0, 4, 4, 0) };
                        Grid.SetColumn(cpuPart, 1);
                        split.Children.Add(gpuPart); split.Children.Add(cpuPart);
                        split.ToolTip = $"{gpu * 100:0}% on the GPU, {(1 - gpu) * 100:0}% on the CPU";
                        info.Children.Add(split);
                        g.Children.Add(info);
                        var name = r.Name;
                        var unload = Ui.Button("Unload", async () =>
                        {
                            try { await Ollama.UnloadAsync(name); } catch (Exception ex) { Say(ex.Message, "B.Amber"); }
                            await Task.Delay(400); await RefreshRunning();
                        }, "Btn.Ghost", Icons.Close);
                        unload.ToolTip = "Free the memory this model uses now.";
                        AutomationProperties.SetName(unload, "Unload " + name);
                        Grid.SetColumn(unload, 1); g.Children.Add(unload);
                        runningHost.Children.Add(g);
                    }
                }
                catch (Exception ex) { runningHost.Children.Add(Ui.Text("Could not read running models: " + ex.Message)); }
            }

            async Task RefreshAll()
            {
                var exe = await Task.Run(Ollama.FindExe);
                var version = await Ollama.VersionAsync();
                st.ServerUp = version != null;
                st.Installed = exe != null;
                if (st.ServerUp) { statusLine.Text = $"Running · version {version} · 127.0.0.1:{Ollama.Port}"; statusLine.Foreground = Ui.Res("B.Mint"); }
                else if (st.Installed) { statusLine.Text = "Installed, server not running"; statusLine.Foreground = Ui.Res("B.Amber"); }
                else { statusLine.Text = "Ollama is not installed"; statusLine.Foreground = Ui.Res("B.Amber"); }
                detail.Text = st.Installed ? "Program: " + exe : "Install it with winget below, or use the official download page.";
                installBtn.IsEnabled = !st.Installed;
                installBtn.Visibility = st.Installed ? Visibility.Collapsed : Visibility.Visible;
                startBtn.IsEnabled = st.Installed && !st.ServerUp;
                stopBtn.IsEnabled = st.ServerUp || Process.GetProcessesByName("ollama").Length > 0;
                PaintHero(st.ServerUp, st.Installed);
                await RefreshModels();
                Core.Language.Refresh(root);   // text written after the page loaded still follows the language
            }

            paintModels = () => PaintModels();
            root.Loaded += async (o, e) => await RefreshAll();
            return root;
        }

        private sealed class Settings_Ollama_State { public bool Installed, ServerUp; }
    }

    internal sealed class ModelNameComparer : IEqualityComparer<OllamaModel>
    {
        public bool Equals(OllamaModel a, OllamaModel b) => string.Equals(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(OllamaModel m) => (m?.Name ?? "").ToLowerInvariant().GetHashCode();
    }
}
