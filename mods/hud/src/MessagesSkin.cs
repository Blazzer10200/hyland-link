using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Il2CppScheduleOne;
using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// Messages, redesigned like the board (upright, 316 board px = the app's 655). The list: 66 px rows with the unread
/// dot, a 42 px face, the name with its category as an outlined chip, a one-line preview and a 1 px divider. A chat: the
/// board's header (round back button, the name, then a strip with the face, the relationship band, the standard and any
/// debt), dark bubbles (yours #2E7FB0, theirs #1b2128), dark response buttons and compose bar. Counter-offer and deal time
/// become bottom sheets; the deal windows become a 2×2 grid of cards. Every conversation keeps its own chat objects alive
/// (one per contact), so each is set up the first time it's seen, open or not. Bubble sizes stay the game's: it places
/// them once, by its own font, and a smaller font would leave gaps.
/// </summary>
internal sealed class MessagesSkin : GameAppSkin
{
	/// <summary>The board's chat header: 16 + 36 + 10 for the back row, then a 26 px strip and 10 below it.</summary>
	private const float HeaderRow = 62f;

	private const float ChatTop = HeaderRow + 26f + 10f;

	/// <summary>Where the strip's center sits, from the top of the chat.</summary>
	private const float StripY = HeaderRow + 13f;

	private readonly Dictionary<int, int> bubbleCounts = new Dictionary<int, int>();

	/// <summary>The preview text we last wrote per row, so it's cut again only when the game writes a new one.</summary>
	private readonly Dictionary<int, string> previews = new Dictionary<int, string>();

	private RectTransform? chatName;

	private RectTransform? chatFace;

	internal MessagesSkin(HomeScreen home, Sprites sprites, AppClaims claims, Action<string> log)
		: base(home, sprites, claims, log)
	{
	}

	internal override string AppName => "Messages";

	protected override void Apply(GameObject app)
	{
		Transform root = app.transform;
		Transform? container = Find(root, "Container");
		if (container == null)
		{
			return;
		}
		if (First(container))
		{
			Recolor(Find<Graphic>(container, "Background"), C(Theme.PhoneApp));
			Scrollbar(Find(container, "Home/Scroll View/Scrollbar Vertical"));
		}
		Transform? list = Find(container, "Home/Scroll View/Viewport/Content");
		if (list != null)
		{
			for (int i = 0; i < list.childCount; i++)
			{
				Transform entry = list.GetChild(i);
				if (First(entry))
				{
					Row(entry);
				}
				OneLine(Find<Text>(entry, "Preview"));
			}
		}
		Transform? topbar = Find(container, "DialogueScreen/Topbar");
		if (topbar != null && First(topbar))
		{
			Topbar(topbar, Find(container, "DialogueScreen/ConversationContainers"));
		}
		Transform? chats = Find(container, "DialogueScreen/ConversationContainers");
		if (chats != null)
		{
			for (int i = 0; i < chats.childCount; i++)
			{
				Conversation(chats.GetChild(i));
			}
		}
		Transform? counter = Find(container, "CounterofferInterface");
		if (counter != null && First(counter))
		{
			Counteroffer(counter);
		}
		Transform? window = Find(container, "DealWindowSelector");
		if (window != null && First(window))
		{
			DealWindow(window);
		}
	}

	/// <summary>The game places the chat's name and face again on every open (by the name's width): ours goes back on
	/// before the frame draws.</summary>
	protected override void EveryFrame(GameObject app)
	{
		if (chatName != null)
		{
			PlaceName(chatName);
		}
		if (chatFace != null)
		{
			PlaceFace(chatFace);
		}
		if (GameInput.GetButtonDown(GameInput.ButtonCode.PrimaryClick))
		{
			Probe();
		}
	}

