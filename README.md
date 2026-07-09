# [RealRim] Water & Pumps

Physics-oriented replacement layer for **Dubs Bad Hygiene** on RimWorld 1.6.

- Author: SIGSEGV11
- Package ID: `sigsegv11.realrim.water`
- Version: 1.1.71
- Required: Harmony, Dubs Bad Hygiene

This is a clean subsystem replacement. Save compatibility with versions 1.0.x is not intended. Existing DBH `ThingDef` identifiers are reused where practical so DBH buildings can still be loaded and constructed.

## Networks

The old shared plumbing model is replaced by five independent tile networks:

| Network | Purpose |
|---|---|
| Fresh water | Pumps, water tanks, fixtures, troughs and pool filling |
| Hot water | Domestic hot-water delivery from thermal tanks |
| Heating water | Boilers, solar/geothermal collectors, heat pumps, radiators and pools |
| Waste water | Drains, septic tanks and sewage treatment |
| Coolant | Outdoor cooling plants, coolant tanks and indoor cooling units |

The old DBH sewage pipe definitions are reused for waste water. The old air-conditioning pipe definitions are reused for coolant. New steel pipe defs provide fresh-water, hot-water and heating-water pipes and valves. Steel-only construction was an explicit replacement-def choice, not a limitation inherited from DBH. Selecting a node or placing a pipe displays the matching connected network in its own color; the new pipe sprites link only to their own pipe type.

## Water supply

| Building | Nominal flow | Electrical power |
|---|---:|---:|
| Wind pump | 1,500 L/h | none |
| Electric pump | 1,200 L/h | 250 W |
| Pumping station | 50,000 L/h | 10 kW |

Pump flow is reduced over long connected routes. The first 10 m are loss-free. The compact pump uses a 50 m hydraulic reference length; the pumping station uses 250 m to represent its greater pressure head. Each pump now has its own automatic enable toggle plus configurable start and stop storage thresholds. The wind pump additionally uses DBH's verified wind-path obstruction and animation component, so trees, structures and roofs reduce its output.

Water storage capacities:

- water butt: 100 L;
- small water tank: 2,000 L;
- small water tower: 8,000 L;
- large water tower: 50,000 L.

Fresh water enters the system at 12 °C.

## Water contamination and pathogens

Every physical water source receives a hidden, deterministic contamination profile when it is created. The profile is persisted with the source. A source repeatedly rolls its category chance and adds another unique pathogen while the roll succeeds:

| Source | Chance per additional pathogen | Per-liter infection risk |
|---|---:|---:|
| Regular well | 10% | 0.015–0.125% |
| Deep well | 3% | 0.015–0.125% |
| Surface water | 20% | 0.050–0.300% |
| Mud, marsh, bog or flood water | 40% | 0.150–0.750% |
| Cryogenic extraction | clean | 0% |

The repeated roll is geometric: most wells are clean, while multiple pathogens become progressively rarer. Connected sources are mixed by contribution weight. Tanks preserve the volume-weighted concentration of every pathogen as water arrives and leaves.

When a flesh pawn or animal drinks, each pathogen is rolled independently using the consumed dose:

```text
infection chance = 1 - (1 - risk per liter) ^ liters consumed
```

The pathogen list includes the verified DBH waterborne diseases and compatible RimWorld diseases such as food poisoning, gut worms, muscle parasites, malaria, sleeping sickness, flu, plague and mechanites. Already-present diseases are not added a second time. Exact source and tank profiles are visible only in developer mode.

A powered `WaterTreatment` on the same fresh-water network removes 99% of every pathogen from delivered water. Stored tank concentrations remain physically traceable; treatment is applied when water is drawn through the network.

## Heating and hot water

Domestic hot water and heating water now use separate storage:

- the **domestic hot-water tank** stores 600 L, connects to fresh water, hot water and heating water, and contains a one-way internal heat exchanger;
- the **heating-water buffer tank** stores 600 L on the closed heating circuit for boilers, heat pumps, radiators and pools. It is optional once the pipe network itself provides enough virtual water volume.
- both physical tank types lose heat to their surroundings at a whole-tank conductance of 2.5 W/K.

