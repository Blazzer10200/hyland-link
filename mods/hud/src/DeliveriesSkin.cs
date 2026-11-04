using System;
using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// Deliveries, redesigned like the board (sideways). The tabs hug their labels on top with the accent underline and a
/// 1 px rule; the shops are the board's 2-column cards with the shop's color as a left stripe (read off the game's own
/// button color); a shop's order form gets the board's dark item rows (no picture, steppers either side of the count) and
/// the side card with the totals and an accent Place order. Active and past orders sit in 2 columns of glass cards. The
/// shop list and the order panel come fixed at the full 1201 wide, so they're stretched to the inset frame first (the
/// game slides them by anchoredPosition, which stretching keeps).
/// </summary>
internal sealed class DeliveriesSkin : GameAppSkin
{
	/// <summary>The frame's width inside AppLayouts' insets, less the lists' 15 padding a side.</summary>
	private const float ListWidth = 1201f - 2f * 16f * Unit - 30f;

	private const float TabWidth = 96f;

	private static readonly Color Cash = Ui.Color(Rgba.Hex(0x7DDC6F));

	internal DeliveriesSkin(HomeScreen home, Sprites sprites, AppClaims claims, Action<string> log)
		: base(home, sprites, claims, log)
	{
	}

	internal override string AppName => "DeliveryApp";

	protected override void Apply(GameObject app)
	{
		Transform? box = Find(app.transform, "Container");
		if (box == null)
		{
			return;
		}
		Transform? tabs = Find(box, "NavigationBar");
		if (tabs != null && First(tabs))
		{
			Tabs(tabs);
		}
		Transform? pages = Find(box, "Contents/Container");
		if (pages == null)
		{
			return;
		}
		Transform? shops = Find(pages, "Shops");
		if (shops != null)
		{
			if (First(shops))
			{
				Stretch(Find(shops, "List"));
				Stretch(Find(shops, "Order"));
				Grid(Find<GridLayoutGroup>(shops, "List/Scroll View/Viewport/Content/Container"), new Vector2((ListWidth - 8f * Unit) / 2f, 48f * Unit), new Vector2(8f * Unit, 8f * Unit));
				Ghost(shops, "List/Scroll View/Viewport/Gradient");
				Scrollbar(Find(shops, "List/Scroll View/Scrollbar Vertical"));
			}
			Transform? grid = Find(shops, "List/Scroll View/Viewport/Content/Container");
			if (grid != null)
			{
				for (int i = 0; i < grid.childCount; i++)
				{
					Transform shop = grid.GetChild(i);
					if (First(shop))
					{
						ShopCard(shop);
					}
				}
			}
			Transform? orders = Find(shops, "Order");
			if (orders != null)
			{
				for (int i = 0; i < orders.childCount; i++)
				{
					Order(orders.GetChild(i));
				}
			}
		}
		Cards(Find(pages, "ActiveOrders"));
		Cards(Find(pages, "PastOrders"));
	}

