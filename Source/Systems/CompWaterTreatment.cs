using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_WaterTreatment : CompProperties
	{
		public float pathogen_removal_fraction = 0.99f;

		public CompProperties_WaterTreatment()
		{
			compClass = typeof(CompWaterTreatment);
		}
	}

	public sealed class CompWaterTreatment : ThingComp
	{
		public CompProperties_WaterTreatment Props
		{
			get
			{
				return (CompProperties_WaterTreatment)props;
			}
		}

		public bool isOperational()
		{
			return FluidUtility.isPoweredOn(parent);
		}

		public override string CompInspectStringExtra()
		{
			return "RealRim_WaterTreatmentStatus".Translate(
				isOperational()
					? "RealRim_StatusRunning".Translate()
					: "RealRim_StatusStandby".Translate(),
				Props.pathogen_removal_fraction.ToStringPercent());
		}
	}
}
