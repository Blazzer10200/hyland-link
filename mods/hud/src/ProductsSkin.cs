using System;
using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// My Products, redesigned like the board (sideways): each product type's header is a full-width #1b2128 row (the
/// game's own toggle, arrow and colored title), the products are 46 px glass tiles whose rounded border is the game's own
/// frame color (green when listed), and the picked product sits in a glass card on the right. The tiles' parts are fixed
/// at the game's 100 size, so the tile is scaled rather than re-laid. Favourite tiles are destroyed and remade by the
/// game, and ours go with them; the next pass finds the new ones.
/// </summary>
internal sealed class ProductsSkin : GameAppSkin
{
	private static readonly string[] Sections = { "Favourites", "Weed", "Meth", "Shrooms", "Cocaine" };

	internal ProductsSkin(HomeScreen home, Sprites sprites, AppClaims claims, Action<string> log)
		: base(home, sprites, claims, log)
	{
	}

	internal override string AppName => "ProductManagerApp";

	protected override void Apply(GameObject app)
	{
		// The first Container is the live one; a second, inactive one is an old layout.
		Transform? box = Find(app.transform, "Container");
		Transform? list = box != null ? Find(box, "Content/Container/Products/Scroll View") : null;
		if (box == null || list == null)
		{
			return;
		}
		Transform? content = Find(list, "Viewport/Content");
		if (First(box))
		{
			Ghost(box, "Background");
			Ghost(list, "Shade");
			Transform? bar = Find(list, "Scrollbar Vertical");
			if (bar != null)
			{
				foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>(true))
				{
					Ghost(graphic);
				}
			}
			Spacing(content != null ? content.GetComponent<VerticalLayoutGroup>() : null, 6f, Pad(0f, 6f, 0f, 8f));
			Details(Find(box, "Content/Container/Details"));
		}
		if (content == null)
		{
			return;
		}
		foreach (string name in Sections)
		{
			Transform? section = Find(content, name);
			if (section == null)
			{
				continue;
			}
			if (First(section))
			{
				Section(section);
			}
			Transform? tiles = Find(section, "Entries");
			if (tiles == null)
			{
				continue;
			}
			for (int i = 0; i < tiles.childCount; i++)
			{
				Transform tile = tiles.GetChild(i);
				if (First(tile))
				{
					Tile(tile);
				}
			}
		}
	}

	/// <summary>A product type: the header as the board's full-width .ap-sec row, the tiles' grid at the board's 46 px with
	/// 6 px gaps.</summary>
	private void Section(Transform section)
	{
		Spacing(section.GetComponent<VerticalLayoutGroup>(), 6f, Pad(0f, 0f, 0f, 0f));
		Transform? header = Find(section, "Header");
		if (header != null)
		{
			Height(header.gameObject, 24f);
			Ghost(header, "Container/Background");
			Behind(header, "row", Solid(9f), C(Theme.PhoneChip));
			Recolor(Find<Graphic>(header, "Container/Arrow"), C(Theme.PhoneMuted));
			Label(header, "Container/Title", 12f, align: TextAnchor.MiddleLeft);
		}
		Label(section, "None", 12f, C(Theme.PhoneMuted));
		// The tiles are scaled to 46 px (their parts are fixed at 100), so the grid keeps 100 cells and takes the shrink
		// out of the gaps: 6 px between what shows.
		Grid(Find<GridLayoutGroup>(section, "Entries"), new Vector2(100f, 100f), Vector2.one * (6f * Unit - (100f - 46f * Unit)));
	}

	/// <summary>A product tile (ProductEntry_0): glass behind, the game's frame as the tile's rounded border in the game's
	/// color (gray turned to the board's slate, green kept for listed).</summary>
	private void Tile(Transform tile)
	{
		Set(tile, () => tile.localScale, v => tile.localScale = v, Vector3.one * (46f * Unit / 100f));
		Behind(tile, "tile", sprites.PhoneGlass(11f * Unit / (46f * Unit / 100f)), Color.white);
		Image? frame = Find<Image>(tile, "Frame");
		Swap(frame, sprites.Ring(11f * Unit / (46f * Unit / 100f), Theme.White));
		Recolor(frame, c => c.g > c.r + 0.1f ? c : C(Theme.PhoneStroke));
	}

	/// <summary>The picked product's panel as the board's glass card, the name bigger, the section labels muted.</summary>
	private void Details(Transform? details)
	{
		if (details == null)
		{
			return;
		}
		Ghost(details.GetComponent<Graphic>());
		Ghost(details, "Background");
		Behind(details, "card", Card(14f), Color.white);
		Transform? bar = Find(details, "Scroll View/Scrollbar Vertical");
		if (bar != null)
		{
			foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>(true))
			{
				Ghost(graphic, hold: false);
			}
		}
		Transform? content = Find(details, "Scroll View/Viewport/Content");
		if (content == null)
		{
			return;
		}
		Spacing(content.GetComponent<VerticalLayoutGroup>(), null, Pad(12f, 12f, 10f, 10f));
		Label(content, "Title", 15f, C(Theme.PhoneText));
		Label(content, "EffectHeader", 10f, C(Theme.PhoneMuted));
		Label(content, "Addiction/Title", 10f, C(Theme.PhoneMuted));
		Label(content, "RecipeHeader", 10f, C(Theme.PhoneMuted));
	}
}
