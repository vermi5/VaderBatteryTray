# VaderBatteryTray diagnostics

## Purpose

Optional diagnostic logging records redacted controller and Dock battery
reports without changing the normal tray interface. It is disabled by default.

## Enabling logging

Set the environment variable before starting the application:

```powershell
$env:VADERBATTERYTRAY_DIAGNOSTIC = "1"
Start-Process ".\VaderBatteryTray.exe"
```

The variable is evaluated once at process startup. Disable it for the current
PowerShell session with:

```powershell
Remove-Item Env:VADERBATTERYTRAY_DIAGNOSTIC -ErrorAction SilentlyContinue
```

The current and rotated logs are:

```text
%LOCALAPPDATA%\VaderBatteryTray\diagnostics.log
%LOCALAPPDATA%\VaderBatteryTray\diagnostics.previous.log
```

The log rotates at 5 MiB. Logging errors are ignored and HID paths are
redacted.

## GET_INFO source

The live controller source is:

```text
VID 37D7 / PID 2401
Usage page 0xFFA0 / usage 0x0001
Flydigi V2 GET_INFO
```

The battery byte contains a status nibble and level nibble:

- status `0`: discharging; levels are displayed in 20-point steps;
- status `1`: charging; levels represent qualitative physical bands;
- status `2`: charged;
- other values: unknown and retained for diagnostics.

## Dock EF source

The powered-off Dock source is:

```text
VID 37D7 / PID 6001
Flydigi Dock 2 EF report
Opcode 0x39
```

Active Dock EF states use this approximate display scale:

| Raw state | Display | Physical band |
| --- | ---: | --- |
| `0x01` | ~10% | Low / critical |
| `0x02` | ~25% | Low / red |
| `0x03` | ~40% | Medium / yellow |
| `0x04` | ~55% | Medium / yellow |
| `0x05` | ~70% | High / blue |
| `0x06` | ~85% | High / blue |

The percentages provide consistent tray and Rainmeter fill steps; they are not
measurements. Active `0x06` remains Charging because physical observation
showed the controller LEDs breathing blue.

`RawDockFlag` is an activity indicator, not physical Dock presence.
`RawDockPresenceFlag` records the following field: it was `1` for the observed
docked charging/full reports and `0` once the empty Dock settled. Inactive
packets are logged as well as active packets.

## Diagnostic fields

Each entry contains tab-separated fields:

- `TimestampUtc`
- `Attempt`
- `Device`
- `Transport`
- `DataSource`
- `Percent`
- `BandLevel`
- `HasBattery`
- `HasBatteryBand`
- `PowerState`
- `RawGetInfoStatusNibble`
- `RawDockFlag`
- `RawDockState`
- `RawDockPresenceFlag`
- `RawGetInfoHex`
- `RawDockEfHex`
- `Result`

Unknown values are written as `-`. Raw GET_INFO and Dock EF reports are
preserved in hexadecimal form.

## Dock log deduplication

The background monitor logs:

- the first Dock observation, including inactive reports;
- a meaningful change in the Dock signature;
- a heartbeat after five minutes without a change.

The signature includes raw flag, raw state, percentage, band, and availability.
Transitions such as `01 06` to `00 06` therefore remain visible while
identical reports are suppressed.

Recent raw and confirmed-Full context is stored under:

```text
HKCU\Software\VaderBatteryTray\RuntimeState
```

It expires after 12 hours and cannot override a new active EF report. Registry
failures never interrupt monitoring.

## Known limitations

- GET_INFO and Dock EF use different representations. Dock percentages are a
  display interpolation, not measurements.
- GET_INFO transport remains `Unknown` until independently verified.
- Diagnostic `PowerState` may remain `Unknown` where semantics are unproven.
- Immediately after removal the Dock can briefly retain its preceding state;
  the subsequent presence-cleared report invalidates it.
- The constant payload fields around opcode `0x39` do not have authoritative
  names.