Both tanks operate over 5–85 °C. The domestic tank refills with 12 °C fresh water and can only receive heat through its exchanger; domestic water cannot feed heat back into the heating circuit.

The connected pipework is also a distributed heat exchanger with outdoor air. One occupied pipe or valve cell is treated as one metre. Domestic hot-water pipe contributes 0.35 W/K per metre; the closed heating circuit contributes 0.70 W/K per metre because each tile represents both supply and return. Heat transfer is bidirectional, proportional to pipe length and the current network-to-outdoor temperature difference, and cannot drive stored water past the outdoor temperature or the tank's 5–85 °C limits. Hidden and visible pipes use the same insulated-pipe coefficient.

Every heating network additionally has a virtual pipe buffer based on its heat-bearing pipe length. Heating pipes, heating valves and floor-heating loops contribute 2 L of virtual heating water per metre/tile, so a closed heating circuit can accept, store and deliver heat without a dedicated physical buffer tank. The virtual buffer is mixed into the network's average heating-water temperature, participates in all heat-source/consumer/mixing-valve calculations, is saved with the map, and is preserved across network rebuilds by matching overlapping pipe networks.

Active heat sources have an individual heating-buffer target, adjustable from 30–85 °C. They stop at the selected target and restart 5 °C below it; existing saves initially retain the previous 75 °C target. Lower buffer targets reduce the temperature lift required from heat pumps and normally improve COP, but the target must still remain high enough to drive radiators, pool heating and domestic-hot-water transfer. Solar and geothermal sources are passive and continue to the tank maximum.

Selecting any node on a heating network provides a **Heating overview** command. Its live report shows current production, current consumption, net storage rate, average network temperature, outdoor temperature, connected pipe length, aggregate pipe conductance, signed pipe heat exchange, and all functional nodes grouped by room. Pipe losses are included in total consumption; heat gained from hotter outdoor air is included in production.

Selecting any node on a domestic hot-water network provides a **Hot-water overview** command using the dedicated hot-water report icon. The live report shows stored volume, average temperature, heating-circuit input, tank standing loss, pipe heat exchange, current delivery flow and useful-heat draw, cold-water refill, net heat rate, pipe length, and connected tanks and fixtures grouped by room. Bedrooms and barracks in both reports use the names of bed owners where available.

| Heat source | Nominal thermal output | Input model |
|---|---:|---|
| Wood boiler | 20 kW | 85% conversion efficiency; about 4,860 kJ useful heat per 400 g wood unit |
| Chemfuel boiler | 24 kW | 90% conversion efficiency; 1,935 kJ useful heat per 50 g chemfuel unit |
| Electric boiler | 12 kW | 98% conversion efficiency; about 12.24 kW electrical at full output |
| Solar collector | up to 5.5 kW | proportional to sky glow; blocked by a roof |
| Geothermal heater | 12 kW | no fuel |
| Air-to-water heat pump | up to 12 kW | dynamic COP; stops below -20 °C |
| Coolant-to-water heat pump | up to 12 kW | transfers recovered heat from coolant storage |

Radiator output follows a ΔT-based exponent curve rather than a fixed room-temperature change:

- 1 m radiator: 1.5 kW at the reference temperature difference;
- 2 m radiator: 3.0 kW;
- towel rail: 1.2 kW.

Floor heating is an unregulated hydronic heat exchanger rather than a thermostat. Indoor tiles are grouped by room and heating network, then cached and processed as one exchanger using 5 W/m²K against the current room temperature. Outdoor tiles are grouped per heating network and only activate below 1 °C while rain/snow is falling; they use 10 W/m²K against outdoor air and spend that heat on snow removal instead of warming the map. Straw matting is intentionally excluded from new floor-heating placement.

The smart mixing valve is a powered 5 W thermostatic bridge for two adjacent heating-water circuits. It does not merge the circuits into one network; instead it reads adjacent heating networks, treats the hotter side as the source, and transfers up to 12 kW into the cooler receiving side until the selected receiving-water temperature is reached or the source water is no longer hot enough.

## Pools

The DBH pool is modeled as:

