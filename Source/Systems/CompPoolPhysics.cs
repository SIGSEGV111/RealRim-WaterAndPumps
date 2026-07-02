using System;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_PoolPhysics : CompProperties
	{
		public float capacity_liters = 65000f;
		public float water_surface_m2 = 45f;
		public float heat_exchanger_surface_m2 = 0.6f;
		public float refill_liters_per_hour = 5000f;
		public float initial_fill_fraction;
		public float initial_temperature_c = 12f;

		public CompProperties_PoolPhysics()
		{
			compClass = typeof(CompPoolPhysics);
		}
	}

	public sealed class CompPoolPhysics : ThingComp, IFluidTickable
	{
		private const float INDOOR_CONVECTION_W_PER_M2_K = 7f;
		private const float OUTDOOR_CONVECTION_W_PER_M2_K = 14f;
		private const float HEAT_EXCHANGER_W_PER_M2_K = 800f;
		private const float SOLAR_IRRADIANCE_KW_PER_M2 = 0.80f;
		private const float SOLAR_ABSORPTION = 0.70f;
		private const float INDOOR_EVAPORATION_COEFFICIENT = 0.10f;
		private const float OUTDOOR_EVAPORATION_COEFFICIENT = 0.25f;
		private const BindingFlags REFLECTION_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public float stored_liters;
		public float temperature_c;
		public float last_heating_kw;
		public float last_environment_kw;
		public float last_solar_kw;
		public float last_sky_kw;
		public float last_evaporation_liters_per_day;
		public float last_rain_liters_per_day;
		public float last_refill_liters_per_day;

		public CompProperties_PoolPhysics Props
		{
			get
			{
				return (CompProperties_PoolPhysics)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			if (!respawning_after_load)
			{
				stored_liters = Props.capacity_liters * Mathf.Clamp01(Props.initial_fill_fraction);
				temperature_c = Props.initial_temperature_c;
				setLegacyFlowRate(1000);
			}
			syncLegacyVisualState();
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref stored_liters, "pool_stored_liters", 0f);
			Scribe_Values.Look(ref temperature_c, "pool_temperature_c", Props.initial_temperature_c);
		}

		public override string CompInspectStringExtra()
		{
			CompTargetTemperature target = parent.TryGetComp<CompTargetTemperature>();
			return "RealRim_PoolStatus".Translate(
				stored_liters.ToString("N0"),
				Props.capacity_liters.ToString("N0"),
				(stored_liters / Props.capacity_liters).ToStringPercent(),
				temperature_c.ToStringTemperature("F1"),
				target == null ? "-" : target.target_temperature_c.ToStringTemperature("F1"),
				last_heating_kw.ToString("N1"),
				last_environment_kw.ToString("N1"),
				last_solar_kw.ToString("N1"),
				last_sky_kw.ToString("N1"),
				last_evaporation_liters_per_day.ToString("N0"),
				last_rain_liters_per_day.ToString("N0"),
				last_refill_liters_per_day.ToString("N0"),
				(getLegacyFlowRate() / 10f).ToString("N0"));
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_heating_kw = 0f;
			last_environment_kw = 0f;
			last_solar_kw = 0f;
			last_sky_kw = 0f;
			last_evaporation_liters_per_day = 0f;
			last_rain_liters_per_day = 0f;
			last_refill_liters_per_day = 0f;

			bool outdoors = !parent.Position.Roofed(parent.Map);
			float air_temperature = GenTemperature.GetTemperatureForCell(parent.Position, parent.Map);
			processRefill(elapsed_seconds);
			processRain(outdoors, air_temperature, elapsed_seconds);
			if (stored_liters <= 1f)
			{
				temperature_c = air_temperature;
				syncLegacyVisualState();
				return;
			}

			processConvection(outdoors, air_temperature, elapsed_seconds);
			processEvaporation(outdoors, air_temperature, elapsed_seconds);
			processSolar(outdoors, elapsed_seconds);
			processSkyRadiation(outdoors, air_temperature, elapsed_seconds);
			processNetworkHeating(elapsed_seconds);
			temperature_c = Mathf.Clamp(temperature_c, -2f, 95f);
			syncLegacyVisualState();
		}

		public bool canPawnUse(Pawn pawn)
		{
			if (stored_liters < Props.capacity_liters * 0.90f)
			{
				return false;
			}
			if (pawn == null)
			{
				return temperature_c >= 10f && temperature_c <= 42f;
			}

			float minimum = WaterTemperaturePreferences.getAdjustedComfortMinimum(pawn) - 5f;
			float maximum = WaterTemperaturePreferences.getAdjustedComfortMaximum(pawn) + 5f;
			return temperature_c >= minimum && temperature_c <= maximum;
		}

		public float getPawnPreferenceScore(Pawn pawn)
		{
			if (!canPawnUse(pawn))
			{
				return -10000f;
			}
			float center = (WaterTemperaturePreferences.getAdjustedComfortMinimum(pawn)
				+ WaterTemperaturePreferences.getAdjustedComfortMaximum(pawn)) * 0.5f;
			return -Mathf.Abs(temperature_c - center);
		}

		public void applyPawnTemperatureThought(Pawn pawn)
		{
			if (pawn == null || pawn.needs == null || pawn.needs.mood == null || !pawn.IsHashIntervalTick(2500))
			{
				return;
			}
			string thought_name = WaterTemperaturePreferences.selectPoolThought(pawn, temperature_c);
			if (thought_name.NullOrEmpty())
			{
				return;
			}
			ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought_name);
			if (thought != null)
			{
				pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
			}
		}

		private void processRefill(float elapsed_seconds)
		{
			float space = Props.capacity_liters - stored_liters;
			if (space <= 0f)
			{
				return;
			}
			FluidNetwork fresh_network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (fresh_network == null)
			{
				return;
			}
			float flow_fraction = Mathf.Clamp01(getLegacyFlowRate() / 1000f);
			float requested = Mathf.Min(
				space,
				Props.refill_liters_per_hour * flow_fraction * elapsed_seconds / 3600f);
			float delivered = fresh_network.drawFreshWater(requested);
			mixWater(delivered, RealPhysics.COLD_WATER_TEMPERATURE_C);
			last_refill_liters_per_day = elapsed_seconds <= 0f ? 0f : delivered * RealPhysics.SECONDS_PER_GAME_DAY / elapsed_seconds;
		}

		private void processRain(bool outdoors, float air_temperature, float elapsed_seconds)
		{
			if (!outdoors || parent.Map.weatherManager.RainRate <= 0f)
			{
				return;
			}
			float liters_per_hour = parent.Map.weatherManager.RainRate * Props.water_surface_m2 * 2f;
			float delivered = Mathf.Min(
				Props.capacity_liters - stored_liters,
				liters_per_hour * elapsed_seconds / 3600f);
			mixWater(delivered, air_temperature);
			last_rain_liters_per_day = elapsed_seconds <= 0f ? 0f : delivered * RealPhysics.SECONDS_PER_GAME_DAY / elapsed_seconds;
		}

		private void processConvection(bool outdoors, float air_temperature, float elapsed_seconds)
		{
			float coefficient = outdoors ? OUTDOOR_CONVECTION_W_PER_M2_K : INDOOR_CONVECTION_W_PER_M2_K;
			float power_kw = coefficient * Props.water_surface_m2 * (air_temperature - temperature_c) / 1000f;
			addPoolEnergy(power_kw * elapsed_seconds);
			last_environment_kw = power_kw;
		}

		private void processEvaporation(bool outdoors, float air_temperature, float elapsed_seconds)
		{
			float relative_humidity = outdoors ? 0.50f : 0.60f;
			float vapor_difference = Mathf.Max(0f,
				RealPhysics.calculateSaturationVaporPressureKPa(temperature_c)
				- relative_humidity * RealPhysics.calculateSaturationVaporPressureKPa(air_temperature));
			float coefficient = outdoors ? OUTDOOR_EVAPORATION_COEFFICIENT : INDOOR_EVAPORATION_COEFFICIENT;
			float kilograms_per_hour = coefficient * Props.water_surface_m2 * vapor_difference;
			float evaporated_liters = Mathf.Min(stored_liters, kilograms_per_hour * elapsed_seconds / 3600f);
			stored_liters -= evaporated_liters;
			float energy_loss_kj = evaporated_liters * RealPhysics.WATER_DENSITY_KG_PER_LITER
				* RealPhysics.WATER_EVAPORATION_LATENT_HEAT_KJ_PER_KG;
			addPoolEnergy(-energy_loss_kj);
			last_evaporation_liters_per_day = elapsed_seconds <= 0f ? 0f : evaporated_liters * RealPhysics.SECONDS_PER_GAME_DAY / elapsed_seconds;
		}

		private void processSolar(bool outdoors, float elapsed_seconds)
		{
			if (!outdoors)
			{
				return;
			}
			last_solar_kw = Props.water_surface_m2 * SOLAR_IRRADIANCE_KW_PER_M2
				* SOLAR_ABSORPTION * parent.Map.skyManager.CurSkyGlow;
			addPoolEnergy(last_solar_kw * elapsed_seconds);
		}

		private void processSkyRadiation(bool outdoors, float air_temperature, float elapsed_seconds)
		{
			if (!outdoors || parent.Map.skyManager.CurSkyGlow > 0.15f)
			{
				return;
			}
			float water_k = temperature_c + 273.15f;
			float sky_k = air_temperature - 20f + 273.15f;
			last_sky_kw = RealPhysics.WATER_EMISSIVITY * RealPhysics.STEFAN_BOLTZMANN
				* Props.water_surface_m2 * (Mathf.Pow(water_k, 4f) - Mathf.Pow(sky_k, 4f)) / 1000f;
			addPoolEnergy(-Mathf.Max(0f, last_sky_kw) * elapsed_seconds);
		}

		private void processNetworkHeating(float elapsed_seconds)
		{
			CompTargetTemperature target = parent.TryGetComp<CompTargetTemperature>();
			FluidNetwork heating_network = FluidUtility.getNetwork(parent, FluidNetworkType.Heating);
			if (target == null || heating_network == null || temperature_c >= target.target_temperature_c - 0.25f)
			{
				return;
			}
			float network_temperature = heating_network.getAverageThermalTemperature();
			float exchanger_kw = Props.heat_exchanger_surface_m2 * HEAT_EXCHANGER_W_PER_M2_K
				* Mathf.Max(0f, network_temperature - temperature_c) / 1000f;
			float delivered_kj = heating_network.drawThermalEnergy(exchanger_kw * elapsed_seconds);
			addPoolEnergy(delivered_kj);
			last_heating_kw = elapsed_seconds <= 0f ? 0f : delivered_kj / elapsed_seconds;
		}

		private void mixWater(float liters, float incoming_temperature_c)
		{
			if (liters <= 0f)
			{
				return;
			}
			float existing_energy = RealPhysics.calculateWaterEnergy(stored_liters, temperature_c);
			float incoming_energy = RealPhysics.calculateWaterEnergy(liters, incoming_temperature_c);
			stored_liters += liters;
			temperature_c = (existing_energy + incoming_energy)
				/ (stored_liters * RealPhysics.WATER_DENSITY_KG_PER_LITER * RealPhysics.WATER_SPECIFIC_HEAT_KJ_PER_KG_K);
		}


		private int getLegacyFlowRate()
		{
			FieldInfo field = findField(parent?.GetType(), "flowRate");
			if (field == null)
			{
				return 1000;
			}
			try
			{
				return Mathf.Clamp(Convert.ToInt32(field.GetValue(parent)), 0, 1000);
			}
			catch
			{
				return 1000;
			}
		}

		private void setLegacyFlowRate(int value)
		{
			FieldInfo field = findField(parent?.GetType(), "flowRate");
			if (field != null)
			{
				field.SetValue(parent, Mathf.Clamp(value, 0, 1000));
			}
		}

		private void syncLegacyVisualState()
		{
			if (parent == null)
			{
				return;
			}
			FieldInfo fuel_field = findField(parent.GetType(), "fuel");
			PropertyInfo capacity_property = findProperty(parent.GetType(), "capacity");
			if (fuel_field == null || capacity_property == null)
			{
				return;
			}
			try
			{
				float legacy_capacity = Convert.ToSingle(capacity_property.GetValue(parent, null));
				fuel_field.SetValue(parent, legacy_capacity * Mathf.Clamp01(stored_liters / Props.capacity_liters));
			}
			catch
			{
			}
		}

		private static FieldInfo findField(Type type, string name)
		{
			while (type != null)
			{
				FieldInfo field = type.GetField(name, REFLECTION_FLAGS);
				if (field != null)
				{
					return field;
				}
				type = type.BaseType;
			}
			return null;
		}

		private static PropertyInfo findProperty(Type type, string name)
		{
			while (type != null)
			{
				PropertyInfo property = type.GetProperty(name, REFLECTION_FLAGS);
				if (property != null)
				{
					return property;
				}
				type = type.BaseType;
			}
			return null;
		}

		private void addPoolEnergy(float energy_kj)
		{
			if (stored_liters <= 0.001f)
			{
				return;
			}
			temperature_c += RealPhysics.calculateWaterTemperatureChange(energy_kj, stored_liters);
		}
	}
}
