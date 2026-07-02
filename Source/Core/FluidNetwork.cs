using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class FluidNetwork
	{
		public readonly int network_id;
		public readonly FluidNetworkType network_type;
		public readonly List<CompFluidNode> nodes;
		public readonly int pipe_length_m;
		public readonly float pipe_heat_transfer_w_per_k;
		public float last_pipe_heat_exchange_kw;
		public float last_hot_water_draw_liters_per_hour;
		public float last_hot_water_draw_heat_kw;
		private float pending_hot_water_draw_liters;
		private float pending_hot_water_draw_heat_kj;

		public FluidNetwork(
			int network_id,
			FluidNetworkType network_type,
			List<CompFluidNode> nodes)
		{
			this.network_id = network_id;
			this.network_type = network_type;
			this.nodes = nodes;
			int calculated_pipe_length_m;
			float calculated_pipe_heat_transfer_w_per_k;
			calculatePipeHeatExchanger(
				out calculated_pipe_length_m,
				out calculated_pipe_heat_transfer_w_per_k);
			pipe_length_m = calculated_pipe_length_m;
			pipe_heat_transfer_w_per_k = calculated_pipe_heat_transfer_w_per_k;
		}

		public IEnumerable<T> getComponents<T>() where T : ThingComp
		{
			for (int index = 0; index < nodes.Count; index++)
			{
				T component = nodes[index].parent.TryGetComp<T>();
				if (component != null)
				{
					yield return component;
				}
			}
		}

		public int getLongestRouteMeters(Thing origin)
		{
			CompFluidNode origin_node = nodes.FirstOrDefault(node => node.parent == origin);
			if (origin_node == null)
			{
				return 0;
			}

			Dictionary<IntVec3, List<CompFluidNode>> by_cell = new Dictionary<IntVec3, List<CompFluidNode>>();
			for (int index = 0; index < nodes.Count; index++)
			{
				foreach (IntVec3 cell in nodes[index].parent.OccupiedRect())
				{
					List<CompFluidNode> cell_nodes;
					if (!by_cell.TryGetValue(cell, out cell_nodes))
					{
						cell_nodes = new List<CompFluidNode>();
						by_cell[cell] = cell_nodes;
					}
					cell_nodes.Add(nodes[index]);
				}
			}

			Queue<CompFluidNode> queue = new Queue<CompFluidNode>();
			Dictionary<CompFluidNode, int> distance = new Dictionary<CompFluidNode, int>();
			queue.Enqueue(origin_node);
			distance[origin_node] = 0;
			int longest = 0;
			IntVec3[] offsets = { IntVec3.Zero, IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };
			while (queue.Count > 0)
			{
				CompFluidNode current = queue.Dequeue();
				int current_distance = distance[current];
				longest = Mathf.Max(longest, current_distance);
				foreach (IntVec3 cell in current.parent.OccupiedRect())
				{
					for (int offset_index = 0; offset_index < offsets.Length; offset_index++)
					{
						List<CompFluidNode> adjacent;
						if (!by_cell.TryGetValue(cell + offsets[offset_index], out adjacent))
						{
							continue;
						}
						for (int adjacent_index = 0; adjacent_index < adjacent.Count; adjacent_index++)
						{
							CompFluidNode neighbor = adjacent[adjacent_index];
							if (distance.ContainsKey(neighbor))
							{
								continue;
							}
							distance[neighbor] = current_distance + 1;
							queue.Enqueue(neighbor);
						}
					}
				}
			}
			return longest;
		}

		public float getStoredFreshWater()
		{
			return getComponents<CompWaterTank>().Sum(tank => tank.stored_liters);
		}

		public float getFreshWaterCapacity()
		{
			return getComponents<CompWaterTank>().Sum(tank => tank.Props.capacity_liters);
		}

		public float addFreshWater(float requested_liters)
		{
			return addFreshWater(requested_liters, null);
		}

		public float addFreshWater(
			float requested_liters,
			WaterContamination contamination)
		{
			float remaining = Mathf.Max(0f, requested_liters);
			List<CompWaterTank> tanks = getComponents<CompWaterTank>()
				.OrderBy(tank => tank.getFillFraction())
				.ToList();
			for (int index = 0; index < tanks.Count && remaining > 0.0001f; index++)
			{
				remaining -= tanks[index].addWater(remaining, contamination);
			}

			return requested_liters - remaining;
		}

		public float drawFreshWater(float requested_liters)
		{
			return drawFreshWaterSample(requested_liters).liters;
		}

		public WaterSample drawFreshWaterSample(float requested_liters)
		{
			float remaining = Mathf.Max(0f, requested_liters);
			WaterSample result = new WaterSample();
			List<CompWaterTank> tanks = getComponents<CompWaterTank>()
				.OrderByDescending(tank => tank.stored_liters)
				.ToList();
			for (int index = 0; index < tanks.Count && remaining > 0.0001f; index++)
			{
				WaterSample sample = tanks[index].drawWaterSample(remaining);
				remaining -= sample.liters;
				result.addSample(sample);
			}

			result.applyTreatment(getPathogenRemovalFraction());
			return result;
		}

		public float getPathogenRemovalFraction()
		{
			float best_removal = 0f;
			foreach (CompWaterTreatment treatment in getComponents<CompWaterTreatment>())
			{
				if (treatment.isOperational())
				{
					best_removal = Mathf.Max(
						best_removal,
						treatment.Props.pathogen_removal_fraction);
				}
			}
			return Mathf.Clamp01(best_removal);
		}

		public float getThermalCapacityEnergyKj()
		{
			return getComponents<CompThermalTank>().Sum(tank => tank.getUsableCapacityKj());
		}

		public float getAverageThermalTemperature()
		{
			if (network_type == FluidNetworkType.HotWater)
			{
				List<CompHotWaterTank> hot_tanks = getComponents<CompHotWaterTank>().ToList();
				float hot_liters = hot_tanks.Sum(tank => tank.stored_liters);
				return hot_liters <= 0.0001f
					? RealPhysics.COLD_WATER_TEMPERATURE_C
					: hot_tanks.Sum(tank => tank.temperature_c * tank.stored_liters) / hot_liters;
			}

			List<CompThermalTank> tanks = getComponents<CompThermalTank>().ToList();
			float total_liters = tanks.Sum(tank => tank.stored_liters);
			if (total_liters <= 0.0001f)
			{
				return RealPhysics.COLD_WATER_TEMPERATURE_C;
			}

			return tanks.Sum(tank => tank.temperature_c * tank.stored_liters) / total_liters;
		}

		public float getOutdoorTemperatureC()
		{
			for (int index = 0; index < nodes.Count; index++)
			{
				Map map = nodes[index]?.parent?.MapHeld;
				if (map != null)
				{
					return map.mapTemperature.OutdoorTemp;
				}
			}

			return RealPhysics.COLD_WATER_TEMPERATURE_C;
		}

		public float getStoredHotWater()
		{
			return getComponents<CompHotWaterTank>().Sum(tank => tank.stored_liters);
		}

		public float getHotWaterCapacity()
		{
			return getComponents<CompHotWaterTank>().Sum(tank => tank.Props.capacity_liters);
		}

		public float addThermalEnergy(float requested_kj)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			List<CompThermalTank> tanks = getComponents<CompThermalTank>()
				.OrderBy(tank => tank.temperature_c)
				.ToList();
			for (int index = 0; index < tanks.Count && remaining > 0.001f; index++)
			{
				remaining -= tanks[index].addEnergy(remaining);
			}

			return requested_kj - remaining;
		}

		public float drawThermalEnergy(float requested_kj)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			List<CompThermalTank> tanks = getComponents<CompThermalTank>()
				.OrderByDescending(tank => tank.temperature_c)
				.ToList();
			for (int index = 0; index < tanks.Count && remaining > 0.001f; index++)
			{
				remaining -= tanks[index].drawEnergy(remaining);
			}

			return requested_kj - remaining;
		}

		public float drawHotWater(float requested_liters)
		{
			float remaining = Mathf.Max(0f, requested_liters);
			List<CompHotWaterTank> tanks = getComponents<CompHotWaterTank>()
				.OrderByDescending(tank => tank.temperature_c)
				.ToList();
			for (int index = 0; index < tanks.Count && remaining > 0.001f; index++)
			{
				CompHotWaterTank tank = tanks[index];
				float delivered_liters = tank.drawHotWater(remaining);
				remaining -= delivered_liters;
				pending_hot_water_draw_liters += delivered_liters;
				pending_hot_water_draw_heat_kj += RealPhysics.calculateWaterEnergy(
					delivered_liters,
					Mathf.Max(0f, tank.temperature_c - RealPhysics.COLD_WATER_TEMPERATURE_C));
			}

			return requested_liters - remaining;
		}

		public void tickOutdoorHeatExchange(float elapsed_seconds)
		{
			finalizeHotWaterDraw(elapsed_seconds);
			last_pipe_heat_exchange_kw = 0f;
			if (elapsed_seconds <= 0f
				|| pipe_heat_transfer_w_per_k <= 0f
				|| (network_type != FluidNetworkType.Heating
					&& network_type != FluidNetworkType.HotWater))
			{
				return;
			}

			float network_temperature_c = getAverageThermalTemperature();
			float outdoor_temperature_c = getOutdoorTemperatureC();
			float temperature_difference_c = outdoor_temperature_c - network_temperature_c;
			if (Mathf.Abs(temperature_difference_c) <= 0.001f)
			{
				return;
			}

			float requested_kj = pipe_heat_transfer_w_per_k
				* Mathf.Abs(temperature_difference_c)
				/ 1000f
				* elapsed_seconds;
			float exchanged_kj = temperature_difference_c > 0f
				? addEnergyTowardTemperature(requested_kj, outdoor_temperature_c)
				: drawEnergyTowardTemperature(requested_kj, outdoor_temperature_c);
			if (exchanged_kj <= 0f)
			{
				return;
			}

			last_pipe_heat_exchange_kw = exchanged_kj / elapsed_seconds;
			if (temperature_difference_c < 0f)
			{
				last_pipe_heat_exchange_kw = -last_pipe_heat_exchange_kw;
			}
		}

		public float getStoredColdEnergyKj()
		{
			return getComponents<CompCoolantTank>().Sum(tank => tank.cold_energy_kj);
		}

		public float getColdEnergyCapacityKj()
		{
			return getComponents<CompCoolantTank>().Sum(tank => tank.getCapacityKj());
		}

		public float getColdEnergySpaceKj()
		{
			return Mathf.Max(0f, getColdEnergyCapacityKj() - getStoredColdEnergyKj());
		}

		public float addColdEnergy(float requested_kj)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			List<CompCoolantTank> tanks = getComponents<CompCoolantTank>()
				.OrderBy(tank => tank.getFillFraction())
				.ToList();
			for (int index = 0; index < tanks.Count && remaining > 0.001f; index++)
			{
				remaining -= tanks[index].addColdEnergy(remaining);
			}

			return requested_kj - remaining;
		}

		public float drawColdEnergy(float requested_kj)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			List<CompCoolantTank> tanks = getComponents<CompCoolantTank>()
				.OrderByDescending(tank => tank.cold_energy_kj)
				.ToList();
			for (int index = 0; index < tanks.Count && remaining > 0.001f; index++)
			{
				remaining -= tanks[index].drawColdEnergy(remaining);
			}

			return requested_kj - remaining;
		}

		public bool canAcceptWaste(float water_liters, float sludge_kg)
		{
			float requested_water = Mathf.Max(0f, water_liters);
			float requested_sludge = Mathf.Max(0f, sludge_kg);
			if (requested_water <= 0.001f && requested_sludge <= 0.0001f)
			{
				return true;
			}

			List<CompWasteStorage> storages = getComponents<CompWasteStorage>().ToList();
			bool has_outlet = getComponents<CompSewageOutlet>().Any(outlet => outlet.isOperational());
			if (storages.Count == 0)
			{
				return has_outlet;
			}
			if (has_outlet)
			{
				return true;
			}

			float water_space = storages.Sum(storage => storage.getWaterSpace());
			float sludge_space = storages.Sum(storage => storage.getSludgeSpace());
			return water_space + 0.001f >= requested_water
				&& sludge_space + 0.0001f >= requested_sludge;
		}

		public bool pushWaste(float water_liters, float sludge_kg)
		{
			float remaining_water = Mathf.Max(0f, water_liters);
			float remaining_sludge = Mathf.Max(0f, sludge_kg);
			if (remaining_water <= 0.001f && remaining_sludge <= 0.0001f)
			{
				return true;
			}

			List<CompWasteStorage> storages = getComponents<CompWasteStorage>()
				.OrderBy(storage => storage.getFillFraction())
				.ToList();
			List<CompSewageOutlet> outlets = getComponents<CompSewageOutlet>()
				.Where(outlet => outlet.isOperational())
				.OrderBy(outlet => outlet.parent.thingIDNumber)
				.ToList();
			if (storages.Count == 0)
			{
				if (outlets.Count == 0)
				{
					return false;
				}
				dischargeUntreated(outlets, remaining_water, remaining_sludge);
				return true;
			}

			if (outlets.Count == 0 && !canAcceptWaste(remaining_water, remaining_sludge))
			{
				return false;
			}

			for (int index = 0; index < storages.Count; index++)
			{
				remaining_water -= storages[index].addWasteWater(remaining_water);
				remaining_sludge -= storages[index].addSludge(remaining_sludge);
			}

			if (remaining_water > 0.001f)
			{
				dischargeHarmlessWater(outlets, remaining_water);
			}
			if (remaining_sludge > 0.0001f)
			{
				dischargeUntreated(outlets, 0f, remaining_sludge);
			}
			return true;
		}

		private static void dischargeHarmlessWater(
			List<CompSewageOutlet> outlets,
			float water_liters)
		{
			float remaining_water = Mathf.Max(0f, water_liters);
			for (int index = 0; index < outlets.Count; index++)
			{
				float share = remaining_water / (outlets.Count - index);
				outlets[index].dischargeHarmlessWater(share);
				remaining_water -= share;
			}
		}

		private static void dischargeUntreated(
			List<CompSewageOutlet> outlets,
			float water_liters,
			float sludge_kg)
		{
			float remaining_water = Mathf.Max(0f, water_liters);
			float remaining_sludge = Mathf.Max(0f, sludge_kg);
			for (int index = 0; index < outlets.Count; index++)
			{
				int remaining_outlet_count = outlets.Count - index;
				float water_share = remaining_water / remaining_outlet_count;
				float sludge_share = remaining_sludge / remaining_outlet_count;
				outlets[index].dischargeUntreated(water_share, sludge_share);
				remaining_water -= water_share;
				remaining_sludge -= sludge_share;
			}
		}

		private void calculatePipeHeatExchanger(
			out int length_m,
			out float heat_transfer_w_per_k)
		{
			Dictionary<IntVec3, float> conductance_by_cell = new Dictionary<IntVec3, float>();
			for (int index = 0; index < nodes.Count; index++)
			{
				CompFluidNode node = nodes[index];
				float cell_conductance = node?.Props?.outdoor_heat_exchange_w_per_m_k ?? 0f;
				if (cell_conductance <= 0f)
				{
					continue;
				}

				foreach (IntVec3 cell in node.parent.OccupiedRect())
				{
					float existing_conductance;
					if (!conductance_by_cell.TryGetValue(cell, out existing_conductance)
						|| existing_conductance < cell_conductance)
					{
						conductance_by_cell[cell] = cell_conductance;
					}
				}
			}

			length_m = conductance_by_cell.Count;
			heat_transfer_w_per_k = conductance_by_cell.Values.Sum();
		}

		private void finalizeHotWaterDraw(float elapsed_seconds)
		{
			if (network_type != FluidNetworkType.HotWater || elapsed_seconds <= 0f)
			{
				return;
			}

			last_hot_water_draw_liters_per_hour = pending_hot_water_draw_liters
				* 3600f
				/ elapsed_seconds;
			last_hot_water_draw_heat_kw = pending_hot_water_draw_heat_kj / elapsed_seconds;
			pending_hot_water_draw_liters = 0f;
			pending_hot_water_draw_heat_kj = 0f;
		}

		private float addEnergyTowardTemperature(float requested_kj, float target_temperature_c)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			if (network_type == FluidNetworkType.HotWater)
			{
				List<CompHotWaterTank> hot_tanks = getComponents<CompHotWaterTank>()
					.OrderBy(tank => tank.temperature_c)
					.ToList();
				for (int index = 0; index < hot_tanks.Count && remaining > 0.001f; index++)
				{
					remaining -= hot_tanks[index].addEnergyTowardTemperature(
						remaining,
						target_temperature_c);
				}
			}
			else if (network_type == FluidNetworkType.Heating)
			{
				List<CompThermalTank> thermal_tanks = getComponents<CompThermalTank>()
					.OrderBy(tank => tank.temperature_c)
					.ToList();
				for (int index = 0; index < thermal_tanks.Count && remaining > 0.001f; index++)
				{
					remaining -= thermal_tanks[index].addEnergyTowardTemperature(
						remaining,
						target_temperature_c);
				}
			}

			return requested_kj - remaining;
		}

		private float drawEnergyTowardTemperature(float requested_kj, float target_temperature_c)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			if (network_type == FluidNetworkType.HotWater)
			{
				List<CompHotWaterTank> hot_tanks = getComponents<CompHotWaterTank>()
					.OrderByDescending(tank => tank.temperature_c)
					.ToList();
				for (int index = 0; index < hot_tanks.Count && remaining > 0.001f; index++)
				{
					remaining -= hot_tanks[index].drawEnergyTowardTemperature(
						remaining,
						target_temperature_c);
				}
			}
			else if (network_type == FluidNetworkType.Heating)
			{
				List<CompThermalTank> thermal_tanks = getComponents<CompThermalTank>()
					.OrderByDescending(tank => tank.temperature_c)
					.ToList();
				for (int index = 0; index < thermal_tanks.Count && remaining > 0.001f; index++)
				{
					remaining -= thermal_tanks[index].drawEnergyTowardTemperature(
						remaining,
						target_temperature_c);
				}
			}

			return requested_kj - remaining;
		}
	}
}
