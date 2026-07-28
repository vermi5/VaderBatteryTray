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

The local endpoint uses `schemaVersion: 4` and reports the qualitative
`bandLevel` and `band` separately from `charging`, `power`, `connection`,
`source`, `controllerPresent`, and `dockPresent`. Rainmeter consumes these
fields instead of reading HID reports itself.

The raw Dock and controller scales differ. Their internal ordinals are
normalized for continuity, then exposed only as `Critical`, `Low`, `Medium`,
`High`, or `Top`.

Source arbitration checks Dock EF even after a valid controller reply. A
present, active Dock charging report overrides simultaneous `GET_INFO`
`Wireless / Discharging`, because that controller field has been observed to
remain stale while the awake controller is physically docked.

Dock precedence is freshness-bounded. After three seconds without a fresh EF
observation it cannot override a live controller session's connection and
power state.

Controller and Dock polling runs on one serialized background worker. Timer,
device-change, local endpoint, and manual refresh requests are coalesced while
a read is already in progress. Completed snapshots return to the UI timer for
tray icon and menu updates, so HID report timeouts do not block the Windows
message loop or delay opening the context menu.

Additionally, an inactive Full EF report never overrides a simultaneous live
controller session, because the empty Dock has been observed to repeat retained
`00 06 01` on a new read. Active charging EF remains authoritative; inactive
Full is authoritative only when the controller is not responding.

When the controller HID interfaces disappear but the Dock 2 receiver remains,
the tray and Rainmeter report `controller-unavailable` (asleep, off, or out of
range). If both disappear, they report `receiver-disconnected`. HID device
arrival/removal notifications queue an immediate refresh, so waking with Home
or docking the powered-off controller does not wait for the normal polling
interval.

Unavailable GET_INFO readings arm the wake filter even if Windows has not yet
removed the controller HID interface. A `Critical` candidate from GET_INFO or
Dock insertion must remain stable for four seconds before it replaces a
settled higher level. Genuine low-battery state is therefore delayed briefly
rather than suppressed.
