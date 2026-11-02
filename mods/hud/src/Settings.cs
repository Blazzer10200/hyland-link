using System;
using System.Collections.Generic;
using MelonLoader;

namespace HylandHud;

internal sealed class Settings
{
	private readonly MelonPreferences_Category category;

	private readonly List<Action> unsubscribe = new List<Action>();

	internal readonly MelonPreferences_Entry<bool> Enabled;

	internal readonly MelonPreferences_Entry<bool> Hotbar;

	internal readonly MelonPreferences_Entry<int> SlotGap;

	internal readonly MelonPreferences_Entry<bool> MoneyPanel;

	internal readonly MelonPreferences_Entry<bool> IdleFade;

	internal readonly MelonPreferences_Entry<float> IdleSeconds;

	internal readonly MelonPreferences_Entry<float> IdleOpacity;

	internal readonly MelonPreferences_Entry<bool> Compass;

	internal readonly MelonPreferences_Entry<float> CompassSmoothing;

	internal readonly MelonPreferences_Entry<bool> Clock;

	internal Settings(Action changed)
	{
		category = MelonPreferences.CreateCategory("HylandHud", "Hyland HUD");
		Enabled = Add("Enabled", defaultValue: true, "Enable Hyland HUD", "Off puts the game's own HUD back.", changed);
		Hotbar = Add("Hotbar", defaultValue: true, "Glass hotbar", "Restyle the hotbar: glass rail, rounded slots, lift and underline on the selected slot, item name above it.", changed);
		SlotGap = Add("SlotGap", 6, "Slot gap", "0–20 HUD units between hotbar slots (the game uses 20).", changed);
		MoneyPanel = Add("MoneyPanel", defaultValue: true, "Money panel", "Cash and bank next to the hotbar, counting up when they change. Replaces the game's bank readout; the cash slot stays.", changed);
		IdleFade = Add("IdleFade", defaultValue: true, "Fade when idle", "Fade the hotbar after a while with no key, click or scroll.", changed);
		IdleSeconds = Add("IdleSeconds", 4f, "Idle after", "1–30 seconds without input.", changed);
		IdleOpacity = Add("IdleOpacity", 0.55f, "Idle opacity", "0.1–1.", changed);
		Compass = Add("Compass", defaultValue: true, "Compass strip", "Restyle the compass: N/E/S/W strip with a heading marker. Quest markers stay.", changed);
		CompassSmoothing = Add("CompassSmoothing", 0.08f, "Compass smoothing", "0–0.5 seconds to catch up with the camera. 0 follows it exactly.", changed);
		Clock = Add("Clock", defaultValue: true, "Clock", "In-game time with a day, dusk or night icon under the compass.", changed);
		category.SaveToFile(printmsg: false);
	}

	private MelonPreferences_Entry<T> Add<T>(string key, T defaultValue, string title, string description, Action changed)
	{
		MelonPreferences_Entry<T> entry = category.CreateEntry(key, defaultValue, title, description);
		LemonAction<T, T> handler = (T _, T _) =>
		{
			changed();
		};
		entry.OnEntryValueChanged.Subscribe(handler);
		unsubscribe.Add(() =>
		{
			entry.OnEntryValueChanged.Unsubscribe(handler);
		});
		return entry;
	}

	internal void Dispose()
	{
		foreach (Action item in unsubscribe)
		{
			item();
		}
		unsubscribe.Clear();
	}
}
