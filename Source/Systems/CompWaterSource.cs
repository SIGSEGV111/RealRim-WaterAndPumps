using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_WaterSource : CompProperties
	{
		public WaterSourceKind source_kind = WaterSourceKind.RegularWell;
		public float contribution_weight = 1f;

		public CompProperties_WaterSource()
		{
			compClass = typeof(CompWaterSource);
		}
	}

	public sealed class CompWaterSource : ThingComp
	{
		private WaterContamination contamination = new WaterContamination();
		private bool contamination_initialized;
		private WaterSourceKind effective_source_kind;

		public CompProperties_WaterSource Props
		{
			get
			{
				return (CompProperties_WaterSource)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			initializeContamination();
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref contamination_initialized, "contamination_initialized", false);
			Scribe_Values.Look(ref effective_source_kind, "effective_source_kind", WaterSourceKind.RegularWell);
			Scribe_Deep.Look(ref contamination, "water_contamination");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && contamination == null)
			{
				contamination = new WaterContamination();
			}
		}

		public override string CompInspectStringExtra()
		{
			string result = "RealRim_WaterSourceStatus".Translate(
				getSourceLabel(effective_source_kind));
			if (Prefs.DevMode)
			{
				result += "\n[DEV] " + WaterPathogenUtility.getDeveloperDescription(contamination);
			}
			return result;
		}

		public float getContributionWeight()
		{
			return UnityEngine.Mathf.Max(0.0001f, Props.contribution_weight);
		}

		public WaterContamination getContamination()
		{
			initializeContamination();
			return contamination.copyContamination();
		}

		private void initializeContamination()
		{
			if (contamination_initialized || parent == null)
			{
				return;
			}
			effective_source_kind = getEffectiveSourceKind();
			contamination = WaterPathogenUtility.generateContamination(
				effective_source_kind,
				Gen.HashCombineInt(parent.thingIDNumber, 148991));
			contamination_initialized = true;
		}

		private WaterSourceKind getEffectiveSourceKind()
		{
			if (Props.source_kind != WaterSourceKind.Auto)
			{
				return Props.source_kind;
			}
			if (parent.MapHeld == null)
			{
				return WaterSourceKind.RegularWell;
			}

			bool found_surface_water = false;
			foreach (IntVec3 cell in parent.OccupiedRect().ExpandedBy(1).Cells)
			{
				if (!cell.InBounds(parent.MapHeld))
				{
					continue;
				}

				WaterSourceKind source_kind = WaterPathogenUtility.classifyTerrain(parent.MapHeld, cell);
				if (source_kind == WaterSourceKind.MudWater)
				{
					return WaterSourceKind.MudWater;
				}
				if (source_kind == WaterSourceKind.SurfaceWater)
				{
					found_surface_water = true;
				}
			}
			return found_surface_water
				? WaterSourceKind.SurfaceWater
				: WaterSourceKind.RegularWell;
		}

		private static string getSourceLabel(WaterSourceKind source_kind)
		{
			switch (source_kind)
			{
				case WaterSourceKind.DeepWell:
					return "RealRim_SourceDeepWell".Translate();
				case WaterSourceKind.SurfaceWater:
					return "RealRim_SourceSurfaceWater".Translate();
				case WaterSourceKind.MudWater:
					return "RealRim_SourceMudWater".Translate();
				case WaterSourceKind.Clean:
					return "RealRim_SourceClean".Translate();
				default:
					return "RealRim_SourceRegularWell".Translate();
			}
		}
	}
}
