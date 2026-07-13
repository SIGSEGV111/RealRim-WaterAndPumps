using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	[StaticConstructorOnStartup]
	internal static class RimefellerIntegration
	{
		private const string HARMONY_ID = "sigsegv11.realrim.water.rimefeller-integration";
		private const float RIMEFELLER_WATER_TICKS_PER_DAY = 60000f;
		private const float WATER_EPSILON_LITERS = 0.0001f;
		private const BindingFlags INSTANCE_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags STATIC_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private static readonly Dictionary<string, FieldInfo> FIELD_CACHE = new Dictionary<string, FieldInfo>();
		private static readonly Dictionary<string, MethodInfo> METHOD_CACHE = new Dictionary<string, MethodInfo>();

		static RimefellerIntegration()
		{
			LongEventHandler.ExecuteWhenFinished(applyPatches);
		}

		private static void applyPatches()
		{
			try
			{
				Type refinery_type = findType("Rimefeller.CompRefinery");
				Type cracker_type = findType("Rimefeller.CompCrudeCracker");
				if (refinery_type == null && cracker_type == null)
				{
					return;
				}

				object harmony;
				MethodInfo patch_method;
				Type harmony_method_type;
				if (!tryCreateHarmony(out harmony, out patch_method, out harmony_method_type))
				{
					Log.Warning("[RealRim] Water & Pumps: Rimefeller integration skipped because Harmony API was not available.");
					return;
				}

				int patched_methods = 0;
				patched_methods += patchRimefellerWaterConsumer(harmony, patch_method, harmony_method_type, refinery_type);
				patched_methods += patchRimefellerWaterConsumer(harmony, patch_method, harmony_method_type, cracker_type);
				if (patched_methods > 0)
				{
					Log.Message("[RealRim] Water & Pumps 1.1.73: patched " + patched_methods
						+ " Rimefeller water-consumer method(s) to use RealRim fresh-water networks.");
				}
			}
			catch (Exception exception)
			{
				Log.Error("[RealRim] Water & Pumps: Rimefeller integration failed: " + exception);
			}
		}

		private static int patchRimefellerWaterConsumer(
			object harmony,
			MethodInfo patch_method,
			Type harmony_method_type,
			Type type)
		{
			if (type == null)
			{
				return 0;
			}

			int patched_methods = 0;
			patched_methods += patchMethod(
				harmony,
				patch_method,
				harmony_method_type,
				type,
				"get_WorkingNow",
				null,
				nameof(workingNowPostfix));
			patched_methods += patchMethod(
				harmony,
				patch_method,
				harmony_method_type,
				type,
				"CompTick",
				nameof(compTickPrefix),
				nameof(compTickPostfix));
			patched_methods += patchMethod(
				harmony,
				patch_method,
				harmony_method_type,
				type,
				"CompInspectStringExtra",
				null,
				nameof(inspectStringPostfix));
			return patched_methods;
		}

		private static void workingNowPostfix(object __instance, ref bool __result)
		{
			if (__result || !hasRimefellerWaterRefuelable(__instance) || !basicCanRun(__instance))
			{
				return;
			}

			Thing thing = getParentThing(__instance);
			float requested_liters = getRequestedLitersPerTick(__instance);
			if (RealRimWaterApi.HasFreshWater(thing, requested_liters))
			{
				__result = true;
			}
		}

		private static void compTickPrefix(object __instance, ref RimefellerWaterTickState __state)
		{
			__state = null;
			if (!hasRimefellerWaterRefuelable(__instance) || !basicCanRun(__instance))
			{
				return;
			}

			Thing thing = getParentThing(__instance);
			float requested_liters = getRequestedLitersPerTick(__instance);
			if (!RealRimWaterApi.HasFreshWater(thing, requested_liters))
			{
				return;
			}

			FieldInfo fuel_field = getField(__instance.GetType(), "FuelComp");
			if (fuel_field == null)
			{
				return;
			}

			__state = new RimefellerWaterTickState
			{
				fuel_field = fuel_field,
				fuel_comp = fuel_field.GetValue(__instance),
				requested_liters = requested_liters,
			};
			fuel_field.SetValue(__instance, null);
		}

		private static void compTickPostfix(object __instance, RimefellerWaterTickState __state)
		{
			if (__state == null)
			{
				return;
			}

			try
			{
				__state.fuel_field.SetValue(__instance, __state.fuel_comp);
				if (basicCanRun(__instance) && invokeBool(__instance, "get_HighPowerMode"))
				{
					RealRimWaterApi.DrawFreshWater(getParentThing(__instance), __state.requested_liters);
				}
			}
			catch (Exception exception)
			{
				Log.Error("[RealRim] Water & Pumps: failed to finish Rimefeller water tick: " + exception);
			}
		}

		private static void inspectStringPostfix(object __instance, ref string __result)
		{
			if (!hasRimefellerWaterRefuelable(__instance))
			{
				return;
			}

			Thing thing = getParentThing(__instance);
			float stored_liters = RealRimWaterApi.GetFreshWaterStored(thing);
			float capacity_liters = RealRimWaterApi.GetFreshWaterCapacity(thing);
			float usage_per_day = getWaterUsagePerDay(__instance);
			string line = "RealRim_RimefellerFreshWaterStatus".Translate(
				stored_liters.ToString("N1"),
				capacity_liters.ToString("N0"),
				usage_per_day.ToString("N1"));
			__result = string.IsNullOrEmpty(__result) ? line : __result + "\n" + line;
		}

		private static bool hasRimefellerWaterRefuelable(object instance)
		{
			FieldInfo fuel_field = instance == null ? null : getField(instance.GetType(), "FuelComp");
			return fuel_field != null && fuel_field.GetValue(instance) != null;
		}

		private static bool basicCanRun(object instance)
		{
			ThingWithComps thing = getParentThing(instance) as ThingWithComps;
			return thing != null && FluidUtility.isPoweredOn(thing);
		}

		private static float getRequestedLitersPerTick(object instance)
		{
			return Mathf.Max(0f, getWaterUsagePerDay(instance) / RIMEFELLER_WATER_TICKS_PER_DAY);
		}

		private static float getWaterUsagePerDay(object instance)
		{
			MethodInfo method = instance == null ? null : getMethod(instance.GetType(), "get_WaterUsage");
			if (method == null)
			{
				return 0f;
			}

			object value = method.Invoke(instance, null);
			return value is float ? (float)value : 0f;
		}

		private static bool invokeBool(object instance, string method_name)
		{
			MethodInfo method = instance == null ? null : getMethod(instance.GetType(), method_name);
			if (method == null)
			{
				return false;
			}

			object value = method.Invoke(instance, null);
			return value is bool && (bool)value;
		}

		private static Thing getParentThing(object instance)
		{
			ThingComp comp = instance as ThingComp;
			return comp?.parent;
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

		private static MethodInfo getMethod(Type type, string method_name)
		{
			string key = type.FullName + ":" + method_name;
			MethodInfo method;
			if (!METHOD_CACHE.TryGetValue(key, out method))
			{
				Type current = type;
				while (current != null && method == null)
				{
					method = current.GetMethod(method_name, INSTANCE_FLAGS, null, Type.EmptyTypes, null);
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

		private static int patchMethod(
			object harmony,
			MethodInfo patch_method,
			Type harmony_method_type,
			Type type,
			string method_name,
			string prefix_name,
			string postfix_name)
		{
			MethodInfo original = getMethod(type, method_name);
			MethodInfo prefix = prefix_name == null ? null : typeof(RimefellerIntegration).GetMethod(prefix_name, STATIC_FLAGS);
			MethodInfo postfix = postfix_name == null ? null : typeof(RimefellerIntegration).GetMethod(postfix_name, STATIC_FLAGS);
			if (original == null || (prefix == null && postfix == null))
			{
				Log.Warning("[RealRim] Water & Pumps: Rimefeller patch target not found: "
					+ type.FullName + "." + method_name);
				return 0;
			}

			object harmony_prefix = prefix == null
				? null
				: Activator.CreateInstance(harmony_method_type, new object[] { prefix });
			object harmony_postfix = postfix == null
				? null
				: Activator.CreateInstance(harmony_method_type, new object[] { postfix });
			patch_method.Invoke(harmony, new object[] { original, harmony_prefix, harmony_postfix, null, null });
			return 1;
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

		private sealed class RimefellerWaterTickState
		{
			public FieldInfo fuel_field;
			public object fuel_comp;
			public float requested_liters;
		}
	}
}
