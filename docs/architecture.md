# Architecture

VaderBatteryTray is a Windows tray application for the Flydigi Vader 5 Pro. It polls the controller or its Dock, normalizes the result into one battery snapshot, and presents that snapshot through the tray icon and a local HTTP status endpoint.

## Components

- `VaderBatteryTray.cs` contains HID discovery, controller and Dock polling, state normalization, the tray UI, diagnostics, and the HTTP endpoint.
- `LedProtocolTest.cs` provides protocol-focused regression checks.
- `rainmeter/` contains the optional controller skin that reads the local endpoint.
- `package.ps1` builds the distributable ZIP used by GitHub releases.

## Data flow

```text
controller / Dock HID report
          |
          v
 raw BatterySnapshot
          |
          v
 source selection + source-specific physical band
          |
          +-- tray icon and tooltip
          +-- local HTTP JSON endpoint
          +-- optional Rainmeter skin
          +-- diagnostic log (when enabled)
```

The local endpoint uses `schemaVersion: 3` and reports source-specific
`percent`, `estimated`, `bandLevel`, `band`, `charging`, `power`, `connection`,
`source`, `controllerPresent`, and `dockPresent`. Rainmeter consumes these
fields instead of reading HID reports itself.

The application does not carry a percentage across a Dock-to-Wireless source
transition. The raw scales differ, so each source keeps its own presentation
step and physical-band color.

Source arbitration checks Dock EF even after a valid controller reply. A
present, active Dock charging report overrides simultaneous `GET_INFO`
`Wireless / Discharging`, because that controller field has been observed to
remain stale while the awake controller is physically docked.

Dock precedence is freshness-bounded. After three seconds without a fresh EF
observation it cannot override a live controller session's connection and
power state.

Additionally, an inactive Full EF report never overrides a simultaneous live
controller session, because the empty Dock has been observed to repeat retained
`00 06 01` on a new read. Active charging EF remains authoritative; inactive
Full is authoritative only when the controller is not responding.

When the controller HID interfaces disappear but the Dock 2 receiver remains,
the tray and Rainmeter report `controller-unavailable` (asleep, off, or out of
range). If both disappear, they report `receiver-disconnected`. HID device
arrival/removal notifications queue an immediate refresh, so waking with Home
does not wait for the normal polling interval.
