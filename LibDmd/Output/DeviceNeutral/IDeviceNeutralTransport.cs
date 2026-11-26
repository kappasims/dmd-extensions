using System;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Defines a connection that carries encoded messages to a receiver.
	/// </summary>
	public interface IDeviceNeutralTransport : IDisposable
	{
		/// <summary>
		/// Gets a description of the endpoint.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// Gets a value indicating whether a connection is open.
		/// </summary>
		bool IsConnected { get; }

		/// <summary>
		/// Attempts to open a connection without blocking.
		/// </summary>
		/// <returns><see langword="true"/> if a connection is open; otherwise, <see langword="false"/>.</returns>
		bool TryConnect();

		/// <summary>
		/// Writes a sequence of bytes in a single write operation.
		/// </summary>
		/// <param name="buffer">The buffer that contains the data to write.</param>
		/// <param name="offset">The zero-based offset in <paramref name="buffer"/> at which to begin.</param>
		/// <param name="count">The number of bytes to write.</param>
		/// <returns>
		/// <see langword="true"/> if the bytes were written; otherwise, <see langword="false"/>, in which
		/// case the connection is closed.
		/// </returns>
		bool Write(byte[] buffer, int offset, int count);
	}
}
