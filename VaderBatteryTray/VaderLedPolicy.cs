using System;

namespace VaderBatteryTray
{
    internal enum DockSettingState
    {
        Unknown,
        Disabled,
        Enabled
    }

    internal sealed class DockHeartbeatSettings
    {
        public DockSettingState SleepWhenCharging;
        public DockSettingState LedSync;
        public DockSettingState CloseWithSystem;
        public DockSettingState ShowAnimationWhenCharging;
    }

    internal static class DockHeartbeatProtocol
    {
        internal static bool TryDecode(
            byte[] report,
            out DockHeartbeatSettings settings)
        {
            settings = null;
            int offset = FindMagicOffset(report);
            // The four persisted flags are protocol data[18..21].
            // The offset points at 5A, so they are offset + 18..21.
            if (offset < 0 ||
                report.Length <= offset + 21 ||
                report[offset + 2] != 0x01)
            {
                return false;
            }

            settings = new DockHeartbeatSettings();
            settings.SleepWhenCharging =
                DecodeSetting(report[offset + 18]);
            settings.LedSync =
                DecodeSetting(report[offset + 19]);
            settings.CloseWithSystem =
                DecodeSetting(report[offset + 20]);
            settings.ShowAnimationWhenCharging =
                DecodeSetting(report[offset + 21]);
            return true;
        }

        private static int FindMagicOffset(byte[] report)
        {
            if (report == null)
            {
                return -1;
            }

            for (int index = 0; index + 1 < report.Length; index++)
            {
                if (report[index] == 0x5A && report[index + 1] == 0xA5)
                {
                    return index;
                }
            }
            return -1;
        }

        private static DockSettingState DecodeSetting(byte value)
        {
            return value == 0
                ? DockSettingState.Disabled
                : DockSettingState.Enabled;
        }
    }

    internal static class VaderLedPolicy
    {
        internal static bool ShouldSendControllerLighting(
            bool isDockBatterySource,
            DockHeartbeatSettings dockSettings)
        {
            if (!isDockBatterySource)
            {
                return true;
            }

            return dockSettings != null &&
                   dockSettings.SleepWhenCharging == DockSettingState.Disabled &&
                   dockSettings.LedSync == DockSettingState.Disabled &&
                   dockSettings.ShowAnimationWhenCharging == DockSettingState.Disabled;
        }

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

    internal static class DockChargeSleepPolicy
    {
        internal static bool ShouldInfer(
            bool controllerPresent,
            bool lastEfActive,
            bool hasConfirmedBand,
            DateTime lastEfUtc,
            DateTime nowUtc,
            TimeSpan evidenceLifetime,
            DockSettingState intelligentStart,
            DateTime intelligentStartCommandUtc)
        {
            if (controllerPresent ||
                !lastEfActive ||
                !hasConfirmedBand ||
                lastEfUtc == DateTime.MinValue ||
                nowUtc < lastEfUtc)
            {
                return false;
            }

            TimeSpan age = nowUtc - lastEfUtc;
            if (age > evidenceLifetime)
            {
                return false;
            }

            bool recentCommand =
                intelligentStartCommandUtc != DateTime.MinValue &&
                nowUtc >= intelligentStartCommandUtc &&
                nowUtc - intelligentStartCommandUtc <= evidenceLifetime;
            return intelligentStart == DockSettingState.Enabled ||
                recentCommand;
        }
    }
}
