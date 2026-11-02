using System;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Map;
using MelonLoader;
using UnityEngine;

namespace HylandMinimap;

public sealed class MinimapMod : MelonMod
{
	private Settings? settings;

	private MinimapHud? hud;

	private bool settingsDirty = true;

	private float retryAt;

	private int consecutiveFailures;

	private readonly ZoomInput zoomInput = new ZoomInput();

	public override void OnInitializeMelon()
	{
		settings = new Settings(() =>
		{
			settingsDirty = true;
		});
		LoggerInstance.Msg("Hyland Minimap 1.2.0 (based on nacho's Schedule I Minimap 1.0.1). Settings: [HylandMinimap] in UserData/MelonPreferences.cfg.");
	}

	public override void OnLateUpdate()
	{
		if (settings == null || Time.unscaledTime < retryAt)
		{
			return;
		}
		try
		{
			LoadManager instance = Singleton<LoadManager>.Instance;
			Player local = Player.Local;
			if (instance == null || !instance.IsGameLoaded || instance.IsLoading || local == null)
			{
				DestroyHud();
				return;
			}
			MapApp instance2 = PlayerSingleton<MapApp>.Instance;
			MapPositionUtility instance3 = Singleton<MapPositionUtility>.Instance;
			if (instance2 == null || instance3 == null || instance2.BackgroundImage == null || instance2.PoIContainer == null)
			{
				DestroyHud();
				retryAt = Time.unscaledTime + 1f;
				return;
			}
			Phone instance4 = PlayerSingleton<Phone>.Instance;
			PauseMenu instance5 = Singleton<PauseMenu>.Instance;
			PlayerCamera instance6 = PlayerSingleton<PlayerCamera>.Instance;
			bool flag = (instance4 != null && instance4.IsOpen) || (instance5 != null && instance5.IsPaused) || (instance6 != null && instance6.ActiveUIElementCount > 0) || Time.timeScale == 0f;
			if (!flag && settings.Enabled.Value && zoomInput.Update(settings))
			{
				settingsDirty = true;
			}
			if (hud == null || !hud.IsAlive || !hud.IsBoundTo(instance2))
			{
				DestroyHud();
				hud = new MinimapHud(instance2, LoggerInstance.Msg);
				settingsDirty = true;
				LoggerInstance.Msg("Minimap HUD created from the native phone map.");
			}
			if (settingsDirty)
			{
				hud.Apply(settings);
				settingsDirty = false;
			}
			bool flag2 = settings.Enabled.Value && (!settings.HideInMenus.Value || !flag);
			hud.SetVisible(flag2);
			if (flag2)
			{
				hud.Update(local, instance6, instance3, settings);
			}
			consecutiveFailures = 0;
		}
		catch (Exception ex)
		{
			DestroyHud();
			retryAt = Time.unscaledTime + 5f;
			if (consecutiveFailures++ < 3)
			{
				LoggerInstance.Warning("Minimap unavailable; retrying in 5s: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
	}

	public override void OnSceneWasLoaded(int buildIndex, string sceneName)
	{
		DestroyHud();
		retryAt = 0f;
		consecutiveFailures = 0;
	}

	public override void OnDeinitializeMelon()
	{
		DestroyHud();
		settings?.Dispose();
	}

	private void DestroyHud()
	{
		hud?.Dispose();
		hud = null;
	}
}
