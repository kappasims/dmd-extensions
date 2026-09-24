using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LibDmd.Frame;
using LibDmd.Output.DeviceNeutral;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class DeviceNeutralDestinationTests : TestBase
	{
		private const byte Panel = 7;

		private DeviceNeutralTransportStub _transport;
		private DeviceNeutralDestination _destination;

		[SetUp]
		public void Setup()
		{
			_transport = new DeviceNeutralTransportStub();
		}

		[TearDown]
		public void Teardown()
		{
			_destination?.Dispose();
		}

		[TestCase]
		public void Should_Send_Size_Before_First_Frame()
		{
			Create();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));

			var messages = Messages();
			var frame = messages.FindIndex(IsFrame);
			frame.Should().BeGreaterThan(0);
			SizeBefore(messages, frame).Should().Be(new Dimensions(128, 32));
		}

		[TestCase]
		public void Should_Send_Size_When_Frame_Size_Changes()
		{
			Create();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.RenderGray4(new DmdFrame(128, 16, 4));

			var messages = Messages();
			SizeBefore(messages, messages.FindLastIndex(IsFrame)).Should().Be(new Dimensions(128, 16));
		}

		[TestCase]
		public void Should_Send_Each_Frame_Type()
		{
			Create();

			_destination.RenderGray2(new DmdFrame(4, 1, 2));
			_destination.RenderGray4(new DmdFrame(4, 1, 4));
			_destination.RenderGray8(new DmdFrame(4, 1, 8));
			_destination.RenderRgb24(new DmdFrame(4, 1, 24));

			Messages().Where(IsFrame).Select(m => (DeviceNeutralMessageType)m[0]).Should().Equal(
				DeviceNeutralMessageType.Gray2, DeviceNeutralMessageType.Gray4, DeviceNeutralMessageType.Gray8, DeviceNeutralMessageType.Rgb24);
		}

		[TestCase]
		public void Should_Address_Configured_Panel()
		{
			Create();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.ClearDisplay();

			Messages().Select(m => m[1]).Should().OnlyContain(p => p == Panel);
		}

		[TestCase]
		public void Should_Send_Clear()
		{
			Create();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.ClearDisplay();

			Messages().Last(m => !IsSize(m)).Should().Equal((byte)DeviceNeutralMessageType.Clear, Panel);
		}

		[TestCase]
		public void Should_Not_Write_Without_Receiver()
		{
			_transport.IsReceiverPresent = false;
			Create();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.ClearDisplay();

			_transport.Writes.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Send_Size_Again_After_Reconnecting()
		{
			Create();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			var before = _transport.Writes.Length;
			_transport.FailNextWrite = true;
			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.RenderGray4(new DmdFrame(128, 32, 4));

			var messages = Messages().Skip(before).ToList();
			var frame = messages.FindIndex(IsFrame);
			frame.Should().BeGreaterThan(0);
			messages.Take(frame).Should().Contain(m => m[0] == (byte)DeviceNeutralMessageType.Size);
		}

		private void Create()
		{
			_destination = new DeviceNeutralDestination(_transport, new byte[0], Panel);
		}

		private List<byte[]> Messages()
		{
			return _transport.Writes.Select(w => w.Skip(4).ToArray()).ToList();
		}

		private static bool IsFrame(byte[] message) => (message[0] & 0x80) != 0;

		private static bool IsSize(byte[] message) => message[0] == (byte)DeviceNeutralMessageType.Size;

		private static Dimensions SizeBefore(List<byte[]> messages, int index)
		{
			var size = messages.Take(index).Last(IsSize);
			return new Dimensions(size[2] | size[3] << 8, size[4] | size[5] << 8);
		}
	}
}
