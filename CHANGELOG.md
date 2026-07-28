# Changelog

## 1.2.0-rc.1 - 2026-07-28

- Replace battery percentages in the tray, tooltip, local status API, and
  Rainmeter skin with `Critical`, `Low`, `Medium`, `High`, and `Top`.
- Present Dock EF `0x06` as `Top / Charging`, preserving the charging bolt
  without treating it as a sixth battery level.
- Use the same red, orange, yellow, green, and blue ascending sequence for the
  tray icon, Rainmeter skin, and optional controller-lighting policy.
- Keep the firmware-owned native red low-battery pulse and the existing Dock
  transient and inferred-Full rules unchanged.
- Invalidate a retained Dock Full when an inactive EF report clears `data[9]`,
  matching the observed powered-off controller removal transition
  `39 00 06 01` to `39 00 06 00`.
- Keep the last settled public state for up to four seconds while a powered-off
  dock/undock transition leaves a non-responsive controller HID interface
  briefly enumerated. This prevents both the tray and Rainmeter from flashing
  `BATTERY UNAVAILABLE`; a persistent read failure is still published.
- Arm the wake filter when GET_INFO is unavailable even if Windows still keeps
  the controller interface enumerated, and require four seconds of stability
  before a `Critical` candidate replaces an established higher level.
- Compensate for the controller LED diffuser with a hardware-only saturated
  RGB palette; tray and Rainmeter colors remain unchanged.
- Separate battery-level color from the native Dock charging accent: retain the
  five-level fill while using red, yellow, or blue for the tray bolt,
  Rainmeter `CHARGING` label, and directly controlled LEDs in Dock charging.

## 1.1.14 - 2026-07-26

- Treat Dock EF `data[9]` as an unknown diagnostic field instead of physical
  controller presence.
- Confirm a changed active Dock band with a repeated report or 1.5 seconds of
  stability, suppressing the observed one-second `0x01` insertion transient.
- Infer Dock Full only from a confirmed active `0x06` followed by inactive
  `0x06`; retained inactive reports remain ambiguous.
- Retain Full across subsequent inactive `0x06` reports from the same observed
  charging session, and apply the existing presentation anchor when the
  controller next appears as Wireless.
- Restore a recently confirmed Full after restarting the tray when the Dock
  still reports inactive `0x06`; live controller data remains authoritative.
- Keep a confirmed Full at 100% when waking the dock starts an active `0x06`
  maintenance/top-off session; present it as Charging until it settles back to
  Charged instead of dropping cosmetically to 85%.
- Normalize controller `GET_INFO` levels and Dock EF states onto the shared
  presentation scale `10`, `25`, `40`, `55`, `70`, `85`, and `100%`.
- Preserve the last valid Dock step across the Dock-to-Wireless transition, so
  `85% Dock Charging` becomes `85% Wireless Discharging` without a cosmetic
  jump caused by different raw protocol ordinals.
- Prefer a valid present Dock EF charging report over a simultaneous
  `GET_INFO` reply that still labels an awake docked controller as Wireless and
  Discharging.
- Expire retained Dock snapshots before source arbitration, preventing a quiet
  Dock's last Full report from labeling an undocked live controller as
  `Dock / Charged`.
- Treat an inactive retained Dock Full report as authoritative only when no
  live controller session exists; active Dock charging still takes precedence.
- Preserve confirmed Full as `100% Wireless Discharging` while the raw
  `GET_INFO` level is unchanged; subsequent changes advance through the same
  shared scale.
- Persist the short-lived presentation anchor separately under the per-user
  `PresentationState` registry subkey.
- Expose the raw `GET_INFO` level nibble and normalized-percentage flag in
  diagnostics, while keeping tray, icon fill, local API, and Rainmeter aligned.
- Keep the tray context menu responsive while controller, Dock, or lighting HID
  operations are waiting for reports by running refresh work off the UI thread.
- Coalesce overlapping refresh requests and serialize HID access so timer,
  device-change, diagnostics, and lighting operations cannot race each other.

## 1.1.13 - 2026-07-25

- Preserve the controller firmware's native red low-battery pulse by avoiding
  RGB writes while an awake controller is discharging at 20% or below.
- Present active Dock EF states `0x01` through `0x06` as the approximate
  display scale `10`, `25`, `40`, `55`, `70`, and `85%`.
- Correct the physical Dock color bands to red for `0x01`-`0x02`, yellow for
  `0x03`-`0x04`, and blue for `0x05`-`0x06`.
- Stop treating active `0x06` as Full: it is an observed blue charging stage.
- Infer Full from the then-observed inactive state and auxiliary field,
  retaining recent context under the per-user `RuntimeState` subkey.
- Include inactive `flag=0` EF packets in diagnostics instead of discarding
  the evidence that follows charge completion.
- Align the Rainmeter percentage, fill width, text color, and bar color with
  the tray presentation.
- Display estimated Dock percentages without the visually ambiguous `~`
  prefix; approximation remains explicit in diagnostics and the local API.

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
