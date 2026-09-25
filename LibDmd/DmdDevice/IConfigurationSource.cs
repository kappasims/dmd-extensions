using IniParser.Model;

namespace LibDmd.DmdDevice
{
	/// <summary>
	/// Defines where <see cref="Configuration"/> loads its ini data from and saves it to.
	/// </summary>
	public interface IConfigurationSource
	{
		/// <summary>
		/// Gets the path of the ini data, as it appears in the log.
		/// </summary>
		string Path { get; }

		/// <summary>
		/// Gets the folder of data files that goes with the ini data.
		/// </summary>
		/// <value>The folder, or <see langword="null"/> if there is none.</value>
		string DataPath { get; }

		/// <summary>
		/// Gets a value indicating whether there is ini data to load.
		/// </summary>
		bool HasIniData { get; }

		/// <summary>
		/// Loads the ini data.
		/// </summary>
		/// <returns>The ini data.</returns>
		IniData LoadIniData();

		/// <summary>
		/// Saves the ini data.
		/// </summary>
		/// <param name="data">The ini data to save.</param>
		void SaveIniData(IniData data);
	}
}
