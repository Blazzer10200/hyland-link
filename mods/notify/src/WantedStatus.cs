using HylandHud;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The police status (the game's <c>CrimeStatusUI</c>) as the board's status box under the compass: a tinted icon badge,
/// the status in its color with a line under it, and a border in that color. Wanted and wanted dead or alive pulse an
/// alarm ring. Once the police lose sight of you, the line counts down the time they keep searching (the game's own
/// search time). Being cuffed shows on the progress bar, not here.
/// </summary>
internal sealed class WantedStatus : Surface
{
	private sealed class Look
	{
		internal readonly string Label;

		internal readonly string Line;

		internal readonly Color Color;

		internal readonly Icons.Kind Icon;

		/// <summary>Seconds per alarm ring; 0 for none.</summary>
		internal readonly float Alarm;

		internal Look(string label, string line, Color color, Icons.Kind icon, float alarm)
		{
			Label = label;
			Line = line;
			Color = color;
			Icon = icon;
			Alarm = alarm;
		}
	}

	private static readonly Look Investigating = new Look("INVESTIGATING", "Police are looking for you", Glass.Blue, Icons.Kind.Eye, 0f);

	private static readonly Look Arresting = new Look("UNDER ARREST", "Break free before they cuff you", Glass.Blue, Icons.Kind.Cuff, 0f);

	private static readonly Look Wanted = new Look("WANTED", "Lose them or hide", Glass.Orange, Icons.Kind.Siren, 1.4f);

	private static readonly Look Lethal = new Look("WANTED DEAD OR ALIVE", "Police will shoot", Glass.Hex(0xFF6B6B), Icons.Kind.Siren, 1f);

	private static readonly Look Search = new Look("BODY SEARCH", "An officer wants to search you", Glass.Blue, Icons.Kind.Hand, 0f);

	private const float Height = 46f;

	private const float Radius = 10f;

	private Muffle? muffle;

	private RectTransform? box;

	private CanvasGroup? group;

	private Image? border;

	private Image? alarm;

	private Image? badge;

	private Image? icon;

	private TextMeshProUGUI? label;

	private TextMeshProUGUI? line;

	private readonly Ease appear = new Ease(0f, 0.25f);

	private readonly Ease width = new Ease(220f, 0.2f);

	private Color color = Glass.Blue;

	private Look? shown;

	private float changedAt;

	private float alarmAt;

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
		if (box == null)
		{
			Build(f);
		}
		PlayerCrimeData crime = player.CrimeData;
		PlayerCrimeData.EPursuitLevel level = crime.CurrentPursuitLevel;
		bool pursued = level != PlayerCrimeData.EPursuitLevel.None;
		Look? look = level switch
		{
			PlayerCrimeData.EPursuitLevel.Investigating => Investigating,
			PlayerCrimeData.EPursuitLevel.Arresting => Arresting,
			PlayerCrimeData.EPursuitLevel.NonLethal => Wanted,
			PlayerCrimeData.EPursuitLevel.Lethal => Lethal,
			_ => crime.BodySearchPending ? Search : null
		};
		appear.Target = look != null ? 1f : 0f;
		float a = appear.Step(f.Dt);
		box!.gameObject.SetActive(a > 0.001f);
		if (a <= 0.001f)
		{
			shown = null;
			return;
		}
		if (look != null)
		{
			if (look != shown)
			{
				changedAt = Time.unscaledTime;
				alarmAt = Time.unscaledTime;
				shown = look;
				label!.text = look.Label;
				icon!.sprite = Icons.Get(f.Sprites, look.Icon);
			}
			// The game's own rule: seen in the last 3 s, else they search for GetSearchTime() more.
			bool lost = pursued && crime.TimeSinceSighted >= 3f;
			line!.text = lost ? "Searching · " + Clock(Mathf.Max(0f, crime.GetSearchTime() - (crime.TimeSinceSighted - 3f))) : look.Line;
		}
		Look current = shown!;
		color = Color.Lerp(color, current.Color, 1f - Mathf.Exp(-7f * f.Dt));
		label!.color = color;
		icon!.color = color;
		badge!.color = Glass.Tint(color, 0.17f);
		border!.color = Glass.Tint(color, 0.85f);
		width.Target = Mathf.Ceil(Mathf.Max(label.preferredWidth, line!.preferredWidth)) + 10f + 26f + 10f + 14f;
		float w = width.Step(f.Dt);
		group!.alpha = a;
		// Drops in from under the compass; a new status gives a small pop.
		float pop = Mathf.Clamp01((Time.unscaledTime - changedAt) / 0.3f);
		float scale = (0.92f + 0.08f * HylandHud.Motion.EaseOutCubic(a)) * (1f + 0.05f * Mathf.Sin(pop * Mathf.PI));
		Glass.Pin(box, new Vector2(0.5f, 1f), new Vector2(0f, -(f.CenterY - 10f * (1f - a))), new Vector2(w, Height));
		box.localScale = Vector3.one * scale;
		// The alarm: a ring in the status color grows 10 out from the box and fades, like the board's box-shadow.
		bool ringing = current.Alarm > 0f;
		alarm!.gameObject.SetActive(ringing);
		if (ringing)
		{
			float t = (Time.unscaledTime - alarmAt) % current.Alarm / current.Alarm;
			float grow = 10f * HylandHud.Motion.EaseOutCubic(t);
			alarm.rectTransform.sizeDelta = new Vector2(grow * 2f, grow * 2f);
			alarm.color = Glass.Tint(color, 0.55f * (1f - t));
		}
		f.CenterY += (Height + ScreenLayout.Gap) * a;
	}

	internal override void Teardown()
	{
		Muffle? m = muffle;
		RectTransform? b = box;
		muffle = null;
		box = null;
		appear.Value = appear.Target = 0f;
		shown = null;
		Steps(() => m?.Release(), () => Destroy(b));
	}

	private void Build(Frame f)
	{
		Image glass = Glass.Panel("Hyland police status", f.Root, f.Sprites, Radius);
		box = glass.rectTransform;
		group = Ui.Group(box.gameObject);
		alarm = Ui.Image("Alarm", box, Icons.Outline(f.Sprites, Radius), Glass.Orange, sliced: true);
		Glass.Fill(alarm.rectTransform);
		border = Ui.Image("Border", box, Icons.Outline(f.Sprites, Radius), Glass.Blue, sliced: true);
		Glass.Fill(border.rectTransform);
		badge = Glass.Solid("Badge", box, f.Sprites, 13, Glass.Tint(Glass.Blue, 0.17f));
		badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 0.5f);
		badge.rectTransform.pivot = new Vector2(0f, 0.5f);
		badge.rectTransform.anchoredPosition = new Vector2(10f, 0f);
		badge.rectTransform.sizeDelta = new Vector2(26f, 26f);
		icon = Ui.Image("Icon", badge.transform, null, Glass.Blue);
		icon.preserveAspect = true;
		icon.rectTransform.sizeDelta = new Vector2(16f, 16f);
		label = Glass.Label("Label", box, f.Font, 13f, Glass.Blue);
		label.fontStyle = FontStyles.Bold;
		label.characterSpacing = 6f;
		Row(label.rectTransform, 6f, 19f);
		line = Glass.Label("Line", box, f.Font, 11f, Glass.Soft);
		Row(line.rectTransform, 24f, 16f);
	}

	/// <summary>A text row right of the badge, stretched to the box's width. Heights are a full line for the font
	/// size: a shorter rect makes TMP drop the line.</summary>
	private static void Row(RectTransform rect, float top, float height)
	{
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = new Vector2(46f, -top);
		rect.sizeDelta = new Vector2(-46f - 14f, height);
	}

	private static string Clock(float seconds)
	{
		int s = Mathf.CeilToInt(seconds);
		return s / 60 + ":" + (s % 60).ToString("00");
	}
}
