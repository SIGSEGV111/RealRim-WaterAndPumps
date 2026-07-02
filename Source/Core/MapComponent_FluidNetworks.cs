using System.Collections.Generic;
using System.Linq;
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

		private readonly List<CompFluidNode> nodes = new List<CompFluidNode>();
		private readonly Dictionary<FluidNetworkType, Dictionary<CompFluidNode, FluidNetwork>> network_by_node =
			new Dictionary<FluidNetworkType, Dictionary<CompFluidNode, FluidNetwork>>();
		private readonly Dictionary<FluidNetworkType, List<FluidNetwork>> networks_by_type =
			new Dictionary<FluidNetworkType, List<FluidNetwork>>();
		private bool networks_dirty = true;

		public MapComponent_FluidNetworks(Map map) : base(map)
		{
		}

		public override void MapComponentTick()
		{
			base.MapComponentTick();
			if (networks_dirty)
			{
				rebuildNetworks();
			}

			if (Find.TickManager.TicksGame % SYSTEM_TICK_INTERVAL == 0)
			{
				tickSystems(SYSTEM_TICK_INTERVAL * RealPhysics.SECONDS_PER_GAME_TICK);
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

		public List<FluidNetwork> getNetworks(FluidNetworkType network_type)
		{
			if (networks_dirty)
			{
				rebuildNetworks();
			}

			List<FluidNetwork> result;
			return networks_by_type.TryGetValue(network_type, out result)
				? result
				: new List<FluidNetwork>();
		}


		public List<CompFluidNode> getAllActiveNodes(FluidNetworkType network_type)
		{
			if (networks_dirty)
			{
				rebuildNetworks();
			}

			return nodes
				.Where(node => node.supportsNetwork(network_type) && node.isConnectionActive())
				.ToList();
		}

		private void rebuildNetworks()
		{
			nodes.RemoveAll(node => node == null || node.parent == null || !node.parent.Spawned);
			network_by_node.Clear();
			networks_by_type.Clear();

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
			List<FluidNetwork> networks = new List<FluidNetwork>();
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
								if (unvisited.Remove(neighbor))
								{
									queue.Enqueue(neighbor);
								}
							}
						}
					}
				}

				FluidNetwork network = new FluidNetwork(network_id++, network_type, connected);
				networks.Add(network);
				for (int index = 0; index < connected.Count; index++)
				{
					lookup[connected[index]] = network;
				}
			}

			network_by_node[network_type] = lookup;
			networks_by_type[network_type] = networks;
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
	}
}
