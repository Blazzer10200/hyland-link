using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace HylandMinimap;

internal sealed class Settings
{
	private readonly MelonPreferences_Category category;

	private readonly List<Action> unsubscribe = new List<Action>();

	internal readonly MelonPreferences_Entry<bool> Enabled;

	internal readonly MelonPreferences_Entry<MapShape> Shape;

	internal readonly MelonPreferences_Entry<MapCorner> Corner;

	internal readonly MelonPreferences_Entry<int> Size;

	internal readonly MelonPreferences_Entry<float> Zoom;

	internal readonly MelonPreferences_Entry<float> Opacity;

	internal readonly MelonPreferences_Entry<bool> Rotate;

	internal readonly MelonPreferences_Entry<bool> HideInMenus;

	internal readonly MelonPreferences_Entry<bool> ShowCoop;

	internal readonly MelonPreferences_Entry<int> Margin;

	internal readonly MelonPreferences_Entry<int> OffsetX;

	internal readonly MelonPreferences_Entry<int> OffsetY;

	internal readonly MelonPreferences_Entry<KeyCode> ZoomInKey;

	internal readonly MelonPreferences_Entry<KeyCode> ZoomOutKey;

	internal readonly MelonPreferences_Entry<float> ZoomStep;

	internal readonly MelonPreferences_Entry<bool> BorderEnabled;

	internal readonly MelonPreferences_Entry<int> BorderWidth;

	internal readonly MelonPreferences_Entry<Color> BorderColor;

	internal readonly MelonPreferences_Entry<bool> OutlineEnabled;

	internal readonly MelonPreferences_Entry<int> OutlineWidth;

	internal readonly MelonPreferences_Entry<Color> OutlineColor;

	internal readonly MelonPreferences_Entry<bool> ShowCustomers;

	internal readonly MelonPreferences_Entry<CustomerMarkerStyle> CustomerStyle;

	internal readonly MelonPreferences_Entry<int> CustomerMarkerSize;

	internal readonly MelonPreferences_Entry<int> CustomerNameSize;

	internal readonly MelonPreferences_Entry<bool> ShowPotentialCustomers;

	internal readonly MelonPreferences_Entry<bool> HighlightPotentialCustomers;

	internal readonly MelonPreferences_Entry<Color> CustomerColor;

	internal readonly MelonPreferences_Entry<Color> PotentialCustomerColor;

	internal readonly MelonPreferences_Entry<bool> ShowAlreadyPurchased;

	internal readonly MelonPreferences_Entry<bool> ShowActiveDeals;

	internal readonly MelonPreferences_Entry<Color> AlreadyPurchasedColor;

	internal readonly MelonPreferences_Entry<Color> ActiveDealColor;

	internal readonly MelonPreferences_Entry<bool> ShowRegionLockedCustomers;

	internal readonly MelonPreferences_Entry<Color> RegionLockedColor;

	internal readonly MelonPreferences_Entry<bool> ShowDealers;

	internal readonly MelonPreferences_Entry<CustomerMarkerStyle> DealerStyle;

	internal readonly MelonPreferences_Entry<int> DealerMarkerSize;

	internal readonly MelonPreferences_Entry<int> DealerNameSize;

	internal readonly MelonPreferences_Entry<Color> DealerColor;

	internal readonly MelonPreferences_Entry<bool> ShowVehicles;

	internal readonly MelonPreferences_Entry<bool> ShowVehicleNames;

	internal readonly MelonPreferences_Entry<int> VehicleMarkerSize;

	internal readonly MelonPreferences_Entry<int> VehicleNameSize;

	internal readonly MelonPreferences_Entry<Color> VehicleColor;

