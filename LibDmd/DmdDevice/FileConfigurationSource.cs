using System;
using System.IO;
using System.Reflection;
using IniParser;
using IniParser.Model;

namespace LibDmd.DmdDevice
{
	/// <summary>
	/// Loads and saves the ini data of <see cref="Configuration"/> as a DmdDevice.ini file.
	/// </summary>
	public class FileConfigurationSource : IConfigurationSource
	{
		private readonly FileIniDataParser _parser;

		/// <summary>
		/// Gets the path of the ini file.
		/// </summary>
		public string Path { get; }

		/// <summary>
		/// Gets the <c>dmdext</c> folder next to the ini file.
		/// </summary>
		/// <value>The folder, or <see langword="null"/> if it doesn't exist.</value>
		public string DataPath {
			get {
				var dataPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), "dmdext");
				return Directory.Exists(dataPath) ? dataPath : null;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileConfigurationSource"/> class.
		/// </summary>
		/// <param name="iniPath">The path of the ini file. Without one, the file that <c>DMDDEVICE_CONFIG</c>
		/// points to, or else DmdDevice.ini next to this assembly.</param>
		/// <exception cref="IniNotFoundException"><paramref name="iniPath"/> is given but doesn't exist.</exception>
		public FileConfigurationSource(string iniPath = null)
		{
			var envConfigPath = Configuration.GetEnvConfigPath();
			if (iniPath != null) {
				if (!File.Exists(iniPath)) {
					throw new IniNotFoundException(iniPath);
				}
				Path = iniPath;

			} else if (envConfigPath != null) {
				Path = envConfigPath;

			} else {
				var assemblyPath = System.IO.Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
				Path = System.IO.Path.Combine(assemblyPath, "DmdDevice.ini");
			}
			_parser = new FileIniDataParser();
			_parser.Parser.Configuration.AllowDuplicateSections = true;
			_parser.Parser.Configuration.AllowDuplicateKeys = true;
		}

		/// <summary>
		/// Gets a value indicating whether the ini file exists.
		/// </summary>
		public bool HasIniData {
			get {
				return File.Exists(Path);
			}
		}

		/// <summary>
		/// Loads the ini file.
		/// </summary>
		/// <returns>The ini data.</returns>
		public IniData LoadIniData()
		{
			return _parser.ReadFile(Path);
		}

		/// <summary>
		/// Saves the ini data to the ini file.
		/// </summary>
		/// <param name="data">The ini data to save.</param>
		public void SaveIniData(IniData data)
		{
			_parser.WriteFile(Path, data);
		}
	}
}
