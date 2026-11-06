using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using MonitoringTools.Clip;
using MonitoringTools.Clip.Smart;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    /// <summary>Turns what the smart detectors and extensions offer into toast buttons, and runs them.</summary>
    public static class SmartActions
    {
        public static void Add(ClipItem item, List<ToastAction> list)
        {
            if (item.Smart == null || item.Smart.Count == 0) return;
            bool first = true;
            foreach (var fact in item.Smart)
            {
                foreach (var a in fact.Actions)
                {
                    var act = a;
                    list.Add(new ToastAction
                    {
                        Label = act.Label, Icon = IconFor(act.Do), Primary = first && act.Primary, Tip = act.Tip,
                        Done = act.Do == SmartDo.Copy ? "Copied" : act.Do == SmartDo.Open ? "Opened" : "Done",
                        HandsOff = act.Do == SmartDo.Run && (act.Value.StartsWith("conversions:") || act.Value.StartsWith("table") || act.Value.StartsWith("regex:")),
                        Run = () => Run(act, fact, item),
                    });
                }
                first = false;
            }
            var best = item.Smart[0];
            if (best.Values.Count > 2)
                list.Add(new ToastAction { Label = "Copy all as a list", Icon = Icons.Clipboard, Done = "Copied", Run = () => ClipboardWatcher.SetText(best.AllValuesText()) });
        }

        private static string IconFor(SmartDo d) => d switch { SmartDo.Copy => Icons.Clipboard, SmartDo.Open => Icons.Link, SmartDo.Code => Icons.Code, _ => null };

        public static void Run(SmartAction a, SmartFact fact, ClipItem item)
        {
            switch (a.Do)
            {
                case SmartDo.Copy: ClipboardWatcher.SetText(a.Value); break;
                case SmartDo.Open: Open(a.Value); break;
                case SmartDo.Code: OpenCode(a.Value); break;
                case SmartDo.Run: RunNamed(a.Value, fact, item); break;
            }
        }

        /// <summary>Opens web links, phone and text links. Nothing else is started from a copied value.</summary>
        public static void Open(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return;
            if (u.Scheme != "http" && u.Scheme != "https" && u.Scheme != "tel" && u.Scheme != "sms" && u.Scheme != "mailto") return;
            try { Process.Start(new ProcessStartInfo(u.Scheme == "tel" || u.Scheme == "sms" ? url : u.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) { Notice("Nothing opened it", ex.Message); }
        }

        private static void OpenCode(string fileLine)
        {
            int cut = fileLine.LastIndexOf(':');
            var path = cut > 1 ? fileLine.Substring(0, cut) : fileLine;
            int.TryParse(cut > 1 ? fileLine.Substring(cut + 1) : "1", out var line);
            if (!File.Exists(path)) { Notice("That file is not on this PC", path + " · open the project folder, then try again."); return; }
            if (!ExternalEditor.OpenAtLine(path, Math.Max(1, line), 1, out var message)) Notice("Could not open the editor", message);
        }

        private static void RunNamed(string v, SmartFact fact, ClipItem item)
        {
            if (v.StartsWith("conversions:")) { Dialogs.ConversionsDialog.Open(v.Substring("conversions:".Length)); return; }
            if (v.StartsWith("ext:")) { _ = RunExtension(v, fact); return; }
            if (v.StartsWith("ping:")) { Console("ping -n 6 " + Safe(v.Substring(5))); return; }
            if (v.StartsWith("tracert:")) { Console("tracert -d " + Safe(v.Substring(8))); return; }
            if (v.StartsWith("clone:")) { Clone(fact); return; }
            if (v.StartsWith("regex:")) { ExplainRegex(v.Substring(6)); return; }
            switch (v)
            {
                case "new-guid": ClipboardWatcher.SetText(Guid.NewGuid().ToString()); break;
                case "clear-clipboard": ClearClipboard(); break;
                case "hash-check": CheckHash(fact); break;
                case "translate": _ = Translate(item?.Text ?? ""); break;
                case "table":
                    var path = Path.Combine(Path.GetTempPath(), "Clipboard table " + DateTime.Now.ToString("HHmmss") + (item.Text.Contains('\t') ? ".tsv" : ".csv"));
                    File.WriteAllText(path, item.Text, new UTF8Encoding(false));
                    Dialogs.DataViewerDialog.Open(path);
                    break;
            }
        }

        private static string Safe(string host) => new string(host.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == ':' || c == '-').ToArray());

        private static void Console(string command)
        {
            try { Process.Start(new ProcessStartInfo("cmd.exe", "/k " + command) { UseShellExecute = true }); }
            catch (Exception ex) { Notice("Could not open a console", ex.Message); }
        }

        private static void Clone(SmartFact f)
        {
            string host = f.Get("host"), owner = f.Get("owner"), repo = f.Get("repo");
            if (host == "github.com") { ActionProvider.CloneWithProgress(owner, repo, RepoActions.CloneFolder); return; }
            Console("cd /d \"" + RepoActions.CloneFolder + "\" && git clone git@" + host + ":" + owner + "/" + repo + ".git");
        }

        public static void ClearClipboard()
        {
            ClipboardWatcher.IgnoreNextChange();
            try { Clipboard.Clear(); } catch { }
        }

        private static void ExplainRegex(string pattern)
        {
            string outline;
            try { outline = RegexTree.Outline(RegexTree.Parse(pattern), true); } catch (Exception ex) { outline = ex.Message; }
            App.Toasts.ShowNotice("What this pattern means", outline, App.Toasts.Mint, 20, new List<ToastAction>
            {
                new ToastAction { Label = "Copy the explanation", Icon = Icons.Clipboard, Primary = true, Done = "Copied", Run = () => ClipboardWatcher.SetText(pattern + Environment.NewLine + outline) },
                new ToastAction { Label = "Open the regex builder", Icon = Icons.Code, Run = () => App.Current.OpenControlCentre("tools") },
            }, Icons.Code);
        }

        private static void CheckHash(SmartFact fact)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose the file to check against the copied " + fact.Get("algorithm") };
            try { dlg.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"); } catch { }
            if (dlg.ShowDialog() != true) return;
            Dialogs.HashVerifyDialog.Open(dlg.FileName);
        }

        private static async Task RunExtension(string value, SmartFact fact)
        {
            var host = App.Extensions;
            if (host == null) return;
            var res = await host.RunAsync(value, fact.Get("text") ?? "");
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!res.Ok) { Notice(fact.Summary + " did not finish", res.Error); return; }
                if (res.Copy != null) ClipboardWatcher.SetText(res.Copy);
                if (res.Open != null) Open(res.Open);
                if (res.Toast != null || res.Values.Count > 0)
                {
                    var acts = res.Values.Take(6).Select(v => new ToastAction { Label = v.Label + ": " + (v.Text.Length > 40 ? v.Text.Substring(0, 40) + "…" : v.Text), Chip = true, Done = "Copied", Run = () => ClipboardWatcher.SetText(v.Text) }).ToList();
                    App.Toasts.ShowNotice(fact.Summary, res.Toast ?? "Tap a value to copy it.", App.Toasts.Violet, 8, acts, Icons.Bolt);
                }
            });
        }

        private static async Task Translate(string text)
        {
            string answer = null, error = null;
            try
            {
                var models = await Ollama.ModelsAsync();
                var model = models?.FirstOrDefault()?.Name;
                if (model == null) error = "No local AI model is installed. Set one up on the Ollama page, or use Translate on the web.";
                else
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                    var body = JsonSerializer.Serialize(new { model, stream = false, prompt = "Translate this into English. Reply with the translation only.\n\n" + text });
                    var r = await http.PostAsync(Ollama.BaseUrl + "/api/generate", new StringContent(body, Encoding.UTF8, "application/json"));
                    using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                    answer = doc.RootElement.TryGetProperty("response", out var resp) ? resp.GetString()?.Trim() : null;
                }
            }
            catch (Exception ex) { error = "The local AI is not running. " + ex.Message; }
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (answer == null) { Notice("Not translated", error ?? "No answer came back."); return; }
                App.Toasts.ShowNotice("Translation", answer, App.Toasts.Mint, 15, new List<ToastAction>
                { new ToastAction { Label = "Copy", Icon = Icons.Clipboard, Primary = true, Done = "Copied", Run = () => ClipboardWatcher.SetText(answer) } }, Icons.Text);
            });
        }

        private static void Notice(string title, string body) => App.Toasts.ShowNotice(title, body, App.Toasts.Amber, 6, icon: Icons.Bolt);
    }
}
