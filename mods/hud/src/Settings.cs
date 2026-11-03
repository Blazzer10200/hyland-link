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

	internal readonly MelonPreferences_Entry<bool> TimeWarnings;

	internal readonly MelonPreferences_Entry<bool> Friends;

	internal readonly MelonPreferences_Entry<bool> GlassPhone;

	internal readonly MelonPreferences_Entry<bool> KeyHints;

	internal readonly MelonPreferences_Entry<int> RainVolume;

	internal readonly MelonPreferences_Entry<bool> QuieterIndoors;

	/// <param name="changed">A HUD setting changed: the hotbar and compass rebuild.</param>
	/// <param name="phoneChanged">A phone setting changed: only the phone's restyle rebuilds, so the Hyland app stays open
	/// while its own switches are flipped. The rain entries need neither: they're read every frame.</param>
	internal Settings(Action changed, Action phoneChanged)
	{
		category = MelonPreferences.CreateCategory("HylandHud", "Hyland HUD");
		Enabled = Add("Enabled", defaultValue: true, "Enable Hyland HUD", "Off puts the game's own HUD back.", changed);
		Hotbar = Add("Hotbar", defaultValue: true, "Glass hotbar", "Restyle the hotbar: glass rail, rounded slots, lift and underline on the selected slot, item name above it.", changed);
		SlotGap = Add("SlotGap", 6, "Slot gap", "0–20 HUD units between hotbar slots (the game uses 20).", changed);
		MoneyPanel = Add("MoneyPanel", defaultValue: true, "Bank panel", "Bank balance right of the hotbar, counting up when it changes. Replaces the game's bank slot; the cash slot shows your cash.", changed);
		IdleFade = Add("IdleFade", defaultValue: true, "Fade when idle", "Fade the hotbar after a while with no hotbar use (picking a slot, clicking, scrolling, the inventory). Walking doesn't count.", changed);
		IdleSeconds = Add("IdleSeconds", 4f, "Idle after", "1–30 seconds without input.", changed);
		IdleOpacity = Add("IdleOpacity", 0.55f, "Idle opacity", "0.1–1.", changed);
		Compass = Add("Compass", defaultValue: true, "Compass strip", "Restyle the compass: N/E/S/W strip with a heading marker. Quest markers stay.", changed);
		CompassSmoothing = Add("CompassSmoothing", 0.08f, "Compass smoothing", "0–0.5 seconds to catch up with the camera. 0 follows it exactly.", changed);
		Clock = Add("Clock", defaultValue: true, "Clock", "In-game time with a day, dusk or night icon under the compass. Part of the compass strip: needs Compass on.", changed);
		TimeWarnings = Add("TimeWarnings", defaultValue: true, "Curfew and sleep warnings", "A tag on the clock: curfew in N min (from 8 PM), curfew (9 PM–5 AM), sleep in N min (from 3 AM), sleep now (4 AM, when time stops). Replaces the game's own curfew and 4 AM text.", changed);
		Friends = Add("Friends", defaultValue: true, "Friends on the compass", "A cyan marker with name and distance for each co-op player.", changed);
		GlassPhone = Add("GlassPhone", defaultValue: true, "Glass phone", "Restyle the phone's home screen: glass app tiles, a clock card with the weather, your bank and who's online, and a dark case. Off puts the game's own back. The Hyland app stays either way.", phoneChanged);
		KeyHints = Add("KeyHints", defaultValue: true, "Number key hints", "A small 1–9 on each app tile: the game opens the first nine apps with those keys while the phone is open.", phoneChanged);
		RainVolume = Add("RainVolume", 100, "Rain volume", "0–100% of the game's rain (rain and rain on the roof). On top of Settings › Audio › Weather.", () => { });
		QuieterIndoors = Add("QuieterIndoors", defaultValue: true, "Much quieter rain indoors", "The game only drops rain about 4 dB inside; this takes it down to about a third of that again.", () => { });
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

	/// <summary>Writes the file now, so a change made in the Hyland app survives a crash, not just a clean quit.</summary>
	internal void Save()
	{
		category.SaveToFile(printmsg: false);
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
