using System;
using System.Threading.Tasks;
using Windows.Security.Credentials.UI;

namespace MonitoringTools.Core
{
    /// <summary>The outcome of asking Windows Hello to confirm that the person at the keyboard is the signed-in Windows user.</summary>
    public sealed record HelloResult(bool Verified, string Message);

    /// <summary>
    /// A thin wrapper over the Windows consent verifier (Windows Hello: face, fingerprint or the Windows PIN). It is only a yes
    /// or no from Windows. This app never sees a biometric, a Windows PIN or a password, and it stores nothing about the check.
    /// It is an extra way to open a toy lock or to reveal an authenticator secret; it never replaces the lock's own credentials.
    /// </summary>
    public static class WindowsHello
    {
        public const string Disclosure = "Windows Hello only tells this app yes or no. No face, fingerprint or Windows PIN is seen or stored here, and the lock's own PIN or password is not changed.";

        /// <summary>Asks Windows whether Hello can be used right now. Never throws; an unexpected failure is reported as unavailable with its reason.</summary>
        public static async Task<(bool Available, string State)> CheckAsync()
        {
            try
            {
                var a = await UserConsentVerifier.CheckAvailabilityAsync();
                return (a == UserConsentVerifierAvailability.Available, Describe(a));
            }
            catch (Exception ex)
            {
                return (false, "Windows Hello could not be checked on this computer (" + ex.GetType().Name + ").");
            }
        }

        private static string Describe(UserConsentVerifierAvailability a) => a switch
        {
            UserConsentVerifierAvailability.Available => "Windows Hello is available.",
            UserConsentVerifierAvailability.DeviceNotPresent => "Windows Hello is not available: this computer has no Hello-capable device (camera, fingerprint reader or PIN support).",
            UserConsentVerifierAvailability.NotConfiguredForUser => "Windows Hello is not set up for this Windows account. Set it up in Windows Settings, Accounts, Sign-in options.",
            UserConsentVerifierAvailability.DisabledByPolicy => "Windows Hello is turned off by a policy on this computer.",
            UserConsentVerifierAvailability.DeviceBusy => "Windows Hello is busy right now. Try again in a moment.",
            _ => "Windows Hello reported an unknown state (" + a + ")."
        };

        /// <summary>Shows the Windows Hello prompt. The message is the line Windows shows in its own dialog.</summary>
        public static async Task<HelloResult> VerifyAsync(string message)
        {
            try
            {
                var r = await UserConsentVerifier.RequestVerificationAsync(message);
                return r switch
                {
                    UserConsentVerificationResult.Verified => new HelloResult(true, "Windows Hello confirmed it is you."),
                    UserConsentVerificationResult.Canceled => new HelloResult(false, "Windows Hello was cancelled. Nothing was changed."),
                    UserConsentVerificationResult.RetriesExhausted => new HelloResult(false, "Windows Hello gave up after too many failed tries. Nothing was changed."),
                    UserConsentVerificationResult.DeviceBusy => new HelloResult(false, "Windows Hello is busy right now. Try again in a moment."),
                    UserConsentVerificationResult.DeviceNotPresent => new HelloResult(false, "Windows Hello is not available: this computer has no Hello-capable device."),
                    UserConsentVerificationResult.NotConfiguredForUser => new HelloResult(false, "Windows Hello is not set up for this Windows account."),
                    UserConsentVerificationResult.DisabledByPolicy => new HelloResult(false, "Windows Hello is turned off by a policy on this computer."),
                    _ => new HelloResult(false, "Windows Hello did not confirm (" + r + "). Nothing was changed.")
                };
            }
            catch (Exception ex)
            {
                Log.Warn("Windows Hello check failed: " + ex.GetType().Name);
                return new HelloResult(false, "Windows Hello could not be started (" + ex.GetType().Name + "). Nothing was changed.");
            }
        }
    }
}
