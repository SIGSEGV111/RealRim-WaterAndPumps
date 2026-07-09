using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_RainwaterCollector : CompProperties
	{
		public int collection_radius = 3;
		public float nominal_liters_per_roof_tile_per_hour = 30f;

		public CompProperties_RainwaterCollector()
		{
			compClass = typeof(CompRainwaterCollector);
		}
	}

	public sealed class CompRainwaterCollector : ThingComp, IFluidTickable
	{
		private static readonly Color COLLECTION_AREA_COLOR = new Color(0.35f, 0.65f, 1f, 0.70f);
		private static readonly Color COLLECTABLE_ROOF_COLOR = new Color(0.20f, 0.95f, 1f, 0.95f);
		private static readonly Dictionary<Map, RainwaterCollectionCache> CACHES_BY_MAP =
			new Dictionary<Map, RainwaterCollectionCache>();

		public int last_assigned_roof_tiles;
		public float last_rain_rate;
		public float last_requested_liters_per_hour;
		public float last_accepted_liters_per_hour;
		public float last_rejected_liters_per_hour;
		private string last_reason;

		public CompProperties_RainwaterCollector Props
		{
			get
			{
				return (CompProperties_RainwaterCollector)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref last_assigned_roof_tiles, "last_assigned_roof_tiles", 0);
			Scribe_Values.Look(ref last_rain_rate, "last_rain_rate", 0f);
			Scribe_Values.Look(ref last_requested_liters_per_hour, "last_requested_liters_per_hour", 0f);
			Scribe_Values.Look(ref last_accepted_liters_per_hour, "last_accepted_liters_per_hour", 0f);
			Scribe_Values.Look(ref last_rejected_liters_per_hour, "last_rejected_liters_per_hour", 0f);
			Scribe_Values.Look(ref last_reason, "last_reason", string.Empty);
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			drawCollectionOverlay();
		}

		public override string CompInspectStringExtra()
		{
			return "RealRim_RainwaterCollectorStatus".Translate(
				last_assigned_roof_tiles,
				last_rain_rate.ToStringPercent(),
				last_requested_liters_per_hour.ToString("N0"),
				last_accepted_liters_per_hour.ToString("N0"),
				last_rejected_liters_per_hour.ToString("N0"),
				last_reason ?? string.Empty);
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_assigned_roof_tiles = 0;
			last_rain_rate = parent?.MapHeld?.weatherManager == null
				? 0f
				: Mathf.Clamp01(parent.MapHeld.weatherManager.RainRate);
			last_requested_liters_per_hour = 0f;
			last_accepted_liters_per_hour = 0f;
			last_rejected_liters_per_hour = 0f;
			last_reason = string.Empty;

			if (parent?.MapHeld == null)
			{
				last_reason = "RealRim_ReasonNotSpawned".Translate();
				return;
			}

			Map map = parent.MapHeld;
			if (!isSupportedWall(parent.Position.GetEdifice(map)))
			{
				last_reason = "RealRim_ReasonNoRainwaterWall".Translate();
				return;
			}

			if (!hasConstructedRoofAnchor(map))
			{
				last_reason = "RealRim_ReasonNoRainwaterRoofAnchor".Translate();
				return;
			}

			last_assigned_roof_tiles = getAssignedRoofTileCount(map);
			if (last_assigned_roof_tiles <= 0)
			{
				last_reason = "RealRim_ReasonNoRainwaterRoofArea".Translate();
				return;
			}

			if (last_rain_rate <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoRain".Translate();
				return;
			}

			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (network == null)
			{
				last_reason = "RealRim_ReasonNoFreshWaterNetwork".Translate();
				return;
			}

			float requested_liters = Props.nominal_liters_per_roof_tile_per_hour
				* last_assigned_roof_tiles
				* last_rain_rate
				* elapsed_seconds / 3600f;
			float accepted_liters = network.addFreshWater(requested_liters, new WaterContamination());
			float rejected_liters = Mathf.Max(0f, requested_liters - accepted_liters);
			last_requested_liters_per_hour = elapsed_seconds <= 0f
				? 0f
				: requested_liters * 3600f / elapsed_seconds;
			last_accepted_liters_per_hour = elapsed_seconds <= 0f
				? 0f
				: accepted_liters * 3600f / elapsed_seconds;
			last_rejected_liters_per_hour = elapsed_seconds <= 0f
				? 0f
				: rejected_liters * 3600f / elapsed_seconds;

			if (accepted_liters <= 0.0001f)
			{
				last_reason = "RealRim_ReasonNoFreshWaterCapacity".Translate();
			}
			else if (rejected_liters > 0.0001f)
			{
				last_reason = "RealRim_ReasonFreshWaterPartlyFull".Translate();
			}
			else
			{
				last_reason = "RealRim_StatusRunning".Translate();
			}
		}

		public bool hasConstructedRoofAnchor(Map map)
		{
			if (map == null || parent == null)
			{
				return false;
			}

			if (parent.Position.InBounds(map) && isCollectableRoof(map.roofGrid.RoofAt(parent.Position)))
			{
				return true;
			}
			return false;
		}

		public CellRect getCollectionRect()
		{
			return getCollectionRect(parent.Position, Props.collection_radius);
		}

		public static CellRect getCollectionRect(IntVec3 center, int radius)
		{
			return CellRect.CenteredOn(center, Mathf.Max(0, radius));
		}

		private int getAssignedRoofTileCount(Map map)
		{
			RainwaterCollectionCache cache;
			if (!CACHES_BY_MAP.TryGetValue(map, out cache))
			{
				cache = new RainwaterCollectionCache();
				CACHES_BY_MAP[map] = cache;
			}

			if (cache.tick != Find.TickManager.TicksGame)
			{
				cache.rebuild(map);
			}

			int count;
			return cache.assigned_tiles_by_collector.TryGetValue(this, out count) ? count : 0;
		}


		public static bool isSupportedWall(Thing thing)
		{
			return thing != null && isSupportedWallDef(thing.def);
		}

		public static bool isSupportedWallDef(ThingDef thing_def)
		{
			return thing_def != null
				&& thing_def.passability == Traversability.Impassable
				&& thing_def.holdsRoof;
		}

		public void drawCollectionOverlay()
		{
			Map map = parent?.MapHeld;
			if (map == null)
			{
				return;
			}

			List<IntVec3> collection_cells = new List<IntVec3>();
			List<IntVec3> collectable_roof_cells = new List<IntVec3>();
			foreach (IntVec3 cell in getCollectionRect().Cells)
			{
				if (!cell.InBounds(map))
				{
					continue;
				}

				collection_cells.Add(cell);
				if (isCollectableRoof(map.roofGrid.RoofAt(cell)))
				{
					collectable_roof_cells.Add(cell);
				}
			}

			if (collection_cells.Count > 0)
			{
				GenDraw.DrawFieldEdges(collection_cells, COLLECTION_AREA_COLOR);
			}
			if (collectable_roof_cells.Count > 0)
			{
				GenDraw.DrawFieldEdges(collectable_roof_cells, COLLECTABLE_ROOF_COLOR);
			}
		}

		public static bool isCollectableRoof(RoofDef roof)
		{
			if (roof == null || roof.isThickRoof)
			{
				return false;
			}

			string def_name = roof.defName ?? string.Empty;
			return def_name.IndexOf("Rock", System.StringComparison.OrdinalIgnoreCase) < 0
				&& def_name.IndexOf("Cave", System.StringComparison.OrdinalIgnoreCase) < 0
				&& def_name.IndexOf("Mountain", System.StringComparison.OrdinalIgnoreCase) < 0;
		}

		private sealed class RainwaterCollectionCache
		{
			public int tick = -1;
			public readonly Dictionary<CompRainwaterCollector, int> assigned_tiles_by_collector =
				new Dictionary<CompRainwaterCollector, int>();

			public void rebuild(Map map)
			{
				tick = Find.TickManager.TicksGame;
				assigned_tiles_by_collector.Clear();
				List<CompRainwaterCollector> collectors = getCollectors(map);
				for (int collector_index = 0; collector_index < collectors.Count; collector_index++)
				{
					CompRainwaterCollector collector = collectors[collector_index];
					foreach (IntVec3 cell in collector.getCollectionRect().Cells)
					{
						if (!cell.InBounds(map)
							|| !isCollectableRoof(map.roofGrid.RoofAt(cell)))
						{
							continue;
						}

						CompRainwaterCollector best_collector = getClosestCollector(cell, collectors);
						if (best_collector == collector)
						{
							int count;
							assigned_tiles_by_collector.TryGetValue(collector, out count);
							assigned_tiles_by_collector[collector] = count + 1;
						}
					}
				}
			}

			private static List<CompRainwaterCollector> getCollectors(Map map)
			{
				return map.listerThings.AllThings
					.OfType<ThingWithComps>()
					.Select(thing => thing.TryGetComp<CompRainwaterCollector>())
					.Where(collector => collector?.parent != null
						&& collector.parent.Spawned
						&& collector.hasConstructedRoofAnchor(map))
					.ToList();
			}

			private static CompRainwaterCollector getClosestCollector(
				IntVec3 cell,
				List<CompRainwaterCollector> collectors)
			{
				CompRainwaterCollector best_collector = null;
				int best_distance = int.MaxValue;
				int best_thing_id = int.MaxValue;
				for (int index = 0; index < collectors.Count; index++)
				{
					CompRainwaterCollector collector = collectors[index];
					if (!collector.getCollectionRect().Contains(cell))
					{
						continue;
					}

					int dx = collector.parent.Position.x - cell.x;
					int dz = collector.parent.Position.z - cell.z;
					int distance = dx * dx + dz * dz;
					int thing_id = collector.parent.thingIDNumber;
					if (best_collector == null
						|| distance < best_distance
						|| (distance == best_distance && thing_id < best_thing_id))
					{
						best_collector = collector;
						best_distance = distance;
						best_thing_id = thing_id;
					}
				}
				return best_collector;
			}
		}
	}
}
