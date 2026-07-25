# Changelog

## Unreleased

- Preserve the controller firmware's native red low-battery pulse by avoiding
  RGB writes while an awake controller is discharging at 20% or below.

## 1.1.12 - 2026-07-25

- Dock EF `0x06` is now presented as `100% | Full`, based on a passive capture
  of an off controller in the Dock when its charge LEDs turned off.
- Keep the last observed `0x06` Full snapshot while the Dock HID interface is
  present but has gone quiet, instead of replacing it with Battery unavailable.
- Align the dock High presentation with the controller's observed 80% step;
  only the observed Full state fills the tray and Rainmeter indicators.
- Use the same blue high-level color for the tray icon whether the controller
  is charging or discharging.
- Document the packaged Rainmeter skin installation and remove the stale
  versioned ZIP name from the download instructions.

## 1.1.11 - 2026-07-24

- Corrected the Dock 2 EF visual-band boundary: state `0x04` now displays as High, matching the observed 80% controller band after undocking.
- Dock EF states remain qualitative; no percentage is inferred or displayed.

## 1.1.10 - 2026-07-24

- Allow direct battery-color lighting for a controller actively responding to `GET_INFO` while it is docked.
- Keep the controller read-only when it is merely enumerated briefly while powered off in the Dock.

## 1.1.9 - 2026-07-24

- The brightness percentage now updates locally while the slider is dragged.
- A brightness preview is sent only after the mouse button or adjustment key is released, reducing repeated HID writes during adjustment.

## 1.1.8 - 2026-07-24

- Added optional battery-color lighting control to the tray menu.
- Added a brightness slider with debounced live preview.
- Added per-user persistence with a default brightness of 25%.
- Kept `VADER_TRAY_LED_CONTROL` and `VADER_TRAY_LED_BRIGHTNESS` as advanced overrides.
- Changed the medium battery color to `RGB(255,255,0)`.
- Added a local Rainmeter bridge and optional Controller skin.
- Added offline protocol and settings self-tests.

## 1.1.7 - 2026-07-21

- Added redacted optional diagnostics logging.
- Replaced synthetic Dock percentages with qualitative Low, Medium, and High bands.
- Preserved raw Dock state values in diagnostics.
