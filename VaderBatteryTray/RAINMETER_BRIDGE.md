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
  "schemaVersion": 2,
  "controller": "Flydigi Vader 5 Pro",
  "status": "ok",
  "connected": true,
  "batteryAvailable": true,
  "percent": 80,
  "estimated": false,
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

For an active Dock 2 reading, `percent` is one of `10`, `25`, `40`, `55`,
`70`, or `85`, `estimated` is `true`, and `source` is `DockEfBand`.
Confirmed Full uses `percent: 100`, `estimated: false`, `bandLevel: 4`, and
`band: "Full"`.

Possible `status` values:

- `starting`: no refresh has completed yet.
- `ok`: an exact percentage or qualitative battery band is available.
- `unavailable`: a compatible interface exists, but battery data is unavailable.
- `disconnected`: no compatible interface was found.

Raw HID reports, device paths, and other diagnostic-only data are intentionally
not exposed.

## Rainmeter integration

Rainmeter can poll the state endpoint with its WebParser measure. Polling this
URL more frequently than the tray refresh interval does not increase HID
traffic; it only retrieves the cached JSON snapshot.

The bundled controller skin uses the command endpoint only when the user
explicitly clicks refresh or middle-clicks the skin. No device configuration,
lighting, mapping, or firmware command is exposed.

The `estimated` field identifies approximate Dock values without adding a
potentially ambiguous prefix to the displayed percentage. Its fill width and
both color indicators use the same normalized percentage and physical band as
the tray icon.
