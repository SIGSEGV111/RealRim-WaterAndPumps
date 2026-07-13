using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public enum SmartMixingValveControlMode
	{
		WaterTemperature,
		RoomTemperature,
	}

	public sealed class CompProperties_SmartMixingValve : CompProperties
	{
		public float maximum_transfer_kw = 12f;
		public float default_water_temperature_c = 35f;
		public float minimum_water_temperature_c = 5f;
		public float maximum_water_temperature_c = 85f;
		public float default_room_temperature_c = 21f;
		public float minimum_room_temperature_c = 5f;
		public float maximum_room_temperature_c = 35f;

		public CompProperties_SmartMixingValve()
		{
			compClass = typeof(CompSmartMixingValve);
		}
	}

	public sealed class CompSmartMixingValve : ThingComp, IFluidTickable
	{
		private const float TEMPERATURE_DEADBAND_C = 0.10f;

		private static readonly IntVec3[] CONNECTION_OFFSETS =
		{
			IntVec3.North,
			IntVec3.East,
			IntVec3.South,
			IntVec3.West,
		};

		public SmartMixingValveControlMode control_mode = SmartMixingValveControlMode.WaterTemperature;
		public FluidNetworkLayer source_layer = FluidNetworkLayer.Layer1;
		public FluidNetworkLayer receiving_layer = FluidNetworkLayer.Layer1;
		public float target_water_temperature_c;
		public float target_room_temperature_c;
		public float last_transfer_kw;
		public float last_source_temperature_c;
		public float last_receiving_temperature_c;
		public float last_room_temperature_c;
		public int last_source_network_id;
		public int last_receiving_network_id;
		public string last_reason = string.Empty;
		public Thing linked_thermostat;

		private bool last_room_temperature_valid;
		private readonly List<FluidNetwork> source_network_buffer = new List<FluidNetwork>();
		private readonly List<FluidNetwork> receiving_network_buffer = new List<FluidNetwork>();

		public CompProperties_SmartMixingValve Props
		{
			get
			{
				return (CompProperties_SmartMixingValve)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			if (!respawning_after_load)
			{
				source_layer = FluidNetworkLayerSettings.getSelectedLayer(FluidNetworkType.Heating);
				receiving_layer = source_layer;
				target_water_temperature_c = Props.default_water_temperature_c;
				target_room_temperature_c = Props.default_room_temperature_c;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref control_mode, "control_mode", SmartMixingValveControlMode.WaterTemperature);
			Scribe_Values.Look(ref source_layer, "source_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref receiving_layer, "receiving_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref target_water_temperature_c, "target_water_temperature_c", Props.default_water_temperature_c);
			Scribe_Values.Look(ref target_room_temperature_c, "target_room_temperature_c", Props.default_room_temperature_c);
			Scribe_References.Look(ref linked_thermostat, "linked_thermostat");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				clampTargets();
				clampLayers();
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			base.PostDrawExtraSelectionOverlays();
			Thing thermostat = getLinkedThermostatOrNull();
			if (thermostat != null)
			{
				ThermostatLinkOverlay.drawLink(parent, thermostat);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo gizmo in base.CompGetGizmosExtra())
			{
				yield return gizmo;
			}

			yield return new Command_Action
			{
				defaultLabel = "RealRim_SmartMixingValveModeLabel".Translate(getControlModeLabel()),
				defaultDesc = "RealRim_SmartMixingValveModeDesc".Translate(),
				icon = RealRimTextures.configure_heat_source,
				action = toggleControlMode,
			};

			yield return createLayerGizmo(true);
			yield return createLayerGizmo(false);

			yield return new Command_Action
			{
				defaultLabel = "RealRim_LinkRoomThermostat".Translate(),
				defaultDesc = "RealRim_LinkRoomThermostatDesc".Translate(),
				icon = RealRimTextures.configure_heat_source,
				action = openThermostatLinkMenu,
			};
			if (getLinkedThermostatOrNull() != null)
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_UnlinkRoomThermostat".Translate(),
					defaultDesc = "RealRim_UnlinkRoomThermostatDesc".Translate(getLinkedThermostatLabel()),
					icon = RealRimTextures.configure_heat_source,
					action = unlinkThermostat,
				};
			}

			if (control_mode == SmartMixingValveControlMode.RoomTemperature)
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_SmartMixingValveRoomTargetDown".Translate(),
					defaultDesc = "RealRim_SmartMixingValveRoomTargetDesc".Translate(
						target_room_temperature_c.ToStringTemperature("F1")),
					icon = RealRimTextures.lower_target,
					action = delegate
					{
						target_room_temperature_c -= 1f;
						clampTargets();
					},
				};
				yield return new Command_Action
				{
					defaultLabel = "RealRim_SmartMixingValveRoomTargetUp".Translate(),
					defaultDesc = "RealRim_SmartMixingValveRoomTargetDesc".Translate(
						target_room_temperature_c.ToStringTemperature("F1")),
					icon = RealRimTextures.raise_target,
					action = delegate
					{
						target_room_temperature_c += 1f;
						clampTargets();
					},
				};
			}
			else
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_SmartMixingValveWaterTargetDown".Translate(),
					defaultDesc = "RealRim_SmartMixingValveWaterTargetDesc".Translate(
						target_water_temperature_c.ToStringTemperature("F1")),
					icon = RealRimTextures.lower_target,
					action = delegate
					{
						target_water_temperature_c -= 1f;
						clampTargets();
					},
				};
				yield return new Command_Action
				{
					defaultLabel = "RealRim_SmartMixingValveWaterTargetUp".Translate(),
					defaultDesc = "RealRim_SmartMixingValveWaterTargetDesc".Translate(
						target_water_temperature_c.ToStringTemperature("F1")),
					icon = RealRimTextures.raise_target,
					action = delegate
					{
						target_water_temperature_c += 1f;
						clampTargets();
					},
				};
			}
		}

		public override string CompInspectStringExtra()
		{
			updateLastRoomTemperature();
			return "RealRim_SmartMixingValveStatus".Translate(
				getControlModeLabel(),
				FluidNetworkLayerUtility.getLayerLabel(source_layer),
				FluidNetworkLayerUtility.getLayerLabel(receiving_layer),
				getWaterTargetText(),
				getRoomTargetText(),
				getRoomTemperatureText(),
				last_source_network_id <= 0 ? "-" : last_source_network_id.ToString(),
				last_source_temperature_c.ToStringTemperature("F1"),
				last_receiving_network_id <= 0 ? "-" : last_receiving_network_id.ToString(),
				last_receiving_temperature_c.ToStringTemperature("F1"),
				last_transfer_kw.ToString("N2"),
				last_reason).ToString().TrimEnd('\r', '\n', ' ', '\t');
		}

		public void tickFluidSystem(float elapsed_seconds)
		{
			resetLastTickValues();

			if (elapsed_seconds <= 0f)
			{
				return;
			}

			if (!FluidUtility.isPoweredOn(parent))
			{
				last_reason = "RealRim_ReasonSwitchedOff".Translate();
				return;
			}

			if (control_mode == SmartMixingValveControlMode.RoomTemperature && !isRoomDemandingHeat())
			{
				return;
			}

			getAdjacentHeatingNetworks(source_layer, source_network_buffer);
			getAdjacentHeatingNetworks(receiving_layer, receiving_network_buffer);
			FluidNetwork source_network;
			FluidNetwork receiving_network;
			if (!trySelectNetworks(
				source_network_buffer,
				receiving_network_buffer,
				control_mode,
				target_water_temperature_c,
				out source_network,
				out receiving_network))
			{
				last_reason = "RealRim_ReasonValveNeedsSelectedLayerNetworks".Translate(
					FluidNetworkLayerUtility.getLayerLabel(source_layer),
					FluidNetworkLayerUtility.getLayerLabel(receiving_layer));
				return;
			}
			last_source_network_id = source_network.network_id;
			last_receiving_network_id = receiving_network.network_id;
			last_source_temperature_c = source_network.getAverageThermalTemperature();
			last_receiving_temperature_c = receiving_network.getAverageThermalTemperature();

			if (receiving_network.getThermalCapacityEnergyKj() <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoReceivingStorage".Translate();
				return;
			}
			if (source_network.getThermalCapacityEnergyKj() <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoHeatingStorage".Translate();
				return;
			}

			if (control_mode == SmartMixingValveControlMode.WaterTemperature
				&& last_receiving_temperature_c >= target_water_temperature_c - TEMPERATURE_DEADBAND_C)
			{
				last_reason = "RealRim_ReasonWaterTargetReached".Translate();
				return;
			}
			if (last_source_temperature_c <= last_receiving_temperature_c + TEMPERATURE_DEADBAND_C)
			{
				last_reason = "RealRim_ReasonSourceTooCold".Translate(
					last_source_temperature_c.ToStringTemperature("F1"));
				return;
			}

			float effective_target_c = getEffectiveReceivingTargetTemperature();
			float receiver_room_kj = receiving_network.getThermalEnergyNeededToReachTemperature(effective_target_c);
			float source_available_kj = source_network.getThermalEnergyAvailableAboveTemperature(last_receiving_temperature_c);
			float requested_kj = Mathf.Min(
				Props.maximum_transfer_kw * elapsed_seconds,
				Mathf.Min(receiver_room_kj, source_available_kj));
			if (requested_kj <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoStoredHeat".Translate();
				return;
			}

			float drawn_kj = source_network.drawThermalEnergyTowardTemperature(
				requested_kj,
				last_receiving_temperature_c);
			float accepted_kj = receiving_network.addThermalEnergyTowardTemperature(
				drawn_kj,
				effective_target_c);
			if (drawn_kj > accepted_kj + 0.001f)
			{
				source_network.addThermalEnergy(drawn_kj - accepted_kj);
			}

			source_network.recordMixingValveOutput(accepted_kj);
			receiving_network.recordMixingValveInput(accepted_kj);
			last_transfer_kw = accepted_kj / elapsed_seconds;
			if (last_transfer_kw <= 0.001f)
			{
				last_reason = "RealRim_ReasonNoStoredHeat".Translate();
			}
		}

		private void getAdjacentHeatingNetworks(
			FluidNetworkLayer layer,
			List<FluidNetwork> result)
		{
			result.Clear();
			Map map = parent.MapHeld;
			MapComponent_FluidNetworks manager = map?.GetComponent<MapComponent_FluidNetworks>();
			if (map == null || manager == null)
			{
				return;
			}

			CompFluidNode own_node = parent.TryGetComp<CompFluidNode>();
			for (int index = 0; index < CONNECTION_OFFSETS.Length; index++)
			{
				IntVec3 cell = parent.Position + CONNECTION_OFFSETS[index];
				if (!cell.InBounds(map))
				{
					continue;
				}

				List<Thing> things = cell.GetThingList(map);
				for (int thing_index = 0; thing_index < things.Count; thing_index++)
				{
					ThingWithComps thing_with_comps = things[thing_index] as ThingWithComps;
					CompFluidNode node = thing_with_comps?.TryGetComp<CompFluidNode>();
					if (node == null
						|| node == own_node
						|| node.Props.transfer_only
						|| !node.supportsNetwork(FluidNetworkType.Heating)
						|| (!node.isLayerConnector(FluidNetworkType.Heating)
							&& node.getLayer(FluidNetworkType.Heating) != layer))
					{
						continue;
					}

					FluidNetwork network = manager.getNetwork(node, FluidNetworkType.Heating);
					if (network != null && !result.Contains(network))
					{
						result.Add(network);
					}
				}
			}
		}

		private static bool trySelectNetworks(
			List<FluidNetwork> source_networks,
			List<FluidNetwork> receiving_networks,
			SmartMixingValveControlMode control_mode,
			float target_water_temperature_c,
			out FluidNetwork source_network,
			out FluidNetwork receiving_network)
		{
			source_network = null;
			receiving_network = null;
			if (source_networks.NullOrEmpty() || receiving_networks.NullOrEmpty())
			{
				return false;
			}

			float source_temperature_c = float.MinValue;
			for (int index = 0; index < source_networks.Count; index++)
			{
				FluidNetwork candidate = source_networks[index];
				if (candidate == null)
				{
					continue;
				}
				float candidate_temperature_c = candidate.getAverageThermalTemperature();
				if (source_network == null || candidate_temperature_c > source_temperature_c)
				{
					source_network = candidate;
					source_temperature_c = candidate_temperature_c;
				}
			}
			if (source_network == null)
			{
				return false;
			}

			float receiving_temperature_c = float.MaxValue;
			bool receiving_at_or_above_target = true;
			for (int index = 0; index < receiving_networks.Count; index++)
			{
				FluidNetwork candidate = receiving_networks[index];
				if (candidate == null || candidate == source_network)
				{
					continue;
				}

				float candidate_temperature_c = candidate.getAverageThermalTemperature();
				if (control_mode == SmartMixingValveControlMode.WaterTemperature)
				{
					bool candidate_at_or_above_target = candidate_temperature_c >= target_water_temperature_c;
					if (receiving_network == null
						|| (receiving_at_or_above_target && !candidate_at_or_above_target)
						|| (receiving_at_or_above_target == candidate_at_or_above_target
							&& candidate_temperature_c < receiving_temperature_c))
					{
						receiving_network = candidate;
						receiving_temperature_c = candidate_temperature_c;
						receiving_at_or_above_target = candidate_at_or_above_target;
					}
				}
				else if (receiving_network == null || candidate_temperature_c < receiving_temperature_c)
				{
					receiving_network = candidate;
					receiving_temperature_c = candidate_temperature_c;
				}
			}
			return receiving_network != null;
		}

		private Command_Action createLayerGizmo(bool source)
		{
			FluidNetworkLayer layer = source ? source_layer : receiving_layer;
			return new Command_Action
			{
				defaultLabel = (source
					? "RealRim_SmartMixingValveSourceLayerLabel"
					: "RealRim_SmartMixingValveReceivingLayerLabel").Translate(
					FluidNetworkLayerUtility.getLayerLabel(layer)),
				defaultDesc = "RealRim_SmartMixingValveLayerDesc".Translate(),
				icon = RealRimTextures.fluid_layer,
				action = delegate
				{
					openLayerMenu(source);
				},
			};
		}

		private void openLayerMenu(bool source)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			for (int index = 0; index < FluidNetworkLayerUtility.LAYERS.Length; index++)
			{
				FluidNetworkLayer layer = FluidNetworkLayerUtility.LAYERS[index];
				options.Add(new FloatMenuOption(
					getLayerMenuLabel(source, layer),
					delegate
					{
						if (source)
						{
							source_layer = layer;
						}
						else
						{
							receiving_layer = layer;
						}
						clampLayers();
					}));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private void openThermostatLinkMenu()
		{
			Map map = parent?.MapHeld;
			if (map == null || map.listerThings?.AllThings == null)
			{
				return;
			}

			List<FloatMenuOption> options = new List<FloatMenuOption>();
			List<CompRoomThermostatLinkTarget> thermostats = getRoomThermostats(map);
			for (int index = 0; index < thermostats.Count; index++)
			{
				CompRoomThermostatLinkTarget thermostat = thermostats[index];
				Thing thermostat_thing = thermostat.parent;
				options.Add(new FloatMenuOption(
					getThermostatMenuLabel(thermostat_thing),
					delegate
					{
						linkThermostat(thermostat_thing);
					}));
			}

			if (options.Count == 0)
			{
				options.Add(new FloatMenuOption(
					"RealRim_NoRoomThermostatsAvailable".Translate().ToString(),
					null));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private List<CompRoomThermostatLinkTarget> getRoomThermostats(Map map)
		{
			List<CompRoomThermostatLinkTarget> result = new List<CompRoomThermostatLinkTarget>();
			List<Thing> all_things = map.listerThings.AllThings;
			for (int index = 0; index < all_things.Count; index++)
			{
				ThingWithComps thing = all_things[index] as ThingWithComps;
				CompRoomThermostatLinkTarget thermostat = thing?.TryGetComp<CompRoomThermostatLinkTarget>();
				if (thermostat != null && thermostat.parent.Spawned)
				{
					result.Add(thermostat);
				}
			}
			result.Sort((left, right) => getDistanceSquared(left.parent.Position)
				.CompareTo(getDistanceSquared(right.parent.Position)));
			return result;
		}

		private int getDistanceSquared(IntVec3 cell)
		{
			int dx = cell.x - parent.Position.x;
			int dz = cell.z - parent.Position.z;
			return dx * dx + dz * dz;
		}

		private string getThermostatMenuLabel(Thing thermostat)
		{
			string label = thermostat.LabelCap.ToString();
			if (linked_thermostat == thermostat)
			{
				label += " " + "RealRim_CurrentSelectionSuffix".Translate();
			}
			return label + " (" + thermostat.Position.ToString() + ")";
		}

		public void linkThermostat(Thing thermostat)
		{
			if (!CompRoomThermostatLinkTarget.isValidThermostat(thermostat)
				|| thermostat.MapHeld != parent.MapHeld)
			{
				return;
			}
			linked_thermostat = thermostat;
			control_mode = SmartMixingValveControlMode.RoomTemperature;
			clampTargets();
		}

		public void unlinkThermostat()
		{
			linked_thermostat = null;
		}

		public bool isLinkedToThermostat(Thing thermostat)
		{
			return thermostat != null && getLinkedThermostatOrNull() == thermostat;
		}

		private Thing getLinkedThermostatOrNull()
		{
			if (linked_thermostat == null
				|| !CompRoomThermostatLinkTarget.isValidThermostat(linked_thermostat)
				|| linked_thermostat.MapHeld != parent.MapHeld)
			{
				return null;
			}
			return linked_thermostat;
		}

		private string getLinkedThermostatLabel()
		{
			Thing thermostat = getLinkedThermostatOrNull();
			return thermostat == null ? "-" : thermostat.LabelCap.ToString();
		}

		private string getLayerMenuLabel(bool source, FluidNetworkLayer layer)
		{
			FluidNetworkLayer current_layer = source ? source_layer : receiving_layer;
			string label = (source
				? "RealRim_SmartMixingValveSourceLayerMenuEntry"
				: "RealRim_SmartMixingValveReceivingLayerMenuEntry").Translate(
				FluidNetworkLayerUtility.getLayerLabel(layer));
			if (current_layer == layer)
			{
				label += " " + "RealRim_CurrentSelectionSuffix".Translate();
			}
			return label;
		}

		private void toggleControlMode()
		{
			if (control_mode == SmartMixingValveControlMode.WaterTemperature)
			{
				control_mode = SmartMixingValveControlMode.RoomTemperature;
			}
			else
			{
				control_mode = SmartMixingValveControlMode.WaterTemperature;
			}
			clampTargets();
		}

		private void clampTargets()
		{
			target_water_temperature_c = Mathf.Clamp(
				target_water_temperature_c,
				Props.minimum_water_temperature_c,
				Props.maximum_water_temperature_c);
			target_room_temperature_c = Mathf.Clamp(
				target_room_temperature_c,
				Props.minimum_room_temperature_c,
				Props.maximum_room_temperature_c);
		}

		private void clampLayers()
		{
			source_layer = FluidNetworkLayerUtility.clampLayer(source_layer);
			receiving_layer = FluidNetworkLayerUtility.clampLayer(receiving_layer);
		}

		private void resetLastTickValues()
		{
			last_transfer_kw = 0f;
			last_source_temperature_c = 0f;
			last_receiving_temperature_c = 0f;
			last_room_temperature_c = 0f;
			last_room_temperature_valid = false;
			last_source_network_id = 0;
			last_receiving_network_id = 0;
			last_reason = string.Empty;
		}

		private bool isRoomDemandingHeat()
		{
			if (!updateLastRoomTemperature())
			{
				last_reason = "RealRim_ReasonNoValveRoom".Translate();
				return false;
			}
			if (last_room_temperature_c >= target_room_temperature_c - TEMPERATURE_DEADBAND_C)
			{
				last_reason = "RealRim_ReasonRoomTargetReached".Translate();
				return false;
			}
			return true;
		}

		private bool updateLastRoomTemperature()
		{
			Thing thermostat = getLinkedThermostatOrNull();
			Map map = thermostat?.MapHeld ?? parent.MapHeld;
			IntVec3 sample_cell = thermostat?.Position ?? parent.Position;
			if (map == null || sample_cell.UsesOutdoorTemperature(map))
			{
				last_room_temperature_valid = false;
				return false;
			}

			last_room_temperature_c = GenTemperature.GetTemperatureForCell(sample_cell, map);
			last_room_temperature_valid = true;
			return true;
		}

		private float getEffectiveReceivingTargetTemperature()
		{
			if (control_mode == SmartMixingValveControlMode.RoomTemperature)
			{
				return last_source_temperature_c;
			}
			return Mathf.Min(target_water_temperature_c, last_source_temperature_c);
		}

		private string getControlModeLabel()
		{
			if (control_mode == SmartMixingValveControlMode.RoomTemperature)
			{
				return "RealRim_SmartMixingValveModeRoom".Translate().ToString();
			}
			return "RealRim_SmartMixingValveModeWater".Translate().ToString();
		}

		private string getWaterTargetText()
		{
			if (control_mode == SmartMixingValveControlMode.RoomTemperature)
			{
				return "-";
			}
			return target_water_temperature_c.ToStringTemperature("F1");
		}

		private string getRoomTargetText()
		{
			if (control_mode == SmartMixingValveControlMode.WaterTemperature)
			{
				return "-";
			}
			return target_room_temperature_c.ToStringTemperature("F1");
		}

		private string getRoomTemperatureText()
		{
			if (!last_room_temperature_valid)
			{
				return "-";
			}

			Thing thermostat = getLinkedThermostatOrNull();
			string temperature = last_room_temperature_c.ToStringTemperature("F1");
			if (thermostat == null)
			{
				return temperature;
			}
			return "RealRim_SmartMixingValveRoomTemperatureFromThermostat".Translate(
				temperature,
				thermostat.LabelCap);
		}
	}
}
