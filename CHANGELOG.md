# Changelog

## 1.1.81 — Restrict fluid-layer work scanning

- Replaced the fluid-layer work giver's whole-map `AllThings` scan with enumeration of only active `RealRim_ChangeFluidLayer` designations.
- Cached the layer-change `DesignationDef` and `JobDef` after their first successful lookup.
- Cached the target fluid-node component for the lifetime of each layer-change job driver.
- Preserved designation, reservation, reachability, work duration and layer-application behavior.
- Updated release metadata and runtime labels to 1.1.81.

## 1.1.80 — Event-driven construction tracking and grouped system ticks

- Replaced the every-tick map-wide `Frame` and `Blueprint` construction-plan scan with a verified `PlaceWorker.PostPlace` capture hook attached to every RealRim fluid-node definition.
- Retained construction-layer save compatibility and added a low-frequency stale-plan cleanup that checks only the recorded construction cells once every 600 ticks.
- Reworked floor-heating scheduling so one representative per heating-network/room group performs the group update instead of invoking every floor-heating tile every 60 ticks.
- Builds floor-heating groups in a single pass and refreshes them after topology changes or after a 250-tick cache window, preserving the original first-tile execution order among other fluid components.
- Supplies the map component's cached rainwater collectors directly to roof-tile assignment, removing the remaining whole-map thing scan from normal fluid-system ticks.
- Kept all Verse, RimWorld, and Unity object access on the main thread; no unsafe background simulation was introduced.
- Updated release metadata and runtime labels to 1.1.80.

## 1.1.79 — Runtime hot-path optimization

- Restricted construction-plan tracking to RimWorld `Frame` and `Blueprint` objects instead of scanning every thing on every map tick, while preserving per-tick capture so fast construction cannot lose replacement metadata.
- Cached topology-derived fluid tickables, heating networks, heat-exchange networks, component groups, and water-source lists until the topology changes.
- Removed repeated LINQ allocations and repeated component discovery from fluid storage, heat transfer, coolant, wastewater, and treatment operations.
- Cached fresh-water route graphs and longest-route results for pumps for the lifetime of each network topology.
- Reworked rainwater roof-tile assignment from repeated collector-to-collector searches to a single deterministic nearest-collector pass.
- Cached reflection metadata used by DBH recipe closures, comfort-stat handling, pools, and wind pumps.
- Cached sprinkler power components, effect definitions, and spray geometry for one fluid simulation interval.
- Kept all simulation and cache mutation on the main thread. The reviewed hot paths depend on live Verse, RimWorld, and Unity objects, so background processing would add synchronization and stale-state risks inconsistent with the stability priority.
- Updated release metadata and runtime labels to 1.1.79.

## 1.1.78 — Preserve hidden pipe transparency

- Restored zero-alpha rendering for stuffed hidden fluid pipes and floor heating.
- Material selection and pipe replacement remain available without exposing concealed pipe graphics.
- Updated release metadata and runtime labels to 1.1.78.

## 1.1.77 — Add composite pipe plastic support

- Added Rimefeller Synthamide Composite (`FiberComposite`) to the `RealRim_PipePlastic` stuff category at runtime.
- Updated release metadata and runtime labels to 1.1.77.

## 1.1.76 — Expand pressure-pipe materials and replacement

- Converted fresh-water, hot-water and heating pipes from fixed steel cost to stuffed construction using one unit of metallic or pipe-plastic material.
- Added a pipe-plastic stuff category and adds Rimefeller Synthylene to that category when Rimefeller is loaded.
- Applies the same metallic/pipe-plastic material support to DBH air-con coolant pipes at runtime.
- Allows pipes to be built over matching same-layer pipes to atomically replace material, visible/hidden form, or both.
- Removes the replaced old pipe when the new pipe completes.
- Increased hidden non-waste pipe hit points to 160 and work to 2000 while keeping material cost equal to visible pipes.
- Updated release metadata and runtime labels to 1.1.76.

## 1.1.75 — Redirect DBH washing-machine water status

- Patched DBH washing-machine working checks so legacy `Nowater`/`Nosewage` failures are re-evaluated against RealRim fresh-water and waste-water networks.
- Removed stale DBH `No water capacity`/`No sewage capacity` lines from washing-machine inspect text when the machine has a RealRim fluid node.
- Added a clear RealRim message for empty connected fresh-water tanks.
- Updated release metadata and runtime labels to 1.1.75.

