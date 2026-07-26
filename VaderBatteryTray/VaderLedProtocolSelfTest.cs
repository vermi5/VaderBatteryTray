using System;

namespace VaderLedProtocolSelfTest
{
    internal static class Program
    {
        private static void Main()
        {
            TestSolidVector();
            TestConfiguredColorAndBrightness();
            TestOffVector();
            TestSettingsResolution();
            TestNativeLowBatteryAlertPolicy();
            TestDockEstimatedScale();
            TestBatterySourcePrecedence();
            TestSharedBatteryDisplayScale();
            TestDockToWirelessContinuity();
            TestFullToWirelessContinuity();
            TestDockFullTransitions();
            TestDockRuntimeRestore();
            Console.WriteLine("Vader LED protocol self-test passed.");
        }

        private static void TestSolidVector()
        {
            byte[][] reports = VaderBatteryTray.VaderLedProtocol.BuildSolid(0x2F, 0xAC, 0xED, 50);

            AssertEqual(4, reports.Length, "solid report count");
            AssertHex(
                reports[0],
                "5a a5 a8 06 00 00 03 14 c5 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
                "solid prelude");
            AssertByte(0x32, reports[1][11], "solid brightness");
            AssertByte(0x05, reports[1][13], "solid mode");
            AssertZones(reports, 0x2F, 0xAC, 0xED, "solid zones");
            AssertChecksums(reports, "solid checksums");
        }

        private static void TestConfiguredColorAndBrightness()
        {
            byte[][] reports = VaderBatteryTray.VaderLedProtocol.BuildSolid(255, 255, 0, 25);

            AssertByte(25, reports[1][11], "configured brightness");
            AssertZones(reports, 255, 255, 0, "configured medium zones");
            AssertChecksums(reports, "configured medium checksums");
        }

        private static void TestOffVector()
        {
            byte[][] reports = VaderBatteryTray.VaderLedProtocol.BuildOff(50);

            AssertEqual(4, reports.Length, "off report count");
            AssertByte(0x06, reports[1][13], "off mode");
            AssertZones(reports, 0x00, 0x00, 0x00, "off zones");
            AssertChecksums(reports, "off checksums");
        }

        private static void TestSettingsResolution()
        {
            AssertBoolean(true, VaderBatteryTray.VaderLedSettings.ResolveControl(null, true), "stored control enabled");
            AssertBoolean(false, VaderBatteryTray.VaderLedSettings.ResolveControl("0", true), "environment control disabled");
            AssertBoolean(true, VaderBatteryTray.VaderLedSettings.ResolveControl("1", false), "environment control enabled");

            AssertByte(40, VaderBatteryTray.VaderLedSettings.ResolveBrightness(null, 40), "stored brightness");
            AssertByte(0, VaderBatteryTray.VaderLedSettings.ResolveBrightness("0", 40), "minimum environment brightness");
            AssertByte(100, VaderBatteryTray.VaderLedSettings.ResolveBrightness("100", 40), "maximum environment brightness");
            AssertByte(25, VaderBatteryTray.VaderLedSettings.ResolveBrightness("invalid", 40), "invalid environment brightness");
            AssertByte(25, VaderBatteryTray.VaderLedSettings.ResolveBrightness("101", 40), "out-of-range environment brightness");
        }

        private static void TestNativeLowBatteryAlertPolicy()
        {
            AssertBoolean(true, VaderBatteryTray.VaderLedPolicy.ShouldPreserveNativeLowBatteryAlert(true, true, 20, false, 0), "preserve awake 20 percent warning");
            AssertBoolean(true, VaderBatteryTray.VaderLedPolicy.ShouldPreserveNativeLowBatteryAlert(true, true, 0, false, 0), "preserve awake zero percent warning");
            AssertBoolean(false, VaderBatteryTray.VaderLedPolicy.ShouldPreserveNativeLowBatteryAlert(true, true, 40, false, 0), "allow RGB above warning band");
            AssertBoolean(false, VaderBatteryTray.VaderLedPolicy.ShouldPreserveNativeLowBatteryAlert(true, true, 20, true, 1), "allow charging color policy");
            AssertBoolean(false, VaderBatteryTray.VaderLedPolicy.ShouldPreserveNativeLowBatteryAlert(false, true, 20, false, 0), "require live controller session");
        }

