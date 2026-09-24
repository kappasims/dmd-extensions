using System.IO;
using System.Linq;
using FluentAssertions;
using LibDmd.DmdDevice;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class DeviceNeutralConfigTests : TestBase
	{
		private string _iniPath;

		[SetUp]
		public void Setup()
		{
			_iniPath = Path.GetTempFileName();
		}

		[TearDown]
		public void Teardown()
		{
			File.Delete(_iniPath);
		}

		[TestCase]
		public void Should_Be_Disabled_By_Default()
		{
			var config = FromIni("[deviceneutral]");

			config.Enabled.Should().BeFalse();
			config.BaudRate.Should().Be(921600);
			config.Panel.Should().Be(0);
		}

		[TestCase]
		public void Should_Default_Start_Marker_To_Dndp()
		{
			var config = FromIni("[deviceneutral]");

			config.StartMarker.Should().Equal(0x44, 0x4E, 0x44, 0x50);
		}

		[TestCase]
		public void Should_Read_Start_Marker_As_Hex()
		{
			var config = FromIni(@"
				[deviceneutral]
				startmarker = FE fe 0A");

			config.StartMarker.Should().Equal(0xFE, 0xFE, 0x0A);
		}

		[TestCase]
		public void Should_Allow_An_Empty_Start_Marker()
		{
			var config = FromIni(@"
				[deviceneutral]
				startmarker =");

			config.StartMarker.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Fall_Back_To_Dndp_On_Invalid_Start_Marker()
		{
			var config = FromIni(@"
				[deviceneutral]
				startmarker = DNDP");

			config.StartMarker.Should().Equal(0x44, 0x4E, 0x44, 0x50);
		}

		[TestCase]
		public void Should_Have_No_Destination_Without_A_Section()
		{
			ConfigurationFromIni("[global]").DeviceNeutralDestinations.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Read_Each_Named_Section_As_Its_Own_Destination()
		{
			var destinations = ConfigurationFromIni(@"
				[deviceneutral.backbox]
				port = COM4
				[deviceneutral.topper]
				port = COM5
				[deviceneutralx]
				port = COM6").DeviceNeutralDestinations;

			destinations.Select(d => d.Name).Should().Equal("deviceneutral.backbox", "deviceneutral.topper");
			destinations.Select(d => d.Port).Should().Equal("COM4", "COM5");
		}

		private IDeviceNeutralConfig FromIni(string ini)
		{
			return ConfigurationFromIni(ini).DeviceNeutralDestinations.Single();
		}

		private Configuration ConfigurationFromIni(string ini)
		{
			File.WriteAllLines(_iniPath, ini.Split('\n').Select(l => l.Trim()));
			return new Configuration(_iniPath);
		}
	}
}