- 65,000 L water volume;
- 45 m² exposed surface;
- 0.6 m² heating heat exchanger;
- adjustable temperature target;
- fresh-water refill at 12 °C;
- convection with room/outdoor air;
- evaporation with latent heat loss;
- lower indoor evaporation;
- solar gain outdoors during daylight;
- night-sky radiation outdoors;
- rain collection at outdoor-air temperature.

DBH's existing pool fill-flow controls now directly scale the RealRim refill rate, and the original water-surface graphic is synchronized to the physical 65,000 L volume.

Pawns can use a pool only above 90% fill and within their ungarbed comfortable-temperature range plus a 5 °C allowance. Optional `VTE_ColdInclined` and `VTE_HeatInclined` traits extend the corresponding comfort limit by 10 °C and provide positive cold/hot-water mood effects. Pool selection prefers the pawn's temperature preference and then distance.

## Sprinklers

DBH's irrigation and fire-sprinkler buildings are retained, including their graphics, research, rotating spray fleck, sound and irrigation-fertility overlay, but their old plumbing and runtime components are replaced.

Both sprinkler types connect to the fresh-water network and expose an adjustable 1.9–6.9 m radius. Walls block both the effective area and the associated water demand.

- **Irrigation sprinkler:** runs once during the local 05:00 hour and replenishes DBH's temporary irrigation fertility bonus on unobstructed fertile cells using the original repeated pulse cadence. It uses 5 L/m² over the unobstructed spray footprint per daily cycle, equivalent to a 5 mm water application. A full 145 m² circle therefore consumes 725 L once per morning; the DBH fertility bonus itself is applied only to fertile cells. The spray animation and fertility pulses continue for one in-game hour (2,500 ticks) without repeatedly charging the network. The original command for creating a growing zone matching the irrigated area is retained.
- **Fire sprinkler:** detects fire anywhere in its unobstructed protected area or activates above 100 °C. It can also be triggered manually. Flow follows a constant area density and reaches 80 L/min over the full 145 m² radius; water is drawn for each 60-tick accounting interval using the mod's 1.44 in-world seconds per tick, and suppression stops when the fresh-water network cannot supply the next interval. Indoors, the spray retains DBH's emergency cooling behavior so the 100 °C trigger can clear instead of running indefinitely.

The inspect panel reports actual unobstructed coverage, calculated demand or design flow, current flow, the last irrigation volume and cumulative water use.

## Air conditioning

The outdoor unit charges a new 600 L phase-change coolant tank. Indoor air-conditioners and freezer units draw stored cooling energy and use electricity only while transferring heat.

- outdoor unit: 12 kW nominal cooling with dynamic COP;
- indoor unit: 3.5 kW cooling, 100 W fan;
- freezer unit: 8 kW cooling, 250 W fan;
- air-to-water and coolant-to-water heat pumps: commercial-scale 12 kW transfer class.

## Fixtures and thirst

Fixtures require fresh water. Sinks, modern showers and baths also use hot water and report cold-water fallback. Toilets, sinks, modern showers and baths require waste-water capacity. Fountains and troughs do not require a drain.

The **simple shower** is intentionally different: it connects only to fresh water, cannot use hot water, and has no waste-water connection. Its unrestricted primitive head uses 0.20 L per shower work tick—about 100 L for a full wash, compared with roughly 65 L for the modern shower. Because it has no basin or drain, every 20 L used creates one layer of short-lived standing-water filth beneath the shower, up to five layers.

The kitchen sink behaves as a normal basin and connects to fresh water, hot water and waste water. It also accounts for active stove use continuously. Each cooking stove linked to it through RimWorld's facility system contributes 12 L/h of mixed sink water and wastewater plus 0.075 kg/h of captured food solids while cooking. Only the authoritative `CompAffectedByFacilities` link is used; unlinked sinks are never selected by proximity.

Default biological water requirements:

- humanoid pawns: `BodySize × 3 L/day`;
- animals: `BodySize × 40 L/day`;
- internal reserve: two days;
- normal drinking frequency: approximately three times per day.

A race or pawn kind can override both values with `RealRim.WaterAndPumps.WaterRequirementExtension`:

```xml
<modExtensions>
	<li Class="RealRim.WaterAndPumps.WaterRequirementExtension">
		<liters_per_day>12</liters_per_day>
		<internal_capacity_liters>24</internal_capacity_liters>
	</li>
</modExtensions>
```

