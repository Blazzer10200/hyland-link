using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HylandMinimap;

/// <summary>
/// Arrows on the map's edge pointing at things that matter but are off the map (active deals, dealers), instead of
/// those markers just vanishing. Markers report what they couldn't place between Begin and End each frame.
/// </summary>
internal sealed class EdgePins
{
	private readonly RectTransform layer;

	private readonly Sprite sprite;

	private readonly List<RectTransform> pool = new List<RectTransform>();

	/// <summary>Each pooled pin's Image, so a frame doesn't look it up again per pin.</summary>
	private readonly List<Image> images = new List<Image>();

	private int used;

	private float radius;

	private bool circle;

	private bool enabled;

	internal EdgePins(RectTransform layer, Sprite sprite)
	{
		this.layer = layer;
		this.sprite = sprite;
	}

	internal void Begin(bool enabled, float radius, bool circle)
	{
		this.enabled = enabled;
		this.radius = radius;
		this.circle = circle;
		used = 0;
	}

	/// <param name="x">The marker's position relative to the map center, already rotated, in HUD units.</param>
	internal void Add(double x, double y, Color color)
	{
		if (!enabled || (x == 0.0 && y == 0.0))
		{
			return;
		}
		RectTransform pin = ((used < pool.Count) ? pool[used] : Create());
		Image image = images[used];
		used++;
		double angle = Math.Atan2(y, x);
		(double, double) point = MapMath.EdgePoint(angle, radius, circle);
		pin.anchoredPosition = new Vector2((float)point.Item1, (float)point.Item2);
		pin.localRotation = Quaternion.Euler(0f, 0f, (float)(angle * 180.0 / Math.PI));
		image.color = color;
		pin.gameObject.SetActive(value: true);
	}

	internal void End()
	{
		for (int i = used; i < pool.Count; i++)
		{
			pool[i].gameObject.SetActive(value: false);
		}
	}

	private RectTransform Create()
	{
		RectTransform rect = new GameObject("Edge pin").AddComponent<RectTransform>();
		rect.SetParent(layer, worldPositionStays: false);
		rect.anchorMin = (rect.anchorMax = (rect.pivot = new Vector2(0.5f, 0.5f)));
		rect.sizeDelta = new Vector2(14f, 14f);
		Image image = rect.gameObject.AddComponent<Image>();
		image.sprite = sprite;
		image.raycastTarget = false;
		Outline outline = rect.gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0.02f, 0.025f, 0.03f, 0.95f);
		outline.effectDistance = new Vector2(1f, -1f);
		pool.Add(rect);
		images.Add(image);
		return rect;
	}
}
