using System.Linq;
using MonitoringTools.Clip;
using MonitoringTools.Core;

namespace MonitoringTools.UI
{
    public static partial class SelfTest
    {
        /// <summary>A copied picture of an authenticator QR code offers to register it, and registering twice keeps one account.</summary>
        private static void TotpQrChecks()
        {
            Check("minecraft: command replies lose terminal colour codes (as a real Forge server sends them over rcon-cli)", () =>
            {
                var forge = "Set the time to 1000 \u001B[0m";
                var got = MonitoringTools.Minecraft.ServerManager.NoAnsi(forge);
                if (got != "Set the time to 1000") return "got \"" + got.Replace("\u001B", "ESC") + "\"";
                if (MonitoringTools.Minecraft.ServerManager.NoAnsi("\u001B[1;32mThere are 0 of a max of 7 players online:\u001B[0m") != "There are 0 of a max of 7 players online:") return "coloured list reply kept its codes";
                if (MonitoringTools.Minecraft.ServerManager.NoAnsi("plain [brackets] stay") != "plain [brackets] stay") return "plain text was changed";
                return null;
            });

            Check("authenticator: a copied otpauth QR picture offers Register for codes, adds the account once and keeps the secret out of the toast", () =>
            {
                var p = new OtpParams { Issuer = "Example", Account = "ava@example.com", Secret = Totp.NewSecret(), Digits = 6, Period = 30 };
                if (!QrEncoder.TryEncode(Totp.BuildUri(p), out var modules, out _, out var err)) return "encode failed: " + err;
                var img = QrEncoder.ToBitmap(modules, 6);
                var found = ActionProvider.AuthenticatorQr(img);
                if (found == null) return "the QR picture was not recognised as an authenticator code";
                if (Base32.Normalize(found.Secret) != Base32.Normalize(p.Secret) || found.Issuer != "Example") return "wrong account read: " + found.Issuer;
                var actions = ActionProvider.For(new ClipItem { Kind = ClipKind.Image, Image = img });
                var reg = actions.FirstOrDefault(a => a.Label == "Register for codes");
                if (reg == null) return "no Register for codes action; actions: " + string.Join(", ", actions.Select(a => a.Label));
                if (!reg.Primary || actions.IndexOf(reg) != 0) return "Register for codes is not the first, primary action";
                if ((reg.Tip ?? "").Contains(Base32.Normalize(p.Secret))) return "the secret appears in the tooltip";
                var before = Authenticator.All.Count(a => a.Secret == Base32.Normalize(p.Secret));
                if (before != 0) return "already present before registering";
                if (ActionProvider.RegisterAuthenticator(found, "Example") != null) return "register failed";
                if (ActionProvider.RegisterAuthenticator(found, "Example") != "already") return "a second register was not reported as already registered";
                if (Authenticator.All.Count(a => a.Secret == Base32.Normalize(p.Secret)) != 1) return "the account is not there exactly once";
                if (!SecureStore.Exists("authenticator.dat") || System.IO.File.ReadAllText(SecureStore.PathOf("authenticator.dat")).Contains(Base32.Normalize(p.Secret))) return "the secret is readable on disk";
                // a QR code that is not an authenticator code offers nothing extra
                if (!QrEncoder.TryEncode("https://example.com/", out var m2, out _, out _)) return "encode of a link failed";
                if (ActionProvider.AuthenticatorQr(QrEncoder.ToBitmap(m2, 6)) != null) return "a plain link QR was treated as an authenticator code";
                foreach (var a in Authenticator.All.Where(a => a.Secret == Base32.Normalize(p.Secret)).ToList()) Authenticator.Remove(a.Id);
                foreach (var t in App.Toasts.LiveToasts.ToList()) { try { t.Close(); } catch { } }
                return null;
            });
        }
    }
}
