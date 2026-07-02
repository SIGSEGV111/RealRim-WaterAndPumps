# Changelog

## 1.1.27 — Kitchen-sink pre-tick inspect fix

- Removed the empty line produced by the kitchen-sink fixture status before the first stove/fluid tick.
- Trimmed the base fixture status before appending the kitchen-sink activity section.
- Updated release metadata and runtime version labels to 1.1.27.

## 1.1.26 — Kitchen-sink facility identification fix

- Fixed kitchen-sink integration for DBH basin-class facility definitions whose defName is not exactly `KitchenSink`.
- Kitchen sinks are now identified by their actual runtime structure: `DubsBadHygiene.Building_basin` plus RimWorld's `CompFacility`.
- The recognized definition receives fresh-water, hot-water and waste-water nodes plus the RealRim kitchen-sink fixture component before any save is loaded or building is created.
- Removed the ineffective runtime component-list mutation from the previous fixes.
- DBH's legacy kitchen-sink inspect text is suppressed for the correctly identified facility sink.
- Stove linkage continues to use only `CompAffectedByFacilities.LinkedFacilitiesListForReading`.
- Updated release metadata and runtime version labels to 1.1.26.

## 1.1.25 — Kitchen-sink runtime component repair

- Fixed the actual cause of the persistent DBH "No water capacity" state: loaded kitchen-sink instances could retain DBH's cached pipe/blockage comps while lacking the RealRim fluid comps even after their `ThingDef` had been changed.
- Repairs the kitchen sink's instance component list before DBH exposes or spawns the fixture, so existing saves and newly built sinks receive fresh-water, hot-water and waste-water connectivity.
- Removes only DBH's legacy `CompPipe` and `CompBlockage` instances; RimWorld's facility component and its authoritative stove links are preserved.
- Replaces DBH's kitchen-sink inspect-string body with the actual component status, retaining facility statistics while removing the obsolete DBH water-capacity/owner text.
- Stove accounting continues to use only `CompAffectedByFacilities.LinkedFacilitiesListForReading`; no proximity or room fallback is used.
- Updated release metadata and runtime version labels to 1.1.25.

## 1.1.24 — Kitchen-sink component initialization fix

- Enforced the exact `KitchenSink` definition immediately before RimWorld initializes its comps, eliminating static-constructor ordering issues that could leave the sink on DBH's legacy plumbing state.
- Removed the DBH pipe and blockage comps from the kitchen sink while retaining DBH's building class for its established pawn-use and assignment jobs.
- The kitchen sink now always receives RealRim fresh-water, hot-water and waste-water nodes plus normal sink behavior.
- Stove water and waste generation continues to use only RimWorld's authoritative `CompAffectedByFacilities.LinkedFacilitiesListForReading` relationship; no proximity or room fallback is used.
- Updated release metadata and runtime version labels to 1.1.24.

## 1.1.23 — Command icons and kitchen-sink integration

- Assigned the five external textures to the heating overview, lower target, raise target, pump-threshold and heat-source target commands.
- Configured the kitchen sink in its own definition phase as a normal sink connected to fresh water, hot water and waste water.
- Made the kitchen sink use the same per-use water, temperature and drain behavior as the regular basin while retaining continuous cooking-related water and waste generation.
- Stove integration now uses only RimWorld's authoritative `CompAffectedByFacilities.LinkedFacilitiesListForReading` relationship; removed the proximity/room fallback entirely.
- Removed the obsolete root-level source file that still contained embedded image data.
- Updated release metadata and runtime version labels to 1.1.23.

## 1.1.22 — External command textures

- Moved the five custom command graphics out of C# source and into normal RimWorld texture assets under `Textures/RealRim/UI`.
- Replaced embedded PNG decoding with standard `ContentFinder<Texture2D>` loading.
- Added an overlay archive intended to be extracted directly over an existing 1.1.21 mod directory.
- Updated release metadata and runtime version labels to 1.1.22.

## 1.1.21 — Pool sky-radiation model and command icons

