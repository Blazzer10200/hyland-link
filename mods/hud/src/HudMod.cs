using System;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Compass;
using Il2CppScheduleOne.UI.Phone;
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

	/// <summary>The phone's own sprites: the HUD's are thrown away on every HUD rebuild, and the Hyland app stays open
	/// through those.</summary>
	private Sprites? phoneSprites;

	private PhoneSkin? phoneSkin;

	private AppsSkin? appsSkin;

	private HylandApp? app;

	private RainLevel? rain;

	private bool phoneDirty;

	/// <summary>Each part fails on its own: a compass problem leaves the glass hotbar up, and the other way round.</summary>
	private readonly Backoff shared = new Backoff("");

	private readonly Backoff hotbarRetry = new Backoff("hotbar");

	private readonly Backoff compassRetry = new Backoff("compass");

	private readonly Backoff phoneShared = new Backoff("phone");

	private readonly Backoff phoneSkinRetry = new Backoff("glass phone");

	private readonly Backoff appsSkinRetry = new Backoff("game apps");

	private readonly Backoff appRetry = new Backoff("phone app");

	private readonly Backoff rainRetry = new Backoff("rain level");

	public override void OnInitializeMelon()
	{
		settings = new Settings(() =>
		{
			settingsDirty = true;
		}, () =>
		{
			phoneDirty = true;
		});
		LoggerInstance.Msg("Hyland HUD " + Info.Version + ". Settings: the Hyland app on the phone, or [HylandHud] in UserData/MelonPreferences.cfg.");
	}

	public override void OnLateUpdate()
	{
		if (settings == null)
		{
			return;
		}
		UpdateHud(settings);
		UpdatePhone(settings);
	}

	private void UpdateHud(Settings settings)
	{
		if (!shared.Due)
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

	/// <summary>The phone: the Hyland app and our rain level always (they're how the HUD is switched back on, and not HUD
	/// art), the glass restyle only with the HUD and "Glass phone" on.</summary>
	private void UpdatePhone(Settings settings)
	{
		if (!phoneShared.Due)
		{
			return;
		}
		Phone phone;
		HomeScreen home;
		AppsCanvas apps;
		try
		{
			LoadManager load = Singleton<LoadManager>.Instance;
			phone = PlayerSingleton<Phone>.Instance;
			home = PlayerSingleton<HomeScreen>.Instance;
			apps = PlayerSingleton<AppsCanvas>.Instance;
			if (load == null || !load.IsGameLoaded || load.IsLoading || Player.Local == null || phone == null || home == null || apps == null)
			{
				TeardownPhone();
				return;
			}
			if ((app != null && !app.IsBoundTo(home)) || (phoneSkin != null && !phoneSkin.IsBoundTo(home)))
			{
				TeardownPhone();
			}
			if (phoneDirty || !(settings.Enabled.Value && settings.GlassPhone.Value))
			{
				DisposePhoneSkin();
				DisposeAppsSkin();
				phoneDirty = false;
			}
			phoneSprites ??= new Sprites();
			phoneShared.Succeeded();
		}
		catch (Exception ex)
		{
			TeardownPhone();
			phoneShared.Failed(ex, LoggerInstance);
			return;
		}
		float dt = Time.unscaledDeltaTime;
		if (appRetry.Due)
		{
			try
			{
				app ??= new HylandApp(phone, home, apps, phoneSprites, settings, LoggerInstance.Msg);
				app.Update(dt);
				appRetry.Succeeded();
			}
			catch (Exception ex)
			{
				DisposeApp();
				appRetry.Failed(ex, LoggerInstance);
			}
		}
		if (settings.Enabled.Value && settings.GlassPhone.Value && phoneSkinRetry.Due)
		{
			try
			{
				phoneSkin ??= new PhoneSkin(phone, home, phoneSprites, settings, LoggerInstance.Msg);
				phoneSkin.Update(dt);
				phoneSkinRetry.Succeeded();
			}
			catch (Exception ex)
			{
				DisposePhoneSkin();
				phoneSkinRetry.Failed(ex, LoggerInstance);
			}
		}
		if (settings.Enabled.Value && settings.GlassPhone.Value && appsSkinRetry.Due)
		{
			try
			{
				appsSkin ??= new AppsSkin(LoggerInstance.Msg);
				appsSkin.Update();
				appsSkinRetry.Succeeded();
			}
			catch (Exception ex)
			{
				DisposeAppsSkin();
				appsSkinRetry.Failed(ex, LoggerInstance);
			}
		}
		if (rainRetry.Due)
		{
			try
			{
				rain ??= new RainLevel(settings);
				rain.Update();
				rainRetry.Succeeded();
			}
			catch (Exception ex)
			{
				DisposeRain();
				rainRetry.Failed(ex, LoggerInstance);
			}
		}
	}

	public override void OnSceneWasLoaded(int buildIndex, string sceneName)
	{
		Teardown();
		TeardownPhone();
		shared.Reset();
		hotbarRetry.Reset();
		compassRetry.Reset();
		phoneShared.Reset();
		phoneSkinRetry.Reset();
		appsSkinRetry.Reset();
		appRetry.Reset();
		rainRetry.Reset();
	}

	public override void OnDeinitializeMelon()
	{
		Teardown();
		TeardownPhone();
		settings?.Dispose();
	}

	private void DisposePhoneSkin()
	{
		try
		{
			phoneSkin?.Dispose();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Putting the game's phone back failed: " + ex.Message);
		}
		phoneSkin = null;
	}

	private void DisposeAppsSkin()
	{
		try
		{
			appsSkin?.Dispose();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Putting the game's app colors back failed: " + ex.Message);
		}
		appsSkin = null;
	}

	private void DisposeApp()
	{
		try
		{
			app?.Dispose();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Removing the Hyland app failed: " + ex.Message);
		}
		app = null;
	}

	private void DisposeRain()
	{
		try
		{
			rain?.Dispose();
		}
		catch (Exception ex)
		{
			LoggerInstance.Warning("Putting the game's rain volume back failed: " + ex.Message);
		}
		rain = null;
	}

	private void TeardownPhone()
	{
		// The restyle first, so it puts the Hyland icon back to plain before the app removes it.
		DisposePhoneSkin();
		DisposeAppsSkin();
		DisposeApp();
		DisposeRain();
		phoneSprites?.Dispose();
		phoneSprites = null;
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
