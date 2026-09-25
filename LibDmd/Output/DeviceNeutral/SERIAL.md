# Serial port

This describes how the [device-neutral protocol](PROTOCOL.md) runs over a serial port.

## Setup

The destination opens the port named by `port` in `[deviceneutral]` at `baudrate`. USB CDC devices, such as a microcontroller's USB serial port, ignore the baud rate. The
destination enables DTR when it opens the port, since USB CDC devices use DTR to detect that a host
has the port open.

Windows can give a display on USB a new port number, for example after it's plugged into another
USB port. To find the display by its USB ID instead, set `port` to `usb:` followed by its vendor and
product ID, four hex digits each, such as `usb:2E8A:000A` for a Raspberry Pi Pico's USB serial
port. Each attempt to open the port looks the ID up again, among the ports that exist. When several
ports match, such as with two of the same display plugged in, the lowest-numbered one is used.

When the port isn't there, frames are dropped and the destination tries again every second. A write
that doesn't finish within 200 ms closes the port, and the next attempt reopens it.

## Reading messages

On a serial port, message boundaries don't align with read boundaries: a message may arrive over
several reads, and a read may hold several messages. A receiver finds each message by its start
marker and length, as described under Message boundaries in [PROTOCOL.md](PROTOCOL.md). A serial
port doesn't tell the receiver when the sender connects, so on a serial link `layout` needs a start
marker, unless every message has a length the receiver already knows.

USB CDC delivers bytes intact and in order. A raw UART doesn't guarantee that, and the protocol has
no checksum.

## Example receiver

A minimal receiver in C# for the example section in [PROTOCOL.md](PROTOCOL.md). It prints what
arrives, where a display would put each frame on its panel instead.

`SerialMessageReader` finds each message by its start marker, since a serial port doesn't mark where
one ends, then reads the length, type and panel after it and as much content as the length gives.
It hands back a `DeviceNeutralSizeMessage`, `DeviceNeutralClearMessage` or
`DeviceNeutralFrameMessage`, and `MessagePrinter` stands in for what a display does with each one.
A message with a type it doesn't know, or with a length past the largest frame, which means the
start marker matched inside pixels, comes back as a `DeviceNeutralSkippedMessage` that says why,
and the printer shows it as a warning.

It takes the port name as its argument, `COM4` if none is given. A serial port doesn't report that
the sender went away, so the receiver runs until it's stopped. It builds as C# 7.3 on .NET
Framework, where `System.IO.Ports` is part of the framework. On .NET 8 and later, add the
`System.IO.Ports` package. To try it on one PC, connect two virtual serial ports with a null modem
emulator, use the example section with `port` set to one of them, and pass the other to the
receiver.

On a microcontroller the loop is the same over the bytes read from USB CDC: find the start marker,
read the length, read that many bytes, and handle the message.

