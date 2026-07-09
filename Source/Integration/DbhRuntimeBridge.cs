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
		private const string BLADDER_NEED_DEF = "Bladder";
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
		[ThreadStatic]
		private static int floor_heating_comfort_depth;
		private static FieldInfo vanilla_swimming_job_field;
		private static bool vanilla_swimming_job_field_resolved;

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
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_takeShower+<>c__DisplayClass4_0", "<MakeNewToils>b__1", nameof(requiredNeedConditionPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_UseToilet", "<MakeNewToils>b__1_1", nameof(requiredNeedConditionPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_GoSwimming", "swimticker", nameof(swimTickerPostfix), true);
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_GoSwimming", "Finishac", nameof(poolSwimmingFinishPostfix), true);
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "Verse.AI.Toils_Recipe+<>c__DisplayClass2_0", "<DoRecipeWork>b__2", nameof(recipeWorkTickPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "RimWorld.StatWorker", "GetValue", nameof(floorHeatingComfortPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "RimWorld.StatWorker", "GetValue", nameof(floorHeatingComfortPostfix), true);

				Log.Message("[RealRim] Water & Pumps 1.1.50: redirected " + patched_methods
					+ " DBH runtime methods to RealRim physics.");
			}
			catch (Exception exception)
			{
				Log.Error("[RealRim] Water & Pumps: runtime bridge failed: " + exception);
			}
		}

		private static bool fixtureInspectStringPrefix(object __instance, ref string __result)
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
					if (comp == null)
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

		private static void floorHeatingComfortPrefix(object __instance)
		{
			if (isComfortStatWorker(__instance))
			{
				floor_heating_comfort_depth++;
			}
		}

		private static void floorHeatingComfortPostfix(object __instance, object[] __args, ref float __result)
		{
			bool is_comfort_stat = isComfortStatWorker(__instance);
			try
			{
				if (!is_comfort_stat || floor_heating_comfort_depth != 1 || __args == null || __args.Length == 0)
				{
					return;
				}

				Thing thing = __args[0] as Thing;
				if (thing == null && __args[0] is StatRequest)
				{
					thing = ((StatRequest)__args[0]).Thing;
				}

				float bonus = FloorHeatingUtility.getComfortBonusFor(thing, __result);
				if (bonus > 0f)
				{
					__result += bonus;
				}
			}
			catch (Exception exception)
			{
				Log.WarningOnce("[RealRim] Water & Pumps: floor-heating comfort bonus failed: "
					+ exception.Message, 11927462);
			}
			finally
			{
				if (is_comfort_stat && floor_heating_comfort_depth > 0)
				{
					floor_heating_comfort_depth--;
				}
			}
		}

		private static bool isComfortStatWorker(object stat_worker)
		{
			if (stat_worker == null)
			{
				return false;
			}

			FieldInfo stat_field = getField(stat_worker.GetType(), "stat");
			StatDef stat = stat_field == null ? null : stat_field.GetValue(stat_worker) as StatDef;
			return stat != null && stat.defName == "Comfort";
		}

		private static bool visiblePipeGraphicPrintPrefix(
			Graphic_Linked __instance,
			SectionLayer layer,
			Thing thing)
		{
			return !FluidNetworkVisuals.tryPrintVisiblePipe(thing, layer, __instance);
		}

		private static bool skipOriginal()
		{
			return false;
		}

		private static bool nullStringPrefix(ref string __result)
		{
			__result = null;
			return false;
		}

		private static bool emptyAlertPrefix(ref AlertReport __result)
		{
			__result = false;
			return false;
		}

		private static bool pullWaterPrefix(object __instance, object[] __args, MethodBase __originalMethod, ref bool __result)
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

		private static bool pushWaterPrefix(object __instance, object[] __args, ref float __result)
		{
			float requested = __args.Length == 0 ? 0f : Convert.ToSingle(__args[0]);
			FluidNetwork network = getLegacyNetwork(__instance, FluidNetworkType.FreshWater);
			__result = network == null ? requested : requested - network.addFreshWater(requested);
			return false;
		}

		private static bool pushSewagePrefix(object __instance, object[] __args, ref bool __result)
		{
			float requested = __args.Length == 0 ? 0f : Convert.ToSingle(__args[0]);
			FluidNetwork network = getLegacyNetwork(__instance, FluidNetworkType.WasteWater);
			__result = network != null && network.pushWaste(requested, 0f);
			return false;
		}

		private static bool pullHotWaterPrefix(object __instance, object[] __args, ref bool __result)
		{
			float requested = __args.Length == 0 ? 0f : Convert.ToSingle(__args[0]);
			FluidNetwork network = getLegacyNetwork(__instance, FluidNetworkType.HotWater);
			float delivered = network == null ? 0f : network.drawHotWater(requested);
			__result = delivered + 0.001f >= requested;
			return false;
		}

		private static bool fixtureWorkingPrefix(object __instance, ref AcceptanceReport __result)
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

		private static bool troughWorkingPrefix(object __instance, ref AcceptanceReport __result)
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

		private static bool latrineWorkingPrefix(object __instance, ref AcceptanceReport __result)
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

		private static bool showerTryUseWaterPrefix(object __instance, object[] __args, MethodBase __originalMethod, ref bool __result)
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

		private static bool bathTryFillPrefix(object __instance, ref bool __result)
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

		private static bool toiletTryUsePrefix(object __instance, ref bool __result)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			CompFixture fixture = thing == null ? null : thing.TryGetComp<CompFixture>();
			if (fixture == null)
			{
				return true;
			}
			__result = fixture.tryUse(FluidUtility.findUsingPawn(thing), out _);
			return false;
		}

		private static bool latrineTryUsePrefix(object __instance, ref bool __result)
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

		private static bool poolInspectStringPrefix(object __instance, ref string __result)
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
			__result = string.Join("\n", lines).TrimEnd('\r', '\n', ' ', '\t');
			return false;
		}

		private static bool waterFillableInspectPrefix(object __instance, ref string __result)
		{
			ThingComp component = __instance as ThingComp;
			if (component?.parent?.TryGetComp<CompLatrine>() == null)
			{
				return true;
			}
			__result = null;
			return false;
		}

		private static bool fillableThingTickPrefix(object __instance)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			return thing == null || thing.TryGetComp<CompPoolPhysics>() == null;
		}

		private static bool washBucketTickRarePrefix(object __instance)
		{
			ThingWithComps thing = __instance as ThingWithComps;
			return thing == null || thing.TryGetComp<CompWaterTrough>() == null;
		}

		private static bool poolWorkingPrefix(object __instance, ref AcceptanceReport __result)
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

		private static bool poolGoodTemperaturePrefix(object __instance, ref bool __result)
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

		private static bool swimmingJobPrefix(object __instance, object[] __args, ref Job __result)
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

		private static bool thirstFallPrefix(ref float __result)
		{
			__result = 0.5f / RealPhysics.TICKS_PER_DAY;
			return false;
		}

		private static bool drinkFromFixtureTickPrefix(object __instance)
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


		private static bool basinDrinkFinishedPrefix(object __instance)
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

		private static bool groundDrinkFinishedPrefix(object __instance)
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

		private static bool washHandsTickPrefix(object __instance)
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

			if (!fixture.tryUseVolume(
				pawn,
				HAND_WASH_LITERS_PER_TICK,
				HAND_WASH_LITERS_PER_TICK,
				0f,
				false,
				out _))
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

		private static bool requiredNeedConditionPrefix(object __instance, MethodBase __originalMethod, ref JobCondition __result)
		{
			string declaring_type_name = __originalMethod?.DeclaringType?.FullName ?? string.Empty;
			string need_def_name;
			if (declaring_type_name.Contains("JobDriver_takeShower"))
			{
				need_def_name = HYGIENE_NEED_DEF;
			}
			else if (declaring_type_name.Contains("JobDriver_UseToilet"))
			{
				need_def_name = BLADDER_NEED_DEF;
			}
			else
			{
				return true;
			}

			object driver_instance = __instance;
			if (!(driver_instance is JobDriver))
			{
				FieldInfo outer_driver_field = __instance == null
					? null
					: getField(__instance.GetType(), "<>4__this");
				driver_instance = outer_driver_field?.GetValue(__instance);
			}

			FieldInfo pawn_field = driver_instance == null
				? null
				: getField(driver_instance.GetType(), "pawn");
			Pawn pawn = pawn_field?.GetValue(driver_instance) as Pawn;
			NeedDef need_def = DefDatabase<NeedDef>.GetNamedSilentFail(need_def_name);
			Need need = pawn?.needs == null || need_def == null
				? null
				: pawn.needs.TryGetNeed(need_def);
			if (need != null)
			{
				return true;
			}

			__result = JobCondition.Incompletable;
			return false;
		}

		private static bool emptyLatrinePrefix(object __instance)
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

		private static bool recipeWorkTickPrefix(object __instance, int delta)
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
				kitchen_sink.recordLinkedStoveUse(work_table, delta * RealPhysics.SECONDS_PER_GAME_TICK);
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

		private static void swimTickerPostfix(object __instance)
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

		private static void poolSwimmingFinishPostfix(object __instance)
		{
			Job job;
			if (tryGetDriverContext(__instance, out _, out job))
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
				vanilla_swimming_job_field = null;
				Log.Error("[RealRim] Water & Pumps: failed to set Odyssey's swimming-pose job field: " + exception);
			}
		}

		private static bool resolveVanillaSwimmingJobField()
		{
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

		private static int patchAllNamed(
			object harmony,
			MethodInfo patch_method,
			Type harmony_method_type,
			string type_name,
			string method_name,
			string patch_name,
			bool postfix = false)
		{
			Type type = findType(type_name);
			MethodInfo patch = typeof(DbhRuntimeBridge).GetMethod(patch_name, STATIC_FLAGS);
			if (type == null || patch == null)
			{
				Log.Warning("[RealRim] Water & Pumps: " + (postfix ? "postfix" : "prefix")
					+ " target not found: " + type_name + "." + method_name);
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

				object harmony_patch = Activator.CreateInstance(harmony_method_type, new object[] { patch });
				patch_method.Invoke(harmony, postfix
					? new object[] { original, null, harmony_patch, null, null }
					: new object[] { original, harmony_patch, null, null, null });
				count++;
			}

			if (count == 0)
			{
				Log.Warning("[RealRim] Water & Pumps: no overload matched "
					+ (postfix ? "postfix" : "prefix") + " target: " + type_name + "." + method_name);
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
