using Microsoft.Win32;
using System;

namespace VaderBatteryTray
{
    internal static class BatterySourcePolicy
    {
        public static bool ShouldPreferDock(
            bool dockAvailable,
            bool dockCharging,
            bool controllerSessionLive)
        {
            return dockAvailable &&
                   (dockCharging || !controllerSessionLive);
        }
    }

    internal static class BatteryDisplayScale
    {
        private static readonly int[] Percentages = new int[]
        {
            10, 25, 40, 55, 70, 85, 100
        };

        public static int PercentFromLevel(int level)
        {
            return PercentFromOrdinal(level);
        }

        public static int PercentFromDockState(int rawState)
        {
            return PercentFromOrdinal(rawState - 1);
        }

        public static int PercentFromOrdinal(int ordinal)
        {
            return ordinal >= 0 && ordinal < Percentages.Length
                ? Percentages[ordinal]
                : -1;
        }

        public static int OrdinalFromPercent(int percent)
        {
            for (int index = 0; index < Percentages.Length; index++)
            {
                if (Percentages[index] == percent)
                {
                    return index;
                }
            }
            return -1;
        }
    }

    internal sealed class BatteryPresentationState
    {
        public int AnchorOrdinal = -1;
        public int BaselineGetInfoLevel = -1;
        public DateTime AnchorUtc = DateTime.MinValue;
    }

    internal interface IBatteryPresentationStateStore
    {
        BatteryPresentationState Load();
        void Save(BatteryPresentationState state);
    }

    internal sealed class BatteryPresentationRegistryStateStore :
        IBatteryPresentationStateStore
    {
        private const string RegistryPath =
            @"Software\VaderBatteryTray\PresentationState";
        private const int SchemaVersion = 1;

        public BatteryPresentationState Load()
        {
            BatteryPresentationState state = new BatteryPresentationState();
            try
            {
                using (RegistryKey key =
                    Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (key == null ||
                        ReadInt32(key, "SchemaVersion", 0) != SchemaVersion)
                    {
                        return state;
                    }

                    state.AnchorOrdinal =
                        ReadInt32(key, "AnchorOrdinalPlusOne", 0) - 1;
                    state.BaselineGetInfoLevel =
                        ReadInt32(key, "BaselineGetInfoLevelPlusOne", 0) - 1;
                    state.AnchorUtc = ReadUtc(key, "AnchorUtc");
                }
            }
            catch
            {
                return new BatteryPresentationState();
            }
            return state;
        }

        public void Save(BatteryPresentationState state)
        {
            try
            {
                using (RegistryKey key =
                    Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null)
                    {
                        return;
                    }

                    key.SetValue(
                        "SchemaVersion",
                        SchemaVersion,
                        RegistryValueKind.DWord);
                    key.SetValue(
                        "AnchorOrdinalPlusOne",
                        state.AnchorOrdinal + 1,
                        RegistryValueKind.DWord);
                    key.SetValue(
                        "BaselineGetInfoLevelPlusOne",
                        state.BaselineGetInfoLevel + 1,
                        RegistryValueKind.DWord);
                    key.SetValue(
                        "AnchorUtc",
                        state.AnchorUtc.ToUniversalTime().ToBinary().ToString(),
                        RegistryValueKind.String);
                }
            }
            catch
            {
                // Presentation continuity must never interrupt monitoring.
            }
        }

        private static int ReadInt32(
            RegistryKey key,
            string name,
            int defaultValue)
        {
            object value = key.GetValue(name, defaultValue);
            return Convert.ToInt32(value);
        }

        private static DateTime ReadUtc(RegistryKey key, string name)
        {
            long binary;
            if (!Int64.TryParse(Convert.ToString(key.GetValue(name, null)), out binary))
            {
                return DateTime.MinValue;
            }

            try
            {
                return DateTime.FromBinary(binary).ToUniversalTime();
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }

    internal sealed class BatteryPresentationContinuity
    {
        internal static readonly TimeSpan AnchorLifetime = TimeSpan.FromHours(12);

        private readonly IBatteryPresentationStateStore store;
        private readonly BatteryPresentationState state;
        private readonly object sync = new object();

        public BatteryPresentationContinuity(IBatteryPresentationStateStore store)
        {
            this.store = store;
            state = store == null
                ? new BatteryPresentationState()
                : store.Load();
        }

        public int ObserveDockPercent(int percent, DateTime observedUtc)
        {
            lock (sync)
            {
                int ordinal = BatteryDisplayScale.OrdinalFromPercent(percent);
                if (ordinal < 0)
                {
                    Clear();
                    return percent;
                }

                state.AnchorOrdinal = ordinal;
                state.BaselineGetInfoLevel = -1;
                state.AnchorUtc = NormalizeUtc(observedUtc);
                Save();
                return percent;
            }
        }

        public int ObserveWirelessDischarging(
            int rawLevel,
            int normalizedPercent,
            DateTime observedUtc)
        {
            lock (sync)
            {
                DateTime now = NormalizeUtc(observedUtc);
                if (!HasRecentAnchor(now))
                {
                    Clear();
                    return normalizedPercent;
                }

                if (state.BaselineGetInfoLevel < 0)
                {
                    state.BaselineGetInfoLevel = rawLevel;
                    Save();
                }

                int adjustedOrdinal =
                    state.AnchorOrdinal +
                    rawLevel -
                    state.BaselineGetInfoLevel;
                if (adjustedOrdinal < 0)
                {
                    adjustedOrdinal = 0;
                }
                if (adjustedOrdinal > 6)
                {
                    adjustedOrdinal = 6;
                }

                return BatteryDisplayScale.PercentFromOrdinal(adjustedOrdinal);
            }
        }

        public void ObserveContradictingAvailableState()
        {
            lock (sync)
            {
                Clear();
            }
        }

        private bool HasRecentAnchor(DateTime now)
        {
            return state.AnchorOrdinal >= 0 &&
                   state.AnchorUtc != DateTime.MinValue &&
                   state.AnchorUtc <= now &&
                   now - state.AnchorUtc <= AnchorLifetime;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value == DateTime.MinValue
                ? DateTime.UtcNow
                : value.ToUniversalTime();
        }

        private void Clear()
        {
            if (state.AnchorOrdinal < 0 &&
                state.BaselineGetInfoLevel < 0 &&
                state.AnchorUtc == DateTime.MinValue)
            {
                return;
            }

            state.AnchorOrdinal = -1;
            state.BaselineGetInfoLevel = -1;
            state.AnchorUtc = DateTime.MinValue;
            Save();
        }

        private void Save()
        {
            if (store != null)
            {
                store.Save(state);
            }
        }
    }
}