- Reworked outdoor swimming-pool radiation so it always exchanges long-wave heat with the sky, both by day and by night.
- Added a weather-aware effective sky-temperature estimate based on air temperature, humidity and cloud cover.
- Added sky temperature and sky-radiation heat loss to the swimming-pool status text and player-facing description.
- Replaced the generic command icons for heating overview, lower target, raise target, pump-threshold configuration and heat-source target configuration with the supplied custom graphics.
- Updated release metadata and runtime version labels to 1.1.21.

## 1.1.20 — Heating report bedroom-label fix

- Use the generic localized room-role label for bedroom and barracks headings before adding bed-owner names.
- Prevent duplicated headings such as `Elida's Elida's bedroom` and `Belle and Mono's Mono and Belle's bedroom`.
- Updated release metadata and runtime version labels to 1.1.20.

## 1.1.19 — Adjustable heating targets and network report

- Added a per-building heating-buffer target to electric, gas and wood boilers and both active heat-pump types. Solar and geothermal sources remain passive and continue heating to the storage maximum.
- Active sources now stop at their selected target and restart 5 °C below it. Existing saves default to the previous 75 °C stop temperature.
- Added a target-temperature dialog and inspection status showing both the target and restart temperature. Lower targets reduce heat-pump temperature lift and therefore normally improve COP.
- Added a Heating overview command to every heating-network node. The live report shows current production, consumption, net storage rate, total connected pipe length and every functional node grouped by room.
- Bedroom and barracks headings use bed-owner names where available; other groups use RimWorld's room-role labels and measured room area.
- Updated release metadata and runtime version labels to 1.1.19.

## 1.1.18 — Visible pipe color correction

- Changed the visible hot-water pipe tint from orange to red.
- Changed the visible heating-pipe tint from red to orange.
- The physical pipe colors now match the DBH-style network overlay colors.
- Hidden pipes remain transparent and unchanged.
- Updated release metadata and runtime version labels to 1.1.18.

## 1.1.17 — Linked pipe material build fix

- Replaced the direct call to the protected `Graphic_Linked.LinkedDrawMatFrom` method with a cached reflection lookup.
- Preserves the 1.1.16 world-space visible-pipe offsets while compiling against RimWorld's public reference API.
- Falls back to RimWorld's normal centered linked-pipe rendering if the internal material method cannot be resolved.
- Updated release metadata and runtime version labels to 1.1.17.

## 1.1.16 — Functional visible-pipe separation

- Replaced the ineffective `GraphicData.drawOffset` approach for visible pipes. RimWorld's linked-graphic mesh printer ignores that field while printing linked pipe atlases.
- Added a selective `Graphic_Linked.Print` bridge for the five visible pipe defs. It renders the same linked atlas material at a real world-space offset, so overlapping fresh-water, hot-water, heating, waste-water and coolant pipes remain simultaneously visible.
- Hidden pipes are unchanged and remain centered.
- Updated release metadata and runtime version labels to 1.1.16.

## 1.1.15 — Pool recreation, boiler efficiency, tank losses and visible-pipe separation

- Fixed pool recreation availability by redirecting DBH's inherited `Building_FillableThing.Working` check to the RealRim pool fill-state model. A pool is available at 90% fill or above; pawn-specific temperature selection remains active.
- Added instantaneous evaporation heat loss to the pool inspection text and replaced the pool information description with a non-technical explanation of every status entry.
- Made boiler conversion efficiency explicit: electric 98%, chemfuel 90%, and wood 85%. Gross fuel-energy values are used so existing realistic useful-energy accounting is retained.
- Added ambient standing heat loss to domestic hot-water tanks and heating buffer tanks using a 2.5 W/K whole-tank heat-loss coefficient.
- Offset only the visible pipe sprites for all five networks; hidden pipe sprites remain centered.
- Confirmed that the bathtub is a hygiene fixture rather than a standalone joy giver. Its normal bathing job still grants joy, rest and comfort according to DBH's existing behavior.
- Updated release metadata and runtime version labels to 1.1.15.

