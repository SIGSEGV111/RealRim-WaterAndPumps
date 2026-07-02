using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_TargetTemperature : CompProperties
	{
		public float default_temperature_c = 21f;
		public float minimum_temperature_c = 5f;
		public float maximum_temperature_c = 35f;

		public CompProperties_TargetTemperature()
		{
			compClass = typeof(CompTargetTemperature);
		}
	}

	public sealed class CompTargetTemperature : ThingComp
	{
		public float target_temperature_c;

		public CompProperties_TargetTemperature Props
		{
			get
			{
				return (CompProperties_TargetTemperature)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			if (!respawning_after_load)
			{
				target_temperature_c = Props.default_temperature_c;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref target_temperature_c, "target_temperature_c", Props.default_temperature_c);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				target_temperature_c = Mathf.Clamp(target_temperature_c, Props.minimum_temperature_c, Props.maximum_temperature_c);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo gizmo in base.CompGetGizmosExtra())
			{
				yield return gizmo;
			}

			yield return new Command_Action
			{
				defaultLabel = "RealRim_TargetTemperatureDown".Translate(),
				defaultDesc = "RealRim_TargetTemperatureDownDesc".Translate(),
				action = delegate
				{
					target_temperature_c = Mathf.Max(Props.minimum_temperature_c, target_temperature_c - 1f);
				},
			};
			yield return new Command_Action
			{
				defaultLabel = "RealRim_TargetTemperatureUp".Translate(),
				defaultDesc = "RealRim_TargetTemperatureUpDesc".Translate(),
				action = delegate
				{
					target_temperature_c = Mathf.Min(Props.maximum_temperature_c, target_temperature_c + 1f);
				},
			};
		}
	}
}
