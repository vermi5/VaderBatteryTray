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

- status `0`: discharging; the low nibble is presented on the controller's own
  ordered display scale;
- status `1`: charging; Dock EF remains the preferred source when available;
- status `2`: charged;
- other values: unknown and retained for diagnostics.

An awake controller can continue to report `status=0` and
`connection=Wireless` while physically seated and charging in Dock 2. When a
simultaneous present, active Dock EF report is available, Dock EF has
presentation precedence; both raw observations remain in the diagnostic log.
That precedence is limited to a fresh EF snapshot. Once the Dock becomes quiet
after removal, its retained Full transition cannot continue to claim
`Dock / Charged`.

Some Dock reads actively repeat inactive `00 06 01` after removal. Therefore
inactive Full is authoritative only without a live controller session. With a
live controller, it cannot override `Wireless / Discharging`; active charging
EF reports still retain priority.

## Dock EF source

The powered-off Dock source is:

```text
VID 37D7 / PID 6001
Flydigi Dock 2 EF report
Opcode 0x39
```

Active Dock EF states use this qualitative display scale:

| Raw state | Display level | Tray / Rainmeter colour |
| --- | --- | --- |
| `0x01` | Critical | red |
| `0x02` | Low | orange |
| `0x03` | Medium | yellow |
| `0x04` | High | green |
| `0x05` | Top | blue |
| `0x06` | Top + Charging | blue with charging indicator |

Active `0x06` remains Charging because physical observation showed the
controller LEDs breathing blue. It is not published as a sixth level.

`RawDockFlag` is an activity indicator, not physical Dock presence.
`RawDockField9` records the following field. Its meaning is unknown: a
controlled capture kept it at `1` while docked, asleep, charging, and after
some removals, so `1` never proves presence. A powered-off removal also
produced `00/06/00`; an inactive cleared value invalidates retained Full as
one-way negative evidence. Inactive packets are logged as well as active
packets.

A changed active Dock level is not published until it repeats or remains
unchanged for 1.5 seconds. While confirmation is pending, the last confirmed
active level is retained when available. This suppresses the observed
approximately one-second `0x01` insertion transient.

## Diagnostic fields

Each entry contains tab-separated fields:

- `TimestampUtc`
- `Attempt`
- `Device`
- `Transport`
- `DataSource`
- `BandLevel`
- `Band`
- `HasBattery`
- `HasBatteryBand`
- `PowerState`
- `RawGetInfoStatusNibble`
- `RawGetInfoLevelNibble`
- `RawDockFlag`
- `RawDockState`
- `RawDockField9`
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

The signature includes raw flag, raw state, internal continuity value,
qualitative level, and availability.
Transitions such as `01 06` to `00 06` therefore remain visible while
identical reports are suppressed.

Recent raw context is stored under:

```text
HKCU\Software\VaderBatteryTray\RuntimeState
```

It expires after 12 hours. Full first requires a confirmed active `0x06`
followed by inactive `0x06`; that confirmation can then be restored after a
tray restart while the Dock continues to report inactive `0x06`. Repeated
inactive reports retain it until a live or active state contradicts it.
Registry failures never interrupt monitoring.

Because the Dock emits the same inactive `0x06` report after an off controller
is removed, retained Full is historical rather than proof of current physical
presence. A live `GET_INFO` reply takes precedence and uses the confirmed Full
as its presentation anchor.

## Known limitations

- GET_INFO and Dock EF use different representations and are normalized onto
  the shared qualitative levels.
- GET_INFO transport remains `Unknown` until independently verified.
- Diagnostic `PowerState` may remain `Unknown` where semantics are unproven.
- Immediately after removal the Dock can briefly retain its preceding state;
  no EF field currently proves physical presence.
- The constant payload fields around opcode `0x39` do not have authoritative
  names.
