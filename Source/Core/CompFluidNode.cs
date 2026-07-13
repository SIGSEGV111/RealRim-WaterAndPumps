using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_FluidNode : CompProperties
	{
		public List<FluidNetworkType> networks = new List<FluidNetworkType>();
		public bool valve;
		public bool transfer_only;
		public bool layer_connector;
		public float outdoor_heat_exchange_w_per_m_k;
		public float virtual_heat_buffer_liters_per_m;

		public CompProperties_FluidNode()
		{
			compClass = typeof(CompFluidNode);
		}
	}

	public sealed class CompFluidNode : ThingComp
	{
		private bool layers_initialized;
		private FluidNetworkLayer fresh_water_layer = FluidNetworkLayer.Layer1;
		private FluidNetworkLayer hot_water_layer = FluidNetworkLayer.Layer1;
		private FluidNetworkLayer heating_layer = FluidNetworkLayer.Layer1;
		private FluidNetworkLayer waste_water_layer = FluidNetworkLayer.Layer1;
		private FluidNetworkLayer coolant_layer = FluidNetworkLayer.Layer1;
		private FluidNetworkLayer pending_fresh_water_layer = FluidNetworkLayer.None;
		private FluidNetworkLayer pending_hot_water_layer = FluidNetworkLayer.None;
		private FluidNetworkLayer pending_heating_layer = FluidNetworkLayer.None;
		private FluidNetworkLayer pending_waste_water_layer = FluidNetworkLayer.None;
		private FluidNetworkLayer pending_coolant_layer = FluidNetworkLayer.None;

		public CompProperties_FluidNode Props
		{
			get
			{
				return (CompProperties_FluidNode)props;
			}
		}

		public override void PostSpawnSetup(bool respawning_after_load)
		{
			base.PostSpawnSetup(respawning_after_load);
			if (!layers_initialized)
			{
				initializeLayers();
			}
			if (!respawning_after_load)
			{
				FluidPipeReplacementUtility.removeReplacedPipes(this);
			}
			getManager()?.registerNode(this);
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			MapComponent_FluidNetworks manager = map?.GetComponent<MapComponent_FluidNetworks>();
			manager?.deregisterNode(this);
			base.PostDeSpawn(map, mode);
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref layers_initialized, "fluid_layers_initialized", false);
			Scribe_Values.Look(ref fresh_water_layer, "fresh_water_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref hot_water_layer, "hot_water_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref heating_layer, "heating_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref waste_water_layer, "waste_water_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref coolant_layer, "coolant_layer", FluidNetworkLayer.Layer1);
			Scribe_Values.Look(ref pending_fresh_water_layer, "pending_fresh_water_layer", FluidNetworkLayer.None);
			Scribe_Values.Look(ref pending_hot_water_layer, "pending_hot_water_layer", FluidNetworkLayer.None);
			Scribe_Values.Look(ref pending_heating_layer, "pending_heating_layer", FluidNetworkLayer.None);
			Scribe_Values.Look(ref pending_waste_water_layer, "pending_waste_water_layer", FluidNetworkLayer.None);
			Scribe_Values.Look(ref pending_coolant_layer, "pending_coolant_layer", FluidNetworkLayer.None);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				clampLayers();
				layers_initialized = true;
			}
		}

		public override void ReceiveCompSignal(string signal)
		{
			base.ReceiveCompSignal(signal);
			if (Props.valve)
			{
				getManager()?.markNetworksDirty();
				FluidNetworkVisuals.markOverlayDirty(parent);
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo gizmo in base.CompGetGizmosExtra())
			{
				yield return gizmo;
			}

			if (!Props.transfer_only && supportsNetwork(FluidNetworkType.Heating))
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_HeatingOverview".Translate(),
					defaultDesc = "RealRim_HeatingOverviewDesc".Translate(),
					icon = RealRimTextures.heating_overview,
					action = delegate
					{
						Find.WindowStack.Add(new Dialog_HeatingNetworkReport(parent));
					},
				};
			}

			if (!Props.transfer_only && supportsNetwork(FluidNetworkType.HotWater))
			{
				yield return new Command_Action
				{
					defaultLabel = "RealRim_HotWaterOverview".Translate(),
					defaultDesc = "RealRim_HotWaterOverviewDesc".Translate(),
					icon = RealRimTextures.hot_water_overview,
					action = delegate
					{
						Find.WindowStack.Add(new Dialog_HotWaterNetworkReport(parent));
					},
				};
			}

			if (!Props.transfer_only && !Props.layer_connector && Props.networks != null)
			{
				for (int index = 0; index < Props.networks.Count; index++)
				{
					yield return createLayerGizmo(Props.networks[index]);
				}
			}
		}

		public override string CompInspectStringExtra()
		{
			MapComponent_FluidNetworks manager = getManager();
			if (manager == null || Props.networks.NullOrEmpty() || Props.transfer_only)
			{
				return null;
			}

			List<string> lines = new List<string>();
			for (int index = 0; index < Props.networks.Count; index++)
			{
				FluidNetworkType network_type = Props.networks[index];
				FluidNetwork network = manager.getNetwork(this, network_type);
				if (Props.layer_connector)
				{
					if (network != null)
					{
						lines.Add("RealRim_FluidNetworkLayerConnectorLine".Translate(
							FluidUtility.getNetworkLabel(network_type),
							network.network_id,
							network.nodes.Count));
					}
					else
					{
						lines.Add("RealRim_FluidNetworkLayerConnectorDisconnectedLine".Translate(
							FluidUtility.getNetworkLabel(network_type)));
					}
				}
				else if (network != null)
				{
					lines.Add("RealRim_FluidNetworkLayerLine".Translate(
						FluidUtility.getNetworkLabel(network_type),
						FluidNetworkLayerUtility.getLayerLabel(getLayer(network_type)),
						network.network_id,
						network.nodes.Count));
				}
				else
				{
					lines.Add("RealRim_FluidNetworkLayerDisconnectedLine".Translate(
						FluidUtility.getNetworkLabel(network_type),
						FluidNetworkLayerUtility.getLayerLabel(getLayer(network_type))));
				}

				FluidNetworkLayer pending_layer = getPendingLayer(network_type);
				if (pending_layer != FluidNetworkLayer.None)
				{
					lines.Add("RealRim_FluidLayerPendingLine".Translate(
						FluidUtility.getNetworkLabel(network_type),
						FluidNetworkLayerUtility.getLayerLabel(pending_layer)));
				}
			}

			return lines.Count == 0 ? null : lines.ToLineList("", false).TrimEndNewlines();
		}

		public bool supportsNetwork(FluidNetworkType network_type)
		{
			return Props.networks != null && Props.networks.Contains(network_type);
		}

		public bool isConnectionActive()
		{
			if (!Props.valve)
			{
				return true;
			}

			CompFlickable flickable = parent.TryGetComp<CompFlickable>();
			return flickable == null || flickable.SwitchIsOn;
		}

		public bool canConnectTo(
			CompFluidNode other_node,
			FluidNetworkType network_type,
			IntVec3 offset)
		{
			if (other_node == null
				|| other_node == this
				|| Props.transfer_only
				|| other_node.Props.transfer_only
				|| !supportsNetwork(network_type)
				|| !other_node.supportsNetwork(network_type)
				|| !isConnectionActive()
				|| !other_node.isConnectionActive())
			{
				return false;
			}

			if (isLayerConnector(network_type) || other_node.isLayerConnector(network_type))
			{
				return offset.x != 0 || offset.z != 0;
			}

			return getLayer(network_type) == other_node.getLayer(network_type);
		}

		public bool isLayerConnector(FluidNetworkType network_type)
		{
			return Props.layer_connector && supportsNetwork(network_type);
		}

		public FluidNetworkLayer getLayer(FluidNetworkType network_type)
		{
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					return FluidNetworkLayerUtility.clampLayer(fresh_water_layer);
				case FluidNetworkType.HotWater:
					return FluidNetworkLayerUtility.clampLayer(hot_water_layer);
				case FluidNetworkType.Heating:
					return FluidNetworkLayerUtility.clampLayer(heating_layer);
				case FluidNetworkType.WasteWater:
					return FluidNetworkLayerUtility.clampLayer(waste_water_layer);
				case FluidNetworkType.Coolant:
					return FluidNetworkLayerUtility.clampLayer(coolant_layer);
				default:
					return FluidNetworkLayer.Layer1;
			}
		}

		public bool hasPendingLayerChange()
		{
			return pending_fresh_water_layer != FluidNetworkLayer.None
				|| pending_hot_water_layer != FluidNetworkLayer.None
				|| pending_heating_layer != FluidNetworkLayer.None
				|| pending_waste_water_layer != FluidNetworkLayer.None
				|| pending_coolant_layer != FluidNetworkLayer.None;
		}

		public void applyPendingLayerChanges()
		{
			bool changed = false;
			changed |= applyPendingLayer(FluidNetworkType.FreshWater);
			changed |= applyPendingLayer(FluidNetworkType.HotWater);
			changed |= applyPendingLayer(FluidNetworkType.Heating);
			changed |= applyPendingLayer(FluidNetworkType.WasteWater);
			changed |= applyPendingLayer(FluidNetworkType.Coolant);
			clearChangeDesignationIfComplete();
			if (changed)
			{
				getManager()?.markNetworksDirty();
				FluidNetworkVisuals.markOverlayDirty(parent);
			}
		}

		public MapComponent_FluidNetworks getManager()
		{
			return parent.MapHeld?.GetComponent<MapComponent_FluidNetworks>();
		}

		internal void applyConstructionPlan(FluidLayerConstructionPlan plan)
		{
			if (plan == null || Props.networks == null)
			{
				return;
			}

			for (int index = 0; index < Props.networks.Count; index++)
			{
				setLayer(Props.networks[index], plan.getLayer(Props.networks[index]));
			}
			clampLayers();
			layers_initialized = true;
		}

		private Command_Action createLayerGizmo(FluidNetworkType network_type)
		{
			FluidNetworkLayer pending_layer = getPendingLayer(network_type);
			return new Command_Action
			{
				defaultLabel = getLayerGizmoLabel(network_type, pending_layer),
				defaultDesc = "RealRim_NodeFluidLayerDesc".Translate(
					FluidUtility.getNetworkLabel(network_type),
					FluidNetworkLayerUtility.getLayerLabel(getLayer(network_type))),
				icon = RealRimTextures.getFluidLayerIcon(network_type),
				action = delegate
				{
					openLayerMenu(network_type);
				},
			};
		}

		private string getLayerGizmoLabel(FluidNetworkType network_type, FluidNetworkLayer pending_layer)
		{
			if (pending_layer != FluidNetworkLayer.None)
			{
				return "RealRim_NodeFluidLayerPendingLabel".Translate(
					FluidUtility.getNetworkLabel(network_type),
					FluidNetworkLayerUtility.getLayerLabel(getLayer(network_type)),
					FluidNetworkLayerUtility.getLayerLabel(pending_layer));
			}
			return "RealRim_NodeFluidLayerLabel".Translate(
				FluidUtility.getNetworkLabel(network_type),
				FluidNetworkLayerUtility.getLayerLabel(getLayer(network_type)));
		}

		private void openLayerMenu(FluidNetworkType network_type)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			for (int index = 0; index < FluidNetworkLayerUtility.LAYERS.Length; index++)
			{
				FluidNetworkLayer layer = FluidNetworkLayerUtility.LAYERS[index];
				options.Add(new FloatMenuOption(
					getNodeLayerMenuLabel(network_type, layer),
					delegate
					{
						requestLayerChangeForSelectedNodes(network_type, layer);
					}));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private string getNodeLayerMenuLabel(FluidNetworkType network_type, FluidNetworkLayer layer)
		{
			string label = "RealRim_NodeFluidLayerMenuEntry".Translate(
				FluidUtility.getNetworkLabel(network_type),
				FluidNetworkLayerUtility.getLayerLabel(layer));
			if (getLayer(network_type) == layer)
			{
				label += " " + "RealRim_CurrentSelectionSuffix".Translate();
			}
			return label;
		}

		private void requestLayerChangeForSelectedNodes(FluidNetworkType network_type, FluidNetworkLayer layer)
		{
			List<CompFluidNode> targets = getSelectedLayerChangeTargets(network_type);
			for (int index = 0; index < targets.Count; index++)
			{
				targets[index].requestLayerChange(network_type, layer);
			}
		}

		private List<CompFluidNode> getSelectedLayerChangeTargets(FluidNetworkType network_type)
		{
			List<CompFluidNode> targets = new List<CompFluidNode>();
			List<object> selected_objects = Find.Selector?.SelectedObjectsListForReading;
			if (selected_objects != null)
			{
				for (int index = 0; index < selected_objects.Count; index++)
				{
					ThingWithComps thing = selected_objects[index] as ThingWithComps;
					CompFluidNode node = thing?.TryGetComp<CompFluidNode>();
					if (node != null
						&& node.canRequestLayerChange(network_type)
						&& !targets.Contains(node))
					{
						targets.Add(node);
					}
				}
			}

			if (targets.Count == 0 && canRequestLayerChange(network_type))
			{
				targets.Add(this);
			}
			return targets;
		}

		private bool canRequestLayerChange(FluidNetworkType network_type)
		{
			return !Props.transfer_only
				&& !Props.layer_connector
				&& supportsNetwork(network_type)
				&& parent.MapHeld != null;
		}

		private void requestLayerChange(FluidNetworkType network_type, FluidNetworkLayer layer)
		{
			if (!canRequestLayerChange(network_type))
			{
				return;
			}

			layer = FluidNetworkLayerUtility.clampLayer(layer);
			if (layer == getLayer(network_type))
			{
				setPendingLayer(network_type, FluidNetworkLayer.None);
				clearChangeDesignationIfComplete();
				return;
			}

			setPendingLayer(network_type, layer);
			DesignationDef designation_def = FluidNetworkLayerUtility.getChangeDesignationDef();
			if (designation_def == null)
			{
				Log.ErrorOnce("[RealRim] Water & Pumps: missing RealRim_ChangeFluidLayer DesignationDef.", 15635531);
				return;
			}

			DesignationManager designation_manager = parent.MapHeld.designationManager;
			if (designation_manager.DesignationOn(parent, designation_def) == null)
			{
				designation_manager.AddDesignation(new Designation(parent, designation_def));
			}
		}

		private bool applyPendingLayer(FluidNetworkType network_type)
		{
			FluidNetworkLayer pending_layer = getPendingLayer(network_type);
			if (pending_layer == FluidNetworkLayer.None)
			{
				return false;
			}

			setPendingLayer(network_type, FluidNetworkLayer.None);
			if (!supportsNetwork(network_type) || pending_layer == getLayer(network_type))
			{
				return false;
			}

			setLayer(network_type, pending_layer);
			return true;
		}

		private void clearChangeDesignationIfComplete()
		{
			if (hasPendingLayerChange() || parent.MapHeld == null)
			{
				return;
			}

			DesignationDef designation_def = FluidNetworkLayerUtility.getChangeDesignationDef();
			if (designation_def == null)
			{
				return;
			}

			Designation designation = parent.MapHeld.designationManager.DesignationOn(parent, designation_def);
			if (designation != null)
			{
				parent.MapHeld.designationManager.RemoveDesignation(designation);
			}
		}

		private void initializeLayers()
		{
			MapComponent_FluidNetworks manager = getManager();
			FluidLayerConstructionPlan plan = manager?.consumeConstructionPlan(this);
			if (plan != null)
			{
				applyConstructionPlan(plan);
				return;
			}

			if (Props.networks != null)
			{
				for (int index = 0; index < Props.networks.Count; index++)
				{
					setLayer(Props.networks[index], FluidNetworkLayerSettings.getSelectedLayer(Props.networks[index]));
				}
			}
			clampLayers();
			layers_initialized = true;
		}

		private FluidNetworkLayer getPendingLayer(FluidNetworkType network_type)
		{
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					return pending_fresh_water_layer;
				case FluidNetworkType.HotWater:
					return pending_hot_water_layer;
				case FluidNetworkType.Heating:
					return pending_heating_layer;
				case FluidNetworkType.WasteWater:
					return pending_waste_water_layer;
				case FluidNetworkType.Coolant:
					return pending_coolant_layer;
				default:
					return FluidNetworkLayer.None;
			}
		}

		private void setPendingLayer(FluidNetworkType network_type, FluidNetworkLayer layer)
		{
			if (layer != FluidNetworkLayer.None)
			{
				layer = FluidNetworkLayerUtility.clampLayer(layer);
			}
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					pending_fresh_water_layer = layer;
					break;
				case FluidNetworkType.HotWater:
					pending_hot_water_layer = layer;
					break;
				case FluidNetworkType.Heating:
					pending_heating_layer = layer;
					break;
				case FluidNetworkType.WasteWater:
					pending_waste_water_layer = layer;
					break;
				case FluidNetworkType.Coolant:
					pending_coolant_layer = layer;
					break;
			}
		}

		private void setLayer(FluidNetworkType network_type, FluidNetworkLayer layer)
		{
			layer = FluidNetworkLayerUtility.clampLayer(layer);
			switch (network_type)
			{
				case FluidNetworkType.FreshWater:
					fresh_water_layer = layer;
					break;
				case FluidNetworkType.HotWater:
					hot_water_layer = layer;
					break;
				case FluidNetworkType.Heating:
					heating_layer = layer;
					break;
				case FluidNetworkType.WasteWater:
					waste_water_layer = layer;
					break;
				case FluidNetworkType.Coolant:
					coolant_layer = layer;
					break;
			}
		}

		private void clampLayers()
		{
			fresh_water_layer = FluidNetworkLayerUtility.clampLayer(fresh_water_layer);
			hot_water_layer = FluidNetworkLayerUtility.clampLayer(hot_water_layer);
			heating_layer = FluidNetworkLayerUtility.clampLayer(heating_layer);
			waste_water_layer = FluidNetworkLayerUtility.clampLayer(waste_water_layer);
			coolant_layer = FluidNetworkLayerUtility.clampLayer(coolant_layer);
			if (pending_fresh_water_layer != FluidNetworkLayer.None)
			{
				pending_fresh_water_layer = FluidNetworkLayerUtility.clampLayer(pending_fresh_water_layer);
			}
			if (pending_hot_water_layer != FluidNetworkLayer.None)
			{
				pending_hot_water_layer = FluidNetworkLayerUtility.clampLayer(pending_hot_water_layer);
			}
			if (pending_heating_layer != FluidNetworkLayer.None)
			{
				pending_heating_layer = FluidNetworkLayerUtility.clampLayer(pending_heating_layer);
			}
			if (pending_waste_water_layer != FluidNetworkLayer.None)
			{
				pending_waste_water_layer = FluidNetworkLayerUtility.clampLayer(pending_waste_water_layer);
			}
			if (pending_coolant_layer != FluidNetworkLayer.None)
			{
				pending_coolant_layer = FluidNetworkLayerUtility.clampLayer(pending_coolant_layer);
			}
		}
	}
}
