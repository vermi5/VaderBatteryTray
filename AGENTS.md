# Agent notes

Guidance for automated contributors working in this repository. It covers the
parts of this project whose constraints are not obvious from the code alone.

## The local API is a contract with external consumers

`VaderBatteryTray/RAINMETER_BRIDGE.md` is the specification for the local HTTP
endpoint on `127.0.0.1:42115`. The bundled Rainmeter skin is the reference
consumer, not the only one: integrations outside this repository read this
endpoint to decide whether a device is safe to write to, so a silently changed
field can cause a third party to act on misread state rather than merely to
display it wrongly.

Read the **Contract stability** section of that document before touching the
bridge. In short, the following are contract, not incidental detail:

- The `status` value set.
- The `source` value set.
- The distinction between `dockState` (conservative physical presence) and
  `dockControllerConnected` (Space Station compatibility replica of
  `ChargerInfo.IsControllerConnected`), including what `dockState: "unknown"`
  implies.
- The `dockControllerState: "charge-sleep"` value and the fields published
  alongside it.

Renaming, repurposing, or redefining any of these is a breaking change. It
requires bumping `schemaVersion` (currently `5`) rather than editing the meaning
in place. Adding a genuinely new, additive field is the normal way to extend the
response.

`RainmeterBridge.cs` is the implementation; `RAINMETER_BRIDGE.md` is what
consumers actually read. Keep them in step in the same commit.

## Behavior this project owns

Two pieces of knowledge live here and are not derivable from the Flydigi
software or from any other repository:

**The battery level to band mapping.** Controller and Dock 2 raw values use
different scales. This project normalizes both into the shared qualitative
levels `Critical`, `Low`, `Medium`, `High`, and `Top`, with charging, charged,
and discharging kept as independent power states. The mapping lives in
`BatteryPresentationPolicy.cs` (`BatteryLevelPresentation`) and
`DockBatteryPolicy.cs`. No percentage is published.

**`GET_INFO` misreports a docked, awake controller.** While a controller that
is awake charges in the Dock 2, its own `GET_INFO` reply can still report
`power: "Discharging"` and `connection: "Wireless"`. That is why a separate
`DockStatusMonitor` watches Dock EF reports independently of the controller
read, and why an active Dock charging report takes precedence over a
simultaneous live controller reply (`source: "DockEfBand"` wins). It is also
why the Dock presence signal is deliberately conservative: an inactive EF report
publishes `unknown`, never `undocked`.

If a change touches either of these, update the documentation in the same
commit — `docs/dock-ef-protocol.md` and `docs/architecture.md` for the observed
behavior, and `RAINMETER_BRIDGE.md` when the published fields or their meaning
change.

## Build and test

From the `VaderBatteryTray` folder, using the .NET Framework compiler included
with Windows. There are no NuGet or third-party dependencies.

```powershell
Set-Location .\VaderBatteryTray
.\build.cmd
.\build_led_protocol_test.cmd
.\VaderLedProtocolSelfTest.exe
```

Building does not open the controller HID interface; running the tray
application does. `VaderLedProtocolSelfTest.exe` is the self-test for the LED
protocol path and should pass before proposing lighting-related changes.

## Documentation layout

- `README.md` — user-facing entry point.
- `docs/` — architecture, Dock EF observations, LED protocol evidence,
  validation notes, and the release process.
- `VaderBatteryTray/RAINMETER_BRIDGE.md` — the local API contract.

Keep implementation notes in `docs/` rather than duplicating them across
documents.