## 1.1.14 — Network colors, wind pump restoration and fixture availability

- Changed the hot-water overlay to red and the heating overlay to orange; fresh water remains blue.
- The definition patcher now explicitly preserves DBH's `CompWindPump` and recreates its exact DBH component properties if another definition pass removed them, restoring the propeller, wind-path obstruction checks and wind-scaled pump output.
- Added DBH's roof/tree wind-path obstruction reason to the RealRim pump inspection text.
- Require the fresh-water network to contain enough water for the next fixture use before DBH offers a sink, shower, toilet or bath job, even when stored hot water is still available.
- Updated release metadata and runtime version labels to 1.1.14.

## 1.1.13 — Load-time inspection and overlay separation

- Trimmed optional status-reason lines before returning inspection text, preventing RimWorld's trailing-whitespace error when a heat source or similar component is selected before the first game tick.
- Removed DBH's circular floor-connector markers from all five fluid-network overlays while retaining the linked pipe-line atlas.
- Applied a distinct diagonal sub-tile offset to each network so fresh water, hot water, heating, waste water and coolant remain separately visible on shared fixtures and pipe routes.
- Updated release metadata and runtime version labels to 1.1.13.

## 1.1.12 — Unity color API build fix

- Replaced the unavailable `Color32.ToColor` extension calls in `FluidNetworkVisuals` with direct `UnityEngine.Color` construction.
- Fixed all five CS1061 errors when building against the supplied RimWorld/Unity assemblies.
- Updated release metadata and runtime version labels to 1.1.12.

## 1.1.11 — DBH-style fluid network overlays

- Removed the custom `GenDraw.DrawLineBetween` network renderer introduced in 1.1.9.
- Reimplemented all five fluid overlays with DBH's linked pipe-atlas, section-layer and connector-base rendering approach.
- Reused DBH's `PipeOverlay_Atlas`, connector-base texture and meta-overlay shader with distinct colors for fresh water, hot water, heating, waste water and coolant.
- Disabled DBH's legacy pipe section layer so waste-water and coolant overlays are not drawn twice.
- Pipe overlays now appear while placing a matching fluid building or pipe and while selecting any connected fluid node.
- Marked fluid overlay meshes dirty when valves change state so their displayed connectivity updates immediately.
- Updated release metadata and runtime version labels to 1.1.11.

## 1.1.10 — C# 7.3 build compatibility fix

- Replaced a target-typed conditional expression in `CompFluidNode.PostDrawExtraSelectionOverlays()` with an explicit C# 7.3-compatible branch.
- Fixed CS8957 when building the project with its configured `<LangVersion>7.3</LangVersion>`.
- Updated release metadata and runtime version labels to 1.1.10.

## 1.1.9 — pump selection and network-overlay fix

- Suppressed DBH's obsolete pump inspection and selection-overlay methods, preventing null-reference errors when selecting the wind pump after the RealRim network replacement.
- Removed the permanently drawn colored building-to-network anchor lines introduced in 1.1.6.
- Replaced translucent cell-edge highlighting with opaque, color-coded center-line network overlays for fresh water, hot water, heating, waste water and coolant.
- Added anticipated pipe connections to the placement overlay, including the currently previewed pipe cell.
- Updated release metadata and runtime version labels to 1.1.9.

## 1.1.8 — linked-pipe save-load graphics fix

- Fixed fresh-water, hot-water and heating pipe definitions declaring `Graphic_Linked` as their base graphic class. RimWorld requires `Graphic_Single` with `linkType` metadata and creates the linked wrapper internally.
- Prevented `Graphic_Linked.MatSingle` null-reference errors while existing 1.1.6/1.1.7 pipe instances are printed immediately after loading a save.
- Save data is unchanged; existing RealRim pipe instances continue using the same `ThingDef` identifiers.
- Updated release metadata and runtime version labels to 1.1.8.

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
