# Validation notes

Run these checks after a battery-state or presentation change:

1. Build the application with `build.cmd`.
2. Run `build_led_protocol_test.cmd`.
3. Start with `VADERBATTERYTRAY_DIAGNOSTIC=1` when inspecting HID behavior.
4. Verify the local status endpoint and Rainmeter receive the same percentage,
   band, charging state, and Full state as the tray.
5. Verify tray fill, Rainmeter fill, and their colors agree.

## Source transition

With the controller awake in the Dock, record the displayed Dock value, remove
it, and refresh. The resulting `Wireless Discharging` value must use the
controller's raw level and physical band; it does not need to retain the Dock
percentage or fill width.

While it remains docked, a simultaneous `GET_INFO status=0`,
`connection=Wireless` must not override a present active Dock EF report: the
published state must remain `Dock Charging`.

After removal, a retained Full EF snapshot must expire before arbitration. A
live controller must then publish its current Wireless state, never stale
`100% Dock Charged`.

Repeat after restarting the monitor while the empty Dock retains `00 06 01`.
Even though the report is newly read, inactive Full must not override a live
controller session.

Repeat after confirmed Full. The first Wireless reading must publish its own raw
`GET_INFO` level; it must not remain latched at Dock Full.

Diagnostics must include both `RawGetInfoStatusNibble` and
`RawGetInfoLevelNibble`; these are evidence, not display percentages.

## Sleep, off, and receiver removal

After controller autosleep or manual power-off, verify that the tray and skin
show `CONTROLLER ASLEEP / OFF` when the Dock 2 receiver remains enumerated but
the controller HID interfaces do not. Wake with Home and verify that HID arrival
causes a refresh without waiting for the normal polling interval.

Remove the receiver and verify that the tray and skin show receiver
disconnected only when both controller and Dock 2 HID interfaces are absent.

## Dock charging scale

With activity flag `1`, verify:

```text
01=~10% red   02=~25% red
03=~40% yellow 04=~55% yellow
05=~70% blue  06=~85% blue
```

Active `0x06` must remain Charging/High and must not display Full.

## Dock Full regression case

With the controller powered off in the Dock, capture the transition when its
blue breathing LEDs turn off. An active `39 01 06 01` followed by inactive
`39 00 06 01` must produce `100%`, `Full`, and `Charged`. Inactive packets must
appear in the diagnostic log.

A full controller re-dock may produce active `01 01` followed within 15 seconds
by inactive `00 01` without lighting the LEDs. That observed insertion sequence
must produce Full unless a subsequent active charging state contradicts it.

After removing the controller, the settled empty-Dock report
`39 00 01 00` must invalidate Full and become unavailable.

The diagnostic log is stored under
`%LOCALAPPDATA%\VaderBatteryTray\diagnostics.log`.
