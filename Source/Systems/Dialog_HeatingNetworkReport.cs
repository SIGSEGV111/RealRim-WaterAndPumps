using System.Collections.Generic;
using System.Linq;
using System.Text;
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
		public static string build(FluidNetwork network)
		{
			if (network == null)
			{
				return "RealRim_HeatingReportDisconnected".Translate().ToString();
			}

			List<NetworkReportRoomGroup> groups = NetworkReportUtility.collectGroups(
				network,
				createEntry);
			aggregateFloorHeating(groups);
			float pipe_exchange_kw = network.last_pipe_heat_exchange_kw;
			float total_production_kw = groups.Sum(group => group.entries.Sum(entry => entry.production_kw))
				+ Mathf.Max(0f, pipe_exchange_kw)
				+ Mathf.Max(0f, network.last_mixing_valve_input_kw);
			float total_consumption_kw = groups.Sum(group => group.entries.Sum(entry => entry.consumption_kw))
				+ Mathf.Max(0f, -pipe_exchange_kw)
				+ Mathf.Max(0f, network.last_mixing_valve_output_kw);

			StringBuilder report = new StringBuilder();
			report.AppendLine("RealRim_HeatingReportProduction".Translate(
				total_production_kw.ToString("N1")).ToString());
			report.AppendLine("RealRim_HeatingReportConsumption".Translate(
				total_consumption_kw.ToString("N1")).ToString());
			report.AppendLine("RealRim_HeatingReportNet".Translate(
				(total_production_kw - total_consumption_kw).ToString("+0.0;-0.0;0.0")).ToString());
			report.AppendLine("RealRim_HeatingReportNetworkTemperature".Translate(
				network.getAverageThermalTemperature().ToStringTemperature("F1")).ToString());
			report.AppendLine("RealRim_NetworkReportOutdoorTemperature".Translate(
				network.getOutdoorTemperatureC().ToStringTemperature("F1")).ToString());
			report.AppendLine("RealRim_NetworkReportPipeLength".Translate(
				network.pipe_length_m).ToString());
			report.AppendLine("RealRim_NetworkReportPipeHeatExchange".Translate(
				pipe_exchange_kw.ToString("+0.00;-0.00;0.00"),
				network.pipe_heat_transfer_w_per_k.ToString("N2")).ToString());
			report.AppendLine("RealRim_HeatingReportMixingValveExchange".Translate(
				network.last_mixing_valve_input_kw.ToString("+0.00;-0.00;0.00"),
				network.last_mixing_valve_output_kw.ToString("-0.00;-0.00;0.00")).ToString());

			NetworkReportUtility.appendGroups(report, groups);
			return report.ToString().TrimEnd('\r', '\n');
		}

		private static NetworkReportEntry createEntry(ThingWithComps thing)
		{
			NetworkReportEntry entry = new NetworkReportEntry
			{
				thing = thing,
				base_label = thing.LabelCap.ToString(),
				details = "RealRim_NetworkReportConnected".Translate(),
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

			CompFloorHeating floor_heating = thing.TryGetComp<CompFloorHeating>();
			if (floor_heating != null)
			{
				entry.base_label = "RealRim_FloorHeatingLabel".Translate();
				entry.consumption_kw = Mathf.Max(0f, floor_heating.last_transfer_kw);
				entry.details = "RealRim_FloorHeatingReportSingleDetails".Translate(
					floor_heating.last_room_temperature_c.ToStringTemperature("F1"),
					floor_heating.last_medium_temperature_c.ToStringTemperature("F1"),
					floor_heating.Props.heat_exchanger_surface_m2.ToString("N1"),
					formatConsumptionKw(floor_heating.last_transfer_kw, "N3"));
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

		private static void aggregateFloorHeating(List<NetworkReportRoomGroup> groups)
		{
			for (int group_index = 0; group_index < groups.Count; group_index++)
			{
				NetworkReportRoomGroup group = groups[group_index];
				List<NetworkReportEntry> floor_entries = group.entries
					.Where(entry => entry.thing != null && entry.thing.TryGetComp<CompFloorHeating>() != null)
					.ToList();
				if (floor_entries.Count <= 1)
				{
					continue;
				}

				float transfer_kw = 0f;
				float surface_m2 = 0f;
				float room_temperature_sum = 0f;
				float medium_temperature_sum = 0f;
				for (int index = 0; index < floor_entries.Count; index++)
				{
					CompFloorHeating floor_heating = floor_entries[index].thing.TryGetComp<CompFloorHeating>();
					transfer_kw += Mathf.Max(0f, floor_heating.last_transfer_kw);
					surface_m2 += floor_heating.Props.heat_exchanger_surface_m2;
					room_temperature_sum += floor_heating.last_room_temperature_c;
					medium_temperature_sum += floor_heating.last_medium_temperature_c;
				}

				group.entries.RemoveAll(entry => floor_entries.Contains(entry));
				group.entries.Add(new NetworkReportEntry
				{
					thing = floor_entries[0].thing,
					base_label = "RealRim_FloorHeatingLabel".Translate(),
					consumption_kw = transfer_kw,
					details = "RealRim_FloorHeatingReportGroupDetails".Translate(
						floor_entries.Count,
						surface_m2.ToString("N1"),
						(room_temperature_sum / floor_entries.Count).ToStringTemperature("F1"),
						(medium_temperature_sum / floor_entries.Count).ToStringTemperature("F1"),
						formatConsumptionKw(transfer_kw, "N2")),
				});
			}
		}

		private static string formatConsumptionKw(float consumption_kw, string format)
		{
			float magnitude_kw = Mathf.Max(0f, consumption_kw);
			float signed_kw = magnitude_kw <= 0.0005f ? 0f : -magnitude_kw;
			return signed_kw.ToString(format);
		}
	}
}
