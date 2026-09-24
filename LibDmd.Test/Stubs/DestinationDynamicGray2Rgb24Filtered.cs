using System;
using System.Windows.Media;
using LibDmd.Frame;
using LibDmd.Output;

namespace LibDmd.Test.Stubs
{
	public class DestinationDynamicGray2Rgb24Filtered : DestinationDynamic<DmdFrame>, IGray2Destination, IRgb24Destination, IFrameFormatFilter
	{
		public string Name => "Destination[Dynamic/Gray2/RGB24/Filtered]";
		public bool IsAvailable => true;
		public bool NeedsDuplicateFrames => false;

		public bool AcceptsGray2 { get; set; } = true;

		public bool Accepts(FrameFormat format)
		{
			return format != FrameFormat.Gray2 || AcceptsGray2;
		}

		public void RenderGray2(DmdFrame frame)
		{
			LastFrame.OnNext(frame);
		}

		public void RenderRgb24(DmdFrame frame)
		{
			LastFrame.OnNext(frame);
		}

		public void Dispose()
		{
		}

		public void ClearPalette()
		{
			throw new NotImplementedException();
		}

		public void ClearColor()
		{
			throw new NotImplementedException();
		}

		public void SetPalette(Color[] colors)
		{
			throw new NotImplementedException();
		}

		public void SetColor(Color color)
		{
			throw new NotImplementedException();
		}
	}
}
