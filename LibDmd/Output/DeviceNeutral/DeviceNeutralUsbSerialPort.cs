using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LibDmd.Common;
using NLog;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents the serial port of a display on USB, found by its USB vendor and product ID.
	/// </summary>
	/// <remarks>
	/// The port is looked up each time it's needed, so a display that Windows gave another port, or
	/// that was plugged in again, is found on its new port.
	/// </remarks>
	public class DeviceNeutralUsbSerialPort : DeviceNeutralSerialPort
	{
		private readonly ushort _vendorId;
		private readonly ushort _productId;
		private readonly string _usbId;
		private readonly Func<List<UsbSerialPort>> _listUsbPorts;
		private string _portName;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralUsbSerialPort"/> class.
		/// </summary>
		/// <param name="vendorId">The USB vendor ID of the device.</param>
		/// <param name="productId">The USB product ID of the device.</param>
		/// <param name="listUsbPorts">Lists the serial ports of the USB devices that are plugged in.</param>
		public DeviceNeutralUsbSerialPort(ushort vendorId, ushort productId, Func<List<UsbSerialPort>> listUsbPorts)
		{
			_vendorId = vendorId;
			_productId = productId;
			_usbId = "usb:" + vendorId.ToString("X4", CultureInfo.InvariantCulture) + ":" + productId.ToString("X4", CultureInfo.InvariantCulture);
			_listUsbPorts = listUsbPorts;
		}

		/// <inheritdoc/>
		/// <remarks>
		/// The description is the USB ID, followed by the port it was last found on while the device is
		/// plugged in.
		/// </remarks>
		public override string Description {
			get {
				return _portName == null ? _usbId : _usbId + " (" + _portName + ")";
			}
		}

		/// <inheritdoc/>
		/// <remarks>
		/// If several ports match, the lowest-numbered one is used.
		/// </remarks>
		public override bool TryFindPortName(out string portName)
		{
			var portNames = _listUsbPorts()
				.Where(port => port.VendorId == _vendorId && port.ProductId == _productId)
				.Select(port => port.Name)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(name => name.Length)
				.ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (portNames.Count == 0) {
				_portName = null;
				portName = null;
				return false;
			}
			if (!string.Equals(portNames[0], _portName, StringComparison.OrdinalIgnoreCase)) {
				if (portNames.Count > 1) {
					Logger.Info("[deviceneutral] Found {0} on {1}, using {2}.", _usbId, string.Join(", ", portNames), portNames[0]);
				} else {
					Logger.Info("[deviceneutral] Found {0} on {1}.", _usbId, portNames[0]);
				}
			}
			_portName = portNames[0];
			portName = _portName;
			return true;
		}
	}
}
