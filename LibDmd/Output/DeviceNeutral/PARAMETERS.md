# Parameters

Every key of a `[deviceneutral]` section. [PROTOCOL.md](PROTOCOL.md) describes what the protocol
keys mean on the wire, and has a complete example section.

None of the keys has a built-in value. Apart from `enabled`, a key is either required or must not be
set, depending on the other keys, as the table says. When `enabled = true` and a key is missing, has
an invalid value, or is set where it must not be, the destination logs an error naming the section
and the key, and that section isn't used.

| key | required | values | meaning |
| --- | --- | --- | --- |
| `enabled` | no; off unless `true` | `true` or `false` | Whether the section is used. |
| `port` | always | a port name, such as `COM4`, or `usb:` and a USB vendor and product ID in hex, such as `usb:2E8A:000A` | The serial port of the display. See [SERIAL.md](SERIAL.md). |
| `baudrate` | always | a positive integer, such as `921600` | The baud rate. USB CDC devices ignore it. |
| `layout` | always | `startmarker`, `length`, `type`, `panel`, `content` and `endmarker`, each at most once, in order | The fields of each message. `content` is required. |
| `startmarker` | when `layout` lists it | 1 or more bytes in hex, such as `AA 55` | The start marker field. |
| `length` | when `layout` lists it | `u32le`, `u16le` or `u16be` | How the length field is written. |
| `panel` | when `layout` lists it | 0 to 255 | The panel field. |
| `endmarker` | when `layout` lists it | 1 or more bytes in hex | The end marker field. |
| `messages` | always | `size`, `clear`, `gray2`, `gray4`, `gray8` and `rgb24`, each at most once, with at least one frame message | The messages that are sent. Frames of a depth that isn't listed are dropped. Without `type` in `layout`, exactly one. |
| `type.size`, `type.clear`, `type.gray2`, `type.gray4`, `type.gray8`, `type.rgb24` | when `layout` lists `type` and `messages` lists the message | 1 byte in hex, such as `80`, different for each message | The type field of each message. |
| `colororder` | when `messages` lists `rgb24` | `rgb` or `rbg` | The channel order of `Rgb24` frames. `rbg` swaps green and blue. |
| `fixedsize` | always | `none`, or a size such as `128x32` | With a size, dmd-extensions scales every frame to it. With `none`, frames are sent at their own size. |
| `connect` | always | `none`, or bytes in hex | Bytes written once each time a connection opens, before any message. |

The length counts every byte after the length field, not counting the end marker. A message too long
for a 16-bit length isn't sent, and the destination logs a warning the first time that happens.
