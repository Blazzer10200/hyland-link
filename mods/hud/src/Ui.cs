using System;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>Small builders for the HUD's own objects. Everything here is non-interactive (no raycasts), so it never steals
/// a click or a drag from the game's slots.</summary>
internal static class Ui
{
	internal static RectTransform Rect(string name, Transform parent)
	{
		RectTransform rect = new GameObject(name).AddComponent<RectTransform>();
		rect.SetParent(parent, worldPositionStays: false);
		Vector2 center = new Vector2(0.5f, 0.5f);
		rect.pivot = center;
		rect.anchorMin = center;
		rect.anchorMax = center;
		rect.sizeDelta = Vector2.zero;
		return rect;
	}

	internal static Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
	{
		Image image = Rect(name, parent).gameObject.AddComponent<Image>();
		image.sprite = sprite;
		image.color = color;
		image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
		image.raycastTarget = false;
		return image;
	}

	internal static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color, float spacing = 0f)
	{
		TextMeshProUGUI text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null)
		{
			text.font = font;
		}
		text.fontSize = size;
		text.color = color;
		text.characterSpacing = spacing;
		text.alignment = TextAlignmentOptions.Center;
		text.enableWordWrapping = false;
		text.overflowMode = TextOverflowModes.Overflow;
		text.raycastTarget = false;
		return text;
	}

	internal static CanvasGroup Group(GameObject target)
	{
		CanvasGroup group = target.AddComponent<CanvasGroup>();
		group.interactable = false;
		group.blocksRaycasts = false;
		return group;
	}

	internal static Color Color(Rgba c, float alpha = -1f)
	{
		return new Color(c.R, c.G, c.B, alpha < 0f ? c.A : alpha);
	}

	/// <summary>The bounds of <paramref name="rect"/> in <paramref name="space"/>'s local coordinates.</summary>
	internal static Rect Bounds(RectTransform rect, Transform space)
	{
		Rect r = rect.rect;
		Vector3 min = space.InverseTransformPoint(rect.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)));
		Vector3 max = space.InverseTransformPoint(rect.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
		return UnityEngine.Rect.MinMaxRect(Math.Min(min.x, max.x), Math.Min(min.y, max.y), Math.Max(min.x, max.x), Math.Max(min.y, max.y));
	}

	/// <summary>Places a center-pivoted rect by its center and size in its parent's local space.</summary>
	internal static void Place(RectTransform rect, Vector2 center, Vector2 size)
	{
		rect.localPosition = new Vector3(center.x, center.y, 0f);
		rect.sizeDelta = size;
	}
}

/// <summary>Frame-rate independent easing, so every animation reaches ~95% of its target in the concept's timings.</summary>
internal static class Motion
{
	/// <summary>Moves <paramref name="current"/> toward <paramref name="target"/>, ~95% of the way in
	/// <paramref name="seconds"/>.</summary>
	internal static float Approach(float current, float target, float seconds, float dt)
	{
		if (seconds <= 0f)
		{
			return target;
		}
		return target + (current - target) * Mathf.Exp(-3f * dt / seconds);
	}

	internal static float ApproachAngle(float current, float target, float seconds, float dt)
	{
		return current + Mathf.DeltaAngle(current, target) * (1f - (seconds <= 0f ? 0f : Mathf.Exp(-3f * dt / seconds)));
	}

	internal static float EaseOutCubic(float t)
	{
		t = Mathf.Clamp01(t);
		return 1f - (1f - t) * (1f - t) * (1f - t);
	}
}
