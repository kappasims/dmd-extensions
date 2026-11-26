# Device-neutral protocol

This describes the messages the device-neutral destination sends, and the parameters that decide how
they're written. Every parameter is set in the destination's section of `DmdDevice.ini`; none has a
built-in value. A display, or any program standing in for one, reads the result.

## Overview

This protocol carries dot matrix display frames one way, from a dmd-extensions destination to a
receiver that puts them on a panel. It runs over any transport that delivers bytes intact and in
order. Each transport, and anything else that builds on this protocol, is described in its own file
next to this one, starting with [SERIAL.md](SERIAL.md). [PARAMETERS.md](PARAMETERS.md) lists every
key.

The stream is a sequence of messages. Each message is made of fields, written in the order `layout`
gives. `content` is always there; the others are there only if `layout` lists them:

```
... ][ startmarker | length | type | panel | content | endmarker ][ ...
```

There are six messages. `Gray2`, `Gray4`, `Gray8` and `Rgb24` each carry one frame of pixels at that
depth. `Size` sets the width and height of a panel's frames until the next `Size` for that panel.
`Clear` blanks a panel. The `messages` key lists which of them are sent, and a key per message, such
as `type.size` or `type.gray4`, sets the byte that identifies it.

Pixels are packed to their bit depth, top left to bottom right, with no row padding.

Over a connection the sender writes the `connect` bytes, then `Size` before the first frame, one
frame message per frame, `Size` again on any size change and at least once a second, and `Clear`
whenever dmd-extensions clears the display, which always includes shutdown. `Size` and `Clear` are
sent only if `messages` lists them. The receiver sends nothing back.

Every message is written in one write.

All integers are unsigned.

## Messages

### Fields

| field | key | encoding | meaning |
| --- | --- | --- | --- |
| `startmarker` | `startmarker` | 1..N bytes | A fixed pattern that starts each message. |
| `length` | `length` | u32 or u16, little or big endian | The number of bytes after the length field, not counting the end marker. |
| `type` | `type.size`, `type.clear`, `type.gray2`, `type.gray4`, `type.gray8`, `type.rgb24` | u8 | Which message this is. For frame messages, also the depth of the pixels. |
| `panel` | `panel` | u8 | Which panel the message applies to, zero based. |
| `content` | | bytes | The message's content, laid out by its type. |
| `endmarker` | `endmarker` | 1..N bytes | A fixed pattern that ends each message. |

A field that `layout` doesn't list isn't written, and its key must not be set.

### Content

| message | content | content bytes | meaning |
| --- | --- | --- | --- |
| `Size` | `width` u16 le, `height` u16 le | 4 | Sets the frame size for one panel. |
| `Clear` | none | 0 | Blanks one panel. |
| `Gray2` | `pixels` | ceil(w*h/4) | One frame, four levels, values 0 to 3. |
| `Gray4` | `pixels` | ceil(w*h/2) | One frame, sixteen levels, values 0 to 15. |
| `Gray8` | `pixels` | w*h | One frame, 256 levels, values 0 to 255. |
| `Rgb24` | `pixels` | w*h*3 | One frame, eight bits per channel. |

Values are relative to the message's own depth, not to 255. A `Gray4` 15 and a `Gray8` 255 are both
fully lit.

### Types

Each message that `messages` lists gets its own type byte, and no two can share one. A frame of a
depth that isn't listed is converted to one that is, by the render graph's usual conversions. Nothing
converts to `Gray8`, so a section that lists only `gray8` gets only `Gray8` frames.

If `layout` doesn't list `type`, nothing tells messages apart, so `messages` must list exactly one.
A receiver that takes only `Rgb24` frames at one size, for example, needs neither a type byte nor
`Size`.

### Panels

A panel is one physical display. `panel` identifies it within a stream and means nothing beyond
identity, so indices need not be contiguous and none is special.

Size is per panel: a `Size` message applies to the panel it names and no other, so several panels on
one link each have their own.

Nothing enumerates panels. An index exists once a message arrives for it.

A destination has one configured index and emits only that, so a link from one destination carries
one panel.

### Size

`Size` is the only message later messages depend on. Every other message is interpreted on its own.

Its `width` and `height` become the current size for the panel it names and hold until the next
`Size` for that panel. They fix the length of `pixels` in every frame message for that panel.
A `Clear` blanks a panel and leaves its size as it was.

A `Size` is emitted ahead of the first frame that uses the new size, never after it. A stream
changing size mid-play is ordinary; dmd-extensions was observed moving between 128x32, 32x8 and
128x16 inside a single table.

With `fixedsize = none`, the destination implements neither `IFixedSizeDestination` nor
`IResizableDestination`, so the render graph doesn't scale frames for it, and each frame arrives at
its own size. The destination takes the size from each frame and emits a `Size` whenever it differs
from the last one sent. The render graph's scaler mode can still double frames of 128x32 or smaller
for every destination; the `Size` follows whatever size arrives.

With `fixedsize` set to a size such as `128x32`, the render graph scales every frame to it. A
receiver that only ever shows that size can leave `size` out of `messages`.

## Pixels

Bit depth decides packing. There is no packing flag and no unpacked variant.

| message | layout |
| --- | --- |
| `Gray2` | 4 px/byte, first pixel in bits 7:6 |
| `Gray4` | 2 px/byte, first pixel in bits 7:4 |
| `Gray8` | 1 px/byte |
| `Rgb24` | 3 bytes/px, in the order `colororder` gives: R, G, B for `rgb`, or R, B, G for `rbg` |

Pixels run top left to bottom right and pack continuously across the frame. Rows are not padded to a
byte boundary, so a width that is not a multiple of the pixels per byte has rows straddling bytes.

