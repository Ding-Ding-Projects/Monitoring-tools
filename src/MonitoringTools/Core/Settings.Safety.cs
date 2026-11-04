using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MonitoringTools.Core
{
    public sealed partial class Settings
    {
        // Safety: locks, the unlock ladder and the authenticator. Credentials never live here.

        /// <summary>How long a lock stays open after it is unlocked, in minutes. 0 keeps it open until the app closes.</summary>
        public int LockUnlockMinutes { get; set; } = 5;

        /// <summary>Show the next authenticator code under the current one.</summary>
        public bool AuthenticatorShowNext { get; set; } = true;

        /// <summary>Hide locked elements from the page search results. Off by default, so locked items are shown and labelled as locked.</summary>
        public bool LocksHiddenInSearch { get; set; } = false;
    }

    /// <summary>
    /// Small files that hold lock credentials, lock state and authenticator secrets. Each file is protected with the
    /// Windows data-protection API for the current user, so another user account or another computer cannot read it.
    /// This is not the Windows credential vault, and it does not stop malware that runs as the same user.
    /// </summary>
    public static class SecureStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MonitoringTools.Safety.v1");

        private static string Dir
        {
            get { var d = Path.Combine(Settings.DataDir, "safety"); Directory.CreateDirectory(d); return d; }
        }

        public static string PathOf(string name) => Path.Combine(Dir, name);
        public static bool Exists(string name) => File.Exists(PathOf(name));

        public static bool TryRead(string name, out string text, out string error)
        {
            text = null; error = null;
            try
            {
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(PathOf(name)), Entropy, DataProtectionScope.CurrentUser);
                text = Encoding.UTF8.GetString(plain);
                return true;
            }
            catch (Exception ex)
            {
                error = "A protected file could not be opened (" + ex.GetType().Name + "). It may belong to another Windows account.";
                Log.Warn("Secure read failed for " + name + ": " + ex.GetType().Name);
                return false;
            }
        }

        public static bool Write(string name, string text)
        {
            try
            {
                var blob = ProtectedData.Protect(Encoding.UTF8.GetBytes(text), Entropy, DataProtectionScope.CurrentUser);
                var path = PathOf(name);
                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, blob);
                File.Move(tmp, path, true);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Secure write failed for " + name + ": " + ex.GetType().Name);
                return false;
            }
        }
    }

    public sealed class SafetyEvent
    {
        public DateTime At { get; set; }
        public string Action { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    /// <summary>
    /// An append-only local list of what changed in locks, authenticator accounts and the display name, and when.
    /// It records labels and actions only. No secret, PIN, password or code is ever written to it.
    /// </summary>
    public static class SafetyHistory
    {
        public const int Cap = 500;
        private static readonly object Gate = new object();
        private static List<SafetyEvent> _list;
        private static bool _started;
        private static string _lastName;
        public static event Action Changed;

        private static string FilePath => Path.Combine(Settings.DataDir, "safety-history.json");

        /// <summary>Starts watching the display name. Safe to call more than once.</summary>
        public static void Start()
        {
            if (_started) return;
            _started = true;
            _lastName = Appearance.DisplayName;
            Appearance.Changed += () =>
            {
                var now = Appearance.DisplayName;
                if (now == _lastName) return;
                _lastName = now;
                Record("Display name changed", "the name shown in the title bar was edited");
            };
        }

        private static void EnsureLoaded()
        {
            lock (Gate)
            {
                if (_list != null) return;
                try { _list = File.Exists(FilePath) ? JsonSerializer.Deserialize<List<SafetyEvent>>(File.ReadAllText(FilePath)) ?? new List<SafetyEvent>() : new List<SafetyEvent>(); }
                catch (Exception ex) { Log.Warn("Safety history unreadable: " + ex.Message); _list = new List<SafetyEvent>(); }
            }
        }

        public static IReadOnlyList<SafetyEvent> All() { EnsureLoaded(); lock (Gate) return _list.OrderByDescending(e => e.At).ToList(); }

        public static void Record(string action, string detail)
        {
            EnsureLoaded();
            lock (Gate)
            {
                _list.Add(new SafetyEvent { At = DateTime.Now, Action = action, Detail = detail ?? "" });
                if (_list.Count > Cap) _list.RemoveRange(0, _list.Count - Cap);
                try { File.WriteAllText(FilePath, JsonSerializer.Serialize(_list)); }
                catch (Exception ex) { Log.Warn("Safety history not saved: " + ex.Message); }
            }
            Changed?.Invoke();
        }
    }

    public sealed class Ticket
    {
        public string Number { get; set; } = "";
        public string Category { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime FiledAt { get; set; }
        public bool FolderOpened { get; set; }
    }

    /// <summary>
    /// The local recovery desk. It is a fictional desk inside this app: nothing is sent anywhere and nobody reads a ticket.
    /// The only real resolution is to open the application-data folder so the person can delete the lock file themselves.
    /// </summary>
    public static class TicketDesk
    {
        public const string Disclosure = "Nothing is sent anywhere. These tickets exist only on this computer, no network request is made, no data is collected, and nobody is reading them.";
        public static readonly string[] Categories = { "Forgot a lock PIN or password", "Lost my authenticator", "Locked out by too many wrong tries", "Something else is locked" };
        public static readonly string[] Severities = { "Low", "Normal", "High", "Critical (will be treated as Normal)" };
        private static readonly object Gate = new object();
        private static List<Ticket> _list;
        public static event Action Changed;

        private static string FilePath => Path.Combine(Settings.DataDir, "tickets.json");

        private static void EnsureLoaded()
        {
            lock (Gate)
            {
                if (_list != null) return;
                try { _list = File.Exists(FilePath) ? JsonSerializer.Deserialize<List<Ticket>>(File.ReadAllText(FilePath)) ?? new List<Ticket>() : new List<Ticket>(); }
                catch (Exception ex) { Log.Warn("Tickets unreadable: " + ex.Message); _list = new List<Ticket>(); }
            }
        }

        private static void Save()
        {
            try { lock (Gate) File.WriteAllText(FilePath, JsonSerializer.Serialize(_list)); }
            catch (Exception ex) { Log.Warn("Tickets not saved: " + ex.Message); }
            Changed?.Invoke();
        }

        public static IReadOnlyList<Ticket> All() { EnsureLoaded(); lock (Gate) return _list.OrderByDescending(t => t.FiledAt).ToList(); }

        public static Ticket Submit(string category, string severity, string description)
        {
            EnsureLoaded();
            Ticket t;
            lock (Gate)
            {
                int n = _list.Count == 0 ? 1 : _list.Max(x => int.TryParse(x.Number.Replace("LOCAL-", ""), out var v) ? v : 0) + 1;
                t = new Ticket { Number = "LOCAL-" + n.ToString("0000"), Category = category, Severity = severity, Description = description.Trim(), FiledAt = DateTime.Now };
                _list.Add(t);
            }
            SafetyHistory.Record("Support ticket filed", t.Number + " (" + category + ")");
            Save();
            return t;
        }

        public static void Remove(IEnumerable<Ticket> tickets)
        {
            EnsureLoaded();
            lock (Gate) foreach (var t in tickets.ToList()) _list.RemoveAll(x => x.Number == t.Number);
            Save();
        }

        /// <summary>The status advances with time since filing. It is a bit, and the disclosure line says so.</summary>
        public static string Status(Ticket t)
        {
            var s = (DateTime.Now - t.FiledAt).TotalSeconds;
            return s < 5 ? "New" : s < 15 ? "Acknowledged" : s < 30 ? "Under review" : "Resolution ready";
        }

        public static bool ResolutionReady(Ticket t) => (DateTime.Now - t.FiledAt).TotalSeconds >= 30;

        public static string FirstResponse(Ticket t) =>
            "Thank you for contacting the local recovery desk. We have read the manual once. The resolution for every ticket is the same: open the app data folder and delete the file named locks.dat inside its safety folder, or the whole folder to reset everything. This app never deletes it for you.";

        /// <summary>Opens the application-data folder in Windows Explorer and stands back. Returns an error message, or null when Explorer was started.</summary>
        public static string OpenFolder(Ticket t)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + Settings.DataDir + "\"") { UseShellExecute = true });
                if (t != null) { t.FolderOpened = true; Save(); }
                SafetyHistory.Record("Recovery folder opened", t?.Number ?? "from the lock prompt");
                return null;
            }
            catch (Exception ex) { return "Windows Explorer could not be started (" + ex.Message + "). Open this folder yourself: " + Settings.DataDir; }
        }

        public static string ExportMarkdown()
        {
            var sb = new StringBuilder("# Local support tickets\n\n" + Disclosure + "\n\n");
            foreach (var t in All())
                sb.Append($"## {t.Number}: {t.Category}\n\n- Filed: {t.FiledAt:yyyy-MM-dd HH:mm}\n- Severity: {t.Severity}\n- Status: {Status(t)}\n\n{t.Description}\n\n");
            return sb.ToString();
        }
    }
}
