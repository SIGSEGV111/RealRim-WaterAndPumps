using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class Dialog_HeatSourceTargetTemperature : Window
	{
		private readonly CompHeatSource heat_source;

		public override Vector2 InitialSize
		{
			get
			{
				return new Vector2(560f, 300f);
			}
		}

		public Dialog_HeatSourceTargetTemperature(CompHeatSource heat_source)
		{
			this.heat_source = heat_source;
			doCloseX = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = true;
		}

		public override void DoWindowContents(Rect in_rect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(
				new Rect(0f, 0f, in_rect.width, 38f),
				"RealRim_HeatSourceTargetTitle".Translate());
			Text.Font = GameFont.Small;

			Widgets.Label(
				new Rect(0f, 50f, in_rect.width, 36f),
				"RealRim_HeatSourceTargetCurrent".Translate(
					heat_source.target_buffer_temperature_c.ToStringTemperature("F1")));
			Widgets.Label(
				new Rect(0f, 85f, in_rect.width, 36f),
				"RealRim_HeatSourceRestartCurrent".Translate(
					heat_source.getRestartTemperatureC().ToStringTemperature("F1")));
			Widgets.Label(
				new Rect(0f, 126f, in_rect.width, 52f),
				"RealRim_HeatSourceTargetHint".Translate());

			float button_y = 187f;
			float button_width = 78f;
			if (Widgets.ButtonText(new Rect(0f, button_y, button_width, 36f), "-5 °C"))
			{
				heat_source.setTargetBufferTemperature(heat_source.target_buffer_temperature_c - 5f);
			}
			if (Widgets.ButtonText(new Rect(88f, button_y, button_width, 36f), "-1 °C"))
			{
				heat_source.setTargetBufferTemperature(heat_source.target_buffer_temperature_c - 1f);
			}
			if (Widgets.ButtonText(new Rect(176f, button_y, button_width, 36f), "+1 °C"))
			{
				heat_source.setTargetBufferTemperature(heat_source.target_buffer_temperature_c + 1f);
			}
			if (Widgets.ButtonText(new Rect(264f, button_y, button_width, 36f), "+5 °C"))
			{
				heat_source.setTargetBufferTemperature(heat_source.target_buffer_temperature_c + 5f);
			}

			if (Widgets.ButtonText(
				new Rect(0f, in_rect.height - 42f, 220f, 38f),
				"RealRim_ResetDefaults".Translate()))
			{
				heat_source.resetTargetBufferTemperature();
			}
			if (Widgets.ButtonText(
				new Rect(in_rect.width - 120f, in_rect.height - 42f, 120f, 38f),
				"CloseButton".Translate()))
			{
				Close();
			}
		}
	}
}
