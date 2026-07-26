# Rainmeter bridge

Vader Battery Tray exposes its latest already-collected controller state through
a read-only HTTP endpoint bound exclusively to the IPv4 loopback interface.

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

Example exact-battery response:

```json
{
  "schemaVersion": 3,
  "controller": "Flydigi Vader 5 Pro",
  "status": "ok",
  "connected": true,
  "controllerPresent": true,
  "dockPresent": true,
  "batteryAvailable": true,
  "percent": 85,
  "estimated": true,
  "bandLevel": 3,
  "band": "High",
  "charging": false,
  "power": "Discharging",
  "connection": "Wireless",
  "source": "GetInfo",
  "firmware": "0x7141",
  "observedUtc": "2026-07-22T18:00:00.0000000Z",
  "publishedUtc": "2026-07-22T18:00:00.0500000Z",
  "error": null
}
```

Controller and Dock 2 values use their own raw scales. `percent` is a
source-specific presentation step: it must not be used to infer the physical
band or colour across a source transition. Active Dock values are estimated;
confirmed Full uses `100`.
Confirmed Full uses `percent: 100`, `estimated: false`, `bandLevel: 4`, and
`band: "Full"`.

Possible `status` values:

- `starting`: no refresh has completed yet.
- `ok`: an exact percentage or qualitative battery band is available.
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

The `estimated` field identifies Dock presentation steps without adding a
potentially ambiguous prefix. The bundled skin uses `bandLevel` for color, not
the display percentage, so a displayed value from one source is never used to
colour the other source.
