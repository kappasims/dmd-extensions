using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Frame;
using LibDmd.Output;
using LibDmd.Output.DeviceNeutral;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class DeviceNeutralDestinationTests : TestBase
	{
		private const byte Panel = 7;

		private static readonly Dictionary<DeviceNeutralMessageType, byte> TypeBytes = new Dictionary<DeviceNeutralMessageType, byte> {
			{ DeviceNeutralMessageType.Size, 0x01 }, { DeviceNeutralMessageType.Clear, 0x02 }, { DeviceNeutralMessageType.Gray2, 0x80 },
			{ DeviceNeutralMessageType.Gray4, 0x81 }, { DeviceNeutralMessageType.Gray8, 0x82 }, { DeviceNeutralMessageType.Rgb24, 0x83 }
		};

		private static readonly DeviceNeutralMessageType[] AllMessages = TypeBytes.Keys.ToArray();

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
			CreateDestination();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));

			var messages = ReadWrittenMessages();
			var frame = messages.FindIndex(IsFrame);
			frame.Should().BeGreaterThan(0);
			FindSizeBefore(messages, frame).Should().Be(new Dimensions(128, 32));
		}

		[TestCase]
		public void Should_Send_Size_When_Frame_Size_Changes()
		{
			CreateDestination();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.RenderGray4(new DmdFrame(128, 16, 4));

			var messages = ReadWrittenMessages();
			FindSizeBefore(messages, messages.FindLastIndex(IsFrame)).Should().Be(new Dimensions(128, 16));
		}

		[TestCase]
		public void Should_Send_Each_Frame_Type()
		{
			CreateDestination();

			_destination.RenderGray2(new DmdFrame(4, 1, 2));
			_destination.RenderGray4(new DmdFrame(4, 1, 4));
			_destination.RenderGray8(new DmdFrame(4, 1, 8));
			_destination.RenderRgb24(new DmdFrame(4, 1, 24));

			ReadWrittenMessages().Where(IsFrame).Select(m => m[0]).Should().Equal(0x80, 0x81, 0x82, 0x83);
		}

		[TestCase]
		public void Should_Address_Configured_Panel()
		{
			CreateDestination();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.ClearDisplay();

			ReadWrittenMessages().Select(m => m[1]).Should().OnlyContain(p => p == Panel);
		}

		[TestCase]
		public void Should_Send_Clear()
		{
			CreateDestination();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.ClearDisplay();

			ReadWrittenMessages().Last(m => !IsSize(m)).Should().Equal(TypeBytes[DeviceNeutralMessageType.Clear], Panel);
		}

		[TestCase]
		public void Should_Drop_Frames_Of_Types_That_Are_Not_Listed()
		{
			CreateDestination(DeviceNeutralMessageType.Size, DeviceNeutralMessageType.Gray4);

			_destination.RenderRgb24(new DmdFrame(128, 32, 24));
			_destination.RenderGray4(new DmdFrame(128, 32, 4));

			ReadWrittenMessages().Where(IsFrame).Select(m => m[0]).Should().Equal(TypeBytes[DeviceNeutralMessageType.Gray4]);
		}

		[TestCase]
		public void Should_Accept_Only_Listed_Frame_Formats()
		{
			CreateDestination(DeviceNeutralMessageType.Size, DeviceNeutralMessageType.Rgb24);

			_destination.Accepts(FrameFormat.Gray2).Should().BeFalse();
			_destination.Accepts(FrameFormat.Gray4).Should().BeFalse();
			_destination.Accepts(FrameFormat.Gray8).Should().BeFalse();
			_destination.Accepts(FrameFormat.Rgb24).Should().BeTrue();
		}

		[TestCase]
		public void Should_Not_Send_Size_Unless_Listed()
		{
			CreateDestination(DeviceNeutralMessageType.Gray4);

			_destination.RenderGray4(new DmdFrame(128, 32, 4));

			ReadWrittenMessages().Should().ContainSingle().Which[0].Should().Be(TypeBytes[DeviceNeutralMessageType.Gray4]);
		}

		[TestCase]
		public void Should_Not_Send_Clear_Unless_Listed()
		{
			CreateDestination(DeviceNeutralMessageType.Size, DeviceNeutralMessageType.Gray4);

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.ClearDisplay();

			ReadWrittenMessages().Should().NotContain(m => m[0] == TypeBytes[DeviceNeutralMessageType.Clear]);
		}

		[TestCase]
		public void Should_Not_Write_Without_Receiver()
		{
			_transport.IsReceiverPresent = false;
			CreateDestination();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.ClearDisplay();

			_transport.Writes.Should().BeEmpty();
		}

		[TestCase]
		public void Should_Send_Size_Again_After_Reconnecting()
		{
			CreateDestination();

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			var before = _transport.Writes.Length;
			_transport.FailNextWrite = true;
			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.RenderGray4(new DmdFrame(128, 32, 4));

			var messages = ReadWrittenMessages().Skip(before).ToList();
			var frame = messages.FindIndex(IsFrame);
			frame.Should().BeGreaterThan(0);
			messages.Take(frame).Should().Contain(m => m[0] == TypeBytes[DeviceNeutralMessageType.Size]);
		}

		[TestCase]
		public void Should_Write_Connect_Bytes_First()
		{
			_destination = new DeviceNeutralDestination(_transport, CreateWriter(), Panel, new byte[] { 0xEF }, AllMessages);

			_destination.RenderGray4(new DmdFrame(128, 32, 4));

			_transport.Writes.First().Should().Equal(0xEF);
		}

		[TestCase]
		public void Should_Write_Connect_Bytes_Again_After_Reconnecting()
		{
			_destination = new DeviceNeutralDestination(_transport, CreateWriter(), Panel, new byte[] { 0xEF }, AllMessages);

			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			var before = _transport.Writes.Length;
			_transport.FailNextWrite = true;
			_destination.RenderGray4(new DmdFrame(128, 32, 4));
			_destination.RenderGray4(new DmdFrame(128, 32, 4));

			_transport.Writes.Skip(before).Should().Contain(w => w.Length == 1 && w[0] == 0xEF);
		}

		[TestCase]
		public void Should_Report_Fixed_Size()
		{
			_destination = new FixedSizeDeviceNeutralDestination(_transport, CreateWriter(), Panel, new byte[0], AllMessages, new Dimensions(128, 32));

			(_destination as IFixedSizeDestination).FixedSize.Should().Be(new Dimensions(128, 32));
		}

		private void CreateDestination(params DeviceNeutralMessageType[] messages)
		{
			_destination = new DeviceNeutralDestination(_transport, CreateWriter(), Panel, new byte[0], messages.Length > 0 ? messages : AllMessages);
		}

		private static DeviceNeutralMessageWriter CreateWriter()
		{
			return new DeviceNeutralMessageWriter(new[] { DeviceNeutralMessageField.Length, DeviceNeutralMessageField.Type, DeviceNeutralMessageField.Panel, DeviceNeutralMessageField.Content },
				new byte[0], new byte[0], DeviceNeutralLengthFormat.UInt32LittleEndian, TypeBytes, ColorMatrix.Rgb);
		}

		private List<byte[]> ReadWrittenMessages()
		{
			return _transport.Writes.Select(w => w.Skip(4).ToArray()).ToList();
		}

		private static bool IsFrame(byte[] message)
		{
			return (message[0] & 0x80) != 0;
		}

		private static bool IsSize(byte[] message)
		{
			return message[0] == TypeBytes[DeviceNeutralMessageType.Size];
		}

		private static Dimensions FindSizeBefore(List<byte[]> messages, int index)
		{
			var size = messages.Take(index).Last(IsSize);
			return new Dimensions(size[2] | size[3] << 8, size[4] | size[5] << 8);
		}
	}
}
