using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_WasteStorage : CompProperties
	{
		public float water_capacity_liters = 12000f;
		public float sludge_capacity_kg = 600f;
		public float infiltration_liters_per_day;
		public float treatment_liters_per_day;
		public float recovery_fraction = 0.95f;
		public bool automatically_eject_sludge;
		public float automatic_ejection_kg = 10f;

		public CompProperties_WasteStorage()
		{
			compClass = typeof(CompWasteStorage);
		}
	}

	public sealed class CompWasteStorage : ThingComp, IFluidTickable
	{
		private const float SLUDGE_KG_PER_ITEM = 0.05f;
		private const string SLUDGE_DEF_NAME = "FecalSludge";

		public float stored_water_liters;
		public float stored_sludge_kg;
		public float last_processed_liters_per_day;
		public float last_recovered_liters_per_day;
		public string last_reason = string.Empty;

		public CompProperties_WasteStorage Props
		{
			get
			{
				return (CompProperties_WasteStorage)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref stored_water_liters, "stored_water_liters", 0f);
			Scribe_Values.Look(ref stored_sludge_kg, "stored_sludge_kg", 0f);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				stored_water_liters = Mathf.Clamp(stored_water_liters, 0f, Props.water_capacity_liters);
				stored_sludge_kg = Mathf.Clamp(stored_sludge_kg, 0f, Props.sludge_capacity_kg);
			}
		}

		public override string CompInspectStringExtra()
		{
			string status = "RealRim_WasteStorageStatus".Translate(
				stored_water_liters.ToString("N0"),
				Props.water_capacity_liters.ToString("N0"),
				stored_sludge_kg.ToString("N1"),
				Props.sludge_capacity_kg.ToString("N1"),
				last_processed_liters_per_day.ToString("N0"),
				last_recovered_liters_per_day.ToString("N0"),
				last_reason);
			return status.TrimEnd('\r', '\n', ' ', '\t');
		}

		public float getWaterSpace()
		{
			return Mathf.Max(0f, Props.water_capacity_liters - stored_water_liters);
		}

		public float getSludgeSpace()
		{
			return Mathf.Max(0f, Props.sludge_capacity_kg - stored_sludge_kg);
		}

		public float getFillFraction()
		{
			float water = Props.water_capacity_liters <= 0f ? 0f : stored_water_liters / Props.water_capacity_liters;
			float sludge = Props.sludge_capacity_kg <= 0f ? 0f : stored_sludge_kg / Props.sludge_capacity_kg;
			return Mathf.Max(water, sludge);
		}

		public float addWasteWater(float requested_liters)
		{
			float accepted = Mathf.Min(Mathf.Max(0f, requested_liters), getWaterSpace());
			stored_water_liters += accepted;
			return accepted;
		}

		public float addSludge(float requested_kg)
		{
			float accepted = Mathf.Min(Mathf.Max(0f, requested_kg), getSludgeSpace());
			stored_sludge_kg += accepted;
			return accepted;
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_processed_liters_per_day = 0f;
			last_recovered_liters_per_day = 0f;
			last_reason = string.Empty;

			if (Props.infiltration_liters_per_day > 0f)
			{
				processInfiltration(elapsed_seconds);
			}

			if (Props.treatment_liters_per_day > 0f)
			{
				processTreatment(elapsed_seconds);
			}

		}

		private void processInfiltration(float elapsed_seconds)
		{
			if (FluidUtility.isOnGravship(parent) || !isEntireFootprintNaturalSoil())
			{
				last_reason = "RealRim_ReasonNoSoilInfiltration".Translate();
				return;
			}

			float requested = Props.infiltration_liters_per_day * elapsed_seconds / RealPhysics.SECONDS_PER_GAME_DAY;
			float removed = Mathf.Min(stored_water_liters, requested);
			stored_water_liters -= removed;
			last_processed_liters_per_day += elapsed_seconds <= 0f ? 0f : removed * RealPhysics.SECONDS_PER_GAME_DAY / elapsed_seconds;
		}

		private bool isEntireFootprintNaturalSoil()
		{
			if (parent.Map == null)
			{
				return false;
			}

			foreach (IntVec3 cell in parent.OccupiedRect())
			{
				TerrainDef terrain = cell.GetTerrain(parent.Map);
				if (terrain == null || !terrain.IsSoil || terrain.IsRock)
				{
					return false;
				}
			}

			return true;
		}

		private void processTreatment(float elapsed_seconds)
		{
			if (!FluidUtility.isPoweredOn(parent))
			{
				last_reason = "RealRim_ReasonNoPower".Translate();
				return;
			}

			float requested = Props.treatment_liters_per_day * elapsed_seconds / RealPhysics.SECONDS_PER_GAME_DAY;
			float processed = Mathf.Min(stored_water_liters, requested);
			if (processed <= 0f)
			{
				return;
			}

			FluidNetwork fresh_network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			float recovered = fresh_network == null ? 0f : fresh_network.addFreshWater(processed * Props.recovery_fraction);
			stored_water_liters -= processed;
			last_processed_liters_per_day += processed * RealPhysics.SECONDS_PER_GAME_DAY / elapsed_seconds;
			last_recovered_liters_per_day = recovered * RealPhysics.SECONDS_PER_GAME_DAY / elapsed_seconds;
		}

		public float getExtractableSludgeKg()
		{
			return stored_sludge_kg;
		}

		public float extractSludge(float requested_kg)
		{
			float extracted = Mathf.Min(Mathf.Max(0f, requested_kg), stored_sludge_kg);
			stored_sludge_kg -= extracted;
			return extracted;
		}

	}
}
