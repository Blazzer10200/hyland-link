using System;
using System.Collections.Generic;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Law;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Compass;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// Restyles the game's compass: its notches are hidden and a 480-wide N/E/S/W strip with a heading marker takes their
/// place inside the same container, so it slides in and out and hides exactly like the game's. Quest and waypoint
/// markers stay the game's; they're re-placed on the strip's scale and faded at its ends. Co-op friends get their own
/// markers, and the clock pill hangs under it.
/// </summary>
internal sealed class CompassSkin : IDisposable
{
	private const float Width = 480f;

	/// <summary>The concept spaces cardinals 160 units apart: 90° per 160 units, so the strip spans ±135°.</summary>
	private const float UnitsPerDegree = 160f / 90f;

	/// <summary>The strip's top edge in its own local space (the root is 36 tall, centered).</summary>
	private const float Top = 18f;

	private readonly struct Mark
	{
		internal readonly float Bearing;

		internal readonly Graphic Graphic;

		/// <summary>The graphic's RectTransform, fetched once: every read through IL2CPP makes a new wrapper object.</summary>
		internal readonly RectTransform Rect;

		internal readonly float Alpha;

		internal Mark(float bearing, Graphic graphic, float alpha)
		{
			Bearing = bearing;
			Graphic = graphic;
			Rect = graphic.rectTransform;
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

	private readonly Friends? friends;

	private float heading = float.NaN;

	private bool disposed;
	internal CompassSkin(CompassManager compass, HUD hud, Sprites sprites, TMP_FontAsset font, Settings settings, Action<string> log)
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
			// In the compass Container, centered: the notches' NotchContainer fills it exactly (same center, so x = 0 is straight
			// ahead for the strip and the game's markers alike), but it carries a RectMask2D cut to its 30-unit height, which
			// clipped the heading marker, friends and clock below the strip. The Container slides and hides the compass, so
			// the strip still moves with it.
			root = Ui.Rect("Hyland compass", compass.Container);
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
				letter.text = letters[i];
				letter.rectTransform.sizeDelta = new Vector2(40f, 28f);
				letter.rectTransform.localPosition = new Vector3(0f, Top - 14f, 0f);
				marks.Add(new Mark(i * 90f, letter, north ? 1f : 0.72f));
				Image d = Ui.Image("Dot", root, dot, Color.white);
				d.rectTransform.sizeDelta = new Vector2(4f, 4f);
				d.rectTransform.localPosition = new Vector3(0f, Top - 14f, 0f);
				marks.Add(new Mark(i * 90f + 45f, d, 0.45f));
			}
			Image line = Ui.Image("Baseline", root, sprites.Get("fadeline", Art.FadeLine), new Color(1f, 1f, 1f, 0.35f));
			Ui.Place(line.rectTransform, new Vector2(0f, Top - 33.5f), new Vector2(Width, 1f));
			if (settings.Friends.Value)
			{
				friends = new Friends(Ui.Rect("Friends", root), dot, font);
			}
			// After the friends' layer (their markers come and go inside it), so the heading marker and the clock pill draw
			// over a friend straight ahead.
			Image pointer = Ui.Image("Heading", root, sprites.Get("pointer", () => Art.Pointer(Theme.Accent)), Color.white);
			Ui.Place(pointer.rectTransform, new Vector2(0f, Top - 38f), new Vector2(24f, 18f));
			if (settings.Clock.Value)
			{
				clock = new Clock(root, sprites, font, settings.TimeWarnings.Value ? hud : null);
			}
			// The game's own markers land on the same scale even on frames before ours runs.
			compass.CompassUIRange = Width;
			compass.AngleDivisor = Width / 2f / UnitsPerDegree;
			// Inside the try: it's the first touch of Container and Canvas, and a throw here must still undo the above.
			log($"Compass: {notches.Count} notches hidden, container '{compass.Container.name}' at {compass.Container.anchoredPosition}, notch anchor {like.anchorMin}, canvas scale {compass.Canvas.scaleFactor:F2}.");		}
		catch
		{
			Dispose();
			throw;
		}
	}

	internal bool IsBoundTo(CompassManager instance)
	{
		return compass == instance && root != null;
	}

	/// <summary>Strip x for a world bearing, clamped to its ends.</summary>
	private float StripX(float bearing)
	{
		return Mathf.Clamp(Mathf.DeltaAngle(heading, bearing) * UnitsPerDegree, -Width / 2f, Width / 2f);
	}

	private static float Bearing(Vector3 from, Vector3 to)
	{
		Vector3 d = to - from;
		return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
	}

	internal void Update(float dt)
	{
		// First: it doesn't need the camera, and it decides whether the game's own curfew prompts show.
		clock?.Update(dt);
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
			RectTransform rect = mark.Rect;
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
			float x = StripX(Bearing(cam.position, element.TargetTransform.position));
			element.Rect.anchoredPosition = new Vector2(x, element.Rect.anchoredPosition.y);
			element.Group.alpha = Mathf.Max(0.25f, Art.EdgeMask((x + Width / 2f) / Width));
		}
		friends?.Update(this, cam.position);
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		disposed = true;
		// Every step runs even if one throws (a half-destroyed scene), so the game's compass always comes back.
		Exception? failure = null;
		if (clock != null)
		{
			Ui.Step(ref failure, clock.Dispose);
		}
		friends?.Dispose();
		foreach (GameObject notch in hiddenNotches)
		{
			Ui.Step(ref failure, () =>
			{
				if (notch != null)
				{
					notch.SetActive(true);
				}
			});
		}
		if (compass != null)
		{
			Ui.Step(ref failure, () =>
			{
				compass.CompassUIRange = range;
				compass.AngleDivisor = divisor;
			});
			// We faded markers toward the strip's ends; the game only sets their alpha when one is shown again, so put it
			// back for the ones on screen now.
			Ui.Step(ref failure, () =>
			{
				Il2CppSystem.Collections.Generic.List<CompassManager.Element> elements = compass.elements;
				for (int i = 0; elements != null && i < elements.Count; i++)
				{
					CompassManager.Element element = elements[i];
					if (element != null && element.Visible && element.Group != null)
					{
						element.Group.alpha = 1f;
					}
				}
			});
		}
		if (root != null)
		{
			Ui.Step(ref failure, () => UnityEngine.Object.Destroy(root.gameObject));
		}
		Ui.Rethrow(failure);
	}

	/// <summary>
	/// The pill under the compass: a day, dusk or night icon (crossfading over 0.3 s), the in-game time and, when there is
	/// one, a warning tag (<see cref="TimeWarnings"/>). With warnings on, the game's own curfew and 4 AM text prompts are
	/// hidden while the pill is on screen, since the tag says the same thing. The game hides the whole compass in menus and
	/// on the phone; then its own prompts show again, so a warning is never lost.
	/// </summary>
	private sealed class Clock : IDisposable
	{
		private static readonly Color Amber = new Color(1f, 0.71f, 0.28f, 1f);

		private static readonly Color Red = new Color(1f, 0.42f, 0.37f, 1f);

		private readonly RectTransform pill;

		private readonly RectTransform icon;

		private readonly RectTransform rays;

		private readonly RectTransform duskSun;

		private readonly CanvasGroup day;

		private readonly CanvasGroup dusk;

		private readonly CanvasGroup night;

		private readonly Image moon;

		private readonly TextMeshProUGUI text;

		private readonly RectTransform? tag;

		private readonly CanvasGroup? tagGroup;

		private readonly Image? tagBack;

		private readonly TextMeshProUGUI? tagText;

		/// <summary>What draws the game's curfew and 4 AM prompts, with whether each was on, to put back.</summary>
		private readonly List<(Graphic Graphic, bool Enabled)> prompts = new List<(Graphic, bool)>();

		private bool promptsHidden;

		private readonly GameObject pillObject;

		private int shownTime = -1;

		/// <summary>Widths measured only when their text changes (TMP re-measures on every read).</summary>
		private float timeWidth;

		private float tagWidth;

		/// <summary>The inputs the warning was last worked out from; it's only redone (a new string) when one changes.</summary>
		private (int Now, bool Enabled, bool Active) warningFor = (-1, false, false);

		private TimeWarning? warning;

		private string tagShown = "";

		private float tagIn;

		private float width;

		internal Clock(Transform parent, Sprites sprites, TMP_FontAsset font, HUD? hudForWarnings)
		{
			pill = Ui.Rect("Clock", parent);
			pillObject = pill.gameObject;
			RectTransform glass = Ui.Image("Glass", pill, sprites.Glass(15), Color.white, sliced: true).rectTransform;
			glass.anchorMin = Vector2.zero;
			glass.anchorMax = Vector2.one;
			glass.offsetMin = Vector2.zero;
			glass.offsetMax = Vector2.zero;
			icon = Ui.Rect("Icon", pill);
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
			if (hudForWarnings != null)
			{
				tag = Ui.Rect("Warning", pill);
				tagGroup = Ui.Group(tag.gameObject);
				tagGroup.alpha = 0f;
				// The white slot panel, tinted: an amber or red stroke over a third-strength fill.
				tagBack = Ui.Image("Back", tag, sprites.Slot(9), Amber, sliced: true);
				RectTransform back = tagBack.rectTransform;
				back.anchorMin = Vector2.zero;
				back.anchorMax = Vector2.one;
				back.offsetMin = Vector2.zero;
				back.offsetMax = Vector2.zero;
				tagText = Ui.Text("Text", tag, font, 13f, Color.white, 6f);
				tagText.rectTransform.sizeDelta = new Vector2(160f, 20f);
				// Only collected here; Update hides them once the pill is actually showing, so a build that fails part way
				// never leaves them off.
				Collect(hudForWarnings.CurfewPrompt);
				Collect(hudForWarnings.SleepPrompt);
			}
		}

		private void Collect(TextMeshProUGUI prompt)
		{
			if (prompt == null)
			{
				return;
			}
			foreach (Graphic graphic in prompt.GetComponentsInChildren<Graphic>(true))
			{
				prompts.Add((graphic, graphic.enabled));
			}
		}

		/// <summary>The game turns these prompts' objects on and off every frame (HUD.Update), so this switches off what
		/// draws them instead, and puts each back as it was.</summary>
		private void HidePrompts(bool hide)
		{
			if (hide == promptsHidden)
			{
				return;
			}
			promptsHidden = hide;
			foreach ((Graphic graphic, bool enabled) in prompts)
			{
				if (graphic != null)
				{
					graphic.enabled = !hide && enabled;
				}
			}
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
			HidePrompts(pillObject.activeInHierarchy);
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
				timeWidth = text.preferredWidth;
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
			float tagWidth = UpdateTag(now, dt);
			// Pill: 8 | icon 22 | 8 | time (at least 64) | [8 | tag] | 12, 30 tall, centered 69 below the strip's top. The
			// width eases, so a tag appearing grows the pill instead of snapping it.
			float target = 8f + 22f + 8f + Mathf.Max(64f, timeWidth) + 12f + (tagWidth > 0f ? 8f + tagWidth : 0f);
			width = width <= 0f ? target : Motion.Approach(width, target, 0.18f, dt);
			Ui.Place(pill, new Vector2(0f, Top - 69f), new Vector2(width, 30f));
			icon.localPosition = new Vector3(-width / 2f + 8f + 11f, 0f, 0f);
			text.rectTransform.localPosition = new Vector3(-width / 2f + 38f, 0f, 0f);
			if (tag != null)
			{
				Ui.Place(tag, new Vector2(width / 2f - 7f - tagWidth / 2f, 0f), new Vector2(tagWidth, 20f));
			}
		}

		/// <summary>Shows the current warning (fading in over 0.22 s) and returns its width, or 0 for none.</summary>
		private float UpdateTag(int now, float dt)
		{
			if (tag == null || tagText == null || tagBack == null || tagGroup == null)
			{
				return 0f;
			}
			CurfewManager curfew = NetworkSingleton<CurfewManager>.Instance;
			(int, bool, bool) inputs = (now, curfew != null && curfew.IsEnabled, curfew != null && curfew.IsCurrentlyActive);
			if (inputs != warningFor)
			{
				warningFor = inputs;
				warning = TimeWarnings.For(inputs.Item1, inputs.Item2, inputs.Item3);
				string shown = warning?.Text ?? "";
				if (shown != tagShown)
				{
					if (tagShown.Length == 0)
					{
						tagIn = 0f;
					}
					tagShown = shown;
					tagText.text = shown;
					tagWidth = tagText.preferredWidth + 16f;
				}
			}
			if (warning == null)
			{
				tagGroup.alpha = 0f;
				return 0f;
			}
			tagBack.color = warning.Value.Urgent ? Red : Amber;
			tagIn = Mathf.Min(1f, tagIn + dt / 0.22f);
			tagGroup.alpha = Motion.EaseOutCubic(tagIn);
			return tagWidth;
		}

		public void Dispose()
		{
			HidePrompts(false);
			prompts.Clear();
		}
	}

	/// <summary>A cyan dot on the baseline and "Name · 140m" under it for each co-op player (the minimap's co-op color),
	/// clamped to the strip's ends when they're behind you. The distance uses the game's own formatter, so it follows the
	/// player's metric or imperial setting.</summary>
	private sealed class Friends : IDisposable
	{
		private sealed class Marker
		{
			internal Player Player;

			internal RectTransform Root;

			internal CanvasGroup Group;

			internal TextMeshProUGUI Label;

			/// <summary>The name (read once a second, a new string per read) and whole metres the label was last built from;
			/// it's only rebuilt when one changes.</summary>
			internal string? Name;

			internal int Metres = -1;
		}

		private static readonly Color Cyan = new Color(0.2f, 0.87f, 1f, 1f);

		private readonly RectTransform parent;

		private readonly Sprite dot;

		private readonly TMP_FontAsset font;

		private readonly Dictionary<int, Marker> markers = new Dictionary<int, Marker>();

		private readonly HashSet<int> live = new HashSet<int>();

		private readonly List<int> stale = new List<int>();

		private float nextRefresh;

		internal Friends(RectTransform parent, Sprite dot, TMP_FontAsset font)
		{
			this.parent = parent;
			this.dot = dot;
			this.font = font;
		}

		internal void Update(CompassSkin strip, Vector3 from)
		{
			if (Time.unscaledTime >= nextRefresh)
			{
				nextRefresh = Time.unscaledTime + 1f;
				Refresh();
			}
			foreach (Marker marker in markers.Values)
			{
				Player player = marker.Player;
				if (player == null)
				{
					marker.Root.gameObject.SetActive(false);
					continue;
				}
				marker.Root.gameObject.SetActive(true);
				Vector3 at = (player.IsInVehicle && player.CurrentVehicle != null ? player.CurrentVehicle.transform : player.transform).position;
				float x = strip.StripX(Bearing(from, at));
				// Dot on the baseline (33.5 from the top), name under it at 46, between the marker and the clock pill (whose
				// top is at 54).
				marker.Root.localPosition = new Vector3(x, Top - 33.5f, 0f);
				marker.Group.alpha = Mathf.Max(0.35f, Art.EdgeMask((x + Width / 2f) / Width));
				float distance = Vector3.Distance(from, at);
				int metres = Mathf.RoundToInt(distance);
				if (metres != marker.Metres)
				{
					marker.Metres = metres;
					marker.Label.text = (string.IsNullOrEmpty(marker.Name) ? "Friend" : marker.Name) + " · " + UnitsUtility.FormatShortDistance(distance, UnitsUtility.ERoundingType.Nearest, 0);
				}
			}
		}

		private void Refresh()
		{
			live.Clear();
			Player local = Player.Local;
			Il2CppSystem.Collections.Generic.List<Player> players = Player.PlayerList;
			for (int i = 0; players != null && i < players.Count; i++)
			{
				Player player = players[i];
				if (player == null || player == local || IsServerPlayer(player))
				{
					continue;
				}
				int id = player.GetInstanceID();
				live.Add(id);
				if (!markers.TryGetValue(id, out Marker? marker))
				{
					marker = Create(player);
					markers.Add(id, marker);
				}
				string name = player.PlayerName;
				if (name != marker.Name)
				{
					marker.Name = name;
					marker.Metres = -1;
				}
			}
			stale.Clear();
			foreach (int id in markers.Keys)
			{
				if (!live.Contains(id))
				{
					stale.Add(id);
				}
			}
			foreach (int id in stale)
			{
				if (markers[id].Root != null)
				{
					UnityEngine.Object.Destroy(markers[id].Root.gameObject);
				}
				markers.Remove(id);
			}
		}

		/// <summary>The dedicated server's own headless player. A client sets its Steam name on spawn (Player.OnStartClient);
		/// the server has no Steam login, so its player keeps the class default "Player", and showed up as a friend.</summary>
		internal static bool IsServerPlayer(Player player)
		{
			return player.PlayerName == "Player";
		}

		private Marker Create(Player player)
		{
			RectTransform root = Ui.Rect("Friend", parent);
			Image d = Ui.Image("Dot", root, dot, Cyan);
			d.rectTransform.sizeDelta = new Vector2(8f, 8f);
			TextMeshProUGUI label = Ui.Text("Name", root, font, 12f, new Color(0.85f, 0.97f, 1f, 1f), 2f);
			label.rectTransform.sizeDelta = new Vector2(200f, 16f);
			label.rectTransform.localPosition = new Vector3(0f, -12.5f, 0f);
			return new Marker { Player = player, Root = root, Group = Ui.Group(root.gameObject), Label = label };
		}

		public void Dispose()
		{
			// The markers live in a layer under the strip, which is destroyed with it.
			markers.Clear();
		}
	}
}
