using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace MonitoringTools.Core
{
    public enum LockPolicy { Pin, Password, PinPassword, PasswordTotp, PinTotp, PinPasswordTotp }
    public enum LockFactor { Pin, Password, Totp }

    /// <summary>One toy lock on one element. It stores hashes of the PIN and password and the TOTP secret, inside a protected file.</summary>
    public sealed class LockEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string PageId { get; set; } = "";
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public LockPolicy Policy { get; set; } = LockPolicy.Pin;
        public int Minutes { get; set; } = 5;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string PinSalt { get; set; } = "";
        public string PinHash { get; set; } = "";
        public string PwSalt { get; set; } = "";
        public string PwHash { get; set; } = "";
        public string TotpSecret { get; set; } = "";
    }

    public sealed class LockState
    {
        public int Failures { get; set; }
        public int Lockouts { get; set; }
        public DateTime LockedUntil { get; set; }
        public bool LadderSpent { get; set; }
    }

    public sealed class LockStateFile
    {
        public Dictionary<string, LockState> States { get; set; } = new Dictionary<string, LockState>();
        public List<DateTime> LadderUses { get; set; } = new List<DateTime>();
    }

    public enum SubmitStatus { Wrong, Next, Done, LockedOut, Error }
    public sealed record SubmitResult(SubmitStatus Status, string Message);

    /// <summary>
    /// Toy locks. They are a self-imposed speed bump for fun, not security, and the recovery is always to delete the local
    /// application-data folder. Credentials are stored as salted hashes (and a TOTP secret) in a protected local file.
    /// </summary>
    public static class Locks
    {
        public const int AttemptsPerWindow = 5;
        public const int LadderPerHour = 3;
        public const string Disclosure = "This lock is just for fun. It is not security and does not protect or encrypt anything.";

        private const string LocksFile = "locks.dat";
        private const string StateFile = "lockstate.dat";
        private static readonly object Gate = new object();
        private static List<LockEntry> _list;
        private static LockStateFile _state;
        private static readonly Dictionary<string, DateTime> Open = new Dictionary<string, DateTime>();
        private static bool _started;

        public static event Action Changed;
        public static string LoadError { get; private set; }

        public static string RecoveryLine => "Locked out for good? Delete the file locks.dat in " + System.IO.Path.Combine(Settings.DataDir, "safety") + " (or the whole " + Settings.DataDir + " folder) and every lock is gone.";

        public static void Start()
        {
            if (_started) return;
            _started = true;
            SafetyHistory.Start();
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            lock (Gate)
            {
                if (_list != null) return;
                _list = new List<LockEntry>();
                _state = new LockStateFile();
                if (SecureStore.Exists(LocksFile))
                {
                    if (SecureStore.TryRead(LocksFile, out var json, out var err))
                    {
                        try { _list = JsonSerializer.Deserialize<List<LockEntry>>(json) ?? new List<LockEntry>(); }
                        catch (Exception ex) { LoadError = "The lock list could not be read: " + ex.Message; }
                    }
                    else LoadError = err;
                }
                if (SecureStore.Exists(StateFile) && SecureStore.TryRead(StateFile, out var sj, out _))
                {
                    try { _state = JsonSerializer.Deserialize<LockStateFile>(sj) ?? new LockStateFile(); } catch { _state = new LockStateFile(); }
                }
            }
        }

        public static IReadOnlyList<LockEntry> All { get { EnsureLoaded(); lock (Gate) return _list.ToList(); } }
        public static bool Any { get { EnsureLoaded(); lock (Gate) return _list.Count > 0; } }

        public static LockEntry Find(string pageId, string key) { EnsureLoaded(); lock (Gate) return _list.FirstOrDefault(l => l.PageId == pageId && l.Key == key); }
        public static LockEntry ById(string id) { EnsureLoaded(); lock (Gate) return _list.FirstOrDefault(l => l.Id == id); }

        private static bool PersistLocks()
        {
            string json; lock (Gate) json = JsonSerializer.Serialize(_list);
            return SecureStore.Write(LocksFile, json);
        }

        private static void PersistState()
        {
            string json; lock (Gate) json = JsonSerializer.Serialize(_state);
            SecureStore.Write(StateFile, json);
        }

        private static void Raise() => Changed?.Invoke();

        // ------------------------------------------------------------ policies

        public static LockFactor[] Factors(LockPolicy p) => p switch
        {
            LockPolicy.Pin => new[] { LockFactor.Pin },
            LockPolicy.Password => new[] { LockFactor.Password },
            LockPolicy.PinPassword => new[] { LockFactor.Pin, LockFactor.Password },
            LockPolicy.PasswordTotp => new[] { LockFactor.Password, LockFactor.Totp },
            LockPolicy.PinTotp => new[] { LockFactor.Pin, LockFactor.Totp },
            _ => new[] { LockFactor.Pin, LockFactor.Password, LockFactor.Totp },
        };

        public static string Describe(LockPolicy p) => p switch
        {
            LockPolicy.Pin => "PIN",
            LockPolicy.Password => "Password",
            LockPolicy.PinPassword => "PIN plus password",
            LockPolicy.PasswordTotp => "Password plus authenticator code",
            LockPolicy.PinTotp => "PIN plus authenticator code",
            _ => "PIN, password and authenticator code",
        };

        public static string Describe(LockFactor f) => f switch { LockFactor.Pin => "PIN", LockFactor.Password => "Password", _ => "Authenticator code" };

        public static bool PinValid(string pin) => pin != null && pin.Length >= 4 && pin.Length <= 8 && pin.All(char.IsDigit);

        // ------------------------------------------------------------ create, remove

        private static byte[] Derive(string secret, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(secret, salt, 120_000, HashAlgorithmName.SHA256, 32);

        private static void SetHash(string secret, out string salt, out string hash)
        {
            var s = RandomNumberGenerator.GetBytes(16);
            salt = Convert.ToBase64String(s);
            hash = Convert.ToBase64String(Derive(secret, s));
        }

        private static bool Matches(string secret, string salt, string hash)
        {
            if (secret == null || salt.Length == 0 || hash.Length == 0) return false;
            try { return CryptographicOperations.FixedTimeEquals(Derive(secret, Convert.FromBase64String(salt)), Convert.FromBase64String(hash)); }
            catch { return false; }
        }

        /// <summary>Creates a lock. Returns an error message, or null on success. Each lock gets its own credentials.</summary>
        public static string Create(string pageId, string key, string label, LockPolicy policy, int minutes, string pin, string password, string totpSecret)
        {
            EnsureLoaded();
            if (LoadError != null) return LoadError;
            if (Find(pageId, key) != null) return "That element is already locked.";
            var factors = Factors(policy);
            var e = new LockEntry { PageId = pageId, Key = key, Label = label, Policy = policy, Minutes = Math.Max(0, minutes) };
            if (factors.Contains(LockFactor.Pin))
            {
                if (!PinValid(pin)) return "A PIN is 4 to 8 digits.";
                SetHash(pin, out var s, out var h); e.PinSalt = s; e.PinHash = h;
            }
            if (factors.Contains(LockFactor.Password))
            {
                if (string.IsNullOrEmpty(password) || password.Length < 4) return "A password needs at least 4 characters.";
                SetHash(password, out var s, out var h); e.PwSalt = s; e.PwHash = h;
            }
            if (factors.Contains(LockFactor.Totp))
            {
                if (!Base32.TryDecode(totpSecret, out _)) return "The authenticator secret is not valid.";
                e.TotpSecret = Base32.Normalize(totpSecret);
            }
            lock (Gate) _list.Add(e);
            if (!PersistLocks()) { lock (Gate) _list.Remove(e); return "The lock could not be saved securely on this computer. Nothing was locked."; }
            lock (Gate) Open.Remove(e.Id);
            SafetyHistory.Record("Lock created", e.Label + " (" + Describe(policy) + ")");
            Raise();
            return null;
        }

        /// <summary>Removes a lock. It has to be unlocked first, so a lock cannot be dropped without passing it.</summary>
        public static string Remove(string id)
        {
            EnsureLoaded();
            var e = ById(id);
            if (e == null) return "That lock no longer exists.";
            if (!IsOpen(e)) return "Unlock it first, then remove it.";
            lock (Gate) { _list.Remove(e); _state.States.Remove(id); Open.Remove(id); }
            PersistLocks(); PersistState();
            SafetyHistory.Record("Lock removed", e.Label);
            Raise();
            return null;
        }

        // ------------------------------------------------------------ open and closed

        public static bool IsOpen(LockEntry e)
        {
            lock (Gate)
            {
                if (!Open.TryGetValue(e.Id, out var until)) return false;
                if (until != DateTime.MaxValue && DateTime.Now >= until) { Open.Remove(e.Id); return false; }
                return true;
            }
        }

        public static bool IsLocked(string pageId, string key)
        {
            if (!Any) return false;
            var e = Find(pageId, key);
            return e != null && !IsOpen(e);
        }

        public static void Relock(string id)
        {
            var e = ById(id);
            lock (Gate) Open.Remove(id);
            if (e != null) SafetyHistory.Record("Lock closed again", e.Label);
            Raise();
        }

        private static void MarkOpen(LockEntry e)
        {
            lock (Gate) Open[e.Id] = e.Minutes <= 0 ? DateTime.MaxValue : DateTime.Now.AddMinutes(e.Minutes);
            SafetyHistory.Record("Lock opened", e.Label);
            Raise();
        }

        // ------------------------------------------------------------ attempts and waiting

        private static LockState StateOf(string id)
        {
            lock (Gate)
            {
                if (!_state.States.TryGetValue(id, out var s)) _state.States[id] = s = new LockState();
                if (s.Failures >= AttemptsPerWindow && DateTime.Now >= s.LockedUntil) { s.Failures = 0; s.LadderSpent = false; }
                return s;
            }
        }

        /// <summary>Seconds left on the current wait, or 0 when attempts are available.</summary>
        public static int WaitSeconds(string id)
        {
            var s = StateOf(id);
            var left = (s.LockedUntil - DateTime.Now).TotalSeconds;
            return s.Failures >= AttemptsPerWindow && left > 0 ? (int)Math.Ceiling(left) : 0;
        }

        public static int AttemptsLeft(string id) => Math.Max(0, AttemptsPerWindow - StateOf(id).Failures);

        private static int WaitFor(int lockouts) => (int)Math.Min(900, 30 * Math.Pow(2, Math.Max(0, lockouts - 1)));

        private static bool Check(LockEntry e, LockFactor f, string value)
        {
            switch (f)
            {
                case LockFactor.Pin: return Matches(value, e.PinSalt, e.PinHash);
                case LockFactor.Password: return Matches(value, e.PwSalt, e.PwHash);
                default: return Totp.Verify(new OtpParams { Secret = e.TotpSecret, Algorithm = "SHA1", Digits = 6, Period = 30 }, value, 1);
            }
        }

        /// <summary>
        /// One attempt at unlocking. The keypad and the keyboard both end up here, so they share one validator and one attempt budget.
        /// A wrong answer at any step ends the attempt, and verified factors are not remembered for the next one.
        /// </summary>
        public sealed class Session
        {
            private readonly LockEntry _e;
            private readonly LockFactor[] _factors;
            public int Step { get; private set; }
            public Session(LockEntry e) { _e = e; _factors = Factors(e.Policy); }
            public LockEntry Entry => _e;
            public LockFactor Current => _factors[Math.Min(Step, _factors.Length - 1)];
            public int StepCount => _factors.Length;

            public SubmitResult Submit(string value)
            {
                if (IsOpen(_e)) return new SubmitResult(SubmitStatus.Done, "Already unlocked.");
                var st = StateOf(_e.Id);
                int wait = WaitSeconds(_e.Id);
                if (wait > 0) return new SubmitResult(SubmitStatus.LockedOut, $"Too many wrong tries. Wait {wait} seconds, or play your way out.");
                bool ok;
                try { ok = Check(_e, Current, value); } catch (Exception ex) { return new SubmitResult(SubmitStatus.Error, "That could not be checked: " + ex.Message); }
                if (ok)
                {
                    Step++;
                    if (Step < _factors.Length) return new SubmitResult(SubmitStatus.Next, Describe(_factors[Step - 1]) + " accepted. Next: " + Describe(_factors[Step]) + ".");
                    lock (Gate) { st.Failures = 0; st.Lockouts = 0; st.LadderSpent = false; }
                    PersistState();
                    MarkOpen(_e);
                    return new SubmitResult(SubmitStatus.Done, "Unlocked.");
                }
                Step = 0;
                lock (Gate)
                {
                    st.Failures++;
                    if (st.Failures >= AttemptsPerWindow) { st.Lockouts++; st.LockedUntil = DateTime.Now.AddSeconds(WaitFor(st.Lockouts)); st.LadderSpent = false; }
                }
                PersistState();
                if (st.Failures >= AttemptsPerWindow)
                    return new SubmitResult(SubmitStatus.LockedOut, $"That did not match. Too many wrong tries: wait {WaitFor(st.Lockouts)} seconds, or play your way out.");
                return new SubmitResult(SubmitStatus.Wrong, $"That did not match. {AttemptsPerWindow - st.Failures} tries left before a wait. " + RecoveryLine);
            }
        }

        // ------------------------------------------------------------ the unlock ladder budget

        public static int LadderRemaining()
        {
            EnsureLoaded();
            lock (Gate)
            {
                _state.LadderUses.RemoveAll(t => (DateTime.Now - t).TotalHours >= 1);
                return Math.Max(0, LadderPerHour - _state.LadderUses.Count);
            }
        }

        public static bool LadderOffered(string id) => WaitSeconds(id) > 0 && !StateOf(id).LadderSpent && LadderRemaining() > 0;

        /// <summary>Ends the current wait after the ladder was won. It returns the same attempts the clock would, never more, and it leaves the escalation of the next wait untouched.</summary>
        public static bool ClearWaitByLadder(string id)
        {
            if (!LadderOffered(id)) return false;
            lock (Gate)
            {
                var s = StateOf(id);
                s.LockedUntil = DateTime.Now; s.Failures = 0; s.LadderSpent = true;
                _state.LadderUses.Add(DateTime.Now);
            }
            PersistState();
            var e = ById(id);
            SafetyHistory.Record("Wait cleared by the unlock ladder", e?.Label ?? "");
            Raise();
            return true;
        }

        /// <summary>Marks the ladder as used up for this wait after a lost round.</summary>
        public static void LadderFailed(string id)
        {
            lock (Gate) StateOf(id).LadderSpent = true;
            PersistState();
        }
    }

    // ---------------------------------------------------------------- challenges

    public sealed class DimSumChallenge { public string Nonce; public string Prompt; public string[] Choices; }
    public sealed class SumChallenge { public string Nonce; public string[] Questions; }
    public sealed class MoleSlot { public int Cell; public int ShowMs; public int HideMs; }
    public sealed class MoleChallenge { public string Nonce; public int DurationMs; public int Needed; public List<MoleSlot> Slots; }

    /// <summary>
    /// Generates and grades the ladder's games in-process. Answers stay here and never reach the screen. Every challenge has a
    /// single-use nonce that is consumed before grading, and challenges expire. This is a local toy lock, so none of it is a
    /// security boundary against someone with a debugger.
    /// </summary>
    public static class Ladder
    {
        private sealed record Pending(string Kind, object Answer, DateTime Created, DateTime Expires);
        private static readonly Dictionary<string, Pending> Store = new Dictionary<string, Pending>();
        private static readonly object Gate = new object();

        private static readonly (string Name, string Description)[] Dishes =
        {
            ("Har gow", "a translucent steamed dumpling filled with shrimp"),
            ("Siu mai", "an open-topped steamed dumpling of pork and shrimp"),
            ("Char siu bao", "a fluffy bun filled with barbecued pork"),
            ("Cheung fun", "silky rolled sheets of steamed rice noodle"),
            ("Lo mai gai", "sticky rice and chicken steamed in a lotus leaf"),
            ("Dan tat", "a flaky pastry shell filled with egg custard"),
            ("Fung zao", "braised chicken feet in a savoury sauce"),
            ("Lo bak go", "pan-fried turnip cake"),
            ("Pai gwat", "steamed pork ribs with fermented black beans"),
            ("Jin deui", "a fried glutinous rice ball covered in sesame seeds"),
        };

        private static string NewNonce() => Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        private static int Rand(int n) => RandomNumberGenerator.GetInt32(n);

        private static string Put(string kind, object answer, int seconds)
        {
            var n = NewNonce();
            lock (Gate)
            {
                foreach (var k in Store.Where(p => p.Value.Expires < DateTime.Now).Select(p => p.Key).ToList()) Store.Remove(k);
                Store[n] = new Pending(kind, answer, DateTime.Now, DateTime.Now.AddSeconds(seconds));
            }
            return n;
        }

        private static Pending Take(string nonce, string kind)
        {
            lock (Gate)
            {
                if (nonce == null || !Store.TryGetValue(nonce, out var p)) return null;
                Store.Remove(nonce);
                if (p.Kind != kind || p.Expires < DateTime.Now) return null;
                return p;
            }
        }

        public static DimSumChallenge NewDimSum()
        {
            int answer = Rand(Dishes.Length);
            var picks = new List<int> { answer };
            while (picks.Count < 4) { int i = Rand(Dishes.Length); if (!picks.Contains(i)) picks.Add(i); }
            picks = picks.OrderBy(_ => Rand(1000)).ToList();
            var nonce = Put("dimsum", picks.IndexOf(answer), 120);
            return new DimSumChallenge { Nonce = nonce, Prompt = "Which dish is " + Dishes[answer].Description + "?", Choices = picks.Select(i => Dishes[i].Name).ToArray() };
        }

        public static bool GradeDimSum(string nonce, int choice)
        {
            var p = Take(nonce, "dimsum");
            return p != null && (int)p.Answer == choice;
        }

        public static SumChallenge NewSums()
        {
            var qs = new List<string>(); var ans = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                int a = 2 + Rand(i < 5 ? 8 : 40), b = 2 + Rand(i < 5 ? 8 : 40);
                switch (Rand(3))
                {
                    case 0: qs.Add($"{a} + {b}"); ans.Add(a + b); break;
                    case 1: if (a < b) (a, b) = (b, a); qs.Add($"{a} − {b}"); ans.Add(a - b); break;
                    default: a = 2 + Rand(9); b = 2 + Rand(9); qs.Add($"{a} × {b}"); ans.Add(a * b); break;
                }
            }
            return new SumChallenge { Nonce = Put("sums", ans.ToArray(), 300), Questions = qs.ToArray() };
        }

        public static bool GradeSums(string nonce, int?[] given)
        {
            var p = Take(nonce, "sums");
            if (p == null) return false;
            var ans = (int[])p.Answer;
            if (given == null || given.Length != ans.Length) return false;
            for (int i = 0; i < ans.Length; i++) if (given[i] != ans[i]) return false;
            return true;
        }

        public static MoleChallenge NewMoles()
        {
            const int duration = 15000;
            var slots = new List<MoleSlot>();
            int t = 600;
            while (t + 1400 <= duration) { slots.Add(new MoleSlot { Cell = Rand(9), ShowMs = t, HideMs = t + 1400 }); t += 1100; }
            int needed = Math.Max(1, (int)Math.Ceiling(slots.Count * 0.6));
            var c = new MoleChallenge { DurationMs = duration, Needed = needed, Slots = slots };
            c.Nonce = Put("moles", c, duration / 1000 + 60);
            return c;
        }

        /// <summary>Grades a round. It refuses a submission that arrives before the round could have finished, counts each mole once, and only counts a hit on the right cell while that mole was showing.</summary>
        public static bool GradeMoles(string nonce, IEnumerable<(int Slot, int Cell, int AtMs)> hits)
        {
            var p = Take(nonce, "moles");
            if (p == null) return false;
            var c = (MoleChallenge)p.Answer;
            if ((DateTime.Now - p.Created).TotalMilliseconds < c.DurationMs - 250) return false;
            var seen = new HashSet<int>(); int good = 0;
            foreach (var h in hits ?? Enumerable.Empty<(int, int, int)>())
            {
                if (h.Slot < 0 || h.Slot >= c.Slots.Count || !seen.Add(h.Slot)) continue;
                var s = c.Slots[h.Slot];
                if (s.Cell == h.Cell && h.AtMs >= s.ShowMs && h.AtMs <= s.HideMs) good++;
            }
            return good >= c.Needed;
        }
    }
}
