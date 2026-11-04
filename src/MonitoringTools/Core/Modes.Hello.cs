using System;
using System.Threading.Tasks;

namespace MonitoringTools.Core
{
    /// <summary>
    /// School mode: Windows Hello as an optional third way to turn the mode off, beside the PIN and the password.
    /// This is Windows Hello user verification (the Windows consent verifier), which tells the app yes or no about the
    /// signed-in Windows user. It is not a FIDO2 or WebAuthn credential, no key pair is created, and nothing about the
    /// person is stored: the record only remembers that the person chose to allow this route.
    /// </summary>
    public static partial class Modes
    {
        public const string HelloDisclosure =
            "This is Windows Hello user verification, not a FIDO2 passkey. Windows only answers yes or no. No face, fingerprint or Windows PIN is seen or stored here, and no key is created. The PIN and password keep working.";

        // Seams so the self-test can drive the same code paths without a Hello prompt on a machine that has none.
        internal static Func<Task<(bool Available, string State)>> HelloChecker = WindowsHello.CheckAsync;
        internal static Func<string, Task<HelloResult>> HelloVerifier = WindowsHello.VerifyAsync;

        /// <summary>True when Windows Hello was set up as a way to turn the mode off and a PIN or password still exists as the fallback.</summary>
        public static bool HelloUnlockEnabled => _school.HelloUnlock && CredentialIsSet;

        /// <summary>Asks Windows whether Hello can be used now, for the Modes page to show an honest state.</summary>
        public static Task<(bool Available, string State)> HelloStateAsync() => HelloChecker();

        /// <summary>
        /// Allows Windows Hello as a way to turn the mode off. Needs a PIN or password already set, the mode switched off (so it
        /// cannot be added from inside the locked state), a Hello-capable Windows account, and one successful Hello check now.
        /// </summary>
        public static async Task<(bool Ok, string Message)> SetUpHelloAsync()
        {
            if (SchoolRecordError != null) return (false, "The shared record cannot be read, so nothing was changed.");
            if (!CredentialIsSet) return (false, "Choose a PIN or a password first. Windows Hello is added beside them, never instead of them.");
            if (SchoolActive) return (false, "Turn the mode off before changing how it is turned off.");
            var (available, state) = await HelloChecker();
            if (!available) return (false, state + " The PIN and password still work.");
            var r = await HelloVerifier("Confirm it is you to allow Windows Hello to turn " + SchoolName + " off");
            if (!r.Verified) return (false, r.Message);
            try
            {
                _school.HelloUnlock = true;
                WriteSchool();
                NotifyChanged();
                return (true, "Windows Hello is now a way to turn " + SchoolName + " off.");
            }
            catch (Exception ex) { _school.HelloUnlock = false; return (false, "Could not save the shared record: " + ex.Message); }
        }

        /// <summary>Stops offering Windows Hello. Allowed only while the mode is off.</summary>
        public static bool RemoveHelloUnlock(out string error)
        {
            error = null;
            if (SchoolActive) { error = "Turn the mode off before changing how it is turned off."; return false; }
            try { _school.HelloUnlock = false; WriteSchool(); NotifyChanged(); return true; }
            catch (Exception ex) { error = "Could not save the shared record: " + ex.Message; return false; }
        }

        /// <summary>Turns the mode off after Windows Hello confirms the Windows user. A cancelled or failed check changes nothing.</summary>
        public static async Task<(bool Ok, string Message)> DisableSchoolWithHelloAsync()
        {
            if (!SchoolActive) return (false, "The mode is already off.");
            if (!HelloUnlockEnabled) return (false, "Windows Hello was not set up for this mode. Use the PIN or the password.");
            var (available, state) = await HelloChecker();
            if (!available) return (false, state + " Use the PIN or the password instead.");
            var r = await HelloVerifier("Confirm it is you to turn " + SchoolName + " off");
            if (!r.Verified) return (false, r.Message);
            try
            {
                var s = Settings.Current;
                s.SchoolFailures = 0; s.SchoolLockUntilUtc = DateTime.MinValue;
                _school.On = false;
                WriteSchool();
                Settings.Save();
                NotifyChanged();
                return (true, SchoolName + " is off.");
            }
            catch (Exception ex) { return (false, "Could not save the shared record: " + ex.Message); }
        }
    }
}
