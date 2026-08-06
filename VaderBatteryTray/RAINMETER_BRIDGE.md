# Rainmeter bridge

Vader Battery Tray exposes its latest already-collected controller state through
a read-only HTTP endpoint bound exclusively to the IPv4 loopback interface.

The bundled Rainmeter skin is the reference consumer, but it is not the only
one: the endpoint is a general-purpose local state API and has consumers
outside this repository. See "Contract stability" below before changing any
field.

## Endpoint

```text
GET http://127.0.0.1:42115/api/v1/state
```

Health check:

```text
GET http://127.0.0.1:42115/api/v1/health
```

Safe immediate refresh command:

```text
GET http://127.0.0.1:42115/api/v1/command/refresh
```

The state endpoint does not open the HID interface and does not trigger
additional controller queries. It only serializes the latest snapshot published
by the existing tray refresh cycle. The refresh command queues one normal tray
refresh and rejects repeated requests received within two seconds.

## State schema

Example response:

```json
{
  "schemaVersion": 5,
  "controller": "Flydigi Vader 5 Pro",
  "status": "ok",
  "connected": true,
  "controllerPresent": true,
  "dockPresent": true,
  "batteryAvailable": true,
  "bandLevel": 5,
  "band": "Top",
  "charging": false,
  "dockControllerStateInferred": false,
  "dockControllerState": "normal",
  "power": "Discharging",
  "connection": "Wireless",
  "source": "GetInfo",
  "firmware": "0x7141",
  "observedUtc": "2026-07-22T18:00:00.0000000Z",
  "publishedUtc": "2026-07-22T18:00:00.0500000Z",
  "dockState": "unknown",
  "dockStateObservedUtc": "2026-07-22T18:00:00.0300000Z",
  "dockStateSequence": 0,
  "dockStateSource": "dock-ef-inactive-ambiguous",
  "dockStateRawField9": 1,
  "dockStateActiveSessionObserved": true,
  "dockControllerConnected": true,
  "dockControllerConnectedObservedUtc": "2026-07-22T18:00:00.0300000Z",
  "dockControllerConnectedSequence": 4,
  "dockControllerConnectedSource": "dock-ef-activity-is-controller-connected",
  "error": null
}
```

`dockState` is a physical-presence signal independent of `connection`,
`charging`, `source`, and the battery timestamps. An active Dock EF session
publishes `docked` with source `dock-ef-active-session`. An inactive EF report
always publishes `unknown`, because it can be retained after removal or mean a
fully charged controller is still seated; it never proves physical `undocked`.
`dockStateSequence` increments only when this state changes;
`dockStateObservedUtc` is refreshed by every EF observation, even when the
battery snapshot does not change. Reading the endpoint remains cache-only and
never starts a controller refresh.
`dockStateRawField9` and `dockStateActiveSessionObserved` are diagnostic
evidence from the exact EF observation that last updated this signal.
`dockControllerConnected` is a separate, non-physical compatibility signal: it
is an exact replica of Space Station Service's
`ChargerInfo.IsControllerConnected`. It follows the Dock EF activity field
directly (`EF[7] = 1` means `true`; `EF[7] = 0` means `false`). Its timestamp
and sequence are independent. A controller may be physically seated in the
Dock but already full, causing `EF[7] = 0`; in that case
`dockControllerConnected: false` is correct for Space Station compatibility
and does not prove physical undock. Consumers needing physical certainty must
use the separate conservative `dockState` signal.

Controller and Dock 2 values use their own raw scales. The bridge exposes only
the common qualitative levels: `Critical`, `Low`, `Medium`, `High`, and `Top`.
Charging and Charged remain independent power states. Confirmed or inferred
Full therefore uses `bandLevel: 5`, `band: "Top"`, and `power: "Charged"`.

When Intelligent Start suspends a docked controller, the bridge retains the
last confirmed band and returns `dockControllerState: "charge-sleep"`,
`dockControllerStateInferred: true`, `charging: true`, and
`power: "Sleeping while charging"`. Charging during this state is physically
confirmed; only the controller's internal sleep state is inferred. The bundled
skin presents the compact label `DOCK SLEEP`.

Possible `status` values:

- `starting`: no refresh has completed yet.
- `ok`: a qualitative battery level is available.
- `unavailable`: controller interfaces exist, but a current battery reply is unavailable.
- `controller-unavailable`: the receiver/dock is present but the controller HID
  interfaces are absent (the controller is asleep, turned off, or out of range).
- `receiver-disconnected`: neither the controller nor the Dock 2 receiver HID
  interfaces are present.

Possible `source` values, identifying which reading produced the published
battery state:

- `"GetInfo"`: the controller's own `GET_INFO` reply.
- `"DockEfBand"`: a Dock 2 EF report. This wins over a live controller
  reading when the Dock is actively charging, because `GET_INFO` can report
  `power: "Discharging"` and `connection: "Wireless"` while an awake
  controller is physically charging in the Dock.
- `null`: no reading has established a source yet.

Raw HID reports, device paths, and other diagnostic-only data are intentionally
not exposed.

## Contract stability

This endpoint is not Rainmeter-specific. `schemaVersion` is the compatibility
contract for every consumer, and integrations outside this repository read it
to decide whether a device is safe to write to — a wrong or silently changed
field can therefore cause a third party to act on stale or misread state, not
just to display it incorrectly.

Treat as part of the contract, not as incidental detail: the `status` and
`source` value sets above, the `dockState` / `dockControllerConnected`
distinction described earlier, and the `dockControllerState:
"charge-sleep"` value. Renaming or repurposing any of them, or changing what
`dockState: "unknown"` implies, is a breaking change and needs a
`schemaVersion` bump rather than an in-place edit.

## Rainmeter integration

Rainmeter can poll the state endpoint with its WebParser measure. Polling this
URL more frequently than the tray refresh interval does not increase HID
traffic; it only retrieves the cached JSON snapshot.

The bundled controller skin uses the command endpoint only when the user
explicitly clicks refresh or middle-clicks the skin. No device configuration,
lighting, mapping, or firmware command is exposed.

The bundled skin uses `bandLevel` for both fill and colour. It never displays or
reconstructs a battery percentage.
