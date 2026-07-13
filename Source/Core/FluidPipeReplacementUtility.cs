using System.Collections.Generic;
using Verse;

namespace RealRim.WaterAndPumps
{
	public static class FluidPipeReplacementUtility
	{
		public static bool canReplacePipe(
			ThingDef new_def,
			CompFluidNode existing_node,
			FluidNetworkType network_type,
			FluidNetworkLayer layer)
		{
			CompProperties_FluidNode new_properties = FluidNetworkVisuals.getNodeProperties(new_def);
			return new_properties != null
				&& isPipeDefinition(new_def)
				&& existing_node != null
				&& existing_node.parent != null
				&& isPipeDefinition(existing_node.parent.def)
				&& supportsSingleNetwork(new_properties, network_type)
				&& existing_node.supportsNetwork(network_type)
				&& existing_node.getLayer(network_type) == FluidNetworkLayerUtility.clampLayer(layer);
		}

		public static void removeReplacedPipes(CompFluidNode new_node)
		{
			if (new_node == null
				|| new_node.parent == null
				|| new_node.parent.MapHeld == null
				|| !isPipeDefinition(new_node.parent.def)
				|| new_node.Props.networks.NullOrEmpty())
			{
				return;
			}

			Map map = new_node.parent.MapHeld;
			List<Thing> things = new List<Thing>(new_node.parent.Position.GetThingList(map));
			for (int thing_index = 0; thing_index < things.Count; thing_index++)
			{
				ThingWithComps thing = things[thing_index] as ThingWithComps;
				if (thing == null || thing == new_node.parent || !isPipeDefinition(thing.def))
				{
					continue;
				}

				CompFluidNode existing_node = thing.TryGetComp<CompFluidNode>();
				if (existing_node != null && shouldReplace(new_node, existing_node))
				{
					thing.Destroy(DestroyMode.Deconstruct);
				}
			}
		}

		public static bool isPipeDefinition(ThingDef def)
		{
			if (def == null)
			{
				return false;
			}

			if (def.thingClass != null && def.thingClass.FullName == "DubsBadHygiene.Building_Pipe")
			{
				return true;
			}

			return def.defName == "RealRim_FreshWaterPipe"
				|| def.defName == "RealRim_FreshWaterPipeHidden"
				|| def.defName == "RealRim_HotWaterPipe"
				|| def.defName == "RealRim_HotWaterPipeHidden"
				|| def.defName == "RealRim_HeatingPipe"
				|| def.defName == "RealRim_HeatingPipeHidden"
				|| def.defName == "sewagePipeStuff"
				|| def.defName == "sewagePipeHidden"
				|| def.defName == "airPipe"
				|| def.defName == "airPipeHidden";
		}

		private static bool shouldReplace(CompFluidNode new_node, CompFluidNode existing_node)
		{
			for (int network_index = 0; network_index < new_node.Props.networks.Count; network_index++)
			{
				FluidNetworkType network_type = new_node.Props.networks[network_index];
				if (existing_node.supportsNetwork(network_type)
					&& new_node.getLayer(network_type) == existing_node.getLayer(network_type))
				{
					return true;
				}
			}
			return false;
		}

		private static bool supportsSingleNetwork(
			CompProperties_FluidNode properties,
			FluidNetworkType network_type)
		{
			return properties.networks != null
				&& properties.networks.Count == 1
				&& properties.networks.Contains(network_type);
		}
	}
}
