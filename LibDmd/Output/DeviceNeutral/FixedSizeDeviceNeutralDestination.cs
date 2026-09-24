using LibDmd.Frame;

namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Represents a <see cref="DeviceNeutralDestination"/> destination whose frames the render graph scales to one size.
	/// </summary>
	public class FixedSizeDeviceNeutralDestination : DeviceNeutralDestination, IFixedSizeDestination
	{
		/// <inheritdoc/>
		public Dimensions FixedSize { get; }

		/// <inheritdoc/>
		public bool DmdAllowHdScaling => true;

		/// <summary>
		/// Initializes a new instance of the <see cref="FixedSizeDeviceNeutralDestination"/> class.
		/// </summary>
		/// <param name="transport">The connection to the receiver. The destination disposes it.</param>
		/// <param name="writer">The encoder, set up with the protocol parameters.</param>
		/// <param name="panel">The index of the panel that this destination addresses.</param>
		/// <param name="connectBytes">The bytes written once each time a connection opens. Can be empty.</param>
		/// <param name="fixedSize">The size that every frame is scaled to.</param>
		public FixedSizeDeviceNeutralDestination(IDeviceNeutralTransport transport, DeviceNeutralMessageWriter writer, byte panel, byte[] connectBytes, Dimensions fixedSize)
			: base(transport, writer, panel, connectBytes)
		{
			FixedSize = fixedSize;
		}
	}
}
