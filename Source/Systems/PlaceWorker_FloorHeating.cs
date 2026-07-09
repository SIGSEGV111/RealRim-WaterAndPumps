using System;
using System.Collections.Generic;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class PlaceWorker_FloorHeating : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(
			BuildableDef checking_def,
			IntVec3 location,
			Rot4 rotation,
			Map map,
			Thing thing_to_ignore = null,
			Thing thing = null)
		{
			if (map == null || !location.InBounds(map))
			{
				return false;
			}

			TerrainDef terrain = location.GetTerrain(map);
			if (!isConstructedFloor(terrain))
			{
				return "RealRim_FloorHeatingRequiresConstructedFloor".Translate();
			}

			if (FloorHeatingUtility.hasFloorHeatingAt(location, map, thing_to_ignore))
			{
				return "RealRim_FloorHeatingAlreadyHere".Translate();
			}

			FluidNetworkLayer selected_layer = FluidNetworkLayerSettings.getSelectedLayer(FluidNetworkType.Heating);
			List<Thing> things = location.GetThingList(map);
			for (int index = 0; index < things.Count; index++)
			{
				ThingWithComps thing_with_comps = things[index] as ThingWithComps;
				CompFluidNode node = thing_with_comps?.TryGetComp<CompFluidNode>();
				if (node != null
					&& node.supportsNetwork(FluidNetworkType.Heating)
					&& node.getLayer(FluidNetworkType.Heating) == selected_layer)
				{
					return "RealRim_DuplicateFluidPipe".Translate(
						FluidUtility.getNetworkLabel(FluidNetworkType.Heating),
						FluidNetworkLayerUtility.getLayerLabel(selected_layer));
				}
			}

			return true;
		}

		private static bool isConstructedFloor(TerrainDef terrain)
		{
			if (terrain == null
				|| !terrain.IsFloor
				|| terrain.IsSoil
				|| terrain.IsWater
				|| terrain.IsIce)
			{
				return false;
			}

			string def_name = terrain.defName ?? string.Empty;
			return def_name.IndexOf("straw", StringComparison.OrdinalIgnoreCase) < 0
				|| def_name.IndexOf("mat", StringComparison.OrdinalIgnoreCase) < 0;
		}
	}
}
