using HylandHud;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The game's progress slider (being cuffed, and every "hold to…" action) as a glass bar with the game's label, color and
/// percentage. It sits where the game's slider sits (above the hotbar), so it moves with whatever the game does there.
/// </summary>
internal sealed class ProgressBar : Surface
{
	private const float Width = 300f;

	private const float Height = 46f;

	private Muffle? muffle;

	private RectTransform? card;

	private CanvasGroup? group;

	private TextMeshProUGUI? label;

	private TextMeshProUGUI? percent;

	private Image? fill;

	private Image? glow;

	private readonly Ease appear = new Ease(0f, 0.12f);

	private readonly Ease value = new Ease(0f, 0.08f);

	private Vector2 at;

	internal ProgressBar()
		: base("progress")
	{
	}

	internal override bool On(Settings settings)
	{
		return settings.Progress.Value;
	}

	internal override void Update(Frame f)
	{
		ProgressSlider slider = Singleton<ProgressSlider>.Instance;
		if (slider == null || slider.Container == null || slider.Slider == null)
		{
			return;
		}
		if (muffle == null || !muffle.Covers(slider.Container))
		{
			Teardown();
			muffle = new Muffle(slider.Container);
		}
		muffle.Hold();
		if (card == null)
		{
			Build(f);
		}
		bool open = slider.Container.activeSelf;
		if (open)
		{
			Color c = slider.SliderFill != null ? slider.SliderFill.color : Glass.Blue;
			c.a = 1f;
			label!.text = Glass.Plain(slider.Label != null ? slider.Label.text : "");
			label.color = c;
			fill!.color = c;
			glow!.color = Glass.Tint(c, 0.35f);
			float v = Mathf.InverseLerp(slider.Slider.minValue, slider.Slider.maxValue, slider.Slider.value);
			if (appear.Value <= 0.001f)
			{
				value.Value = v;
			}
			value.Target = v;
			percent!.text = Mathf.RoundToInt(v * 100f) + "%";
			// The game's slider position, in our canvas.
			RectTransform rect = slider.Container.GetComponent<RectTransform>();
			if (rect != null)
			{
				Vector3 local = f.Root.InverseTransformPoint(rect.position);
				at = new Vector2(local.x, local.y);
			}
		}
		appear.Target = open ? 1f : 0f;
		float a = appear.Step(f.Dt);
		card!.gameObject.SetActive(a > 0.001f);
		if (a <= 0.001f)
		{
			return;
		}
		float shown = value.Step(f.Dt);
		group!.alpha = a;
		card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
		card.pivot = new Vector2(0.5f, 0.5f);
		card.anchoredPosition = at + new Vector2(0f, -6f * (1f - a));
		card.sizeDelta = new Vector2(Width, Height);
		float inner = Width - 28f;
		fill!.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, inner * shown), 6f);
		glow!.rectTransform.anchoredPosition = new Vector2(Mathf.Max(3f, inner * shown), 0f);
	}

	internal override void Teardown()
	{
		Muffle? m = muffle;
		RectTransform? c = card;
		muffle = null;
		card = null;
		appear.Value = appear.Target = 0f;
		Steps(() => m?.Release(), () => Destroy(c));
	}

	private void Build(Frame f)
	{
		Image glass = Glass.Panel("Hyland progress", f.Root, f.Sprites);
		card = glass.rectTransform;
		group = Ui.Group(card.gameObject);
		label = Glass.Label("Label", card, f.Font, 12f, Glass.Blue);
		label.fontStyle = FontStyles.Bold;
		Glass.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(Width - 80f, 18f));
		percent = Glass.Label("Percent", card, f.Font, 12f, Glass.Soft, TextAlignmentOptions.MidlineRight);
		Glass.Pin(percent.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(50f, 18f));
		Image track = Glass.Solid("Track", card, f.Sprites, 3, Glass.Hex(0xFFFFFF, 0.1f));
		Glass.Pin(track.rectTransform, new Vector2(0f, 0f), new Vector2(14f, 11f), new Vector2(Width - 28f, 6f));
		fill = Glass.Solid("Fill", track.transform, f.Sprites, 3, Glass.Blue);
		Glass.Pin(fill.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
		glow = Glass.Solid("Glow", track.transform, f.Sprites, 7, Glass.Blue);
		glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0f, 0.5f);
		glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		glow.rectTransform.sizeDelta = new Vector2(14f, 14f);
	}
}
