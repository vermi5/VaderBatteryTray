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

Active Dock EF states use this approximate display scale:

| Raw state | Display | Physical band |
| --- | ---: | --- |
| `0x01` | ~10% | Low / critical |
| `0x02` | ~25% | Low / red |
| `0x03` | ~40% | Medium / yellow |
| `0x04` | ~55% | Medium / yellow |
| `0x05` | ~70% | High / blue |
| `0x06` | ~85% | High / blue |

The percentages provide Dock fill steps; they are not measurements. Active
`0x06` remains Charging because physical observation showed the controller LEDs
breathing blue. Controller and Dock display values are not carried across a
source transition because their raw ordinal systems differ.

`RawDockFlag` is an activity indicator, not physical Dock presence.
`RawDockField9` records the following field. Its meaning is unknown: a controlled
capture kept it at `1` while docked, asleep, charging, and after removal. Inactive
packets are logged as well as active packets.

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
- `Percent`
- `PercentEstimated`
- `BandLevel`
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

The signature includes raw flag, raw state, percentage, band, and availability.
Transitions such as `01 06` to `00 06` therefore remain visible while
identical reports are suppressed.

Recent raw context is stored under:

```text
HKCU\Software\VaderBatteryTray\RuntimeState
```

It expires after 12 hours and cannot independently restore Full. Full requires
a confirmed active `0x06` followed by inactive `0x06` during the current
monitoring session. Repeated inactive `0x06` reports retain that confirmation
until a live or active state contradicts it. Registry failures never interrupt
monitoring.

Because the Dock emits the same inactive `0x06` report after an off controller
is removed, retained Full is historical rather than proof of current physical
presence. A live `GET_INFO` reply takes precedence and uses the confirmed Full
as its presentation anchor.

## Known limitations

- GET_INFO and Dock EF use different representations. Displayed percentages
  are source-specific presentation values, not measurements.
- GET_INFO transport remains `Unknown` until independently verified.
- Diagnostic `PowerState` may remain `Unknown` where semantics are unproven.
- Immediately after removal the Dock can briefly retain its preceding state;
  no EF field currently proves physical presence.
- The constant payload fields around opcode `0x39` do not have authoritative
  names.
