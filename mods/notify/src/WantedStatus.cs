using HylandHud;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The police status (the game's <c>CrimeStatusUI</c>): investigating, under arrest, wanted, wanted dead or alive, or a
/// pending body search, as a glass pill under the compass. While the police see you the dot beats fast; once they lose
/// you, the bar under it drains for the time they keep searching, the same fill the game shows.
/// </summary>
internal sealed class WantedStatus : Surface
{
	private const float Height = 40f;

	private Muffle? muffle;

	private RectTransform? pill;

	private CanvasGroup? group;

	private Image? dot;

	private Image? halo;

	private TextMeshProUGUI? label;

	private TextMeshProUGUI? detail;

	private Image? track;

	private Image? fill;

	private readonly Ease appear = new Ease(0f, 0.25f);

	private readonly Ease width = new Ease(220f, 0.2f);

	private Color color = Glass.Yellow;

	private string lastLabel = "";

	private float changedAt;

	internal WantedStatus()
		: base("wanted")
	{
	}

	internal override bool On(Settings settings)
	{
		return settings.Wanted.Value;
	}

	internal override void Update(Frame f)
	{
		CrimeStatusUI ui = f.Hud.CrimeStatusUI;
		Player player = Player.Local;
		if (ui == null || player.CrimeData == null)
		{
			return;
		}
		if (muffle == null || !muffle.Covers(ui.gameObject))
		{
			Teardown();
			muffle = new Muffle(ui.gameObject);
		}
		muffle.Hold();
		if (pill == null)
		{
			Build(f);
		}
		PlayerCrimeData crime = player.CrimeData;
		PlayerCrimeData.EPursuitLevel level = crime.CurrentPursuitLevel;
		bool pursued = level != PlayerCrimeData.EPursuitLevel.None;
		bool search = !pursued && crime.BodySearchPending;
		bool sighted = pursued && crime.TimeSinceSighted < 3f;
		string text;
		Color target;
		switch (level)
		{
		case PlayerCrimeData.EPursuitLevel.Investigating:
			text = "INVESTIGATING";
			target = Glass.Yellow;
			break;
		case PlayerCrimeData.EPursuitLevel.Arresting:
			text = "UNDER ARREST";
			target = Glass.Orange;
			break;
		case PlayerCrimeData.EPursuitLevel.NonLethal:
			text = "WANTED";
			target = Glass.Red;
			break;
		case PlayerCrimeData.EPursuitLevel.Lethal:
			text = "WANTED DEAD OR ALIVE";
			target = Glass.Hex(0xFF5B5B);
			break;
		default:
			text = "BODY SEARCH";
			target = Glass.Blue;
			break;
		}
		appear.Target = pursued || search ? 1f : 0f;
		float a = appear.Step(f.Dt);
		pill!.gameObject.SetActive(a > 0.001f);
		if (a <= 0.001f)
		{
			lastLabel = "";
			return;
		}
		if (pursued || search)
		{
			if (text != lastLabel)
			{
				changedAt = Time.unscaledTime;
				lastLabel = text;
			}
			label!.text = text;
			// The game's own drain: full while seen, then empties over the search time.
			float search01 = 1f - Mathf.Clamp01((crime.TimeSinceSighted - 3f) / Mathf.Max(0.01f, crime.GetSearchTime()));
			bool bar = pursued;
			track!.gameObject.SetActive(bar);
			if (bar)
			{
				float inner = width.Value - 32f;
				fill!.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, inner * search01), 3f);
				fill.color = target;
			}
			detail!.text = search ? "An officer wants to search you" : sighted ? "Seen" : "Searching · " + Clock(Mathf.Max(0f, crime.GetSearchTime() - (crime.TimeSinceSighted - 3f)));
		}
		color = Color.Lerp(color, target, 1f - Mathf.Exp(-8f * f.Dt));
		label!.color = color;
		width.Target = Mathf.Max(200f, Mathf.Ceil(Mathf.Max(label.preferredWidth, detail!.preferredWidth)) + 64f);
		float w = width.Step(f.Dt);
		group!.alpha = a;
		// Drops in from under the compass; a new level gives a small pop.
		float pop = Mathf.Clamp01((Time.unscaledTime - changedAt) / 0.3f);
		float scale = (0.92f + 0.08f * HylandHud.Motion.EaseOutCubic(a)) * (1f + 0.06f * Mathf.Sin(pop * Mathf.PI));
		Glass.Pin(pill, new Vector2(0.5f, 1f), new Vector2(0f, -(f.CenterY - 10f * (1f - a))), new Vector2(w, Height));
		pill.localScale = Vector3.one * scale;
		track!.rectTransform.sizeDelta = new Vector2(w - 32f, 3f);
		// The beat: fast while seen, slow while they search.
		float period = sighted ? 0.5f : 1.4f;
		float t = Time.unscaledTime % period / period;
		dot!.color = color;
		halo!.color = Glass.Tint(color, 0.5f * (1f - t));
		float s = 10f + 12f * HylandHud.Motion.EaseOutCubic(t);
		halo.rectTransform.sizeDelta = new Vector2(s, s);
		f.CenterY += (Height + ScreenLayout.Gap) * a;
	}

	internal override void Teardown()
	{
		Muffle? m = muffle;
		RectTransform? p = pill;
		muffle = null;
		pill = null;
		appear.Value = appear.Target = 0f;
		lastLabel = "";
		Steps(() => m?.Release(), () => Destroy(p));
	}

	private void Build(Frame f)
	{
		Image glass = Glass.Panel("Hyland police status", f.Root, f.Sprites, Height / 2f);
		pill = glass.rectTransform;
		group = Ui.Group(pill.gameObject);
		halo = Glass.Solid("Halo", pill, f.Sprites, 11, Glass.Yellow);
		Middle(halo.rectTransform, new Vector2(20f, -15f), 10f);
		dot = Glass.Solid("Dot", pill, f.Sprites, 5, Glass.Yellow);
		Middle(dot.rectTransform, new Vector2(20f, -15f), 10f);
		label = Glass.Label("Label", pill, f.Font, 13f, Glass.Yellow);
		label.fontStyle = FontStyles.Bold;
		label.characterSpacing = 10f;
		label.rectTransform.anchorMin = new Vector2(0f, 1f);
		label.rectTransform.anchorMax = new Vector2(1f, 1f);
		label.rectTransform.pivot = new Vector2(0f, 1f);
		label.rectTransform.anchoredPosition = new Vector2(34f, -6f);
		label.rectTransform.sizeDelta = new Vector2(-46f, 18f);
		detail = Glass.Label("Detail", pill, f.Font, 11f, Glass.Muted);
		detail.rectTransform.anchorMin = new Vector2(0f, 1f);
		detail.rectTransform.anchorMax = new Vector2(1f, 1f);
		detail.rectTransform.pivot = new Vector2(0f, 1f);
		detail.rectTransform.anchoredPosition = new Vector2(34f, -22f);
		detail.rectTransform.sizeDelta = new Vector2(-46f, 14f);
		track = Glass.Solid("Track", pill, f.Sprites, 1.5, Glass.Hex(0xFFFFFF, 0.1f));
		track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(0.5f, 0f);
		track.rectTransform.pivot = new Vector2(0.5f, 0f);
		track.rectTransform.anchoredPosition = new Vector2(0f, 3f);
		fill = Glass.Solid("Fill", track.transform, f.Sprites, 1.5, Glass.Yellow);
		fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
		fill.rectTransform.pivot = new Vector2(0f, 0.5f);
		fill.rectTransform.anchoredPosition = Vector2.zero;
	}

	private static void Middle(RectTransform rect, Vector2 at, float size)
	{
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = at;
		rect.sizeDelta = new Vector2(size, size);
	}

	private static string Clock(float seconds)
	{
		int s = Mathf.CeilToInt(seconds);
		return s / 60 + ":" + (s % 60).ToString("00");
	}
}
