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
	/// The writer writes the fields of every message in the order of its layout.
	/// </remarks>
	public class DeviceNeutralMessageWriter
	{
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
		/// Initializes a new instance of the <see cref="DeviceNeutralMessageWriter"/> class with the given protocol
		/// parameters.
		/// </summary>
		/// <param name="layout">The fields of each message, in order. Must contain <see cref="DeviceNeutralMessageField.Content"/>.</param>
		/// <param name="startMarker">The bytes of the <see cref="DeviceNeutralMessageField.StartMarker"/> field.</param>
		/// <param name="endMarker">The bytes of the <see cref="DeviceNeutralMessageField.EndMarker"/> field.</param>
		/// <param name="lengthFormat">How the <see cref="DeviceNeutralMessageField.Length"/> field is written.</param>
		/// <param name="typeBytes">The byte of the <see cref="DeviceNeutralMessageField.Type"/> field for each message type that is written.</param>
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
				switch (field) {
					case DeviceNeutralMessageField.StartMarker:
						_fieldBytes += _startMarker.Length;
						break;

					case DeviceNeutralMessageField.Length:
						_fieldBytes += LengthBytes;
						break;

					case DeviceNeutralMessageField.EndMarker:
						_fieldBytes += _endMarker.Length;
						break;

					case DeviceNeutralMessageField.Content:
						break;

					default:
						_fieldBytes += 1;
						break;
				}
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
		/// <exception cref="KeyNotFoundException">The layout contains the type and no type byte is set for <see cref="DeviceNeutralMessageType.Size"/>.</exception>
		public ArraySegment<byte> WriteSizeMessage(byte panel, Dimensions dim)
		{
			BeginMessage(DeviceNeutralMessageType.Size, panel, 4);
			WriteUInt16(dim.Width);
			WriteUInt16(dim.Height);
			return EndMessage();
		}

		/// <summary>
		/// Encodes a <see cref="DeviceNeutralMessageType.Clear"/> message.
		/// </summary>
		/// <param name="panel">The index of the panel.</param>
		/// <returns>The encoded message, valid until the next call.</returns>
		/// <exception cref="KeyNotFoundException">The layout contains the type and no type byte is set for <see cref="DeviceNeutralMessageType.Clear"/>.</exception>
		public ArraySegment<byte> WriteClearMessage(byte panel)
		{
			BeginMessage(DeviceNeutralMessageType.Clear, panel, 0);
			return EndMessage();
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
		/// <exception cref="KeyNotFoundException">The layout contains the type and no type byte is set for <paramref name="type"/>.</exception>
		public ArraySegment<byte> WriteFrameMessage(DeviceNeutralMessageType type, byte panel, DmdFrame frame)
		{
			var pixels = frame.Dimensions.Surface;
			var data = frame.Data;
			switch (type) {
				case DeviceNeutralMessageType.Gray2:
					if (data.Length < pixels) {
						return default;
					}
					BeginMessage(type, panel, (pixels + 3) / 4);
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
					break;

				case DeviceNeutralMessageType.Gray4:
					if (data.Length < pixels) {
						return default;
					}
					BeginMessage(type, panel, (pixels + 1) / 2);
					for (var i = 0; i < pixels; i += 2) {
						var packed = (data[i] & 0x0F) << 4;
						if (i + 1 < pixels) {
							packed |= data[i + 1] & 0x0F;
						}
						_buffer[_position++] = (byte)packed;
					}
					break;

				case DeviceNeutralMessageType.Gray8:
					if (data.Length < pixels) {
						return default;
					}
					BeginMessage(type, panel, pixels);
					CopyBytes(data, pixels);
					break;

				case DeviceNeutralMessageType.Rgb24:
					if (data.Length < pixels * 3) {
						return default;
					}
					BeginMessage(type, panel, pixels * 3);
					if (!_swapGreenBlue) {
						CopyBytes(data, pixels * 3);
						break;
					}
					for (var i = 0; i < pixels * 3; i += 3) {
						_buffer[_position++] = data[i];
						_buffer[_position++] = data[i + 2];
						_buffer[_position++] = data[i + 1];
					}
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(type), type, "Not a frame message type.");
			}
			return EndMessage();
		}

		private int LengthBytes {
			get {
				return _lengthFormat == DeviceNeutralLengthFormat.UInt32LittleEndian ? 4 : 2;
			}
		}

		private void BeginMessage(DeviceNeutralMessageType type, byte panel, int contentBytes)
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

		private ArraySegment<byte> EndMessage()
		{
			for (var i = _contentField + 1; i < _layout.Length; i++) {
				WriteField(_layout[i]);
			}
			if (_lengthPosition < 0) {
				return new ArraySegment<byte>(_buffer, 0, _position);
			}
			var length = _position - _lengthPosition - LengthBytes - _endMarkerBytesAfterLength;
			var at = _lengthPosition;
			switch (_lengthFormat) {
				case DeviceNeutralLengthFormat.UInt32LittleEndian:
					_buffer[at] = (byte)length;
					_buffer[at + 1] = (byte)(length >> 8);
					_buffer[at + 2] = (byte)(length >> 16);
					_buffer[at + 3] = (byte)(length >> 24);
					break;

				case DeviceNeutralLengthFormat.UInt16LittleEndian:
					if (length > ushort.MaxValue) {
						return default;
					}
					_buffer[at] = (byte)length;
					_buffer[at + 1] = (byte)(length >> 8);
					break;

				case DeviceNeutralLengthFormat.UInt16BigEndian:
					if (length > ushort.MaxValue) {
						return default;
					}
					_buffer[at] = (byte)(length >> 8);
					_buffer[at + 1] = (byte)length;
					break;

				default:
					throw new InvalidOperationException("Unknown length format " + _lengthFormat + ".");
			}
			return new ArraySegment<byte>(_buffer, 0, _position);
		}

		private void WriteField(DeviceNeutralMessageField field)
		{
			switch (field) {
				case DeviceNeutralMessageField.StartMarker:
					CopyBytes(_startMarker, _startMarker.Length);
					break;

				case DeviceNeutralMessageField.Length:
					_lengthPosition = _position;
					_position += LengthBytes;
					break;

				case DeviceNeutralMessageField.Type:
					_buffer[_position++] = _typeBytes[_type];
					break;

				case DeviceNeutralMessageField.Panel:
					_buffer[_position++] = _panel;
					break;

				case DeviceNeutralMessageField.EndMarker:
					CopyBytes(_endMarker, _endMarker.Length);
					if (_lengthPosition >= 0) {
						_endMarkerBytesAfterLength += _endMarker.Length;
					}
					break;
			}
		}

		private void WriteUInt16(int value)
		{
			_buffer[_position++] = (byte)value;
			_buffer[_position++] = (byte)(value >> 8);
		}

		private void CopyBytes(byte[] data, int count)
		{
			Buffer.BlockCopy(data, 0, _buffer, _position, count);
			_position += count;
		}
	}
}
