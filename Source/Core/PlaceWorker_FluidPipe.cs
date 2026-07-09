using System.Collections.Generic;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class PlaceWorker_FluidPipe : PlaceWorker
	{
		public override bool ForceAllowPlaceOver(BuildableDef other_def)
		{
			ThingDef thing_def = other_def as ThingDef;
			return thing_def != null && FluidNetworkVisuals.getNodeProperties(thing_def) != null;
		}

		public override AcceptanceReport AllowsPlacing(
			BuildableDef checking_def,
			IntVec3 location,
			Rot4 rotation,
			Map map,
			Thing thing_to_ignore = null,
			Thing thing = null)
		{
			ThingDef checking_thing_def = checking_def as ThingDef;
			CompProperties_FluidNode checking_node = FluidNetworkVisuals.getNodeProperties(checking_thing_def);
			if (checking_node == null || checking_node.networks == null)
			{
				return true;
			}

			MapComponent_FluidNetworks manager = map?.GetComponent<MapComponent_FluidNetworks>();
			List<Thing> things = location.GetThingList(map);
			for (int index = 0; index < things.Count; index++)
			{
				if (things[index] == thing_to_ignore)
				{
					continue;
				}

				CompFluidNode existing_node = (things[index] as ThingWithComps)?.TryGetComp<CompFluidNode>();
				CompProperties_FluidNode planned_node = existing_node == null
					? FluidNetworkVisuals.getNodeProperties(things[index].def.entityDefToBuild as ThingDef)
					: null;
				for (int network_index = 0; network_index < checking_node.networks.Count; network_index++)
				{
					FluidNetworkType network_type = checking_node.networks[network_index];
					FluidNetworkLayer selected_layer = FluidNetworkLayerSettings.getSelectedLayer(network_type);
					bool checking_bridges_layers = checking_node.layer_connector;
					if (existing_node != null
						&& existing_node.supportsNetwork(network_type)
						&& (checking_bridges_layers
							|| existing_node.isLayerConnector(network_type)
							|| existing_node.getLayer(network_type) == selected_layer))
					{
						return duplicateReport(network_type, selected_layer);
					}

					FluidNetworkLayer planned_layer = manager == null
						? FluidNetworkLayer.None
						: manager.getConstructionPlanLayer(things[index], network_type);
					if (planned_node?.networks != null
						&& planned_node.networks.Contains(network_type)
						&& (checking_bridges_layers
							|| planned_node.layer_connector
							|| planned_layer == selected_layer))
					{
						return duplicateReport(network_type, selected_layer);
					}
				}
			}
			return true;
		}

		private static AcceptanceReport duplicateReport(
			FluidNetworkType network_type,
			FluidNetworkLayer layer)
		{
			return "RealRim_DuplicateFluidPipe".Translate(
				FluidUtility.getNetworkLabel(network_type),
				FluidNetworkLayerUtility.getLayerLabel(layer));
		}
	}
}