## 1.1.74 — Link thermostats to smart mixing valves

- Removed RealRim heating/coolant fluid-node connections from the DBH thermostat.
- Added a room-thermostat link target component to DBH thermostats.
- Smart mixing valves can now link to a room thermostat from the valve gizmo menu.
- Smart mixing valves can unlink their thermostat from the valve gizmo menu.
- In room-temperature mode, linked valves measure the linked thermostat room instead of the valve room.
- Selecting a thermostat or linked smart mixing valve draws a faint link overlay between them.
- Updated release metadata and runtime labels to 1.1.74.

## 1.1.73 — Add remaining DBH fluid consumer nodes

- Added RealRim fluid nodes to remaining DBH buildings that still used legacy DBH plumbing directly.
- Washing machines now connect to fresh-water and waste-water networks while keeping their DBH washing-machine behavior.
- Hot tubs now connect to the fresh-water network while keeping their DBH hot-tub behavior.
- Standalone DBH thermostats now expose heating and coolant network connections so they no longer remain on the old plumbing graph only.
- Updated release metadata and runtime labels to 1.1.73.

## 1.1.72 — Add Rimefeller fresh-water integration

- Added a public RealRim fresh-water API for other mods to query, draw from, and add to connected fresh-water networks.
- Added fresh-water nodes to Rimefeller crude crackers and refinery buildings when those defs are present.
- Patched Rimefeller refinery and crude-cracker water consumers so DBH-style water demand can be satisfied from RealRim fresh-water tanks.
- Added Rimefeller fresh-water status to affected building inspect strings.
- Updated release metadata and runtime labels to 1.1.72.

## 1.1.71 — Raise rainwater collector draw layer

- Moved the wall-mounted rainwater collector to the `BuildingOnTop` altitude layer so its graphic renders above the underlying wall.
- Added DBH `Plumbing` as a prerequisite for rainwater collection research.
- Updated release metadata and runtime labels to 1.1.71.

## 1.1.70 — Fix rainwater collector wall placement

- Made the rainwater collector a non-edifice wall attachment again so it can be placed over an existing wall instead of trying to occupy the wall cell as a second wall.
- Added explicit placement-over-wall support for valid roof-holding impassable walls.
- Kept the custom wall and constructed-roof validation rules.
- Updated release metadata and runtime labels to 1.1.70.

## 1.1.69 — Rework rainwater collector mounting and area

- Increased the rainwater collector catchment area from 5 × 5 to 7 × 7.
- Changed the rainwater collector to be wall-mounted: it must now be placed in a wall tile under a constructed roof.
- Scaled the in-world rainwater collector graphic down to half-tile size and kept it centered on the wall tile.
- Added the collection-area overlay to the selected rainwater collector, not only while placing the blueprint.
- Updated release metadata and runtime labels to 1.1.69.

## 1.1.68 — Add rainwater collector graphic

- Added the dedicated rainwater collector building texture.
- Updated the rainwater collector building and architect icon to use the new graphic instead of the placeholder plumbing/valve art.
- Updated release metadata and runtime labels to 1.1.68.

## 1.1.67 — Show rainwater collection area while placing

- Added placement ghost overlays for the rainwater collector's 5×5 collection area.
- Highlighted collectable constructed roof tiles inside the placement area separately from the full catchment rectangle.
- Shared the collector area calculation between runtime collection logic and placement preview.
- Updated release metadata and runtime labels to 1.1.67.

## 1.1.66 — Add rainwater collection

- Added buildable rainwater collectors in the Hygiene architect menu.
- Added tribal rainwater collection research with 200 base cost in the Dubs Bad Hygiene research tab.
- Rainwater collectors require a constructed roof under or next to the collector; natural cave and mountain roofs do not count.
- Each collector gathers rain from constructed roof tiles in a 5 × 5 area and feeds connected fresh-water tanks directly without a pump.
- Overlapping collection areas assign each roof tile to the closest collector so roof tiles are not double-counted.
- Added inspection status explaining assigned roof area, rain intensity, collection rate, tank acceptance and overflow/rejection.
- Updated release metadata and runtime labels to 1.1.66.

## 1.1.65 — Color-code fluid layer gizmos

