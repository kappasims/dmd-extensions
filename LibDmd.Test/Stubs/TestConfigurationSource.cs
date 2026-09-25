using IniParser.Model;
using IniParser.Parser;
using LibDmd.DmdDevice;

namespace LibDmd.Test.Stubs
{
	public class TestConfigurationSource : IConfigurationSource
	{
		public string Path { get; set; } = "test.ini";
		public string DataPath { get; set; }
		public bool HasIniData { get; set; } = true;
		public IniData IniData { get; set; }
		public IniData SavedIniData { get; private set; }

		public TestConfigurationSource(string ini)
		{
			var parser = new IniDataParser();
			parser.Configuration.AllowDuplicateSections = true;
			parser.Configuration.AllowDuplicateKeys = true;
			IniData = parser.Parse(ini);
		}

		public IniData LoadIniData()
		{
			return IniData;
		}

		public void SaveIniData(IniData data)
		{
			SavedIniData = data;
		}
	}
}
