using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibDmd.DmdDevice;
using NLog;

namespace DmdExt.Validate
{
	/// <summary>
	/// Represents the <c>validate</c> command, which reports problems in a DmdDevice.ini without running anything.
	/// </summary>
	public static class ValidateCommand
	{
		/// <summary>
		/// Checks the ini that the options name and prints what it finds.
		/// </summary>
		/// <param name="options">The command line options.</param>
		/// <returns>The exit code: 0 when there are no errors, otherwise 1.</returns>
		internal static int Execute(ValidateOptions options)
		{
			var path = options.DmdDeviceIni ?? Configuration.GetEnvConfigPath();
			if (path == null) {
				Console.Error.WriteLine("No ini to check. Pass one with --use-ini, or set DMDDEVICE_CONFIG.");
				return 1;
			}
			if (!File.Exists(path)) {
				Console.Error.WriteLine($"{path} doesn't exist.");
				return 1;
			}

			var issues = new IniValidator(ListSectionNames()).Validate(File.ReadAllLines(path));
			foreach (var issue in issues) {
				var severity = issue.Severity == IniSeverity.Error ? "error" : "warning";
				var where = issue.Section == null ? "" : issue.Key == null ? $"[{issue.Section}] " : $"[{issue.Section}] {issue.Key}: ";
				Console.WriteLine($"{path}({issue.Line}): {severity}: {where}{issue.Message}");
			}
			var errors = issues.Count(i => i.Severity == IniSeverity.Error);
			Console.WriteLine($"{path}: {errors} error{(errors == 1 ? "" : "s")}, {issues.Count - errors} warning{(issues.Count - errors == 1 ? "" : "s")}.");
			return errors == 0 ? 0 : 1;
		}

		/// <summary>
		/// Returns the names of the configuration sections that DmdDevice reads.
		/// </summary>
		/// <returns>The section names.</returns>
		public static IReadOnlyList<string> ListSectionNames()
		{
			Configuration config;
			using (LogManager.SuspendLogging()) {
				config = new Configuration(new TextConfigurationSource("DmdDevice.ini", string.Empty));
			}
			return typeof(Configuration).GetProperties()
				.Where(p => p.GetIndexParameters().Length == 0)
				.Select(p => p.GetValue(config))
				.OfType<IConfigurationSection>()
				.Select(s => s.Name)
				.Distinct()
				.ToList();
		}
	}
}
