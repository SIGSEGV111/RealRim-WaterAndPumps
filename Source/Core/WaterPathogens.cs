using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class WaterbornePathogenDef : Def
	{
		public HediffDef hediff;
		public float selection_weight = 1f;
		public bool affects_animals = true;
	}

	public enum WaterSourceKind
	{
		Auto,
		RegularWell,
		DeepWell,
		SurfaceWater,
		MudWater,
		Clean,
	}

	public sealed class WaterContamination : IExposable
	{
		private Dictionary<WaterbornePathogenDef, float> risks_per_liter =
			new Dictionary<WaterbornePathogenDef, float>();

		public void ExposeData()
		{
			Scribe_Collections.Look(
				ref risks_per_liter,
				"risks_per_liter",
				LookMode.Def,
				LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (risks_per_liter == null)
				{
					risks_per_liter = new Dictionary<WaterbornePathogenDef, float>();
				}
				removeInvalidEntries();
			}
		}

		public WaterContamination copyContamination()
		{
			WaterContamination result = new WaterContamination();
			foreach (KeyValuePair<WaterbornePathogenDef, float> pair in risks_per_liter)
			{
				result.risks_per_liter[pair.Key] = pair.Value;
			}
			return result;
		}

		public bool hasPathogens()
		{
			return risks_per_liter.Count > 0;
		}

		public int getPathogenCount()
		{
			return risks_per_liter.Count;
		}

		public float getRiskPerLiter(WaterbornePathogenDef pathogen)
		{
			float risk;
			return pathogen != null && risks_per_liter.TryGetValue(pathogen, out risk)
				? risk
				: 0f;
		}

		public float getCombinedRiskPerLiter()
		{
			float survival = 1f;
			foreach (float risk in risks_per_liter.Values)
			{
				survival *= 1f - Mathf.Clamp01(risk);
			}
			return 1f - survival;
		}

		public IEnumerable<KeyValuePair<WaterbornePathogenDef, float>> getPathogens()
		{
			return risks_per_liter;
		}

		public void setRiskPerLiter(WaterbornePathogenDef pathogen, float risk)
		{
			if (pathogen == null || pathogen.hediff == null || risk <= 0f)
			{
				return;
			}
			risks_per_liter[pathogen] = Mathf.Clamp(risk, 0f, 0.25f);
		}

		public void mixWater(
			float existing_liters,
			float incoming_liters,
			WaterContamination incoming)
		{
			incoming_liters = Mathf.Max(0f, incoming_liters);
			existing_liters = Mathf.Max(0f, existing_liters);
			float total_liters = existing_liters + incoming_liters;
			if (total_liters <= 0.0001f)
			{
				risks_per_liter.Clear();
				return;
			}

			HashSet<WaterbornePathogenDef> pathogens =
				new HashSet<WaterbornePathogenDef>(risks_per_liter.Keys);
			if (incoming != null)
			{
				pathogens.UnionWith(incoming.risks_per_liter.Keys);
			}

			Dictionary<WaterbornePathogenDef, float> mixed =
				new Dictionary<WaterbornePathogenDef, float>();
			foreach (WaterbornePathogenDef pathogen in pathogens)
			{
				float old_risk = getRiskPerLiter(pathogen);
				float incoming_risk = incoming == null
					? 0f
					: incoming.getRiskPerLiter(pathogen);
				float mixed_risk = (
					old_risk * existing_liters
					+ incoming_risk * incoming_liters)
					/ total_liters;
				if (mixed_risk > 0.0000001f)
				{
					mixed[pathogen] = mixed_risk;
				}
			}
			risks_per_liter = mixed;
		}

		public void addWeightedSample(float current_liters, WaterSample sample)
		{
			if (sample == null || sample.liters <= 0f)
			{
				return;
			}
			mixWater(current_liters, sample.liters, sample.contamination);
		}

		public void reducePathogens(float removal_fraction)
		{
			float remaining_fraction = 1f - Mathf.Clamp01(removal_fraction);
			List<WaterbornePathogenDef> pathogens = risks_per_liter.Keys.ToList();
			for (int index = 0; index < pathogens.Count; index++)
			{
				WaterbornePathogenDef pathogen = pathogens[index];
				float remaining = risks_per_liter[pathogen] * remaining_fraction;
				if (remaining <= 0.0000001f)
				{
					risks_per_liter.Remove(pathogen);
				}
				else
				{
					risks_per_liter[pathogen] = remaining;
				}
			}
		}

		private void removeInvalidEntries()
		{
			risks_per_liter = risks_per_liter
				.Where(pair => pair.Key != null && pair.Key.hediff != null && pair.Value > 0f)
				.ToDictionary(pair => pair.Key, pair => pair.Value);
		}
	}

	public sealed class WaterSample
	{
		public float liters;
		public WaterContamination contamination = new WaterContamination();

		public void addSample(WaterSample incoming)
		{
			if (incoming == null || incoming.liters <= 0f)
			{
				return;
			}
			contamination.mixWater(liters, incoming.liters, incoming.contamination);
			liters += incoming.liters;
		}

		public void applyTreatment(float removal_fraction)
		{
			contamination.reducePathogens(removal_fraction);
		}
	}

	public static class WaterPathogenUtility
	{
		private const float REGULAR_WELL_PATHOGEN_CHANCE = 0.10f;
		private const float DEEP_WELL_PATHOGEN_CHANCE = 0.03f;
		private const float SURFACE_WATER_PATHOGEN_CHANCE = 0.20f;
		private const float MUD_WATER_PATHOGEN_CHANCE = 0.40f;

		public static WaterContamination generateContamination(
			WaterSourceKind source_kind,
			int seed)
		{
			WaterContamination result = new WaterContamination();
			float pathogen_chance;
			float minimum_risk;
			float maximum_risk;
			getSourceParameters(
				source_kind,
				out pathogen_chance,
				out minimum_risk,
				out maximum_risk);
			if (pathogen_chance <= 0f)
			{
				return result;
			}

			List<WaterbornePathogenDef> available = DefDatabase<WaterbornePathogenDef>
				.AllDefsListForReading
				.Where(pathogen => pathogen.hediff != null && pathogen.selection_weight > 0f)
				.ToList();
			Rand.PushState(seed);
			try
			{
				while (available.Count > 0 && Rand.Chance(pathogen_chance))
				{
					WaterbornePathogenDef pathogen = selectWeightedPathogen(available);
					if (pathogen == null)
					{
						break;
					}
					available.Remove(pathogen);
					result.setRiskPerLiter(
						pathogen,
						Rand.Range(minimum_risk, maximum_risk));
				}
			}
			finally
			{
				Rand.PopState();
			}
			return result;
		}

		public static WaterSourceKind classifyTerrain(Map map, IntVec3 cell)
		{
			if (map == null || !cell.InBounds(map))
			{
				return WaterSourceKind.SurfaceWater;
			}

			TerrainDef terrain = cell.GetTerrain(map);
			if (isMudTerrain(terrain))
			{
				return WaterSourceKind.MudWater;
			}
			if (terrain != null && terrain.IsWater)
			{
				return WaterSourceKind.SurfaceWater;
			}
			return WaterSourceKind.RegularWell;
		}

		public static WaterContamination getTerrainContamination(Map map, IntVec3 cell)
		{
			WaterSourceKind source_kind = classifyTerrain(map, cell);
			int seed = map == null
				? Gen.HashCombineInt(cell.x, cell.z)
				: Gen.HashCombineInt(map.ConstantRandSeed, cell.x, cell.z, 917431);
			return generateContamination(source_kind, seed);
		}

		public static void tryInfectPawn(Pawn pawn, WaterSample sample)
		{
			if (pawn?.health == null
				|| pawn.Dead
				|| pawn.RaceProps == null
				|| !pawn.RaceProps.IsFlesh
				|| sample == null
				|| sample.liters <= 0f
				|| sample.contamination == null)
			{
				return;
			}

			foreach (KeyValuePair<WaterbornePathogenDef, float> pair
				in sample.contamination.getPathogens())
			{
				WaterbornePathogenDef pathogen = pair.Key;
				HediffDef disease = pathogen?.hediff;
				if (disease == null
					|| (!pathogen.affects_animals && pawn.RaceProps.Animal)
					|| pawn.health.hediffSet.HasHediff(disease))
				{
					continue;
				}

				float risk_per_liter = Mathf.Clamp01(pair.Value);
				float infection_chance = 1f - Mathf.Pow(
					1f - risk_per_liter,
					sample.liters);
				if (Rand.Chance(infection_chance))
				{
					pawn.health.AddHediff(disease);
					if (Prefs.DevMode)
					{
						Log.Message("[RealRim] Water & Pumps: "
							+ pawn.LabelShortCap
							+ " contracted "
							+ disease.defName
							+ " from "
							+ sample.liters.ToString("N3")
							+ " L of water at "
							+ infection_chance.ToStringPercent("F4")
							+ " risk.");
					}
				}
			}
		}

		public static string getDeveloperDescription(WaterContamination contamination)
		{
			if (contamination == null || !contamination.hasPathogens())
			{
				return "clean";
			}
			return contamination.getPathogens()
				.OrderByDescending(pair => pair.Value)
				.Select(pair => pair.Key.defName + ": " + pair.Value.ToStringPercent("F4") + "/L")
				.ToLineList("", false)
				.TrimEndNewlines();
		}

		private static void getSourceParameters(
			WaterSourceKind source_kind,
			out float pathogen_chance,
			out float minimum_risk,
			out float maximum_risk)
		{
			switch (source_kind)
			{
				case WaterSourceKind.DeepWell:
					pathogen_chance = DEEP_WELL_PATHOGEN_CHANCE;
					minimum_risk = 0.00015f;
					maximum_risk = 0.00125f;
					break;
				case WaterSourceKind.SurfaceWater:
					pathogen_chance = SURFACE_WATER_PATHOGEN_CHANCE;
					minimum_risk = 0.00050f;
					maximum_risk = 0.00300f;
					break;
				case WaterSourceKind.MudWater:
					pathogen_chance = MUD_WATER_PATHOGEN_CHANCE;
					minimum_risk = 0.00150f;
					maximum_risk = 0.00750f;
					break;
				case WaterSourceKind.Clean:
					pathogen_chance = 0f;
					minimum_risk = 0f;
					maximum_risk = 0f;
					break;
				default:
					pathogen_chance = REGULAR_WELL_PATHOGEN_CHANCE;
					minimum_risk = 0.00015f;
					maximum_risk = 0.00125f;
					break;
			}
		}

		private static WaterbornePathogenDef selectWeightedPathogen(
			List<WaterbornePathogenDef> available)
		{
			float total_weight = available.Sum(pathogen => Mathf.Max(0f, pathogen.selection_weight));
			if (total_weight <= 0f)
			{
				return null;
			}
			float selection = Rand.Value * total_weight;
			for (int index = 0; index < available.Count; index++)
			{
				selection -= Mathf.Max(0f, available[index].selection_weight);
				if (selection <= 0f)
				{
					return available[index];
				}
			}
			return available[available.Count - 1];
		}

		private static bool isMudTerrain(TerrainDef terrain)
		{
			if (terrain == null)
			{
				return false;
			}
			string identity = (terrain.defName + " " + terrain.label).ToLowerInvariant();
			return terrain.IsFlood
				|| identity.Contains("mud")
				|| identity.Contains("marsh")
				|| identity.Contains("bog")
				|| identity.Contains("swamp");
		}
	}
}
