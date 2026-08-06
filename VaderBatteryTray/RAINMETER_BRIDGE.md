# Local status API

Vader Battery Tray exposes its latest already-collected controller state through
a read-only HTTP endpoint bound exclusively to the IPv4 loopback interface.
This is the shared contract for every consumer: the bundled Rainmeter skin,
the OBS Studio overlay, the Wallpaper Engine widget, and any other local
tool that wants to read it.

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

Every response includes `Access-Control-Allow-Origin: *`, so a page's own
`fetch`/`XMLHttpRequest` can read the JSON cross-origin — required for
browser-based consumers like OBS Browser Source and Wallpaper Engine, which
run as real Chromium contexts rather than a plain HTTP client. The endpoint
stays loopback-only and unauthenticated; this header means any web page open
in an ordinary browser on the same machine can also read this low-sensitivity,
read-only JSON, not only the bundled integrations below.

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

Raw HID reports, device paths, and other diagnostic-only data are intentionally
not exposed.

## Rainmeter integration

Rainmeter can poll the state endpoint with its WebParser measure. Polling this
URL more frequently than the tray refresh interval does not increase HID
traffic; it only retrieves the cached JSON snapshot.

The bundled controller skin uses the command endpoint only when the user
explicitly clicks refresh or middle-clicks the skin. No device configuration,
lighting, mapping, or firmware command is exposed.

The bundled skin uses `bandLevel` for both fill and colour. It never displays or
reconstructs a battery percentage.

## OBS Studio integration

`overlays/obs/vader-battery-overlay.html` polls the state endpoint once per
second via `fetch` and only re-renders when the relevant fields change,
matching the Rainmeter skin's own polling cadence. On a fetch failure or a
non-2xx response it shows an explicit offline state rather than freezing on
the last successful reading. Add it as an OBS Browser Source with "Local
file" checked; it never opens the command or health endpoints.

## Wallpaper Engine integration

`overlays/wallpaper-engine/` is a `"type": "web"` project using the same
polling and offline behavior as the OBS overlay. Corner position and scale
are exposed as Wallpaper Engine user properties. Enabling Widget mode (a
per-wallpaper toggle in the Wallpaper Engine app itself, not a project file
setting) lets it float over the desktop independently of the user's actual
wallpaper.

Both overlays and the Rainmeter skin share one hand-maintained
implementation of the band-color, bar-fill, and status-text rules in
`overlays/shared/battery-overlay-core.js`, spliced into each distributable
HTML file by `overlays/shared/build.ps1` so every consumer agrees on wording
and color for the same live state.
