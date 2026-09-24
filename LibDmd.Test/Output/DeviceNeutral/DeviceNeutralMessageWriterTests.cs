using System;
using System.Linq;
using FluentAssertions;
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
		public void Should_Reject_Control_Types_As_Frames()
		{
			var frame = FrameGenerator.FromString("123");

			Action act = () => _writer.Frame(DeviceNeutralMessageType.Size, 0, frame);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}
	}
}
