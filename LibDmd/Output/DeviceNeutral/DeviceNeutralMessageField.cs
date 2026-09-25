namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Specifies a field of a message on the wire.
	/// </summary>
	public enum DeviceNeutralMessageField
	{
		/// <summary>The configured bytes that precede a message.</summary>
		StartMarker,

		/// <summary>The number of bytes that follow the length, up to the end marker.</summary>
		Length,

		/// <summary>The byte that identifies the message type.</summary>
		Type,

		/// <summary>The index of the panel that the message applies to.</summary>
		Panel,

		/// <summary>The content of the message, such as the pixels of a frame.</summary>
		Content,

		/// <summary>The configured bytes that follow a message.</summary>
		EndMarker,
	}
}
