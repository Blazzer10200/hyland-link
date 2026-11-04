using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HylandHud;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The game's tracked quests (top left) as one glass panel: deals first (face, goods, place, pay, countdown), then cartel
/// deals and quests (current steps, progress). It reads exactly what the game puts in its own quest list, so the game's
/// rules for what shows stay in charge.
/// </summary>
internal sealed class Tracker : Surface
{
	private enum Kind
	{
		Deal,
		Cartel,
		Quest
	}

	private enum Timing
	{
		None,
		Upcoming,
		Open,
		Closing
	}

	private sealed class Data
	{
		internal string Key = "";

		internal Kind Kind;

		internal Quest Quest = null!;

		internal string Name = "";

		internal Sprite? Mugshot;

		internal string Chip = "";

		internal Timing Timing;

		internal int Minutes;

		internal string Line1 = "";

		internal string Line2 = "";

		internal string Pay = "";

		internal int Order;
	}

	private sealed class Row
	{
		internal string Key = "";

		internal Kind Kind;

		internal Quest Quest = null!;

		internal RectTransform Rect = null!;

		internal CanvasGroup Group = null!;

		internal Image Divider = null!;

		internal Image Ring = null!;

		internal Image Avatar = null!;

		internal Image Picture = null!;

		internal Image Tile = null!;

		internal TextMeshProUGUI Initial = null!;

		internal RectTransform Check = null!;

		internal TextMeshProUGUI Name = null!;

		internal Image ChipBack = null!;

		internal TextMeshProUGUI ChipText = null!;

		internal TextMeshProUGUI Line1 = null!;

		internal TextMeshProUGUI Line2 = null!;

		internal TextMeshProUGUI Pay = null!;

		internal Timing Timing;

		internal Color ChipColor = Glass.Soft;

		internal readonly Ease Appear = new Ease(0f, 0.25f);

		internal readonly Ease Y = new Ease(0f, 0.22f);

		internal readonly Ease Height = new Ease(0f, 0.22f);

		internal float TargetHeight;

		internal bool Gone;

		internal bool Completed;

		internal float GoneTime;

		internal bool Placed;
	}

	private const float Width = ScreenLayout.ColumnWidth;

	private const float DealHeight = 72f;

	private const float HeaderHeight = 22f;

	private static readonly Regex Quantity = new Regex("^(\\d+)x ", RegexOptions.Compiled);

	private Muffle? muffle;

	private RectTransform? panel;

	private TextMeshProUGUI? dealHeader;

	private TextMeshProUGUI? dealCount;

	private TextMeshProUGUI? questHeader;

	private TextMeshProUGUI? questCount;

	private TextMeshProUGUI? more;

	private readonly Ease panelHeight = new Ease(0f, 0.22f);

	private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();

	/// <summary>Per-frame scratch sets, kept so a frame allocates nothing.</summary>
	private readonly HashSet<string> present = new HashSet<string>();

	private readonly List<string> remove = new List<string>();

	private List<Data> data = new List<Data>();

	private float nextRead;

	internal Tracker()
		: base("tracker")
	{
	}

	internal override bool On(Settings settings)
	{
		return settings.Tracker.Value;
	}

	internal override void Update(Frame f)
	{
		HUD hud = f.Hud;
		if (hud.QuestEntryContainer == null)
		{
			return;
		}
		if (muffle == null || !muffle.Covers(hud.QuestEntryContainer.gameObject))
		{
			Teardown();
			muffle = new Muffle(hud.QuestEntryContainer.gameObject);
		}
		muffle.Hold();
		if (panel == null)
		{
			Build(f);
		}
		bool fresh = Time.unscaledTime >= nextRead;
		if (fresh)
		{
			nextRead = Time.unscaledTime + 0.25f;
			data = Read(hud);
		}
		Layout(f, fresh);
	}

	internal override void Teardown()
	{
		Muffle? m = muffle;
		RectTransform? p = panel;
		muffle = null;
		panel = null;
		rows.Clear();
		data.Clear();
		panelHeight.Value = panelHeight.Target = 0f;
		Steps(() => m?.Release(), () => Destroy(p));
	}

