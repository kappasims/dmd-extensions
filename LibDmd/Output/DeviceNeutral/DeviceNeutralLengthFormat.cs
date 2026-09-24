namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Specifies how the length of a message is written.
	/// </summary>
	public enum DeviceNeutralLengthFormat
	{
		/// <summary>Indicates a 32-bit little-endian integer.</summary>
		UInt32LittleEndian,

		/// <summary>Indicates a 16-bit little-endian integer.</summary>
		UInt16LittleEndian,

		/// <summary>Indicates a 16-bit big-endian integer.</summary>
		UInt16BigEndian,

		/// <summary>Indicates that no length is written.</summary>
		None,
	}
}
