using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	[StaticConstructorOnStartup]
	public static class RealRimTextures
	{
		public static readonly Texture2D heating_overview = ContentFinder<Texture2D>.Get(
			"RealRim/UI/HeatingOverview");
		public static readonly Texture2D hot_water_overview = ContentFinder<Texture2D>.Get(
			"RealRim/UI/HotWaterOverview");
		public static readonly Texture2D lower_target = ContentFinder<Texture2D>.Get(
			"RealRim/UI/LowerTarget");
		public static readonly Texture2D raise_target = ContentFinder<Texture2D>.Get(
			"RealRim/UI/RaiseTarget");
		public static readonly Texture2D configure_pump = ContentFinder<Texture2D>.Get(
			"RealRim/UI/ConfigurePump");
		public static readonly Texture2D configure_heat_source = ContentFinder<Texture2D>.Get(
			"RealRim/UI/ConfigureHeatSource");
		public static readonly Texture2D fluid_layer = ContentFinder<Texture2D>.Get(
			"RealRim/UI/FluidLayer");
		public static readonly Texture2D fluid_layer_fresh_water = ContentFinder<Texture2D>.Get(
			"RealRim/UI/FluidLayerFreshWater");
		public static readonly Texture2D fluid_layer_hot_water = ContentFinder<Texture2D>.Get(
			"RealRim/UI/FluidLayerHotWater");
		public static readonly Texture2D fluid_layer_heating = ContentFinder<Texture2D>.Get(
			"RealRim/UI/FluidLayerHeating");
		public static readonly Texture2D fluid_layer_waste_water = ContentFinder<Texture2D>.Get(
			"RealRim/UI/FluidLayerWasteWater");
		public static readonly Texture2D fluid_layer_coolant = ContentFinder<Texture2D>.Get(
			"RealRim/UI/FluidLayerCoolant");
		public static readonly Texture2D growing_zone = ContentFinder<Texture2D>.Get(
			"UI/Designators/ZoneCreate_Growing");

		public static Texture2D getFluidLayerIcon(FluidNetworkType network_type)
		{
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					return fluid_layer_fresh_water;
				case FluidNetworkType.HotWater:
					return fluid_layer_hot_water;
				case FluidNetworkType.Heating:
					return fluid_layer_heating;
				case FluidNetworkType.WasteWater:
					return fluid_layer_waste_water;
				case FluidNetworkType.Coolant:
					return fluid_layer_coolant;
				default:
					return fluid_layer;
			}
		}
	}
}
