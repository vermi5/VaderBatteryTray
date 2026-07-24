# Changelog

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
