using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MonitoringTools.Core
{
    /// <summary>How saved script and template versions are kept on disk.</summary>
    public static class HistoryProtection
    {
        public const string None = "none";
        public const string Windows = "windows";
        public const string Password = "password";
    }

    /// <summary>
    /// Encryption for the local history of scripts and templates. Two modes, both authenticated AES-256-GCM:
    /// "windows" keeps a random data key protected by the Windows data-protection API for this user (another Windows account or
    /// another computer cannot read it, malware running as this user can), and "password" derives the key from a password the person
    /// chooses with PBKDF2-SHA256 (600,000 rounds, random salt). The password is never stored; a small encrypted check value proves it.
    /// A password-protected history is locked until the password is typed, and locks again when the person asks or the app closes.
    /// </summary>
    public static class HistoryVault
    {
        public const int Rounds = 600_000;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MTH1");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MonitoringTools.History.v1");
        private static byte[] _sessionKey;   // the unlocked data key in password mode, kept in memory only
        private static readonly object Gate = new object();

        private static string Root => Path.Combine(Settings.DataDir, "history");
        private static string ConfigPath => Path.Combine(Root, "protection.json");
        private static string DpapiKeyPath => Path.Combine(Root, "key.dpapi");

        private sealed class Config
        {
            public string Salt { get; set; } = "";
            public int Rounds { get; set; } = HistoryVault.Rounds;
            public string Check { get; set; } = "";
        }

        public static string Mode => Settings.Current?.HistoryProtection is HistoryProtection.Windows or HistoryProtection.Password ? Settings.Current.HistoryProtection : HistoryProtection.None;

        /// <summary>True when the history is password-protected and the password has not been typed yet.</summary>
        public static bool IsLocked { get { lock (Gate) return Mode == HistoryProtection.Password && _sessionKey == null; } }

        /// <summary>True when new versions are written encrypted.</summary>
        public static bool Encrypts => Mode != HistoryProtection.None;

        public static void Lock() { lock (Gate) { if (_sessionKey != null) CryptographicOperations.ZeroMemory(_sessionKey); _sessionKey = null; } }

        // ---------------------------------------------------------------- the sealed format: "MTH1" + nonce(12) + tag(16) + ciphertext

        private static byte[] Seal(byte[] key, byte[] plain)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16]; var cipher = new byte[plain.Length];
            using (var gcm = new AesGcm(key, 16)) gcm.Encrypt(nonce, plain, cipher, tag, Magic);
            var all = new byte[4 + 12 + 16 + cipher.Length];
            Buffer.BlockCopy(Magic, 0, all, 0, 4); Buffer.BlockCopy(nonce, 0, all, 4, 12); Buffer.BlockCopy(tag, 0, all, 16, 16); Buffer.BlockCopy(cipher, 0, all, 32, cipher.Length);
            return all;
        }

        private static byte[] Open(byte[] key, byte[] blob)
        {
            if (blob == null || blob.Length < 32 || !blob.Take(4).SequenceEqual(Magic)) return null;
            var nonce = blob.AsSpan(4, 12).ToArray(); var tag = blob.AsSpan(16, 16).ToArray(); var cipher = blob.AsSpan(32).ToArray();
            var plain = new byte[cipher.Length];
            try { using var gcm = new AesGcm(key, 16); gcm.Decrypt(nonce, cipher, tag, plain, Magic); return plain; }
            catch (CryptographicException) { return null; }
        }

        private static byte[] Derive(string password, byte[] salt, int rounds) => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, rounds, HashAlgorithmName.SHA256, 32);

        private static byte[] WindowsKey(bool create)
        {
            try
            {
                if (File.Exists(DpapiKeyPath)) return ProtectedData.Unprotect(File.ReadAllBytes(DpapiKeyPath), Entropy, DataProtectionScope.CurrentUser);
                if (!create) return null;
                Directory.CreateDirectory(Root);
                var key = RandomNumberGenerator.GetBytes(32);
                File.WriteAllBytes(DpapiKeyPath, ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser));
                return key;
            }
            catch (Exception ex) { Log.Warn("History key unavailable: " + ex.Message); return null; }
        }

        /// <summary>The data key for the mode in force, or null when locked or unavailable.</summary>
        private static byte[] CurrentKey()
        {
            lock (Gate)
            {
                if (Mode == HistoryProtection.Windows) return WindowsKey(false);
                if (Mode == HistoryProtection.Password) return _sessionKey;
                return null;
            }
        }

        public static byte[] Encrypt(string text)
        {
            var key = CurrentKey();
            return key == null ? null : Seal(key, new UTF8Encoding(false).GetBytes(text ?? ""));
        }

        /// <summary>Decrypts a stored version with the key in force. Null when the history is locked, the data was changed or the key is not this one.</summary>
        public static string Decrypt(byte[] blob)
        {
            var key = CurrentKey();
            if (key == null) return null;
            var plain = Open(key, blob);
            return plain == null ? null : new UTF8Encoding(false).GetString(plain);
        }

        // ---------------------------------------------------------------- unlocking and changing the mode

        /// <summary>Types the password for a password-protected history. Returns an error sentence, or null when it opened.</summary>
        public static string Unlock(string password)
        {
            if (Mode != HistoryProtection.Password) return "This history is not protected by a password.";
            try
            {
                var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath));
                var key = Derive(password ?? "", Convert.FromBase64String(cfg.Salt), cfg.Rounds);
                var ok = Open(key, Convert.FromBase64String(cfg.Check));
                if (ok == null || Encoding.ASCII.GetString(ok) != "history-ok") return "That password is not right.";
                lock (Gate) _sessionKey = key;
                return null;
            }
            catch (Exception ex) { return "The password check could not be read: " + ex.Message; }
        }

        /// <summary>
        /// Moves every saved version to a new mode. Every version is read with the key in force, written under the new one, and only then are the
        /// old files removed, so a failure part-way leaves the readable copies in place. Returns an error sentence, or null on success.
        /// </summary>
        public static string Change(string newMode, string newPassword = null)
        {
            if (newMode != HistoryProtection.None && newMode != HistoryProtection.Windows && newMode != HistoryProtection.Password) return "Unknown history protection.";
            if (IsLocked) return "Unlock the history with its password first.";
            if (newMode == HistoryProtection.Password && (newPassword ?? "").Length < 8) return "A history password needs at least 8 characters.";
            try
            {
                Directory.CreateDirectory(Root);
                // read everything with the key in force
                var plain = new List<(string Path, string Text)>();
                if (Directory.Exists(Root))
                    foreach (var f in Directory.GetFiles(Root, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".txt") || f.EndsWith(".enc")))
                    {
                        var text = ScriptHistory.ReadFile(f);
                        if (text == null) return "A saved version could not be read, so nothing was changed.";
                        plain.Add((f, text));
                    }

                // set up the new key
                byte[] key = null;
                Config cfg = null;
                if (newMode == HistoryProtection.Windows)
                {
                    if (File.Exists(DpapiKeyPath)) File.Delete(DpapiKeyPath);
                    key = WindowsKey(true);
                    if (key == null) return "Windows could not protect a key for this account, so nothing was changed.";
                }
                else if (newMode == HistoryProtection.Password)
                {
                    var salt = RandomNumberGenerator.GetBytes(16);
                    key = Derive(newPassword, salt, Rounds);
                    cfg = new Config { Salt = Convert.ToBase64String(salt), Rounds = Rounds, Check = Convert.ToBase64String(Seal(key, Encoding.ASCII.GetBytes("history-ok"))) };
                }

                // write the new copies next to the old ones, then drop the old ones
                var written = new List<(string Old, string New)>();
                foreach (var (path, text) in plain)
                {
                    var target = Path.ChangeExtension(path, newMode == HistoryProtection.None ? ".txt" : ".enc");
                    if (newMode == HistoryProtection.None) File.WriteAllText(target + ".new", text, new UTF8Encoding(false));
                    else File.WriteAllBytes(target + ".new", Seal(key, new UTF8Encoding(false).GetBytes(text)));
                    written.Add((path, target));
                }
                foreach (var (old, target) in written)
                {
                    if (!string.Equals(old, target, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
                    File.Move(target + ".new", target, true);
                }
                if (newMode == HistoryProtection.Password) File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg));
                else if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
                if (newMode != HistoryProtection.Windows && File.Exists(DpapiKeyPath)) File.Delete(DpapiKeyPath);
                lock (Gate) { Lock(); if (newMode == HistoryProtection.Password) _sessionKey = key; }
                Settings.Current.HistoryProtection = newMode;
                Settings.Save();
                return null;
            }
            catch (Exception ex) { Log.Warn("History protection change failed: " + ex.Message); return "The change failed and was stopped: " + ex.Message; }
        }
    }
}
