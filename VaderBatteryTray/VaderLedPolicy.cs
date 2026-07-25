namespace VaderBatteryTray
{
    internal static class VaderLedPolicy
    {
        internal static bool ShouldPreserveNativeLowBatteryAlert(
            bool hasLiveControllerSession,
            bool hasBattery,
            int percent,
            bool isCharging,
            byte? rawGetInfoStatusNibble)
        {
            return hasLiveControllerSession &&
                   hasBattery &&
                   percent >= 0 &&
                   percent <= 20 &&
                   !isCharging &&
                   rawGetInfoStatusNibble.HasValue &&
                   rawGetInfoStatusNibble.Value == 0;
        }
    }
}
