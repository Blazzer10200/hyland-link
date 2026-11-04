using HylandHud;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The top-right corner: the save indicator ("Saving…", then "Game saved") and the game's hints. Both sit under a
/// top-right minimap (the game puts them exactly where the map is) and top right otherwise. Hints keep the game's text,
/// colors and timing; dismissing stays the game's key.
/// </summary>
internal sealed class CornerStack : Surface
{
	private const float HintWidth = 340f;

	private const float SavedHeight = 30f;

	private const float SavedShow = 2.5f;

	private Muffle? hintMuffle;

	private Muffle? saveMuffle;

	private SaveIndicator? indicator;

	private float nextFind;

	private RectTransform? saved;

	private CanvasGroup? savedGroup;

	private TextMeshProUGUI? savedLabel;

	private RectTransform? spinner;

	private RectTransform? check;

	private readonly Ease savedAppear = new Ease(0f, 0.2f);

	private readonly Ease savedWidth = new Ease(120f, 0.2f);

	private bool wasSaving;

	private float savedAt = -100f;

	private RectTransform? hint;

	private CanvasGroup? hintGroup;

	private TextMeshProUGUI? hintLabel;

	private Image? hintAccent;

	private readonly Ease hintAppear = new Ease(0f, 0.25f);

	private readonly Ease hintHeight = new Ease(48f, 0.2f);

	private string lastHint = "";

	private float hintAt;

	internal CornerStack()
		: base("corner")
	{
	}

	internal override bool On(Settings settings)
	{
		return settings.Hints.Value || settings.Saved.Value;
	}

	internal override void Update(Frame f)
	{
		if (saved == null)
		{
			Build(f);
		}
		if (f.Settings.Saved.Value)
		{
			UpdateSaved(f);
		}
		else
		{
			ReleaseSaved();
		}
		if (f.Settings.Hints.Value)
		{
			UpdateHint(f);
		}
		else
		{
			ReleaseHint();
		}
	}

	internal override void Teardown()
	{
		Muffle? h = hintMuffle;
		Muffle? s = saveMuffle;
		RectTransform? a = saved;
		RectTransform? b = hint;
		hintMuffle = null;
		saveMuffle = null;
		indicator = null;
		saved = null;
		hint = null;
		savedAppear.Value = savedAppear.Target = 0f;
		hintAppear.Value = hintAppear.Target = 0f;
		lastHint = "";
		wasSaving = false;
		savedAt = -100f;
		Steps(() => h?.Release(), () => s?.Release(), () => Destroy(a), () => Destroy(b));
	}

	private void UpdateSaved(Frame f)
	{
		SaveManager manager = Singleton<SaveManager>.Instance;
		if (indicator == null && Time.unscaledTime >= nextFind)
		{
			nextFind = Time.unscaledTime + 2f;
			indicator = Object.FindObjectOfType<SaveIndicator>();
		}
		if (indicator != null && indicator.Canvas != null)
		{
			if (saveMuffle == null || !saveMuffle.Covers(indicator.Canvas.gameObject))
			{
				saveMuffle?.Release();
				// The game's indicator is off between saves; it switches itself on for the next one.
				saveMuffle = new Muffle(indicator.Canvas.gameObject, () => false);
			}
			saveMuffle.Hold();
		}
		bool saving = manager != null && manager.IsSaving;
		if (wasSaving && !saving)
		{
			savedAt = Time.unscaledTime;
		}
		wasSaving = saving;
		bool done = !saving && Time.unscaledTime - savedAt < SavedShow;
		savedAppear.Target = saving || done ? 1f : 0f;
		float a = savedAppear.Step(f.Dt);
		saved!.gameObject.SetActive(a > 0.001f);
		if (a <= 0.001f)
		{
			return;
		}
		savedLabel!.text = saving ? "Saving…" : "Game saved";
		savedLabel.color = saving ? Glass.Soft : Glass.Green;
		spinner!.gameObject.SetActive(saving);
		check!.gameObject.SetActive(!saving);
		if (saving)
		{
			spinner.localRotation = Quaternion.Euler(0f, 0f, -360f * Time.unscaledTime);
		}
		else
		{
			float pop = Mathf.Clamp01((Time.unscaledTime - savedAt) / 0.3f);
			check.localScale = Vector3.one * (pop < 0.6f ? Mathf.Lerp(0.3f, 1.15f, pop / 0.6f) : Mathf.Lerp(1.15f, 1f, (pop - 0.6f) / 0.4f));
		}
		savedWidth.Target = Mathf.Ceil(savedLabel.preferredWidth) + 46f;
		float w = savedWidth.Step(f.Dt);
		savedGroup!.alpha = a;
		Glass.Pin(saved, new Vector2(1f, 1f), new Vector2(-f.Layout.CornerRight + 16f * (1f - HylandHud.Motion.EaseOutCubic(a)), -f.CornerY), new Vector2(w, SavedHeight));
		f.CornerY += (SavedHeight + ScreenLayout.Gap) * a;
	}

