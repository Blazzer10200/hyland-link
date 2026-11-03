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

	private readonly Color raised = Ui.Color(Theme.PhoneLine);

	private readonly Color text = Ui.Color(Theme.PhoneText);

	private readonly Color muted = Ui.Color(Theme.PhoneMuted);

	/// <summary>Graphics judged best left as the game has them, by the color they had then: Messages alone has ~1000, and
	/// judging each again every pass (a walk up its parents for text) cost a hitch.</summary>
	private readonly Dictionary<int, Color> kept = new Dictionary<int, Color>();

	private readonly HashSet<string> reported = new HashSet<string>();

	private GameObject? current;

	private float openedAt;

	private float nextPass;

	private float nextCorners;

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
			nextCorners = 0f;
		}
		if (now < nextPass)
		{
			return;
		}
		// Often enough that panels the game rebuilds (Journal's details, on every hover) don't flash light for long.
		nextPass = now + (now - openedAt < 2f ? 0.1f : 0.25f);
		// Labels only move when the game lays a screen out again: once a second after the first two.
		bool corners = now >= nextCorners;
		if (corners)
		{
			nextCorners = now + (now - openedAt < 2f ? 0.1f : 1f);
		}
		Restyle(app, corners);
		if (now - openedAt > 3f && reported.Add(app.name))
		{
			ReportLeftovers(app);
		}
	}

	/// <summary>
	/// Once per app per run, a few seconds after it opens: the biggest light panels still on screen that this left alone,
	/// by path, sprite and color. Something light that should be dark shows up here instead of in a screenshot.
	/// </summary>
	private void ReportLeftovers(GameObject app)
	{
		List<(float Area, string Line)> light = new List<(float, string)>();
		foreach (Graphic graphic in app.GetComponentsInChildren<Graphic>(false))
		{
			if (!graphic.enabled || records.ContainsKey(graphic.GetInstanceID()))
			{
				continue;
			}
			Color color = graphic.color;
			Rect rect = graphic.rectTransform.rect;
			float area = Mathf.Abs(rect.width * rect.height);
			if (color.a < 0.5f || Lightness(color) < 0.6f || area < 1500f || graphic.TryCast<RawImage>() != null)
			{
				continue;
			}
			Image? image = graphic.TryCast<Image>();
			string kind = image != null ? "image " + (image.sprite != null ? image.sprite.name : "none") : "text";
			light.Add((area, $"{PathOf(graphic.transform, app.transform)} [{kind}] #{ColorUtility.ToHtmlStringRGBA(color)} {rect.width:0}x{rect.height:0}"));
		}
		light.Sort((a, b) => b.Area.CompareTo(a.Area));
		log($"Phone: {app.name} left light: {(light.Count == 0 ? "nothing" : light.Count.ToString())}.");
		for (int i = 0; i < light.Count && i < 10; i++)
		{
			log("  " + light[i].Line);
		}
	}

	private static string PathOf(Transform transform, Transform root)
	{
		string path = transform.name;
		for (Transform? at = transform.parent; at != null && at != root; at = at.parent)
		{
			path = at.name + "/" + path;
		}
		return path;
	}

	private void Restyle(GameObject app, bool corners)
	{
		float? appBase = BaseOf(app);
		// Many of the game's buttons are white images that their Button tints (Confirm: white × #5DBAFF; my bubbles:
		// white × #4BAFE1), so the color seen is image × tint.
		Dictionary<int, Selectable> tinted = new Dictionary<int, Selectable>();
		foreach (Selectable selectable in app.GetComponentsInChildren<Selectable>(true))
		{
			if (selectable.transition == Selectable.Transition.ColorTint && selectable.targetGraphic != null)
			{
				tinted[selectable.targetGraphic.GetInstanceID()] = selectable;
			}
		}
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
			else if (kept.TryGetValue(id, out Color was) && Same(color, was))
			{
				continue;
			}
			Color? tint = null;
			if (tinted.TryGetValue(id, out Selectable? owner))
			{
				ColorBlock block = owner.colors;
				tint = (owner.interactable ? block.normalColor : block.disabledColor) * block.colorMultiplier;
			}
			Color? target = Target(graphic, color, appBase, tint, tinted);
			if (target == null)
			{
				records.Remove(id);
				kept[id] = color;
				continue;
			}
			kept.Remove(id);
			record ??= new Record { Graphic = graphic, Original = color };
			record.Applied = target.Value;
			records[id] = record;
			graphic.color = target.Value;
			themed++;
		}
		int moved = 0;
		RectTransform? root = app.GetComponent<RectTransform>();
		if (root != null && corners)
		{
			foreach (Graphic label in app.GetComponentsInChildren<Graphic>(false))
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
	private bool ClearCorner(RectTransform root, Graphic label)
	{
		TMP_Text? pro = label.TryCast<TMP_Text>();
		Text? legacy = pro == null ? label.TryCast<Text>() : null;
		if (pro == null && legacy == null)
		{
			return false;
		}
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
		if (string.IsNullOrEmpty(pro != null ? pro.text : legacy!.text))
		{
			return false;
		}
		Bounds glyphs = pro != null ? pro.textBounds : Glyphs(legacy!);
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
		// Last, being the dear one: labels in scroll lists only pass the corner.
		if (Mathf.Abs(shift) < 0.5f || label.GetComponentInParent<ScrollRect>() != null)
		{
			return false;
		}
		Vector3 local = rect.parent != null ? rect.parent.InverseTransformVector(root.TransformVector(new Vector3(shift, 0f, 0f))) : new Vector3(shift, 0f, 0f);
		Vector2 original = rect.anchoredPosition;
		rect.anchoredPosition = original + new Vector2(local.x, local.y);
		nudges[id] = new Nudge { Rect = rect, Original = original, Applied = rect.anchoredPosition };
		return true;
	}

	/// <summary>Where a legacy Text's letters are, in its own rect's space: Unity keeps no bounds for them, so this goes by
	/// the line's width and the alignment (a line is about 1.2 font sizes tall).</summary>
	private static Bounds Glyphs(Text text)
	{
		Rect rect = text.rectTransform.rect;
		float width = Mathf.Min(text.preferredWidth, rect.width);
		float height = Mathf.Min(text.fontSize * 1.2f, rect.height);
		int alignment = (int)text.alignment;
		int column = alignment % 3, row = alignment / 3;
		float x = column == 0 ? rect.xMin + width / 2f : column == 1 ? rect.center.x : rect.xMax - width / 2f;
		float y = row == 0 ? rect.yMax - height / 2f : row == 1 ? rect.center.y : rect.yMin + height / 2f;
		return new Bounds(new Vector3(x, y, 0f), new Vector3(width, height, 0f));
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
	private Color? Target(Graphic graphic, Color color, float? appBase, Color? tint, Dictionary<int, Selectable> tinted)
	{
		Image? image = graphic.TryCast<Image>();
		if (image != null && color.a > 0.01f)
		{
			// The board has no colored app bars (Journal's orange, Products' maroon): they're the app's background.
			if (image.sprite == null && Header(graphic.transform))
			{
				return new Color(background.r, background.g, background.b, color.a);
			}
			// Black drop shades under bars and at list edges pile up into heavy bands on a dark phone.
			if (image.sprite != null && image.sprite.name.Contains("gradient", StringComparison.OrdinalIgnoreCase)
				&& (graphic.name.Contains("Shade") || graphic.name.Contains("Gradient")) && Lightness(color) < 0.1f)
			{
				return new Color(color.r, color.g, color.b, 0f);
			}
		}
		// A button clear at rest (a list row that only shows on hover) is judged by its own color: that's its hover look.
		Color seen = image != null && tint != null && tint.Value.a > 0.01f ? color * tint.Value : color;
		if (color.a <= 0.01f || graphic.TryCast<RawImage>() != null || !Gray(seen))
		{
			return null;
		}
		float light = Lightness(seen);
		if (image != null)
		{
			Sprite? sprite = image.sprite;
			// A mask's own shape (Messages' #DCDCDC avatar rings, around every face) is UI, not the picture inside it.
			if (sprite != null && sprite.border == Vector4.zero && !Plain(sprite.name) && image.GetComponent<Mask>() == null)
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
			float lift = Mathf.Clamp01(Mathf.Abs(light - from) / 0.15f);
			// A shaped panel (rounded, sliced) is a card even in the background's own gray (Deliveries' rows, Products'
			// price box): it keeps a step of lift so it doesn't vanish into the background.
			if (sprite != null)
			{
				lift = Mathf.Max(lift, 0.4f);
			}
			Color dark = Color.Lerp(background, raised, lift);
			if (tint != null && tint.Value.a > 0.01f)
			{
				// What the image needs to be so that the tint lands it on the slate (as near as a dark tint allows).
				Color t = tint.Value;
				dark = new Color(Divide(dark.r, t.r), Divide(dark.g, t.g), Divide(dark.b, t.b));
			}
			return new Color(dark.r, dark.g, dark.b, color.a);
		}
		// Text: on a colored button (white on blue, black on yellow) it's already right.
		if (OnColor(graphic.transform, tinted))
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

	/// <summary>Whether the nearest solid image behind this text (it or one of 3 parents) shows colored, not gray, its
	/// button's tint included. The original colors count, not the slate this put on them.</summary>
	private bool OnColor(Transform transform, Dictionary<int, Selectable> tinted)
	{
		Transform? at = transform;
		for (int i = 0; i < 4 && at != null; i++, at = at.parent)
		{
			Image? image = at.GetComponent<Image>();
			if (image == null || !image.enabled)
			{
				continue;
			}
			int id = image.GetInstanceID();
			Color color = records.TryGetValue(id, out Record? record) && Same(image.color, record.Applied) ? record.Original : image.color;
			if (tinted.TryGetValue(id, out Selectable? owner))
			{
				ColorBlock block = owner.colors;
				Color tint = (owner.interactable ? block.normalColor : block.disabledColor) * block.colorMultiplier;
				if (tint.a > 0.01f)
				{
					color *= tint;
				}
			}
			if (color.a > 0.5f)
			{
				// Text on a picture (Map's place names on the map art) keeps its color like the picture does.
				Sprite? sprite = image.sprite;
				return !Gray(color) || (sprite != null && sprite.border == Vector4.zero && !Plain(sprite.name) && image.GetComponent<Mask>() == null);
			}
		}
		return false;
	}

	/// <summary>An app's top bar: "Topbar" (Journal, Map), its "Background" (a Messages chat) or "Header/Background"
	/// (Products).</summary>
	private static bool Header(Transform transform)
	{
		if (transform.name == "Topbar")
		{
			return true;
		}
		Transform? parent = transform.parent;
		return transform.name == "Background" && parent != null && (parent.name == "Topbar" || parent.name == "Header");
	}

	/// <summary>UI shapes that may be drawn unsliced: Unity's built-ins, the game's rectangles, Contacts' tiled backdrop and
	/// Products' plain white slots. Circles stay: they're Contacts' relationship rings and avatar backs.</summary>
	private static bool Plain(string name)
	{
		return name.Contains("UISprite", StringComparison.OrdinalIgnoreCase)
			|| name == "Tile"
			|| name.Contains("Triangle", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("SlotBackground", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Background", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Rect", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Round", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Square", StringComparison.OrdinalIgnoreCase);
	}

	private static float Divide(float value, float by)
	{
		return by > 0.01f ? Mathf.Clamp01(value / by) : 1f;
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
