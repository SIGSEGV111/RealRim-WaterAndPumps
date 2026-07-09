using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RealRim.WaterAndPumps
{
	public abstract class Designator_SelectFluidLayer : Designator
	{
		private readonly FluidNetworkType network_type;

		protected Designator_SelectFluidLayer(FluidNetworkType network_type)
		{
			this.network_type = network_type;
			icon = RealRimTextures.getFluidLayerIcon(network_type);
			soundSucceeded = SoundDefOf.Tick_High;
			refreshLabels();
		}

		public override AcceptanceReport CanDesignateCell(IntVec3 cell)
		{
			return false;
		}

		public override void DesignateSingleCell(IntVec3 cell)
		{
		}

		public override void ProcessInput(Event ev)
		{
			refreshLabels();
			base.ProcessInput(ev);
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			for (int index = 0; index < FluidNetworkLayerUtility.LAYERS.Length; index++)
			{
				FluidNetworkLayer layer = FluidNetworkLayerUtility.LAYERS[index];
				options.Add(new FloatMenuOption(
					getLayerMenuLabel(layer),
					delegate
					{
						FluidNetworkLayerSettings.setSelectedLayer(network_type, layer);
						refreshLabels();
					}));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private void refreshLabels()
		{
			defaultLabel = "RealRim_SelectFluidLayerLabel".Translate(
				FluidUtility.getNetworkLabel(network_type),
				FluidNetworkLayerUtility.getLayerLabel(FluidNetworkLayerSettings.getSelectedLayer(network_type)));
			defaultDesc = "RealRim_SelectFluidLayerDesc".Translate(
				FluidUtility.getNetworkLabel(network_type),
				FluidNetworkLayerUtility.getLayerLabel(FluidNetworkLayerSettings.getSelectedLayer(network_type)));
		}

		private string getLayerMenuLabel(FluidNetworkLayer layer)
		{
			string label = "RealRim_SelectFluidLayerMenuEntry".Translate(
				FluidUtility.getNetworkLabel(network_type),
				FluidNetworkLayerUtility.getLayerLabel(layer));
			if (FluidNetworkLayerSettings.getSelectedLayer(network_type) == layer)
			{
				label += " " + "RealRim_CurrentSelectionSuffix".Translate();
			}
			return label;
		}
	}

	public sealed class Designator_SelectFreshWaterLayer : Designator_SelectFluidLayer
	{
		public Designator_SelectFreshWaterLayer() : base(FluidNetworkType.FreshWater)
		{
		}
	}

	public sealed class Designator_SelectHotWaterLayer : Designator_SelectFluidLayer
	{
		public Designator_SelectHotWaterLayer() : base(FluidNetworkType.HotWater)
		{
		}
	}

	public sealed class Designator_SelectHeatingLayer : Designator_SelectFluidLayer
	{
		public Designator_SelectHeatingLayer() : base(FluidNetworkType.Heating)
		{
		}
	}

	public sealed class Designator_SelectWasteWaterLayer : Designator_SelectFluidLayer
	{
		public Designator_SelectWasteWaterLayer() : base(FluidNetworkType.WasteWater)
		{
		}
	}

	public sealed class Designator_SelectCoolantLayer : Designator_SelectFluidLayer
	{
		public Designator_SelectCoolantLayer() : base(FluidNetworkType.Coolant)
		{
		}
	}

	public sealed class WorkGiver_ChangeFluidLayer : WorkGiver_Scanner
	{
		public override PathEndMode PathEndMode
		{
			get
			{
				return PathEndMode.ClosestTouch;
			}
		}

		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
		{
			if (pawn?.Map == null)
			{
				yield break;
			}

			List<Thing> things = pawn.Map.listerThings.AllThings;
			for (int index = 0; index < things.Count; index++)
			{
				ThingWithComps thing = things[index] as ThingWithComps;
				CompFluidNode node = thing?.TryGetComp<CompFluidNode>();
				if (node != null && node.hasPendingLayerChange())
				{
					yield return thing;
				}
			}
		}

		public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			CompFluidNode node = (thing as ThingWithComps)?.TryGetComp<CompFluidNode>();
			DesignationDef designation_def = FluidNetworkLayerUtility.getChangeDesignationDef();
			return node != null
				&& thing.Map != null
				&& designation_def != null
				&& node.hasPendingLayerChange()
				&& thing.Map.designationManager.DesignationOn(thing, designation_def) != null
				&& !thing.IsForbidden(pawn)
				&& pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Some);
		}

		public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			JobDef job_def = FluidNetworkLayerUtility.getChangeJobDef();
			if (job_def == null)
			{
				return null;
			}

			return JobMaker.MakeJob(job_def, thing);
		}
	}

	public sealed class JobDriver_ChangeFluidLayer : JobDriver
	{
		private const TargetIndex NODE_INDEX = TargetIndex.A;

		public override bool TryMakePreToilReservations(bool error_on_failed)
		{
			return pawn.Reserve(job.targetA, job, 1, -1, null, error_on_failed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(NODE_INDEX);
			this.FailOn(() => getTargetNode() == null || !getTargetNode().hasPendingLayerChange());
			yield return Toils_Goto.GotoThing(NODE_INDEX, PathEndMode.ClosestTouch);

			Toil wait = Toils_General.WaitWith(
				NODE_INDEX,
				FluidNetworkLayerUtility.CHANGE_LAYER_WORK_TICKS,
				true);
			wait.WithProgressBarToilDelay(NODE_INDEX, false, -0.5f);
			wait.FailOnDespawnedNullOrForbidden(NODE_INDEX);
			wait.FailOnCannotTouch(NODE_INDEX, PathEndMode.ClosestTouch);
			wait.FailOn(() => getTargetNode() == null || !getTargetNode().hasPendingLayerChange());
			yield return wait;
			yield return Toils_General.Do(applyLayerChange);
		}

		private void applyLayerChange()
		{
			getTargetNode()?.applyPendingLayerChanges();
		}

		private CompFluidNode getTargetNode()
		{
			ThingWithComps thing = job.targetA.Thing as ThingWithComps;
			return thing?.TryGetComp<CompFluidNode>();
		}
	}
}
