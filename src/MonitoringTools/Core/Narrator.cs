using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace MonitoringTools.Core
{
    /// <summary>An installed Windows speech voice.</summary>
    public sealed class VoiceChoice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
    }

    /// <summary>
    /// Reads toast titles aloud with the Windows speech voices. One queue, so speech never overlaps;
    /// it stops and stays quiet in game mode and while snoozed. The narrator is off by default.
    /// </summary>
    public static class Narrator
    {
        private sealed class Item { public string Text; public bool Cantonese; }

        private static readonly object Gate = new object();
        private static readonly Queue<Item> Pending = new Queue<Item>();
        private static bool _running;
        private static int _generation;
        private static MediaPlayer _player;
        private static TaskCompletionSource<bool> _done;
        private static bool _attached;

        /// <summary>Raised when something worth telling the user happened (no Cantonese voice, speech failed). The text is already in the current language.</summary>
        public static event Action<string> Note;

        // ------------------------------------------------------------------ voices

        public static IReadOnlyList<VoiceChoice> Voices(bool cantonese)
        {
            try
            {
                return SpeechSynthesizer.AllVoices
                    .Where(v => cantonese ? IsCantonese(v.Language) : IsEnglish(v.Language))
                    .Select(v => new VoiceChoice { Id = v.Id, Name = v.DisplayName, Language = v.Language })
                    .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warn("Could not list speech voices: " + ex.Message);
                return new List<VoiceChoice>();
            }
        }

        public static bool HasCantoneseVoice => Voices(true).Count > 0;

        private static bool IsCantonese(string lang) =>
            lang != null && (lang.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) || lang.StartsWith("yue", StringComparison.OrdinalIgnoreCase));

        private static bool IsEnglish(string lang) =>
            lang != null && (lang.StartsWith("en-", StringComparison.OrdinalIgnoreCase) || lang.Equals("en", StringComparison.OrdinalIgnoreCase));

        private static VoiceInformation Pick(bool cantonese)
        {
            try
            {
                var id = cantonese ? Settings.Current.CantoneseVoiceId : Settings.Current.EnglishVoiceId;
                var all = SpeechSynthesizer.AllVoices.Where(v => cantonese ? IsCantonese(v.Language) : IsEnglish(v.Language)).ToList();
                if (!string.IsNullOrEmpty(id)) { var chosen = all.FirstOrDefault(v => v.Id == id); if (chosen != null) return chosen; }
                return all.FirstOrDefault();
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ speaking

        /// <summary>
        /// Queues a toast title. English plays in English mode, Cantonese in Cantonese mode, both (English first) in Bilingual.
        /// Does nothing unless the narrator is switched on, and nothing in game mode or while snoozed.
        /// </summary>
        public static void Speak(string en, string yue)
        {
            if (!Settings.Current.NarratorEnabled || Hushed()) return;
            Enqueue(en, yue);
        }

        /// <summary>Speaks a sample in the current language, even when the narrator switch is off.</summary>
        public static void Test()
        {
            Enqueue(Language.Variant("narrator.test", LanguageMode.English, Settings.Current.EnglishHumour),
                    Language.Variant("narrator.test", LanguageMode.Cantonese, Settings.Current.CantoneseHumour));
        }

        public static void Stop()
        {
            TaskCompletionSource<bool> done;
            MediaPlayer p;
            lock (Gate)
            {
                _generation++;
                Pending.Clear();
                done = _done; p = _player;
            }
            try { p?.Pause(); } catch { }
            done?.TrySetResult(false);
        }

        /// <summary>Called once by Language.Initialize: stops speech the moment game mode starts.</summary>
        internal static void Attach()
        {
            if (_attached) return;
            _attached = true;
            var app = System.Windows.Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { if (App.Games != null) App.Games.GamingChanged += gaming => { if (gaming) Stop(); }; }
                catch (Exception ex) { Log.Warn("Narrator could not watch game mode: " + ex.Message); }
            }), DispatcherPriority.ApplicationIdle);
        }

        private static bool Hushed()
        {
            try { return App.ShouldSuppress; } catch { return false; }
        }

        private static void Enqueue(string en, string yue)
        {
            en = Clean(en); yue = Clean(yue);
            var mode = Language.Mode;
            var items = new List<Item>();
            if (mode == LanguageMode.English) { if (en.Length > 0) items.Add(new Item { Text = en }); }
            else if (mode == LanguageMode.Cantonese)
            {
                if (yue.Length > 0) items.Add(new Item { Text = yue, Cantonese = true });
                else if (en.Length > 0) items.Add(new Item { Text = en });
            }
            else
            {
                if (en.Length > 0) items.Add(new Item { Text = en });
                if (yue.Length > 0 && yue != en) items.Add(new Item { Text = yue, Cantonese = true });
            }
            if (items.Count == 0) return;
            lock (Gate)
            {
                if (Pending.Count > 8) return;   // a burst of toasts is not read out one by one for a minute
                foreach (var i in items) Pending.Enqueue(i);
                if (_running) return;
                _running = true;
            }
            _ = Task.Run(Pump);
        }

        private static string Clean(string s) => string.IsNullOrWhiteSpace(s) ? "" : Language.StripEmoji(s.Replace("\r", " ").Replace("\n", ". ")).Trim();

        private static async Task Pump()
        {
            try
            {
                while (true)
                {
                    Item item; int gen;
                    lock (Gate)
                    {
                        if (Pending.Count == 0) { _running = false; return; }
                        item = Pending.Dequeue(); gen = _generation;
                    }
                    if (Hushed()) { Stop(); continue; }
                    try { await SayAsync(item, gen); }
                    catch (Exception ex) { Log.Warn("Narrator failed: " + ex.Message); Note?.Invoke("The voice could not speak: " + ex.Message); }
                }
            }
            finally { lock (Gate) _running = false; }
        }

        private static async Task SayAsync(Item item, int gen)
        {
            var voice = Pick(item.Cantonese);
            if (item.Cantonese && voice == null) { Note?.Invoke("No Cantonese voice is installed, so Cantonese is not spoken."); return; }
            using var synth = new SpeechSynthesizer();
            if (voice != null) synth.Voice = voice;
            synth.Options.SpeakingRate = Math.Clamp(Settings.Current.NarratorRate, 0.5, 2.0);
            using var stream = await synth.SynthesizeTextToStreamAsync(item.Text);
            lock (Gate) { if (gen != _generation) return; }

            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var player = new MediaPlayer { AutoPlay = false };
            player.CommandManager.IsEnabled = false;
            player.MediaEnded += (s, e) => done.TrySetResult(true);
            player.MediaFailed += (s, e) => done.TrySetResult(false);
            player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            lock (Gate) { _player = player; _done = done; }
            try
            {
                player.Play();
                await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(45)));
            }
            finally
            {
                lock (Gate) { if (_player == player) { _player = null; _done = null; } }
                try { player.Dispose(); } catch { }
            }
        }
    }
}
