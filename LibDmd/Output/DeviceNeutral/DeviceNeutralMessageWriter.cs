using System;
using System.Collections.Generic;
using LibDmd.Common;
using LibDmd.Frame;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents an encoder that writes messages into a reusable buffer.
	/// </summary>
	/// <remarks>
	/// The writer writes the fields of every message in the order of its layout. The default layout is
	/// the start marker, the length as a 32-bit little-endian integer, the type, the panel and the
	/// content.
	/// </remarks>
	public class DeviceNeutralMessageWriter
	{
		/// <summary>
		/// Gets the start marker of the documented format, the ASCII characters <c>DNDP</c>.
		/// </summary>
		public static byte[] DefaultStartMarker => new byte[] { 0x44, 0x4E, 0x44, 0x50 };

		/// <summary>
		/// Gets the layout of the documented format.
		/// </summary>
		public static DeviceNeutralMessageField[] DefaultLayout => new[] {
			DeviceNeutralMessageField.StartMarker, DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Content
		};

		private readonly DeviceNeutralMessageField[] _layout;
		private readonly byte[] _startMarker;
		private readonly byte[] _endMarker;
		private readonly DeviceNeutralLengthFormat _lengthFormat;
		private readonly IReadOnlyDictionary<DeviceNeutralMessageType, byte> _typeBytes;
		private readonly bool _swapGreenBlue;
		private readonly int _contentField;
		private readonly int _fieldBytes;

		private byte[] _buffer;
		private int _position;
		private int _lengthPosition;
		private int _endMarkerBytesAfterLength;
		private DeviceNeutralMessageType _type;
		private byte _panel;

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralMessageWriter"/> class that writes the documented
		/// format.
		/// </summary>
		/// <param name="startMarker">The bytes written before each message. Can be empty.</param>
		public DeviceNeutralMessageWriter(byte[] startMarker)
			: this(DefaultLayout, startMarker, new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian, new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rgb)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralMessageWriter"/> class with the given protocol
		/// parameters.
		/// </summary>
		/// <param name="layout">The fields of each message, in order. Must contain <see cref="DeviceNeutralMessageField.Content"/>.</param>
		/// <param name="startMarker">The bytes of the <see cref="DeviceNeutralMessageField.StartMarker"/> field. Can be empty.</param>
		/// <param name="endMarker">The bytes of the <see cref="DeviceNeutralMessageField.EndMarker"/> field. Can be empty.</param>
		/// <param name="lengthFormat">How the <see cref="DeviceNeutralMessageField.Length"/> field is written.</param>
		/// <param name="typeBytes">The type bytes that differ from the <see cref="DeviceNeutralMessageType"/> values.</param>
		/// <param name="colorOrder">The channel order of <see cref="DeviceNeutralMessageType.Rgb24"/> frames.</param>
		/// <exception cref="ArgumentException"><paramref name="layout"/> doesn't contain <see cref="DeviceNeutralMessageField.Content"/>.</exception>
		public DeviceNeutralMessageWriter(DeviceNeutralMessageField[] layout, byte[] startMarker, byte[] endMarker, DeviceNeutralLengthFormat lengthFormat,
			IReadOnlyDictionary<DeviceNeutralMessageType, byte> typeBytes, ColorMatrix colorOrder)
		{
			_contentField = Array.IndexOf(layout, DeviceNeutralMessageField.Content);
			if (_contentField < 0) {
				throw new ArgumentException("The layout must contain the content.", nameof(layout));
			}
			_layout = layout;
			_startMarker = startMarker;
			_endMarker = endMarker;
			_lengthFormat = lengthFormat;
			_typeBytes = typeBytes;
			_swapGreenBlue = colorOrder == ColorMatrix.Rbg;

			foreach (var field in layout) {
				_fieldBytes += FieldBytes(field);
			}
			_buffer = new byte[_fieldBytes + 4];
		}

		/// <summary>
		/// Encodes a <see cref="DeviceNeutralMessageType.Size"/> message.
		/// </summary>
		/// <param name="panel">The index of the panel.</param>
		/// <param name="dim">The frame size.</param>
		/// <returns>
		/// The encoded message, valid until the next call, or an empty segment if the message is too long
		/// for the length format.
		/// </returns>
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
		/// contains fewer bytes than its dimensions require or the message is too long for the length
		/// format.
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
					if (_swapGreenBlue) {
						CopySwappingGreenBlue(data, pixels);
					} else {
						Copy(data, pixels * 3);
					}
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, "Not a frame message type.");
			}
			return End();
		}

		private void Begin(DeviceNeutralMessageType type, byte panel, int contentBytes)
		{
			var total = _fieldBytes + contentBytes;
			if (_buffer.Length < total) {
				Array.Resize(ref _buffer, total);
			}
			_type = type;
			_panel = panel;
			_position = 0;
			_lengthPosition = -1;
			_endMarkerBytesAfterLength = 0;
			for (var i = 0; i < _contentField; i++) {
				WriteField(_layout[i]);
			}
		}

		private ArraySegment<byte> End()
		{
			for (var i = _contentField + 1; i < _layout.Length; i++) {
				WriteField(_layout[i]);
			}
			if (_lengthPosition >= 0) {
				var counted = _position - _lengthPosition - LengthBytes - _endMarkerBytesAfterLength;
				if (!WriteLength(counted)) {
					return default;
				}
			}
			return new ArraySegment<byte>(_buffer, 0, _position);
		}

		private void WriteField(DeviceNeutralMessageField field)
		{
			switch (field) {
				case DeviceNeutralMessageField.StartMarker:
					Copy(_startMarker, _startMarker.Length);
					break;

				case DeviceNeutralMessageField.Length:
					_lengthPosition = _position;
					_position += LengthBytes;
					break;

				case DeviceNeutralMessageField.Type:
					_buffer[_position++] = _typeBytes.TryGetValue(_type, out var typeByte) ? typeByte : (byte)_type;
					break;

				case DeviceNeutralMessageField.Panel:
					_buffer[_position++] = _panel;
					break;

				case DeviceNeutralMessageField.EndMarker:
					Copy(_endMarker, _endMarker.Length);
					if (_lengthPosition >= 0) {
						_endMarkerBytesAfterLength += _endMarker.Length;
					}
					break;
			}
		}

		private bool WriteLength(int value)
		{
			var at = _lengthPosition;
			switch (_lengthFormat) {
				case DeviceNeutralLengthFormat.UInt32LittleEndian:
					_buffer[at] = (byte)value;
					_buffer[at + 1] = (byte)(value >> 8);
					_buffer[at + 2] = (byte)(value >> 16);
					_buffer[at + 3] = (byte)(value >> 24);
					return true;

				case DeviceNeutralLengthFormat.UInt16LittleEndian:
					if (value > ushort.MaxValue) {
						return false;
					}
					_buffer[at] = (byte)value;
					_buffer[at + 1] = (byte)(value >> 8);
					return true;

				case DeviceNeutralLengthFormat.UInt16BigEndian:
					if (value > ushort.MaxValue) {
						return false;
					}
					_buffer[at] = (byte)(value >> 8);
					_buffer[at + 1] = (byte)value;
					return true;

				default:
					return true;
			}
		}

		private int LengthBytes => _lengthFormat == DeviceNeutralLengthFormat.UInt32LittleEndian ? 4 : _lengthFormat == DeviceNeutralLengthFormat.None ? 0 : 2;

		private int FieldBytes(DeviceNeutralMessageField field)
		{
			switch (field) {
				case DeviceNeutralMessageField.StartMarker: return _startMarker.Length;
				case DeviceNeutralMessageField.Length: return LengthBytes;
				case DeviceNeutralMessageField.EndMarker: return _endMarker.Length;
				case DeviceNeutralMessageField.Content: return 0;
				default: return 1;
			}
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

		private void CopySwappingGreenBlue(byte[] data, int pixels)
		{
			for (var i = 0; i < pixels * 3; i += 3) {
				_buffer[_position++] = data[i];
				_buffer[_position++] = data[i + 2];
				_buffer[_position++] = data[i + 1];
			}
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
