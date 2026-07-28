namespace VaderBatteryTray
{
    internal static class VaderLedPolicy
    {
        internal static bool TryGetBatteryColor(
            int bandLevel,
            out byte red,
            out byte green,
            out byte blue)
        {
            // The controller's diffuser desaturates mixed colors. Use a
            // hardware-specific saturated palette; tray and Rainmeter retain
            // their screen-oriented palette.
            switch (bandLevel)
            {
                case 1:
                    red = 255;
                    green = 0;
                    blue = 0;
                    return true;
                case 2:
                    red = 255;
                    green = 64;
                    blue = 0;
                    return true;
                case 3:
                    red = 255;
                    green = 255;
                    blue = 0;
                    return true;
                case 4:
                    red = 0;
                    green = 255;
                    blue = 0;
                    return true;
                case 5:
                    red = 0;
                    green = 0;
                    blue = 255;
                    return true;
                default:
                    red = 0;
                    green = 0;
                    blue = 0;
                    return false;
            }
        }

        internal static bool TryGetDockChargingColor(
            int bandLevel,
            out byte red,
            out byte green,
            out byte blue)
        {
            ChargingAccent accent =
                ChargingAccentPolicy.FromState(true, true, bandLevel);
            switch (accent)
            {
                case ChargingAccent.Red:
                    red = 255;
                    green = 0;
                    blue = 0;
                    return true;
                case ChargingAccent.Yellow:
                    red = 255;
                    green = 255;
                    blue = 0;
                    return true;
                case ChargingAccent.Blue:
                    red = 0;
                    green = 0;
                    blue = 255;
                    return true;
                default:
                    red = 0;
                    green = 0;
                    blue = 0;
                    return false;
            }
        }

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
