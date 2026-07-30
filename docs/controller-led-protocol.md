# Controller LED protocol evidence

The controller lighting path is based on real USBPcap captures of Flydigi Space Station communicating with a Vader 5 Pro. The capture files are intentionally kept outside this repository: they are evidence material, not project assets.

## What the captures verify

- Target controller: `VID 37D7`, `PID 2401`.
- Lighting/configuration writes use endpoint `0x06 OUT`; matching acknowledgements arrive on `0x82 IN`.
- The reports are 32 bytes and use the `5A A5 A8` / `5A A5 A9` families.
- A solid-color payload contains ten RGB entries, confirming ten ordered onboard lighting zones.
- Captured Space Station native effects include solid, off, breathing, and gradient commands.

The native breathing capture uses mode byte `0x02`; the final captured command uses a single color across the ten zones. The native gradient capture uses mode byte `0x03` and has a prelude, configuration chunk, and five data chunks. The multi-mode capture contains the same controller endpoint and acknowledged 32-byte command family.

## Interpretation and limits

This proves that the controller firmware accepts native lighting modes and that the ten-zone representation is a real protocol payload, rather than a UI assumption. It does **not** prove that the charging indicator itself uses ten percentage steps, nor does it establish an exact battery percentage for every Dock EF band.

The application currently uses the conservative static-color path for battery synchronization. Native animation replay is not part of the normal battery workflow: it needs individual ACK handling and has previously shown incomplete-transfer behavior in exploratory work. Treat new native modes as controlled experiments, not as a background polling or animation loop.

The controller uses a diffuser-compensated saturated palette distinct from the
screen palette: `Critical=255,0,0`, `Low=255,64,0`,
`Medium=255,255,0`, `High=0,255,0`, and `Top=0,0,255`. The semantic
red-orange-yellow-green-blue sequence remains identical in the tray and
Rainmeter.

During an active Dock charging snapshot, directly controlled controller LEDs
instead follow the observed native charge-stage grouping:
`Critical/Low=red`, `Medium/High=yellow`, and `Top=blue`. An inactive
confirmed `Top / Charged` snapshot has no charging accent.

While the controller is docked, direct controller LED writes are allowed only
when the Dock heartbeat confirms that Dock Sync (`data[19]`), Intelligent
Start / Sleep when charging (`data[18]`), and Power Display / Show charging
animation (`data[21]`) are all disabled. Any enabled or unknown lighting owner
fails safe by preserving the firmware-controlled lighting. Close When Shutdown
(`data[20]`) is recorded for diagnostics but does not own live lighting.
