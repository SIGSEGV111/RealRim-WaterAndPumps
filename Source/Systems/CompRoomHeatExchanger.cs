using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public enum RoomHeatExchangerKind
	{
		Radiator,
		CoolingUnit,
	}

	public sealed class CompProperties_RoomHeatExchanger : CompProperties
	{
		public RoomHeatExchangerKind kind;
		public float rated_output_kw = 1.5f;
		public float fan_power_watts;

		public CompProperties_RoomHeatExchanger()
		{
			compClass = typeof(CompRoomHeatExchanger);
		}
	}

	public sealed class CompRoomHeatExchanger : ThingComp, IFluidTickable
	{
		public float last_transfer_kw;
		public float last_room_temperature_c;
		public float last_medium_temperature_c;
		public string last_reason = string.Empty;

		public CompProperties_RoomHeatExchanger Props
		{
			get
			{
				return (CompProperties_RoomHeatExchanger)props;
			}
		}

		public override string CompInspectStringExtra()
		{
			CompTargetTemperature controller = parent.TryGetComp<CompTargetTemperature>();
			string status = "RealRim_HeatExchangerStatus".Translate(
				controller == null ? "-" : controller.target_temperature_c.ToStringTemperature("F1"),
				last_room_temperature_c.ToStringTemperature("F1"),
				last_medium_temperature_c.ToStringTemperature("F1"),
				last_transfer_kw.ToString("N2"),
				last_reason);
			return status.TrimEnd('\r', '\n', ' ', '\t');
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_transfer_kw = 0f;
			last_reason = string.Empty;
			last_room_temperature_c = GenTemperature.GetTemperatureForCell(parent.Position, parent.Map);
			CompTargetTemperature controller = parent.TryGetComp<CompTargetTemperature>();
			if (controller == null || !FluidUtility.isPoweredOn(parent))
			{
				last_reason = "RealRim_ReasonSwitchedOff".Translate();
				FluidUtility.setPowerConsumption(parent, Props.fan_power_watts, false);
				return;
			}

			if (Props.kind == RoomHeatExchangerKind.Radiator)
			{
				heatRoom(controller.target_temperature_c, elapsed_seconds);
			}
			else
			{
				coolRoom(controller.target_temperature_c, elapsed_seconds);
			}
		}

		private void heatRoom(float target_temperature_c, float elapsed_seconds)
		{
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.Heating);
			if (network == null)
			{
				last_reason = "RealRim_ReasonNoHeatingNetwork".Translate();
				return;
			}

			last_medium_temperature_c = network.getAverageThermalTemperature();
			if (last_room_temperature_c >= target_temperature_c - 0.25f)
			{
				last_reason = "RealRim_ReasonTargetReached".Translate();
				return;
			}

			float output_kw = RealPhysics.calculateRadiatorOutputKw(
				Props.rated_output_kw,
				last_medium_temperature_c,
				last_room_temperature_c);
			float delivered_kj = network.drawThermalEnergy(output_kw * elapsed_seconds);
			last_transfer_kw = elapsed_seconds <= 0f ? 0f : delivered_kj / elapsed_seconds;
			if (last_transfer_kw > 0f)
			{
				GenTemperature.PushHeat(parent, last_transfer_kw * RealPhysics.RIMWORLD_HEAT_UNITS_PER_KW_SECOND * elapsed_seconds);
			}
			else
			{
				last_reason = "RealRim_ReasonNoStoredHeat".Translate();
			}
		}

		private void coolRoom(float target_temperature_c, float elapsed_seconds)
		{
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.Coolant);
			if (network == null)
			{
				last_reason = "RealRim_ReasonNoCoolantNetwork".Translate();
				return;
			}

			last_medium_temperature_c = 0f;
			if (last_room_temperature_c <= target_temperature_c + 0.25f)
			{
				last_reason = "RealRim_ReasonTargetReached".Translate();
				FluidUtility.setPowerConsumption(parent, Props.fan_power_watts, false);
				return;
			}

			float temperature_factor = Mathf.Clamp01((last_room_temperature_c - target_temperature_c) / 10f + 0.25f);
			float requested_kw = Props.rated_output_kw * temperature_factor;
			float delivered_kj = network.drawColdEnergy(requested_kw * elapsed_seconds);
			last_transfer_kw = elapsed_seconds <= 0f ? 0f : delivered_kj / elapsed_seconds;
			FluidUtility.setPowerConsumption(parent, Props.fan_power_watts, last_transfer_kw > 0f);
			if (last_transfer_kw > 0f)
			{
				GenTemperature.PushHeat(parent, -last_transfer_kw * RealPhysics.RIMWORLD_HEAT_UNITS_PER_KW_SECOND * elapsed_seconds);
			}
			else
			{
				last_reason = "RealRim_ReasonNoStoredCold".Translate();
			}
		}
	}
}
