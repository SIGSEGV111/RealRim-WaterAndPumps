using UnityEngine;
using Verse;

namespace RealRim.WaterAndPumps
{
	public sealed class Dialog_WaterPumpThresholds : Window
	{
		private const float STEP = 0.05f;
		private readonly CompWaterPump pump;

		public override Vector2 InitialSize
		{
			get
			{
				return new Vector2(520f, 290f);
			}
		}

		public Dialog_WaterPumpThresholds(CompWaterPump pump)
		{
			this.pump = pump;
			doCloseX = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = true;
		}

		public override void DoWindowContents(Rect in_rect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, 0f, in_rect.width, 38f), "RealRim_WaterPumpThresholdTitle".Translate());
			Text.Font = GameFont.Small;

			drawRow(
				new Rect(0f, 55f, in_rect.width, 42f),
				"RealRim_WaterPumpStartThreshold".Translate(pump.start_fill_fraction.ToStringPercent()),
				true);
			drawRow(
				new Rect(0f, 108f, in_rect.width, 42f),
				"RealRim_WaterPumpStopThreshold".Translate(pump.stop_fill_fraction.ToStringPercent()),
				false);

			Widgets.Label(
				new Rect(0f, 164f, in_rect.width, 42f),
				"RealRim_WaterPumpThresholdHint".Translate());

			if (Widgets.ButtonText(new Rect(0f, in_rect.height - 42f, 220f, 38f), "RealRim_ResetDefaults".Translate()))
			{
				pump.setThresholds(pump.Props.start_fill_fraction, pump.Props.stop_fill_fraction);
			}
			if (Widgets.ButtonText(new Rect(in_rect.width - 120f, in_rect.height - 42f, 120f, 38f), "CloseButton".Translate()))
			{
				Close();
			}
		}

		private void drawRow(Rect row, string label, bool start_threshold)
		{
			Widgets.Label(new Rect(row.x, row.y, row.width - 130f, row.height), label);
			if (Widgets.ButtonText(new Rect(row.xMax - 120f, row.y, 52f, 36f), "-5%"))
			{
				if (start_threshold)
				{
					pump.setThresholds(pump.start_fill_fraction - STEP, pump.stop_fill_fraction);
				}
				else
				{
					pump.setThresholds(pump.start_fill_fraction, pump.stop_fill_fraction - STEP);
				}
			}
			if (Widgets.ButtonText(new Rect(row.xMax - 56f, row.y, 52f, 36f), "+5%"))
			{
				if (start_threshold)
				{
					pump.setThresholds(pump.start_fill_fraction + STEP, pump.stop_fill_fraction);
				}
				else
				{
					pump.setThresholds(pump.start_fill_fraction, pump.stop_fill_fraction + STEP);
				}
			}
		}
	}
}
