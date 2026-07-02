using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_CoolantTank : CompProperties
	{
		public float water_liters = 600f;
		public float warm_temperature_c = 12f;

		public CompProperties_CoolantTank()
		{
			compClass = typeof(CompCoolantTank);
		}
	}

	public sealed class CompCoolantTank : ThingComp
	{
		public float cold_energy_kj;

		public CompProperties_CoolantTank Props
		{
			get
			{
				return (CompProperties_CoolantTank)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref cold_energy_kj, "cold_energy_kj", 0f);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				cold_energy_kj = Mathf.Clamp(cold_energy_kj, 0f, getCapacityKj());
			}
		}

		public override string CompInspectStringExtra()
		{
			return "RealRim_CoolantTankStatus".Translate(
				Props.water_liters.ToString("N0"),
				getEquivalentTemperatureC().ToStringTemperature("F1"),
				getFrozenFraction().ToStringPercent(),
				getFillFraction().ToStringPercent());
		}

		public float getCapacityKj()
		{
			float sensible = RealPhysics.calculateWaterEnergy(Props.water_liters, Props.warm_temperature_c);
			float latent = Props.water_liters
				* RealPhysics.WATER_DENSITY_KG_PER_LITER
				* RealPhysics.WATER_FREEZING_LATENT_HEAT_KJ_PER_KG;
			return sensible + latent;
		}

		public float getFillFraction()
		{
			float capacity = getCapacityKj();
			return capacity <= 0.001f ? 0f : cold_energy_kj / capacity;
		}

		public float getEquivalentTemperatureC()
		{
			float sensible = RealPhysics.calculateWaterEnergy(Props.water_liters, Props.warm_temperature_c);
			if (cold_energy_kj >= sensible)
			{
				return 0f;
			}

			return Props.warm_temperature_c
				- RealPhysics.calculateWaterTemperatureChange(cold_energy_kj, Props.water_liters);
		}

		public float getFrozenFraction()
		{
			float sensible = RealPhysics.calculateWaterEnergy(Props.water_liters, Props.warm_temperature_c);
			float latent = getCapacityKj() - sensible;
			if (latent <= 0.001f)
			{
				return 0f;
			}

			return Mathf.Clamp01((cold_energy_kj - sensible) / latent);
		}

		public float addColdEnergy(float requested_kj)
		{
			float accepted = Mathf.Min(Mathf.Max(0f, requested_kj), getCapacityKj() - cold_energy_kj);
			cold_energy_kj += accepted;
			return accepted;
		}

		public float drawColdEnergy(float requested_kj)
		{
			float delivered = Mathf.Min(Mathf.Max(0f, requested_kj), cold_energy_kj);
			cold_energy_kj -= delivered;
			return delivered;
		}
	}
}
