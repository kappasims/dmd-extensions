using System;
using System.Collections.Generic;
using System.Linq;

namespace DmdExt.Validate
{
	/// <summary>
	/// Represents a checker that reports problems in the text of DmdDevice.ini.
	/// </summary>
	/// <remarks>
	/// The text is read as an <see cref="IniDocument"/>, and each value is checked with the rules of the getter
	/// that reads it. A section that isn't one of the configuration sections is treated as a game section.
	/// </remarks>
	public class IniValidator
	{
		private readonly HashSet<string> _sections;

		/// <summary>
		/// Initializes a new instance of the <see cref="IniValidator"/> class.
		/// </summary>
		/// <param name="sections">The names of the configuration sections.</param>
		public IniValidator(IEnumerable<string> sections)
		{
			_sections = new HashSet<string>(sections);
		}

		/// <summary>
		/// Returns the problems in the given lines of DmdDevice.ini.
		/// </summary>
		/// <param name="lines">The lines of the file.</param>
		/// <returns>The problems, in line order.</returns>
		public IReadOnlyList<IniIssue> Validate(IReadOnlyList<string> lines)
		{
			var document = new IniDocument(lines);
			var issues = new List<IniIssue>(document.ReadIssues);
			issues.AddRange(document.ListDuplicateIssues());

			foreach (var header in document.Headers.GroupBy(h => h.Section).Select(g => g.First())) {
				var sectionEntries = document.Entries.Where(e => e.Section == header.Section).ToList();
				if (_sections.Contains(header.Section)) {
					if (IniSchema.Sections.Contains(header.Section)) {
						foreach (var entry in sectionEntries) {
							CheckKey(header.Section, entry.Key, entry, false, issues);
						}
					}
					continue;
				}

				var sameName = _sections.FirstOrDefault(s => string.Equals(s, header.Section, StringComparison.OrdinalIgnoreCase));
				if (sameName != null) {
					issues.Add(new IniIssue(header.Line, IniSeverity.Warning, header.Section, null,
						$"Section names are case-sensitive, so this section is ignored. Did you mean [{sameName}]?"));
					continue;
				}
				var overridesSetting = sectionEntries.Any(e => {
					var space = e.Key.IndexOf(' ');
					if (space < 0) {
						return IniSchema.ReadKey("global", e.Key).Key != IniKey.Unread;
					}
					var section = e.Key.Substring(0, space);
					return section != "global" && _sections.Contains(section) && IniSchema.ReadKey(section, e.Key.Substring(space + 1)).Key != IniKey.Unread;
				});
				if (!overridesSetting) {
					var near = FindNearestName(_sections, header.Section);
					issues.Add(new IniIssue(header.Line, IniSeverity.Warning, header.Section, null,
						"This isn't a configuration section. As a game section it overrides nothing, since none of its keys is a setting."
						+ (near.Length == 0 ? "" : $" Did you mean [{near}]?")));
					continue;
				}
				foreach (var entry in sectionEntries) {
					var space = entry.Key.IndexOf(' ');
					if (space < 0) {
						CheckKey("global", entry.Key, entry, true, issues);
						continue;
					}
					var section = entry.Key.Substring(0, space);
					if (section == "global" || !_sections.Contains(section) || !IniSchema.Sections.Contains(section)) {
						issues.Add(new IniIssue(entry.Line, IniSeverity.Warning, entry.Section, entry.Key,
							"DmdDevice doesn't read this key, so it's ignored. A game section overrides a key of another section as \"<section> <key>\", and a key of [global] without a prefix."));
						continue;
					}
					CheckKey(section, entry.Key.Substring(space + 1), entry, true, issues);
				}
			}
			issues.AddRange(document.ListPluginIssues());
			issues.AddRange(document.ListStyleSelectionIssues());

			return issues.OrderBy(i => i.Line).ThenByDescending(i => i.Severity).ToList();
		}

		private static void CheckKey(string section, string key, IniEntry entry, bool inGameSection, List<IniIssue> issues)
		{
			var reading = IniSchema.ReadKey(section, key);
			if (reading.Key == IniKey.Unread) {
				var sameName = IniSchema.ListFixedKeys(section).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
				var near = sameName == null ? FindNearestName(IniSchema.ListFixedKeys(section), key) : "";
				issues.Add(new IniIssue(entry.Line, IniSeverity.Warning, entry.Section, entry.Key, "DmdDevice doesn't read this key, so it's ignored."
					+ (sameName != null ? $" Keys are case-sensitive. Did you mean \"{sameName}\"?" : near.Length > 0 ? $" Did you mean \"{near}\"?" : "")));
				return;
			}
			if (reading.Problem.Length > 0) {
				issues.Add(new IniIssue(entry.Line, reading.Severity, entry.Section, entry.Key, reading.Problem));
			}
			if (reading.Key.IsUnused) {
				issues.Add(new IniIssue(entry.Line, IniSeverity.Warning, entry.Section, entry.Key, "DmdDevice reads this key but never uses it."));
			}
			if (inGameSection && reading.Key.IsStyleProperty) {
				issues.Add(new IniIssue(entry.Line, IniSeverity.Warning, entry.Section, entry.Key,
					"A game section can't override a style property, so this is ignored. It can only choose a different style."));
				return;
			}
			issues.AddRange(reading.Key.ListValueIssues(entry));
		}

		// The closest candidate within two edits, or an empty string if there is none.
		private static string FindNearestName(IEnumerable<string> candidates, string name)
		{
			var nearest = string.Empty;
			var nearestDistance = 3;
			foreach (var candidate in candidates) {
				var d = new int[candidate.Length + 1, name.Length + 1];
				for (var i = 0; i <= candidate.Length; i++) {
					d[i, 0] = i;
				}
				for (var j = 0; j <= name.Length; j++) {
					d[0, j] = j;
				}
				for (var i = 1; i <= candidate.Length; i++) {
					for (var j = 1; j <= name.Length; j++) {
						var cost = candidate[i - 1] == name[j - 1] ? 0 : 1;
						d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
					}
				}
				var distance = d[candidate.Length, name.Length];
				if (distance > 0 && distance < nearestDistance) {
					nearest = candidate;
					nearestDistance = distance;
				}
			}
			return nearest;
		}
	}
}