	/// <summary>Temporary, while the row's ✕ doesn't hide it in game: logs what's under the cursor on each click.</summary>
	private void Probe()
	{
		try
		{
			EventSystem system = EventSystem.current;
			if (system == null)
			{
				log("Messages click: no EventSystem.");
				return;
			}
			Vector3 mouse = GameInput.MousePosition;
			PointerEventData data = new PointerEventData(system) { position = new Vector2(mouse.x, mouse.y) };
			Il2CppSystem.Collections.Generic.List<RaycastResult> hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
			system.RaycastAll(data, hits);
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hits.Count && i < 3; i++)
			{
				sb.Append("\n  hit ").Append(i).Append(':').Append(Diagnostics.Chain(hits[i].gameObject.transform, 3));
			}
			log($"Messages click at {data.position}: {hits.Count} hit(s).{sb}");
		}
		catch (Exception e)
		{
			log($"Messages click probe failed: {e.Message}");
		}
	}

	/// <summary>The board's one-line preview (.ap-preview). The game's is the last message as it is, line breaks and rich
	/// text and all ("This is what they got:" then one line per item, the cash last). Ours is that message flat, its lines
	/// joined with " · ", cut with "…" where the row ends. The game writes it again on each new message.</summary>
	private void OneLine(Text? preview)
	{
		if (preview == null)
		{
			return;
		}
		int id = preview.GetInstanceID();
		string text = preview.text ?? string.Empty;
		if (previews.TryGetValue(id, out string? mine) && mine == text)
		{
			return;
		}
		float room = preview.rectTransform.rect.width;
		if (room <= 0f)
		{
			return;
		}
		string flat = Regex.Replace(text, "<[^>]*>", string.Empty).Replace("\r", string.Empty).Replace(":\n", ": ").Replace("\n", " · ");
		string fit = Fit(preview, flat, room);
		previews[id] = fit;
		if (fit != text)
		{
			preview.text = fit;
		}
	}

	/// <summary>The longest start of <paramref name="value"/> that fits <paramref name="room"/> with "…" after it.</summary>
	private static string Fit(Text label, string value, float room)
	{
		TextGenerationSettings settings = label.GetGenerationSettings(Vector2.zero);
		TextGenerator generator = label.cachedTextGeneratorForLayout;
		float Width(string s) => generator.GetPreferredWidth(s, settings) / label.pixelsPerUnit;
		if (Width(value) <= room)
		{
			return value;
		}
		int low = 0, high = value.Length;
		while (low < high)
		{
			int mid = (low + high + 1) / 2;
			if (Width(value.Substring(0, mid).TrimEnd() + "…") <= room)
			{
				low = mid;
			}
			else
			{
				high = mid - 1;
			}
		}
		return value.Substring(0, low).TrimEnd() + "…";
	}

	/// <summary>The ✕'s hover as the board's round #222a33 chip (.ap-hide). The game tints only the cross; its clear hit
	/// area becomes the chip and takes the tint instead.</summary>
	private void HideChip(Transform hide)
	{
		Image? area = hide.GetComponent<Image>();
		Button? button = hide.GetComponent<Button>();
		if (area == null || button == null)
		{
			return;
		}
		Swap(area, Solid(13f));
		Recolor(area, Color.white);
		Set<Graphic>(button, () => button.targetGraphic, g => button.targetGraphic = g, area);
		Tints(button, b =>
		{
			b.normalColor = Color.clear;
			b.highlightedColor = C(0x222A33);
			b.pressedColor = C(0x2A313B);
			b.selectedColor = Color.clear;
			b.disabledColor = Color.clear;
			b.colorMultiplier = 1f;
			return b;
		});
	}

	/// <summary>The offer's timer (the game shows it while a customer waits on a deal, red to green as time runs out): the
	/// board's 2 px bar (.ap-expiry) from under the face to 16 px short of the edge, 4 px up. The game sets its value and
	/// color.</summary>
	private void Expiry(Transform? slider)
	{
		if (slider == null)
		{
			return;
		}
		Move(slider.Cast<RectTransform>(), r =>
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = new Vector2(1f, 0f);
			r.pivot = new Vector2(0.5f, 0f);
			r.offsetMin = new Vector2(33f * Unit, 4f * Unit);
			r.offsetMax = new Vector2(-16f * Unit, 6f * Unit);
		});
		Swap(Find<Image>(slider, "Fill Area/Fill"), Solid(1f));
	}

	private void Scrollbar(Transform? bar)
	{
		if (bar == null)
		{
			return;
		}
		foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>(true))
		{
			Ghost(graphic, hold: false);
		}
	}

	/// <summary>One conversation in the list (Entry_2): the board's 66 px row.</summary>
	private void Row(Transform entry)
	{
		Move(entry.Cast<RectTransform>(), r => r.sizeDelta = new Vector2(r.sizeDelta.x, 66f * Unit));
		// Clear at rest, the game's hover tint shows it: the board's #151a20 hover.
		Recolor(Find<Graphic>(entry, "Button"), C(0x151A20));
		Ghost(entry, "Seperator");
		Line(entry, top: false, C(0x1B2128));
		Image? dot = Find<Image>(entry, "UnreadDot");
		Recolor(dot, C(Theme.Friend));
		Move(dot != null ? dot.rectTransform : null, r =>
		{
			r.anchoredPosition = new Vector2(19.5f * Unit, 0f);
			r.sizeDelta = new Vector2(7f * Unit, 7f * Unit);
		});
		Image? face = Find<Image>(entry, "IconMask");
		Recolor(face, C(Theme.PhoneTrack));
		Move(face != null ? face.rectTransform : null, r =>
		{
			r.anchoredPosition = new Vector2(54f * Unit, 0f);
			r.sizeDelta = new Vector2(42f * Unit, 42f * Unit);
		});
		Text? name = Find<Text>(entry, "Name");
		Label(name, 15f, C(Theme.PhoneText), align: TextAnchor.MiddleLeft);
		Move(name != null ? name.rectTransform : null, r => Column(r, 11f, 20f));
		Text? preview = Find<Text>(entry, "Preview");
		Label(preview, 12f, C(Theme.PhoneMuted), align: TextAnchor.UpperLeft);
		if (preview != null)
		{
			// One line that never wraps: OneLine cuts it to the row with "…".
			Set(preview, () => preview.verticalOverflow, v => preview.verticalOverflow = v, VerticalWrapMode.Truncate);
			Set(preview, () => preview.horizontalOverflow, v => preview.horizontalOverflow = v, HorizontalWrapMode.Overflow);
			Move(preview.rectTransform, r => Column(r, 34f, 18f));
		}
		Category(entry, name);
		Expiry(Find(entry, "Slider"));
		Transform? hide = Find(entry, "Hide");
		if (hide != null)
		{
			HideChip(hide);
		}
		Move(hide != null ? hide.Cast<RectTransform>() : null, r =>
		{
			r.anchoredPosition = new Vector2(-19f * Unit, -19f * Unit);
			r.sizeDelta = new Vector2(26f * Unit, 26f * Unit);
		});
		Image? cross = hide != null ? Find<Image>(hide, "Image") : null;
		Recolor(cross, C(0x6B7684));
		Move(cross != null ? cross.rectTransform : null, r => r.sizeDelta = new Vector2(11f * Unit, 11f * Unit));
	}

	/// <summary>The text column of a row: from after the face to clear of the ✕, <paramref name="top"/> board px down.</summary>
	private static void Column(RectTransform rect, float top, float height)
	{
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = new Vector2(85f * Unit, -top * Unit);
		rect.sizeDelta = new Vector2(-(85f + 34f) * Unit, height * Unit);
	}

	/// <summary>The category letter as the board's outlined chip after the name: the game's filled square goes clear, a ring
	/// and the letter take its color. Placed by the name's width at our size (the game placed it by its own).</summary>
	private void Category(Transform entry, Text? name)
	{
		Image? fill = Find<Image>(entry, "Category");
		if (fill == null)
		{
			return;
		}
		Color color = fill.color;
		Ghost(fill);
		Image ring = Front(fill.transform, "ring", sprites.Ring(4f * Unit, new Rgba(color.r, color.g, color.b, 1f)), Color.white);
		Fill(ring.rectTransform);
		Label(Find<Text>(fill.transform, "Label"), 9f, new Color(color.r, color.g, color.b, 1f));
		float x = 85f * Unit + (name != null ? name.preferredWidth : 0f) + 6f * Unit + 7f * Unit;
		Move(fill.rectTransform, r =>
		{
			r.anchoredPosition = new Vector2(x, -21f * Unit);
			r.sizeDelta = new Vector2(14f * Unit, 14f * Unit);
		});
	}

	/// <summary>The chat's header: taller (the back row plus the strip), the conversation moved down under it.</summary>
	private void Topbar(Transform topbar, Transform? chats)
	{
		Move(topbar.Cast<RectTransform>(), r =>
		{
			r.sizeDelta = new Vector2(r.sizeDelta.x, ChatTop * Unit);
			r.anchoredPosition = new Vector2(r.anchoredPosition.x, -ChatTop * Unit / 2f);
		});
		Move(chats != null ? chats.Cast<RectTransform>() : null, r =>
		{
			r.sizeDelta = new Vector2(r.sizeDelta.x, -ChatTop * Unit);
			r.anchoredPosition = new Vector2(r.anchoredPosition.x, -ChatTop * Unit / 2f);
		});
		Recolor(Find<Graphic>(topbar, "Background"), C(Theme.PhoneApp));
		Line(topbar, top: false, C(0x1B2128));
		Transform? back = Find(topbar, "Return");
		if (back != null)
		{
			Move(back.Cast<RectTransform>(), r =>
			{
				r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
				r.pivot = new Vector2(0.5f, 0.5f);
				r.anchoredPosition = new Vector2(34f * Unit, -34f * Unit);
				r.sizeDelta = new Vector2(36f * Unit, 36f * Unit);
			});
			Behind(back, "chip", BackChip(), Color.white);
			Ghost(back, "Icon");
			Image arrow = Front(back, "arrow", sprites.Glyph(Art.Icon.Back), Color.white);
			arrow.rectTransform.sizeDelta = new Vector2(18f * Unit, 18f * Unit);
		}
		Text? name = Find<Text>(topbar, "Name");
		if (name != null)
		{
			Label(name, 20f, C(Theme.PhoneText), align: TextAnchor.MiddleLeft);
			Move(name.rectTransform, PlaceName);
			chatName = name.rectTransform;
			Relationship(name.transform);
		}
		Image? face = Find<Image>(topbar, "IconMask");
		if (face != null)
		{
			Recolor(face, C(Theme.PhoneTrack));
			Move(face.rectTransform, PlaceFace);
			chatFace = face.rectTransform;
		}
		Transform? debt = Find(topbar, "Debt");
		if (debt != null)
		{
			Move(debt.Cast<RectTransform>(), r =>
			{
				r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
				r.pivot = new Vector2(1f, 0.5f);
				r.anchoredPosition = new Vector2(-16f * Unit, -34f * Unit);
				r.sizeDelta = new Vector2(85f, 80f);
			});
			Recolor(Find<Graphic>(debt, "Container/Content/Background"), C(Theme.PhoneChip));
			Label(debt, "Container/DebtHeader", 10f, C(Theme.PhoneMuted));
			Label(debt, "Container/Content/Value", 13f);
		}
	}

	/// <summary>The name as the header's title, right of the back button (the game centers it by its width).</summary>
	private static void PlaceName(RectTransform rect)
	{
		Vector2 corner = new Vector2(0f, 1f);
		if (rect.anchorMin != corner || rect.anchorMax != corner)
		{
			rect.anchorMin = rect.anchorMax = corner;
			rect.pivot = new Vector2(0f, 0.5f);
			rect.sizeDelta = new Vector2(340f, 36f * Unit);
		}
		rect.anchoredPosition = new Vector2(62f * Unit, -34f * Unit);
	}

	/// <summary>The face at the left of the strip under the header.</summary>
	private static void PlaceFace(RectTransform rect)
	{
		Vector2 corner = new Vector2(0f, 1f);
		if (rect.anchorMin != corner || rect.anchorMax != corner)
		{
			rect.anchorMin = rect.anchorMax = corner;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(26f * Unit, 26f * Unit);
		}
		rect.anchoredPosition = new Vector2(29f * Unit, -StripY * Unit);
	}

	/// <summary>The relationship band and the standard, children of the name: moved into the strip under it. The band
	/// keeps the game's gradient and notch (the scrollbar's value places it), slimmed to the board's 5 px.</summary>
	private void Relationship(Transform name)
	{
		// From the name's top-left corner (62, 16 board px into the header) to the strip.
		float y = -(StripY - 16f) * Unit;
		Transform? relationship = Find(name, "Relationship");
		if (relationship != null)
		{
			Move(relationship.Cast<RectTransform>(), r =>
			{
				r.anchoredPosition = new Vector2((-12f + 65f) * Unit, y);
				r.sizeDelta = new Vector2(130f * Unit, 14f * Unit);
			});
			Ghost(relationship, "Background");
			Move(relationship, "Band", r => r.sizeDelta = new Vector2(130f * Unit, 5f * Unit));
			Image? notch = Find<Image>(relationship, "Band/Scrollbar/Sliding Area/Handle/Notch");
			Swap(notch, Solid(1.5f));
			Move(notch != null ? notch.rectTransform : null, r => r.sizeDelta = new Vector2(3f * Unit, 11f * Unit));
		}
		Transform? standards = Find(name, "Standards");
		if (standards != null)
		{
			Move(standards.Cast<RectTransform>(), r =>
			{
				r.anchoredPosition = new Vector2((-12f + 130f + 8f + 11f) * Unit, y);
				r.sizeDelta = new Vector2(22f * Unit, 22f * Unit);
			});
			Image? pill = Find<Image>(standards, "Background");
			Swap(pill, Solid(11f));
			Recolor(pill, C(Theme.PhoneChip));
			Move(standards, "Star", r => r.sizeDelta = new Vector2(13f * Unit, 13f * Unit));
		}
	}

	/// <summary>One conversation's chat (all exist at once, one per contact): its response area and compose bar once, then
	/// every bubble and response as the game adds them.</summary>
	private void Conversation(Transform chat)
	{
		if (First(chat))
		{
			Transform? responses = Find(chat, "Responses");
			// One set per contact, and the game never recolors these: set once, not held every pass.
			if (responses != null)
			{
				Recolor(Find<Graphic>(responses, "Background"), C(0x0C0F12), hold: false);
				Ghost(Find<Graphic>(responses, "Shade"), hold: false);
				Line(responses, top: true, C(0x1B2128));
			}
			Scrollbar(Find(chat, "ScrollContainer/ScrollRect/Scrollbar Vertical"));
			Transform? menu = Find(chat, "SenderInterface/Menu");
			if (menu != null)
			{
				Recolor(Find<Graphic>(menu, "Background"), C(0x0C0F12), hold: false);
				Ghost(Find<Graphic>(menu, "Background/Shade"), hold: false);
				Bar(Find(menu, "ComposeButton"));
				Bar(Find(menu, "CancelButton"));
				Label(Find<Graphic>(menu, "SendTitle"), null, C(Theme.PhoneText), hold: false);
			}
		}
		// Only when a list's count changes: the game adds and clears these, it doesn't recolor them.
		Changed(Find(chat, "ScrollContainer/ScrollRect/Viewport/Content"), response: false);
		Changed(Find(chat, "Responses"), response: true);
		Changed(Find(chat, "SenderInterface/Menu/Sendables"), response: true);
	}

	private void Changed(Transform? list, bool response)
	{
		if (list == null)
		{
			return;
		}
		int id = list.GetInstanceID();
		int count = list.childCount;
		if (!bubbleCounts.TryGetValue(id, out int seen) || seen != count)
		{
			bubbleCounts[id] = count;
			Bubbles(list, response);
		}
	}

	/// <summary>The compose and cancel bars: the board's .ap-resp, muted label.</summary>
	private void Bar(Transform? button)
	{
		if (button == null)
		{
			return;
		}
		Recolor(button.GetComponent<Graphic>(), C(Theme.PhoneChip), hold: false);
		Label(Find<Graphic>(button, "Text (Legacy)"), null, C(Theme.PhoneMuted), hold: false);
		Recolor(Find<Graphic>(button, "Image"), C(Theme.PhoneMuted), hold: false);
	}

	private void Bubbles(Transform parent, bool response)
	{
		for (int i = 0; i < parent.childCount; i++)
		{
			Transform bubble = parent.GetChild(i);
			if (bubble.name.StartsWith(AppLayouts.Prefix, StringComparison.Ordinal) || bubble.name == "Background" || bubble.name == "Shade")
			{
				continue;
			}
			if (First(bubble))
			{
				Bubble(bubble, response);
			}
		}
	}

	/// <summary>
	/// A bubble (MessageBubble): its color is its button's tint over a white image. Yours are disabled-state blue, theirs
	/// the game's #E1E1E1, responses interactable. The tails go (sized to nothing: the game turns them on and colors
	/// them, but never sizes them). Set once: the game colors a bubble only when it spawns it.
	/// </summary>
	private void Bubble(Transform bubble, bool response)
	{
		Transform? background = Find(bubble, "Background");
		Button? button = background != null ? background.GetComponent<Button>() : null;
		if (background == null || button == null)
		{
			return;
		}
		Move(background, "Triangle_Left", r => r.sizeDelta = Vector2.zero);
		Move(background, "Triangle_Right", r => r.sizeDelta = Vector2.zero);
		if (response || button.interactable)
		{
			Tints(button, b =>
			{
				b.normalColor = C(Theme.PhoneChip);
				b.highlightedColor = C(0x222831);
				b.pressedColor = C(0x2A313B);
				b.selectedColor = C(0x222831);
				b.disabledColor = C(Theme.PhoneChip);
				b.colorMultiplier = 1f;
				return b;
			});
		}
		else
		{
			Color was = button.colors.disabledColor;
			bool theirs = Mathf.Abs(was.r - was.b) < 0.05f && was.r > 0.8f;
			Color fill = theirs ? C(Theme.PhoneChip) : C(0x2E7FB0);
			Tints(button, b =>
			{
				b.normalColor = fill;
				b.disabledColor = fill;
				b.colorMultiplier = 1f;
				return b;
			});
		}
		Label(Find<Text>(background, "Content"), null, C(Theme.PhoneText), hold: false);
	}

	/// <summary>Counter-offer: the game's centered white card becomes the board's bottom sheet. Its rows stay the game's
	/// (AppsSkin darkens them); Send takes the accent.</summary>
	private void Counteroffer(Transform popup)
	{
		Recolor(Find<Graphic>(popup, "Background"), new Color(0f, 0f, 0f, 0.55f));
		Transform? card = Find(popup, "Content");
		if (card == null)
		{
			return;
		}
		Sheet(card.Cast<RectTransform>(), null);
		Ghost(card, "Background");
		Ghost(card, "Shade");
		Behind(card, "sheet", Solid(20f), C(0x151A20));
		Label(card, "Header/Title", 16f, C(Theme.PhoneText));
		Transform? send = Deep(card, "Confirm");
		if (send != null)
		{
			Recolor(send.GetComponent<Graphic>(), C(Theme.PhoneAccent));
			foreach (Text label in send.GetComponentsInChildren<Text>(true))
			{
				Label(label, null, C(0x0D1014));
			}
		}
	}

	/// <summary>Pins a popup card to the bottom edge, full width: the board's sheet. A null height keeps the card's own
	/// (Counter-offer sizes itself to its rows).</summary>
	private void Sheet(RectTransform card, float? height)
	{
		Move(card, r =>
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = new Vector2(1f, 0f);
			r.pivot = new Vector2(0.5f, 0f);
			r.anchoredPosition = Vector2.zero;
			r.sizeDelta = new Vector2(0f, height ?? r.sizeDelta.y);
		});
	}

	/// <summary>
	/// Select deal time: the clock face of four wedges becomes the board's sheet with a 2×2 grid of window cards (Morning,
	/// Afternoon / Night, Late night). Each wedge keeps its button and its mask; the mask and the wedge art just take a
	/// rounded card's shape. The clock hand goes.
	/// </summary>
	private void DealWindow(Transform popup)
	{
		Recolor(Find<Graphic>(popup, "Background"), new Color(0f, 0f, 0f, 0.55f));
		Transform? shade = Find(popup, "Shade");
		Transform? card = Find(popup, "Shade/Content");
		if (shade == null || card == null)
		{
			return;
		}
		Ghost(shade.GetComponent<Graphic>());
		Sheet(shade.Cast<RectTransform>(), 224f * Unit);
		Move(card.Cast<RectTransform>(), r =>
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = Vector2.one;
			r.offsetMin = Vector2.zero;
			r.offsetMax = Vector2.zero;
		});
		Ghost(card.GetComponent<Graphic>());
		Behind(card, "sheet", Solid(20f), C(0x151A20));
		Text? title = Find<Text>(card, "Title");
		Label(title, 16f, C(Theme.PhoneText), align: TextAnchor.MiddleLeft);
		Move(title != null ? title.rectTransform : null, r =>
		{
			r.anchoredPosition = new Vector2(0f, -28f * Unit);
			r.sizeDelta = new Vector2(-32f * Unit, 28f * Unit);
		});
		Transform? cancel = Find(card, "Cancel");
		if (cancel != null)
		{
			Move(cancel.Cast<RectTransform>(), r =>
			{
				r.anchoredPosition = new Vector2(-30f * Unit, -28f * Unit);
				r.sizeDelta = new Vector2(28f * Unit, 28f * Unit);
			});
			Behind(cancel, "x", Solid(14f), C(Theme.PhoneChip));
			Image? cross = Find<Image>(cancel, "Image");
			Recolor(cross, C(Theme.PhoneMuted));
			Move(cross != null ? cross.rectTransform : null, r =>
			{
				r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
				r.anchoredPosition = Vector2.zero;
				r.sizeDelta = new Vector2(11f * Unit, 11f * Unit);
			});
		}
		Text? subtitle = Find<Text>(card, "Subtitle");
		Label(subtitle, 12f, C(Theme.PhoneMuted), align: TextAnchor.UpperLeft);
		Move(subtitle != null ? subtitle.rectTransform : null, r =>
		{
			r.anchoredPosition = new Vector2(0f, -54f * Unit);
			r.sizeDelta = new Vector2(-32f * Unit, 16f * Unit);
		});
		Transform? wheel = Find(card, "Wheel");
		if (wheel != null)
		{
			Move(wheel.Cast<RectTransform>(), r =>
			{
				r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
				r.pivot = new Vector2(0.5f, 1f);
				r.anchoredPosition = new Vector2(0f, -72f * Unit);
				r.sizeDelta = new Vector2(284f * Unit, 108f * Unit);
			});
			for (int i = 0; i < wheel.childCount; i++)
			{
				GameObject part = wheel.GetChild(i).gameObject;
				if (part.name == "Line" || part.name == "Arm" || part.name == "Center")
				{
					Off(part);
				}
			}
			Window(wheel, "Morning", -1f, 1f);
			Window(wheel, "Afternoon", 1f, 1f);
			Window(wheel, "Night", -1f, -1f);
			Window(wheel, "LateNight", 1f, -1f);
		}
		Text? now = Find<Text>(card, "CurrentTime");
		Label(now, 11f, C(Theme.PhoneMuted), align: TextAnchor.MiddleCenter);
		Move(now != null ? now.rectTransform : null, r =>
		{
			r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
			r.pivot = new Vector2(0f, 1f);
			r.anchoredPosition = new Vector2(16f * Unit, -(72f + 108f + 10f) * Unit);
			r.sizeDelta = new Vector2(284f * Unit, 16f * Unit);
		});
	}

	/// <summary>One deal window as the board's card (.ap-win: 12 px padding, the name over its hours), in the grid cell at
	/// <paramref name="column"/>, <paramref name="row"/> (±1).</summary>
	private void Window(Transform wheel, string name, float column, float row)
	{
		Transform? window = Find(wheel, name);
		if (window == null)
		{
			return;
		}
		Move(window.Cast<RectTransform>(), r =>
		{
			r.anchoredPosition = new Vector2(column * (69f + 4f) * Unit, row * (25f + 4f) * Unit);
			r.sizeDelta = new Vector2(138f * Unit, 50f * Unit);
		});
		Swap(window.GetComponent<Image>(), Solid(12f));
		Image? face = Find<Image>(window, "Normal");
		Swap(face, Solid(12f));
		// Under the button's #C8C8C8 tint this shows as the board's #1b2128 (and a step lighter on hover).
		Recolor(face, C(0x222A33));
		Transform? ring = Find(window, "Selection");
		if (ring != null)
		{
			foreach (Graphic graphic in ring.GetComponentsInChildren<Graphic>(true))
			{
				Ghost(graphic, hold: false);
			}
		}
		Text? title = Find<Text>(window, "Title");
		if (title != null)
		{
			Label(title, 14f, C(Theme.PhoneText), align: TextAnchor.MiddleLeft);
			Move(title.rectTransform, r =>
			{
				r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
				r.pivot = new Vector2(0f, 1f);
				r.anchoredPosition = new Vector2(12f * Unit, -8f * Unit);
				r.sizeDelta = new Vector2(114f * Unit, 18f * Unit);
			});
			Text? hours = Find<Text>(title.transform, "Time");
			Label(hours, 11f, C(Theme.PhoneMuted), align: TextAnchor.MiddleLeft);
			Move(hours != null ? hours.rectTransform : null, r =>
			{
				r.anchoredPosition = new Vector2(0f, -25f * Unit);
				r.sizeDelta = new Vector2(0f, 14f * Unit);
			});
		}
	}
}
