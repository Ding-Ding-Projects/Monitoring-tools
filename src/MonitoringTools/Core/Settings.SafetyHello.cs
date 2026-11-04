namespace MonitoringTools.Core
{
    public sealed partial class Settings
    {
        // Safety: the Windows Hello route.

        /// <summary>Offer "Unlock with Windows Hello" on lock prompts and in the unlock ladder when Windows Hello is available. Default: on.</summary>
        public bool LockOfferWindowsHello { get; set; } = true;
    }
}
