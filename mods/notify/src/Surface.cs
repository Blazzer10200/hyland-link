using System;
using HylandHud;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;

namespace HylandNotify;

/// <summary>What every surface gets each frame.</summary>
internal sealed class Frame
{
	internal HUD Hud = null!;

	/// <summary>Hidden with the game's HUD. Nearly everything goes here.</summary>
	internal RectTransform Root = null!;

	/// <summary>Always shown (under the game's menus): for pieces the game shows while its HUD is hidden.</summary>
	internal RectTransform Overlay = null!;

	internal Sprites Sprites = null!;

	internal TMP_FontAsset Font = null!;

	internal ScreenLayout Layout = null!;

	internal Settings Settings = null!;

	internal float Dt;

	/// <summary>The left column's next free y (HUD units from the top). Call, toasts and the tracker stack down it.</summary>
	internal float ColumnY;

	/// <summary>The next free y in the top-center stack (police status, then the big popups).</summary>
	internal float CenterY;

	/// <summary>The next free y in the corner stack (game saved, then hints).</summary>
	internal float CornerY;
}

/// <summary>
/// One of the game's notifications, replaced. It hides the game's piece (see <see cref="Muffle"/>), reads the same state
/// the game shows and draws it in glass. <see cref="Teardown"/> must leave the game's piece exactly as it was.
/// </summary>
internal abstract class Surface
{
	internal readonly Backoff Retry;

	protected Surface(string name)
	{
		Retry = new Backoff(name);
	}

	internal abstract bool On(Settings settings);

	internal abstract void Update(Frame frame);

	internal abstract void Teardown();

	/// <summary>Runs teardown steps so one failure doesn't stop the rest, then raises the first failure.</summary>
	protected static void Steps(params Action[] steps)
	{
		Exception? failure = null;
		foreach (Action step in steps)
		{
			Ui.Step(ref failure, step);
		}
		Ui.Rethrow(failure);
	}

	protected static void Destroy(Component? component)
	{
		if (component != null)
		{
			UnityEngine.Object.Destroy(component.gameObject);
		}
	}
}
