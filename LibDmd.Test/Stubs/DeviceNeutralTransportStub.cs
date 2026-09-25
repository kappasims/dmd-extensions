using System;
using System.Collections.Generic;
using LibDmd.Output.DeviceNeutral;

namespace LibDmd.Test.Stubs
{
	public class DeviceNeutralTransportStub : IDeviceNeutralTransport
	{
		public string Description {
			get {
				return "stub";
			}
		}
		public bool IsConnected { get; private set; }

		public bool IsReceiverPresent { get; set; } = true;
		public bool FailNextWrite { get; set; }

		private readonly List<byte[]> _writes = new List<byte[]>();

		public byte[][] Writes {
			get {
				lock (_writes) {
					return _writes.ToArray();
				}
			}
		}

		public bool TryConnect()
		{
			IsConnected = IsReceiverPresent;
			return IsConnected;
		}

		public bool Write(byte[] buffer, int offset, int count)
		{
			if (!IsConnected) {
				return false;
			}
			if (FailNextWrite) {
				FailNextWrite = false;
				IsConnected = false;
				return false;
			}
			var write = new byte[count];
			Buffer.BlockCopy(buffer, offset, write, 0, count);
			lock (_writes) {
				_writes.Add(write);
			}
			return true;
		}

		public void Dispose()
		{
			IsConnected = false;
		}
	}
}
