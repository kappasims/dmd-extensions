using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents a destination that sends frames to a receiver through an <see cref="IDeviceNeutralTransport"/>, in
	/// the format described in PROTOCOL.md.
	/// </summary>
	/// <remarks>
	/// Frames are sent at their own size. A <see cref="DeviceNeutralMessageType.Size"/> message precedes the first
	/// frame of each size and is repeated every second. A frame whose type isn't among the messages that
	/// are sent is converted to one that is, using the color and palette set on the destination.
	/// </remarks>
	public class DeviceNeutralDestination : IGray2Destination, IGray4Destination, IGray8Destination, IRgb24Destination
	{
		public string Name => "DeviceNeutral";

		/// <summary>
		/// Gets a value indicating whether the destination can be used.
		/// </summary>
		/// <remarks>
		/// This property always returns <see langword="true"/>. The destination connects on demand and
		/// retries every second while no receiver is connected.
		/// </remarks>
		public bool IsAvailable => true;

		public bool NeedsDuplicateFrames => false;
		public bool NeedsIdentificationFrames => false;

		/// <summary>
		/// Gets every message type that the destination sends by default.
		/// </summary>
		public static IReadOnlyCollection<DeviceNeutralMessageType> AllMessages => new[] {
			DeviceNeutralMessageType.Size, DeviceNeutralMessageType.Clear, DeviceNeutralMessageType.Gray2, DeviceNeutralMessageType.Gray4, DeviceNeutralMessageType.Gray8, DeviceNeutralMessageType.Rgb24
		};

		private const int TickMs = 1000;

		private readonly IDeviceNeutralTransport _transport;
		private readonly DeviceNeutralMessageWriter _writer;
		private readonly byte _panel;
		private readonly byte[] _connectBytes;
		private readonly HashSet<DeviceNeutralMessageType> _messages;
		private readonly object _lock = new object();
		private readonly Timer _timer;

		private Dimensions _size;
		private bool _hasSize;
		private bool _sizeSent;
		private bool _disposed;
		private bool _encodeWarned;
		private bool _convertWarned;
		private Color _color = RenderGraph.DefaultColor;
		private Color[] _palette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralDestination"/> class that sends the documented format.
		/// </summary>
		/// <param name="transport">The connection to the receiver. The destination disposes it.</param>
		/// <param name="startMarker">The bytes written before each message. Can be empty.</param>
		/// <param name="panel">The index of the panel that this destination addresses.</param>
		public DeviceNeutralDestination(IDeviceNeutralTransport transport, byte[] startMarker, byte panel)
			: this(transport, new DeviceNeutralMessageWriter(startMarker), panel, new byte[0])
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralDestination"/> class with the given protocol parameters.
		/// </summary>
		/// <param name="transport">The connection to the receiver. The destination disposes it.</param>
		/// <param name="writer">The encoder, set up with the protocol parameters.</param>
		/// <param name="panel">The index of the panel that this destination addresses.</param>
		/// <param name="connectBytes">The bytes written once each time a connection opens. Can be empty.</param>
		public DeviceNeutralDestination(IDeviceNeutralTransport transport, DeviceNeutralMessageWriter writer, byte panel, byte[] connectBytes)
			: this(transport, writer, panel, connectBytes, AllMessages)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralDestination"/> class that sends only the given messages.
		/// </summary>
		/// <param name="transport">The connection to the receiver. The destination disposes it.</param>
		/// <param name="writer">The encoder, set up with the protocol parameters.</param>
		/// <param name="panel">The index of the panel that this destination addresses.</param>
		/// <param name="connectBytes">The bytes written once each time a connection opens. Can be empty.</param>
		/// <param name="messages">The message types that are sent.</param>
		public DeviceNeutralDestination(IDeviceNeutralTransport transport, DeviceNeutralMessageWriter writer, byte panel, byte[] connectBytes, IReadOnlyCollection<DeviceNeutralMessageType> messages)
		{
			_transport = transport;
			_writer = writer;
			_panel = panel;
			_connectBytes = connectBytes;
			_messages = new HashSet<DeviceNeutralMessageType>(messages);
			_timer = new Timer(Tick, null, 0, TickMs);
		}

		public void RenderGray2(DmdFrame frame)
		{
			Send(DeviceNeutralMessageType.Gray2, frame);
		}

		public void RenderGray4(DmdFrame frame)
		{
			Send(DeviceNeutralMessageType.Gray4, frame);
		}

		public void RenderGray8(DmdFrame frame)
		{
			Send(DeviceNeutralMessageType.Gray8, frame);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			Send(DeviceNeutralMessageType.Rgb24, frame);
		}

		public void ClearDisplay()
		{
			lock (_lock) {
				if (!_disposed && _messages.Contains(DeviceNeutralMessageType.Clear) && _transport.IsConnected) {
					Write(_writer.Clear(_panel), DeviceNeutralMessageType.Clear);
				}
			}
		}

		public void SetColor(Color color)
		{
			lock (_lock) {
				_color = color;
			}
		}

		public void ClearColor()
		{
			lock (_lock) {
				_color = RenderGraph.DefaultColor;
			}
		}

		public void SetPalette(Color[] colors)
		{
			lock (_lock) {
				_palette = colors;
			}
		}

		public void ClearPalette()
		{
			lock (_lock) {
				_palette = null;
			}
		}

		public void Dispose()
		{
			lock (_lock) {
				_disposed = true;
				_timer.Dispose();
				_transport.Dispose();
			}
		}

		private void Send(DeviceNeutralMessageType type, DmdFrame frame)
		{
			lock (_lock) {
				if (_disposed) {
					return;
				}
				if (!_messages.Contains(type)) {
					frame = Convert(type, frame, out type);
					if (frame == null) {
						if (!_convertWarned) {
							Logger.Warn("[deviceneutral] No listed message type to convert frames to, skipping them.");
							_convertWarned = true;
						}
						return;
					}
				}
				if (!_hasSize || frame.Dimensions != _size) {
					_size = frame.Dimensions;
					_hasSize = true;
					_sizeSent = false;
				}
				if (!Connect()) {
					return;
				}
				if (!_sizeSent) {
					SendSize();
				}
				Write(_writer.Frame(type, _panel, frame), type);
			}
		}

		/// <summary>
		/// Converts a copy of a frame to a message type that is sent, preferring RGB for gray frames.
		/// </summary>
		/// <param name="type">The message type of the frame.</param>
		/// <param name="frame">The frame, which is left unchanged.</param>
		/// <param name="converted">The message type of the returned frame.</param>
		/// <returns>The converted frame, or <see langword="null"/> if no listed type can be converted to.</returns>
		private DmdFrame Convert(DeviceNeutralMessageType type, DmdFrame frame, out DeviceNeutralMessageType converted)
		{
			if (type != DeviceNeutralMessageType.Rgb24 && _messages.Contains(DeviceNeutralMessageType.Rgb24)) {
				converted = DeviceNeutralMessageType.Rgb24;
				var colors = _palette ?? new[] { Colors.Black, _color };
				return frame.CloneFrame().ConvertGrayToRgb24(ColorUtil.GetPalette(colors, 1 << frame.BitLength));
			}
			if (type != DeviceNeutralMessageType.Gray4 && _messages.Contains(DeviceNeutralMessageType.Gray4)) {
				converted = DeviceNeutralMessageType.Gray4;
				return frame.CloneFrame().ConvertToGray4();
			}
			if (type != DeviceNeutralMessageType.Gray2 && _messages.Contains(DeviceNeutralMessageType.Gray2)) {
				converted = DeviceNeutralMessageType.Gray2;
				return frame.CloneFrame().ConvertToGray2();
			}
			converted = DeviceNeutralMessageType.None;
			return null;
		}

		private void Tick(object state)
		{
			lock (_lock) {
				if (!_disposed && Connect() && _hasSize) {
					SendSize();
				}
			}
		}

		private bool Connect()
		{
			if (_transport.IsConnected) {
				return true;
			}
			if (!_transport.TryConnect()) {
				return false;
			}
			Logger.Info("[deviceneutral] Connected to {0}.", _transport.Description);
			_sizeSent = false;
			if (_connectBytes.Length > 0) {
				return _transport.Write(_connectBytes, 0, _connectBytes.Length);
			}
			return true;
		}

		private void SendSize()
		{
			_sizeSent = !_messages.Contains(DeviceNeutralMessageType.Size) || Write(_writer.Size(_panel, _size), DeviceNeutralMessageType.Size);
		}

		private bool Write(ArraySegment<byte> message, DeviceNeutralMessageType type)
		{
			if (message.Count == 0) {
				if (!_encodeWarned) {
					Logger.Warn("[deviceneutral] Could not encode a {0} message, skipping. Check that the frame fits the configured length format.", type);
					_encodeWarned = true;
				}
				return false;
			}
			return _transport.Write(message.Array, message.Offset, message.Count);
		}
	}
}