- Added network-colored fluid layer selector icons for fresh water, hot water, heating water, waste water and coolant.
- Architect-menu layer selectors, selected-node layer-change gizmos and layer-connector build icons now use the icon matching their fluid network.
- Multi-network buildings now show visually distinct layer-change gizmos instead of several identical layer-stack icons.
- Updated release metadata and runtime labels to 1.1.65.

## 1.1.64 — Harmonize gizmo icon framing

- Cropped and re-centered all RealRim UI gizmo/icon textures to reduce unused transparent space.
- Harmonized padding across the full UI icon set so RimWorld displays them at a more consistent visual size.
- Normalized the floor-heating architect icon to the same 512×512 centered layout used by the other UI icons.
- Updated release metadata and runtime labels to 1.1.64.

## 1.1.63 — Fix fluid layer-change job loop
- Fixed layer-change jobs failing immediately because the job driver checked `FailOnCannotTouch` before the pawn had walked to the hidden pipe.
- Moved the touch checks onto the work toil after `GotoThing`, matching the pattern used by other RealRim manual work jobs.
- Removed the stale temporary claim state from the previous masking workaround.
- Updated release metadata and runtime labels to 1.1.63.

## 1.1.62 — Restore fluid layer-change work

- Removed the temporary layer-change job claim gate from work discovery, manual forced work and job creation.
- Kept the hidden-pipe reach fix from 1.1.59, so layer changes still use adjacent touch access instead of same-cell touch.
- Clear stale layer-change claims when a layer change is requested.
- Updated release metadata and runtime labels to 1.1.62.

## 1.1.61 — Explain floor-heating comfort

- Added floor-heating comfort bonus lines to the furniture Comfort stat explanation.
- The explanation now reports whether the bonus is active or why it is not applied.
- Updated release metadata and runtime labels to 1.1.61.

## 1.1.60 — Fluid layer and smart valve build fixes

- Fixed the layer-change job driver for RimWorld's non-virtual JobDriver.Cleanup implementation.
- Fixed smart mixing valve layer routing so the selected source network is not captured as an out parameter inside a lambda.
- Updated release metadata and runtime labels to 1.1.60.

## 1.1.59 — Fluid layer change job stability

- Prevented repeated same-tick layer-change job assignment when a layer-change target cannot immediately start or keep its job.
- Layer-change jobs now claim their target temporarily before job creation and release the claim after success.
- Layer-change jobs now use closest-touch pathing, which is safer for hidden pipes under walls or other structures.
- Updated release metadata and runtime labels to 1.1.59.

## 1.1.58 — Fluid layer connectors and smart mixing valve layer support

- Added buildable layer connectors for fresh water, hot water, heating water, waste water and coolant networks.
- Layer connectors join adjacent same-fluid nodes across any pipe layer without collapsing same-tile parallel pipes.
- Smart mixing valves now store selectable source and receiving heating-water layers and only draw from/charge adjacent networks on those selected layers.
- Layer connectors are shown in the selected layer overlay and in node inspection text.
- Updated release metadata and runtime labels to 1.1.58.

## 1.1.57 — Fluid layer selector graphic

- Added a dedicated layer-stack texture for fluid layer selection gizmos.
- Updated the architect-menu layer selectors and selected-node layer gizmos to use the new texture.
- Changing a selected node layer from the node gizmo now queues the layer-change construction job on every selected compatible node, not only the first selected node.
- Updated release metadata and runtime labels to 1.1.57.

## 1.1.56 — Layer overlay refresh fix

- Fixed fluid-network grid overlays becoming empty after layer filtering when the selected designator or selected node changed.
- Overlay section layers now rebuild their mesh when the displayed fluid layer changes, so existing layer-1 networks are shown again when selected.
- Updated release metadata and runtime labels to 1.1.56.

## 1.1.55 — Fluid network construction layers

- Added five construction layers per fluid-network type for fresh water, hot water, heating water, waste water and coolant.
- Added architect-menu layer selectors that control the layer assigned to newly placed fluid-network pipes and buildings.
- Nodes now connect only to same-type nodes on the same layer, allowing separate pipes on the same tile or adjacent tiles.
- Added node gizmos to queue layer changes; a pawn performs a short no-resource construction job before the layer is changed and networks are rebuilt.
- Added layer-aware placement validation, overlays and inspection text.

## 1.1.54 — Smart mixing valve room-temperature control

