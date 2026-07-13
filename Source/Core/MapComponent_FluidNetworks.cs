using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class MapComponent_FluidNetworks : MapComponent
	{
		private const int SYSTEM_TICK_INTERVAL = 60;
		private const int CONSTRUCTION_PLAN_PRUNE_INTERVAL = 600;

		private static readonly IntVec3[] CONNECTION_OFFSETS =
		{
			IntVec3.Zero,
			IntVec3.North,
			IntVec3.East,
			IntVec3.South,
			IntVec3.West,
		};

		private static readonly FluidNetworkType[] NETWORK_TYPES =
		{
			FluidNetworkType.FreshWater,
			FluidNetworkType.HotWater,
			FluidNetworkType.Heating,
			FluidNetworkType.WasteWater,
			FluidNetworkType.Coolant,
		};

		private readonly List<CompFluidNode> nodes = new List<CompFluidNode>();
		private readonly Dictionary<FluidNetworkType, Dictionary<CompFluidNode, FluidNetwork>> network_by_node =
			new Dictionary<FluidNetworkType, Dictionary<CompFluidNode, FluidNetwork>>();
		private readonly Dictionary<string, float> heating_buffer_temperature_by_key =
			new Dictionary<string, float>();
		private readonly List<IFluidTickable> all_fluid_tickables = new List<IFluidTickable>();
		private readonly List<IFluidTickable> fluid_tickables = new List<IFluidTickable>();
		private readonly List<CompFloorHeating> floor_heatings = new List<CompFloorHeating>();
		private readonly List<CompRainwaterCollector> rainwater_collectors =
			new List<CompRainwaterCollector>();
		private readonly List<FluidNetwork> heating_networks = new List<FluidNetwork>();
		private readonly List<FluidNetwork> heat_exchange_networks = new List<FluidNetwork>();
		private readonly HashSet<ThingComp> cached_tickable_components = new HashSet<ThingComp>();
		private readonly HashSet<FluidNetwork> cached_networks = new HashSet<FluidNetwork>();
		private readonly HashSet<string> active_heating_buffer_keys = new HashSet<string>();
		private readonly List<string> stale_heating_buffer_keys = new List<string>();
		private List<string> saved_heating_buffer_keys = new List<string>();
		private List<float> saved_heating_buffer_temperatures_c = new List<float>();
		private List<FluidLayerConstructionPlan> construction_plans = new List<FluidLayerConstructionPlan>();
		private bool networks_dirty = true;
		private bool fluid_tickable_schedule_dirty = true;

		public MapComponent_FluidNetworks(Map map) : base(map)
		{
		}

		public override void MapComponentTick()
		{
			base.MapComponentTick();
			int current_tick = Find.TickManager.TicksGame;
			if (current_tick % CONSTRUCTION_PLAN_PRUNE_INTERVAL == 0)
			{
				pruneConstructionPlans();
			}
			if (networks_dirty)
			{
				rebuildNetworks();
			}

			if (current_tick % SYSTEM_TICK_INTERVAL == 0)
			{
				float elapsed_seconds = SYSTEM_TICK_INTERVAL * RealPhysics.SECONDS_PER_GAME_TICK;
				tickSystems(elapsed_seconds);
				tickNetworkHeatExchange(elapsed_seconds);
				cacheCurrentHeatingBufferStates();
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				cacheCurrentHeatingBufferStates();
				saved_heating_buffer_keys = heating_buffer_temperature_by_key.Keys.ToList();
				saved_heating_buffer_temperatures_c = saved_heating_buffer_keys
					.Select(key => heating_buffer_temperature_by_key[key])
					.ToList();
			}

			Scribe_Collections.Look(
				ref saved_heating_buffer_keys,
				"virtual_heating_buffer_keys",
				LookMode.Value);
			Scribe_Collections.Look(
				ref saved_heating_buffer_temperatures_c,
				"virtual_heating_buffer_temperatures_c",
				LookMode.Value);

			Scribe_Collections.Look(
				ref construction_plans,
				"fluid_layer_construction_plans",
				LookMode.Deep);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (construction_plans == null)
				{
					construction_plans = new List<FluidLayerConstructionPlan>();
				}
				heating_buffer_temperature_by_key.Clear();
				if (saved_heating_buffer_keys == null)
				{
					saved_heating_buffer_keys = new List<string>();
				}
				if (saved_heating_buffer_temperatures_c == null)
				{
					saved_heating_buffer_temperatures_c = new List<float>();
				}

				int count = Mathf.Min(saved_heating_buffer_keys.Count, saved_heating_buffer_temperatures_c.Count);
				for (int index = 0; index < count; index++)
				{
					string key = saved_heating_buffer_keys[index];
					if (key.NullOrEmpty())
					{
						continue;
					}

					heating_buffer_temperature_by_key[key] = Mathf.Clamp(
						saved_heating_buffer_temperatures_c[index],
						RealPhysics.HEATING_BUFFER_MINIMUM_TEMPERATURE_C,
						RealPhysics.HEATING_BUFFER_MAXIMUM_TEMPERATURE_C);
				}
				networks_dirty = true;
			}
		}

		public void registerNode(CompFluidNode node)
		{
			if (node != null && !nodes.Contains(node))
			{
				nodes.Add(node);
				networks_dirty = true;
			}
		}

		public void deregisterNode(CompFluidNode node)
		{
			if (node != null && nodes.Remove(node))
			{
				networks_dirty = true;
			}
		}

		public void markNetworksDirty()
		{
			networks_dirty = true;
		}

		public FluidNetwork getNetwork(CompFluidNode node, FluidNetworkType network_type)
		{
			if (networks_dirty)
			{
				rebuildNetworks();
			}

			Dictionary<CompFluidNode, FluidNetwork> lookup;
			FluidNetwork network;
			return network_by_node.TryGetValue(network_type, out lookup)
				&& lookup.TryGetValue(node, out network)
				? network
				: null;
		}

		public List<CompFluidNode> getAllActiveNodes(FluidNetworkType network_type)
		{
			return getAllActiveNodes(network_type, FluidNetworkLayer.None);
		}

		public List<CompFluidNode> getAllActiveNodes(
			FluidNetworkType network_type,
			FluidNetworkLayer layer)
		{
			if (networks_dirty)
			{
				rebuildNetworks();
			}

			List<CompFluidNode> result = new List<CompFluidNode>();
			for (int index = 0; index < nodes.Count; index++)
			{
				CompFluidNode node = nodes[index];
				if (node.supportsNetwork(network_type)
					&& node.isConnectionActive()
					&& (layer == FluidNetworkLayer.None
						|| node.isLayerConnector(network_type)
						|| node.getLayer(network_type) == layer))
				{
					result.Add(node);
				}
			}
			return result;
		}

		public FluidLayerConstructionPlan consumeConstructionPlan(CompFluidNode node)
		{
			if (node == null || construction_plans == null)
			{
				return null;
			}

			for (int index = 0; index < construction_plans.Count; index++)
			{
				FluidLayerConstructionPlan plan = construction_plans[index];
				if (plan != null && plan.matches(node))
				{
					construction_plans.RemoveAt(index);
					return plan;
				}
			}

			return null;
		}

		public void recordConstructionPlan(
			ThingDef build_def,
			IntVec3 position,
			Rot4 rotation)
		{
			if (build_def == null
				|| !position.IsValid
				|| FluidNetworkVisuals.getNodeProperties(build_def) == null)
			{
				return;
			}

			if (construction_plans == null)
			{
				construction_plans = new List<FluidLayerConstructionPlan>();
			}

			for (int index = construction_plans.Count - 1; index >= 0; index--)
			{
				FluidLayerConstructionPlan plan = construction_plans[index];
				if (plan != null && plan.matchesConstructionBuild(build_def, position, rotation))
				{
					construction_plans.RemoveAt(index);
				}
			}

			construction_plans.Add(FluidLayerConstructionPlan.create(build_def, position, rotation));
		}

		public FluidNetworkLayer getConstructionPlanLayer(
			Thing construction,
			FluidNetworkType network_type)
		{
			if (construction == null)
			{
				return FluidNetworkLayer.None;
			}

			ThingDef build_def = construction.def?.entityDefToBuild as ThingDef;
			if (construction_plans != null)
			{
				for (int index = 0; index < construction_plans.Count; index++)
				{
					FluidLayerConstructionPlan plan = construction_plans[index];
					if (plan != null
						&& (plan.matchesConstruction(construction)
							|| plan.matchesConstructionBuild(construction, build_def)))
					{
						return plan.getLayer(network_type);
					}
				}
			}

			CompProperties_FluidNode properties = FluidNetworkVisuals.getNodeProperties(build_def);
			return properties?.networks != null && properties.networks.Contains(network_type)
				? FluidNetworkLayerSettings.getSelectedLayer(network_type)
				: FluidNetworkLayer.None;
		}

		private void rebuildNetworks()
		{
			cacheCurrentHeatingBufferStates();
			for (int index = nodes.Count - 1; index >= 0; index--)
			{
				CompFluidNode node = nodes[index];
				if (node == null || node.parent == null || !node.parent.Spawned)
				{
					nodes.RemoveAt(index);
				}
			}
			network_by_node.Clear();

			for (int type_index = 0; type_index < NETWORK_TYPES.Length; type_index++)
			{
				rebuildNetworkType(NETWORK_TYPES[type_index]);
			}

			rebuildRuntimeCaches();
			networks_dirty = false;
		}

		private void rebuildNetworkType(FluidNetworkType network_type)
		{
			List<CompFluidNode> candidates = new List<CompFluidNode>();
			for (int index = 0; index < nodes.Count; index++)
			{
				CompFluidNode node = nodes[index];
				if (node.supportsNetwork(network_type) && node.isConnectionActive())
				{
					candidates.Add(node);
				}
			}
			Dictionary<IntVec3, List<CompFluidNode>> cell_index = buildCellIndex(candidates);
			HashSet<CompFluidNode> unvisited = new HashSet<CompFluidNode>(candidates);
			Dictionary<CompFluidNode, FluidNetwork> lookup = new Dictionary<CompFluidNode, FluidNetwork>();
			int network_id = 1;

			while (unvisited.Count > 0)
			{
				CompFluidNode first = null;
				foreach (CompFluidNode candidate in unvisited)
				{
					first = candidate;
					break;
				}
				Queue<CompFluidNode> queue = new Queue<CompFluidNode>();
				List<CompFluidNode> connected = new List<CompFluidNode>();
				queue.Enqueue(first);
				unvisited.Remove(first);

				while (queue.Count > 0)
				{
					CompFluidNode current = queue.Dequeue();
					connected.Add(current);
					if (current.Props.transfer_only)
					{
						continue;
					}

					foreach (IntVec3 occupied_cell in current.parent.OccupiedRect())
					{
						for (int offset_index = 0; offset_index < CONNECTION_OFFSETS.Length; offset_index++)
						{
							List<CompFluidNode> neighbors;
							if (!cell_index.TryGetValue(occupied_cell + CONNECTION_OFFSETS[offset_index], out neighbors))
							{
								continue;
							}

							for (int neighbor_index = 0; neighbor_index < neighbors.Count; neighbor_index++)
							{
								CompFluidNode neighbor = neighbors[neighbor_index];
								if (neighbor.Props.transfer_only)
								{
									continue;
								}

								if (current.canConnectTo(neighbor, network_type, CONNECTION_OFFSETS[offset_index])
									&& unvisited.Remove(neighbor))
								{
									queue.Enqueue(neighbor);
								}
							}
						}
					}
				}

				string state_key = buildNetworkStateKey(network_type, connected);
				float virtual_heating_buffer_temperature_c = getVirtualHeatingBufferTemperature(
					network_type,
					state_key,
					connected);
				FluidNetwork network = new FluidNetwork(
					network_id++,
					network_type,
					connected,
					state_key,
					virtual_heating_buffer_temperature_c);
				for (int index = 0; index < connected.Count; index++)
				{
					lookup[connected[index]] = network;
				}
			}

			network_by_node[network_type] = lookup;
		}

		private static Dictionary<IntVec3, List<CompFluidNode>> buildCellIndex(List<CompFluidNode> candidates)
		{
			Dictionary<IntVec3, List<CompFluidNode>> result = new Dictionary<IntVec3, List<CompFluidNode>>();
			for (int index = 0; index < candidates.Count; index++)
			{
				CompFluidNode node = candidates[index];
				foreach (IntVec3 cell in node.parent.OccupiedRect())
				{
					List<CompFluidNode> cell_nodes;
					if (!result.TryGetValue(cell, out cell_nodes))
					{
						cell_nodes = new List<CompFluidNode>();
						result[cell] = cell_nodes;
					}

					cell_nodes.Add(node);
				}
			}

			return result;
		}

		private static string buildNetworkStateKey(
			FluidNetworkType network_type,
			List<CompFluidNode> connected)
		{
			return network_type.ToString() + ":" + string.Join(
				",",
				connected
					.Where(node => node?.parent != null)
					.Select(node => node.parent.thingIDNumber)
					.OrderBy(thing_id => thing_id)
					.Select(thing_id => thing_id.ToString())
					.ToArray());
		}

		private float getVirtualHeatingBufferTemperature(
			FluidNetworkType network_type,
			string state_key,
			List<CompFluidNode> connected)
		{
			if (network_type != FluidNetworkType.Heating || state_key.NullOrEmpty())
			{
				return RealPhysics.HEATING_BUFFER_INITIAL_TEMPERATURE_C;
			}

			float exact_temperature_c;
			if (heating_buffer_temperature_by_key.TryGetValue(state_key, out exact_temperature_c))
			{
				return exact_temperature_c;
			}

			HashSet<int> node_ids = new HashSet<int>(connected
				.Where(node => node?.parent != null)
				.Select(node => node.parent.thingIDNumber));
			float weighted_temperature_c = 0f;
			int total_weight = 0;
			foreach (KeyValuePair<string, float> entry in heating_buffer_temperature_by_key)
			{
				int overlap = countNodeIdOverlap(entry.Key, node_ids);
				if (overlap <= 0)
				{
					continue;
				}

				weighted_temperature_c += entry.Value * overlap;
				total_weight += overlap;
			}

			if (total_weight <= 0)
			{
				return RealPhysics.HEATING_BUFFER_INITIAL_TEMPERATURE_C;
			}

			return weighted_temperature_c / total_weight;
		}

		private static int countNodeIdOverlap(string state_key, HashSet<int> node_ids)
		{
			if (state_key.NullOrEmpty() || node_ids == null || node_ids.Count == 0)
			{
				return 0;
			}

			int separator_index = state_key.IndexOf(':');
			if (separator_index < 0 || separator_index + 1 >= state_key.Length)
			{
				return 0;
			}

			int overlap = 0;
			string[] parts = state_key.Substring(separator_index + 1).Split(',');
			for (int index = 0; index < parts.Length; index++)
			{
				int thing_id;
				if (int.TryParse(parts[index], out thing_id) && node_ids.Contains(thing_id))
				{
					overlap++;
				}
			}

			return overlap;
		}

		private void pruneConstructionPlans()
		{
			if (construction_plans == null || construction_plans.Count == 0)
			{
				return;
			}

			for (int index = construction_plans.Count - 1; index >= 0; index--)
			{
				FluidLayerConstructionPlan plan = construction_plans[index];
				if (plan == null || !hasActiveConstruction(plan))
				{
					construction_plans.RemoveAt(index);
				}
			}
		}

		private bool hasActiveConstruction(FluidLayerConstructionPlan plan)
		{
			if (plan == null || !plan.position.IsValid || !plan.position.InBounds(map))
			{
				return false;
			}

			List<Thing> things = plan.position.GetThingList(map);
			for (int index = 0; index < things.Count; index++)
			{
				Thing construction = things[index];
				if (!(construction is Frame) && !(construction is Blueprint))
				{
					continue;
				}

				ThingDef build_def = construction.def?.entityDefToBuild as ThingDef;
				if (plan.matchesConstruction(construction)
					|| plan.matchesConstructionBuild(construction, build_def))
				{
					return true;
				}
			}

			return false;
		}

		private void cacheCurrentHeatingBufferStates()
		{
			if (!network_by_node.ContainsKey(FluidNetworkType.Heating))
			{
				return;
			}

			active_heating_buffer_keys.Clear();
			for (int index = 0; index < heating_networks.Count; index++)
			{
				FluidNetwork network = heating_networks[index];
				if (network == null || network.state_key.NullOrEmpty())
				{
					continue;
				}

				active_heating_buffer_keys.Add(network.state_key);
				heating_buffer_temperature_by_key[network.state_key] = network.virtual_heating_buffer_temperature_c;
			}

			stale_heating_buffer_keys.Clear();
			foreach (string key in heating_buffer_temperature_by_key.Keys)
			{
				if (!active_heating_buffer_keys.Contains(key))
				{
					stale_heating_buffer_keys.Add(key);
				}
			}
			for (int index = 0; index < stale_heating_buffer_keys.Count; index++)
			{
				heating_buffer_temperature_by_key.Remove(stale_heating_buffer_keys[index]);
			}
		}

		private void rebuildRuntimeCaches()
		{
			all_fluid_tickables.Clear();
			fluid_tickables.Clear();
			floor_heatings.Clear();
			rainwater_collectors.Clear();
			cached_tickable_components.Clear();
			for (int node_index = 0; node_index < nodes.Count; node_index++)
			{
				ThingWithComps parent = nodes[node_index].parent;
				if (parent?.AllComps == null)
				{
					continue;
				}

				for (int comp_index = 0; comp_index < parent.AllComps.Count; comp_index++)
				{
					ThingComp component = parent.AllComps[comp_index];
					IFluidTickable tickable = component as IFluidTickable;
					if (tickable != null && cached_tickable_components.Add(component))
					{
						all_fluid_tickables.Add(tickable);
						CompFloorHeating floor_heating = component as CompFloorHeating;
						if (floor_heating != null)
						{
							floor_heatings.Add(floor_heating);
						}

						CompRainwaterCollector rainwater_collector = component as CompRainwaterCollector;
						if (rainwater_collector != null)
						{
							rainwater_collectors.Add(rainwater_collector);
						}
					}
				}
			}
			fluid_tickable_schedule_dirty = true;

			heating_networks.Clear();
			collectUniqueNetworks(FluidNetworkType.Heating, heating_networks, true);

			heat_exchange_networks.Clear();
			cached_networks.Clear();
			collectUniqueNetworks(FluidNetworkType.Heating, heat_exchange_networks, false);
			collectUniqueNetworks(FluidNetworkType.HotWater, heat_exchange_networks, false);
		}

		private void collectUniqueNetworks(
			FluidNetworkType network_type,
			List<FluidNetwork> result,
			bool clear_cache)
		{
			if (clear_cache)
			{
				cached_networks.Clear();
			}

			Dictionary<CompFluidNode, FluidNetwork> lookup;
			if (!network_by_node.TryGetValue(network_type, out lookup))
			{
				return;
			}

			foreach (FluidNetwork network in lookup.Values)
			{
				if (network != null && cached_networks.Add(network))
				{
					result.Add(network);
				}
			}
		}

		private void tickSystems(float elapsed_seconds)
		{
			bool floor_heating_groups_rebuilt = FloorHeatingUtility.prepareFloorHeatingGroups(
				map,
				floor_heatings,
				fluid_tickable_schedule_dirty);
			if (fluid_tickable_schedule_dirty || floor_heating_groups_rebuilt)
			{
				rebuildFluidTickableSchedule();
				fluid_tickable_schedule_dirty = false;
			}

			CompRainwaterCollector.prepareCollectionCache(map, rainwater_collectors);
			for (int index = 0; index < fluid_tickables.Count; index++)
			{
				fluid_tickables[index].tickFluidSystem(elapsed_seconds);
			}
		}

		private void rebuildFluidTickableSchedule()
		{
			fluid_tickables.Clear();
			for (int index = 0; index < all_fluid_tickables.Count; index++)
			{
				IFluidTickable tickable = all_fluid_tickables[index];
				CompFloorHeating floor_heating = tickable as CompFloorHeating;
				if (floor_heating == null
					|| FloorHeatingUtility.isFloorHeatingScheduled(map, floor_heating))
				{
					fluid_tickables.Add(tickable);
				}
			}
		}

		private void tickNetworkHeatExchange(float elapsed_seconds)
		{
			for (int index = 0; index < heat_exchange_networks.Count; index++)
			{
				heat_exchange_networks[index].tickOutdoorHeatExchange(elapsed_seconds);
			}
		}
	}
}
