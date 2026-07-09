using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	internal static class FluidNetworkVisuals
	{
		private const string PIPE_OVERLAY_TEXTURE = "DBH/Things/Building/PipeOverlay_Atlas";

		private static readonly Dictionary<FluidNetworkType, Graphic> SUB_GRAPHICS =
			new Dictionary<FluidNetworkType, Graphic>();

		private static readonly MethodInfo LINKED_DRAW_MAT_FROM_METHOD =
			typeof(Graphic_Linked).GetMethod(
				"LinkedDrawMatFrom",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				new[] { typeof(Thing), typeof(IntVec3) },
				null);

		private static readonly Dictionary<string, Vector3> VISIBLE_PIPE_OFFSETS =
			new Dictionary<string, Vector3>
			{
				{ "RealRim_FreshWaterPipe", new Vector3(-0.16f, 0f, -0.16f) },
				{ "RealRim_HotWaterPipe", new Vector3(-0.08f, 0f, -0.08f) },
				{ "RealRim_HeatingPipe", Vector3.zero },
				{ "sewagePipeStuff", new Vector3(0.08f, 0f, 0.08f) },
				{ "airPipe", new Vector3(0.16f, 0f, 0.16f) },
			};

		public static bool tryPrintVisiblePipe(Thing thing, SectionLayer layer, Graphic_Linked linked_graphic)
		{
			if (thing?.def == null || layer == null || linked_graphic == null)
			{
				return false;
			}

			Vector3 pipe_offset;
			if (!VISIBLE_PIPE_OFFSETS.TryGetValue(thing.def.defName, out pipe_offset))
			{
				return false;
			}

			Vector2 draw_size = Vector2.one;
			if (thing.def.graphicData != null)
			{
				draw_size = thing.def.graphicData.drawSize;
			}

			Material linked_material = getLinkedDrawMaterial(
				linked_graphic,
				thing,
				thing.Position);
			if (linked_material == null)
			{
				return false;
			}

			Vector3 position = thing.DrawPos + pipe_offset;
			Printer_Plane.PrintPlane(
				layer,
				position,
				draw_size,
				linked_material,
				0f,
				false,
				null,
				null,
				0.01f,
				0f);
			return true;
		}

		private static Material getLinkedDrawMaterial(
			Graphic_Linked linked_graphic,
			Thing thing,
			IntVec3 cell)
		{
			if (LINKED_DRAW_MAT_FROM_METHOD == null)
			{
				return null;
			}

			return LINKED_DRAW_MAT_FROM_METHOD.Invoke(
				linked_graphic,
				new object[] { thing, cell }) as Material;
		}

		public static Color getColor(FluidNetworkType network_type)
		{
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					return new Color(51f / 255f, 166f / 255f, 1f, 190f / 255f);
				case FluidNetworkType.HotWater:
					return new Color(217f / 255f, 51f / 255f, 38f / 255f, 190f / 255f);
				case FluidNetworkType.Heating:
					return new Color(1f, 148f / 255f, 51f / 255f, 190f / 255f);
				case FluidNetworkType.WasteWater:
					return new Color(115f / 255f, 87f / 255f, 46f / 255f, 190f / 255f);
				case FluidNetworkType.Coolant:
					return new Color(64f / 255f, 230f / 255f, 242f / 255f, 190f / 255f);
				default:
					return Color.white;
			}
		}

		public static Vector3 getOverlayOffset(FluidNetworkType network_type)
		{
			const float OFFSET_STEP = 0.12f;
			float offset;
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					offset = -2f * OFFSET_STEP;
					break;
				case FluidNetworkType.HotWater:
					offset = -OFFSET_STEP;
					break;
				case FluidNetworkType.Heating:
					offset = 0f;
					break;
				case FluidNetworkType.WasteWater:
					offset = OFFSET_STEP;
					break;
				case FluidNetworkType.Coolant:
					offset = 2f * OFFSET_STEP;
					break;
				default:
					offset = 0f;
					break;
			}

			return new Vector3(offset, 0f, offset);
		}

		public static Graphic getSubGraphic(FluidNetworkType network_type)
		{
			Graphic graphic;
			if (!SUB_GRAPHICS.TryGetValue(network_type, out graphic))
			{
				graphic = GraphicDatabase.Get<Graphic_Single>(
					PIPE_OVERLAY_TEXTURE,
					ShaderDatabase.MetaOverlay,
					Vector2.one,
					getColor(network_type));
				SUB_GRAPHICS[network_type] = graphic;
			}

			return graphic;
		}

		public static HashSet<IntVec3> collectActiveCells(List<CompFluidNode> nodes)
		{
			HashSet<IntVec3> result = new HashSet<IntVec3>();
			if (nodes == null)
			{
				return result;
			}

			for (int node_index = 0; node_index < nodes.Count; node_index++)
			{
				CompFluidNode node = nodes[node_index];
				if (node?.parent == null || !node.parent.Spawned)
				{
					continue;
				}

				foreach (IntVec3 cell in node.parent.OccupiedRect())
				{
					result.Add(cell);
				}
			}

			return result;
		}

		public static CompProperties_FluidNode getNodeProperties(ThingDef def)
		{
			if (def?.comps == null)
			{
				return null;
			}

			for (int index = 0; index < def.comps.Count; index++)
			{
				CompProperties_FluidNode node = def.comps[index] as CompProperties_FluidNode;
				if (node != null)
				{
					return node;
				}
			}

			return null;
		}

		public static bool shouldDrawOverlay(Map map, FluidNetworkType network_type)
		{
			return getDisplayedOverlayLayer(map, network_type) != FluidNetworkLayer.None;
		}

		public static FluidNetworkLayer getDisplayedOverlayLayer(Map map, FluidNetworkType network_type)
		{
			if (map == null || Find.CurrentMap != map)
			{
				return FluidNetworkLayer.None;
			}

			Designator_Build designator = Find.DesignatorManager?.SelectedDesignator as Designator_Build;
			ThingDef placing_def = designator?.PlacingDef as ThingDef;
			CompProperties_FluidNode placing_node = getNodeProperties(placing_def);
			if (placing_node?.networks != null && placing_node.networks.Contains(network_type))
			{
				return FluidNetworkLayerSettings.getSelectedLayer(network_type);
			}

			List<object> selected_objects = Find.Selector?.SelectedObjectsListForReading;
			if (selected_objects == null)
			{
				return FluidNetworkLayer.None;
			}

			for (int index = 0; index < selected_objects.Count; index++)
			{
				ThingWithComps thing = selected_objects[index] as ThingWithComps;
				if (thing == null || thing.Map != map)
				{
					continue;
				}

				CompFluidNode node = thing.TryGetComp<CompFluidNode>();
				if (node != null && node.supportsNetwork(network_type))
				{
					return node.isLayerConnector(network_type)
						? FluidNetworkLayerSettings.getSelectedLayer(network_type)
						: node.getLayer(network_type);
				}
			}

			return FluidNetworkLayer.None;
		}

		public static void markOverlayDirty(Thing thing)
		{
			Map map = thing?.MapHeld;
			if (map == null)
			{
				return;
			}

			foreach (IntVec3 cell in thing.OccupiedRect())
			{
				map.mapDrawer.MapMeshDirty(
					cell,
					MapMeshFlagDefOf.Buildings,
					true,
					true);
			}
		}
	}

	internal sealed class Graphic_LinkedFluidOverlay : Graphic_Linked
	{
		private readonly Map map;
		private readonly HashSet<IntVec3> linked_cells;
		private readonly Vector3 overlay_offset;

		public Graphic_LinkedFluidOverlay(
			Graphic sub_graphic,
			Map map,
			HashSet<IntVec3> linked_cells,
			Vector3 overlay_offset) : base(sub_graphic)
		{
			this.map = map;
			this.linked_cells = linked_cells;
			this.overlay_offset = overlay_offset;
		}

		public override bool ShouldLinkWith(IntVec3 cell, Thing parent)
		{
			return cell.InBounds(map) && linked_cells.Contains(cell);
		}

		public override void Print(SectionLayer layer, Thing thing, float extra_rotation)
		{
			foreach (IntVec3 cell in thing.OccupiedRect())
			{
				Vector3 position = cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays)
					+ overlay_offset;
				Printer_Plane.PrintPlane(
					layer,
					position,
					Vector2.one,
					LinkedDrawMatFrom(thing, cell),
					0f,
					false,
					null,
					null,
					0.01f,
					0f);
			}
		}
	}

	public abstract class SectionLayer_FluidOverlay : SectionLayer
	{
		private readonly FluidNetworkType network_type;
		private FluidNetworkLayer cached_displayed_layer = FluidNetworkLayer.None;

		protected SectionLayer_FluidOverlay(
			Section section,
			FluidNetworkType network_type) : base(section)
		{
			this.network_type = network_type;
			relevantChangeTypes = MapMeshFlagDefOf.Buildings;
		}

		public override void DrawLayer()
		{
			FluidNetworkLayer displayed_layer = FluidNetworkVisuals.getDisplayedOverlayLayer(Map, network_type);
			if (cached_displayed_layer != displayed_layer)
			{
				cached_displayed_layer = displayed_layer;
				Regenerate();
			}

			if (displayed_layer != FluidNetworkLayer.None)
			{
				base.DrawLayer();
			}
		}

		public override void Regenerate()
		{
			ClearSubMeshes(MeshParts.All);

			MapComponent_FluidNetworks manager = Map.GetComponent<MapComponent_FluidNetworks>();
			FluidNetworkLayer displayed_layer = FluidNetworkVisuals.getDisplayedOverlayLayer(Map, network_type);
			cached_displayed_layer = displayed_layer;
			List<CompFluidNode> nodes = displayed_layer == FluidNetworkLayer.None
				? new List<CompFluidNode>()
				: manager?.getAllActiveNodes(network_type, displayed_layer);
			HashSet<IntVec3> linked_cells = FluidNetworkVisuals.collectActiveCells(nodes);
			if (linked_cells.Count > 0)
			{
				printLinkedPipes(nodes, linked_cells);
			}

			FinalizeMesh(MeshParts.All);
		}

		private void printLinkedPipes(
			List<CompFluidNode> nodes,
			HashSet<IntVec3> linked_cells)
		{
			Graphic_LinkedFluidOverlay overlay = new Graphic_LinkedFluidOverlay(
				FluidNetworkVisuals.getSubGraphic(network_type),
				Map,
				linked_cells,
				FluidNetworkVisuals.getOverlayOffset(network_type));
			for (int index = 0; index < nodes.Count; index++)
			{
				CompFluidNode node = nodes[index];
				Thing parent = node?.parent;
				if (parent == null
					|| !parent.Spawned
					|| !section.CellRect.Contains(parent.Position))
				{
					continue;
				}

				overlay.Print(this, parent, 0f);
			}
		}
	}

	public sealed class SectionLayer_FreshWaterOverlay : SectionLayer_FluidOverlay
	{
		public SectionLayer_FreshWaterOverlay(Section section) :
			base(section, FluidNetworkType.FreshWater)
		{
		}
	}

	public sealed class SectionLayer_HotWaterOverlay : SectionLayer_FluidOverlay
	{
		public SectionLayer_HotWaterOverlay(Section section) :
			base(section, FluidNetworkType.HotWater)
		{
		}
	}

	public sealed class SectionLayer_HeatingOverlay : SectionLayer_FluidOverlay
	{
		public SectionLayer_HeatingOverlay(Section section) :
			base(section, FluidNetworkType.Heating)
		{
		}
	}

	public sealed class SectionLayer_WasteWaterOverlay : SectionLayer_FluidOverlay
	{
		public SectionLayer_WasteWaterOverlay(Section section) :
			base(section, FluidNetworkType.WasteWater)
		{
		}
	}

	public sealed class SectionLayer_CoolantOverlay : SectionLayer_FluidOverlay
	{
		public SectionLayer_CoolantOverlay(Section section) :
			base(section, FluidNetworkType.Coolant)
		{
		}
	}
}
