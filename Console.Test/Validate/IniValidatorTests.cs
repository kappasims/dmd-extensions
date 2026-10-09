using System.Collections.Generic;
using System.IO;
using System.Linq;
using DmdExt.Validate;
using FluentAssertions;
using LibDmd.DmdDevice;
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

			var lines = sample.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
			var report = new IniValidator(Sections, ValidateCommand.ListDeviceNeutralDestinations("DmdDevice.ini", lines)).Validate(lines);

			report.Issues.Should().BeEmpty();
			report.DisabledSections.Should().ContainSingle().Which.Section.Should().Be("deviceneutral");
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

		[TestCase]
		public void Should_Accept_Device_Neutral_Sections()
		{
			var issues = Validate(@"
				[deviceneutral]
				enabled = true
				port = COM3
				baudrate = 921600
				layout = startmarker length type content
				startmarker = 44 4E 44 50
				length = u32le
				messages = size gray4
				type.size = 01
				type.gray4 = 02
				fixedsize = none
				connect = none
				[DeviceNeutral.Backbox]
				enabled = true
				pipe = backbox
				layout = content
				messages = rgb24
				colororder = rgb
				fixedsize = 128x32
				connect = none
				[afm_113b]
				DeviceNeutral.Backbox enabled = false",
				new TestDeviceNeutralConfig { Enabled = true },
				new TestDeviceNeutralConfig { Name = "DeviceNeutral.Backbox", Enabled = true });

			issues.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Report_A_Problem_In_An_Enabled_Device_Neutral_Section()
		{
			var issues = Validate(@"
				[deviceneutral]
				enabled = true
				pipe = panel
				baudrate = 9600",
				new TestDeviceNeutralConfig { Enabled = true, Problems = new[] { "\"baudrate\" under [deviceneutral] is set, but only applies to \"port\"." } });

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Line == 2 && i.Message.Contains("\"baudrate\""));
		}

		[TestCase]
		public void Should_List_A_Disabled_Device_Neutral_Section_Apart_From_The_Issues()
		{
			var report = ReadReport(@"
				[deviceneutral]
				port =",
				new TestDeviceNeutralConfig { Problems = new[] { "Exactly one of \"port\" and \"pipe\" under [deviceneutral] must be set." } });

			report.Issues.Should().BeEmpty();
			report.DisabledSections.Should().ContainSingle().Which.Should().Match<IniDisabledSection>(s => s.Line == 2 && s.Section == "deviceneutral" && s.Problems.Count == 1);
		}

		[TestCase]
		public void Should_List_A_Disabled_Device_Neutral_Section_That_Is_Ready_To_Enable()
		{
			var report = ReadReport(@"
				[deviceneutral]
				pipe = panel",
				new TestDeviceNeutralConfig());

			report.DisabledSections.Should().ContainSingle().Which.Problems.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Suggest_A_Device_Neutral_Key_For_A_Typo()
		{
			var issues = Validate(@"
				[deviceneutral.backbox]
				mesages = gray4",
				new TestDeviceNeutralConfig { Name = "deviceneutral.backbox" });

			issues.Should().ContainSingle().Which.Message.Should().Contain("Did you mean \"messages\"?");
		}

		[TestCase]
		public void Should_Check_The_Keys_Of_A_Device_Neutral_Section_DmdDevice_Could_Not_Load()
		{
			var issues = Validate(@"
				[deviceneutral]
				enabled = yes");

			issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Key == "enabled");
		}

		[TestCase]
		public void Should_Report_An_Override_Of_A_Device_Neutral_Section_That_Is_Not_There()
		{
			var issues = Validate(@"
				[afm_113b]
				deviceneutral.topper enabled = true");

			issues.Should().ContainSingle().Which.Message.Should().Contain("overrides nothing");
		}

		[TestCase]
		public void Should_Accept_A_Usb_Id_As_A_Device_Neutral_Port()
		{
			var report = ReadReportWithLoadedDestinations(@"
				[deviceneutral]
				enabled = true
				port = usb:2E8A:000A
				baudrate = 921600
				layout = content
				messages = gray4
				fixedsize = none
				connect = none");

			report.Issues.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Report_An_Invalid_Usb_Id_As_A_Device_Neutral_Port()
		{
			var report = ReadReportWithLoadedDestinations(@"
				[deviceneutral]
				enabled = true
				port = usb:2E8A
				baudrate = 921600
				layout = content
				messages = gray4
				fixedsize = none
				connect = none");

			report.Issues.Should().ContainSingle().Which.Should().Match<IniIssue>(i => i.Severity == IniSeverity.Error && i.Line == 2 && i.Message.Contains("usb:2E8A"));
		}

		private static IReadOnlyList<IniIssue> Validate(string ini, params IDeviceNeutralConfig[] deviceNeutralDestinations)
		{
			return ReadReport(ini, deviceNeutralDestinations).Issues;
		}

		private static IniReport ReadReport(string ini, params IDeviceNeutralConfig[] deviceNeutralDestinations)
		{
			return new IniValidator(Sections, deviceNeutralDestinations).Validate(ini.Split('\n').Select(l => l.Trim()).ToList());
		}

		private static IniReport ReadReportWithLoadedDestinations(string ini)
		{
			var lines = ini.Split('\n').Select(l => l.Trim()).ToList();
			return new IniValidator(Sections, ValidateCommand.ListDeviceNeutralDestinations("DmdDevice.ini", lines)).Validate(lines);
		}
	}
}
