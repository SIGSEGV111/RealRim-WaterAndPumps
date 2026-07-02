using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
		private const string SLUDGE_DEF_NAME = "FecalSludge";
		private const float DRINK_NEED_PER_TICK = 0.006f;
		private const float HAND_WASH_LITERS_PER_TICK = 0.04f;
		private const float HAND_WASH_HYGIENE_PER_TICK = 0.002f;
		private const float SLUDGE_KG_PER_ITEM = 0.05f;
		private const BindingFlags INSTANCE_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags STATIC_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private static readonly Dictionary<string, FieldInfo> FIELD_CACHE = new Dictionary<string, FieldInfo>();
		private static readonly Dictionary<string, MethodInfo> METHOD_CACHE = new Dictionary<string, MethodInfo>();

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
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "DubsBadHygiene.JobDriver_GoSwimming", "swimticker", nameof(swimTickerPrefix));
				patched_methods += patchAllNamed(harmony, patch_method, harmony_method_type, "Verse.AI.Toils_Recipe+<>c__DisplayClass2_0", "<DoRecipeWork>b__2", nameof(recipeWorkTickPrefix));

				Log.Message("[RealRim] Water & Pumps 1.1.7: redirected " + patched_methods
					+ " DBH runtime methods to RealRim physics.");
			}
			catch (Exception exception)
			{
				Log.Error("[RealRim] Water & Pumps: runtime bridge failed: " + exception);
			}
		}

		public static bool skipOriginal()
		{
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
			CompFixture kitchen_sink = findKitchenSink(work_table);
			if (kitchen_sink != null)
			{
				kitchen_sink.recordLinkedStoveUse(delta * RealPhysics.SECONDS_PER_GAME_TICK);
			}
			return true;
		}

		private static CompFixture findKitchenSink(Building_WorkTable work_table)
		{
			if (work_table?.Map == null)
			{
				return null;
			}

			CompAffectedByFacilities affected = work_table.TryGetComp<CompAffectedByFacilities>();
			List<Thing> linked = affected?.LinkedFacilitiesListForReading;
			if (linked != null)
			{
				for (int index = 0; index < linked.Count; index++)
				{
					CompFixture fixture = (linked[index] as ThingWithComps)?.TryGetComp<CompFixture>();
					if (fixture != null && fixture.Props.kind == FixtureKind.KitchenSink)
					{
						return fixture;
					}
				}
			}

			ThingDef sink_def = DefDatabase<ThingDef>.GetNamedSilentFail("KitchenSink");
			if (sink_def == null)
			{
				return null;
			}
			Room work_room = work_table.Position.GetRoom(work_table.Map);
			Thing nearest = null;
			float nearest_distance = 12.01f;
			List<Thing> sinks = work_table.Map.listerThings.ThingsOfDef(sink_def);
			for (int index = 0; index < sinks.Count; index++)
			{
				Thing candidate = sinks[index];
				if (candidate.Position.GetRoom(work_table.Map) != work_room)
				{
					continue;
				}
				float distance = candidate.Position.DistanceTo(work_table.Position);
				if (distance < nearest_distance)
				{
					nearest_distance = distance;
					nearest = candidate;
				}
			}
			return (nearest as ThingWithComps)?.TryGetComp<CompFixture>();
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

		public static bool swimTickerPrefix(object __instance)
		{
			Pawn pawn;
			Job job;
			if (!tryGetDriverContext(__instance, out pawn, out job))
			{
				return true;
			}
			CompPoolPhysics pool = (job.targetA.Thing as ThingWithComps)?.TryGetComp<CompPoolPhysics>();
			pool?.applyPawnTemperatureThought(pawn);
			return true;
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
