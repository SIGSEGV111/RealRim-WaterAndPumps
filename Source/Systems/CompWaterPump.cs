using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_WaterPump : CompProperties
	{
		public float nominal_liters_per_hour = 1200f;
		public float power_watts = 250f;
		public float start_fill_fraction = 0.80f;
		public float stop_fill_fraction = 0.98f;
		public bool requires_power = true;
		public float loss_free_length_m = 10f;
		public float hydraulic_reference_length_m = 50f;

		public CompProperties_WaterPump()
		{
			compClass = typeof(CompWaterPump);
		}
	}

	public sealed class CompWaterPump : ThingComp, IFluidTickable
	{
		private const float MINIMUM_THRESHOLD_GAP = 0.05f;
		private const BindingFlags REFLECTION_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public bool pumping;
		public bool controller_enabled = true;
		public float start_fill_fraction;
		public float stop_fill_fraction;
		public float last_flow_liters_per_hour;
		public int last_route_length_m;
		public float last_flow_multiplier = 1f;
		public float last_drive_multiplier = 1f;
		public int last_source_count;

		public CompProperties_WaterPump Props
		{
			get
			{
				return (CompProperties_WaterPump)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			if (!respawning_after_load)
			{
				start_fill_fraction = Props.start_fill_fraction;
				stop_fill_fraction = Props.stop_fill_fraction;
			}
			setThresholds(start_fill_fraction, stop_fill_fraction);
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref pumping, "pumping", false);
			Scribe_Values.Look(ref controller_enabled, "pump_controller_enabled", true);
			Scribe_Values.Look(ref start_fill_fraction, "pump_start_fill_fraction", Props.start_fill_fraction);
			Scribe_Values.Look(ref stop_fill_fraction, "pump_stop_fill_fraction", Props.stop_fill_fraction);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				setThresholds(start_fill_fraction, stop_fill_fraction);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield return new Command_Toggle
			{
				defaultLabel = "RealRim_WaterPumpAutomation".Translate(),
				defaultDesc = "RealRim_WaterPumpAutomationDesc".Translate(),
				icon = parent.def.uiIcon,
				isActive = () => controller_enabled,
				toggleAction = delegate
				{
					controller_enabled = !controller_enabled;
				},
			};
			yield return new Command_Action
			{
				defaultLabel = "RealRim_WaterPumpConfigure".Translate(),
				defaultDesc = "RealRim_WaterPumpConfigureDesc".Translate(),
				icon = parent.def.uiIcon,
				action = delegate
				{
					Find.WindowStack.Add(new Dialog_WaterPumpThresholds(this));
				},
			};
		}

		public override string CompInspectStringExtra()
		{
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			float stored = network == null ? 0f : network.getStoredFreshWater();
			float capacity = network == null ? 0f : network.getFreshWaterCapacity();
			string status = "RealRim_WaterPumpStatus".Translate(
				pumping ? "RealRim_StatusRunning".Translate() : "RealRim_StatusStandby".Translate(),
				last_flow_liters_per_hour.ToString("N0"),
				stored.ToString("N0"),
				capacity.ToString("N0"),
				last_route_length_m,
				last_flow_multiplier.ToStringPercent())
				+ "\n"
				+ "RealRim_WaterPumpSources".Translate(last_source_count)
				+ "\n"
				+ "RealRim_WaterPumpThresholdStatus".Translate(
					start_fill_fraction.ToStringPercent(),
					stop_fill_fraction.ToStringPercent())
				+ "\n"
				+ "RealRim_WaterPumpDriveStatus".Translate(last_drive_multiplier.ToStringPercent());
			string drive_reason = getWindDriveReason();
			if (!drive_reason.NullOrEmpty())
			{
				status += "\n" + drive_reason;
			}
			return status.TrimEnd('\r', '\n', ' ', '\t');
		}

		public void setThresholds(float requested_start, float requested_stop)
		{
			float new_start = Mathf.Clamp01((float)Math.Round(requested_start * 20f) / 20f);
			float new_stop = Mathf.Clamp01((float)Math.Round(requested_stop * 20f) / 20f);
			if (new_start > new_stop - MINIMUM_THRESHOLD_GAP)
			{
				if (Mathf.Abs(requested_start - start_fill_fraction) > 0.001f)
				{
					new_start = Mathf.Max(0f, new_stop - MINIMUM_THRESHOLD_GAP);
				}
				else
				{
					new_stop = Mathf.Min(1f, new_start + MINIMUM_THRESHOLD_GAP);
				}
			}
			start_fill_fraction = new_start;
			stop_fill_fraction = new_stop;
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			last_flow_liters_per_hour = 0f;
			last_source_count = 0;
			last_drive_multiplier = getDriveMultiplier();
			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.FreshWater);
			if (network == null || network.getFreshWaterCapacity() <= 0f)
			{
				pumping = false;
				FluidUtility.setPowerConsumption(parent, Props.power_watts, false);
				return;
			}

			List<CompWaterSource> sources = network.getComponents<CompWaterSource>().ToList();
			last_source_count = sources.Count;
			if (sources.Count == 0)
			{
				pumping = false;
				FluidUtility.setPowerConsumption(parent, Props.power_watts, false);
				return;
			}

			float fill_fraction = network.getStoredFreshWater() / network.getFreshWaterCapacity();
			if (controller_enabled)
			{
				if (pumping && fill_fraction >= stop_fill_fraction)
				{
					pumping = false;
				}
				else if (!pumping && fill_fraction <= start_fill_fraction)
				{
					pumping = true;
				}
			}
			else
			{
				pumping = fill_fraction < 0.9999f;
			}

			if (!pumping
				|| last_drive_multiplier <= 0.001f
				|| (Props.requires_power && !FluidUtility.isPoweredOn(parent)))
			{
				FluidUtility.setPowerConsumption(parent, Props.power_watts, false);
				return;
			}

			FluidUtility.setPowerConsumption(parent, Props.power_watts, Props.requires_power);
			last_route_length_m = network.getLongestRouteMeters(parent);
			float lossy_length = Mathf.Max(0f, last_route_length_m - Props.loss_free_length_m);
			float hydraulic_multiplier = 1f / Mathf.Sqrt(
				1f + lossy_length / Mathf.Max(1f, Props.hydraulic_reference_length_m));
			last_flow_multiplier = hydraulic_multiplier * last_drive_multiplier;
			float requested_liters = Props.nominal_liters_per_hour
				* last_flow_multiplier
				* elapsed_seconds / 3600f;
			WaterContamination source_contamination = mixSourceContamination(sources);
			float accepted_liters = network.addFreshWater(requested_liters, source_contamination);
			last_flow_liters_per_hour = elapsed_seconds <= 0f
				? 0f
				: accepted_liters * 3600f / elapsed_seconds;
		}

		private float getDriveMultiplier()
		{
			ThingComp component = getWindComponent();
			if (component == null)
			{
				return 1f;
			}

			PropertyInfo property = component.GetType().GetProperty("PumpPercent", REFLECTION_FLAGS);
			if (property != null)
			{
				try
				{
					return Mathf.Clamp01(Convert.ToSingle(property.GetValue(component, null)));
				}
				catch
				{
					return 0f;
				}
			}
			return 0f;
		}

		private ThingComp getWindComponent()
		{
			if (parent?.AllComps == null)
			{
				return null;
			}

			for (int index = 0; index < parent.AllComps.Count; index++)
			{
				ThingComp component = parent.AllComps[index];
				if (component?.GetType().FullName == "DubsBadHygiene.CompWindPump")
				{
					return component;
				}
			}
			return null;
		}

		private string getWindDriveReason()
		{
			ThingComp component = getWindComponent();
			if (component == null)
			{
				return string.Empty;
			}

			FieldInfo blocked_cells_field = component.GetType().GetField("windPathBlockedCells", REFLECTION_FLAGS);
			ICollection blocked_cells = blocked_cells_field?.GetValue(component) as ICollection;
			if (blocked_cells == null || blocked_cells.Count == 0)
			{
				return string.Empty;
			}

			FieldInfo blockers_field = component.GetType().GetField("windPathBlockedByThings", REFLECTION_FLAGS);
			IList blockers = blockers_field?.GetValue(component) as IList;
			Thing blocker = blockers != null && blockers.Count > 0 ? blockers[0] as Thing : null;
			if (blocker != null)
			{
				return "WindTurbine_WindPathIsBlockedBy".Translate().ToString()
					+ " " + blocker.Label;
			}

			return "WindTurbine_WindPathIsBlockedByRoof".Translate().ToString();
		}

		private static WaterContamination mixSourceContamination(List<CompWaterSource> sources)
		{
			WaterContamination result = new WaterContamination();
			float mixed_sources = 0f;
			for (int index = 0; index < sources.Count; index++)
			{
				WaterContamination source = sources[index].getContamination();
				float contribution = sources[index].getContributionWeight();
				result.mixWater(mixed_sources, contribution, source);
				mixed_sources += contribution;
			}
			return result;
		}
	}
}
