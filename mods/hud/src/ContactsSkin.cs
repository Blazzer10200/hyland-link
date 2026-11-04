using System;
using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// Contacts, redesigned like the board (sideways): the relationship web in a dark rounded card (#12161b) with the region
/// tabs on a dim bar across its top and the accent underline, the picked contact's card on the right at the board's 182
/// px. The web's nodes already use the board's colors (they're the game's), so they stay; the connection lines go to the
/// board's slate and the selection becomes an accent ring. Pan and zoom stay the game's.
/// </summary>
internal sealed class ContactsSkin : GameAppSkin
{
	private const float CardWidth = 182f;

	/// <summary>The frame's width inside AppLayouts' insets.</summary>
	private const float Width = 1201f - 2f * 16f * Unit;

	internal ContactsSkin(HomeScreen home, Sprites sprites, AppClaims claims, Action<string> log)
		: base(home, sprites, claims, log)
	{
	}

	internal override string AppName => "ContactsApp";

	protected override void Apply(GameObject app)
	{
		Transform? box = Find(app.transform, "Container");
		if (box == null || !First(box))
		{
			return;
		}
		float web = (CardWidth + 12f) * Unit;
		Transform? scroll = Find(box, "Scroll View");
		if (scroll != null)
		{
			Move(scroll.Cast<RectTransform>(), r => r.offsetMax = new Vector2(-web, r.offsetMax.y));
			Ghost(scroll.GetComponent<Graphic>());
			Behind(scroll, "card", Solid(14f), C(0x12161B));
			Swap(Find<Image>(scroll, "Viewport"), Solid(14f));
			Ghost(scroll, "Viewport/Content/Background");
			Ghost(scroll, "Locked/Image");
			Ghost(scroll, "CenterPoint");
			Ghost(scroll, "LowerContainer/Shade");
			Ghost(scroll, "LowerContainer/UnlockInfo/Shade");
			Ghost(scroll, "LowerContainer/UnlockInfo/Background");
			foreach (string bar in new[] { "Scrollbar Horizontal", "Scrollbar Vertical" })
			{
				Transform? found = Find(scroll, bar);
				if (found != null)
				{
					foreach (Graphic graphic in found.GetComponentsInChildren<Graphic>(true))
					{
						Ghost(graphic);
					}
				}
			}
			Transform? content = Find(scroll, "Viewport/Content");
			if (content != null)
			{
				Web(content);
			}
		}
		Transform? tabs = Find(box, "RegionSelection");
		if (tabs != null)
		{
			Move(tabs.Cast<RectTransform>(), r => r.offsetMax = new Vector2(-web, r.offsetMax.y));
			foreach (Graphic graphic in tabs.GetComponentsInChildren<Graphic>(true))
			{
				if (graphic.name == "Shade")
				{
					Ghost(graphic);
				}
			}
			Image bar = Behind(tabs, "bar", null!, new Color(0f, 0f, 0f, 0.35f));
			RectTransform rect = bar.rectTransform;
			rect.anchorMin = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.offsetMin = new Vector2(0f, -28f * Unit);
			rect.offsetMax = Vector2.zero;
			Transform? buttons = Find(tabs, "Buttons");
			if (buttons != null)
			{
				for (int i = 0; i < buttons.childCount; i++)
				{
					Label(buttons.GetChild(i), "Name", 10f);
				}
			}
			Image? underline = Find<Image>(tabs, "Selection");
			Recolor(underline, C(Theme.PhoneAccent));
			Move(underline != null ? underline.rectTransform : null, r => r.sizeDelta = new Vector2(r.sizeDelta.x, 2f * Unit));
		}
		Transform? detail = Find(box, "Detail");
		if (detail != null)
		{
			Card(detail);
		}
	}

	/// <summary>The web: the connection lines (spawned at the game's start, one container per region) in the board's
	/// slate, the selection as an accent ring.</summary>
	private void Web(Transform content)
	{
		for (int i = 0; i < content.childCount; i++)
		{
			Transform child = content.GetChild(i);
			if (!child.name.StartsWith("Connections", StringComparison.Ordinal))
			{
				continue;
			}
			for (int j = 0; j < child.childCount; j++)
			{
				Recolor(child.GetChild(j).GetComponent<Graphic>(), C(0x232A33));
			}
		}
		Image? selection = Find<Image>(content, "SelectionIndicator");
		Swap(selection, sprites.Ring(55f, Theme.White));
		Recolor(selection, C(Theme.PhoneAccent));
	}

	/// <summary>The picked contact as the board's glass card, 182 px wide; headers muted, values a size up.</summary>
	private void Card(Transform detail)
	{
		Move(detail.Cast<RectTransform>(), r => r.offsetMin = new Vector2(Width - CardWidth * Unit, r.offsetMin.y));
		Ghost(detail.GetComponent<Graphic>());
		Behind(detail, "card", Card(14f), Color.white);
		Transform? stack = Find(detail, "Container");
		if (stack == null)
		{
			return;
		}
		Spacing(stack.GetComponent<VerticalLayoutGroup>(), null, Pad(12f, 12f, 10f, 10f));
		Label(stack, "Name", 14f, C(Theme.PhoneText));
		Label(stack, "Type", 10f);
		foreach (string header in new[] { "Relationship/Title", "Addiction/Title", "StandardContainer/StandardHeader", "FavouriteContainer/FavouriteHeader", "MostPurchasedProductContainer/MostPurchasedProduct", "TotalSpentContainer/TotalSpent", "DebtContainer/DebtHeader" })
		{
			Label(stack, header, 10f, C(Theme.PhoneMuted));
		}
		Transform? map = Find(stack, "Map/ShowOnMap");
		if (map != null)
		{
			Image? image = map.GetComponent<Image>();
			Swap(image, Solid(8f));
			Recolor(image, C(0x222831));
			foreach (Text label in map.GetComponentsInChildren<Text>(true))
			{
				Label(label, null, Ui.Color(Rgba.Hex(0x7DDC6F)));
			}
		}
	}
}
