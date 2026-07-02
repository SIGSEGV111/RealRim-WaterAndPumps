using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class Dialog_HotWaterNetworkReport : Window
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

		public Dialog_HotWaterNetworkReport(ThingWithComps origin)
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
				? "RealRim_HotWaterReportTitleDisconnected".Translate().ToString()
				: "RealRim_HotWaterReportTitle".Translate(network.network_id).ToString();
			Widgets.Label(new Rect(0f, 0f, in_rect.width, 38f), title);
			Text.Font = GameFont.Small;

			Rect out_rect = new Rect(0f, 46f, in_rect.width, in_rect.height - 102f);
			string report = network == null
				? "RealRim_HotWaterReportDisconnected".Translate().ToString()
				: HotWaterNetworkReportBuilder.build(network);
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
			if (node == null || !node.supportsNetwork(FluidNetworkType.HotWater))
			{
				return null;
			}
			return node.getManager()?.getNetwork(node, FluidNetworkType.HotWater);
		}
	}

	internal static class HotWaterNetworkReportBuilder
	{
		public static string build(FluidNetwork network)
		{
			if (network == null)
			{
				return "RealRim_HotWaterReportDisconnected".Translate().ToString();
			}

			List<CompHotWaterTank> tanks = network.getComponents<CompHotWaterTank>().ToList();
			List<NetworkReportRoomGroup> groups = NetworkReportUtility.collectGroups(
				network,
				createEntry);
			float heating_input_kw = tanks.Sum(tank => Mathf.Max(0f, tank.last_transfer_kw));
			float standing_loss_kw = tanks.Sum(tank => Mathf.Max(0f, tank.last_heat_loss_kw));
			float refill_liters_per_hour = tanks.Sum(tank => Mathf.Max(0f, tank.last_refill_liters_per_hour));
			float pipe_exchange_kw = network.last_pipe_heat_exchange_kw;
			float hot_water_draw_kw = network.last_hot_water_draw_heat_kw;
			float net_heat_rate_kw = heating_input_kw
				- standing_loss_kw
				+ pipe_exchange_kw
				- hot_water_draw_kw;

			StringBuilder report = new StringBuilder();
			report.AppendLine("RealRim_HotWaterReportStorage".Translate(
				network.getStoredHotWater().ToString("N0"),
				network.getHotWaterCapacity().ToString("N0")).ToString());
			report.AppendLine("RealRim_HotWaterReportTemperature".Translate(
				network.getAverageThermalTemperature().ToStringTemperature("F1")).ToString());
			report.AppendLine("RealRim_HotWaterReportHeatingInput".Translate(
				heating_input_kw.ToString("+0.00;-0.00;0.00")).ToString());
			report.AppendLine("RealRim_HotWaterReportStandingLoss".Translate(
				formatLossKw(standing_loss_kw, "N2")).ToString());
			report.AppendLine("RealRim_NetworkReportPipeHeatExchange".Translate(
				pipe_exchange_kw.ToString("+0.00;-0.00;0.00"),
				network.pipe_heat_transfer_w_per_k.ToString("N2")).ToString());
			report.AppendLine("RealRim_HotWaterReportDraw".Translate(
				network.last_hot_water_draw_liters_per_hour.ToString("N0"),
				formatLossKw(hot_water_draw_kw, "N2")).ToString());
			report.AppendLine("RealRim_HotWaterReportNetHeatRate".Translate(
				net_heat_rate_kw.ToString("+0.00;-0.00;0.00")).ToString());
			report.AppendLine("RealRim_HotWaterReportRefill".Translate(
				refill_liters_per_hour.ToString("N0")).ToString());
			report.AppendLine("RealRim_NetworkReportOutdoorTemperature".Translate(
				network.getOutdoorTemperatureC().ToStringTemperature("F1")).ToString());
			report.AppendLine("RealRim_NetworkReportPipeLength".Translate(
				network.pipe_length_m).ToString());

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

			CompHotWaterTank tank = thing.TryGetComp<CompHotWaterTank>();
			if (tank != null)
			{
				entry.details = "RealRim_HotWaterReportTankDetails".Translate(
					tank.stored_liters.ToString("N0"),
					tank.Props.capacity_liters.ToString("N0"),
					tank.temperature_c.ToStringTemperature("F1"),
					tank.last_transfer_kw.ToString("+0.00;-0.00;0.00"),
					tank.last_refill_liters_per_hour.ToString("N0"),
					formatLossKw(tank.last_heat_loss_kw, "N2"));
				return entry;
			}

			CompFixture fixture = thing.TryGetComp<CompFixture>();
			if (fixture != null)
			{
				entry.details = "RealRim_HotWaterReportFixtureDetails".Translate(
					fixture.last_water_temperature_c.ToStringTemperature("F1"),
					fixture.total_water_used_liters.ToString("N1"));
				return entry;
			}

			return entry;
		}

		private static string formatLossKw(float loss_kw, string format)
		{
			float magnitude_kw = Mathf.Max(0f, loss_kw);
			float signed_kw = magnitude_kw <= 0.0005f ? 0f : -magnitude_kw;
			return signed_kw.ToString(format);
		}
	}
}