- Added selectable smart mixing valve control modes for receiving-water temperature or monitored-room temperature.
- Smart mixing valves now store separate water and room targets, expose mode-specific target gizmos, and save the selected control mode.
- Room-temperature mode opens the valve only while the room containing the valve is below the selected target temperature; otherwise it closes.
- Updated smart mixing valve status text and release metadata to 1.1.54.

## 1.1.53 — Heating report provider interface

- Replaced the ad-hoc external heating report cache with network-owned component lists.
- Every fluid network now exposes connected things, connected thing comps and heating report providers through its central `FluidNetwork` data structure.
- Added `IFluidNetworkComponent`, `IHeatingNetworkReportProvider` and `HeatingNetworkReport` as the direct integration API for heat producers.
- The heating overview now queries provider interfaces instead of special-casing external report records.
- Removed `FluidUtility.recordHeatingNetworkReport`; external producers now supply report data through their component interface.
- Updated release metadata and runtime labels to 1.1.53.

## 1.1.52 — External heating producer API

- Added a public heating-network API for optional mods to inject heat and publish per-building report details without depending on Water & Pumps at compile time.
- Heating network reports can now show external heat producers, their accepted network output and custom integration details.
- Heating energy added through the public API now charges the virtual pipe buffer as well as physical heating buffer tanks.
- Updated release metadata and runtime labels to 1.1.52.

## 1.1.51 — Virtual heating pipe buffer

- Added a virtual heat buffer to every heating network based on heat-bearing pipe length.
- Heating pipes, heating valves and floor-heating loops now contribute 2 L of virtual heating-water volume per metre/tile.
- Heating networks can now accept, store and deliver heat without a dedicated heating-water buffer tank when enough pipe volume exists.
- The heating network report now shows virtual pipe-buffer volume and temperature.
- Virtual pipe-buffer temperatures are saved with the map and preserved across network rebuilds by matching overlapping pipe networks.
- Updated release metadata and runtime labels to 1.1.51.

## 1.1.50 — Smart mixing valve graphics and research prerequisite

- Switched the smart mixing valve to the dedicated RealRim mixing-valve texture.
- Added Electricity as a prerequisite for the smart mixing valve research project.
- Updated release metadata and runtime labels to 1.1.50.

## 1.1.49 — Floor-heating build compatibility

- Fixed C# 7.3 build errors in floor-heating status text selection.
- Fixed smart mixing valve network selection by avoiding captured `out` parameters in LINQ predicates.
- Updated release metadata and runtime labels to 1.1.49.

## 1.1.48 — Physical floor heating and smart mixing valve

- Reworked floor heating into an unregulated hydronic heat exchanger. Indoor transfer is now based on the heating-water-to-room temperature delta instead of a hard 21 °C room target.
- Aggregated floor-heating tiles by room/outdoor area per heating network and cached group conductance to reduce per-tile CPU work.
- Added outdoor floor-heating behavior: below 1 °C and only while precipitation is falling, outdoor constructed-floor loops consume heat using a 10 W/m²K snow-melt conductance and remove snow from the heated tiles.
- Removed straw matting as a valid terrain for new floor-heating placement.
- Added the powered smart mixing valve, its 500-point Industrial research project, and runtime logic for transferring heat from a hotter adjacent heating circuit into a cooler receiving circuit at a player-selected target temperature.
- Updated release metadata and runtime labels to 1.1.48.

## 1.1.47 — DBH floor-heating research placement

- Assigned the `floor heating` research project to the Dubs Bad Hygiene research tab.
- Moved it from the off-grid 11.0 × 7.2 coordinates to 4 × 0.5, next to the central-heating branch.
- Updated release metadata and runtime labels to 1.1.47.

## 1.1.46 — Hydronic floor heating

- Added a concealed one-tile hydronic floor-heating building under the Temperature architect category.
- Floor heating requires the new 1000-point industrial `floor heating` research, Central Heating as a prerequisite, Construction skill 8, 600 work and 2 steel per tile.
- Placement is limited to constructed floors and smoothed stone; natural soil, natural rock, water and ice are rejected.
- Each 1 × 1 tile is connected to the heating-water network and represents 1.0 m² of heated floor surface with a plausible 75 W rated output at a 50 °C water-to-room temperature difference.
- Floor-heating tiles heat enclosed rooms from connected heating-water storage and are aggregated in the heating-system report by room.
- Furniture fully standing on floor-heating tiles receives a dynamic +0.10 Comfort bonus. Facility providers are excluded so their bonus cannot stack indirectly onto affected furniture.
- Added the supplied floor-heating architect icon.
- Updated release metadata and runtime labels to 1.1.46.

