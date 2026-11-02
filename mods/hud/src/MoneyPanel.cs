using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Money;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>Cash (green) and bank (blue) in a glass panel right of the rail. Each counts up to a new value over 0.4 s.
/// The game's own "+ $120" popups still float over the cash slot, so this doesn't draw a second one.</summary>
internal sealed class MoneyPanel
{
	private sealed class Row
	{
		internal readonly TextMeshProUGUI Text;

		private float shown = float.NaN;

		private float from;

		private float to;

		private float t = 1f;

		internal Row(TextMeshProUGUI text)
		{
			Text = text;
		}

		internal void Update(float value, float dt)
		{
			if (float.IsNaN(shown))
			{
				shown = from = to = value;
				Text.text = MoneyManager.FormatAmount(value, false, false);
			}
			else if (value != to)
			{
				from = shown;
				to = value;
				t = 0f;
			}
			if (t < 1f)
			{
				t = Mathf.Min(1f, t + dt / 0.4f);
				shown = Mathf.Lerp(from, to, Motion.EaseOutCubic(t));
				Text.text = MoneyManager.FormatAmount(Mathf.Round(shown), false, false);
			}
		}
	}

	private const float PadX = 14f;

	private const float PadY = 10f;

	private const float RowHeight = 20f;

	private const float IconSize = 18f;

	private readonly RectTransform root;

	private readonly RectTransform shadow;

	private readonly RectTransform glass;

	private readonly RectTransform divider;

	private readonly RectTransform cashIcon;

	private readonly RectTransform bankIcon;

	private readonly Row cash;

	private readonly Row bank;

	internal MoneyPanel(Transform parent, Sprites sprites, TMP_FontAsset font)
	{
		root = Ui.Rect("Money", parent);
		shadow = Ui.Image("Shadow", root, sprites.Shadow(16, 16), Color.white, sliced: true).rectTransform;
		glass = Ui.Image("Glass", root, sprites.Glass(16), Color.white, sliced: true).rectTransform;
		divider = Ui.Image("Divider", root, null, new Color(1f, 1f, 1f, 0.1f)).rectTransform;
		cashIcon = Ui.Image("Cash icon", root, sprites.Icon(Art.Icon.Cash), Color.white).rectTransform;
		bankIcon = Ui.Image("Bank icon", root, sprites.Icon(Art.Icon.Bank), Color.white).rectTransform;
		cash = new Row(NewText("Cash", font, Theme.Cash));
		bank = new Row(NewText("Bank", font, Theme.Bank));
	}

	private TextMeshProUGUI NewText(string name, TMP_FontAsset font, Rgba color)
	{
		TextMeshProUGUI text = Ui.Text(name, root, font, 20f, Ui.Color(color), 2f);
		text.alignment = TextAlignmentOptions.Left;
		text.rectTransform.pivot = new Vector2(0f, 0.5f);
		text.rectTransform.sizeDelta = new Vector2(200f, RowHeight);
		return text;
	}

	/// <param name="bottomLeft">Where the panel's bottom-left corner goes, in the parent's space.</param>
	internal void Update(Vector2 bottomLeft, float dt)
	{
		MoneyManager money = NetworkSingleton<MoneyManager>.Instance;
		if (money != null)
		{
			cash.Update(money.cashBalance, dt);
			bank.Update(money.sync___get_value_onlineBalance(), dt);
		}
		float textWidth = Mathf.Max(62f, Mathf.Max(cash.Text.preferredWidth, bank.Text.preferredWidth));
		float width = PadX + IconSize + 8f + textWidth + PadX;
		float height = PadY + RowHeight + 6f + 1f + 6f + RowHeight + PadY;
		Ui.Place(root, bottomLeft + new Vector2(width / 2f, height / 2f), new Vector2(width, height));
		Ui.Place(glass, Vector2.zero, new Vector2(width, height));
		Ui.Place(shadow, new Vector2(0f, -6f), new Vector2(width + 32f, height + 32f));
		float left = -width / 2f + PadX;
		float topRow = height / 2f - PadY - RowHeight / 2f;
		float bottomRow = -height / 2f + PadY + RowHeight / 2f;
		Ui.Place(cashIcon, new Vector2(left + IconSize / 2f, topRow), new Vector2(IconSize, IconSize));
		Ui.Place(bankIcon, new Vector2(left + IconSize / 2f, bottomRow), new Vector2(IconSize, IconSize));
		cash.Text.rectTransform.localPosition = new Vector3(left + IconSize + 8f, topRow, 0f);
		bank.Text.rectTransform.localPosition = new Vector3(left + IconSize + 8f, bottomRow, 0f);
		Ui.Place(divider, Vector2.zero, new Vector2(width - PadX * 2f, 1f));
	}
}
