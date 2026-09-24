# Named pipe

This describes how the [device-neutral protocol](PROTOCOL.md) runs over a named pipe, for a display
running on the same PC, such as a software display.

## Setup

The display creates the pipe, and the destination connects to it as a client, using the name set by
`pipe` in `[deviceneutral]` instead of `port`. Until the pipe exists, frames are dropped and the
destination tries again every second. A write that doesn't finish within 200 ms, or a display that
disconnects, closes the connection, and the next attempt reopens it.

## Reading messages

The destination writes each message, with its start marker and length, in a single write. On a pipe
that the display creates in message mode, each read therefore holds exactly one start marker, length
and message. On a pipe in byte mode, reads don't line up with messages, and a receiver finds each one
by its start marker and length, as described under Message boundaries in [PROTOCOL.md](PROTOCOL.md).

The destination frames messages the same way either way, because it connects as the pipe client and
can't tell which mode the pipe was created in: on .NET Framework, a client opened for writing only
reports byte mode even when the server created the pipe in message mode.

## Example receiver

A minimal receiver in C#. It creates the pipe in message mode, so each read holds one whole message
and there's no start marker to search for; it only checks the start marker and length. It prints
what arrives, where a display would put each frame on its panel instead, and skips unknown message
types and frames whose length doesn't match the current size. It builds as C# 7.3 on .NET
Framework.

Set `pipe` in `[deviceneutral]` to the name passed to the receiver, `deviceneutral` by default. The
receiver waits for the destination again after it disconnects, for example when a game closes.

```csharp
using System;
using System.IO;
using System.IO.Pipes;

static class PipeReceiver
{
	// Must match startmarker in the [deviceneutral] section.
	static readonly byte[] StartMarker = { 0x44, 0x4E, 0x44, 0x50 };

	static void Main(string[] args)
	{
		var name = args.Length > 0 ? args[0] : "deviceneutral";
		var widths = new int[256];
		var heights = new int[256];

		using (var pipe = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Message)) {
			while (true) {
				pipe.WaitForConnection();
				Console.WriteLine($"connected on \\\\.\\pipe\\{name}");

				byte[] packet;
				while ((packet = ReadPipeMessage(pipe)) != null) {
					var message = Unwrap(packet);
					if (message != null) {
						Handle(message, widths, heights);
					}
				}
				pipe.Disconnect();
				Console.WriteLine("disconnected");
			}
		}
	}

	// Reads one pipe message, which holds one start marker, length and message. Returns null when the
	// sender disconnects.
	static byte[] ReadPipeMessage(NamedPipeServerStream pipe)
	{
		var packet = new MemoryStream();
		var buffer = new byte[64 * 1024];
		do {
			var n = pipe.Read(buffer, 0, buffer.Length);
			if (n == 0) {
				return null;
			}
			packet.Write(buffer, 0, n);
		} while (!pipe.IsMessageComplete);
		return packet.ToArray();
	}

	// Returns the message inside a packet, or null if the start marker or length doesn't match.
	static byte[] Unwrap(byte[] packet)
	{
		var headerBytes = StartMarker.Length + 4;
		if (packet.Length < headerBytes + 2) {
			return null;
		}
		for (var i = 0; i < StartMarker.Length; i++) {
			if (packet[i] != StartMarker[i]) {
				return null;
			}
		}
		var at = StartMarker.Length;
		var length = packet[at] | packet[at + 1] << 8 | packet[at + 2] << 16 | packet[at + 3] << 24;
		if (length != packet.Length - headerBytes) {
			return null;
		}
		var message = new byte[length];
		Array.Copy(packet, headerBytes, message, 0, length);
		return message;
	}

	static void Handle(byte[] message, int[] widths, int[] heights)
	{
		var type = message[0];
		var panel = message[1];

		if (type == 0x01 && message.Length == 6) {
			widths[panel] = message[2] | message[3] << 8;
			heights[panel] = message[4] | message[5] << 8;
			Console.WriteLine($"panel {panel}: size {widths[panel]}x{heights[panel]}");

		} else if (type == 0x02) {
			Console.WriteLine($"panel {panel}: clear");

		} else if ((type & 0x80) != 0) {
			var expected = PixelBytes(type, widths[panel] * heights[panel]);
			if (expected > 0 && message.Length - 2 == expected) {
				Console.WriteLine($"panel {panel}: frame 0x{type:X2}, {expected} bytes");
			}
		}
	}

	// Returns the pixel bytes of a frame type at the given pixel count, or 0 for an unknown type.
	static int PixelBytes(byte type, int pixels)
	{
		switch (type) {
			case 0x80: return (pixels + 3) / 4;
			case 0x81: return (pixels + 1) / 2;
			case 0x82: return pixels;
			case 0x83: return pixels * 3;
			default: return 0;
		}
	}
}
```
