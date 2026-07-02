using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class PlaceWorker_FluidPipe : PlaceWorker
	{
		public override void DrawGhost(
			ThingDef def,
			IntVec3 center,
			Rot4 rotation,
			Color ghost_color,
			Thing thing = null)
		{
			base.DrawGhost(def, center, rotation, ghost_color, thing);
			Map map = Find.CurrentMap;
			CompProperties_FluidNode node = getNodeProperties(def);
			MapComponent_FluidNetworks manager = map?.GetComponent<MapComponent_FluidNetworks>();
			if (node == null || manager == null || node.networks.NullOrEmpty())
			{
				return;
			}

			for (int index = 0; index < node.networks.Count; index++)
			{
				FluidNetworkType network_type = node.networks[index];
				FluidNetworkVisuals.drawCells(
					manager.getAllNodeCells(network_type),
					network_type);
			}
		}

		public override bool ForceAllowPlaceOver(BuildableDef other_def)
		{
			ThingDef thing_def = other_def as ThingDef;
			return thing_def != null && getNodeProperties(thing_def) != null;
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
			CompProperties_FluidNode checking_node = getNodeProperties(checking_thing_def);
			if (checking_node == null)
			{
				return true;
			}

			List<Thing> things = location.GetThingList(map);
			for (int index = 0; index < things.Count; index++)
			{
				if (things[index] == thing_to_ignore)
				{
					continue;
				}
				CompFluidNode existing_node = (things[index] as ThingWithComps)?.TryGetComp<CompFluidNode>();
				if (existing_node == null)
				{
					continue;
				}
				for (int network_index = 0; network_index < checking_node.networks.Count; network_index++)
				{
					if (existing_node.supportsNetwork(checking_node.networks[network_index]))
					{
						return "RealRim_DuplicateFluidPipe".Translate(
							FluidUtility.getNetworkLabel(checking_node.networks[network_index]));
					}
				}
			}
			return true;
		}

		private static CompProperties_FluidNode getNodeProperties(ThingDef def)
		{
			if (def?.comps == null)
			{
				return null;
			}
			for (int index = 0; index < def.comps.Count; index++)
			{
				CompProperties_FluidNode node = def.comps[index] as CompProperties_FluidNode;
				if (node != null)
				{
					return node;
				}
			}
			return null;
		}
	}
}
