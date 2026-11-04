using System;
using System.Collections.Generic;
using HylandHud;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

public sealed class NotifyMod : MelonMod
{
	private Settings? settings;

	private Sprites? sprites;

	private RectTransform? root;

	private Canvas? canvas;

	private RectTransform? hudLayer;

	private bool settingsDirty = true;

	private readonly Backoff shared = new Backoff("");

	private readonly ScreenLayout layout = new ScreenLayout();

	private readonly Frame frame = new Frame();

	/// <summary>Column order matters: call card, then toasts, then the tracker stack down the left column.</summary>
	private readonly List<Surface> surfaces = new List<Surface>
	{
		new CallCard(),
		new Toasts(),
		new Tracker(),
		new WantedStatus(),
		new Popups(),
		new CornerStack(),
		new ProgressBar(),
		new PoliceScreens()
	};

	private readonly HashSet<Surface> live = new HashSet<Surface>();

	public override void OnInitializeMelon()
	{
		settings = new Settings(() =>
		{
			settingsDirty = true;
		});
		LoggerInstance.Msg("Hyland Notifications " + Info.Version + ". Settings: [HylandNotify] in UserData/MelonPreferences.cfg.");
	}

	public override void OnLateUpdate()
	{
		if (settings == null || !shared.Due)
		{
			return;
		}
		HUD hud;
		try
		{
			LoadManager load = Singleton<LoadManager>.Instance;
			hud = Singleton<HUD>.Instance;
			if (load == null || !load.IsGameLoaded || load.IsLoading || Player.Local == null || hud == null)
			{
				Teardown();
				return;
			}
			if (settingsDirty)
			{
				Teardown();
				settingsDirty = false;
			}
			if (!settings.Enabled.Value)
			{
				return;
			}
			sprites ??= new Sprites();
			if (root == null)
			{
				Build();
			}
			// Our HUD layer shows when the game's does (cutscenes, menus and the like hide it); the overlay stays on for
			// the pieces the game shows over a hidden HUD.
			bool hudVisible = hud.canvas != null && hud.canvas.enabled && hud.canvas.gameObject.activeInHierarchy;
			if (hudLayer!.gameObject.activeSelf != hudVisible)
			{
				hudLayer.gameObject.SetActive(hudVisible);
			}
			layout.Update(root!.rect.size);
			frame.Hud = hud;
			frame.Root = hudLayer;
			frame.Overlay = root;
			frame.Sprites = sprites;
			frame.Font = hud.selectedItemLabel != null ? hud.selectedItemLabel.font : null!;
			frame.Layout = layout;
			frame.Settings = settings;
			frame.Dt = Time.unscaledDeltaTime;
			frame.ColumnY = layout.BadgeTop + ScreenLayout.BadgeSize + 14f;
			frame.CenterY = ScreenLayout.WantedTop;
			frame.CornerY = layout.CornerTop;
			shared.Succeeded();
		}
		catch (Exception ex)
		{
			Teardown();
			shared.Failed(ex, LoggerInstance);
			return;
		}
		foreach (Surface surface in surfaces)
		{
			if (!surface.On(settings))
			{
				Release(surface);
				continue;
			}
			if (!surface.Retry.Due)
			{
				continue;
			}
			try
			{
				live.Add(surface);
				surface.Update(frame);
				surface.Retry.Succeeded();
			}
			catch (Exception ex)
			{
				Release(surface);
				surface.Retry.Failed(ex, LoggerInstance);
			}
		}
	}

	public override void OnSceneWasLoaded(int buildIndex, string sceneName)
	{
		Teardown();
		shared.Reset();
		foreach (Surface surface in surfaces)
		{
			surface.Retry.Reset();
		}
	}

	public override void OnDeinitializeMelon()
	{
		Teardown();
		settings?.Dispose();
	}

	private void Build()
	{
		GameObject go = new GameObject("Hyland Notifications");
		canvas = go.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		// Over the game's HUD (1), under its death screen (10), pause menu (100) and loading screen (200).
		canvas.sortingOrder = 2;
		UnityEngine.UI.CanvasScaler scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
		scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0f;
		root = go.GetComponent<RectTransform>();
		hudLayer = Glass.Fill(Ui.Rect("HUD layer", root));
	}

	private void Release(Surface surface)
	{
		if (!live.Remove(surface))
		{
			return;
		}
		try
		{
			surface.Teardown();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Putting the game's own notifications back failed: " + ex.Message);
		}
	}

	/// <summary>Each surface on its own: a half-destroyed scene must not stop the rest from being put back.</summary>
	private void Teardown()
	{
		foreach (Surface surface in surfaces)
		{
			Release(surface);
		}
		if (root != null)
		{
			UnityEngine.Object.Destroy(root.gameObject);
		}
		root = null;
		hudLayer = null;
		canvas = null;
		sprites?.Dispose();
		sprites = null;
	}
}
