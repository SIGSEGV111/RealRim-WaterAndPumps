using RimWorld;
using Verse;

namespace RealRim.WaterAndPumps
{
	public static class WaterTemperaturePreferences
	{
		private const string HEAT_INCLINED_DEF_NAME = "VTE_HeatInclined";
		private const string COLD_INCLINED_DEF_NAME = "VTE_ColdInclined";
		private const float TRAIT_TOLERANCE_OFFSET_C = 10f;

		private static TraitDef heat_inclined_def;
		private static TraitDef cold_inclined_def;
		private static bool traits_resolved;

		public static bool hasHeatInclined(Pawn pawn)
		{
			resolveTraits();
			return heat_inclined_def != null
				&& pawn?.story?.traits != null
				&& pawn.story.traits.HasTrait(heat_inclined_def);
		}

		public static bool hasColdInclined(Pawn pawn)
		{
			resolveTraits();
			return cold_inclined_def != null
				&& pawn?.story?.traits != null
				&& pawn.story.traits.HasTrait(cold_inclined_def);
		}

		public static float getBaseComfortMinimum(Pawn pawn)
		{
			return pawn?.def == null
				? 10f
				: pawn.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin);
		}

		public static float getBaseComfortMaximum(Pawn pawn)
		{
			return pawn?.def == null
				? 40f
				: pawn.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax);
		}

		public static float getAdjustedComfortMinimum(Pawn pawn)
		{
			return getBaseComfortMinimum(pawn)
				- (hasColdInclined(pawn) ? TRAIT_TOLERANCE_OFFSET_C : 0f);
		}

		public static float getAdjustedComfortMaximum(Pawn pawn)
		{
			return getBaseComfortMaximum(pawn)
				+ (hasHeatInclined(pawn) ? TRAIT_TOLERANCE_OFFSET_C : 0f);
		}

		public static string selectFixtureThought(Pawn pawn, float water_temperature_c, float reference_temperature_c)
		{
			if (hasColdInclined(pawn) && water_temperature_c <= reference_temperature_c - 5f)
			{
				return "RealRim_ColdWaterEnjoyment";
			}
			if (hasHeatInclined(pawn) && water_temperature_c >= reference_temperature_c + 5f)
			{
				return "RealRim_HotWaterEnjoyment";
			}
			if (water_temperature_c < reference_temperature_c - 4f)
			{
				return "RealRim_ColdWaterComplaint";
			}
			if (water_temperature_c > reference_temperature_c + 5f)
			{
				return "RealRim_HotWaterComplaint";
			}
			return null;
		}

		public static string selectPoolThought(Pawn pawn, float water_temperature_c)
		{
			float base_minimum = getBaseComfortMinimum(pawn);
			float base_maximum = getBaseComfortMaximum(pawn);
			if (hasColdInclined(pawn) && water_temperature_c <= base_minimum - 5f)
			{
				return "RealRim_ColdWaterEnjoyment";
			}
			if (hasHeatInclined(pawn) && water_temperature_c >= base_maximum + 5f)
			{
				return "RealRim_HotWaterEnjoyment";
			}
			float center = (getAdjustedComfortMinimum(pawn) + getAdjustedComfortMaximum(pawn)) * 0.5f;
			return water_temperature_c < center ? "RealRim_ColdWaterComplaint" : null;
		}

		private static void resolveTraits()
		{
			if (traits_resolved)
			{
				return;
			}
			traits_resolved = true;
			heat_inclined_def = DefDatabase<TraitDef>.GetNamedSilentFail(HEAT_INCLINED_DEF_NAME);
			cold_inclined_def = DefDatabase<TraitDef>.GetNamedSilentFail(COLD_INCLINED_DEF_NAME);
		}
	}
}
