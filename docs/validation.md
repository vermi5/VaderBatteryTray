# Validation notes

Run these checks after a battery-state or presentation change:

1. Build the application with `build.cmd`.
2. Run `build_led_protocol_test.cmd`.
3. Start with `VADERBATTERYTRAY_DIAGNOSTIC=1` when inspecting HID behavior.
4. Verify the local status endpoint and Rainmeter receive the same percentage,
   band, charging state, and Full state as the tray.
5. Verify tray fill, Rainmeter fill, and their colors agree.

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
