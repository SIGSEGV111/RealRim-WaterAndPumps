using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public static class FluidUtility
	{
		private const BindingFlags STATIC_FLAGS = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private static readonly Type GRAV_ENGINE_TYPE = findType("RimWorld.Building_GravEngine");
		private static readonly MethodInfo IS_ONBOARD_GRAVSHIP_METHOD = findType("RimWorld.GravshipUtility")
			?.GetMethod("IsOnboardGravship", STATIC_FLAGS);

		public static FluidNetwork getNetwork(Thing thing, FluidNetworkType network_type)
		{
			CompFluidNode node = thing?.TryGetComp<CompFluidNode>();
			return node?.getManager()?.getNetwork(node, network_type);
		}

		public static float addHeatingEnergy(Thing source, float requested_kj)
		{
			FluidNetwork network = getNetwork(source, FluidNetworkType.Heating);
			return network == null ? 0f : network.addThermalEnergy(requested_kj);
		}

		public static string getNetworkLabel(FluidNetworkType network_type)
		{
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					return "RealRim_Fluid_FreshWater".Translate();
				case FluidNetworkType.HotWater:
					return "RealRim_Fluid_HotWater".Translate();
				case FluidNetworkType.Heating:
					return "RealRim_Fluid_Heating".Translate();
				case FluidNetworkType.WasteWater:
					return "RealRim_Fluid_WasteWater".Translate();
				case FluidNetworkType.Coolant:
					return "RealRim_Fluid_Coolant".Translate();
				default:
					return network_type.ToString();
			}
		}

		public static bool isPoweredOn(Thing thing)
		{
			CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
			CompFlickable flickable = thing.TryGetComp<CompFlickable>();
			CompBreakdownable breakdownable = thing.TryGetComp<CompBreakdownable>();
			return (power == null || power.PowerOn)
				&& (flickable == null || flickable.SwitchIsOn)
				&& (breakdownable == null || !breakdownable.BrokenDown);
		}

		public static void setPowerConsumption(Thing thing, float watts, bool active)
		{
			CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
			if (power != null)
			{
				power.PowerOutput = active ? -Mathf.Abs(watts) : 0f;
			}
		}

		public static bool consumeFuel(Thing thing, float fuel_units)
		{
			CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
			if (refuelable == null)
			{
				return fuel_units <= 0f;
			}

			if (!refuelable.HasFuel || refuelable.Fuel + 0.0001f < fuel_units)
			{
				return false;
			}

			refuelable.ConsumeFuel(fuel_units);
			return true;
		}

		public static Pawn findUsingPawn(Thing target)
		{
			Map map = target?.MapHeld;
			if (map == null)
			{
				return null;
			}

			IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
			for (int index = 0; index < pawns.Count; index++)
			{
				Pawn pawn = pawns[index];
				if (pawn.CurJob == null)
				{
					continue;
				}

				if (pawn.CurJob.targetA.Thing == target
					|| pawn.CurJob.targetB.Thing == target
					|| pawn.CurJob.targetC.Thing == target)
				{
					return pawn;
				}
			}

			return null;
		}

		public static bool isOnGravship(Thing thing)
		{
			if (thing?.MapHeld == null)
			{
				return false;
			}

			try
			{
				if (GRAV_ENGINE_TYPE == null || IS_ONBOARD_GRAVSHIP_METHOD == null)
				{
					return false;
				}

				List<Building> buildings = thing.MapHeld.listerBuildings.allBuildingsColonist;
				for (int index = 0; index < buildings.Count; index++)
				{
					Building building = buildings[index];
					if (!GRAV_ENGINE_TYPE.IsInstanceOfType(building))
					{
						continue;
					}

					object result = IS_ONBOARD_GRAVSHIP_METHOD.Invoke(null, new object[]
					{
						thing.PositionHeld,
						building,
						null,
						false,
					});
					if (result is bool && (bool)result)
					{
						return true;
					}
				}
			}
			catch (Exception exception)
			{
				Log.WarningOnce("[RealRim] Water & Pumps: Gravship detection failed: " + exception.Message, 8849213);
			}

			return false;
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