## 1.1.45 — DBH shower and toilet job null-safety

- Fixed two `NullReferenceException` errors from DBH shower and toilet fail conditions when a saved job resumes for a pawn whose Hygiene or Bladder need is absent.
- Added narrow Harmony guards around the two failing DBH-generated callbacks. Valid jobs retain DBH's original completion logic; invalid stale jobs end as incompletable instead of repeatedly erroring.
- Updated release metadata and runtime labels to 1.1.45.

## 1.1.44 — Swimming-pool pathfinding avoidance

- Made the DBH swimming-pool building and its pool-water terrain effectively impassable to ordinary route selection by assigning a very high path cost.
- Pawns and animals now route around the 9 × 5 pool instead of treating it as a zero-cost shortcut and then suffering the water movement slowdown.
- Kept the pool technically traversable so the dedicated `DBHGoSwimming` job can still enter, move within, and leave the pool normally.
- Updated release metadata and runtime labels to 1.1.44.

## 1.1.43 — Sewage dump port integration and compile fix

- Fixed the sewage-outlet build failure by importing `RimWorld.FilthMaker` from its correct namespace.
- Replaced DBH's inactive sewage dump-port component with a RealRim waste-water network node and discharge component.
- A powered, switched-on dump port ejects waste directly into vacuum when the cell immediately in front of it is exposed to vacuum. Vacuum discharge creates no surface filth and requires no 3 × 5 terrain area.
- In atmosphere, the dump port requires the same complete 3 × 5 natural-soil discharge area as a ground sewage outlet and creates harmless-water or raw-sewage filth according to the network's storage state.
- Dump ports participate in direct untreated discharge and in septic-tank/treatment-plant overflow routing alongside normal sewage outlets.
- Updated release metadata and runtime labels to 1.1.43.

## 1.1.42 — Sewage-outlet atmosphere and terrain requirements

- Sewage outlets now participate in waste routing only while their complete DBH-compatible 3 × 5 discharge area is operational.
- Every discharge-area cell must be natural soil. Floors, rock and gravship substructure invalidate the outlet, while the one-cell building itself may still stand on a gravship or other constructed surface.
- Outlets cannot discharge into vacuum. On asteroid and other space maps, the complete discharge area must additionally be inside one airtight, pressurized room.
- Invalid outlets are excluded before capacity checks, so fixtures cannot silently discard sewage through an unusable outlet. Connected septic tanks and treatment plants continue accepting waste until their independent water or sludge capacity is exhausted.
- Pending filth remains queued while an outlet is invalid and resumes only after the terrain and atmospheric requirements are restored. The inspect text reports the exact inactive reason.
- Filth is now confined to the same 3 × 5 discharge area shown by DBH's placement overlay.
- Updated release metadata and runtime labels to 1.1.42.

## 1.1.41 — Functional sewage outlet and overflow routing

- Replaced DBH's inactive sewage-outlet component with a RealRim waste-water node and discharge component.
- Waste-water networks without a septic tank or treatment plant now require a connected sewage outlet; the complete waste-water and fecal-sludge stream is released there as raw-sewage filth.
- When storage is connected, waste water and fecal sludge are accepted independently. Water beyond the aggregate liquid capacity overflows through connected outlets as short-lived standing-water filth, while solids continue to accumulate in available sludge capacity.
- Raw-sewage contamination occurs only after the aggregate fecal-sludge capacity is exhausted. A full liquid volume alone therefore causes harmless water overflow rather than sewage contamination.
- Multiple connected outlets share discharge evenly. Pending fractional filth and cumulative harmless/untreated discharge totals are saved and shown in the outlet inspect text.
- Updated the standing-water description so the same filth can represent simple-shower runoff and harmless sewage overflow.
- Updated release metadata and runtime labels to 1.1.41.

## 1.1.40 — Fresh-water-only simple shower

