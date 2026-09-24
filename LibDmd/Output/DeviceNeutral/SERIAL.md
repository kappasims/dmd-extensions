# Serial port

This describes how the [device-neutral protocol](PROTOCOL.md) runs over a serial port.

## Setup

The destination opens the port named by `port` in `[deviceneutral]` at `baudrate`, 921600 by
default. USB CDC devices, such as a microcontroller's USB serial port, ignore the baud rate. The
destination enables DTR when it opens the port, since USB CDC devices use DTR to detect that a host
has the port open.

When the port isn't there, frames are dropped and the destination tries again every second. A write
that doesn't finish within 200 ms closes the port, and the next attempt reopens it.

## Reading messages

On a serial port, message boundaries don't align with read boundaries: a message may arrive over
several reads, and a read may hold several messages. A receiver finds each message by its start
marker and length, as described under Message boundaries in [PROTOCOL.md](PROTOCOL.md). A serial
port doesn't tell the receiver when the sender connects, so a serial link needs a start marker.

USB CDC delivers bytes intact and in order. A raw UART doesn't guarantee that, and the protocol has
no checksum.

## Example receiver

A minimal receiver in C#. It reads messages from a serial port and prints
what arrives; a display would put each frame on its panel instead. It skips unknown message types
and frames whose length doesn't match the current size.

It builds as C# 7.3 on .NET Framework, where `System.IO.Ports` is part of the framework. On .NET 8
and later, add the `System.IO.Ports` package. To try it on one PC, connect two virtual serial ports
with a null modem emulator, set `port` in `[deviceneutral]` to one of them, and pass the other to the
receiver.

On a microcontroller the loop is the same over the bytes read from USB CDC: find the start marker,
read the length, read that many bytes, and handle the message.

```csharp
using System;
using System.IO;
using System.IO.Ports;

static class SerialReceiver
{
	// Must match startmarker in the [deviceneutral] section.
	static readonly byte[] StartMarker = { 0x44, 0x4E, 0x44, 0x50 };

	const int MaxMessageBytes = 16 * 1024 * 1024;

	static void Main(string[] args)
	{
		var port = new SerialPort(args.Length > 0 ? args[0] : "COM4", 921600);
		port.Open();
		var stream = port.BaseStream;

		var widths = new int[256];
		var heights = new int[256];

		while (true) {
			var message = ReadMessage(stream);
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

	// Reads the next message, skipping any bytes before a start marker.
	static byte[] ReadMessage(Stream stream)
	{
		while (true) {
			SkipToStartMarker(stream);
			var header = ReadExactly(stream, 4);
			var length = header[0] | header[1] << 8 | header[2] << 16 | header[3] << 24;
			if (length >= 2 && length <= MaxMessageBytes) {
				return ReadExactly(stream, length);
			}
		}
	}

	static void SkipToStartMarker(Stream stream)
	{
		var window = new byte[StartMarker.Length];
		var seen = 0;
		while (seen < window.Length || !Matches(window)) {
			var b = stream.ReadByte();
			if (b < 0) {
				throw new EndOfStreamException();
			}
			Array.Copy(window, 1, window, 0, window.Length - 1);
			window[window.Length - 1] = (byte)b;
			seen++;
		}
	}

	static bool Matches(byte[] window)
	{
		for (var i = 0; i < window.Length; i++) {
			if (window[i] != StartMarker[i]) {
				return false;
			}
		}
		return true;
	}

	static byte[] ReadExactly(Stream stream, int count)
	{
		var buffer = new byte[count];
		var read = 0;
		while (read < count) {
			var n = stream.Read(buffer, read, count - read);
			if (n == 0) {
				throw new EndOfStreamException();
			}
			read += n;
		}
		return buffer;
	}
}
```
