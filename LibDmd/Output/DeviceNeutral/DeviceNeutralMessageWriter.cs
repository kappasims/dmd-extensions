using System;
using LibDmd.Frame;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents an encoder that writes messages into a reusable buffer.
	/// </summary>
	/// <remarks>
	/// The writer precedes every message with the start marker and the message length as a 32-bit
	/// little-endian integer, on every transport.
	/// </remarks>
	public class DeviceNeutralMessageWriter
	{
		/// <summary>
		/// Gets the start marker of the documented format, the ASCII characters <c>DNDP</c>.
		/// </summary>
		public static byte[] DefaultStartMarker => new byte[] { 0x44, 0x4E, 0x44, 0x50 };

		private const int LengthBytes = 4;
		private const int TypeAndPanelBytes = 2;

		private readonly int _messageStart;
		private byte[] _buffer;
		private int _position;

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralMessageWriter"/> class.
		/// </summary>
		/// <param name="startMarker">The bytes written before each message. Can be empty.</param>
		public DeviceNeutralMessageWriter(byte[] startMarker)
		{
			_messageStart = startMarker.Length + LengthBytes;
			_buffer = new byte[_messageStart + TypeAndPanelBytes + 4];
			Buffer.BlockCopy(startMarker, 0, _buffer, 0, startMarker.Length);
		}

		/// <summary>
		/// Encodes a <see cref="DeviceNeutralMessageType.Size"/> message.
		/// </summary>
		/// <param name="panel">The index of the panel.</param>
		/// <param name="dim">The frame size.</param>
		/// <returns>The encoded message, valid until the next call.</returns>
		public ArraySegment<byte> Size(byte panel, Dimensions dim)
		{
			Begin(DeviceNeutralMessageType.Size, panel, 4);
			WriteUInt16(dim.Width);
			WriteUInt16(dim.Height);
			return End();
		}

		/// <summary>
		/// Encodes a <see cref="DeviceNeutralMessageType.Clear"/> message.
		/// </summary>
		/// <param name="panel">The index of the panel.</param>
		/// <returns>The encoded message, valid until the next call.</returns>
		public ArraySegment<byte> Clear(byte panel)
		{
			Begin(DeviceNeutralMessageType.Clear, panel, 0);
			return End();
		}

		/// <summary>
		/// Encodes a frame message, packing the pixels to the bit depth of <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The frame message type.</param>
		/// <param name="panel">The index of the panel.</param>
		/// <param name="frame">The frame, with one byte per pixel for the gray types.</param>
		/// <returns>
		/// The encoded message, valid until the next call, or an empty segment if <paramref name="frame"/>
		/// contains fewer bytes than its dimensions require.
		/// </returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a frame message type.</exception>
		public ArraySegment<byte> Frame(DeviceNeutralMessageType type, byte panel, DmdFrame frame)
		{
			var pixels = frame.Dimensions.Surface;
			var data = frame.Data;
			switch (type) {
				case DeviceNeutralMessageType.Gray2:
					if (data.Length < pixels) {
						return default;
					}
					Begin(type, panel, (pixels + 3) / 4);
					PackGray2(data, pixels);
					break;

				case DeviceNeutralMessageType.Gray4:
					if (data.Length < pixels) {
						return default;
					}
					Begin(type, panel, (pixels + 1) / 2);
					PackGray4(data, pixels);
					break;

				case DeviceNeutralMessageType.Gray8:
					if (data.Length < pixels) {
						return default;
					}
					Begin(type, panel, pixels);
					Copy(data, pixels);
					break;

				case DeviceNeutralMessageType.Rgb24:
					if (data.Length < pixels * 3) {
						return default;
					}
					Begin(type, panel, pixels * 3);
					Copy(data, pixels * 3);
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, "Not a frame message type.");
			}
			return End();
		}

		private void Begin(DeviceNeutralMessageType type, byte panel, int contentBytes)
		{
			var total = _messageStart + TypeAndPanelBytes + contentBytes;
			if (_buffer.Length < total) {
				Array.Resize(ref _buffer, total);
			}
			_position = _messageStart;
			_buffer[_position++] = (byte)type;
			_buffer[_position++] = panel;
		}

		private ArraySegment<byte> End()
		{
			var messageBytes = _position - _messageStart;
			var at = _messageStart - LengthBytes;
			_buffer[at] = (byte)messageBytes;
			_buffer[at + 1] = (byte)(messageBytes >> 8);
			_buffer[at + 2] = (byte)(messageBytes >> 16);
			_buffer[at + 3] = (byte)(messageBytes >> 24);
			return new ArraySegment<byte>(_buffer, 0, _position);
		}

		private void WriteUInt16(int value)
		{
			_buffer[_position++] = (byte)value;
			_buffer[_position++] = (byte)(value >> 8);
		}

		private void Copy(byte[] data, int count)
		{
			Buffer.BlockCopy(data, 0, _buffer, _position, count);
			_position += count;
		}

		private void PackGray2(byte[] data, int pixels)
		{
			for (var i = 0; i < pixels; i += 4) {
				var packed = (data[i] & 0x03) << 6;
				if (i + 1 < pixels) {
					packed |= (data[i + 1] & 0x03) << 4;
				}
				if (i + 2 < pixels) {
					packed |= (data[i + 2] & 0x03) << 2;
				}
				if (i + 3 < pixels) {
					packed |= data[i + 3] & 0x03;
				}
				_buffer[_position++] = (byte)packed;
			}
		}

		private void PackGray4(byte[] data, int pixels)
		{
			for (var i = 0; i < pixels; i += 2) {
				var packed = (data[i] & 0x0F) << 4;
				if (i + 1 < pixels) {
					packed |= data[i + 1] & 0x0F;
				}
				_buffer[_position++] = (byte)packed;
			}
		}
	}
}
