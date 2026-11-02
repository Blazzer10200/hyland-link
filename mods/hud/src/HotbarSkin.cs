using System;
using System.Collections.Generic;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// Restyles the game's own hotbar in place: the slots stay the game's (drag and drop, item icons, quantities, controller
/// support all keep working), they just get a glass rail behind them, rounded backgrounds, tighter spacing, a lift on the
/// selected one, a sliding underline and an item name pill above it. Everything changed on a game object is recorded and
/// put back in <see cref="Dispose"/>.
/// </summary>
internal sealed class HotbarSkin : IDisposable
{
	private sealed class Slot
	{
		internal ItemSlotUI Ui;

		/// <summary>The slot UI's RectTransform, fetched once: every read through IL2CPP makes a new wrapper object.</summary>
		internal RectTransform Rect;

		/// <summary>The slot's game slot, cast once: a slot UI keeps its slot for life (PlayerInventory.SetupInventoryUI).</summary>
		internal HotbarSlot? Hotbar;

		internal Sprite Sprite;

		internal Image.Type Type;

		internal float PixelsPerUnitMultiplier;

		internal Color32 Normal;

		internal Color32 Highlight;

		internal Vector3 Scale;

		internal float BaseY;

		internal TextMeshProUGUI Index;

		internal Color IndexColor;

		internal float Lift;

		/// <summary>Center x in the slot row (SlotContainer space), from the last layout.</summary>
		internal float X;
	}

	private const float Padding = 12f;

	private const float PaddingY = 10f;

	private const float LiftUnits = 10f;

	private const float LiftScale = 0.07f;

	private const float SelectSeconds = 0.18f;

	private readonly HUD hud;

	private readonly PlayerInventory inventory;

	private readonly Settings settings;

	/// <summary>The game's slot row and the containers laid out in it, fetched once (see <see cref="Slot.Rect"/>).</summary>
	private readonly RectTransform slotRow;

	private readonly RectTransform cashContainer;

	private readonly RectTransform managementContainer;

	private readonly GameObject managementObject;

	private readonly List<Slot> slots = new List<Slot>();

	private readonly Slot? management;

	private readonly Slot cash;

	private readonly RectTransform? separator;

	private readonly Vector2 separatorPosition;

	private readonly Vector3 separatorScale;

	private readonly RectTransform back;

	private readonly RectTransform front;

	private readonly RectTransform rail;

	private readonly RectTransform railShadow;

	private readonly Image underline;

	private readonly RectTransform label;

	private readonly CanvasGroup labelGroup;

	private readonly TextMeshProUGUI labelText;

	private readonly MoneyPanel? money;

	private readonly CanvasGroup fade;

	private readonly bool fadeAdded;

	private readonly float fadeAlpha;

	private readonly bool labelWasActive;

	private readonly bool balanceWasActive;

	private Slot? selected;

	private string labelName = "";

	/// <summary>The slot and item the name pill was last read from, so the name (a new string per read) and its width are
	/// only fetched when either changes.</summary>
	private Slot? labelSlot;

	private IntPtr labelItem;

	private float labelWidth;

	private float labelIn;

	private readonly List<Slot> rowSlots = new List<Slot>();

	private float underlineX;

	private float underlineAlpha;

	private float idleFor;

	private bool disposed;

