using System.Collections.Generic;
using HylandHud;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.SpecialCustomers.UI;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The game's big popups as glass cards at the top center, under the police status: deal done (standard and special
/// customers), new customers, cartel influence and cartel status. The special customers' arrival card (shown during their
/// cutscene, with the HUD hidden) goes bottom left on the overlay. Every card shows exactly while the game's popup
/// plays and copies the game's labels each frame, so the game's counting animations come through as they are.
/// </summary>
internal sealed class Popups : Surface
{
	private abstract class Card
	{
		internal RectTransform? Rect;

		internal CanvasGroup? Group;

		internal Muffle? Muffle;

		internal readonly Ease Appear = new Ease(0f, 0.3f);

		internal float Height = 100f;

		internal float NextFind;

		internal virtual float Width => 420f;

		internal virtual bool Overlay => false;

		/// <summary>Finds the game's popup and hides it; false while it isn't there yet.</summary>
		internal abstract bool Find(Frame f);

		internal abstract bool Showing();

		internal abstract void Build(Frame f, RectTransform rect);

		internal abstract void Fill(Frame f);

		internal virtual void Forget()
		{
		}
	}

	private sealed class DealDone : Card
	{
		private DealCompletionPopup? popup;

		private const float Pad = 16f;

		/// <summary>Where the text after the check badge starts.</summary>
		private const float TextX = 62f;

		/// <summary>Where the satisfaction bar and the relationship chip start (after their 82-wide labels).</summary>
		private const float ValueX = Pad + 82f + 10f;

		private TextMeshProUGUI? name;

		private TextMeshProUGUI? pay;

		private RectTransform? check;

		private TextMeshProUGUI? satisfactionCaption;

		private Image? satisfactionTrack;

		private Image? satisfactionFill;

		private TextMeshProUGUI? satisfaction;

		private TextMeshProUGUI? relationshipCaption;

		private Image? relationshipChip;

		private TextMeshProUGUI? relationship;

		private readonly List<TextMeshProUGUI> bonuses = new List<TextMeshProUGUI>();

		internal override float Width => 360f;

		internal override bool Find(Frame f)
		{
			popup = Singleton<DealCompletionPopup>.Instance;
			if (popup == null || popup._canvas == null)
			{
				return false;
			}
			DealCompletionPopup p = popup;
			Muffle = new Muffle(p._canvas.gameObject, () => p.IsPlaying);
			return true;
		}

		internal override bool Showing()
		{
			return popup != null && popup.IsPlaying && popup.StandardDealAnimation != null && popup.StandardDealAnimation.gameObject.activeSelf;
		}

