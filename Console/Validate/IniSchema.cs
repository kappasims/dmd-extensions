using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using LibDmd.Common;
using LibDmd.Input;
using LibDmd.Output.Virtual.AlphaNumeric;

namespace DmdExt.Validate
{
	/// <summary>
	/// Specifies how DmdDevice reads the value of a key.
	/// </summary>
	public enum IniKind
	{
		/// <summary>DmdDevice doesn't read the key.</summary>
		Unknown,

		Boolean,
		Integer,
		Double,
		String,
		Color,
		Enum,
	}

	/// <summary>
	/// Represents a key that DmdDevice reads.
	/// </summary>
	public class IniKey
	{
		/// <summary>
		/// Represents a key that DmdDevice doesn't read.
		/// </summary>
		public static readonly IniKey Unread = new IniKey(IniKind.Unknown);

		internal static readonly IniKey Boolean = new IniKey(IniKind.Boolean);
		internal static readonly IniKey Integer = new IniKey(IniKind.Integer);
		internal static readonly IniKey Double = new IniKey(IniKind.Double);
		internal static readonly IniKey String = new IniKey(IniKind.String);

		private static readonly Regex Color = new Regex("^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");

		/// <summary>
		/// Gets how the value is read.
		/// </summary>
		public IniKind Kind { get; }

		/// <summary>
		/// Gets the enum the value is parsed into, if <see cref="Kind"/> is <see cref="IniKind.Enum"/>.
		/// </summary>
		public Type EnumType { get; }

		/// <summary>
		/// Gets a value indicating whether DmdDevice reads the key but never uses it.
		/// </summary>
		public bool IsUnused { get; }

		/// <summary>
		/// Gets a value indicating whether the key belongs to a style, which a game section can't override.
		/// </summary>
		public bool IsStyleProperty { get; }

		public IniKey(IniKind kind, Type enumType = null, bool isUnused = false, bool isStyleProperty = false)
		{
			Kind = kind;
			EnumType = enumType;
			IsUnused = isUnused;
			IsStyleProperty = isStyleProperty;
		}

		/// <summary>
		/// Returns the problems with the value of an entry for this key, checked the way its getter reads it.
		/// </summary>
		/// <param name="entry">The entry.</param>
		/// <returns>The problems, or an empty list if the value is read as written.</returns>
		public IReadOnlyList<IniIssue> ListValueIssues(IniEntry entry)
		{
			var value = entry.Value;
			switch (Kind) {
				case IniKind.Boolean:
					if (!bool.TryParse(value, out _)) {
						return CreateValueIssue(entry, IniSeverity.Error, "must be true or false. DmdDevice uses the default instead.");
					}
					break;

				case IniKind.Integer:
					try {
						int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
					} catch (FormatException) {
						return CreateValueIssue(entry, IniSeverity.Error, "must be a whole number. DmdDevice uses the default instead.");
					} catch (OverflowException) {
						return CreateValueIssue(entry, IniSeverity.Error, "is too large. DmdDevice fails when it reads this key.");
					}
					break;

				case IniKind.Double:
					double number;
					try {
						number = double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
					} catch (FormatException) {
						return CreateValueIssue(entry, IniSeverity.Error, "must be a number. DmdDevice uses the default instead.");
					} catch (OverflowException) {
						return CreateValueIssue(entry, IniSeverity.Error, "is too large. DmdDevice fails when it reads this key.");
					}
					if (number >= int.MaxValue) {
						return CreateValueIssue(entry, IniSeverity.Error, $"must be smaller than {int.MaxValue}. DmdDevice uses the default instead.");
					}
					if (value.Contains(",")) {
						return CreateValueIssue(entry, IniSeverity.Warning,
							$"is read as {number.ToString(CultureInfo.InvariantCulture)}, since a comma is a thousands separator. Decimals take a period.");
					}
					break;

				case IniKind.Color:
					if (!Color.IsMatch(value)) {
						return CreateValueIssue(entry, IniSeverity.Error, "must be a color in hex, such as #FF3000. DmdDevice uses the default instead.");
					}
					break;

				case IniKind.Enum:
					var isEnumValue = false;
					try {
						var parsed = Enum.Parse(EnumType, value.Substring(0, 1).ToUpper() + value.Substring(1));
						isEnumValue = Enum.IsDefined(EnumType, parsed);

					} catch (ArgumentException) {
					}
					if (!isEnumValue) {
						var names = Enum.GetNames(EnumType).Select(n => char.ToLowerInvariant(n[0]) + n.Substring(1));
						return CreateValueIssue(entry, IniSeverity.Error, $"must be one of {string.Join(", ", names)}. DmdDevice uses the default instead.");
					}
					break;
			}
			return new IniIssue[0];
		}

