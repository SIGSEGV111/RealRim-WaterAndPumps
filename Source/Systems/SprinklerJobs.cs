using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RealRim.WaterAndPumps
{
	public sealed class PlaceWorker_Sprinkler : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(
			BuildableDef checking_def,
			IntVec3 location,
			Rot4 rotation,
			Map map,
			Thing thing_to_ignore = null,
			Thing thing = null)
		{
			List<Thing> things = map.thingGrid.ThingsListAt(location);
			for (int index = 0; index < things.Count; index++)
			{
				Thing existing = things[index];
				if (existing == thing_to_ignore)
				{
					continue;
				}

				ThingDef existing_def = existing.def.entityDefToBuild as ThingDef ?? existing.def;
				if (existing_def.GetCompProperties<CompProperties_Sprinkler>() != null)
				{
					return "RealRim_SprinklerAlreadyHere".Translate();
				}
			}

			return base.AllowsPlacing(
				checking_def,
				location,
				rotation,
				map,
				thing_to_ignore,
				thing);
		}

		public override void DrawGhost(
			ThingDef def,
			IntVec3 center,
			Rot4 rotation,
			Color ghost_color,
			Thing thing = null)
		{
			CompProperties_Sprinkler properties = def.GetCompProperties<CompProperties_Sprinkler>();
			GenDraw.DrawRadiusRing(center, properties?.maximum_radius ?? 6.9f);
		}
	}

	public sealed class JobDriver_TriggerSprinkler : JobDriver
	{
		private const TargetIndex SPRINKLER_INDEX = TargetIndex.A;
		private const int DEFAULT_TRIGGER_TICKS = 60;

		private int trigger_ticks = DEFAULT_TRIGGER_TICKS;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref trigger_ticks, "sprinkler_trigger_ticks", DEFAULT_TRIGGER_TICKS);
		}

		public override void Notify_Starting()
		{
			base.Notify_Starting();
			ThingWithComps sprinkler = job.targetA.Thing as ThingWithComps;
			CompUsable usable = sprinkler?.TryGetComp<CompUsable>();
			if (usable != null)
			{
				trigger_ticks = Mathf.Max(1, usable.Props.useDuration);
			}
		}

		public override bool TryMakePreToilReservations(bool error_on_failed)
		{
			return pawn.Reserve(job.targetA, job, 1, -1, null, error_on_failed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
			yield return Toils_Goto.GotoThing(SPRINKLER_INDEX, PathEndMode.Touch);

			Toil wait = Toils_General.Wait(trigger_ticks, TargetIndex.None);
			wait.WithProgressBarToilDelay(SPRINKLER_INDEX, false, -0.5f);
			wait.FailOnDespawnedNullOrForbidden(SPRINKLER_INDEX);
			wait.FailOnCannotTouch(SPRINKLER_INDEX, PathEndMode.Touch);
			yield return wait;
			yield return Toils_General.Do(triggerSprinkler);
		}

		private void triggerSprinkler()
		{
			ThingWithComps sprinkler = job.targetA.Thing as ThingWithComps;
			sprinkler?.TryGetComp<CompSprinkler>()?.triggerManually();
		}
	}
}
