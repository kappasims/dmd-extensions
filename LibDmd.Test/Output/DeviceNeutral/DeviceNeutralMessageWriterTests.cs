using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output.DeviceNeutral;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class DeviceNeutralMessageWriterTests : TestBase
	{
		private static readonly Dictionary<DeviceNeutralMessageType, byte> TypeBytes = new Dictionary<DeviceNeutralMessageType, byte> {
			{ DeviceNeutralMessageType.Size, 0x01 }, { DeviceNeutralMessageType.Clear, 0x02 }, { DeviceNeutralMessageType.Gray2, 0x80 },
			{ DeviceNeutralMessageType.Gray4, 0x81 }, { DeviceNeutralMessageType.Gray8, 0x82 }, { DeviceNeutralMessageType.Rgb24, 0x83 }
		};

		private static readonly DeviceNeutralMessageField[] LengthTypePanelContent = {
			DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Content
		};

		private static readonly DeviceNeutralMessageField[] TypePanelContent = {
			DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Content
		};

		private DeviceNeutralMessageWriter _writer;

		[SetUp]
		public void Setup()
		{
			_writer = new DeviceNeutralMessageWriter(LengthTypePanelContent, new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian, TypeBytes, ColorMatrix.Rgb);
		}

		[TestCase]
		public void Should_Write_Size()
		{
			var writer = CreateWriterWithStartMarker();

			writer.WriteSizeMessage(0, new Dimensions(256, 64)).ToArray().Should().Equal(
				0xAA, 0x55, 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x01, 0x40, 0x00);
		}

		[TestCase]
		public void Should_Write_Clear()
		{
			var writer = CreateWriterWithStartMarker();

			writer.WriteClearMessage(0).ToArray().Should().Equal(
				0xAA, 0x55, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00);
		}

		[TestCase]
		public void Should_Pack_Gray2_With_First_Pixel_In_Top_Bits()
		{
			var frame = FrameGenerator.FromString("32103");

			_writer.WriteFrameMessage(DeviceNeutralMessageType.Gray2, 0, frame).ToArray().Should().Equal(
				0x04, 0x00, 0x00, 0x00, 0x80, 0x00, 0xE4, 0xC0);
		}

		[TestCase]
		public void Should_Pack_Gray4_With_First_Pixel_In_High_Nibble()
		{
			var frame = FrameGenerator.FromString("123");

			_writer.WriteFrameMessage(DeviceNeutralMessageType.Gray4, 0, frame).ToArray().Should().Equal(
				0x04, 0x00, 0x00, 0x00, 0x81, 0x00, 0x12, 0x30);
		}

		[TestCase]
		public void Should_Pack_Across_Rows_Without_Padding()
		{
			var frame = FrameGenerator.FromString(@"
				123
				456");

			_writer.WriteFrameMessage(DeviceNeutralMessageType.Gray4, 0, frame).ToArray().Should().Equal(
				0x05, 0x00, 0x00, 0x00, 0x81, 0x00, 0x12, 0x34, 0x56);
		}

		[TestCase]
		public void Should_Copy_Gray8_Pixels()
		{
			var frame = new DmdFrame(2, 1, new byte[] { 0x00, 0xFF }, 8);

			_writer.WriteFrameMessage(DeviceNeutralMessageType.Gray8, 0, frame).ToArray().Should().Equal(
				0x04, 0x00, 0x00, 0x00, 0x82, 0x00, 0x00, 0xFF);
		}

		[TestCase]
		public void Should_Copy_Rgb24_Pixels()
		{
			var frame = new DmdFrame(1, 1, new byte[] { 0x01, 0x02, 0x03 }, 24);

			_writer.WriteFrameMessage(DeviceNeutralMessageType.Rgb24, 0, frame).ToArray().Should().Equal(
				0x05, 0x00, 0x00, 0x00, 0x83, 0x00, 0x01, 0x02, 0x03);
		}

		[TestCase]
		public void Should_Write_Panel_Index()
		{
			_writer.WriteClearMessage(7).ToArray().Should().Equal(0x02, 0x00, 0x00, 0x00, 0x02, 0x07);
		}

		[TestCase]
		public void Should_Write_Correctly_After_A_Larger_Message()
		{
			_writer.WriteFrameMessage(DeviceNeutralMessageType.Rgb24, 0, new DmdFrame(128, 32, 24));

			_writer.WriteClearMessage(0).ToArray().Should().Equal(0x02, 0x00, 0x00, 0x00, 0x02, 0x00);
		}

		[TestCase]
		public void Should_Write_Fields_In_Layout_Order()
		{
			var writer = new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Content },
				new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian, TypeBytes, ColorMatrix.Rgb);

			writer.WriteClearMessage(3).ToArray().Should().Equal(0x03, 0x02);
		}

		[TestCase]
		public void Should_Count_Length_Up_To_End_Marker()
		{
			var writer = new DeviceNeutralMessageWriter(
				new[] { DeviceNeutralMessageField.StartMarker, DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Content, DeviceNeutralMessageField.EndMarker },
				new byte[] { 0xFE, 0xFE }, new byte[] { 0xAA }, DeviceNeutralLengthFormat.UInt16LittleEndian, TypeBytes, ColorMatrix.Rgb);

			writer.WriteFrameMessage(DeviceNeutralMessageType.Gray8, 0, new DmdFrame(2, 1, new byte[] { 0x01, 0x02 }, 8)).ToArray().Should().Equal(
				0xFE, 0xFE, 0x03, 0x00, 0x82, 0x01, 0x02, 0xAA);
		}

		[TestCase]
		public void Should_Write_Big_Endian_Length()
		{
			var writer = new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Content },
				new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt16BigEndian, TypeBytes, ColorMatrix.Rgb);

			writer.WriteClearMessage(0).ToArray().Should().Equal(0x00, 0x02, 0x02, 0x00);
		}

		[TestCase]
		public void Should_Use_Configured_Type_Bytes()
		{
			var writer = new DeviceNeutralMessageWriter(TypePanelContent, new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian,
				new Dictionary<DeviceNeutralMessageType, byte> { { DeviceNeutralMessageType.Clear, 0x7E } }, ColorMatrix.Rgb);

			writer.WriteClearMessage(0).ToArray().Should().Equal(0x7E, 0x00);
		}

		[TestCase]
		public void Should_Swap_Green_And_Blue_For_Rbg()
		{
			var writer = new DeviceNeutralMessageWriter(TypePanelContent, new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian,
				TypeBytes, ColorMatrix.Rbg);

			writer.WriteFrameMessage(DeviceNeutralMessageType.Rgb24, 0, new DmdFrame(1, 1, new byte[] { 0x01, 0x02, 0x03 }, 24)).ToArray().Should().Equal(
				0x83, 0x00, 0x01, 0x03, 0x02);
		}

		[TestCase]
		public void Should_Match_Pixelcade_V2_Framing()
		{
			var writer = new DeviceNeutralMessageWriter(
				new[] { DeviceNeutralMessageField.StartMarker, DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Content, DeviceNeutralMessageField.EndMarker },
				new byte[] { 0xFE, 0xFE }, new byte[] { 0xAA }, DeviceNeutralLengthFormat.UInt16LittleEndian,
				new Dictionary<DeviceNeutralMessageType, byte> { { DeviceNeutralMessageType.Rgb24, 0x40 } }, ColorMatrix.Rbg);

			var frame = new DmdFrame(2, 1, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }, 24);

			writer.WriteFrameMessage(DeviceNeutralMessageType.Rgb24, 0, frame).ToArray().Should().Equal(
				0xFE, 0xFE, 0x07, 0x00, 0x40, 0x01, 0x03, 0x02, 0x04, 0x06, 0x05, 0xAA);
		}

		[TestCase]
		public void Should_Skip_Message_Too_Long_For_Length_Format()
		{
			var writer = new DeviceNeutralMessageWriter(LengthTypePanelContent, new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt16LittleEndian,
				TypeBytes, ColorMatrix.Rgb);

			writer.WriteFrameMessage(DeviceNeutralMessageType.Rgb24, 0, new DmdFrame(256, 86, 24)).Count.Should().Be(0);
		}

		[TestCase]
		public void Should_Reject_Layout_Without_Content()
		{
			Action act = () => new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel }, new byte[0], new byte[0],
				DeviceNeutralLengthFormat.UInt32LittleEndian, TypeBytes, ColorMatrix.Rgb);

			act.Should().Throw<ArgumentException>();
		}

		[TestCase]
		public void Should_Reject_Message_Type_Without_Type_Byte()
		{
			var writer = new DeviceNeutralMessageWriter(TypePanelContent, new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian,
				new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rgb);

			Action act = () => writer.WriteClearMessage(0);

			act.Should().Throw<KeyNotFoundException>();
		}

		[TestCase]
		public void Should_Write_Only_Content_For_Content_Layout()
		{
			var writer = new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.Content }, new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian,
				new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rgb);

			writer.WriteFrameMessage(DeviceNeutralMessageType.Gray8, 0, new DmdFrame(2, 1, new byte[] { 0x01, 0x02 }, 8)).ToArray().Should().Equal(0x01, 0x02);
		}

		[TestCase]
		public void Should_Reject_Control_Types_As_Frames()
		{
			var frame = FrameGenerator.FromString("123");

			Action act = () => _writer.WriteFrameMessage(DeviceNeutralMessageType.Size, 0, frame);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		private static DeviceNeutralMessageWriter CreateWriterWithStartMarker()
		{
			return new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.StartMarker, DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Content },
				new byte[] { 0xAA, 0x55 }, new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian, TypeBytes, ColorMatrix.Rgb);
		}
	}
}
