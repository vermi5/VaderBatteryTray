# Dock EF battery states

Dock 2 exposes a six-step charging state through passive EF reports. It does
not expose a measured percentage, so the application uses a documented display
scale rather than claiming measurement precision.

## Active charging scale

For reports with activity flag `1`:

| Raw state | Display percentage | Physical band | Observed controller LEDs |
| --- | ---: | --- | --- |
| `0x01` | ~10% | Critical / Low | insertion or lowest state |
| `0x02` | ~25% | Low | pulsing red |
| `0x03` | ~40% | Medium | pulsing yellow |
| `0x04` | ~55% | Medium | pulsing yellow |
| `0x05` | ~70% | High | pulsing blue |
| `0x06` | ~85% | High | pulsing blue |

The percentages are evenly spaced presentation values chosen to preserve room
between the final active charging step and confirmed Full. Colors are derived
from the observed physical bands, not from generic percentage thresholds.

The controller `GET_INFO` level is normalized through the same ordered scale.
At a Dock-to-Wireless transition, the last Dock ordinal is used as an anchor:
the first Wireless reading keeps the same displayed percentage, and later raw
level changes move by the same number of scale positions. This avoids implying
that unrelated raw ordinals from the two report families are directly equal.

## EF packet fields

An observed report has this form:

```text
00 5A A5 EF 08 01 00 39 <activity> <state> 01 <checksum> 00...
```

- `00` is the HID report ID.
- `5A A5` is the Flydigi frame marker.
- `EF` identifies the passive report family.
- `08` is the remaining frame length.
- `01 00` are constant fields whose meaning is not proven.
- `39` is the Dock charge-status opcode.
- `activity` is `1` while the charging indication is active and `0` when it is
  inactive or retaining a previous state. It must not be interpreted as
  physical Dock presence.
- `state` is the six-step value.
- the following field is `1` with the controller present in the observed
  charging/full reports and becomes `0` once the empty Dock settles. It is
  therefore used as a controller-present/validity field.
- `checksum` is the low byte of the sum from `5A` through that constant `01`,
  plus one.
- trailing zeroes pad the HID report.

## Flydigi service evidence

Decompilation of `Flydigi.ChargerSdk.dll` and the managed service bundle
confirms how Flydigi carries the Dock values through its own application:

- `ChargerProtocol.ParseData` recognizes the `5A A5 EF` frame and forwards the
  complete Flydigi span to its raw-data listener.
- `ChargerRepository.OnRawDataReceived` assigns `data[7] == 1` to
  `Charger.IsControllerConnected` and `data[8]` to
  `Charger.ControllerBattery`.
- `ChargerDataMapper` copies those properties unchanged to protobuf fields
  `ChargerInfo.isControllerConnected` and `ChargerInfo.controllerBattery`.
- the renderer uses battery levels `1..6` directly to select its six battery
  assets. It does not convert the value from a percentage.

Those service offsets exclude the HID report ID. In the raw HID report shown
above they therefore correspond to the activity field and six-step state at
`offset + 7` and `offset + 8` after the `5A A5` marker. Flydigi's internal
`IsControllerConnected` name is evidence of its software model, but physical
captures show that this field can become inactive while the controller remains
in the Dock. The following field remains the stronger observed
controller-present/validity signal.

No separate `isCharging`, `chargeState`, or equivalent protobuf field was
found. Charging and Full in this application remain interpretations of the
observed EF transitions and physical LED behavior, rather than a distinct
charging-status value exposed by Flydigi.

## Full and inactive reports

Physical observation and a passive live capture established:

```text
39 01 06 01 39  -> present, blue LEDs breathing; still charging
39 00 06 01 38  -> present, LEDs off; charge indication inactive
39 00 01 00 32  -> empty Dock after the removal transition settled
```

The application therefore does not equate active `0x06` with Full. Inactive
`0x06` with the controller-present field set confirms Full; an
active-to-inactive `0x06` transition provides additional context. An insertion
that settles from active `0x01` to inactive `0x01`, with presence still set and
without starting a charge band, is also treated as an already-full controller.

Immediately after removal, the Dock can briefly retain the previous inactive
state and presence field. Once it settles, the observed empty-Dock report clears
the presence field and invalidates Full. Inactive reports with the presence
field cleared remain unavailable.

## Runtime cache

Transition context is stored separately from user settings under:

```text
HKCU\Software\VaderBatteryTray\RuntimeState
```

The cache records raw state, last active state, timestamps, and whether Full was
confirmed. It expires after 12 hours and is never allowed to override a new
active EF report. Registry read or write failures do not affect monitoring.

Display continuity is stored separately under:

```text
HKCU\Software\VaderBatteryTray\PresentationState
```

This 12-hour anchor contains only an ordinal, the first Wireless raw level, and
a timestamp. Confirmed Full therefore remains `100%` after removal while the
raw Wireless level is unchanged; the first decrement advances to `85%`.
