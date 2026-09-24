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
		/// <summary>
		/// Represents the baud rate used when none is configured.
		/// </summary>
		public const int DefaultBaudRate = 921600;

		private const int WriteTimeoutMs = 200;

		private readonly string _portName;
		private readonly int _baudRate;
		private SerialPort _port;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralSerialTransport"/> class.
		/// </summary>
		/// <param name="portName">The name of the port, for example <c>COM3</c>.</param>
		/// <param name="baudRate">The baud rate. USB CDC devices ignore it.</param>
		public DeviceNeutralSerialTransport(string portName, int baudRate)
		{
			_portName = portName;
			_baudRate = baudRate;
		}

		/// <inheritdoc/>
		public string Description => _portName;

		/// <inheritdoc/>
		public bool IsConnected => _port != null && _port.IsOpen;

		/// <inheritdoc/>
		public bool TryConnect()
		{
			if (IsConnected) {
				return true;
			}
			Close();

			var port = new SerialPort(_portName, _baudRate) { WriteTimeout = WriteTimeoutMs, DtrEnable = true };
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
				Logger.Info("[deviceneutral] Write to {0} failed: {1}", _portName, e.Message);
				Close();
				return false;
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Close();
		}

		private void Close()
		{
			_port?.Dispose();
			_port = null;
		}
	}
}