- The DBH simple shower now connects only to the fresh-water network. It no longer has hot-water or waste-water connectors and does not require drain capacity.
- Increased simple-shower demand from 0.13 L to 0.20 L per shower work tick, corresponding to about 100 L for a full wash instead of the modern shower's roughly 65 L.
- The simple shower deliberately uses 12 °C fresh water and reports the use as cold to DBH's shower job, while avoiding a misleading "no hot water" fixture error because hot water is not a requirement. RealRim's temperature-preference thoughts remain active.
- Added short-lived standing-water filth under the open shower. One filth thickness is produced per 20 L used, up to five layers, representing the lack of a basin or drain.
- Retained DBH's existing primitive shower-head graphic, pawn shower job, washing effect, sound and steam animation.
- Updated release metadata and runtime labels to 1.1.40.

## 1.1.39 — Pipe heat exchange and hot-water report

- Heating-water and domestic hot-water pipe cells now exchange heat bidirectionally with outdoor air. Heat transfer scales linearly with connected pipe length and the live network-to-outdoor temperature difference.
- Domestic hot-water pipe uses 0.35 W/(m·K); the closed heating supply-and-return circuit uses 0.70 W/(m·K). Visible pipes, hidden pipes and valves contribute one metre per occupied cell, with overlapping infrastructure counted once.
- Pipe exchange draws from or adds to the actual connected storage tanks without crossing outdoor temperature or each tank's configured 5–85 °C limits.
- The heating report now includes average network temperature, outdoor temperature, aggregate pipe conductance and signed pipe exchange; pipe loss/gain is included in its production, consumption and net totals.
- Added a live domestic hot-water report with stored volume, temperature, heating input, standing loss, pipe exchange, hot-water delivery, cold refill, net useful-heat rate and room-grouped connected nodes.
- Added the supplied hot-water report graphic as the new report command icon.
- Updated release metadata and runtime labels to 1.1.39.

## 1.1.38 — Sprinkler compile fix

- Fixed the DBH irrigation-grid reflection signature check by qualifying `MapMeshFlagDef` with its `RimWorld` namespace.
- No runtime sprinkler behavior or water-use values changed.
- Updated release metadata and runtime labels to 1.1.38.

## 1.1.37 — Fresh-water sprinkler integration

- Replaced DBH's irrigation- and fire-sprinkler runtime components with RealRim implementations connected to the fresh-water network.
- Irrigation now consumes one daily volume based on the unobstructed spray footprint: 5 L/m², up to 725 L for the full 145 m² radius. It drives DBH's existing irrigation fertility grid with the original repeated pulse cadence while charging the daily water volume only once.
- Fire sprinklers now consume water continuously at a constant application density, up to 80 L/min at maximum radius. Flow conversion uses the mod's 1.44 in-world seconds per game tick, and spraying stops immediately when the network cannot supply the next interval.
- Fire triggering checks every unobstructed protected cell for fire and retains DBH's 100 °C emergency trigger, indoor cooling behavior and manual-use job.
- Reimplemented radius controls, placement checks, matching growing-zone creation and manual triggering while retaining DBH's building graphics, research, rotating spray fleck, looping sprinkler sound and irrigation overlay.
- Added live English and German sprinkler status text with coverage, demand, current flow and cumulative water use.
- Updated release metadata and runtime labels to 1.1.37.

## 1.1.36 — Kitchen-sink multi-stove status fix

- The kitchen sink now reads its total linked-stove count from RimWorld's authoritative `CompFacility.LinkedBuildings` list.
- Active cooking is tracked separately for each linked stove over a short rolling activity window instead of requiring every stove callback to occur during the same game tick.
- The inspect text now reports linked and active stoves separately. Current water load and captured-food-solids rates update from the active count, so two operating stoves report 24 L/h and 150 g/h.
- Updated release metadata and runtime version labels to 1.1.36.

## 1.1.35 — Use Odyssey's actual Job swimming-pose flag

- Corrected the Odyssey integration after decoding `JobDriver_GoSwimming.CheckForSwimmingPose()`.
- The vanilla method does not assign an animation or a field on `Pawn`; it writes a private boolean field on the active `Verse.AI.Job` based on whether the pawn stands in water.
- Resolves that exact job field from the vanilla method IL and sets it while a pawn is inside the DBH swimming-pool footprint.
- Removed the `Pawn.Swimming` getter override, allowing RimWorld's normal getter and render pipeline to consume the same job state used by vanilla river and sea swimming.
- Clears the job flag when the pool-swimming job finishes.
- Updated release metadata and runtime labels to 1.1.35.

