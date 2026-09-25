using System;
using System.IO;
using System.IO.Ports;
using NLog;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents a transport over a serial port.
	/// </summary>
	/// <remarks>
	/// The transport enables DTR when it opens the port. USB CDC devices use DTR to detect that a host
	/// has the port open.
	/// </remarks>
	public class DeviceNeutralSerialTransport : IDeviceNeutralTransport
	{
		private const int WriteTimeoutMs = 200;

		private readonly DeviceNeutralSerialPort _displayPort;
		private readonly int _baudRate;
		private SerialPort _port;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralSerialTransport"/> class.
		/// </summary>
		/// <param name="displayPort">The serial port of the display.</param>
		/// <param name="baudRate">The baud rate. USB CDC devices ignore it.</param>
		public DeviceNeutralSerialTransport(DeviceNeutralSerialPort displayPort, int baudRate)
		{
			_displayPort = displayPort;
			_baudRate = baudRate;
		}

		/// <inheritdoc/>
		public string Description {
			get {
				return _displayPort.Description;
			}
		}

		/// <inheritdoc/>
		public bool IsConnected {
			get {
				return _port != null && _port.IsOpen;
			}
		}

		/// <inheritdoc/>
		public bool TryConnect()
		{
			if (IsConnected) {
				return true;
			}
			ClosePort();
			if (!_displayPort.TryFindPortName(out var portName)) {
				return false;
			}

			var port = new SerialPort(portName, _baudRate) { WriteTimeout = WriteTimeoutMs, DtrEnable = true };
			try {
				port.Open();

			} catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is InvalidOperationException) {
				port.Dispose();
				return false;
			}
			_port = port;
			return true;
		}

		/// <inheritdoc/>
		public bool Write(byte[] buffer, int offset, int count)
		{
			if (!IsConnected) {
				return false;
			}
			try {
				_port.Write(buffer, offset, count);
				return true;

			} catch (Exception e) when (e is TimeoutException || e is IOException || e is InvalidOperationException) {
				Logger.Info("[deviceneutral] Write to {0} failed: {1}", _displayPort.Description, e.Message);
				ClosePort();
				return false;
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			ClosePort();
		}

		private void ClosePort()
		{
			_port?.Dispose();
			_port = null;
		}
	}
}
