# Runtime performance review — 1.1.80

## Dubs Performance Analyzer evidence

The supplied profile showed two relevant inclusive costs:

- `MapComponent_FluidNetworks.captureConstructionPlans`: approximately 2.576 ms per game tick, called once per tick.
- `MapComponent_FluidNetworks.tickSystems`: approximately 0.073 ms amortized per game tick, with approximately 4.481 ms per 60-tick invocation.

Other measured methods from this mod were below approximately 0.001 ms per tick in that recording.

## Construction-plan tracking

The 1.1.79 implementation still enumerated every `Frame` and `Blueprint` on the map every game tick. Version 1.1.80 removes that polling path entirely.

A neutral `PlaceWorker_CaptureFluidLayer` is attached at runtime to each `ThingDef` containing `CompProperties_FluidNode`. Its `PostPlace` callback records the selected fluid layers only when a build designator successfully places the construction. The plan remains keyed by build definition, position, and rotation, so it survives blueprint-to-frame and frame-to-building transitions and remains compatible with plans already saved by earlier versions.

Canceled plans are pruned every 600 ticks. Cleanup scales with the number of outstanding fluid construction plans and checks only each plan's recorded cell; it does not scan map-wide thing registries.

## Fluid-system scheduling

Floor heating was the broadest repeating component set because one component exists per heated floor tile. Although the old group cache prevented duplicate heat transfer, every tile still entered `tickFloorHeating`, resolved its room and network, and periodically caused group reconstruction by scanning network components.

Version 1.1.80 now:

- Builds all floor-heating groups in one pass over the cached floor-heating components.
- Groups by actual `FluidNetwork` and room, with a separate outdoor group.
- Keeps only the first component of each group in the scheduled tickable list.
- Preserves the original component ordering: the representative occupies the position where that group's first floor tile previously ran.
- Rebuilds immediately after fluid topology changes and otherwise every 250 ticks to account for room changes.
- Updates every tile's displayed state and comfort data when its group is processed, as before.

Rainwater assignment now receives the map component's topology-derived collector list. Normal 60-tick simulation no longer scans `map.listerThings.AllThings` to rediscover collectors.

## Threading decision

No simulation was moved to worker threads. The reviewed operations read and mutate live maps, things, rooms, roofs, terrain, weather, temperature grids, networks, power components, and definition-backed state. These objects are not treated as thread-safe by the game. Snapshotting and applying results later would add stale-state, ordering, save/load, and destruction races for a small remaining workload. Main-thread deterministic execution remains the safer design.

## Expected profile change

`captureConstructionPlans` no longer exists in `MapComponentTick`, so its former per-tick cost should disappear. A low-frequency `pruneConstructionPlans` entry may appear, but it performs only cell-local checks for outstanding fluid builds.

`tickSystems` should scale with the number of actual devices plus the number of floor-heating groups, rather than the number of floor-heating tiles. Its occasional group-refresh call still scales linearly with floor-heating tiles once per approximately 250–300 ticks.

Actual timings must be measured in the same save and camera/game-speed conditions after compiling 1.1.80.
