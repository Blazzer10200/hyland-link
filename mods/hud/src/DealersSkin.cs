using System;
using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// Dealer Management, redesigned like the board (upright). The dealer dropdown is the board's picker card (its list
/// too), Cash and Cut are two glass cards with the board's big green and blue values, the inventory sits in a glass card,
/// the assigned customers are glass rows and "+ Assign customer" the board's outlined button; the customer picker becomes
/// a bottom sheet. The rows keep the game's 55 height: the app can't scroll, and ten board-sized rows wouldn't fit. The
/// game's container background is the parent of everything, so it's hidden by alpha, never switched off.
/// </summary>
internal sealed class DealersSkin : GameAppSkin
{
	/// <summary>The content column's width inside AppLayouts' insets.</summary>
	private const float Column = 655f - 2f * 16f * Unit;

	internal DealersSkin(HomeScreen home, Sprites sprites, AppClaims claims, Action<string> log)
		: base(home, sprites, claims, log)
	{
	}

	internal override string AppName => "DealerManagement";

	protected override void Apply(GameObject app)
	{
		Transform? box = Find(app.transform, "Container");
		if (box == null)
		{
			return;
		}
		if (First(box))
		{
			Ghost(box, "Background");
			Transform? column = Find(box, "Background/Content/Container");
			if (column != null)
			{
				Layout(column);
			}
			Transform? picker = Find(box, "CustomerSelector");
			if (picker != null)
			{
				CustomerSheet(picker);
			}
		}
		Transform? rows = Find(box, "CustomerSelector/Shade/Content/Scroll View/Viewport/Content");
		if (rows != null)
		{
			for (int i = 0; i < rows.childCount; i++)
			{
				Transform row = rows.GetChild(i);
				if (First(row))
				{
					PickerRow(row);
				}
			}
		}
	}

	private void Layout(Transform column)
	{
		Spacing(column.GetComponent<VerticalLayoutGroup>(), 10f);
		Transform? dropdown = Find(column, "Dropdown (Legacy)");
		if (dropdown != null)
		{
			Picker(dropdown);
		}
		Transform? details = Find(column, "Details");
		if (details != null)
		{
			Height(details.gameObject, 57f);
			Spacing(Find<HorizontalLayoutGroup>(details, "Container"), 8f);
			Money(Find(details, "Container/Cash"), Rgba.Hex(0x7DDC6F));
			Money(Find(details, "Container/Cut"), Theme.Friend);
		}
		Transform? inventory = Find(column, "Inventory");
		if (inventory != null)
		{
			Ghost(inventory, "Backgroiund");
			Behind(inventory, "card", Card(14f), Color.white);
			Spacing(inventory.GetComponent<VerticalLayoutGroup>(), 5f, Pad(12f, 12f, 10f, 10f));
			Grid(Find<GridLayoutGroup>(inventory, "Entries"), new Vector2((Column - 24f * Unit - 10f * Unit) / 2f, 20f * Unit), new Vector2(10f * Unit, 8f * Unit));
			Transform? entries = Find(inventory, "Entries");
			if (entries != null)
			{
				foreach (Text text in entries.GetComponentsInChildren<Text>(true))
				{
					Label(text, null, C(Theme.PhoneText));
				}
			}
			Label(inventory, "Description", 13f, C(Theme.PhoneMuted));
		}
		Label(column, "CustomerTitle", 10f, C(Theme.PhoneMuted), align: TextAnchor.MiddleLeft);
		Transform? customers = Find(column, "Customers");
		if (customers != null)
		{
			Spacing(customers.GetComponent<VerticalLayoutGroup>(), 6f);
			for (int i = 0; i < customers.childCount; i++)
			{
				Customer(customers.GetChild(i));
			}
		}
		Transform? assign = Find(column, "Assign");
		if (assign != null)
		{
			Image? image = assign.GetComponent<Image>();
			Swap(image, OpaqueChip(10f));
			Recolor(image, Color.white);
			ChipTints(assign.GetComponent<Selectable>());
			Label(assign, "Name", 13f, C(Theme.PhoneText));
			Recolor(Find<Graphic>(assign, "Image"), C(Theme.PhoneText));
		}
	}

	/// <summary>The board's .ap-resp fill: #1b2128 with the slate edge.</summary>
	private Sprite OpaqueChip(float boardRadius)
	{
		return sprites.Get("chip" + boardRadius, () => Art.Glass(boardRadius * Unit, Theme.PhoneChip, Theme.PhoneStroke, Art.PhoneEdge), mipmaps: true);
	}

