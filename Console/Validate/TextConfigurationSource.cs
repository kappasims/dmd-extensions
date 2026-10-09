using System;
using IniParser.Model;
using IniParser.Parser;
using LibDmd.DmdDevice;

namespace DmdExt.Validate
{
	/// <summary>
	/// Represents ini text that a <see cref="Configuration"/> loads, parsed the way DmdDevice parses DmdDevice.ini.
	/// </summary>
	public class TextConfigurationSource : IConfigurationSource
	{
		private readonly string _text;

		/// <summary>
		/// Initializes a new instance of the <see cref="TextConfigurationSource"/> class.
		/// </summary>
		/// <param name="path">The path the text came from, as it appears in the log.</param>
		/// <param name="text">The ini text.</param>
		public TextConfigurationSource(string path, string text)
		{
			Path = path;
			_text = text;
		}

		public string Path { get; }

		public string DataPath {
			get {
				return null;
			}
		}

		public bool HasIniData {
			get {
				return true;
			}
		}

		public IniData LoadIniData()
		{
			var parser = new IniDataParser();
			parser.Configuration.AllowDuplicateSections = true;
			parser.Configuration.AllowDuplicateKeys = true;
			return parser.Parse(_text);
		}

		public void SaveIniData(IniData data)
		{
			throw new NotSupportedException("The validator never saves the ini.");
		}
	}
}
