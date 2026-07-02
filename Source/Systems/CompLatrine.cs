using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class CompProperties_Latrine : CompProperties
	{
		public float water_capacity_liters = 30f;
		public float water_per_flush_liters = 5f;
		public float waste_capacity_liters = 60f;
		public float sludge_capacity_kg = 15f;
		public float sludge_per_use_kg = 0.225f;

		public CompProperties_Latrine()
		{
			compClass = typeof(CompLatrine);
		}
	}

	public sealed class CompLatrine : ThingComp
	{
		private const float SLUDGE_KG_PER_ITEM = 0.05f;
		private const string DBH_WATER_FILLABLE_TYPE_NAME = "DubsBadHygiene.CompWaterFillable";
		private const string DBH_WATER_FIELD_NAME = "Water";
		private const BindingFlags INSTANCE_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public float fallback_water_liters;
		public float waste_liters;
		public float sludge_kg;

		public CompProperties_Latrine Props
		{
			get
			{
				return (CompProperties_Latrine)props;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref fallback_water_liters, "latrine_fallback_water_liters", 0f);
			Scribe_Values.Look(ref waste_liters, "latrine_waste_liters", 0f);
			Scribe_Values.Look(ref sludge_kg, "latrine_sludge_kg", 0f);
		}

		public override string CompInspectStringExtra()
		{
			return "RealRim_LatrineStatus".Translate(
				getWaterLiters().ToString("N1"),
				Props.water_capacity_liters.ToString("N0"),
				waste_liters.ToString("N1"),
				Props.waste_capacity_liters.ToString("N0"),
				sludge_kg.ToString("N2"),
				Props.sludge_capacity_kg.ToString("N1"));
		}

		public bool canUse()
		{
			return getWaterLiters() + 0.001f >= Props.water_per_flush_liters
				&& waste_liters + Props.water_per_flush_liters <= Props.waste_capacity_liters + 0.001f
				&& sludge_kg + Props.sludge_per_use_kg <= Props.sludge_capacity_kg + 0.0001f;
		}

		public bool useLatrine()
		{
			if (!canUse())
			{
				return false;
			}

			setWaterLiters(getWaterLiters() - Props.water_per_flush_liters);
			waste_liters += Props.water_per_flush_liters;
			sludge_kg += Props.sludge_per_use_kg;
			return true;
		}

		public float getWaterLiters()
		{
			ThingComp water_comp = getWaterFillableComp();
			FieldInfo water_field = water_comp?.GetType().GetField(DBH_WATER_FIELD_NAME, INSTANCE_FLAGS);
			if (water_field == null)
			{
				return fallback_water_liters;
			}

			return Mathf.Clamp(Convert.ToSingle(water_field.GetValue(water_comp)), 0f, Props.water_capacity_liters);
		}

		public void setWaterLiters(float liters)
		{
			float clamped = Mathf.Clamp(liters, 0f, Props.water_capacity_liters);
			fallback_water_liters = clamped;
			ThingComp water_comp = getWaterFillableComp();
			FieldInfo water_field = water_comp?.GetType().GetField(DBH_WATER_FIELD_NAME, INSTANCE_FLAGS);
			if (water_field != null)
			{
				water_field.SetValue(water_comp, Convert.ChangeType(clamped, water_field.FieldType));
			}
		}

		public float addWater(float liters)
		{
			float current = getWaterLiters();
			float accepted = Mathf.Min(Mathf.Max(0f, liters), Props.water_capacity_liters - current);
			setWaterLiters(current + accepted);
			return accepted;
		}

		public float getSludgeItemCount()
		{
			return sludge_kg / SLUDGE_KG_PER_ITEM;
		}

		public float clearWaste()
		{
			float extracted_sludge_kg = sludge_kg;
			waste_liters = 0f;
			sludge_kg = 0f;
			return extracted_sludge_kg;
		}

		private ThingComp getWaterFillableComp()
		{
			if (parent == null)
			{
				return null;
			}
			for (int index = 0; index < parent.AllComps.Count; index++)
			{
				ThingComp comp = parent.AllComps[index];
				if (comp?.GetType().FullName == DBH_WATER_FILLABLE_TYPE_NAME)
				{
					return comp;
				}
			}
			return null;
		}
	}
}
