using System.Text;
using UnityEngine;

namespace HylandHud;

/// <summary>
/// Layout dumps for debugging against the real prefab: an object (and its parents and children) with its rect, anchors and
/// components. Not called in normal runs; drop a <c>log(...)</c> with these into a skin's constructor when a screenshot
/// shows something out of place. That's how the compass's RectMask2D and the cash slot's container were found.
/// </summary>
internal static class Diagnostics
{
	/// <summary>The object and its parents, nearest first.</summary>
	internal static string Chain(Transform? t, int levels)
	{
		StringBuilder sb = new StringBuilder();
		for (int i = 0; t != null && i < levels; i++, t = t.parent)
		{
			sb.Append("\n    ").Append(i == 0 ? "" : "< ").Append(Node(t));
		}
		return sb.ToString();
	}

	internal static string Children(Transform t)
	{
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < t.childCount; i++)
		{
			sb.Append("\n      - ").Append(Node(t.GetChild(i)));
		}
		return sb.ToString();
	}

	internal static string Node(Transform t)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append('\'').Append(t.name).Append('\'');
		if (!t.gameObject.activeSelf)
		{
			sb.Append(" (off)");
		}
		RectTransform? r = t.TryCast<RectTransform>();
		if (r != null)
		{
			sb.Append($" pos {r.anchoredPosition} size {r.rect.size} anchors {r.anchorMin}-{r.anchorMax} pivot {r.pivot} scale {r.localScale} world {r.position}");
		}
		sb.Append(" [");
		var components = t.GetComponents<Component>();
		for (int i = 0; i < components.Length; i++)
		{
			sb.Append(i == 0 ? "" : ", ").Append(components[i].GetIl2CppType().Name);
		}
		sb.Append(']');
		return sb.ToString();
	}
}
