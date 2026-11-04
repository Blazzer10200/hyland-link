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

	private Text? effects;

	private RectTransform? scrolled;

	/// <summary>The favourite effects on one wrapping line ("Calming · Munchies · Smelly"), as the game rewrites them on
	/// every pick: one per line didn't fit the card's height.</summary>
	protected override void EveryFrame(GameObject app)
	{
		if (effects != null && effects.text.Contains('\n'))
		{
			string[] lines = effects.text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < lines.Length; i++)
			{
				// The game pads each name inside its color tag ("<color=#…>  Calming</color>").
				lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i].Replace("•", string.Empty), @">\s+", ">").Trim();
			}
			effects.text = string.Join("  ·  ", lines);
		}
		// The card's rows as tall as they ask to be, so its scroll view knows how far there is to go (the prefab's fitter
		// left them 0 tall once anchored to the top).
		if (scrolled != null)
		{
			float rows = LayoutUtility.GetPreferredHeight(scrolled);
			if (!Mathf.Approximately(scrolled.sizeDelta.y, rows))
			{
				scrolled.sizeDelta = new Vector2(scrolled.sizeDelta.x, rows);
			}
		}
		// Legacy Text reports one line's height to the layout here; the generator at the card's width gives the real one.
		foreach ((Text text, LayoutElement? element) in values)
		{
			if (text == null || element == null)
			{
				continue;
			}
			float width = text.rectTransform.rect.width;
			if (width <= 0f)
			{
				continue;
			}
			float height = Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, text.GetGenerationSettings(new Vector2(width, 0f))) / text.pixelsPerUnit) + 2f;
			if (!Mathf.Approximately(element.preferredHeight, height))
			{
				element.minHeight = element.preferredHeight = height;
			}
		}
	}

	private readonly System.Collections.Generic.List<(Text Text, LayoutElement? Element)> values = new System.Collections.Generic.List<(Text, LayoutElement?)>();

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
			// The web shows from under the tab band down: nodes near the top drew across the tab names (they sort above
			// the tabs whatever the sibling order).
			Move(Find<RectTransform>(scroll, "Viewport"), r => r.offsetMax = new Vector2(r.offsetMax.x, r.offsetMax.y - 28f * Unit));
			Ghost(scroll, "Viewport/Content/Background");
			Ghost(scroll, "Locked/Image");
			Ghost(box, "CenterPoint");
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
			// Over the web, as on the board (z-index 2): nodes near the top drew across the tab names.
			Transform tabsKept = tabs;
			Set(tabsKept, () => tabsKept.GetSiblingIndex(), v => tabsKept.SetSiblingIndex(v), box.childCount - 1);
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
					Text? name = Find<Text>(buttons.GetChild(i), "Name");
					Label(name, 10f);
					// The board's lock sits just before the name, on its line (the game's, placed by its own font size,
					// landed on the next tab's name at ours).
					float half = name != null ? name.preferredWidth / 2f : 30f;
					Move(Find<RectTransform>(buttons.GetChild(i), "Locked"), r =>
					{
						r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
						r.pivot = new Vector2(1f, 0.5f);
						r.anchoredPosition = new Vector2(-half - 3f * Unit, 1f);
						r.sizeDelta = new Vector2(8f * Unit, 8f * Unit);
					});
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

	/// <summary>
	/// Relationship or Addiction as the board lays them out: the caps label, then the 5 px band with its value at the
	/// right on the same line (the game stacks title, band and value in a 48-unit box their 82 units overflowed).
	/// </summary>
	private void Band(Transform? group)
	{
		if (group == null)
		{
			return;
		}
		Prefer(group.gameObject, null, 34f);
		Move(Find<RectTransform>(group, "Title"), r =>
		{
			r.anchorMin = new Vector2(0f, 1f);
			r.anchorMax = Vector2.one;
			r.pivot = new Vector2(0.5f, 1f);
			r.anchoredPosition = Vector2.zero;
			r.sizeDelta = new Vector2(0f, 18f * Unit);
		});
		Move(Find<RectTransform>(group, "Band"), r =>
		{
			r.anchorMin = new Vector2(0f, 1f);
			r.anchorMax = Vector2.one;
			r.pivot = new Vector2(0f, 0.5f);
			r.anchoredPosition = new Vector2(0f, -26f * Unit);
			r.sizeDelta = new Vector2(-46f * Unit, 5f * Unit);
		});
		Text? value = Find<Text>(group, "Description");
		Label(value, 10f, align: TextAnchor.MiddleRight);
		Move(value != null ? value.rectTransform : null, r =>
		{
			r.anchorMin = r.anchorMax = Vector2.one;
			r.pivot = new Vector2(1f, 0.5f);
			r.anchoredPosition = new Vector2(0f, -26f * Unit);
			r.sizeDelta = new Vector2(42f * Unit, 14f * Unit);
		});
	}

	/// <summary>The picked contact as the board's glass card, 182 px wide; headers muted, values a size up.</summary>
	private void Card(Transform detail)
	{
		Move(detail.Cast<RectTransform>(), r => r.offsetMin = new Vector2(Width - CardWidth * Unit, r.offsetMin.y));
		Behind(detail, "card", Card(14f), Color.white);
		Transform? stack = Find(detail, "Container");
		if (stack == null)
		{
			return;
		}
		// The board's card scrolls (overflow-y: auto): a customer's full details are taller than the card. The panel's
		// own image becomes the mask (opaque so it writes the stencil, hidden by the mask): a stencil Mask, because this
		// screen is turned 90° in the sideways AppsCanvas and a RectMask2D would clip everything.
		Image? panel = detail.GetComponent<Image>();
		// No "already has a ScrollRect?" guard: after a reload the old one is still there until the frame ends (Destroy
		// waits), and the guard skipped all of this. First() already runs this once per panel.
		if (panel != null)
		{
			Swap(panel, Solid(14f));
			Recolor(panel, Color.white);
			Add<Mask>(detail.gameObject).showMaskGraphic = false;
			ScrollRect scroll = Add<ScrollRect>(detail.gameObject);
			scroll.viewport = detail.Cast<RectTransform>();
			scroll.content = stack.Cast<RectTransform>();
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Clamped;
			scroll.scrollSensitivity = 8f;
			Move(stack.Cast<RectTransform>(), r =>
			{
				r.anchorMin = new Vector2(0f, 1f);
				r.anchorMax = Vector2.one;
				r.pivot = new Vector2(0.5f, 1f);
				r.anchoredPosition = Vector2.zero;
				r.sizeDelta = new Vector2(0f, r.sizeDelta.y);
			});
			scrolled = stack.Cast<RectTransform>();
		}
		VerticalLayoutGroup? outer = stack.GetComponent<VerticalLayoutGroup>();
		Spacing(outer, 2f, Pad(12f, 12f, 10f, 10f));
		if (outer != null)
		{
			// Sections take the height their contents ask for, not the prefab's fixed boxes.
			Set(outer, () => outer.childControlHeight, v => outer.childControlHeight = v, true);
			Set(outer, () => outer.childForceExpandHeight, v => outer.childForceExpandHeight = v, false);
		}
		Label(stack, "Name", 14f, C(Theme.PhoneText), bold: true);
		Label(stack, "Type", 10f);
		// The board's .ap-label: short 10 px muted capitals ("Most Purchased Product (Weekly)" wrapped at this width and
		// ran into its list). The game never rewrites these headers.
		(string Path, string Text)[] headers =
		{
			("Relationship/Title", "RELATIONSHIP"), ("Addiction/Title", "ADDICTION"),
			("StandardContainer/StandardHeader", "STANDARDS"), ("FavouriteContainer/FavouriteHeader", "FAVOURITE EFFECTS"),
			("MostPurchasedProductContainer/MostPurchasedProduct", "MOST PURCHASED (WEEKLY)"),
			("TotalSpentContainer/TotalSpent", "TOTAL SPENT (WEEKLY)"), ("DebtContainer/DebtHeader", "DEBT"),
		};
		foreach ((string path, string caps) in headers)
		{
			Text? header = Find<Text>(stack, path);
			Label(header, 10f, C(Theme.PhoneMuted), align: TextAnchor.LowerLeft);
			if (header != null)
			{
				Set(header, () => header.text, v => header.text = v, caps);
				Prefer(header.gameObject, null, 18f);
			}
		}
		Band(Find(stack, "Relationship"));
		Band(Find(stack, "Addiction"));
		foreach (string value in new[] { "FavouriteContainer/Value", "MostPurchasedProductContainer/Value" })
		{
			Label(stack, value, 11f);
		}
		Transform? standard = Find(stack, "StandardContainer/StandardIItem");
		if (standard != null)
		{
			// "★ Low standards" on one 16 px line (the prefab's 15-unit row put the words over the header).
			Prefer(standard.gameObject, null, 16f);
			foreach (Text item in standard.GetComponentsInChildren<Text>(true))
			{
				Label(item, 11f, align: TextAnchor.MiddleLeft);
			}
			foreach (Image star in standard.GetComponentsInChildren<Image>(true))
			{
				Prefer(star.gameObject, 11f, 11f);
			}
		}
		Label(Find<Text>(stack, "TotalSpentContainer/Value"), 13f, bold: true);
		Label(Find<Text>(stack, "DebtContainer/Value"), 13f, bold: true);
		// Each section sizes from its text (the game's boxes are fixed: three lines of effects or products overflowed
		// into the next header), with the board's tighter rhythm.
		foreach (string section in new[] { "StandardContainer", "FavouriteContainer", "MostPurchasedProductContainer", "TotalSpentContainer", "DebtContainer" })
		{
			VerticalLayoutGroup? column = Find<VerticalLayoutGroup>(stack, section);
			if (column == null)
			{
				continue;
			}
			Set(column, () => column.childControlHeight, v => column.childControlHeight = v, true);
			Set(column, () => column.childForceExpandHeight, v => column.childForceExpandHeight = v, false);
			Spacing(column, 0f);
			Prefer(Find(stack, section + "/Space")?.gameObject ?? Find(stack, section + "/Space ")?.gameObject, null, 4f);
		}
		effects = Find<Text>(stack, "FavouriteContainer/Value");
		values.Clear();
		foreach (string path in new[] { "Name", "Type", "FavouriteContainer/Value", "MostPurchasedProductContainer/Value", "TotalSpentContainer/Value", "DebtContainer/Value" })
		{
			Text? value = Find<Text>(stack, path);
			if (value == null)
			{
				continue;
			}
			Text kept = value;
			Set(kept, () => kept.horizontalOverflow, v => kept.horizontalOverflow = v, HorizontalWrapMode.Wrap);
			Prefer(value.gameObject, null, 16f);
			values.Add((value, value.GetComponent<LayoutElement>()));
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