		internal override void Build(Frame f, RectTransform rect)
		{
			// The board's layout: a green check badge, "DEAL DONE" over the name, the pay level with them on the right;
			// bonuses under the name; then satisfaction as a bar and relationship as a chip.
			check = Glass.Solid("Check", rect, f.Sprites, 17, Glass.Tint(Glass.Green, 0.16f)).rectTransform;
			check.anchorMin = check.anchorMax = new Vector2(0f, 1f);
			check.pivot = new Vector2(0.5f, 0.5f);
			check.anchoredPosition = new Vector2(Pad + 17f, -31f);
			check.sizeDelta = new Vector2(34f, 34f);
			Tracker.Tick(f, check, Glass.Green);
			TextMeshProUGUI caps = Glass.Label("Caps", rect, f.Font, 11f, Glass.Muted);
			caps.text = "DEAL DONE";
			caps.characterSpacing = 8f;
			Glass.Pin(caps.rectTransform, new Vector2(0f, 1f), new Vector2(TextX, -15f), new Vector2(150f, 14f));
			name = Glass.Label("Name", rect, f.Font, 16f, Glass.Text);
			name.fontStyle = FontStyles.Bold;
			Glass.Pin(name.rectTransform, new Vector2(0f, 1f), new Vector2(TextX, -29f), new Vector2(Width - TextX - 130f, 20f));
			pay = Glass.Label("Pay", rect, f.Font, 22f, Glass.Green, TextAlignmentOptions.MidlineRight);
			pay.fontStyle = FontStyles.Bold;
			Glass.Pin(pay.rectTransform, new Vector2(1f, 1f), new Vector2(-Pad, -17f), new Vector2(120f, 28f));
			for (int i = 0; i < 4; i++)
			{
				TextMeshProUGUI bonus = Glass.Label("Bonus", rect, f.Font, 12f, Glass.Soft);
				Glass.Pin(bonus.rectTransform, new Vector2(0f, 1f), new Vector2(TextX, -(58f + 18f * i)), new Vector2(Width - TextX - Pad, 16f));
				bonuses.Add(bonus);
			}
			satisfactionCaption = Caption(f, rect, "Satisfaction");
			satisfactionTrack = Glass.Solid("Track", rect, f.Sprites, 3, Glass.Line);
			satisfactionFill = Glass.Solid("Fill", satisfactionTrack.transform, f.Sprites, 3, Glass.Green);
			Glass.Pin(satisfactionFill.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
			satisfaction = Glass.Label("Value", rect, f.Font, 12f, Glass.Soft, TextAlignmentOptions.MidlineRight);
			relationshipCaption = Caption(f, rect, "Relationship");
			relationshipChip = Glass.Solid("Chip", rect, f.Sprites, 9, Glass.Tint(Glass.Blue, 0.16f));
			relationship = Glass.Label("Value", relationshipChip.transform, f.Font, 11f, Glass.Blue, TextAlignmentOptions.Center);
			relationship.fontStyle = FontStyles.Bold;
			Glass.Fill(relationship.rectTransform);
		}

		private static TextMeshProUGUI Caption(Frame f, RectTransform rect, string text)
		{
			TextMeshProUGUI label = Glass.Label("Caption", rect, f.Font, 12f, Glass.Soft);
			label.text = text;
			return label;
		}

		internal override void Fill(Frame f)
		{
			DealCompletionPopup p = popup!;
			name!.text = Glass.Plain(p.Title.text).Replace("Deal completed for ", "");
			pay!.text = Glass.Plain(p.PaymentLabel.text);
			// The badge pops in with the card.
			float a = HylandHud.Motion.EaseOutCubic(Appear.Value);
			check!.localScale = Vector3.one * (0.6f + 0.4f * a);
			// The game counts "0 %" up to "87 %"; the bar follows its number and its color.
			string percent = Glass.Plain(p.SatisfactionValueLabel.text).Replace(" ", "");
			satisfaction!.text = percent;
			Color satColor = Opaque(p.SatisfactionValueLabel.color);
			float sat01 = float.TryParse(percent.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n) ? Mathf.Clamp01(n / 100f) : 0f;
			relationship!.text = Glass.Plain(p.RelationshipLabel.text);
			Color relColor = Opaque(p.RelationshipLabel.color);
			relationship.color = relColor;
			relationshipChip!.color = Glass.Tint(relColor, 0.16f);
			int shown = 0;
			for (int i = 0; i < bonuses.Count; i++)
			{
				bool on = p.BonusLabels != null && i < p.BonusLabels.Length && p.BonusLabels[i] != null && p.BonusLabels[i].gameObject.activeSelf;
				bonuses[i].gameObject.SetActive(on);
				if (on)
				{
					// The game's own rich text: a green "+$50" then what it's for.
					bonuses[i].text = p.BonusLabels[i].text;
					shown++;
				}
			}
			float satTop = 58f + (shown > 0 ? 18f * shown + 6f : 0f);
			float relTop = satTop + 24f;
			Glass.Pin(satisfactionCaption!.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -satTop), new Vector2(82f, 18f));
			float trackWidth = Width - ValueX - 10f - 40f - Pad;
			Glass.Pin(satisfactionTrack!.rectTransform, new Vector2(0f, 1f), new Vector2(ValueX, -(satTop + 6f)), new Vector2(trackWidth, 6f));
			satisfactionFill!.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, trackWidth * sat01), 6f);
			satisfactionFill.color = satColor;
			Glass.Pin(satisfaction.rectTransform, new Vector2(1f, 1f), new Vector2(-Pad, -satTop), new Vector2(40f, 18f));
			Glass.Pin(relationshipCaption!.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -relTop), new Vector2(82f, 18f));
			float chipWidth = Mathf.Ceil(relationship.preferredWidth) + 14f;
			Glass.Pin(relationshipChip.rectTransform, new Vector2(0f, 1f), new Vector2(ValueX, -relTop), new Vector2(chipWidth, 18f));
			Height = relTop + 18f + 14f;
		}

		internal override void Forget()
		{
			popup = null;
		}
	}

	private sealed class SpecialDeal : Card
	{
		private DealCompletionPopup? popup;

		private TextMeshProUGUI? title;

		private TextMeshProUGUI? pay;

		internal override bool Find(Frame f)
		{
			// The same popup as a standard deal; the deal-done card hides it.
			popup = Singleton<DealCompletionPopup>.Instance;
			return popup != null && popup._specialCustomerDealAnimation != null;
		}

		internal override bool Showing()
		{
			return popup != null && popup.IsPlaying && popup._specialCustomerDealAnimation.gameObject.activeSelf;
		}

		internal override void Build(Frame f, RectTransform rect)
		{
			Caps(f, rect, "SPECIAL DEAL DONE", Glass.Green, 14f);
			title = Line(f, rect, 16f, Glass.Text, 32f, Width - 170f, bold: true);
			pay = Glass.Label("Pay", rect, f.Font, 28f, Glass.Green, TextAlignmentOptions.MidlineRight);
			pay.fontStyle = FontStyles.Bold;
			Glass.Pin(pay.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -16f), new Vector2(170f, 40f));
			Height = 70f;
		}

		internal override void Fill(Frame f)
		{
			// "Deal completed for the <color>Group</color>": keep the group's color.
			title!.text = (popup!._specialCustomerDealTitle.text ?? "").Replace("Deal completed for the ", "");
			pay!.text = Glass.Plain(popup._specialCustomerDealPaymentLabel.text);
		}

		internal override void Forget()
		{
			popup = null;
		}
	}

	private sealed class NewCustomers : Card
	{
		private const int Faces = 5;

		private NewCustomerPopup? popup;

		private TextMeshProUGUI? title;

		private readonly List<(RectTransform rect, Image picture, TextMeshProUGUI name, TextMeshProUGUI initial)> faces = new List<(RectTransform, Image, TextMeshProUGUI, TextMeshProUGUI)>();

		internal override bool Find(Frame f)
		{
			popup = Singleton<NewCustomerPopup>.Instance;
			if (popup == null || popup.Canvas == null)
			{
				return false;
			}
			NewCustomerPopup p = popup;
			Muffle = new Muffle(p.Canvas.gameObject, () => p.IsPlaying);
			return true;
		}

		internal override bool Showing()
		{
			return popup != null && popup.IsPlaying && popup.Container != null && popup.Container.gameObject.activeSelf && popup.Group != null && popup.Group.alpha > 0.005f;
		}

		internal override void Build(Frame f, RectTransform rect)
		{
			title = Caps(f, rect, "NEW CUSTOMER", Glass.Blue, 14f);
			for (int i = 0; i < Faces; i++)
			{
				RectTransform slot = Ui.Rect("Face", rect);
				Glass.Pin(slot, new Vector2(0f, 1f), Vector2.zero, new Vector2(72f, 90f));
				Image avatar = Glass.Avatar("Avatar", slot, f.Sprites, 48f, out Image picture);
				Glass.Pin(avatar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(48f, 48f));
				TextMeshProUGUI initial = Glass.Label("Initial", avatar.transform, f.Font, 18f, Glass.Text, TextAlignmentOptions.Center);
				Glass.Fill(initial.rectTransform);
				TextMeshProUGUI label = Glass.Label("Name", slot, f.Font, 12f, Glass.Text, TextAlignmentOptions.Top);
				label.enableWordWrapping = true;
				Glass.Pin(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(72f, 34f));
				faces.Add((slot, picture, label, initial));
			}
			Height = 140f;
		}

		internal override void Fill(Frame f)
		{
			NewCustomerPopup p = popup!;
			title!.text = Glass.Plain(p.Title.text).ToUpperInvariant().Replace("!", "").Replace(" UNLOCKED", "");
			int count = 0;
			for (int i = 0; i < faces.Count; i++)
			{
				RectTransform? entry = p.Entries != null && i < p.Entries.Length ? p.Entries[i] : null;
				bool on = entry != null && entry.gameObject.activeSelf;
				faces[i].rect.gameObject.SetActive(on);
				if (!on)
				{
					continue;
				}
				count++;
				Image icon = entry!.Find("Mask/Icon")?.GetComponent<Image>();
				TextMeshProUGUI label = entry.Find("Name")?.GetComponent<TextMeshProUGUI>();
				Sprite sprite = icon != null ? icon.sprite : null;
				string full = label != null ? Glass.Plain(label.text) : "";
				faces[i].picture.sprite = sprite;
				faces[i].picture.enabled = sprite != null;
				faces[i].initial.text = sprite == null && full.Length > 0 ? full.Substring(0, 1) : "";
				faces[i].name.text = full;
			}
			// The faces centered as a row.
			float rowWidth = count * 72f + (count - 1) * 8f;
			float x = (Width - rowWidth) / 2f;
			for (int i = 0; i < faces.Count; i++)
			{
				if (faces[i].rect.gameObject.activeSelf)
				{
					faces[i].rect.anchoredPosition = new Vector2(x, -38f);
					x += 80f;
				}
			}
		}

		internal override void Forget()
		{
			popup = null;
		}
	}

	private sealed class CartelInfluence : Card
	{
		private CartelInfluenceChangePopup? popup;

		private TextMeshProUGUI? region;

		private TextMeshProUGUI? count;

		private Image? fill;

		internal override bool Find(Frame f)
		{
			popup = Object.FindObjectOfType<CartelInfluenceChangePopup>();
			if (popup == null)
			{
				return false;
			}
			Muffle = new Muffle(popup.gameObject);
			return true;
		}

		internal override bool Showing()
		{
			return popup != null && popup.Anim != null && popup.Anim.isPlaying;
		}

		internal override void Build(Frame f, RectTransform rect)
		{
			Caps(f, rect, "CARTEL INFLUENCE", Glass.Red, 14f);
			region = Line(f, rect, 16f, Glass.Text, 32f, Width - 140f, bold: true);
			count = Glass.Label("Count", rect, f.Font, 13f, Glass.Soft, TextAlignmentOptions.MidlineRight);
			Glass.Pin(count.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -34f), new Vector2(120f, 20f));
			Image track = Glass.Solid("Track", rect, f.Sprites, 3, Glass.Hex(0xFFFFFF, 0.1f));
			Glass.Pin(track.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -66f), new Vector2(Width - 36f, 6f));
			fill = Glass.Solid("Fill", track.transform, f.Sprites, 3, Glass.Red);
			Glass.Pin(fill.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
			Height = 88f;
		}

		internal override void Fill(Frame f)
		{
			CartelInfluenceChangePopup p = popup!;
			// "Benzies' Influence in Westville" → "Westville".
			string title = Glass.Plain(p.TitleLabel.text);
			int at = title.LastIndexOf(" in ", System.StringComparison.Ordinal);
			region!.text = at >= 0 ? title.Substring(at + 4) : title;
			count!.text = Glass.Plain(p.InfluenceCountLabel.text);
			float v = p.Slider != null ? Mathf.InverseLerp(p.Slider.minValue, p.Slider.maxValue, p.Slider.value) : 0f;
			fill!.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, (Width - 36f) * v), 6f);
		}

		internal override void Forget()
		{
			popup = null;
		}
	}

	private sealed class CartelStatus : Card
	{
		private CartelStatusChangePopup? popup;

		private TextMeshProUGUI? from;

		private TextMeshProUGUI? to;

		private Image? strike;

		internal override bool Find(Frame f)
		{
			popup = Object.FindObjectOfType<CartelStatusChangePopup>();
			if (popup == null)
			{
				return false;
			}
			Muffle = new Muffle(popup.gameObject);
			return true;
		}

		internal override bool Showing()
		{
			return popup != null && popup.Anim != null && popup.Anim.isPlaying;
		}

		internal override void Build(Frame f, RectTransform rect)
		{
			Caps(f, rect, "CARTEL STATUS", Glass.Muted, 14f);
			from = Glass.Label("From", rect, f.Font, 18f, Glass.Muted, TextAlignmentOptions.MidlineRight);
			from.fontStyle = FontStyles.Bold;
			Glass.Pin(from.rectTransform, new Vector2(0.5f, 1f), new Vector2(-24f, -34f), new Vector2(170f, 28f));
			from.rectTransform.pivot = new Vector2(1f, 1f);
			strike = Glass.Solid("Strike", from.transform, f.Sprites, 1, Glass.Muted);
			strike.rectTransform.anchorMin = strike.rectTransform.anchorMax = new Vector2(1f, 0.5f);
			strike.rectTransform.pivot = new Vector2(1f, 0.5f);
			TextMeshProUGUI arrow = Glass.Label("Arrow", rect, f.Font, 18f, Glass.Soft, TextAlignmentOptions.Center);
			arrow.text = "→";
			Glass.Pin(arrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(40f, 28f));
			to = Glass.Label("To", rect, f.Font, 18f, Glass.Text, TextAlignmentOptions.MidlineLeft);
			to.fontStyle = FontStyles.Bold;
			Glass.Pin(to.rectTransform, new Vector2(0.5f, 1f), new Vector2(24f, -34f), new Vector2(170f, 28f));
			to.rectTransform.pivot = new Vector2(0f, 1f);
			Height = 76f;
		}

		internal override void Fill(Frame f)
		{
			CartelStatusChangePopup p = popup!;
			from!.text = Glass.Plain(p.OldStatusLabel.text);
			from.color = Glass.Tint(Opaque(p.OldStatusLabel.color), 0.55f);
			to!.text = Glass.Plain(p.NewStatusLabel.text);
			to.color = Opaque(p.NewStatusLabel.color);
			strike!.color = from.color;
			strike.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(from.preferredWidth) + 4f, 2f);
		}

		internal override void Forget()
		{
			popup = null;
		}
	}

	private sealed class SpecialArrival : Card
	{
		private SpecialCustomersArrivalCutsceneInfoPopup? popup;

		private TextMeshProUGUI? wants;

		private TextMeshProUGUI? effects;

		private TextMeshProUGUI? quantity;

		private bool open;

		internal override float Width => 340f;

		internal override bool Overlay => true;

		internal override bool Find(Frame f)
		{
			popup = Object.FindObjectOfType<SpecialCustomersArrivalCutsceneInfoPopup>();
			if (popup == null || popup._anim == null)
			{
				return false;
			}
			Muffle = new Muffle(popup.gameObject);
			return true;
		}

		internal override bool Showing()
		{
			// Open from the moment its open clip plays until its close clip does (it stays up between the two).
			Animation anim = popup!._anim;
			string clip = anim.clip != null ? anim.clip.name : "";
			if (popup._openClip != null && clip == popup._openClip.name && anim.isPlaying)
			{
				open = true;
			}
			else if (popup._closeClip != null && clip == popup._closeClip.name)
			{
				open = false;
			}
			return open;
		}

		internal override void Build(Frame f, RectTransform rect)
		{
			Caps(f, rect, "SPECIAL CUSTOMERS", Glass.Yellow, 14f);
			Small(f, rect, "WANTS", 36f);
			wants = Line(f, rect, 14f, Glass.Text, 50f, Width - 36f, bold: true);
			Small(f, rect, "BONUS FOR", 74f);
			effects = Line(f, rect, 13f, Glass.Text, 88f, Width - 36f);
			effects.enableWordWrapping = true;
			quantity = Glass.Label("Quantity", rect, f.Font, 13f, Glass.Soft, TextAlignmentOptions.MidlineRight);
			Glass.Pin(quantity.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -12f), new Vector2(140f, 18f));
			Height = 118f;
		}

		internal override void Fill(Frame f)
		{
			SpecialCustomersArrivalCutsceneInfoPopup p = popup!;
			wants!.text = Join(p._drugTypeLabels);
			effects!.text = Join(p._bonusEffectLabels);
			quantity!.text = "buys " + Glass.Plain(p._buyQuantityLabel.text);
			Vector2 size = effects.GetPreferredValues(effects.text, Width - 36f, 0f);
			Height = 104f + Mathf.Max(18f, Mathf.Ceil(size.y));
		}

		internal override void Forget()
		{
			popup = null;
			open = false;
		}

		private static string Join(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<TextMeshProUGUI>? labels)
		{
			List<string> parts = new List<string>();
			if (labels != null)
			{
				for (int i = 0; i < labels.Length; i++)
				{
					if (labels[i] != null && labels[i].enabled && !string.IsNullOrEmpty(labels[i].text))
					{
						parts.Add(labels[i].text);
					}
				}
			}
			return parts.Count > 0 ? string.Join("  ·  ", parts) : "—";
		}
	}

	private readonly List<Card> cards = new List<Card>
	{
		new DealDone(),
		new SpecialDeal(),
		new NewCustomers(),
		new CartelInfluence(),
		new CartelStatus(),
		new SpecialArrival()
	};

	private readonly HashSet<Card> found = new HashSet<Card>();

	internal Popups()
		: base("popups")
	{
	}

	internal override bool On(Settings settings)
	{
		return settings.Popups.Value;
	}

	internal override void Update(Frame f)
	{
		foreach (Card card in cards)
		{
			if (!found.Contains(card))
			{
				if (Time.unscaledTime < card.NextFind)
				{
					continue;
				}
				card.NextFind = Time.unscaledTime + 2f;
				if (!card.Find(f))
				{
					continue;
				}
				found.Add(card);
			}
			card.Muffle?.Hold();
			bool showing = card.Showing();
			if (showing && card.Rect == null)
			{
				RectTransform parent = card.Overlay ? f.Overlay : f.Root;
				Image glass = Glass.Panel("Hyland popup", parent, f.Sprites, 14);
				card.Rect = glass.rectTransform;
				card.Group = Ui.Group(card.Rect.gameObject);
				card.Build(f, card.Rect);
			}
			if (card.Rect == null)
			{
				continue;
			}
			if (showing)
			{
				card.Fill(f);
			}
			card.Appear.Target = showing ? 1f : 0f;
			float a = card.Appear.Step(f.Dt);
			card.Rect.gameObject.SetActive(a > 0.001f);
			if (a <= 0.001f)
			{
				continue;
			}
			card.Group!.alpha = a;
			float ease = HylandHud.Motion.EaseOutCubic(a);
			card.Rect.localScale = Vector3.one * (0.94f + 0.06f * ease);
			if (card.Overlay)
			{
				Glass.Pin(card.Rect, new Vector2(0f, 0f), new Vector2(f.Layout.SpecialX - 30f * (1f - ease), ScreenLayout.SpecialBottom), new Vector2(card.Width, card.Height));
				continue;
			}
			Glass.Pin(card.Rect, new Vector2(0.5f, 1f), new Vector2(0f, -(f.CenterY - 14f * (1f - ease))), new Vector2(card.Width, card.Height));
			card.Rect.pivot = new Vector2(0.5f, 1f);
			f.CenterY += (card.Height + ScreenLayout.Gap) * a;
		}
	}

	internal override void Teardown()
	{
		List<Card> all = new List<Card>(cards);
		found.Clear();
		List<System.Action> steps = new List<System.Action>();
		foreach (Card card in all)
		{
			Muffle? m = card.Muffle;
			RectTransform? r = card.Rect;
			card.Muffle = null;
			card.Rect = null;
			card.Group = null;
			card.NextFind = 0f;
			card.Appear.Value = card.Appear.Target = 0f;
			card.Forget();
			steps.Add(() => m?.Release());
			steps.Add(() => Destroy(r));
		}
		Steps(steps.ToArray());
	}

	private static Color Opaque(Color c)
	{
		c.a = 1f;
		return c;
	}

	private static TextMeshProUGUI Caps(Frame f, RectTransform rect, string text, Color color, float top)
	{
		TextMeshProUGUI label = Glass.Label("Caps", rect, f.Font, 11f, color);
		label.text = text;
		label.characterSpacing = 10f;
		label.fontStyle = FontStyles.Bold;
		Glass.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -top), new Vector2(240f, 16f));
		return label;
	}

	private static void Small(Frame f, RectTransform rect, string text, float top)
	{
		TextMeshProUGUI label = Glass.Label("Small", rect, f.Font, 10f, Glass.Muted);
		label.text = text;
		label.characterSpacing = 8f;
		Glass.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -top), new Vector2(200f, 14f));
	}

	private static TextMeshProUGUI Line(Frame f, RectTransform rect, float size, Color color, float top, float width, bool bold = false)
	{
		TextMeshProUGUI label = Glass.Label("Line", rect, f.Font, size, color);
		if (bold)
		{
			label.fontStyle = FontStyles.Bold;
		}
		Glass.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -top), new Vector2(width, size + 8f));
		return label;
	}
}
