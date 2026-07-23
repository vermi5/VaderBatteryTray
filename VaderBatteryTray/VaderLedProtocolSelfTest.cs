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
