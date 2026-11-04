using System.Collections.Generic;
using HylandHud;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The game's toasts (texts, app notices: <c>NotificationsManager.SendNotification</c>) as glass cards in the left column,
/// newest on top. Toasts with the same title and icon (one person texting three times) fold into one card with a count.
/// Each card lives exactly as long as the game's toast, so the game's durations stay in charge. Also the unread messages
/// badge, with the number of unread conversations.
/// </summary>
internal sealed class Toasts : Surface
{
	private sealed class Source
	{
		internal TextMeshProUGUI? Title;

		internal TextMeshProUGUI? Body;

		internal Image? Icon;
	}

	private sealed class Card
	{
		internal string Key = "";

		internal readonly HashSet<int> Sources = new HashSet<int>();

		internal int Count;

		internal RectTransform Rect = null!;

		internal CanvasGroup Group = null!;

		internal Image Picture = null!;

		internal TextMeshProUGUI Title = null!;

		internal TextMeshProUGUI Body = null!;

		internal Image CountBack = null!;

		internal TextMeshProUGUI CountText = null!;

		internal readonly Ease Appear = new Ease(0f, 0.22f);

		internal readonly Ease Y = new Ease(0f, 0.2f);

		internal float Bump;

		internal bool Placed;

		internal bool Gone;
	}

	private const float Height = 58f;

	private const int MaxCards = 4;

	private Muffle? toastMuffle;

	private Muffle? badgeMuffle;

	private RectTransform? badge;

	private CanvasGroup? badgeGroup;

	private Image? badgeCountBack;

	private TextMeshProUGUI? badgeCount;

	private readonly Ease badgeAppear = new Ease(0f, 0.2f);

	private int lastUnread;

	private float badgeBump;

	private readonly Dictionary<int, Source> sources = new Dictionary<int, Source>();

	private readonly List<Card> cards = new List<Card>();

	internal Toasts()
		: base("toasts")
	{
	}

	internal override bool On(Settings settings)
	{
		return settings.Toasts.Value;
	}

	internal override void Update(Frame f)
	{
		NotificationsManager manager = Singleton<NotificationsManager>.Instance;
		if (manager == null || manager.EntryContainer == null || f.Hud.UnreadMessagesPrompt == null)
		{
			return;
		}
		if (toastMuffle == null || !toastMuffle.Covers(manager.EntryContainer.gameObject) || badgeMuffle == null || !badgeMuffle.Covers(f.Hud.UnreadMessagesPrompt.gameObject))
		{
			Teardown();
			toastMuffle = new Muffle(manager.EntryContainer.gameObject);
			badgeMuffle = new Muffle(f.Hud.UnreadMessagesPrompt.gameObject);
		}
		toastMuffle.Hold();
		badgeMuffle.Hold();
		if (badge == null)
		{
			BuildBadge(f);
		}
		UpdateBadge(f);
		Read(f, manager.EntryContainer);
		Layout(f);
	}

	internal override void Teardown()
	{
		Muffle? t = toastMuffle;
		Muffle? b = badgeMuffle;
		RectTransform? badgeRect = badge;
		List<Card> old = new List<Card>(cards);
		toastMuffle = null;
		badgeMuffle = null;
		badge = null;
		cards.Clear();
		sources.Clear();
		badgeAppear.Value = badgeAppear.Target = 0f;
		lastUnread = 0;
		Steps(() => t?.Release(), () => b?.Release(), () => Destroy(badgeRect), () =>
		{
			foreach (Card card in old)
			{
				Destroy(card.Rect);
			}
		});
	}

