using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Money;
using Il2CppTMPro;
using UnityEngine;

namespace HylandHud;

/// <summary>The bank balance (blue) in a small glass panel right of the rail, where the game's own bank slot was. It counts
/// up to a new value over 0.4 s. Cash isn't repeated here: the game's cash slot on the rail already shows it, and its own
/// "+ $120" popups still float over that slot.</summary>
internal sealed class MoneyPanel
{
	private const float PadX = 14f;

	private const float PadY = 10f;

	private const float RowHeight = 20f;

	private const float IconSize = 18f;

	private readonly RectTransform root;

	private readonly RectTransform shadow;

	private readonly RectTransform glass;

	private readonly RectTransform icon;

	private readonly TextMeshProUGUI text;

	private float shown = float.NaN;

	private float from;

	private float to;

	private float t = 1f;

	/// <summary>The text's width, measured only when the text changes (TMP re-measures on every read).</summary>
	private float textWidth;

	internal MoneyPanel(Transform parent, Sprites sprites, TMP_FontAsset font)
	{
		root = Ui.Rect("Money", parent);
		shadow = Ui.Image("Shadow", root, sprites.Shadow(16, 16), Color.white, sliced: true).rectTransform;
		glass = Ui.Image("Glass", root, sprites.Glass(16), Color.white, sliced: true).rectTransform;
		icon = Ui.Image("Bank icon", root, sprites.Icon(Art.Icon.Bank), Color.white).rectTransform;
		text = Ui.Text("Bank", root, font, 20f, Ui.Color(Theme.Bank), 2f);
		text.alignment = TextAlignmentOptions.Left;
		text.rectTransform.pivot = new Vector2(0f, 0.5f);
		text.rectTransform.sizeDelta = new Vector2(200f, RowHeight);
	}

	/// <param name="bottomLeft">Where the panel's bottom-left corner goes, in the parent's space.</param>
	internal void Update(Vector2 bottomLeft, float dt)
	{
		MoneyManager money = NetworkSingleton<MoneyManager>.Instance;
		if (money != null)
		{
			Count(money.sync___get_value_onlineBalance(), dt);
		}
		float width = PadX + IconSize + 8f + Mathf.Max(62f, textWidth) + PadX;
		float height = PadY + RowHeight + PadY;
		Ui.Place(root, bottomLeft + new Vector2(width / 2f, height / 2f), new Vector2(width, height));
		Ui.Place(glass, Vector2.zero, new Vector2(width, height));
		Ui.Place(shadow, new Vector2(0f, -6f), new Vector2(width + 32f, height + 32f));
		float left = -width / 2f + PadX;
		Ui.Place(icon, new Vector2(left + IconSize / 2f, 0f), new Vector2(IconSize, IconSize));
		text.rectTransform.localPosition = new Vector3(left + IconSize + 8f, 0f, 0f);
	}

	private void Count(float value, float dt)
	{
		if (float.IsNaN(shown))
		{
			shown = from = to = value;
			Show(value);
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
			Show(Mathf.Round(shown));
		}
	}

	private void Show(float amount)
	{
		text.text = MoneyManager.FormatAmount(amount, false, false);
		textWidth = text.preferredWidth;
	}
}
