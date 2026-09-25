using System.Collections.Generic;
using LibDmd.Output.DeviceNeutral;

namespace LibDmd.Test.Stubs
{
	public class SerialPortSourceStub : ISerialPortSource
	{
		public List<string> PortNames { get; } = new List<string>();
		public List<UsbSerialPortAssignment> UsbPortAssignments { get; } = new List<UsbSerialPortAssignment>();

		public IReadOnlyCollection<string> GetPortNames()
		{
			return PortNames;
		}

		public IReadOnlyList<UsbSerialPortAssignment> GetUsbPortAssignments()
		{
			return UsbPortAssignments;
		}
	}
}
