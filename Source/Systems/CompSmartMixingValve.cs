using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_SmartMixingValve : CompProperties
	{
		public float maximum_transfer_kw = 12f;

		public CompProperties_SmartMixingValve()
		{
			compClass = typeof(CompSmartMixingValve);
		}
	}

	public sealed class CompSmartMixingValve : ThingComp, IFluidTickable
	{
		private static readonly IntVec3[] CONNECTION_OFFSETS =
		{
			IntVec3.North,
			IntVec3.East,
			IntVec3.South,
			IntVec3.West,
		};

		public float last_transfer_kw;
		public float last_source_temperature_c;
		public float last_receiving_temperature_c;
		public int last_source_network_id;
		public int last_receiving_network_id;
		public string last_reason = string.Empty;

		public CompProperties_SmartMixingValve Props
		{
			get
			{
				return (CompProperties_SmartMixingValve)props;
			}
		}

		public override string CompInspectStringExtra()
		{
			CompTargetTemperature target = parent.TryGetComp<CompTargetTemperature>();
			string target_temperature = target == null
				? "-"
				: target.target_temperature_c.ToStringTemperature("F1");
			return "RealRim_SmartMixingValveStatus".Translate(
				target_temperature,
				last_source_network_id <= 0 ? "-" : last_source_network_id.ToString(),
				last_source_temperature_c.ToStringTemperature("F1"),
				last_receiving_network_id <= 0 ? "-" : last_receiving_network_id.ToString(),
				last_receiving_temperature_c.ToStringTemperature("F1"),
				last_transfer_kw.ToString("N2"),
				last_reason).ToString().TrimEnd('\r', '\n', ' ', '\t');
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_transfer_kw = 0f;
			last_source_temperature_c = 0f;
			last_receiving_temperature_c = 0f;
			last_source_network_id = 0;
			last_receiving_network_id = 0;
			last_reason = string.Empty;

			if (elapsed_seconds <= 0f)
			{
				return;
			}

			if (!FluidUtility.isPoweredOn(parent))
			{
				last_reason = "RealRim_ReasonSwitchedOff".Translate();
				return;
			}

			CompTargetTemperature target = parent.TryGetComp<CompTargetTemperature>();
			if (target == null)
			{
				last_reason = "RealRim_ReasonNoValveTarget".Translate();
				return;
			}

			List<FluidNetwork> networks = getAdjacentHeatingNetworks();
			if (networks.Count < 2)
			{
				last_reason = "RealRim_ReasonValveNeedsTwoNetworks".Translate();
				return;
			}

			FluidNetwork source_network;
			FluidNetwork receiving_network;
			selectNetworks(networks, target.target_temperature_c, out source_network, out receiving_network);
			last_source_network_id = source_network.network_id;
			last_receiving_network_id = receiving_network.network_id;
			last_source_temperature_c = source_network.getAverageThermalTemperature();
			last_receiving_temperature_c = receiving_network.getAverageThermalTemperature();

			if (receiving_network.getThermalCapacityEnergyKj() <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoReceivingStorage".Translate();
				return;
			}
			if (source_network.getThermalCapacityEnergyKj() <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoHeatingStorage".Translate();
				return;
			}

			if (last_receiving_temperature_c >= target.target_temperature_c - 0.10f)
			{
				last_reason = "RealRim_ReasonTargetReached".Translate();
				return;
			}
			if (last_source_temperature_c <= last_receiving_temperature_c + 0.10f)
			{
				last_reason = "RealRim_ReasonSourceTooCold".Translate(
					last_source_temperature_c.ToStringTemperature("F1"));
				return;
			}

			float effective_target_c = Mathf.Min(target.target_temperature_c, last_source_temperature_c);
			float receiver_room_kj = receiving_network.getThermalEnergyNeededToReachTemperature(effective_target_c);
			float source_available_kj = source_network.getThermalEnergyAvailableAboveTemperature(last_receiving_temperature_c);
			float requested_kj = Mathf.Min(
				Props.maximum_transfer_kw * elapsed_seconds,
				Mathf.Min(receiver_room_kj, source_available_kj));
			if (requested_kj <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoStoredHeat".Translate();
				return;
			}

			float drawn_kj = source_network.drawThermalEnergyTowardTemperature(
				requested_kj,
				last_receiving_temperature_c);
			float accepted_kj = receiving_network.addThermalEnergyTowardTemperature(
				drawn_kj,
				effective_target_c);
			if (drawn_kj > accepted_kj + 0.001f)
			{
				source_network.addThermalEnergy(drawn_kj - accepted_kj);
			}

			source_network.recordMixingValveOutput(accepted_kj);
			receiving_network.recordMixingValveInput(accepted_kj);
			last_transfer_kw = accepted_kj / elapsed_seconds;
			if (last_transfer_kw <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoStoredHeat".Translate();
			}
		}

		private List<FluidNetwork> getAdjacentHeatingNetworks()
		{
			List<FluidNetwork> result = new List<FluidNetwork>();
			Map map = parent.MapHeld;
			MapComponent_FluidNetworks manager = map?.GetComponent<MapComponent_FluidNetworks>();
			if (map == null || manager == null)
			{
				return result;
			}

			CompFluidNode own_node = parent.TryGetComp<CompFluidNode>();
			for (int index = 0; index < CONNECTION_OFFSETS.Length; index++)
			{
				IntVec3 cell = parent.Position + CONNECTION_OFFSETS[index];
				if (!cell.InBounds(map))
				{
					continue;
				}

				List<Thing> things = cell.GetThingList(map);
				for (int thing_index = 0; thing_index < things.Count; thing_index++)
				{
					ThingWithComps thing_with_comps = things[thing_index] as ThingWithComps;
					CompFluidNode node = thing_with_comps?.TryGetComp<CompFluidNode>();
					if (node == null
						|| node == own_node
						|| node.Props.transfer_only
						|| !node.supportsNetwork(FluidNetworkType.Heating))
					{
						continue;
					}

					FluidNetwork network = manager.getNetwork(node, FluidNetworkType.Heating);
					if (network != null && !result.Contains(network))
					{
						result.Add(network);
					}
				}
			}
			return result;
		}

		private static void selectNetworks(
			List<FluidNetwork> networks,
			float target_temperature_c,
			out FluidNetwork source_network,
			out FluidNetwork receiving_network)
		{
			FluidNetwork selected_source_network = networks
				.OrderByDescending(network => network.getAverageThermalTemperature())
				.First();
			source_network = selected_source_network;
			receiving_network = networks
				.Where(network => network != selected_source_network)
				.OrderBy(network => network.getAverageThermalTemperature() >= target_temperature_c)
				.ThenBy(network => network.getAverageThermalTemperature())
				.First();
		}
	}
}