	private void BuildBadge(Frame f)
	{
		Image glass = Glass.Panel("Hyland unread", f.Root, f.Sprites, ScreenLayout.BadgeSize / 2f);
		badge = glass.rectTransform;
		badgeGroup = Ui.Group(badge.gameObject);
		// A speech bubble: a rounded body and a small tail.
		Image bubble = Glass.Solid("Bubble", badge, f.Sprites, 4, Glass.Soft);
		bubble.rectTransform.sizeDelta = new Vector2(18f, 13f);
		bubble.rectTransform.anchoredPosition = new Vector2(0f, 1.5f);
		Image tail = Glass.Solid("Tail", badge, f.Sprites, 1, Glass.Soft);
		tail.rectTransform.sizeDelta = new Vector2(6f, 6f);
		tail.rectTransform.anchoredPosition = new Vector2(-4f, -5f);
		tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
		badgeCountBack = Glass.Solid("Count", badge, f.Sprites, 9, Glass.Red);
		badgeCountBack.rectTransform.anchorMin = badgeCountBack.rectTransform.anchorMax = new Vector2(1f, 1f);
		badgeCountBack.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
		badgeCount = Glass.Label("N", badgeCountBack.transform, f.Font, 11f, Glass.Ink, TextAlignmentOptions.Center);
		badgeCount.fontStyle = FontStyles.Bold;
		Glass.Fill(badgeCount.rectTransform);
	}

