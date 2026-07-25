# VaderBatteryTray diagnostics

## Purpose

VaderBatteryTray includes optional diagnostic logging for investigating battery reports from Flydigi Vader controllers and Flydigi Dock 2 without changing the normal tray interface.

Diagnostic logging is disabled by default.

## Enabling diagnostic logging

Set the environment variable before starting the application:

```powershell
$env:VADERBATTERYTRAY_DIAGNOSTIC = "1"
Start-Process ".\VaderBatteryTray.exe"
```

The variable is evaluated once when the process starts. Changing or removing it after VaderBatteryTray is already running does not affect that process.

Disable diagnostics for the current PowerShell session with:

```powershell
Remove-Item Env:VADERBATTERYTRAY_DIAGNOSTIC -ErrorAction SilentlyContinue
```

## Log location

Current log:

```text
%LOCALAPPDATA%\VaderBatteryTray\diagnostics.log
```

Previous rotated log:

```text
%LOCALAPPDATA%\VaderBatteryTray\diagnostics.previous.log
```

The current log rotates when it reaches 5 MiB. Logging failures are ignored so diagnostics cannot interrupt battery reads or application operation. HID interface paths are redacted before being written.

## Data sources

### GET_INFO

Source:

```text
VID 37D7 / PID 2401
Flydigi V2 GET_INFO
```

GET_INFO provides a status nibble and a level nibble.

Current interpretation:

- Status `0`: discharging, numeric battery level.
- Status `1`: charging, qualitative battery band.
- Status `2`: charged.
- Other values: unknown and retained for diagnostics.

The numeric discharging level is represented in 20-point steps. These values must not be assumed to use the same scale as Dock EF states.

### Dock EF battery band

Source:

```text
VID 37D7 / PID 6001
Flydigi Dock 2 EF report
Opcode 0x39
```

Dock EF states are represented as qualitative bands except for the observed
Full state:

| Raw state | Displayed band |
| --- | --- |
| `0x01`–`0x02` | Low |
| `0x03` | Medium |
| `0x04`–`0x05` | High |
| `0x06` | Full / 100% |

The lower Dock states are deliberately not converted to percentages. The
`0x04` and `0x05` High states use the controller's observed 80% step only for
the visual fill. A passive capture observed an off controller transition from
`0x05` to a sustained `0x06` while its Dock charge LEDs turned off; `0x06` is
therefore displayed as Full / 100%.

The lower state boundaries remain qualitative. They must not be inferred to
be a continuous six-step percentage scale.

`0x05` currently displays:

```text
High | Charging | Dock
```

`0x06` displays:

```text
100% | Charged | Dock
```

All raw values remain available in the diagnostic log as `RawDockState`.

## Diagnostic fields

Each log entry contains tab-separated name/value fields:

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
- `RawGetInfoHex`
- `RawDockEfHex`
- `Result`

Unknown or unavailable values are written as `-`. Raw GET_INFO and Dock EF reports are preserved in hexadecimal form.

## Dock log deduplication

The background Dock monitor does not write one identical entry per second.

It logs:

- the first valid Dock observation;
- a meaningful change in the Dock signature;
- a heartbeat after five minutes without a change.

The signature includes the raw Dock flag, raw Dock state, percentage, battery band, and availability state. Transitions such as `0x05` to `0x06` remain visible while identical reports are suppressed.

If the Dock becomes quiet after a valid `0x06` Full report, the monitor retains
that Full snapshot while the Dock HID interface remains present. A Dock removal
or a direct inactive/invalid Dock report clears the cached snapshot.

## Known limitations

- GET_INFO and Dock EF use different representations and should not be compared as measurements on one continuous percentage scale.
- GET_INFO transport remains `Unknown` until its transport semantics are independently verified.
- Diagnostic `PowerState` may remain `Unknown` where the protocol meaning has not been proven.
- The numeric meaning of Dock EF `0x01` through `0x05` is not yet proven.
- Bytes following the Dock state have been observed to change, but their meaning is not yet documented.

## Related commits

```text
8030736 Add optional battery diagnostics logging
13354d4 Use qualitative Dock battery bands
```