		private static IReadOnlyList<IniIssue> CreateValueIssue(IniEntry entry, IniSeverity severity, string message)
		{
			return new[] { new IniIssue(entry.Line, severity, entry.Section, entry.Key, $"Value \"{entry.Value}\" {message}") };
		}
	}

	/// <summary>
	/// Represents how DmdDevice reads a key, and any problem with how the key is written.
	/// </summary>
	public class IniKeyReading
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="IniKeyReading"/> class for a key with no problem.
		/// </summary>
		/// <param name="key">How the key is read.</param>
		public IniKeyReading(IniKey key) : this(key, string.Empty, IniSeverity.None)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IniKeyReading"/> class.
		/// </summary>
		/// <param name="key">How the key is read.</param>
		/// <param name="problem">A problem with how the key is written.</param>
		/// <param name="severity">How serious <paramref name="problem"/> is.</param>
		public IniKeyReading(IniKey key, string problem, IniSeverity severity)
		{
			Key = key;
			Problem = problem;
			Severity = severity;
		}

		/// <summary>
		/// Gets how the key is read, or <see cref="IniKey.Unread"/> if DmdDevice never reads it.
		/// </summary>
		public IniKey Key { get; }

		/// <summary>
		/// Gets a problem with how the key is written, or an empty string if there is none.
		/// </summary>
		public string Problem { get; }