        private static void TestDockEstimatedScale()
        {
            DateTime now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
            VaderBatteryTray.DockBatteryStateTracker tracker =
                new VaderBatteryTray.DockBatteryStateTracker(null);
            int[] expectedPercent = new int[] { 10, 25, 40, 55, 70, 85 };
            int[] expectedBand = new int[] { 1, 1, 2, 2, 3, 3 };

            for (int state = 1; state <= 6; state++)
            {
                VaderBatteryTray.DockBatteryDecision decision =
                    tracker.Process(1, (byte)state, 1, now.AddSeconds(state));
                AssertBoolean(true, decision.Available, "Dock state available " + state.ToString());
                AssertBoolean(true, decision.IsCharging, "Dock state charging " + state.ToString());
                AssertBoolean(false, decision.IsFull, "Dock state not Full " + state.ToString());
                AssertEqual(expectedPercent[state - 1], decision.Percent, "Dock estimated percent " + state.ToString());
                AssertEqual(expectedBand[state - 1], decision.BandLevel, "Dock physical band " + state.ToString());
            }
        }

        private static void TestSharedBatteryDisplayScale()
        {
            int[] expected = new int[] { 10, 25, 40, 55, 70, 85, 100 };
            for (int ordinal = 0; ordinal < expected.Length; ordinal++)
            {
                AssertEqual(
                    expected[ordinal],
                    VaderBatteryTray.BatteryDisplayScale.PercentFromOrdinal(ordinal),
                    "shared display scale ordinal " + ordinal.ToString());
                AssertEqual(
                    ordinal,
                    VaderBatteryTray.BatteryDisplayScale.OrdinalFromPercent(expected[ordinal]),
                    "shared display scale reverse ordinal " + ordinal.ToString());
            }

            AssertEqual(
                -1,
                VaderBatteryTray.BatteryDisplayScale.PercentFromOrdinal(-1),
                "negative display ordinal unavailable");
            AssertEqual(
                -1,
                VaderBatteryTray.BatteryDisplayScale.PercentFromOrdinal(7),
                "oversized display ordinal unavailable");
        }

        private static void TestBatterySourcePrecedence()
        {
            AssertBoolean(
                true,
                VaderBatteryTray.BatterySourcePolicy.ShouldPreferDock(
                    true,
                    true,
                    true),
                "active Dock overrides live controller");
            AssertBoolean(
                false,
                VaderBatteryTray.BatterySourcePolicy.ShouldPreferDock(
                    true,
                    false,
                    true),
                "inactive retained Dock Full does not override live controller");
            AssertBoolean(
                true,
                VaderBatteryTray.BatterySourcePolicy.ShouldPreferDock(
                    true,
                    false,
                    false),
                "inactive Dock Full is valid without a live controller");
            AssertBoolean(
                false,
                VaderBatteryTray.BatterySourcePolicy.ShouldPreferDock(
                    false,
                    true,
                    false),
                "unavailable Dock never wins");
        }

        private static void TestDockToWirelessContinuity()
        {
            DateTime now = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);
            MemoryBatteryPresentationStateStore store =
                new MemoryBatteryPresentationStateStore();
            VaderBatteryTray.BatteryPresentationContinuity continuity =
                new VaderBatteryTray.BatteryPresentationContinuity(store);

            AssertEqual(85, continuity.ObserveDockPercent(85, now), "Dock anchor 85");
            AssertEqual(
                85,
                continuity.ObserveWirelessDischarging(4, 70, now.AddSeconds(1)),
                "first Wireless sample preserves Dock 85");
            AssertEqual(
                85,
                continuity.ObserveWirelessDischarging(4, 70, now.AddMinutes(1)),
                "unchanged Wireless raw level preserves 85");
            AssertEqual(
                70,
                continuity.ObserveWirelessDischarging(3, 55, now.AddMinutes(2)),
                "next Wireless raw step advances to 70");

