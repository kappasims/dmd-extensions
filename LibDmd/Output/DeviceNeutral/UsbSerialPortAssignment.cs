namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents a serial port Windows assigned to a USB device interface.
	/// </summary>
	public class UsbSerialPortAssignment
	{
		/// <summary>
		/// Gets the hardware ID of the device interface, for example <c>VID_2E8A&amp;PID_000A&amp;MI_00</c>.
		/// </summary>
		public string HardwareId { get; }

		/// <summary>
		/// Gets the name of the port, for example <c>COM3</c>.
		/// </summary>
		public string PortName { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="UsbSerialPortAssignment"/> class.
		/// </summary>
		/// <param name="hardwareId">The hardware ID of the device interface.</param>
		/// <param name="portName">The name of the port.</param>
		public UsbSerialPortAssignment(string hardwareId, string portName)
		{
			HardwareId = hardwareId;
			PortName = portName;
		}
	}
}