		/// <summary>
		/// Gets how serious <see cref="Problem"/> is.
		/// </summary>
		public IniSeverity Severity { get; }
	}

	/// <summary>
	/// Represents a <c>plugin.N.property</c> key of <c>[global]</c>.
	/// </summary>
	public class PluginKey
	{
		/// <summary>
		/// Represents a key that isn't a plugin key.
		/// </summary>
		public static readonly PluginKey None = new PluginKey(-1, string.Empty, IniKey.Unread);

		private static readonly Regex Pattern = new Regex(@"^plugin\.(\d+)\.(.+)$");

		private static readonly Dictionary<string, IniKey> Properties = new Dictionary<string, IniKey> {
			{ "path", IniKey.String },
			{ "path64", IniKey.String },
			{ "passthrough", IniKey.Boolean },
			{ "scalermode", new IniKey(IniKind.Enum, typeof(ScalerMode)) },
		};

		private PluginKey(int index, string property, IniKey key)
		{
			Index = index;
			Property = property;
			Key = key;
		}

		/// <summary>
		/// Gets the plugin index.
		/// </summary>
		public int Index { get; }

		/// <summary>
		/// Gets the part after the index, such as <c>path64</c>.
		/// </summary>
		public string Property { get; }

		/// <summary>
		/// Gets how the value is read.
		/// </summary>
		public IniKey Key { get; }

		/// <summary>
		/// Reads a plugin key from a key name.
		/// </summary>
		/// <param name="key">The key name.</param>
		/// <returns>The plugin key, or <see cref="None"/> if the name isn't one.</returns>
		public static PluginKey Parse(string key)
		{
			var match = Pattern.Match(key);
			if (!match.Success) {
				return None;
			}
			var digits = match.Groups[1].Value;
			var property = match.Groups[2].Value;
			var isIndex = digits == "0" || !digits.StartsWith("0") && digits.Length < 10;
			if (!isIndex || !Properties.TryGetValue(property, out var iniKey)) {
				return None;
			}
			return new PluginKey(int.Parse(digits), property, iniKey);
		}
	}

	/// <summary>
	/// Represents a family of keys in one section whose names follow a pattern, such as <c>plugin.N.path</c>.
	/// </summary>
	public abstract class IniKeyFamily
	{
		/// <summary>
		/// Finds how DmdDevice reads a key of the family.
		/// </summary>
		/// <param name="key">The key name.</param>
		/// <returns>How the key is read, with <see cref="IniKey.Unread"/> if it isn't in the family.</returns>
		public abstract IniKeyReading ReadKey(string key);
	}

	/// <summary>
	/// Represents the plugin keys of <c>[global]</c>.
	/// </summary>
	public class PluginKeyFamily : IniKeyFamily
	{
		public override IniKeyReading ReadKey(string key)
		{
			var plugin = PluginKey.Parse(key);
			if (plugin == PluginKey.None) {
				return new IniKeyReading(IniKey.Unread);
			}
			if (plugin.Index > 9) {
				return new IniKeyReading(plugin.Key, "Only plugin.0 to plugin.9 are read, so this is ignored.", IniSeverity.Warning);
			}
			return new IniKeyReading(plugin.Key);
		}
	}

	/// <summary>
	/// Represents the style keys of <c>[virtualdmd]</c>.
	/// </summary>
	public class VirtualDmdStyleKeyFamily : IniKeyFamily
	{
		private static readonly Dictionary<string, IniKey> StyleKeys = new Dictionary<string, IniKey> {
			{ "dotsize", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "dotrounding", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "dotsharpness", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "unlitdot", new IniKey(IniKind.Color, isStyleProperty: true) },
			{ "brightness", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "dotglow", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "backglow", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "gamma", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "tint", new IniKey(IniKind.Color, isStyleProperty: true) },
			{ "glass", new IniKey(IniKind.String, isStyleProperty: true) },
			{ "glass.padding.left", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "glass.padding.top", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "glass.padding.right", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "glass.padding.bottom", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "glass.color", new IniKey(IniKind.Color, isStyleProperty: true) },
			{ "glass.lighting", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "frame", new IniKey(IniKind.String, isStyleProperty: true) },
			{ "frame.padding.left", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "frame.padding.top", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "frame.padding.right", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "frame.padding.bottom", new IniKey(IniKind.Double, isStyleProperty: true) },
		};

		public override IniKeyReading ReadKey(string key)
		{
			var names = key.Split(new[] { '.' }, 4);
			if (names[0] != "style" || names.Length < 2) {
				return new IniKeyReading(IniKey.Unread);
			}
			if (names.Length == 2) {
				return new IniKeyReading(IniKey.String, "This defines style \"" + names[1] + "\" with no property.", IniSeverity.Warning);
			}
			return new IniKeyReading(StyleKeys.TryGetValue(string.Join(".", names.Skip(2)), out var styleKey) ? styleKey : IniKey.Unread);
		}
	}

	/// <summary>
	/// Represents the position and style keys of <c>[alphanumeric]</c>.
	/// </summary>
	public class AlphaNumericKeyFamily : IniKeyFamily
	{
		private const string MissingProperty = "DmdDevice fails to load the ini with this key.";

		private static readonly Regex PositionKey = new Regex(@"^pos\.(\d+)\.(left|top|height)$");

		private static readonly Dictionary<string, IniKey> StyleKeys = new Dictionary<string, IniKey> {
			{ "skewangle", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "linepad", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "outerpad", new IniKey(IniKind.Double, isStyleProperty: true) },
			{ "weight", new IniKey(IniKind.Enum, typeof(SegmentWeight), isStyleProperty: true) },
			{ "backgroundcolor", new IniKey(IniKind.Color, isStyleProperty: true) },
		};

		private static readonly string[] Layers = { "foreground", "innerglow", "outerglow", "background" };

		private static readonly Dictionary<string, IniKey> LayerKeys = new Dictionary<string, IniKey> {
			{ "enabled", new IniKey(IniKind.Boolean, isStyleProperty: true) },
			{ "color", new IniKey(IniKind.Color, isStyleProperty: true) },
			{ "blur.enabled", new IniKey(IniKind.Boolean, isStyleProperty: true) },
			{ "blur.x", new IniKey(IniKind.Integer, isStyleProperty: true) },
			{ "blur.y", new IniKey(IniKind.Integer, isStyleProperty: true) },
			{ "dilate.enabled", new IniKey(IniKind.Boolean, isStyleProperty: true) },
			{ "dilate.x", new IniKey(IniKind.Integer, isStyleProperty: true) },
			{ "dilate.y", new IniKey(IniKind.Integer, isStyleProperty: true) },
		};

		public override IniKeyReading ReadKey(string key)
		{
			var position = PositionKey.Match(key);
			if (position.Success) {
				var digits = position.Groups[1].Value;
				if (digits == "0" || !digits.StartsWith("0") && digits.Length < 10) {
					return new IniKeyReading(IniKey.Double);
				}
			}
			var names = key.Split(new[] { '.' }, 4);
			if (names[0] != "style" || names.Length < 2) {
				return new IniKeyReading(IniKey.Unread);
			}
			if (names.Length == 2) {
				return new IniKeyReading(IniKey.String, "A style key needs a property after the style name. " + MissingProperty, IniSeverity.Error);
			}
			if (StyleKeys.TryGetValue(names[2], out var styleKey)) {
				if (names.Length == 4) {
					return new IniKeyReading(styleKey, "Everything after \"" + names[2] + "\" is ignored, so this is read as style." + names[1] + "." + names[2] + ".", IniSeverity.Warning);
				}
				return new IniKeyReading(styleKey);
			}
			if (!Layers.Contains(names[2])) {
				return new IniKeyReading(IniKey.Unread);
			}
			if (names.Length == 3) {
				return new IniKeyReading(IniKey.String, "A style layer key needs a property after the layer. " + MissingProperty, IniSeverity.Error);
			}
			return new IniKeyReading(LayerKeys.TryGetValue(names[3], out var layerKey) ? layerKey : IniKey.Unread);
		}
	}

	/// <summary>
	/// Provides the keys that DmdDevice reads from each section of DmdDevice.ini.
	/// </summary>
	/// <remarks>
	/// This mirrors the getters in <c>LibDmd/DmdDevice/Configuration.cs</c>. A key added there has to be
	/// added here too.
	/// </remarks>
	public static class IniSchema
	{
		private static readonly IniKey UnusedBoolean = new IniKey(IniKind.Boolean, isUnused: true);
		private static readonly IniKey UnusedString = new IniKey(IniKind.String, isUnused: true);
		private static readonly IniKey ScalerModeKey = new IniKey(IniKind.Enum, typeof(ScalerMode));

		private static readonly Dictionary<string, IniKey> ZeDmdKeys = new Dictionary<string, IniKey> {
			{ "enabled", IniKey.Boolean }, { "debug", IniKey.Boolean }, { "brightness", IniKey.Integer }, { "port", IniKey.String },
		};

		private static readonly Dictionary<string, IniKey> ZeDmdWiFiKeys = new Dictionary<string, IniKey> {
			{ "enabled", IniKey.Boolean }, { "debug", IniKey.Boolean }, { "brightness", IniKey.Integer }, { "port", UnusedString }, { "wifi.address", IniKey.String },
		};

		private static readonly Dictionary<string, IniKey> VideoKeys = new Dictionary<string, IniKey> {
			{ "enabled", IniKey.Boolean }, { "path", IniKey.String }, { "scaletohd", UnusedBoolean },
		};

		private static readonly Dictionary<string, Dictionary<string, IniKey>> FixedKeys = new Dictionary<string, Dictionary<string, IniKey>> {
			{ "global", new Dictionary<string, IniKey> {
				{ "resize", new IniKey(IniKind.Enum, typeof(ResizeMode)) },
				{ "fliphorizontally", IniKey.Boolean },
				{ "flipvertically", IniKey.Boolean },
				{ "colorize", IniKey.Boolean },
				{ "scaletohd", IniKey.Boolean },
				{ "scalermode", ScalerModeKey },
				{ "vni.scalermode", ScalerModeKey },
				{ "vni.key", IniKey.String },
				{ "skipanalytics", IniKey.Boolean },
			} },
			{ "virtualdmd", new Dictionary<string, IniKey> {
				{ "enabled", IniKey.Boolean }, { "stayontop", IniKey.Boolean }, { "ignorear", IniKey.Boolean }, { "useregistry", IniKey.Boolean },
				{ "left", IniKey.Double }, { "top", IniKey.Double }, { "width", IniKey.Double }, { "height", IniKey.Double },
				{ "scalermode", new IniKey(IniKind.Enum, typeof(ScalerMode), isUnused: true) },
				{ "style", IniKey.String },
			} },
			{ "alphanumeric", new Dictionary<string, IniKey> {
				{ "enabled", IniKey.Boolean }, { "stayontop", IniKey.Boolean }, { "style", IniKey.String },
			} },
			{ "pindmd1", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean }, { "scaletohd", UnusedBoolean } } },
			{ "pindmd2", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean }, { "scaletohd", UnusedBoolean } } },
			{ "pindmd3", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean }, { "port", IniKey.String }, { "scaletohd", UnusedBoolean } } },
			{ "zedmd", ZeDmdKeys },
			{ "zedmdhd", ZeDmdKeys },
			{ "zedmdwifi", ZeDmdWiFiKeys },
			{ "zedmdhdwifi", ZeDmdWiFiKeys },
			{ "pin2dmd", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean }, { "delay", IniKey.Integer }, { "scaletohd", UnusedBoolean } } },
			{ "pixelcade", new Dictionary<string, IniKey> {
				{ "enabled", IniKey.Boolean }, { "port", IniKey.String }, { "matrix", new IniKey(IniKind.Enum, typeof(ColorMatrix)) }, { "scaletohd", UnusedBoolean },
			} },
			{ "video", VideoKeys },
			{ "gif", VideoKeys },
			{ "bitmap", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean }, { "path", IniKey.String } } },
			{ "browserstream", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean }, { "port", IniKey.Integer } } },
			{ "networkstream", new Dictionary<string, IniKey> {
				{ "enabled", IniKey.Boolean }, { "url", IniKey.String }, { "retry", IniKey.Boolean }, { "retry-interval", IniKey.Integer },
			} },
			{ "vpdbstream", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean }, { "endpoint", IniKey.String } } },
			{ "pinup", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean } } },
			{ "rawoutput", new Dictionary<string, IniKey> { { "enabled", IniKey.Boolean } } },
		};

		private static readonly Dictionary<string, IniKeyFamily> KeyFamilies = new Dictionary<string, IniKeyFamily> {
			{ "global", new PluginKeyFamily() },
			{ "virtualdmd", new VirtualDmdStyleKeyFamily() },
			{ "alphanumeric", new AlphaNumericKeyFamily() },
		};

		/// <summary>
		/// Gets the names of the sections that have keys here.
		/// </summary>
		public static IEnumerable<string> Sections {
			get {
				return FixedKeys.Keys;
			}
		}

		/// <summary>
		/// Lists the fixed keys of a section, without the key families.
		/// </summary>
		/// <param name="section">The section name.</param>
		/// <returns>The key names, or an empty sequence for an unknown section.</returns>
		public static IEnumerable<string> ListFixedKeys(string section)
		{
			return FixedKeys.TryGetValue(section, out var keys) ? keys.Keys : Enumerable.Empty<string>();
		}

		/// <summary>
		/// Finds how DmdDevice reads a key of a section.
		/// </summary>
		/// <param name="section">The section name.</param>
		/// <param name="key">The key name.</param>
		/// <returns>How the key is read, with <see cref="IniKey.Unread"/> if DmdDevice never reads it.</returns>
		public static IniKeyReading ReadKey(string section, string key)
		{
			if (FixedKeys.TryGetValue(section, out var keys) && keys.TryGetValue(key, out var fixedKey)) {
				return new IniKeyReading(fixedKey);
			}
			return KeyFamilies.TryGetValue(section, out var family) ? family.ReadKey(key) : new IniKeyReading(IniKey.Unread);
		}
	}
}
