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
	private Settings? settings;

	private Sprites? sprites;

	private HotbarSkin? hotbar;

	private CompassSkin? compass;

	private bool settingsDirty = true;

	private float retryAt;

	private int consecutiveFailures;

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
		if (settings == null || Time.unscaledTime < retryAt)
		{
			return;
		}
		try
		{
			LoadManager load = Singleton<LoadManager>.Instance;
			HUD hud = Singleton<HUD>.Instance;
			PlayerInventory inventory = PlayerSingleton<PlayerInventory>.Instance;
			CompassManager compassManager = Singleton<CompassManager>.Instance;
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
			TMP_FontAsset font = hud.selectedItemLabel != null ? hud.selectedItemLabel.font : null;
			if (hotbar == null && settings.Hotbar.Value)
			{
				hotbar = new HotbarSkin(hud, inventory, sprites, font, settings, LoggerInstance.Msg);
			}
			if (compass == null && settings.Compass.Value && compassManager != null)
			{
				compass = new CompassSkin(compassManager, hud, sprites, font, settings, LoggerInstance.Msg);
			}
			float dt = Time.unscaledDeltaTime;
			hotbar?.Update(dt);
			compass?.Update(dt);
			consecutiveFailures = 0;
		}
		catch (Exception ex)
		{
			// Teardown puts the game's HUD back, so a failure leaves the vanilla one working.
			Teardown();
			float delay = Math.Min(60f, 5f * (1 << Math.Min(consecutiveFailures, 4)));
			retryAt = Time.unscaledTime + delay;
			consecutiveFailures++;
			string error = ex.GetType().Name + ": " + ex.Message;
			if (consecutiveFailures <= 3)
			{
				LoggerInstance.Warning("Hyland HUD unavailable; retrying in " + delay + "s: " + error);
			}
			else if (consecutiveFailures == 4)
			{
				LoggerInstance.Warning("Hyland HUD still unavailable; retrying every minute without logging again: " + error);
			}
		}
	}

	public override void OnSceneWasLoaded(int buildIndex, string sceneName)
	{
		Teardown();
		retryAt = 0f;
		consecutiveFailures = 0;
	}

	public override void OnDeinitializeMelon()
	{
		Teardown();
		settings?.Dispose();
	}

	private void Teardown()
	{
		// Each step on its own: a half-destroyed scene must not stop the rest from being put back.
		try
		{
			hotbar?.Dispose();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Putting the game's hotbar back failed: " + ex.Message);
		}
		try
		{
			compass?.Dispose();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Putting the game's compass back failed: " + ex.Message);
		}
		hotbar = null;
		compass = null;
		sprites?.Dispose();
		sprites = null;
	}
}
