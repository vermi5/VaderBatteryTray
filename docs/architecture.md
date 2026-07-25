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
     BatterySnapshot
          |
          +-- tray icon and tooltip
          +-- local HTTP JSON endpoint
          +-- optional Rainmeter skin
          +-- diagnostic log (when enabled)
```

The local endpoint uses `schemaVersion: 1` and reports `percent`, `bandLevel`, `band`, `charging`, `power`, `connection`, and `source`. Rainmeter should consume these normalized fields instead of reading HID reports itself.
