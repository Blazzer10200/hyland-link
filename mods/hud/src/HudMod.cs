using System;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Compass;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;

namespace HylandHud;

public sealed class HudMod : MelonMod
{
	/// <summary>
	/// Retry timing for one part of the HUD. After a failure it waits 5, 10, 20, 40, then 60 s between tries, and only five
	/// clean minutes reset that, so a fault that hits a few frames after every rebuild still backs off instead of making
	/// the HUD flicker every 5 s.
	/// </summary>
	private sealed class Backoff
	{
		private readonly string part;

		private float retryAt;

		private float lastFailure = float.NegativeInfinity;

		private int failures;

		internal Backoff(string part)
		{
			this.part = part;
		}

		internal bool Due => Time.unscaledTime >= retryAt;

		internal void Succeeded()
		{
			if (failures > 0 && Time.unscaledTime - lastFailure > 300f)
			{
				failures = 0;
			}
		}

		internal void Failed(Exception ex, MelonLogger.Instance log)
		{
			float delay = Math.Min(60f, 5f * (1 << Math.Min(failures, 4)));
			retryAt = Time.unscaledTime + delay;
			lastFailure = Time.unscaledTime;
			failures++;
			string error = ex.GetType().Name + ": " + ex.Message;
			if (failures <= 3)
			{
				log.Warning("Hyland HUD " + part + " unavailable; retrying in " + delay + "s: " + error);
			}
			else if (failures == 4)
			{
				log.Warning("Hyland HUD " + part + " still unavailable; retrying every minute without logging again: " + error);
			}
		}

		internal void Reset()
		{
			retryAt = 0f;
			lastFailure = float.NegativeInfinity;
			failures = 0;
		}
	}

	private Settings? settings;

	private Sprites? sprites;

	private HotbarSkin? hotbar;

	private CompassSkin? compass;

	private bool settingsDirty = true;

	/// <summary>Each part fails on its own: a compass problem leaves the glass hotbar up, and the other way round.</summary>
	private readonly Backoff shared = new Backoff("");

	private readonly Backoff hotbarRetry = new Backoff("hotbar");

	private readonly Backoff compassRetry = new Backoff("compass");

	public override void OnInitializeMelon()
	{
		settings = new Settings(() =>
		{
			settingsDirty = true;
		});
		LoggerInstance.Msg("Hyland HUD " + Info.Version + ". Settings: [HylandHud] in UserData/MelonPreferences.cfg.");
	}

	public override void OnLateUpdate()
	{
		if (settings == null || !shared.Due)
		{
			return;
		}
		HUD hud;
		PlayerInventory inventory;
		CompassManager compassManager;
		TMP_FontAsset font;
		try
		{
			LoadManager load = Singleton<LoadManager>.Instance;
			hud = Singleton<HUD>.Instance;
			inventory = PlayerSingleton<PlayerInventory>.Instance;
			compassManager = Singleton<CompassManager>.Instance;
			if (load == null || !load.IsGameLoaded || load.IsLoading || Player.Local == null || hud == null || inventory == null)
			{
				Teardown();
				return;
			}
			// A settings change rebuilds from the game's own HUD, so every switch puts things back exactly.
			if (settingsDirty || (hotbar != null && !hotbar.IsBoundTo(hud)) || (compass != null && !compass.IsBoundTo(compassManager)))
			{
				Teardown();
				settingsDirty = false;
			}
			if (!settings.Enabled.Value)
			{
				return;
			}
			sprites ??= new Sprites();
			font = hud.selectedItemLabel != null ? hud.selectedItemLabel.font : null;
			shared.Succeeded();
		}
		catch (Exception ex)
		{
			// Teardown puts the game's HUD back, so a failure leaves the vanilla one working.
			Teardown();
			shared.Failed(ex, LoggerInstance);
			return;
		}
		float dt = Time.unscaledDeltaTime;
		if (settings.Hotbar.Value && hotbarRetry.Due)
		{
			try
			{
				hotbar ??= new HotbarSkin(hud, inventory, sprites, font, settings, LoggerInstance.Msg);
				hotbar.Update(dt);
				hotbarRetry.Succeeded();
			}
			catch (Exception ex)
			{
				DisposeHotbar();
				hotbarRetry.Failed(ex, LoggerInstance);
			}
		}
		if (settings.Compass.Value && compassManager != null && compassRetry.Due)
		{
			try
			{
				compass ??= new CompassSkin(compassManager, hud, sprites, font, settings, LoggerInstance.Msg);
				compass.Update(dt);
				compassRetry.Succeeded();
			}
			catch (Exception ex)
			{
				DisposeCompass();
				compassRetry.Failed(ex, LoggerInstance);
			}
		}
	}

	public override void OnSceneWasLoaded(int buildIndex, string sceneName)
	{
		Teardown();
		shared.Reset();
		hotbarRetry.Reset();
		compassRetry.Reset();
	}

	public override void OnDeinitializeMelon()
	{
		Teardown();
		settings?.Dispose();
	}

	private void DisposeHotbar()
	{
		try
		{
			hotbar?.Dispose();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Putting the game's hotbar back failed: " + ex.Message);
		}
		hotbar = null;
	}

	private void DisposeCompass()
	{
		try
		{
			compass?.Dispose();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Putting the game's compass back failed: " + ex.Message);
		}
		compass = null;
	}

	private void Teardown()
	{
		// Each part on its own: a half-destroyed scene must not stop the rest from being put back.
		DisposeHotbar();
		DisposeCompass();
		sprites?.Dispose();
		sprites = null;
	}
}
