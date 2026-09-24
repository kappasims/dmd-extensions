namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Specifies the type of a message.
	/// </summary>
	/// <remarks>
	/// Bit 7 is set for every type that carries pixels.
	/// </remarks>
	public enum DeviceNeutralMessageType : byte
	{
		/// <summary>Indicates that no message type is specified.</summary>
		None = 0x00,

		/// <summary>Indicates a message that sets the frame size of a panel.</summary>
		Size = 0x01,

		/// <summary>Indicates a message that blanks a panel.</summary>
		Clear = 0x02,

		/// <summary>Indicates a frame message with 2 bits per pixel, packed 4 pixels per byte.</summary>
		Gray2 = 0x80,

		/// <summary>Indicates a frame message with 4 bits per pixel, packed 2 pixels per byte.</summary>
		Gray4 = 0x81,

		/// <summary>Indicates a frame message with 8 bits per pixel.</summary>
		Gray8 = 0x82,

		/// <summary>Indicates a frame message with 24 bits per pixel, in red, green, blue order.</summary>
		Rgb24 = 0x83,
	}
}
