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
            var runningHost = new StackPanel();
            SearchField filter = new SearchField("ollama:models", "Search installed models");
            var chatModelChips = new WrapPanel();
            Button installBtn = null, startBtn = null, stopBtn = null;
            OllamaSuite suite = null;

            void Say(string text, string brush = "B.TextMute") { notice.Text = text; notice.Foreground = Ui.Res(brush); }

            // ---- status card
            var statusBox = new StackPanel();
            statusBox.Children.Add(CardTitle("Ollama on this computer", "Detected from the installed program and the local server's version endpoint. Default address: 127.0.0.1:11434."));
            statusBox.Children.Add(statusLine);
            statusBox.Children.Add(detail);

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
            root.Children.Add(Card(statusBox, "#FF2B4A3F"));

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
            var pullBar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 6, 0, 6), Foreground = Ui.Res("B.Mint"), Background = Ui.Res("B.Sunken"), Visibility = Visibility.Collapsed };
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
                pullBar.Visibility = Visibility.Visible; pullBar.IsIndeterminate = true; pullBar.Value = 0;
                pullText.Text = "Contacting the server…"; pullText.Foreground = Ui.Res("B.TextMute");
                pullCts = new CancellationTokenSource();
                try
                {
                    await Ollama.PullAsync(name, (s, f) =>
                    {
                        pullText.Text = f >= 0 ? $"{s} · {f * 100:0}%" : s;
                        if (f >= 0) { pullBar.IsIndeterminate = false; pullBar.Value = f; } else pullBar.IsIndeterminate = true;
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

            // ---- chat test card
            var chatBox = new StackPanel();
            chatBox.Children.Add(CardTitle("Quick chat test", "A short local conversation to check that a model answers. Replies stream in as they are produced; nothing is stored."));
            chatBox.Children.Add(Ui.Mono("MODEL", Ui.Res("B.TextMute"), 11));
            chatBox.Children.Add(chatModelChips);
            chatBox.Children.Add(Choice("Keep the model loaded after replying (default 5 minutes)",
                new[] { ("5 minutes", "5m"), ("30 minutes", "30m"), ("1 hour", "1h"), ("Unload at once", "0") }, Settings.Current.OllamaKeepAlive ?? "5m",
                v => Settings.Current.OllamaKeepAlive = v));
            var chatOut = new TextBlock { Style = Ui.Style("T.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap };
            var chatScroll = new ScrollViewer { Content = chatOut, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var chatFrame = new Border { Background = Ui.Res("B.Ground"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 8), Child = chatScroll };
            var prompt = Input("", double.NaN, "Type a message and press Enter or Send");
            prompt.HorizontalAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetName(prompt, "Chat message");
            var chatStats = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            CancellationTokenSource chatCts = null;
            var turns = new List<OllamaChatTurn>();
            var sendBtn = Ui.Button("Send", null, "Btn.Accent", Icons.Bolt);
            var stopChat = Ui.Button("Stop", () => chatCts?.Cancel(), "Btn.Ghost", Icons.Pause);
            stopChat.IsEnabled = false;
            var clearChat = Ui.Button("Clear", () => { turns.Clear(); chatOut.Text = ""; chatStats.Text = ""; }, "Btn.Ghost", Icons.Trash);
            async Task Send()
            {
                var text = prompt.Text.Trim();
                if (text.Length == 0) return;
                var model = Settings.Current.OllamaLastModel;
                if (string.IsNullOrWhiteSpace(model)) { chatStats.Text = "Choose an installed model above first."; chatStats.Foreground = Ui.Res("B.Amber"); return; }
                if (await Ollama.VersionAsync() == null) { chatStats.Text = "The Ollama server is not running."; chatStats.Foreground = Ui.Res("B.Amber"); return; }
                prompt.Text = "";
                turns.Add(new OllamaChatTurn { Role = "user", Content = text });
                chatOut.Text += (chatOut.Text.Length > 0 ? "\n\n" : "") + "You: " + text + "\n" + model + ": ";
                sendBtn.IsEnabled = false; stopChat.IsEnabled = true;
                chatStats.Text = "Waiting for the model…"; chatStats.Foreground = Ui.Res("B.TextMute");
                chatCts = new CancellationTokenSource();
                var reply = new StringBuilder();
                try
                {
                    var stats = await Ollama.ChatAsync(model, turns, c => { reply.Append(c); chatOut.Text += c; chatScroll.ScrollToEnd(); }, chatCts.Token);
                    turns.Add(new OllamaChatTurn { Role = "assistant", Content = reply.ToString() });
                    chatStats.Text = stats.Length > 0 ? stats : "Done."; chatStats.Foreground = Ui.Res("B.Mint");
                }
                catch (OperationCanceledException)
                {
                    if (reply.Length > 0) turns.Add(new OllamaChatTurn { Role = "assistant", Content = reply.ToString() });
                    chatStats.Text = "Stopped."; chatStats.Foreground = Ui.Res("B.TextMute");
                }
                catch (Exception ex) { turns.RemoveAt(turns.Count - 1); chatStats.Text = ex.Message; chatStats.Foreground = Ui.Res("B.Amber"); }
                sendBtn.IsEnabled = true; stopChat.IsEnabled = false;
                await RefreshRunning();
            }
            sendBtn.Click += async (o, e) => await Send();
            prompt.KeyDown += async (o, e) => { if (e.Key == Key.Enter && sendBtn.IsEnabled) { e.Handled = true; await Send(); } };
            sendBtn.ToolTip = "Sends the message to the chosen local model.";
            stopChat.ToolTip = "Stops the reply that is streaming.";
            clearChat.ToolTip = "Clears this test conversation.";
            chatBox.Children.Add(chatFrame);
            chatBox.Children.Add(prompt);
            var chatRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            chatRow.Children.Add(sendBtn); chatRow.Children.Add(stopChat); chatRow.Children.Add(clearChat);
            chatBox.Children.Add(chatRow);
            chatBox.Children.Add(chatStats);
            root.Children.Add(Card(chatBox));

            // ---- what is not here
            var scope = new StackPanel();
            scope.Children.Add(CardTitle("Not included yet", "Stated plainly so nothing here is mistaken for more than it is."));
            scope.Children.Add(Ui.Text("The built-in catalogue is a dated snapshot, not the full library, and its sizes are typical figures. Ollama publishes no documented catalogue API, so the online search reads the public search page and can break if that page changes. Hardware verdicts are a rule of thumb. There are no file attachments in chat and no launching of other local tools from here. See docs/features/ollama for the full list."));
            root.Children.Add(Card(scope));
            root.Children.Add(suite.Troubleshooter);

            // ---------------------------------------------------------------- refresh logic
            void PaintModels()
            {
                modelsHost.Children.Clear();
                var hits = models.Where(m => filter.State.Matches($"{m.Name} {m.Family} {m.ParameterSize} {m.Quantization}")).ToList();
                filter.SampleSource = () => models.Select(m => m.Name);
                if (!st.ServerUp)
                    modelsHost.Children.Add(Ui.Text("The server is not running, so the model list cannot be read. Start it above."));
                else if (models.Count == 0)
                    modelsHost.Children.Add(Ui.Text("No models are installed yet. Pull one below."));
                foreach (var m in hits)
                {
                    var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var info = new StackPanel();
                    info.Children.Add(new TextBlock { Text = m.Name, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body") });
                    var meta = string.Join(" · ", new[] { Ollama.Bytes(m.Size), m.Family, m.ParameterSize, m.Quantization }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    info.Children.Add(Ui.Mono(meta, Ui.Res("B.TextMute"), 11));
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
                    modelsHost.Children.Add(g);
                }
                filter.SetCount(hits.Count, models.Count, "models");

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
            }

            async Task RefreshModels()
            {
                try { models = st.ServerUp ? await Ollama.ModelsAsync() : new List<OllamaModel>(); }
                catch (Exception ex) { models = new List<OllamaModel>(); Say("Could not read the model list: " + ex.Message, "B.Amber"); }
                PaintModels();
                await RefreshRunning();
            }

            async Task RefreshRunning()
            {
                runningHost.Children.Clear();
                if (!st.ServerUp) { runningHost.Children.Add(Ui.Text("The server is not running.")); return; }
                try
                {
                    var running = await Ollama.RunningAsync();
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
                await RefreshModels();
            }

            root.Loaded += async (o, e) => await RefreshAll();
            return root;
        }

        private sealed class Settings_Ollama_State { public bool Installed, ServerUp; }
    }
}
