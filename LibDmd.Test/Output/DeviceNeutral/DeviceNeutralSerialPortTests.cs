using System.Collections.Generic;
using FluentAssertions;
using LibDmd.Common;
using LibDmd.Output.DeviceNeutral;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class DeviceNeutralSerialPortTests : TestBase
	{
		private const ushort PicoVendorId = 0x2E8A;
		private const ushort PicoProductId = 0x000A;

		private List<UsbSerialPort> _usbPorts;
		private DeviceNeutralUsbSerialPort _pico;

		[SetUp]
		public void Setup()
		{
			_usbPorts = new List<UsbSerialPort>();
			_pico = new DeviceNeutralUsbSerialPort(PicoVendorId, PicoProductId, () => _usbPorts);
		}

		[TestCase]
		public void Should_Parse_A_Port_Name()
		{
			DeviceNeutralSerialPort.TryParse("COM7", out var port).Should().BeTrue();

			port.Should().BeOfType<DeviceNeutralNamedSerialPort>();
			port.Description.Should().Be("COM7");
		}

		[TestCase]
		public void Should_Parse_A_Usb_Id()
		{
			DeviceNeutralSerialPort.TryParse("usb:2E8A:000A", out var port).Should().BeTrue();

			port.Should().BeOfType<DeviceNeutralUsbSerialPort>();
			port.Description.Should().Be("usb:2E8A:000A");
		}

		[TestCase]
		public void Should_Parse_A_Usb_Id_In_Any_Case()
		{
			DeviceNeutralSerialPort.TryParse("USB:2e8a:000a", out var port).Should().BeTrue();

			port.Description.Should().Be("usb:2E8A:000A");
		}

		[TestCase]
		public void Should_Reject_An_Empty_Value()
		{
			DeviceNeutralSerialPort.TryParse(" ", out var port).Should().BeFalse();
		}

		[TestCase]
		public void Should_Reject_A_Usb_Id_Without_A_Product_Id()
		{
			DeviceNeutralSerialPort.TryParse("usb:2E8A", out var port).Should().BeFalse();
		}

		[TestCase]
		public void Should_Reject_A_Usb_Id_That_Is_Not_Hex()
		{
			DeviceNeutralSerialPort.TryParse("usb:2E8G:000A", out var port).Should().BeFalse();
		}

		[TestCase]
		public void Should_Reject_A_Usb_Id_Without_Four_Digits()
		{
			DeviceNeutralSerialPort.TryParse("usb:2E8A:A", out var port).Should().BeFalse();
		}

		[TestCase]
		public void Should_Find_A_Named_Port_By_Its_Name()
		{
			var port = new DeviceNeutralNamedSerialPort("COM7");

			port.TryFindPortName(out var portName).Should().BeTrue();
			portName.Should().Be("COM7");
		}

		[TestCase]
		public void Should_Find_The_Port_Of_A_Device()
		{
			_usbPorts.Add(new UsbSerialPort("COM7", PicoVendorId, PicoProductId));

			_pico.TryFindPortName(out var portName).Should().BeTrue();
			portName.Should().Be("COM7");
		}

		[TestCase]
		public void Should_Not_Find_The_Port_Of_Another_Product()
		{
			_usbPorts.Add(new UsbSerialPort("COM7", PicoVendorId, 0x0003));

			_pico.TryFindPortName(out var portName).Should().BeFalse();
		}

		[TestCase]
		public void Should_Not_Find_The_Port_Of_Another_Vendor()
		{
			_usbPorts.Add(new UsbSerialPort("COM7", 0x10C4, PicoProductId));

			_pico.TryFindPortName(out var portName).Should().BeFalse();
		}

		[TestCase]
		public void Should_Use_The_Lowest_Numbered_Port_When_Several_Match()
		{
			_usbPorts.Add(new UsbSerialPort("COM10", PicoVendorId, PicoProductId));
			_usbPorts.Add(new UsbSerialPort("COM9", PicoVendorId, PicoProductId));

			_pico.TryFindPortName(out var portName).Should().BeTrue();
			portName.Should().Be("COM9");
		}

		[TestCase]
		public void Should_Find_A_Device_Again_On_Its_New_Port()
		{
			_usbPorts.Add(new UsbSerialPort("COM7", PicoVendorId, PicoProductId));
			_pico.TryFindPortName(out var firstPortName);
			_usbPorts.Clear();
			_usbPorts.Add(new UsbSerialPort("COM9", PicoVendorId, PicoProductId));

			_pico.TryFindPortName(out var portName).Should().BeTrue();
			portName.Should().Be("COM9");
		}

		[TestCase]
		public void Should_Describe_A_Usb_Port_With_The_Port_It_Was_Found_On()
		{
			_usbPorts.Add(new UsbSerialPort("COM7", PicoVendorId, PicoProductId));

			_pico.TryFindPortName(out var portName);

			_pico.Description.Should().Be("usb:2E8A:000A (COM7)");
		}

		[TestCase]
		public void Should_Describe_A_Usb_Port_Without_A_Port_Once_The_Device_Is_Gone()
		{
			_usbPorts.Add(new UsbSerialPort("COM7", PicoVendorId, PicoProductId));
			_pico.TryFindPortName(out var firstPortName);
			_usbPorts.Clear();

			_pico.TryFindPortName(out var portName);

			_pico.Description.Should().Be("usb:2E8A:000A");
		}
	}
}