            VaderBatteryTray.BatteryPresentationContinuity restored =
                new VaderBatteryTray.BatteryPresentationContinuity(store);
            AssertEqual(
                70,
                restored.ObserveWirelessDischarging(3, 55, now.AddMinutes(3)),
                "continuity survives process restart");
        }

        private static void TestFullToWirelessContinuity()
        {
            DateTime now = new DateTime(2026, 7, 26, 14, 0, 0, DateTimeKind.Utc);
            MemoryBatteryPresentationStateStore store =
                new MemoryBatteryPresentationStateStore();
            VaderBatteryTray.BatteryPresentationContinuity continuity =
                new VaderBatteryTray.BatteryPresentationContinuity(store);

            AssertEqual(100, continuity.ObserveDockPercent(100, now), "Full anchor");
            AssertEqual(
                100,
                continuity.ObserveWirelessDischarging(4, 70, now.AddSeconds(1)),
                "first Wireless sample retains Full");
            AssertEqual(
                100,
                continuity.ObserveWirelessDischarging(4, 70, now.AddMinutes(1)),
                "Full remains while raw level is unchanged");
            AssertEqual(
                85,
                continuity.ObserveWirelessDischarging(3, 55, now.AddMinutes(2)),
                "first raw level change leaves Full at 85");

            continuity.ObserveContradictingAvailableState();
            AssertEqual(
                55,
                continuity.ObserveWirelessDischarging(3, 55, now.AddMinutes(3)),
                "contradicting state clears continuity");
        }

        private static void TestDockFullTransitions()
        {
            DateTime now = new DateTime(2026, 7, 25, 18, 0, 0, DateTimeKind.Utc);
            VaderBatteryTray.DockBatteryStateTracker ambiguousTracker =
                new VaderBatteryTray.DockBatteryStateTracker(null);
            VaderBatteryTray.DockBatteryDecision ambiguous =
                ambiguousTracker.Process(0, 0x06, 0, now);
            AssertBoolean(false, ambiguous.Available, "presence-cleared inactive 0x06 unavailable");

            VaderBatteryTray.DockBatteryDecision presentFull =
                ambiguousTracker.Process(0, 0x06, 1, now.AddMilliseconds(500));
            AssertBoolean(true, presentFull.IsFull, "present inactive 0x06 is Full");

            VaderBatteryTray.DockBatteryStateTracker tracker =
                new VaderBatteryTray.DockBatteryStateTracker(null);
            tracker.Process(1, 0x06, 1, now);
            VaderBatteryTray.DockBatteryDecision full =
                tracker.Process(0, 0x06, 1, now.AddSeconds(1));
            AssertBoolean(true, full.Available, "active-to-inactive 0x06 available");
            AssertBoolean(true, full.IsFull, "active-to-inactive 0x06 Full");
            AssertBoolean(false, full.IsCharging, "Full not charging");
            AssertEqual(100, full.Percent, "Full percent");
            AssertEqual(4, full.BandLevel, "Full band");

            VaderBatteryTray.DockBatteryStateTracker redockTracker =
                new VaderBatteryTray.DockBatteryStateTracker(null);
            redockTracker.Process(1, 0x01, 1, now.AddSeconds(2));
            redockTracker.Process(1, 0x01, 1, now.AddSeconds(3));
            VaderBatteryTray.DockBatteryDecision redockedFull =
                redockTracker.Process(0, 0x01, 1, now.AddSeconds(4));
            AssertBoolean(true, redockedFull.IsFull, "insertion without charging settles as Full");
        }

