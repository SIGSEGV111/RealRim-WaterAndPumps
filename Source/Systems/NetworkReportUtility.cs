using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RealRim.WaterAndPumps
{
	internal sealed class NetworkReportEntry
	{
		public ThingWithComps thing;
		public string base_label;
		public string details;
		public float production_kw;
		public float consumption_kw;
	}

	internal sealed class NetworkReportRoomGroup
	{
		public string label;
		public int area_m2;
		public readonly List<NetworkReportEntry> entries = new List<NetworkReportEntry>();
	}

	internal static class NetworkReportUtility
	{
		public static List<NetworkReportRoomGroup> collectGroups(
			FluidNetwork network,
			Func<ThingWithComps, NetworkReportEntry> create_entry)
		{
			Dictionary<Room, NetworkReportRoomGroup> groups_by_room =
				new Dictionary<Room, NetworkReportRoomGroup>();
			NetworkReportRoomGroup outdoors = null;
			for (int index = 0; index < network.nodes.Count; index++)
			{
				CompFluidNode node = network.nodes[index];
				ThingWithComps thing = node?.parent;
				if (thing == null
					|| !thing.Spawned
					|| isPipeInfrastructure(thing, network.network_type))
				{
					continue;
				}

				NetworkReportEntry entry = create_entry(thing);
				if (entry == null)
				{
					continue;
				}

				Room room = thing.GetRoom();
				if (room == null || room.PsychologicallyOutdoors)
				{
					if (outdoors == null)
					{
						outdoors = new NetworkReportRoomGroup
						{
							label = "RealRim_NetworkReportOutdoors".Translate(),
							area_m2 = 0,
						};
					}
					outdoors.entries.Add(entry);
					continue;
				}

				NetworkReportRoomGroup group;
				if (!groups_by_room.TryGetValue(room, out group))
				{
					group = new NetworkReportRoomGroup
					{
						label = getRoomLabel(room),
						area_m2 = room.CellCount,
					};
					groups_by_room[room] = group;
				}
				group.entries.Add(entry);
			}

			List<NetworkReportRoomGroup> result = groups_by_room.Values
				.OrderBy(group => group.label, StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(group => group.area_m2)
				.ToList();
			if (outdoors != null)
			{
				result.Insert(0, outdoors);
			}
			return result;
		}

		public static List<NetworkReportRoomGroup> collectGroupsFromEntries(
			IEnumerable<NetworkReportEntry> entries)
		{
			Dictionary<Room, NetworkReportRoomGroup> groups_by_room =
				new Dictionary<Room, NetworkReportRoomGroup>();
			NetworkReportRoomGroup outdoors = null;
			foreach (NetworkReportEntry entry in entries)
			{
				ThingWithComps thing = entry?.thing;
				if (thing == null || !thing.Spawned)
				{
					continue;
				}

				Room room = thing.GetRoom();
				if (room == null || room.PsychologicallyOutdoors)
				{
					if (outdoors == null)
					{
						outdoors = new NetworkReportRoomGroup
						{
							label = "RealRim_NetworkReportOutdoors".Translate(),
							area_m2 = 0,
						};
					}
					outdoors.entries.Add(entry);
					continue;
				}

				NetworkReportRoomGroup group;
				if (!groups_by_room.TryGetValue(room, out group))
				{
					group = new NetworkReportRoomGroup
					{
						label = getRoomLabel(room),
						area_m2 = room.CellCount,
					};
					groups_by_room[room] = group;
				}
				group.entries.Add(entry);
			}

			List<NetworkReportRoomGroup> result = groups_by_room.Values
				.OrderBy(group => group.label, StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(group => group.area_m2)
				.ToList();
			if (outdoors != null)
			{
				result.Insert(0, outdoors);
			}
			return result;
		}

		public static void appendGroups(
			StringBuilder report,
			List<NetworkReportRoomGroup> groups)
		{
			Dictionary<string, int> next_index_by_label =
				new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			for (int group_index = 0; group_index < groups.Count; group_index++)
			{
				NetworkReportRoomGroup group = groups[group_index];
				report.AppendLine();
				if (group.area_m2 > 0)
				{
					report.AppendLine("RealRim_NetworkReportRoomHeader".Translate(
						group.label,
						group.area_m2).ToString());
				}
				else
				{
					report.AppendLine(group.label + ":");
				}

				group.entries.Sort(compareEntries);
				for (int entry_index = 0; entry_index < group.entries.Count; entry_index++)
				{
					NetworkReportEntry entry = group.entries[entry_index];
					int next_index;
					if (!next_index_by_label.TryGetValue(entry.base_label, out next_index))
					{
						next_index = 0;
					}
					next_index++;
					next_index_by_label[entry.base_label] = next_index;
					string numbered_label = "RealRim_NetworkReportNumberedNode".Translate(
						entry.base_label,
						next_index);
					if (entry.details.NullOrEmpty())
					{
						report.AppendLine("- " + numbered_label);
					}
					else
					{
						report.AppendLine("- " + numbered_label + ": " + entry.details);
					}
				}
			}
		}

		private static bool isPipeInfrastructure(
			ThingWithComps thing,
			FluidNetworkType network_type)
		{
			CompFluidNode node = thing.TryGetComp<CompFluidNode>();
			return node != null
				&& node.supportsNetwork(network_type)
				&& node.Props.outdoor_heat_exchange_w_per_m_k > 0f;
		}

		private static string getRoomLabel(Room room)
		{
			string role_def_name = room.Role?.defName;
			bool uses_bed_owners = role_def_name == "Bedroom" || role_def_name == "Barracks";
			string role_label = uses_bed_owners
				? room.Role?.label
				: room.GetRoomRoleLabel();
			if (role_label.NullOrEmpty())
			{
				role_label = "RealRim_NetworkReportRoomFallback".Translate();
			}
			role_label = role_label.CapitalizeFirst();

			if (!uses_bed_owners)
			{
				return role_label;
			}

			List<Pawn> owners = room.Owners
				.Where(owner => owner != null)
				.Distinct()
				.OrderBy(owner => owner.LabelShortCap, StringComparer.CurrentCultureIgnoreCase)
				.ToList();
			if (owners.Count == 1)
			{
				return "RealRim_NetworkReportBedroomSingle".Translate(
					owners[0].LabelShortCap,
					role_label);
			}
			if (owners.Count == 2)
			{
				return "RealRim_NetworkReportBedroomPair".Translate(
					owners[0].LabelShortCap,
					owners[1].LabelShortCap,
					role_label);
			}
			if (owners.Count > 2)
			{
				return "RealRim_NetworkReportBedroomMany".Translate(
					owners[0].LabelShortCap,
					owners[1].LabelShortCap,
					owners.Count - 2,
					role_label);
			}
			return role_label;
		}

		private static int compareEntries(NetworkReportEntry left, NetworkReportEntry right)
		{
			int label_comparison = string.Compare(
				left.base_label,
				right.base_label,
				StringComparison.CurrentCultureIgnoreCase);
			if (label_comparison != 0)
			{
				return label_comparison;
			}
			int z_comparison = left.thing.Position.z.CompareTo(right.thing.Position.z);
			return z_comparison != 0
				? z_comparison
				: left.thing.Position.x.CompareTo(right.thing.Position.x);
		}
	}
}
