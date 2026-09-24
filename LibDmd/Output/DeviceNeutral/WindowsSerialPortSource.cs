using System.Collections.Generic;
using System.IO.Ports;
using System.Security;
using Microsoft.Win32;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Looks up serial ports and the USB devices they belong to in the Windows registry.
	/// </summary>
	public class WindowsSerialPortSource : ISerialPortSource
	{
		private const string UsbDevicesKey = @"SYSTEM\CurrentControlSet\Enum\USB";
		private const string DeviceParametersKey = "Device Parameters";
		private const string PortNameValue = "PortName";

		/// <inheritdoc/>
		public IReadOnlyCollection<string> GetPortNames()
		{
			return SerialPort.GetPortNames();
		}

		/// <inheritdoc/>
		public IReadOnlyList<UsbSerialPortAssignment> GetUsbPortAssignments()
		{
			var assignments = new List<UsbSerialPortAssignment>();
			using (var devices = Registry.LocalMachine.OpenSubKey(UsbDevicesKey)) {
				if (devices == null) {
					return assignments;
				}
				foreach (var hardwareId in devices.GetSubKeyNames()) {
					try {
						using (var device = devices.OpenSubKey(hardwareId)) {
							if (device == null) {
								continue;
							}
							foreach (var instance in device.GetSubKeyNames()) {
								using (var parameters = device.OpenSubKey(instance + @"\" + DeviceParametersKey)) {
									if (parameters?.GetValue(PortNameValue) is string portName) {
										assignments.Add(new UsbSerialPortAssignment(hardwareId, portName));
									}
								}
							}
						}

					} catch (SecurityException) {
						// Leaves out a device whose key the user can't read.
					}
				}
			}
			return assignments;
		}
	}
}
