using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_HotWaterTank : CompProperties
	{
		public float capacity_liters = 600f;
		public float initial_temperature_c = 20f;
		public float minimum_temperature_c = 5f;
		public float maximum_temperature_c = 85f;
		public float heat_exchanger_surface_m2 = 1.5f;
		public float heat_transfer_w_per_m2_k = 800f;
		public float maximum_transfer_kw = 20f;
		public float heat_loss_w_per_k = 2.5f;

		public CompProperties_HotWaterTank()
		{
			compClass = typeof(CompHotWaterTank);
		}
	}

	public sealed class CompHotWaterTank : ThingComp, IFluidTickable
	{
		public float stored_liters;
		public float temperature_c;
		public float last_transfer_kw;
		public float last_refill_liters_per_hour;
		public float last_heat_loss_kw;

		public CompProperties_HotWaterTank Props
		{
			get
			{
				return (CompProperties_HotWaterTank)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			if (!respawning_after_load)
			{
				stored_liters = Props.capacity_liters;
				temperature_c = Props.initial_temperature_c;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref stored_liters, "hot_water_stored_liters", Props.capacity_liters);
			Scribe_Values.Look(ref temperature_c, "hot_water_temperature_c", Props.initial_temperature_c);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				stored_liters = Mathf.Clamp(stored_liters, 0f, Props.capacity_liters);
				temperature_c = Mathf.Clamp(temperature_c, Props.minimum_temperature_c, Props.maximum_temperature_c);
			}
		}

		public override string CompInspectStringExtra()
		{
			return "RealRim_HotWaterTankStatus".Translate(
				stored_liters.ToString("N0"),
				Props.capacity_liters.ToString("N0"),
				temperature_c.ToStringTemperature("F1"),
				last_transfer_kw.ToString("N1"),
				last_refill_liters_per_hour.ToString("N0"),
				last_heat_loss_kw.ToString("N2"));
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_transfer_kw = 0f;
			last_refill_liters_per_hour = 0f;
			last_heat_loss_kw = 0f;
			refillFromFreshWater(elapsed_seconds);
			transferHeatFromHeatingNetwork(elapsed_seconds);
			loseHeatToAmbient(elapsed_seconds);
		}

		public float drawHotWater(float requested_liters)
		{
			float delivered = Mathf.Min(Mathf.Max(0f, requested_liters), stored_liters);
			stored_liters -= delivered;
			return delivered;
		}

		private void refillFromFreshWater(float elapsed_seconds)
		{
			float missing = Props.capacity_liters - stored_liters;
			if (missing <= 0.001f)
			{
				return;
			}
			FluidNetwork fresh_network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (fresh_network == null)
			{
				return;
			}
			float delivered = fresh_network.drawFreshWater(missing);
			if (delivered <= 0f)
			{
				return;
			}

			float old_energy = RealPhysics.calculateWaterEnergy(stored_liters, temperature_c);
			float incoming_energy = RealPhysics.calculateWaterEnergy(
				delivered,
				RealPhysics.COLD_WATER_TEMPERATURE_C);
			stored_liters += delivered;
			temperature_c = (old_energy + incoming_energy)
				/ (stored_liters
					* RealPhysics.WATER_DENSITY_KG_PER_LITER
					* RealPhysics.WATER_SPECIFIC_HEAT_KJ_PER_KG_K);
			last_refill_liters_per_hour = elapsed_seconds <= 0f
				? 0f
				: delivered * 3600f / elapsed_seconds;
		}

		private void loseHeatToAmbient(float elapsed_seconds)
		{
			if (elapsed_seconds <= 0f || stored_liters <= 0.001f || parent == null || !parent.Spawned)
			{
				return;
			}

			float ambient_temperature_c = parent.AmbientTemperature;
			float minimum_target_c = Mathf.Max(Props.minimum_temperature_c, ambient_temperature_c);
			float temperature_difference = temperature_c - minimum_target_c;
			if (temperature_difference <= 0.001f)
			{
				return;
			}

			float requested_loss_kw = Props.heat_loss_w_per_k * temperature_difference / 1000f;
			float available_energy_kj = RealPhysics.calculateWaterEnergy(stored_liters, temperature_difference);
			float lost_energy_kj = Mathf.Min(requested_loss_kw * elapsed_seconds, available_energy_kj);
			temperature_c -= RealPhysics.calculateWaterTemperatureChange(lost_energy_kj, stored_liters);
			last_heat_loss_kw = lost_energy_kj / elapsed_seconds;
		}

		private void transferHeatFromHeatingNetwork(float elapsed_seconds)
		{
			FluidNetwork heating_network = FluidUtility.getNetwork(parent, FluidNetworkType.Heating);
			if (heating_network == null || stored_liters <= 0.001f)
			{
				return;
			}
			float heating_temperature = heating_network.getAverageThermalTemperature();
			float temperature_difference = heating_temperature - temperature_c;
			if (temperature_difference <= 0.1f || temperature_c >= Props.maximum_temperature_c - 0.05f)
			{
				return;
			}

			float exchanger_kw = Props.heat_exchanger_surface_m2
				* Props.heat_transfer_w_per_m2_k
				* temperature_difference / 1000f;
			exchanger_kw = Mathf.Min(exchanger_kw, Props.maximum_transfer_kw);
			float temperature_room_kj = RealPhysics.calculateWaterEnergy(
				stored_liters,
				Props.maximum_temperature_c - temperature_c);
			float requested_kj = Mathf.Min(exchanger_kw * elapsed_seconds, temperature_room_kj);
			float delivered_kj = heating_network.drawThermalEnergy(requested_kj);
			temperature_c += RealPhysics.calculateWaterTemperatureChange(delivered_kj, stored_liters);
			last_transfer_kw = elapsed_seconds <= 0f ? 0f : delivered_kj / elapsed_seconds;
		}
	}
}