	/// <summary>The dealer dropdown as the board's picker card. The game swaps its sprite on open and pick, so it's hidden
	/// by alpha (the click and the swaps keep working) and our card drawn behind.</summary>
	private void Picker(Transform dropdown)
	{
		Ghost(dropdown.GetComponent<Graphic>());
		Behind(dropdown, "card", Card(14f), Color.white);
		Height(dropdown.gameObject, 52f);
		Spacing(Find<HorizontalLayoutGroup>(dropdown, "Content"), 10f, Pad(12f, 12f, 9f, 9f));
		Transform? face = Find(dropdown, "Content/Image");
		if (face != null)
		{
			Prefer(face.gameObject, 34f, 34f);
		}
		Label(dropdown, "Content/Label", 14f, C(Theme.PhoneText));
		Recolor(Find<Graphic>(dropdown, "Content/Arrow"), C(Theme.PhoneMuted));
		Transform? template = Find(dropdown, "Template");
		if (template != null)
		{
			// Unity clones the template on every open: restyled once, every list inherits it.
			Recolor(template.GetComponent<Graphic>(), C(Theme.PhoneChip));
			Transform? background = Deep(template, "Item Background");
			Recolor(background != null ? background.GetComponent<Graphic>() : null, C(Theme.PhoneChip));
			Transform? label = Deep(template, "Item Label");
			Label(label != null ? label.GetComponent<Graphic>() : null, 14f, C(Theme.PhoneText));
			Transform? line = Deep(template, "Line");
			Recolor(line != null ? line.GetComponent<Graphic>() : null, C(0x1B2128));
		}
	}

	/// <summary>Cash or Cut as the board's card: a muted label over the big value in its color.</summary>
	private void Money(Transform? card, Rgba color)
	{
		if (card == null)
		{
			return;
		}
		Ghost(card.GetComponent<Graphic>());
		Behind(card, "card", Card(14f), Color.white);
		Text? title = Find<Text>(card, "Title");
		Label(title, 11f, C(Theme.PhoneMuted), align: TextAnchor.MiddleLeft);
		Move(title != null ? title.rectTransform : null, r => Top(r, 10f, 14f));
		Text? value = Find<Text>(card, "Value");
		Label(value, 20f, C(color), align: TextAnchor.MiddleLeft);
		Move(value != null ? value.rectTransform : null, r => Top(r, 26f, 26f));
	}

	/// <summary>A label across the card, <paramref name="top"/> board px down, 12 px in from each side.</summary>
	private static void Top(RectTransform rect, float top, float height)
	{
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = new Vector2(0f, -top * Unit);
		rect.sizeDelta = new Vector2(-24f * Unit, height * Unit);
	}

	/// <summary>An assigned customer (pooled, ten of them): a glass row, the name light, the ✕ muted.</summary>
	private void Customer(Transform row)
	{
		Ghost(row.GetComponent<Graphic>());
		Behind(row, "card", Card(12f), Color.white);
		Label(row, "Name", 14f, C(Theme.PhoneText));
		Recolor(Find<Graphic>(row, "Remove/Image"), C(Theme.PhoneMuted));
	}

	/// <summary>"Assign customer": the game's centered card becomes the board's bottom sheet.</summary>
	private void CustomerSheet(Transform picker)
	{
		Recolor(Find<Graphic>(picker, "Background"), new Color(0f, 0f, 0f, 0.55f));
		Transform? shade = Find(picker, "Shade");
		Transform? card = Find(picker, "Shade/Content");
		if (shade == null || card == null)
		{
			return;
		}
		Move(shade.Cast<RectTransform>(), r =>
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = new Vector2(1f, 0f);
			r.pivot = new Vector2(0.5f, 0f);
			r.anchoredPosition = Vector2.zero;
			r.sizeDelta = new Vector2(0f, 640f);
		});
		Move(card.Cast<RectTransform>(), r =>
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = Vector2.one;
			r.offsetMin = Vector2.zero;
			r.offsetMax = Vector2.zero;
		});
		Ghost(card.GetComponent<Graphic>());
		Behind(card, "sheet", Solid(20f), C(0x151A20));
		Label(card, "Top/Title", 16f, C(Theme.PhoneText));
		Recolor(Find<Graphic>(card, "Top/Cancel/Cross"), C(Theme.PhoneMuted));
	}

	/// <summary>A customer in the picker sheet: clear at rest, a faint lift on hover, the name light.</summary>
	private void PickerRow(Transform row)
	{
		Recolor(row.GetComponent<Graphic>(), Color.white);
		Tints(row.GetComponent<Selectable>(), b =>
		{
			b.normalColor = new Color(1f, 1f, 1f, 0f);
			b.highlightedColor = new Color(1f, 1f, 1f, 0.06f);
			b.pressedColor = new Color(1f, 1f, 1f, 0.1f);
			b.selectedColor = new Color(1f, 1f, 1f, 0.06f);
			return b;
		});
		Label(row, "Name", 14f, C(Theme.PhoneText));
		Line(row, top: false, C(0x1B2128));
	}
}
