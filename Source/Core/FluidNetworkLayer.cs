using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public enum FluidNetworkLayer
	{
		None = 0,
		Layer1 = 1,
		Layer2 = 2,
		Layer3 = 3,
		Layer4 = 4,
		Layer5 = 5,
	}

	public static class FluidNetworkLayerUtility
	{
		public const string CHANGE_LAYER_DESIGNATION_DEF_NAME = "RealRim_ChangeFluidLayer";
		public const string CHANGE_LAYER_JOB_DEF_NAME = "RealRim_ChangeFluidLayer";
		public const int CHANGE_LAYER_WORK_TICKS = 180;

		public static readonly FluidNetworkLayer[] LAYERS =
		{
			FluidNetworkLayer.Layer1,
			FluidNetworkLayer.Layer2,
			FluidNetworkLayer.Layer3,
			FluidNetworkLayer.Layer4,
			FluidNetworkLayer.Layer5,
		};

		public static FluidNetworkLayer clampLayer(FluidNetworkLayer layer)
		{
			int value = Mathf.Clamp((int)layer, (int)FluidNetworkLayer.Layer1, (int)FluidNetworkLayer.Layer5);
			return (FluidNetworkLayer)value;
		}

		public static string getLayerLabel(FluidNetworkLayer layer)
		{
			return "RealRim_FluidLayerNumber".Translate((int)clampLayer(layer)).ToString();
		}

		public static DesignationDef getChangeDesignationDef()
		{
			return DefDatabase<DesignationDef>.GetNamedSilentFail(CHANGE_LAYER_DESIGNATION_DEF_NAME);
		}

		public static JobDef getChangeJobDef()
		{
			return DefDatabase<JobDef>.GetNamedSilentFail(CHANGE_LAYER_JOB_DEF_NAME);
		}
	}

	public static class FluidNetworkLayerSettings
	{
		private static FluidNetworkLayer fresh_water_layer = FluidNetworkLayer.Layer1;
		private static FluidNetworkLayer hot_water_layer = FluidNetworkLayer.Layer1;
		private static FluidNetworkLayer heating_layer = FluidNetworkLayer.Layer1;
		private static FluidNetworkLayer waste_water_layer = FluidNetworkLayer.Layer1;
		private static FluidNetworkLayer coolant_layer = FluidNetworkLayer.Layer1;

		public static FluidNetworkLayer getSelectedLayer(FluidNetworkType network_type)
		{
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					return fresh_water_layer;
				case FluidNetworkType.HotWater:
					return hot_water_layer;
				case FluidNetworkType.Heating:
					return heating_layer;
				case FluidNetworkType.WasteWater:
					return waste_water_layer;
				case FluidNetworkType.Coolant:
					return coolant_layer;
				default:
					return FluidNetworkLayer.Layer1;
			}
		}

		public static void setSelectedLayer(FluidNetworkType network_type, FluidNetworkLayer layer)
		{
			layer = FluidNetworkLayerUtility.clampLayer(layer);
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					fresh_water_layer = layer;
					break;
				case FluidNetworkType.HotWater:
					hot_water_layer = layer;
					break;
				case FluidNetworkType.Heating:
					heating_layer = layer;
					break;
				case FluidNetworkType.WasteWater:
					waste_water_layer = layer;
					break;
				case FluidNetworkType.Coolant:
					coolant_layer = layer;
					break;
			}
			markCurrentMapOverlayDirty(network_type);
		}

		private static void markCurrentMapOverlayDirty(FluidNetworkType network_type)
		{
			Map map = Find.CurrentMap;
			MapComponent_FluidNetworks manager = map?.GetComponent<MapComponent_FluidNetworks>();
			List<CompFluidNode> nodes = manager?.getAllActiveNodes(network_type);
			if (nodes == null)
			{
				return;
			}

			for (int index = 0; index < nodes.Count; index++)
			{
				FluidNetworkVisuals.markOverlayDirty(nodes[index]?.parent);
			}
		}
	}

	public sealed class FluidLayerConstructionPlan : IExposable
	{
		public int construction_thing_id;
		public string build_def_name;
		public IntVec3 position;
		public Rot4 rotation;
		public FluidNetworkLayer fresh_water_layer = FluidNetworkLayer.Layer1;
		public FluidNetworkLayer hot_water_layer = FluidNetworkLayer.Layer1;
		public FluidNetworkLayer heating_layer = FluidNetworkLayer.Layer1;
		public FluidNetworkLayer waste_water_layer = FluidNetworkLayer.Layer1;
		public FluidNetworkLayer coolant_layer = FluidNetworkLayer.Layer1;

		public void ExposeData()
		{
			Scribe_Values.Look(ref construction_thing_id, "construction_thing_id", 0);
			Scribe_Values.Look(ref build_def_name, "build_def_name", null);
			Scribe_Values.Look(ref position, "position", IntVec3.Invalid);
			Scribe_Values.Look(ref rotation, "rotation", Rot4.North);
			Scribe_Values.Look(ref fresh_water_layer, "fresh_water_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref hot_water_layer, "hot_water_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref heating_layer, "heating_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref waste_water_layer, "waste_water_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref coolant_layer, "coolant_layer", FluidNetworkLayer.Layer1);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				clampLayers();
			}
		}

		public static FluidLayerConstructionPlan create(Thing construction, ThingDef build_def)
		{
			FluidLayerConstructionPlan plan = new FluidLayerConstructionPlan
			{
				construction_thing_id = construction.thingIDNumber,
				build_def_name = build_def.defName,
				position = construction.Position,
				rotation = construction.Rotation,
			};
			plan.captureSelectedLayers(build_def);
			return plan;
		}

		public bool matches(CompFluidNode node)
		{
			return node?.parent != null
				&& node.parent.def != null
				&& node.parent.def.defName == build_def_name
				&& node.parent.Position == position
				&& node.parent.Rotation == rotation;
		}

		public bool matchesConstruction(Thing construction)
		{
			return construction != null && construction.thingIDNumber == construction_thing_id;
		}

		public bool matchesConstructionBuild(Thing construction, ThingDef build_def)
		{
			return construction != null
				&& build_def != null
				&& build_def.defName == build_def_name
				&& construction.Position == position
				&& construction.Rotation == rotation;
		}

		public void updateConstructionThing(Thing construction)
		{
			if (construction != null)
			{
				construction_thing_id = construction.thingIDNumber;
			}
		}

		public FluidNetworkLayer getLayer(FluidNetworkType network_type)
		{
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					return fresh_water_layer;
				case FluidNetworkType.HotWater:
					return hot_water_layer;
				case FluidNetworkType.Heating:
					return heating_layer;
				case FluidNetworkType.WasteWater:
					return waste_water_layer;
				case FluidNetworkType.Coolant:
					return coolant_layer;
				default:
					return FluidNetworkLayer.Layer1;
			}
		}

		private void captureSelectedLayers(ThingDef build_def)
		{
			CompProperties_FluidNode properties = FluidNetworkVisuals.getNodeProperties(build_def);
			if (properties?.networks == null)
			{
				return;
			}

			for (int index = 0; index < properties.networks.Count; index++)
			{
				setLayer(properties.networks[index], FluidNetworkLayerSettings.getSelectedLayer(properties.networks[index]));
			}
			clampLayers();
		}

		private void setLayer(FluidNetworkType network_type, FluidNetworkLayer layer)
		{
			layer = FluidNetworkLayerUtility.clampLayer(layer);
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					fresh_water_layer = layer;
					break;
				case FluidNetworkType.HotWater:
					hot_water_layer = layer;
					break;
				case FluidNetworkType.Heating:
					heating_layer = layer;
					break;
				case FluidNetworkType.WasteWater:
					waste_water_layer = layer;
					break;
				case FluidNetworkType.Coolant:
					coolant_layer = layer;
					break;
			}
		}

		private void clampLayers()
		{
			fresh_water_layer = FluidNetworkLayerUtility.clampLayer(fresh_water_layer);
			hot_water_layer = FluidNetworkLayerUtility.clampLayer(hot_water_layer);
			heating_layer = FluidNetworkLayerUtility.clampLayer(heating_layer);
			waste_water_layer = FluidNetworkLayerUtility.clampLayer(waste_water_layer);
			coolant_layer = FluidNetworkLayerUtility.clampLayer(coolant_layer);
		}
	}
}
