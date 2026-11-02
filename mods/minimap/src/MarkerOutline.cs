using UnityEngine;
using UnityEngine.UI;

namespace HylandMinimap;

/// <summary>A thin dark edge around a marker so it stays readable over light roads and buildings.</summary>
internal static class MarkerOutline
{
	internal static readonly Color Color = new Color(0.04f, 0.048f, 0.06f, 0.85f);

	internal static void AddTo(GameObject target, float distance = 1.2f)
	{
		Outline outline = target.AddComponent<Outline>();
		outline.effectColor = Color;
		outline.effectDistance = new Vector2(distance, 0f - distance);
		outline.useGraphicAlpha = true;
	}
}
