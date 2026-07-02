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
		public static readonly Texture2D growing_zone = ContentFinder<Texture2D>.Get(
			"UI/Designators/ZoneCreate_Growing");
	}
}
