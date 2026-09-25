using System;
using System.IO;
using System.IO.Pipes;
using NLog;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents a transport that connects to a receiver's named pipe as a client.
	/// </summary>
	public class DeviceNeutralNamedPipeTransport : IDeviceNeutralTransport
	{
		private const int WriteTimeoutMs = 200;

		private readonly string _pipeName;
		private NamedPipeClientStream _pipe;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceNeutralNamedPipeTransport"/> class.
		/// </summary>
		/// <param name="pipeName">The name of the pipe, without the <c>\\.\pipe\</c> prefix.</param>
		public DeviceNeutralNamedPipeTransport(string pipeName)
		{
			_pipeName = pipeName;
		}

		/// <inheritdoc/>
		public string Description {
			get {
				return $@"\\.\pipe\{_pipeName}";
			}
		}

		/// <inheritdoc/>
		public bool IsConnected {
			get {
				return _pipe != null && _pipe.IsConnected;
			}
		}

		/// <inheritdoc/>
		public bool TryConnect()
		{
			if (IsConnected) {
				return true;
			}
			ClosePipe();

			var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
			try {
				pipe.Connect(0);

			} catch (Exception e) when (e is TimeoutException || e is IOException || e is UnauthorizedAccessException) {
				pipe.Dispose();
				return false;
			}
			_pipe = pipe;
			return true;
		}

		/// <inheritdoc/>
		public bool Write(byte[] buffer, int offset, int count)
		{
			if (!IsConnected) {
				return false;
			}
			try {
				if (_pipe.WriteAsync(buffer, offset, count).Wait(WriteTimeoutMs)) {
					return true;
				}
				Logger.Info("[deviceneutral] Write to {0} timed out after {1} ms.", Description, WriteTimeoutMs);

			} catch (Exception e) when (e is AggregateException || e is IOException || e is ObjectDisposedException) {
				Logger.Info("[deviceneutral] Write to {0} failed: {1}", Description, e.GetBaseException().Message);
			}
			ClosePipe();
			return false;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			ClosePipe();
		}

		private void ClosePipe()
		{
			_pipe?.Dispose();
			_pipe = null;
		}
	}
}
