using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LibDmd.Common
{
	/// <summary>
	/// A serial port of a USB device, as libserialport lists it.
	/// </summary>
	/// <see cref="https://sigrok.org/api/libserialport/unstable/"/>
	public class UsbSerialPort
	{
		public string Name { get; }
		public ushort VendorId { get; }
		public ushort ProductId { get; }

		public UsbSerialPort(string name, ushort vendorId, ushort productId)
		{
			Name = name;
			VendorId = vendorId;
			ProductId = productId;
		}

		/// <summary>
		/// Lists the serial ports of the USB devices that are plugged in.
		/// </summary>
		public static List<UsbSerialPort> ListAll()
		{
			var usbPorts = new List<UsbSerialPort>();
			if (sp_list_ports(out var ports) != 0) {
				return usbPorts;
			}
			var offset = 0;
			var port = Marshal.ReadIntPtr(ports, offset);
			while (port != IntPtr.Zero) {
				// only USB ports have a vendor and product ID
				if (sp_get_port_usb_vid_pid(port, out var vendorId, out var productId) == 0) {
					usbPorts.Add(new UsbSerialPort(Marshal.PtrToStringAnsi(sp_get_port_name(port)), (ushort)vendorId, (ushort)productId));
				}
				offset += IntPtr.Size;
				port = Marshal.ReadIntPtr(ports, offset);
			}
			sp_free_port_list(ports);
			return usbPorts;
		}

#if PLATFORM_X64
		private const string LibSerialPort = "libserialport64-0.dll";
#else
		private const string LibSerialPort = "libserialport-0.dll";
#endif

		[DllImport(LibSerialPort, CallingConvention = CallingConvention.Cdecl)]
		private static extern int sp_list_ports(out IntPtr ports);

		[DllImport(LibSerialPort, CallingConvention = CallingConvention.Cdecl)]
		private static extern void sp_free_port_list(IntPtr ports);

		[DllImport(LibSerialPort, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr sp_get_port_name(IntPtr port);

		[DllImport(LibSerialPort, CallingConvention = CallingConvention.Cdecl)]
		private static extern int sp_get_port_usb_vid_pid(IntPtr port, out int vendorId, out int productId);
	}
}
