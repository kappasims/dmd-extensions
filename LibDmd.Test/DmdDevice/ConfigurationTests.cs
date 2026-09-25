using FluentAssertions;
using LibDmd.DmdDevice;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class ConfigurationTests
	{
		[TestCase]
		public void Should_Read_Settings_From_The_Source()
		{
			var config = new Configuration(new TestConfigurationSource("[virtualdmd]\nenabled = false"));

			config.VirtualDmd.Enabled.Should().BeFalse();
		}

		[TestCase]
		public void Should_Use_Defaults_When_The_Source_Has_No_Ini_Data()
		{
			var config = new Configuration(new TestConfigurationSource("[virtualdmd]\nenabled = false") { HasIniData = false });

			config.VirtualDmd.Enabled.Should().BeTrue();
		}

		[TestCase]
		public void Should_Take_The_Data_Path_From_The_Source()
		{
			var config = new Configuration(new TestConfigurationSource("") { DataPath = @"C:\Pinball\dmdext" });

			config.DataPath.Should().Be(@"C:\Pinball\dmdext");
		}

		[TestCase]
		public void Should_Reload_From_The_Source()
		{
			var source = new TestConfigurationSource("[virtualdmd]\nenabled = false");
			var config = new Configuration(source);

			source.IniData = new TestConfigurationSource("[virtualdmd]\nenabled = true").IniData;
			config.Reload();

			config.VirtualDmd.Enabled.Should().BeTrue();
		}

		[TestCase]
		public void Should_Keep_Its_Settings_When_A_Reload_Finds_No_Ini_Data()
		{
			var source = new TestConfigurationSource("[virtualdmd]\nenabled = false");
			var config = new Configuration(source);

			source.HasIniData = false;
			source.IniData = new TestConfigurationSource("[virtualdmd]\nenabled = true").IniData;
			config.Reload();

			config.VirtualDmd.Enabled.Should().BeFalse();
		}
	}
}
