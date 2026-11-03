using System;
using System.Collections.Generic;
using Il2CppScheduleOne.UI.Phone;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// The game's own apps in the glass phone's colors, restyled in place like the home screen: whichever app is open gets
/// its gray UI themed dark. Light gray panels become the board's slate, darkest for the lightest so the app's own layering
/// survives (white background → #0F1317, its dividers and headers a step up); dark gray text becomes the board's text,
/// mid gray its muted gray. Anything colored is the game's meaning (blue bubbles, green prices, relationship tints,
/// badges, accent buttons) and stays; so does art (icons, avatars, item pictures, the map), recognized as an image with
/// a sprite that isn't a plain or 9-sliced UI shape. Rows the game adds later and colors it sets later are caught on the
/// next pass (fast while an app has just opened, then once a second). Every change is recorded and put back in
/// <see cref="Dispose"/>, unless the game has changed that color since.
/// </summary>
internal sealed class AppsSkin : IDisposable
{
	private sealed class Record
	{
		internal Graphic Graphic = null!;

		internal Color Original;

		internal Color Applied;
	}

	private sealed class Nudge
	{
		internal RectTransform Rect = null!;

		internal Vector2 Original;

		internal Vector2 Applied;
	}

	/// <summary>The frame's screen corner (<see cref="PhoneSkin"/>'s 30 board units) plus a little air: the apps share the
	/// home canvas's scale.</summary>
	private const float Corner = 30f * 655f / 316f;

	private const float CornerAir = 6f;

	private readonly Dictionary<int, Record> records = new Dictionary<int, Record>();

	private readonly Dictionary<int, Nudge> nudges = new Dictionary<int, Nudge>();

	private readonly HashSet<string> logged = new HashSet<string>();

	private readonly Action<string> log;

	private readonly Color background = Ui.Color(Theme.PhoneApp);

	private readonly Color raised = Ui.Color(Theme.PhoneStroke);

	private readonly Color text = Ui.Color(Theme.PhoneText);

	private readonly Color muted = Ui.Color(Theme.PhoneMuted);

	private GameObject? current;

	private float openedAt;

	private float nextPass;

	internal AppsSkin(Action<string> log)
	{
		this.log = log;
	}

	internal void Update()
	{
		GameObject? app = Phone.ActiveApp;
		if (app == null || app.name == HylandApp.RootName)
		{
			current = null;
			return;
		}
		float now = Time.unscaledTime;
		if (app != current)
		{
			current = app;
			openedAt = now;
			nextPass = 0f;
		}
		if (now < nextPass)
		{
			return;
		}
		nextPass = now + (now - openedAt < 2f ? 0.2f : 1f);
		Restyle(app);
	}

	private void Restyle(GameObject app)
	{
		float? appBase = BaseOf(app);
		int themed = 0;
		foreach (Graphic graphic in app.GetComponentsInChildren<Graphic>(true))
		{
			int id = graphic.GetInstanceID();
			Color color = graphic.color;
			if (records.TryGetValue(id, out Record? record))
			{
				if (Same(color, record.Applied))
				{
					continue;
				}
				// The game has set a new color since (a selected tab, a hover, a new row's state): that's the original now.
				record.Original = color;
			}
			Color? target = Target(graphic, color, appBase);
			if (target == null)
			{
				records.Remove(id);
				continue;
			}
			record ??= new Record { Graphic = graphic, Original = color };
			record.Applied = target.Value;
			records[id] = record;
			graphic.color = target.Value;
			themed++;
		}
		int moved = 0;
		RectTransform? root = app.GetComponent<RectTransform>();
		if (root != null)
		{
			foreach (TMP_Text label in app.GetComponentsInChildren<TMP_Text>(false))
			{
				if (ClearCorner(root, label))
				{
					moved++;
				}
			}
		}
		if ((themed > 0 || moved > 0) && logged.Add(app.name))
		{
			log($"Phone: {app.name} themed ({themed} graphics, {moved} labels moved off the corners).");
		}
	}

	/// <summary>
	/// Moves a label whose letters reach into one of the screen's rounded corners (the game's titles sit 5 units from the
	/// edge: "Map" lost its M) sideways until they clear it. Labels in scroll lists are left alone, since they only pass
	/// the corner. Returns whether it moved the label.
	/// </summary>
	private bool ClearCorner(RectTransform root, TMP_Text label)
	{
		RectTransform rect = label.rectTransform;
		int id = rect.GetInstanceID();
		if (nudges.TryGetValue(id, out Nudge? nudge))
		{
			if ((rect.anchoredPosition - nudge.Applied).sqrMagnitude < 0.01f)
			{
				return false;
			}
			// The game placed it again: start over from where it put it.
			nudges.Remove(id);
		}
		if (string.IsNullOrEmpty(label.text) || label.GetComponentInParent<ScrollRect>() != null)
		{
			return false;
		}
		Bounds glyphs = label.textBounds;
		if (glyphs.size.x <= 0f)
		{
			return false;
		}
		Vector3 a = root.InverseTransformPoint(rect.TransformPoint(glyphs.min));
		Vector3 b = root.InverseTransformPoint(rect.TransformPoint(glyphs.max));
		float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
		Rect area = root.rect;
		// How deep the letters reach into the top or bottom band, whichever edge is nearer.
		float depth = Mathf.Min(area.yMax - y1, y0 - area.yMin);
		if (depth >= Corner)
		{
			return false;
		}
		float inset = Inset(depth) + CornerAir;
		float shift = 0f;
		if (x0 < area.xMin + inset)
		{
			shift = area.xMin + inset - x0;
		}
		else if (x1 > area.xMax - inset)
		{
			shift = area.xMax - inset - x1;
		}
		if (Mathf.Abs(shift) < 0.5f)
		{
			return false;
		}
		Vector3 local = rect.parent != null ? rect.parent.InverseTransformVector(root.TransformVector(new Vector3(shift, 0f, 0f))) : new Vector3(shift, 0f, 0f);
		Vector2 original = rect.anchoredPosition;
		rect.anchoredPosition = original + new Vector2(local.x, local.y);
		nudges[id] = new Nudge { Rect = rect, Original = original, Applied = rect.anchoredPosition };
		return true;
	}

