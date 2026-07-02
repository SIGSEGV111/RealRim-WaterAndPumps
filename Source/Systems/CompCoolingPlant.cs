using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_CoolingPlant : CompProperties
	{
		public float nominal_cooling_kw = 12f;
		public float start_fill_fraction = 0.25f;
		public float stop_fill_fraction = 0.90f;
		public float minimum_ambient_temperature_c = -30f;
		public float maximum_ambient_temperature_c = 50f;

		public CompProperties_CoolingPlant()
		{
			compClass = typeof(CompCoolingPlant);
		}
	}

	public sealed class CompCoolingPlant : ThingComp, IFluidTickable
	{
		public bool cooling;
		public float last_cooling_kw;
		public float last_power_kw;
		public float last_cop;
		public string last_reason = string.Empty;

		public CompProperties_CoolingPlant Props
		{
			get
			{
				return (CompProperties_CoolingPlant)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref cooling, "cooling", false);
		}

		public override string CompInspectStringExtra()
		{
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.Coolant);
			float fill = network == null || network.getColdEnergyCapacityKj() <= 0f
				? 0f
				: network.getStoredColdEnergyKj() / network.getColdEnergyCapacityKj();
			return "RealRim_CoolingPlantStatus".Translate(
				cooling ? "RealRim_StatusRunning".Translate() : "RealRim_StatusStandby".Translate(),
				fill.ToStringPercent(),
				last_cooling_kw.ToString("N1"),
				last_power_kw.ToString("N1"),
				last_cop.ToString("N2"),
				last_reason);
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_cooling_kw = 0f;
			last_power_kw = 0f;
			last_cop = 0f;
			last_reason = string.Empty;
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.Coolant);
			if (network == null || network.getColdEnergyCapacityKj() <= 0f)
			{
				stopCooling("RealRim_ReasonNoCoolantStorage".Translate());
				return;
			}

			float fill = network.getStoredColdEnergyKj() / network.getColdEnergyCapacityKj();
			if (cooling && fill >= Props.stop_fill_fraction)
			{
				cooling = false;
			}
			else if (!cooling && fill <= Props.start_fill_fraction)
			{
				cooling = true;
			}

			float ambient = parent.AmbientTemperature;
			if (!cooling || ambient < Props.minimum_ambient_temperature_c || ambient > Props.maximum_ambient_temperature_c)
			{
				stopCooling(!cooling ? "RealRim_ReasonThresholdReached".Translate() : "RealRim_ReasonAmbientOutOfRange".Translate());
				return;
			}

			if (!FluidUtility.isPoweredOn(parent))
			{
				stopCooling("RealRim_ReasonNoPower".Translate());
				return;
			}

			last_cop = RealPhysics.calculateCoolingCop(0f, ambient);
			last_power_kw = Props.nominal_cooling_kw / last_cop;
			float accepted_kj = network.addColdEnergy(Props.nominal_cooling_kw * elapsed_seconds);
			last_cooling_kw = elapsed_seconds <= 0f ? 0f : accepted_kj / elapsed_seconds;
			FluidUtility.setPowerConsumption(parent, last_power_kw * 1000f, last_cooling_kw > 0f);
			if (last_cooling_kw > 0f)
			{
				float rejected_heat_kw = last_cooling_kw + last_power_kw;
				GenTemperature.PushHeat(
					parent,
					rejected_heat_kw * RealPhysics.RIMWORLD_HEAT_UNITS_PER_KW_SECOND * elapsed_seconds);
			}
		}

		private void stopCooling(string reason)
		{
			last_reason = reason;
			FluidUtility.setPowerConsumption(parent, 0f, false);
		}
	}
}
