using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.DmdDevice;
using LibDmd.Frame;
using LibDmd.Output.DeviceNeutral;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class DeviceNeutralConfigTests : TestBase
	{
		private static readonly string[] CompleteSection = {
			"[deviceneutral]",
			"enabled = true",
			"port = COM4",
			"baudrate = 921600",
			"layout = startmarker length type panel content",
			"startmarker = 44 4E 44 50",
			"length = u32le",
			"panel = 0",
			"messages = size clear gray2 gray4 gray8 rgb24",
			"type.size = 01",
			"type.clear = 02",
			"type.gray2 = 80",
			"type.gray4 = 81",
			"type.gray8 = 82",
			"type.rgb24 = 83",
			"colororder = rgb",
			"fixedsize = none",
			"connect = none",
		};

		[TestCase]
		public void Should_Be_Disabled_By_Default()
		{
			var config = ReadDestination("[deviceneutral]");

			config.Enabled.Should().BeFalse();
		}

		[TestCase]
		public void Should_Accept_A_Complete_Section()
		{
			var config = ReadCompleteSectionWith();

			config.Validate().Should().BeEmpty();
			config.Port.Should().BeOfType<DeviceNeutralNamedSerialPort>();
			config.Port.Description.Should().Be("COM4");
			config.BaudRate.Should().Be(921600);
			config.Layout.Should().Equal(
				DeviceNeutralMessageField.StartMarker, DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Content);
			config.StartMarker.Should().Equal(0x44, 0x4E, 0x44, 0x50);
			config.Length.Should().Be(DeviceNeutralLengthFormat.UInt32LittleEndian);
			config.Panel.Should().Be(0);
			config.Messages.Should().Equal(
				DeviceNeutralMessageType.Size, DeviceNeutralMessageType.Clear, DeviceNeutralMessageType.Gray2, DeviceNeutralMessageType.Gray4, DeviceNeutralMessageType.Gray8, DeviceNeutralMessageType.Rgb24);
			config.TypeBytes.Should().BeEquivalentTo(new Dictionary<DeviceNeutralMessageType, byte> {
				{ DeviceNeutralMessageType.Size, 0x01 }, { DeviceNeutralMessageType.Clear, 0x02 }, { DeviceNeutralMessageType.Gray2, 0x80 },
				{ DeviceNeutralMessageType.Gray4, 0x81 }, { DeviceNeutralMessageType.Gray8, 0x82 }, { DeviceNeutralMessageType.Rgb24, 0x83 }
			});
			config.ColorOrder.Should().Be(ColorMatrix.Rgb);
			config.FixedSize.Should().Be(Dimensions.Dynamic);
			config.Connect.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Read_Pixelcade_Parameters()
		{
			var config = ReadDestination(@"
				[deviceneutral]
				port = COM3
				baudrate = 1000000
				layout = startmarker length type content endmarker
				startmarker = FE FE
				length = u16le
				endmarker = AA
				messages = rgb24
				type.rgb24 = 40
				colororder = rbg
				fixedsize = 128x32
				connect = EF FE FE 02 00 2E 14 AA");

			config.Validate().Should().BeEmpty();
			config.Layout.Should().Equal(
				DeviceNeutralMessageField.StartMarker, DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Content, DeviceNeutralMessageField.EndMarker);
			config.StartMarker.Should().Equal(0xFE, 0xFE);
			config.Length.Should().Be(DeviceNeutralLengthFormat.UInt16LittleEndian);
			config.EndMarker.Should().Equal(0xAA);
			config.Messages.Should().Equal(DeviceNeutralMessageType.Rgb24);
			config.TypeBytes.Should().BeEquivalentTo(new Dictionary<DeviceNeutralMessageType, byte> { { DeviceNeutralMessageType.Rgb24, 0x40 } });
			config.ColorOrder.Should().Be(ColorMatrix.Rbg);
			config.FixedSize.Should().Be(new Dimensions(128, 32));
			config.Connect.Should().Equal(0xEF, 0xFE, 0xFE, 0x02, 0x00, 0x2E, 0x14, 0xAA);
		}

		[TestCase]
		public void Should_Require_A_Port()
		{
			var config = ReadCompleteSectionWith("port");

			config.Validate().Should().Contain(e => e.Contains("\"port\""));
		}

		[TestCase]
		public void Should_Read_A_Port_By_Usb_Id()
		{
			var config = ReadCompleteSectionWith("port = usb:2E8A:000A");

			config.Validate().Should().BeEmpty();
			config.Port.Should().BeOfType<DeviceNeutralUsbSerialPort>();
			config.Port.Description.Should().Be("usb:2E8A:000A");
		}

		[TestCase]
		public void Should_Reject_An_Invalid_Usb_Id()
		{
			var config = ReadCompleteSectionWith("port = usb:2E8A");

			config.Validate().Should().Contain(e => e.Contains("\"port\""));
		}

		[TestCase]
		public void Should_Require_A_Baud_Rate()
		{
			var config = ReadCompleteSectionWith("baudrate");

			config.Validate().Should().Contain(e => e.Contains("\"baudrate\""));
		}

		[TestCase]
		public void Should_Require_A_Layout()
		{
			var config = ReadCompleteSectionWith("layout");

			config.Validate().Should().Contain(e => e.Contains("\"layout\""));
		}

		[TestCase]
		public void Should_Require_A_Start_Marker()
		{
			var config = ReadCompleteSectionWith("startmarker");

			config.Validate().Should().Contain(e => e.Contains("\"startmarker\""));
		}

		[TestCase]
		public void Should_Require_A_Length_Format()
		{
			var config = ReadCompleteSectionWith("length");

			config.Validate().Should().Contain(e => e.Contains("\"length\""));
		}

		[TestCase]
		public void Should_Require_A_Panel()
		{
			var config = ReadCompleteSectionWith("panel");

			config.Validate().Should().Contain(e => e.Contains("\"panel\""));
		}

		[TestCase]
		public void Should_Require_Messages()
		{
			var config = ReadCompleteSectionWith("messages");

			config.Validate().Should().Contain(e => e.Contains("\"messages\""));
		}

		[TestCase]
		public void Should_Require_A_Type_Byte_For_Each_Message()
		{
			var config = ReadCompleteSectionWith("type.gray4");

			config.Validate().Should().Contain(e => e.Contains("\"type.gray4\""));
		}

		[TestCase]
		public void Should_Require_A_Color_Order()
		{
			var config = ReadCompleteSectionWith("colororder");

			config.Validate().Should().Contain(e => e.Contains("\"colororder\""));
		}

		[TestCase]
		public void Should_Require_A_Fixed_Size()
		{
			var config = ReadCompleteSectionWith("fixedsize");

			config.Validate().Should().Contain(e => e.Contains("\"fixedsize\""));
		}

		[TestCase]
		public void Should_Require_Connect()
		{
			var config = ReadCompleteSectionWith("connect");

			config.Validate().Should().Contain(e => e.Contains("\"connect\""));
		}

		[TestCase]
		public void Should_Read_Pipe_Instead_Of_Port()
		{
			var config = ReadCompleteSectionWith("port", "baudrate", "pipe = deviceneutral");

			config.Validate().Should().BeEmpty();
			config.Pipe.Should().Be("deviceneutral");
			config.Invoking(c => c.Port).Should().Throw<InvalidOperationException>();
		}

		[TestCase]
		public void Should_Report_Both_Port_And_Pipe()
		{
			var config = ReadCompleteSectionWith("pipe = deviceneutral");

			config.Validate().Should().ContainSingle().Which.Should().Contain("\"pipe\"");
		}

		[TestCase]
		public void Should_Report_A_Baud_Rate_With_A_Pipe()
		{
			var config = ReadCompleteSectionWith("port", "pipe = deviceneutral");

			config.Validate().Should().ContainSingle().Which.Should().Contain("\"baudrate\"");
		}

		[TestCase]
		public void Should_Treat_An_Empty_Value_As_Missing()
		{
			var config = ReadCompleteSectionWith("startmarker =");

			config.Validate().Should().Contain(e => e.Contains("\"startmarker\""));
		}

		[TestCase]
		public void Should_Report_A_Baud_Rate_That_Is_Not_A_Number()
		{
			var config = ReadCompleteSectionWith("baudrate = fast");

			config.Validate().Should().Contain(e => e.Contains("\"baudrate\""));
		}

		[TestCase]
		public void Should_Report_A_Baud_Rate_Of_Zero()
		{
			var config = ReadCompleteSectionWith("baudrate = 0");

			config.Validate().Should().Contain(e => e.Contains("\"baudrate\""));
		}

		[TestCase]
		public void Should_Report_A_Field_Listed_Twice_In_The_Layout()
		{
			var config = ReadCompleteSectionWith("layout = type type content");

			config.Validate().Should().Contain(e => e.Contains("\"layout\""));
		}

		[TestCase]
		public void Should_Report_A_Layout_Without_Content()
		{
			var config = ReadCompleteSectionWith("layout = type panel");

			config.Validate().Should().Contain(e => e.Contains("\"layout\""));
		}

		[TestCase]
		public void Should_Report_An_Unknown_Layout_Field()
		{
			var config = ReadCompleteSectionWith("layout = type checksum content");

			config.Validate().Should().Contain(e => e.Contains("\"layout\""));
		}

		[TestCase]
		public void Should_Report_A_Start_Marker_That_Is_Not_Hex()
		{
			var config = ReadCompleteSectionWith("startmarker = DNDP");

			config.Validate().Should().Contain(e => e.Contains("\"startmarker\""));
		}

		[TestCase]
		public void Should_Report_An_Unknown_Length_Format()
		{
			var config = ReadCompleteSectionWith("length = u64le");

			config.Validate().Should().Contain(e => e.Contains("\"length\""));
		}

		[TestCase]
		public void Should_Report_A_Panel_Past_255()
		{
			var config = ReadCompleteSectionWith("panel = 256");

			config.Validate().Should().Contain(e => e.Contains("\"panel\""));
		}

		[TestCase]
		public void Should_Report_A_Message_Listed_Twice()
		{
			var config = ReadCompleteSectionWith("messages = gray4 gray4");

			config.Validate().Should().Contain(e => e.Contains("\"messages\""));
		}

		[TestCase]
		public void Should_Report_Messages_Without_A_Frame_Message()
		{
			var config = ReadCompleteSectionWith("messages = size clear");

			config.Validate().Should().Contain(e => e.Contains("\"messages\""));
		}

		[TestCase]
		public void Should_Report_An_Unknown_Message()
		{
			var config = ReadCompleteSectionWith("messages = gray4 mono");

			config.Validate().Should().Contain(e => e.Contains("\"messages\""));
		}

		[TestCase]
		public void Should_Report_A_Type_Byte_Longer_Than_One_Byte()
		{
			var config = ReadCompleteSectionWith("type.rgb24 = 40 41");

			config.Validate().Should().Contain(e => e.Contains("\"type.rgb24\""));
		}

		[TestCase]
		public void Should_Report_An_Unknown_Color_Order()
		{
			var config = ReadCompleteSectionWith("colororder = bgr");

			config.Validate().Should().Contain(e => e.Contains("\"colororder\""));
		}

		[TestCase]
		public void Should_Report_A_Fixed_Size_Without_A_Height()
		{
			var config = ReadCompleteSectionWith("fixedsize = 128");

			config.Validate().Should().Contain(e => e.Contains("\"fixedsize\""));
		}

		[TestCase]
		public void Should_Report_Connect_Bytes_That_Are_Not_Hex()
		{
			var config = ReadCompleteSectionWith("connect = EFG");

			config.Validate().Should().Contain(e => e.Contains("\"connect\""));
		}

		[TestCase]
		public void Should_Report_A_Start_Marker_The_Layout_Does_Not_Include()
		{
			var config = ReadCompleteSectionWith("layout = length type panel content");

			config.Validate().Should().ContainSingle().Which.Should().Contain("\"startmarker\"");
		}

		[TestCase]
		public void Should_Report_A_Length_The_Layout_Does_Not_Include()
		{
			var config = ReadCompleteSectionWith("layout = startmarker type panel content");

			config.Validate().Should().ContainSingle().Which.Should().Contain("\"length\"");
		}

		[TestCase]
		public void Should_Report_A_Panel_The_Layout_Does_Not_Include()
		{
			var config = ReadCompleteSectionWith("layout = startmarker length type content");

			config.Validate().Should().ContainSingle().Which.Should().Contain("\"panel\"");
		}

		[TestCase]
		public void Should_Report_An_End_Marker_The_Layout_Does_Not_Include()
		{
			var config = ReadCompleteSectionWith("endmarker = AA");

			config.Validate().Should().ContainSingle().Which.Should().Contain("\"endmarker\"");
		}

		[TestCase]
		public void Should_Report_A_Type_Byte_For_A_Message_That_Is_Not_Listed()
		{
			var config = ReadCompleteSectionWith("messages = size clear gray2 gray4 gray8");

			config.Validate().Should().Contain(e => e.Contains("\"type.rgb24\""));
		}

		[TestCase]
		public void Should_Report_Equal_Type_Bytes()
		{
			var config = ReadCompleteSectionWith("type.gray2 = 81");

			config.Validate().Should().ContainSingle();
		}

		[TestCase]
		public void Should_Require_Type_In_Layout_For_More_Than_One_Message()
		{
			var config = ReadCompleteSectionWith("layout = startmarker length panel content",
				"type.size", "type.clear", "type.gray2", "type.gray4", "type.gray8", "type.rgb24");

			config.Validate().Should().ContainSingle().Which.Should().Contain("\"messages\"");
		}

		[TestCase]
		public void Should_Accept_One_Message_Without_Type_In_Layout()
		{
			var config = ReadCompleteSectionWith("layout = content", "messages = gray4",
				"startmarker", "length", "panel", "type.size", "type.clear", "type.gray2", "type.gray4", "type.gray8", "type.rgb24", "colororder");

			config.Validate().Should().BeEmpty();
			config.Layout.Should().Equal(DeviceNeutralMessageField.Content);
		}

		[TestCase]
		public void Should_Report_A_Color_Order_Without_Rgb24()
		{
			var config = ReadCompleteSectionWith("messages = size clear gray2 gray4 gray8", "type.rgb24");

			config.Validate().Should().ContainSingle().Which.Should().Contain("\"colororder\"");
		}

		[TestCase]
		public void Should_Read_Fixed_Size()
		{
			var config = ReadCompleteSectionWith("fixedsize = 128x32");

			config.Validate().Should().BeEmpty();
			config.FixedSize.Should().Be(new Dimensions(128, 32));
		}

		[TestCase]
		public void Should_Read_Connect_Bytes()
		{
			var config = ReadCompleteSectionWith("connect = 55 AA 01");

			config.Validate().Should().BeEmpty();
			config.Connect.Should().Equal(0x55, 0xAA, 0x01);
		}

		[TestCase]
		public void Should_Have_No_Destination_Without_A_Section()
		{
			ReadConfiguration("[global]").DeviceNeutralDestinations.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Read_Each_Named_Section_As_Its_Own_Destination()
		{
			var destinations = ReadConfiguration(@"
				[deviceneutral.backbox]
				port = COM4
				[deviceneutral.topper]
				port = COM5
				[deviceneutralx]
				port = COM6").DeviceNeutralDestinations;

			destinations.Select(d => d.Name).Should().Equal("deviceneutral.backbox", "deviceneutral.topper");
			destinations.Select(d => d.Port.Description).Should().Equal("COM4", "COM5");
		}

		// "key = value" sets a key, and a key alone removes it.
		private IDeviceNeutralConfig ReadCompleteSectionWith(params string[] changes)
		{
			var lines = CompleteSection.ToList();
			foreach (var change in changes) {
				var key = change.Split('=')[0].Trim();
				var index = lines.FindIndex(l => l.Split('=')[0].Trim() == key);
				if (!change.Contains("=")) {
					lines.RemoveAt(index);
				} else if (index < 0) {
					lines.Add(change);
				} else {
					lines[index] = change;
				}
			}
			return ReadDestination(string.Join("\n", lines));
		}

		private IDeviceNeutralConfig ReadDestination(string ini)
		{
			return ReadConfiguration(ini).DeviceNeutralDestinations.Single();
		}

		private Configuration ReadConfiguration(string ini)
		{
			return new Configuration(new TestConfigurationSource(string.Join("\n", ini.Split('\n').Select(l => l.Trim()))));
		}
	}
}
