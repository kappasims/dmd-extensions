# Device-neutral protocol

This is the format the device-neutral destination writes. A display, or any program standing in for one,
reads it to show dmd-extensions output.

## Overview

This protocol carries dot matrix display frames one way, from a dmd-extensions destination to a
receiver that puts them on a panel. It runs over any transport that delivers bytes intact and in
order. Each transport, and anything else that builds on this protocol, is described in its own file
next to this one, starting with [SERIAL.md](SERIAL.md).

The stream is a sequence of messages. Each message is a `messageType` byte, a `panel` byte naming the
display it applies to, and content whose layout the type fixes:

```
... ][ messageType | panel | content ][ messageType | panel | content ][ ...
```

There are six message types. `Gray2`, `Gray4`, `Gray8` and `Rgb24` each carry one frame of pixels at
that depth. `Size` sets the width and height of a panel's frames until the next `Size` for that
panel. `Clear` blanks a panel.

Pixels are packed to their bit depth, top left to bottom right, with no row padding.

Over a connection the sender emits `Size` before the first frame, then one frame message per frame,
`Size` again on any size change and at least once a second, and `Clear` whenever dmd-extensions
clears the display, which always includes shutdown. The receiver sends nothing back.

Every message is preceded by a `startMarker` and a `u32` length, on every transport, and written in
one write together with them.

Configuration sets the `startMarker` and the `panel`.

All integers are unsigned and little endian.

## Messages

```
[ messageType u8 | panel u8 | content ]
```

### Fields

| field | encoding | meaning |
| --- | --- | --- |
| `messageType` | u8 | Which message this is. For frame messages, also the pixel format. |
| `panel` | u8 | Which panel the message applies to, zero based. |
| `width` | u16 | Frame width in pixels. |
| `height` | u16 | Frame height in pixels. |
| `pixels` | bytes | Packed pixel data. Its length follows from the type and the current size. |

### Message types

Value is what `messageType` holds, content is what follows `panel`, bytes is the whole message.

| message | value | content | bytes | meaning |
| --- | --- | --- | --- | --- |
| `Size` | `0x01` | `width`, `height` | 6 | Sets the frame size for one panel. |
| `Clear` | `0x02` | none | 2 | Blanks one panel. |
| `Gray2` | `0x80` | `pixels` | 2 + ceil(w*h/4) | One frame, four levels, values 0 to 3. |
| `Gray4` | `0x81` | `pixels` | 2 + ceil(w*h/2) | One frame, sixteen levels, values 0 to 15. |
| `Gray8` | `0x82` | `pixels` | 2 + w*h | One frame, 256 levels, values 0 to 255. |
| `Rgb24` | `0x83` | `pixels` | 2 + w*h*3 | One frame, eight bits per channel. |

Values are relative to the type's own depth, not to 255. A `Gray4` 15 and a `Gray8` 255 are both
fully lit.

### Value allocation

The 256 values of `messageType`:

```
range        use        bit 7
0x00 - 0x7F  control    clear
0x80 - 0xFF  frame      set
```

Bit 7 is the frame bit, so `messageType & 0x80` is nonzero exactly when a message carries pixels.
That stays true as types are added, because a new pixel format sets the bit by definition and a new
control message does not.

A sender emits only the types listed in Message types; 126 control values and 124 frame values are
unassigned and name no message. `0x00` is one of them, so an all zero buffer names no message for
the same reason any unassigned value does, with no rule of its own.

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

In dmd-extensions the destination implements neither `IFixedSizeDestination` nor
`IResizableDestination`, so the render graph doesn't scale frames for it, and each frame arrives at
its own size. The destination takes the size from each frame and emits a `Size` whenever it differs
from the last one sent. The render graph's scaler mode can still double frames of 128x32 or smaller
for every destination; the `Size` follows whatever size arrives.

## Pixels

Bit depth decides packing. There is no packing flag and no unpacked variant.

| message | layout |
| --- | --- |
| `Gray2` | 4 px/byte, first pixel in bits 7:6 |
| `Gray4` | 2 px/byte, first pixel in bits 7:4 |
| `Gray8` | 1 px/byte |
| `Rgb24` | 3 bytes/px, R then G then B |

Pixels run top left to bottom right and pack continuously across the frame. Rows are not padded to a
byte boundary, so a width that is not a multiple of the pixels per byte has rows straddling bytes.

dmd-extensions holds every gray depth as one byte per pixel
(`BytesPerPixel => BitLength <= 8 ? 1 : BitLength / 8`), so packing is a transform at every depth
rather than a special case for four bit.

## Lifecycle

What the sender emits, across one connection.

