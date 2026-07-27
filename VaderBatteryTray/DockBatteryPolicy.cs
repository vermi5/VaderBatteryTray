using Microsoft.Win32;
using System;

namespace VaderBatteryTray
{
    internal sealed class DockBatteryDecision
    {
        public bool Available;
        public bool IsCharging;
        public bool IsFull;
        public int Percent;
        public int BandLevel;
        public string Reason;

        public static DockBatteryDecision Unavailable(string reason)
        {
            return new DockBatteryDecision
            {
                Available = false,
                Percent = -1,
                BandLevel = 0,
                Reason = reason
            };
        }
    }

    internal sealed class DockRuntimeState
    {
        public int LastRawFlag = -1;
        public int LastRawState = -1;
        public int LastField9 = -1;
        public int LastActiveState = -1;
        public DateTime LastActiveUtc = DateTime.MinValue;
        public bool FullConfirmed;
        public DateTime FullConfirmedUtc = DateTime.MinValue;
    }

    internal interface IDockRuntimeStateStore
    {
        DockRuntimeState Load();
        void Save(DockRuntimeState state);
    }

    internal sealed class DockRegistryRuntimeStateStore : IDockRuntimeStateStore
    {
        private const string RegistryPath = @"Software\VaderBatteryTray\RuntimeState";
        private const int SchemaVersion = 2;

        public DockRuntimeState Load()
        {
            DockRuntimeState state = new DockRuntimeState();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
                {
                    if (key == null || ReadInt32(key, "SchemaVersion", 0) != SchemaVersion)
                    {
                        return state;
                    }

                    state.LastRawFlag = ReadInt32(key, "LastRawFlag", -1);
                    state.LastRawState = ReadInt32(key, "LastRawState", -1);
                    state.LastField9 = ReadInt32(key, "LastField9", -1);
                    state.LastActiveState = ReadInt32(key, "LastActiveState", -1);
                    state.LastActiveUtc = ReadUtc(key, "LastActiveUtc");
                    state.FullConfirmed = ReadInt32(key, "FullConfirmed", 0) != 0;
                    state.FullConfirmedUtc = ReadUtc(key, "FullConfirmedUtc");
                }
            }
            catch
            {
                return new DockRuntimeState();
            }
            return state;
        }

