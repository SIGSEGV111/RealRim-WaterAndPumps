using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_WaterTank : CompProperties
	{
		public float capacity_liters = 1000f;
		public float initial_fill_fraction;

		public CompProperties_WaterTank()
		{
			compClass = typeof(CompWaterTank);
		}
	}

	public sealed class CompWaterTank : ThingComp
	{
		public float stored_liters;
		private WaterContamination contamination = new WaterContamination();

		public CompProperties_WaterTank Props
		{
			get
			{
				return (CompProperties_WaterTank)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			if (!respawning_after_load && stored_liters <= 0f)
			{
				stored_liters = Props.capacity_liters * Mathf.Clamp01(Props.initial_fill_fraction);
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref stored_liters, "stored_liters", 0f);
			Scribe_Deep.Look(ref contamination, "water_contamination");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				stored_liters = Mathf.Clamp(stored_liters, 0f, Props.capacity_liters);
				if (contamination == null)
				{
					contamination = new WaterContamination();
				}
			}
		}

		public override string CompInspectStringExtra()
		{
			string result = "RealRim_WaterStored".Translate(
				stored_liters.ToString("N0"),
				Props.capacity_liters.ToString("N0"),
				getFillFraction().ToStringPercent());
			if (Prefs.DevMode)
			{
				result += "\n[DEV] " + WaterPathogenUtility.getDeveloperDescription(contamination);
			}
			return result;
		}

		public float getFillFraction()
		{
			return Props.capacity_liters <= 0f ? 0f : stored_liters / Props.capacity_liters;
		}

		public WaterContamination getContamination()
		{
			return contamination.copyContamination();
		}

		public float addWater(
			float requested_liters,
			WaterContamination incoming_contamination)
		{
			float accepted = Mathf.Min(
				Mathf.Max(0f, requested_liters),
				Props.capacity_liters - stored_liters);
			if (accepted <= 0f)
			{
				return 0f;
			}
			contamination.mixWater(
				stored_liters,
				accepted,
				incoming_contamination);
			stored_liters += accepted;
			return accepted;
		}

		public WaterSample drawWaterSample(float requested_liters)
		{
			WaterSample sample = new WaterSample
			{
				liters = Mathf.Min(Mathf.Max(0f, requested_liters), stored_liters),
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
	}
}
