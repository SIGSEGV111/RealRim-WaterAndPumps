using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RealRim.WaterAndPumps
{
	[StaticConstructorOnStartup]
	internal static class DbhRuntimeBridge
	{
		private const string HARMONY_ID = "sigsegv11.realrim.water.physics-replacement";
		private const string THIRST_NEED_DEF = "DBHThirst";
		private const string HYGIENE_NEED_DEF = "Hygiene";
		private const string SWIMMING_JOB_DEF = "DBHGoSwimming";
		private const string SLUDGE_DEF_NAME = "FecalSludge";
		private const float DRINK_NEED_PER_TICK = 0.006f;
		private const float HAND_WASH_LITERS_PER_TICK = 0.04f;
		private const float HAND_WASH_HYGIENE_PER_TICK = 0.002f;
		private const float SLUDGE_KG_PER_ITEM = 0.05f;
		private const BindingFlags INSTANCE_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags STATIC_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private static readonly Dictionary<string, FieldInfo> FIELD_CACHE = new Dictionary<string, FieldInfo>();
		private static readonly Dictionary<string, MethodInfo> METHOD_CACHE = new Dictionary<string, MethodInfo>();
		private static readonly HashSet<int> SWIMMING_ANIMATION_PAWNS = new HashSet<int>();
		private static readonly HashSet<int> SWIMMING_DIAGNOSTIC_PAWNS = new HashSet<int>();
		private static AnimationDef vanilla_swimming_animation;
		private static bool vanilla_swimming_animation_resolved;
		private static bool vanilla_swimming_animation_disabled;
		private static bool swimming_diagnostics_logged;
		private static FieldInfo vanilla_swimming_job_field;
		private static bool vanilla_swimming_job_field_resolved;
		private static bool vanilla_swimming_job_field_disabled;

		static DbhRuntimeBridge()
		{
			LongEventHandler.ExecuteWhenFinished(applyPatches);
		}

		private static void applyPatches()
		{
			try
			{
				object harmony;
				MethodInfo patch_method;
				Type harmony_method_type;
				if (!tryCreateHarmony(out harmony, out patch_method, out harmony_method_type))
				{
					Log.Error("[RealRim] Water & Pumps: Harmony API was not available.");
					return;
				}

				int patched_methods = 0;
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.PlumbingNet", "Tick", nameof(skipOriginal));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.PlumbingNet", "TickWater", nameof(skipOriginal));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.PlumbingNet", "TickHeating", nameof(skipOriginal));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.PlumbingNet", "TickAircon", nameof(skipOriginal));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.PlumbingNet", "PullWater", nameof(pullWaterPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.PlumbingNet", "PushWater", nameof(pushWaterPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.PlumbingNet", "PushSewage", nameof(pushSewagePrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.PlumbingNet", "PullHotWater", nameof(pullHotWaterPrefix));

				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_AssignableFixture", "GetInspectString", nameof(fixtureInspectStringPrefix));

				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.CompWaterPumpingStation", "PostDrawExtraSelectionOverlays", nameof(skipOriginal));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.CompWaterPumpingStation", "CompInspectStringExtra", nameof(nullStringPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.CompWindPump", "CompInspectStringExtra", nameof(nullStringPrefix));

				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.SectionLayer_PipeOverlay", "DrawLayer", nameof(skipOriginal));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.SectionLayer_PipeOverlay", "Regenerate", nameof(skipOriginal));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "Verse.Graphic_Linked", "Print", nameof(visiblePipeGraphicPrintPrefix));

				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Alert_BlockedSewer", "GetReport", nameof(emptyAlertPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Alert_WaterTemp", "GetReport", nameof(emptyAlertPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Alert_ContamLevels", "GetReport", nameof(emptyAlertPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Alert_ContaminatedTower", "GetReport", nameof(emptyAlertPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Alert_MissingPump", "GetReport", nameof(emptyAlertPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Alert_MissingWell", "GetReport", nameof(emptyAlertPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Alert_MissingTower", "GetReport", nameof(emptyAlertPrefix));

				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_AssignableFixture", "Working", nameof(fixtureWorkingPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_WaterTrough", "Working", nameof(troughWorkingPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_Latrine", "Working", nameof(latrineWorkingPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_shower", "TryUseWater", nameof(showerTryUseWaterPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_bath", "TryFillBath", nameof(bathTryFillPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_toilet", "TryUseFlush", nameof(toiletTryUsePrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.bibblefuckwit", "TryUseFlush", nameof(toiletTryUsePrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_SpacerToilet", "TryUseFlush", nameof(toiletTryUsePrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_Latrine", "TryUseFlush", nameof(latrineTryUsePrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_FillableThing", "Working", nameof(poolWorkingPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_Pool", "GoodTemp", nameof(poolGoodTemperaturePrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_Pool", "GetInspectString", nameof(poolInspectStringPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.CompWaterFillable", "CompInspectStringExtra", nameof(waterFillableInspectPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_FillableThing", "Tick", nameof(fillableThingTickPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Building_washbucket", "TickRare", nameof(washBucketTickRarePrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JoyGiver_GoSwimming", "TryGivePlayJob", nameof(swimmingJobPrefix));

				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.Need_Thirst", "get_FallPerTick", nameof(thirstFallPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_DrinkFromBasin", "<MakeNewToils>b__3_2", nameof(drinkFromFixtureTickPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_DrinkFromBasin", "<MakeNewToils>b__3_3", nameof(basinDrinkFinishedPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_DrinkFromGround", "<MakeNewToils>b__1_2", nameof(groundDrinkFinishedPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_washHands", "<MakeNewToils>b__3_3", nameof(washHandsTickPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_emptyLatrine", "<MakeNewToils>b__1_0", nameof(emptyLatrinePrefix));
				patched_methods += patchAllNamedPostfix(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_GoSwimming", "swimticker", nameof(swimTickerPostfix));
				patched_methods += patchAllNamedPostfix(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_GoSwimming", "Finishac", nameof(poolSwimmingFinishPostfix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "Verse.AI.Toils_Recipe+<>c__DisplayClass2_0", "<DoRecipeWork>b__2", nameof(recipeWorkTickPrefix));

				Log.Message("[RealRim] Water & Pumps 1.1.35: redirected " + patched_methods
					+ " DBH runtime methods to RealRim physics.");
			}
			catch (Exception exception)
			{
				Log.Error("[RealRim] Water & Pumps: runtime bridge failed: " + exception);
			}
		}

		public static bool fixtureInspectStringPrefix(object __instance, ref string __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			if (!isKitchenSink(thing))
			{
				return true;
			}

			StringBuilder status = new StringBuilder();
			List<ThingComp> comps = thing.AllComps;
			if (comps != null)
			{
				for (int index = 0; index < comps.Count; index++)
				{
					ThingComp comp = comps[index];
					if (comp == null || isLegacyKitchenSinkComp(comp))
					{
						continue;
					}
					string line = comp.CompInspectStringExtra();
					if (line.NullOrEmpty())
					{
						continue;
					}
					if (status.Length > 0)
					{
						status.AppendLine();
					}
					status.Append(line.TrimEndNewlines());
				}
			}
			__result = status.ToString().TrimEndNewlines();
			return false;
		}

		private static bool isKitchenSink(ThingWithComps thing)
		{
			return thing != null && RealRimDefPatcher.isKitchenSinkDefinition(thing.def);
		}

		private static bool isLegacyKitchenSinkComp(ThingComp comp)
		{
			if (comp == null)
			{
				return false;
			}
			string type_name = comp.GetType().FullName;
			return type_name == "DubsBadHygiene.CompPipe"
				|| type_name == "DubsBadHygiene.CompBlockage";
		}

		public static bool visiblePipeGraphicPrintPrefix(
			Graphic_Linked __instance,
			SectionLayer layer,
			Thing thing)
		{
			return !FluidNetworkVisuals.tryPrintVisiblePipe(thing, layer, __instance);
		}

		public static bool skipOriginal()
		{
			return false;
		}

		public static bool nullStringPrefix(ref string __result)
		{
			__result = null;
			return false;
		}

		public static bool emptyAlertPrefix(ref AlertReport __result)
		{
			__result = false;
			return false;
		}

		public static bool pullWaterPrefix(object __instance, object[] __args, MethodBase __originalMethod, ref bool __result)
		{
			float requested = __args.Length == 0 ? 0f : Convert.ToSingle(__args[0]);
			FluidNetwork network = getLegacyNetwork(__instance, FluidNetworkType.FreshWater);
			float delivered = network == null ? 0f : network.drawFreshWater(requested);
			__result = delivered + 0.001f >= requested;
			if (__args.Length > 1)
			{
				Type parameter_type = __originalMethod.GetParameters()[1].ParameterType.GetElementType();
				__args[1] = parameter_type == null ? null : Activator.CreateInstance(parameter_type);
			}
			return false;
		}

		public static bool pushWaterPrefix(object __instance, object[] __args, ref float __result)
		{
			float requested = __args.Length == 0 ? 0f : Convert.ToSingle(__args[0]);
			FluidNetwork network = getLegacyNetwork(__instance, FluidNetworkType.FreshWater);
			__result = network == null ? requested : requested - network.addFreshWater(requested);
			return false;
		}

		public static bool pushSewagePrefix(object __instance, object[] __args, ref bool __result)
		{
			float requested = __args.Length == 0 ? 0f : Convert.ToSingle(__args[0]);
			FluidNetwork network = getLegacyNetwork(__instance, FluidNetworkType.WasteWater);
			__result = network != null && network.pushWaste(requested, 0f);
			return false;
		}

		public static bool pullHotWaterPrefix(object __instance, object[] __args, ref bool __result)
		{
			float requested = __args.Length == 0 ? 0f : Convert.ToSingle(__args[0]);
			FluidNetwork network = getLegacyNetwork(__instance, FluidNetworkType.HotWater);
			float delivered = network == null ? 0f : network.drawHotWater(requested);
			__result = delivered + 0.001f >= requested;
			return false;
		}

		public static bool fixtureWorkingPrefix(object __instance, ref AcceptanceReport __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompFixture fixture = thing == null ? null : thing.TryGetComp<CompFixture>();
			if (fixture == null)
			{
				return true;
			}
			__result = fixture.getWorkingReport();
			return false;
		}

		public static bool troughWorkingPrefix(object __instance, ref AcceptanceReport __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompWaterTrough trough = thing == null ? null : thing.TryGetComp<CompWaterTrough>();
			Pawn pawn = thing == null ? null : FluidUtility.findUsingPawn(thing);
			if (trough == null)
			{
				return true;
			}
			__result = trough.canDrink(pawn) ? (AcceptanceReport)true : (AcceptanceReport)"RealRim_TroughEmpty".Translate();
			return false;
		}

		public static bool latrineWorkingPrefix(object __instance, ref AcceptanceReport __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompLatrine latrine = thing == null ? null : thing.TryGetComp<CompLatrine>();
			if (latrine == null)
			{
				return true;
			}
			__result = latrine.canUse() ? (AcceptanceReport)true : (AcceptanceReport)"RealRim_LatrineUnavailable".Translate();
			return false;
		}

		public static bool showerTryUseWaterPrefix(object __instance, object[] __args, MethodBase __originalMethod, ref bool __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompFixture fixture = thing == null ? null : thing.TryGetComp<CompFixture>();
			if (fixture == null)
			{
				return true;
			}
			bool cold;
			__result = fixture.tryUse(FluidUtility.findUsingPawn(thing), out cold);
			if (__args.Length > 0)
			{
				__args[0] = cold;
			}
			if (__args.Length > 1)
			{
				Type parameter_type = __originalMethod.GetParameters()[1].ParameterType.GetElementType();
				__args[1] = parameter_type == null ? null : Activator.CreateInstance(parameter_type);
			}
			return false;
		}

		public static bool bathTryFillPrefix(object __instance, ref bool __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompFixture fixture = thing == null ? null : thing.TryGetComp<CompFixture>();
			if (fixture == null)
			{
				return true;
			}
			bool cold;
			__result = fixture.tryUse(FluidUtility.findUsingPawn(thing), out cold);
			if (__result)
			{
				addFloatField(__instance, "WaterInTub", fixture.Props.water_per_use_liters);
				setField(__instance, "cold", cold);
			}
			return false;
		}

		public static bool toiletTryUsePrefix(object __instance, ref bool __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompFixture fixture = thing == null ? null : thing.TryGetComp<CompFixture>();
			if (fixture == null)
			{
				return true;
			}
			bool cold;
			__result = fixture.tryUse(FluidUtility.findUsingPawn(thing), out cold);
			return false;
		}

		public static bool latrineTryUsePrefix(object __instance, ref bool __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompLatrine latrine = thing == null ? null : thing.TryGetComp<CompLatrine>();
			if (latrine == null)
			{
				return true;
			}
			__result = latrine.useLatrine();
			if (__result)
			{
				setField(__instance, "sewage", latrine.getSludgeItemCount());
			}
			return false;
		}

		public static bool poolInspectStringPrefix(object __instance, ref string __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompPoolPhysics pool = thing?.TryGetComp<CompPoolPhysics>();
			if (pool == null)
			{
				return true;
			}
			List<string> lines = new List<string>();
			string pool_status = pool.CompInspectStringExtra();
			if (!pool_status.NullOrEmpty())
			{
				lines.Add(pool_status);
			}
			CompFluidNode node = thing.TryGetComp<CompFluidNode>();
			string network_status = node?.CompInspectStringExtra();
			if (!network_status.NullOrEmpty())
			{
				lines.Add(network_status);
			}
			__result = string.Join("\n", lines.ToArray()).TrimEnd('\r', '\n', ' ', '\t');
			return false;
		}

		public static bool waterFillableInspectPrefix(object __instance, ref string __result)
		{
			ThingComp component = __instance as ThingComp;
			if (component?.parent?.TryGetComp<CompLatrine>() == null)
			{
				return true;
			}
			__result = null;
			return false;
		}

		public static bool fillableThingTickPrefix(object __instance)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			return thing == null || thing.TryGetComp<CompPoolPhysics>() == null;
		}

		public static bool washBucketTickRarePrefix(object __instance)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			return thing == null || thing.TryGetComp<CompWaterTrough>() == null;
		}

		public static bool poolWorkingPrefix(object __instance, ref AcceptanceReport __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompPoolPhysics pool = thing == null ? null : thing.TryGetComp<CompPoolPhysics>();
			if (pool == null)
			{
				return true;
			}

			__result = pool.getWorkingReport();
			return false;
		}

		public static bool poolGoodTemperaturePrefix(object __instance, ref bool __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompPoolPhysics pool = thing == null ? null : thing.TryGetComp<CompPoolPhysics>();
			if (pool == null)
			{
				return true;
			}
			__result = pool.canPawnUse(FluidUtility.findUsingPawn(thing));
			return false;
		}

		public static bool swimmingJobPrefix(object __instance, object[] __args, ref Job __result)
		{
			Pawn pawn = __args.Length > 0 ? __args[0] as Pawn : null;
			Thing offered_pool = __args.Length > 1 ? __args[1] as Thing : null;
			if (pawn == null || offered_pool == null || pawn.Map == null)
			{
				return true;
			}

			ThingDef pool_def = DefDatabase<ThingDef>.GetNamedSilentFail("DBHSwimmingPool");
			if (pool_def == null)
			{
				return true;
			}

			Thing best_pool = null;
			float best_score = -10000f;
			List<Thing> pools = pawn.Map.listerThings.ThingsOfDef(pool_def);
			for (int index = 0; index < pools.Count; index++)
			{
				Thing candidate = pools[index];
				CompPoolPhysics physics = (candidate as ThingWithComps)?.TryGetComp<CompPoolPhysics>();
				if (physics == null || !physics.canPawnUse(pawn) || candidate.IsForbidden(pawn))
				{
					continue;
				}
				if (!pawn.CanReach(candidate, PathEndMode.Touch, Danger.Some))
				{
					continue;
				}
				float score = physics.getPawnPreferenceScore(pawn) - pawn.Position.DistanceTo(candidate.Position) * 0.01f;
				if (score > best_score)
				{
					best_score = score;
					best_pool = candidate;
				}
			}

			if (best_pool == null)
			{
				__result = null;
				return false;
			}
			if (best_pool == offered_pool)
			{
				return true;
			}

			FieldInfo joy_def_field = getField(__instance.GetType(), "def");
			JoyGiverDef joy_def = joy_def_field == null ? null : joy_def_field.GetValue(__instance) as JoyGiverDef;
			if (joy_def == null || joy_def.jobDef == null)
			{
				return true;
			}
			__result = JobMaker.MakeJob(joy_def.jobDef, best_pool);
			return false;
		}

		public static bool thirstFallPrefix(ref float __result)
		{
			__result = 0.5f / RealPhysics.TICKS_PER_DAY;
			return false;
		}

		public static bool drinkFromFixtureTickPrefix(object __instance)
		{
			Pawn pawn;
			Job job;
			if (!tryGetDriverContext(__instance, out pawn, out job) || pawn.needs == null)
			{
				return true;
			}

			Thing target = job.targetA.Thing;
			ThingWithComps target_with_comps = target as ThingWithComps;
			CompWaterTrough trough = target_with_comps?.TryGetComp<CompWaterTrough>();
			CompFixture fixture = target_with_comps?.TryGetComp<CompFixture>();
			if (trough == null && fixture == null)
			{
				return true;
			}

			NeedDef thirst_def = DefDatabase<NeedDef>.GetNamedSilentFail(THIRST_NEED_DEF);
			Need thirst = thirst_def == null ? null : pawn.needs.TryGetNeed(thirst_def);
			if (thirst == null)
			{
				return false;
			}

			float capacity_liters = CompWaterTrough.getInternalCapacityLiters(pawn);
			float requested_liters = capacity_liters * DRINK_NEED_PER_TICK;
			WaterSample sample = trough != null
				? trough.drawWaterSample(requested_liters)
				: fixture.drawDrinkingWaterSample(requested_liters);
			if (sample.liters + 0.0001f < requested_liters)
			{
				endDriverJob(__instance, JobCondition.Incompletable);
				return false;
			}

			WaterPathogenUtility.tryInfectPawn(pawn, sample);
			thirst.CurLevel = Mathf.Clamp01(thirst.CurLevel + sample.liters / capacity_liters);
			setField(thirst, "lastGainTick", Find.TickManager.TicksGame);
			return false;
		}


		public static bool basinDrinkFinishedPrefix(object __instance)
		{
			Pawn pawn;
			Job job;
			if (!tryGetDriverContext(__instance, out pawn, out job))
			{
				return true;
			}
			ThingWithComps target = job.targetA.Thing as ThingWithComps;
			return target == null
				|| (target.TryGetComp<CompWaterTrough>() == null
					&& target.TryGetComp<CompFixture>() == null);
		}

		public static bool groundDrinkFinishedPrefix(object __instance)
		{
			Pawn pawn;
			Job job;
			if (!tryGetDriverContext(__instance, out pawn, out job) || pawn.Map == null)
			{
				return true;
			}
			IntVec3 cell = job.targetA.Cell;
			WaterSample sample = new WaterSample
			{
				liters = CompWaterTrough.getDrinkLiters(pawn),
				contamination = WaterPathogenUtility.getTerrainContamination(pawn.Map, cell),
			};
			WaterPathogenUtility.tryInfectPawn(pawn, sample);
			return false;
		}

		public static bool washHandsTickPrefix(object __instance)
		{
			Pawn pawn;
			Job job;
			if (!tryGetDriverContext(__instance, out pawn, out job) || pawn.needs == null)
			{
				return true;
			}

			CompFixture fixture = (job.targetA.Thing as ThingWithComps)?.TryGetComp<CompFixture>();
			if (fixture == null)
			{
				return true;
			}

			bool cold;
			if (!fixture.tryUseVolume(
				pawn,
				HAND_WASH_LITERS_PER_TICK,
				HAND_WASH_LITERS_PER_TICK,
				0f,
				false,
				out cold))
			{
				endDriverJob(__instance, JobCondition.Incompletable);
				return false;
			}

			NeedDef hygiene_def = DefDatabase<NeedDef>.GetNamedSilentFail(HYGIENE_NEED_DEF);
			Need hygiene = hygiene_def == null ? null : pawn.needs.TryGetNeed(hygiene_def);
			MethodInfo clean_method = hygiene == null
				? null
				: getMethod(hygiene.GetType(), "clean", new Type[] { typeof(float) });
			if (clean_method != null)
			{
				clean_method.Invoke(hygiene, new object[] { HAND_WASH_HYGIENE_PER_TICK });
			}
			return false;
		}

		public static bool emptyLatrinePrefix(object __instance)
		{
			Pawn pawn;
			Job job;
			if (!tryGetDriverContext(__instance, out pawn, out job))
			{
				return true;
			}

			ThingWithComps latrine_thing = job.targetA.Thing as ThingWithComps;
			CompLatrine latrine = latrine_thing?.TryGetComp<CompLatrine>();
			if (latrine == null)
			{
				return true;
			}

			float sludge_kg = latrine.clearWaste();
			int item_count = Mathf.CeilToInt(sludge_kg / SLUDGE_KG_PER_ITEM);
			ThingDef sludge_def = DefDatabase<ThingDef>.GetNamedSilentFail(SLUDGE_DEF_NAME);
			if (item_count > 0 && sludge_def != null && pawn.Map != null)
			{
				Thing sludge = ThingMaker.MakeThing(sludge_def);
				sludge.stackCount = item_count;
				GenPlace.TryPlaceThing(sludge, pawn.Position, pawn.Map, ThingPlaceMode.Near);
			}
			setField(latrine_thing, "sewage", 0f);
			return false;
		}

		public static bool recipeWorkTickPrefix(object __instance, int delta)
		{
			if (__instance == null || delta <= 0)
			{
				return true;
			}

			Toil toil = findClosureReference<Toil>(__instance, 3);
			Pawn pawn = toil?.GetActor() ?? findClosureReference<Pawn>(__instance, 3);
			Job job = pawn?.CurJob ?? findClosureReference<Job>(__instance, 3);
			if (job?.RecipeDef == null || job.RecipeDef.workSkill != SkillDefOf.Cooking)
			{
				return true;
			}

			Building_WorkTable work_table = job.targetA.Thing as Building_WorkTable
				?? job.targetB.Thing as Building_WorkTable
				?? job.targetC.Thing as Building_WorkTable;
			CompFixture kitchen_sink = findFacilityLinkedKitchenSink(work_table);
			if (kitchen_sink != null)
			{
				kitchen_sink.recordLinkedStoveUse(delta * RealPhysics.SECONDS_PER_GAME_TICK);
			}
			return true;
		}

		private static CompFixture findFacilityLinkedKitchenSink(Building_WorkTable work_table)
		{
			CompAffectedByFacilities affected = work_table?.TryGetComp<CompAffectedByFacilities>();
			List<Thing> linked_facilities = affected?.LinkedFacilitiesListForReading;
			if (linked_facilities == null)
			{
				return null;
			}

			for (int index = 0; index < linked_facilities.Count; index++)
			{
				CompFixture fixture = (linked_facilities[index] as ThingWithComps)?.TryGetComp<CompFixture>();
				if (fixture != null && fixture.Props.kitchen_sink)
				{
					return fixture;
				}
			}

			return null;
		}

		private static T findClosureReference<T>(object instance, int remaining_depth) where T : class
		{
			if (instance == null)
			{
				return null;
			}
			T direct = instance as T;
			if (direct != null)
			{
				return direct;
			}
			if (remaining_depth <= 0)
			{
				return null;
			}

			FieldInfo[] fields = instance.GetType().GetFields(INSTANCE_FLAGS);
			for (int index = 0; index < fields.Length; index++)
			{
				FieldInfo field = fields[index];
				if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string))
				{
					continue;
				}
				object value;
				try
				{
					value = field.GetValue(instance);
				}
				catch
				{
					continue;
				}
				T result = findClosureReference<T>(value, remaining_depth - 1);
				if (result != null)
				{
					return result;
				}
			}
			return null;
		}

		public static void swimTickerPostfix(object __instance)
		{
			Pawn pawn;
			Job job;
			if (!tryGetDriverContext(__instance, out pawn, out job))
			{
				return;
			}
			CompPoolPhysics pool = (job.targetA.Thing as ThingWithComps)?.TryGetComp<CompPoolPhysics>();
			pool?.applyPawnTemperatureThought(pawn);
			setPoolSwimmingState(job, isSwimmingInPool(pawn));
		}

		public static void poolSwimmingFinishPostfix(object __instance)
		{
			Pawn pawn;
			Job job;
			if (tryGetDriverContext(__instance, out pawn, out job))
			{
				setPoolSwimmingState(job, false);
			}
		}

		private static void setPoolSwimmingState(Job job, bool swimming)
		{
			if (job == null || !ModsConfig.OdysseyActive || !resolveVanillaSwimmingJobField())
			{
				return;
			}

			try
			{
				vanilla_swimming_job_field.SetValue(job, swimming);
			}
			catch (Exception exception)
			{
				vanilla_swimming_job_field_disabled = true;
				Log.Error("[RealRim] Water & Pumps: failed to set Odyssey's swimming-pose job field: " + exception);
			}
		}

		private static bool resolveVanillaSwimmingJobField()
		{
			if (vanilla_swimming_job_field_disabled)
			{
				return false;
			}
			if (vanilla_swimming_job_field_resolved)
			{
				return vanilla_swimming_job_field != null;
			}

			vanilla_swimming_job_field_resolved = true;
			Type swimming_driver_type = findType("RimWorld.JobDriver_GoSwimming");
			MethodInfo pose_method = swimming_driver_type == null
				? null
				: getMethod(swimming_driver_type, "CheckForSwimmingPose", Type.EmptyTypes);
			vanilla_swimming_job_field = resolveJobBooleanFieldWrittenBy(pose_method);
			if (vanilla_swimming_job_field == null)
			{
				vanilla_swimming_job_field_disabled = true;
				Log.Error("[RealRim] Water & Pumps: could not resolve Odyssey's Job swimming-pose field from JobDriver_GoSwimming.CheckForSwimmingPose.");
				return false;
			}

			Log.Message("[RealRim] Water & Pumps: resolved Odyssey swimming-pose job field "
				+ vanilla_swimming_job_field.DeclaringType.FullName
				+ "." + vanilla_swimming_job_field.Name + ".");
			return true;
		}

		private static FieldInfo resolveJobBooleanFieldWrittenBy(MethodInfo method)
		{
			if (method == null)
			{
				return null;
			}

			MethodBody body;
			try
			{
				body = method.GetMethodBody();
			}
			catch
			{
				return null;
			}
			byte[] il = body?.GetILAsByteArray();
			if (il == null || il.Length < 5)
			{
				return null;
			}

			for (int index = 0; index <= il.Length - 5; index++)
			{
				if (il[index] != 0x7D)
				{
					continue;
				}
				int token = BitConverter.ToInt32(il, index + 1);
				FieldInfo field;
				try
				{
					field = method.Module.ResolveField(token);
				}
				catch
				{
					continue;
				}
				if (field != null
					&& !field.IsStatic
					&& field.FieldType == typeof(bool)
					&& field.DeclaringType == typeof(Job))
				{
					return field;
				}
			}
			return null;
		}

		public static void pawnRendererPostTickPostfix(object __instance)
		{
			if (__instance == null || !ModsConfig.OdysseyActive)
			{
				return;
			}
			FieldInfo pawn_field = getField(__instance.GetType(), "pawn");
			Pawn pawn = pawn_field == null ? null : pawn_field.GetValue(__instance) as Pawn;
			maintainVanillaSwimmingAnimation(pawn);
		}

		public static void pawnRendererCurAnimationPostfix(object __instance, ref AnimationDef __result)
		{
			Pawn pawn = getRendererPawn(__instance);
			if (!isSwimmingInPool(pawn) || !resolveVanillaSwimmingAnimation())
			{
				return;
			}

			__result = vanilla_swimming_animation;
		}

		public static void pawnRendererHasAnimationPostfix(object __instance, ref bool __result)
		{
			Pawn pawn = getRendererPawn(__instance);
			if (isSwimmingInPool(pawn) && resolveVanillaSwimmingAnimation())
			{
				__result = true;
			}
		}

		public static void pawnRenderTreeAnimationTickPostfix(object __instance, ref int __result)
		{
			Pawn pawn = getRenderTreePawn(__instance);
			if (!isSwimmingInPool(pawn) || !resolveVanillaSwimmingAnimation())
			{
				return;
			}

			int duration_ticks = getAnimationDurationTicks(vanilla_swimming_animation);
			int ticks_game = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
			__result = duration_ticks <= 0 ? ticks_game : ticks_game % duration_ticks;
		}

		public static void pawnRenderTreeAnimationFinishedPostfix(object __instance, ref bool __result)
		{
			Pawn pawn = getRenderTreePawn(__instance);
			if (isSwimmingInPool(pawn) && resolveVanillaSwimmingAnimation())
			{
				__result = false;
			}
		}

		private static Pawn getRendererPawn(object renderer)
		{
			if (renderer == null)
			{
				return null;
			}
			FieldInfo pawn_field = getField(renderer.GetType(), "pawn");
			return pawn_field == null ? null : pawn_field.GetValue(renderer) as Pawn;
		}

		private static Pawn getRenderTreePawn(object render_tree)
		{
			if (render_tree == null)
			{
				return null;
			}
			FieldInfo pawn_field = getField(render_tree.GetType(), "pawn");
			return pawn_field == null ? null : pawn_field.GetValue(render_tree) as Pawn;
		}

		private static int getAnimationDurationTicks(AnimationDef animation)
		{
			if (animation == null)
			{
				return 0;
			}
			FieldInfo duration_field = getField(animation.GetType(), "durationTicks");
			if (duration_field == null)
			{
				return 240;
			}
			try
			{
				return Mathf.Max(1, Convert.ToInt32(duration_field.GetValue(animation)));
			}
			catch
			{
				return 240;
			}
		}

		private static bool isSwimmingInPool(Pawn pawn)
		{
			Job job = pawn?.CurJob;
			ThingWithComps pool = job == null ? null : job.targetA.Thing as ThingWithComps;
			return job != null
				&& job.def != null
				&& job.def.defName == SWIMMING_JOB_DEF
				&& pool != null
				&& pool.TryGetComp<CompPoolPhysics>() != null
				&& pawn.Spawned
				&& GenAdj.OccupiedRect(pool).Contains(pawn.Position);
		}

		private static void maintainVanillaSwimmingAnimation(Pawn pawn)
		{
			if (pawn == null || !ModsConfig.OdysseyActive)
			{
				return;
			}
			if (!isSwimmingInPool(pawn))
			{
				if (SWIMMING_ANIMATION_PAWNS.Remove(pawn.thingIDNumber))
				{
					clearSwimmingAnimation(pawn);
				}
				return;
			}
			if (!resolveVanillaSwimmingAnimation())
			{
				return;
			}

			try
			{
				object drawer = pawn.Drawer;
				FieldInfo renderer_field = drawer == null ? null : getField(drawer.GetType(), "renderer");
				object renderer = renderer_field == null ? null : renderer_field.GetValue(drawer);
				if (renderer == null)
				{
					return;
				}
				if (!SWIMMING_ANIMATION_PAWNS.Contains(pawn.thingIDNumber))
				{
					MethodInfo set_animation = getMethod(
						renderer.GetType(),
						"SetAnimation",
						new Type[] { typeof(AnimationDef) });
					if (set_animation == null)
					{
						throw new MissingMethodException(renderer.GetType().FullName, "SetAnimation(AnimationDef)");
					}
					set_animation.Invoke(renderer, new object[] { vanilla_swimming_animation });
					SWIMMING_ANIMATION_PAWNS.Add(pawn.thingIDNumber);
				}
			}
			catch (Exception exception)
			{
				vanilla_swimming_animation_disabled = true;
				Log.Error("[RealRim] Water & Pumps: Odyssey swimming-animation assignment failed: " + exception);
			}
		}

		private static bool resolveVanillaSwimmingAnimation()
		{
			if (vanilla_swimming_animation_disabled)
			{
				return false;
			}
			if (vanilla_swimming_animation_resolved)
			{
				return vanilla_swimming_animation != null;
			}

			vanilla_swimming_animation_resolved = true;
			Type swimming_driver_type = findType("RimWorld.JobDriver_GoSwimming");
			MethodInfo pose_method = swimming_driver_type == null
				? null
				: getMethod(swimming_driver_type, "CheckForSwimmingPose", Type.EmptyTypes);
			vanilla_swimming_animation = resolveAnimationOperand(pose_method);
			if (vanilla_swimming_animation == null)
			{
				vanilla_swimming_animation_disabled = true;
				Log.Warning("[RealRim] Water & Pumps: Odyssey's swimming animation could not be read from JobDriver_GoSwimming.CheckForSwimmingPose; pool pawns will use the swimming render state without an animation.");
			}
			return vanilla_swimming_animation != null;
		}

		private static AnimationDef resolveAnimationOperand(MethodInfo method)
		{
			if (method == null)
			{
				return null;
			}
			MethodBody body;
			try
			{
				body = method.GetMethodBody();
			}
			catch
			{
				return null;
			}
			byte[] il = body?.GetILAsByteArray();
			if (il == null || il.Length < 5)
			{
				return null;
			}

			for (int index = 0; index <= il.Length - 5; index++)
			{
				if (il[index] != 0x7E && il[index] != 0x7F)
				{
					continue;
				}
				int token = BitConverter.ToInt32(il, index + 1);
				FieldInfo field;
				try
				{
					field = method.Module.ResolveField(token);
				}
				catch
				{
					continue;
				}
				if (field == null || !field.IsStatic || field.FieldType != typeof(AnimationDef))
				{
					continue;
				}
				try
				{
					AnimationDef animation = field.GetValue(null) as AnimationDef;
					if (animation != null)
					{
						return animation;
					}
				}
				catch
				{
				}
			}
			return null;
		}

		private static void clearSwimmingAnimation(Pawn pawn)
		{
			try
			{
				object drawer = pawn?.Drawer;
				FieldInfo renderer_field = drawer == null ? null : getField(drawer.GetType(), "renderer");
				object renderer = renderer_field == null ? null : renderer_field.GetValue(drawer);
				MethodInfo set_animation = renderer == null
					? null
					: getMethod(renderer.GetType(), "SetAnimation", new Type[] { typeof(AnimationDef) });
				set_animation?.Invoke(renderer, new object[] { null });
			}
			catch (Exception exception)
			{
				Log.Warning("[RealRim] Water & Pumps: could not clear Odyssey swimming animation: " + exception.Message);
			}
		}

		private static void logPoolPawnRuntimeDiagnostics(Pawn pawn, Job job)
		{
			if (pawn == null || job == null || !ModsConfig.OdysseyActive
				|| !SWIMMING_DIAGNOSTIC_PAWNS.Add(pawn.thingIDNumber))
			{
				return;
			}
			try
			{
				ThingWithComps pool = job.targetA.Thing as ThingWithComps;
				object drawer = pawn.Drawer;
				FieldInfo renderer_field = drawer == null ? null : getField(drawer.GetType(), "renderer");
				object renderer = renderer_field == null ? null : renderer_field.GetValue(drawer);
				FieldInfo render_tree_field = renderer == null ? null : getField(renderer.GetType(), "renderTree");
				object render_tree = render_tree_field == null ? null : render_tree_field.GetValue(renderer);
				StringBuilder report = new StringBuilder();
				report.AppendLine("[RealRim] Pool-swimming pawn runtime diagnostics:");
				report.AppendLine("  pawn=" + pawn.LabelShortCap
					+ "; thingID=" + pawn.thingIDNumber
					+ "; position=" + pawn.Position
					+ "; spawned=" + pawn.Spawned);
				report.AppendLine("  jobDef=" + (job.def == null ? "<null>" : job.def.defName)
					+ "; jobDriver=" + (pawn.jobs?.curDriver == null ? "<null>" : pawn.jobs.curDriver.GetType().AssemblyQualifiedName));
				report.AppendLine("  pool=" + (pool == null ? "<null>" : pool.LabelCap + " / " + pool.def?.defName)
					+ "; target=" + job.targetA
					+ "; insidePool=" + isSwimmingInPool(pawn));
				report.AppendLine("  Pawn.Swimming=" + pawn.Swimming
					+ "; resolverSucceeded=" + resolveVanillaSwimmingAnimation()
					+ "; resolvedAnimation=" + (vanilla_swimming_animation == null ? "<null>" : vanilla_swimming_animation.defName));
				report.AppendLine("  drawerType=" + (drawer == null ? "<null>" : drawer.GetType().AssemblyQualifiedName));
				report.AppendLine("  rendererType=" + (renderer == null ? "<null>" : renderer.GetType().AssemblyQualifiedName));
				report.AppendLine("  renderer.CurAnimation=" + getReflectedValue(renderer, "CurAnimation"));
				report.AppendLine("  renderer.HasAnimation=" + getReflectedValue(renderer, "HasAnimation"));
				report.AppendLine("  renderTreeType=" + (render_tree == null ? "<null>" : render_tree.GetType().AssemblyQualifiedName));
				report.AppendLine("  renderTree.AnimationTick=" + getReflectedValue(render_tree, "AnimationTick"));
				report.AppendLine("  renderTree.AnimationFinished=" + getReflectedValue(render_tree, "AnimationFinished"));
				appendObjectAnimationFields(report, renderer, "renderer");
				appendObjectAnimationFields(report, render_tree, "renderTree");
				Log.Message(report.ToString().TrimEndNewlines());
			}
			catch (Exception exception)
			{
				Log.Warning("[RealRim] Water & Pumps: pool-swimming pawn runtime diagnostics failed: " + exception);
			}
		}

		private static void appendObjectAnimationFields(StringBuilder report, object instance, string label)
		{
			if (instance == null)
			{
				return;
			}
			FieldInfo[] fields = instance.GetType().GetFields(INSTANCE_FLAGS);
			for (int index = 0; index < fields.Length; index++)
			{
				FieldInfo field = fields[index];
				string field_name = field.Name.ToLowerInvariant();
				if (!field_name.Contains("anim") && !field_name.Contains("pose") && !field_name.Contains("swim"))
				{
					continue;
				}
				string value;
				try
				{
					object field_value = field.GetValue(instance);
					AnimationDef animation = field_value as AnimationDef;
					value = animation == null
						? (field_value == null ? "<null>" : field_value.ToString())
						: animation.defName;
				}
				catch (Exception exception)
				{
					value = "<error " + exception.GetType().Name + ">";
				}
				report.AppendLine("  " + label + "." + field.Name + "=" + value
					+ " [" + field.FieldType.FullName + "]");
			}
		}

		private static void logSwimmingDiagnostics()
		{
			if (swimming_diagnostics_logged || !ModsConfig.OdysseyActive)
			{
				return;
			}
			swimming_diagnostics_logged = true;
			try
			{
				StringBuilder report = new StringBuilder();
				report.AppendLine("[RealRim] Water & Pumps 1.1.35 swimming diagnostics BEGIN");
				report.AppendLine("Odyssey active: " + ModsConfig.OdysseyActive);
				appendAnimationDefinitions(report);
				appendAnimationDefOfFields(report);
				appendSwimmingJobDefinitions(report);
				appendSwimmingRuntimeTypes(report);
				appendVanillaSwimmingMethod(report);
				report.AppendLine("[RealRim] Water & Pumps 1.1.35 swimming diagnostics END");
				logDiagnosticChunks(report.ToString());
			}
			catch (Exception exception)
			{
				Log.Warning("[RealRim] Water & Pumps: swimming diagnostics failed: " + exception);
			}
		}

		private static void appendAnimationDefinitions(StringBuilder report)
		{
			List<AnimationDef> animations = DefDatabase<AnimationDef>.AllDefsListForReading;
			report.AppendLine("AnimationDef count: " + (animations == null ? 0 : animations.Count));
			if (animations == null)
			{
				return;
			}
			for (int index = 0; index < animations.Count; index++)
			{
				AnimationDef animation = animations[index];
				if (animation == null)
				{
					report.AppendLine("  [" + index + "] <null>");
					continue;
				}
				string duration = getReflectedValue(animation, "durationTicks");
				string worker = getReflectedValue(animation, "workerClass");
				if (worker == "<missing>")
				{
					worker = getReflectedValue(animation, "animationWorkerClass");
				}
				report.AppendLine("  [" + index + "] defName=" + animation.defName
					+ "; label=" + animation.label
					+ "; durationTicks=" + duration
					+ "; worker=" + worker
					+ "; type=" + animation.GetType().FullName);
			}
		}

		private static void appendAnimationDefOfFields(StringBuilder report)
		{
			Type animation_def_of = findType("RimWorld.AnimationDefOf");
			report.AppendLine("AnimationDefOf type: " + (animation_def_of == null ? "<not found>" : animation_def_of.AssemblyQualifiedName));
			if (animation_def_of == null)
			{
				return;
			}
			FieldInfo[] fields = animation_def_of.GetFields(STATIC_FLAGS);
			for (int index = 0; index < fields.Length; index++)
			{
				FieldInfo field = fields[index];
				object value = null;
				string error = null;
				try
				{
					value = field.GetValue(null);
				}
				catch (Exception exception)
				{
					error = exception.GetType().Name + ": " + exception.Message;
				}
				AnimationDef animation = value as AnimationDef;
				report.AppendLine("  field " + field.Name
					+ ": fieldType=" + field.FieldType.FullName
					+ "; valueType=" + (value == null ? "<null>" : value.GetType().FullName)
					+ "; animationDef=" + (animation == null ? "<none>" : animation.defName)
					+ (error == null ? string.Empty : "; error=" + error));
			}
		}

		private static void appendSwimmingJobDefinitions(StringBuilder report)
		{
			List<JobDef> jobs = DefDatabase<JobDef>.AllDefsListForReading;
			report.AppendLine("Relevant JobDefs:");
			if (jobs == null)
			{
				report.AppendLine("  <none>");
				return;
			}
			int matches = 0;
			for (int index = 0; index < jobs.Count; index++)
			{
				JobDef job = jobs[index];
				if (job == null)
				{
					continue;
				}
				Type driver_class = getJobDriverClass(job);
				string searchable = ((job.defName ?? string.Empty) + " "
					+ (job.label ?? string.Empty) + " "
					+ (driver_class == null ? string.Empty : driver_class.FullName)).ToLowerInvariant();
				if (!searchable.Contains("swim")
					&& !searchable.Contains("pool")
					&& !searchable.Contains("bath")
					&& !searchable.Contains("water"))
				{
					continue;
				}
				matches++;
				report.AppendLine("  defName=" + job.defName
					+ "; label=" + job.label
					+ "; driverClass=" + (driver_class == null ? "<null>" : driver_class.AssemblyQualifiedName));
			}
			if (matches == 0)
			{
				report.AppendLine("  <no matching JobDefs>");
			}
		}

		private static void appendSwimmingRuntimeTypes(StringBuilder report)
		{
			report.AppendLine("Runtime types containing swim/pool/bath and deriving from JobDriver, or otherwise animation-related:");
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			int matches = 0;
			for (int assembly_index = 0; assembly_index < assemblies.Length; assembly_index++)
			{
				Type[] types;
				try
				{
					types = assemblies[assembly_index].GetTypes();
				}
				catch (ReflectionTypeLoadException exception)
				{
					types = exception.Types;
				}
				catch
				{
					continue;
				}
				if (types == null)
				{
					continue;
				}
				for (int type_index = 0; type_index < types.Length; type_index++)
				{
					Type type = types[type_index];
					if (type == null)
					{
						continue;
					}
					string name = (type.FullName ?? type.Name ?? string.Empty).ToLowerInvariant();
					bool relevant_name = name.Contains("swim") || name.Contains("pool") || name.Contains("bath");
					bool job_driver = typeof(JobDriver).IsAssignableFrom(type);
					bool animation_related = name.Contains("animation") && name.Contains("pawn");
					if ((!relevant_name || !job_driver) && !animation_related)
					{
						continue;
					}
					matches++;
					report.AppendLine("  type=" + type.AssemblyQualifiedName
						+ "; base=" + (type.BaseType == null ? "<null>" : type.BaseType.FullName));
					MethodInfo[] methods;
					try
					{
						methods = type.GetMethods(INSTANCE_FLAGS | STATIC_FLAGS | BindingFlags.DeclaredOnly);
					}
					catch
					{
						continue;
					}
					for (int method_index = 0; method_index < methods.Length; method_index++)
					{
						MethodInfo method = methods[method_index];
						report.AppendLine("    method " + describeMethod(method));
					}
				}
			}
			if (matches == 0)
			{
				report.AppendLine("  <no matching runtime types>");
			}
		}

		private static void appendVanillaSwimmingMethod(StringBuilder report)
		{
			Type swimming_driver_type = findType("RimWorld.JobDriver_GoSwimming");
			report.AppendLine("Vanilla swimming driver: "
				+ (swimming_driver_type == null ? "<not found>" : swimming_driver_type.AssemblyQualifiedName));
			if (swimming_driver_type == null)
			{
				return;
			}
			FieldInfo[] fields = swimming_driver_type.GetFields(INSTANCE_FLAGS | STATIC_FLAGS);
			for (int index = 0; index < fields.Length; index++)
			{
				FieldInfo field = fields[index];
				report.AppendLine("  field " + field.Name + ": " + field.FieldType.FullName
					+ "; static=" + field.IsStatic);
			}
			MethodInfo[] methods = swimming_driver_type.GetMethods(INSTANCE_FLAGS | STATIC_FLAGS | BindingFlags.DeclaredOnly);
			for (int index = 0; index < methods.Length; index++)
			{
				MethodInfo method = methods[index];
				report.AppendLine("  method " + describeMethod(method));
				if (method.Name == "CheckForSwimmingPose")
				{
					report.AppendLine("    IL: " + getMethodIlHex(method));
				}
			}
			Type renderer_type = findType("Verse.PawnRenderer");
			appendAnimationMembers(report, renderer_type, "PawnRenderer animation members");
			Type render_tree_type = findType("Verse.PawnRenderTree");
			appendAnimationMembers(report, render_tree_type, "PawnRenderTree animation members");
		}

		private static void appendAnimationMembers(StringBuilder report, Type type, string heading)
		{
			report.AppendLine(heading + ": " + (type == null ? "<not found>" : type.AssemblyQualifiedName));
			if (type == null)
			{
				return;
			}
			MethodInfo[] methods = type.GetMethods(INSTANCE_FLAGS | STATIC_FLAGS);
			for (int index = 0; index < methods.Length; index++)
			{
				MethodInfo method = methods[index];
				string name = method.Name.ToLowerInvariant();
				if (name.Contains("anim") || name.Contains("swim") || name.Contains("pose"))
				{
					report.AppendLine("  method " + describeMethod(method));
				}
			}
			FieldInfo[] fields = type.GetFields(INSTANCE_FLAGS | STATIC_FLAGS);
			for (int index = 0; index < fields.Length; index++)
			{
				FieldInfo field = fields[index];
				string name = field.Name.ToLowerInvariant();
				if (name.Contains("anim") || name.Contains("swim") || name.Contains("pose"))
				{
					report.AppendLine("  field " + field.Name + ": " + field.FieldType.FullName);
				}
			}
		}

		private static Type getJobDriverClass(JobDef job)
		{
			if (job == null)
			{
				return null;
			}
			FieldInfo field = getField(job.GetType(), "driverClass");
			return field == null ? null : field.GetValue(job) as Type;
		}

		private static string getReflectedValue(object instance, string member_name)
		{
			if (instance == null)
			{
				return "<null>";
			}
			FieldInfo field = getField(instance.GetType(), member_name);
			if (field != null)
			{
				try
				{
					object value = field.GetValue(instance);
					return value == null ? "<null>" : value.ToString();
				}
				catch (Exception exception)
				{
					return "<error " + exception.GetType().Name + ">";
				}
			}
			PropertyInfo property = instance.GetType().GetProperty(member_name, INSTANCE_FLAGS);
			if (property != null)
			{
				try
				{
					object value = property.GetValue(instance, null);
					return value == null ? "<null>" : value.ToString();
				}
				catch (Exception exception)
				{
					return "<error " + exception.GetType().Name + ">";
				}
			}
			return "<missing>";
		}

		private static string describeMethod(MethodInfo method)
		{
			if (method == null)
			{
				return "<null>";
			}
			StringBuilder description = new StringBuilder();
			description.Append(method.ReturnType == null ? "void" : method.ReturnType.FullName);
			description.Append(" ");
			description.Append(method.Name);
			description.Append("(");
			ParameterInfo[] parameters = method.GetParameters();
			for (int index = 0; index < parameters.Length; index++)
			{
				if (index > 0)
				{
					description.Append(", ");
				}
				description.Append(parameters[index].ParameterType.FullName);
				description.Append(" ");
				description.Append(parameters[index].Name);
			}
			description.Append(")");
			return description.ToString();
		}

		private static string getMethodIlHex(MethodInfo method)
		{
			if (method == null)
			{
				return "<method missing>";
			}
			try
			{
				byte[] il = method.GetMethodBody()?.GetILAsByteArray();
				if (il == null)
				{
					return "<no method body>";
				}
				StringBuilder result = new StringBuilder(il.Length * 3);
				for (int index = 0; index < il.Length; index++)
				{
					if (index > 0)
					{
						result.Append(' ');
					}
					result.Append(il[index].ToString("X2"));
				}
				return result.ToString();
			}
			catch (Exception exception)
			{
				return "<error " + exception.GetType().Name + ": " + exception.Message + ">";
			}
		}

		private static void logDiagnosticChunks(string text)
		{
			const int MAX_CHUNK_LENGTH = 12000;
			if (text.NullOrEmpty())
			{
				return;
			}
			int offset = 0;
			int chunk_index = 1;
			while (offset < text.Length)
			{
				int length = Mathf.Min(MAX_CHUNK_LENGTH, text.Length - offset);
				if (offset + length < text.Length)
				{
					int newline = text.LastIndexOf('\n', offset + length - 1, length);
					if (newline > offset)
					{
						length = newline - offset + 1;
					}
				}
				Log.Message("[RealRim] Swimming diagnostics chunk " + chunk_index + ":\n" + text.Substring(offset, length));
				offset += length;
				chunk_index++;
			}
		}

		private static bool tryGetDriverContext(object driver_instance, out Pawn pawn, out Job job)
		{
			pawn = null;
			job = null;
			if (driver_instance == null)
			{
				return false;
			}
			FieldInfo pawn_field = getField(driver_instance.GetType(), "pawn");
			FieldInfo job_field = getField(driver_instance.GetType(), "job");
			pawn = pawn_field == null ? null : pawn_field.GetValue(driver_instance) as Pawn;
			job = job_field == null ? null : job_field.GetValue(driver_instance) as Job;
			return pawn != null && job != null;
		}

		private static void endDriverJob(object driver_instance, JobCondition condition)
		{
			JobDriver driver = driver_instance as JobDriver;
			if (driver != null)
			{
				driver.EndJobWith(condition);
			}
		}

		private static FluidNetwork getLegacyNetwork(object plumbing_net, FluidNetworkType network_type)
		{
			if (plumbing_net == null)
			{
				return null;
			}
			FieldInfo field = getField(plumbing_net.GetType(), "PipedThings");
			IEnumerable items = field == null ? null : field.GetValue(plumbing_net) as IEnumerable;
			if (items == null)
			{
				return null;
			}
			foreach (object item in items)
			{
				Thing thing = item as Thing;
				ThingComp comp = item as ThingComp;
				if (thing == null && comp != null)
				{
					thing = comp.parent;
				}
				FluidNetwork network = FluidUtility.getNetwork(thing, network_type);
				if (network != null)
				{
					return network;
				}
			}
			return null;
		}

		private static void addFloatField(object instance, string field_name, float amount)
		{
			FieldInfo field = getField(instance.GetType(), field_name);
			if (field != null && field.FieldType == typeof(float))
			{
				field.SetValue(instance, Convert.ToSingle(field.GetValue(instance)) + amount);
			}
		}

		private static void setField(object instance, string field_name, object value)
		{
			if (instance == null)
			{
				return;
			}
			FieldInfo field = getField(instance.GetType(), field_name);
			if (field != null)
			{
				field.SetValue(instance, value);
			}
		}

		private static FieldInfo getField(Type type, string field_name)
		{
			string key = type.FullName + ":" + field_name;
			FieldInfo field;
			if (!FIELD_CACHE.TryGetValue(key, out field))
			{
				Type current = type;
				while (current != null && field == null)
				{
					field = current.GetField(field_name, INSTANCE_FLAGS);
					current = current.BaseType;
				}
				FIELD_CACHE[key] = field;
			}
			return field;
		}

		private static MethodInfo getMethod(Type type, string method_name, Type[] parameter_types)
		{
			string key = type.FullName + ":" + method_name + ":" + parameter_types.Length;
			MethodInfo method;
			if (!METHOD_CACHE.TryGetValue(key, out method))
			{
				Type current = type;
				while (current != null && method == null)
				{
					method = current.GetMethod(method_name, INSTANCE_FLAGS, null, parameter_types, null);
					current = current.BaseType;
				}
				METHOD_CACHE[key] = method;
			}
			return method;
		}

		private static bool tryCreateHarmony(out object harmony, out MethodInfo patch_method, out Type harmony_method_type)
		{
			harmony = null;
			patch_method = null;
			Type harmony_type = findType("HarmonyLib.Harmony");
			harmony_method_type = findType("HarmonyLib.HarmonyMethod");
			if (harmony_type == null || harmony_method_type == null)
			{
				return false;
			}
			harmony = Activator.CreateInstance(harmony_type, new object[] { HARMONY_ID });
			MethodInfo[] methods = harmony_type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
			for (int index = 0; index < methods.Length; index++)
			{
				if (methods[index].Name == "Patch" && methods[index].GetParameters().Length == 5)
				{
					patch_method = methods[index];
					break;
				}
			}
			return harmony != null && patch_method != null;
		}

		private static int patchAllNamedPostfix(object harmony, MethodInfo patch_method, Type harmony_method_type, string type_name, string method_name, string postfix_name)
		{
			Type type = findType(type_name);
			MethodInfo postfix = typeof(DbhRuntimeBridge).GetMethod(postfix_name, STATIC_FLAGS);
			if (type == null || postfix == null)
			{
				Log.Warning("[RealRim] Water & Pumps: postfix target not found: " + type_name + "." + method_name);
				return 0;
			}
			int count = 0;
			MethodInfo[] methods = type.GetMethods(INSTANCE_FLAGS);
			for (int index = 0; index < methods.Length; index++)
			{
				MethodInfo original = methods[index];
				if (original.Name != method_name || original.IsAbstract)
				{
					continue;
				}
				object harmony_postfix = Activator.CreateInstance(harmony_method_type, new object[] { postfix });
				patch_method.Invoke(harmony, new object[] { original, null, harmony_postfix, null, null });
				count++;
			}
			if (count == 0)
			{
				Log.Warning("[RealRim] Water & Pumps: no overload matched postfix target: " + type_name + "." + method_name);
			}
			return count;
		}

		private static int patchAllNamed(object harmony, MethodInfo patch_method, Type harmony_method_type, string type_name, string method_name, string prefix_name)
		{
			Type type = findType(type_name);
			MethodInfo prefix = typeof(DbhRuntimeBridge).GetMethod(prefix_name, STATIC_FLAGS);
			if (type == null || prefix == null)
			{
				Log.Warning("[RealRim] Water & Pumps: patch target not found: " + type_name + "." + method_name);
				return 0;
			}
			int count = 0;
			MethodInfo[] methods = type.GetMethods(INSTANCE_FLAGS);
			for (int index = 0; index < methods.Length; index++)
			{
				MethodInfo original = methods[index];
				if (original.Name != method_name || original.IsAbstract)
				{
					continue;
				}
				object harmony_prefix = Activator.CreateInstance(harmony_method_type, new object[] { prefix });
				patch_method.Invoke(harmony, new object[] { original, harmony_prefix, null, null, null });
				count++;
			}
			if (count == 0)
			{
				Log.Warning("[RealRim] Water & Pumps: no overload matched patch target: " + type_name + "." + method_name);
			}
			return count;
		}

		private static Type findType(string full_name)
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int index = 0; index < assemblies.Length; index++)
			{
				Type type = assemblies[index].GetType(full_name, false);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}
	}
}
