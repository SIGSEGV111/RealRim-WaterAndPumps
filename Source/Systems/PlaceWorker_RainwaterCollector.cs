using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class PlaceWorker_RainwaterCollector : PlaceWorker
	{
		private static readonly Color COLLECTION_AREA_COLOR = new Color(0.35f, 0.65f, 1f, 0.70f);
		private static readonly Color COLLECTABLE_ROOF_COLOR = new Color(0.20f, 0.95f, 1f, 0.95f);


		public override bool ForceAllowPlaceOver(BuildableDef other_def)
		{
			return CompRainwaterCollector.isSupportedWallDef(other_def as ThingDef);
		}

		public override AcceptanceReport AllowsPlacing(
			BuildableDef checking_def,
			IntVec3 location,
			Rot4 rotation,
			Map map,
			Thing thing_to_ignore = null,
			Thing thing = null)
		{
			if (map == null || !location.InBounds(map))
			{
				return false;
			}

			Thing edifice = location.GetEdifice(map);
			if (!CompRainwaterCollector.isSupportedWall(edifice))
			{
				return "RealRim_RainwaterCollectorRequiresWall".Translate();
			}

			if (CompRainwaterCollector.isCollectableRoof(map.roofGrid.RoofAt(location)))
			{
				return true;
			}

			return "RealRim_RainwaterCollectorRequiresRoof".Translate();
		}

		public override void DrawGhost(
			ThingDef def,
			IntVec3 center,
			Rot4 rotation,
			Color ghost_color,
			Thing thing = null)
		{
			Map map = Find.CurrentMap;
			CompProperties_RainwaterCollector properties = def.GetCompProperties<CompProperties_RainwaterCollector>();
			int collection_radius = properties == null ? 3 : properties.collection_radius;
			CellRect collection_rect = CompRainwaterCollector.getCollectionRect(center, collection_radius);

			List<IntVec3> collection_cells = new List<IntVec3>();
			List<IntVec3> collectable_roof_cells = new List<IntVec3>();
			foreach (IntVec3 cell in collection_rect.Cells)
			{
				if (map != null && !cell.InBounds(map))
				{
					continue;
				}

				collection_cells.Add(cell);
				if (map != null
					&& CompRainwaterCollector.isCollectableRoof(map.roofGrid.RoofAt(cell)))
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
	}
}
