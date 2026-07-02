using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class Dialog_HeatingNetworkReport : Window
	{
		private readonly ThingWithComps origin;
		private Vector2 scroll_position;

		public override Vector2 InitialSize
		{
			get
			{
				return new Vector2(820f, 720f);
			}
		}

		public Dialog_HeatingNetworkReport(ThingWithComps origin)
		{
			this.origin = origin;
			doCloseX = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = true;
		}

		public override void DoWindowContents(Rect in_rect)
		{
			FluidNetwork network = getNetwork();
			Text.Font = GameFont.Medium;
			string title = network == null
				? "RealRim_HeatingReportTitleDisconnected".Translate().ToString()
				: "RealRim_HeatingReportTitle".Translate(network.network_id).ToString();
			Widgets.Label(new Rect(0f, 0f, in_rect.width, 38f), title);
			Text.Font = GameFont.Small;

			Rect out_rect = new Rect(0f, 46f, in_rect.width, in_rect.height - 102f);
			string report = network == null
				? "RealRim_HeatingReportDisconnected".Translate().ToString()
				: HeatingNetworkReportBuilder.build(network);
			float view_width = Mathf.Max(100f, out_rect.width - 18f);
			float view_height = Mathf.Max(
				out_rect.height,
				Text.CalcHeight(report, view_width - 12f) + 16f);
			Rect view_rect = new Rect(0f, 0f, view_width, view_height);
			Widgets.BeginScrollView(out_rect, ref scroll_position, view_rect);
			Widgets.Label(new Rect(6f, 4f, view_width - 12f, view_height - 8f), report);
			Widgets.EndScrollView();

			if (Widgets.ButtonText(
				new Rect(in_rect.width - 120f, in_rect.height - 42f, 120f, 38f),
				"CloseButton".Translate()))
			{
				Close();
			}
		}

		private FluidNetwork getNetwork()
		{
			CompFluidNode node = origin?.TryGetComp<CompFluidNode>();
			if (node == null || !node.supportsNetwork(FluidNetworkType.Heating))
			{
				return null;
			}
			return node.getManager()?.getNetwork(node, FluidNetworkType.Heating);
		}
	}

	internal static class HeatingNetworkReportBuilder
	{
		private sealed class ReportEntry
		{
			public ThingWithComps thing;
			public string base_label;
			public string details;
			public float production_kw;
			public float consumption_kw;
		}

		private sealed class RoomGroup
		{
			public Room room;
			public string label;
			public int area_m2;
			public readonly List<ReportEntry> entries = new List<ReportEntry>();
		}

		public static string build(FluidNetwork network)
		{
			if (network == null)
			{
				return "RealRim_HeatingReportDisconnected".Translate().ToString();
			}

			List<RoomGroup> groups = collectGroups(network);
			float total_production_kw = groups.Sum(group => group.entries.Sum(entry => entry.production_kw));
			float total_consumption_kw = groups.Sum(group => group.entries.Sum(entry => entry.consumption_kw));
			int pipe_length_m = getPipeLengthMeters(network);
			StringBuilder report = new StringBuilder();
			report.AppendLine("RealRim_HeatingReportProduction".Translate(total_production_kw.ToString("N1")).ToString());
			report.AppendLine("RealRim_HeatingReportConsumption".Translate(total_consumption_kw.ToString("N1")).ToString());
			report.AppendLine("RealRim_HeatingReportNet".Translate((total_production_kw - total_consumption_kw).ToString("+0.0;-0.0;0.0")).ToString());
			report.AppendLine("RealRim_HeatingReportPipeLength".Translate(pipe_length_m).ToString());

			Dictionary<string, int> next_index_by_label = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			for (int group_index = 0; group_index < groups.Count; group_index++)
			{
				RoomGroup group = groups[group_index];
				report.AppendLine();
				if (group.area_m2 > 0)
				{
					report.AppendLine("RealRim_HeatingReportRoomHeader".Translate(group.label, group.area_m2).ToString());
				}
				else
				{
					report.AppendLine(group.label + ":");
				}

				group.entries.Sort(compareEntries);
				for (int entry_index = 0; entry_index < group.entries.Count; entry_index++)
				{
					ReportEntry entry = group.entries[entry_index];
					int next_index;
					if (!next_index_by_label.TryGetValue(entry.base_label, out next_index))
					{
						next_index = 0;
					}
					next_index++;
					next_index_by_label[entry.base_label] = next_index;
					string numbered_label = "RealRim_HeatingReportNumberedNode".Translate(
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

			return report.ToString().TrimEnd('\r', '\n');
		}

		private static List<RoomGroup> collectGroups(FluidNetwork network)
		{
			Dictionary<Room, RoomGroup> groups_by_room = new Dictionary<Room, RoomGroup>();
			RoomGroup outdoors = null;
			for (int index = 0; index < network.nodes.Count; index++)
			{
				CompFluidNode node = network.nodes[index];
				ThingWithComps thing = node?.parent;
				if (thing == null || !thing.Spawned || isHeatingPipeInfrastructure(thing.def?.defName))
				{
					continue;
				}

				ReportEntry entry = createEntry(thing);
				Room room = thing.GetRoom();
				if (room == null || room.PsychologicallyOutdoors)
				{
					if (outdoors == null)
					{
						outdoors = new RoomGroup
						{
							room = null,
							label = "RealRim_HeatingReportOutdoors".Translate(),
							area_m2 = 0,
						};
					}
					outdoors.entries.Add(entry);
					continue;
				}

				RoomGroup group;
				if (!groups_by_room.TryGetValue(room, out group))
				{
					group = new RoomGroup
					{
						room = room,
						label = getRoomLabel(room),
						area_m2 = room.CellCount,
					};
					groups_by_room[room] = group;
				}
				group.entries.Add(entry);
			}

			List<RoomGroup> result = groups_by_room.Values
				.OrderBy(group => group.label, StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(group => group.area_m2)
				.ToList();
			if (outdoors != null)
			{
				result.Insert(0, outdoors);
			}
			return result;
		}

		private static ReportEntry createEntry(ThingWithComps thing)
		{
			ReportEntry entry = new ReportEntry
			{
				thing = thing,
				base_label = thing.LabelCap.ToString(),
				details = "RealRim_HeatingReportConnected".Translate(),
			};

			CompHeatSource source = thing.TryGetComp<CompHeatSource>();
			if (source != null)
			{
				entry.production_kw = Mathf.Max(0f, source.last_thermal_kw);
				if (source.hasAdjustableTarget())
				{
					entry.details = "RealRim_HeatingReportSourceDetails".Translate(
						source.last_thermal_kw.ToString("+0.0;-0.0;0.0"),
						source.target_buffer_temperature_c.ToStringTemperature("F1"),
						source.last_cop.ToString("N2"));
				}
				else
				{
					entry.details = "RealRim_HeatingReportPassiveSourceDetails".Translate(
						source.last_thermal_kw.ToString("+0.0;-0.0;0.0"));
				}
				return entry;
			}

			CompThermalTank thermal_tank = thing.TryGetComp<CompThermalTank>();
			if (thermal_tank != null)
			{
				entry.consumption_kw = Mathf.Max(0f, thermal_tank.last_heat_loss_kw);
				entry.details = "RealRim_HeatingReportThermalTankDetails".Translate(
					thermal_tank.temperature_c.ToStringTemperature("F1"),
					formatConsumptionKw(thermal_tank.last_heat_loss_kw, "N2"));
				return entry;
			}

			CompHotWaterTank hot_water_tank = thing.TryGetComp<CompHotWaterTank>();
			if (hot_water_tank != null)
			{
				entry.consumption_kw = Mathf.Max(0f, hot_water_tank.last_transfer_kw);
				entry.details = "RealRim_HeatingReportHotWaterTankDetails".Translate(
					hot_water_tank.temperature_c.ToStringTemperature("F1"),
					formatConsumptionKw(hot_water_tank.last_transfer_kw, "N1"),
					formatConsumptionKw(hot_water_tank.last_heat_loss_kw, "N2"));
				return entry;
			}

			CompRoomHeatExchanger exchanger = thing.TryGetComp<CompRoomHeatExchanger>();
			if (exchanger != null)
			{
				entry.consumption_kw = Mathf.Max(0f, exchanger.last_transfer_kw);
				entry.details = "RealRim_HeatingReportConsumerDetails".Translate(
					formatConsumptionKw(exchanger.last_transfer_kw, "N2"));
				return entry;
			}

			CompPoolPhysics pool = thing.TryGetComp<CompPoolPhysics>();
			if (pool != null)
			{
				entry.consumption_kw = Mathf.Max(0f, pool.last_heating_kw);
				entry.details = "RealRim_HeatingReportPoolDetails".Translate(
					pool.temperature_c.ToStringTemperature("F1"),
					formatConsumptionKw(pool.last_heating_kw, "N1"));
				return entry;
			}

			return entry;
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
				role_label = "RealRim_HeatingReportRoomFallback".Translate();
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
				return "RealRim_HeatingReportBedroomSingle".Translate(
					owners[0].LabelShortCap,
					role_label);
			}
			if (owners.Count == 2)
			{
				return "RealRim_HeatingReportBedroomPair".Translate(
					owners[0].LabelShortCap,
					owners[1].LabelShortCap,
					role_label);
			}
			if (owners.Count > 2)
			{
				return "RealRim_HeatingReportBedroomMany".Translate(
					owners[0].LabelShortCap,
					owners[1].LabelShortCap,
					owners.Count - 2,
					role_label);
			}
			return role_label;
		}

		private static int getPipeLengthMeters(FluidNetwork network)
		{
			HashSet<IntVec3> pipe_cells = new HashSet<IntVec3>();
			for (int index = 0; index < network.nodes.Count; index++)
			{
				Thing parent = network.nodes[index]?.parent;
				if (parent == null || !isHeatingPipeInfrastructure(parent.def?.defName))
				{
					continue;
				}
				foreach (IntVec3 cell in parent.OccupiedRect())
				{
					pipe_cells.Add(cell);
				}
			}
			return pipe_cells.Count;
		}

		private static bool isHeatingPipeInfrastructure(string def_name)
		{
			return def_name == "RealRim_HeatingPipe"
				|| def_name == "RealRim_HeatingPipeHidden"
				|| def_name == "RealRim_HeatingValve";
		}

		private static string formatConsumptionKw(float consumption_kw, string format)
		{
			float magnitude_kw = Mathf.Max(0f, consumption_kw);
			float signed_kw = magnitude_kw <= 0.0005f ? 0f : -magnitude_kw;
			return signed_kw.ToString(format);
		}

		private static int compareEntries(ReportEntry left, ReportEntry right)
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
