using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace VaderBatteryTray
{
    internal sealed class VaderLedController
    {
        private const ushort TargetVendorId = 0x37D7;
        private const ushort TargetProductId = 0x2401;
        private const ushort TargetUsagePage = 0xFFA0;
        private const ushort TargetUsage = 0x0001;
        private const int AckTimeoutMs = 1500;
        private const int InterReportDelayMs = 60;

        private readonly VaderLedSettings settings;
        private string lastSignature;
        private string lastError;

        public VaderLedController()
        {
            settings = VaderLedSettings.Load();
        }

        public bool Enabled
        {
            get { return settings.ControlEnabled; }
        }

        public byte BrightnessPercent
        {
            get { return settings.BrightnessPercent; }
        }

        public bool ControlManagedByEnvironment
        {
            get { return settings.ControlManagedByEnvironment; }
        }

        public bool BrightnessManagedByEnvironment
        {
            get { return settings.BrightnessManagedByEnvironment; }
        }

        public bool WarningAccepted
        {
            get { return settings.WarningAccepted; }
        }

        public string Status
        {
            get
            {
                string state = Enabled
                    ? "enabled at " + BrightnessPercent.ToString() + "%"
                    : "disabled";
                if (ControlManagedByEnvironment || BrightnessManagedByEnvironment)
                {
                    state += " (environment override)";
                }
                return String.IsNullOrEmpty(lastError) ? state : state + "; last error: " + lastError;
            }
        }

        public bool TrySetEnabled(bool value, out string error)
        {
            if (!settings.TrySetControlEnabled(value, out error))
            {
                return false;
            }

            lastSignature = null;
            lastError = null;
            return true;
        }

        public bool TrySetBrightness(byte value, out string error)
        {
            if (!settings.TrySetBrightness(value, out error))
            {
                return false;
            }

            lastSignature = null;
            lastError = null;
            return true;
        }

        public bool TryAcceptWarning(out string error)
        {
            return settings.TryAcceptWarning(out error);
        }

        public bool TryResetUserSettings(out string error)
        {
            if (!settings.TryResetUserSettings(out error))
            {
                return false;
            }

            lastSignature = null;
            lastError = null;
            return true;
        }

        public void ApplySnapshot(BatterySnapshot snapshot)
        {
            ApplySnapshot(snapshot, BrightnessPercent);
        }

        public void PreviewBrightness(BatterySnapshot snapshot, byte brightnessPercent)
        {
            ApplySnapshot(snapshot, brightnessPercent);
        }

        private void ApplySnapshot(BatterySnapshot snapshot, byte brightnessPercent)
        {
            if (!Enabled)
            {
                return;
            }

            if (snapshot == null ||
                !snapshot.InterfacePresent ||
                !snapshot.HasLiveControllerSession)
            {
                // The controller loses its volatile lighting state while it is
                // asleep/off. Forget the cached command so the same color and
                // brightness are sent again after the next valid GET_INFO.
                lastSignature = null;
                return;
            }

            if (VaderLedPolicy.ShouldPreserveNativeLowBatteryAlert(
                snapshot.HasLiveControllerSession,
                snapshot.HasBattery,
                snapshot.Percent,
                snapshot.IsCharging,
                snapshot.RawGetInfoStatusNibble))
            {
                // The controller firmware owns the faster red low-battery pulse
                // while the controller is awake. Forget our previous color so it
                // is reapplied after the controller leaves the warning band.
                lastSignature = null;
                return;
            }

            byte red;
            byte green;
            byte blue;
            if (!TryGetColor(snapshot, out red, out green, out blue))
            {
                return;
            }

            string signature = red.ToString("X2") + green.ToString("X2") + blue.ToString("X2") + ":" + brightnessPercent.ToString();
            if (String.Equals(lastSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                byte[][] reports = VaderLedProtocol.BuildSolid(red, green, blue, brightnessPercent);
                SendReportsReopenEach(FindTargetInterface(), reports);
                lastSignature = signature;
                lastError = null;
            }
            catch (Exception ex)
            {
                lastError = ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static bool TryGetColor(BatterySnapshot snapshot, out byte red, out byte green, out byte blue)
        {
            int level = snapshot.BandLevel;
            if (level <= 0 && snapshot.Percent >= 0)
            {
                level = snapshot.Percent <= 33 ? 1 : (snapshot.Percent <= 66 ? 2 : 3);
            }

            switch (level)
            {
                case 1:
                    red = 252;
                    green = 1;
                    blue = 1;
                    return true;
                case 2:
                    red = 255;
                    green = 255;
                    blue = 0;
                    return true;
                case 3:
                case 4:
                    red = 51;
                    green = 153;
                    blue = 255;
                    return true;
                default:
                    red = 0;
                    green = 0;
                    blue = 0;
                    return false;
            }
        }

        private static HidDeviceInfo FindTargetInterface()
        {
            List<HidDeviceInfo> devices = HidEnumerator.Enumerate();
            foreach (HidDeviceInfo device in devices)
            {
                if (device.IsTarget(TargetVendorId, TargetProductId) &&
                    device.HasCaps &&
                    device.UsagePage == TargetUsagePage &&
                    device.Usage == TargetUsage &&
                    device.InputReportByteLength > 0 &&
                    device.OutputReportByteLength > 0)
                {
                    return device;
                }
            }

            throw new InvalidOperationException("No 0xFFA0/0x0001 Vader LED interface found.");
        }

        private static void SendReportsReopenEach(HidDeviceInfo target, byte[][] reports)
        {
            if (target == null)
            {
                throw new InvalidOperationException("Vader LED interface is unavailable.");
            }

            for (int index = 0; index < reports.Length; index++)
            {
                using (SafeFileHandle handle = Native.CreateFile(
                    target.Path,
                    Native.GENERIC_READ | Native.GENERIC_WRITE,
                    Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    Native.OPEN_EXISTING,
                    Native.FILE_FLAG_OVERLAPPED,
                    IntPtr.Zero))
                {
                    if (handle.IsInvalid)
                    {
                        throw new InvalidOperationException("Vader LED CreateFile failed: " + Native.LastErrorText());
                    }

                    int outputLength = Math.Max(33, (int)target.OutputReportByteLength);
                    int inputLength = Math.Max(33, (int)target.InputReportByteLength);
                    using (FileStream stream = new FileStream(handle, FileAccess.ReadWrite, Math.Max(outputLength, inputLength), true))
                    {
                        byte[] report = reports[index];
                        byte[] writeBuffer = new byte[outputLength];
                        writeBuffer[0] = 0x00;
                        Array.Copy(report, 0, writeBuffer, 1, Math.Min(report.Length, writeBuffer.Length - 1));
                        stream.Write(writeBuffer, 0, writeBuffer.Length);
                        stream.Flush();

                        byte[] ack = ReadAck(stream, inputLength, report, AckTimeoutMs);
                        if (ack == null)
                        {
                            throw new TimeoutException("No matching Vader LED ACK for report " + index.ToString() + ".");
                        }
                    }
                }

                Thread.Sleep(InterReportDelayMs);
            }
        }

        private static byte[] ReadAck(FileStream stream, int inputLength, byte[] sent, int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                byte[] buffer = new byte[inputLength];
                IAsyncResult asyncResult = stream.BeginRead(buffer, 0, buffer.Length, null, null);
                while (!asyncResult.IsCompleted && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(5);
                }
                if (!asyncResult.IsCompleted)
                {
                    return null;
                }

                int bytesRead = stream.EndRead(asyncResult);
                if (bytesRead <= 0)
                {
                    continue;
                }

                if (bytesRead < buffer.Length)
                {
                    byte[] resized = new byte[bytesRead];
                    Array.Copy(buffer, resized, bytesRead);
                    buffer = resized;
                }

                int offset = buffer.Length > 0 && buffer[0] == 0x5A ? 0 : 1;
                if (buffer.Length < offset + 4 ||
                    buffer[offset] != 0x5A ||
                    buffer[offset + 1] != 0xA5 ||
                    buffer[offset + 2] != sent[2])
                {
                    continue;
                }

                if (sent[2] == 0xA9)
                {
                    if (buffer.Length > offset + 5 &&
                        buffer[offset + 3] == 0x01 &&
                        buffer[offset + 5] == sent[4])
                    {
                        return buffer;
                    }
                }
                else if (sent[2] == 0xA8 && buffer[offset + 3] == 0x01)
                {
                    return buffer;
                }
                else if (sent[2] != 0xA8)
                {
                    return buffer;
                }
            }

            return null;
        }
    }
}