**Connect.** A `Size` before any frame. If a frame was rendered before the connection opened, the
`Size` of that frame is sent as soon as the connection opens; otherwise it goes ahead of the first
frame.

**Streaming.** One frame message per frame. A `Size` whenever width or height change, ahead of
the first frame that uses them, and at least once per second regardless.

**Idle.** Nothing of its own. dmd-extensions clears on idle only when a render graph has an
`IdleAfter` timeout, which defaults to 0 and is set only by the console's `mirror` command; the DLL
never sets it. So in Pinball FX a panel keeps its last frame while play is paused. Where a timeout
is set, its `ClearDisplay` becomes a `Clear`, or `IdlePlay` sends an image as ordinary frames.

**Shutdown.** A `Clear`, then the connection closes. dmd-extensions clears before disposing its
graphs.

## Message boundaries

```
[ startMarker 0..N | length u32 ]  [ messageType u8 | panel u8 | content ]
                                     message, counted by length
```

| field | encoding | meaning |
| --- | --- | --- |
| `startMarker` | 0..N bytes | Configured pattern before each message. Default the ASCII `DNDP`. |
| `length` | u32 | Size of the message that follows, counting neither the start marker nor itself. |

Every message is preceded by a start marker and a length, on every transport. The sender writes the
start marker, the length and the message together in one write.

Together they add four bytes plus the start marker to every message, so eight with the default
`DNDP`.

To find a message start from anywhere in the stream: scan for the start marker, read the length, skip
that many bytes, and require the start marker again immediately after.

Whether a read holds exactly one message depends on the transport; each transport's file says
which.

The start marker may be configured empty, leaving a bare length prefix. This requires the receiver to
start reading at a message boundary, which requires it to know when the sender attached. Only a
transport that signals connection provides that.

The start marker is not error detection; the transports deliver bytes intact and in order.

The length is 32 bits because `Rgb24` at 512x128 is 196,608 bytes. Nothing caps a message below what
the field expresses.

## Configuration

```ini
[deviceneutral]
; bytes in hex; "44 4E 44 50" is DNDP, the default; empty means none
startmarker = 44 4E 44 50
panel = 0
```

The keys that choose and set up the transport are described in its file.

Each display gets its own section. To drive more than one, copy the section and add a dot and a
name of your choice to its name, such as `[deviceneutral.backbox]` and `[deviceneutral.topper]`.
Each section is a separate destination with its own transport and settings. Two sections can't
share one serial port, since a port can only be opened once.

## Worked examples

`Size`, panel 0, 256x64, default start marker:

```
44 4E 44 50  06 00 00 00  01   00   00 01  40 00
D  N  D  P   len 6        type pnl  w=256  h=64
```

`Clear`, panel 0:

```
44 4E 44 50  02 00 00 00  02   00
D  N  D  P   len 2        type pnl
```

`Gray4`, panel 0, 256x64. Message 8194, which is the value in the length field:

```
44 4E 44 50  02 20 00 00  81   00   <8192 bytes>
D  N  D  P   len 8194     type pnl
```

At 256x64, sixty `Gray4` frames a second over a byte stream is 492 KB/s.

## Not carried

- No acknowledgment: the transports deliver bytes intact and in order. libzedmd acks every
  chunk because it drives a raw ESP32 UART with a small buffer.
- No checksum: same reason. A raw UART would add one alongside the start marker and length, leaving
  messages unchanged.
- No compression: libzedmd applies zlib to zone streams only.
- No version: both ends are configured together, and a version cannot live inside a start marker the
  user can set.
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
| `messageType` | command byte: Pixelcade `0x1F` `0x30` `0x40`, PinDmd3 `0x02` `0x30` `0x31` `0x32`, libzedmd `RGB888Stream` `RGB565Stream` |
| `startMarker` | frame sync bytes: `0x81 0xC3` then a command dependent third byte in PinDmd1, PinDmd2 and Pin2Dmd; `FrameStartMarker` `0xFE` in Pixelcade; `FRAME_HEADER` plus `CTRL_CHARS_HEADER` in libzedmd |
| `Size` | `FrameSize` command `0x02` in libzedmd; the `dimensions` message in `WebsocketSerializer`; the `Dimensions` type and `FixedSize` property in dmd-extensions |
| `message` | frame: `BuildFrame` in Pixelcade, and the framed unit throughout libzedmd |

The last row is a collision rather than a synonym. Pixelcade and libzedmd use frame for the wire
unit, dmd-extensions uses it for the picture. This spec uses frame only for the picture and message
for the wire unit.

`panel` has no equivalent. Every dmd-extensions destination drives exactly one device.

Four bit pixel data also carries three distinct meanings in dmd-extensions: unpacked one byte per
pixel, bit planes at eight pixels per byte per plane, and the nibble packing used here.
