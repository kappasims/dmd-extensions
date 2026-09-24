using System.Collections.Generic;
using System.IO;
using System.Linq;
using DmdExt.Validate;
using FluentAssertions;
using NUnit.Framework;

namespace DmdExt.Test
{
	[TestFixture]
	public class IniValidatorTests
	{
		private static readonly IReadOnlyList<string> Sections = ValidateCommand.ListSectionNames();

		[TestCase]
		public void Should_Know_Every_Configuration_Section()
		{
			IniSchema.Sections.Should().BeEquivalentTo(Sections);
		}

		[TestCase]
		public void Should_Find_Nothing_In_The_Sample_Ini()
		{
			string sample;
			using (var reader = new StreamReader(typeof(IniValidatorTests).Assembly.GetManifestResourceStream("DmdDevice.ini"))) {
				sample = reader.ReadToEnd();
			}

			var issues = new IniValidator(Sections).Validate(sample.Split('\n').Select(l => l.TrimEnd('\r')).ToList());

			issues.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Accept_Valid_Settings()
		{
			var issues = Validate(@"
				[global]
				resize = fit
				scalermode = Scale2x
				plugin.0.path64 = colorizer.dll
				plugin.0.passthrough = true
				[virtualdmd]
				left = 12.5
				style = mine
				style.mine.tint = #FF3000
				[zedmd]
				brightness = 10");

			issues.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Report_A_Resize_Mode_In_Capitals()
		{
			var issues = Validate("[global]\nresize = FIT");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "resize" && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_A_Boolean_That_Is_Not_True_Or_False()
		{
			var issues = Validate("[global]\nfliphorizontally = yes");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "fliphorizontally" && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_An_Unknown_Color_Matrix()
		{
			var issues = Validate("[pixelcade]\nmatrix = bgr");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "matrix" && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_An_Integer_With_Letters()
		{
			var issues = Validate("[zedmd]\nbrightness = 12x");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "brightness" && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_An_Integer_Too_Large_For_Its_Type()
		{
			var issues = Validate("[zedmd]\nbrightness = 99999999999");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "brightness" && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_A_Decimal_That_Is_Not_A_Number()
		{
			var issues = Validate("[virtualdmd]\nleft = abc");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "left" && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_A_Decimal_Too_Large_To_Read()
		{
			var issues = Validate("[virtualdmd]\nleft = 1e999");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "left" && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_A_Decimal_Past_The_Integer_Limit()
		{
			var issues = Validate("[virtualdmd]\nleft = 3000000000");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "left" && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_A_Color_That_Is_Not_Hex()
		{
			var issues = Validate("[virtualdmd]\nstyle.mine.tint = red");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "style.mine.tint" && i.Line == 2);
		}

		[TestCase]
		public void Should_Warn_About_A_Comma_In_A_Number()
		{
			var issues = Validate(@"
				[virtualdmd]
				left = 0,85");

			issues.Should().ContainSingle().Which.Message.Should().Contain("read as 85");
		}

		[TestCase]
		public void Should_Suggest_A_Key_For_A_Typo()
		{
			var issues = Validate(@"
				[virtualdmd]
				widht = 10");

			issues.Should().ContainSingle().Which.Message.Should().Contain("Did you mean \"width\"?");
		}

		[TestCase]
		public void Should_Report_A_Key_In_The_Wrong_Case()
		{
			var issues = Validate(@"
				[virtualdmd]
				Width = 10");

			issues.Should().ContainSingle().Which.Message.Should().Contain("Keys are case-sensitive");
		}

		[TestCase]
		public void Should_Report_A_Section_In_The_Wrong_Case()
		{
			var issues = Validate(@"
				[Global]
				resize = fit");

			issues.Should().ContainSingle().Which.Message.Should().Contain("Did you mean [global]?");
		}

		[TestCase]
		public void Should_Report_A_Repeated_Key()
		{
			var issues = Validate(@"
				[global]
				resize = fit
				resize = fill");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Key == "resize" && i.Line == 4);
		}

		[TestCase]
		public void Should_Report_A_Repeated_Section()
		{
			var issues = Validate(@"
				[global]
				resize = fit
				[global]
				colorize = true");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Section == "global" && i.Key == null && i.Line == 4);
		}

		[TestCase]
		public void Should_Report_A_Section_Header_Without_A_Closing_Bracket()
		{
			var issues = Validate("[global]\n[broken");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_A_Line_Without_An_Equals_Sign()
		{
			var issues = Validate("[global]\nresize");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_A_Comment_After_A_Section_Header()
		{
			var issues = Validate("[global]\n[global] ; comment");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Line == 2);
		}

		[TestCase]
		public void Should_Report_A_Key_Before_Any_Section()
		{
			var issues = Validate(@"
				resize = fit
				[global]");

			issues.Should().ContainSingle().Which.Line.Should().Be(2);
		}

		[TestCase]
		public void Should_Report_A_Hash_Line()
		{
			var issues = Validate(@"
				[global]
				# resize = fit");

			issues.Should().ContainSingle().Which.Message.Should().Contain("isn't a comment");
		}

		[TestCase]
		public void Should_Accept_A_Game_Section()
		{
			var issues = Validate(@"
				[afm_113b]
				resize = fill
				virtualdmd left = 10
				virtualdmd style = mine
				[virtualdmd]
				style.mine.dotsize = 0.9");

			issues.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Suggest_A_Section_For_A_Typo()
		{
			var issues = Validate(@"
				[virtualdmdd]
				enabled = true");

			issues.Should().ContainSingle().Which.Message.Should().Contain("Did you mean [virtualdmd]?");
		}

		[TestCase]
		public void Should_Report_A_Style_Property_In_A_Game_Section()
		{
			var issues = Validate(@"
				[afm_113b]
				virtualdmd style.mine.dotsize = 1");

			issues.Should().ContainSingle().Which.Message.Should().Contain("can't override a style property");
		}

		[TestCase]
		public void Should_Report_A_Missing_Style()
		{
			var issues = Validate(@"
				[virtualdmd]
				style = nope");

			issues.Should().ContainSingle().Which.Message.Should().Contain("No style named \"nope\"");
		}

		[TestCase]
		public void Should_Report_A_Plugin_After_A_Gap()
		{
			var issues = Validate(@"
				[global]
				plugin.0.path64 = a.dll
				plugin.2.path64 = c.dll");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Key == "plugin.2.path64" && i.Message.Contains("plugin.1.path64"));
		}

		[TestCase]
		public void Should_Report_Plugin_Settings_Without_A_Path()
		{
			var issues = Validate(@"
				[global]
				plugin.0.passthrough = true");

			issues.Should().ContainSingle().Which.Key.Should().Be("plugin.0.passthrough");
		}

		[TestCase]
		public void Should_Report_A_Plugin_Past_Nine()
		{
			var issues = Validate(@"
				[global]
				plugin.10.path64 = a.dll");

			issues.Should().Contain(i => i.Key == "plugin.10.path64" && i.Message.Contains("plugin.0 to plugin.9"));
		}

		[TestCase]
		public void Should_Report_An_Alphanumeric_Style_Without_A_Property()
		{
			var issues = Validate("[alphanumeric]\nstyle.x = 1");

			issues.Should().ContainSingle().Which.Severity.Should().Be(IniSeverity.Error);
		}

		[TestCase]
		public void Should_Report_An_Alphanumeric_Style_Layer_Without_A_Property()
		{
			var issues = Validate("[alphanumeric]\nstyle.x.foreground = 1");

			issues.Should().ContainSingle().Which.Severity.Should().Be(IniSeverity.Error);
		}

		[TestCase]
		public void Should_Report_An_Unused_Key()
		{
			var issues = Validate(@"
				[pindmd1]
				scaletohd = true");

			issues.Should().ContainSingle().Which.Message.Should().Contain("never uses it");
		}

		private static IReadOnlyList<IniIssue> Validate(string ini)
		{
			return new IniValidator(Sections).Validate(ini.Split('\n').Select(l => l.Trim()).ToList());
		}
	}
}
