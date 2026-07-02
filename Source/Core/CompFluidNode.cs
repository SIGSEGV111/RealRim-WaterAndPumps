using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_FluidNode : CompProperties
	{
		public List<FluidNetworkType> networks = new List<FluidNetworkType>();
		public bool valve;

		public CompProperties_FluidNode()
		{
			compClass = typeof(CompFluidNode);
		}
	}

	public sealed class CompFluidNode : ThingComp
	{
		public CompProperties_FluidNode Props
		{
			get
			{
				return (CompProperties_FluidNode)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			getManager()?.registerNode(this);
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			MapComponent_FluidNetworks manager = map?.GetComponent<MapComponent_FluidNetworks>();
			manager?.deregisterNode(this);
			base.PostDeSpawn(map, mode);
		}

		public override void ReceiveCompSignal(string signal)
		{
			base.ReceiveCompSignal(signal);
			if (Props.valve)
			{
				getManager()?.markNetworksDirty();
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (isPipeDef())
			{
				return;
			}

			MapComponent_FluidNetworks manager = getManager();
			if (manager == null || Props.networks.NullOrEmpty() || parent.Map == null)
			{
				return;
			}

			IntVec3[] offsets =
			{
				IntVec3.North,
				IntVec3.East,
				IntVec3.South,
				IntVec3.West,
			};
			CellRect occupied = parent.OccupiedRect();
			for (int network_index = 0; network_index < Props.networks.Count; network_index++)
			{
				FluidNetworkType network_type = Props.networks[network_index];
				FluidNetwork own_network = manager.getNetwork(this, network_type);
				if (own_network == null)
				{
					continue;
				}
				Material material = SolidColorMaterials.SimpleSolidColorMaterial(
					FluidNetworkVisuals.getColor(network_type));
				foreach (IntVec3 cell in occupied)
				{
					for (int offset_index = 0; offset_index < offsets.Length; offset_index++)
					{
						IntVec3 neighbor_cell = cell + offsets[offset_index];
						if (occupied.Contains(neighbor_cell) || !neighbor_cell.InBounds(parent.Map))
						{
							continue;
						}
						List<Thing> things = neighbor_cell.GetThingList(parent.Map);
						for (int thing_index = 0; thing_index < things.Count; thing_index++)
						{
							CompFluidNode neighbor = (things[thing_index] as ThingWithComps)?.TryGetComp<CompFluidNode>();
							if (neighbor == null
								|| !neighbor.supportsNetwork(network_type)
								|| manager.getNetwork(neighbor, network_type) != own_network)
							{
								continue;
							}
							Vector3 start = cell.ToVector3Shifted();
							Vector3 end = neighbor_cell.ToVector3Shifted();
							float altitude = AltitudeLayer.Conduits.AltitudeFor() + 0.03f;
							start.y = altitude;
							end.y = altitude;
							GenDraw.DrawLineBetween(start, end, material, 0.12f);
							break;
						}
					}
				}
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			MapComponent_FluidNetworks manager = getManager();
			if (manager == null || Props.networks.NullOrEmpty())
			{
				return;
			}

			for (int index = 0; index < Props.networks.Count; index++)
			{
				FluidNetworkType network_type = Props.networks[index];
				FluidNetworkVisuals.drawCells(
					manager.getNetworkCells(this, network_type),
					network_type);
			}
		}

		public override string CompInspectStringExtra()
		{
			MapComponent_FluidNetworks manager = getManager();
			if (manager == null || Props.networks.NullOrEmpty())
			{
				return null;
			}

			List<string> lines = new List<string>();
			for (int index = 0; index < Props.networks.Count; index++)
			{
				FluidNetwork network = manager.getNetwork(this, Props.networks[index]);
				if (network != null)
				{
					lines.Add("RealRim_FluidNetworkLine".Translate(
						FluidUtility.getNetworkLabel(Props.networks[index]),
						network.network_id,
						network.nodes.Count));
				}
			}

			return lines.Count == 0 ? null : lines.ToLineList("", false).TrimEndNewlines();
		}

		public bool supportsNetwork(FluidNetworkType network_type)
		{
			return Props.networks != null && Props.networks.Contains(network_type);
		}

		public bool isConnectionActive()
		{
			if (!Props.valve)
			{
				return true;
			}

			CompFlickable flickable = parent.TryGetComp<CompFlickable>();
			return flickable == null || flickable.SwitchIsOn;
		}

		private bool isPipeDef()
		{
			if (parent?.def?.placeWorkers == null)
			{
				return false;
			}
			for (int index = 0; index < parent.def.placeWorkers.Count; index++)
			{
				System.Type worker_type = parent.def.placeWorkers[index];
				if (worker_type == typeof(PlaceWorker_FluidPipe)
					|| worker_type?.FullName == "DubsBadHygiene.PlaceWorker_Pipe")
				{
					return true;
				}
			}
			return false;
		}

		public MapComponent_FluidNetworks getManager()
		{
			return parent.MapHeld?.GetComponent<MapComponent_FluidNetworks>();
		}
	}
}
