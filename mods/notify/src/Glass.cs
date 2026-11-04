using HylandHud;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>The board's colors and the builders every surface shares. Everything is non-interactive (no raycasts).</summary>
internal static class Glass
{
	internal static readonly Color Text = Hex(0xE8EAED);

	internal static readonly Color Soft = Hex(0xC9D1DB);

	internal static readonly Color Muted = Hex(0x9AA5B1);

	internal static readonly Color Line = Hex(0x232A33);

	internal static readonly Color Green = Hex(0x7DDC6F);

	internal static readonly Color Orange = Hex(0xFFB060);

	internal static readonly Color Red = Hex(0xFF8A7A);

	internal static readonly Color Blue = Hex(0x5CC4FF);

	internal static readonly Color Yellow = Hex(0xFFE652);

	internal static readonly Color Ink = Hex(0x14171B);

	internal static Color Hex(int rgb, float a = 1f)
	{
		return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
	}

	internal static Color Tint(Color c, float a)
	{
		return new Color(c.r, c.g, c.b, a);
	}

	/// <summary>A rect anchored and pivoted at one point of its parent (0,1 = top left), placed by that point.</summary>
	internal static RectTransform Pin(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
	{
		rect.anchorMin = anchor;
		rect.anchorMax = anchor;
		rect.pivot = anchor;
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
		return rect;
	}

	internal static RectTransform Fill(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = Vector2.zero;
		return rect;
	}

	/// <summary>The HUD's glass: dark fill with a faint white edge, 9-sliced so it stretches to any size.</summary>
	internal static Image Panel(string name, Transform parent, Sprites sprites, double radius = 10)
	{
		return Ui.Image(name, parent, sprites.Glass(radius), Color.white, sliced: true);
	}

	/// <summary>A white rounded rect for tinting: chips, tiles, bars. At a size of 2×radius it is a circle.</summary>
	internal static Image Solid(string name, Transform parent, Sprites sprites, double radius, Color color)
	{
		Sprite sprite = sprites.Get("solid" + radius, () => Art.Glass(radius, Theme.White, Theme.White));
		return Ui.Image(name, parent, sprite, color, sliced: true);
	}

	internal static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
	{
		TextMeshProUGUI text = Ui.Text(name, parent, font, size, color);
		text.alignment = align;
		text.overflowMode = TextOverflowModes.Ellipsis;
		text.richText = true;
		return text;
	}

	/// <summary>A round picture (a mugshot), masked by a circle, with a tinted circle behind it for when there's none.</summary>
	internal static Image Avatar(string name, Transform parent, Sprites sprites, float size, out Image picture)
	{
		Image back = Solid(name, parent, sprites, size / 2f, Hex(0x3A4250));
		back.gameObject.AddComponent<Mask>().showMaskGraphic = true;
		picture = Ui.Image("Picture", back.transform, null, Color.white);
		picture.preserveAspect = true;
		Fill(picture.rectTransform);
		return back;
	}

	/// <summary>Strips TMP rich-text tags, for text copied from the game that we restyle ourselves.</summary>
	internal static string Plain(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
		bool inTag = false;
		foreach (char c in text)
		{
			if (c == '<')
			{
				inTag = true;
			}
			else if (c == '>' && inTag)
			{
				inTag = false;
			}
			else if (!inTag)
			{
				sb.Append(c);
			}
		}
		return sb.ToString().Trim();
	}
}

/// <summary>A value easing toward a target, ~95% of the way in <see cref="Seconds"/>.</summary>
internal sealed class Ease
{
	internal float Value;

	internal float Target;

	internal float Seconds;

	internal Ease(float value, float seconds)
	{
		Value = value;
		Target = value;
		Seconds = seconds;
	}

	internal float Step(float dt)
	{
		Value = HylandHud.Motion.Approach(Value, Target, Seconds, dt);
		if (Mathf.Abs(Value - Target) < 0.001f)
		{
			Value = Target;
		}
		return Value;
	}

	internal bool Settled => Value == Target;
}
