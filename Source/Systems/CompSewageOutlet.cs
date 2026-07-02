using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public class CompProperties_SewageOutlet : CompProperties
	{
		public float harmless_water_liters_per_filth = 20f;
		public float untreated_water_liters_per_filth = 20f;
		public float untreated_sludge_kg_per_filth = 0.225f;

		public CompProperties_SewageOutlet()
		{
			compClass = typeof(CompSewageOutlet);
		}
	}

	public class CompSewageOutlet : ThingComp, IFluidTickable
	{
		protected enum OperationalState
		{
			Operational,
			NoMap,
			OutsideMap,
			Vacuum,
			NoNaturalSoil,
			NotPressurized,
			NoPower,
			SwitchedOff,
		}

		private const int MAX_FILTH_PER_FLUSH = 8;
		protected const float MAX_OPERATIONAL_VACUUM = 0.001f;
		protected const string HARMLESS_FILTH_DEF = "RealRim_WaterDirt";
		protected const string UNTREATED_FILTH_DEF = "RawSewage";

		public float total_harmless_water_liters;
		public float total_untreated_water_liters;
		public float total_untreated_sludge_kg;
		private float pending_harmless_filth_units;
		private float pending_untreated_filth_units;
		private int spill_sequence;

		public CompProperties_SewageOutlet Props
		{
			get
			{
				return (CompProperties_SewageOutlet)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref total_harmless_water_liters, "total_harmless_water_liters", 0f);
			Scribe_Values.Look(ref total_untreated_water_liters, "total_untreated_water_liters", 0f);
			Scribe_Values.Look(ref total_untreated_sludge_kg, "total_untreated_sludge_kg", 0f);
			Scribe_Values.Look(ref pending_harmless_filth_units, "pending_harmless_filth_units", 0f);
			Scribe_Values.Look(ref pending_untreated_filth_units, "pending_untreated_filth_units", 0f);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				total_harmless_water_liters = Mathf.Max(0f, total_harmless_water_liters);
				total_untreated_water_liters = Mathf.Max(0f, total_untreated_water_liters);
				total_untreated_sludge_kg = Mathf.Max(0f, total_untreated_sludge_kg);
				pending_harmless_filth_units = Mathf.Max(0f, pending_harmless_filth_units);
				pending_untreated_filth_units = Mathf.Max(0f, pending_untreated_filth_units);
			}
		}

		public override string CompInspectStringExtra()
		{
			OperationalState operational_state = getOperationalState();
			bool operational = operational_state == OperationalState.Operational;
			string operating_state = getOperationalStateText(operational_state);

			FluidNetwork network = FluidUtility.getNetwork(parent, FluidNetworkType.WasteWater);
			bool has_storage = network != null && network.getComponents<CompWasteStorage>().Any();
			string mode = network == null
				? "RealRim_SewageOutletModeDisconnected".Translate().ToString()
				: !operational
					? "RealRim_SewageOutletModeInactive".Translate().ToString()
					: has_storage
						? "RealRim_SewageOutletModeOverflow".Translate().ToString()
						: "RealRim_SewageOutletModeDirect".Translate().ToString();

			return "RealRim_SewageOutletOperatingState".Translate(operating_state)
				+ "\n"
				+ "RealRim_SewageOutletStatus".Translate(
					mode,
					total_harmless_water_liters.ToString("N0"),
					total_untreated_water_liters.ToString("N0"),
					total_untreated_sludge_kg.ToString("N2"),
					(pending_harmless_filth_units + pending_untreated_filth_units).ToString("N1"));
		}

		public bool isOperational()
		{
			return getOperationalState() == OperationalState.Operational;
		}

		public void dischargeHarmlessWater(float water_liters)
		{
			if (!isOperational())
			{
				return;
			}

			float discharged_liters = Mathf.Max(0f, water_liters);
			if (discharged_liters <= 0f)
			{
				return;
			}

			total_harmless_water_liters += discharged_liters;
			if (!isVacuumEjection())
			{
				pending_harmless_filth_units += discharged_liters
					/ Mathf.Max(0.1f, Props.harmless_water_liters_per_filth);
				flushPendingFilth();
			}
		}

		public void dischargeUntreated(float water_liters, float sludge_kg)
		{
			if (!isOperational())
			{
				return;
			}

			float discharged_water_liters = Mathf.Max(0f, water_liters);
			float discharged_sludge_kg = Mathf.Max(0f, sludge_kg);
			if (discharged_water_liters <= 0f && discharged_sludge_kg <= 0f)
			{
				return;
			}

			total_untreated_water_liters += discharged_water_liters;
			total_untreated_sludge_kg += discharged_sludge_kg;
			if (!isVacuumEjection())
			{
				pending_untreated_filth_units += discharged_water_liters
					/ Mathf.Max(0.1f, Props.untreated_water_liters_per_filth);
				pending_untreated_filth_units += discharged_sludge_kg
					/ Mathf.Max(0.001f, Props.untreated_sludge_kg_per_filth);
				flushPendingFilth();
			}
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			if (isOperational())
			{
				flushPendingFilth();
			}
		}

		protected virtual bool isVacuumEjection()
		{
			return false;
		}

		protected virtual OperationalState getOperationalState()
		{
			Map map = parent?.Map;
			if (map == null || !parent.Spawned)
			{
				return OperationalState.NoMap;
			}

			CellRect outlet_area = getOutletArea();
			if (!outlet_area.InBounds(map))
			{
				return OperationalState.OutsideMap;
			}

			if (ModsConfig.OdysseyActive && isAreaExposedToVacuum(outlet_area, map))
			{
				return OperationalState.Vacuum;
			}

			if (!isEntireAreaNaturalSoil(outlet_area, map))
			{
				return OperationalState.NoNaturalSoil;
			}

			if (map.Parent is SpaceMapParent && !isAreaInsidePressurizedRoom(outlet_area, map))
			{
				return OperationalState.NotPressurized;
			}

			return OperationalState.Operational;
		}

		protected static string getOperationalStateText(OperationalState state)
		{
			switch (state)
			{
				case OperationalState.Operational:
					return "RealRim_SewageOutletOperational".Translate().ToString();
				case OperationalState.NoMap:
					return "RealRim_SewageOutletInactiveNoMap".Translate().ToString();
				case OperationalState.OutsideMap:
					return "RealRim_SewageOutletInactiveOutsideMap".Translate().ToString();
				case OperationalState.Vacuum:
					return "RealRim_SewageOutletInactiveVacuum".Translate().ToString();
				case OperationalState.NoNaturalSoil:
					return "RealRim_SewageOutletInactiveNoNaturalSoil".Translate().ToString();
				case OperationalState.NotPressurized:
					return "RealRim_SewageOutletInactiveNotPressurized".Translate().ToString();
				case OperationalState.NoPower:
					return "RealRim_SewageOutletInactiveNoPower".Translate().ToString();
				case OperationalState.SwitchedOff:
					return "RealRim_SewageOutletInactiveSwitchedOff".Translate().ToString();
				default:
					return state.ToString();
			}
		}

		protected CellRect getOutletArea()
		{
			IntVec3 position = parent.Position;
			if (parent.Rotation == Rot4.North)
			{
				return new CellRect(position.x - 1, position.z + 1, 3, 5);
			}
			if (parent.Rotation == Rot4.South)
			{
				return new CellRect(position.x - 1, position.z - 5, 3, 5);
			}
			if (parent.Rotation == Rot4.East)
			{
				return new CellRect(position.x + 1, position.z - 1, 5, 3);
			}
			return new CellRect(position.x - 5, position.z - 1, 5, 3);
		}

		protected static bool isAreaExposedToVacuum(CellRect outlet_area, Map map)
		{
			foreach (IntVec3 cell in outlet_area.Cells)
			{
				if (VacuumUtility.GetVacuum(cell, map) > MAX_OPERATIONAL_VACUUM)
				{
					return true;
				}
			}
			return false;
		}

		protected static bool isEntireAreaNaturalSoil(CellRect outlet_area, Map map)
		{
			foreach (IntVec3 cell in outlet_area.Cells)
			{
				TerrainDef terrain = cell.GetTerrain(map);
				if (terrain == null
					|| !terrain.IsSoil
					|| terrain.IsRock
					|| terrain.IsFloor
					|| terrain.IsSubstructure)
				{
					return false;
				}
			}
			return true;
		}

		protected static bool isAreaInsidePressurizedRoom(CellRect outlet_area, Map map)
		{
			Room outlet_room = null;
			foreach (IntVec3 cell in outlet_area.Cells)
			{
				Room room = cell.GetRoom(map);
				if (room == null)
				{
					return false;
				}

				if (outlet_room == null)
				{
					outlet_room = room;
				}
				else if (outlet_room != room)
				{
					return false;
				}
			}

			return outlet_room != null
				&& !outlet_room.PsychologicallyOutdoors
				&& VacuumUtility.IsRoomAirtight(outlet_room);
		}

		protected void flushPendingFilth()
		{
			flushFilth(ref pending_untreated_filth_units, UNTREATED_FILTH_DEF);
			flushFilth(ref pending_harmless_filth_units, HARMLESS_FILTH_DEF);
		}

		protected void flushFilth(ref float pending_units, string def_name)
		{
			int requested_count = Mathf.Min(MAX_FILTH_PER_FLUSH, Mathf.FloorToInt(pending_units));
			if (requested_count <= 0)
			{
				return;
			}

			ThingDef filth_def = DefDatabase<ThingDef>.GetNamedSilentFail(def_name);
			if (filth_def == null)
			{
				Log.ErrorOnce(
					"[RealRim] Water & Pumps: required sewage-outlet filth def is unavailable: " + def_name + ".",
					Gen.HashCombineInt(def_name.GetHashCode(), 0x5252534f));
				return;
			}

			int created_count = 0;
			for (int index = 0; index < requested_count; index++)
			{
				if (!trySpawnFilth(filth_def))
				{
					break;
				}
				created_count++;
			}
			pending_units -= created_count;
		}

		protected virtual bool trySpawnFilth(ThingDef filth_def)
		{
			Map map = parent?.Map;
			if (map == null || !parent.Spawned)
			{
				return false;
			}

			CellRect outlet_area = getOutletArea();
			IntVec3 minimum = outlet_area.Min;
			int first_cell = spill_sequence++ % outlet_area.Area;
			for (int index = 0; index < outlet_area.Area; index++)
			{
				int cell_index = (first_cell + index) % outlet_area.Area;
				IntVec3 cell = new IntVec3(
					minimum.x + cell_index % outlet_area.Width,
					0,
					minimum.z + cell_index / outlet_area.Width);
				if (FilthMaker.TryMakeFilth(cell, map, filth_def, 1))
				{
					return true;
				}
			}
			return false;
		}
	}

	public sealed class CompProperties_SewageDumpPort : CompProperties_SewageOutlet
	{
		public CompProperties_SewageDumpPort()
		{
			compClass = typeof(CompSewageDumpPort);
		}
	}

	public sealed class CompSewageDumpPort : CompSewageOutlet
	{
		protected override OperationalState getOperationalState()
		{
			Map map = parent?.Map;
			if (map == null || !parent.Spawned)
			{
				return OperationalState.NoMap;
			}

			CompFlickable flickable = parent.TryGetComp<CompFlickable>();
			if (flickable != null && !flickable.SwitchIsOn)
			{
				return OperationalState.SwitchedOff;
			}

			CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
			if (power != null && !power.PowerOn)
			{
				return OperationalState.NoPower;
			}

			if (isVacuumEjection())
			{
				return OperationalState.Operational;
			}

			CellRect outlet_area = getOutletArea();
			if (!outlet_area.InBounds(map))
			{
				return OperationalState.OutsideMap;
			}

			if (!isEntireAreaNaturalSoil(outlet_area, map))
			{
				return OperationalState.NoNaturalSoil;
			}

			return OperationalState.Operational;
		}

		protected override bool isVacuumEjection()
		{
			Map map = parent?.Map;
			if (!ModsConfig.OdysseyActive || map == null || !parent.Spawned)
			{
				return false;
			}

			IntVec3 discharge_cell = getDischargeCell();
			return discharge_cell.InBounds(map)
				&& VacuumUtility.GetVacuum(discharge_cell, map) > MAX_OPERATIONAL_VACUUM;
		}

		protected override bool trySpawnFilth(ThingDef filth_def)
		{
			if (isVacuumEjection())
			{
				return true;
			}
			return base.trySpawnFilth(filth_def);
		}

		private IntVec3 getDischargeCell()
		{
			IntVec3 position = parent.Position;
			if (parent.Rotation == Rot4.North)
			{
				return new IntVec3(position.x, 0, position.z + 1);
			}
			if (parent.Rotation == Rot4.South)
			{
				return new IntVec3(position.x, 0, position.z - 1);
			}
			if (parent.Rotation == Rot4.East)
			{
				return new IntVec3(position.x + 1, 0, position.z);
			}
			return new IntVec3(position.x - 1, 0, position.z);
		}
	}

}
