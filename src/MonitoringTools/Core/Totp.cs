using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MonitoringTools.Core
{
    /// <summary>RFC 4648 base32, the encoding authenticator secrets travel in.</summary>
    public static class Base32
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string Encode(byte[] data)
        {
            var sb = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b; bits += 8;
                while (bits >= 5) { sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]); bits -= 5; }
            }
            if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        /// <summary>Accepts spaces, dashes and lower case, as people paste them. Padding is optional.</summary>
        public static bool TryDecode(string text, out byte[] data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var clean = new string(text.Where(c => c != ' ' && c != '-' && c != '=' && !char.IsControl(c)).ToArray()).ToUpperInvariant();
            if (clean.Length == 0) return false;
            var bytes = new List<byte>();
            int buffer = 0, bits = 0;
            foreach (var c in clean)
            {
                int v = Alphabet.IndexOf(c);
                if (v < 0) return false;
                buffer = (buffer << 5) | v; bits += 5;
                if (bits >= 8) { bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF)); bits -= 8; }
            }
            if (bytes.Count == 0) return false;
            data = bytes.ToArray();
            return true;
        }

        public static string Normalize(string text) => new string((text ?? "").Where(c => c != ' ' && c != '-' && c != '=' && !char.IsControl(c)).ToArray()).ToUpperInvariant();

        /// <summary>Groups a secret in fours for reading aloud or copying by eye.</summary>
        public static string Group(string text)
        {
            var n = Normalize(text);
            return string.Join(" ", Enumerable.Range(0, (n.Length + 3) / 4).Select(i => n.Substring(i * 4, Math.Min(4, n.Length - i * 4))));
        }
    }

    /// <summary>The parameters of one time-based one-time-password account.</summary>
    public sealed class OtpParams
    {
        public string Issuer { get; set; } = "";
        public string Account { get; set; } = "";
        public string Secret { get; set; } = "";
        public string Algorithm { get; set; } = "SHA1";
        public int Digits { get; set; } = 6;
        public int Period { get; set; } = 30;
    }

    /// <summary>RFC 6238 time-based one-time passwords over RFC 4226, with SHA-1, SHA-256 and SHA-512.</summary>
    public static class Totp
    {
        public static readonly string[] Algorithms = { "SHA1", "SHA256", "SHA512" };

        public static string Code(byte[] key, long unixSeconds, string algorithm, int digits, int period)
        {
            long counter = unixSeconds / period;
            var msg = new byte[8];
            for (int i = 7; i >= 0; i--) { msg[i] = (byte)(counter & 0xFF); counter >>= 8; }
            byte[] hash;
            switch ((algorithm ?? "SHA1").ToUpperInvariant())
            {
                case "SHA256": using (var h = new HMACSHA256(key)) hash = h.ComputeHash(msg); break;
                case "SHA512": using (var h = new HMACSHA512(key)) hash = h.ComputeHash(msg); break;
                default: using (var h = new HMACSHA1(key)) hash = h.ComputeHash(msg); break;
            }
            int o = hash[hash.Length - 1] & 0x0F;
            int bin = ((hash[o] & 0x7F) << 24) | (hash[o + 1] << 16) | (hash[o + 2] << 8) | hash[o + 3];
            long mod = 1; for (int i = 0; i < digits; i++) mod *= 10;
            return (bin % mod).ToString().PadLeft(digits, '0');
        }

        public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>The code for an account at a moment. Returns null when the stored secret is unreadable.</summary>
        public static string CodeFor(OtpParams p, long unix, int stepOffset = 0)
        {
            if (!Base32.TryDecode(p.Secret, out var key)) return null;
            return Code(key, unix + (long)stepOffset * p.Period, p.Algorithm, p.Digits, p.Period);
        }

        public static int SecondsLeft(OtpParams p, long unix) => p.Period - (int)(unix % p.Period);

        /// <summary>Checks a typed code against the current step and a small window either side, to forgive a slightly slow clock.</summary>
        public static bool Verify(OtpParams p, string typed, int window = 1)
        {
            typed = new string((typed ?? "").Where(char.IsDigit).ToArray());
            if (typed.Length != p.Digits) return false;
            long now = NowUnix();
            bool ok = false;
            for (int i = -window; i <= window; i++)
            {
                var c = CodeFor(p, now, i);
                if (c != null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(c), Encoding.ASCII.GetBytes(typed))) ok = true;
            }
            return ok;
        }

        public static string NewSecret() => Base32.Encode(RandomNumberGenerator.GetBytes(20));

        /// <summary>Reads an otpauth://totp/ URI, honouring the algorithm, digits and period it carries.</summary>
        public static bool TryParseUri(string uri, out OtpParams result, out string error)
        {
            result = null; error = null;
            uri = (uri ?? "").Trim();
            if (!uri.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase)) { error = "That does not start with otpauth://."; return false; }
            var rest = uri.Substring("otpauth://".Length);
            int slash = rest.IndexOf('/');
            if (slash < 0) { error = "The link has no account part."; return false; }
            var type = rest.Substring(0, slash).ToLowerInvariant();
            if (type != "totp") { error = type == "hotp" ? "Counter-based (HOTP) accounts are not supported. Only time-based (TOTP) accounts are." : "Unknown account type \"" + type + "\"."; return false; }
            var tail = rest.Substring(slash + 1);
            string label = tail, query = "";
            int q = tail.IndexOf('?');
            if (q >= 0) { label = tail.Substring(0, q); query = tail.Substring(q + 1); }
            label = SafeUnescape(label);
            var p = new OtpParams();
            int colon = label.IndexOf(':');
            if (colon >= 0) { p.Issuer = label.Substring(0, colon).Trim(); p.Account = label.Substring(colon + 1).Trim(); }
            else p.Account = label.Trim();
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                var k = SafeUnescape(eq < 0 ? pair : pair.Substring(0, eq)).ToLowerInvariant();
                var v = eq < 0 ? "" : SafeUnescape(pair.Substring(eq + 1));
                switch (k)
                {
                    case "secret": p.Secret = Base32.Normalize(v); break;
                    case "issuer": if (v.Length > 0) p.Issuer = v.Trim(); break;
                    case "algorithm":
                        var a = v.ToUpperInvariant().Replace("-", "");
                        if (!Algorithms.Contains(a)) { error = "Unsupported algorithm \"" + v + "\". Use SHA1, SHA256 or SHA512."; return false; }
                        p.Algorithm = a; break;
                    case "digits":
                        if (!int.TryParse(v, out var d) || d < 6 || d > 8) { error = "Digits must be 6, 7 or 8."; return false; }
                        p.Digits = d; break;
                    case "period":
                        if (!int.TryParse(v, out var per) || per < 1 || per > 3600) { error = "The period must be between 1 and 3600 seconds."; return false; }
                        p.Period = per; break;
                }
            }
            if (!Base32.TryDecode(p.Secret, out _)) { error = "The secret is missing or is not valid base32."; return false; }
            result = p;
            return true;
        }

        private static string SafeUnescape(string s) { try { return Uri.UnescapeDataString(s.Replace("+", "%20")); } catch { return s; } }

        public static string BuildUri(OtpParams p)
        {
            var label = Uri.EscapeDataString(string.IsNullOrEmpty(p.Issuer) ? p.Account : p.Issuer + ":" + p.Account);
            var sb = new StringBuilder("otpauth://totp/" + label + "?secret=" + p.Secret);
            if (!string.IsNullOrEmpty(p.Issuer)) sb.Append("&issuer=" + Uri.EscapeDataString(p.Issuer));
            sb.Append("&algorithm=" + p.Algorithm + "&digits=" + p.Digits + "&period=" + p.Period);
            return sb.ToString();
        }

        /// <summary>Runs the published RFC 6238 test vectors (SHA-1, SHA-256, SHA-512, 8 digits). Used by the Safety page to prove the maths live.</summary>
        public static bool SelfTest(out string detail)
        {
            var k1 = Encoding.ASCII.GetBytes("12345678901234567890");
            var k256 = Encoding.ASCII.GetBytes("12345678901234567890123456789012");
            var k512 = Encoding.ASCII.GetBytes("1234567890123456789012345678901234567890123456789012345678901234");
            var cases = new (byte[] Key, string Algo, long T, string Expect)[]
            {
                (k1, "SHA1", 59, "94287082"), (k256, "SHA256", 59, "46119246"), (k512, "SHA512", 59, "90693936"),
                (k1, "SHA1", 1111111109, "07081804"), (k256, "SHA256", 1111111109, "68084774"), (k512, "SHA512", 1111111109, "25091201"),
                (k1, "SHA1", 1234567890, "89005924"), (k256, "SHA256", 1234567890, "91819424"), (k512, "SHA512", 1234567890, "93441116"),
            };
            int pass = 0;
            foreach (var c in cases) if (Code(c.Key, c.T, c.Algo, 8, 30) == c.Expect) pass++;
            detail = $"{pass} of {cases.Length} published RFC 6238 vectors match";
            return pass == cases.Length;
        }
    }

    /// <summary>One stored authenticator account. The whole list lives in a Windows data-protection file, never in settings.json.</summary>
    public sealed class AuthAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Issuer { get; set; } = "";
        public string Account { get; set; } = "";
        public string Secret { get; set; } = "";
        public string Algorithm { get; set; } = "SHA1";
        public int Digits { get; set; } = 6;
        public int Period { get; set; } = 30;
        public DateTime AddedAt { get; set; } = DateTime.Now;

        public OtpParams ToParams() => new OtpParams { Issuer = Issuer, Account = Account, Secret = Secret, Algorithm = Algorithm, Digits = Digits, Period = Period };
        public string Title => string.IsNullOrEmpty(Issuer) ? Account : Issuer + (string.IsNullOrEmpty(Account) ? "" : " · " + Account);
    }

    /// <summary>The built-in authenticator's account list.</summary>
    public static class Authenticator
    {
        private const string File = "authenticator.dat";
        private static readonly object Gate = new object();
        private static List<AuthAccount> _list;
        public static event Action Changed;
        public static string LoadError { get; private set; }

        public static IReadOnlyList<AuthAccount> All { get { EnsureLoaded(); lock (Gate) return _list.ToList(); } }

        private static void EnsureLoaded()
        {
            lock (Gate)
            {
                if (_list != null) return;
                _list = new List<AuthAccount>();
                if (SecureStore.Exists(File))
                {
                    if (SecureStore.TryRead(File, out var json, out var err))
                    {
                        try { _list = JsonSerializer.Deserialize<List<AuthAccount>>(json) ?? new List<AuthAccount>(); }
                        catch (Exception ex) { LoadError = "The account list could not be read: " + ex.Message; }
                    }
                    else LoadError = err;
                }
            }
        }

        private static bool Persist()
        {
            if (LoadError != null) { Log.Warn("Authenticator not saved because the stored list could not be read."); return false; }
            string json; lock (Gate) json = JsonSerializer.Serialize(_list);
            return SecureStore.Write(File, json);
        }

        /// <summary>Adds an account. Returns an error message, or null on success.</summary>
        public static string Add(OtpParams p)
        {
            EnsureLoaded();
            if (LoadError != null) return LoadError;
            if (!Base32.TryDecode(p.Secret, out _)) return "That secret is not valid base32.";
            var a = new AuthAccount { Issuer = p.Issuer ?? "", Account = p.Account ?? "", Secret = Base32.Normalize(p.Secret), Algorithm = p.Algorithm, Digits = p.Digits, Period = p.Period };
            lock (Gate) _list.Add(a);
            if (!Persist()) { lock (Gate) _list.Remove(a); return "The account could not be saved securely on this computer. Nothing was changed."; }
            SafetyHistory.Record("Authenticator account added", a.Title);
            Changed?.Invoke();
            return null;
        }

        public static string Remove(string id)
        {
            EnsureLoaded();
            AuthAccount a; lock (Gate) a = _list.FirstOrDefault(x => x.Id == id);
            if (a == null) return "That account no longer exists.";
            lock (Gate) _list.Remove(a);
            if (!Persist()) { lock (Gate) _list.Add(a); return "The change could not be saved. The account was kept."; }
            SafetyHistory.Record("Authenticator account removed", a.Title);
            Changed?.Invoke();
            return null;
        }

        /// <summary>Changes the issuer and account label. The secret is never touched.</summary>
        public static string Rename(string id, string issuer, string account)
        {
            EnsureLoaded();
            AuthAccount a; lock (Gate) a = _list.FirstOrDefault(x => x.Id == id);
            if (a == null) return "That account no longer exists.";
            var oi = a.Issuer; var oa = a.Account;
            a.Issuer = issuer ?? ""; a.Account = account ?? "";
            if (!Persist()) { a.Issuer = oi; a.Account = oa; return "The change could not be saved."; }
            SafetyHistory.Record("Authenticator account edited", "label changed to " + a.Title);
            Changed?.Invoke();
            return null;
        }

        public static void Move(string id, int delta)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int i = _list.FindIndex(x => x.Id == id), j = i + delta;
                if (i < 0 || j < 0 || j >= _list.Count) return;
                (_list[i], _list[j]) = (_list[j], _list[i]);
            }
            Persist();
            SafetyHistory.Record("Authenticator list reordered", "");
            Changed?.Invoke();
        }
    }
}
