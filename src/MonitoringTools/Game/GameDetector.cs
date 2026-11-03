using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using MonitoringTools.Core;

namespace MonitoringTools.Game
{
    /// <summary>
    /// Game mode: true while a listed game runs, or (optionally) while any app is fullscreen.
    /// List entries are process names; "proc:Title" also requires the window title to contain Title
    /// (Minecraft Java runs as javaw, which many other apps use too).
    /// </summary>
    public sealed class GameDetector
    {
        public bool IsGaming { get; private set; }
        public string ActiveGame { get; private set; }
        public event Action<bool> GamingChanged;

        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };

        public GameDetector()
        {
            _timer.Tick += (s, e) => Check();
        }

        public void Start()
        {
            Check();
            _timer.Start();
        }

        public void Check()
        {
            string found = null;
            var s = Settings.Current;
            try
            {
                foreach (var entry in s.GameProcesses)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;
                    var parts = entry.Split(new[] { ':' }, 2);
                    var name = parts[0].Trim();
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
                    var procs = Process.GetProcessesByName(name);
                    try
                    {
                        if (procs.Length == 0) continue;
                        if (parts.Length == 1) { found = Friendly(name); break; }
                        var title = parts[1].Trim();
                        if (procs.Any(p => { try { return p.MainWindowTitle.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0; } catch { return false; } }))
                        { found = title; break; }
                    }
                    finally { foreach (var p in procs) p.Dispose(); }
                }

                if (found == null && s.GameModeFullscreen && Native.SHQueryUserNotificationState(out var q) == 0)
                {
                    if (q == Native.QUNS_RUNNING_D3D_FULL_SCREEN) found = "Fullscreen game";
                    else if (q == Native.QUNS_BUSY) found = "Fullscreen app";
                    else if (q == Native.QUNS_PRESENTATION_MODE) found = "Presentation";
                }
            }
            catch (Exception ex) { Log.Warn("Game check failed: " + ex.Message); }

            ActiveGame = found;
            var gaming = found != null;
            if (gaming != IsGaming)
            {
                IsGaming = gaming;
                Log.Info(gaming ? "Game mode on: " + found : "Game mode off");
                GamingChanged?.Invoke(gaming);
            }
        }

        public static string Friendly(string proc)
        {
            var p = proc.ToLowerInvariant();
            if (p.StartsWith("roblox")) return "Roblox";
            if (p.StartsWith("minecraft")) return "Minecraft";
            if (p.StartsWith("league")) return "League of Legends";
            if (p.StartsWith("valorant")) return "Valorant";
            if (p.StartsWith("fortnite")) return "Fortnite";
            if (p == "cs2") return "Counter-Strike 2";
            if (p == "dota2") return "Dota 2";
            if (p == "r5apex") return "Apex Legends";
            if (p == "gta5") return "GTA V";
            if (p == "eldenring") return "Elden Ring";
            return proc;
        }
    }
}
