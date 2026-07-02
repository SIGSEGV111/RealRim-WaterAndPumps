using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace RealRim.WaterAndPumps
{
	[StaticConstructorOnStartup]
	internal static class RealRimDefPatcher
	{
		static RealRimDefPatcher()
		{
			LongEventHandler.ExecuteWhenFinished(applyDefinitions);
		}

		private static void applyDefinitions()
		{
			int failed_phases = 0;

			runPatchPhase("pipes", patchPipes, ref failed_phases);
			runPatchPhase("water sources", patchWaterSources, ref failed_phases);
			runPatchPhase("water storage", patchWaterStorage, ref failed_phases);
			runPatchPhase("pumps", patchPumps, ref failed_phases);
			runPatchPhase("heating", patchHeating, ref failed_phases);
			runPatchPhase("cooling", patchCooling, ref failed_phases);
			runPatchPhase("fixtures", patchFixtures, ref failed_phases);
			runPatchPhase("sprinklers", patchSprinklers, ref failed_phases);
			runPatchPhase("swimming recreation", patchSwimmingRecreation, ref failed_phases);
			runPatchPhase("kitchen sink", patchKitchenSink, ref failed_phases);
			runPatchPhase("waste processing", patchWaste, ref failed_phases);
			runPatchPhase("work definitions", patchWorkDefinitions, ref failed_phases);

			if (failed_phases == 0)
			{
				Log.Message("[RealRim] Water & Pumps 1.1.39: replaced DBH water, heating, cooling, sprinkler and sewage definitions.");
			}
			else
			{
				Log.Error("[RealRim] Water & Pumps 1.1.39: definition replacement completed with "
					+ failed_phases + " failed phase(s). Later phases were still applied; see the preceding errors.");
			}
		}

		private static void runPatchPhase(string phase_name, Action patch_action, ref int failed_phases)
		{
			try
			{
				patch_action();
			}
			catch (Exception exception)
			{
				failed_phases++;
				Log.Error("[RealRim] Water & Pumps: failed to patch " + phase_name + ": " + exception);
			}
		}

		private static void patchPipes()
		{
			setNode("sewagePipeStuff", FluidNetworkType.WasteWater);
			setNode("sewagePipeHidden", FluidNetworkType.WasteWater);
			setNode("plumbingValve", true, FluidNetworkType.WasteWater);
			setNode("airPipe", FluidNetworkType.Coolant);
			setNode("airPipeHidden", FluidNetworkType.Coolant);
			setNode("RealRim_FreshWaterPipe", FluidNetworkType.FreshWater);
			setNode("RealRim_FreshWaterPipeHidden", FluidNetworkType.FreshWater);
			setPipeNode(
				"RealRim_HotWaterPipe",
				RealPhysics.HOT_WATER_PIPE_HEAT_TRANSFER_W_PER_M_K,
				FluidNetworkType.HotWater);
			setPipeNode(
				"RealRim_HotWaterPipeHidden",
				RealPhysics.HOT_WATER_PIPE_HEAT_TRANSFER_W_PER_M_K,
				FluidNetworkType.HotWater);
			setPipeNode(
				"RealRim_HeatingPipe",
				RealPhysics.HEATING_PIPE_HEAT_TRANSFER_W_PER_M_K,
				FluidNetworkType.Heating);
			setPipeNode(
				"RealRim_HeatingPipeHidden",
				RealPhysics.HEATING_PIPE_HEAT_TRANSFER_W_PER_M_K,
				FluidNetworkType.Heating);
			setNode("RealRim_FreshWaterValve", true, FluidNetworkType.FreshWater);
			setPipeNode(
				"RealRim_HotWaterValve",
				true,
				RealPhysics.HOT_WATER_PIPE_HEAT_TRANSFER_W_PER_M_K,
				FluidNetworkType.HotWater);
			setPipeNode(
				"RealRim_HeatingValve",
				true,
				RealPhysics.HEATING_PIPE_HEAT_TRANSFER_W_PER_M_K,
				FluidNetworkType.Heating);
			setNode("RealRim_CoolantValve", true, FluidNetworkType.Coolant);
			setLabel("sewagePipeStuff", "waste-water pipe");
			setLabel("sewagePipeHidden", "waste-water pipe (hidden)");
			setLabel("plumbingValve", "waste-water valve");
			setLabel("airPipe", "air-con coolant pipe");
			setLabel("airPipeHidden", "air-con coolant pipe (hidden)");
		}

		private static void patchWaterSources()
		{
			setWaterSource("WaterWellInlet", WaterSourceKind.RegularWell);
			setWaterSource("DeepWaterWellInlet", WaterSourceKind.DeepWell);
			setWaterSource("PrimitiveWell", WaterSourceKind.Auto);
			setWaterSource("CryogenicExtractionNode", WaterSourceKind.Clean);
		}

		private static void patchWaterStorage()
		{
			setWaterTank("WaterButt", 100f);
			setWaterTank("WaterTowerS", 8000f);
			setWaterTank("WaterTowerL", 50000f);
			setWaterTank("WaterTankSmall", 2000f);

			ThingDef hot_tank = preparePassiveBuilding("HotWaterTank", false);
			if (hot_tank != null)
			{
				hot_tank.description = "A 600 L domestic hot-water tank. Its internal heat exchanger transfers heat one-way from the heating-water circuit, while the tank supplies the separate hot-water network and refills from fresh water.";
				addNode(
					hot_tank,
					false,
					FluidNetworkType.FreshWater,
					FluidNetworkType.HotWater,
					FluidNetworkType.Heating);
				addComp(hot_tank, new CompProperties_HotWaterTank
				{
					capacity_liters = 600f,
					initial_temperature_c = 20f,
					minimum_temperature_c = 5f,
					maximum_temperature_c = 85f,
					heat_exchanger_surface_m2 = 1.5f,
					maximum_transfer_kw = 20f,
					heat_loss_w_per_k = 2.5f,
				});
			}

			ThingDef heating_tank = preparePassiveBuilding("RealRim_HeatingTank", false);
			if (heating_tank != null)
			{
				addNode(heating_tank, false, FluidNetworkType.Heating);
				addComp(heating_tank, new CompProperties_ThermalTank
				{
					capacity_liters = 600f,
					initial_temperature_c = 20f,
					minimum_temperature_c = 5f,
					maximum_temperature_c = 85f,
					heat_loss_w_per_k = 2.5f,
				});
			}

			ThingDef coolant_tank = preparePassiveBuilding("RealRim_CoolantTank", false);
			if (coolant_tank != null)
			{
				addNode(coolant_tank, false, FluidNetworkType.Coolant);
				addComp(coolant_tank, new CompProperties_CoolantTank
				{
					water_liters = 600f,
					warm_temperature_c = 12f,
				});
			}
		}

		private static void patchPumps()
		{
			setWaterPump("WindPump", 1500f, 0f, false, 100f);
			setWaterPump("ElectricPump", 1200f, 250f, true, 50f);
			setWaterPump("PumpingStation", 50000f, 10000f, true, 250f);
		}

		private static void patchHeating()
		{
			setHeatSource("LogBoiler", HeatSourceKind.WoodBoiler, 20f, 0f, 0.85f, 5718f);
			setHeatSource("GasBoiler", HeatSourceKind.GasBoiler, 24f, 0f, 0.90f, 2150f);
			setHeatSource("ElectricBoiler", HeatSourceKind.ElectricBoiler, 12f, 12000f, 0.98f, 0f);
			setHeatSource("SolarHeater", HeatSourceKind.SolarThermal, 5.5f, 0f, 1f, 0f);
			setHeatSource("GeothermHeater", HeatSourceKind.Geothermal, 12f, 0f, 1f, 0f);
			setHeatSource("RealRim_AirToWaterHeatPump", HeatSourceKind.AirToWaterHeatPump, 12f, 4500f, 1f, 0f);

			ThingDef recovery_pump = preparePassiveBuilding("RealRim_CoolantToWaterHeatPump", false);
			if (recovery_pump != null)
			{
				addNode(recovery_pump, false, FluidNetworkType.Coolant, FluidNetworkType.Heating);
				addComp(recovery_pump, new CompProperties_HeatSource
				{
					kind = HeatSourceKind.CoolantToWaterHeatPump,
					nominal_thermal_kw = 12f,
					nominal_power_watts = 4500f,
				});
				setPower(recovery_pump, 4500f);
			}

			setRadiator("RadiatorStuffed", 1.5f);
			setRadiator("RadiatorLarge", 3.0f);
			setRadiator("RadiatorTowelRail", 1.2f);
		}

		private static void patchCooling()
		{
			ThingDef outdoor = preparePassiveBuilding("AirConOutdoorUnit", false);
			if (outdoor != null)
			{
				addNode(outdoor, false, FluidNetworkType.Coolant);
				addComp(outdoor, new CompProperties_CoolingPlant
				{
					nominal_cooling_kw = 12f,
					start_fill_fraction = 0.25f,
					stop_fill_fraction = 0.90f,
				});
				setPower(outdoor, 5000f);
			}

			setCoolingEmitter("AirconIndoorUnit", 3.5f, 100f, 21f, 5f, 35f);
			setCoolingEmitter("FreezerUnit", 8f, 250f, -4f, -22f, 2f);
		}

		private static void patchFixtures()
		{
			setFixture("Fountain", 1f, 12f, 0f, 0f, false, false,
				FluidNetworkType.FreshWater);
			setFixture("BasinStuff", 0.04f, 35f, 0.04f, 0f, true, true,
				FluidNetworkType.FreshWater, FluidNetworkType.HotWater, FluidNetworkType.WasteWater);
			setFixture("ToiletStuff", 9f, 12f, 10.5f, 0.225f, false, true,
				FluidNetworkType.FreshWater, FluidNetworkType.WasteWater);
			setFixture("ToiletAdvStuff", 6f, 12f, 7.5f, 0.225f, false, true,
				FluidNetworkType.FreshWater, FluidNetworkType.WasteWater);
			setFixture("ToiletSpacer", 3f, 12f, 4.5f, 0.225f, false, true,
				FluidNetworkType.FreshWater, FluidNetworkType.WasteWater);
			setFixture("ShowerStuff", 0.13f, 40f, 0.13f, 0f, true, true,
				FluidNetworkType.FreshWater, FluidNetworkType.HotWater, FluidNetworkType.WasteWater);
			setFixture("ShowerSimple", 0.13f, 39f, 0.13f, 0f, true, true,
				FluidNetworkType.FreshWater, FluidNetworkType.HotWater, FluidNetworkType.WasteWater);
			setFixture("ShowerAdvStuff", 0.16f, 40f, 0.16f, 0f, true, true,
				FluidNetworkType.FreshWater, FluidNetworkType.HotWater, FluidNetworkType.WasteWater);
			setFixture("BathtubStuff", 1f, 39f, 1f, 0f, true, true,
				FluidNetworkType.FreshWater, FluidNetworkType.HotWater, FluidNetworkType.WasteWater);

			ThingDef pool = getDef("DBHSwimmingPool");
			if (pool != null)
			{
				removeReplacementComps(pool, false);
				pool.description = "RealRim_PoolDescription".Translate().ToString();
				addNode(pool, false, FluidNetworkType.FreshWater, FluidNetworkType.Heating);
				addComp(pool, new CompProperties_PoolPhysics
				{
					capacity_liters = 65000f,
					water_surface_m2 = 45f,
					heat_exchanger_surface_m2 = 0.6f,
					refill_liters_per_hour = 5000f,
				});
				addComp(pool, new CompProperties_TargetTemperature
				{
					default_temperature_c = 28f,
					minimum_temperature_c = 10f,
					maximum_temperature_c = 40f,
				});
			}

			ThingDef latrine = getDef("PitLatrine");
			if (latrine != null)
			{
				removeReplacementComps(latrine, false);
				if (latrine.placeWorkers != null)
				{
					latrine.placeWorkers.RemoveAll(type => type != null && type.Namespace == "DubsBadHygiene");
				}
				addComp(latrine, new CompProperties_Latrine());
				addLatrineRefillComponent(latrine);
			}

			setTrough("WaterTrough", 200f, 500f);
			setTrough("PetWaterBowl", 12f, 60f);
		}

		private static void patchSprinklers()
		{
			setSprinkler("IrrigationSprinkler", SprinklerKind.Irrigation);
			setSprinkler("FireSprinkler", SprinklerKind.FireSuppression);

			JobDef trigger_job = DefDatabase<JobDef>.GetNamedSilentFail("TriggerFireSprinkler");
			if (trigger_job == null)
			{
				throw new InvalidOperationException("TriggerFireSprinkler JobDef was not loaded.");
			}
			trigger_job.driverClass = typeof(JobDriver_TriggerSprinkler);
		}

		private static void setSprinkler(string def_name, SprinklerKind kind)
		{
			ThingDef def = getDef(def_name);
			if (def == null)
			{
				throw new InvalidOperationException(def_name + " ThingDef was not loaded.");
			}

			removeReplacementComps(def, false);
			addNode(def, false, FluidNetworkType.FreshWater);
			addComp(def, new CompProperties_Sprinkler
			{
				kind = kind,
			});

			def.description = (kind == SprinklerKind.Irrigation
				? "RealRim_IrrigationSprinklerDescription"
				: "RealRim_FireSprinklerDescription").Translate().ToString();

			if (def.placeWorkers == null)
			{
				def.placeWorkers = new List<Type>();
			}
			def.placeWorkers.RemoveAll(type => type == typeof(PlaceWorker_Sprinkler)
				|| (type != null && type.FullName == "DubsBadHygiene.PlaceWorker_Sprinkler"));
			def.placeWorkers.Insert(0, typeof(PlaceWorker_Sprinkler));
		}

		private static void patchSwimmingRecreation()
		{
			JoyKindDef swimming = DefDatabase<JoyKindDef>.GetNamedSilentFail("RealRim_Swimming");
			if (swimming == null)
			{
				throw new InvalidOperationException("RealRim_Swimming JoyKindDef was not loaded.");
			}

			JobDef swimming_job = DefDatabase<JobDef>.GetNamedSilentFail("DBHGoSwimming");
			if (swimming_job == null)
			{
				throw new InvalidOperationException("DBHGoSwimming JobDef was not loaded.");
			}
			swimming_job.joyKind = swimming;

			JoyGiverDef swimming_giver = DefDatabase<JoyGiverDef>.GetNamedSilentFail("UseDBHSwimmingPool");
			if (swimming_giver == null)
			{
				throw new InvalidOperationException("UseDBHSwimmingPool JoyGiverDef was not loaded.");
			}
			swimming_giver.joyKind = swimming;

			ThingDef pool = getDef("DBHSwimmingPool");
			if (pool == null || pool.building == null)
			{
				throw new InvalidOperationException("DBHSwimmingPool building definition is unavailable.");
			}
			pool.building.joyKind = swimming;
		}

		private static void patchKitchenSink()
		{
			List<ThingDef> all_defs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int index = 0; index < all_defs.Count; index++)
			{
				ThingDef def = all_defs[index];
				if (isKitchenSinkDefinition(def))
				{
					ensureKitchenSinkDefinition(def);
				}
			}
		}

		internal static bool isKitchenSinkDefinition(ThingDef def)
		{
			if (def == null)
			{
				return false;
			}
			if (def.defName == "KitchenSink")
			{
				return true;
			}
			Type building_type = def.thingClass;
			bool is_dbh_basin = false;
			while (building_type != null)
			{
				if (building_type.FullName == "DubsBadHygiene.Building_basin")
				{
					is_dbh_basin = true;
					break;
				}
				building_type = building_type.BaseType;
			}
			if (!is_dbh_basin)
			{
				return false;
			}
			if (def.comps == null)
			{
				return false;
			}
			for (int index = 0; index < def.comps.Count; index++)
			{
				if (def.comps[index] is CompProperties_Facility)
				{
					return true;
				}
			}
			return false;
		}

		private static void ensureKitchenSinkDefinition(ThingDef def)
		{
			// The DBH building class is retained so its established pawn-use and assignment
			// jobs continue to recognize the fixture. Its legacy pipe/blockage comps are
			// removed; all water, heat and waste accounting is supplied by RealRim comps.
			removeReplacementComps(def, false);
			addNode(
				def,
				false,
				FluidNetworkType.FreshWater,
				FluidNetworkType.HotWater,
				FluidNetworkType.WasteWater);
			addComp(def, new CompProperties_Fixture
			{
				water_per_use_liters = 0.04f,
				desired_temperature_c = 35f,
				waste_water_liters = 0.04f,
				sludge_kg = 0f,
				wants_hot_water = true,
				needs_drain = true,
				kitchen_sink = true,
				linked_stove_water_liters_per_hour = 12f,
				linked_stove_sludge_kg_per_hour = 0.075f,
			});
		}

		private static void addLatrineRefillComponent(ThingDef latrine)
		{
			Type water_fillable_type = findType("DubsBadHygiene.CompWaterFillable");
			ThingDef water_bottle = DefDatabase<ThingDef>.GetNamedSilentFail("DBH_WaterBottle");
			if (water_fillable_type == null || water_bottle == null)
			{
				Log.Error("[RealRim] Water & Pumps: latrine refill integration is unavailable. "
					+ "CompWaterFillable found=" + (water_fillable_type != null)
					+ ", DBH_WaterBottle found=" + (water_bottle != null) + ".");
				return;
			}

			ThingFilter fuel_filter = new ThingFilter();
			fuel_filter.SetAllow(water_bottle, true);

			CompProperties_Refuelable refill_properties = new CompProperties_Refuelable
			{
				compClass = water_fillable_type,
				fuelCapacity = 30f,
				fuelConsumptionRate = 0f,
				fuelFilter = fuel_filter,
				showAllowAutoRefuelToggle = true,
			};

			// ResolveReferences expects a non-null filter and, on some RimWorld builds,
			// expects the properties object to already be present on the parent def.
			addComp(latrine, refill_properties);
			try
			{
				refill_properties.ResolveReferences(latrine);
			}
			catch (Exception exception)
			{
				latrine.comps.Remove(refill_properties);
				Log.Error("[RealRim] Water & Pumps: failed to initialize DBH latrine refill component; "
					+ "the latrine remains usable through its internal water state but will not receive DBH refill jobs. "
					+ exception);
			}
		}

		private static void patchWaste()
		{
			ThingDef septic = preparePassiveBuilding("SewageSepticTank", true);
			if (septic != null)
			{
				addNode(septic, false, FluidNetworkType.WasteWater);
				addComp(septic, new CompProperties_WasteStorage
				{
					water_capacity_liters = 12000f,
					sludge_capacity_kg = 600f,
					infiltration_liters_per_day = 1200f,
					treatment_liters_per_day = 0f,
				});
			}

			ThingDef treatment = preparePassiveBuilding("SewageTreatment", true);
			if (treatment != null)
			{
				addNode(treatment, false, FluidNetworkType.WasteWater, FluidNetworkType.FreshWater);
				addComp(treatment, new CompProperties_WasteStorage
				{
					water_capacity_liters = 50000f,
					sludge_capacity_kg = 1000f,
					treatment_liters_per_day = 5000f,
					recovery_fraction = 0.95f,
				});
				setPower(treatment, 2000f);
			}

			ThingDef spacer_recovery = preparePassiveBuilding("SpacerWaterRecoverySystem", true);
			if (spacer_recovery != null)
			{
				addNode(spacer_recovery, false, FluidNetworkType.WasteWater, FluidNetworkType.FreshWater);
				addComp(spacer_recovery, new CompProperties_WasteStorage
				{
					water_capacity_liters = 20000f,
					sludge_capacity_kg = 600f,
					treatment_liters_per_day = 10000f,
					recovery_fraction = 0.98f,
				});
				setPower(spacer_recovery, 1500f);
			}

			ThingDef water_treatment = preparePassiveBuilding("WaterTreatment", false);
			if (water_treatment != null)
			{
				addNode(water_treatment, false, FluidNetworkType.FreshWater);
				addComp(water_treatment, new CompProperties_WaterTreatment
				{
					pathogen_removal_fraction = 0.99f,
				});
				setPower(water_treatment, 800f);
				water_treatment.description = "Removes 99% of waterborne pathogens from all water drawn through its connected fresh-water network. Requires power.";
			}

			ThingDef sludge = getDef("FecalSludge");
			if (sludge != null)
			{
				sludge.SetStatBaseValue(StatDefOf.Mass, 0.05f);
				sludge.stackLimit = 2000;
			}
		}

		private static void patchWorkDefinitions()
		{
			JobDef empty_job = DefDatabase<JobDef>.GetNamedSilentFail("emptySeptictank");
			if (empty_job != null)
			{
				empty_job.driverClass = typeof(JobDriver_EmptyWasteStorage);
			}

			string[] work_giver_names =
			{
				"emptySepticTank",
				"WardenEmptySepticTank",
			};
			for (int index = 0; index < work_giver_names.Length; index++)
			{
				WorkGiverDef work_giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail(work_giver_names[index]);
				if (work_giver != null)
				{
					work_giver.giverClass = typeof(WorkGiver_EmptyWasteStorage);
				}
			}
		}



		private static void setWaterSource(
			string def_name,
			WaterSourceKind source_kind)
		{
			ThingDef def = getDef(def_name);
			if (def == null)
			{
				return;
			}
			removeComp<CompProperties_WaterSource>(def);
			addNode(def, false, FluidNetworkType.FreshWater);
			addComp(def, new CompProperties_WaterSource
			{
				source_kind = source_kind,
			});
		}

		private static void setWaterTank(string def_name, float capacity_liters)
		{
			ThingDef def = preparePassiveBuilding(def_name, false);
			if (def == null)
			{
				return;
			}
			addNode(def, false, FluidNetworkType.FreshWater);
			addComp(def, new CompProperties_WaterTank
			{
				capacity_liters = capacity_liters,
				initial_fill_fraction = 0f,
			});
		}

		private static void setWaterPump(string def_name, float liters_per_hour, float power_watts, bool requires_power, float hydraulic_reference_length_m)
		{
			ThingDef def = def_name == "WindPump"
				? prepareWindPump()
				: preparePassiveBuilding(def_name, false);
			if (def == null)
			{
				return;
			}
			addNode(def, false, FluidNetworkType.FreshWater);
			addComp(def, new CompProperties_WaterPump
			{
				nominal_liters_per_hour = liters_per_hour,
				power_watts = power_watts,
				requires_power = requires_power,
				hydraulic_reference_length_m = hydraulic_reference_length_m,
			});
			setPower(def, power_watts);
		}

		private static void setHeatSource(
			string def_name,
			HeatSourceKind kind,
			float thermal_kw,
			float power_watts,
			float conversion_efficiency,
			float fuel_kj_per_unit)
		{
			ThingDef def = preparePassiveBuilding(def_name, false);
			if (def == null)
			{
				return;
			}
			addNode(def, false, FluidNetworkType.Heating);
			addComp(def, new CompProperties_HeatSource
			{
				kind = kind,
				nominal_thermal_kw = thermal_kw,
				nominal_power_watts = power_watts,
				conversion_efficiency = conversion_efficiency,
				fuel_energy_kj_per_unit = fuel_kj_per_unit,
			});
			setPower(def, power_watts);
		}

		private static void setRadiator(string def_name, float rated_output_kw)
		{
			ThingDef def = preparePassiveBuilding(def_name, false);
			if (def == null)
			{
				return;
			}
			addNode(def, false, FluidNetworkType.Heating);
			addComp(def, new CompProperties_RoomHeatExchanger
			{
				kind = RoomHeatExchangerKind.Radiator,
				rated_output_kw = rated_output_kw,
			});
			addComp(def, new CompProperties_TargetTemperature
			{
				default_temperature_c = 21f,
				minimum_temperature_c = 5f,
				maximum_temperature_c = 35f,
			});
		}

		private static void setCoolingEmitter(string def_name, float output_kw, float power_watts, float target_c, float minimum_c, float maximum_c)
		{
			ThingDef def = preparePassiveBuilding(def_name, false);
			if (def == null)
			{
				return;
			}
			addNode(def, false, FluidNetworkType.Coolant);
			addComp(def, new CompProperties_RoomHeatExchanger
			{
				kind = RoomHeatExchangerKind.CoolingUnit,
				rated_output_kw = output_kw,
				fan_power_watts = power_watts,
			});
			addComp(def, new CompProperties_TargetTemperature
			{
				default_temperature_c = target_c,
				minimum_temperature_c = minimum_c,
				maximum_temperature_c = maximum_c,
			});
			setPower(def, power_watts);
		}

		private static void setFixture(
			string def_name,
			float water_liters,
			float desired_temperature_c,
			float waste_liters,
			float sludge_kg,
			bool wants_hot,
			bool needs_drain,
			params FluidNetworkType[] networks)
		{
			ThingDef def = getDef(def_name);
			if (def == null)
			{
				return;
			}
			removeReplacementComps(def, needs_drain);
			addNode(def, false, networks);
			addComp(def, new CompProperties_Fixture
			{
				water_per_use_liters = water_liters,
				desired_temperature_c = desired_temperature_c,
				waste_water_liters = waste_liters,
				sludge_kg = sludge_kg,
				wants_hot_water = wants_hot,
				needs_drain = needs_drain,
			});
		}

		private static void setTrough(string def_name, float capacity_liters, float refill_liters_per_hour)
		{
			ThingDef def = getDef(def_name);
			if (def == null)
			{
				return;
			}
			removeReplacementComps(def, false);
			addNode(def, false, FluidNetworkType.FreshWater);
			addComp(def, new CompProperties_WaterTrough
			{
				capacity_liters = capacity_liters,
				refill_liters_per_hour = refill_liters_per_hour,
			});
		}

		private static ThingDef prepareWindPump()
		{
			const string WIND_PROPERTIES_TYPE = "DubsBadHygiene.CompProperties_WaterPumpingStation";
			const string WIND_COMPONENT_TYPE = "DubsBadHygiene.CompWindPump";

			ThingDef def = getDef("WindPump");
			if (def == null)
			{
				return null;
			}
			if (def.comps == null)
			{
				def.comps = new List<CompProperties>();
			}

			CompProperties wind_properties = null;
			for (int index = 0; index < def.comps.Count; index++)
			{
				CompProperties candidate = def.comps[index];
				if (candidate != null && candidate.GetType().FullName == WIND_PROPERTIES_TYPE)
				{
					wind_properties = candidate;
					break;
				}
			}

			def.comps.RemoveAll(comp =>
			{
				if (comp == null)
				{
					return true;
				}
				Type properties_type = comp.GetType();
				bool ours = properties_type.Namespace == typeof(RealRimDefPatcher).Namespace;
				bool other_dbh_component = properties_type.Namespace == "DubsBadHygiene"
					&& !ReferenceEquals(comp, wind_properties);
				return ours || other_dbh_component;
			});

			Type wind_properties_type = findType(WIND_PROPERTIES_TYPE);
			Type wind_component_type = findType(WIND_COMPONENT_TYPE);
			if (wind_properties == null && wind_properties_type != null)
			{
				wind_properties = Activator.CreateInstance(wind_properties_type) as CompProperties;
				if (wind_properties != null)
				{
					def.comps.Add(wind_properties);
				}
			}

			if (wind_properties == null || wind_component_type == null)
			{
				Log.Error("[RealRim] Water & Pumps: could not restore DBH's wind-pump component. "
					+ "Properties found=" + (wind_properties != null)
					+ ", component type found=" + (wind_component_type != null) + ".");
				return def;
			}

			wind_properties.compClass = wind_component_type;
			setFloatMember(wind_properties, "Capacity", 2000f);
			return def;
		}

		private static void setFloatMember(object instance, string member_name, float value)
		{
			if (instance == null)
			{
				return;
			}

			const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = instance.GetType().GetField(member_name, FLAGS);
			if (field != null && field.FieldType == typeof(float))
			{
				field.SetValue(instance, value);
				return;
			}

			PropertyInfo property = instance.GetType().GetProperty(member_name, FLAGS);
			if (property != null && property.CanWrite && property.PropertyType == typeof(float))
			{
				property.SetValue(instance, value, null);
			}
		}

		private static ThingDef preparePassiveBuilding(string def_name, bool preserve_legacy_pipe)
		{
			ThingDef def = getDef(def_name);
			if (def == null)
			{
				return null;
			}
			def.thingClass = typeof(Building);
			removeReplacementComps(def, preserve_legacy_pipe);
			return def;
		}

		private static void setNode(string def_name, params FluidNetworkType[] networks)
		{
			setNode(def_name, false, networks);
		}

		private static void setNode(string def_name, bool valve, params FluidNetworkType[] networks)
		{
			ThingDef def = getDef(def_name);
			if (def != null)
			{
				addNode(def, valve, networks);
			}
		}

		private static void setPipeNode(
			string def_name,
			float outdoor_heat_exchange_w_per_m_k,
			params FluidNetworkType[] networks)
		{
			setPipeNode(def_name, false, outdoor_heat_exchange_w_per_m_k, networks);
		}

		private static void setPipeNode(
			string def_name,
			bool valve,
			float outdoor_heat_exchange_w_per_m_k,
			params FluidNetworkType[] networks)
		{
			ThingDef def = getDef(def_name);
			if (def != null)
			{
				addNode(def, valve, outdoor_heat_exchange_w_per_m_k, networks);
			}
		}

		private static void addNode(ThingDef def, bool valve, params FluidNetworkType[] networks)
		{
			addNode(def, valve, 0f, networks);
		}

		private static void addNode(
			ThingDef def,
			bool valve,
			float outdoor_heat_exchange_w_per_m_k,
			params FluidNetworkType[] networks)
		{
			removeComp<CompProperties_FluidNode>(def);
			addComp(def, new CompProperties_FluidNode
			{
				networks = new List<FluidNetworkType>(networks),
				valve = valve,
				outdoor_heat_exchange_w_per_m_k = outdoor_heat_exchange_w_per_m_k,
			});
		}

		private static void removeReplacementComps(ThingDef def, bool preserve_legacy_pipe)
		{
			if (def.comps == null)
			{
				def.comps = new List<CompProperties>();
				return;
			}

			def.comps.RemoveAll(comp =>
			{
				if (comp == null)
				{
					return true;
				}
				Type properties_type = comp.GetType();
				Type comp_type = comp.compClass;
				bool legacy_pipe = properties_type.FullName == "DubsBadHygiene.CompProperties_Pipe";
				bool old_dbh = properties_type.Namespace == "DubsBadHygiene"
					|| (comp_type != null && comp_type.Namespace == "DubsBadHygiene");
				bool ours = properties_type.Namespace == typeof(RealRimDefPatcher).Namespace;
				return ours || (old_dbh && !(preserve_legacy_pipe && legacy_pipe));
			});
		}

		private static void setPower(ThingDef def, float watts)
		{
			if (def.comps == null)
			{
				return;
			}

			for (int index = 0; index < def.comps.Count; index++)
			{
				CompProperties_Power power = def.comps[index] as CompProperties_Power;
				if (power == null)
				{
					continue;
				}

				if (!trySetPowerConsumption(power, watts))
				{
					Log.ErrorOnce(
						"[RealRim] Water & Pumps: could not set power consumption for " + def.defName
						+ ". Runtime CompProperties_Power exposes " + power.PowerConsumption.ToString("N1") + " W.",
						Gen.HashCombineInt(def.defName.GetHashCode(), 0x52415750));
				}
				return;
			}
		}

		private static bool trySetPowerConsumption(CompProperties_Power power, float watts)
		{
			const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

			PropertyInfo property = typeof(CompProperties_Power).GetProperty("PowerConsumption", FLAGS);
			if (property != null && property.CanWrite)
			{
				try
				{
					property.SetValue(power, watts, null);
					return Math.Abs(power.PowerConsumption - watts) <= Math.Max(0.01f, Math.Abs(watts) * 0.001f);
				}
				catch
				{
				}
			}

			float current = power.PowerConsumption;
			FieldInfo[] fields = typeof(CompProperties_Power).GetFields(FLAGS);
			for (int index = 0; index < fields.Length; index++)
			{
				FieldInfo field = fields[index];
				if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.FieldType != typeof(float))
				{
					continue;
				}

				float original;
				try
				{
					original = (float)field.GetValue(power);
				}
				catch
				{
					continue;
				}

				float probe_delta = Math.Max(1f, Math.Abs(original) * 0.01f);
				try
				{
					field.SetValue(power, original + probe_delta);
					float sensitivity = (power.PowerConsumption - current) / probe_delta;
					field.SetValue(power, original);

					if (Math.Abs(sensitivity) < 0.0001f)
					{
						continue;
					}

					float adjusted = original + (watts - current) / sensitivity;
					field.SetValue(power, adjusted);
					if (Math.Abs(power.PowerConsumption - watts) <= Math.Max(0.01f, Math.Abs(watts) * 0.001f))
					{
						return true;
					}
					field.SetValue(power, original);
				}
				catch
				{
					try
					{
						field.SetValue(power, original);
					}
					catch
					{
					}
				}
			}

			return false;
		}

		private static void addComp(ThingDef def, CompProperties comp)
		{
			if (def.comps == null)
			{
				def.comps = new List<CompProperties>();
			}
			def.comps.Add(comp);
		}

		private static void removeComp<T>(ThingDef def) where T : CompProperties
		{
			if (def.comps != null)
			{
				def.comps.RemoveAll(comp => comp is T);
			}
		}

		private static void setLabel(string def_name, string label)
		{
			ThingDef def = getDef(def_name);
			if (def != null)
			{
				def.label = label;
			}
		}

		private static Type findType(string full_name)
		{
			System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
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

		private static ThingDef getDef(string def_name)
		{
			ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(def_name);
			if (def == null)
			{
				Log.Warning("[RealRim] Water & Pumps: DBH ThingDef not found: " + def_name);
			}
			return def;
		}
	}
}
