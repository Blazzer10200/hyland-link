using System;
using System.Collections.Generic;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI.Compass;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// Restyles the game's compass: its notches are hidden and a 480-wide N/E/S/W strip with a heading marker takes their
/// place inside the same container, so it slides in and out and hides exactly like the game's. Quest and waypoint
/// markers stay the game's; they're re-placed on the strip's scale and faded at its ends. The clock pill hangs under it.
/// </summary>
internal sealed class CompassSkin : IDisposable
{
	private const float Width = 480f;

	/// <summary>The concept spaces cardinals 160 units apart: 90° per 160 units, so the strip spans ±135°.</summary>
	private const float UnitsPerDegree = 160f / 90f;

	private readonly struct Mark
	{
		internal readonly float Bearing;

		internal readonly Graphic Graphic;

		internal readonly float Alpha;

		internal Mark(float bearing, Graphic graphic, float alpha)
		{
			Bearing = bearing;
			Graphic = graphic;
			Alpha = alpha;
		}
	}

	private readonly CompassManager compass;

	private readonly Settings settings;

	private readonly float range;

	private readonly float divisor;

	private readonly List<GameObject> hiddenNotches = new List<GameObject>();

	private readonly RectTransform root;

	private readonly List<Mark> marks = new List<Mark>();

	private readonly Clock? clock;

	private float heading = float.NaN;

	private bool disposed;

	internal CompassSkin(CompassManager compass, Sprites sprites, TMP_FontAsset font, Settings settings, Action<string> log)
	{
		this.compass = compass;
		this.settings = settings;
		range = compass.CompassUIRange;
		divisor = compass.AngleDivisor;
		Il2CppSystem.Collections.Generic.List<CompassManager.Notch> notches = compass.notches;
		if (notches == null || notches.Count == 0)
		{
			throw new InvalidOperationException("The compass has no notches yet.");
		}
		RectTransform like = notches[0].Rect;
		try
		{
			// Same parent and anchors as the notches, so x = 0 is straight ahead for the strip and the game's markers alike.
			root = Ui.Rect("Hyland compass", compass.NotchUIContainer);
			root.anchorMin = like.anchorMin;
			root.anchorMax = like.anchorMax;
			root.anchoredPosition = Vector2.zero;
			root.sizeDelta = new Vector2(Width, 36f);
			for (int i = 0; i < notches.Count; i++)
			{
				GameObject notch = notches[i].Rect.gameObject;
				if (notch.activeSelf)
				{
					notch.SetActive(false);
					hiddenNotches.Add(notch);
				}
			}
			// Concept geometry, measured from the strip's top: letters at the top, dots at 12, baseline at 33, the marker
			// below it at 34–42.
			Sprite dot = sprites.Get("dot", Art.Dot, mipmaps: true);
			string[] letters = { "N", "E", "S", "W" };
			for (int i = 0; i < 4; i++)
			{
				bool north = i == 0;
				TextMeshProUGUI letter = Ui.Text(letters[i], root, font, north ? 24f : 18f, Color.white, 8f);
				letter.rectTransform.sizeDelta = new Vector2(40f, 28f);
				letter.rectTransform.localPosition = new Vector3(0f, 18f - 14f, 0f);
				marks.Add(new Mark(i * 90f, letter, north ? 1f : 0.72f));
				Image d = Ui.Image("Dot", root, dot, Color.white);
				d.rectTransform.sizeDelta = new Vector2(4f, 4f);
				d.rectTransform.localPosition = new Vector3(0f, 18f - 14f, 0f);
				marks.Add(new Mark(i * 90f + 45f, d, 0.45f));
			}
			Image line = Ui.Image("Baseline", root, sprites.Get("fadeline", Art.FadeLine), new Color(1f, 1f, 1f, 0.35f));
			Ui.Place(line.rectTransform, new Vector2(0f, 18f - 33.5f), new Vector2(Width, 1f));
			Image pointer = Ui.Image("Heading", root, sprites.Get("pointer", () => Art.Pointer(Theme.Accent)), Color.white);
			Ui.Place(pointer.rectTransform, new Vector2(0f, 18f - 38f), new Vector2(24f, 18f));
			if (settings.Clock.Value)
			{
				clock = new Clock(root, sprites, font);
			}
			// The game's own markers land on the same scale even on frames before ours runs.
			compass.CompassUIRange = Width;
			compass.AngleDivisor = Width / 2f / UnitsPerDegree;
		}
		catch
		{
			Dispose();
			throw;
		}
		log($"Compass: {notches.Count} notches hidden, container '{compass.Container.name}' at {compass.Container.anchoredPosition}, notch anchor {like.anchorMin}, canvas scale {compass.Canvas.scaleFactor:F2}.");
	}

