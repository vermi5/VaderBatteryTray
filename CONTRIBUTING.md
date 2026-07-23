# Contributing

Thanks for helping improve Vader Battery Tray.

## Before opening a change

1. Search existing issues to avoid duplicates.
2. Open an issue before changing HID commands or device-detection rules.
3. Do not include controller serial numbers, unredacted device paths, captures,
   proprietary binaries, or personal diagnostic logs.

## Build and test

On Windows x64:

```powershell
Set-Location .\VaderBatteryTray
.\build.cmd
.\build_led_protocol_test.cmd
.\VaderLedProtocolSelfTest.exe
```

The build and self-test are offline. Do not run the tray or hardware probes
unless hardware access is intentionally part of your test.

## Pull requests

Keep each pull request focused. Explain:

- what changed;
- why it changed;
- how it was tested;
- whether it sends new HID commands or changes existing ones.

Changes to HID behavior should preserve raw diagnostic evidence and must not
guess unknown battery states.
