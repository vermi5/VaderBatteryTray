# Validation notes

Run the following checks after a battery-state or presentation change.

1. Build the application with `build.cmd`.
2. Run `build_led_protocol_test.cmd`.
3. Start the application with `VADERBATTERYTRAY_DIAGNOSTIC=1` when inspecting HID behavior.
4. Verify the local status endpoint reports a coherent snapshot for controller and Dock paths.
5. Verify the tray color and Rainmeter bar use the same normalized state.

## Dock Full regression case

With the controller powered off in the Dock, wait until its charge LEDs turn off. A confirmed Dock `0x06` state must remain visible as `100%`, `Full`, and `Charged` even if subsequent EF reports are absent while the Dock remains connected.

The diagnostic log is stored under `%LOCALAPPDATA%\\VaderBatteryTray\\diagnostics.log` when diagnostics are enabled. Do not treat a missing fresh Dock report as proof that the battery state changed.
