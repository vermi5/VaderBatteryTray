# Validation notes

Run these checks after a battery-state or presentation change:

1. Build the application with `build.cmd`.
2. Run `build_led_protocol_test.cmd`.
3. Start with `VADERBATTERYTRAY_DIAGNOSTIC=1` when inspecting HID behavior.
4. Verify the local status endpoint and Rainmeter receive the same qualitative
   level, charging state, and Full state as the tray.
5. Verify tray fill, Rainmeter fill, and their colors agree.

## Source transition

With the controller awake in the Dock, record the displayed Dock level, remove
it, and refresh. The resulting `Wireless Discharging` value must use the
controller's normalized qualitative level.

While it remains docked, a simultaneous `GET_INFO status=0`,
`connection=Wireless` must not override a present active Dock EF report: the
published state must remain `Dock Charging`.

After removal, a retained Full EF snapshot must expire before arbitration. A
live controller must then publish its current Wireless state, never stale
`Top / Dock / Charged`.

Repeat after restarting the monitor while the empty Dock retains `00 06 01`.
Even though the report is newly read, inactive Full must not override a live
controller session.

Repeat after confirmed Full. The first Wireless reading must publish its own raw
`GET_INFO` level; it must not remain latched at Dock Full.

Diagnostics must include both `RawGetInfoStatusNibble` and
`RawGetInfoLevelNibble`; these are protocol evidence, not public display
levels.

## Sleep, off, and receiver removal

After controller autosleep or manual power-off, verify that the tray and skin
show `CONTROLLER ASLEEP / OFF` when the Dock 2 receiver remains enumerated but
the controller HID interfaces do not. Wake with Guide, or dock the powered-off
controller, and verify that the resulting HID or Dock activity causes a refresh
without waiting for the normal polling interval.

While the monitor settles interface state, the shared tray/API publication
must retain the preceding valid display for no more than four seconds. Both the
tray and Rainmeter must then move directly to `CONTROLLER ASLEEP / OFF`,
without flashing `BATTERY UNAVAILABLE`. A deliberately sustained GET_INFO
failure beyond the grace period must still publish `unavailable`.

On reconnect, a raw GET_INFO level `0` or Dock lowest-band candidate following
a settled higher level must not flash `Critical`. It must remain stable for
four seconds before publication, so a genuine critical battery is delayed
briefly rather than hidden.

Remove the receiver and verify that the tray and skin show receiver
disconnected only when both controller and Dock 2 HID interfaces are absent.

## Dock charging scale

With activity flag `1`, verify:

```text
01=Critical/red  02=Low/orange  03=Medium/yellow
04=High/green   05=Top/blue    06=Top/blue + Charging
```

Active `0x06` must remain Top/Charging and must not display Charged.

For active Dock charging, verify that the tray battery fill and Rainmeter bar
retain the five-level colour while the tray bolt and Rainmeter `CHARGING` text
use red for Critical/Low, yellow for Medium/High, and blue for Top. Confirmed
Charged must show no charging accent.

## Dock Full regression case

With the controller powered off in the Dock, capture the transition when its
blue breathing LEDs turn off. An active `39 01 06 01` followed by inactive
`39 00 06 01` must produce `Top` and `Charged`. Inactive packets must
appear in the diagnostic log.

A full controller re-dock may produce active `01 01` followed within 15 seconds
by inactive `00 01` without lighting the LEDs. That observed insertion sequence
must produce Full unless a subsequent active charging state contradicts it.

After removing the controller, the settled empty-Dock report
`39 00 01 00` must invalidate Full and become unavailable.

A powered-off removal may instead change retained Full from `39 00 06 01` to
`39 00 06 00`. That cleared final field must also invalidate Full and publish
the controller-unavailable state. A value of `1` alone must never be treated as
proof that the controller remains present.

The diagnostic log is stored under
`%LOCALAPPDATA%\VaderBatteryTray\diagnostics.log`.
