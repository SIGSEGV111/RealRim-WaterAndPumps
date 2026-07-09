using Verse;

namespace RealRim.WaterAndPumps
{
	public interface IFluidNetworkComponent
	{
		ThingWithComps ParentThing { get; }
	}

	public interface IHeatingNetworkReportProvider : IFluidNetworkComponent
	{
		bool tryGetHeatingNetworkReport(
			FluidNetwork network,
			out HeatingNetworkReport report);
	}

	public sealed class HeatingNetworkReport
	{
		public string label;
		public string details;
		public float production_kw;
		public float consumption_kw;
	}

	internal static class HeatingNetworkReportFormatting
	{
		public static string formatConsumptionKw(float consumption_kw, string format)
		{
			float magnitude_kw = UnityEngine.Mathf.Max(0f, consumption_kw);
			float signed_kw = magnitude_kw <= 0.0005f ? 0f : -magnitude_kw;
			return signed_kw.ToString(format);
		}
	}
}
