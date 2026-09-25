using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
		private readonly string _usbId;
		private readonly string _hardwareId;
		private readonly ISerialPortSource _ports;
		private string _portName;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralUsbSerialPort"/> class.
		/// </summary>
		/// <param name="vendorId">The USB vendor ID of the device.</param>
		/// <param name="productId">The USB product ID of the device.</param>
		/// <param name="ports">Where the ports and their devices are looked up.</param>
		public DeviceNeutralUsbSerialPort(ushort vendorId, ushort productId, ISerialPortSource ports)
		{
			var vendor = vendorId.ToString("X4", CultureInfo.InvariantCulture);
			var product = productId.ToString("X4", CultureInfo.InvariantCulture);
			_usbId = "usb:" + vendor + ":" + product;
			_hardwareId = "VID_" + vendor + "&PID_" + product;
			_ports = ports;
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
		/// A composite USB device has a hardware ID per interface, such as <c>VID_2E8A&amp;PID_000A&amp;MI_00</c>,
		/// so the ports of all its interfaces match. Only ports that exist now are found. If several
		/// match, the lowest-numbered one is used.
		/// </remarks>
		public override bool TryFindPortName(out string portName)
		{
			var existingPorts = new HashSet<string>(_ports.GetPortNames(), StringComparer.OrdinalIgnoreCase);
			var interfacePrefix = _hardwareId + "&";
			var portNames = _ports.GetUsbPortAssignments()
				.Where(a => string.Equals(a.HardwareId, _hardwareId, StringComparison.OrdinalIgnoreCase)
				            || a.HardwareId.StartsWith(interfacePrefix, StringComparison.OrdinalIgnoreCase))
				.Select(a => a.PortName)
				.Where(existingPorts.Contains)
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