	internal Settings(Action changed)
	{
		category = MelonPreferences.CreateCategory("HylandMinimap", "Hyland Minimap");
		Enabled = Add("Enabled", defaultValue: true, "Show minimap", "Enable or disable the HUD map.", changed);
		Shape = Add("Shape", MapShape.Circle, "Map shape", "Circle or square clipping.", changed);
		Corner = Add("Corner", MapCorner.TopRight, "Screen corner", "Choose a corner to avoid other HUD mods.", changed);
		Size = Add("Size", 240, "Map size", "120–480 units, relative to a 1920x1080 HUD.", changed);
		Zoom = Add("Zoom", 3f, "Zoom", "0.1–12. Lower values show much more of the town.", changed);
		Opacity = Add("Opacity", 0.95f, "Opacity", "0.1–1. Applies to the entire minimap.", changed);
		Rotate = Add("Rotate", defaultValue: false, "Rotate with view", "Keep your viewing direction at the top.", changed);
		HideInMenus = Add("HideInMenus", defaultValue: true, "Hide during menus", "Hide while the phone, pause menu or another game UI is open.", changed);
		ShowCoop = Add("ShowCoop", defaultValue: true, "Show co-op players", "Show nearby connected players in cyan.", changed);
		Margin = Add("Margin", 24, "Corner margin", "0–200 units from the screen edges.", changed);
		OffsetX = Add("OffsetX", 0, "Horizontal offset", "-1000–1000 HUD units. Positive moves right from the chosen corner.", changed);
		OffsetY = Add("OffsetY", 0, "Vertical offset", "-1000–1000 HUD units. Positive moves up from the chosen corner.", changed);
		ZoomInKey = Add("ZoomInKey", KeyCode.PageUp, "Zoom in key", "Keyboard key; None disables it. Ignored while a menu is open.", changed);
		ZoomOutKey = Add("ZoomOutKey", KeyCode.PageDown, "Zoom out key", "Keyboard key; None disables it. Ignored while a menu is open.", changed);
		ZoomStep = Add("ZoomStep", 0.5f, "Zoom per key press", "0.1–3. Each press saves the new zoom level.", changed);
		BorderEnabled = Add("BorderEnabled", defaultValue: true, "Show border", "Show a border around either map shape.", changed);
		BorderWidth = Add("BorderWidth", 4, "Border thickness", "0–24 reference HUD units.", changed);
		BorderColor = Add("BorderColor", new Color(0.18f, 0.22f, 0.27f, 1f), "Border color", "Color and transparency of the border.", changed);
		OutlineEnabled = Add("OutlineEnabled", defaultValue: true, "Show outer outline", "Draw an outer outline around the border.", changed);
		OutlineWidth = Add("OutlineWidth", 2, "Outline thickness", "0–12 reference HUD units.", changed);
		OutlineColor = Add("OutlineColor", new Color(0.02f, 0.025f, 0.03f, 1f), "Outline color", "Color and transparency of the outer outline.", changed);
		ShowCustomers = Add("ShowCustomers", defaultValue: true, "Show customers", "Show customers and eligible potential customers on the map.", changed);
		CustomerStyle = Add("CustomerStyle", CustomerMarkerStyle.Portraits, "Customer marker style", "Small faces or dots with names. Missing faces use named dots.", changed);
		CustomerMarkerSize = Add("CustomerMarkerSize", 24, "Customer marker size", "12–40 reference HUD units for portraits. Dots are smaller.", changed);
		CustomerNameSize = Add("CustomerNameSize", 11, "Customer name size", "8–18 reference HUD units.", changed);
		ShowPotentialCustomers = Add("ShowPotentialCustomers", defaultValue: true, "Show potential customers", "Show the potential customers identified by the game's map.", changed);
		HighlightPotentialCustomers = Add("HighlightPotentialCustomers", defaultValue: true, "Highlight potential customers", "Give potential customers a colored halo.", changed);
		CustomerColor = Add("CustomerColor", new Color(0.25f, 0.9f, 0.65f, 1f), "Customer color", "Color of customer dots and portrait borders.", changed);
		PotentialCustomerColor = Add("PotentialCustomerColor", new Color(0.75f, 0.35f, 1f, 1f), "Potential customer color", "Highlight color for potential customers.", changed);
		ShowAlreadyPurchased = Add("ShowAlreadyPurchased", defaultValue: true, "Show already purchased", "Show a check while a completed purchase blocks another offer in the game's dialogue.", changed);
		ShowActiveDeals = Add("ShowActiveDeals", defaultValue: true, "Show active deals", "Show a clock for accepted deals awaiting delivery.", changed);
		AlreadyPurchasedColor = Add("AlreadyPurchasedColor", new Color(0.2f, 0.85f, 0.3f, 1f), "Already purchased color", "Color of the completed-purchase check badge.", changed);
		ActiveDealColor = Add("ActiveDealColor", new Color(0.25f, 0.65f, 1f, 1f), "Active deal color", "Color of the accepted-deal clock badge.", changed);
		ShowRegionLockedCustomers = Add("ShowRegionLockedCustomers", defaultValue: true, "Show region-locked customers", "Show customers in locked regions with a lock badge.", changed);
		RegionLockedColor = Add("RegionLockedColor", new Color(0.7f, 0.72f, 0.77f, 1f), "Region lock color", "Color of locked-region customer markers and lock badges.", changed);
		ShowDealers = Add("ShowDealers", defaultValue: true, "Show dealers", "Show hired dealers on the minimap.", changed);
		DealerStyle = Add("DealerStyle", CustomerMarkerStyle.Portraits, "Dealer marker style", "Portraits or dots with names, plus a dealer badge. Missing faces use named dots.", changed);
		DealerMarkerSize = Add("DealerMarkerSize", 24, "Dealer marker size", "12–40 reference HUD units for portraits. Dots are smaller.", changed);
		DealerNameSize = Add("DealerNameSize", 11, "Dealer name size", "8–18 reference HUD units for dealer names.", changed);
		DealerColor = Add("DealerColor", new Color(1f, 0.7f, 0.2f, 1f), "Dealer color", "Color of dealer dots, portrait borders, and dealer badges.", changed);
		ShowVehicles = Add("ShowVehicles", defaultValue: true, "Show owned vehicles", "Show owned vehicles. Hide your current vehicle, and occupied co-op vehicles while co-op markers are shown.", changed);
		ShowVehicleNames = Add("ShowVehicleNames", defaultValue: true, "Show vehicle names", "Show the native vehicle name or vehicle code beside its icon.", changed);
		VehicleMarkerSize = Add("VehicleMarkerSize", 18, "Vehicle marker size", "12–32 reference HUD units for car and bike icons.", changed);
		VehicleNameSize = Add("VehicleNameSize", 11, "Vehicle name size", "8–18 reference HUD units for vehicle names.", changed);
		VehicleColor = Add("VehicleColor", new Color(0.5f, 0.8f, 1f, 1f), "Vehicle color", "Color of owned vehicle icons.", changed);
		category.SaveToFile(printmsg: false);
	}

	internal void Save()
	{
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
