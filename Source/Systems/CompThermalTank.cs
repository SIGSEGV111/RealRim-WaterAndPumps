using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_ThermalTank : CompProperties
	{
		public float capacity_liters = 600f;
		public float initial_temperature_c = 20f;
		public float minimum_temperature_c = 5f;
		public float maximum_temperature_c = 85f;

		public CompProperties_ThermalTank()
		{
			compClass = typeof(CompThermalTank);
		}
	}

	public sealed class CompThermalTank : ThingComp
	{
		public float stored_liters;
		public float temperature_c;

		public CompProperties_ThermalTank Props
		{
			get
			{
				return (CompProperties_ThermalTank)props;
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
			Scribe_Values.Look(ref stored_liters, "stored_liters", Props.capacity_liters);
			Scribe_Values.Look(ref temperature_c, "temperature_c", Props.initial_temperature_c);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				stored_liters = Mathf.Clamp(stored_liters, 0f, Props.capacity_liters);
				temperature_c = Mathf.Clamp(temperature_c, Props.minimum_temperature_c, Props.maximum_temperature_c);
			}
		}

		public override string CompInspectStringExtra()
		{
			return "RealRim_ThermalTankStatus".Translate(
				stored_liters.ToString("N0"),
				Props.capacity_liters.ToString("N0"),
				temperature_c.ToStringTemperature("F1"),
				getEnergyFillFraction().ToStringPercent());
		}

		public float getUsableEnergyKj()
		{
			return RealPhysics.calculateWaterEnergy(
				stored_liters,
				Mathf.Max(0f, temperature_c - Props.minimum_temperature_c));
		}

		public float getUsableCapacityKj()
		{
			return RealPhysics.calculateWaterEnergy(
				Props.capacity_liters,
				Props.maximum_temperature_c - Props.minimum_temperature_c);
		}

		public float getEnergyFillFraction()
		{
			float capacity = getUsableCapacityKj();
			return capacity <= 0.001f ? 0f : getUsableEnergyKj() / capacity;
		}

		public float addEnergy(float requested_kj)
		{
			if (stored_liters <= 0.001f)
			{
				return 0f;
			}

			float room_kj = RealPhysics.calculateWaterEnergy(
				stored_liters,
				Props.maximum_temperature_c - temperature_c);
			float accepted = Mathf.Min(Mathf.Max(0f, requested_kj), room_kj);
			temperature_c += RealPhysics.calculateWaterTemperatureChange(accepted, stored_liters);
			return accepted;
		}

		public float drawEnergy(float requested_kj)
		{
			float available = getUsableEnergyKj();
			float delivered = Mathf.Min(Mathf.Max(0f, requested_kj), available);
			temperature_c -= RealPhysics.calculateWaterTemperatureChange(delivered, stored_liters);
			return delivered;
		}

		public float drawHotWater(float requested_liters, float replacement_temperature_c, float replacement_liters)
		{
			float delivered = Mathf.Min(Mathf.Max(0f, requested_liters), stored_liters);
			if (delivered <= 0f)
			{
				return 0f;
			}

			stored_liters -= delivered;
			float accepted_replacement = Mathf.Min(
				Mathf.Max(0f, replacement_liters),
				Props.capacity_liters - stored_liters);
			if (accepted_replacement > 0f)
			{
				float old_energy = RealPhysics.calculateWaterEnergy(stored_liters, temperature_c);
				float replacement_energy = RealPhysics.calculateWaterEnergy(accepted_replacement, replacement_temperature_c);
				stored_liters += accepted_replacement;
				temperature_c = (old_energy + replacement_energy)
					/ (stored_liters * RealPhysics.WATER_DENSITY_KG_PER_LITER * RealPhysics.WATER_SPECIFIC_HEAT_KJ_PER_KG_K);
			}

			return delivered;
		}
	}
}
