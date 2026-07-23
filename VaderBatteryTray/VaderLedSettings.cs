using System;
using Microsoft.Win32;

namespace VaderBatteryTray
{
    internal sealed class VaderLedSettings
    {
        private const string RegistryPath = @"Software\VaderBatteryTray";
        private const string ControlValueName = "LedControl";
        private const string BrightnessValueName = "LedBrightness";
        private const string WarningValueName = "LedWarningAccepted";
        private const string ControlEnvironmentVariable = "VADER_TRAY_LED_CONTROL";
        private const string BrightnessEnvironmentVariable = "VADER_TRAY_LED_BRIGHTNESS";

        public const byte DefaultBrightnessPercent = 25;

        private bool storedControlEnabled;
        private byte storedBrightnessPercent;
        private bool warningAccepted;

        private VaderLedSettings()
        {
            string controlEnvironment = Environment.GetEnvironmentVariable(ControlEnvironmentVariable);
            string brightnessEnvironment = Environment.GetEnvironmentVariable(BrightnessEnvironmentVariable);

            ControlManagedByEnvironment = controlEnvironment != null;
            BrightnessManagedByEnvironment = brightnessEnvironment != null;
            storedControlEnabled = ReadDword(ControlValueName, 0) != 0;
            storedBrightnessPercent = ResolveStoredBrightness(ReadDword(BrightnessValueName, DefaultBrightnessPercent));
            warningAccepted = ReadDword(WarningValueName, 0) != 0;
            ControlEnabled = ResolveControl(controlEnvironment, storedControlEnabled);
            BrightnessPercent = ResolveBrightness(brightnessEnvironment, storedBrightnessPercent);
        }

        public bool ControlManagedByEnvironment { get; private set; }

        public bool BrightnessManagedByEnvironment { get; private set; }

        public bool ControlEnabled { get; private set; }

        public byte BrightnessPercent { get; private set; }

        public bool WarningAccepted
        {
            get { return warningAccepted; }
        }

        public static VaderLedSettings Load()
        {
            return new VaderLedSettings();
        }

        public bool TrySetControlEnabled(bool value, out string error)
        {
            if (ControlManagedByEnvironment)
            {
                error = ControlEnvironmentVariable + " is controlling this setting.";
                return false;
            }

            if (!TryWriteDword(ControlValueName, value ? 1 : 0, out error))
            {
                return false;
            }

            storedControlEnabled = value;
            ControlEnabled = value;
            return true;
        }

        public bool TrySetBrightness(byte value, out string error)
        {
            if (BrightnessManagedByEnvironment)
            {
                error = BrightnessEnvironmentVariable + " is controlling this setting.";
                return false;
            }

            if (value > 100)
            {
                error = "Brightness must be between 0 and 100.";
                return false;
            }

            if (!TryWriteDword(BrightnessValueName, value, out error))
            {
                return false;
            }

            storedBrightnessPercent = value;
            BrightnessPercent = value;
            return true;
        }

        public bool TryAcceptWarning(out string error)
        {
            if (!TryWriteDword(WarningValueName, 1, out error))
            {
                return false;
            }

            warningAccepted = true;
            return true;
        }

        public bool TryResetUserSettings(out string error)
        {
            if (!TryDeleteValue(ControlValueName, out error) ||
                !TryDeleteValue(BrightnessValueName, out error))
            {
                return false;
            }

            storedControlEnabled = false;
            storedBrightnessPercent = DefaultBrightnessPercent;

            if (!ControlManagedByEnvironment)
            {
                ControlEnabled = false;
            }
            if (!BrightnessManagedByEnvironment)
            {
                BrightnessPercent = DefaultBrightnessPercent;
            }

            return true;
        }

        internal static bool ResolveControl(string environmentValue, bool storedValue)
        {
            return environmentValue == null
                ? storedValue
                : String.Equals(environmentValue, "1", StringComparison.Ordinal);
        }

        internal static byte ResolveBrightness(string environmentValue, byte storedValue)
        {
            if (environmentValue == null)
            {
                return storedValue;
            }

            int parsed;
            if (Int32.TryParse(environmentValue, out parsed) && parsed >= 0 && parsed <= 100)
            {
                return (byte)parsed;
            }

            return DefaultBrightnessPercent;
        }

        private static byte ResolveStoredBrightness(int storedValue)
        {
            return storedValue >= 0 && storedValue <= 100
                ? (byte)storedValue
                : DefaultBrightnessPercent;
        }

        private static int ReadDword(string name, int defaultValue)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (key == null)
                    {
                        return defaultValue;
                    }

                    object value = key.GetValue(name, defaultValue);
                    return Convert.ToInt32(value);
                }
            }
            catch
            {
                return defaultValue;
            }
        }

        private static bool TryWriteDword(string name, int value, out string error)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null)
                    {
                        error = "The user settings key could not be opened.";
                        return false;
                    }

                    key.SetValue(name, value, RegistryValueKind.DWord);
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryDeleteValue(string name, out string error)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(name, false);
                    }
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
