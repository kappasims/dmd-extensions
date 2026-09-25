namespace LibDmd.Output.DeviceNeutral
{
	/// <summary>
	/// Specifies the type of a message.
	/// </summary>
	public enum DeviceNeutralMessageType
	{
		/// <summary>Indicates a message that sets the frame size of a panel.</summary>
		Size,

		/// <summary>Indicates a message that blanks a panel.</summary>
		Clear,

		/// <summary>Indicates a frame message with 2 bits per pixel, packed 4 pixels per byte.</summary>
		Gray2,

		/// <summary>Indicates a frame message with 4 bits per pixel, packed 2 pixels per byte.</summary>
		Gray4,

		/// <summary>Indicates a frame message with 8 bits per pixel.</summary>
		Gray8,

		/// <summary>Indicates a frame message with 24 bits per pixel, in red, green, blue order.</summary>
		Rgb24,
	}
}