	private void UpdateHint(Frame f)
	{
		HintDisplay display = Singleton<HintDisplay>.Instance;
		if (display == null || display.Container == null || display.Label == null)
		{
			return;
		}
		if (hintMuffle == null || !hintMuffle.Covers(display.Container.gameObject))
		{
			hintMuffle?.Release();
			hintMuffle = new Muffle(display.Container.gameObject);
		}
		hintMuffle.Hold();
		bool open = display.IsOpen;
		if (open)
		{
			string text = display.Label.text ?? "";
			if (text != lastHint)
			{
				lastHint = text;
				hintAt = Time.unscaledTime;
				hintLabel!.text = text;
				Vector2 size = hintLabel.GetPreferredValues(text, HintWidth - 40f, 0f);
				hintHeight.Target = Mathf.Max(44f, Mathf.Ceil(size.y) + 26f);
			}
		}
		hintAppear.Target = open ? 1f : 0f;
		float a = hintAppear.Step(f.Dt);
		hint!.gameObject.SetActive(a > 0.001f);
		if (a <= 0.001f)
		{
			lastHint = "";
			return;
		}
		float h = hintHeight.Step(f.Dt);
		hintGroup!.alpha = a;
		// A new hint pulses the accent bar once, the board's "flash".
		float flash = Mathf.Clamp01((Time.unscaledTime - hintAt) / 0.6f);
		hintAccent!.color = Glass.Tint(Glass.Blue, 0.55f + 0.45f * Mathf.Sin((1f - flash) * Mathf.PI * 0.5f));
		Glass.Pin(hint, new Vector2(1f, 1f), new Vector2(-f.Layout.CornerRight + 20f * (1f - HylandHud.Motion.EaseOutCubic(a)), -f.CornerY), new Vector2(HintWidth, h));
		f.CornerY += (h + ScreenLayout.Gap) * a;
	}

	private void ReleaseSaved()
	{
		Muffle? s = saveMuffle;
		saveMuffle = null;
		indicator = null;
		saved?.gameObject.SetActive(false);
		s?.Release();
	}

	private void ReleaseHint()
	{
		Muffle? h = hintMuffle;
		hintMuffle = null;
		hint?.gameObject.SetActive(false);
		lastHint = "";
		h?.Release();
	}

	private void Build(Frame f)
	{
		Image savedGlass = Glass.Panel("Hyland saved", f.Root, f.Sprites, SavedHeight / 2f);
		saved = savedGlass.rectTransform;
		savedGroup = Ui.Group(saved.gameObject);
		// A spinner: three dots round a circle; a check for "saved".
		spinner = Ui.Rect("Spinner", saved);
		Left(spinner, 18f, 14f);
		for (int i = 0; i < 3; i++)
		{
			float angle = i * 120f * Mathf.Deg2Rad;
			Image dot = Glass.Solid("Dot", spinner, f.Sprites, 2, Glass.Tint(Glass.Soft, 1f - i * 0.3f));
			dot.rectTransform.sizeDelta = new Vector2(4f, 4f);
			dot.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * 5f, Mathf.Sin(angle) * 5f);
		}
		check = Glass.Solid("Check", saved, f.Sprites, 8, Glass.Hex(0x2E8F55)).rectTransform;
		Left(check, 18f, 16f);
		Image shortBar = Glass.Solid("Tick1", check, f.Sprites, 1, Color.white);
		shortBar.rectTransform.sizeDelta = new Vector2(5f, 2f);
		shortBar.rectTransform.anchoredPosition = new Vector2(-2.5f, -1f);
		shortBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
		Image longBar = Glass.Solid("Tick2", check, f.Sprites, 1, Color.white);
		longBar.rectTransform.sizeDelta = new Vector2(9f, 2f);
		longBar.rectTransform.anchoredPosition = new Vector2(1.5f, 0.5f);
		longBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
		savedLabel = Glass.Label("Label", saved, f.Font, 12f, Glass.Soft);
		savedLabel.fontStyle = FontStyles.Bold;
		savedLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
		savedLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
		savedLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
		savedLabel.rectTransform.anchoredPosition = new Vector2(34f, 0f);
		savedLabel.rectTransform.sizeDelta = new Vector2(-40f, 0f);
		savedLabel.overflowMode = TextOverflowModes.Overflow;
		saved.gameObject.SetActive(false);

		Image hintGlass = Glass.Panel("Hyland hint", f.Root, f.Sprites);
		hint = hintGlass.rectTransform;
		hintGroup = Ui.Group(hint.gameObject);
		hintAccent = Glass.Solid("Accent", hint, f.Sprites, 1.5, Glass.Blue);
		hintAccent.rectTransform.anchorMin = new Vector2(0f, 0f);
		hintAccent.rectTransform.anchorMax = new Vector2(0f, 1f);
		hintAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
		hintAccent.rectTransform.anchoredPosition = new Vector2(12f, 0f);
		hintAccent.rectTransform.sizeDelta = new Vector2(3f, -24f);
		hintLabel = Glass.Label("Text", hint, f.Font, 14f, Glass.Text, TextAlignmentOptions.TopLeft);
		hintLabel.enableWordWrapping = true;
		hintLabel.overflowMode = TextOverflowModes.Overflow;
		hintLabel.rectTransform.anchorMin = Vector2.zero;
		hintLabel.rectTransform.anchorMax = Vector2.one;
		hintLabel.rectTransform.pivot = new Vector2(0f, 1f);
		hintLabel.rectTransform.offsetMin = new Vector2(26f, 12f);
		hintLabel.rectTransform.offsetMax = new Vector2(-14f, -13f);
		hint.gameObject.SetActive(false);
	}

	private static void Left(RectTransform rect, float x, float size)
	{
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = new Vector2(x, 0f);
		rect.sizeDelta = new Vector2(size, size);
	}
}
