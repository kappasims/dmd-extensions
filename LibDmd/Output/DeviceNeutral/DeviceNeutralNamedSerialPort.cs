namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents a serial port given by its name.
	/// </summary>
	public class DeviceNeutralNamedSerialPort : DeviceNeutralSerialPort
	{
		private readonly string _portName;

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralNamedSerialPort"/> class.
		/// </summary>
		/// <param name="portName">The name of the port, for example <c>COM3</c>.</param>
		public DeviceNeutralNamedSerialPort(string portName)
		{
			_portName = portName;
		}

		/// <inheritdoc/>
		public override string Description {
			get {
				return _portName;
			}
		}

		/// <inheritdoc/>
		public override bool TryFindPortName(out string portName)
		{
			portName = _portName;
			return true;
		}
	}
}