	/// <summary>How far in from the side the screen's rounded corner reaches at this depth below (or above) the edge.</summary>
	private static float Inset(float depth)
	{
		float d = Mathf.Clamp(depth, 0f, Corner);
		return Corner - Mathf.Sqrt(Corner * Corner - (Corner - d) * (Corner - d));
	}

	/// <summary>
	/// The lightness of the app's own background (Container/Background), as the game set it: the apps come light (Journal,
	/// Messages #F0F0F0) and dark (Products, Deliveries #323232, Dealers #282828), and both map their background to the
	/// board's. Its original color counts, not the one this put on it. Null when the app has none (Contacts, Map).
	/// </summary>
	private float? BaseOf(GameObject app)
	{
		Transform? background = app.transform.Find("Container/Background");
		Image? image = background != null ? background.GetComponent<Image>() : null;
		if (image == null || image.sprite != null)
		{
			return null;
		}
		Color color = records.TryGetValue(image.GetInstanceID(), out Record? record) && Same(image.color, record.Applied) ? record.Original : image.color;
		return color.a > 0.9f && Gray(color) ? Lightness(color) : null;
	}

	/// <summary>The board's color for one of the game's graphics, or null to leave it as the game has it.</summary>
	private Color? Target(Graphic graphic, Color color, float? appBase)
	{
		if (color.a <= 0.01f || graphic.TryCast<RawImage>() != null || !Gray(color))
		{
			return null;
		}
		float light = Lightness(color);
		Image? image = graphic.TryCast<Image>();
		if (image != null)
		{
			Sprite? sprite = image.sprite;
			if (sprite != null && sprite.border == Vector4.zero && !Plain(sprite.name))
			{
				return null;
			}
			// Black see-through shades (dimming behind a popup, shadows) already suit a dark phone.
			if (light < 0.08f && color.a < 0.9f)
			{
				return null;
			}
			// The background itself is the board's darkest; the further a panel is from it, lighter or darker, the more raised.
			float from = appBase ?? (light >= 0.5f ? 0.94f : 0.125f);
			Color dark = Color.Lerp(background, raised, Mathf.Clamp01(Mathf.Abs(light - from) / 0.3f));
			return new Color(dark.r, dark.g, dark.b, color.a);
		}
		// Text: on a colored button (white on blue, black on yellow) it's already right.
		if (OnColor(graphic.transform))
		{
			return null;
		}
		if (light < 0.35f)
		{
			return new Color(text.r, text.g, text.b, color.a);
		}
		if (light < 0.75f)
		{
			return new Color(muted.r, muted.g, muted.b, color.a);
		}
		return null;
	}

	/// <summary>Whether the nearest solid image behind this text (it or one of 3 parents) is colored, not gray.</summary>
	private static bool OnColor(Transform transform)
	{
		Transform? at = transform;
		for (int i = 0; i < 4 && at != null; i++, at = at.parent)
		{
			Image? image = at.GetComponent<Image>();
			if (image != null && image.color.a > 0.5f && image.enabled)
			{
				return !Gray(image.color);
			}
		}
		return false;
	}

	/// <summary>UI shapes that may be drawn unsliced: Unity's built-ins and the game's rectangles. Circles stay: they're
	/// Contacts' relationship rings and avatar backs.</summary>
	private static bool Plain(string name)
	{
		return name.Contains("UISprite", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Background", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Rect", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Round", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Square", StringComparison.OrdinalIgnoreCase);
	}

	private static bool Gray(Color c)
	{
		return Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b)) <= 0.1f;
	}

	private static float Lightness(Color c)
	{
		return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
	}

	private static bool Same(Color a, Color b)
	{
		return Mathf.Abs(a.r - b.r) < 0.003f && Mathf.Abs(a.g - b.g) < 0.003f && Mathf.Abs(a.b - b.b) < 0.003f && Mathf.Abs(a.a - b.a) < 0.003f;
	}

	public void Dispose()
	{
		Exception? failure = null;
		foreach (Record record in records.Values)
		{
			Ui.Step(ref failure, () =>
			{
				if (record.Graphic != null && Same(record.Graphic.color, record.Applied))
				{
					record.Graphic.color = record.Original;
				}
			});
		}
		records.Clear();
		foreach (Nudge nudge in nudges.Values)
		{
			Ui.Step(ref failure, () =>
			{
				if (nudge.Rect != null && (nudge.Rect.anchoredPosition - nudge.Applied).sqrMagnitude < 0.01f)
				{
					nudge.Rect.anchoredPosition = nudge.Original;
				}
			});
		}
		nudges.Clear();
		Ui.Rethrow(failure);
	}
}
