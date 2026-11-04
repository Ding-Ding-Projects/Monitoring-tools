using System;
using System.Collections.Generic;
using System.IO;
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
        /// <summary>Handle to the chat card so the page can tell it when the selected model changes.</summary>
        private sealed class OllamaChatUi
        {
            public FrameworkElement Card;
            public Action ModelChanged;
        }

        private const int MaxPendingAttachments = 6;

        /// <summary>
        /// The Ollama chat card: attachments (images for vision models, text files inlined), saved chats, profiles
        /// (system prompt presets), per-chat parameters and an honest capability badge read from /api/show.
        /// </summary>
        private OllamaChatUi BuildOllamaChat(WrapPanel modelChips, Func<bool> serverUp, Func<IEnumerable<string>> installed, Action<string> chooseModel, Func<Task> afterReply)
        {
            var ui = new OllamaChatUi();
            var box = new StackPanel();
            box.Children.Add(CardTitle("Chat",
                "A local conversation with an installed model. Replies stream in as they are produced. Chats are stored only if you press Save chat or switch automatic saving on; they never leave this computer."));

            // ---------------------------------------------------------------- state
            var session = new OllamaSession();
            var turns = new List<OllamaChatTurn>();
            var pending = new List<OllamaAttachment>();
            var capCache = new Dictionary<string, (OllamaCapabilityInfo Info, long Context)>(StringComparer.OrdinalIgnoreCase);
            var profiles = OllamaProfiles.Load();
            CancellationTokenSource chatCts = null;

            string CurrentModel() => Settings.Current.OllamaLastModel;

            // ---------------------------------------------------------------- model and capability badge
            box.Children.Add(Ui.Mono("MODEL", Ui.Res("B.TextMute"), 11));
            box.Children.Add(modelChips);
            var capRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 2) };
            var capNote = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            capNote.TextWrapping = TextWrapping.Wrap;
            box.Children.Add(capRow);
            box.Children.Add(capNote);

            Border Pill(string text, bool on)
            {
                var t = new TextBlock { Text = text, FontSize = 11, FontFamily = Ui.Font("F.Body"), Foreground = Ui.Res(on ? "B.OnPrimaryContainer" : "B.TextMute") };
                return new Border { Child = t, CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 2, 9, 3), Margin = new Thickness(0, 0, 6, 4), Background = Ui.Res(on ? "B.MintWash" : "B.Sunken") };
            }

            void PaintCaps(string model, (OllamaCapabilityInfo Info, long Context)? got, string error)
            {
                capRow.Children.Clear();
                capNote.Foreground = Ui.Res("B.TextMute");
                if (string.IsNullOrWhiteSpace(model)) { capNote.Text = "Choose an installed model to see what it can do."; return; }
                if (error != null) { capNote.Text = error; capNote.Foreground = Ui.Res("B.Amber"); return; }
                if (got == null) { capNote.Text = "Reading capabilities from the server…"; return; }
                var (info, ctx) = got.Value;
                if (!info.Reported)
                {
                    capNote.Text = "This Ollama version does not report model capabilities, so vision and tool support are unknown. Update Ollama to see them. Images can still be attached, but the model may ignore them.";
                    capNote.Foreground = Ui.Res("B.Amber");
                    return;
                }
                capRow.Children.Add(Pill(info.Vision ? "Vision" : "No vision", info.Vision));
                capRow.Children.Add(Pill(info.Tools ? "Tools" : "No tools", info.Tools));
                capRow.Children.Add(Pill(info.Embedding ? "Embedding" : "Not an embedding model", info.Embedding));
                foreach (var other in info.All.Where(c => c is not ("vision" or "tools" or "embedding")))
                    capRow.Children.Add(Pill(other, true));
                var parts = new List<string> { "Capabilities as reported by the server for " + model + "." };
                if (ctx > 0) parts.Add("Stated context length: " + ctx.ToString("N0") + " tokens.");
                var block = info.ChatBlockReason();
                if (block != null) parts.Add(block);
                capNote.Text = string.Join(" ", parts);
                if (block != null) capNote.Foreground = Ui.Res("B.Amber");
            }

            async Task<(OllamaCapabilityInfo Info, long Context)?> GetCaps(string model, bool force = false)
            {
                if (string.IsNullOrWhiteSpace(model)) return null;
                if (!force && capCache.TryGetValue(model, out var hit)) return hit;
                if (!serverUp()) return null;
                try
                {
                    var d = await Ollama.ShowAsync(model);
                    var r = (OllamaCapabilityInfo.From(d.Capabilities), d.ContextLength);
                    capCache[model] = r;
                    return r;
                }
                catch { return null; }
            }

            async Task RefreshCaps(bool force = false)
            {
                var model = CurrentModel();
                if (string.IsNullOrWhiteSpace(model)) { PaintCaps(model, null, null); return; }
                if (!serverUp()) { PaintCaps(model, null, "The server is not running, so capabilities cannot be read."); return; }
                PaintCaps(model, null, null);
                var got = await GetCaps(model, force);
                if (model != CurrentModel()) return;   // the selection moved on while reading
                PaintCaps(model, got, got == null ? "Could not read capabilities for " + model + " from the server." : null);
            }
            ui.ModelChanged = () => _ = RefreshCaps();

            // ---------------------------------------------------------------- profile and parameters
            box.Children.Add(Ui.Mono("PROFILE (system prompt preset)", Ui.Res("B.TextMute"), 11));
            var profileChips = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            box.Children.Add(profileChips);
            var systemBox = Input("", double.NaN, "Instructions sent before the conversation. Empty sends none. A profile fills this in; you can still edit it for this chat.");
            systemBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            systemBox.AcceptsReturn = true; systemBox.TextWrapping = TextWrapping.Wrap; systemBox.MinHeight = 56; systemBox.MaxHeight = 140;
            systemBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            AutomationProperties.SetName(systemBox, "System prompt");
            box.Children.Add(systemBox);

            var temp = Input("", 80, "Randomness, 0 to 2. Empty uses the model's own default.");
            var topP = Input("", 80, "Nucleus sampling, above 0 and at most 1. Empty uses the model's own default.");
            var numCtx = Input("", 110, "Context window in tokens (128 to 1048576). Empty uses the server default. A larger window needs more memory.");
            var keep = Input("", 80, "How long the model stays loaded after this chat's reply, such as 5m, 1h or 0. Empty uses the default below.");
            AutomationProperties.SetName(temp, "Temperature"); AutomationProperties.SetName(topP, "Top-p");
            AutomationProperties.SetName(numCtx, "Context length in tokens"); AutomationProperties.SetName(keep, "Keep-alive for this chat");
            var paramRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            StackPanel Labelled(string label, TextBox tb) { var sp = new StackPanel { Margin = new Thickness(0, 0, 8, 0) }; sp.Children.Add(Ui.Mono(label, Ui.Res("B.TextMute"), 11)); sp.Children.Add(tb); return sp; }
            paramRow.Children.Add(Labelled("TEMPERATURE", temp)); paramRow.Children.Add(Labelled("TOP-P", topP));
            paramRow.Children.Add(Labelled("CONTEXT (TOKENS)", numCtx)); paramRow.Children.Add(Labelled("KEEP-ALIVE", keep));
            box.Children.Add(paramRow);
            var paramNote = Ui.Mono("Empty fields use the model's own defaults. These apply to this chat only and are saved with it.", Ui.Res("B.TextMute"), 11);
            paramNote.TextWrapping = TextWrapping.Wrap;
            box.Children.Add(paramNote);

            OllamaChatOptions ReadOptions(out string parseProblem)
            {
                parseProblem = null;
                var o = new OllamaChatOptions { SystemPrompt = systemBox.Text.Trim(), KeepAlive = keep.Text.Trim() };
                if (!OllamaChatOptions.TryParseDouble(temp.Text, out var t)) parseProblem = "Temperature is not a number."; else o.Temperature = t;
                if (!OllamaChatOptions.TryParseDouble(topP.Text, out var p)) parseProblem ??= "Top-p is not a number."; else o.TopP = p;
                if (!OllamaChatOptions.TryParseInt(numCtx.Text, out var c)) parseProblem ??= "Context length must be a whole number."; else o.NumCtx = c;
                return o;
            }

            void ShowParamProblems()
            {
                var o = ReadOptions(out var parse);
                long ctx = capCache.TryGetValue(CurrentModel() ?? "", out var g) ? g.Context : 0;
                var bad = parse != null ? new List<string> { parse } : o.Validate(ctx);
                if (bad.Count == 0) { paramNote.Text = "Empty fields use the model's own defaults. These apply to this chat only and are saved with it."; paramNote.Foreground = Ui.Res("B.TextMute"); }
                else { paramNote.Text = string.Join(" ", bad); paramNote.Foreground = Ui.Res("B.Amber"); }
            }
            foreach (var tb in new[] { temp, topP, numCtx, keep }) tb.TextChanged += (a, b) => ShowParamProblems();

            string Fmt(double? d) => d?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";

            var profileName = Input("", 180, "A name for the profile you are about to save. Saving over an existing name replaces it.");
            AutomationProperties.SetName(profileName, "Profile name");
            Action paintProfiles = null;
            void ApplyProfile(OllamaProfile p)
            {
                systemBox.Text = p.SystemPrompt ?? "";
                temp.Text = Fmt(p.Temperature); topP.Text = Fmt(p.TopP); numCtx.Text = p.NumCtx?.ToString() ?? "";
                profileName.Text = p.Name;
                session.Profile = p.Name;
                Settings.Current.OllamaLastProfile = p.Name; Settings.Save();
                paintProfiles();
            }
            paintProfiles = () =>
            {
                profileChips.Children.Clear();
                foreach (var p in profiles)
                {
                    var prof = p;
                    var b = Ui.Button(prof.Name, () => ApplyProfile(prof), "Btn.Chip");
                    b.Height = 28; b.Margin = new Thickness(0, 0, 4, 4);
                    b.ToolTip = string.IsNullOrWhiteSpace(prof.SystemPrompt) ? "No system prompt. Fills the fields below with this profile's parameters." : "Fills the system prompt and parameters below. " + (prof.SystemPrompt.Length > 160 ? prof.SystemPrompt[..160] + "…" : prof.SystemPrompt);
                    bool on = string.Equals(session.Profile, prof.Name, StringComparison.OrdinalIgnoreCase);
                    b.Background = Ui.Res(on ? "B.Mint" : "B.Raised"); b.Foreground = Ui.Res(on ? "B.MintInk" : "B.TextSoft");
                    profileChips.Children.Add(b);
                }
                if (profiles.Count == 0) profileChips.Children.Add(Ui.Mono("No profiles saved. Write a system prompt below and save it as one.", Ui.Res("B.TextMute"), 11));
            };
            var profNote = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            profNote.TextWrapping = TextWrapping.Wrap;
            var saveProfile = Ui.Button("Save as profile", () =>
            {
                var o = ReadOptions(out var parse);
                if (parse != null) { profNote.Text = parse; profNote.Foreground = Ui.Res("B.Amber"); return; }
                var err = OllamaProfiles.Upsert(profiles, new OllamaProfile { Name = profileName.Text, SystemPrompt = o.SystemPrompt, Temperature = o.Temperature, TopP = o.TopP, NumCtx = o.NumCtx });
                if (err != null) { profNote.Text = err; profNote.Foreground = Ui.Res("B.Amber"); return; }
                try { OllamaProfiles.Save(profiles); profNote.Text = "Saved profile “" + profileName.Text.Trim() + "”."; profNote.Foreground = Ui.Res("B.Mint"); session.Profile = profileName.Text.Trim(); Settings.Current.OllamaLastProfile = session.Profile; Settings.Save(); }
                catch (Exception ex) { profNote.Text = "Could not save the profile: " + ex.Message; profNote.Foreground = Ui.Res("B.Amber"); }
                paintProfiles();
            }, "Btn.Ghost", Icons.Plus);
            saveProfile.ToolTip = "Stores the system prompt, temperature, top-p and context length shown here under the name in the box. The same name replaces the old profile.";
            var deleteProfile = Ui.Button("Delete profile", () =>
            {
                var name = profileName.Text.Trim();
                var p = profiles.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (p == null) { profNote.Text = "No saved profile has that name."; profNote.Foreground = Ui.Res("B.Amber"); return; }
                var dlg = new ConfirmWindow($"Delete the profile “{p.Name}”?", "The profile is removed from the list. Chats that used it keep their own copy of the system prompt.", "Delete", danger: true);
                dlg.Confirmed += _ =>
                {
                    profiles.Remove(p);
                    try { OllamaProfiles.Save(profiles); profNote.Text = "Deleted the profile."; profNote.Foreground = Ui.Res("B.Mint"); }
                    catch (Exception ex) { profNote.Text = ex.Message; profNote.Foreground = Ui.Res("B.Amber"); }
                    if (string.Equals(session.Profile, p.Name, StringComparison.OrdinalIgnoreCase)) session.Profile = "";
                    paintProfiles();
                };
                dlg.Show();
            }, "Btn.Danger", Icons.Trash);
            deleteProfile.ToolTip = "Deletes the saved profile whose name is in the box (asks first).";
            var profRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            profRow.Children.Add(profileName); profRow.Children.Add(saveProfile); profRow.Children.Add(deleteProfile);
            box.Children.Add(profRow);
            box.Children.Add(profNote);
            paintProfiles();

            box.Children.Add(Choice("Default keep-alive when a chat sets none (default 5 minutes)",
                new[] { ("5 minutes", "5m"), ("30 minutes", "30m"), ("1 hour", "1h"), ("Unload at once", "0") }, Settings.Current.OllamaKeepAlive ?? "5m",
                v => Settings.Current.OllamaKeepAlive = v,
                "How long a model stays loaded in memory after a reply when the chat does not set its own time. A shorter time frees memory sooner but the next reply starts slower. Default: 5 minutes."));

            // ---------------------------------------------------------------- transcript
            var chatOut = new TextBlock { Style = Ui.Style("T.Mono"), FontSize = 12, Foreground = Ui.Res("B.TextSoft"), TextWrapping = TextWrapping.Wrap };
            var chatScroll = new ScrollViewer { Content = chatOut, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var chatFrame = new Border { Background = Ui.Res("B.Ground"), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 8), Child = chatScroll };
            var chatStats = Ui.Mono("", Ui.Res("B.TextMute"), 11);
            chatStats.TextWrapping = TextWrapping.Wrap;
            void Status(string text, string brush = "B.TextMute") { chatStats.Text = text; chatStats.Foreground = Ui.Res(brush); }

            // ---------------------------------------------------------------- attachments
            var attachRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            void PaintAttachments()
            {
                attachRow.Children.Clear();
                foreach (var a in pending)
                {
                    var att = a;
                    var b = Ui.Button((att.Kind == "image" ? "Image: " : "Text: ") + att.Name + "  ×", () => { pending.Remove(att); PaintAttachments(); }, "Btn.Chip");
                    b.Height = 28; b.Margin = new Thickness(0, 0, 4, 4);
                    b.ToolTip = $"{att.Name}, {Ollama.Bytes(att.Bytes)}" + (att.Truncated ? ", cut to the first " + OllamaAttachments.MaxInlineChars.ToString("N0") + " characters" : "") + ". Click to remove it from the next message.";
                    AutomationProperties.SetName(b, "Remove attachment " + att.Name);
                    attachRow.Children.Add(b);
                }
            }

            async Task AddFiles(IEnumerable<string> paths)
            {
                foreach (var path in paths)
                {
                    if (pending.Count >= MaxPendingAttachments) { Status($"At most {MaxPendingAttachments} files can be attached to one message.", "B.Amber"); break; }
                    try
                    {
                        var att = await Task.Run(() => OllamaAttachments.Load(path));
                        if (att.Kind == "image")
                        {
                            var got = await GetCaps(CurrentModel());
                            var why = got?.Info.ImageBlockReason();
                            if (why != null) { Status(att.Name + ": " + why + " Choose a vision model to send images.", "B.Amber"); continue; }
                            pending.Add(att);
                            Status(got == null ? att.Name + " attached. Vision support could not be checked, so the model may ignore the image." : att.Name + " attached.", got == null ? "B.Amber" : "B.TextMute");
                        }
                        else
                        {
                            pending.Add(att);
                            Status(att.Name + " attached" + (att.Truncated ? "; only the first " + OllamaAttachments.MaxInlineChars.ToString("N0") + " characters will be included." : "."), att.Truncated ? "B.Amber" : "B.TextMute");
                        }
                    }
                    catch (Exception ex) { Status(ex.Message, "B.Amber"); }
                }
                PaintAttachments();
            }

            var attachBtn = Ui.Button("Attach file", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Attach files to the next message", Multiselect = true,
                    Filter = "Images and text|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.txt;*.md;*.json;*.csv;*.log;*.xml;*.yaml;*.yml;*.ini;*.toml;*.cs;*.py;*.js;*.ts;*.html;*.css;*.sql|All files|*.*"
                };
                if (dlg.ShowDialog(this) == true) _ = AddFiles(dlg.FileNames);
            }, "Btn.Ghost", Icons.File);
            attachBtn.ToolTip = $"Attach up to {MaxPendingAttachments} files to the next message. Images (PNG, JPEG, GIF, WebP, BMP, up to {OllamaAttachments.MaxImageBytes / 1048576} MB) go to vision models; text files up to {OllamaAttachments.MaxTextFileBytes / 1024} KB are pasted into the message, cut at {OllamaAttachments.MaxInlineChars:N0} characters. You can also drop files on this card.";

            box.PreviewDragOver += (o, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; } };
            box.AllowDrop = true;
            box.PreviewDrop += (o, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files) { e.Handled = true; _ = AddFiles(files); }
            };

            // ---------------------------------------------------------------- conversation
            var prompt = Input("", double.NaN, "Type a message and press Enter or Send");
            prompt.HorizontalAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetName(prompt, "Chat message");
            var sendBtn = Ui.Button("Send", null, "Btn.Accent", Icons.Bolt);
            var stopChat = Ui.Button("Stop", () => chatCts?.Cancel(), "Btn.Ghost", Icons.Pause);
            stopChat.IsEnabled = false;
            Action paintSaved = null;

            void SyncSession()
            {
                var o = ReadOptions(out _);
                session.Model = CurrentModel() ?? "";
                session.SystemPrompt = o.SystemPrompt; session.Temperature = o.Temperature; session.TopP = o.TopP; session.NumCtx = o.NumCtx; session.KeepAlive = o.KeepAlive;
            }

            bool SaveSession(bool announce)
            {
                if (session.Messages.Count == 0) { if (announce) Status("There is nothing to save yet.", "B.Amber"); return false; }
                try
                {
                    SyncSession();
                    OllamaSessionStore.Save(session);
                    if (announce) Status("Chat saved: " + session.Title, "B.Mint");
                    paintSaved?.Invoke();
                    return true;
                }
                catch (Exception ex) { Status("Could not save the chat: " + ex.Message, "B.Amber"); return false; }
            }

            void NewChat()
            {
                if (chatCts != null && stopChat.IsEnabled) return;
                session = new OllamaSession { Profile = session.Profile };
                turns.Clear(); pending.Clear(); chatOut.Text = ""; Status(""); PaintAttachments(); paintProfiles(); paintSaved?.Invoke();
            }

            async Task Send()
            {
                var text = prompt.Text.Trim();
                if (text.Length == 0 && pending.Count == 0) return;
                var model = CurrentModel();
                if (string.IsNullOrWhiteSpace(model)) { Status("Choose an installed model above first.", "B.Amber"); return; }
                if (await Ollama.VersionAsync() == null) { Status("The Ollama server is not running.", "B.Amber"); return; }
                var opts = ReadOptions(out var parse);
                var got = await GetCaps(model);
                var problems = parse != null ? new List<string> { parse } : opts.Validate(got?.Context ?? 0);
                if (problems.Count > 0) { Status(problems[0], "B.Amber"); return; }
                var chatBlock = got?.Info.ChatBlockReason();
                if (chatBlock != null) { Status(chatBlock, "B.Amber"); return; }
                var images = pending.Where(a => a.Kind == "image").ToList();
                var imgBlock = images.Count > 0 ? got?.Info.ImageBlockReason() : null;
                if (imgBlock != null) { Status(imgBlock + " Remove the image or pick a vision model.", "B.Amber"); return; }

                var sent = pending.ToList();
                var content = OllamaAttachments.BuildContent(text, sent);
                var names = sent.Select(a => a.Name).ToList();
                prompt.Text = ""; pending.Clear(); PaintAttachments();
                turns.Add(new OllamaChatTurn { Role = "user", Content = content, Images = images.Select(a => a.Base64).ToList() });
                session.Messages.Add(new OllamaSessionMessage { Role = "user", Content = content, Attachments = names });
                if (session.Messages.Count == 1) session.Model = model;
                chatOut.Text += (chatOut.Text.Length > 0 ? "\n\n" : "") + "You: " + text + (names.Count > 0 ? "\n[attached: " + string.Join(", ", names) + "]" : "") + "\n" + model + ": ";
                sendBtn.IsEnabled = false; stopChat.IsEnabled = true;
                Status("Waiting for the model…");
                chatCts = new CancellationTokenSource();
                var reply = new StringBuilder();
                try
                {
                    var stats = await Ollama.ChatAsync(model, turns, opts, c => { reply.Append(c); chatOut.Text += c; chatScroll.ScrollToEnd(); }, chatCts.Token);
                    turns.Add(new OllamaChatTurn { Role = "assistant", Content = reply.ToString() });
                    session.Messages.Add(new OllamaSessionMessage { Role = "assistant", Content = reply.ToString() });
                    Status(stats.Length > 0 ? stats : "Done.", "B.Mint");
                    if (Settings.Current.OllamaAutoSaveChats) SaveSession(false);
                }
                catch (OperationCanceledException)
                {
                    if (reply.Length > 0)
                    {
                        turns.Add(new OllamaChatTurn { Role = "assistant", Content = reply.ToString() });
                        session.Messages.Add(new OllamaSessionMessage { Role = "assistant", Content = reply.ToString() });
                    }
                    else { turns.RemoveAt(turns.Count - 1); session.Messages.RemoveAt(session.Messages.Count - 1); }
                    Status("Stopped.");
                }
                catch (Exception ex)
                {
                    turns.RemoveAt(turns.Count - 1); session.Messages.RemoveAt(session.Messages.Count - 1);
                    Status(ex.Message, "B.Amber");
                }
                chatCts = null;
                sendBtn.IsEnabled = true; stopChat.IsEnabled = false;
                await afterReply();
            }
            sendBtn.Click += async (o, e) => await Send();
            prompt.KeyDown += async (o, e) => { if (e.Key == Key.Enter && sendBtn.IsEnabled) { e.Handled = true; await Send(); } };
            sendBtn.ToolTip = "Sends the message and any attached files to the chosen local model.";
            stopChat.ToolTip = "Stops the reply that is streaming.";

            var newChat = Ui.Button("New chat", NewChat, "Btn.Ghost", Icons.Restart);
            newChat.ToolTip = "Starts an empty conversation. The current one is kept only if you saved it.";
            var saveChat = Ui.Button("Save chat", () => SaveSession(true), "Btn.Ghost", Icons.Archive);
            saveChat.ToolTip = "Stores this conversation on this computer so you can open, export or delete it later.";
            var exportChat = Ui.Button("Export as Markdown", () => ExportSession(session, SyncSession, Status), "Btn.Ghost", Icons.Clipboard);
            exportChat.ToolTip = "Writes this conversation to a .md file you choose.";

            box.Children.Add(chatFrame);
            box.Children.Add(attachRow);
            box.Children.Add(prompt);
            var chatRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var b in new UIElement[] { sendBtn, stopChat, attachBtn, newChat, saveChat, exportChat }) chatRow.Children.Add(b);
            box.Children.Add(chatRow);
            box.Children.Add(chatStats);
            box.Children.Add(Switch("Save chats automatically", "Writes the chat to disk after every reply. Default off, so nothing is stored unless you press Save chat.", Settings.Current.OllamaAutoSaveChats, v => Settings.Current.OllamaAutoSaveChats = v));

            // ---------------------------------------------------------------- saved chats
            box.Children.Add(Ui.Mono("SAVED CHATS", Ui.Res("B.TextMute"), 11));
            var savedFilter = new SearchField("ollama:chats", "Search saved chats", compact: true) { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 6) };
            var savedHost = new StackPanel();
            box.Children.Add(savedFilter);
            box.Children.Add(new ScrollViewer { Content = savedHost, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            box.Children.Add(Ui.Mono("Saved chats are plain JSON files in " + PathDisplay.Shown(OllamaSessionStore.Dir) + ". Attached image bytes are not stored, only the file names; text files are part of the saved message.", Ui.Res("B.TextMute"), 11));

            paintSaved = () =>
            {
                savedHost.Children.Clear();
                List<OllamaSession> all;
                try { all = OllamaSessionStore.List(); }
                catch (Exception ex) { savedHost.Children.Add(Ui.Text("Could not read saved chats: " + ex.Message)); savedFilter.SetCount(0, 0, "chats"); return; }
                savedFilter.SampleSource = () => all.Select(s => s.Title);
                var hits = all.Where(s => savedFilter.State.Matches($"{s.Title} {s.Model} {s.Profile} " + string.Join(" ", s.Messages.Select(m => m.Content)))).ToList();
                if (all.Count == 0) savedHost.Children.Add(Ui.Text("No saved chats yet."));
                foreach (var s in hits)
                {
                    var sess = s;
                    var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var info = new StackPanel();
                    info.Children.Add(new TextBlock { Text = s.Title, Foreground = Ui.Res("B.Text"), FontSize = 14, FontFamily = Ui.Font("F.Body"), TextTrimming = TextTrimming.CharacterEllipsis });
                    info.Children.Add(Ui.Mono($"{s.Model} · {s.Messages.Count} messages · {s.Updated:yyyy-MM-dd HH:mm}", Ui.Res("B.TextMute"), 11));
                    g.Children.Add(info);
                    var open = Ui.Button("Open", () =>
                    {
                        if (stopChat.IsEnabled) { Status("Wait for the reply to finish or press Stop first.", "B.Amber"); return; }
                        session = sess; turns.Clear(); pending.Clear(); PaintAttachments();
                        var sb = new StringBuilder();
                        foreach (var m in sess.Messages)
                        {
                            turns.Add(new OllamaChatTurn { Role = m.Role, Content = m.Content });
                            if (sb.Length > 0) sb.Append("\n\n");
                            sb.Append(m.Role == "user" ? "You: " : sess.Model + ": ").Append(m.Content);
                            if (m.Attachments?.Count > 0 && m.Role == "user") sb.Append("\n[attached earlier: ").Append(string.Join(", ", m.Attachments)).Append(']');
                        }
                        chatOut.Text = sb.ToString();
                        systemBox.Text = sess.SystemPrompt ?? ""; temp.Text = Fmt(sess.Temperature); topP.Text = Fmt(sess.TopP); numCtx.Text = sess.NumCtx?.ToString() ?? ""; keep.Text = sess.KeepAlive ?? "";
                        paintProfiles();
                        var have = installed().ToList();
                        if (!string.IsNullOrWhiteSpace(sess.Model) && have.Contains(sess.Model)) { chooseModel(sess.Model); Status("Opened “" + sess.Title + "”. Image bytes from earlier messages are not stored, so they are not re-sent."); }
                        else Status($"Opened “{sess.Title}”. It used {sess.Model}, which is not installed now; the selected model will answer the next message.", "B.Amber");
                        chatScroll.ScrollToEnd();
                    }, "Btn.Ghost", Icons.Folder);
                    open.ToolTip = "Loads this chat, its system prompt and its parameters so you can continue it.";
                    AutomationProperties.SetName(open, "Open chat " + s.Title);
                    var export = Ui.Button("Export", () => ExportSession(sess, null, Status), "Btn.Ghost", Icons.Clipboard);
                    export.ToolTip = "Writes this chat to a Markdown file.";
                    AutomationProperties.SetName(export, "Export chat " + s.Title);
                    var del = Ui.Button("Delete", () =>
                    {
                        var dlg = new ConfirmWindow($"Delete the chat “{sess.Title}”?", "The saved file is removed from this computer. This cannot be undone.", "Delete", danger: true);
                        dlg.Confirmed += _ =>
                        {
                            try { OllamaSessionStore.Delete(sess.Id); Status("Deleted the saved chat.", "B.Mint"); }
                            catch (Exception ex) { Status(ex.Message, "B.Amber"); }
                            paintSaved();
                        };
                        dlg.Show();
                    }, "Btn.Danger", Icons.Trash);
                    del.ToolTip = "Deletes the saved chat file (asks twice). The conversation on screen is not touched.";
                    AutomationProperties.SetName(del, "Delete chat " + s.Title);
                    var btns = new StackPanel { Orientation = Orientation.Horizontal };
                    btns.Children.Add(open); btns.Children.Add(export); btns.Children.Add(del);
                    Grid.SetColumn(btns, 1); g.Children.Add(btns);
                    SearchScope.SetItem(g, "saved chat " + s.Title + " " + s.Model);
                    savedHost.Children.Add(g);
                }
                savedFilter.SetCount(hits.Count, all.Count, "chats");
            };
            savedFilter.Filter += () => paintSaved();
            paintSaved();

            // restore the last profile's wording into the empty fields once, without overwriting anything typed
            var last = profiles.FirstOrDefault(p => p.Name.Equals(Settings.Current.OllamaLastProfile ?? "", StringComparison.OrdinalIgnoreCase));
            if (last != null) ApplyProfile(last);
            _ = RefreshCaps();

            ui.Card = Card(box);
            return ui;
        }

        private static void ExportSession(OllamaSession session, Action sync, Action<string, string> status)
        {
            try
            {
                sync?.Invoke();
                if (session.Messages.Count == 0) { status("There is nothing to export yet.", "B.Amber"); return; }
                var title = string.IsNullOrWhiteSpace(session.Title) ? session.AutoTitle() : session.Title;
                var safe = new string(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
                var dlg = new Microsoft.Win32.SaveFileDialog { Title = "Export chat as Markdown", FileName = (safe.Length > 0 ? safe : "chat") + ".md", Filter = "Markdown (*.md)|*.md" };
                if (dlg.ShowDialog() != true) return;
                File.WriteAllText(dlg.FileName, OllamaSessionStore.ToMarkdown(session), new UTF8Encoding(false));
                status("Exported to " + dlg.FileName, "B.Mint");
            }
            catch (Exception ex) { status("Could not export: " + ex.Message, "B.Amber"); }
        }
    }
}
