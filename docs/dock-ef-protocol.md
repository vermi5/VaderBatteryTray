# Dock EF battery states

Dock 2 exposes five ordinary battery-level presentations plus the special
`0x06` charging presentation through passive EF reports. It does not expose a
measured percentage.

## Active charging scale

For reports with activity flag `1`:

| Raw state | Display level | Tray / Rainmeter colour | Observed controller LEDs |
| --- | --- | --- | --- |
| `0x01` | Critical | red | insertion or lowest state |
| `0x02` | Low | orange | pulsing red |
| `0x03` | Medium | yellow | pulsing yellow |
| `0x04` | High | green | pulsing yellow |
| `0x05` | Top | blue | pulsing blue |
| `0x06` | Top + Charging | blue with charging indicator | pulsing blue |

The five-colour palette is VaderBatteryTray presentation, not a claim about
the Dock's physical LED colour. `0x06` is not exposed as a sixth battery level.
While charging, the battery fill retains that five-colour level palette, but
the charging accent follows the native three-stage grouping: red for
Critical/Low, yellow for Medium/High, and blue for Top. `Charged` has no
charging accent.

The controller `GET_INFO` level is normalized through the same qualitative
scale. At a Dock-to-Wireless transition, the last Dock ordinal is used as an
internal anchor. This avoids implying that unrelated raw ordinals from the two
report families are directly equal.

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
- the following field (`field9`) has unknown meaning. A controlled capture kept
  it at `1` while inactive, docked, charging, asleep, and even after some
  removals, so `1` must not be used as proof of physical presence. A separate
  powered-off removal produced `00/06/00`; an inactive transition to `0` is
  therefore used only as negative evidence that invalidates retained Full.
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
- the renderer uses `ControllerBattery` directly to select `Power1` through
  `Power6`; `Power6` is the charging glyph rather than a sixth fill level.

Those service offsets exclude the HID report ID. In the raw HID report shown
above they therefore correspond to the activity field and six-step state at
`offset + 7` and `offset + 8` after the `5A A5` marker. Flydigi's internal
`IsControllerConnected` name is evidence of its software model, but physical
captures show that this field can become inactive while the controller remains
in the Dock. It is best treated as an active-session indicator. No EF field
currently proves physical presence; cleared `field9` is only a one-way absence
hint.

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

The application therefore does not equate active `0x06` with Full. An
active-to-inactive `0x06` transition is the strongest observed evidence for
Full. An isolated inactive report is ambiguous because the Dock may retain its
last state after removal.

After removal, the Dock can retain the previous inactive state and `field9`.
Inactive retained reports therefore remain unavailable unless the current
monitoring session first observed the active-to-inactive `0x06` completion
transition. Once confirmed, repeated `00/06` reports retain historical Full;
they do not prove that the controller remains physically present. An inactive
`00/06/00` clears that retained Full because this exact report was observed
when removing a powered-off controller.

If waking a controller after confirmed Full reactivates `01/06`, the
application treats it as a maintenance/top-off session and retains `Top` while
showing Charging. A subsequent inactive `00/06` returns to Charged.

## Active-state stability

A newly changed active state must be observed twice or remain unchanged for
1.5 seconds before it replaces the published band. While it is pending, the
last confirmed active band is retained when available. This filters the
controlled insertion sequence in which `0x01` lasted about 0.99 seconds before
the Dock returned to the stable `0x04` level.

## Runtime cache

Transition context is stored separately from user settings under:

```text
HKCU\Software\VaderBatteryTray\RuntimeState
```

The cache records raw state, last active state, timestamps, and historical Full
context. It expires after 12 hours and may restore a previously confirmed Full
when the Dock still reports inactive `0x06`; it never overrides a new active EF
report or live controller data. Registry read or write failures do not affect
monitoring.

Display continuity is stored separately under:

```text
HKCU\Software\VaderBatteryTray\PresentationState
```

This 12-hour anchor contains only an ordinal, the first Wireless raw level, and
a timestamp. It is internal continuity state; public presentation remains
qualitative.
