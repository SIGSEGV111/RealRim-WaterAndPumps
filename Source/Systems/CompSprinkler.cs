using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RealRim.WaterAndPumps
{
	public enum SprinklerKind
	{
		Irrigation,
		FireSuppression,
	}

	public sealed class CompProperties_Sprinkler : CompProperties
	{
		public SprinklerKind kind;
		public float minimum_radius = 1.9f;
		public float maximum_radius = 6.9f;
		public float radius_step = 1f;
		public float irrigation_liters_per_square_meter = 5f;
		public float irrigation_grid_amount = 3f;
		public int irrigation_hour = 5;
		public int irrigation_animation_ticks = 2500;
		public float fire_flow_liters_per_minute = 80f;
		public float fire_extinguish_damage = 20f;
		public float fire_trigger_temperature_c = 100f;
		public float fire_hold_minutes = 5f;

		public CompProperties_Sprinkler()
		{
			compClass = typeof(CompSprinkler);
		}
	}

	public sealed class CompSprinkler : ThingComp
	{
		private const int OPERATION_INTERVAL_TICKS = 60;
		private const int IRRIGATION_RETRY_TICKS = 600;
		private const float WATER_EPSILON_LITERS = 0.001f;
		private const float FIRE_COOLING_TARGET_C = 20f;
		private const float FIRE_COOLING_FULL_EFFECT_C = 120f;
		private const float FIRE_MAX_COOLING_CHANGE = 416.66666f;
		private const string IRRIGATION_FLECK_DEF = "Mote_Irrigation";
		private const string SPRINKLER_SOUND_DEF = "sprinklers";

		private float radius;
		private float total_water_used_liters;
		private float last_irrigation_water_liters;
		private float current_flow_liters_per_minute;
		private string last_reason = string.Empty;

		private int last_irrigation_day = -1;
		private int next_irrigation_retry_tick;
		private int spray_until_tick;
		private int fire_accounted_until_tick;
		private int next_fire_effect_tick;
		private int cached_spray_cells_tick = -1;
		private float cached_spray_radius = -1f;
		private readonly List<IntVec3> cached_spray_cells = new List<IntVec3>();
		private readonly List<IntVec3> cached_fertile_spray_cells = new List<IntVec3>();
		private CompPowerTrader power_component;
		private CompFlickable flickable_component;
		private CompBreakdownable breakdownable_component;
		private FleckDef irrigation_fleck_def;
		private SoundDef sprinkler_sound_def;
		private Sustainer sprinkler_sustainer;
		private bool was_disabled;

		private CompProperties_Sprinkler Props
		{
			get
			{
				return (CompProperties_Sprinkler)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			cacheRuntimeReferences();
			if (radius <= 0f)
			{
				radius = Props.maximum_radius;
			}
			clampRadius();
			if (Props.kind == SprinklerKind.FireSuppression
				&& Find.TickManager != null
				&& Find.TickManager.TicksGame < spray_until_tick
				&& isPoweredOn())
			{
				current_flow_liters_per_minute = getFireDesignFlow(getSprayCells(false).Count);
			}
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			endSound();
			base.PostDeSpawn(map, mode);
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref radius, "radius", Props.maximum_radius);
			Scribe_Values.Look(ref total_water_used_liters, "sprinkler_total_water_liters", 0f);
			Scribe_Values.Look(ref last_irrigation_water_liters, "sprinkler_last_irrigation_liters", 0f);
			Scribe_Values.Look(ref last_irrigation_day, "sprinkler_last_irrigation_day", -1);
			Scribe_Values.Look(ref spray_until_tick, "sprinkler_spray_until_tick", 0);
			Scribe_Values.Look(ref fire_accounted_until_tick, "sprinkler_fire_accounted_until_tick", 0);
			Scribe_Values.Look(ref next_fire_effect_tick, "sprinkler_next_fire_effect_tick", 0);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				clampRadius();
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			if (!parent.Spawned)
			{
				return;
			}

			int current_tick = Find.TickManager.TicksGame;
			if (!isPoweredOn())
			{
				if (Props.kind == SprinklerKind.FireSuppression)
				{
					spray_until_tick = current_tick;
					fire_accounted_until_tick = current_tick;
				}
				current_flow_liters_per_minute = 0f;
				last_reason = "RealRim_ReasonSwitchedOff".Translate();
				was_disabled = true;
				endSound();
				return;
			}
			if (was_disabled)
			{
				was_disabled = false;
				last_reason = string.Empty;
			}

			if (parent.IsHashIntervalTick(OPERATION_INTERVAL_TICKS))
			{
				if (Props.kind == SprinklerKind.Irrigation)
				{
					tickIrrigation(current_tick);
				}
				else
				{
					tickFireSuppression(current_tick);
				}

				if (current_tick < spray_until_tick)
				{
					spawnSprayFleck();
				}
			}

			if (Props.kind == SprinklerKind.FireSuppression
				&& current_tick < spray_until_tick
				&& current_tick >= next_fire_effect_tick)
			{
				applyFireEffect(current_tick, getSprayCells(false));
			}

			if (current_tick < spray_until_tick)
			{
				maintainSound();
			}
			else
			{
				current_flow_liters_per_minute = 0f;
				endSound();
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			GenDraw.DrawRadiusRing(parent.Position, radius);
			if (Props.kind == SprinklerKind.Irrigation)
			{
				DbhIrrigationBridge.markForDraw(parent.Map);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo gizmo in base.CompGetGizmosExtra())
			{
				yield return gizmo;
			}

			bool radius_locked = Props.kind == SprinklerKind.Irrigation
				&& Find.TickManager != null
				&& Find.TickManager.TicksGame < spray_until_tick;

			if (!radius_locked && radius > Props.minimum_radius + 0.01f)
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_SprinklerRadiusDown".Translate(),
					defaultDesc = "RealRim_SprinklerRadiusDownDesc".Translate(),
					icon = RealRimTextures.lower_target,
					action = delegate
					{
						radius -= Props.radius_step;
						clampRadius();
					},
				};
			}

			if (!radius_locked && radius < Props.maximum_radius - 0.01f)
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_SprinklerRadiusUp".Translate(),
					defaultDesc = "RealRim_SprinklerRadiusUpDesc".Translate(),
					icon = RealRimTextures.raise_target,
					action = delegate
					{
						radius += Props.radius_step;
						clampRadius();
					},
				};
			}

			if (Props.kind == SprinklerKind.Irrigation)
			{
				yield return new Command_Action
				{
					defaultLabel = "CommandSunLampMakeGrowingZoneLabel".Translate(),
					defaultDesc = "CommandSunLampMakeGrowingZoneDesc".Translate(),
					icon = RealRimTextures.growing_zone,
					hotKey = KeyBindingDefOf.Misc2,
					action = makeMatchingGrowZone,
				};
			}
		}

		public override string CompInspectStringExtra()
		{
			int covered_cells = getSprayCells(false).Count;
			bool is_spraying = isPoweredOn()
				&& Find.TickManager.TicksGame < spray_until_tick;
			string state = is_spraying
				? "RealRim_StatusRunning".Translate()
				: "RealRim_StatusStandby".Translate();
			string reason = last_reason.NullOrEmpty() ? string.Empty : last_reason;

			if (Props.kind == SprinklerKind.Irrigation)
			{
				int fertile_cells = getSprayCells(true).Count;
				float required_liters = covered_cells * Props.irrigation_liters_per_square_meter;
				return "RealRim_IrrigationSprinklerStatus".Translate(
					state,
					radius.ToString("N1"),
					covered_cells,
					fertile_cells,
					required_liters.ToString("N0"),
					Props.irrigation_liters_per_square_meter.ToString("N1"),
					last_irrigation_water_liters.ToString("N0"),
					total_water_used_liters.ToString("N0"),
					reason).ToString().TrimEnd('\r', '\n', ' ', '\t');
			}

			float design_flow = getFireDesignFlow(covered_cells);
			return "RealRim_FireSprinklerStatus".Translate(
				state,
				radius.ToString("N1"),
				covered_cells,
				design_flow.ToString("N1"),
				current_flow_liters_per_minute.ToString("N1"),
				total_water_used_liters.ToString("N1"),
				reason).ToString().TrimEnd('\r', '\n', ' ', '\t');
		}

		internal bool triggerManually()
		{
			if (Props.kind != SprinklerKind.FireSuppression || !parent.Spawned)
			{
				return false;
			}

			int current_tick = Find.TickManager.TicksGame;
			List<IntVec3> cells = getSprayCells(false);
			activateFireSuppression(current_tick);
			accountFireSuppression(current_tick, cells);
			if (current_tick < spray_until_tick)
			{
				applyFireEffect(current_tick, cells);
			}
			return current_tick < spray_until_tick;
		}

		private void makeMatchingGrowZone()
		{
			Designator_ZoneAdd_Growing designator =
				DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_Growing>();
			if (designator == null)
			{
				return;
			}

			List<IntVec3> cells = getSprayCells(true);
			List<IntVec3> accepted_cells = new List<IntVec3>(cells.Count);
			for (int index = 0; index < cells.Count; index++)
			{
				if (designator.CanDesignateCell(cells[index]).Accepted)
				{
					accepted_cells.Add(cells[index]);
				}
			}

			if (accepted_cells.Count > 0)
			{
				designator.DesignateMultiCell(accepted_cells);
			}
		}

		private void tickIrrigation(int current_tick)
		{
			current_flow_liters_per_minute = 0f;
			if (current_tick < spray_until_tick)
			{
				applyIrrigationPulse();
				return;
			}

			if (GenLocalDate.HourOfDay(parent) != Props.irrigation_hour)
			{
				return;
			}

			int local_day = GenLocalDate.Year(parent) * 60 + GenLocalDate.DayOfYear(parent);
			if (last_irrigation_day == local_day || current_tick < next_irrigation_retry_tick)
			{
				return;
			}
			next_irrigation_retry_tick = current_tick + IRRIGATION_RETRY_TICKS;

			List<IntVec3> spray_cells = getSprayCells(false);
			List<IntVec3> fertile_cells = getSprayCells(true);
			if (fertile_cells.Count == 0)
			{
				last_reason = "RealRim_SprinklerNoEligibleCells".Translate();
				return;
			}
			if (!DbhIrrigationBridge.isAvailable(parent.Map))
			{
				last_reason = "RealRim_SprinklerIrrigationUnavailable".Translate();
				return;
			}

			float requested_liters = spray_cells.Count * Props.irrigation_liters_per_square_meter;
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (network == null || network.getStoredFreshWater() + WATER_EPSILON_LITERS < requested_liters)
			{
				last_reason = "RealRim_NoFreshWater".Translate();
				return;
			}

			int applied_cells = applyIrrigationPulse(fertile_cells);
			if (applied_cells == 0)
			{
				last_irrigation_water_liters = 0f;
				last_reason = "RealRim_SprinklerIrrigationUnavailable".Translate();
				return;
			}

			last_irrigation_day = local_day;
			float charged_liters = requested_liters * applied_cells / fertile_cells.Count;
			float delivered_liters = network.drawFreshWater(charged_liters);
			last_irrigation_water_liters = delivered_liters;
			total_water_used_liters += delivered_liters;
			if (delivered_liters + WATER_EPSILON_LITERS < charged_liters)
			{
				Log.ErrorOnce(
					"[RealRim] Water & Pumps: the irrigation sprinkler received less water "
					+ "than the fresh-water network reported as available.",
					77160424);
				last_reason = "RealRim_NoFreshWater".Translate();
				return;
			}

			if (applied_cells < fertile_cells.Count)
			{
				last_reason = "RealRim_SprinklerIrrigationPartial".Translate(
					applied_cells,
					fertile_cells.Count);
				return;
			}

			spray_until_tick = current_tick + Props.irrigation_animation_ticks;
			last_reason = string.Empty;
		}

		private void applyIrrigationPulse()
		{
			List<IntVec3> cells = getSprayCells(true);
			if (cells.Count == 0)
			{
				return;
			}

			int applied_cells = applyIrrigationPulse(cells);
			if (applied_cells < cells.Count)
			{
				spray_until_tick = Find.TickManager.TicksGame;
				last_reason = applied_cells == 0
					? "RealRim_SprinklerIrrigationUnavailable".Translate()
					: "RealRim_SprinklerIrrigationPartial".Translate(applied_cells, cells.Count);
			}
		}

		private int applyIrrigationPulse(List<IntVec3> cells)
		{
			return DbhIrrigationBridge.addIrrigation(
				parent.Map,
				cells,
				Props.irrigation_grid_amount);
		}

		private void tickFireSuppression(int current_tick)
		{
			List<IntVec3> cells = getSprayCells(false);
			if (hasFireOrExcessiveHeat(cells))
			{
				activateFireSuppression(current_tick);
			}

			if (current_tick >= spray_until_tick)
			{
				current_flow_liters_per_minute = 0f;
				return;
			}

			accountFireSuppression(current_tick, cells);
		}

		private void activateFireSuppression(int current_tick)
		{
			if (current_tick >= spray_until_tick)
			{
				fire_accounted_until_tick = current_tick;
				next_fire_effect_tick = current_tick;
			}
			spray_until_tick = Mathf.Max(spray_until_tick, current_tick + getFireHoldTicks());
			last_reason = string.Empty;
		}

		private void accountFireSuppression(int current_tick, List<IntVec3> cells)
		{
			int accounting_start_tick = Mathf.Max(current_tick, fire_accounted_until_tick);
			int accounting_end_tick = Mathf.Min(
				current_tick + OPERATION_INTERVAL_TICKS,
				spray_until_tick);
			if (accounting_end_tick <= accounting_start_tick)
			{
				current_flow_liters_per_minute = getFireDesignFlow(cells.Count);
				return;
			}

			float design_flow_liters_per_minute = getFireDesignFlow(cells.Count);
			float interval_minutes = (accounting_end_tick - accounting_start_tick)
				* RealPhysics.SECONDS_PER_GAME_TICK / 60f;
			float requested_liters = design_flow_liters_per_minute * interval_minutes;
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (network == null || network.getStoredFreshWater() + WATER_EPSILON_LITERS < requested_liters)
			{
				spray_until_tick = Mathf.Min(spray_until_tick, fire_accounted_until_tick);
				current_flow_liters_per_minute = current_tick < spray_until_tick
					? design_flow_liters_per_minute
					: 0f;
				last_reason = "RealRim_NoFreshWater".Translate();
				return;
			}

			float delivered_liters = network.drawFreshWater(requested_liters);
			if (delivered_liters + WATER_EPSILON_LITERS < requested_liters)
			{
				spray_until_tick = Mathf.Min(spray_until_tick, fire_accounted_until_tick);
				current_flow_liters_per_minute = current_tick < spray_until_tick
					? design_flow_liters_per_minute
					: 0f;
				last_reason = "RealRim_NoFreshWater".Translate();
				return;
			}

			fire_accounted_until_tick = accounting_end_tick;
			total_water_used_liters += delivered_liters;
			current_flow_liters_per_minute = design_flow_liters_per_minute;
			last_reason = string.Empty;
		}

		private void applyFireEffect(int current_tick, List<IntVec3> cells)
		{
			extinguishFires(cells);
			coolRoom();
			next_fire_effect_tick = current_tick + OPERATION_INTERVAL_TICKS;
		}

		private void coolRoom()
		{
			Map map = parent.Map;
			if (map == null || parent.Position.UsesOutdoorTemperature(map))
			{
				return;
			}

			float ambient_temperature_c = parent.AmbientTemperature;
			float temperature_factor = Mathf.Clamp01(Mathf.InverseLerp(
				FIRE_COOLING_TARGET_C,
				FIRE_COOLING_FULL_EFFECT_C,
				ambient_temperature_c));
			float flow_factor = Props.fire_flow_liters_per_minute <= 0f
				? 0f
				: Mathf.Clamp01(current_flow_liters_per_minute / Props.fire_flow_liters_per_minute);
			float requested_change = -FIRE_MAX_COOLING_CHANGE * temperature_factor * flow_factor;
			if (requested_change >= -0.001f)
			{
				return;
			}

			float applied_change = GenTemperature.ControlTemperatureTempChange(
				parent.Position,
				map,
				requested_change,
				FIRE_COOLING_TARGET_C);
			if (Mathf.Approximately(applied_change, 0f))
			{
				return;
			}

			Room room = parent.Position.GetRoom(map);
			if (room != null)
			{
				room.Temperature += applied_change;
			}
		}

		private bool hasFireOrExcessiveHeat(List<IntVec3> cells)
		{
			if (parent.AmbientTemperature > Props.fire_trigger_temperature_c)
			{
				return true;
			}

			for (int index = 0; index < cells.Count; index++)
			{
				if (FireUtility.ContainsStaticFire(cells[index], parent.Map))
				{
					return true;
				}
			}
			return false;
		}

		private void extinguishFires(List<IntVec3> cells)
		{
			for (int index = 0; index < cells.Count; index++)
			{
				Thing fire = cells[index].GetFirstThing(parent.Map, ThingDefOf.Fire);
				if (fire != null)
				{
					fire.TakeDamage(new DamageInfo(
						DamageDefOf.Extinguish,
						Props.fire_extinguish_damage));
				}
			}
		}

		private List<IntVec3> getSprayCells(bool fertile_only)
		{
			Map map = parent.Map;
			int current_tick = Find.TickManager?.TicksGame ?? 0;
			if (map == null)
			{
				cached_spray_cells.Clear();
				cached_fertile_spray_cells.Clear();
				return fertile_only ? cached_fertile_spray_cells : cached_spray_cells;
			}
			if (cached_spray_cells_tick < 0
				|| current_tick - cached_spray_cells_tick >= OPERATION_INTERVAL_TICKS
				|| Mathf.Abs(cached_spray_radius - radius) >= 0.001f)
			{
				cacheSprayCells(map, current_tick);
			}
			return fertile_only ? cached_fertile_spray_cells : cached_spray_cells;
		}

		private void cacheSprayCells(Map map, int current_tick)
		{
			cached_spray_cells_tick = current_tick;
			cached_spray_radius = radius;
			cached_spray_cells.Clear();
			cached_fertile_spray_cells.Clear();
			foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, radius, true))
			{
				if (!cell.InBounds(map))
				{
					continue;
				}
				if (cell != parent.Position && !GenSight.LineOfSight(parent.Position, cell, map))
				{
					continue;
				}

				cached_spray_cells.Add(cell);
				if (cell.GetTerrain(map).fertility > 0f)
				{
					cached_fertile_spray_cells.Add(cell);
				}
			}
		}

		private int getFireHoldTicks()
		{
			float hold_seconds = Mathf.Max(0f, Props.fire_hold_minutes) * 60f;
			return Mathf.Max(1, Mathf.RoundToInt(hold_seconds / RealPhysics.SECONDS_PER_GAME_TICK));
		}

		private float getFireDesignFlow(int covered_cells)
		{
			int maximum_cells = Mathf.Max(1, GenRadial.NumCellsInRadius(Props.maximum_radius));
			return Props.fire_flow_liters_per_minute * covered_cells / maximum_cells;
		}

		private void spawnSprayFleck()
		{
			if (irrigation_fleck_def == null || parent.Map == null)
			{
				return;
			}

			float diameter = radius * 2f;
			FleckCreationData data = FleckMaker.GetDataStatic(
				parent.Position.ToVector3Shifted(),
				parent.Map,
				irrigation_fleck_def,
				diameter);
			data.exactScale = new Vector3(diameter, 1f, diameter);
			data.rotationRate = Rand.Chance(0.5f) ? 30f : -30f;
			parent.Map.flecks.CreateFleck(data);
		}

		private void maintainSound()
		{
			if (sprinkler_sustainer == null || sprinkler_sustainer.Ended)
			{
				if (sprinkler_sound_def != null)
				{
					sprinkler_sustainer = SoundStarter.TrySpawnSustainer(
						sprinkler_sound_def,
						SoundInfo.InMap(parent, MaintenanceType.PerTick));
				}
			}
			else
			{
				sprinkler_sustainer.Maintain();
			}
		}

		private void endSound()
		{
			if (sprinkler_sustainer != null && !sprinkler_sustainer.Ended)
			{
				sprinkler_sustainer.End();
			}
			sprinkler_sustainer = null;
		}

		private void cacheRuntimeReferences()
		{
			power_component = parent.TryGetComp<CompPowerTrader>();
			flickable_component = parent.TryGetComp<CompFlickable>();
			breakdownable_component = parent.TryGetComp<CompBreakdownable>();
			irrigation_fleck_def = DefDatabase<FleckDef>.GetNamedSilentFail(IRRIGATION_FLECK_DEF);
			sprinkler_sound_def = DefDatabase<SoundDef>.GetNamedSilentFail(SPRINKLER_SOUND_DEF);
		}

		private bool isPoweredOn()
		{
			return (power_component == null || power_component.PowerOn)
				&& (flickable_component == null || flickable_component.SwitchIsOn)
				&& (breakdownable_component == null || !breakdownable_component.BrokenDown);
		}

		private void clampRadius()
		{
			radius = Mathf.Clamp(radius, Props.minimum_radius, Props.maximum_radius);
			cached_spray_cells_tick = -1;
		}
	}
}
