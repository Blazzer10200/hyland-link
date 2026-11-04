using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.UI.Tooltips;
using UnityEngine;
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

	private Transform? conversations;

	/// <summary>The size we last gave each bubble: the game sizes them all again (by its own numbers) whenever a chat
	/// opens, and a mismatch here is how the next frame knows to lay the chat out again.</summary>
	private readonly Dictionary<int, Vector2> laid = new Dictionary<int, Vector2>();

	/// <summary>Per bubble list: the count, viewport height and bottom padding it was last laid out for.</summary>
	private readonly Dictionary<int, (int Count, float Viewport, float Pad)> stacks = new Dictionary<int, (int, float, float)>();

	/// <summary>Bubble roots whose original placement is recorded (once each, so teardown restores the game's layout).</summary>
	private readonly HashSet<int> recorded = new HashSet<int>();

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
		if (window != null && window.gameObject.activeInHierarchy)
		{
			WindowEdges();
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
		Strip();
		conversations ??= Find(app.transform, "Container/DialogueScreen/ConversationContainers");
		if (conversations == null)
		{
			return;
		}
		for (int i = 0; i < conversations.childCount; i++)
		{
			Transform chat = conversations.GetChild(i);
			if (chat.gameObject.activeSelf)
			{
				OpenChat(chat);
				return;
			}
		}
		openChat = 0;
	}

	/// <summary>The chat open last frame (0: none), so a chat that just opened starts at its newest message.</summary>
	private int openChat;

	/// <summary>
	/// The open chat laid out like the board, after the game: bubbles at 13 px with 8/11 px padding, at most 80% wide,
	/// 4 px apart within a run and 14 px where the sender changes (the game uses 5, 50 and 100 of its units, with a 75
	/// minimum height), sitting at the bottom when there are few, the last of each run with its tail corner. Response
	/// buttons as the board's 36 px rows 6 px apart. Only redone when the game has changed something.
	/// </summary>
	private void OpenChat(Transform chat)
	{
		Transform? responses = Find(chat, "Responses");
		bool responsesOn = responses != null && responses.gameObject.activeSelf;
		Transform? compose = Find(chat, "SenderInterface/Menu");
		bool composeOn = compose != null && compose.gameObject.activeInHierarchy;
		if (responses != null && responsesOn)
		{
			StackResponses(chat, responses);
		}
		Transform? content = Find(chat, "ScrollContainer/ScrollRect/Viewport/Content");
		if (content != null)
		{
			StackBubbles(chat, content, responsesOn || composeOn ? 12f : 26f);
		}
		if (openChat != chat.GetInstanceID())
		{
			openChat = chat.GetInstanceID();
			ScrollRect? scroll = Find<ScrollRect>(chat, "ScrollContainer/ScrollRect");
			if (scroll != null)
			{
				scroll.verticalNormalizedPosition = 0f;
			}
		}
	}

	private static List<Transform> BubblesIn(Transform list)
	{
		List<Transform> found = new List<Transform>();
		for (int i = 0; i < list.childCount; i++)
		{
			Transform child = list.GetChild(i);
			if (child.gameObject.activeSelf && child.GetComponent<MessageBubble>() != null)
			{
				found.Add(child);
			}
		}
		return found;
	}

	/// <summary>True when the list's bubbles aren't the sizes we gave them (new ones, or the game sized them again).</summary>
	private bool Dirty(List<Transform> bubbles)
	{
		foreach (Transform bubble in bubbles)
		{
			if (!laid.TryGetValue(bubble.GetInstanceID(), out Vector2 size) || bubble.Cast<RectTransform>().sizeDelta != size)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Records a bubble's placement once and styles it, before the first layout.</summary>
	private void Prepare(Transform bubble, bool response)
	{
		if (recorded.Add(bubble.GetInstanceID()))
		{
			Move(bubble.Cast<RectTransform>(), r => r.pivot = new Vector2(0.5f, 0.5f));
		}
		if (First(bubble))
		{
			Bubble(bubble, response);
		}
	}

	/// <summary>A bubble's size for its text: <paramref name="maxWidth"/> wide at most (or exactly, when
	/// <paramref name="fixedWidth"/>), 8/11 px padding, at least <paramref name="minHeight"/> tall (board px).</summary>
	private static Vector2 Measure(Transform bubble, float maxWidth, bool fixedWidth, float minHeight)
	{
		Text? text = Find<Text>(bubble, "Background/Content");
		if (text == null)
		{
			return bubble.Cast<RectTransform>().sizeDelta;
		}
		float padX = 22f * Unit, padY = 16f * Unit;
		TextGenerator generator = text.cachedTextGeneratorForLayout;
		float ppu = text.pixelsPerUnit;
		float width = maxWidth;
		if (!fixedWidth)
		{
			float natural = generator.GetPreferredWidth(text.text, text.GetGenerationSettings(Vector2.zero)) / ppu;
			width = Mathf.Min(Mathf.Ceil(natural) + padX + 1f, maxWidth);
		}
		float textHeight = generator.GetPreferredHeight(text.text, text.GetGenerationSettings(new Vector2(width - padX, 0f))) / ppu;
		return new Vector2(width, Mathf.Max(Mathf.Ceil(textHeight) + padY, minHeight * Unit));
	}

	private void StackBubbles(Transform chat, Transform content, float bottomPad)
	{
		RectTransform list = content.Cast<RectTransform>();
		RectTransform? viewport = list.parent != null ? list.parent.Cast<RectTransform>() : null;
		float viewportHeight = viewport != null ? viewport.rect.height : 0f;
		List<Transform> bubbles = BubblesIn(content);
		int id = content.GetInstanceID();
		stacks.TryGetValue(id, out var was);
		// New bubbles, or the view got shorter (the response buttons came up under it): keep the newest one in sight.
		bool grew = bubbles.Count != was.Count || viewportHeight < was.Viewport - 1f;
		if (!grew && Mathf.Approximately(was.Viewport, viewportHeight) && Mathf.Approximately(was.Pad, bottomPad) && !Dirty(bubbles))
		{
			return;
		}
		stacks[id] = (bubbles.Count, viewportHeight, bottomPad);
		float maxWidth = (list.rect.width - 28f * Unit) * 0.8f;
		Vector2[] sizes = new Vector2[bubbles.Count];
		int[] sides = new int[bubbles.Count];
		float total = 12f * Unit;
		for (int i = 0; i < bubbles.Count; i++)
		{
			Prepare(bubbles[i], response: false);
			RectTransform root = bubbles[i].Cast<RectTransform>();
			sides[i] = root.anchorMin.x < 0.25f ? -1 : root.anchorMin.x > 0.75f ? 1 : 0;
			sizes[i] = Measure(bubbles[i], maxWidth, fixedWidth: false, minHeight: 34f);
			total += (i == 0 ? 0f : (sides[i] == sides[i - 1] ? 4f : 14f) * Unit) + sizes[i].y;
		}
		total += bottomPad * Unit;
		// The board's flex-end: a short chat sits on the bottom edge, like a messenger.
		float y = 12f * Unit + Mathf.Max(0f, viewportHeight - total);
		for (int i = 0; i < bubbles.Count; i++)
		{
			if (i > 0)
			{
				y += (sides[i] == sides[i - 1] ? 4f : 14f) * Unit;
			}
			RectTransform root = bubbles[i].Cast<RectTransform>();
			Vector2 size = sizes[i];
			root.pivot = new Vector2(0.5f, 0.5f);
			root.sizeDelta = size;
			float x = sides[i] < 0 ? 14f * Unit + size.x / 2f : sides[i] > 0 ? -(14f * Unit + size.x / 2f) : 0f;
			root.anchoredPosition = new Vector2(x, -(y + size.y / 2f));
			laid[root.GetInstanceID()] = size;
			y += size.y;
			bool tail = i == bubbles.Count - 1 || sides[i + 1] != sides[i];
			Image? background = Find<Image>(bubbles[i], "Background");
			if (background != null)
			{
				Sprite shape = tail && sides[i] != 0 ? sprites.Tail(14f * Unit, 4f * Unit, sides[i] > 0) : Solid(14f);
				if (background.sprite != shape)
				{
					background.sprite = shape;
					background.type = Image.Type.Sliced;
				}
			}
		}
		list.sizeDelta = new Vector2(list.sizeDelta.x, Mathf.Max(total, viewportHeight));
		if (grew)
		{
			ScrollRect? scroll = Find<ScrollRect>(chat, "ScrollContainer/ScrollRect");
			if (scroll != null)
			{
				scroll.verticalNormalizedPosition = 0f;
			}
		}
	}

	/// <summary>The response buttons: the board's .ap-resp, full width less 14 px a side, 6 px apart, 10 px above and
	/// 24 px below (room for the home bar). The chat above shrinks to match, as the game does with its own height.</summary>
	private void StackResponses(Transform chat, Transform responses)
	{
		RectTransform area = responses.Cast<RectTransform>();
		List<Transform> buttons = BubblesIn(responses);
		int id = responses.GetInstanceID();
		stacks.TryGetValue(id, out var was);
		if (buttons.Count == was.Count && !Dirty(buttons))
		{
			return;
		}
		stacks[id] = (buttons.Count, 0f, 0f);
		float width = area.rect.width - 28f * Unit;
		float y = 10f * Unit;
		for (int i = 0; i < buttons.Count; i++)
		{
			Prepare(buttons[i], response: true);
			// The game brackets its actions ("[Counter-offer]"); the board doesn't. Only the shown text: the game's own
			// copy (MessageBubble.text) puts it back on every open, and this runs again then.
			Text? label = Find<Text>(buttons[i], "Background/Content");
			if (label != null && label.text.StartsWith("[", StringComparison.Ordinal) && label.text.EndsWith("]", StringComparison.Ordinal))
			{
				label.text = label.text.Substring(1, label.text.Length - 2);
			}
			RectTransform root = buttons[i].Cast<RectTransform>();
			Vector2 size = Measure(buttons[i], width, fixedWidth: true, minHeight: 36f);
			y += i == 0 ? 0f : 6f * Unit;
			root.pivot = new Vector2(0.5f, 0.5f);
			root.sizeDelta = size;
			root.anchoredPosition = new Vector2(0f, -(y + size.y / 2f));
			laid[root.GetInstanceID()] = size;
			y += size.y;
		}
		y += 24f * Unit;
		area.sizeDelta = new Vector2(area.sizeDelta.x, y);
		area.anchoredPosition = new Vector2(area.anchoredPosition.x, y / 2f);
		RectTransform? scroll = Find<RectTransform>(chat, "ScrollContainer");
		if (scroll != null)
		{
			scroll.offsetMin = new Vector2(scroll.offsetMin.x, y);
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
			// The board's pill at the strip's end: "Debt $360", the word muted, the amount red. The game stacks the two
			// in a column; they're placed in a row instead.
			Move(debt.Cast<RectTransform>(), r =>
			{
				r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
				r.pivot = new Vector2(1f, 0.5f);
				r.anchoredPosition = new Vector2(-16f * Unit, -StripY * Unit);
				r.sizeDelta = new Vector2(60f * Unit, 18f * Unit);
			});
			foreach (string path in new[] { "Container", "Container/Content" })
			{
				VerticalLayoutGroup? column = Find<VerticalLayoutGroup>(debt, path);
				if (column != null)
				{
					Set(column, () => column.enabled, v => column.enabled = v, false);
				}
			}
			Ghost(Find<Graphic>(debt, "Container/Content/Background"));
			Behind(debt, "pill", Solid(8f), C(Theme.PhoneChip));
			Stretch(Find(debt, "Container"));
			debtRect = debt.Cast<RectTransform>();
			debtWord = Find<Text>(debt, "Container/DebtHeader");
			debtValue = Find<Text>(debt, "Container/Content/Value");
			Label(debtWord, 10f, C(Theme.PhoneMuted), align: TextAnchor.MiddleLeft);
			Label(debtValue, 10f, C(0xED4B41), bold: true, align: TextAnchor.MiddleLeft);
			Move(debtWord != null ? debtWord.rectTransform : null, r => Inline(r, 7f));
			Move(Find<RectTransform>(debt, "Container/Content"), r =>
			{
				r.anchorMin = Vector2.zero;
				r.anchorMax = Vector2.one;
				r.offsetMin = r.offsetMax = Vector2.zero;
			});
			Move(debtValue != null ? debtValue.rectTransform : null, r => Inline(r, 7f + 26f));
		}
	}

	private RectTransform? debtRect;

	private Text? debtWord;

	private Text? debtValue;

	/// <summary>A label in a pill's row, <paramref name="x"/> board px from its left edge.</summary>
	private static void Inline(RectTransform rect, float x)
	{
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
		rect.pivot = new Vector2(0f, 0.5f);
		rect.anchoredPosition = new Vector2(x * Unit, 0f);
		rect.sizeDelta = new Vector2(80f * Unit, 14f * Unit);
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
			// The board's white notch (the game's is the band's dark track color, lost on the band).
			Recolor(notch, Color.white);
			Move(notch != null ? notch.rectTransform : null, r => r.sizeDelta = new Vector2(3f * Unit, 11f * Unit));
			relationshipRect = relationship.Cast<RectTransform>();
			relationshipTip = relationship.GetComponent<Tooltip>();
			relationshipLabel = NewLabel(relationship, "relationship", 11f, C(Theme.PhoneText), TextAnchor.MiddleLeft);
			RectTransform label = relationshipLabel.rectTransform;
			label.anchorMin = label.anchorMax = new Vector2(1f, 0.5f);
			label.pivot = new Vector2(0f, 0.5f);
			label.anchoredPosition = new Vector2(8f * Unit, 0f);
			label.sizeDelta = new Vector2(80f * Unit, 16f * Unit);
		}
		Transform? standards = Find(name, "Standards");
		if (standards != null)
		{
			// The board's pill (.ap-pill): "★ Moderate standards" on #1b2128, star and words in the quality's color.
			Move(standards.Cast<RectTransform>(), r => r.sizeDelta = new Vector2(120f * Unit, 18f * Unit));
			Image? pill = Find<Image>(standards, "Background");
			Swap(pill, Solid(8f));
			Recolor(pill, C(Theme.PhoneChip));
			Image? star = Find<Image>(standards, "Star");
			starImage = star;
			Move(star != null ? star.rectTransform : null, r =>
			{
				r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
				r.pivot = new Vector2(0f, 0.5f);
				r.anchoredPosition = new Vector2(7f * Unit, 0f);
				r.sizeDelta = new Vector2(10f * Unit, 10f * Unit);
			});
			standardsRect = standards.Cast<RectTransform>();
			standardsTip = standards.GetComponent<Tooltip>();
			standardsLabel = NewLabel(standards, "standards", 10f, Color.white, TextAnchor.MiddleLeft);
			RectTransform label = standardsLabel.rectTransform;
			label.anchorMin = label.anchorMax = new Vector2(0f, 0.5f);
			label.pivot = new Vector2(0f, 0.5f);
			label.anchoredPosition = new Vector2(21f * Unit, 0f);
			label.sizeDelta = new Vector2(100f * Unit, 14f * Unit);
		}
		stripY = y;
	}

	private RectTransform? relationshipRect;

	private Tooltip? relationshipTip;

	private Text? relationshipLabel;

	private RectTransform? standardsRect;

	private Tooltip? standardsTip;

	private Text? standardsLabel;

	private Image? starImage;

	private float stripY;

	/// <summary>The relationship word in the game's color for it (RelationshipCategory's).</summary>
	private static Color RelationshipColor(string word)
	{
		switch (word)
		{
			case "Hostile": return C(0xAD3F3F);
			case "Unfriendly": return C(0xE38837);
			case "Friendly": return C(0x3DB5F3);
			case "Loyal": return C(0x3FD33F);
			default: return C(0xD0D0D0);
		}
	}

	/// <summary>
	/// The strip under the chat's name, as the board lays it out from the face (16 + 26 + 8 px) to 16 px short of the
	/// edge: the band takes what the word and the standards pill leave. The game writes the word and the standard into
	/// its hover tooltips on every open; they're read from there, so they follow the game.
	/// </summary>
	private void Strip()
	{
		if (relationshipRect == null || relationshipLabel == null)
		{
			return;
		}
		string word = relationshipTip != null ? relationshipTip.text ?? string.Empty : string.Empty;
		if (relationshipLabel.text != word)
		{
			relationshipLabel.text = word;
			relationshipLabel.color = RelationshipColor(word);
		}
		float pill = 0f;
		if (standardsRect != null && standardsLabel != null && standardsRect.gameObject.activeSelf)
		{
			string standard = (standardsTip != null ? standardsTip.text ?? string.Empty : string.Empty).TrimEnd('.');
			if (standardsLabel.text != standard)
			{
				standardsLabel.text = standard;
			}
			if (starImage != null && standardsLabel.color != starImage.color)
			{
				standardsLabel.color = starImage.color;
			}
			pill = 7f + 10f + 4f + standardsLabel.preferredWidth / Unit + 7f;
		}
		if (debtRect != null && debtWord != null && debtValue != null && debtRect.gameObject.activeSelf)
		{
			float debtWidth = debtWord.preferredWidth / Unit;
			debtValue.rectTransform.anchoredPosition = new Vector2((7f + debtWidth + 4f) * Unit, 0f);
			pill = 7f + debtWidth + 4f + debtValue.preferredWidth / Unit + 7f;
			debtRect.sizeDelta = new Vector2(pill * Unit, 18f * Unit);
		}
		float wordWidth = relationshipLabel.preferredWidth / Unit;
		// Board px from the phone's left edge: the strip runs 50 → 300; the name (these rects' reference) starts at 62.
		float end = 300f - (pill > 0f ? pill + 8f : 0f) - wordWidth - 8f;
		float band = Mathf.Max(40f, end - 50f);
		relationshipRect.sizeDelta = new Vector2(band * Unit, relationshipRect.sizeDelta.y);
		relationshipRect.anchoredPosition = new Vector2((50f - 62f + band / 2f) * Unit, stripY);
		Transform? bandRect = relationshipRect.Find("Band");
		if (bandRect != null)
		{
			RectTransform r = bandRect.Cast<RectTransform>();
			r.sizeDelta = new Vector2(band * Unit, r.sizeDelta.y);
		}
		if (standardsRect != null && pill > 0f)
		{
			standardsRect.sizeDelta = new Vector2(pill * Unit, 18f * Unit);
			standardsRect.anchoredPosition = new Vector2((300f - 62f - pill / 2f) * Unit, stripY);
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

	/// <summary>The compose and cancel bars: the board's .ap-resp (36 px, 10 px corners, an edge), its 13 px muted label
	/// centered; the pencil 16 px at the right. Same centers as the game's, so the menu's slide still lines up.</summary>
	private void Bar(Transform? button)
	{
		if (button == null)
		{
			return;
		}
		// Stretched across (sizeDelta is then the inset) or not: 14 px from each edge either way.
		Move(button.Cast<RectTransform>(), r => r.sizeDelta = new Vector2(r.anchorMin.x != r.anchorMax.x ? -28f * Unit : 288f * Unit, 36f * Unit));
		Image? face = button.GetComponent<Image>();
		Swap(face, Solid(10f));
		Recolor(face, C(Theme.PhoneChip), hold: false);
		Image edge = Front(button, "edge", sprites.Ring(10f * Unit, Theme.PhoneStroke), Color.white);
		edge.raycastTarget = false;
		Fill(edge.rectTransform);
		Graphic? label = Find<Graphic>(button, "Text (Legacy)");
		Label(label, 13f, C(Theme.PhoneMuted), align: TextAnchor.MiddleCenter, hold: false);
		Move(label != null ? label.rectTransform : null, r =>
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = Vector2.one;
			r.offsetMin = r.offsetMax = Vector2.zero;
		});
		Graphic? pencil = Find<Graphic>(button, "Image");
		Recolor(pencil, C(Theme.PhoneMuted), hold: false);
		Move(pencil != null ? pencil.rectTransform : null, r =>
		{
			r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
			r.anchoredPosition = new Vector2(-20f * Unit, 0f);
			r.sizeDelta = new Vector2(16f * Unit, 16f * Unit);
		});
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
		// The board's shape and text box: 14 px corners (10 on responses), 13 px text inset 11 px across and 8 down.
		Swap(background.GetComponent<Image>(), Solid(response ? 10f : 14f));
		Text? content = Find<Text>(background, "Content");
		Move(content != null ? content.rectTransform : null, r =>
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = Vector2.one;
			r.pivot = new Vector2(0.5f, 0.5f);
			r.offsetMin = new Vector2(11f * Unit, 8f * Unit);
			r.offsetMax = new Vector2(-11f * Unit, -8f * Unit);
		});
		if (response)
		{
			Image edge = Front(background, "edge", sprites.Ring(10f * Unit, Theme.PhoneStroke), Color.white);
			edge.raycastTarget = false;
			Fill(edge.rectTransform);
		}
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
		Label(content, 13f, C(Theme.PhoneText), align: response ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft, hold: false);
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
		Image edge = Front(card, "sheet edge", sprites.Ring(20f * Unit, Theme.PhoneStroke), Color.white);
		edge.raycastTarget = false;
		Fill(edge.rectTransform);
		edge.transform.SetSiblingIndex(1);
		// The board's .ap-sheet-card: 14 px in at the top, 16 at the sides, room for the home bar under Send.
		Spacing(card.GetComponent<VerticalLayoutGroup>(), 6f, Pad(16f, 16f, 14f, 24f));
		Transform? header = Find(card, "Header");
		if (header != null)
		{
			Off(Find(header, "Space")?.gameObject);
			Label(Find<Text>(header, "Title"), 16f, C(Theme.PhoneText), bold: true, align: TextAnchor.MiddleLeft);
			Prefer(Find(header, "Title")?.gameObject, 240f, 28f);
			Transform? cancel = Find(header, "Cancel");
			if (cancel != null)
			{
				Prefer(cancel.gameObject, 28f, 28f);
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
		}
		foreach (string section in new[] { "Customer", "Dealer" })
		{
			Transform? group = Find(card, section);
			if (group == null)
			{
				continue;
			}
			Spacing(group.GetComponent<VerticalLayoutGroup>(), 5f, Pad(0f, 0f, 4f, 0f));
			// The board's .ap-label: 10 px muted capitals.
			Text? subtitle = Find<Text>(group, "Subtitle");
			if (subtitle != null)
			{
				Label(subtitle, 10f, C(Theme.PhoneMuted), align: TextAnchor.MiddleLeft);
				string caps = subtitle.text.ToUpperInvariant();
				Set(subtitle, () => subtitle.text, v => subtitle.text = v, caps);
				Prefer(subtitle.gameObject, null, 14f);
			}
		}
		foreach (string path in new[] { "Customer/Product", "Customer/Amount", "Dealer/AmountSelector" })
		{
			Image? row = Find<Image>(card, path);
			Swap(row, Solid(10f));
			Recolor(row, C(Theme.PhoneChip));
			Prefer(row != null ? row.gameObject : null, null, 40f);
		}
		Label(card, "Customer/Product/Title", 14f, C(Theme.PhoneText));
		// Quantity −/+ and the price steps: the board's .ap-step (30 px, 8 px corners, #222831 with an edge).
		foreach (Button step in card.GetComponentsInChildren<Button>(true))
		{
			string name = step.gameObject.name;
			bool quantity = name == "RemoveButton" || name == "AddButton";
			bool price = step.transform.parent != null && step.transform.parent.name.StartsWith("NegativeValues", StringComparison.Ordinal)
				|| step.transform.parent != null && step.transform.parent.name.StartsWith("PositiveValues", StringComparison.Ordinal);
			if (!quantity && !price)
			{
				continue;
			}
			Image? face = step.GetComponent<Image>();
			Swap(face, Solid(8f));
			Recolor(face, C(0x222831));
			float width = quantity ? 28f : step.gameObject.name.Length switch { <= 2 => 28f, 3 => 34f, _ => 40f };
			Prefer(step.gameObject, width, 28f);
			foreach (Text label in step.GetComponentsInChildren<Text>(true))
			{
				// These labels sit at half scale in the prefab: twice the size reads as the board's 12 px.
				float scale = Mathf.Max(0.1f, label.rectTransform.localScale.x);
				Label(label, 12f / scale, C(Theme.PhoneText));
				Text kept = label;
				Set(kept, () => kept.horizontalOverflow, v => kept.horizontalOverflow = v, HorizontalWrapMode.Overflow);
			}
		}
		Text? fair = Find<Text>(card, "Dealer/Fair price");
		Label(fair, 11f, C(Theme.PhoneMuted), bold: false, align: TextAnchor.MiddleCenter);
		Prefer(fair != null ? fair.gameObject : null, null, 16f);
		Transform? send = Deep(card, "Confirm");
		if (send != null)
		{
			// The board's .ap-primary: 40 px, 10 px corners, the accent, 12 px under the rows.
			Image? face = send.GetComponent<Image>();
			Swap(face, Solid(10f));
			Recolor(face, C(Theme.PhoneAccent));
			Prefer(send.gameObject, null, 40f);
			foreach (Text label in send.GetComponentsInChildren<Text>(true))
			{
				Label(label, 14f, C(0x0D1014), bold: true);
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
		Sheet(shade.Cast<RectTransform>(), 240f * Unit);
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
			// Two lines: "…any time within the window" wraps at the board's width too.
			r.sizeDelta = new Vector2(-32f * Unit, 32f * Unit);
		});
		Transform? wheel = Find(card, "Wheel");
		if (wheel != null)
		{
			Move(wheel.Cast<RectTransform>(), r =>
			{
				r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
				r.pivot = new Vector2(0.5f, 1f);
				r.anchoredPosition = new Vector2(0f, -WindowsTop * Unit);
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
		// The board's one line, "Current time 12:42 AM": the game's caption ends at the middle, its clock starts there.
		Text? now = Find<Text>(card, "CurrentTime");
		Label(now, 11f, C(Theme.PhoneMuted), align: TextAnchor.MiddleRight);
		if (now != null)
		{
			Set(now, () => now.text, v => now.text = v, "Current time");
		}
		Move(now != null ? now.rectTransform : null, r =>
		{
			r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
			r.pivot = new Vector2(0f, 1f);
			r.anchoredPosition = new Vector2(16f * Unit, -(WindowsTop + 108f + 10f) * Unit);
			r.sizeDelta = new Vector2(140f * Unit, 16f * Unit);
		});
		Text? clock = now != null ? Find<Text>(now.transform, "Title") : null;
		Label(clock, 11f, C(Theme.PhoneText), bold: false, align: TextAnchor.MiddleLeft);
		Move(clock != null ? clock.rectTransform : null, r =>
		{
			r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
			r.pivot = new Vector2(0f, 0.5f);
			r.anchoredPosition = new Vector2(4f * Unit, 0f);
			r.sizeDelta = new Vector2(140f * Unit, 16f * Unit);
		});
	}

	/// <summary>Where the window grid starts, under the title and the two-line subtitle (board px).</summary>
	private const float WindowsTop = 88f;

	/// <summary>Each window card's edge, by the game's window name (EDealWindow's names).</summary>
	private readonly Dictionary<string, Image> windowEdges = new Dictionary<string, Image>();

	/// <summary>The board marks the window the clock is in with the accent (the rest get the slate edge).</summary>
	private void WindowEdges()
	{
		if (windowEdges.Count == 0)
		{
			return;
		}
		TimeManager time = NetworkSingleton<TimeManager>.Instance;
		if (time == null)
		{
			return;
		}
		string current = DealWindowInfo.GetWindow(time.CurrentTime).ToString();
		foreach (KeyValuePair<string, Image> edge in windowEdges)
		{
			if (edge.Value != null)
			{
				edge.Value.color = edge.Key == current ? C(Theme.PhoneAccent) : C(Theme.PhoneStroke);
			}
		}
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
		Image edge = Front(window, "edge", sprites.Ring(12f * Unit, Theme.White), C(Theme.PhoneStroke));
		edge.raycastTarget = false;
		Fill(edge.rectTransform);
		windowEdges[name] = edge;
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
