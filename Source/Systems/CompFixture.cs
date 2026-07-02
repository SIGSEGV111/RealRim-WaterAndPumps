using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public enum FixtureKind
	{
		Fountain,
		Sink,
		KitchenSink,
		Toilet,
		Shower,
		Bath,
	}

	public sealed class CompProperties_Fixture : CompProperties
	{
		public FixtureKind kind;
		public float water_per_use_liters = 1f;
		public float desired_temperature_c = 12f;
		public float waste_water_liters;
		public float sludge_kg;
		public bool wants_hot_water;
		public bool needs_drain;
		public bool kitchen_sink;
		public float linked_stove_water_liters_per_hour;
		public float linked_stove_sludge_kg_per_hour;

		public CompProperties_Fixture()
		{
			compClass = typeof(CompFixture);
		}
	}

	public sealed class CompFixture : ThingComp
	{
		private const int THOUGHT_COOLDOWN_TICKS = 2500;

		public float last_water_temperature_c = RealPhysics.COLD_WATER_TEMPERATURE_C;
		public float total_water_used_liters;
		public string last_reason = string.Empty;
		public int last_stove_activity_tick = -999999;
		public int active_stove_count;
		private int last_thought_tick = -999999;
		private int last_thought_pawn_id = -1;

		public CompProperties_Fixture Props
		{
			get
			{
				return (CompProperties_Fixture)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref last_water_temperature_c, "last_water_temperature_c", RealPhysics.COLD_WATER_TEMPERATURE_C);
			Scribe_Values.Look(ref total_water_used_liters, "total_water_used_liters", 0f);
		}

		public override string CompInspectStringExtra()
		{
			string status = "RealRim_FixtureStatus".Translate(
				last_water_temperature_c.ToStringTemperature("F1"),
				total_water_used_liters.ToString("N1"),
				last_reason);
			if (Props.kitchen_sink)
			{
				int current_tick = Find.TickManager?.TicksGame ?? 0;
				int stove_count = current_tick - last_stove_activity_tick <= 120 ? active_stove_count : 0;
				status += "\n" + "RealRim_KitchenSinkStatus".Translate(
					stove_count,
					(stove_count * Props.linked_stove_water_liters_per_hour).ToString("N1"),
					(stove_count * Props.linked_stove_sludge_kg_per_hour * 1000f).ToString("N0"));
			}
			return status.TrimEnd('\r', '\n', ' ', '\t');
		}

		public AcceptanceReport getWorkingReport()
		{
			return getWorkingReport(
				Props.water_per_use_liters,
				Props.waste_water_liters,
				Props.sludge_kg);
		}

		public bool tryUse(Pawn pawn, out bool cold_water)
		{
			return tryUseVolume(
				pawn,
				Props.water_per_use_liters,
				Props.waste_water_liters,
				Props.sludge_kg,
				true,
				out cold_water);
		}

		public bool tryUseVolume(
			Pawn pawn,
			float water_liters,
			float waste_water_liters,
			float sludge_kg,
			bool apply_thought,
			out bool cold_water)
		{
			cold_water = true;
			AcceptanceReport report = getWorkingReport(water_liters, waste_water_liters, sludge_kg);
			if (!report.Accepted)
			{
				last_reason = report.Reason;
				return false;
			}

			FluidNetwork fresh_network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			FluidNetwork hot_network = FluidUtility.getNetwork(parent, FluidNetworkType.HotWater);
			float hot_temperature = hot_network == null
				? RealPhysics.COLD_WATER_TEMPERATURE_C
				: hot_network.getAverageThermalTemperature();
			float requested_hot_fraction = Props.wants_hot_water
				? RealPhysics.calculateMixedHotFraction(Props.desired_temperature_c, hot_temperature)
				: 0f;
			bool hot_available = hot_network != null
				&& hot_temperature > RealPhysics.COLD_WATER_TEMPERATURE_C + 0.1f
				&& hot_network.getStoredHotWater() > 0.001f
				&& hot_network.getComponents<CompHotWaterTank>().Any();
			float requested_hot_liters = hot_available
				? water_liters * requested_hot_fraction
				: 0f;
			float available_hot_liters = hot_available
				? Mathf.Min(requested_hot_liters, hot_network.getStoredHotWater())
				: 0f;
			float required_cold_liters = water_liters - available_hot_liters;

			if (required_cold_liters > 0.001f
				&& (fresh_network == null
					|| fresh_network.getStoredFreshWater() + 0.001f < required_cold_liters))
			{
				last_reason = "RealRim_NoFreshWater".Translate();
				return false;
			}

			float hot_delivered_liters = available_hot_liters <= 0f
				? 0f
				: hot_network.drawHotWater(available_hot_liters);
			float cold_requested_liters = water_liters - hot_delivered_liters;
			float cold_delivered_liters = cold_requested_liters <= 0.001f
				? 0f
				: fresh_network.drawFreshWater(cold_requested_liters);
			if (cold_delivered_liters + 0.001f < cold_requested_liters)
			{
				last_reason = "RealRim_NoFreshWater".Translate();
				return false;
			}

			float actual_hot_fraction = water_liters <= 0.001f
				? 0f
				: hot_delivered_liters / water_liters;
			last_water_temperature_c = Mathf.Lerp(
				RealPhysics.COLD_WATER_TEMPERATURE_C,
				hot_temperature,
				actual_hot_fraction);
			cold_water = Props.wants_hot_water && last_water_temperature_c < Props.desired_temperature_c - 3f;
			total_water_used_liters += water_liters;

			if (Props.needs_drain)
			{
				FluidNetwork waste_network = FluidUtility.getNetwork(parent, FluidNetworkType.WasteWater);
				if (waste_network == null || !waste_network.pushWaste(waste_water_liters, sludge_kg))
				{
					last_reason = "RealRim_NoWasteCapacity".Translate();
					return false;
				}
			}

			last_reason = cold_water ? "RealRim_ReasonNoHotWater".Translate().ToString() : string.Empty;
			if (apply_thought)
			{
				applyTemperatureThought(pawn, last_water_temperature_c, Props.desired_temperature_c);
			}
			return true;
		}

		public bool recordLinkedStoveUse(float elapsed_seconds)
		{
			if (!Props.kitchen_sink || elapsed_seconds <= 0f)
			{
				return false;
			}

			int current_tick = Find.TickManager?.TicksGame ?? 0;
			if (current_tick != last_stove_activity_tick)
			{
				last_stove_activity_tick = current_tick;
				active_stove_count = 0;
			}
			active_stove_count++;

			float water_liters = Props.linked_stove_water_liters_per_hour * elapsed_seconds / 3600f;
			float sludge_kg = Props.linked_stove_sludge_kg_per_hour * elapsed_seconds / 3600f;
			if (water_liters <= 0f && sludge_kg <= 0f)
			{
				return false;
			}

			bool cold_water;
			return tryUseVolume(
				null,
				water_liters,
				water_liters,
				sludge_kg,
				false,
				out cold_water);
		}

		public float drawDrinkingWater(float requested_liters)
		{
			return drawDrinkingWaterSample(requested_liters).liters;
		}

		public WaterSample drawDrinkingWaterSample(float requested_liters)
		{
			FluidNetwork fresh_network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (fresh_network == null)
			{
				last_reason = "RealRim_NoFreshWater".Translate();
				return new WaterSample();
			}
			WaterSample sample = fresh_network.drawFreshWaterSample(requested_liters);
			total_water_used_liters += sample.liters;
			last_water_temperature_c = RealPhysics.COLD_WATER_TEMPERATURE_C;
			last_reason = sample.liters + 0.001f >= requested_liters
				? string.Empty
				: "RealRim_NoFreshWater".Translate().ToString();
			return sample;
		}

		private AcceptanceReport getWorkingReport(float water_liters, float waste_water_liters, float sludge_kg)
		{
			FluidNetwork fresh_network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (water_liters > 0.001f
				&& (fresh_network == null
					|| fresh_network.getStoredFreshWater() + 0.001f < water_liters))
			{
				return "RealRim_NoFreshWater".Translate();
			}

			FluidNetwork hot_network = FluidUtility.getNetwork(parent, FluidNetworkType.HotWater);
			float hot_temperature = hot_network == null
				? RealPhysics.COLD_WATER_TEMPERATURE_C
				: hot_network.getAverageThermalTemperature();
			float requested_hot_fraction = Props.wants_hot_water
				? RealPhysics.calculateMixedHotFraction(Props.desired_temperature_c, hot_temperature)
				: 0f;
			float available_hot_liters = hot_network == null
				? 0f
				: Mathf.Min(
					water_liters * requested_hot_fraction,
					hot_network.getStoredHotWater());
			float required_fresh_liters = water_liters - available_hot_liters;
			if (required_fresh_liters > 0.001f
				&& (fresh_network == null
					|| fresh_network.getStoredFreshWater() + 0.001f < required_fresh_liters))
			{
				return "RealRim_NoFreshWater".Translate();
			}

			if (Props.needs_drain)
			{
				FluidNetwork waste_network = FluidUtility.getNetwork(parent, FluidNetworkType.WasteWater);
				if (waste_network == null || !waste_network.canAcceptWaste(waste_water_liters, sludge_kg))
				{
					return "RealRim_NoWasteCapacity".Translate();
				}
			}

			return true;
		}

		private void applyTemperatureThought(Pawn pawn, float water_temperature_c, float reference_temperature_c)
		{
			if (pawn == null || pawn.needs == null || pawn.needs.mood == null)
			{
				return;
			}
			int current_tick = Find.TickManager.TicksGame;
			if (last_thought_pawn_id == pawn.thingIDNumber
				&& current_tick - last_thought_tick < THOUGHT_COOLDOWN_TICKS)
			{
				return;
			}

			string thought_name = WaterTemperaturePreferences.selectFixtureThought(
				pawn,
				water_temperature_c,
				reference_temperature_c);
			if (thought_name.NullOrEmpty())
			{
				return;
			}

			ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought_name);
			if (thought != null)
			{
				pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
				last_thought_tick = current_tick;
				last_thought_pawn_id = pawn.thingIDNumber;
			}
		}
	}
}