	internal HotbarSkin(HUD hud, PlayerInventory inventory, Sprites sprites, TMP_FontAsset font, Settings settings, Action<string> log)
	{
		this.hud = hud;
		this.inventory = inventory;
		this.settings = settings;
		// SlotUIs: the 8 hotbar slots, then the clipboard (management) slot, then the cash slot.
		Il2CppSystem.Collections.Generic.List<ItemSlotUI> uis = inventory.SlotUIs;
		if (uis == null || uis.Count < 10)
		{
			throw new InvalidOperationException("Hotbar has " + (uis?.Count ?? 0) + " slot UIs, expected 10.");
		}
		slotRow = hud.SlotContainer;
		cashContainer = hud.cashSlotContainer;
		managementContainer = hud.managementSlotContainer;
		managementObject = managementContainer.gameObject;
		// Recorded before anything changes, so a build that fails part way puts these back as they really were.
		labelWasActive = hud.selectedItemLabel.gameObject.activeSelf;
		balanceWasActive = hud.onlineBalanceContainer.gameObject.activeSelf;
		RectTransform row = slotRow;
		float slotWidth;
		try
		{
			Sprite slotSprite = sprites.Slot(14);
			for (int i = 0; i < 8; i++)
			{
				slots.Add(Restyle(uis[i], slotSprite));
			}
			management = Restyle(hud.managementSlotUI, slotSprite);
			cash = Restyle(hud.cashSlotUI.GetComponent<ItemSlotUI>(), slotSprite);
			Transform sep = slots[7].Rect.Find("Seperator");
			separator = sep != null ? sep.Cast<RectTransform>() : null;
			if (separator != null)
			{
				separatorPosition = separator.anchoredPosition;
				separatorScale = separator.localScale;
			}
			// Two layers inside the slot row, so they share its coordinates: the rail behind the slots, the underline,
			// name pill and money panel in front.
			back = Layer("Hyland rail (back)", row, slots[0].Rect);
			back.SetAsFirstSibling();
			front = Layer("Hyland rail (front)", row, slots[0].Rect);
			front.SetAsLastSibling();
			railShadow = Ui.Image("Shadow", back, sprites.Shadow(22, 16), Color.white, sliced: true).rectTransform;
			rail = Ui.Image("Glass", back, sprites.Glass(22), Color.white, sliced: true).rectTransform;
			slotWidth = slots[0].Rect.rect.width;
			float barWidth = slotWidth;
			underline = Ui.Image("Underline", front, sprites.Get("underline" + barWidth, () => Art.GlowBar(barWidth, Theme.Accent)), Color.white);
			underline.rectTransform.sizeDelta = new Vector2(barWidth + 16f, 19f);
			underline.color = new Color(1f, 1f, 1f, 0f);
			label = Ui.Rect("Item name", front);
			labelGroup = Ui.Group(label.gameObject);
			labelGroup.alpha = 0f;
			RectTransform labelGlass = Ui.Image("Glass", label, sprites.Glass(10), Color.white, sliced: true).rectTransform;
			labelGlass.anchorMin = Vector2.zero;
			labelGlass.anchorMax = Vector2.one;
			labelGlass.offsetMin = Vector2.zero;
			labelGlass.offsetMax = Vector2.zero;
			labelText = Ui.Text("Text", label, font, 15f, Color.white, 2f);
			labelText.rectTransform.sizeDelta = new Vector2(400f, 24f);
			if (settings.MoneyPanel.Value)
			{
				money = new MoneyPanel(front, sprites, font);
			}
			// The fade covers the whole row (slots and both layers). The game doesn't put a CanvasGroup here, but if a
			// later version does, it's reused and its alpha put back.
			fade = row.GetComponent<CanvasGroup>();
			fadeAdded = fade == null;
			if (fadeAdded)
			{
				fade = row.gameObject.AddComponent<CanvasGroup>();
			}
			fadeAlpha = fade.alpha;
			hud.selectedItemLabel.gameObject.SetActive(false);
			if (money != null)
			{
				hud.onlineBalanceContainer.gameObject.SetActive(false);
			}
			// One line per session about the real layout, so the first in-game run tells us what the prefab looks like.
			// Inside the try: it's the first touch of the cash and clipboard containers, and a throw here must still undo.
			log($"Hotbar: slot {slotWidth:F0}x{slots[0].Rect.rect.height:F0}, cash slot under '{cashContainer.parent?.name}', clipboard under '{managementContainer.parent?.name}', row '{row.name}' (scale {row.lossyScale.x:F3}).");
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	internal bool IsBoundTo(HUD instance)
	{
		return hud == instance && back != null;
	}

	private static RectTransform Layer(string name, RectTransform parent, RectTransform like)
	{
		RectTransform layer = Ui.Rect(name, parent);
		layer.anchorMin = like.anchorMin;
		layer.anchorMax = like.anchorMax;
		layer.anchoredPosition = Vector2.zero;
		return layer;
	}

	private static Slot Restyle(ItemSlotUI ui, Sprite sprite)
	{
		if (ui == null || ui.Background == null)
		{
			throw new InvalidOperationException("A hotbar slot has no background image.");
		}
		RectTransform rect = ui.Rect;
		Slot slot = new Slot
		{
			Ui = ui,
			Rect = rect,
			Sprite = ui.Background.sprite,
			Type = ui.Background.type,
			PixelsPerUnitMultiplier = ui.Background.pixelsPerUnitMultiplier,
			Normal = ui.normalColor,
			Highlight = ui.highlightColor,
			Scale = rect.localScale,
			BaseY = rect.anchoredPosition.y
		};
		Transform index = rect.Find("Background/Index");
		slot.Index = index != null ? index.GetComponent<TextMeshProUGUI>() : null;
		if (slot.Index != null)
		{
			slot.IndexColor = slot.Index.color;
		}
		ui.Background.sprite = sprite;
		ui.Background.type = Image.Type.Sliced;
		ui.Background.pixelsPerUnitMultiplier = 1f;
		// The baked sprite is a white stroke over a third-strength fill, so these give the concept's
		// rgba(255,255,255,.04)/.08 at rest and .13/.42 when selected.
		HotbarSlot? hotbarSlot = ui.assignedSlot?.TryCast<HotbarSlot>();
		slot.Hotbar = hotbarSlot;
		ui.SetNormalColor(new Color(1f, 1f, 1f, 0.12f));
		ui.SetHighlightColor(new Color(1f, 1f, 1f, 0.4f));
		ui.SetHighlighted(hotbarSlot != null && hotbarSlot.IsSelected);
		return slot;
	}

	/// <summary>The slots in the row right now (the clipboard only once unlocked), in one reused list.</summary>
	private List<Slot> Row()
	{
		rowSlots.Clear();
		rowSlots.AddRange(slots);
		if (management != null && managementObject.activeSelf)
		{
			rowSlots.Add(management);
		}
		rowSlots.Add(cash);
		return rowSlots;
	}

	internal void Update(float dt)
	{
		float gap = Mathf.Clamp(settings.SlotGap.Value, 0, 20);
		Layout(gap);
		List<Slot> row = Row();
		Slot? now = null;
		foreach (Slot slot in row)
		{
			if (slot.Hotbar != null && slot.Hotbar.IsSelected)
			{
				now = slot;
			}
		}
		bool changed = now != selected;
		selected = now;
		float height = 0f;
		foreach (Slot slot in row)
		{
			height = Mathf.Max(height, slot.Rect.rect.height);
			slot.Lift = Motion.Approach(slot.Lift, slot == selected ? 1f : 0f, SelectSeconds, dt);
			slot.Rect.localScale = slot.Scale * (1f + LiftScale * slot.Lift);
			Vector2 position = slot.Rect.anchoredPosition;
			slot.Rect.anchoredPosition = new Vector2(position.x, slot.BaseY + LiftUnits * slot.Lift);
			if (slot.Index != null)
			{
				slot.Index.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.4f, 1f, slot.Lift));
			}
		}
		if (separator != null)
		{
			// The separator hangs off slot 8, so undo that slot's lift to keep it still.
			float scale = 1f + LiftScale * slots[7].Lift;
			separator.localScale = separatorScale / scale;
			separator.anchoredPosition = new Vector2(gap / scale, (separatorPosition.y - LiftUnits * slots[7].Lift) / scale);
		}
		// The rail spans the row from the first slot to the cash slot (the layout starts at x = gap).
		float left = gap - Padding;
		float right = cash.X + cash.Rect.rect.width / 2f + Padding;
		float bottom = -height / 2f - PaddingY;
		float top = height / 2f + PaddingY;
		Ui.Place(rail, new Vector2((left + right) / 2f, 0f), new Vector2(right - left, top - bottom));
		Ui.Place(railShadow, new Vector2((left + right) / 2f, -6f), new Vector2(right - left + 32f, top - bottom + 32f));
		// Underline and name pill follow the selected slot.
		float targetX = selected?.X ?? underlineX;
		underlineX = changed && underlineAlpha < 0.05f ? targetX : Motion.Approach(underlineX, targetX, SelectSeconds, dt);
		underlineAlpha = Motion.Approach(underlineAlpha, selected != null ? 1f : 0f, SelectSeconds, dt);
		underline.rectTransform.localPosition = new Vector3(underlineX, bottom + 7.5f, 0f);
		underline.color = new Color(1f, 1f, 1f, underlineAlpha);
		ItemInstance? item = selected?.Hotbar?.ItemInstance;
		IntPtr itemPointer = item?.Pointer ?? IntPtr.Zero;
		if (selected != labelSlot || itemPointer != labelItem)
		{
			labelSlot = selected;
			labelItem = itemPointer;
			string name = selected == null ? "" : (item?.Name ?? "Empty");
			if (name != labelName)
			{
				labelName = name;
				labelText.text = name;
				labelWidth = labelText.preferredWidth;
				labelIn = 0f;
			}
		}
		labelIn = Mathf.Min(1f, labelIn + dt / 0.22f);
		float rise = (1f - Motion.EaseOutCubic(labelIn)) * 6f;
		label.sizeDelta = new Vector2(labelWidth + 24f, 26f);
		label.localPosition = new Vector3(underlineX, top + 21f - rise, 0f);
		labelGroup.alpha = labelName.Length == 0 ? 0f : Motion.EaseOutCubic(labelIn);
		money?.Update(new Vector2(right + 12f, bottom), dt);
		UpdateIdle(changed, dt);
	}

