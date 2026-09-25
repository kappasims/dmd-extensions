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

			var lines = File.ReadAllLines(path);
			IniReport report;
			using (LogManager.SuspendLogging()) {
				report = new IniValidator(ListSectionNames(), ListDeviceNeutralDestinations(path, lines)).Validate(lines);
			}
			foreach (var issue in report.Issues) {
				var severity = issue.Severity == IniSeverity.Error ? "error" : "warning";
				var where = issue.Section == null ? "" : issue.Key == null ? $"[{issue.Section}] " : $"[{issue.Section}] {issue.Key}: ";
				Console.WriteLine($"{path}({issue.Line}): {severity}: {where}{issue.Message}");
			}
			if (report.DisabledSections.Count > 0) {
				if (report.Issues.Count > 0) {
					Console.WriteLine();
				}
				Console.WriteLine("Disabled sections:");
				foreach (var section in report.DisabledSections) {
					if (section.Problems.Count == 0) {
						Console.WriteLine($"{path}({section.Line}): [{section.Section}] is ready to enable.");
						continue;
					}
					Console.WriteLine($"{path}({section.Line}): [{section.Section}] would be skipped if it were enabled:");
					foreach (var problem in section.Problems) {
						Console.WriteLine($"  {problem}");
					}
				}
				Console.WriteLine();
			}
			var errors = report.Issues.Count(i => i.Severity == IniSeverity.Error);
			var warnings = report.Issues.Count - errors;
			Console.WriteLine($"{path}: {errors} error{(errors == 1 ? "" : "s")}, {warnings} warning{(warnings == 1 ? "" : "s")}.");
			return errors == 0 ? 0 : 1;
		}

		/// <summary>
		/// Returns the device-neutral destinations that DmdDevice reads from the lines of an ini.
		/// </summary>
		/// <param name="path">The path the lines came from, as it appears in the log.</param>
		/// <param name="lines">The lines of the ini.</param>
		/// <returns>A destination for each device-neutral section, or none if DmdDevice fails to load the ini.</returns>
		public static IReadOnlyList<IDeviceNeutralConfig> ListDeviceNeutralDestinations(string path, IReadOnlyList<string> lines)
		{
			try {
				return new Configuration(new TextConfigurationSource(path, string.Join("\n", lines))).DeviceNeutralDestinations;

			} catch (Exception) {
				// DmdDevice fails on this ini too, and the line checks report why.
				return new IDeviceNeutralConfig[0];
			}
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
