using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace VaderBatteryTray
{
    internal static class DiagnosticLogger
    {
        private const long MaximumLogSize = 5L * 1024L * 1024L;
        private static readonly object Sync = new object();
        private static readonly bool Enabled = String.Equals(
            Environment.GetEnvironmentVariable("VADERBATTERYTRAY_DIAGNOSTIC"),
            "1",
            StringComparison.Ordinal);

        public static void LogSnapshot(
            BatterySnapshot snapshot,
            string redactedDeviceId,
            int? attempt,
            string result)
        {
            if (!Enabled || snapshot == null)
            {
                return;
            }

            try
            {
                lock (Sync)
                {
                    string directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "VaderBatteryTray");
                    string currentPath = Path.Combine(directory, "diagnostics.log");
                    string previousPath = Path.Combine(directory, "diagnostics.previous.log");

                    Directory.CreateDirectory(directory);
                    RotateIfNeeded(currentPath, previousPath);

                    string timestamp = snapshot.UtcObservationTimestamp == DateTime.MinValue
                        ? "-"
                        : snapshot.UtcObservationTimestamp.ToString(
                            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                            CultureInfo.InvariantCulture);

                    StringBuilder line = new StringBuilder(512);
                    Append(line, "TimestampUtc", timestamp);
                    Append(line, "Attempt", attempt.HasValue
                        ? attempt.Value.ToString(CultureInfo.InvariantCulture)
                        : "-");
                    Append(line, "Device", Clean(redactedDeviceId));
                    Append(line, "Transport", snapshot.Transport.ToString());
                    Append(line, "DataSource", snapshot.DataSource.ToString());
                    Append(line, "BandLevel", snapshot.BandLevel.ToString(CultureInfo.InvariantCulture));
                    Append(line, "Band", Clean(snapshot.BandText));
                    Append(line, "HasBattery", snapshot.HasBattery.ToString());
                    Append(line, "HasBatteryBand", snapshot.HasBatteryBand.ToString());
                    Append(line, "PowerState", snapshot.PowerState.ToString());
                    Append(line, "RawGetInfoStatusNibble", FormatNullableByte(snapshot.RawGetInfoStatusNibble));
                    Append(line, "RawGetInfoLevelNibble", FormatNullableByte(snapshot.RawGetInfoLevelNibble));
                    Append(line, "RawDockFlag", FormatNullableByte(snapshot.RawDockFlag));
                    Append(line, "RawDockState", snapshot.RawDockState.HasValue
                        ? "0x" + snapshot.RawDockState.Value.ToString("X2", CultureInfo.InvariantCulture)
                        : "-");
                    Append(line, "RawDockField9", FormatNullableByte(snapshot.RawDockField9));
                    Append(line, "RawGetInfoHex", Clean(snapshot.RawReplyHex));
                    Append(line, "RawDockEfHex", Clean(snapshot.RawDockReportHex));
                    Append(line, "Result", Clean(result));

                    File.AppendAllText(
                        currentPath,
                        line.ToString() + Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // Diagnostics must never affect battery reads or application stability.
            }
        }

        private static void RotateIfNeeded(string currentPath, string previousPath)
        {
            if (!File.Exists(currentPath))
            {
                return;
            }

            FileInfo current = new FileInfo(currentPath);
            if (current.Length < MaximumLogSize)
            {
                return;
            }

            if (File.Exists(previousPath))
            {
                File.Delete(previousPath);
            }

            File.Move(currentPath, previousPath);
        }

        private static string FormatNullableByte(byte? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
        }

        private static void Append(StringBuilder line, string name, string value)
        {
            if (line.Length > 0)
            {
                line.Append('\t');
            }

            line.Append(name);
            line.Append('=');
            line.Append(String.IsNullOrEmpty(value) ? "-" : value);
        }

        private static string Clean(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return "-";
            }

            return value
                .Replace('\t', ' ')
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        }
    }
}
