using System;
using System.Globalization;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Identifies the serial port of a display, either by the port's name or by the display's USB ID.
	/// </summary>
	public abstract class DeviceNeutralSerialPort
	{
		private const string UsbPrefix = "usb:";
		private const int UsbIdDigits = 4;

		/// <summary>
		/// Gets a description of the port.
		/// </summary>
		public abstract string Description { get; }

		/// <summary>
		/// Finds the name of the port to open.
		/// </summary>
		/// <param name="portName">The name of the port, for example <c>COM3</c>.</param>
		/// <returns><see langword="true"/> if a port was found; otherwise, <see langword="false"/>.</returns>
		public abstract bool TryFindPortName(out string portName);

		/// <summary>
		/// Parses a port name, such as <c>COM3</c>, or <c>usb:</c> followed by a USB vendor and product
		/// ID of four hex digits each, such as <c>usb:2E8A:000A</c>.
		/// </summary>
		/// <param name="value">The value to parse.</param>
		/// <param name="port">The port the value identifies.</param>
		/// <returns><see langword="true"/> if the value identifies a port; otherwise, <see langword="false"/>.</returns>
		public static bool TryParse(string value, out DeviceNeutralSerialPort port)
		{
			port = null;
			if (string.IsNullOrWhiteSpace(value)) {
				return false;
			}
			var trimmed = value.Trim();
			if (!trimmed.StartsWith(UsbPrefix, StringComparison.OrdinalIgnoreCase)) {
				port = new DeviceNeutralNamedSerialPort(trimmed);
				return true;
			}
			var ids = trimmed.Substring(UsbPrefix.Length).Split(':');
			if (ids.Length != 2 || !TryParseUsbId(ids[0], out var vendorId) || !TryParseUsbId(ids[1], out var productId)) {
				return false;
			}
			port = new DeviceNeutralUsbSerialPort(vendorId, productId, new WindowsSerialPortSource());
			return true;
		}

		private static bool TryParseUsbId(string value, out ushort id)
		{
			id = 0;
			return value.Length == UsbIdDigits && ushort.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out id);
		}
	}
}
