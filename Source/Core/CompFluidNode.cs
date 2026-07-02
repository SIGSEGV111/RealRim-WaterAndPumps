using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_FluidNode : CompProperties
	{
		public List<FluidNetworkType> networks = new List<FluidNetworkType>();
		public bool valve;
		public float outdoor_heat_exchange_w_per_m_k;

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
				FluidNetworkVisuals.markOverlayDirty(parent);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo gizmo in base.CompGetGizmosExtra())
			{
				yield return gizmo;
			}

			if (supportsNetwork(FluidNetworkType.Heating))
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_HeatingOverview".Translate(),
					defaultDesc = "RealRim_HeatingOverviewDesc".Translate(),
					icon = RealRimTextures.heating_overview,
					action = delegate
					{
						Find.WindowStack.Add(new Dialog_HeatingNetworkReport(parent));
					},
				};
			}

			if (supportsNetwork(FluidNetworkType.HotWater))
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_HotWaterOverview".Translate(),
					defaultDesc = "RealRim_HotWaterOverviewDesc".Translate(),
					icon = RealRimTextures.hot_water_overview,
					action = delegate
					{
						Find.WindowStack.Add(new Dialog_HotWaterNetworkReport(parent));
					},
				};
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

		public MapComponent_FluidNetworks getManager()
		{
			return parent.MapHeld?.GetComponent<MapComponent_FluidNetworks>();
		}
	}
}
