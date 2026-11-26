namespace DmdExt.Validate
{
	/// <summary>
	/// Specifies how serious an <see cref="IniIssue"/> is.
	/// </summary>
	public enum IniSeverity
	{
		/// <summary>No problem.</summary>
		None,

		/// <summary>A setting that is ignored or doesn't do what it looks like it does.</summary>
		Warning,

		/// <summary>A setting that DmdDevice rejects, falls back from, or fails on.</summary>
		Error,
	}

	/// <summary>
	/// Represents a problem found in DmdDevice.ini.
	/// </summary>
	public class IniIssue
	{
		/// <summary>
		/// Gets the one-based line number the problem is on.
		/// </summary>
		public int Line { get; }

		/// <summary>
		/// Gets how serious the problem is.
		/// </summary>
		public IniSeverity Severity { get; }

		/// <summary>
		/// Gets the section the problem is in, or <see langword="null"/> if it isn't in a section.
		/// </summary>
		public string Section { get; }

		/// <summary>
		/// Gets the key the problem is about, or <see langword="null"/> if it isn't about a key.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// Gets a description of the problem.
		/// </summary>
		public string Message { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="IniIssue"/> class.
		/// </summary>
		/// <param name="line">The one-based line number.</param>
		/// <param name="severity">How serious the problem is.</param>
		/// <param name="section">The section, or <see langword="null"/>.</param>
		/// <param name="key">The key, or <see langword="null"/>.</param>
		/// <param name="message">A description of the problem.</param>
		public IniIssue(int line, IniSeverity severity, string section, string key, string message)
		{
			Line = line;
			Severity = severity;
			Section = section;
			Key = key;
			Message = message;
		}
	}
}
