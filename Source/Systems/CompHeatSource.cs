using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public enum HeatSourceKind
	{
		ElectricBoiler,
		GasBoiler,
		WoodBoiler,
		SolarThermal,
		Geothermal,
		AirToWaterHeatPump,
		CoolantToWaterHeatPump,
	}

	public sealed class CompProperties_HeatSource : CompProperties
	{
		public HeatSourceKind kind;
		public float nominal_thermal_kw = 12f;
		public float nominal_power_watts;
		public float minimum_source_temperature_c = -20f;
		public float start_temperature_c = 55f;
		public float stop_temperature_c = 75f;
		public float fuel_energy_kj_per_unit;

		public CompProperties_HeatSource()
		{
			compClass = typeof(CompHeatSource);
		}
	}

	public sealed class CompHeatSource : ThingComp, IFluidTickable
	{
		public bool heating;
		public float last_thermal_kw;
		public float last_electrical_kw;
		public float last_cop = 1f;
		public string last_reason = string.Empty;

		public CompProperties_HeatSource Props
		{
			get
			{
				return (CompProperties_HeatSource)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref heating, "heating", false);
		}

		public override string CompInspectStringExtra()
		{
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.Heating);
			float temperature = network == null ? 0f : network.getAverageThermalTemperature();
			return "RealRim_HeatSourceStatus".Translate(
				heating ? "RealRim_StatusRunning".Translate() : "RealRim_StatusStandby".Translate(),
				temperature.ToStringTemperature("F1"),
				last_thermal_kw.ToString("N1"),
				last_electrical_kw.ToString("N1"),
				last_cop.ToString("N2"),
				last_reason);
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_thermal_kw = 0f;
			last_electrical_kw = 0f;
			last_cop = 1f;
			last_reason = string.Empty;

			FluidNetwork heating_network = FluidUtility.getNetwork(parent, FluidNetworkType.Heating);
			if (heating_network == null || heating_network.getThermalCapacityEnergyKj() <= 0f)
			{
				stopHeating("RealRim_ReasonNoHeatingStorage".Translate());
				return;
			}

			float tank_temperature = heating_network.getAverageThermalTemperature();
			float stop_temperature = Props.kind == HeatSourceKind.SolarThermal || Props.kind == HeatSourceKind.Geothermal
				? RealPhysics.DEFAULT_HOT_WATER_MAX_C
				: Props.stop_temperature_c;
			if (heating && tank_temperature >= stop_temperature)
			{
				heating = false;
			}
			else if (!heating && tank_temperature <= Props.start_temperature_c)
			{
				heating = true;
			}

			if (!heating)
			{
				stopHeating("RealRim_ReasonThresholdReached".Translate());
				return;
			}

			if (!FluidUtility.isPoweredOn(parent))
			{
				stopHeating("RealRim_ReasonSwitchedOff".Translate());
				return;
			}

			float output_kw = Props.nominal_thermal_kw;
			float electrical_kw = Props.nominal_power_watts / 1000f;
			float fuel_energy_kj = 0f;

			switch (Props.kind)
			{
				case HeatSourceKind.SolarThermal:
					if (parent.Position.Roofed(parent.Map))
					{
						stopHeating("RealRim_ReasonRoofed".Translate());
						return;
					}
					output_kw *= parent.Map.skyManager.CurSkyGlow;
					if (output_kw < 0.05f)
					{
						stopHeating("RealRim_ReasonNoSun".Translate());
						return;
					}
					break;
				case HeatSourceKind.AirToWaterHeatPump:
					float source_temperature = parent.AmbientTemperature;
					if (source_temperature < Props.minimum_source_temperature_c)
					{
						stopHeating("RealRim_ReasonSourceTooCold".Translate(source_temperature.ToStringTemperature("F1")));
						return;
					}
					last_cop = RealPhysics.calculateHeatPumpCop(source_temperature, Mathf.Max(tank_temperature + 5f, 35f));
					output_kw *= Mathf.Clamp01((source_temperature - Props.minimum_source_temperature_c) / 15f + 0.35f);
					electrical_kw = output_kw / last_cop;
					break;
				case HeatSourceKind.CoolantToWaterHeatPump:
					FluidNetwork coolant_network = FluidUtility.getNetwork(parent, FluidNetworkType.Coolant);
					if (coolant_network == null || coolant_network.getColdEnergySpaceKj() <= 0.001f)
					{
						stopHeating("RealRim_ReasonNoCoolantHeatCapacity".Translate());
						return;
					}
					last_cop = RealPhysics.calculateHeatPumpCop(0f, Mathf.Max(tank_temperature + 5f, 35f));
					electrical_kw = output_kw / last_cop;
					float source_energy_kj = output_kw * elapsed_seconds * (last_cop - 1f) / last_cop;
					float accepted_source_kj = coolant_network.addColdEnergy(source_energy_kj);
					float source_fraction = source_energy_kj <= 0.001f ? 0f : accepted_source_kj / source_energy_kj;
					electrical_kw *= source_fraction;
					output_kw = elapsed_seconds <= 0f
						? 0f
						: (accepted_source_kj + electrical_kw * elapsed_seconds) / elapsed_seconds;
					break;
				case HeatSourceKind.GasBoiler:
				case HeatSourceKind.WoodBoiler:
					fuel_energy_kj = output_kw * elapsed_seconds;
					break;
			}


			if (fuel_energy_kj > 0f)
			{
				float units = Props.fuel_energy_kj_per_unit <= 0f ? 0f : fuel_energy_kj / Props.fuel_energy_kj_per_unit;
				if (!FluidUtility.consumeFuel(parent, units))
				{
					stopHeating("RealRim_ReasonNoFuel".Translate());
					return;
				}
			}

			float accepted_kj = heating_network.addThermalEnergy(output_kw * elapsed_seconds);
			last_thermal_kw = elapsed_seconds <= 0f ? 0f : accepted_kj / elapsed_seconds;
			if (Props.kind == HeatSourceKind.AirToWaterHeatPump && parent.Position.Roofed(parent.Map))
			{
				float source_heat_kw = Mathf.Max(0f, last_thermal_kw - electrical_kw);
				GenTemperature.PushHeat(parent, -source_heat_kw * RealPhysics.RIMWORLD_HEAT_UNITS_PER_KW_SECOND * elapsed_seconds);
			}
			last_electrical_kw = electrical_kw;
			FluidUtility.setPowerConsumption(parent, electrical_kw * 1000f, electrical_kw > 0f);
		}

		private void stopHeating(string reason)
		{
			last_reason = reason;
			last_thermal_kw = 0f;
			last_electrical_kw = 0f;
			FluidUtility.setPowerConsumption(parent, Props.nominal_power_watts, false);
		}
	}
}
