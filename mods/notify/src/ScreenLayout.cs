using System;
using MelonLoader;
using UnityEngine;

namespace HylandNotify;

/// <summary>
/// Where each group goes, in HUD units (1920 wide). Built around Hyland HUD (compass top center, hotbar and money bottom
/// center) and Hyland Minimap, whose corner, size, margin and offsets are read from its own settings, so nothing of ours
/// ever lands on the map whichever corner a player picks. See the "Screen map" board.
/// </summary>
internal sealed class ScreenLayout
{
	internal const float Edge = 24f;

	internal const float ColumnWidth = 320f;

	internal const float BadgeSize = 40f;

	/// <summary>Under Hyland HUD's compass (top 18, 36 tall) and its clock tag.</summary>
	internal const float WantedTop = 104f;

	internal const float Gap = 8f;

	internal float Width = 1920f;

	internal float Height = 1080f;

	internal float ColumnX = Edge;

	internal float BadgeTop = Edge;

	/// <summary>The left column (call, toasts, tracker) stops here; the tracker folds the rest into "+N more".</summary>
	internal float ColumnBottom = 648f;

	/// <summary>Hints and "Game saved": right inset and top.</summary>
	internal float CornerRight = Edge;

	internal float CornerTop = Edge;

	/// <summary>Special customers card: bottom-left, beside a bottom-left minimap.</summary>
	internal float SpecialX = Edge;

	internal const float SpecialBottom = 150f;

	private float nextRead;

	private string corner = "";

	private bool mapOn;

	private float size;

	private float margin;

	private float offsetX;

	private float offsetY;

	internal void Update(Vector2 canvasSize)
	{
		Width = canvasSize.x > 1f ? canvasSize.x : 1920f;
		Height = canvasSize.y > 1f ? canvasSize.y : 1080f;
		if (Time.unscaledTime >= nextRead)
		{
			nextRead = Time.unscaledTime + 1f;
			ReadMinimap();
		}
		ColumnX = Edge;
		BadgeTop = Edge;
		ColumnBottom = Mathf.Round(Height * 0.6f);
		CornerRight = Edge;
		CornerTop = Edge;
		SpecialX = Edge;
		if (!mapOn)
		{
			return;
		}
		// The map's rect from its own placement rules: offsets move it right (+x) and up (+y) from its corner.
		bool top = corner.StartsWith("Top", StringComparison.Ordinal);
		bool left = corner.EndsWith("Left", StringComparison.Ordinal);
		float mapLeft = left ? margin + offsetX : Width - margin - size + offsetX;
		float mapTop = top ? margin - offsetY : Height - margin - size - offsetY;
		float mapRight = mapLeft + size;
		float mapBottom = mapTop + size;
		if (top && left)
		{
			// The whole column steps beside the map.
			ColumnX = mapRight + Edge;
		}
		if (!top && left)
		{
			ColumnBottom = Mathf.Min(ColumnBottom, mapTop - Edge);
			SpecialX = mapRight + Edge;
		}
		if (top && !left)
		{
			CornerRight = Mathf.Max(Edge, Width - mapRight);
			CornerTop = mapBottom + 16f;
		}
	}

	private void ReadMinimap()
	{
		MelonPreferences_Category map = MelonPreferences.GetCategory("HylandMinimap");
		mapOn = map != null && Melon<bool>(map, "Enabled", true);
		if (!mapOn)
		{
			return;
		}
		corner = map.GetEntry("Corner")?.BoxedValue?.ToString() ?? "TopRight";
		size = Melon<int>(map, "Size", 240);
		margin = Melon<int>(map, "Margin", 24);
		offsetX = Melon<int>(map, "OffsetX", 0);
		offsetY = Melon<int>(map, "OffsetY", 0);
	}

	private static T Melon<T>(MelonPreferences_Category category, string key, T fallback)
	{
		object? value = category.GetEntry(key)?.BoxedValue;
		return value is T typed ? typed : fallback;
	}
}