	private void Build(Frame f)
	{
		Image glass = Glass.Panel("Hyland tracker", f.Root, f.Sprites);
		panel = glass.rectTransform;
		Ui.Group(panel.gameObject);
		dealHeader = Header(f, "DEALS", out dealCount);
		questHeader = Header(f, "QUESTS", out questCount);
		more = Glass.Label("More", panel, f.Font, 12f, Glass.Muted);
	}

	private TextMeshProUGUI Header(Frame f, string text, out TextMeshProUGUI count)
	{
		TextMeshProUGUI label = Glass.Label(text, panel!, f.Font, 11f, Glass.Muted);
		label.text = text;
		label.characterSpacing = 8f;
		label.fontStyle = FontStyles.Bold;
		Image pill = Glass.Solid("Count", label.transform, f.Sprites, 8, Glass.Hex(0x222831));
		Glass.Pin(pill.rectTransform, new Vector2(0f, 0.5f), new Vector2(label.preferredWidth + 8f, 0f), new Vector2(24f, 16f));
		count = Glass.Label("N", pill.transform, f.Font, 11f, Glass.Soft, TextAlignmentOptions.Center);
		Glass.Fill(count.rectTransform);
		return label;
	}

	private static List<Data> Read(HUD hud)
	{
		List<Data> list = new List<Data>();
		Transform container = hud.QuestEntryContainer;
		for (int i = 0; i < container.childCount; i++)
		{
			Transform child = container.GetChild(i);
			if (!child.gameObject.activeSelf)
			{
				continue;
			}
			QuestHUDUI ui = child.GetComponent<QuestHUDUI>();
			Quest quest = ui != null ? ui.Quest : null;
			if (quest == null)
			{
				continue;
			}
			Data d = new Data { Quest = quest, Key = quest.GUID.ToString(), Order = i };
			Contract contract = quest.TryCast<Contract>();
			if (contract != null)
			{
				ReadDeal(contract, d);
			}
			else
			{
				ReadQuest(quest, d);
			}
			list.Add(d);
		}
		list.Sort((a, b) =>
		{
			int c = Rank(a).CompareTo(Rank(b));
			if (c != 0)
			{
				return c;
			}
			return a.Kind == Kind.Deal ? a.Minutes.CompareTo(b.Minutes) : a.Order.CompareTo(b.Order);
		});
		return list;
	}

	private static int Rank(Data d)
	{
		if (d.Kind != Kind.Deal)
		{
			return d.Kind == Kind.Cartel ? 3 : 4;
		}
		return d.Timing switch
		{
			Timing.Closing => 0,
			Timing.Open => 1,
			_ => 2
		};
	}

	private static void ReadDeal(Contract contract, Data d)
	{
		d.Kind = Kind.Deal;
		NPC npc = null;
		if (contract.Customer != null)
		{
			Customer customer = contract.Customer.GetComponent<Customer>();
			npc = customer != null ? customer.NPC : null;
		}
		d.Name = npc != null ? npc.FirstName : Glass.Plain(contract.GetQuestTitle()).Replace("Deal for ", "");
		d.Mugshot = npc != null ? npc.MugshotSprite : null;
		// The game's own entry title, "5x Granddaddy Purple, Skatepark": goods, then the place after the last comma.
		string entry = contract.Entries.Count > 0 ? Glass.Plain(contract.Entries[0].Title) : "";
		int comma = entry.LastIndexOf(", ", StringComparison.Ordinal);
		d.Line1 = Quantity.Replace(comma > 0 ? entry.Substring(0, comma) : entry, "$1× ");
		d.Line2 = comma > 0 ? entry.Substring(comma + 2) : "";
		d.Pay = "$" + Mathf.RoundToInt(contract.Payment).ToString("N0");
		if (!contract.Expires)
		{
			d.Timing = Timing.None;
			d.Minutes = int.MaxValue;
			return;
		}
		// The game's own thresholds (Contract.UpdateTiming): the window opens 6 h before expiry, closing under 2 h.
		int mins = contract.GetMinsUntilExpiry();
		d.Minutes = mins;
		if (mins - 360 > 0)
		{
			d.Timing = Timing.Upcoming;
			d.Chip = "in " + Span(mins - 360);
		}
		else if (mins < 120)
		{
			d.Timing = Timing.Closing;
			d.Chip = Span(Math.Max(0, mins)) + " left";
		}
		else
		{
			d.Timing = Timing.Open;
			d.Chip = "open · " + Span(mins) + " left";
		}
	}

