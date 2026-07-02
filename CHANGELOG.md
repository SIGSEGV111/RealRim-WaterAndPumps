# Changelog

## 1.1.7 — RimWorld API build compatibility

- Replaced invalid `ThingDef.UIIcon` accesses with the RimWorld 1.6 `ThingDef.uiIcon` field.
- Converted the water-pump translated inspection text to `string` before calling `TrimEnd`, avoiding the incompatible `TaggedString` overload resolution.
- Updated release metadata and runtime version labels to 1.1.7.

## 1.1.6 — network visuals and thermal-storage split

- Added colored full-network overlays while placing pipes and selecting connected nodes.
- Changed fresh-water, hot-water and heating pipe graphics to linked atlases.
- Kept the new pressure/circuit pipes steel-only; this was an explicit replacement-def choice rather than inherited DBH behavior.
- Added short colored connector stubs from multi-network buildings to adjacent matching pipes.
- Fixed fixture inspection strings ending in whitespace.
- Fixed sinks and baths reporting 12 °C when warm water below the requested target was available.
- Corrected mixed hot/cold availability checks so fixtures require only the actual cold-water share.
- Suppressed DBH's invalid latrine refill-time inspection suffix.
- Added per-pump automatic control enable/disable and configurable start/stop storage thresholds.
- Split the former shared tank into a domestic hot-water tank and a dedicated heating-water buffer tank.
- Added a one-way heating-to-domestic-water heat exchanger and fresh-water refill for the hot-water tank.
- Restored DBH wind-pump propeller animation and obstacle detection; its reported wind availability now scales RealRim flow.
- Integrated DBH swimming-pool fill-flow controls into RealRim refill flow.
- Synchronized the DBH pool water graphic with RealRim volume and replaced mixed legacy inspection text.
- Simplified `build.sh` for clean source-tree extraction.

## 1.1.5 — startup safety and clean overlay builds

- Fixed initialization of the DBH latrine refill component and its water-bottle filter.
- Isolated definition-replacement phases so a failure in one phase no longer blocks later phases.
- Removed the invalid attempt to patch DBH's abstract `ToiletAdv` parent.
- Restricted the project to explicit current source directories so stale 1.0.x files cannot be compiled accidentally.

## 1.1.4 — C# 7.3 and RimWorld 1.6 API build fixes

- Converted mixed `TaggedString`/`string` conditional expressions to explicit strings for C# 7.3.
- Updated spawned-pawn handling to the RimWorld 1.6 `IReadOnlyList<Pawn>` API.
- Replaced the removed `CompProperties_Power.basePowerConsumption` field access with runtime detection through the verified `PowerConsumption` property.
- Retains the 1.1.3 RimWorld 1.6 despawn-hook correction.

## 1.1.3 — RimWorld 1.6 despawn API fix

- Updated `CompFluidNode.PostDeSpawn` to the RimWorld 1.6 signature `(Map, DestroyMode)`.
- Forwarded the destroy mode to `ThingComp.PostDeSpawn` after deregistering the node.
- Fixes build error CS0115 against RimWorld 1.6.4850.

## 1.1.2 — waterborne pathogens and treatment

- Added persistent hidden contamination profiles for regular wells, deep wells, surface water, mud/flood water and clean cryogenic sources.
- Added geometric multi-pathogen selection with category-specific occurrence and concentration ranges.
- Added DBH diarrhea, dysentery and cholera plus compatible RimWorld disease pathogens.
- Added volume-weighted pathogen mixing in fresh-water tanks and troughs.
- Added dose-based disease rolls for flesh pawns and animals drinking from fixtures, troughs or terrain water.
- Added developer-only source/tank contamination diagnostics.
- Restored the DBH `WaterTreatment` building as an 800 W powered unit that removes 99% of every pathogen from delivered water.
- Added weighted source contribution support for networks with multiple water sources.

## 1.1.1 — fixture integration and calibrated room heat

- Calibrated `GenTemperature.PushHeat` conversion to `1000 / 3025` heat units per kW·s.
- Added optional support for `VTE_HeatInclined` and `VTE_ColdInclined`, including water-temperature preference and mood effects.
- Reused DBH `CompWaterFillable`, `WorkGiver_RefillWater` and `JobDriver_RefillWater` for the independent 30 L latrine flush tank.
- Removed DBH sewage placement restrictions from the independent latrine.
- Added continuous kitchen-sink water, wastewater and captured-solid generation while linked or nearby cooking stoves are active; multiple stoves contribute independently.
- Replaced DBH septic-emptying job/work-giver classes with RealRim implementations.
- Automatically schedules sludge extraction at 10 kg, equal to 200 × 50 g units; no manual building command is required.
- Added the spacer water-recovery system as a 10,000 L/day, 98% recovery, 1.5 kW processor.
- Hid the unused legacy clean-water treatment building while contamination and disease simulation remain deferred.
- Disabled direct automatic sludge ejection from treatment buildings.

## 1.1.0 — physics replacement foundation

- Replaced the DBH shared water/plumbing model with independent fresh-water, hot-water, heating-water, waste-water and coolant networks.
- Reused existing DBH building `ThingDef` IDs where practical; no 1.0.x save compatibility is intended.
- Added new visible/hidden pipe defs and valves for fresh water, hot water and heating water.
- Reused DBH sewage pipes for waste water and DBH air pipes for coolant.
- Added graph-based network discovery, valve separation and per-building metric status output.
- Added realistic pump power, nominal flow, storage and long-route attenuation.
- Added 600 L thermal tanks, boiler/collector/heat-pump hysteresis, fuel-energy accounting and ΔT radiator output.
- Added air-to-water and coolant-to-water heat pumps.
- Added phase-change coolant storage and demand-driven outdoor/indoor air-conditioning logic.
- Added 65 m³ pool water, temperature, rain, solar, convection, evaporation and night-sky calculations.
- Added fresh/hot/waste fixture checks and water-temperature complaints.
- Added body-size-based thirst volume and configurable race/kind water requirements.
- Added trough storage/refill and approximately three drinks per day.
- Added independent latrine water, wastewater and sludge storage.
- Added septic infiltration and sewage treatment with 95% fresh-water recovery and automatic 50 g sludge-unit ejection.
- Redirected verified DBH network, fixture, thirst, drink, hand-wash, pool and latrine runtime callbacks.
- Disabled obsolete DBH water/sewer status alerts under the replacement system.
- Added `KNOWN-LIMITATIONS.md` for unresolved exact integration identifiers and calibration hooks.

## 1.0.5

- Fixed DBH network discovery returning a `PlumbingNet[]` array instead of the pump's concrete `PlumbingNet`.
- Persisted the controller-to-pump reference and added a load grace period.
- Changed orphan handling to uninstall the controller as a minified item.

## 1.0.4

- Corrected DBH storage discovery and added detailed adapter diagnostics.

## 1.0.3

- Added realistic electric-pump power and flow values plus pipe-length attenuation.

## 1.0.2

- Fixed the RimWorld 1.6 `Tick()` access modifier and completed the RealRim rename.

## 1.0.1

- Renamed the mod and corrected its DBH architect-category reference.

## 1.0.0

- Initial automatic water-pump controller source release.
