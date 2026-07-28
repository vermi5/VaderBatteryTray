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
            TestSaturatedControllerPalette();
            TestDockChargingAccentPalette();
            TestDockEstimatedScale();
            TestQualitativeBatteryLevels();
            TestTransientUnavailablePublicationPolicy();
            TestCriticalPublicationPolicy();
            TestBatterySourcePrecedence();
            TestSharedBatteryDisplayScale();
            TestDockToWirelessContinuity();
            TestFullToWirelessContinuity();
            TestDockFullTransitions();
            TestDockRuntimeRestore();
            TestDockTransientStateFiltering();
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

        private static void TestSaturatedControllerPalette()
        {
            byte[][] expected = new byte[][]
            {
                new byte[] { 255, 0, 0 },
                new byte[] { 255, 64, 0 },
                new byte[] { 255, 255, 0 },
                new byte[] { 0, 255, 0 },
                new byte[] { 0, 0, 255 }
            };

            for (int level = 1; level <= expected.Length; level++)
            {
                byte red;
                byte green;
                byte blue;
                AssertBoolean(
                    true,
                    VaderBatteryTray.VaderLedPolicy.TryGetBatteryColor(
                        level,
                        out red,
                        out green,
                        out blue),
                    "controller palette level " + level.ToString());
                AssertByte(
                    expected[level - 1][0],
                    red,
                    "controller palette red " + level.ToString());
                AssertByte(
                    expected[level - 1][1],
                    green,
                    "controller palette green " + level.ToString());
                AssertByte(
                    expected[level - 1][2],
                    blue,
                    "controller palette blue " + level.ToString());
            }
        }

        private static void TestDockChargingAccentPalette()
        {
            AssertEqual(
                (int)VaderBatteryTray.ChargingAccent.Red,
                (int)VaderBatteryTray.ChargingAccentPolicy.FromState(
                    true,
                    true,
                    2),
                "Dock Low charging accent");
            AssertEqual(
                (int)VaderBatteryTray.ChargingAccent.Yellow,
                (int)VaderBatteryTray.ChargingAccentPolicy.FromState(
                    true,
                    true,
                    4),
                "Dock High charging accent");
            AssertEqual(
                (int)VaderBatteryTray.ChargingAccent.Blue,
                (int)VaderBatteryTray.ChargingAccentPolicy.FromState(
                    true,
                    true,
                    5),
                "Dock Top charging accent");
            AssertEqual(
                (int)VaderBatteryTray.ChargingAccent.None,
                (int)VaderBatteryTray.ChargingAccentPolicy.FromState(
                    false,
                    true,
                    5),
                "Charged has no charging accent");
            AssertEqual(
                (int)VaderBatteryTray.ChargingAccent.None,
                (int)VaderBatteryTray.ChargingAccentPolicy.FromState(
                    true,
                    false,
                    4),
                "non-Dock charging has no native accent");

            byte red;
            byte green;
            byte blue;
            AssertBoolean(
                true,
                VaderBatteryTray.VaderLedPolicy.TryGetDockChargingColor(
                    4,
                    out red,
                    out green,
                    out blue),
                "Dock High controller charging color");
            AssertByte(255, red, "Dock High charging red");
            AssertByte(255, green, "Dock High charging green");
            AssertByte(0, blue, "Dock High charging blue");
        }

        private static void TestDockEstimatedScale()
        {
            DateTime now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
            VaderBatteryTray.DockBatteryStateTracker tracker =
                new VaderBatteryTray.DockBatteryStateTracker(null);
            int[] expectedPercent = new int[] { 10, 25, 40, 55, 70, 85 };
            int[] expectedBand = new int[] { 1, 2, 3, 4, 5, 5 };

            for (int state = 1; state <= 6; state++)
            {
                tracker.Process(
                    1,
                    (byte)state,
                    1,
                    now.AddSeconds(state * 2));
                VaderBatteryTray.DockBatteryDecision decision =
                    tracker.Process(
                        1,
                        (byte)state,
                        1,
                        now.AddSeconds((state * 2) + 1));
                AssertBoolean(true, decision.Available, "Dock state available " + state.ToString());
                AssertBoolean(true, decision.IsCharging, "Dock state charging " + state.ToString());
                AssertBoolean(false, decision.IsFull, "Dock state not Full " + state.ToString());
                AssertEqual(expectedPercent[state - 1], decision.Percent, "Dock estimated percent " + state.ToString());
                AssertEqual(expectedBand[state - 1], decision.BandLevel, "Dock physical band " + state.ToString());
            }
        }

        private static void TestQualitativeBatteryLevels()
        {
            string[] expected =
                new string[] { "Critical", "Low", "Medium", "High", "Top" };
            for (int level = 1; level <= expected.Length; level++)
            {
                AssertEqual(
                    expected[level - 1],
                    VaderBatteryTray.BatteryLevelPresentation.Text(level),
                    "qualitative level text " + level.ToString());
            }

            AssertEqual(
                1,
                VaderBatteryTray.BatteryLevelPresentation.FromControllerLevel(0),
                "controller lowest level is Critical");
            AssertEqual(
                5,
                VaderBatteryTray.BatteryLevelPresentation.FromControllerLevel(4),
                "controller upper level is Top");
            AssertEqual(
                5,
                VaderBatteryTray.BatteryLevelPresentation.FromDockState(0x05),
                "Dock 0x05 is Top");
            AssertEqual(
                5,
                VaderBatteryTray.BatteryLevelPresentation.FromDockState(0x06),
                "Dock 0x06 remains Top while charging is separate");
        }

        private static void TestTransientUnavailablePublicationPolicy()
        {
            DateTime now =
                new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
            VaderBatteryTray.TransientUnavailablePublicationPolicy policy =
                new VaderBatteryTray.TransientUnavailablePublicationPolicy();

            AssertBoolean(
                true,
                policy.ShouldDefer(true, true, now),
                "first ambiguous transition is deferred");
            AssertBoolean(
                true,
                policy.ShouldDefer(true, true, now.AddSeconds(3)),
                "ambiguous transition remains deferred inside grace");
            AssertBoolean(
                false,
                policy.ShouldDefer(true, true, now.AddSeconds(4)),
                "persistent unavailable is published after grace");
            AssertBoolean(
                false,
                policy.ShouldDefer(true, true, now.AddSeconds(5)),
                "expired grace does not restart while state remains ambiguous");
            AssertBoolean(
                false,
                policy.ShouldDefer(true, false, now.AddSeconds(6)),
                "startup unavailable is not deferred");
            AssertBoolean(
                false,
                policy.ShouldDefer(false, true, now.AddSeconds(7)),
                "settled state resets transition grace");
            AssertBoolean(
                true,
                policy.ShouldDefer(true, true, now.AddSeconds(8)),
                "a later transition receives a new grace");
        }

        private static void TestCriticalPublicationPolicy()
        {
            DateTime now =
                new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
            VaderBatteryTray.CriticalPublicationPolicy policy =
                new VaderBatteryTray.CriticalPublicationPolicy();

            AssertBoolean(
                true,
                policy.ShouldDefer(true, true, now),
                "first Critical candidate is deferred");
            AssertBoolean(
                true,
                policy.ShouldDefer(true, true, now.AddSeconds(2)),
                "repeated Critical inside stability period remains deferred");
            AssertBoolean(
                false,
                policy.ShouldDefer(true, true, now.AddSeconds(4)),
                "stable Critical is accepted after bounded delay");
            AssertBoolean(
                false,
                policy.ShouldDefer(false, true, now.AddSeconds(5)),
                "non-Critical reading clears pending stability");
            AssertBoolean(
                false,
                policy.ShouldDefer(true, false, now.AddSeconds(6)),
                "already Critical presentation is not deferred");
            policy.Reset();
            AssertBoolean(
                true,
                policy.ShouldDefer(true, true, now.AddSeconds(7)),
                "explicit reset starts a new stability window");
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
            AssertBoolean(false, ambiguous.Available, "field-9-cleared inactive 0x06 unavailable");

            VaderBatteryTray.DockBatteryDecision presentFull =
                ambiguousTracker.Process(0, 0x06, 1, now.AddMilliseconds(500));
            AssertBoolean(false, presentFull.Available, "field 9 does not make inactive 0x06 available");

            VaderBatteryTray.DockBatteryStateTracker tracker =
                new VaderBatteryTray.DockBatteryStateTracker(null);
            tracker.Process(1, 0x06, 1, now);
            tracker.Process(1, 0x06, 1, now.AddMilliseconds(500));
            VaderBatteryTray.DockBatteryDecision full =
                tracker.Process(0, 0x06, 1, now.AddSeconds(1));
            AssertBoolean(true, full.Available, "active-to-inactive 0x06 available");
            AssertBoolean(true, full.IsFull, "active-to-inactive 0x06 Full");
            AssertBoolean(false, full.IsCharging, "Full not charging");
            AssertEqual(100, full.Percent, "Full percent");
            AssertEqual(5, full.BandLevel, "Full uses Top level");

            VaderBatteryTray.DockBatteryDecision retainedFull =
                tracker.Process(0, 0x06, 1, now.AddSeconds(2));
            AssertBoolean(true, retainedFull.Available, "repeated inactive 0x06 remains available");
            AssertBoolean(true, retainedFull.IsFull, "repeated inactive 0x06 retains Full");
            AssertEqual(100, retainedFull.Percent, "retained Full percent");

            VaderBatteryTray.DockBatteryDecision topOffPending =
                tracker.Process(1, 0x06, 1, now.AddSeconds(3));
            AssertBoolean(true, topOffPending.Available, "confirmed Full remains available during top-off");
            AssertEqual(100, topOffPending.Percent, "top-off keeps 100 percent");
            AssertBoolean(true, topOffPending.IsCharging, "top-off reports charging");

            VaderBatteryTray.DockBatteryDecision topOff =
                tracker.Process(1, 0x06, 1, now.AddSeconds(4));
            AssertBoolean(true, topOff.Available, "confirmed top-off remains available");
            AssertEqual(100, topOff.Percent, "confirmed top-off remains at 100");

            VaderBatteryTray.DockBatteryDecision completedAgain =
                tracker.Process(0, 0x06, 1, now.AddSeconds(5));
            AssertBoolean(true, completedAgain.IsFull, "top-off completion returns to Full");

            VaderBatteryTray.DockBatteryDecision removed =
                tracker.Process(0, 0x06, 0, now.AddSeconds(6));
            AssertBoolean(false, removed.Available, "cleared inactive field 9 invalidates retained Full");
            AssertBoolean(false, removed.IsFull, "removed controller is not Full");

            VaderBatteryTray.DockBatteryStateTracker redockTracker =
                new VaderBatteryTray.DockBatteryStateTracker(null);
            redockTracker.Process(1, 0x01, 1, now.AddSeconds(2));
            redockTracker.Process(1, 0x01, 1, now.AddSeconds(3));
            VaderBatteryTray.DockBatteryDecision redockedFull =
                redockTracker.Process(0, 0x01, 1, now.AddSeconds(4));
            AssertBoolean(false, redockedFull.Available, "inactive 0x01 remains ambiguous");
        }

        private static void TestDockRuntimeRestore()
        {
            DateTime now = new DateTime(2026, 7, 25, 20, 0, 0, DateTimeKind.Utc);
            MemoryDockRuntimeStateStore store = new MemoryDockRuntimeStateStore();
            store.State.LastRawFlag = 1;
            store.State.LastRawState = 0x06;
            store.State.LastField9 = 1;
            store.State.LastActiveState = 0x06;
            store.State.LastActiveUtc = now.AddMinutes(-10);
            store.State.FullConfirmed = true;
            store.State.FullConfirmedUtc = now.AddMinutes(-10);

            VaderBatteryTray.DockBatteryStateTracker tracker =
                new VaderBatteryTray.DockBatteryStateTracker(store);
            VaderBatteryTray.DockBatteryDecision restored =
                tracker.Process(0, 0x06, 1, now);
            AssertBoolean(true, restored.Available, "recent confirmed Full restores after restart");
            AssertBoolean(true, restored.IsFull, "restored state is Full");
            AssertEqual(100, restored.Percent, "restored Full percent");

            VaderBatteryTray.DockBatteryDecision expired =
                tracker.Process(0, 0x06, 0, now.AddHours(13));
            AssertBoolean(false, expired.Available, "expired persisted Full becomes unavailable");

            VaderBatteryTray.DockBatteryDecision contradiction =
                tracker.Process(1, 0x03, 1, now.AddHours(13).AddSeconds(1));
            AssertBoolean(false, contradiction.IsFull, "active state invalidates restored Full");
            AssertBoolean(false, contradiction.Available, "new active state awaits confirmation");
            AssertBoolean(false, store.State.FullConfirmed, "contradiction clears persisted Full");
        }

        private static void TestDockTransientStateFiltering()
        {
            DateTime now = new DateTime(2026, 7, 27, 10, 37, 49, DateTimeKind.Utc);
            VaderBatteryTray.DockBatteryStateTracker tracker =
                new VaderBatteryTray.DockBatteryStateTracker(null);

            VaderBatteryTray.DockBatteryDecision transient =
                tracker.Process(1, 0x01, 1, now);
            AssertBoolean(false, transient.Available, "isolated active 0x01 is not published");

            VaderBatteryTray.DockBatteryDecision replacement =
                tracker.Process(1, 0x04, 1, now.AddMilliseconds(990));
            AssertBoolean(false, replacement.Available, "replacement 0x04 also awaits confirmation");

            VaderBatteryTray.DockBatteryDecision stable =
                tracker.Process(1, 0x04, 1, now.AddMilliseconds(1100));
            AssertBoolean(true, stable.Available, "repeated 0x04 is published");
            AssertEqual(55, stable.Percent, "stable 0x04 display value");
            AssertEqual(4, stable.BandLevel, "stable 0x04 is High");

            VaderBatteryTray.DockBatteryDecision field9Changed =
                tracker.Process(1, 0x04, 0, now.AddMilliseconds(1200));
            AssertBoolean(true, field9Changed.Available, "field 9 does not control availability");
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

        private static void AssertEqual(string expected, string actual, string name)
        {
            if (!String.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(name + " mismatch: expected " + expected + ", got " + actual);
            }
        }
    }
}