	private static void ReadQuest(Quest quest, Data d)
	{
		d.Kind = quest.TryCast<Quest_DealForCartel>() != null ? Kind.Cartel : Kind.Quest;
		d.Name = Glass.Plain(quest.GetQuestTitle());
		int done = 0;
		int total = quest.Entries.Count;
		List<string> active = new List<string>();
		for (int i = 0; i < total; i++)
		{
			QuestEntry entry = quest.Entries[i];
			if (entry.State == EQuestState.Completed)
			{
				done++;
			}
			else if (entry.State == EQuestState.Active)
			{
				active.Add(Glass.Plain(entry.Title));
			}
		}
		d.Line1 = active.Count > 0 ? active[0] : "";
		d.Line2 = active.Count > 1 ? active[1] : "";
		string subtitle = Glass.Plain(quest.Subtitle).Trim('(', ')', ' ');
		if (subtitle.Length > 0)
		{
			// "2 days, 5 hours remaining" / "Expires in 3 hrs" → "2d 5h left" / "3h left".
			d.Chip = subtitle.Replace(" days", "d").Replace(" day", "d").Replace(" hours", "h").Replace(" hour", "h")
				.Replace(" hrs", "h").Replace(" min", "m").Replace(", ", " ").Replace(" remaining", " left")
				.Replace("Expires in ", "").Replace("Begins in ", "in ");
			if (!d.Chip.EndsWith(" left", StringComparison.Ordinal) && !d.Chip.StartsWith("in ", StringComparison.Ordinal))
			{
				d.Chip += " left";
			}
		}
		else if (total > 1)
		{
			d.Chip = Math.Min(done + 1, total) + " of " + total;
		}
		d.Minutes = int.MaxValue;
	}

	private static string Span(int minutes)
	{
		if (minutes < 60)
		{
			return minutes + "m";
		}
		int m = minutes % 60;
		return minutes / 60 + "h" + (m > 0 ? " " + m + "m" : "");
	}

