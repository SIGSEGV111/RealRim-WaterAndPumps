using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_RoomThermostatLinkTarget : CompProperties
	{
		public CompProperties_RoomThermostatLinkTarget()
		{
			compClass = typeof(CompRoomThermostatLinkTarget);
		}
	}

	public sealed class CompRoomThermostatLinkTarget : ThingComp
	{
		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			drawLinkedValveOverlays();
		}

		public override string CompInspectStringExtra()
		{
			Map map = parent?.MapHeld;
			string temperature = getTemperatureText();
			int linked_valve_count = getLinkedValves(map).Count;
			return "RealRim_RoomThermostatLinkStatus".Translate(
				linked_valve_count,
				temperature);
		}

		public static bool isValidThermostat(Thing thing)
		{
			ThingWithComps thing_with_comps = thing as ThingWithComps;
			return thing_with_comps != null
				&& thing_with_comps.Spawned
				&& thing_with_comps.TryGetComp<CompRoomThermostatLinkTarget>() != null;
		}

		public List<CompSmartMixingValve> getLinkedValves(Map map)
		{
			List<CompSmartMixingValve> result = new List<CompSmartMixingValve>();
			if (map == null || parent == null)
			{
				return result;
			}

			List<Thing> all_things = map.listerThings?.AllThings;
			if (all_things == null)
			{
				return result;
			}

			for (int index = 0; index < all_things.Count; index++)
			{
				ThingWithComps thing = all_things[index] as ThingWithComps;
				CompSmartMixingValve valve = thing?.TryGetComp<CompSmartMixingValve>();
				if (valve != null && valve.isLinkedToThermostat(parent))
				{
					result.Add(valve);
				}
			}
			return result;
		}

		private void drawLinkedValveOverlays()
		{
			Map map = parent?.MapHeld;
			if (map == null)
			{
				return;
			}

			List<CompSmartMixingValve> valves = getLinkedValves(map);
			for (int index = 0; index < valves.Count; index++)
			{
				ThermostatLinkOverlay.drawLink(parent, valves[index].parent);
			}
		}

		private string getTemperatureText()
		{
			Map map = parent?.MapHeld;
			if (map == null || parent.Position.UsesOutdoorTemperature(map))
			{
				return "-";
			}
			return GenTemperature.GetTemperatureForCell(parent.Position, map).ToStringTemperature("F1");
		}
	}

	internal static class ThermostatLinkOverlay
	{
		private static readonly Color LINK_COLOR = new Color(1f, 0.85f, 0.20f, 0.38f);

		public static void drawLink(Thing first, Thing second)
		{
			Map map = first?.MapHeld;
			if (map == null || second?.MapHeld != map)
			{
				return;
			}

			List<IntVec3> cells = getLineCells(first.Position, second.Position, map);
			if (cells.Count > 0)
			{
				GenDraw.DrawFieldEdges(cells, LINK_COLOR);
			}
		}

		private static List<IntVec3> getLineCells(IntVec3 start, IntVec3 end, Map map)
		{
			List<IntVec3> result = new List<IntVec3>();
			int x0 = start.x;
			int z0 = start.z;
			int x1 = end.x;
			int z1 = end.z;
			int dx = Mathf.Abs(x1 - x0);
			int dz = Mathf.Abs(z1 - z0);
			int sx = x0 < x1 ? 1 : -1;
			int sz = z0 < z1 ? 1 : -1;
			int err = dx - dz;

			while (true)
			{
				IntVec3 cell = new IntVec3(x0, 0, z0);
				if (cell.InBounds(map))
				{
					result.Add(cell);
				}
				if (x0 == x1 && z0 == z1)
				{
					break;
				}

				int e2 = 2 * err;
				if (e2 > -dz)
				{
					err -= dz;
					x0 += sx;
				}
				if (e2 < dx)
				{
					err += dx;
					z0 += sz;
				}
			}
			return result;
		}
	}
}
