using System;
using System.Reflection;
using System.Collections.Generic;
using Verse;

namespace RealRim.WaterAndPumps
{
	internal static class DbhIrrigationBridge
	{
		private const BindingFlags INSTANCE_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const string MAP_COMPONENT_TYPE_NAME = "DubsBadHygiene.MapComponent_Hygiene";
		private const string IRRIGATION_GRID_FIELD_NAME = "IrrigationGrid";
		private const string ADD_METHOD_NAME = "AddAtCapped";
		private const string DRAW_METHOD_NAME = "MarkForDraw";

		private static Type map_component_type;
		private static FieldInfo irrigation_grid_field;
		private static MethodInfo add_at_capped_method;
		private static MethodInfo mark_for_draw_method;
		private static bool resolved;

		public static bool isAvailable(Map map)
		{
			return getGrid(map) != null && add_at_capped_method != null;
		}

		public static int addIrrigation(Map map, List<IntVec3> cells, float amount)
		{
			object grid = getGrid(map);
			if (grid == null || add_at_capped_method == null || cells.NullOrEmpty())
			{
				return 0;
			}

			object[] arguments =
			{
				default(IntVec3),
				amount,
				100f,
				true,
				false,
				null,
			};
			int applied_cells = 0;
			try
			{
				for (; applied_cells < cells.Count; applied_cells++)
				{
					arguments[0] = cells[applied_cells];
					add_at_capped_method.Invoke(grid, arguments);
				}
			}
			catch (Exception exception)
			{
				Log.ErrorOnce(
					"[RealRim] Water & Pumps: failed to update DBH's irrigation grid: " + exception,
					77160421);
			}
			return applied_cells;
		}

		public static void markForDraw(Map map)
		{
			object grid = getGrid(map);
			if (grid == null || mark_for_draw_method == null)
			{
				return;
			}

			try
			{
				mark_for_draw_method.Invoke(grid, null);
			}
			catch (Exception exception)
			{
				Log.WarningOnce(
					"[RealRim] Water & Pumps: failed to display DBH's irrigation overlay: " + exception.Message,
					77160422);
			}
		}

		private static object getGrid(Map map)
		{
			resolve();
			if (map == null || map_component_type == null || irrigation_grid_field == null)
			{
				return null;
			}

			try
			{
				MapComponent component = map.GetComponent(map_component_type);
				return component == null ? null : irrigation_grid_field.GetValue(component);
			}
			catch (Exception exception)
			{
				Log.ErrorOnce(
					"[RealRim] Water & Pumps: failed to access DBH's irrigation grid: " + exception,
					77160423);
				return null;
			}
		}

		private static void resolve()
		{
			if (resolved)
			{
				return;
			}
			resolved = true;

			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int index = 0; index < assemblies.Length && map_component_type == null; index++)
			{
				map_component_type = assemblies[index].GetType(MAP_COMPONENT_TYPE_NAME, false);
			}
			if (map_component_type == null)
			{
				return;
			}

			irrigation_grid_field = map_component_type.GetField(IRRIGATION_GRID_FIELD_NAME, INSTANCE_FLAGS);
			Type grid_type = irrigation_grid_field?.FieldType;
			if (grid_type == null)
			{
				return;
			}

			MethodInfo[] methods = grid_type.GetMethods(INSTANCE_FLAGS);
			for (int index = 0; index < methods.Length; index++)
			{
				MethodInfo method = methods[index];
				ParameterInfo[] parameters = method.GetParameters();
				if (method.Name == ADD_METHOD_NAME
					&& parameters.Length == 6
					&& parameters[0].ParameterType == typeof(IntVec3)
					&& parameters[1].ParameterType == typeof(float)
					&& parameters[2].ParameterType == typeof(float)
					&& parameters[3].ParameterType == typeof(bool)
					&& parameters[4].ParameterType == typeof(bool)
					&& parameters[5].ParameterType == typeof(RimWorld.MapMeshFlagDef))
				{
					add_at_capped_method = method;
				}
				else if (method.Name == DRAW_METHOD_NAME && parameters.Length == 0)
				{
					mark_for_draw_method = method;
				}
			}
		}
	}
}
