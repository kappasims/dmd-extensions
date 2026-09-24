namespace LibDmd.Output
{
	/// <summary>
	/// Defines a destination that decides at runtime which of the frame formats it implements it takes.
	/// </summary>
	/// <remarks>
	/// The render graph treats a format that the destination turns down the same as one it doesn't
	/// implement, and converts frames to another format the destination takes.
	/// </remarks>
	public interface IFrameFormatFilter
	{
		/// <summary>
		/// Returns whether the destination takes frames in the given format.
		/// </summary>
		/// <param name="format">A frame format that the destination implements.</param>
		/// <returns><see langword="true"/> if the render graph can send frames in <paramref name="format"/>; otherwise, <see langword="false"/>.</returns>
		bool Accepts(FrameFormat format);
	}
}
