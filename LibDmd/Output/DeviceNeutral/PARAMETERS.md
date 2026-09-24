# Parameters

[PROTOCOL.md](PROTOCOL.md) describes the device-neutral protocol with its default parameters. The
`[deviceneutral]` section can change them, so the destination sends what an existing display
already expects. With every parameter at its default, the destination sends exactly the format
that PROTOCOL.md describes.

| key | default | meaning |
| --- | --- | --- |
| `layout` | `startmarker length type panel content` | The fields of each message, in order. Each field appears at most once, and `content` is required. |
| `startmarker` | `44 4E 44 50` | The bytes of the start marker field, in hex. |
| `endmarker` | empty | The bytes of the end marker field, in hex. |
| `length` | `u32le` | How the length field is written: `u32le`, `u16le`, `u16be` or `none`. |
| `type.size`, `type.clear`, `type.gray2`, `type.gray4`, `type.gray8`, `type.rgb24` | the values in Message types | The type byte of each message, in hex. |
| `fixedsize` | empty | A size such as `128x32`. When it's set, dmd-extensions scales every frame to it. |
| `colororder` | `rgb` | The channel order of `Rgb24` frames. `rbg` swaps green and blue. |
| `connect` | empty | Bytes written once each time a connection opens, before any message. |

The length counts every byte after the length field, not counting the end marker. A message too long
for a 16-bit length isn't sent, and the destination logs a warning the first time that happens.

An invalid value is logged and replaced by its default.
