using System.Collections.Generic;
using System.Threading.Tasks;
using LibDmd.Common;
using LibDmd.Output;
using LibDmd.Test.Stubs;
using NUnit.Framework;

namespace LibDmd.Test
{
	[TestFixture]
	public class FrameFormatFilterTests : TestBase
	{
		private SourceGray2 _source;
		private RenderGraph _graph;

		[SetUp]
		public void Setup()
		{
			Profiler.Reset();

			_graph = new RenderGraph(new UndisposedReferences(), true);
			_source = new SourceGray2();
		}

		[TearDown]
		public void Teardown()
		{
			_graph.Dispose();

			AddLogger();
			Profiler.Print();
			RemoveLogger();
		}

		[TestCase]
		public async Task Should_Passthrough_Accepted_Format()
		{
			var dest = new DestinationDynamicGray2Rgb24Filtered { AcceptsGray2 = true };

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			await AssertFrame(_source, dest, frame, frame);
		}

		[TestCase]
		public async Task Should_Convert_Turned_Down_Format()
		{
			var dest = new DestinationDynamicGray2Rgb24Filtered { AcceptsGray2 = false };

			_graph.Source = _source;
			_graph.Destinations = new List<IDestination> { dest };
			_graph.StartRendering();

			var frame = FrameGenerator.FromString(@"
				33333333
				02020202
				10101010
				00000000");

			var rgbFrame = FrameGenerator.FromString(@"
				FF FF FF FF FF FF FF FF
				00 AA 00 AA 00 AA 00 AA
				55 00 55 00 55 00 55 00
				00 00 00 00 00 00 00 00", @"
				45 45 45 45 45 45 45 45
				00 2E 00 2E 00 2E 00 2E
				17 00 17 00 17 00 17 00
				00 00 00 00 00 00 00 00", @"
				00 00 00 00 00 00 00 00
				00 00 00 00 00 00 00 00
				00 00 00 00 00 00 00 00
				00 00 00 00 00 00 00 00");

			await AssertFrame(_source, dest, frame, rgbFrame);
		}
	}
}
