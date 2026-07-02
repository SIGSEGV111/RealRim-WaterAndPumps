# Known limitations — 1.1.39

1. **Kitchen-sink resource failures**
   - Stove integration uses only RimWorld's existing `CompAffectedByFacilities` link. No proximity or room-based fallback is used.
   - Cooking is not blocked when water, hot water or drain capacity is unavailable; the linked sink reports the resource failure and produces no waste for that work interval.

2. **Sludge hauling**
   - At 10 kg (200 × 50 g units), hauling work is scheduled automatically and a pawn extracts one 10 kg batch.
   - The extracted stack is placed near the pawn; ordinary hauling moves it to a stockpile afterward.

3. **Pathogen scope**
   - Pathogens are represented by existing RimWorld/DBH `HediffDef`s. Disease-specific incubation, immunity and progression remain controlled by those defs.
   - The system models concentration and ingestion dose; it does not yet model pathogen decay over time, boiling, chlorination, or cross-contamination from sewage leaks.


4. **Heating-pipe thermal storage**
   - Heating pipes do not currently add thermal mass. Implementing this correctly requires persistent network-level water volume and energy state, including deterministic handling when networks split or merge. That is not a small, zero-cost change, so it was deliberately deferred.

5. **DBH irrigation-grid bridge**
   - The sprinkler runtime and all water accounting are owned by this mod, but the crop-fertility bonus still uses DBH's `IrrigationGrid` so it remains compatible with DBH's plant-growth patches and overlay.
   - If a future DBH release changes that private grid field or its `AddAtCapped` method, irrigation stops safely, consumes no water, and reports that the grid is unavailable.

6. **Compilation in the generation environment**
   - The source is syntax-, XML- and metadata-validated, but this environment has no .NET SDK and cannot produce or test the DLL.
