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

		public FluidNetwork(
			int network_id,
			FluidNetworkType network_type,
			List<CompFluidNode> nodes)
		{
			this.network_id = network_id;
			this.network_type = network_type;
			this.nodes = nodes;
		}

		public IEnumerable<T> getComponents<T>() where T : ThingComp
		{
			HashSet<T> result = new HashSet<T>();
			for (int index = 0; index < nodes.Count; index++)
			{
				T component = nodes[index].parent.TryGetComp<T>();
				if (component != null)
				{
					result.Add(component);
				}
			}

			return result;
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

		public float getThermalStoredEnergyKj()
		{
			return getComponents<CompThermalTank>().Sum(tank => tank.getUsableEnergyKj());
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

		public float getStoredHotWater()
		{
			return getComponents<CompHotWaterTank>().Sum(tank => tank.stored_liters);
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
				remaining -= tanks[index].drawHotWater(remaining);
			}

			return requested_liters - remaining;
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
			float water_space = getComponents<CompWasteStorage>().Sum(storage => storage.getWaterSpace());
			float sludge_space = getComponents<CompWasteStorage>().Sum(storage => storage.getSludgeSpace());
			return water_space + 0.001f >= water_liters && sludge_space + 0.0001f >= sludge_kg;
		}

		public bool pushWaste(float water_liters, float sludge_kg)
		{
			if (!canAcceptWaste(water_liters, sludge_kg))
			{
				return false;
			}

			float remaining_water = Mathf.Max(0f, water_liters);
			float remaining_sludge = Mathf.Max(0f, sludge_kg);
			List<CompWasteStorage> storages = getComponents<CompWasteStorage>()
				.OrderBy(storage => storage.getFillFraction())
				.ToList();
			for (int index = 0; index < storages.Count; index++)
			{
				remaining_water -= storages[index].addWasteWater(remaining_water);
				remaining_sludge -= storages[index].addSludge(remaining_sludge);
			}

			return remaining_water <= 0.001f && remaining_sludge <= 0.0001f;
		}
	}
}