	/// <summary>The game's RepositionUI with our gap: 8 slots, a double gap with the separator, then the clipboard (when
	/// unlocked) and the cash slot, centered. Runs every frame, so it also wins after the game re-lays the row.</summary>
	private void Layout(float gap)
	{
		float x = 0f;
		foreach (Slot slot in slots)
		{
			float w = slot.Rect.rect.width;
			slot.X = x + w / 2f + gap;
			slot.Rect.anchoredPosition = new Vector2(slot.X, slot.Rect.anchoredPosition.y);
			x += w + gap;
		}
		x += gap;
		if (management != null && managementObject.activeSelf)
		{
			float w = managementContainer.sizeDelta.x;
			management.X = x + w / 2f + gap;
			managementContainer.anchoredPosition = new Vector2(management.X, 0f);
			x += w + gap;
		}
		float cw = cashContainer.sizeDelta.x;
		cash.X = x + cw / 2f + gap;
		cashContainer.anchoredPosition = new Vector2(cash.X, 0f);
		x += cw + gap;
		slotRow.anchoredPosition = new Vector2(-x / 2f, slotRow.anchoredPosition.y);
	}

	private void UpdateIdle(bool selectionChanged, float dt)
	{
		// Hotbar use only, the same on every device: picking a slot (number keys, scroll, bumpers all change the selection),
		// using the item in hand (mouse buttons, triggers), the d-pad and an open inventory. Walking and looking around
		// aren't, so the bar fades while you just move.
		bool input = selectionChanged || inventory.AttachedScreen != null;
		Mouse mouse = Mouse.current;
		if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame || mouse.scroll.ReadValue().y != 0f))
		{
			input = true;
		}
		Gamepad gamepad = Gamepad.current;
		if (gamepad != null && (gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame
			|| gamepad.leftTrigger.wasPressedThisFrame || gamepad.rightTrigger.wasPressedThisFrame || gamepad.dpad.up.wasPressedThisFrame
			|| gamepad.dpad.down.wasPressedThisFrame || gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame))
		{
			input = true;
		}
		idleFor = input ? 0f : idleFor + dt;
		bool idle = settings.IdleFade.Value && idleFor >= Mathf.Clamp(settings.IdleSeconds.Value, 1f, 30f);
		float target = idle ? Mathf.Clamp(settings.IdleOpacity.Value, 0.1f, 1f) : 1f;
		// Concept: eases out over 0.6 s, snaps back in 0.12 s.
		fade.alpha = Motion.Approach(fade.alpha, target, idle ? 0.6f : 0.12f, dt);
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		disposed = true;
		// Every step runs even if one throws (a half-destroyed scene), so one bad slot can't leave the rest restyled.
		Exception? failure = null;
		List<Slot> all = new List<Slot>(slots);
		if (management != null)
		{
			all.Add(management);
		}
		if (cash != null)
		{
			all.Add(cash);
		}
		foreach (Slot slot in all)
		{
			if (slot.Ui == null)
			{
				continue;
			}
			Ui.Step(ref failure, () =>
			{
				HotbarSlot? hotbarSlot = slot.Hotbar;
				slot.Ui.Background.sprite = slot.Sprite;
				slot.Ui.Background.type = slot.Type;
				slot.Ui.Background.pixelsPerUnitMultiplier = slot.PixelsPerUnitMultiplier;
				slot.Ui.SetNormalColor(slot.Normal);
				slot.Ui.SetHighlightColor(slot.Highlight);
				slot.Ui.SetHighlighted(hotbarSlot != null && hotbarSlot.IsSelected);
				slot.Rect.localScale = slot.Scale;
				slot.Rect.anchoredPosition = new Vector2(slot.Rect.anchoredPosition.x, slot.BaseY);
				if (slot.Index != null)
				{
					slot.Index.color = slot.IndexColor;
				}
			});
		}
		if (separator != null)
		{
			Ui.Step(ref failure, () =>
			{
				separator.localScale = separatorScale;
				separator.anchoredPosition = separatorPosition;
			});
		}
		if (fade != null)
		{
			Ui.Step(ref failure, () =>
			{
				if (fadeAdded)
				{
					// Immediately: a settings change rebuilds the skin in this same frame, and a merely queued Destroy would
					// still be found (and kept) by the new skin's GetComponent, then throw on the next frame.
					UnityEngine.Object.DestroyImmediate(fade);
				}
				else
				{
					fade.alpha = fadeAlpha;
				}
			});
		}
		if (hud != null)
		{
			Ui.Step(ref failure, () =>
			{
				if (hud.selectedItemLabel != null)
				{
					hud.selectedItemLabel.gameObject.SetActive(labelWasActive);
				}
			});
			Ui.Step(ref failure, () =>
			{
				if (hud.onlineBalanceContainer != null)
				{
					hud.onlineBalanceContainer.gameObject.SetActive(balanceWasActive);
				}
			});
		}
		if (back != null)
		{
			Ui.Step(ref failure, () => UnityEngine.Object.Destroy(back.gameObject));
		}
		if (front != null)
		{
			Ui.Step(ref failure, () => UnityEngine.Object.Destroy(front.gameObject));
		}
		// The game's own layout (spacing 20) comes back with its own method.
		if (inventory != null)
		{
			Ui.Step(ref failure, () => inventory.RepositionUI());
		}
		Ui.Rethrow(failure);
	}
}