## 1.1.34 — Odyssey swimming pose field fix

- Replaced the incorrect AnimationDef-based pool integration after diagnostics confirmed that Odyssey swimming has no dedicated AnimationDef.
- Resolves the exact private Pawn boolean field written by `JobDriver_GoSwimming.CheckForSwimmingPose()` and sets that field while a pawn is inside a swimming pool.
- Clears the field when the DBH swimming job finishes.
- Removed the active renderer-animation overrides and startup diagnostic dump.
- Updated release metadata and runtime version labels to 1.1.34.

## 1.1.33 — Odyssey swimming diagnostics

- Added a one-time diagnostic report when Odyssey is active.
- Logs every loaded `AnimationDef`, all fields on `AnimationDefOf`, relevant swimming/pool/bath/water `JobDef` entries and their driver classes, swimming-related runtime job-driver types, the vanilla `JobDriver_GoSwimming` fields and methods, raw IL for `CheckForSwimmingPose`, and animation-related renderer/render-tree members.
- Adds a one-time runtime snapshot for each pawn entering a pool, including its active job driver, pool position test, renderer animation, render-tree animation tick and animation-related backing fields.
- Splits the report into bounded log chunks so it survives Unity log limits.
- Updated release metadata and runtime version labels to 1.1.33.

## 1.1.32 — Resolve Odyssey swimming animation from vanilla IL

- Replaced the incorrect AnimationDefOf field-name search, which found no field because Odyssey's swimming animation is stored under a non-swimming field name.
- Resolves the exact AnimationDef operand used by RimWorld's own JobDriver_GoSwimming.CheckForSwimmingPose method.
- Downgraded failure to a warning and leaves the pool recreation functional if a future RimWorld build changes the vanilla method body.
- Updated release metadata and runtime version labels to 1.1.32.

## 1.1.31 — Forced Odyssey pool animation state

- Replaced the transient swimming-animation assignment with renderer getter patches that keep Odyssey's swimming `AnimationDef` active throughout the DBH pool job.
- Forces `PawnRenderer.CurAnimation` and `HasAnimation` for pawns swimming inside the pool footprint.
- Supplies a continuously advancing, duration-wrapped animation tick through `PawnRenderTree` and prevents the swimming animation from being marked finished.
- Retains Odyssey's swimming graphic state and clears the transient renderer state after the pawn leaves the pool.
- Updated release metadata and runtime labels to 1.1.31.

## 1.1.30 — Direct Odyssey swimming animation assignment

- Removed the ineffective call to `JobDriver_GoSwimming.CheckForSwimmingPose`, whose vanilla water/job checks reject DBH pool tiles.
- Resolves Odyssey's actual swimming `AnimationDef` from `RimWorld.AnimationDefOf` and assigns it directly to the pawn renderer while the pawn is inside a swimming pool.
- Maintains the animation through both DBH movement and waiting toils, and clears it when the pawn leaves the pool or swimming job.
- Retains the pool-only `Pawn.Swimming` state so Odyssey's swimming graphics and apparel handling remain active.
- Updated release metadata and runtime version labels to 1.1.30.

## 1.1.29 — Odyssey swimming animation integration

- Replaced the pool-only `Pawn.Swimming` visual flag with Odyssey's own `JobDriver_GoSwimming.CheckForSwimmingPose` path.
- Applies the vanilla swimming animation after DBH's pool swimming tick, while retaining RealRim's pool recreation and temperature logic.
- Clears the swimming animation when the pawn leaves the pool job or pool footprint.
- Updated release metadata and runtime version labels to 1.1.29.

## 1.1.28 — Swimming recreation and Odyssey pool animation

- Added a dedicated `RealRim_Swimming` recreation type for swimming pools.
- Reassigned DBH's swimming-pool joy giver, swimming job and pool building joy kind from Hydrotherapy to Swimming.
- Bathtubs and hot tubs remain on DBH's existing Hydrotherapy recreation type.
- When Odyssey is active, pawns performing the DBH pool-swimming job are reported as swimming while they are inside the pool footprint, enabling Odyssey's swimming rendering/animation path without affecting their approach or exit walk.
- Updated the pool description and English/German recreation labels.
- Updated release metadata and runtime version labels to 1.1.28.

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