	private void Scrollbar(Transform? bar)
	{
		if (bar == null)
		{
			return;
		}
		foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>(true))
		{
			Ghost(graphic);
		}
	}

	/// <summary>The tab bar (already moved on top by AppLayouts): no shade, tabs as wide as the board's, the underline
	/// slim and in the accent. The game moves the underline under the picked tab by its x.</summary>
	private void Tabs(Transform bar)
	{
		Transform? shade = Find(bar, "Background");
		if (shade != null)
		{
			foreach (Graphic graphic in shade.GetComponentsInChildren<Graphic>(true))
			{
				Ghost(graphic);
			}
		}
		Line(bar, top: false, C(0x1B2128));
		HorizontalLayoutGroup? row = Find<HorizontalLayoutGroup>(bar, "Container");
		if (row != null)
		{
			Set(() => row.childForceExpandWidth, v => row.childForceExpandWidth = v, false);
			Transform items = row.transform;
			for (int i = 0; i < items.childCount; i++)
			{
				Transform tab = items.GetChild(i);
				Prefer(tab.gameObject, TabWidth, null);
				Label(tab, "Name", 11f);
				Image? badge = Find<Image>(tab, "Indicator");
				if (badge != null)
				{
					Recolor(badge, C(Theme.Badge));
					Move(badge.rectTransform, r =>
					{
						r.anchoredPosition = new Vector2(38f * Unit, r.anchoredPosition.y);
						r.sizeDelta = new Vector2(14f * Unit, 14f * Unit);
					});
					Label(badge.transform, "Text", 9f, Color.white);
				}
			}
			LayoutRebuilder.MarkLayoutForRebuild(row.transform.Cast<RectTransform>());
		}
		Image? underline = Find<Image>(bar, "Selection");
		Recolor(underline, C(Theme.PhoneAccent));
		Move(underline != null ? underline.rectTransform : null, r =>
		{
			r.sizeDelta = new Vector2((TabWidth - 14f) * Unit, 2f * Unit);
			r.anchoredPosition = new Vector2(r.anchoredPosition.x, 2f * Unit);
		});
	}

	/// <summary>A shop as the board's .ap-shop: a glass card over a rounded panel in the shop's color, inset 4 px on the
	/// left, so the color shows as the stripe. The game's colored button goes clear (it still takes the click).</summary>
	private void ShopCard(Transform shop)
	{
		Image? button = shop.GetComponent<Image>();
		if (button == null)
		{
			return;
		}
		Color color = button.color;
		Ghost(button);
		Image card = Behind(shop, "card", OpaqueCard(12f), Color.white);
		card.rectTransform.offsetMin = new Vector2(4f * Unit, 0f);
		Behind(shop, "stripe", Solid(12f), new Color(color.r, color.g, color.b, 1f));
		Image? face = Find<Image>(shop, "Icon");
		bool hasFace = face != null && face.gameObject.activeSelf;
		Recolor(face, C(Theme.PhoneTrack));
		Move(face != null ? face.rectTransform : null, r =>
		{
			r.anchoredPosition = new Vector2(29f * Unit, 0f);
			r.sizeDelta = new Vector2(30f * Unit, 30f * Unit);
		});
		float left = (hasFace ? 53f : 14f) * Unit;
		Text? title = Find<Text>(shop, "Title");
		Label(title, 13f, C(Theme.PhoneText));
		Move(title != null ? title.rectTransform : null, r => Across(r, left, 30f * Unit));
		Text? description = Find<Text>(shop, "Description");
		Label(description, 10f, C(Theme.PhoneMuted));
		Move(description != null ? description.rectTransform : null, r => Across(r, left, 30f * Unit));
		Image? arrow = Find<Image>(shop, "Arrow");
		Recolor(arrow, C(Theme.PhoneMuted));
		Move(arrow != null ? arrow.rectTransform : null, r =>
		{
			r.anchoredPosition = new Vector2(-14f * Unit, 0f);
			r.sizeDelta = new Vector2(12f * Unit, 12f * Unit);
		});
	}

	/// <summary>The board's shop card fill: opaque (the stripe panel under it mustn't show through), with the slate edge.</summary>
	private Sprite OpaqueCard(float boardRadius)
	{
		return sprites.Get("opaqueCard" + boardRadius, () => Art.Glass(boardRadius * Unit, Theme.PhoneApp, Theme.PhoneStroke, Art.PhoneEdge), mipmaps: true);
	}

	/// <summary>One shop's order panel: its fixed parts once, then the item rows (spawned at the game's start, re-sorted on
	/// every open).</summary>
	private void Order(Transform shop)
	{
		Transform? form = Find(shop, "Container");
		if (form == null)
		{
			return;
		}
		if (First(shop))
		{
			Ghost(shop, "Dropshadow");
			OrderHeader(form);
			Ghost(form, "Content/Background");
			Transform? gradient = Deep(form, "Gradient");
			Ghost(gradient != null ? gradient.GetComponent<Graphic>() : null);
			Grid(Find<GridLayoutGroup>(form, "Content/Divider/Entries/ScrollView/Viewport/Listings"), new Vector2(350f, 34f * Unit), new Vector2(6f * Unit, 6f * Unit));
			Details(Find(form, "Content/Divider/Details"));
		}
		Transform? listings = Find(form, "Content/Divider/Entries/ScrollView/Viewport/Listings");
		if (listings != null)
		{
			for (int i = 0; i < listings.childCount; i++)
			{
				Transform item = listings.GetChild(i);
				if (First(item))
				{
					Item(item);
				}
			}
		}
	}

	/// <summary>The order's header: the shop-colored band goes, Back is a step chip, the face and name small.</summary>
	private void OrderHeader(Transform form)
	{
		Transform? header = Find(form, "Header");
		if (header == null)
		{
			return;
		}
		Ghost(header.GetComponent<Graphic>());
		Height(header.gameObject, 40f);
		Spacing(Find<HorizontalLayoutGroup>(header, "Container"), 8f, Pad(0f, 0f, 4f, 4f));
		Transform? back = Find(header, "Container/Interaction/Container/BackButton");
		if (back != null)
		{
			Prefer(back.gameObject, 74f, null);
			Recolor(Find<Graphic>(back, "Background"), C(0x222831));
			ChipTints(back.GetComponent<Selectable>());
			Label(back, "Container/Label", 12f, C(Theme.PhoneText));
			Recolor(Find<Graphic>(back, "Container/Arrow"), C(Theme.PhoneText));
		}
		Transform? face = Find(header, "Container/Icon");
		if (face != null)
		{
			Prefer(face.gameObject, 22f, 22f);
			Recolor(face.GetComponent<Graphic>(), C(Theme.PhoneTrack));
		}
		Label(header, "Container/Labels/Container/Title", 13f, C(Theme.PhoneText));
		Label(header, "Container/Labels/Container/Description", 10f, C(Theme.PhoneMuted));
	}

	/// <summary>The side card: destination and dock chips, the totals with green amounts, the note, Place order in the
	/// accent (its tint flips with whether you can order, every physics tick: the colors are the tint's, so that's kept).</summary>
	private void Details(Transform? details)
	{
		if (details == null)
		{
			return;
		}
		Prefer(details.gameObject, 168f, null);
		Ghost(details, "Background");
		Behind(details, "card", Card(14f), Color.white);
		Transform? stack = Find(details, "Container");
		if (stack == null)
		{
			return;
		}
		Spacing(stack.GetComponent<VerticalLayoutGroup>(), 6f, Pad(11f, 11f, 9f, 9f));
		Transform? confirm = Find(stack, "Confirm");
		foreach (Dropdown dropdown in stack.GetComponentsInChildren<Dropdown>(true))
		{
			Recolor(dropdown.targetGraphic, C(0x222831));
			ChipTints(dropdown);
			Label(dropdown.captionText, 11f, C(Theme.PhoneText));
			Recolor(Find<Graphic>(dropdown.transform, "Arrow"), C(Theme.PhoneMuted));
			RectTransform template = dropdown.template;
			if (template != null)
			{
				// Unity clones the template on every open: restyled once, every list inherits it.
				Recolor(template.GetComponent<Graphic>(), C(Theme.PhoneChip));
				Transform? background = Deep(template, "Item Background");
				Recolor(background != null ? background.GetComponent<Graphic>() : null, C(0x222831));
				Transform? label = Deep(template, "Item Label");
				Label(label != null ? label.GetComponent<Graphic>() : null, 11f, C(Theme.PhoneText));
			}
		}
		foreach (Text text in stack.GetComponentsInChildren<Text>(true))
		{
			Transform at = text.transform;
			if ((confirm != null && at.IsChildOf(confirm)) || text.GetComponentInParent<Dropdown>(true) != null)
			{
				continue;
			}
			Transform? parent = at.parent;
			string row = parent != null ? parent.name : "";
			if (text.name == "Amount" && text.color.g > text.color.r)
			{
				Label(text, 11f, Cash);
			}
			else if (text.name == "Time" || row == "Destination" || row == "LoadingDock")
			{
				Label(text, row == "Destination" || row == "LoadingDock" ? 10f : 11f, C(Theme.PhoneMuted));
			}
			else if (text.name == "Note")
			{
				Label(text, 10f);
			}
			else
			{
				Label(text, 11f, text.color.r > 0.6f && text.color.g > 0.6f && text.color.b > 0.6f ? C(Theme.PhoneText) : null);
			}
		}
		if (confirm != null)
		{
			Tints(confirm.GetComponent<Selectable>(), b =>
			{
				b.normalColor = C(Theme.PhoneAccent);
				b.highlightedColor = C(0xFFF1A0);
				b.pressedColor = C(0xE6CF4A);
				b.selectedColor = C(Theme.PhoneAccent);
				b.disabledColor = C(Theme.PhoneTrack);
				b.colorMultiplier = 1f;
				return b;
			});
			Swap(confirm.GetComponent<Image>(), Solid(10f));
			foreach (Text label in confirm.GetComponentsInChildren<Text>(true))
			{
				Label(label, 14f, C(0x0D1014));
			}
		}
	}

	/// <summary>An item row (Entry_3) as the board's .ap-item: no picture, name over the green price, − count + on the
	/// right as step chips.</summary>
	private void Item(Transform item)
	{
		Recolor(item.GetComponent<Graphic>(), C(Theme.PhoneChip));
		Off(item, "Icon");
		Text? name = Find<Text>(item, "Name");
		Label(name, 11f, C(Theme.PhoneText));
		Move(name != null ? name.rectTransform : null, r => Across(r, 9f * Unit, 165f));
		Text? price = Find<Text>(item, "Price");
		Label(price, 10f, Cash);
		Move(price != null ? price.rectTransform : null, r => Across(r, 9f * Unit, 165f));
		Step(Find(item, "Remove"));
		Step(Find(item, "Add"));
		Transform? count = Find(item, "InputField (Legacy)");
		if (count != null)
		{
			Ghost(count.GetComponent<Graphic>());
			Label(count, "Text (Legacy)", 13f, C(Theme.PhoneText));
		}
	}

	/// <summary>A − or + button as the board's .ap-step: shown at rest (the game's are clear until hovered).</summary>
	private void Step(Transform? button)
	{
		if (button == null)
		{
			return;
		}
		Graphic? image = button.GetComponent<Graphic>();
		Swap(image != null ? image.TryCast<Image>() : null, Solid(8f));
		Recolor(image, C(0x222831));
		ChipTints(button.GetComponent<Selectable>());
		Move(button.Cast<RectTransform>(), r => r.sizeDelta = new Vector2(44f, 44f));
		foreach (Text label in button.GetComponentsInChildren<Text>(true))
		{
			Label(label, 13f, C(Theme.PhoneText));
		}
	}

	/// <summary>Active or past orders: 2 columns of the board's glass cards. The game spawns and destroys them as orders come
	/// and go; their status pill and shop colors stay the game's.</summary>
	private void Cards(Transform? page)
	{
		if (page == null)
		{
			return;
		}
		Transform? grid = Find(page, "Scroll View/Viewport/Content/Container");
		if (First(page))
		{
			Grid(grid != null ? grid.GetComponent<GridLayoutGroup>() : null, new Vector2((ListWidth - 8f * Unit) / 2f, 215f), new Vector2(8f * Unit, 8f * Unit));
			Scrollbar(Find(page, "Scroll View/Scrollbar Vertical"));
			Label(page, "DeliveriesIndicator", 13f, C(Theme.PhoneMuted));
		}
		if (grid == null)
		{
			return;
		}
		for (int i = 0; i < grid.childCount; i++)
		{
			Transform card = grid.GetChild(i);
			if (!First(card))
			{
				continue;
			}
			Ghost(card, "Shade");
			Image? background = Find<Image>(card, "Background");
			Swap(background, Card(14f));
			Recolor(background, Color.white);
			Ghost(card, "Background/Container/Details/Background");
			Transform? reorder = Deep(card, "ReorderButton");
			if (reorder != null)
			{
				Recolor(reorder.GetComponent<Graphic>(), C(0x222831));
				ChipTints(reorder.GetComponent<Selectable>());
			}
		}
	}
}
