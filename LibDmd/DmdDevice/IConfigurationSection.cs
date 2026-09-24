namespace LibDmd.DmdDevice
{
	/// <summary>
	/// Defines a section of DmdDevice.ini.
	/// </summary>
	public interface IConfigurationSection
	{
		/// <summary>
		/// Gets the name of the section.
		/// </summary>
		string Name { get; }
	}
}