```csharp
using System;
using System.IO;
using System.IO.Ports;

internal static class Program
{
	private static void Main(string[] args)
	{
		var portName = args.Length > 0 ? args[0] : "COM4";
		var baudRate = 921600;
		var printer = new MessagePrinter();

		using (var reader = new SerialMessageReader(portName, baudRate)) {
			Console.WriteLine($"Reading from {portName}.");

			while (reader.IsOpen) {
				reader.ReadMessage().Print(printer);
			}
		}
	}
}

// Opens a serial port and reads messages from it in the byte layout of the example section in PROTOCOL.md.
public class SerialMessageReader : IDisposable
{
	private static readonly byte[] StartMarker = { 0x44, 0x4E, 0x44, 0x50 };

	// The type and panel, which the length field counts along with the content.
	private const int TypeAndPanelLength = 2;

	// A length past the largest frame in PROTOCOL.md, 512x128 in Rgb24, means the start marker matched inside pixels.
	private const int MaxContentLength = 512 * 128 * 3;

	// The type bytes of the example section.
	private const byte SizeType = 0x01;
	private const byte ClearType = 0x02;
	private const byte Gray2Type = 0x80;
	private const byte Gray4Type = 0x81;
	private const byte Gray8Type = 0x82;
	private const byte Rgb24Type = 0x83;

	private readonly SerialPort _port;
	private readonly BinaryReader _reader;

	public SerialMessageReader(string portName, int baudRate)
	{
		_port = new SerialPort(portName, baudRate);
		_port.Open();
		_reader = new BinaryReader(_port.BaseStream);
	}

	public bool IsOpen {
		get { return _port.IsOpen; }
	}

	// Reads the next message after a start marker.
	public DeviceNeutralMessage ReadMessage()
	{
		var window = new byte[StartMarker.Length];
		for (var i = 1; i < window.Length; i++) {
			window[i] = _reader.ReadByte();
		}
		var markerMatched = 0;
		while (markerMatched < StartMarker.Length) {
			Array.Copy(window, 1, window, 0, window.Length - 1);
			window[window.Length - 1] = _reader.ReadByte();
			markerMatched = 0;
			while (markerMatched < StartMarker.Length && window[markerMatched] == StartMarker[markerMatched]) {
				markerMatched++;
			}
		}

		int length = _reader.ReadByte();
		length |= _reader.ReadByte() << 8;
		length |= _reader.ReadByte() << 16;
		length |= _reader.ReadByte() << 24;
		var contentLength = length - TypeAndPanelLength;
		var type = _reader.ReadByte();
		var panel = _reader.ReadByte();
		if (contentLength < 0 || contentLength > MaxContentLength) {
			return new DeviceNeutralSkippedMessage($"a content length of {contentLength}, which means the start marker matched inside pixels");
		}
		var content = _reader.ReadBytes(contentLength);

		switch (type) {
			case SizeType:
				return new DeviceNeutralSizeMessage(panel, content);

			case ClearType:
				return new DeviceNeutralClearMessage(panel);

			case Gray2Type:
				return new DeviceNeutralFrameMessage(panel, 2, content);

			case Gray4Type:
				return new DeviceNeutralFrameMessage(panel, 4, content);

			case Gray8Type:
				return new DeviceNeutralFrameMessage(panel, 8, content);

			case Rgb24Type:
				return new DeviceNeutralFrameMessage(panel, 24, content);

			default:
				return new DeviceNeutralSkippedMessage($"unknown type 0x{type:X2}");
		}
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing) {
			_reader.Dispose();
			_port.Dispose();
		}
	}
}

// Stands in for a display: prints each message instead of putting it on a panel.
public class MessagePrinter
{
	public void PrintMessage(DeviceNeutralSizeMessage message)
	{
		Console.WriteLine($"Panel {message.Panel}: size {message.Width}x{message.Height}");
	}

	public void PrintMessage(DeviceNeutralClearMessage message)
	{
		Console.WriteLine($"Panel {message.Panel}: clear");
	}

	public void PrintMessage(DeviceNeutralFrameMessage message)
	{
		Console.WriteLine($"Panel {message.Panel}: {message.BitsPerPixel}-bit frame, {message.Pixels.Length} bytes");
	}

	public void PrintMessage(DeviceNeutralSkippedMessage message)
	{
		Console.Error.WriteLine($"Skipped a message with {message.Reason}.");
	}
}

public abstract class DeviceNeutralMessage
{
	public abstract void Print(MessagePrinter printer);
}

public class DeviceNeutralSizeMessage : DeviceNeutralMessage
{
	public DeviceNeutralSizeMessage(byte panel, byte[] content)
	{
		Panel = panel;
		Width = content[0] | content[1] << 8;
		Height = content[2] | content[3] << 8;
	}

	public byte Panel { get; }

	public int Width { get; }

	public int Height { get; }

	public override void Print(MessagePrinter printer)
	{
		printer.PrintMessage(this);
	}
}

public class DeviceNeutralClearMessage : DeviceNeutralMessage
{
	public DeviceNeutralClearMessage(byte panel)
	{
		Panel = panel;
	}

	public byte Panel { get; }

	public override void Print(MessagePrinter printer)
	{
		printer.PrintMessage(this);
	}
}

public class DeviceNeutralFrameMessage : DeviceNeutralMessage
{
	public DeviceNeutralFrameMessage(byte panel, int bitsPerPixel, byte[] pixels)
	{
		Panel = panel;
		BitsPerPixel = bitsPerPixel;
		Pixels = pixels;
	}

	public byte Panel { get; }

	public int BitsPerPixel { get; }

	public byte[] Pixels { get; }

	public override void Print(MessagePrinter printer)
	{
		printer.PrintMessage(this);
	}
}

public class DeviceNeutralSkippedMessage : DeviceNeutralMessage
{
	public DeviceNeutralSkippedMessage(string reason)
	{
		Reason = reason;
	}

	public string Reason { get; }

	public override void Print(MessagePrinter printer)
	{
		printer.PrintMessage(this);
	}
}
```