        private static void TestDockRuntimeRestore()
        {
            DateTime now = new DateTime(2026, 7, 25, 20, 0, 0, DateTimeKind.Utc);
            MemoryDockRuntimeStateStore store = new MemoryDockRuntimeStateStore();
            store.State.LastRawFlag = 1;
            store.State.LastRawState = 0x06;
            store.State.LastPresenceFlag = 1;
            store.State.LastActiveState = 0x06;
            store.State.LastActiveUtc = now.AddMinutes(-10);

            VaderBatteryTray.DockBatteryStateTracker tracker =
                new VaderBatteryTray.DockBatteryStateTracker(store);
            VaderBatteryTray.DockBatteryDecision restored =
                tracker.Process(0, 0x06, 1, now);
            AssertBoolean(true, restored.IsFull, "recent persisted active 0x06 restores Full");
            AssertBoolean(true, store.State.FullConfirmed, "restored Full persisted");

            VaderBatteryTray.DockBatteryDecision expired =
                tracker.Process(0, 0x06, 0, now.AddHours(13));
            AssertBoolean(false, expired.Available, "presence-cleared state invalidates persisted Full");

            VaderBatteryTray.DockBatteryDecision contradiction =
                tracker.Process(1, 0x03, 1, now.AddHours(13).AddSeconds(1));
            AssertBoolean(false, contradiction.IsFull, "active state invalidates restored Full");
            AssertEqual(40, contradiction.Percent, "contradicting active state wins");
            AssertBoolean(false, store.State.FullConfirmed, "contradiction clears persisted Full");
        }

        private sealed class MemoryDockRuntimeStateStore : VaderBatteryTray.IDockRuntimeStateStore
        {
            public VaderBatteryTray.DockRuntimeState State =
                new VaderBatteryTray.DockRuntimeState();

            public VaderBatteryTray.DockRuntimeState Load()
            {
                return State;
            }

            public void Save(VaderBatteryTray.DockRuntimeState state)
            {
                State = state;
            }
        }

        private sealed class MemoryBatteryPresentationStateStore :
            VaderBatteryTray.IBatteryPresentationStateStore
        {
            public VaderBatteryTray.BatteryPresentationState State =
                new VaderBatteryTray.BatteryPresentationState();

            public VaderBatteryTray.BatteryPresentationState Load()
            {
                return State;
            }

            public void Save(VaderBatteryTray.BatteryPresentationState state)
            {
                State = state;
            }
        }

        private static void AssertZones(byte[][] reports, byte red, byte green, byte blue, string name)
        {
            byte[] zoneBytes = new byte[30];
            Array.Copy(reports[2], 5, zoneBytes, 0, 20);
            Array.Copy(reports[3], 5, zoneBytes, 20, 10);

            for (int index = 0; index < zoneBytes.Length; index += 3)
            {
                AssertByte(red, zoneBytes[index], name + " red");
                AssertByte(green, zoneBytes[index + 1], name + " green");
                AssertByte(blue, zoneBytes[index + 2], name + " blue");
            }

            for (int index = 15; index < 25; index++)
            {
                AssertByte(0x00, reports[3][index], name + " padding");
            }
        }

        private static void AssertChecksums(byte[][] reports, string name)
        {
            for (int reportIndex = 1; reportIndex < reports.Length; reportIndex++)
            {
                int sum = 0;
                for (int index = 0; index < 25; index++)
                {
                    sum += reports[reportIndex][index];
                }
                AssertByte((byte)((sum + 1) & 0xFF), reports[reportIndex][25], name);
            }
        }

        private static void AssertHex(byte[] actual, string expected, string name)
        {
            string normalized = expected.Replace(" ", String.Empty);
            string actualHex = BitConverter.ToString(actual).Replace("-", String.Empty).ToLowerInvariant();
            if (!String.Equals(actualHex, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(name + " mismatch: " + actualHex);
            }
        }

        private static void AssertByte(byte expected, byte actual, string name)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(name + " mismatch: expected 0x" + expected.ToString("X2") + ", got 0x" + actual.ToString("X2"));
            }
        }

        private static void AssertBoolean(bool expected, bool actual, string name)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(name + " mismatch: expected " + expected.ToString() + ", got " + actual.ToString());
            }
        }

        private static void AssertEqual(int expected, int actual, string name)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(name + " mismatch: expected " + expected + ", got " + actual);
            }
        }
    }
}
