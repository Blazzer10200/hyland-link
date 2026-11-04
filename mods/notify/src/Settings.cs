using System;
using System.Collections.Generic;
using MelonLoader;

namespace HylandNotify;

internal sealed class Settings
{
	private readonly MelonPreferences_Category category;

	private readonly List<Action> unsubscribe = new List<Action>();

	internal readonly MelonPreferences_Entry<bool> Enabled;

	internal readonly MelonPreferences_Entry<bool> Tracker;

	internal readonly MelonPreferences_Entry<int> TrackerRows;

	internal readonly MelonPreferences_Entry<bool> Toasts;

	internal readonly MelonPreferences_Entry<bool> Call;

	internal readonly MelonPreferences_Entry<bool> Wanted;

	internal readonly MelonPreferences_Entry<bool> Hints;

	internal readonly MelonPreferences_Entry<bool> Saved;

	internal readonly MelonPreferences_Entry<bool> Progress;

	internal readonly MelonPreferences_Entry<bool> Popups;

	internal readonly MelonPreferences_Entry<bool> Screens;

	internal Settings(Action changed)
	{
		category = MelonPreferences.CreateCategory("HylandNotify", "Hyland Notifications");
		Enabled = Add("Enabled", defaultValue: true, "Enable Hyland Notifications", "Off puts every one of the game's own notifications back.", changed);
		Tracker = Add("Tracker", defaultValue: true, "Deal and quest tracker", "Left column: deals with face, goods, place, pay and a countdown; quests with their current step.", changed);
		TrackerRows = Add("TrackerRows", 5, "Tracker rows", "2–10 rows before the tracker folds the rest into \"+N more\".", changed);
		Toasts = Add("Toasts", defaultValue: true, "Toasts and unread badge", "Glass toasts (texts from one person fold into one) and a count on the unread messages badge.", changed);
		Call = Add("Call", defaultValue: true, "Incoming call card", "The caller's name and picture at the top of the left column.", changed);
		Wanted = Add("Wanted", defaultValue: true, "Police status", "Investigating, wanted, dead or alive, under arrest and body search, under the compass.", changed);
		Hints = Add("Hints", defaultValue: true, "Hints", "Game hints under the minimap (or top right when the minimap is elsewhere), never over it.", changed);
		Saved = Add("Saved", defaultValue: true, "Game saved", "A small \"Game saved\" tag next to the hints.", changed);
		Progress = Add("Progress", defaultValue: true, "Progress bar", "Glass progress bar (cuffing and the like) above the hotbar.", changed);
		Popups = Add("Popups", defaultValue: true, "Big popups", "Deal done, new customers, cartel influence and status, special customers: glass cards at the top center.", changed);
		Screens = Add("Screens", defaultValue: true, "Police screens", "Ticket, arrest notice and hospital bill restyled in glass. Their buttons stay the game's.", changed);
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