	/// <param name="fresh">The game's list was just re-read: only then do the rows' texts change. Setting a TMP text copies
	/// the string into the game's runtime, so it isn't done every frame.</param>
	private void Layout(Frame f, bool fresh)
	{
		float dt = f.Dt;
		present.Clear();
		foreach (Data d in data)
		{
			present.Add(d.Key);
			bool made = false;
			if (!rows.TryGetValue(d.Key, out Row row) || row.Gone)
			{
				if (row != null)
				{
					Destroy(row.Rect);
				}
				row = MakeRow(f, d);
				rows[d.Key] = row;
				made = true;
			}
			if (fresh || made)
			{
				Fill(row, d);
			}
		}
		foreach (Row row in rows.Values)
		{
			if (!row.Gone && !present.Contains(row.Key))
			{
				row.Gone = true;
				row.GoneTime = Time.unscaledTime;
				row.Completed = row.Quest != null && row.Quest.State == EQuestState.Completed;
				row.Check.gameObject.SetActive(row.Completed);
			}
		}

		// Stack: deals header + rows, then quests header + rows, then "+N more"; rows past the cap or the column's
		// bottom fold away.
		int cap = Mathf.Clamp(f.Settings.TrackerRows.Value, 2, 10);
		float limit = f.Layout.ColumnBottom - f.ColumnY;
		float y = 10f;
		int shown = 0;
		int hidden = 0;
		int deals = 0;
		int quests = 0;
		bool questHeaderPlaced = false;
		bool dealHeaderPlaced = false;
		foreach (Data d in data)
		{
			if (d.Kind == Kind.Deal)
			{
				deals++;
			}
			else
			{
				quests++;
			}
		}
		foreach (Data d in data)
		{
			Row row = rows[d.Key];
			bool isDeal = d.Kind == Kind.Deal;
			float header = (isDeal ? !dealHeaderPlaced : !questHeaderPlaced) ? HeaderHeight + (isDeal || !dealHeaderPlaced ? 0f : 8f) : 0f;
			if (shown >= cap || y + header + row.TargetHeight + 30f > limit)
			{
				hidden++;
				row.Appear.Target = 0f;
				row.Rect.gameObject.SetActive(row.Appear.Value > 0.01f);
				continue;
			}
			if (header > 0f)
			{
				TextMeshProUGUI label = isDeal ? dealHeader! : questHeader!;
				Place(label.rectTransform, y + header - HeaderHeight);
				if (isDeal)
				{
					dealHeaderPlaced = true;
				}
				else
				{
					questHeaderPlaced = true;
				}
				y += header;
			}
			row.Rect.gameObject.SetActive(true);
			row.Appear.Target = 1f;
			row.Height.Target = row.TargetHeight;
			if (!row.Placed)
			{
				row.Y.Value = y;
				row.Height.Value = row.TargetHeight;
				row.Placed = true;
			}
			row.Y.Target = y;
			y += row.TargetHeight;
			shown++;
		}
		dealHeader!.gameObject.SetActive(dealHeaderPlaced);
		questHeader!.gameObject.SetActive(questHeaderPlaced);
		dealCount!.text = deals.ToString();
		questCount!.text = quests.ToString();
		more!.gameObject.SetActive(hidden > 0);
		if (hidden > 0)
		{
			more.text = "+" + hidden + " more";
			Place(more.rectTransform, y + 2f);
			y += 22f;
		}

		// Rows that left: a finished quest ticks for a moment first, then each folds its height away.
		remove.Clear();
		foreach (Row row in rows.Values)
		{
			if (row.Gone)
			{
				float age = Time.unscaledTime - row.GoneTime;
				float hold = row.Completed ? 0.7f : 0f;
				if (age > hold)
				{
					row.Appear.Target = 0f;
					row.Height.Target = 0f;
				}
				if (age > hold + 0.6f)
				{
					remove.Add(row.Key);
				}
				else
				{
					y += row.Height.Value;
				}
			}
			Animate(row, dt);
		}
		foreach (string key in remove)
		{
			Destroy(rows[key].Rect);
			rows.Remove(key);
		}

		bool any = rows.Count > 0;
		panel!.gameObject.SetActive(any || panelHeight.Value > 1f);
		panelHeight.Target = any ? y + 6f : 0f;
		float h = panelHeight.Step(dt);
		Glass.Pin(panel, new Vector2(0f, 1f), new Vector2(f.Layout.ColumnX, -f.ColumnY), new Vector2(Width, h));
		if (any)
		{
			f.ColumnY += h + ScreenLayout.Gap;
		}
	}

	private static void Place(RectTransform rect, float top)
	{
		Glass.Pin(rect, new Vector2(0f, 1f), new Vector2(12f, -top), new Vector2(Width - 24f, HeaderHeight));
	}

