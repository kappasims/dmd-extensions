# Named pipe

This describes how the [device-neutral protocol](PROTOCOL.md) runs over a named pipe, for a display
running on the same PC, such as a software display.

## Setup

The display creates the pipe, and the destination connects to it as a client, using the name set by
`pipe` in `[deviceneutral]` in place of `port` and `baudrate`. Until the pipe exists, frames are dropped and the
destination tries again every second. A write that doesn't finish within 200 ms, or a display that
disconnects, closes the connection, and the next attempt reopens it.

## Reading messages

The destination writes each message, with all of its fields, in a single write. On a pipe that the
display creates in message mode, each read therefore holds exactly one message, so `layout` needs
neither a start marker nor a length to find it. On a pipe in byte mode, reads don't line up with
messages, and a receiver finds each one from the fields `layout` lists, as described under Message
boundaries in [PROTOCOL.md](PROTOCOL.md).

The destination frames messages the same way either way, because it connects as the pipe client and
can't tell which mode the pipe was created in: on .NET Framework, a client opened for writing only
reports byte mode even when the server created the pipe in message mode.

## Example receiver

A minimal receiver in C# for the example section in [PROTOCOL.md](PROTOCOL.md), with `pipe` in
place of `port` and `baudrate`. It prints what arrives, where a display would put each frame on its
panel instead.

It creates the pipe in message mode, so each read holds exactly one message and it never has to
search for a start marker. It still checks the start marker and the length. A message that doesn't match
them or has a type it doesn't know comes back as a `DeviceNeutralSkippedMessage` that says why, and
the printer shows it as a warning.

It takes the pipe name as its argument, `dmd-extensions` if none is given, which has to match `pipe`
in the section. It handles one connection and exits when the destination disconnects, for example
when a game closes. The destination tries to connect every second, so running the receiver again
picks up the next game. It builds as C# 7.3 on .NET Framework.

`PipeMessageReader` reads each message from the pipe, checks it against the byte layout of the
example section, and hands back a `DeviceNeutralSizeMessage`, `DeviceNeutralClearMessage` or
`DeviceNeutralFrameMessage`. `MessagePrinter` stands in for what a display does with each one.
These message types and `MessagePrinter` are the same as in the serial receiver in
[SERIAL.md](SERIAL.md). The reader differs, and a `DeviceNeutralDisconnectMessage` marks the end of
the connection.

```csharp
using System;
using System.IO.Pipes;

internal static class Program
{
	private static void Main(string[] args)
	{
		var name = args.Length > 0 ? args[0] : "dmd-extensions";
		var serverInstances = 1;
		var printer = new MessagePrinter();

		using (var reader = new PipeMessageReader(name, serverInstances)) {
			reader.WaitForConnection();
			Console.WriteLine($"Connected on \\\\.\\pipe\\{name}.");

			while (reader.IsConnected) {
				reader.ReadMessage().Print(printer);
			}
			Console.WriteLine("Disconnected.");
		}
	}
}

// Creates a named pipe in message mode and reads messages from it in the byte layout of the example section in PROTOCOL.md.
public class PipeMessageReader : IDisposable
{
	private static readonly byte[] StartMarker = { 0x44, 0x4E, 0x44, 0x50 };
	private const int LengthAt = 4;
	private const int TypeAt = LengthAt + sizeof(int);
	private const int PanelAt = TypeAt + 1;
	private const int HeaderLength = PanelAt + 1;

	// The type bytes of the example section.
	private const byte SizeType = 0x01;
	private const byte ClearType = 0x02;
	private const byte Gray2Type = 0x80;
	private const byte Gray4Type = 0x81;
	private const byte Gray8Type = 0x82;
	private const byte Rgb24Type = 0x83;

	private readonly NamedPipeServerStream _pipe;
	private readonly byte[] _header = new byte[HeaderLength];

	public PipeMessageReader(string pipeName, int serverInstances)
	{
		_pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, serverInstances, PipeTransmissionMode.Message);
	}

	public void WaitForConnection()
	{
		_pipe.WaitForConnection();
	}

	public bool IsConnected {
		get { return _pipe.IsConnected; }
	}

	// Reads one pipe message.
	public DeviceNeutralMessage ReadMessage()
	{
		var headerLength = _pipe.Read(_header, 0, HeaderLength);
		if (headerLength == 0) {
			return new DeviceNeutralDisconnectMessage();
		}
		if (headerLength < HeaderLength) {
			return new DeviceNeutralSkippedMessage($"{headerLength} bytes, fewer than a header");
		}

		var markerMatched = 0;
		while (markerMatched < StartMarker.Length && _header[markerMatched] == StartMarker[markerMatched]) {
			markerMatched++;
		}
		var length = _header[LengthAt] | _header[LengthAt + 1] << 8 | _header[LengthAt + 2] << 16 | _header[LengthAt + 3] << 24;
		var contentLength = length - (HeaderLength - TypeAt);
		var type = _header[TypeAt];
		var panel = _header[PanelAt];

		var content = new byte[markerMatched == StartMarker.Length && contentLength > 0 ? contentLength : 0];
		var contentRead = 0;
		while (!_pipe.IsMessageComplete) {
			// Bytes past the content array are read into the header array and dropped.
			var read = contentRead < content.Length
				? _pipe.Read(content, contentRead, content.Length - contentRead)
				: _pipe.Read(_header, 0, HeaderLength);
			if (read == 0) {
				return new DeviceNeutralDisconnectMessage();
			}
			contentRead += read;
		}

		if (markerMatched < StartMarker.Length) {
			return new DeviceNeutralSkippedMessage("no start marker");
		}
		if (contentLength < 0) {
			return new DeviceNeutralSkippedMessage("a negative length field");
		}
		if (contentRead != contentLength) {
			return new DeviceNeutralSkippedMessage("a length field that doesn't match its size");
		}

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
			_pipe.Dispose();
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

public class DeviceNeutralDisconnectMessage : DeviceNeutralMessage
{
	public override void Print(MessagePrinter printer)
	{
	}
}
```