## Waste water

The septic tank stores 12,000 L of waste water and 600 kg of sludge. It infiltrates up to 1,200 L/day only when its entire footprint is on natural soil and it is not aboard a gravship.

The sewage treatment plant stores 50,000 L, processes up to 5,000 L/day and returns up to 95% to a connected fresh-water network. The spacer recovery system stores 20,000 L, processes 10,000 L/day, returns up to 98%, and uses 1.5 kW while operating.

The sewage outlet is the terminal discharge point for a waste-water network. Its DBH-compatible 3 × 5 discharge area extends forward from the building and must consist entirely of natural soil; floors, rock and gravship substructure make the outlet inactive. The building itself may stand on a gravship as long as the full area in front is natural soil. The outlet never works in vacuum. On asteroid and other space maps, all 15 discharge cells must also belong to one airtight, pressurized indoor room. The inspect text reports the current operating condition.

If no septic tank, treatment plant or recovery system is connected, every litre of waste water and all fecal sludge are discharged untreated as raw sewage within that area. If storage exists, liquid and sludge capacity are tracked separately: liquid beyond the connected tanks' water capacity overflows as relatively clean standing-water filth, while fecal solids remain captured until the network's sludge capacity is also full. Only sludge overflow creates raw-sewage contamination. Multiple operational outlets divide the discharge evenly; invalid outlets are ignored by capacity checks and routing.

Toilet use creates 225 g of fecal sludge, representing a realistic per-use amount scaled by 50% for reduced RimWorld fixture-use frequency. Non-toilet fixtures produce waste water but no fecal sludge.

The pit latrine is independent and has a 30 L flush tank, 5 L flushes, 60 L internal waste storage and 15 kg sludge capacity. It reuses DBH's existing refill-water work giver and job driver, like the passive cooler and water tub.

When a septic or treatment tank accumulates 10 kg of sludge (200 × 50 g units), hauling work is scheduled automatically. A pawn extracts one 10 kg batch; ordinary hauling then moves the spawned stack to storage.

## Runtime replacement

Harmony prefixes disable DBH network ticks, legacy resource transfers and obsolete water/sewage alerts. Verified DBH fixture, thirst, drinking, hand-washing, recipe-work, pool and latrine callbacks are redirected to the RealRim components. The DBH sewage visual remains only on fixtures and processors that genuinely use a drain, where it acts as the waste-water compatibility layer. Non-waste tanks, pumps, heaters, coolant equipment, pools, troughs and latrines no longer retain DBH plumbing visuals. RealRim overlays and connector stubs display fresh-water, hot-water, heating-water and coolant connections independently; all resource accounting is performed by the new networks.

## Heating integration API

External heat producers should reference `RealRim.WaterAndPumps.dll`, add a `CompFluidNode` for the `Heating` network to their building, and implement `IHeatingNetworkReportProvider` on the producing component. Heating networks cache all connected things and thing comps in `FluidNetwork.things` and `FluidNetwork.components`; heating report providers are exposed as `FluidNetwork.heating_report_providers` and are queried directly when the heating overview is built.

```csharp
public interface IHeatingNetworkReportProvider : IFluidNetworkComponent
{
	bool tryGetHeatingNetworkReport(FluidNetwork network, out HeatingNetworkReport report);
}

FluidUtility.addHeatingEnergy(Thing source, float requested_kj);
```

`addHeatingEnergy` adds heat to the source building's connected heating network, including both physical heating buffer tanks and the virtual pipe buffer. `tryGetHeatingNetworkReport` should return heating-only production/consumption and status details for the connected network; unrelated resource output, electrical demand and internal production details should stay in the building inspect pane.

## Build on openSUSE Linux

Requirements:

- .NET SDK;
- internet access for NuGet restore.

```bash
./build.sh
```

The project targets .NET Framework 4.7.2 through `Krafs.Rimworld.Ref` and writes:

```text
Assemblies/RealRim.WaterAndPumps.dll
```

To build and create a versioned ZIP:

```bash
./package.sh
```

See `KNOWN-LIMITATIONS.md` for the remaining implementation constraints.
