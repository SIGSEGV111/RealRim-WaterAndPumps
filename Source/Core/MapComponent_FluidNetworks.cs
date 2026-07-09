using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class MapComponent_FluidNetworks : MapComponent
	{
		private const int SYSTEM_TICK_INTERVAL = 60;

		private static readonly IntVec3[] CONNECTION_OFFSETS =
		{
			IntVec3.Zero,
			IntVec3.North,
			IntVec3.East,
			IntVec3.South,
			IntVec3.West,
		};

		private static readonly FluidNetworkType[] HEAT_EXCHANGE_NETWORK_TYPES =
		{
			FluidNetworkType.Heating,
			FluidNetworkType.HotWater,
		};

		private readonly List<CompFluidNode> nodes = new List<CompFluidNode>();
		private readonly Dictionary<FluidNetworkType, Dictionary<CompFluidNode, FluidNetwork>> network_by_node =
			new Dictionary<FluidNetworkType, Dictionary<CompFluidNode, FluidNetwork>>();
		private readonly Dictionary<string, float> heating_buffer_temperature_by_key =
			new Dictionary<string, float>();
		private List<string> saved_heating_buffer_keys = new List<string>();
		private List<float> saved_heating_buffer_temperatures_c = new List<float>();
		private List<FluidLayerConstructionPlan> construction_plans = new List<FluidLayerConstructionPlan>();
		private bool networks_dirty = true;

		public MapComponent_FluidNetworks(Map map) : base(map)
		{
		}

		public override void MapComponentTick()
		{
			base.MapComponentTick();
			captureConstructionPlans();
			if (networks_dirty)
			{
				rebuildNetworks();
			}

			if (Find.TickManager.TicksGame % SYSTEM_TICK_INTERVAL == 0)
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

			return nodes
				.Where(node => node.supportsNetwork(network_type)
					&& node.isConnectionActive()
					&& (layer == FluidNetworkLayer.None
						|| node.isLayerConnector(network_type)
						|| node.getLayer(network_type) == layer))
				.ToList();
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
			nodes.RemoveAll(node => node == null || node.parent == null || !node.parent.Spawned);
			network_by_node.Clear();

			FluidNetworkType[] network_types = (FluidNetworkType[])System.Enum.GetValues(typeof(FluidNetworkType));
			for (int type_index = 0; type_index < network_types.Length; type_index++)
			{
				rebuildNetworkType(network_types[type_index]);
			}

			networks_dirty = false;
		}

		private void rebuildNetworkType(FluidNetworkType network_type)
		{
			List<CompFluidNode> candidates = nodes
				.Where(node => node.supportsNetwork(network_type) && node.isConnectionActive())
				.ToList();
			Dictionary<IntVec3, List<CompFluidNode>> cell_index = buildCellIndex(candidates);
			HashSet<CompFluidNode> unvisited = new HashSet<CompFluidNode>(candidates);
			Dictionary<CompFluidNode, FluidNetwork> lookup = new Dictionary<CompFluidNode, FluidNetwork>();
			int network_id = 1;

			while (unvisited.Count > 0)
			{
				CompFluidNode first = unvisited.First();
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

		private void captureConstructionPlans()
		{
			if (construction_plans == null)
			{
				construction_plans = new List<FluidLayerConstructionPlan>();
			}

			List<Thing> things = map.listerThings.AllThings;
			for (int index = 0; index < things.Count; index++)
			{
				Thing construction = things[index];
				ThingDef build_def = construction?.def?.entityDefToBuild as ThingDef;
				if (build_def == null || FluidNetworkVisuals.getNodeProperties(build_def) == null)
				{
					continue;
				}

				FluidLayerConstructionPlan existing_plan = getConstructionPlan(construction, build_def);
				if (existing_plan != null)
				{
					existing_plan.updateConstructionThing(construction);
					continue;
				}

				construction_plans.Add(FluidLayerConstructionPlan.create(construction, build_def));
			}

			pruneConstructionPlans(things);
		}

		private FluidLayerConstructionPlan getConstructionPlan(Thing construction, ThingDef build_def)
		{
			for (int index = 0; index < construction_plans.Count; index++)
			{
				FluidLayerConstructionPlan plan = construction_plans[index];
				if (plan != null
					&& (plan.matchesConstruction(construction)
						|| plan.matchesConstructionBuild(construction, build_def)))
				{
					return plan;
				}
			}
			return null;
		}

		private void pruneConstructionPlans(List<Thing> things)
		{
			HashSet<int> construction_ids = new HashSet<int>();
			for (int index = 0; index < things.Count; index++)
			{
				Thing construction = things[index];
				if (construction?.def?.entityDefToBuild != null)
				{
					construction_ids.Add(construction.thingIDNumber);
				}
			}

			construction_plans.RemoveAll(plan => plan == null
				|| (plan.construction_thing_id != 0
					&& !construction_ids.Contains(plan.construction_thing_id)));
		}

		private void cacheCurrentHeatingBufferStates()
		{
			Dictionary<CompFluidNode, FluidNetwork> lookup;
			if (!network_by_node.TryGetValue(FluidNetworkType.Heating, out lookup))
			{
				return;
			}

			HashSet<FluidNetwork> recorded_networks = new HashSet<FluidNetwork>();
			HashSet<string> active_keys = new HashSet<string>();
			foreach (FluidNetwork network in lookup.Values)
			{
				if (network == null
					|| network.state_key.NullOrEmpty()
					|| !recorded_networks.Add(network))
				{
					continue;
				}

				active_keys.Add(network.state_key);
				heating_buffer_temperature_by_key[network.state_key] = network.virtual_heating_buffer_temperature_c;
			}

			List<string> stale_keys = heating_buffer_temperature_by_key.Keys
				.Where(key => !active_keys.Contains(key))
				.ToList();
			for (int index = 0; index < stale_keys.Count; index++)
			{
				heating_buffer_temperature_by_key.Remove(stale_keys[index]);
			}
		}

		private void tickSystems(float elapsed_seconds)
		{
			HashSet<ThingComp> ticked_components = new HashSet<ThingComp>();
			for (int node_index = 0; node_index < nodes.Count; node_index++)
			{
				ThingWithComps parent = nodes[node_index].parent;
				if (parent == null || parent.AllComps == null)
				{
					continue;
				}

				for (int comp_index = 0; comp_index < parent.AllComps.Count; comp_index++)
				{
					ThingComp component = parent.AllComps[comp_index];
					IFluidTickable tickable = component as IFluidTickable;
					if (tickable != null && ticked_components.Add(component))
					{
						tickable.tickFluidSystem(elapsed_seconds);
					}
				}
			}
		}

		private void tickNetworkHeatExchange(float elapsed_seconds)
		{
			HashSet<FluidNetwork> ticked_networks = new HashSet<FluidNetwork>();
			for (int type_index = 0; type_index < HEAT_EXCHANGE_NETWORK_TYPES.Length; type_index++)
			{
				Dictionary<CompFluidNode, FluidNetwork> lookup;
				if (!network_by_node.TryGetValue(HEAT_EXCHANGE_NETWORK_TYPES[type_index], out lookup))
				{
					continue;
				}

				foreach (FluidNetwork network in lookup.Values)
				{
					if (ticked_networks.Add(network))
					{
						network.tickOutdoorHeatExchange(elapsed_seconds);
					}
				}
			}
		}
	}
}
