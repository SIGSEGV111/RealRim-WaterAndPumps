using RimWorld;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_FloorHeating : CompProperties
	{
		public float heat_exchanger_surface_m2 = 1.0f;
		public float heat_transfer_w_per_m2_k = 5.0f;
		public float outdoor_heat_transfer_w_per_m2_k = 10.0f;
		public float rated_output_kw = 0.075f;
		public float target_temperature_c = 21f;
		public float comfort_bonus = 0.10f;

		public CompProperties_FloorHeating()
		{
			compClass = typeof(CompFloorHeating);
		}
	}

	public sealed class CompFloorHeating : ThingComp, IFluidTickable
	{
		public float last_transfer_kw;
		public float last_room_temperature_c;
		public float last_medium_temperature_c;
		public float last_group_surface_m2;
		public float last_snow_depth_removed;
		public bool last_outdoor_mode;
		public string last_reason = string.Empty;

		public CompProperties_FloorHeating Props
		{
			get
			{
				return (CompProperties_FloorHeating)props;
			}
		}

		public override string CompInspectStringExtra()
		{
			string status = "RealRim_FloorHeatingStatus".Translate(
				(last_outdoor_mode
					? "RealRim_FloorHeatingModeOutdoor".Translate()
					: "RealRim_FloorHeatingModeIndoor".Translate()).ToString(),
				last_room_temperature_c.ToStringTemperature("F1"),
				last_medium_temperature_c.ToStringTemperature("F1"),
				last_group_surface_m2.ToString("N1"),
				last_transfer_kw.ToString("N3"),
				last_snow_depth_removed.ToString("N3"),
				Props.comfort_bonus.ToString("+0.00;-0.00;0.00"),
				last_reason);
			return status.TrimEnd('\r', '\n', ' ', '\t');
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			FloorHeatingUtility.tickFloorHeating(this, elapsed_seconds);
		}
	}
}