	internal bool IsBoundTo(CompassManager instance)
	{
		return compass == instance && root != null;
	}

	internal void Update(float dt)
	{
		PlayerCamera camera = PlayerSingleton<PlayerCamera>.Instance;
		if (camera == null)
		{
			return;
		}
		Transform cam = camera.transform;
		Vector3 forward = cam.forward;
		float target = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
		heading = float.IsNaN(heading) ? target : Motion.ApproachAngle(heading, target, Mathf.Clamp(settings.CompassSmoothing.Value, 0f, 0.5f), dt);
		foreach (Mark mark in marks)
		{
			float x = Mathf.DeltaAngle(heading, mark.Bearing) * UnitsPerDegree;
			RectTransform rect = mark.Graphic.rectTransform;
			rect.localPosition = new Vector3(x, rect.localPosition.y, 0f);
			Color c = mark.Graphic.color;
			mark.Graphic.color = new Color(c.r, c.g, c.b, mark.Alpha * Art.EdgeMask((x + Width / 2f) / Width));
		}
		// The game placed its markers in Update with the raw heading; re-place them on the smoothed one and fade them at
		// the strip's ends like everything else on it.
		Il2CppSystem.Collections.Generic.List<CompassManager.Element> elements = compass.elements;
		for (int i = 0; elements != null && i < elements.Count; i++)
		{
			CompassManager.Element element = elements[i];
			if (element == null || !element.Visible || element.TargetTransform == null)
			{
				continue;
			}
			Vector3 to = element.TargetTransform.position - cam.position;
			float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
			float x = Mathf.Clamp(Mathf.DeltaAngle(heading, bearing) * UnitsPerDegree, -Width / 2f, Width / 2f);
			element.Rect.anchoredPosition = new Vector2(x, element.Rect.anchoredPosition.y);
			element.Group.alpha = Mathf.Max(0.25f, Art.EdgeMask((x + Width / 2f) / Width));
		}
		clock?.Update(dt);
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		disposed = true;
		foreach (GameObject notch in hiddenNotches)
		{
			if (notch != null)
			{
				notch.SetActive(true);
			}
		}
		if (compass != null)
		{
			compass.CompassUIRange = range;
			compass.AngleDivisor = divisor;
		}
		if (root != null)
		{
			UnityEngine.Object.Destroy(root.gameObject);
		}
	}

	/// <summary>The pill under the compass: a day, dusk or night icon (crossfading over 0.3 s) and the in-game time.</summary>
	private sealed class Clock
	{
		private readonly RectTransform pill;

		private readonly RectTransform rays;

		private readonly RectTransform duskSun;

		private readonly CanvasGroup day;

		private readonly CanvasGroup dusk;

		private readonly CanvasGroup night;

		private readonly Image moon;

		private readonly TextMeshProUGUI text;

		private int shownTime = -1;

