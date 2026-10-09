using System.Collections.Generic;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using LibDmd.Output.DeviceNeutral;

namespace DmdExt.Test
{
	public class TestDeviceNeutralConfig : IDeviceNeutralConfig
	{
		public string Name { get; set; } = "deviceneutral";
		public bool Enabled { get; set; }
		public IReadOnlyList<string> Problems { get; set; } = new string[0];
		public DeviceNeutralSerialPort Port { get; set; }
		public string Pipe { get; set; }
		public int BaudRate { get; set; }
		public DeviceNeutralMessageField[] Layout { get; set; }
		public byte[] StartMarker { get; set; }
		public DeviceNeutralLengthFormat Length { get; set; }
		public byte Panel { get; set; }
		public byte[] EndMarker { get; set; }
		public IReadOnlyCollection<DeviceNeutralMessageType> Messages { get; set; }
		public IReadOnlyDictionary<DeviceNeutralMessageType, byte> TypeBytes { get; set; }
		public ColorMatrix ColorOrder { get; set; }
		public Dimensions FixedSize { get; set; }
		public byte[] Connect { get; set; }

		public IReadOnlyList<string> Validate()
		{
			return Problems;
		}
	}
}
