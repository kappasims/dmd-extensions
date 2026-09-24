using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using LibDmd.Output.DeviceNeutral;
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
		public void Should_Default_To_Documented_Format()
		{
			var config = FromIni("[deviceneutral]");

			config.Layout.Should().Equal(DeviceNeutralMessageWriter.DefaultLayout);
			config.EndMarker.Should().BeEmpty();
			config.Length.Should().Be(DeviceNeutralLengthFormat.UInt32LittleEndian);
			config.TypeBytes.Should().BeEmpty();
			config.FixedSize.Should().Be(Dimensions.Dynamic);
			config.ColorOrder.Should().Be(ColorMatrix.Rgb);
			config.Connect.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Read_Pixelcade_Parameters()
		{
			var config = FromIni(@"
				[deviceneutral]
				layout = startmarker length type content endmarker
				startmarker = FE FE
				length = u16le
				endmarker = AA
				type.rgb24 = 40
				fixedsize = 128x32
				colororder = rbg
				connect = EF FE FE 02 00 2E 14 AA");

			config.Layout.Should().Equal(
				DeviceNeutralMessageField.StartMarker, DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Content, DeviceNeutralMessageField.EndMarker);
			config.StartMarker.Should().Equal(0xFE, 0xFE);
			config.Length.Should().Be(DeviceNeutralLengthFormat.UInt16LittleEndian);
			config.EndMarker.Should().Equal(0xAA);
			config.TypeBytes.Should().BeEquivalentTo(new Dictionary<DeviceNeutralMessageType, byte> { { DeviceNeutralMessageType.Rgb24, 0x40 } });
			config.FixedSize.Should().Be(new Dimensions(128, 32));
			config.ColorOrder.Should().Be(ColorMatrix.Rbg);
			config.Connect.Should().Equal(0xEF, 0xFE, 0xFE, 0x02, 0x00, 0x2E, 0x14, 0xAA);
		}

		[TestCase("type type content")]
		[TestCase("type panel")]
		[TestCase("type checksum content")]
		public void Should_Fall_Back_On_Invalid_Layout(string layout)
		{
			var config = FromIni($@"
				[deviceneutral]
				layout = {layout}");

			config.Layout.Should().Equal(DeviceNeutralMessageWriter.DefaultLayout);
		}

		[TestCase]
		public void Should_Fall_Back_On_Invalid_Length()
		{
			var config = FromIni(@"
				[deviceneutral]
				length = u64le");

			config.Length.Should().Be(DeviceNeutralLengthFormat.UInt32LittleEndian);
		}

		[TestCase]
		public void Should_Ignore_Type_Byte_That_Is_Not_One_Byte()
		{
			var config = FromIni(@"
				[deviceneutral]
				type.rgb24 = 40 41");

			config.TypeBytes.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Fall_Back_On_Invalid_Fixed_Size()
		{
			var config = FromIni(@"
				[deviceneutral]
				fixedsize = 128");

			config.FixedSize.Should().Be(Dimensions.Dynamic);
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
