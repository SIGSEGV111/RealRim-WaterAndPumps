using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class FluidNetwork
	{
		public readonly int network_id;
		public readonly FluidNetworkType network_type;
		public readonly List<CompFluidNode> nodes;
		public readonly List<ThingWithComps> things;
		public readonly List<ThingComp> components;
		public readonly List<IHeatingNetworkReportProvider> heating_report_providers;
		public readonly List<CompWaterSource> water_sources;
		public readonly string state_key;
		public readonly int pipe_length_m;
		public readonly float pipe_heat_transfer_w_per_k;
		public readonly float virtual_heating_buffer_capacity_liters;
		public float virtual_heating_buffer_temperature_c;
		public float last_pipe_heat_exchange_kw;
		public float last_mixing_valve_input_kw;
		public float last_mixing_valve_output_kw;
		public float last_hot_water_draw_liters_per_hour;
		public float last_hot_water_draw_heat_kw;
		private float pending_hot_water_draw_liters;
		private float pending_hot_water_draw_heat_kj;
		private float pending_mixing_valve_input_kj;
		private float pending_mixing_valve_output_kj;

		private static readonly IntVec3[] ROUTE_OFFSETS =
		{
			IntVec3.Zero,
			IntVec3.North,
			IntVec3.East,
			IntVec3.South,
			IntVec3.West,
		};

		private static readonly Comparison<CompWaterTank> WATER_TANK_FILL_ASCENDING =
			compareWaterTankFillAscending;
		private static readonly Comparison<CompWaterTank> WATER_TANK_STORED_DESCENDING =
			compareWaterTankStoredDescending;
		private static readonly Comparison<CompHotWaterTank> HOT_WATER_TANK_TEMPERATURE_ASCENDING =
			compareHotWaterTankTemperatureAscending;
		private static readonly Comparison<CompHotWaterTank> HOT_WATER_TANK_TEMPERATURE_DESCENDING =
			compareHotWaterTankTemperatureDescending;
		private static readonly Comparison<CompThermalTank> THERMAL_TANK_TEMPERATURE_ASCENDING =
			compareThermalTankTemperatureAscending;
		private static readonly Comparison<CompThermalTank> THERMAL_TANK_TEMPERATURE_DESCENDING =
			compareThermalTankTemperatureDescending;
		private static readonly Comparison<CompCoolantTank> COOLANT_TANK_FILL_ASCENDING =
			compareCoolantTankFillAscending;
		private static readonly Comparison<CompCoolantTank> COOLANT_TANK_ENERGY_DESCENDING =
			compareCoolantTankEnergyDescending;
		private static readonly Comparison<CompWasteStorage> WASTE_STORAGE_FILL_ASCENDING =
			compareWasteStorageFillAscending;
		private static readonly Comparison<CompSewageOutlet> SEWAGE_OUTLET_ID_ASCENDING =
			compareSewageOutletIdAscending;

		private readonly List<CompWaterTank> water_tanks = new List<CompWaterTank>();
		private readonly List<CompWaterTank> ordered_water_tanks = new List<CompWaterTank>();
		private readonly List<CompHotWaterTank> hot_water_tanks = new List<CompHotWaterTank>();
		private readonly List<CompHotWaterTank> ordered_hot_water_tanks = new List<CompHotWaterTank>();
		private readonly List<CompThermalTank> thermal_tanks = new List<CompThermalTank>();
		private readonly List<CompThermalTank> ordered_thermal_tanks = new List<CompThermalTank>();
		private readonly List<CompCoolantTank> coolant_tanks = new List<CompCoolantTank>();
		private readonly List<CompCoolantTank> ordered_coolant_tanks = new List<CompCoolantTank>();
		private readonly List<CompWasteStorage> waste_storages = new List<CompWasteStorage>();
		private readonly List<CompWasteStorage> ordered_waste_storages = new List<CompWasteStorage>();
		private readonly List<CompSewageOutlet> sewage_outlets = new List<CompSewageOutlet>();
		private readonly List<CompSewageOutlet> operational_sewage_outlets = new List<CompSewageOutlet>();
		private readonly List<CompWaterTreatment> water_treatments = new List<CompWaterTreatment>();
		private readonly Dictionary<Thing, CompFluidNode> node_by_thing =
			new Dictionary<Thing, CompFluidNode>();
		private readonly Dictionary<CompFluidNode, List<CompFluidNode>> adjacent_nodes_by_node =
			new Dictionary<CompFluidNode, List<CompFluidNode>>();
		private readonly Dictionary<Thing, int> longest_route_by_origin = new Dictionary<Thing, int>();
		private bool route_graph_built;

		public FluidNetwork(
			int network_id,
			FluidNetworkType network_type,
			List<CompFluidNode> nodes,
			string state_key,
			float virtual_heating_buffer_temperature_c)
		{
			this.network_id = network_id;
			this.network_type = network_type;
			this.nodes = nodes;
			things = collectThings(nodes);
			components = collectComponents(things);
			heating_report_providers = new List<IHeatingNetworkReportProvider>();
			water_sources = new List<CompWaterSource>();
			cacheComponents();
			this.state_key = state_key;
			int calculated_pipe_length_m;
			float calculated_pipe_heat_transfer_w_per_k;
			float calculated_virtual_heating_buffer_capacity_liters;
			calculatePipeProperties(
				out calculated_pipe_length_m,
				out calculated_pipe_heat_transfer_w_per_k,
				out calculated_virtual_heating_buffer_capacity_liters);
			pipe_length_m = calculated_pipe_length_m;
			pipe_heat_transfer_w_per_k = calculated_pipe_heat_transfer_w_per_k;
			virtual_heating_buffer_capacity_liters = calculated_virtual_heating_buffer_capacity_liters;
			this.virtual_heating_buffer_temperature_c = Mathf.Clamp(
				virtual_heating_buffer_temperature_c,
				RealPhysics.HEATING_BUFFER_MINIMUM_TEMPERATURE_C,
				RealPhysics.HEATING_BUFFER_MAXIMUM_TEMPERATURE_C);
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

		private void cacheComponents()
		{
			for (int index = 0; index < nodes.Count; index++)
			{
				ThingWithComps thing = nodes[index]?.parent;
				if (thing == null)
				{
					continue;
				}

				addComponent(thing.TryGetComp<CompWaterTank>(), water_tanks);
				addComponent(thing.TryGetComp<CompHotWaterTank>(), hot_water_tanks);
				addComponent(thing.TryGetComp<CompThermalTank>(), thermal_tanks);
				addComponent(thing.TryGetComp<CompCoolantTank>(), coolant_tanks);
				addComponent(thing.TryGetComp<CompWasteStorage>(), waste_storages);
				addComponent(thing.TryGetComp<CompSewageOutlet>(), sewage_outlets);
				addComponent(thing.TryGetComp<CompWaterSource>(), water_sources);
				addComponent(thing.TryGetComp<CompWaterTreatment>(), water_treatments);
			}

			for (int index = 0; index < components.Count; index++)
			{
				IHeatingNetworkReportProvider report_provider =
					components[index] as IHeatingNetworkReportProvider;
				if (report_provider != null)
				{
					heating_report_providers.Add(report_provider);
				}
			}
			sewage_outlets.Sort(SEWAGE_OUTLET_ID_ASCENDING);
		}

		private static void addComponent<T>(T component, List<T> result) where T : ThingComp
		{
			if (component != null)
			{
				result.Add(component);
			}
		}

		private static List<ThingWithComps> collectThings(List<CompFluidNode> nodes)
		{
			List<ThingWithComps> result = new List<ThingWithComps>();
			HashSet<ThingWithComps> recorded = new HashSet<ThingWithComps>();
			for (int index = 0; index < nodes.Count; index++)
			{
				ThingWithComps thing = nodes[index]?.parent;
				if (thing != null && recorded.Add(thing))
				{
					result.Add(thing);
				}
			}
			return result;
		}

		private static List<ThingComp> collectComponents(List<ThingWithComps> things)
		{
			List<ThingComp> result = new List<ThingComp>();
			HashSet<ThingComp> recorded = new HashSet<ThingComp>();
			for (int thing_index = 0; thing_index < things.Count; thing_index++)
			{
				ThingWithComps thing = things[thing_index];
				if (thing?.AllComps == null)
				{
					continue;
				}

				for (int comp_index = 0; comp_index < thing.AllComps.Count; comp_index++)
				{
					ThingComp component = thing.AllComps[comp_index];
					if (component != null && recorded.Add(component))
					{
						result.Add(component);
					}
				}
			}
			return result;
		}

		private void buildRouteGraph()
		{
			if (route_graph_built)
			{
				return;
			}
			route_graph_built = true;
			Dictionary<IntVec3, List<CompFluidNode>> nodes_by_cell =
				new Dictionary<IntVec3, List<CompFluidNode>>();
			for (int index = 0; index < nodes.Count; index++)
			{
				CompFluidNode node = nodes[index];
				if (node?.parent == null)
				{
					continue;
				}

				if (!node_by_thing.ContainsKey(node.parent))
				{
					node_by_thing[node.parent] = node;
				}
				adjacent_nodes_by_node[node] = new List<CompFluidNode>();
				foreach (IntVec3 cell in node.parent.OccupiedRect())
				{
					List<CompFluidNode> cell_nodes;
					if (!nodes_by_cell.TryGetValue(cell, out cell_nodes))
					{
						cell_nodes = new List<CompFluidNode>();
						nodes_by_cell[cell] = cell_nodes;
					}
					cell_nodes.Add(node);
				}
			}

			for (int index = 0; index < nodes.Count; index++)
			{
				CompFluidNode node = nodes[index];
				List<CompFluidNode> adjacent_nodes;
				if (node?.parent == null || !adjacent_nodes_by_node.TryGetValue(node, out adjacent_nodes))
				{
					continue;
				}

				foreach (IntVec3 cell in node.parent.OccupiedRect())
				{
					for (int offset_index = 0; offset_index < ROUTE_OFFSETS.Length; offset_index++)
					{
						List<CompFluidNode> cell_nodes;
						if (!nodes_by_cell.TryGetValue(cell + ROUTE_OFFSETS[offset_index], out cell_nodes))
						{
							continue;
						}
						for (int neighbor_index = 0; neighbor_index < cell_nodes.Count; neighbor_index++)
						{
							CompFluidNode neighbor = cell_nodes[neighbor_index];
							if (neighbor != node && !adjacent_nodes.Contains(neighbor))
							{
								adjacent_nodes.Add(neighbor);
							}
						}
					}
				}
			}
		}

		public int getLongestRouteMeters(Thing origin)
		{
			int cached_distance;
			if (origin != null && longest_route_by_origin.TryGetValue(origin, out cached_distance))
			{
				return cached_distance;
			}

			buildRouteGraph();
			CompFluidNode origin_node;
			if (origin == null || !node_by_thing.TryGetValue(origin, out origin_node))
			{
				return 0;
			}

			Queue<CompFluidNode> queue = new Queue<CompFluidNode>();
			Dictionary<CompFluidNode, int> distance = new Dictionary<CompFluidNode, int>();
			queue.Enqueue(origin_node);
			distance[origin_node] = 0;
			int longest = 0;
			while (queue.Count > 0)
			{
				CompFluidNode current = queue.Dequeue();
				int current_distance = distance[current];
				longest = Mathf.Max(longest, current_distance);
				List<CompFluidNode> adjacent_nodes;
				if (!adjacent_nodes_by_node.TryGetValue(current, out adjacent_nodes))
				{
					continue;
				}

				for (int index = 0; index < adjacent_nodes.Count; index++)
				{
					CompFluidNode neighbor = adjacent_nodes[index];
					if (distance.ContainsKey(neighbor))
					{
						continue;
					}
					distance[neighbor] = current_distance + 1;
					queue.Enqueue(neighbor);
				}
			}

			longest_route_by_origin[origin] = longest;
			return longest;
		}

		public float getStoredFreshWater()
		{
			float stored_liters = 0f;
			for (int index = 0; index < water_tanks.Count; index++)
			{
				stored_liters += water_tanks[index].stored_liters;
			}
			return stored_liters;
		}

		public float getFreshWaterCapacity()
		{
			float capacity_liters = 0f;
			for (int index = 0; index < water_tanks.Count; index++)
			{
				capacity_liters += water_tanks[index].Props.capacity_liters;
			}
			return capacity_liters;
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
			copyAndSortStable(water_tanks, ordered_water_tanks, WATER_TANK_FILL_ASCENDING);
			for (int index = 0; index < ordered_water_tanks.Count && remaining > 0.0001f; index++)
			{
				remaining -= ordered_water_tanks[index].addWater(remaining, contamination);
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
			copyAndSortStable(water_tanks, ordered_water_tanks, WATER_TANK_STORED_DESCENDING);
			for (int index = 0; index < ordered_water_tanks.Count && remaining > 0.0001f; index++)
			{
				WaterSample sample = ordered_water_tanks[index].drawWaterSample(remaining);
				remaining -= sample.liters;
				result.addSample(sample);
			}

			result.applyTreatment(getPathogenRemovalFraction());
			return result;
		}

		public float getPathogenRemovalFraction()
		{
			float best_removal = 0f;
			for (int index = 0; index < water_treatments.Count; index++)
			{
				CompWaterTreatment treatment = water_treatments[index];
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
			float capacity_kj = 0f;
			for (int index = 0; index < thermal_tanks.Count; index++)
			{
				capacity_kj += thermal_tanks[index].getUsableCapacityKj();
			}
			if (network_type == FluidNetworkType.Heating)
			{
				capacity_kj += getVirtualHeatingBufferUsableCapacityKj();
			}
			return capacity_kj;
		}

		public float getAverageThermalTemperature()
		{
			if (network_type == FluidNetworkType.HotWater)
			{
				float hot_liters = 0f;
				float weighted_hot_temperature = 0f;
				for (int index = 0; index < hot_water_tanks.Count; index++)
				{
					CompHotWaterTank tank = hot_water_tanks[index];
					hot_liters += tank.stored_liters;
					weighted_hot_temperature += tank.temperature_c * tank.stored_liters;
				}
				return hot_liters <= 0.0001f
					? RealPhysics.COLD_WATER_TEMPERATURE_C
					: weighted_hot_temperature / hot_liters;
			}

			float total_liters = 0f;
			float weighted_temperature = 0f;
			for (int index = 0; index < thermal_tanks.Count; index++)
			{
				CompThermalTank tank = thermal_tanks[index];
				total_liters += tank.stored_liters;
				weighted_temperature += tank.temperature_c * tank.stored_liters;
			}
			if (network_type == FluidNetworkType.Heating && virtual_heating_buffer_capacity_liters > 0.001f)
			{
				total_liters += virtual_heating_buffer_capacity_liters;
				weighted_temperature += virtual_heating_buffer_temperature_c
					* virtual_heating_buffer_capacity_liters;
			}
			if (total_liters <= 0.0001f)
			{
				return RealPhysics.COLD_WATER_TEMPERATURE_C;
			}

			return weighted_temperature / total_liters;
		}

		public float getVirtualHeatingBufferUsableEnergyKj()
		{
			if (network_type != FluidNetworkType.Heating || virtual_heating_buffer_capacity_liters <= 0.001f)
			{
				return 0f;
			}

			return RealPhysics.calculateWaterEnergy(
				virtual_heating_buffer_capacity_liters,
				Mathf.Max(
					0f,
					virtual_heating_buffer_temperature_c
						- RealPhysics.HEATING_BUFFER_MINIMUM_TEMPERATURE_C));
		}

		public float getVirtualHeatingBufferUsableCapacityKj()
		{
			if (network_type != FluidNetworkType.Heating || virtual_heating_buffer_capacity_liters <= 0.001f)
			{
				return 0f;
			}

			return RealPhysics.calculateWaterEnergy(
				virtual_heating_buffer_capacity_liters,
				RealPhysics.HEATING_BUFFER_MAXIMUM_TEMPERATURE_C
					- RealPhysics.HEATING_BUFFER_MINIMUM_TEMPERATURE_C);
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
			float stored_liters = 0f;
			for (int index = 0; index < hot_water_tanks.Count; index++)
			{
				stored_liters += hot_water_tanks[index].stored_liters;
			}
			return stored_liters;
		}

		public float getHotWaterCapacity()
		{
			float capacity_liters = 0f;
			for (int index = 0; index < hot_water_tanks.Count; index++)
			{
				capacity_liters += hot_water_tanks[index].Props.capacity_liters;
			}
			return capacity_liters;
		}

		public float addThermalEnergy(float requested_kj)
		{
			if (network_type == FluidNetworkType.Heating)
			{
				return addEnergyTowardTemperature(
					requested_kj,
					RealPhysics.HEATING_BUFFER_MAXIMUM_TEMPERATURE_C);
			}

			float remaining = Mathf.Max(0f, requested_kj);
			copyAndSortStable(thermal_tanks, ordered_thermal_tanks, THERMAL_TANK_TEMPERATURE_ASCENDING);
			for (int index = 0; index < ordered_thermal_tanks.Count && remaining > 0.001f; index++)
			{
				remaining -= ordered_thermal_tanks[index].addEnergy(remaining);
			}

			return requested_kj - remaining;
		}

		public float drawThermalEnergy(float requested_kj)
		{
			if (network_type == FluidNetworkType.Heating)
			{
				return drawEnergyTowardTemperature(
					requested_kj,
					RealPhysics.HEATING_BUFFER_MINIMUM_TEMPERATURE_C);
			}

			float remaining = Mathf.Max(0f, requested_kj);
			copyAndSortStable(thermal_tanks, ordered_thermal_tanks, THERMAL_TANK_TEMPERATURE_DESCENDING);
			for (int index = 0; index < ordered_thermal_tanks.Count && remaining > 0.001f; index++)
			{
				remaining -= ordered_thermal_tanks[index].drawEnergy(remaining);
			}

			return requested_kj - remaining;
		}

		public float addThermalEnergyTowardTemperature(float requested_kj, float target_temperature_c)
		{
			return addEnergyTowardTemperature(requested_kj, target_temperature_c);
		}

		public float drawThermalEnergyTowardTemperature(float requested_kj, float target_temperature_c)
		{
			return drawEnergyTowardTemperature(requested_kj, target_temperature_c);
		}

		public float getThermalEnergyNeededToReachTemperature(float target_temperature_c)
		{
			float needed_kj = 0f;
			if (network_type == FluidNetworkType.Heating && virtual_heating_buffer_capacity_liters > 0.001f)
			{
				float target_c = Mathf.Min(
					RealPhysics.HEATING_BUFFER_MAXIMUM_TEMPERATURE_C,
					target_temperature_c);
				float temperature_room_c = target_c - virtual_heating_buffer_temperature_c;
				if (temperature_room_c > 0.001f)
				{
					needed_kj += RealPhysics.calculateWaterEnergy(
						virtual_heating_buffer_capacity_liters,
						temperature_room_c);
				}
			}

			for (int index = 0; index < thermal_tanks.Count; index++)
			{
				CompThermalTank tank = thermal_tanks[index];
				if (tank.stored_liters <= 0.001f)
				{
					continue;
				}

				float target_c = Mathf.Min(tank.Props.maximum_temperature_c, target_temperature_c);
				float temperature_room_c = target_c - tank.temperature_c;
				if (temperature_room_c > 0.001f)
				{
					needed_kj += RealPhysics.calculateWaterEnergy(tank.stored_liters, temperature_room_c);
				}
			}
			return needed_kj;
		}

		public float getThermalEnergyAvailableAboveTemperature(float target_temperature_c)
		{
			float available_kj = 0f;
			if (network_type == FluidNetworkType.Heating && virtual_heating_buffer_capacity_liters > 0.001f)
			{
				float target_c = Mathf.Max(
					RealPhysics.HEATING_BUFFER_MINIMUM_TEMPERATURE_C,
					target_temperature_c);
				float temperature_room_c = virtual_heating_buffer_temperature_c - target_c;
				if (temperature_room_c > 0.001f)
				{
					available_kj += RealPhysics.calculateWaterEnergy(
						virtual_heating_buffer_capacity_liters,
						temperature_room_c);
				}
			}

			for (int index = 0; index < thermal_tanks.Count; index++)
			{
				CompThermalTank tank = thermal_tanks[index];
				if (tank.stored_liters <= 0.001f)
				{
					continue;
				}

				float target_c = Mathf.Max(tank.Props.minimum_temperature_c, target_temperature_c);
				float temperature_room_c = tank.temperature_c - target_c;
				if (temperature_room_c > 0.001f)
				{
					available_kj += RealPhysics.calculateWaterEnergy(tank.stored_liters, temperature_room_c);
				}
			}
			return available_kj;
		}

		public void recordMixingValveInput(float heat_kj)
		{
			pending_mixing_valve_input_kj += Mathf.Max(0f, heat_kj);
		}

		public void recordMixingValveOutput(float heat_kj)
		{
			pending_mixing_valve_output_kj += Mathf.Max(0f, heat_kj);
		}

		public float drawHotWater(float requested_liters)
		{
			float remaining = Mathf.Max(0f, requested_liters);
			copyAndSortStable(hot_water_tanks, ordered_hot_water_tanks, HOT_WATER_TANK_TEMPERATURE_DESCENDING);
			for (int index = 0; index < ordered_hot_water_tanks.Count && remaining > 0.001f; index++)
			{
				CompHotWaterTank tank = ordered_hot_water_tanks[index];
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
			finalizeMixingValveTransfer(elapsed_seconds);
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
			float stored_kj = 0f;
			for (int index = 0; index < coolant_tanks.Count; index++)
			{
				stored_kj += coolant_tanks[index].cold_energy_kj;
			}
			return stored_kj;
		}

		public float getColdEnergyCapacityKj()
		{
			float capacity_kj = 0f;
			for (int index = 0; index < coolant_tanks.Count; index++)
			{
				capacity_kj += coolant_tanks[index].getCapacityKj();
			}
			return capacity_kj;
		}

		public float getColdEnergySpaceKj()
		{
			return Mathf.Max(0f, getColdEnergyCapacityKj() - getStoredColdEnergyKj());
		}

		public float addColdEnergy(float requested_kj)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			copyAndSortStable(coolant_tanks, ordered_coolant_tanks, COOLANT_TANK_FILL_ASCENDING);
			for (int index = 0; index < ordered_coolant_tanks.Count && remaining > 0.001f; index++)
			{
				remaining -= ordered_coolant_tanks[index].addColdEnergy(remaining);
			}

			return requested_kj - remaining;
		}

		public float drawColdEnergy(float requested_kj)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			copyAndSortStable(coolant_tanks, ordered_coolant_tanks, COOLANT_TANK_ENERGY_DESCENDING);
			for (int index = 0; index < ordered_coolant_tanks.Count && remaining > 0.001f; index++)
			{
				remaining -= ordered_coolant_tanks[index].drawColdEnergy(remaining);
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

			cacheOperationalSewageOutlets();
			if (waste_storages.Count == 0)
			{
				return operational_sewage_outlets.Count > 0;
			}
			if (operational_sewage_outlets.Count > 0)
			{
				return true;
			}

			float water_space = 0f;
			float sludge_space = 0f;
			for (int index = 0; index < waste_storages.Count; index++)
			{
				water_space += waste_storages[index].getWaterSpace();
				sludge_space += waste_storages[index].getSludgeSpace();
			}
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

			cacheOperationalSewageOutlets();
			copyAndSortStable(waste_storages, ordered_waste_storages, WASTE_STORAGE_FILL_ASCENDING);
			if (ordered_waste_storages.Count == 0)
			{
				if (operational_sewage_outlets.Count == 0)
				{
					return false;
				}
				dischargeUntreated(operational_sewage_outlets, remaining_water, remaining_sludge);
				return true;
			}

			if (operational_sewage_outlets.Count == 0)
			{
				float water_space = 0f;
				float sludge_space = 0f;
				for (int index = 0; index < ordered_waste_storages.Count; index++)
				{
					water_space += ordered_waste_storages[index].getWaterSpace();
					sludge_space += ordered_waste_storages[index].getSludgeSpace();
				}
				if (water_space + 0.001f < remaining_water
					|| sludge_space + 0.0001f < remaining_sludge)
				{
					return false;
				}
			}

			for (int index = 0; index < ordered_waste_storages.Count; index++)
			{
				remaining_water -= ordered_waste_storages[index].addWasteWater(remaining_water);
				remaining_sludge -= ordered_waste_storages[index].addSludge(remaining_sludge);
			}

			if (remaining_water > 0.001f)
			{
				dischargeHarmlessWater(operational_sewage_outlets, remaining_water);
			}
			if (remaining_sludge > 0.0001f)
			{
				dischargeUntreated(operational_sewage_outlets, 0f, remaining_sludge);
			}
			return true;
		}

		private void cacheOperationalSewageOutlets()
		{
			operational_sewage_outlets.Clear();
			for (int index = 0; index < sewage_outlets.Count; index++)
			{
				CompSewageOutlet outlet = sewage_outlets[index];
				if (outlet.isOperational())
				{
					operational_sewage_outlets.Add(outlet);
				}
			}
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

		private void calculatePipeProperties(
			out int length_m,
			out float heat_transfer_w_per_k,
			out float virtual_heating_buffer_capacity_liters)
		{
			Dictionary<IntVec3, float> conductance_by_cell = new Dictionary<IntVec3, float>();
			Dictionary<IntVec3, float> buffer_liters_by_cell = new Dictionary<IntVec3, float>();
			for (int index = 0; index < nodes.Count; index++)
			{
				CompFluidNode node = nodes[index];
				float cell_conductance = node?.Props?.outdoor_heat_exchange_w_per_m_k ?? 0f;
				float cell_buffer_liters = node?.Props?.virtual_heat_buffer_liters_per_m ?? 0f;
				if (cell_conductance <= 0f && cell_buffer_liters <= 0f)
				{
					continue;
				}

				foreach (IntVec3 cell in node.parent.OccupiedRect())
				{
					if (cell_conductance > 0f)
					{
						float existing_conductance;
						if (!conductance_by_cell.TryGetValue(cell, out existing_conductance)
							|| existing_conductance < cell_conductance)
						{
							conductance_by_cell[cell] = cell_conductance;
						}
					}

					if (network_type == FluidNetworkType.Heating && cell_buffer_liters > 0f)
					{
						float existing_buffer_liters;
						if (!buffer_liters_by_cell.TryGetValue(cell, out existing_buffer_liters)
							|| existing_buffer_liters < cell_buffer_liters)
						{
							buffer_liters_by_cell[cell] = cell_buffer_liters;
						}
					}
				}
			}

			HashSet<IntVec3> pipe_cells = new HashSet<IntVec3>(conductance_by_cell.Keys);
			foreach (IntVec3 cell in buffer_liters_by_cell.Keys)
			{
				pipe_cells.Add(cell);
			}

			length_m = pipe_cells.Count;
			heat_transfer_w_per_k = 0f;
			foreach (float conductance in conductance_by_cell.Values)
			{
				heat_transfer_w_per_k += conductance;
			}
			virtual_heating_buffer_capacity_liters = 0f;
			foreach (float buffer_liters in buffer_liters_by_cell.Values)
			{
				virtual_heating_buffer_capacity_liters += buffer_liters;
			}
		}

		private void finalizeMixingValveTransfer(float elapsed_seconds)
		{
			if (elapsed_seconds <= 0f)
			{
				last_mixing_valve_input_kw = 0f;
				last_mixing_valve_output_kw = 0f;
				return;
			}

			last_mixing_valve_input_kw = pending_mixing_valve_input_kj / elapsed_seconds;
			last_mixing_valve_output_kw = pending_mixing_valve_output_kj / elapsed_seconds;
			pending_mixing_valve_input_kj = 0f;
			pending_mixing_valve_output_kj = 0f;
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
				copyAndSortStable(hot_water_tanks, ordered_hot_water_tanks, HOT_WATER_TANK_TEMPERATURE_ASCENDING);
				for (int index = 0; index < ordered_hot_water_tanks.Count && remaining > 0.001f; index++)
				{
					remaining -= ordered_hot_water_tanks[index].addEnergyTowardTemperature(
						remaining,
						target_temperature_c);
				}
			}
			else if (network_type == FluidNetworkType.Heating)
			{
				copyAndSortStable(thermal_tanks, ordered_thermal_tanks, THERMAL_TANK_TEMPERATURE_ASCENDING);
				int index = 0;
				bool virtual_buffer_processed = virtual_heating_buffer_capacity_liters <= 0.001f;
				while (remaining > 0.001f && (index < ordered_thermal_tanks.Count || !virtual_buffer_processed))
				{
					bool use_virtual_buffer = !virtual_buffer_processed
						&& (index >= ordered_thermal_tanks.Count
							|| virtual_heating_buffer_temperature_c <= ordered_thermal_tanks[index].temperature_c);
					if (use_virtual_buffer)
					{
						remaining -= addVirtualHeatingBufferEnergyTowardTemperature(
							remaining,
							target_temperature_c);
						virtual_buffer_processed = true;
					}
					else
					{
						remaining -= ordered_thermal_tanks[index].addEnergyTowardTemperature(
							remaining,
							target_temperature_c);
						index++;
					}
				}
			}

			return requested_kj - remaining;
		}

		private float drawEnergyTowardTemperature(float requested_kj, float target_temperature_c)
		{
			float remaining = Mathf.Max(0f, requested_kj);
			if (network_type == FluidNetworkType.HotWater)
			{
				copyAndSortStable(hot_water_tanks, ordered_hot_water_tanks, HOT_WATER_TANK_TEMPERATURE_DESCENDING);
				for (int index = 0; index < ordered_hot_water_tanks.Count && remaining > 0.001f; index++)
				{
					remaining -= ordered_hot_water_tanks[index].drawEnergyTowardTemperature(
						remaining,
						target_temperature_c);
				}
			}
			else if (network_type == FluidNetworkType.Heating)
			{
				copyAndSortStable(thermal_tanks, ordered_thermal_tanks, THERMAL_TANK_TEMPERATURE_DESCENDING);
				int index = 0;
				bool virtual_buffer_processed = virtual_heating_buffer_capacity_liters <= 0.001f;
				while (remaining > 0.001f && (index < ordered_thermal_tanks.Count || !virtual_buffer_processed))
				{
					bool use_virtual_buffer = !virtual_buffer_processed
						&& (index >= ordered_thermal_tanks.Count
							|| virtual_heating_buffer_temperature_c >= ordered_thermal_tanks[index].temperature_c);
					if (use_virtual_buffer)
					{
						remaining -= drawVirtualHeatingBufferEnergyTowardTemperature(
							remaining,
							target_temperature_c);
						virtual_buffer_processed = true;
					}
					else
					{
						remaining -= ordered_thermal_tanks[index].drawEnergyTowardTemperature(
							remaining,
							target_temperature_c);
						index++;
					}
				}
			}

			return requested_kj - remaining;
		}

		private static void copyAndSortStable<T>(
			List<T> source,
			List<T> result,
			Comparison<T> comparison)
		{
			result.Clear();
			result.AddRange(source);
			for (int index = 1; index < result.Count; index++)
			{
				T item = result[index];
				int insert_index = index;
				while (insert_index > 0 && comparison(item, result[insert_index - 1]) < 0)
				{
					result[insert_index] = result[insert_index - 1];
					insert_index--;
				}
				result[insert_index] = item;
			}
		}

		private static int compareWaterTankFillAscending(CompWaterTank left, CompWaterTank right)
		{
			return left.getFillFraction().CompareTo(right.getFillFraction());
		}

		private static int compareWaterTankStoredDescending(CompWaterTank left, CompWaterTank right)
		{
			return right.stored_liters.CompareTo(left.stored_liters);
		}

		private static int compareHotWaterTankTemperatureAscending(CompHotWaterTank left, CompHotWaterTank right)
		{
			return left.temperature_c.CompareTo(right.temperature_c);
		}

		private static int compareHotWaterTankTemperatureDescending(CompHotWaterTank left, CompHotWaterTank right)
		{
			return right.temperature_c.CompareTo(left.temperature_c);
		}

		private static int compareThermalTankTemperatureAscending(CompThermalTank left, CompThermalTank right)
		{
			return left.temperature_c.CompareTo(right.temperature_c);
		}

		private static int compareThermalTankTemperatureDescending(CompThermalTank left, CompThermalTank right)
		{
			return right.temperature_c.CompareTo(left.temperature_c);
		}

		private static int compareCoolantTankFillAscending(CompCoolantTank left, CompCoolantTank right)
		{
			return left.getFillFraction().CompareTo(right.getFillFraction());
		}

		private static int compareCoolantTankEnergyDescending(CompCoolantTank left, CompCoolantTank right)
		{
			return right.cold_energy_kj.CompareTo(left.cold_energy_kj);
		}

		private static int compareWasteStorageFillAscending(CompWasteStorage left, CompWasteStorage right)
		{
			return left.getFillFraction().CompareTo(right.getFillFraction());
		}

		private static int compareSewageOutletIdAscending(CompSewageOutlet left, CompSewageOutlet right)
		{
			return left.parent.thingIDNumber.CompareTo(right.parent.thingIDNumber);
		}

		private float addVirtualHeatingBufferEnergyTowardTemperature(
			float requested_kj,
			float target_temperature_c)
		{
			if (network_type != FluidNetworkType.Heating || virtual_heating_buffer_capacity_liters <= 0.001f)
			{
				return 0f;
			}

			float target_c = Mathf.Min(
				RealPhysics.HEATING_BUFFER_MAXIMUM_TEMPERATURE_C,
				target_temperature_c);
			float temperature_room_c = target_c - virtual_heating_buffer_temperature_c;
			if (temperature_room_c <= 0.001f)
			{
				return 0f;
			}

			float room_kj = RealPhysics.calculateWaterEnergy(
				virtual_heating_buffer_capacity_liters,
				temperature_room_c);
			float accepted_kj = Mathf.Min(Mathf.Max(0f, requested_kj), room_kj);
			virtual_heating_buffer_temperature_c += RealPhysics.calculateWaterTemperatureChange(
				accepted_kj,
				virtual_heating_buffer_capacity_liters);
			return accepted_kj;
		}

		private float drawVirtualHeatingBufferEnergyTowardTemperature(
			float requested_kj,
			float target_temperature_c)
		{
			if (network_type != FluidNetworkType.Heating || virtual_heating_buffer_capacity_liters <= 0.001f)
			{
				return 0f;
			}

			float target_c = Mathf.Max(
				RealPhysics.HEATING_BUFFER_MINIMUM_TEMPERATURE_C,
				target_temperature_c);
			float temperature_room_c = virtual_heating_buffer_temperature_c - target_c;
			if (temperature_room_c <= 0.001f)
			{
				return 0f;
			}

			float available_kj = RealPhysics.calculateWaterEnergy(
				virtual_heating_buffer_capacity_liters,
				temperature_room_c);
			float delivered_kj = Mathf.Min(Mathf.Max(0f, requested_kj), available_kj);
			virtual_heating_buffer_temperature_c -= RealPhysics.calculateWaterTemperatureChange(
				delivered_kj,
				virtual_heating_buffer_capacity_liters);
			return delivered_kj;
		}
	}
}
