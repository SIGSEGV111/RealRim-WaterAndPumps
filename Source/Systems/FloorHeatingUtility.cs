using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	internal static class FloorHeatingUtility
	{
		public const string FLOOR_HEATING_DEF_NAME = "RealRim_FloorHeating";
		private const int GROUP_CACHE_REFRESH_TICKS = 250;
		private const float SNOW_FULL_DEPTH_MASS_KG_PER_M2 = 100f;
		private const float SNOW_MELT_SENSIBLE_HEAT_KJ_PER_KG = 21f;
		private const float SNOW_MELT_ENERGY_KJ_PER_FULL_DEPTH =
			SNOW_FULL_DEPTH_MASS_KG_PER_M2
			* (RealPhysics.WATER_FREEZING_LATENT_HEAT_KJ_PER_KG + SNOW_MELT_SENSIBLE_HEAT_KJ_PER_KG);

		private static readonly Dictionary<int, CachedFloorHeatingGroup> GROUP_CACHE =
			new Dictionary<int, CachedFloorHeatingGroup>();

		public static bool hasFloorHeatingAt(IntVec3 cell, Map map, Thing thing_to_ignore = null)
		{
			if (map == null || !cell.InBounds(map))
			{
				return false;
			}

			List<Thing> things = cell.GetThingList(map);
			for (int index = 0; index < things.Count; index++)
			{
				Thing thing = things[index];
				if (thing == thing_to_ignore)
				{
					continue;
				}
				ThingWithComps thing_with_comps = thing as ThingWithComps;
				if (thing_with_comps != null && thing_with_comps.TryGetComp<CompFloorHeating>() != null)
				{
					return true;
				}
			}
			return false;
		}

		public static bool hasFullFloorHeatingCoverage(Thing thing)
		{
			if (thing == null || !thing.Spawned || thing.Map == null)
			{
				return false;
			}

			foreach (IntVec3 cell in thing.OccupiedRect())
			{
				if (!hasFloorHeatingAt(cell, thing.Map))
				{
					return false;
				}
			}
			return true;
		}

		public static bool shouldApplyComfortBonus(Thing thing, float current_comfort)
		{
			return getComfortStatusFor(thing, current_comfort, true).applies;
		}

		public static float getComfortBonusFor(Thing thing, float current_comfort)
		{
			FloorHeatingComfortStatus status = getComfortStatusFor(thing, current_comfort, true);
			return status.applies ? status.bonus : 0f;
		}

		public static FloorHeatingComfortStatus getComfortStatusFor(
			Thing thing,
			float current_comfort,
			bool require_positive_comfort)
		{
			FloorHeatingComfortStatus status = new FloorHeatingComfortStatus();
			ThingWithComps thing_with_comps = thing as ThingWithComps;
			if (thing_with_comps == null || thing.def == null || thing.def.category != ThingCategory.Building)
			{
				status.reason = "RealRim_FloorHeatingComfortReasonNotFurniture".Translate().ToString();
				return status;
			}

			if (!thing.Spawned || thing.Map == null)
			{
				status.reason = "RealRim_FloorHeatingComfortReasonNotSpawned".Translate().ToString();
				return status;
			}

			if (require_positive_comfort && current_comfort <= 0f)
			{
				status.reason = "RealRim_FloorHeatingComfortReasonNoBaseComfort".Translate().ToString();
				return status;
			}

			if (thing.def.defName == FLOOR_HEATING_DEF_NAME || thing_with_comps.TryGetComp<CompFacility>() != null)
			{
				status.reason = "RealRim_FloorHeatingComfortReasonNotFurniture".Translate().ToString();
				return status;
			}

			List<CompFloorHeating> floor_heatings = getFloorHeatingCoverageUnder(thing);
			if (floor_heatings == null || floor_heatings.Count == 0)
			{
				status.reason = "RealRim_FloorHeatingComfortReasonMissingCoverage".Translate().ToString();
				return status;
			}

			for (int index = 0; index < floor_heatings.Count; index++)
			{
				CompFloorHeating floor_heating = floor_heatings[index];
				status.floor_heating = status.floor_heating ?? floor_heating;
				if (!isFloorHeatingComfortActive(floor_heating, out status.reason))
				{
					return status;
				}
			}

			status.applies = true;
			status.bonus = status.floor_heating == null ? 0f : status.floor_heating.Props.comfort_bonus;
			status.reason = "RealRim_FloorHeatingComfortReasonActive".Translate(
				status.floor_heating.last_transfer_kw.ToString("N3"),
				status.floor_heating.last_medium_temperature_c.ToStringTemperature("F1"),
				status.floor_heating.last_room_temperature_c.ToStringTemperature("F1")).ToString();
			return status;
		}

		public static void tickFloorHeating(CompFloorHeating floor_heating, float elapsed_seconds)
		{
			if (floor_heating == null || floor_heating.parent == null || !floor_heating.parent.Spawned)
			{
				resetRuntimeState(floor_heating);
				return;
			}

			Map map = floor_heating.parent.Map;
			Room room = floor_heating.parent.GetRoom();
			bool outdoor_mode = room == null || room.PsychologicallyOutdoors;
			floor_heating.last_outdoor_mode = outdoor_mode;
			floor_heating.last_room_temperature_c = outdoor_mode
				? map.mapTemperature.OutdoorTemp
				: GenTemperature.GetTemperatureForCell(floor_heating.parent.Position, map);

			FluidNetwork network = FluidUtility.getNetwork(floor_heating.parent, FluidNetworkType.Heating);
			if (network == null)
			{
				resetRuntimeState(floor_heating);
				floor_heating.last_outdoor_mode = outdoor_mode;
				floor_heating.last_room_temperature_c = outdoor_mode
					? map.mapTemperature.OutdoorTemp
					: GenTemperature.GetTemperatureForCell(floor_heating.parent.Position, map);
				floor_heating.last_reason = "RealRim_ReasonNoHeatingNetwork".Translate();
				return;
			}

			int cache_key = getGroupCacheKey(map, network, room, outdoor_mode);
			CachedFloorHeatingGroup group = getGroup(cache_key, network, room, outdoor_mode);
			if (group.last_processed_tick == Find.TickManager.TicksGame)
			{
				return;
			}

			group.last_processed_tick = Find.TickManager.TicksGame;
			if (outdoor_mode)
			{
				tickOutdoorGroup(group, elapsed_seconds);
			}
			else
			{
				tickIndoorGroup(group, elapsed_seconds);
			}
		}

		private static CompFloorHeating getFirstFloorHeatingUnder(Thing thing)
		{
			if (thing == null || !thing.Spawned || thing.Map == null)
			{
				return null;
			}

			foreach (IntVec3 cell in thing.OccupiedRect())
			{
				List<Thing> things = cell.GetThingList(thing.Map);
				for (int index = 0; index < things.Count; index++)
				{
					ThingWithComps thing_with_comps = things[index] as ThingWithComps;
					CompFloorHeating floor_heating = thing_with_comps?.TryGetComp<CompFloorHeating>();
					if (floor_heating != null)
					{
						return floor_heating;
					}
				}
			}
			return null;
		}

		private static List<CompFloorHeating> getFloorHeatingCoverageUnder(Thing thing)
		{
			List<CompFloorHeating> floor_heatings = new List<CompFloorHeating>();
			if (thing == null || !thing.Spawned || thing.Map == null)
			{
				return floor_heatings;
			}

			foreach (IntVec3 cell in thing.OccupiedRect())
			{
				CompFloorHeating floor_heating = getFloorHeatingAt(cell, thing.Map);
				if (floor_heating == null)
				{
					floor_heatings.Clear();
					return floor_heatings;
				}
				floor_heatings.Add(floor_heating);
			}
			return floor_heatings;
		}

		private static CompFloorHeating getFloorHeatingAt(IntVec3 cell, Map map)
		{
			if (map == null || !cell.InBounds(map))
			{
				return null;
			}

			List<Thing> things = cell.GetThingList(map);
			for (int index = 0; index < things.Count; index++)
			{
				ThingWithComps thing_with_comps = things[index] as ThingWithComps;
				CompFloorHeating floor_heating = thing_with_comps?.TryGetComp<CompFloorHeating>();
				if (floor_heating != null)
				{
					return floor_heating;
				}
			}
			return null;
		}

		private static bool isFloorHeatingComfortActive(CompFloorHeating floor_heating, out string reason)
		{
			reason = string.Empty;
			if (floor_heating == null || floor_heating.parent == null || !floor_heating.parent.Spawned)
			{
				reason = "RealRim_FloorHeatingComfortReasonNotSpawned".Translate().ToString();
				return false;
			}

			Room room = floor_heating.parent.GetRoom();
			if (room == null || room.PsychologicallyOutdoors)
			{
				reason = "RealRim_FloorHeatingComfortReasonOutdoor".Translate().ToString();
				return false;
			}

			FluidNetwork network = FluidUtility.getNetwork(floor_heating.parent, FluidNetworkType.Heating);
			if (network == null)
			{
				reason = "RealRim_ReasonNoHeatingNetwork".Translate().ToString();
				return false;
			}

			if (floor_heating.last_transfer_kw <= 0.0001f)
			{
				reason = floor_heating.last_reason.NullOrEmpty()
					? "RealRim_FloorHeatingComfortReasonNoCurrentHeat".Translate().ToString()
					: floor_heating.last_reason;
				return false;
			}

			return true;
		}

		private static CachedFloorHeatingGroup getGroup(
			int cache_key,
			FluidNetwork network,
			Room room,
			bool outdoor_mode)
		{
			CachedFloorHeatingGroup group;
			if (!GROUP_CACHE.TryGetValue(cache_key, out group))
			{
				group = new CachedFloorHeatingGroup();
				GROUP_CACHE[cache_key] = group;
			}

			int tick = Find.TickManager.TicksGame;
			group.last_seen_tick = tick;
			if (group.network != network
				|| group.last_rebuild_tick + GROUP_CACHE_REFRESH_TICKS < tick
				|| group.tiles.Count == 0)
			{
				rebuildGroup(group, network, room, outdoor_mode, tick);
				pruneCache(tick);
			}
			return group;
		}

		private static void rebuildGroup(
			CachedFloorHeatingGroup group,
			FluidNetwork network,
			Room room,
			bool outdoor_mode,
			int tick)
		{
			group.network = network;
			group.room = room;
			group.outdoor_mode = outdoor_mode;
			group.last_rebuild_tick = tick;
			group.last_processed_tick = -1;
			group.tiles.Clear();
			group.surface_m2 = 0f;
			group.indoor_conductance_w_per_k = 0f;
			group.outdoor_conductance_w_per_k = 0f;
			foreach (CompFloorHeating candidate in network.getComponents<CompFloorHeating>())
			{
				if (candidate == null || candidate.parent == null || !candidate.parent.Spawned)
				{
					continue;
				}

				Room candidate_room = candidate.parent.GetRoom();
				bool candidate_outdoor = candidate_room == null || candidate_room.PsychologicallyOutdoors;
				if (candidate_outdoor != outdoor_mode || (!outdoor_mode && candidate_room != room))
				{
					continue;
				}

				float surface_m2 = Mathf.Max(0f, candidate.Props.heat_exchanger_surface_m2);
				group.tiles.Add(candidate);
				group.surface_m2 += surface_m2;
				group.indoor_conductance_w_per_k += surface_m2
					* Mathf.Max(0f, candidate.Props.heat_transfer_w_per_m2_k);
				group.outdoor_conductance_w_per_k += surface_m2
					* Mathf.Max(0f, candidate.Props.outdoor_heat_transfer_w_per_m2_k);
			}
		}

		private static void tickIndoorGroup(CachedFloorHeatingGroup group, float elapsed_seconds)
		{
			float room_temperature_c = getGroupTemperature(group);
			float heating_temperature_c = group.network.getAverageThermalTemperature();
			float temperature_delta_c = heating_temperature_c - room_temperature_c;
			if (elapsed_seconds <= 0f || group.indoor_conductance_w_per_k <= 0f)
			{
				applyGroupState(group, room_temperature_c, heating_temperature_c, 0f, 0f, "");
				return;
			}
			if (temperature_delta_c <= 0.001f)
			{
				applyGroupState(
					group,
					room_temperature_c,
					heating_temperature_c,
					0f,
					0f,
					"RealRim_ReasonNoHeatDelta".Translate());
				return;
			}

			float requested_kw = group.indoor_conductance_w_per_k * temperature_delta_c / 1000f;
			float delivered_kj = group.network.drawThermalEnergy(requested_kw * elapsed_seconds);
			float delivered_kw = delivered_kj / elapsed_seconds;
			if (delivered_kw > 0f)
			{
				GenTemperature.PushHeat(
					group.tiles[0].parent,
					delivered_kw * RealPhysics.RIMWORLD_HEAT_UNITS_PER_KW_SECOND * elapsed_seconds);
			}
			applyGroupState(
				group,
				room_temperature_c,
				heating_temperature_c,
				delivered_kw,
				0f,
				delivered_kw > 0f ? string.Empty : "RealRim_ReasonNoStoredHeat".Translate().ToString());
		}

		private static void tickOutdoorGroup(CachedFloorHeatingGroup group, float elapsed_seconds)
		{
			Map map = group.tiles.Count == 0 ? null : group.tiles[0].parent.Map;
			float outdoor_temperature_c = map == null
				? RealPhysics.COLD_WATER_TEMPERATURE_C
				: map.mapTemperature.OutdoorTemp;
			float heating_temperature_c = group.network.getAverageThermalTemperature();
			if (map == null || elapsed_seconds <= 0f || group.outdoor_conductance_w_per_k <= 0f)
			{
				applyGroupState(group, outdoor_temperature_c, heating_temperature_c, 0f, 0f, "");
				return;
			}

			if (outdoor_temperature_c >= 1f || map.weatherManager.RainRate <= 0.001f)
			{
				applyGroupState(
					group,
					outdoor_temperature_c,
					heating_temperature_c,
					0f,
					0f,
					"RealRim_ReasonNoWinterPrecipitation".Translate());
				return;
			}

			float temperature_delta_c = heating_temperature_c - outdoor_temperature_c;
			if (temperature_delta_c <= 0.001f)
			{
				applyGroupState(
					group,
					outdoor_temperature_c,
					heating_temperature_c,
					0f,
					0f,
					"RealRim_ReasonNoHeatDelta".Translate());
				return;
			}

			float requested_kw = group.outdoor_conductance_w_per_k * temperature_delta_c / 1000f;
			float delivered_kj = group.network.drawThermalEnergy(requested_kw * elapsed_seconds);
			float delivered_kw = delivered_kj / elapsed_seconds;
			float removed_snow_depth = removeSnow(group, delivered_kj);
			applyGroupState(
				group,
				outdoor_temperature_c,
				heating_temperature_c,
				delivered_kw,
				removed_snow_depth,
				delivered_kw > 0f ? string.Empty : "RealRim_ReasonNoStoredHeat".Translate().ToString());
		}

		private static float removeSnow(CachedFloorHeatingGroup group, float delivered_kj)
		{
			float remaining_kj = Mathf.Max(0f, delivered_kj);
			float removed_depth = 0f;
			for (int index = 0; index < group.tiles.Count && remaining_kj > 0.001f; index++)
			{
				CompFloorHeating floor_heating = group.tiles[index];
				Map map = floor_heating.parent.Map;
				float snow_depth = map.snowGrid.GetDepth(floor_heating.parent.Position);
				if (snow_depth <= 0.001f)
				{
					continue;
				}

				float removable_depth = Mathf.Min(
					snow_depth,
					remaining_kj / SNOW_MELT_ENERGY_KJ_PER_FULL_DEPTH);
				if (removable_depth <= 0.001f)
				{
					continue;
				}

				map.snowGrid.AddDepth(floor_heating.parent.Position, -removable_depth);
				remaining_kj -= removable_depth * SNOW_MELT_ENERGY_KJ_PER_FULL_DEPTH;
				removed_depth += removable_depth;
			}
			return removed_depth;
		}

		private static float getGroupTemperature(CachedFloorHeatingGroup group)
		{
			if (group.tiles.Count == 0)
			{
				return RealPhysics.COLD_WATER_TEMPERATURE_C;
			}
			if (group.outdoor_mode)
			{
				return group.tiles[0].parent.Map.mapTemperature.OutdoorTemp;
			}
			return GenTemperature.GetTemperatureForCell(
				group.tiles[0].parent.Position,
				group.tiles[0].parent.Map);
		}

		private static void applyGroupState(
			CachedFloorHeatingGroup group,
			float room_temperature_c,
			float heating_temperature_c,
			float transfer_kw,
			float removed_snow_depth,
			string reason)
		{
			for (int index = 0; index < group.tiles.Count; index++)
			{
				CompFloorHeating floor_heating = group.tiles[index];
				floor_heating.last_room_temperature_c = room_temperature_c;
				floor_heating.last_medium_temperature_c = heating_temperature_c;
				floor_heating.last_group_surface_m2 = group.surface_m2;
				floor_heating.last_outdoor_mode = group.outdoor_mode;
				floor_heating.last_reason = reason ?? string.Empty;

				float share = group.surface_m2 <= 0.001f
					? 0f
					: Mathf.Max(0f, floor_heating.Props.heat_exchanger_surface_m2) / group.surface_m2;
				floor_heating.last_transfer_kw = transfer_kw * share;
				floor_heating.last_snow_depth_removed = removed_snow_depth * share;
			}
		}

		private static void resetRuntimeState(CompFloorHeating floor_heating)
		{
			if (floor_heating == null)
			{
				return;
			}
			floor_heating.last_transfer_kw = 0f;
			floor_heating.last_group_surface_m2 = floor_heating.Props.heat_exchanger_surface_m2;
			floor_heating.last_snow_depth_removed = 0f;
			floor_heating.last_reason = string.Empty;
		}

		private static int getGroupCacheKey(Map map, FluidNetwork network, Room room, bool outdoor_mode)
		{
			unchecked
			{
				int result = 17;
				result = result * 31 + (map == null ? 0 : map.uniqueID);
				result = result * 31 + (network == null ? 0 : network.network_id);
				result = result * 31 + (outdoor_mode ? -1 : (room == null ? 0 : room.ID));
				return result;
			}
		}

		private static void pruneCache(int tick)
		{
			List<int> stale_keys = GROUP_CACHE
				.Where(pair => pair.Value.last_seen_tick + 5000 < tick)
				.Select(pair => pair.Key)
				.ToList();
			for (int index = 0; index < stale_keys.Count; index++)
			{
				GROUP_CACHE.Remove(stale_keys[index]);
			}
		}

		private sealed class CachedFloorHeatingGroup
		{
			public readonly List<CompFloorHeating> tiles = new List<CompFloorHeating>();
			public FluidNetwork network;
			public Room room;
			public bool outdoor_mode;
			public float surface_m2;
			public float indoor_conductance_w_per_k;
			public float outdoor_conductance_w_per_k;
			public int last_rebuild_tick = -1;
			public int last_processed_tick = -1;
			public int last_seen_tick = -1;
		}
	}

	public sealed class FloorHeatingComfortStatus
	{
		public bool applies;
		public float bonus;
		public string reason = string.Empty;
		public CompFloorHeating floor_heating;
	}

	internal static class FloorHeatingComfortFormatting
	{
		public static string buildStatExplanationLine(FloorHeatingComfortStatus status)
		{
			if (status == null)
			{
				return string.Empty;
			}
			if (status.applies)
			{
				return "RealRim_FloorHeatingComfortAppliedStat".Translate(
					status.bonus.ToString("+0.00;-0.00;0.00"),
					status.reason).ToString();
			}
			return "RealRim_FloorHeatingComfortInactiveStat".Translate(status.reason).ToString();
		}
	}
}
