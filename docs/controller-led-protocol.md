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
