using CommandLine;

namespace DmdExt.Validate
{
	class ValidateOptions
	{
		[Option("use-ini", HelpText = "Path to the DmdDevice.ini to check. Without a path, the one DMDDEVICE_CONFIG points to.")]
		public string DmdDeviceIni { get; set; } = null;

		[ParserState]
		public IParserState LastParserState { get; set; }
	}
}
