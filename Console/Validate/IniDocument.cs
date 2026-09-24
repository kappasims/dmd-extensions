using System.Collections.Generic;
using System.Linq;

namespace DmdExt.Validate
{
	/// <summary>
	/// Represents a section header in DmdDevice.ini.
	/// </summary>
	public class IniHeader
	{
		public IniHeader(int line, string section)
		{
			Line = line;
			Section = section;
		}

		/// <summary>
		/// Gets the one-based line number of the header.
		/// </summary>
		public int Line { get; }

		/// <summary>
		/// Gets the section name.
		/// </summary>
		public string Section { get; }
	}

	/// <summary>
	/// Represents a key and its value in DmdDevice.ini.
	/// </summary>
	public class IniEntry
	{
		public IniEntry(int line, string section, string key, string value)
		{
			Line = line;
			Section = section;
			Key = key;
			Value = value;
		}

		/// <summary>
		/// Gets the one-based line number of the entry.
		/// </summary>
		public int Line { get; }

		/// <summary>
		/// Gets the section the entry is in.
		/// </summary>
		public string Section { get; }

		/// <summary>
		/// Gets the key name.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// Gets the value.
		/// </summary>
		public string Value { get; }
	}

	/// <summary>
	/// Represents the lines of DmdDevice.ini, read the way ini-parser 2.5.2 reads them with the settings DmdDevice uses.
	/// </summary>
	public class IniDocument
	{
		private const string UnreadableLine = "ini-parser can't read this line, so DmdDevice ignores the whole file and uses its defaults.";

		/// <summary>
		/// Initializes a new instance of the <see cref="IniDocument"/> class from the lines of a file.
		/// </summary>
		/// <param name="lines">The lines of the file.</param>
		public IniDocument(IReadOnlyList<string> lines)
		{
			var headers = new List<IniHeader>();
			var entries = new List<IniEntry>();
			var issues = new List<IniIssue>();
			string section = null;
			for (var i = 0; i < lines.Count; i++) {
				var number = i + 1;
				var line = lines[i].Trim();
				if (line.Length == 0 || line.StartsWith(";")) {
					continue;
				}
				if (line.StartsWith("[")) {
					if (!line.EndsWith("]")) {
						issues.Add(new IniIssue(number, IniSeverity.Error, null, null, UnreadableLine));
						continue;
					}
					section = line.Substring(1, line.Length - 2).Trim();
					headers.Add(new IniHeader(number, section));
					continue;
				}
				var equals = line.IndexOf('=');
				if (equals < 0) {
					issues.Add(new IniIssue(number, IniSeverity.Error, section, null, UnreadableLine));
					continue;
				}
				var key = line.Substring(0, equals).Trim();
				var value = line.Substring(equals + 1).Trim();
				if (section == null) {
					issues.Add(new IniIssue(number, IniSeverity.Warning, null, key, "This key comes before any section, so it's ignored."));
					continue;
				}
				if (key.StartsWith("#")) {
					issues.Add(new IniIssue(number, IniSeverity.Warning, section, key, "A line starting with # isn't a comment, so this is read as a key and ignored. Comments start with ;."));
					continue;
				}
				entries.Add(new IniEntry(number, section, key, value));
			}
			Headers = headers;
			Entries = entries;
			ReadIssues = issues;
		}

		/// <summary>
		/// Gets the section headers, in line order.
		/// </summary>
		public IReadOnlyList<IniHeader> Headers { get; }

		/// <summary>
		/// Gets the keys and values, in line order.
		/// </summary>
		public IReadOnlyList<IniEntry> Entries { get; }

		/// <summary>
		/// Gets the lines that can't be read as a header or a key.
		/// </summary>
		public IReadOnlyList<IniIssue> ReadIssues { get; }

		/// <summary>
		/// Lists sections and keys that appear more than once, which are read as one.
		/// </summary>
		/// <returns>An issue for each repeat after the first.</returns>
		public IReadOnlyList<IniIssue> ListDuplicateIssues()
		{
			var issues = new List<IniIssue>();
			foreach (var group in Headers.GroupBy(h => h.Section)) {
				var first = group.First();
				foreach (var repeat in group.Skip(1)) {
					issues.Add(new IniIssue(repeat.Line, IniSeverity.Warning, repeat.Section, null,
						$"This section also starts on line {first.Line}, and the two are read as one."));
				}
			}
			foreach (var group in Entries.GroupBy(e => new { e.Section, e.Key })) {
				var first = group.First();
				foreach (var repeat in group.Skip(1)) {
					issues.Add(new IniIssue(repeat.Line, IniSeverity.Warning, repeat.Section, repeat.Key,
						$"This key is also on line {first.Line}, and only that first value is used."));
				}
			}
			return issues;
		}

		/// <summary>
		/// Lists plugin keys of <c>[global]</c> that DmdDevice never gets to, after a gap in the numbering or without a path.
		/// </summary>
		/// <returns>An issue for each plugin key that isn't loaded.</returns>
		public IReadOnlyList<IniIssue> ListPluginIssues()
		{
			var issues = new List<IniIssue>();
			var plugins = Entries
				.Where(e => e.Section == "global")
				.Select(e => new { Entry = e, Key = PluginKey.Parse(e.Key) })
				.Where(p => p.Key != PluginKey.None && p.Key.Index <= 9)
				.ToList();

			foreach (var path in new[] { "path", "path64" }) {
				var indices = new HashSet<int>(plugins.Where(p => p.Key.Property == path).Select(p => p.Key.Index));
				var gap = Enumerable.Range(0, 10).Where(i => !indices.Contains(i)).DefaultIfEmpty(10).First();
				foreach (var plugin in plugins.Where(p => p.Key.Property == path && p.Key.Index > gap)) {
					issues.Add(new IniIssue(plugin.Entry.Line, IniSeverity.Warning, "global", plugin.Entry.Key,
						$"There's no plugin.{gap}.{path}, so the {(path == "path" ? "32" : "64")}-bit DmdDevice stops looking for plugins there and never loads this one."));
				}
			}
			var withPath = new HashSet<int>(plugins.Where(p => p.Key.Property == "path" || p.Key.Property == "path64").Select(p => p.Key.Index));
			foreach (var plugin in plugins.Where(p => p.Key.Property != "path" && p.Key.Property != "path64" && !withPath.Contains(p.Key.Index))) {
				issues.Add(new IniIssue(plugin.Entry.Line, IniSeverity.Warning, "global", plugin.Entry.Key,
					$"There's no plugin.{plugin.Key.Index}.path or plugin.{plugin.Key.Index}.path64, so this is ignored."));
			}
			return issues;
		}

		/// <summary>
		/// Lists virtual DMD style selections that name a style <c>[virtualdmd]</c> doesn't define.
		/// </summary>
		/// <returns>An issue for each such selection.</returns>
		public IReadOnlyList<IniIssue> ListStyleSelectionIssues()
		{
			var styles = new HashSet<string>(Entries
				.Where(e => e.Section == "virtualdmd")
				.Select(e => e.Key.Split(new[] { '.' }, 4))
				.Where(names => names[0] == "style" && names.Length >= 2)
				.Select(names => names[1]));

			return Entries
				.Where(e => e.Section == "virtualdmd" && e.Key == "style" || e.Key == "virtualdmd style")
				.Where(e => !styles.Contains(e.Value))
				.Select(e => new IniIssue(e.Line, IniSeverity.Warning, e.Section, e.Key,
					$"No style named \"{e.Value}\" is defined in [virtualdmd], so the virtual DMD uses its built-in look."))
				.ToList();
		}
	}
}
