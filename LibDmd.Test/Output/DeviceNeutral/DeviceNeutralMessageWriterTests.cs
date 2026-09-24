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
		private DeviceNeutralMessageWriter _writer;

		[SetUp]
		public void Setup()
		{
			_writer = new DeviceNeutralMessageWriter(new byte[0]);
		}

		[TestCase]
		public void Should_Write_Size()
		{
			var writer = new DeviceNeutralMessageWriter(DeviceNeutralMessageWriter.DefaultStartMarker);

			writer.Size(0, new Dimensions(256, 64)).ToArray().Should().Equal(
				0x44, 0x4E, 0x44, 0x50, 0x06, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x01, 0x40, 0x00);
		}

		[TestCase]
		public void Should_Write_Clear()
		{
			var writer = new DeviceNeutralMessageWriter(DeviceNeutralMessageWriter.DefaultStartMarker);

			writer.Clear(0).ToArray().Should().Equal(
				0x44, 0x4E, 0x44, 0x50, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00);
		}

		[TestCase]
		public void Should_Pack_Gray2_With_First_Pixel_In_Top_Bits()
		{
			var frame = FrameGenerator.FromString("32103");

			_writer.Frame(DeviceNeutralMessageType.Gray2, 0, frame).ToArray().Should().Equal(
				0x04, 0x00, 0x00, 0x00, 0x80, 0x00, 0xE4, 0xC0);
		}

		[TestCase]
		public void Should_Pack_Gray4_With_First_Pixel_In_High_Nibble()
		{
			var frame = FrameGenerator.FromString("123");

			_writer.Frame(DeviceNeutralMessageType.Gray4, 0, frame).ToArray().Should().Equal(
				0x04, 0x00, 0x00, 0x00, 0x81, 0x00, 0x12, 0x30);
		}

		[TestCase]
		public void Should_Pack_Across_Rows_Without_Padding()
		{
			var frame = FrameGenerator.FromString(@"
				123
				456");

			_writer.Frame(DeviceNeutralMessageType.Gray4, 0, frame).ToArray().Should().Equal(
				0x05, 0x00, 0x00, 0x00, 0x81, 0x00, 0x12, 0x34, 0x56);
		}

		[TestCase]
		public void Should_Copy_Gray8_Pixels()
		{
			var frame = new DmdFrame(2, 1, new byte[] { 0x00, 0xFF }, 8);

			_writer.Frame(DeviceNeutralMessageType.Gray8, 0, frame).ToArray().Should().Equal(
				0x04, 0x00, 0x00, 0x00, 0x82, 0x00, 0x00, 0xFF);
		}

		[TestCase]
		public void Should_Copy_Rgb24_Pixels()
		{
			var frame = new DmdFrame(1, 1, new byte[] { 0x01, 0x02, 0x03 }, 24);

			_writer.Frame(DeviceNeutralMessageType.Rgb24, 0, frame).ToArray().Should().Equal(
				0x05, 0x00, 0x00, 0x00, 0x83, 0x00, 0x01, 0x02, 0x03);
		}

		[TestCase]
		public void Should_Write_Panel_Index()
		{
			_writer.Clear(7).ToArray().Should().Equal(0x02, 0x00, 0x00, 0x00, 0x02, 0x07);
		}

		[TestCase]
		public void Should_Write_Correctly_After_A_Larger_Message()
		{
			_writer.Frame(DeviceNeutralMessageType.Rgb24, 0, new DmdFrame(128, 32, 24));

			_writer.Clear(0).ToArray().Should().Equal(0x02, 0x00, 0x00, 0x00, 0x02, 0x00);
		}

		[TestCase]
		public void Should_Write_Fields_In_Layout_Order()
		{
			var writer = new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Content },
				new byte[0], new byte[0], DeviceNeutralLengthFormat.None, new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rgb);

			writer.Clear(3).ToArray().Should().Equal(0x03, 0x02);
		}

		[TestCase]
		public void Should_Count_Length_Up_To_End_Marker()
		{
			var writer = new DeviceNeutralMessageWriter(
				new[] { DeviceNeutralMessageField.StartMarker, DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Content, DeviceNeutralMessageField.EndMarker },
				new byte[] { 0xFE, 0xFE }, new byte[] { 0xAA }, DeviceNeutralLengthFormat.UInt16LittleEndian, new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rgb);

			writer.Frame(DeviceNeutralMessageType.Gray8, 0, new DmdFrame(2, 1, new byte[] { 0x01, 0x02 }, 8)).ToArray().Should().Equal(
				0xFE, 0xFE, 0x03, 0x00, 0x82, 0x01, 0x02, 0xAA);
		}

		[TestCase]
		public void Should_Write_Big_Endian_Length()
		{
			var writer = new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Content },
				new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt16BigEndian, new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rgb);

			writer.Clear(0).ToArray().Should().Equal(0x00, 0x02, 0x02, 0x00);
		}

		[TestCase]
		public void Should_Use_Configured_Type_Bytes()
		{
			var writer = new DeviceNeutralMessageWriter(DeviceNeutralMessageWriter.DefaultLayout, new byte[0], new byte[0], DeviceNeutralLengthFormat.None,
				new Dictionary<DeviceNeutralMessageType, byte> { { DeviceNeutralMessageType.Clear, 0x7E } }, ColorMatrix.Rgb);

			writer.Clear(0).ToArray().Should().Equal(0x7E, 0x00);
		}

		[TestCase]
		public void Should_Swap_Green_And_Blue_For_Rbg()
		{
			var writer = new DeviceNeutralMessageWriter(DeviceNeutralMessageWriter.DefaultLayout, new byte[0], new byte[0], DeviceNeutralLengthFormat.None,
				new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rbg);

			writer.Frame(DeviceNeutralMessageType.Rgb24, 0, new DmdFrame(1, 1, new byte[] { 0x01, 0x02, 0x03 }, 24)).ToArray().Should().Equal(
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

			writer.Frame(DeviceNeutralMessageType.Rgb24, 0, frame).ToArray().Should().Equal(
				0xFE, 0xFE, 0x07, 0x00, 0x40, 0x01, 0x03, 0x02, 0x04, 0x06, 0x05, 0xAA);
		}

		[TestCase]
		public void Should_Skip_Message_Too_Long_For_Length_Format()
		{
			var writer = new DeviceNeutralMessageWriter(DeviceNeutralMessageWriter.DefaultLayout, new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt16LittleEndian,
				new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rgb);

			writer.Frame(DeviceNeutralMessageType.Rgb24, 0, new DmdFrame(256, 86, 24)).Count.Should().Be(0);
		}

		[TestCase]
		public void Should_Reject_Layout_Without_Content()
		{
			Action act = () => new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel }, new byte[0], new byte[0],
				DeviceNeutralLengthFormat.None, new Dictionary<DeviceNeutralMessageType, byte>(), ColorMatrix.Rgb);

			act.Should().Throw<ArgumentException>();
		}

		[TestCase]
		public void Should_Reject_Control_Types_As_Frames()
		{
			var frame = FrameGenerator.FromString("123");

			Action act = () => _writer.Frame(DeviceNeutralMessageType.Size, 0, frame);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}
	}
}
