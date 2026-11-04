using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace MonitoringTools.Core
{
    /// <summary>
    /// School mode, the ADHD modes, scheduled settings and the dim sum surprise. Call
    /// <see cref="Initialize"/> once at startup, after <see cref="Settings.Load"/> and once the toast
    /// manager exists. Consult the flags and <see cref="Blocks"/> instead of reading the settings directly.
    /// </summary>
    public static partial class Modes
    {
        public const string DefaultSchoolName = "School mode";

        /// <summary>Raised on the UI thread whenever any effective mode or overlay value changed.</summary>
        public static event Action Changed;
        public static void NotifyChanged() => Changed?.Invoke();

        private static DispatcherTimer _reminder;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            var s = Settings.Current;
            if (s.ScheduleRules == null) s.ScheduleRules = new System.Collections.Generic.List<ScheduleRule>();
            if (s.TimeReminderMinutes < 5) s.TimeReminderMinutes = 30;
            LoadSchool();
            StartScheduler();
            if (s.ExternalFollowing && !string.IsNullOrWhiteSpace(s.ExternalSettingsPath)) StartFollowing(s.ExternalSettingsPath, false);
            Settings.Changed += RestartReminder;
            RestartReminder();
            ScheduleDimSumDraw();
            UI.FocusSpotlight.Install();
        }

        // ------------------------------------------------------------ ADHD flags

        /// <summary>Non-urgent toasts are held back into history so one thing stays in front.</summary>
        public static bool Focus => Settings.Current.AdhdFocus;
        /// <summary>No non-essential motion or sound; also true when Windows asks for less animation.</summary>
        public static bool LowStimulation => Settings.Current.AdhdLowStimulation || !SystemParameters.ClientAreaAnimation;
        /// <summary>Show elapsed time on running tasks and send the gentle time reminder.</summary>
        public static bool TimeAwareness => Settings.Current.AdhdTimeAwareness;
        /// <summary>At most one toast on screen; the rest wait.</summary>
        public static bool OneThing => Settings.Current.AdhdOneThing;
        /// <summary>Reopen the page you were on, and name the next step after a finished task.</summary>
        public static bool Momentum => Settings.Current.AdhdMomentum;
        /// <summary>Reduce motion if the user chose Low stimulation or Windows reduces animation.</summary>
        public static bool ReduceMotion => LowStimulation;

        /// <summary>True when a toast that is not urgent should go quietly to history instead of showing.</summary>
        public static bool ShouldHoldToast(bool urgent) => Focus && !urgent;

        /// <summary>True when a cue may play. Essential cues (a failure, a lost connection) still play under Low stimulation.</summary>
        public static bool SoundAllowed(bool essential)
            => SoundsOn && (essential || !Settings.Current.AdhdLowStimulation);

        private static void RestartReminder()
        {
            var s = Settings.Current;
            if (!s.AdhdTimeAwareness)
            {
                if (_reminder != null) { _reminder.Stop(); _reminder = null; }
                return;
            }
            var every = TimeSpan.FromMinutes(Math.Max(5, s.TimeReminderMinutes));
            if (_reminder != null && _reminder.Interval == every) return;
            _reminder?.Stop();
            var started = DateTime.Now;
            _reminder = new DispatcherTimer { Interval = every };
            _reminder.Tick += (a, b) =>
            {
                if (!Settings.Current.AdhdTimeAwareness || App.ShouldSuppress) return;
                var mins = (int)Math.Round((DateTime.Now - started).TotalMinutes);
                App.Toasts?.ShowNotice("Time check", $"{mins} minutes since this reminder started. It repeats every {Settings.Current.TimeReminderMinutes} minutes.",
                    App.Toasts.Violet, 6);
            };
            _reminder.Start();
        }

        // ------------------------------------------------------------ School mode (shared record)

        private sealed class SchoolRecord
        {
            public int Version { get; set; } = 1;
            public bool On { get; set; }
            public string Name { get; set; } = DefaultSchoolName;
            public string Salt { get; set; } = "";
            public string Hash { get; set; } = "";
            public int Iterations { get; set; } = Pbkdf2Iterations;
            // Optional password, kept apart from the PIN so apps that only know PINs keep reading the record.
            public string PasswordSalt { get; set; } = "";
            public string PasswordHash { get; set; } = "";
            public int PasswordIterations { get; set; } = Pbkdf2Iterations;
        }

        private const int Pbkdf2Iterations = 200_000;
        private static SchoolRecord _school = new SchoolRecord();
        private static FileSystemWatcher _schoolWatch;
        private static DispatcherTimer _schoolDebounce;
        private static readonly JsonSerializerOptions Pretty = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>The shared record every Ding-Ding app reads. Deleting it resets School mode.</summary>
        public static string SchoolRecordPath => Path.Combine(
            Settings.DataDirOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DingDingShared", "school-mode.json");   // a throwaway-data run (self-test, captures) never touches the real shared record

        /// <summary>Null when the record is readable (or absent); otherwise why it could not be read.</summary>
        public static string SchoolRecordError { get; private set; }

        public static bool SchoolActive => (_school.On || _testSchool) && SchoolRecordError == null;

        // Lets the self-test switch the mode on in memory without touching the shared record on disk.
        private static bool _testSchool;
        internal static void ForceSchoolForTest(bool on) { _testSchool = on; NotifyChanged(); }
        public static string SchoolName => string.IsNullOrWhiteSpace(_school.Name) ? DefaultSchoolName : _school.Name;
        public static bool PinIsSet => !string.IsNullOrEmpty(_school.Hash);
        public static bool PasswordIsSet => !string.IsNullOrEmpty(_school.PasswordHash);
        /// <summary>True once at least one way to turn the mode off exists.</summary>
        public static bool CredentialIsSet => PinIsSet || PasswordIsSet;
        /// <summary>The word used in messages: PIN, password, or both.</summary>
        public static string CredentialWord => PinIsSet && PasswordIsSet ? "PIN or password" : PasswordIsSet ? "password" : "PIN";

        /// <summary>Features School mode removes. See <see cref="ModeFeature"/> for the names.</summary>
        public static bool Blocks(string feature)
        {
            if (!SchoolActive) return false;
            switch (feature)
            {
                case ModeFeature.Push:
                case ModeFeature.Sounds:
                case ModeFeature.Cantonese:
                case ModeFeature.Bilingual:
                case ModeFeature.Funny:
                case ModeFeature.PersonalVocabulary:
                case ModeFeature.DimSum:
                case ModeFeature.GameModeOff:
                    return true;
                default: return false;
            }
        }

        private static void LoadSchool()
        {
            ReadSchool();
            try
            {
                var dir = Path.GetDirectoryName(SchoolRecordPath);
                Directory.CreateDirectory(dir);
                _schoolWatch = new FileSystemWatcher(dir, Path.GetFileName(SchoolRecordPath)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
                FileSystemEventHandler h = (s, e) => Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _schoolDebounce?.Stop();
                    _schoolDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    _schoolDebounce.Tick += (a, b) => { _schoolDebounce.Stop(); ReadSchool(); NotifyChanged(); };
                    _schoolDebounce.Start();
                }));
                _schoolWatch.Changed += h; _schoolWatch.Created += h; _schoolWatch.Deleted += h;
                _schoolWatch.Renamed += (s, e) => h(s, null);
                _schoolWatch.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Log.Warn("School record not watchable: " + ex.Message);
                SchoolWatchError = "The shared record cannot be watched, so a change made by another app appears only after a restart.";
            }
        }

        /// <summary>Set when live watching of the shared record is unavailable.</summary>
        public static string SchoolWatchError { get; private set; }

        private static void ReadSchool()
        {
            try
            {
                if (!File.Exists(SchoolRecordPath)) { _school = new SchoolRecord(); SchoolRecordError = null; return; }
                var info = new FileInfo(SchoolRecordPath);
                if (info.Length > 16 * 1024) throw new InvalidDataException("the record is larger than 16 KB");
                var rec = JsonSerializer.Deserialize<SchoolRecord>(File.ReadAllText(SchoolRecordPath));
                if (rec == null || rec.Version != 1) throw new InvalidDataException("unknown record version");
                if (rec.Name != null && rec.Name.Length > 60) rec.Name = rec.Name.Substring(0, 60);
                _school = rec; SchoolRecordError = null;
            }
            catch (Exception ex)
            {
                // Unreadable: behave as off, and say so on the control rather than pretending it is fine.
                _school = new SchoolRecord();
                SchoolRecordError = "The shared record could not be read (" + ex.Message + "), so the mode is treated as off until it is fixed or deleted.";
                Log.Warn(SchoolRecordError);
            }
        }

        private static void WriteSchool()
        {
            var dir = Path.GetDirectoryName(SchoolRecordPath);
            Directory.CreateDirectory(dir);
            var tmp = SchoolRecordPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_school, Pretty));
            File.Move(tmp, SchoolRecordPath, true);
            SchoolRecordError = null;
        }

        private static byte[] Derive(string pin, byte[] salt, int iterations)
            => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, iterations, HashAlgorithmName.SHA256, 32);

        public static bool PinLooksValid(string pin)
        {
            if (pin == null || pin.Length < 4 || pin.Length > 12) return false;
            foreach (var c in pin) if (c < '0' || c > '9') return false;
            return true;
        }

        public const int PasswordMinLength = 8, PasswordMaxLength = 128;
        public static bool PasswordLooksValid(string password)
            => password != null && password.Length >= PasswordMinLength && password.Length <= PasswordMaxLength;

        /// <summary>Salted PBKDF2-SHA256 hash of a PIN or password, as base64 text.</summary>
        internal static (string Salt, string Hash, int Iterations) MakeCredential(string secret)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            return (Convert.ToBase64String(salt), Convert.ToBase64String(Derive(secret, salt, Pbkdf2Iterations)), Pbkdf2Iterations);
        }

        internal static bool MatchesCredential(string secret, string saltB64, string hashB64, int iterations)
        {
            try
            {
                if (string.IsNullOrEmpty(hashB64) || string.IsNullOrEmpty(saltB64)) return false;
                var h = Derive(secret, Convert.FromBase64String(saltB64), Math.Clamp(iterations, 1000, 5_000_000));
                return CryptographicOperations.FixedTimeEquals(h, Convert.FromBase64String(hashB64));
            }
            catch { return false; }
        }

        /// <summary>Turn the mode on with a PIN. See the longer overload for a password.</summary>
        public static bool EnableSchool(string pin, string confirm, out string error)
            => EnableSchool(pin, confirm, null, null, out error);

        /// <summary>
        /// Turn the mode on. At least one way to turn it off must exist: a PIN of 4 to 12 digits, a password of at least
        /// 8 characters, or both. Each is stored as its own salted PBKDF2-SHA256 hash. A credential that is already set is
        /// left as it is.
        /// </summary>
        public static bool EnableSchool(string pin, string confirm, string password, string passwordConfirm, out string error)
        {
            error = null;
            try
            {
                (string Salt, string Hash, int Iterations)? newPin = null, newPassword = null;
                if (!PinIsSet && !string.IsNullOrEmpty(pin + confirm))
                {
                    if (!PinLooksValid(pin)) { error = "Choose a PIN of 4 to 12 digits."; return false; }
                    if (pin != confirm) { error = "The two PINs do not match."; return false; }
                    newPin = MakeCredential(pin);
                }
                if (!PasswordIsSet && !string.IsNullOrEmpty(password + passwordConfirm))
                {
                    if (!PasswordLooksValid(password)) { error = $"Choose a password of {PasswordMinLength} to {PasswordMaxLength} characters."; return false; }
                    if (password != passwordConfirm) { error = "The two passwords do not match."; return false; }
                    newPassword = MakeCredential(password);
                }
                if (!CredentialIsSet && newPin == null && newPassword == null)
                { error = "Choose a PIN of 4 to 12 digits or a password of at least 8 characters."; return false; }
                if (newPin is { } np) { _school.Salt = np.Salt; _school.Hash = np.Hash; _school.Iterations = np.Iterations; }
                if (newPassword is { } pw) { _school.PasswordSalt = pw.Salt; _school.PasswordHash = pw.Hash; _school.PasswordIterations = pw.Iterations; }
                _school.On = true;
                WriteSchool();
                NotifyChanged();
                return true;
            }
            catch (Exception ex) { error = "Could not save the shared record: " + ex.Message; return false; }
        }

        /// <summary>Seconds left before another PIN try is allowed, or 0.</summary>
        public static int LockSeconds
        {
            get
            {
                var left = Settings.Current.SchoolLockUntilUtc - DateTime.UtcNow;
                return left.TotalSeconds > 0 ? (int)Math.Ceiling(left.TotalSeconds) : 0;
            }
        }

        /// <summary>Turn the mode off. Needs the PIN or the password; five wrong tries start a growing wait, never a permanent lock.</summary>
        public static bool DisableSchool(string secret, out string error)
        {
            error = null;
            var s = Settings.Current;
            if (LockSeconds > 0) { error = $"Too many wrong tries. Wait {LockSeconds} seconds."; return false; }
            try
            {
                bool ok = false;
                if (PinIsSet && PinLooksValid(secret)) ok = MatchesCredential(secret, _school.Salt, _school.Hash, _school.Iterations);
                if (!ok && PasswordIsSet && PasswordLooksValid(secret)) ok = MatchesCredential(secret, _school.PasswordSalt, _school.PasswordHash, _school.PasswordIterations);
                if (!ok)
                {
                    var what = CredentialWord;
                    s.SchoolFailures++;
                    if (s.SchoolFailures >= 5)
                    {
                        var wait = Math.Min(900, 30 * Math.Pow(2, Math.Min(10, s.SchoolFailures - 5)));
                        s.SchoolLockUntilUtc = DateTime.UtcNow.AddSeconds(wait);
                        error = $"Wrong {what}. Wait {(int)wait} seconds before the next try.";
                    }
                    else error = $"Wrong {what}. {5 - s.SchoolFailures} tries left before a short wait.";
                    Settings.Save();
                    return false;
                }
                s.SchoolFailures = 0; s.SchoolLockUntilUtc = DateTime.MinValue;
                _school.On = false;
                WriteSchool();
                Settings.Save();
                NotifyChanged();
                return true;
            }
            catch (Exception ex) { error = "Could not save the shared record: " + ex.Message; return false; }
        }

        /// <summary>Delete the shared record. This turns the mode off and forgets its name and PIN.</summary>
        public static void ResetSchoolRecord()
        {
            try { if (File.Exists(SchoolRecordPath)) File.Delete(SchoolRecordPath); }
            catch (Exception ex) { Log.Warn("School record reset failed: " + ex.Message); return; }
            Settings.Current.SchoolFailures = 0; Settings.Current.SchoolLockUntilUtc = DateTime.MinValue;
            Settings.Save();
            ReadSchool();
            NotifyChanged();
        }

        /// <summary>Rename the mode. Every surface then shows only the new name.</summary>
        public static bool RenameSchool(string name, out string error)
        {
            error = null;
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 60) { error = "The name must be 1 to 60 characters."; return false; }
            try { _school.Name = name; WriteSchool(); NotifyChanged(); return true; }
            catch (Exception ex) { error = "Could not save the shared record: " + ex.Message; return false; }
        }
    }

    /// <summary>Feature names accepted by <see cref="Modes.Blocks"/>.</summary>
    public static class ModeFeature
    {
        public const string Push = "push";                  // quick commands that push to git
        public const string Sounds = "sounds";
        public const string Cantonese = "cantonese";
        public const string Bilingual = "bilingual";
        public const string Funny = "funny";
        public const string PersonalVocabulary = "vocabulary";
        public const string DimSum = "dimsum";
        public const string GameModeOff = "gamemode-off";   // game mode cannot be switched off
    }
}
