# Dock EF battery states

The Dock exposes a compact qualitative battery state through its EF flow. It does not provide a measured percentage in the reports observed so far.

| Raw state | Normalized band | Display percentage | Evidence |
| --- | --- | ---: | --- |
| `0x01`-`0x02` | Low | qualitative | Observed Dock range |
| `0x03` | Medium | qualitative | Observed Dock range |
| `0x04`-`0x05` | High | qualitative | Observed while charging |
| `0x06` | Full | 100% | Sustained observation after the controller LEDs turned off and `GET_INFO` reported 100 |

Only `0x06` is assigned a numeric value: it is exposed as `100%`, `Full`, and `Charged`. Lower Dock bands remain qualitative, because mapping them to percentages would invent precision that the Dock has not supplied.

## Silent full Dock

After reaching Full, the Dock can stop sending fresh EF reports. The application retains a previously confirmed `0x06` snapshot while the same Dock HID device remains available. Device removal, an invalid active report, or a new valid non-Full report replaces that cached state. This prevents a completed charge from becoming “battery unavailable” merely because the Dock is quiet.
