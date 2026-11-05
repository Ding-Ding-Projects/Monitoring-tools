using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows.Media;

namespace MonitoringTools.Core
{
    public enum Cue { Success, Complete, Warning, Restored, Tada, Invalid }

    /// <summary>
    /// One shared, non-blocking playback path for every action cue. Cues use the sounds that ship
    /// with Windows (C:\Windows\Media), so nothing extra is bundled and the palette stays familiar.
    /// They obey the Settings switch and volume, stay quiet during Windows quiet time, presentations
    /// and game mode, never overlap the same cue, and never change system audio settings.
    /// </summary>
    public static class Sounds
    {
        private static readonly Dictionary<Cue, string> Files = new Dictionary<Cue, string>
        {
            [Cue.Success] = "Windows Notify System Generic.wav",
            [Cue.Complete] = "Windows Proximity Notification.wav",
            [Cue.Warning] = "Windows Foreground.wav",
            [Cue.Restored] = "Windows Unlock.wav",
            [Cue.Tada] = "tada.wav",
            [Cue.Invalid] = "Windows Background.wav",     // the Windows "default beep" used for invalid input
        };

        private static readonly Dictionary<Cue, DateTime> Last = new Dictionary<Cue, DateTime>();
        private static MediaPlayer _player;
        private const int QUNS_QUIET_TIME = 5;
        /// <summary>For the self-test: how many times playback was actually started, and the file or system sound it started.</summary>
        internal static int StartedForTest;
        internal static string LastStartedForTest;
        /// <summary>For the self-test: the reason Windows gave when it could not play the last sound (for example no audio device), or null.</summary>
        internal static string FailureForTest;
        /// <summary>For the self-test: answers the quiet-time question so a check does not depend on what the host is doing.</summary>
        internal static bool? QuietOverrideForTest;

        public static void Play(Cue cue)
        {
            try
            {
                var s = Settings.Current;
                if (!Modes.SoundAllowed(cue == Cue.Warning || cue == Cue.Invalid) || s.SoundVolume <= 0) return;
                if (QuietNow()) return;
                if (Last.TryGetValue(cue, out var t) && (DateTime.UtcNow - t).TotalMilliseconds < 450) return;
                Last[cue] = DateTime.UtcNow;

                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", Files[cue]);
                if (!File.Exists(path)) { Fallback(cue); return; }
                if (_player == null) { _player = new MediaPlayer(); _player.MediaFailed += (o, e) => FailureForTest = e.ErrorException?.Message ?? "media failed"; }
                _player.Volume = Math.Clamp(s.SoundVolume, 0, 1);
                _player.Open(new Uri(path));
                _player.Play();
                StartedForTest++; LastStartedForTest = path;
            }
            catch (Exception ex) { Log.Warn("Sound unavailable: " + ex.Message); }  // a playback failure never changes an action result
        }

        private static bool QuietNow()
        {
            if (QuietOverrideForTest.HasValue) return QuietOverrideForTest.Value;
            try
            {
                if (Native.SHQueryUserNotificationState(out var q) == 0 &&
                    (q == QUNS_QUIET_TIME || q == Native.QUNS_PRESENTATION_MODE || q == Native.QUNS_RUNNING_D3D_FULL_SCREEN)) return true;
            }
            catch { }
            return App.ShouldSuppress;
        }

        private static void Fallback(Cue cue)
        {
            try
            {
                if (cue == Cue.Invalid || cue == Cue.Warning) SystemSounds.Beep.Play();
                else SystemSounds.Asterisk.Play();
                StartedForTest++; LastStartedForTest = "system:" + cue;
            }
            catch { }
        }
    }
}
