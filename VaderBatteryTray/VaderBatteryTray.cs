using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Vader Battery Tray")]
[assembly: System.Reflection.AssemblyDescription("HID battery monitor for Flydigi Vader controllers")]
[assembly: System.Reflection.AssemblyCompany("Open source utility")]
[assembly: System.Reflection.AssemblyProduct("Vader Battery Tray")]
[assembly: System.Reflection.AssemblyCopyright("2026")]
[assembly: System.Reflection.AssemblyVersion("1.1.9.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.9.0")]

namespace VaderBatteryTray
{
    internal enum BatteryTransport
    {
        Unknown,
        Usb,
        Bluetooth,
        Dock,
        Dongle
    }

    internal enum BatteryPowerState
    {
        Unknown,
        Discharging,
        Charging,
        Charged
    }

    internal enum BatteryDataSource
    {
        Unknown,
        GetInfo,
        DockEfBand
    }
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\VaderBatteryTray_28F6D68B", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    Application.Run(new TrayApplicationContext());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Vader Battery Tray could not start.\r\n\r\n" + ex.Message,
                        "Vader Battery Tray",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                GC.KeepAlive(mutex);
            }
        }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private const int RefreshIntervalMs = 120000;

        private readonly NotifyIcon notifyIcon;
        private readonly ToolStripMenuItem statusMenuItem;
        private readonly ToolStripMenuItem ledControlMenuItem;
        private readonly ToolStripMenuItem ledBrightnessMenuItem;
        private readonly ToolStripMenuItem ledSettingsStatusMenuItem;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly System.Windows.Forms.Timer commandTimer;
        private readonly HidBatteryReader reader;
        private readonly VaderLedController ledController;
        private readonly SharedBatteryState sharedState;
        private readonly RainmeterBridge rainmeterBridge;

        private Icon currentIcon;
        private int lastPercent = -2;
        private int lastBandLevel = -1;
        private string lastTooltip = String.Empty;
        private bool lastCharging;
        private bool lastConnected;
        private BatterySnapshot lastSnapshot;
        private int refreshRequested;

        public TrayApplicationContext()
        {
            reader = new HidBatteryReader();
            ledController = new VaderLedController();
            sharedState = new SharedBatteryState();
            rainmeterBridge = new RainmeterBridge(sharedState, RequestRefresh);
            rainmeterBridge.Start();

            notifyIcon = new NotifyIcon();
            notifyIcon.Text = "Vader 5 Pro | Starting...";

            statusMenuItem = new ToolStripMenuItem("Starting...");
            statusMenuItem.Enabled = false;

            ToolStripMenuItem refreshItem = new ToolStripMenuItem("Refresh now");
            refreshItem.Click += delegate { RefreshStatus(); };

            ToolStripMenuItem diagnosticsItem = new ToolStripMenuItem("Copy diagnostics");
            diagnosticsItem.Click += delegate { CopyDiagnostics(); };

            ToolStripMenuItem lightingMenuItem = new ToolStripMenuItem("Controller lighting");

            ledControlMenuItem = new ToolStripMenuItem("Sync color with battery");
            ledControlMenuItem.Click += delegate { ToggleLedControl(); };

            ledBrightnessMenuItem = new ToolStripMenuItem("Brightness...");
            ledBrightnessMenuItem.Click += delegate { ShowLedBrightnessDialog(); };

            ToolStripMenuItem resetLightingItem = new ToolStripMenuItem("Reset saved settings");
            resetLightingItem.Click += delegate { ResetLedSettings(); };

            ledSettingsStatusMenuItem = new ToolStripMenuItem();
            ledSettingsStatusMenuItem.Enabled = false;

            lightingMenuItem.DropDownItems.Add(ledControlMenuItem);
            lightingMenuItem.DropDownItems.Add(ledBrightnessMenuItem);
            lightingMenuItem.DropDownItems.Add(new ToolStripSeparator());
            lightingMenuItem.DropDownItems.Add(resetLightingItem);
            lightingMenuItem.DropDownItems.Add(ledSettingsStatusMenuItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate { ExitThread(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(statusMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(refreshItem);
            menu.Items.Add(diagnosticsItem);
            menu.Items.Add(lightingMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            notifyIcon.ContextMenuStrip = menu;
            UpdateLedMenuState();

            SetTrayVisual(-1, 0, false, false, "Vader 5 Pro | Starting...");
            notifyIcon.Visible = true;

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = RefreshIntervalMs;
            refreshTimer.Tick += delegate { RefreshStatus(); };
            refreshTimer.Start();

            commandTimer = new System.Windows.Forms.Timer();
            commandTimer.Interval = 250;
            commandTimer.Tick += delegate
            {
                if (Interlocked.Exchange(ref refreshRequested, 0) != 0)
                {
                    RefreshStatus();
                }
            };
            commandTimer.Start();

            RefreshStatus();
        }

        private void RequestRefresh()
        {
            Interlocked.Exchange(ref refreshRequested, 1);
        }

        private void ToggleLedControl()
        {
            bool enable = !ledController.Enabled;
            if (enable && !ledController.WarningAccepted)
            {
                DialogResult result = MessageBox.Show(
                    "Direct controller lighting sends experimental HID commands to the controller.\r\n\r\n" +
                    "It remains disabled while the Dock owns the lighting. Continue?",
                    "Enable controller lighting",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                {
                    return;
                }

                string warningError;
                if (!ledController.TryAcceptWarning(out warningError))
                {
                    ShowLedSettingsError(warningError);
                    return;
                }
            }

            string error;
            if (!ledController.TrySetEnabled(enable, out error))
            {
                ShowLedSettingsError(error);
                return;
            }

            UpdateLedMenuState();
            if (enable)
            {
                ApplyLedSettingsToLastSnapshot();
            }
        }

        private void ShowLedBrightnessDialog()
        {
            using (VaderLedSettingsForm form = new VaderLedSettingsForm(
                ledController.BrightnessPercent,
                delegate(byte value)
                {
                    if (lastSnapshot != null)
                    {
                        ledController.PreviewBrightness(lastSnapshot, value);
                    }
                }))
            {
                if (form.ShowDialog() != DialogResult.OK)
                {
                    ApplyLedSettingsToLastSnapshot();
                    return;
                }

                string error;
                if (!ledController.TrySetBrightness(form.BrightnessPercent, out error))
                {
                    ShowLedSettingsError(error);
                    return;
                }
            }

            UpdateLedMenuState();
            ApplyLedSettingsToLastSnapshot();
        }

        private void ResetLedSettings()
        {
            string error;
            if (!ledController.TryResetUserSettings(out error))
            {
                ShowLedSettingsError(error);
                return;
            }

            UpdateLedMenuState();
            ApplyLedSettingsToLastSnapshot();
        }

        private void ApplyLedSettingsToLastSnapshot()
        {
            if (lastSnapshot != null)
            {
                ledController.ApplySnapshot(lastSnapshot);
            }
        }

        private void UpdateLedMenuState()
        {
            ledControlMenuItem.Checked = ledController.Enabled;
            ledControlMenuItem.Enabled = !ledController.ControlManagedByEnvironment;
            ledControlMenuItem.Text = ledController.ControlManagedByEnvironment
                ? "Sync color with battery (environment)"
                : "Sync color with battery";

            ledBrightnessMenuItem.Enabled = !ledController.BrightnessManagedByEnvironment;
            ledBrightnessMenuItem.Text = ledController.BrightnessManagedByEnvironment
                ? "Brightness: " + ledController.BrightnessPercent.ToString() + "% (environment)"
                : "Brightness... (" + ledController.BrightnessPercent.ToString() + "%)";

            ledSettingsStatusMenuItem.Text = "Status: " + ledController.Status;
        }

        private static void ShowLedSettingsError(string error)
        {
            MessageBox.Show(
                "The controller lighting setting could not be saved.\r\n\r\n" + error,
                "Controller lighting",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void RefreshStatus()
        {
            try
            {
                BatterySnapshot snapshot = reader.ReadBattery();
                lastSnapshot = snapshot;
                sharedState.Publish(snapshot);
                ledController.ApplySnapshot(snapshot);

                if (snapshot.HasBattery)
                {
                    string tooltip = "Vader 5 Pro | " + snapshot.Percent.ToString() + "% | " + snapshot.PowerText + " | " + snapshot.ConnectionText;
                    string menuText = "Vader 5 Pro: " + snapshot.Percent.ToString() + "% - " + snapshot.PowerText + " - " + snapshot.ConnectionText;
                    statusMenuItem.Text = LimitText(menuText, 120);
                    SetTrayVisual(snapshot.Percent, snapshot.BandLevel, snapshot.IsCharging, true, LimitText(tooltip, 63));
                }
                else if (snapshot.HasBatteryBand)
                {
                    string batteryText = snapshot.Percent >= 0 ? "~" + snapshot.Percent.ToString() + "%" : snapshot.BandText;
                    string tooltip = "Vader 5 Pro | " + batteryText + " | " + snapshot.PowerText + " | " + snapshot.ConnectionText;
                    string menuText = "Vader 5 Pro: " + batteryText + " - " + snapshot.PowerText + " - " + snapshot.ConnectionText;
                    statusMenuItem.Text = LimitText(menuText, 120);
                    SetTrayVisual(snapshot.Percent >= 0 ? snapshot.Percent : -1, snapshot.BandLevel, snapshot.IsCharging, true, LimitText(tooltip, 63));
                }
                else if (snapshot.InterfacePresent)
                {
                    statusMenuItem.Text = LimitText("Vader 5 Pro: battery unavailable - " + snapshot.Error, 120);
                    string tooltip = "Vader 5 Pro | Battery unavailable";
                    if (!String.IsNullOrEmpty(snapshot.PowerText))
                    {
                        tooltip += " | " + snapshot.PowerText;
                    }
                    if (!String.IsNullOrEmpty(snapshot.ConnectionText))
                    {
                        tooltip += " | " + snapshot.ConnectionText;
                    }
                    SetTrayVisual(-1, 0, snapshot.IsCharging, true, LimitText(tooltip, 63));
                }
                else
                {
                    statusMenuItem.Text = "Vader 5 Pro: HID interface not found";
                    SetTrayVisual(-1, 0, false, false, "Vader 5 Pro | Not connected");
                }
            }
            catch (Exception ex)
            {
                lastSnapshot = BatterySnapshot.Unavailable("Reader error: " + ex.Message, false);
                sharedState.Publish(lastSnapshot);
                statusMenuItem.Text = LimitText("HID reader error: " + ex.Message, 120);
                SetTrayVisual(-1, 0, false, false, "Vader 5 Pro | Battery unavailable");
            }
        }

        private void CopyDiagnostics()
        {
            try
            {
                StringBuilder text = new StringBuilder();
                text.AppendLine("Vader Battery Tray 1.1.9 diagnostics");
                text.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine("OS: " + Environment.OSVersion);
                text.AppendLine("64-bit process: " + Environment.Is64BitProcess);
                text.AppendLine("Refresh interval: " + RefreshIntervalMs.ToString() + " ms");
                text.AppendLine("Controller polling: enabled");
                text.AppendLine("Direct controller LED control: " + ledController.Status);
                text.AppendLine("Direct LED policy: suppressed while the snapshot is dock-owned");
                text.AppendLine("Rainmeter bridge: " + (rainmeterBridge.IsRunning ? RainmeterBridge.StateUrl : "unavailable"));
                if (!String.IsNullOrEmpty(rainmeterBridge.StartError))
                {
                    text.AppendLine("Rainmeter bridge error: " + rainmeterBridge.StartError);
                }
                text.AppendLine("Battery source: Flydigi HID V2 GET_INFO on VID 0x37D7 / PID 0x2401 / usage 0xFFA0:0x0001");
                text.AppendLine("Dock fallback: Flydigi Dock 2 EF band on VID 0x37D7 / PID 0x6001 / usage 0xFFA0:0x0001");
                text.AppendLine("Generic XInput battery values are not used.");
                text.AppendLine();

                BatterySnapshot snapshot = reader.ReadBattery();
                lastSnapshot = snapshot;
                sharedState.Publish(snapshot);
                text.AppendLine("Last query:");
                text.AppendLine("    Interface present: " + snapshot.InterfacePresent);
                text.AppendLine("    Has battery: " + snapshot.HasBattery);
                text.AppendLine("    Percent: " + (snapshot.HasBattery ? snapshot.Percent.ToString() : (snapshot.HasBatteryBand && snapshot.Percent >= 0 ? snapshot.Percent + " (approximate)" : "(unavailable)")));
                text.AppendLine("    Has battery band: " + snapshot.HasBatteryBand);
                text.AppendLine("    Battery band: " + EmptyMarker(snapshot.BandText));
                text.AppendLine("    Power: " + EmptyMarker(snapshot.PowerText));
                text.AppendLine("    Connection: " + EmptyMarker(snapshot.ConnectionText));
                text.AppendLine("    Device ID: " + EmptyMarker(snapshot.DeviceId));
                text.AppendLine("    Firmware: " + EmptyMarker(snapshot.Firmware));
                text.AppendLine("    Interface path: " + EmptyMarker(snapshot.RedactedPath));
                text.AppendLine("    Provenance: " + EmptyMarker(snapshot.Provenance));
                text.AppendLine("    Raw GET_INFO reply: " + EmptyMarker(snapshot.RawReplyHex));
                text.AppendLine("    Raw dock EF report: " + EmptyMarker(snapshot.RawDockReportHex));
                text.AppendLine("    Error: " + EmptyMarker(snapshot.Error));
                text.AppendLine();

                text.Append(reader.BuildInventoryText());

                Clipboard.SetText(text.ToString());
                notifyIcon.ShowBalloonTip(2500, "Vader Battery Tray", "Diagnostics copied to the clipboard.", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not copy diagnostics.\r\n\r\n" + ex.Message,
                    "Vader Battery Tray",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SetTrayVisual(int percent, int bandLevel, bool charging, bool connected, string tooltip)
        {
            bool visualChanged = percent != lastPercent || bandLevel != lastBandLevel || charging != lastCharging || connected != lastConnected;

            if (visualChanged || currentIcon == null)
            {
                Icon newIcon = BatteryIcon.Create(percent, bandLevel, charging, connected);
                Icon oldIcon = currentIcon;
                currentIcon = newIcon;
                notifyIcon.Icon = currentIcon;
                if (oldIcon != null)
                {
                    oldIcon.Dispose();
                }
            }

            if (!String.Equals(lastTooltip, tooltip, StringComparison.Ordinal))
            {
                notifyIcon.Text = LimitText(tooltip, 63);
                lastTooltip = tooltip;
            }

            lastPercent = percent;
            lastBandLevel = bandLevel;
            lastCharging = charging;
            lastConnected = connected;
        }

        protected override void ExitThreadCore()
        {
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer.Dispose();
            }

            if (commandTimer != null)
            {
                commandTimer.Stop();
                commandTimer.Dispose();
            }

            if (rainmeterBridge != null)
            {
                rainmeterBridge.Dispose();
            }

            if (reader != null)
            {
                reader.Dispose();
            }

            notifyIcon.Visible = false;
            notifyIcon.Dispose();

            if (currentIcon != null)
            {
                currentIcon.Dispose();
                currentIcon = null;
            }

            base.ExitThreadCore();
        }

        private static string EmptyMarker(string value)
        {
            return String.IsNullOrEmpty(value) ? "(unavailable)" : value;
        }

        private static string LimitText(string value, int maximumLength)
        {
            if (String.IsNullOrEmpty(value) || value.Length <= maximumLength)
            {
                return value;
            }
            if (maximumLength <= 3)
            {
                return value.Substring(0, maximumLength);
            }
            return value.Substring(0, maximumLength - 3) + "...";
        }
    }

    internal sealed class BatterySnapshot
    {
        public bool InterfacePresent;
        public bool HasBattery;
        public bool HasBatteryBand;
        public int Percent;
        public int BandLevel;
        public string BandText;
        public bool IsCharging;
        public string PowerText;
        public string ConnectionText;
        public string DeviceId;
        public string Firmware;
        public string RedactedPath;
        public string Provenance;
        public string RawReplyHex;
        public string RawDockReportHex;
        public string Error;

        public BatteryTransport Transport;
        public BatteryPowerState PowerState;
        public BatteryDataSource DataSource;
        public byte? RawGetInfoStatusNibble;
        public byte? RawDockFlag;
        public byte? RawDockState;
        public DateTime UtcObservationTimestamp;
        public static BatterySnapshot Unavailable(string error, bool interfacePresent)
        {
            return new BatterySnapshot
            {
                InterfacePresent = interfacePresent,
                HasBattery = false,
                Percent = -1,
                BandLevel = 0,
                BandText = String.Empty,
                PowerText = String.Empty,
                ConnectionText = String.Empty,
                Error = error
            };
        }
    }

    internal sealed class HidBatteryReader : IDisposable
    {
        private const ushort TargetVendorId = 0x37D7;
        private const ushort TargetProductId = 0x2401;
        private const ushort DockProductId = 0x6001;
        private const ushort TargetUsagePage = 0xFFA0;
        private const ushort TargetUsage = 0x0001;
        private const ushort MinimumVader5Firmware = 0x7141;
        private const byte Magic1 = 0x5A;
        private const byte Magic2 = 0xA5;
        private const byte GetInfoCommand = 0x01;
        private const int QueryTimeoutMs = 1000;
        private const int DockReadTimeoutMs = 8000;
        private const int QueryAttempts = 1;
        private readonly DockStatusMonitor dockMonitor;

        public HidBatteryReader()
        {
            dockMonitor = new DockStatusMonitor();
        }

        public void Dispose()
        {
            if (dockMonitor != null)
            {
                dockMonitor.Dispose();
            }
        }

        public BatterySnapshot ReadBattery()
        {
            BatterySnapshot last = null;
            for (int attempt = 1; attempt <= QueryAttempts; attempt++)
            {
                last = ReadBatteryOnce();
                DiagnosticLogger.LogSnapshot(
                    last,
                    last == null ? null : last.RedactedPath,
                    attempt,
                    last == null ? "null snapshot" : (String.IsNullOrEmpty(last.Error) ? "OK" : last.Error));
                if (last.HasBattery)
                {
                    if (last.Percent < 100)
                    {
                        return last;
                    }

                    // Preserve the valid 100% GET_INFO result while checking Dock 2 state.
                    break;
                }

                if (last.HasBatteryBand && last.Percent >= 0)
                {
                    return last;
                }
                if (last.Error == "Flydigi HID V2 interface not found")
                {
                    break;
                }
                if ((last.Error != null && last.Error.StartsWith("Unsupported firmware", StringComparison.Ordinal)) ||
                    (last.Error != null && last.Error.StartsWith("Unexpected Flydigi device ID", StringComparison.Ordinal)) ||
                    last.Error == "Invalid GET_INFO reply")
                {
                    return last;
                }
                Thread.Sleep(100);
            }

            BatterySnapshot monitorSnapshot = dockMonitor.WaitForSnapshot(2500);
            if (monitorSnapshot.HasBatteryBand)
            {
                return monitorSnapshot;
            }

            BatterySnapshot dock = ReadDockBatteryBand();
            if (dock.HasBatteryBand)
            {
                return dock;
            }
            if (last != null && last.HasBattery)
            {
                return last;
            }

            if (monitorSnapshot != null && monitorSnapshot.Error != null && monitorSnapshot.Error.StartsWith("Dock monitor:", StringComparison.Ordinal))
            {
                if (dock != null && !String.IsNullOrEmpty(dock.Error))
                {
                    BatterySnapshot combined = BatterySnapshot.Unavailable(monitorSnapshot.Error + "; one-shot fallback: " + dock.Error, true);
                    combined.RedactedPath = !String.IsNullOrEmpty(dock.RedactedPath) ? dock.RedactedPath : monitorSnapshot.RedactedPath;
                    return combined;
                }
                return monitorSnapshot;
            }

            return dock ?? last ?? BatterySnapshot.Unavailable("GET_INFO reply not received", true);
        }

        private BatterySnapshot ReadBatteryOnce()
        {
            HidDeviceInfo device = FindTargetInterface();
            if (device == null)
            {
                return BatterySnapshot.Unavailable("Flydigi HID V2 interface not found", false);
            }

            using (SafeFileHandle handle = Native.CreateFile(device.Path, Native.GENERIC_READ | Native.GENERIC_WRITE, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    BatterySnapshot snapshot = BatterySnapshot.Unavailable("Open read/write failed: " + Native.LastErrorText(), true);
                    snapshot.RedactedPath = device.RedactedPath;
                    return snapshot;
                }

                try
                {
                    int outputLength = Math.Max(33, (int)device.OutputReportByteLength);
                    int inputLength = Math.Max(33, (int)device.InputReportByteLength);

                    using (FileStream stream = new FileStream(handle, FileAccess.ReadWrite, Math.Max(outputLength, inputLength), true))
                    {
                        byte[] request = new byte[outputLength];
                        request[0] = 0x00;
                        request[1] = Magic1;
                        request[2] = Magic2;
                        request[3] = GetInfoCommand;
                        request[4] = 0x02;
                        request[5] = 0x00;

                        stream.Write(request, 0, request.Length);
                        stream.Flush();

                        byte[] response = ReadUntilInfoResponse(stream, inputLength);
                        if (response == null)
                        {
                            BatterySnapshot snapshot = BatterySnapshot.Unavailable("GET_INFO reply not received", true);
                            snapshot.RedactedPath = device.RedactedPath;
                            return snapshot;
                        }

                        return DecodeInfoResponse(response, device.RedactedPath);
                    }
                }
                catch (Exception ex)
                {
                    BatterySnapshot snapshot = BatterySnapshot.Unavailable("GET_INFO query failed: " + ex.Message, true);
                    snapshot.RedactedPath = device.RedactedPath;
                    return snapshot;
                }
            }
        }

        public string BuildInventoryText()
        {
            StringBuilder text = new StringBuilder();
            List<HidDeviceInfo> devices = EnumerateTargetDevices();
            text.AppendLine("HID inventory for VID 0x37D7 / PID 0x2401:");
            text.AppendLine("    Matching interfaces: " + devices.Count);
            text.AppendLine();

            for (int i = 0; i < devices.Count; i++)
            {
                HidDeviceInfo device = devices[i];
                text.AppendLine("[" + (i + 1).ToString() + "] " + device.RedactedPath);
                text.AppendLine("    Usage page / usage: " + (device.HasCaps ? String.Format("0x{0:X4} / 0x{1:X4}", device.UsagePage, device.Usage) : "(unavailable)"));
                text.AppendLine("    Report lengths: input=" + FormatLength(device.InputReportByteLength) + ", output=" + FormatLength(device.OutputReportByteLength) + ", feature=" + FormatLength(device.FeatureReportByteLength));
                text.AppendLine("    Product: " + EmptyMarker(device.Product));
                text.AppendLine("    Metadata open: " + device.MetadataOpenResult);
                text.AppendLine("    Shared read open: " + device.ReadOpenResult);
                text.AppendLine();
            }

            List<HidDeviceInfo> docks = EnumerateDevices(TargetVendorId, DockProductId);
            text.AppendLine("HID inventory for VID 0x37D7 / PID 0x6001:");
            text.AppendLine("    Matching interfaces: " + docks.Count);
            text.AppendLine();

            for (int i = 0; i < docks.Count; i++)
            {
                HidDeviceInfo device = docks[i];
                text.AppendLine("[" + (i + 1).ToString() + "] " + device.RedactedPath);
                text.AppendLine("    Usage page / usage: " + (device.HasCaps ? String.Format("0x{0:X4} / 0x{1:X4}", device.UsagePage, device.Usage) : "(unavailable)"));
                text.AppendLine("    Report lengths: input=" + FormatLength(device.InputReportByteLength) + ", output=" + FormatLength(device.OutputReportByteLength) + ", feature=" + FormatLength(device.FeatureReportByteLength));
                text.AppendLine("    Product: " + EmptyMarker(device.Product));
                text.AppendLine("    Metadata open: " + device.MetadataOpenResult);
                text.AppendLine("    Shared read open: " + device.ReadOpenResult);
                text.AppendLine();
            }

            return text.ToString();
        }

        private HidDeviceInfo FindTargetInterface()
        {
            List<HidDeviceInfo> devices = EnumerateTargetDevices();
            foreach (HidDeviceInfo device in devices)
            {
                if (device.HasCaps &&
                    device.UsagePage == TargetUsagePage &&
                    device.Usage == TargetUsage &&
                    device.InputReportByteLength > 0 &&
                    device.OutputReportByteLength > 0)
                {
                    return device;
                }
            }
            return null;
        }

        private static BatterySnapshot DecodeInfoResponse(byte[] response, string redactedPath)
        {
            DateTime observedUtc = DateTime.UtcNow;
            int offset = response[0] == Magic1 ? 0 : 1;
            if (response.Length < offset + 31 ||
                response[offset] != Magic1 ||
                response[offset + 1] != Magic2 ||
                response[offset + 2] != GetInfoCommand)
            {
                BatterySnapshot invalid = BatterySnapshot.Unavailable("Invalid GET_INFO reply", true);
                invalid.RedactedPath = redactedPath;
                return ApplyGetInfoDiagnostics(invalid, response, null, observedUtc);
            }

            byte deviceId = response[offset + 5];
            byte connection = response[offset + 6];
            ushort firmware = (ushort)(response[offset + 16] | (response[offset + 15] << 8));
            byte statusLevel = response[offset + 11];
            byte statusNibble = (byte)((statusLevel >> 4) & 0x0F);
            int status = statusNibble;
            int level = statusLevel & 0x0F;

            if (deviceId != 130)
            {
                BatterySnapshot wrongDevice = BatterySnapshot.Unavailable("Unexpected Flydigi device ID " + deviceId.ToString(), true);
                wrongDevice.RedactedPath = redactedPath;
                return ApplyGetInfoDiagnostics(wrongDevice, response, statusNibble, observedUtc);
            }

            if (firmware < MinimumVader5Firmware)
            {
                BatterySnapshot oldFirmware = BatterySnapshot.Unavailable("Unsupported firmware 0x" + firmware.ToString("X4"), true);
                oldFirmware.RedactedPath = redactedPath;
                oldFirmware.DeviceId = deviceId.ToString();
                oldFirmware.Firmware = "0x" + firmware.ToString("X4");
                return ApplyGetInfoDiagnostics(oldFirmware, response, statusNibble, observedUtc);
            }

            BatterySnapshot snapshot = new BatterySnapshot();
            snapshot.InterfacePresent = true;
            snapshot.DeviceId = deviceId.ToString();
            snapshot.Firmware = "0x" + firmware.ToString("X4");
            snapshot.ConnectionText = DecodeConnection(connection);
            snapshot.RedactedPath = redactedPath;
            snapshot.Provenance = "Flydigi V2 GET_INFO byte 11, status nibble " + status.ToString() + ", level nibble " + level.ToString();
            snapshot.RawReplyHex = Hex(response);

            if (status == 0)
            {
                snapshot.HasBattery = true;
                snapshot.Percent = Math.Max(0, Math.Min(5, level)) * 20;
                snapshot.BandLevel = ChargingBandFromLevel(level);
                snapshot.BandText = BandText(snapshot.BandLevel);
                snapshot.IsCharging = false;
                snapshot.PowerText = "Discharging";
            }
            else if (status == 1)
            {
                snapshot.HasBattery = false;
                snapshot.HasBatteryBand = true;
                snapshot.Percent = -1;
                snapshot.BandLevel = ChargingBandFromLevel(level);
                snapshot.BandText = BandText(snapshot.BandLevel);
                snapshot.IsCharging = true;
                snapshot.PowerText = "Charging";
            }
            else if (status == 2)
            {
                snapshot.HasBattery = true;
                snapshot.Percent = 100;
                snapshot.BandLevel = 3;
                snapshot.BandText = BandText(snapshot.BandLevel);
                snapshot.IsCharging = false;
                snapshot.PowerText = "Charged";
            }
            else
            {
                snapshot.HasBattery = false;
                snapshot.Percent = -1;
                snapshot.PowerText = String.Empty;
                snapshot.Error = "Unknown battery status nibble " + status.ToString();
            }

            return ApplyGetInfoDiagnostics(snapshot, response, statusNibble, observedUtc);
        }

        private static BatterySnapshot ApplyGetInfoDiagnostics(
            BatterySnapshot snapshot,
            byte[] response,
            byte? statusNibble,
            DateTime observedUtc)
        {
            snapshot.Transport = BatteryTransport.Unknown;
            snapshot.PowerState = BatteryPowerState.Unknown;
            snapshot.DataSource = BatteryDataSource.GetInfo;
            snapshot.RawReplyHex = Hex(response);
            snapshot.RawGetInfoStatusNibble = statusNibble;
            snapshot.UtcObservationTimestamp = observedUtc;
            return snapshot;
        }

        private BatterySnapshot ReadDockBatteryBand()
        {
            HidDeviceInfo dock = FindDockInterface();
            if (dock == null)
            {
                return BatterySnapshot.Unavailable("Flydigi dock HID interface not found", false);
            }

            using (SafeFileHandle handle = Native.CreateFile(dock.Path, Native.GENERIC_READ, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    BatterySnapshot snapshot = BatterySnapshot.Unavailable("Dock read open failed: " + Native.LastErrorText(), true);
                    snapshot.RedactedPath = dock.RedactedPath;
                    return snapshot;
                }

                try
                {
                    int inputLength = Math.Max(65, (int)dock.InputReportByteLength);
                    using (FileStream stream = new FileStream(handle, FileAccess.Read, inputLength, true))
                    {
                        DateTime deadline = DateTime.UtcNow.AddMilliseconds(DockReadTimeoutMs);
                        while (DateTime.UtcNow < deadline)
                        {
                            int remainingMs = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                            byte[] report = ReadDockReport(stream, inputLength, remainingMs);
                            if (report == null)
                            {
                                break;
                            }

                            BatterySnapshot snapshot = DecodeDockEfReport(report, dock.RedactedPath);
                            if (snapshot.HasBatteryBand)
                            {
                                return snapshot;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BatterySnapshot snapshot = BatterySnapshot.Unavailable("Dock read failed: " + ex.Message, true);
                    snapshot.RedactedPath = dock.RedactedPath;
                    return snapshot;
                }
            }

            BatterySnapshot missing = BatterySnapshot.Unavailable("Dock EF battery-band report not received within " + DockReadTimeoutMs.ToString() + " ms", true);
            missing.RedactedPath = dock.RedactedPath;
            return missing;
        }

        private static byte[] ReadDockReport(FileStream stream, int inputLength, int timeoutMs)
        {
            byte[] buffer = new byte[inputLength];
            IAsyncResult asyncResult = stream.BeginRead(buffer, 0, buffer.Length, null, null);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!asyncResult.IsCompleted && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }

            if (!asyncResult.IsCompleted)
            {
                return null;
            }

            int bytesRead = stream.EndRead(asyncResult);
            if (bytesRead <= 0)
            {
                return new byte[0];
            }

            if (bytesRead < buffer.Length)
            {
                byte[] resized = new byte[bytesRead];
                Array.Copy(buffer, resized, bytesRead);
                buffer = resized;
            }
            return buffer;
        }

        private static BatterySnapshot DecodeDockEfReport(byte[] report, string redactedPath)
        {
            DateTime observedUtc = DateTime.UtcNow;
            int offset = FindMagicOffset(report);
            if (offset < 0 || report.Length < offset + 11 ||
                report[offset] != Magic1 ||
                report[offset + 1] != Magic2 ||
                report[offset + 2] != 0xEF ||
                report[offset + 6] != 0x39)
            {
                BatterySnapshot invalid = BatterySnapshot.Unavailable("Invalid dock EF report", true);
                invalid.RedactedPath = redactedPath;
                return ApplyDockDiagnostics(invalid, report, null, null, observedUtc);
            }

            byte rawFlag = report[offset + 7];
            int flag = rawFlag;
            int state = report[offset + 8];
            if (flag == 0)
            {
                BatterySnapshot inactive = BatterySnapshot.Unavailable("Dock EF reports no active charging controller", true);
                inactive.RedactedPath = redactedPath;
                inactive.Provenance = "Flydigi Dock 2 EF status opcode 0x39, flag " + flag.ToString() + ", state 0x" + state.ToString("X2");
                return ApplyDockDiagnostics(inactive, report, rawFlag, (byte)state, observedUtc);
            }

            int band = 0;
            if (state == 0x01 || state == 0x02)
            {
                band = 1;
            }
            else if (state == 0x03 || state == 0x04)
            {
                band = 2;
            }
            else if (state == 0x05 || state == 0x06)
            {
                band = 3;
            }

            if (band == 0)
            {
                BatterySnapshot unknown = BatterySnapshot.Unavailable("Unknown dock battery-band state 0x" + state.ToString("X2"), true);
                unknown.RedactedPath = redactedPath;
                unknown.Provenance = "Flydigi Dock 2 EF status state 0x" + state.ToString("X2") + ", flag " + flag.ToString();
                return ApplyDockDiagnostics(unknown, report, rawFlag, (byte)state, observedUtc);
            }

            BatterySnapshot snapshot = new BatterySnapshot();
            snapshot.InterfacePresent = true;
            snapshot.HasBattery = false;
            snapshot.HasBatteryBand = true;
            snapshot.Percent = -1;
            snapshot.BandLevel = band;
            snapshot.BandText = BandText(band);
            snapshot.IsCharging = flag != 0;
            snapshot.PowerText = flag != 0 ? "Charging" : "Docked";
            snapshot.ConnectionText = "Dock";
            snapshot.RedactedPath = redactedPath;
            snapshot.Provenance = "Flydigi Dock 2 EF status opcode 0x39, flag " + flag.ToString() + ", state 0x" + state.ToString("X2");
            return ApplyDockDiagnostics(snapshot, report, rawFlag, (byte)state, observedUtc);
        }

        private static BatterySnapshot ApplyDockDiagnostics(
            BatterySnapshot snapshot,
            byte[] report,
            byte? rawFlag,
            byte? rawState,
            DateTime observedUtc)
        {
            snapshot.Transport = BatteryTransport.Dock;
            snapshot.PowerState = BatteryPowerState.Unknown;
            snapshot.DataSource = BatteryDataSource.DockEfBand;
            snapshot.RawDockReportHex = Hex(report);
            snapshot.RawDockFlag = rawFlag;
            snapshot.RawDockState = rawState;
            snapshot.UtcObservationTimestamp = observedUtc;
            return snapshot;
        }

        private static byte[] ReadUntilInfoResponse(FileStream stream, int inputLength)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(QueryTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                int remainingMs = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                byte[] buffer;
                if (!TryReadReport(stream, inputLength, remainingMs, out buffer))
                {
                    return null;
                }

                int offset = buffer[0] == Magic1 ? 0 : 1;
                if (buffer.Length >= offset + 31 &&
                    buffer[offset] == Magic1 &&
                    buffer[offset + 1] == Magic2 &&
                    buffer[offset + 2] == GetInfoCommand)
                {
                    return buffer;
                }
            }
            return null;
        }

        private static HidDeviceInfo FindDockInterface()
        {
            List<HidDeviceInfo> devices = HidEnumerator.Enumerate();
            foreach (HidDeviceInfo device in devices)
            {
                if (device.IsTarget(TargetVendorId, DockProductId) &&
                    device.HasCaps &&
                    device.UsagePage == TargetUsagePage &&
                    device.Usage == TargetUsage &&
                    device.InputReportByteLength > 0)
                {
                    return device;
                }
            }
            return null;
        }

        private sealed class DockStatusMonitor : IDisposable
        {
            private static readonly TimeSpan DiagnosticHeartbeatInterval =
                TimeSpan.FromMinutes(5);

            private readonly object sync = new object();
            private readonly Thread thread;
            private bool disposed;
            private BatterySnapshot lastSnapshot;
            private string lastError =
                "Dock monitor has not received an EF report yet";

            private string lastLoggedDockSignature = String.Empty;
            private DateTime lastDockLogUtc = DateTime.MinValue;

            public DockStatusMonitor()
            {
                thread = new Thread(Run);
                thread.IsBackground = true;
                thread.Name = "Vader Dock EF monitor";
                thread.Start();
            }

            public BatterySnapshot WaitForSnapshot(int timeoutMs)
            {
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (!disposed && DateTime.UtcNow < deadline)
                {
                    BatterySnapshot snapshot = GetSnapshot();
                    if (snapshot.HasBatteryBand)
                    {
                        return snapshot;
                    }
                    Thread.Sleep(100);
                }

                return GetSnapshot();
            }

            private BatterySnapshot GetSnapshot()
            {
                lock (sync)
                {
                    if (lastSnapshot != null)
                    {
                        return lastSnapshot;
                    }

                    return BatterySnapshot.Unavailable("Dock monitor: " + lastError, true);
                }
            }

            public void Dispose()
            {
                disposed = true;
                if (thread != null && thread.IsAlive)
                {
                    thread.Join(500);
                }
            }

            private void Run()
            {
                while (!disposed)
                {
                    try
                    {
                        HidDeviceInfo dock = FindDockInterface();
                        if (dock == null)
                        {
                            SetError("Flydigi dock HID interface not found");
                            SleepInterruptible(2000);
                            continue;
                        }

                        using (SafeFileHandle handle = Native.CreateFile(
                            dock.Path,
                            Native.GENERIC_READ,
                            Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                            IntPtr.Zero,
                            Native.OPEN_EXISTING,
                            Native.FILE_FLAG_OVERLAPPED,
                            IntPtr.Zero))
                        {
                            if (handle.IsInvalid)
                            {
                                SetError("Dock monitor read open failed: " + Native.LastErrorText());
                                SleepInterruptible(2000);
                                continue;
                            }

                            int inputLength = Math.Max(65, (int)dock.InputReportByteLength);

                            using (FileStream stream =
                                new FileStream(handle, FileAccess.Read, inputLength, true))
                            {
                                SetError("open, waiting for EF report on " + dock.RedactedPath);

                                while (!disposed)
                                {
                                    byte[] report =
                                        ReadDockReport(stream, inputLength, 10000);

                                    if (report == null)
                                    {
                                        SetError("Dock monitor timed out waiting for EF report");
                                        break;
                                    }

                                    BatterySnapshot snapshot =
                                        DecodeDockEfReport(report, dock.RedactedPath);

                                    if (snapshot.HasBatteryBand)
                                    {
                                        if (ShouldLogDockSnapshot(snapshot))
                                        {
                                            DiagnosticLogger.LogSnapshot(
                                                snapshot,
                                                snapshot.RedactedPath,
                                                null,
                                                String.IsNullOrEmpty(snapshot.Error)
                                                    ? "OK"
                                                    : snapshot.Error);
                                        }

                                        snapshot.Provenance +=
                                            " via background dock monitor";

                                        lock (sync)
                                        {
                                            lastSnapshot = snapshot;
                                            lastError = String.Empty;
                                        }
                                    }
                                    else
                                    {
                                        SetError(
                                            "received non-band dock report: " +
                                            Hex(report));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SetError("Dock monitor failed: " + ex.Message);
                        SleepInterruptible(2000);
                    }
                }
            }

            private bool ShouldLogDockSnapshot(BatterySnapshot snapshot)
            {
                if (snapshot == null)
                {
                    return false;
                }

                string signature = String.Concat(
                    snapshot.RawDockFlag.HasValue
                        ? snapshot.RawDockFlag.Value.ToString("X2")
                        : "-",
                    "|",
                    snapshot.RawDockState.HasValue
                        ? snapshot.RawDockState.Value.ToString("X2")
                        : "-",
                    "|",
                    snapshot.Percent.ToString(),
                    "|",
                    snapshot.BandLevel.ToString(),
                    "|",
                    snapshot.HasBatteryBand.ToString());

                DateTime observedUtc =
                    snapshot.UtcObservationTimestamp == DateTime.MinValue
                        ? DateTime.UtcNow
                        : snapshot.UtcObservationTimestamp;

                lock (sync)
                {
                    bool stateChanged = !String.Equals(
                        lastLoggedDockSignature,
                        signature,
                        StringComparison.Ordinal);

                    bool heartbeatDue =
                        lastDockLogUtc == DateTime.MinValue ||
                        observedUtc - lastDockLogUtc >=
                            DiagnosticHeartbeatInterval;

                    if (!stateChanged && !heartbeatDue)
                    {
                        return false;
                    }

                    lastLoggedDockSignature = signature;
                    lastDockLogUtc = observedUtc;
                    return true;
                }
            }

            private void SetError(string error)
            {
                lock (sync)
                {
                    lastSnapshot = null;
                    lastError = error;
                    lastLoggedDockSignature = String.Empty;
                    lastDockLogUtc = DateTime.MinValue;
                }
            }

            private void SleepInterruptible(int milliseconds)
            {
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
                while (!disposed && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private static int FindMagicOffset(byte[] report)
        {
            if (report == null)
            {
                return -1;
            }

            for (int i = 0; i + 1 < report.Length; i++)
            {
                if (report[i] == Magic1 && report[i + 1] == Magic2)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int ChargingBandFromLevel(int level)
        {
            if (level <= 1)
            {
                return 1;
            }
            if (level <= 3)
            {
                return 2;
            }
            return 3;
        }

        private static string BandText(int bandLevel)
        {
            switch (bandLevel)
            {
                case 1:
                    return "Low";
                case 2:
                    return "Medium";
                case 3:
                    return "High";
                default:
                    return String.Empty;
            }
        }

        private static bool TryReadReport(FileStream stream, int inputLength, int timeoutMs, out byte[] report)
        {
            report = null;
            byte[] buffer = new byte[inputLength];
            IAsyncResult asyncResult = stream.BeginRead(buffer, 0, buffer.Length, null, null);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (!asyncResult.IsCompleted && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(2);
            }

            if (!asyncResult.IsCompleted)
            {
                return false;
            }

            int bytesRead = stream.EndRead(asyncResult);
            if (bytesRead <= 0)
            {
                return false;
            }

            if (bytesRead < buffer.Length)
            {
                byte[] resized = new byte[bytesRead];
                Array.Copy(buffer, resized, bytesRead);
                buffer = resized;
            }

            report = buffer;
            return true;
        }

        private static List<HidDeviceInfo> EnumerateTargetDevices()
        {
            return EnumerateDevices(TargetVendorId, TargetProductId);
        }

        private static List<HidDeviceInfo> EnumerateDevices(ushort vendorId, ushort productId)
        {
            List<HidDeviceInfo> devices = HidEnumerator.Enumerate();
            List<HidDeviceInfo> target = new List<HidDeviceInfo>();
            foreach (HidDeviceInfo device in devices)
            {
                if (device.IsTarget(vendorId, productId))
                {
                    target.Add(device);
                }
            }
            return target;
        }

        private static string Hex(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return String.Empty;
            }

            StringBuilder builder = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(data[i].ToString("X2"));
            }
            return builder.ToString();
        }

        private static string DecodeConnection(byte value)
        {
            switch (value)
            {
                case 1:
                    return "Wired";
                case 2:
                    return "Wireless";
                default:
                    return "Unknown";
            }
        }

        private static string FormatLength(ushort length)
        {
            return length == 0 ? "(unavailable)" : length.ToString();
        }

        private static string EmptyMarker(string value)
        {
            return String.IsNullOrEmpty(value) ? "(unavailable)" : value;
        }
    }

    internal sealed class HidDeviceInfo
    {
        public string Path;
        public string RedactedPath;
        public bool HasAttributes;
        public ushort VendorId;
        public ushort ProductId;
        public bool HasCaps;
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        public string Product;
        public string MetadataOpenResult;
        public string ReadOpenResult;

        public bool IsTarget(ushort vendorId, ushort productId)
        {
            if (HasAttributes)
            {
                return VendorId == vendorId && ProductId == productId;
            }

            string lower = Path == null ? String.Empty : Path.ToLowerInvariant();
            return lower.IndexOf("vid_" + vendorId.ToString("x4"), StringComparison.Ordinal) >= 0 &&
                   lower.IndexOf("pid_" + productId.ToString("x4"), StringComparison.Ordinal) >= 0;
        }
    }

    internal static class HidEnumerator
    {
        public static List<HidDeviceInfo> Enumerate()
        {
            Guid hidGuid;
            Native.HidD_GetHidGuid(out hidGuid);

            IntPtr infoSet = Native.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            if (infoSet == Native.InvalidHandleValue)
            {
                throw new InvalidOperationException("SetupDiGetClassDevs failed: " + Native.LastErrorText());
            }

            try
            {
                List<HidDeviceInfo> result = new List<HidDeviceInfo>();
                Native.SP_DEVICE_INTERFACE_DATA interfaceData = new Native.SP_DEVICE_INTERFACE_DATA();
                interfaceData.cbSize = Marshal.SizeOf(typeof(Native.SP_DEVICE_INTERFACE_DATA));

                for (uint index = 0; Native.SetupDiEnumDeviceInterfaces(infoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData); index++)
                {
                    string path = GetDevicePath(infoSet, interfaceData);
                    result.Add(InspectDevice(path));
                    interfaceData = new Native.SP_DEVICE_INTERFACE_DATA();
                    interfaceData.cbSize = Marshal.SizeOf(typeof(Native.SP_DEVICE_INTERFACE_DATA));
                }

                int error = Marshal.GetLastWin32Error();
                if (error != Native.ERROR_NO_MORE_ITEMS)
                {
                    throw new InvalidOperationException("SetupDiEnumDeviceInterfaces failed: " + Native.Win32ErrorText(error));
                }

                return result;
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(infoSet);
            }
        }

        private static string GetDevicePath(IntPtr infoSet, Native.SP_DEVICE_INTERFACE_DATA interfaceData)
        {
            int requiredSize = 0;
            Native.SetupDiGetDeviceInterfaceDetail(infoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
            int error = Marshal.GetLastWin32Error();
            if (requiredSize <= 0 && error != Native.ERROR_INSUFFICIENT_BUFFER)
            {
                throw new InvalidOperationException("SetupDiGetDeviceInterfaceDetail(size) failed: " + Native.Win32ErrorText(error));
            }

            IntPtr detailData = Marshal.AllocHGlobal(requiredSize);
            try
            {
                Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);
                if (!Native.SetupDiGetDeviceInterfaceDetail(infoSet, ref interfaceData, detailData, requiredSize, out requiredSize, IntPtr.Zero))
                {
                    throw new InvalidOperationException("SetupDiGetDeviceInterfaceDetail(path) failed: " + Native.LastErrorText());
                }
                return Marshal.PtrToStringUni(IntPtr.Add(detailData, 4));
            }
            finally
            {
                Marshal.FreeHGlobal(detailData);
            }
        }

        private static HidDeviceInfo InspectDevice(string path)
        {
            HidDeviceInfo device = new HidDeviceInfo();
            device.Path = path;
            device.RedactedPath = RedactDevicePath(path);

            using (SafeFileHandle metadataHandle = Native.CreateFile(path, 0, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (metadataHandle.IsInvalid)
                {
                    device.MetadataOpenResult = "failed: " + Native.LastErrorText();
                }
                else
                {
                    device.MetadataOpenResult = "ok";
                    FillAttributes(device, metadataHandle);
                    FillProduct(device, metadataHandle);
                    FillCaps(device, metadataHandle);
                }
            }

            using (SafeFileHandle readHandle = Native.CreateFile(path, Native.GENERIC_READ, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero))
            {
                device.ReadOpenResult = readHandle.IsInvalid ? "failed: " + Native.LastErrorText() : "ok";
            }

            return device;
        }

        private static void FillAttributes(HidDeviceInfo device, SafeFileHandle handle)
        {
            Native.HIDD_ATTRIBUTES attributes = new Native.HIDD_ATTRIBUTES();
            attributes.Size = Marshal.SizeOf(typeof(Native.HIDD_ATTRIBUTES));
            if (Native.HidD_GetAttributes(handle, ref attributes))
            {
                device.HasAttributes = true;
                device.VendorId = attributes.VendorID;
                device.ProductId = attributes.ProductID;
            }
        }

        private static void FillProduct(HidDeviceInfo device, SafeFileHandle handle)
        {
            byte[] buffer = new byte[256];
            if (Native.HidD_GetProductString(handle, buffer, buffer.Length))
            {
                device.Product = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
            }
        }

        private static void FillCaps(HidDeviceInfo device, SafeFileHandle handle)
        {
            IntPtr preparsedData;
            if (!Native.HidD_GetPreparsedData(handle, out preparsedData))
            {
                return;
            }

            try
            {
                Native.HIDP_CAPS caps;
                if (Native.HidP_GetCaps(preparsedData, out caps) == Native.HIDP_STATUS_SUCCESS)
                {
                    device.HasCaps = true;
                    device.Usage = caps.Usage;
                    device.UsagePage = caps.UsagePage;
                    device.InputReportByteLength = caps.InputReportByteLength;
                    device.OutputReportByteLength = caps.OutputReportByteLength;
                    device.FeatureReportByteLength = caps.FeatureReportByteLength;
                }
            }
            finally
            {
                Native.HidD_FreePreparsedData(preparsedData);
            }
        }

        private static string RedactDevicePath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return "(empty)";
            }

            return Regex.Replace(path, @"(?i)(#vid_[0-9a-f]{4}&pid_[0-9a-f]{4}[^#]*#)[^#]+(#\{)", "$1<redacted>$2");
        }
    }

    internal static class BatteryIcon
    {
        public static Icon Create(int percent, int bandLevel, bool charging, bool connected)
        {
            using (Bitmap bitmap = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.ScaleTransform(2.0f, 2.0f);

                Rectangle body = new Rectangle(1, 6, 26, 21);
                Rectangle terminal = new Rectangle(27, 12, 4, 9);

                using (Brush shadow = new SolidBrush(Color.FromArgb(110, Color.Black)))
                {
                    graphics.FillRectangle(shadow, new Rectangle(body.X + 1, body.Y + 1, body.Width, body.Height));
                    graphics.FillRectangle(shadow, new Rectangle(terminal.X + 1, terminal.Y + 1, terminal.Width, terminal.Height));
                }

                if (!connected)
                {
                    using (Pen outline = new Pen(Color.FromArgb(40, 40, 40), 2.0f))
                    using (Pen disconnectedPen = new Pen(Color.FromArgb(210, 210, 210), 4.0f))
                    using (Pen slash = new Pen(Color.FromArgb(80, 80, 80), 2.0f))
                    {
                        graphics.DrawRectangle(outline, body);
                        graphics.DrawRectangle(outline, terminal);
                        graphics.DrawLine(disconnectedPen, 6, 10, 25, 25);
                        graphics.DrawLine(disconnectedPen, 25, 10, 6, 25);
                        graphics.DrawLine(slash, 6, 10, 25, 25);
                        graphics.DrawLine(slash, 25, 10, 6, 25);
                    }
                }
                else if ((percent >= 0 && percent <= 100) || bandLevel > 0)
                {
                    int visualPercent = percent >= 0 ? percent : BandPercent(bandLevel);
                    Color fillColor = BandColor(percent >= 0 ? PercentBand(percent) : bandLevel, charging);

                    Rectangle inner = new Rectangle(4, 9, 20, 15);
                    using (Brush background = new SolidBrush(Color.FromArgb(235, 245, 245, 245)))
                    using (Pen outline = new Pen(Color.FromArgb(35, 35, 35), 2.0f))
                    using (Pen highlight = new Pen(Color.FromArgb(230, Color.White), 1.0f))
                    {
                        graphics.FillRectangle(background, body);
                        graphics.FillRectangle(background, terminal);
                        graphics.DrawRectangle(outline, body);
                        graphics.DrawRectangle(outline, terminal);
                        graphics.DrawLine(highlight, body.Left + 2, body.Top + 2, body.Right - 2, body.Top + 2);
                    }

                    int fillWidth = (int)Math.Round(inner.Width * visualPercent / 100.0);
                    if (visualPercent > 0 && fillWidth < 2)
                    {
                        fillWidth = 2;
                    }
                    if (fillWidth > 0)
                    {
                        using (Brush fill = new SolidBrush(fillColor))
                        {
                            graphics.FillRectangle(fill, new Rectangle(inner.X, inner.Y, fillWidth, inner.Height));
                        }
                    }

                    if (charging)
                    {
                        Point[] bolt = new Point[]
                        {
                            new Point(18, 4),
                            new Point(9, 18),
                            new Point(16, 18),
                            new Point(13, 29),
                            new Point(24, 13),
                            new Point(17, 13)
                        };
                        using (Brush boltFill = new SolidBrush(Color.FromArgb(255, 250, 255, 255)))
                        using (Pen boltOutline = new Pen(Color.FromArgb(35, 35, 35), 1.0f))
                        {
                            graphics.FillPolygon(boltFill, bolt);
                            graphics.DrawPolygon(boltOutline, bolt);
                        }
                    }
                }
                else
                {
                    using (Font font = new Font("Segoe UI", 13.0f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (Brush white = new SolidBrush(Color.White))
                    using (Brush black = new SolidBrush(Color.Black))
                    {
                        graphics.DrawString("?", font, black, 10.5f, 7.5f);
                        graphics.DrawString("?", font, white, 9.5f, 6.5f);
                    }
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    Native.DestroyIcon(handle);
                }
            }
        }

        private static int BandPercent(int bandLevel)
        {
            switch (bandLevel)
            {
                case 1:
                    return 30;
                case 2:
                    return 62;
                case 3:
                    return 100;
                default:
                    return 0;
            }
        }

        private static int PercentBand(int percent)
        {
            if (percent <= 40)
            {
                return 1;
            }
            if (percent <= 80)
            {
                return 2;
            }
            return 3;
        }

        private static Color BandColor(int bandLevel, bool charging)
        {
            switch (bandLevel)
            {
                case 1:
                    return Color.FromArgb(236, 64, 64);
                case 2:
                    return Color.FromArgb(245, 184, 42);
                case 3:
                    return charging ? Color.FromArgb(53, 168, 255) : Color.FromArgb(53, 196, 110);
                default:
                    return Color.Gray;
            }
        }
    }

    internal static class Native
    {
        public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        public const int ERROR_NO_MORE_ITEMS = 259;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;
        public const uint DIGCF_PRESENT = 0x00000002;
        public const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        public const int HIDP_STATUS_SUCCESS = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, out int requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, byte[] buffer, int bufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool HidD_GetInputReport(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr icon);

        public static string LastErrorText()
        {
            return Win32ErrorText(Marshal.GetLastWin32Error());
        }

        public static string Win32ErrorText(int error)
        {
            return error + " (" + new Win32Exception(error).Message + ")";
        }
    }
}