	private void Animate(Row row, float dt)
	{
		float a = row.Appear.Step(dt);
		float y = row.Y.Step(dt);
		float h = row.Height.Step(dt);
		row.Group.alpha = a;
		Glass.Pin(row.Rect, new Vector2(0f, 1f), new Vector2(-14f * (1f - a), -y), new Vector2(Width, Mathf.Max(0f, h)));
		Color target = row.Timing switch
		{
			Timing.Open => Glass.Green,
			Timing.Closing => Glass.Orange,
			_ => Glass.Soft
		};
		row.ChipColor = Color.Lerp(row.ChipColor, target, 1f - Mathf.Exp(-6f * dt));
		row.ChipText.color = row.ChipColor;
		row.ChipBack.color = Glass.Tint(row.ChipColor, 0.16f);
		bool closing = row.Timing == Timing.Closing && !row.Gone;
		row.Ring.gameObject.SetActive(closing);
		if (closing)
		{
			// A slow breath: the ring grows and fades, every 1.6 s.
			float t = Time.unscaledTime % 1.6f / 1.6f;
			float pulse = Mathf.Sin(t * Mathf.PI);
			row.Ring.color = Glass.Tint(Glass.Orange, 1f - 0.75f * pulse);
			float s = 40f + 6f * pulse;
			row.Ring.rectTransform.sizeDelta = new Vector2(s, s);
		}
		if (row.Gone && row.Completed)
		{
			float age = Time.unscaledTime - row.GoneTime;
			float pop = Mathf.Clamp01(age / 0.26f);
			row.Check.localScale = Vector3.one * (pop < 0.7f ? Mathf.Lerp(0.4f, 1.15f, pop / 0.7f) : Mathf.Lerp(1.15f, 1f, (pop - 0.7f) / 0.3f));
		}
	}

