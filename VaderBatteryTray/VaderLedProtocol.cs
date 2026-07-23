using System;

namespace VaderBatteryTray
{
    internal static class VaderLedProtocol
    {
        private const int ReportLength = 32;
        private const int ChecksumIndex = 25;
        private const byte Magic1 = 0x5A;
        private const byte Magic2 = 0xA5;
        private const byte PreludeCommand = 0xA8;
        private const byte DataCommand = 0xA9;
        private const byte DataLength = 0x17;
        private const int ZoneCount = 10;
        private const int BytesPerZone = 3;
        private const byte SolidMode = 0x05;
        private const byte OffMode = 0x06;

        public static byte[][] BuildSolid(byte red, byte green, byte blue, byte brightnessPercent)
        {
            return BuildSequence(red, green, blue, brightnessPercent, SolidMode);
        }

        public static byte[][] BuildOff(byte brightnessPercent)
        {
            return BuildSequence(0, 0, 0, brightnessPercent, OffMode);
        }

        private static byte[][] BuildSequence(
            byte red,
            byte green,
            byte blue,
            byte brightnessPercent,
            byte mode)
        {
            if (brightnessPercent > 100)
            {
                throw new ArgumentOutOfRangeException("brightnessPercent");
            }

            byte[] prelude = new byte[ReportLength];
            prelude[0] = Magic1;
            prelude[1] = Magic2;
            prelude[2] = PreludeCommand;
            prelude[3] = 0x06;
            prelude[4] = 0x00;
            prelude[5] = 0x00;
            prelude[6] = 0x03;
            prelude[7] = 0x14;
            prelude[8] = 0xC5;

            byte[] config = NewDataReport(0);
            config[5] = 0x00;
            config[6] = 0x03;
            config[7] = 0x00;
            config[8] = 0x00;
            config[9] = 0x00;
            config[10] = 0x01;
            config[11] = brightnessPercent;
            config[12] = ZoneCount;
            config[13] = mode;
            config[14] = 0x00;
            for (int index = 15; index <= 24; index++)
            {
                config[index] = 0xFF;
            }
            SetChecksum(config);

            byte[] zoneBytes = new byte[ZoneCount * BytesPerZone];
            for (int index = 0; index < ZoneCount; index++)
            {
                int offset = index * BytesPerZone;
                zoneBytes[offset] = red;
                zoneBytes[offset + 1] = green;
                zoneBytes[offset + 2] = blue;
            }

            byte[] firstColors = NewDataReport(1);
            Array.Copy(zoneBytes, 0, firstColors, 5, 20);
            SetChecksum(firstColors);

            byte[] secondColors = NewDataReport(2);
            Array.Copy(zoneBytes, 20, secondColors, 5, 10);
            SetChecksum(secondColors);

            return new byte[][] { prelude, config, firstColors, secondColors };
        }

        private static byte[] NewDataReport(byte sequence)
        {
            byte[] report = new byte[ReportLength];
            report[0] = Magic1;
            report[1] = Magic2;
            report[2] = DataCommand;
            report[3] = DataLength;
            report[4] = sequence;
            return report;
        }

        private static void SetChecksum(byte[] report)
        {
            int sum = 0;
            for (int index = 0; index < ChecksumIndex; index++)
            {
                sum += report[index];
            }
            report[ChecksumIndex] = (byte)((sum + 1) & 0xFF);
        }
    }
}
