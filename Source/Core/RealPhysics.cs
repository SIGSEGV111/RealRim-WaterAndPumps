using UnityEngine;

namespace RealRim.WaterAndPumps
{
	public static class RealPhysics
	{
		public const float SECONDS_PER_GAME_TICK = 1.44f;
		public const float TICKS_PER_DAY = 60000f;
		public const float SECONDS_PER_GAME_DAY = SECONDS_PER_GAME_TICK * TICKS_PER_DAY;
		public const float WATER_DENSITY_KG_PER_LITER = 0.997f;
		public const float WATER_SPECIFIC_HEAT_KJ_PER_KG_K = 4.186f;
		public const float WATER_FREEZING_LATENT_HEAT_KJ_PER_KG = 333.55f;
		public const float WATER_EVAPORATION_LATENT_HEAT_KJ_PER_KG = 2450f;
		public const float STEFAN_BOLTZMANN = 0.00000005670374419f;
		public const float WATER_EMISSIVITY = 0.96f;
		public const float COLD_WATER_TEMPERATURE_C = 12f;
		public const float DEFAULT_HOT_WATER_MAX_C = 85f;
		public const float RIMWORLD_HEAT_UNITS_PER_KW_SECOND = 1000f / 3025f;
		public const float HOT_WATER_PIPE_HEAT_TRANSFER_W_PER_M_K = 0.35f;
		public const float HEATING_PIPE_HEAT_TRANSFER_W_PER_M_K = 0.70f;
		public const float HEATING_PIPE_BUFFER_LITERS_PER_M = 2.0f;
		public const float HEATING_BUFFER_INITIAL_TEMPERATURE_C = 20f;
		public const float HEATING_BUFFER_MINIMUM_TEMPERATURE_C = 5f;
		public const float HEATING_BUFFER_MAXIMUM_TEMPERATURE_C = 85f;

		public static float calculateWaterEnergy(float liters, float temperature_delta)
		{
			return liters * WATER_DENSITY_KG_PER_LITER * WATER_SPECIFIC_HEAT_KJ_PER_KG_K * temperature_delta;
		}

		public static float calculateWaterTemperatureChange(float energy_kj, float liters)
		{
			float heat_capacity = liters * WATER_DENSITY_KG_PER_LITER * WATER_SPECIFIC_HEAT_KJ_PER_KG_K;
			return heat_capacity <= 0.0001f ? 0f : energy_kj / heat_capacity;
		}

		public static float calculateSaturationVaporPressureKPa(float temperature_c)
		{
			return 0.61078f * Mathf.Exp((17.2694f * temperature_c) / (temperature_c + 237.29f));
		}

		public static float calculateRadiatorOutputKw(float rated_output_kw, float water_temperature_c, float room_temperature_c)
		{
			float delta_t = Mathf.Max(0f, water_temperature_c - room_temperature_c);
			return delta_t <= 0f ? 0f : rated_output_kw * Mathf.Pow(delta_t / 50f, 1.3f);
		}

		public static float calculateHeatPumpCop(float source_temperature_c, float sink_temperature_c)
		{
			float source_k = Mathf.Max(200f, source_temperature_c + 273.15f);
			float sink_k = Mathf.Max(source_k + 1f, sink_temperature_c + 273.15f);
			float carnot_cop = sink_k / Mathf.Max(1f, sink_k - source_k);
			return Mathf.Clamp(carnot_cop * 0.42f, 1.15f, 5.5f);
		}

		public static float calculateCoolingCop(float cold_temperature_c, float ambient_temperature_c)
		{
			float cold_k = Mathf.Max(200f, cold_temperature_c + 273.15f);
			float hot_k = Mathf.Max(cold_k + 1f, ambient_temperature_c + 273.15f);
			float carnot_cop = cold_k / Mathf.Max(1f, hot_k - cold_k);
			return Mathf.Clamp(carnot_cop * 0.35f, 1.2f, 5.0f);
		}

		public static float calculateMixedHotFraction(float target_temperature_c, float hot_temperature_c)
		{
			float denominator = hot_temperature_c - COLD_WATER_TEMPERATURE_C;
			if (denominator <= 0.1f)
			{
				return 0f;
			}

			return Mathf.Clamp01((target_temperature_c - COLD_WATER_TEMPERATURE_C) / denominator);
		}
	}
}
