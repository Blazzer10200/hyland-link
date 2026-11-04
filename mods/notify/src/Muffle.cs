using System;
using UnityEngine;

namespace HylandNotify;

/// <summary>
/// Hides one of the game's UI pieces without touching anything the game reads back. A piece on a shared canvas gets its
/// own nested Canvas, switched off (a disabled nested Canvas draws nothing); a piece that already is a canvas has that
/// Canvas held off every frame, since the game switches it on when it plays. The game's logic, timers and sounds keep
/// running, so turning the mod off just lets the game draw again.
/// </summary>
internal sealed class Muffle
{
	private readonly Canvas canvas;

	private readonly bool added;

	private readonly Func<bool>? restoreEnabled;

	internal Muffle(GameObject target, Func<bool>? restoreEnabled = null)
	{
		this.restoreEnabled = restoreEnabled;
		canvas = target.GetComponent<Canvas>();
		if (canvas == null)
		{
			canvas = target.AddComponent<Canvas>();
			added = true;
		}
		canvas.enabled = false;
	}

	internal bool Covers(GameObject target)
	{
		return canvas != null && canvas.gameObject == target;
	}

	/// <summary>Call every frame (after the game's Update): the game may have switched its own canvas back on.</summary>
	internal void Hold()
	{
		if (canvas != null && canvas.enabled)
		{
			canvas.enabled = false;
		}
	}

	internal void Release()
	{
		if (canvas == null)
		{
			return;
		}
		if (added)
		{
			UnityEngine.Object.Destroy(canvas);
		}
		else
		{
			canvas.enabled = restoreEnabled?.Invoke() ?? true;
		}
	}
}
