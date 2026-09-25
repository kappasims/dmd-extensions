using System.Collections.Generic;

namespace DmdExt.Validate
{
	/// <summary>
	/// Represents what <see cref="IniValidator"/> found in DmdDevice.ini.
	/// </summary>
	public class IniReport
	{
		/// <summary>
		/// Gets the problems with settings that DmdDevice reads, in line order.
		/// </summary>
		public IReadOnlyList<IniIssue> Issues { get; }

		/// <summary>
		/// Gets the device-neutral sections that aren't enabled, in line order.
		/// </summary>
		public IReadOnlyList<IniDisabledSection> DisabledSections { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IniReport"/> class.
		/// </summary>
		/// <param name="issues">The problems with settings that DmdDevice reads.</param>
		/// <param name="disabledSections">The device-neutral sections that aren't enabled.</param>
		public IniReport(IReadOnlyList<IniIssue> issues, IReadOnlyList<IniDisabledSection> disabledSections)
		{
			Issues = issues;
			DisabledSections = disabledSections;
		}
	}

	/// <summary>
	/// Represents a device-neutral section that isn't enabled, with what would keep DmdDevice from using it once it is.
	/// </summary>
	public class IniDisabledSection
	{
		/// <summary>
		/// Gets the one-based line number the section starts on.
		/// </summary>
		public int Line { get; }

		/// <summary>
		/// Gets the section name.
		/// </summary>
		public string Section { get; }

		/// <summary>
		/// Gets a description of each problem, or an empty list if the section is ready to enable.
		/// </summary>
		public IReadOnlyList<string> Problems { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IniDisabledSection"/> class.
		/// </summary>
		/// <param name="line">The one-based line number the section starts on.</param>
		/// <param name="section">The section name.</param>
		/// <param name="problems">A description of each problem.</param>
		public IniDisabledSection(int line, string section, IReadOnlyList<string> problems)
		{
			Line = line;
			Section = section;
			Problems = problems;
		}
	}
}
