using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class WaterRequirementExtension : DefModExtension
	{
		public float liters_per_day = -1f;
		public float internal_capacity_liters = -1f;
	}

	public sealed class CompProperties_WaterTrough : CompProperties
	{
		public float capacity_liters = 200f;
		public float refill_liters_per_hour = 500f;

		public CompProperties_WaterTrough()
		{
			compClass = typeof(CompWaterTrough);
		}
	}

	public sealed class CompWaterTrough : ThingComp, IFluidTickable
	{
		public float stored_liters;
		public float last_refill_liters_per_hour;
		private WaterContamination contamination = new WaterContamination();

		public CompProperties_WaterTrough Props
		{
			get
			{
				return (CompProperties_WaterTrough)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref stored_liters, "trough_stored_liters", 0f);
			Scribe_Deep.Look(ref contamination, "trough_water_contamination");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && contamination == null)
			{
				contamination = new WaterContamination();
			}
		}

		public override string CompInspectStringExtra()
		{
			string result = "RealRim_TroughStatus".Translate(
				stored_liters.ToString("N1"),
				Props.capacity_liters.ToString("N0"),
				last_refill_liters_per_hour.ToString("N0"));
			if (Prefs.DevMode)
			{
				result += "\n[DEV] " + WaterPathogenUtility.getDeveloperDescription(contamination);
			}
			return result;
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_refill_liters_per_hour = 0f;
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (network != null && stored_liters < Props.capacity_liters)
			{
				float requested = Mathf.Min(
					Props.capacity_liters - stored_liters,
					Props.refill_liters_per_hour * elapsed_seconds / 3600f);
				WaterSample sample = network.drawFreshWaterSample(requested);
				contamination.mixWater(stored_liters, sample.liters, sample.contamination);
				stored_liters += sample.liters;
				last_refill_liters_per_hour = elapsed_seconds <= 0f
					? 0f
					: sample.liters * 3600f / elapsed_seconds;
			}

			if (!parent.Position.Roofed(parent.Map) && parent.Map.weatherManager.RainRate > 0f)
			{
				float rain_liters = Mathf.Min(
					Props.capacity_liters - stored_liters,
					parent.Map.weatherManager.RainRate * elapsed_seconds * 0.002f);
				contamination.mixWater(stored_liters, rain_liters, null);
				stored_liters += rain_liters;
			}
		}

		public bool canDrink(Pawn pawn)
		{
			return stored_liters + 0.001f >= getDrinkLiters(pawn);
		}

		public WaterSample drawWaterSample(float requested_liters)
		{
			WaterSample sample = new WaterSample
			{
				liters = Mathf.Min(stored_liters, Mathf.Max(0f, requested_liters)),
				contamination = contamination.copyContamination(),
			};
			stored_liters -= sample.liters;
			if (stored_liters <= 0.0001f)
			{
				stored_liters = 0f;
				contamination = new WaterContamination();
			}
			return sample;
		}

		public static float getDailyLiters(Pawn pawn)
		{
			if (pawn == null)
			{
				return 3f;
			}

			WaterRequirementExtension extension = getRequirementExtension(pawn);
			if (extension != null && extension.liters_per_day > 0f)
			{
				return extension.liters_per_day;
			}

			return pawn.RaceProps.Animal
				? Mathf.Max(0.25f, pawn.BodySize * 40f)
				: Mathf.Max(1f, pawn.BodySize * 3f);
		}

		public static float getInternalCapacityLiters(Pawn pawn)
		{
			WaterRequirementExtension extension = pawn == null ? null : getRequirementExtension(pawn);
			if (extension != null && extension.internal_capacity_liters > 0f)
			{
				return extension.internal_capacity_liters;
			}
			return getDailyLiters(pawn) * 2f;
		}

		public static float getDrinkLiters(Pawn pawn)
		{
			return getDailyLiters(pawn) / 3f;
		}

		private static WaterRequirementExtension getRequirementExtension(Pawn pawn)
		{
			WaterRequirementExtension extension = pawn.def.GetModExtension<WaterRequirementExtension>();
			if (extension == null && pawn.kindDef != null)
			{
				extension = pawn.kindDef.GetModExtension<WaterRequirementExtension>();
			}
			return extension;
		}
	}
}
