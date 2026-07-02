using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	internal static class FluidNetworkVisuals
	{
		public static Color getColor(FluidNetworkType network_type)
		{
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					return new Color(0.20f, 0.65f, 1.00f, 0.80f);
				case FluidNetworkType.HotWater:
					return new Color(1.00f, 0.55f, 0.15f, 0.80f);
				case FluidNetworkType.Heating:
					return new Color(0.90f, 0.18f, 0.12f, 0.80f);
				case FluidNetworkType.WasteWater:
					return new Color(0.45f, 0.34f, 0.18f, 0.80f);
				case FluidNetworkType.Coolant:
					return new Color(0.25f, 0.90f, 0.95f, 0.80f);
				default:
					return Color.white;
			}
		}

		public static void drawCells(List<IntVec3> cells, FluidNetworkType network_type)
		{
			if (cells == null || cells.Count == 0)
			{
				return;
			}
			GenDraw.DrawFieldEdges(cells, getColor(network_type));
		}
	}
}
