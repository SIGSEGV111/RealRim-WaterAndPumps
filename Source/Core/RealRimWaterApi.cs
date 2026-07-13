using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public static class RealRimWaterApi
	{
		public static bool IsConnectedToFreshWater(Thing thing)
		{
			return getFreshWaterNetwork(thing) != null;
		}

		public static float GetFreshWaterStored(Thing thing)
		{
			FluidNetwork network = getFreshWaterNetwork(thing);
			return network == null ? 0f : network.getStoredFreshWater();
		}

		public static float GetFreshWaterCapacity(Thing thing)
		{
			FluidNetwork network = getFreshWaterNetwork(thing);
			return network == null ? 0f : network.getFreshWaterCapacity();
		}

		public static bool HasFreshWater(Thing thing, float requested_liters)
		{
			return GetFreshWaterStored(thing) + 0.0001f >= Mathf.Max(0f, requested_liters);
		}

		public static float DrawFreshWater(Thing thing, float requested_liters)
		{
			FluidNetwork network = getFreshWaterNetwork(thing);
			return network == null ? 0f : network.drawFreshWater(Mathf.Max(0f, requested_liters));
		}

		public static bool TryDrawFreshWater(Thing thing, float requested_liters, out float drawn_liters)
		{
			drawn_liters = DrawFreshWater(thing, requested_liters);
			return drawn_liters + 0.0001f >= Mathf.Max(0f, requested_liters);
		}

		public static float AddFreshWater(Thing thing, float requested_liters)
		{
			FluidNetwork network = getFreshWaterNetwork(thing);
			return network == null ? 0f : network.addFreshWater(Mathf.Max(0f, requested_liters));
		}

		private static FluidNetwork getFreshWaterNetwork(Thing thing)
		{
			return FluidUtility.getNetwork(thing, FluidNetworkType.FreshWater);
		}
	}
}
