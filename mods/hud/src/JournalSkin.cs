using System;
using System.Collections.Generic;
using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// Journal, redesigned like the board (sideways): the quest rows on the left (a track dot, the title, the expiry pill),
/// the picked quest's card on the right, with the two column titles gone and a 12 px gap between. The game rebuilds the
/// detail card on every new hover and places its task list by the description's height in its own font, so the card's
/// texts get the board's colors but keep their size. Tracking stays the game's: a click on a row.
/// </summary>
internal sealed class JournalSkin : GameAppSkin
{
	private readonly List<(GameObject Tracked, Image Dot)> dots = new List<(GameObject, Image)>();

	internal JournalSkin(HomeScreen home, Sprites sprites, AppClaims claims, Action<string> log)
		: base(home, sprites, claims, log)
	{
	}

	internal override string AppName => "Journal";

	protected override void Apply(GameObject app)
	{
		Transform? box = Find(app.transform, "Container");
		if (box == null)
		{
			return;
		}
		if (First(box))
		{
			Off(box, "Tasks/Title");
			Off(box, "Details/Title");
			Transform? scroll = Find(box, "Tasks/Scroll View");
			if (scroll != null)
			{
				Move(scroll.Cast<RectTransform>(), r =>
				{
					r.offsetMin = Vector2.zero;
					r.offsetMax = new Vector2(-6f * Unit, 0f);
				});
				Ghost(scroll.GetComponent<Graphic>());
				Transform? bar = Find(scroll, "Scrollbar Vertical");
				if (bar != null)
				{
					foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>(true))
					{
						Ghost(graphic);
					}
				}
				Spacing(Find<VerticalLayoutGroup>(scroll, "Viewport/Content"), 5f, Pad(0f, 0f, 0f, 0f));
				Recolor(Find<Graphic>(scroll, "Viewport/NoTasks"), C(Theme.PhoneMuted));
			}
			Transform? details = Find(box, "Details/Container");
			if (details != null)
			{
				Move(details.Cast<RectTransform>(), r =>
				{
					r.offsetMin = new Vector2(6f * Unit, 0f);
					r.offsetMax = Vector2.zero;
				});
				Ghost(details, "Background");
				Behind(details, "card", Card(14f), Color.white);
				Recolor(Find<Graphic>(details, "NoDetails"), C(Theme.PhoneMuted));
			}
		}
		Transform? rows = Find(box, "Tasks/Scroll View/Viewport/Content");
		if (rows != null)
		{
			for (int i = 0; i < rows.childCount; i++)
			{
				Transform row = rows.GetChild(i);
				if (row.name != "NoTasks" && First(row))
				{
					Quest(row);
				}
			}
		}
		Transform? card = Find(box, "Details/Container");
		if (card != null)
		{
			for (int i = 0; i < card.childCount; i++)
			{
				Transform panel = card.GetChild(i);
				if (panel.Find("OuterContainer") != null && First(panel))
				{
					Panel(panel);
				}
			}
		}
		for (int i = dots.Count - 1; i >= 0; i--)
		{
			(GameObject tracked, Image dot) = dots[i];
			if (tracked == null || dot == null)
			{
				dots.RemoveAt(i);
				continue;
			}
			dot.color = tracked.activeSelf ? C(Theme.PhoneAccent) : C(Theme.PhoneTrack);
		}
	}

	/// <summary>A quest row (GenericEntry) as the board's .ap-qrow: 30 px, #1b2128, the track dot (accent when tracked)
	/// where the quest's icon was, the title light, the expiry as a pill (red when the game says it's critical).</summary>
	private void Quest(Transform row)
	{
		Image? fill = row.GetComponent<Image>();
		Swap(fill, Solid(9f));
		// The game sets this gray on every track toggle: held at the board's row color.
		Recolor(fill, C(Theme.PhoneChip));
		Move(row.Cast<RectTransform>(), r => r.sizeDelta = new Vector2(r.sizeDelta.x, 30f * Unit));
		Off(row, "IconContainer");
		Transform? tracked = Find(row, "Tracked");
		if (tracked != null)
		{
			Image dot = Front(row, "dot", Solid(4f), C(Theme.PhoneTrack));
			RectTransform rect = dot.rectTransform;
			rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
			rect.anchoredPosition = new Vector2(14f * Unit, 0f);
			rect.sizeDelta = new Vector2(8f * Unit, 8f * Unit);
			dots.Add((tracked.gameObject, dot));
		}
		Text? title = Find<Text>(row, "Title");
		Label(title, 12f, C(Theme.PhoneText), align: TextAnchor.MiddleLeft);
		Move(title != null ? title.rectTransform : null, r => Across(r, 26f * Unit, 90f));
		Transform? expiry = Find(row, "Expiry");
		if (expiry != null)
		{
			Behind(expiry, "pill", Solid(7f), C(Theme.PhoneTrack));
			Image? critical = Find<Image>(expiry, "Critical");
			Swap(critical, Solid(7f));
			Label(expiry, "Time", 10f, Color.white);
		}
	}

	/// <summary>The picked quest's card (GenericDetailPanel, rebuilt on each new hover): board colors, the "Tasks" label
	/// in the board's caps, task rows #1b2128, "Show on map" as a step chip with green text.</summary>
	private void Panel(Transform panel)
	{
		Recolor(Find<Graphic>(panel, "Title"), C(Theme.PhoneText));
		Recolor(Find<Graphic>(panel, "Description"), C(Theme.PhoneMuted));
		Text? heading = Find<Text>(panel, "OuterContainer/Text (Legacy)");
		if (heading != null)
		{
			Label(heading, null, C(Theme.PhoneMuted));
			Set(heading, () => heading.text, v => heading.text = v, "TASKS");
		}
		Transform? entries = Find(panel, "OuterContainer/Entries");
		if (entries != null)
		{
			for (int i = 0; i < entries.childCount; i++)
			{
				Transform task = entries.GetChild(i);
				Image? fill = task.GetComponent<Image>();
				Swap(fill, Solid(7f));
				Recolor(fill, C(Theme.PhoneChip));
				Recolor(Find<Graphic>(task, "Title"), C(Theme.PhoneText));
				// The game grays a done task's state (50) or an open one's (150): light and muted.
				Recolor(Find<Graphic>(task, "State"), c => c.r < 0.3f ? C(Theme.PhoneText) : C(Theme.PhoneMuted));
			}
		}
		Ghost(panel, "OuterContainer/Contents/Seperator");
		Transform? map = Find(panel, "OuterContainer/Contents/ShowOnMap");
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