dmd-extensions holds every gray depth as one byte per pixel
(`BytesPerPixel => BitLength <= 8 ? 1 : BitLength / 8`), so packing is a transform at every depth
rather than a special case for four bit.

## Lifecycle

What the sender emits, across one connection.

**Connect.** The `connect` bytes, unless `connect = none`. Then, if `messages` lists `size`, a `Size`
before any frame. If a frame was rendered before the connection opened, the `Size` of that frame is
sent as soon as the connection opens; otherwise it goes ahead of the first frame.

**Streaming.** One frame message per frame. If `messages` lists `size`, a `Size` whenever width or
height change, ahead of the first frame that uses them, and at least once per second regardless.

**Idle.** Nothing of its own. dmd-extensions clears on idle only when a render graph has an
`IdleAfter` timeout, which defaults to 0 and is set only by the console's `mirror` command; the DLL
never sets it. So in Pinball FX a panel keeps its last frame while play is paused. Where a timeout
is set, its `ClearDisplay` becomes a `Clear`, or `IdlePlay` sends an image as ordinary frames.

**Shutdown.** A `Clear` if `messages` lists `clear`, then the connection closes. dmd-extensions
clears before disposing its graphs.

## Message boundaries

A receiver finds where each message starts and ends from the fields `layout` lists:

- With `startmarker` and `length`, scan for the start marker, read the length, skip that many bytes,
  and require the start marker again immediately after.
- With `endmarker` and no `length`, read up to the end marker. This only works if the end marker
  can't appear inside a message, which pixels don't guarantee.
- With `length` and no start marker, the receiver has to start reading at a message boundary, which
  requires it to know when the sender attached. Only a transport that signals connection provides
  that.
- With neither, every message has to have a length the receiver already knows, such as frames of
  one type at a fixed size.

Whether a read holds exactly one message depends on the transport; each transport's file says
which.

The start and end markers aren't error detection; the transports deliver bytes intact and in order.

A `u32le` length covers any frame; `Rgb24` at 512x128 is 196,608 bytes. A message too long for a
`u16le` or `u16be` length isn't sent, and the destination logs a warning the first time that
happens.

## Example section

A complete section. A display that has no protocol of its own can use it as it is, or change
whatever it needs; a display that already has a protocol sets the values that match it.

```ini
[deviceneutral]
enabled = true
port = COM4
baudrate = 921600
layout = startmarker length type panel content
; the ASCII characters DNDP
startmarker = 44 4E 44 50
length = u32le
panel = 0
messages = size clear gray2 gray4 gray8 rgb24
type.size = 01
type.clear = 02
type.gray2 = 80
type.gray4 = 81
type.gray8 = 82
type.rgb24 = 83
colororder = rgb
fixedsize = none
connect = none
```

`port` and `baudrate` belong to the serial transport and are described in [SERIAL.md](SERIAL.md).

Each display gets its own section. To drive more than one, copy the section and add a dot and a
name of your choice to its name, such as `[deviceneutral.backbox]` and `[deviceneutral.topper]`.
Each section is a separate destination with its own transport and settings. Two sections can't
share one serial port, since a port can only be opened once.

## Worked examples

These use the example section.

`Size`, panel 0, 256x64:

```
44 4E 44 50  06 00 00 00  01    00     00 01      40 00
D  N  D  P   length 6     type  panel  width 256  height 64
```

`Clear`, panel 0:

```
44 4E 44 50  02 00 00 00  02    00
D  N  D  P   length 2     type  panel
```

`Gray4`, panel 0, 256x64:

```
44 4E 44 50  02 20 00 00  81    00     <8192 bytes>
D  N  D  P   length 8194  type  panel  pixels
```

At 256x64, sixty `Gray4` frames a second over a byte stream is 492 KB/s.

## Not carried

- No acknowledgment: the transports deliver bytes intact and in order. libzedmd acks every
  chunk because it drives a raw ESP32 UART with a small buffer.
- No checksum: same reason.
- No compression: libzedmd applies zlib to zone streams only.
- No version: both ends are configured together.
- No deduplication: `NeedsDuplicateFrames => false` makes the render graph subscribe with
  `dedupe: true`, so duplicates do not reach a destination. Pixelcade and Pin2Dmd dedupe again
  because RGB565 downsampling and plane splitting can map distinct frames onto identical bytes;
  every packing here is lossless, so that cannot arise.
- No palette or color: the receiver owns its panel, including what color it lights.

## Names elsewhere

The same concepts appear under different names across dmd-extensions and libzedmd. When reading
that code, these are equivalent:

| here | elsewhere |
| --- | --- |
| `type` | command byte: Pixelcade `0x1F` `0x30` `0x40`, PinDmd3 `0x02` `0x30` `0x31` `0x32`, libzedmd `RGB888Stream` `RGB565Stream` |
| `startmarker` | frame sync bytes: `0x81 0xC3` then a command dependent third byte in PinDmd1, PinDmd2 and Pin2Dmd; `FrameStartMarker` `0xFE` in Pixelcade; `FRAME_HEADER` plus `CTRL_CHARS_HEADER` in libzedmd |
| `Size` | `FrameSize` command `0x02` in libzedmd; the `dimensions` message in `WebsocketSerializer`; the `Dimensions` type and `FixedSize` property in dmd-extensions |
| `message` | frame: `BuildFrame` in Pixelcade, and the framed unit throughout libzedmd |

The last row is a collision rather than a synonym. Pixelcade and libzedmd use frame for the wire
unit, dmd-extensions uses it for the picture. This spec uses frame only for the picture and message
for the wire unit.

`panel` has no equivalent. Every dmd-extensions destination drives exactly one display.

Four bit pixel data also carries three distinct meanings in dmd-extensions: unpacked one byte per
pixel, bit planes at eight pixels per byte per plane, and the nibble packing used here.