		internal Clock(Transform parent, Sprites sprites, TMP_FontAsset font)
		{
			pill = Ui.Rect("Clock", parent);
			RectTransform glass = Ui.Image("Glass", pill, sprites.Glass(15), Color.white, sliced: true).rectTransform;
			glass.anchorMin = Vector2.zero;
			glass.anchorMax = Vector2.one;
			glass.offsetMin = Vector2.zero;
			glass.offsetMax = Vector2.zero;
			RectTransform icon = Ui.Rect("Icon", pill);
			icon.sizeDelta = new Vector2(22f, 22f);
			day = Layer("Day", icon);
			Ui.Image("Core", day.transform, sprites.Icon(Art.Icon.SunCore), Color.white);
			rays = Ui.Image("Rays", day.transform, sprites.Icon(Art.Icon.SunRays), Color.white).rectTransform;
			dusk = Layer("Dusk", icon);
			duskSun = Ui.Image("Sun", dusk.transform, sprites.Icon(Art.Icon.DuskSun), Color.white).rectTransform;
			Ui.Image("Horizon", dusk.transform, sprites.Icon(Art.Icon.DuskHorizon), Color.white);
			night = Layer("Night", icon);
			moon = Ui.Image("Moon", night.transform, sprites.Icon(Art.Icon.Moon), Color.white);
			foreach (CanvasGroup layer in new[] { day, dusk, night })
			{
				for (int i = 0; i < layer.transform.childCount; i++)
				{
					RectTransform child = layer.transform.GetChild(i).Cast<RectTransform>();
					child.sizeDelta = new Vector2(22f, 22f);
				}
			}
			text = Ui.Text("Time", pill, font, 19f, Color.white, 6f);
			text.alignment = TextAlignmentOptions.Left;
			text.rectTransform.pivot = new Vector2(0f, 0.5f);
			text.rectTransform.sizeDelta = new Vector2(120f, 24f);
		}

		private static CanvasGroup Layer(string name, Transform parent)
		{
			RectTransform rect = Ui.Rect(name, parent);
			CanvasGroup group = Ui.Group(rect.gameObject);
			group.alpha = 0f;
			return group;
		}

		internal void Update(float dt)
		{
			TimeManager time = NetworkSingleton<TimeManager>.Instance;
			if (time == null)
			{
				return;
			}
			int now = time.CurrentTime;
			if (now != shownTime)
			{
				shownTime = now;
				text.text = TimeManager.Get12HourTime(now, true);
			}
			// The game's night is 18:00–06:00; the last daylight hour gets the setting sun.
			bool isNight = time.IsNight;
			bool isDusk = !isNight && now >= 1700;
			day.alpha = Motion.Approach(day.alpha, !isNight && !isDusk ? 1f : 0f, 0.3f, dt);
			dusk.alpha = Motion.Approach(dusk.alpha, isDusk ? 1f : 0f, 0.3f, dt);
			night.alpha = Motion.Approach(night.alpha, isNight ? 1f : 0f, 0.3f, dt);
			// Concept loops: rays turn once per 24 s, the dusk sun bobs 2 units over 4 s, the moon glows .75–1 over 3 s.
			float t = Time.unscaledTime;
			rays.localEulerAngles = new Vector3(0f, 0f, -(t / 24f % 1f) * 360f);
			duskSun.localPosition = new Vector3(0f, -(1f - Mathf.Cos(t / 4f * Mathf.PI * 2f)), 0f);
			moon.color = new Color(1f, 1f, 1f, 0.875f + 0.125f * Mathf.Cos(t / 3f * Mathf.PI * 2f));
			// Pill: 8 | icon 22 | 8 | time (at least 64) | 12, 30 tall, centered 69 below the strip's top.
			float textWidth = Mathf.Max(64f, text.preferredWidth);
			float width = 8f + 22f + 8f + textWidth + 12f;
			Ui.Place(pill, new Vector2(0f, 18f - 69f), new Vector2(width, 30f));
			pill.GetChild(1).localPosition = new Vector3(-width / 2f + 8f + 11f, 0f, 0f);
			text.rectTransform.localPosition = new Vector3(-width / 2f + 38f, 0f, 0f);
		}
	}
}
