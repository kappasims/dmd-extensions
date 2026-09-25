using System.Collections.Generic;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Defines where serial ports and the USB devices they belong to are looked up.
	/// </summary>
	public interface ISerialPortSource
	{
		/// <summary>
		/// Returns the names of the serial ports that exist now.
		/// </summary>
		/// <returns>The port names, for example <c>COM3</c>.</returns>
		IReadOnlyCollection<string> GetPortNames();

		/// <summary>
		/// Returns the serial ports Windows has assigned to USB device interfaces, including those of
		/// devices that aren't plugged in.
		/// </summary>
		/// <returns>Each assignment, or an empty list if there are none.</returns>
		IReadOnlyList<UsbSerialPortAssignment> GetUsbPortAssignments();
	}
}