        public void Save(DockRuntimeState state)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null)
                    {
                        return;
                    }

                    key.SetValue("SchemaVersion", SchemaVersion, RegistryValueKind.DWord);
                    key.SetValue("LastRawFlag", state.LastRawFlag, RegistryValueKind.DWord);
                    key.SetValue("LastRawState", state.LastRawState, RegistryValueKind.DWord);
                    key.SetValue("LastField9", state.LastField9, RegistryValueKind.DWord);
                    key.SetValue("LastActiveState", state.LastActiveState, RegistryValueKind.DWord);
                    WriteUtc(key, "LastActiveUtc", state.LastActiveUtc);
                    key.SetValue("FullConfirmed", state.FullConfirmed ? 1 : 0, RegistryValueKind.DWord);
                    WriteUtc(key, "FullConfirmedUtc", state.FullConfirmedUtc);
                }
            }
            catch
            {
                // Runtime caching must never interrupt battery monitoring.
            }
        }

        private static int ReadInt32(RegistryKey key, string name, int defaultValue)
        {
            object value = key.GetValue(name, defaultValue);
            if (value is int)
            {
                return (int)value;
            }
            if (value is uint)
            {
                return unchecked((int)(uint)value);
            }

            long numeric = Convert.ToInt64(value);
            if (numeric > Int32.MaxValue && numeric <= UInt32.MaxValue)
            {
                return unchecked((int)(uint)numeric);
            }
            return checked((int)numeric);
        }

        private static DateTime ReadUtc(RegistryKey key, string name)
        {
            object value = key.GetValue(name, null);
            if (value == null)
            {
                return DateTime.MinValue;
            }

            long binary;
            if (!Int64.TryParse(Convert.ToString(value), out binary))
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

        private static void WriteUtc(RegistryKey key, string name, DateTime value)
        {
            long binary = value == DateTime.MinValue
                ? DateTime.MinValue.ToBinary()
                : value.ToUniversalTime().ToBinary();
            key.SetValue(name, binary.ToString(), RegistryValueKind.String);
        }
    }

    internal sealed class DockBatteryStateTracker
    {
        internal static readonly TimeSpan RuntimeCacheLifetime = TimeSpan.FromHours(12);
        internal static readonly TimeSpan RuntimePersistHeartbeat = TimeSpan.FromMinutes(5);
        internal static readonly TimeSpan ActiveStateConfirmationWindow =
            TimeSpan.FromMilliseconds(1500);

        private readonly IDockRuntimeStateStore store;
        private readonly DockRuntimeState state;
        private readonly object sync = new object();
        private int savedLastRawFlag;
        private int savedLastRawState;
        private int savedLastField9;
        private int savedLastActiveState;
        private DateTime savedLastActiveUtc;
        private bool savedFullConfirmed;
        private DateTime savedFullConfirmedUtc;
        private int pendingActiveState = -1;
        private int pendingActiveCount;
        private DateTime pendingActiveSinceUtc = DateTime.MinValue;
        private bool hasConfirmedActiveThisSession;

        public DockBatteryStateTracker(IDockRuntimeStateStore store)
        {
            this.store = store;
            state = store == null ? new DockRuntimeState() : store.Load();
            RememberSavedState();
        }

        public DockBatteryDecision Process(
            byte flag,
            byte rawState,
            byte field9,
            DateTime observedUtc)
        {
            lock (sync)
            {
                return ProcessLocked(flag, rawState, field9, observedUtc);
            }
        }

        private DockBatteryDecision ProcessLocked(
            byte flag,
            byte rawState,
            byte field9,
            DateTime observedUtc)
        {
            DateTime now = observedUtc == DateTime.MinValue
                ? DateTime.UtcNow
                : observedUtc.ToUniversalTime();
            int previousRawFlag = state.LastRawFlag;
            int previousRawState = state.LastRawState;

            state.LastRawFlag = flag;
            state.LastRawState = rawState;
            state.LastField9 = field9;

            if (rawState < 0x01 || rawState > 0x06)
            {
                Save();
                return DockBatteryDecision.Unavailable(
                    "unknown Dock EF state 0x" + rawState.ToString("X2"));
            }

            if (flag != 0)
            {
                if (!hasConfirmedActiveThisSession ||
                    state.LastActiveState != rawState)
                {
                    if (pendingActiveState != rawState)
                    {
                        pendingActiveState = rawState;
                        pendingActiveCount = 1;
                        pendingActiveSinceUtc = now;
                    }
                    else
                    {
                        pendingActiveCount++;
                    }

                    bool confirmed =
                        pendingActiveCount >= 2 ||
                        now - pendingActiveSinceUtc >= ActiveStateConfirmationWindow;
                    if (!confirmed)
                    {
                        Save();
                        if (hasConfirmedActiveThisSession &&
                            state.LastActiveState >= 0x01 &&
                            state.LastActiveState <= 0x06 &&
                            IsRecent(state.LastActiveUtc, now, RuntimeCacheLifetime))
                        {
                            return ActiveDecision(
                                state.LastActiveState,
                                "retained stable Dock EF step while a new step is pending");
                        }
                        return DockBatteryDecision.Unavailable(
                            "new active Dock EF step is awaiting confirmation");
                    }

                    state.LastActiveState = rawState;
                    state.LastActiveUtc = now;
                    hasConfirmedActiveThisSession = true;
                    ClearPendingActiveState();
                }
                else
                {
                    ClearPendingActiveState();
                    if (!IsRecent(state.LastActiveUtc, now, RuntimePersistHeartbeat))
                    {
                        state.LastActiveUtc = now;
                    }
                }
                state.FullConfirmed = false;
                Save();
                return ActiveDecision(rawState, "confirmed active Dock EF step");
            }

            ClearPendingActiveState();
            bool completedHighCharge =
                rawState == 0x06 &&
                hasConfirmedActiveThisSession &&
                previousRawFlag != 0 &&
                previousRawState == 0x06 &&
                state.LastActiveState == 0x06 &&
                IsRecent(state.LastActiveUtc, now, RuntimeCacheLifetime);

            bool retainedSessionFull =
                rawState == 0x06 &&
                hasConfirmedActiveThisSession &&
                state.FullConfirmed &&
                IsRecent(state.FullConfirmedUtc, now, RuntimeCacheLifetime);

            bool restoredRecentFull =
                rawState == 0x06 &&
                state.FullConfirmed &&
                IsRecent(state.FullConfirmedUtc, now, RuntimeCacheLifetime);

            if (completedHighCharge ||
                retainedSessionFull ||
                restoredRecentFull)
            {
                state.FullConfirmed = true;
                if (completedHighCharge)
                {
                    state.FullConfirmedUtc = now;
                }
                Save();
                return new DockBatteryDecision
                {
                    Available = true,
                    IsCharging = false,
                    IsFull = true,
                    Percent = 100,
                    BandLevel = 4,
                    Reason = completedHighCharge
                        ? "active 0x06 transitioned to inactive 0x06"
                        : (retainedSessionFull
                            ? "retained Full confirmed in the current Dock session"
                            : "restored recent confirmed Full")
                };
            }

            state.FullConfirmed = false;
            Save();
            return DockBatteryDecision.Unavailable(
                "inactive Dock EF state is retained and ambiguous");
        }

        internal static int EstimatedPercent(int rawState)
        {
            return BatteryDisplayScale.PercentFromDockState(rawState);
        }

        private static DockBatteryDecision ActiveDecision(int rawState, string reason)
        {
            return new DockBatteryDecision
            {
                Available = true,
                IsCharging = true,
                IsFull = false,
                Percent = EstimatedPercent(rawState),
                BandLevel = BandLevel(rawState),
                Reason = reason
            };
        }

        private void ClearPendingActiveState()
        {
            pendingActiveState = -1;
            pendingActiveCount = 0;
            pendingActiveSinceUtc = DateTime.MinValue;
        }

        internal static int BandLevel(int rawState)
        {
            if (rawState <= 0x02)
            {
                return 1;
            }
            if (rawState <= 0x04)
            {
                return 2;
            }
            return 3;
        }

        private static bool IsRecent(DateTime value, DateTime now, TimeSpan lifetime)
        {
            if (value == DateTime.MinValue || value > now)
            {
                return false;
            }
            return now - value <= lifetime;
        }

        private void Save()
        {
            if (store != null && HasStateChanged())
            {
                store.Save(state);
                RememberSavedState();
            }
        }

        private bool HasStateChanged()
        {
            return savedLastRawFlag != state.LastRawFlag ||
                   savedLastRawState != state.LastRawState ||
                   savedLastField9 != state.LastField9 ||
                   savedLastActiveState != state.LastActiveState ||
                   savedLastActiveUtc != state.LastActiveUtc ||
                   savedFullConfirmed != state.FullConfirmed ||
                   savedFullConfirmedUtc != state.FullConfirmedUtc;
        }

        private void RememberSavedState()
        {
            savedLastRawFlag = state.LastRawFlag;
            savedLastRawState = state.LastRawState;
            savedLastField9 = state.LastField9;
            savedLastActiveState = state.LastActiveState;
            savedLastActiveUtc = state.LastActiveUtc;
            savedFullConfirmed = state.FullConfirmed;
            savedFullConfirmedUtc = state.FullConfirmedUtc;
        }
    }
}
