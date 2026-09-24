using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using FluentAssertions;
using LibDmd.Output.DeviceNeutral;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class DeviceNeutralNamedPipeTransportTests : TestBase
	{
		private string _pipeName;
		private DeviceNeutralNamedPipeTransport _transport;

		[SetUp]
		public void Setup()
		{
			_pipeName = "deviceneutral-test-" + Guid.NewGuid();
			_transport = new DeviceNeutralNamedPipeTransport(_pipeName);
		}

		[TearDown]
		public void Teardown()
		{
			_transport.Dispose();
		}

		[TestCase]
		public void Should_Not_Connect_Without_Pipe()
		{
			_transport.TryConnect().Should().BeFalse();
			_transport.IsConnected.Should().BeFalse();
		}

		[TestCase]
		public void Should_Deliver_Each_Write_As_One_Pipe_Message()
		{
			using (var server = CreateServer()) {
				Connect(server);
				var reading = Task.Run(() => new[] { ReadPipeMessage(server), ReadPipeMessage(server) });

				_transport.Write(new byte[] { 0x00, 0x01, 0x02, 0x03 }, 1, 2).Should().BeTrue();
				_transport.Write(new byte[] { 0x04, 0x05, 0x06 }, 0, 3).Should().BeTrue();

				reading.Wait(1000).Should().BeTrue();
				reading.Result[0].Should().Equal(0x01, 0x02);
				reading.Result[1].Should().Equal(0x04, 0x05, 0x06);
			}
		}

		[TestCase]
		public void Should_Close_When_Receiver_Disconnects()
		{
			using (var server = CreateServer()) {
				Connect(server);
				server.Disconnect();

				_transport.Write(new byte[] { 0x01 }, 0, 1).Should().BeFalse();
				_transport.IsConnected.Should().BeFalse();
			}
		}

		private NamedPipeServerStream CreateServer()
		{
			return new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Message);
		}

		private void Connect(NamedPipeServerStream server)
		{
			var connection = server.WaitForConnectionAsync();
			_transport.TryConnect().Should().BeTrue();
			connection.Wait(1000).Should().BeTrue();
		}

		private static byte[] ReadPipeMessage(NamedPipeServerStream server)
		{
			var message = new MemoryStream();
			var buffer = new byte[16];
			do {
				var n = server.Read(buffer, 0, buffer.Length);
				message.Write(buffer, 0, n);
			} while (!server.IsMessageComplete);
			return message.ToArray();
		}
	}
}
