using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace RealRim.WaterAndPumps
{
	public sealed class WorkGiver_EmptyWasteStorage : WorkGiver_Scanner
	{
		private const float EXTRACTION_THRESHOLD_KG = 10f;
		private const string JOB_DEF_NAME = "emptySeptictank";

		public override PathEndMode PathEndMode
		{
			get
			{
				return PathEndMode.InteractionCell;
			}
		}

		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
		{
			if (pawn?.Map == null)
			{
				return Enumerable.Empty<Thing>();
			}

			return pawn.Map.listerThings.AllThings
				.Where(thing => (thing as ThingWithComps)?.TryGetComp<CompWasteStorage>() != null);
		}

		public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			CompWasteStorage storage = (thing as ThingWithComps)?.TryGetComp<CompWasteStorage>();
			return storage != null
				&& storage.getExtractableSludgeKg() + 0.0001f >= EXTRACTION_THRESHOLD_KG
				&& !thing.IsForbidden(pawn)
				&& pawn.CanReserveAndReach(thing, PathEndMode.InteractionCell, Danger.Some);
		}

		public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			JobDef job_def = DefDatabase<JobDef>.GetNamedSilentFail(JOB_DEF_NAME);
			return job_def == null ? null : JobMaker.MakeJob(job_def, thing);
		}
	}

	public sealed class JobDriver_EmptyWasteStorage : JobDriver
	{
		private const float EXTRACTION_BATCH_KG = 10f;
		private const float SLUDGE_KG_PER_ITEM = 0.05f;
		private const int EXTRACTION_TICKS = 600;
		private const string SLUDGE_DEF_NAME = "FecalSludge";
		private const TargetIndex STORAGE_INDEX = TargetIndex.A;

		public override bool TryMakePreToilReservations(bool error_on_failed)
		{
			return pawn.Reserve(job.targetA, job, 1, -1, null, error_on_failed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(STORAGE_INDEX);
			yield return Toils_Goto.GotoThing(STORAGE_INDEX, PathEndMode.InteractionCell);
			yield return Toils_General.WaitWith(STORAGE_INDEX, EXTRACTION_TICKS, true);
			yield return Toils_General.Do(extractSludge);
		}

		private void extractSludge()
		{
			ThingWithComps storage_thing = job.targetA.Thing as ThingWithComps;
			CompWasteStorage storage = storage_thing?.TryGetComp<CompWasteStorage>();
			ThingDef sludge_def = DefDatabase<ThingDef>.GetNamedSilentFail(SLUDGE_DEF_NAME);
			if (storage == null || sludge_def == null || pawn.Map == null)
			{
				return;
			}

			float extracted_kg = storage.extractSludge(EXTRACTION_BATCH_KG);
			int item_count = UnityEngine.Mathf.RoundToInt(extracted_kg / SLUDGE_KG_PER_ITEM);
			if (item_count <= 0)
			{
				return;
			}

			Thing sludge = ThingMaker.MakeThing(sludge_def);
			sludge.stackCount = item_count;
			GenPlace.TryPlaceThing(sludge, pawn.Position, pawn.Map, ThingPlaceMode.Near);
		}
	}
}