	private void UpdateBadge(Frame f)
	{
		MessagesApp app = PlayerSingleton<MessagesApp>.Instance;
		int unread = app != null && app.unreadConversations != null ? app.unreadConversations.Count : 0;
		// The game's own rule for showing its prompt; the count comes from the same list.
		bool show = f.Hud.UnreadMessagesPrompt.gameObject.activeSelf && unread > 0;
		if (unread > lastUnread)
		{
			badgeBump = 1f;
		}
		lastUnread = unread;
		badgeAppear.Target = show ? 1f : 0f;
		float a = badgeAppear.Step(f.Dt);
		badge!.gameObject.SetActive(a > 0.001f);
		if (a <= 0.001f)
		{
			return;
		}
		badgeBump = Mathf.Max(0f, badgeBump - f.Dt * 3f);
		badgeGroup!.alpha = a;
		Glass.Pin(badge, new Vector2(0f, 1f), new Vector2(f.Layout.ColumnX, -f.Layout.BadgeTop), new Vector2(ScreenLayout.BadgeSize, ScreenLayout.BadgeSize));
		badge.localScale = Vector3.one * (0.85f + 0.15f * HylandHud.Motion.EaseOutCubic(a));
		badgeCount!.text = unread > 99 ? "99+" : unread.ToString();
		float w = Mathf.Max(18f, Mathf.Ceil(badgeCount.preferredWidth) + 10f);
		badgeCountBack!.rectTransform.sizeDelta = new Vector2(w, 18f);
		badgeCountBack.rectTransform.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(badgeBump * Mathf.PI));
	}

	private void Read(Frame f, RectTransform container)
	{
		HashSet<int> alive = new HashSet<int>();
		for (int i = 0; i < container.childCount; i++)
		{
			Transform entry = container.GetChild(i);
			int id = entry.GetInstanceID();
			alive.Add(id);
			if (!sources.TryGetValue(id, out Source source))
			{
				source = new Source();
				Transform inner = entry.Find("Container");
				if (inner != null)
				{
					source.Title = inner.Find("Title")?.GetComponent<TextMeshProUGUI>();
					source.Body = inner.Find("Subtitle")?.GetComponent<TextMeshProUGUI>();
					source.Icon = inner.Find("AppIcon/Mask/Image")?.GetComponent<Image>();
				}
				sources[id] = source;
				// The game fills the texts right after it makes the entry, in the same call, so they're there now.
				Attach(f, id, source);
			}
		}
		List<int> dead = new List<int>();
		foreach (int id in sources.Keys)
		{
			if (!alive.Contains(id))
			{
				dead.Add(id);
			}
		}
		foreach (int id in dead)
		{
			sources.Remove(id);
			foreach (Card card in cards)
			{
				card.Sources.Remove(id);
			}
		}
		foreach (Card card in cards)
		{
			if (!card.Gone && card.Sources.Count == 0)
			{
				card.Gone = true;
			}
		}
	}

	private void Attach(Frame f, int id, Source source)
	{
		string title = source.Title != null ? source.Title.text : "";
		string body = source.Body != null ? source.Body.text : "";
		Sprite icon = source.Icon != null ? source.Icon.sprite : null;
		string key = Glass.Plain(title) + "|" + (icon != null ? icon.GetInstanceID() : 0);
		Card? card = null;
		foreach (Card c in cards)
		{
			if (!c.Gone && c.Key == key)
			{
				card = c;
				break;
			}
		}
		if (card == null)
		{
			card = Make(f, key);
			cards.Insert(0, card);
			card.Picture.sprite = icon;
			card.Picture.enabled = icon != null;
			card.Title.text = title;
		}
		else
		{
			// Folded: the newest text shows, the card moves to the top and bumps.
			cards.Remove(card);
			cards.Insert(0, card);
			card.Bump = 1f;
		}
		card.Sources.Add(id);
		card.Count++;
		card.Body.text = body;
		bool counted = card.Count > 1;
		card.CountBack.gameObject.SetActive(counted);
		if (counted)
		{
			card.CountText.text = "×" + card.Count;
			float w = Mathf.Ceil(card.CountText.preferredWidth) + 12f;
			Glass.Pin(card.CountBack.rectTransform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(w, 18f));
			Glass.Pin(card.Title.rectTransform, new Vector2(0f, 1f), new Vector2(58f, -9f), new Vector2(ScreenLayout.ColumnWidth - 58f - 18f - w, 20f));
		}
	}

	private Card Make(Frame f, string key)
	{
		Card card = new Card { Key = key };
		Image glass = Glass.Panel("Hyland toast", f.Root, f.Sprites);
		card.Rect = glass.rectTransform;
		card.Group = Ui.Group(card.Rect.gameObject);
		Image tile = Glass.Solid("Icon", card.Rect, f.Sprites, 8, Glass.Hex(0x2A313B));
		tile.gameObject.AddComponent<Mask>().showMaskGraphic = true;
		Glass.Pin(tile.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(34f, 34f));
		card.Picture = Ui.Image("Picture", tile.transform, null, Color.white);
		card.Picture.preserveAspect = true;
		Glass.Fill(card.Picture.rectTransform);
		float w = ScreenLayout.ColumnWidth - 58f - 12f;
		card.Title = Glass.Label("Title", card.Rect, f.Font, 14f, Glass.Text);
		card.Title.fontStyle = FontStyles.Bold;
		Glass.Pin(card.Title.rectTransform, new Vector2(0f, 1f), new Vector2(58f, -9f), new Vector2(w, 20f));
		card.Body = Glass.Label("Body", card.Rect, f.Font, 12f, Glass.Soft);
		Glass.Pin(card.Body.rectTransform, new Vector2(0f, 1f), new Vector2(58f, -30f), new Vector2(w, 18f));
		card.CountBack = Glass.Solid("Count", card.Rect, f.Sprites, 9, Glass.Tint(Glass.Blue, 0.18f));
		card.CountText = Glass.Label("N", card.CountBack.transform, f.Font, 11f, Glass.Blue, TextAlignmentOptions.Center);
		card.CountText.fontStyle = FontStyles.Bold;
		Glass.Fill(card.CountText.rectTransform);
		card.CountBack.gameObject.SetActive(false);
		return card;
	}

	private void Layout(Frame f)
	{
		float top = f.ColumnY;
		float y = 0f;
		int shown = 0;
		List<Card> remove = new List<Card>();
		foreach (Card card in cards)
		{
			bool visible = !card.Gone && shown < MaxCards;
			if (visible)
			{
				shown++;
			}
			card.Appear.Target = visible ? 1f : 0f;
			if (!card.Placed)
			{
				card.Y.Value = y;
				card.Placed = true;
			}
			card.Y.Target = y;
			float a = card.Appear.Step(f.Dt);
			float cy = card.Y.Step(f.Dt);
			card.Bump = Mathf.Max(0f, card.Bump - f.Dt * 4f);
			if (card.Gone && a <= 0.001f)
			{
				remove.Add(card);
				continue;
			}
			card.Group.alpha = a;
			// In from the left on arrival, out to the left on leave; a fold gives a small nudge right.
			float slide = HylandHud.Motion.EaseOutCubic(a);
			float nudge = 6f * Mathf.Sin(card.Bump * Mathf.PI);
			Glass.Pin(card.Rect, new Vector2(0f, 1f), new Vector2(f.Layout.ColumnX - 48f * (1f - slide) + nudge, -(top + cy)), new Vector2(ScreenLayout.ColumnWidth, Height));
			y += (Height + ScreenLayout.Gap) * a;
		}
		foreach (Card card in remove)
		{
			Destroy(card.Rect);
			cards.Remove(card);
		}
		f.ColumnY += y;
	}
}