	private Row MakeRow(Frame f, Data d)
	{
		Row row = new Row { Key = d.Key, Kind = d.Kind, Quest = d.Quest };
		row.Rect = Ui.Rect("Row " + d.Name, panel!);
		row.Group = Ui.Group(row.Rect.gameObject);
		row.Divider = Glass.Solid("Divider", row.Rect, f.Sprites, 0.5, Glass.Line);
		Glass.Pin(row.Divider.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(Width, 1f));
		Vector2 avatarAt = new Vector2(12f, -8f);
		row.Ring = Glass.Solid("Ring", row.Rect, f.Sprites, 23, Glass.Orange);
		Glass.Pin(row.Ring.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(40f, 40f));
		row.Ring.rectTransform.anchorMin = row.Ring.rectTransform.anchorMax = new Vector2(0f, 1f);
		row.Ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		row.Ring.rectTransform.anchoredPosition = new Vector2(avatarAt.x + 17f, avatarAt.y - 17f);
		Image gap = Glass.Solid("Gap", row.Ring.transform, f.Sprites, 18, Glass.Hex(0x0F1317));
		gap.rectTransform.sizeDelta = new Vector2(36f, 36f);
		gap.rectTransform.anchorMin = gap.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		row.Ring.gameObject.SetActive(false);
		if (d.Kind == Kind.Deal)
		{
			row.Avatar = Glass.Avatar("Avatar", row.Rect, f.Sprites, 34f, out row.Picture);
			Glass.Pin(row.Avatar.rectTransform, new Vector2(0f, 1f), avatarAt, new Vector2(34f, 34f));
			row.Initial = Glass.Label("Initial", row.Avatar.transform, f.Font, 14f, Glass.Text, TextAlignmentOptions.Center);
			Glass.Fill(row.Initial.rectTransform);
		}
		else
		{
			Color tile = d.Kind == Kind.Cartel ? Glass.Hex(0x8C2F2F) : Glass.Yellow;
			row.Tile = Glass.Solid("Tile", row.Rect, f.Sprites, 8, tile);
			Glass.Pin(row.Tile.rectTransform, new Vector2(0f, 1f), avatarAt, new Vector2(34f, 34f));
			Image diamond = Glass.Solid("Diamond", row.Tile.transform, f.Sprites, 2, d.Kind == Kind.Cartel ? Glass.Text : Glass.Ink);
			diamond.rectTransform.sizeDelta = new Vector2(12f, 12f);
			diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
		}
		row.Check = Glass.Solid("Check", row.Rect, f.Sprites, 8, Glass.Hex(0x2E8F55)).rectTransform;
		Glass.Pin(row.Check, new Vector2(0f, 1f), avatarAt, new Vector2(34f, 34f));
		row.Check.pivot = new Vector2(0.5f, 0.5f);
		row.Check.anchoredPosition = new Vector2(avatarAt.x + 17f, avatarAt.y - 17f);
		Tick(f, row.Check, Color.white);
		row.Check.gameObject.SetActive(false);

		float textX = 56f;
		float textW = Width - textX - 12f;
		row.Name = Glass.Label("Name", row.Rect, f.Font, 14f, Glass.Text);
		row.Name.fontStyle = FontStyles.Bold;
		Glass.Pin(row.Name.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -7f), new Vector2(textW, 20f));
		row.ChipBack = Glass.Solid("Chip", row.Rect, f.Sprites, 9, Glass.Tint(Glass.Soft, 0.16f));
		row.ChipText = Glass.Label("Text", row.ChipBack.transform, f.Font, 11f, Glass.Soft, TextAlignmentOptions.Center);
		Glass.Fill(row.ChipText.rectTransform);
		row.Line1 = Glass.Label("Line1", row.Rect, f.Font, 12f, Glass.Soft);
		Glass.Pin(row.Line1.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -28f), new Vector2(textW, 17f));
		row.Line2 = Glass.Label("Line2", row.Rect, f.Font, 12f, Glass.Muted);
		Glass.Pin(row.Line2.rectTransform, new Vector2(0f, 1f), new Vector2(textX, -46f), new Vector2(textW - 52f, 17f));
		row.Pay = Glass.Label("Pay", row.Rect, f.Font, 12f, Glass.Green, TextAlignmentOptions.MidlineRight);
		Glass.Pin(row.Pay.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -46f), new Vector2(60f, 17f));
		row.Timing = d.Timing;
		row.ChipColor = d.Timing == Timing.Open ? Glass.Green : d.Timing == Timing.Closing ? Glass.Orange : Glass.Soft;
		return row;
	}

	/// <summary>A check mark from two rounded bars.</summary>
	internal static void Tick(Frame f, RectTransform parent, Color color)
	{
		Image shortBar = Glass.Solid("Tick1", parent, f.Sprites, 1.5, color);
		shortBar.rectTransform.sizeDelta = new Vector2(8f, 3f);
		shortBar.rectTransform.anchoredPosition = new Vector2(-4f, -1.5f);
		shortBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
		Image longBar = Glass.Solid("Tick2", parent, f.Sprites, 1.5, color);
		longBar.rectTransform.sizeDelta = new Vector2(14f, 3f);
		longBar.rectTransform.anchoredPosition = new Vector2(2.5f, 1f);
		longBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
	}

	private static void Fill(Row row, Data d)
	{
		row.Timing = d.Timing;
		row.Quest = d.Quest;
		row.Name.text = d.Name;
		if (row.Kind == Kind.Deal)
		{
			row.Picture.sprite = d.Mugshot;
			row.Picture.enabled = d.Mugshot != null;
			row.Initial.text = d.Mugshot == null && d.Name.Length > 0 ? d.Name.Substring(0, 1) : "";
		}
		row.Line1.text = d.Line1;
		row.Line2.text = d.Line2;
		row.Pay.text = d.Pay;
		row.Pay.gameObject.SetActive(d.Pay.Length > 0);
		row.Line2.gameObject.SetActive(d.Line2.Length > 0);
		bool chip = d.Chip.Length > 0;
		row.ChipBack.gameObject.SetActive(chip);
		if (chip)
		{
			row.ChipText.text = d.Chip;
			float w = Mathf.Ceil(row.ChipText.preferredWidth) + 14f;
			Glass.Pin(row.ChipBack.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(w, 18f));
			Glass.Pin(row.Name.rectTransform, new Vector2(0f, 1f), new Vector2(56f, -7f), new Vector2(Width - 56f - 12f - w - 8f, 20f));
		}
		bool twoLines = row.Kind == Kind.Deal || d.Line2.Length > 0;
		row.TargetHeight = twoLines ? DealHeight : 56f;
		if (row.Kind != Kind.Deal && d.Line1.Length == 0)
		{
			row.TargetHeight = 50f;
		}
	}
}
