using System;
using System.Linq;
using FluentAssertions;
using LibDmd.DmdDevice;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class PluginConfigTests : TestBase
	{
		private static readonly string PathKey = IntPtr.Size == 8 ? "path64" : "path";

		[TestCase]
		public void Should_Load_Contiguous_Plugins()
		{
			var config = ReadConfiguration($@"
				[global]
				plugin.0.{PathKey} = first.dll
				plugin.1.{PathKey} = second.dll");

			config.Global.Plugins.Select(p => p.Path).Should().Equal("first.dll", "second.dll");
		}

		[TestCase]
		public void Should_Load_Plugins_After_A_Gap_In_Indices()
		{
			var config = ReadConfiguration($@"
				[global]
				plugin.0.{PathKey} = first.dll
				plugin.2.{PathKey} = third.dll");

			config.Global.Plugins.Select(p => p.Path).Should().Equal("first.dll", "third.dll");
		}

		private Configuration ReadConfiguration(string ini)
		{
			return new Configuration(new TestConfigurationSource(string.Join("\n", ini.Split('\n').Select(l => l.Trim()))));
		}
	}
}
