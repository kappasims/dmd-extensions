using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using LibDmd.Frame;
using NLog;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents a destination that sends frames to a receiver through an <see cref="IDeviceNeutralTransport"/>, in
	/// the format set by its protocol parameters.
	/// </summary>
	/// <remarks>
	/// Frames are sent at their own size. If <see cref="DeviceNeutralMessageType.Size"/> is among the messages that
	/// are sent, a size message precedes the first frame of each size and is repeated every second. Frames of a type
	/// that isn't among them are dropped.
	/// </remarks>
	public class DeviceNeutralDestination : IGray2Destination, IGray4Destination, IGray8Destination, IRgb24Destination
	{
		public string Name {
			get {
				return "DeviceNeutral";
			}
		}

		/// <summary>
		/// Gets a value indicating whether the destination can be used.
		/// </summary>
		/// <remarks>
		/// This property always returns <see langword="true"/>. The destination connects on demand and
		/// retries every second while no receiver is connected.
		/// </remarks>
		public bool IsAvailable {
			get {
				return true;
			}
		}

		public bool NeedsDuplicateFrames {
			get {
				return false;
			}
		}

		public bool NeedsIdentificationFrames {
			get {
				return false;
			}
		}

		private const int TickMs = 1000;

		private readonly IDeviceNeutralTransport _transport;
		private readonly DeviceNeutralMessageWriter _writer;
		private readonly byte _panel;
		private readonly byte[] _connectBytes;
		private readonly HashSet<DeviceNeutralMessageType> _messages;
		private readonly HashSet<DeviceNeutralMessageType> _dropWarned = new HashSet<DeviceNeutralMessageType>();
		private readonly object _lock = new object();
		private readonly Timer _timer;

		private Dimensions _size;
		private bool _hasSize;
		private bool _sizeSent;
		private bool _disposed;
		private bool _encodeWarned;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralDestination"/> class with the given protocol parameters.
		/// </summary>
		/// <param name="transport">The connection to the receiver. The destination disposes it.</param>
		/// <param name="writer">The encoder, set up with the protocol parameters.</param>
		/// <param name="panel">The index of the panel that this destination addresses.</param>
		/// <param name="connectBytes">The bytes written once each time a connection opens. Can be empty.</param>
		/// <param name="messages">The message types that are sent.</param>
		public DeviceNeutralDestination(IDeviceNeutralTransport transport, DeviceNeutralMessageWriter writer, byte panel, byte[] connectBytes, IEnumerable<DeviceNeutralMessageType> messages)
		{
			_transport = transport;
			_writer = writer;
			_panel = panel;
			_connectBytes = connectBytes;
			_messages = new HashSet<DeviceNeutralMessageType>(messages);
			_timer = new Timer(ResendSize, null, 0, TickMs);
		}

		public void RenderGray2(DmdFrame frame)
		{
			SendFrame(DeviceNeutralMessageType.Gray2, frame);
		}

		public void RenderGray4(DmdFrame frame)
		{
			SendFrame(DeviceNeutralMessageType.Gray4, frame);
		}

		public void RenderGray8(DmdFrame frame)
		{
			SendFrame(DeviceNeutralMessageType.Gray8, frame);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			SendFrame(DeviceNeutralMessageType.Rgb24, frame);
		}

		public void ClearDisplay()
		{
			lock (_lock) {
				if (!_disposed && _messages.Contains(DeviceNeutralMessageType.Clear) && _transport.IsConnected) {
					WriteMessage(_writer.WriteClearMessage(_panel), DeviceNeutralMessageType.Clear);
				}
			}
		}

		public void SetColor(Color color)
		{
		}

		public void ClearColor()
		{
		}

		public void SetPalette(Color[] colors)
		{
		}

		public void ClearPalette()
		{
		}

		public void Dispose()
		{
			lock (_lock) {
				_disposed = true;
				_timer.Dispose();
				_transport.Dispose();
			}
		}

		private void SendFrame(DeviceNeutralMessageType type, DmdFrame frame)
		{
			lock (_lock) {
				if (_disposed) {
					return;
				}
				if (!_messages.Contains(type)) {
					if (_dropWarned.Add(type)) {
						Logger.Warn("[deviceneutral] {0} frames are not among the configured messages, dropping them.", type);
					}
					return;
				}
				if (!_hasSize || frame.Dimensions != _size) {
					_size = frame.Dimensions;
					_hasSize = true;
					_sizeSent = false;
				}
				if (!ConnectReceiver()) {
					return;
				}
				if (!_sizeSent) {
					SendSize();
				}
				WriteMessage(_writer.WriteFrameMessage(type, _panel, frame), type);
			}
		}

		private void ResendSize(object state)
		{
			lock (_lock) {
				if (!_disposed && ConnectReceiver() && _hasSize) {
					SendSize();
				}
			}
		}

		private bool ConnectReceiver()
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
			_sizeSent = !_messages.Contains(DeviceNeutralMessageType.Size) || WriteMessage(_writer.WriteSizeMessage(_panel, _size), DeviceNeutralMessageType.Size);
		}

		private bool WriteMessage(ArraySegment<byte> message, DeviceNeutralMessageType type)
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
