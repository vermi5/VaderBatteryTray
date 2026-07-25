# Vader Battery Tray

[![Windows build](https://github.com/vermi5/VaderBatteryTray/actions/workflows/build.yml/badge.svg)](https://github.com/vermi5/VaderBatteryTray/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A small Windows tray application that shows the battery state of a Flydigi
Vader 5 Pro without trusting the often incorrect generic XInput battery value.

## Download

1. Open the [latest release](https://github.com/vermi5/VaderBatteryTray/releases/latest).
2. Download the ZIP asset from that release.
3. Extract the ZIP to a permanent folder.
4. Double-click `VaderBatteryTray.exe`.

No installer, administrator access, custom driver, or account is required.

### Optional launcher script

`VaderBatteryTray.cmd` is a convenience launcher for a source checkout. It
starts `VaderBatteryTray.exe` when that file is already present; otherwise it
runs `build.cmd` first and starts the resulting executable. For a normal
release download, simply run `VaderBatteryTray.exe` directly.

## What it does

- Shows controller battery and charging state in the Windows notification area.
- Reads the Flydigi HID `GET_INFO` response for the supported controller.
- Uses qualitative battery bands when the controller is detected through Dock 2.
- Provides copyable, redacted diagnostics.
- Exposes an optional local Rainmeter bridge on `127.0.0.1`.
- Can optionally synchronize the controller lighting with battery state.

The application does not collect telemetry or connect to the internet.

## Controller lighting

Right-click the tray icon and open **Controller lighting**.

- **Sync color with battery** enables or disables direct lighting control.
- **Brightness** accepts values from 0% to 100% and defaults to 25%.
- Moving the slider updates the percentage immediately and sends one preview after release.
- **Reset saved settings** returns to disabled lighting and 25% brightness.

Lighting control is experimental and disabled by default. It does not send
lighting commands to an off controller merely enumerated by the Dock. For a
controller actively responding to `GET_INFO` while docked, it can apply the
battery color.

At 20% or below while an awake controller is discharging, the application
leaves lighting control to the controller firmware so its native red
low-battery pulse remains visible.

Advanced users can override the saved settings when starting the process:

```text
VADER_TRAY_LED_CONTROL=1
VADER_TRAY_LED_BRIGHTNESS=25
```

Environment variables take priority over the tray settings and are clearly
identified in the menu.

## Start with Windows

Run `Install Startup Shortcut.cmd` from the application folder. To undo it, run
`Remove Startup Shortcut.cmd`.

## Supported environment

- Windows 10 or Windows 11, x64.
- Flydigi Vader 5 Pro using the currently supported HID interface.
- Dock 2 support is based on the observed qualitative EF battery states.

Other Flydigi models and firmware revisions have not been tested exhaustively.
Unknown data is reported as unavailable instead of being converted into a
guessed battery percentage.

## Build from source

Windows includes the .NET Framework compiler used by this project.

```powershell
Set-Location .\VaderBatteryTray
.\build.cmd
.\build_led_protocol_test.cmd
.\VaderLedProtocolSelfTest.exe
```

The build has no NuGet or third-party package dependencies. Building does not
open the controller HID interface. Running the tray application does.

## Rainmeter

An optional skin is included under `rainmeter/RainformerHWi/Controller`. See
[RAINMETER_BRIDGE.md](VaderBatteryTray/RAINMETER_BRIDGE.md) for the local API
and setup details. Copy the `RainformerHWi` folder to your Rainmeter `Skins`
directory, then refresh the `RainformerHWi\\Controller` skin.

## Technical documentation

Developer and verification material lives in [`docs/`](docs/): the architecture, Dock EF battery-state observations, validation notes, and the release process. The repository documentation describes current behavior; historical handoff material is kept outside the working repository.

## Help and contributions

- For a problem, open a [bug report](https://github.com/vermi5/VaderBatteryTray/issues/new?template=bug_report.yml).
- For an idea, open a [feature request](https://github.com/vermi5/VaderBatteryTray/issues/new?template=feature_request.yml).
- Please read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting code.
- Security concerns should follow [SECURITY.md](SECURITY.md).

## License and disclaimer

Released under the [MIT License](LICENSE).

This is an independent open-source project. It is not affiliated with or
endorsed by Flydigi. Product names and trademarks belong to their respective
owners.
