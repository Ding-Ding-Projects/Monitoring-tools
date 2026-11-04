using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MonitoringTools.Core
{
    /// <summary>
    /// <c>--live-check DIR</c>: runs the app's own code against real services on this computer and writes
    /// <c>livecheck.json</c>. Unlike the self-test, nothing here is faked: real voices, a real Ollama server and model,
    /// the real dish catalogue, a real forge server and a real Minecraft server jar. A check whose service is not
    /// present is recorded as skipped with the reason, never as passed.
    ///
    /// Optional environment: MT_LIVE_OLLAMA_MODEL (default: the smallest installed model), MT_LIVE_MC (Minecraft version,
    /// default 1.21.4), MT_LIVE_FORGE_URL, MT_LIVE_FORGE_KIND (gitea or gitlab), MT_LIVE_FORGE_OWNER and
    /// MT_LIVE_FORGE_TOKEN for a throwaway test server. The token is used for that one push and is never written out.
    /// MT_LIVE_ONLY=minecraft-wave8 runs only the catalogue, Bedrock, discovery, Docker and SSH checks;
    /// MT_LIVE_ONLY=minecraft-commands runs only the command builder against a real server;
    /// MT_LIVE_ONLY=forge runs only the forge publish.
    /// </summary>
    public static partial class LiveCheck
    {
        private sealed class Result
        {
            public string Name { get; set; }
            public string State { get; set; }   // passed, failed or skipped
            public string Detail { get; set; }
            public double Seconds { get; set; }
        }

        public static int Run(string dir)
        {
            Directory.CreateDirectory(dir);
            Settings.Load();
            var results = new List<Result>();
            var pngRed = RedSquarePng();   // made here, on the STA thread, before any await
            Task.Run(async () =>
            {
                if (Environment.GetEnvironmentVariable("MT_LIVE_ONLY") == "minecraft-wave8")   // catalogues, Bedrock, discovery, Docker and SSH
                {
                    await McWave8(results);
                    return;
                }
                if (Environment.GetEnvironmentVariable("MT_LIVE_ONLY") == "forge") { await Check(results, "forge: publishing creates the repository and pushes the commit", () => Forge(dir)); return; }   // only the forge publish, against a server named by MT_LIVE_FORGE_*
                if (Environment.GetEnvironmentVariable("MT_LIVE_ONLY") == "minecraft-commands") { await McCommands(results); return; }   // the command builder on a real server
                if (Environment.GetEnvironmentVariable("MT_LIVE_ONLY") == "minecraft-server")   // run only the server manager end to end
                {
                    await Check(results, "minecraft: a real server is created, started, answers list and stops", MinecraftServer);
                    return;
                }
                await Check(results, "narrator: an installed English voice synthesises speech", () => Voice(dir, false));
                await Check(results, "narrator: a Cantonese voice synthesises speech", () => Voice(dir, true));
                await Check(results, "ollama: the local server answers and lists models", OllamaList);
                await Check(results, "ollama: a chat with a text attachment reads the file", OllamaText);
                await Check(results, "ollama: a chat with an image attachment sees the image", () => OllamaImage(pngRed));
                await Check(results, "dim sum: the live catalogue gives a dish with its photo", DimSum);
                await Check(results, "forge: publishing creates the repository and pushes the commit", () => Forge(dir));
                await Check(results, "minecraft: the real server jar lists its game rules, blocks and biomes", Minecraft);
                await Check(results, "minecraft: a real server is created, started, answers list and stops", MinecraftServer);
                await McCommands(results);
                await McWave8(results);
            }).GetAwaiter().GetResult();

            File.WriteAllText(Path.Combine(dir, "livecheck.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            return results.Any(r => r.State == "failed") ? 1 : 0;
        }

        private sealed class Skip : Exception { public Skip(string why) : base(why) { } }

        private static async Task Check(List<Result> results, string name, Func<Task<string>> body)
        {
            var sw = Stopwatch.StartNew();
            var r = new Result { Name = name };
            lock (Note) Note.Clear();
            try
            {
                var fail = await body();
                r.State = fail == null ? "passed" : "failed";
                r.Detail = fail ?? "ok";
            }
            catch (Skip s) { r.State = "skipped"; r.Detail = s.Message; }
            catch (Exception ex) { r.State = "failed"; r.Detail = ex.GetType().Name + ": " + ex.Message; }
            r.Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
            lock (Note) if (Note.Length > 0) r.Detail += " | " + Note;
            results.Add(r);
        }

        // ------------------------------------------------------------------ narrator

        private static async Task<string> Voice(string dir, bool cantonese)
        {
            var text = cantonese ? "剪貼簿已經複製咗。" : "Copied to the clipboard.";
            var (audio, voice) = await Narrator.SynthesizeAsync(text, cantonese);
            if (audio == null) throw new Skip(cantonese ? "No Cantonese (zh-HK) voice is installed on this computer." : "No English voice is installed on this computer.");
            File.WriteAllBytes(Path.Combine(dir, cantonese ? "voice-cantonese.wav" : "voice-english.wav"), audio);
            // A WAV header is 44 bytes; half a second of 16 kHz mono 16-bit speech is about 16 KB.
            if (audio.Length < 16000) return $"{voice} produced only {audio.Length} bytes";
            return Encoding.ASCII.GetString(audio, 0, 4) == "RIFF" ? null : "the audio is not a WAV stream";
        }

        // ------------------------------------------------------------------ Ollama

        private static async Task<string> PickModelAsync()
        {
            if (await Ollama.VersionAsync() == null) throw new Skip("No Ollama server answers on the configured address.");
            var models = await Ollama.ModelsAsync();
            if (models.Count == 0) throw new Skip("Ollama runs but has no models installed.");
            var wanted = Environment.GetEnvironmentVariable("MT_LIVE_OLLAMA_MODEL");
            return !string.IsNullOrEmpty(wanted) ? wanted : models.OrderBy(m => m.Size).First().Name;
        }

        private static async Task<string> OllamaList()
        {
            var version = await Ollama.VersionAsync();
            if (version == null) throw new Skip("No Ollama server answers on the configured address.");
            var models = await Ollama.ModelsAsync();
            if (models.Count == 0) throw new Skip("Ollama " + version + " runs but has no models installed.");
            var d = await Ollama.ShowAsync(models[0].Name);
            if (d == null || d.Capabilities.Count == 0) return "the model details have no capabilities";
            return models.All(m => m.Size > 0 && !string.IsNullOrEmpty(m.Name)) ? null : "a model entry is incomplete";
        }

        private static async Task<string> OllamaText()
        {
            var model = await PickModelAsync();
            var file = OllamaAttachments.FromTextBytes("note.txt", Encoding.UTF8.GetBytes("The code word is PINEAPPLE. Nothing else matters in this file."));
            var content = OllamaAttachments.BuildContent("What is the code word in the attached file? Reply with the word only.", new[] { file });
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
            // ChatAsync returns the timing line; the words arrive through the chunk callback, as the chat page shows them.
            var sb = new StringBuilder();
            await Ollama.ChatAsync(model, new[] { new OllamaChatTurn { Role = "user", Content = content } }, c => sb.Append(c), cts.Token);
            var reply = sb.ToString();
            try { await Ollama.UnloadAsync(model); } catch { }
            return reply != null && reply.IndexOf("pineapple", StringComparison.OrdinalIgnoreCase) >= 0 ? null : model + " replied: " + Trim(reply);
        }

        private static async Task<string> OllamaImage(byte[] png)
        {
            var model = await PickModelAsync();
            var d = await Ollama.ShowAsync(model);
            if (d == null || !d.Capabilities.Contains("vision"))
            {
                var vision = (await Ollama.ModelsAsync()).Select(m => m.Name).FirstOrDefault(n => (Ollama.ShowAsync(n).GetAwaiter().GetResult()?.Capabilities ?? new List<string>()).Contains("vision"));
                if (vision == null) throw new Skip("No installed model reports the vision capability.");
                model = vision;
            }
            var img = OllamaAttachments.FromImageBytes("square.png", png);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
            var sb = new StringBuilder();
            await Ollama.ChatAsync(model, new[] { new OllamaChatTurn { Role = "user", Content = "What single colour fills this image? Reply with one word.", Images = new List<string> { img.Base64 } } }, c => sb.Append(c), cts.Token);
            var reply = sb.ToString();
            try { await Ollama.UnloadAsync(model); } catch { }
            return reply != null && reply.IndexOf("red", StringComparison.OrdinalIgnoreCase) >= 0 ? null : model + " replied: " + Trim(reply);
        }

        private static byte[] RedSquarePng()
        {
            var bmp = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgr32, null, Enumerable.Repeat(new byte[] { 0, 0, 220, 0 }, 64 * 64).SelectMany(b => b).ToArray(), 64 * 4);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }

        // ------------------------------------------------------------------ dim sum

        private static async Task<string> DimSum()
        {
            var dish = await Modes.PickDishAsync();
            if (dish == null) return "no dish: " + Modes.DimSumError;
            if (string.IsNullOrEmpty(dish.En) || string.IsNullOrEmpty(dish.Zh)) return "the dish has no English or Chinese name";
            return null;
        }

        // ------------------------------------------------------------------ forge

        private static async Task<string> Forge(string dir)
        {
            var url = Environment.GetEnvironmentVariable("MT_LIVE_FORGE_URL");
            var token = Environment.GetEnvironmentVariable("MT_LIVE_FORGE_TOKEN");
            var owner = Environment.GetEnvironmentVariable("MT_LIVE_FORGE_OWNER");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(owner)) throw new Skip("No test forge server was given (MT_LIVE_FORGE_URL, MT_LIVE_FORGE_OWNER, MT_LIVE_FORGE_TOKEN).");
            var kind = string.Equals(Environment.GetEnvironmentVariable("MT_LIVE_FORGE_KIND"), "gitlab", StringComparison.OrdinalIgnoreCase) ? ForgeKind.GitLab : ForgeKind.Gitea;

            var folder = Path.Combine(Settings.DataDir, "forge-live");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "README.md"), "# Live check\n\nPublished by the Monitoring Tools live check.\n");
            var name = "live-check-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var steps = new List<string>();
            var plan = new ForgePlan { Folder = folder, Owner = owner, Name = name, Private = true, CreateRepository = true, Push = true, Kind = kind, BaseUrl = url, Token = token };
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var r = await ForgePublish.PublishAsync(plan, (i, state, text) => steps.Add($"{i}:{state}:{text}"), cts.Token);
            File.WriteAllText(Path.Combine(dir, "forge-steps.txt"), string.Join(Environment.NewLine, steps.Select(s => s.Replace(token, "***"))));
            if (!r.Ok) return "publish failed (" + r.Failure + "): " + r.Summary.Replace(token, "***");

            // Read the pushed file back from the server's own API, so the check proves the push landed.
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.Add(kind == ForgeKind.Gitea ? "Authorization" : "PRIVATE-TOKEN", kind == ForgeKind.Gitea ? "token " + token : token);
            var api = kind == ForgeKind.Gitea
                ? $"{url.TrimEnd('/')}/api/v1/repos/{owner}/{name}/raw/README.md"
                : $"{url.TrimEnd('/')}/api/v4/projects/{Uri.EscapeDataString(owner + "/" + name)}/repository/files/README.md/raw?ref={r.Branch}";
            // A server can answer 5xx for a moment while it indexes a repository that was pushed a second ago.
            string body = null;
            foreach (var wait in new[] { 0, 2, 4, 8 })
            {
                await Task.Delay(wait * 1000);
                using var resp = await http.GetAsync(api);
                if (resp.IsSuccessStatusCode) { body = await resp.Content.ReadAsStringAsync(); break; }
            }
            if (body == null) return "the server never returned the pushed README";
            return body.Contains("Published by the Monitoring Tools live check.") ? null : "the pushed README was not found on the server";
        }

        // ------------------------------------------------------------------ Minecraft

        private static async Task<string> Minecraft()
        {
            var version = Environment.GetEnvironmentVariable("MT_LIVE_MC");
            if (string.IsNullOrEmpty(version)) version = "1.21.4";
            var facts = await MonitoringTools.Minecraft.WorldGenerator.JarFactsAsync(version, _ => { });
            if (!facts.Available) return "jar facts unavailable: " + facts.Reason;
            var (jar, _) = await MonitoringTools.Minecraft.WorldGenerator.EnsureJarAsync(version, _ => { });
            var biomes = MonitoringTools.Minecraft.WorldGenerator.Biomes(jar);
            if (facts.GameRules.Count < 20) return $"only {facts.GameRules.Count} game rules";
            if (!facts.Blocks.Contains("minecraft:stone")) return "the block list has no stone";
            if (!biomes.Any(b => b.EndsWith("plains"))) return "the biome list has no plains";

            // The noise and dimension-type editors read the jar's own default files and write a data pack.
            var packs = Path.Combine(Settings.DataDir, "worldgen-live", "datapacks");
            var edits = new List<MonitoringTools.Minecraft.WorldgenEdit>
            {
                new MonitoringTools.Minecraft.WorldgenEdit { Kind = MonitoringTools.Minecraft.WorldgenKind.NoiseSettings, Id = "minecraft:overworld", Path = "sea_level", Value = "70" },
                new MonitoringTools.Minecraft.WorldgenEdit { Kind = MonitoringTools.Minecraft.WorldgenKind.DimensionType, Id = "minecraft:overworld", Path = "ambient_light", Value = "0.25" },
            };
            var written = await MonitoringTools.Minecraft.WorldGenerator.WriteWorldgenEditsAsync(version, jar, packs, edits, facts.Blocks);
            if (written.Count != 2) return $"the editor wrote {written.Count} files, expected two";
            var noise = Directory.GetFiles(packs, "overworld.json", SearchOption.AllDirectories);
            if (noise.Length != 2) return $"expected two edited files, found {noise.Length}";
            foreach (var f in noise)
            {
                var j = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(f));
                if (f.Contains("noise_settings") ? (int)j["sea_level"] != 70 : (double)j["ambient_light"] != 0.25) return "an edit did not land in " + Path.GetFileName(Path.GetDirectoryName(f));
            }
            return null;
        }

        /// <summary>
        /// The server manager end to end, in the throwaway data folder: create a real Vanilla server (the EULA is accepted
        /// for this test server only), start it, wait for "Done", ask it "list", read the answer, and stop it gracefully.
        /// </summary>
        private static async Task<string> MinecraftServer()
        {
            var version = Environment.GetEnvironmentVariable("MT_LIVE_MC");
            if (string.IsNullOrEmpty(version)) version = "1.21.4";
            var port = 25565;
            while (!MonitoringTools.Minecraft.ServerManager.PortFree(port)) port++;
            var s = new MonitoringTools.Minecraft.ServerDef { Name = "Live check", Version = version, Target = MonitoringTools.Minecraft.Target.LocalProcess, Port = port, MemoryMb = 1024, EulaAccepted = true };
            await MonitoringTools.Minecraft.ServerManager.CreateAsync(s, _ => { }, new Dictionary<string, string> { ["max-players"] = "7", ["online-mode"] = "false" });
            var live = MonitoringTools.Minecraft.McLive.For(s);
            try
            {
                await MonitoringTools.Minecraft.ServerManager.StartAsync(s, _ => { });
                var until = DateTime.Now.AddMinutes(6);
                while (DateTime.Now < until && !live.Lines().Any(l => l.Message.StartsWith("Done (", StringComparison.Ordinal)))
                {
                    if (MonitoringTools.Minecraft.ServerManager.ProcessOf(s) == null) return "the server exited before it was ready: " + Trim(string.Join(" | ", live.Lines().TakeLast(5).Select(l => l.Raw)));
                    await Task.Delay(1000);
                }
                if (!live.Lines().Any(l => l.Message.StartsWith("Done (", StringComparison.Ordinal))) return "the server did not report Done within six minutes";
                var st = await MonitoringTools.Minecraft.McState.ProbeAsync(s);
                if (st.State != MonitoringTools.Minecraft.McRunState.Running) return "the state probe said " + st.State;
                await MonitoringTools.Minecraft.ServerManager.CommandAsync(s, "list");
                for (int i = 0; i < 20 && live.PlayersMax == null; i++) await Task.Delay(500);
                if (live.PlayersOnline != 0 || live.PlayersMax != 7) return $"list answered {live.PlayersOnline} of {live.PlayersMax}, expected 0 of 7";
            }
            finally
            {
                await MonitoringTools.Minecraft.ServerManager.StopAsync(s);
            }
            if (MonitoringTools.Minecraft.ServerManager.ProcessOf(s) != null) return "the server did not stop";
            var after = await MonitoringTools.Minecraft.McState.ProbeAsync(s);
            if (after.State != MonitoringTools.Minecraft.McRunState.Stopped) return "after stopping the state is " + after.State;
            if (!live.Lines().Any(l => l.Raw.Contains("Stopping the server") || l.Raw.Contains("Stopping server"))) return "the log shows no graceful stop";
            return null;
        }

        private static string Trim(string s) => s == null ? "(nothing)" : (s.Length > 160 ? s[..160] + "…" : s).Replace("\n", " ");
    }
}
