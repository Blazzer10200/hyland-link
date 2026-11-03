using System;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// App 8 on the phone: the HUD's switches and the rain controls, so nobody has to edit MelonPreferences.cfg. It's built
/// the way the game builds its own apps (<c>App&lt;T&gt;</c>, <c>HomeScreen.GenerateAppIcon</c>), without being one: an
/// icon from the game's own icon prefab (so 8 opens it and a controller can select it), a screen inside the phone's
/// AppsCanvas upright like Messages, and the same open, close, Escape and "click the phone's button" rules. Everything is
/// removed again in <see cref="Dispose"/>. Stays up whether or not the HUD is on, so the HUD can be switched back on here.
/// </summary>
internal sealed class HylandApp : IDisposable
{
	/// <summary>The icon's object name, which <see cref="PhoneSkin"/> knows it by.</summary>
	internal const string IconName = "Hyland";

	internal const string RootName = "Hyland app";

	/// <summary>Canvas units per board pixel (the board's phone screen is 316 wide, the game's 655).</summary>
	private const float Unit = 655f / 316f;

	private const float Pad = 16f * Unit;

	private sealed class Toggle
	{
		internal MelonPreferences_Entry<bool> Entry = null!;

		internal Image Track = null!;

		internal RectTransform Knob = null!;

		internal float Position;
	}

	private sealed class Range
	{
		internal Slider Slider = null!;

		internal Text Value = null!;

		internal Func<float> Read = null!;

		internal Func<float, string> Format = null!;
	}

	private readonly Phone phone;

	private readonly HomeScreen home;

	private readonly AppsCanvas apps;

	private readonly Sprites sprites;

	private readonly Settings settings;

	private readonly Font font;

	private readonly Button iconButton;

	private readonly UISelectable iconSelectable;

	private readonly RectTransform root;

	private readonly CanvasGroup group;

	private readonly RectTransform content;

	private readonly CanvasGroup contentGroup;

	private readonly List<Toggle> toggles = new List<Toggle>();

	private readonly List<Range> ranges = new List<Range>();

	private readonly List<(RectTransform Bar, Image Image, float Height)> meter = new List<(RectTransform, Image, float)>();

	private readonly GameInput.ExitDelegate exitListener;

	private readonly Il2CppSystem.Action closeListener;

	private bool isOpen;

	private bool closing;

	private float animatedAt = -1f;

	private Vector3 origin;

	private bool phoneWasOpen;

	private float saveAt = -1f;

	private bool disposed;

	internal HylandApp(Phone phone, HomeScreen home, AppsCanvas apps, Sprites sprites, Settings settings, Action<string> log)
	{
		this.phone = phone;
		this.home = home;
		this.apps = apps;
		this.sprites = sprites;
		this.settings = settings;
		font = home.timeText.font;
		exitListener = new Action<ExitAction>(OnExit);
		closeListener = new Action(OnCloseApps);

		// The icon, exactly as HomeScreen.GenerateAppIcon makes one: last in the grid, so it's key 8 after the game's seven.
		GameObject icon = UnityEngine.Object.Instantiate(home.appIconPrefab, home.appIconContainer);
		icon.name = IconName;
		iconButton = icon.GetComponent<Button>();
		iconSelectable = icon.GetComponent<UISelectable>();
		icon.transform.Find("Mask/Image").GetComponent<Image>().sprite = sprites.Glyph(Art.Icon.AppHyland);
		icon.transform.Find("Label").GetComponent<Text>().text = "Hyland";
		icon.transform.Find("Notifications").gameObject.SetActive(false);
		iconButton.onClick.AddListener((UnityAction)new Action(() => SetOpen(!isOpen)));
		home.appIcons.Add(iconButton);
		home.uiPanel.AddSelectable(iconSelectable);

		// The screen: like Messages, a 655×1201 rect turned 90° inside the sideways AppsCanvas.
		root = Ui.Rect(RootName, apps.canvas.transform);
		root.sizeDelta = new Vector2(655f, 1201f);
		root.localRotation = Quaternion.Euler(0f, 0f, 90f);
		group = root.gameObject.AddComponent<CanvasGroup>();
		Image background = Ui.Image("Background", root, null!, Ui.Color(Theme.PhoneApp));
		background.raycastTarget = true;
		Stretch(background.rectTransform);
		try
		{
			float headerHeight = BuildHeader();
			content = BuildScroll(headerHeight, out contentGroup);
			float y = BuildRows();
			content.sizeDelta = new Vector2(0f, y + 24f * Unit);
			root.gameObject.SetActive(false);

			GameInput.RegisterExitListener(exitListener, 1);
			phone.closeApps = Il2CppSystem.Delegate.Combine(phone.closeApps, closeListener).Cast<Il2CppSystem.Action>();
			log($"Phone: Hyland app is icon {home.appIcons.Count} ({toggles.Count} switches, {ranges.Count} sliders).");
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	internal bool IsBoundTo(HomeScreen instance)
	{
		return home == instance && iconButton != null && root != null;
	}

	private float BuildHeader()
	{
		float top = 16f * Unit, size = 36f * Unit;
		Image back = Ui.Image("Back", root, sprites.Get("backButton", () => Art.Glass(18 * Unit, Theme.PhoneChip, Theme.PhoneStroke)), Color.white, sliced: true);
		back.raycastTarget = true;
		RectTransform rect = back.rectTransform;
		TopLeft(rect, new Vector2(Pad, -top), new Vector2(size, size));
		Button button = back.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener((UnityAction)new Action(() => SetOpen(false)));
		Image arrow = Ui.Image("Arrow", rect, sprites.Glyph(Art.Icon.Back), Color.white);
		arrow.rectTransform.sizeDelta = new Vector2(18f * Unit, 18f * Unit);
		Text title = Label("Title", root, 20f, Theme.PhoneText, TextAnchor.MiddleLeft);
		title.text = "Hyland";
		TopLeft(title.rectTransform, new Vector2(Pad + size + 10f * Unit, -top), new Vector2(400f, size));
		return top + size + 10f * Unit;
	}

	/// <summary>Everything under the header scrolls (the screen is shorter than the rows): drag or the mouse wheel, at the
	/// game's own Messages sensitivity.</summary>
	private RectTransform BuildScroll(float top, out CanvasGroup rows)
	{
		RectTransform viewport = Ui.Rect("Scroll", root);
		viewport.anchorMin = Vector2.zero;
		viewport.anchorMax = Vector2.one;
		viewport.offsetMin = Vector2.zero;
		viewport.offsetMax = new Vector2(0f, -top);
		viewport.gameObject.AddComponent<RectMask2D>();
		// Clear but hit, so a drag or scroll anywhere in the list moves it.
		Image hit = viewport.gameObject.AddComponent<Image>();
		hit.color = new Color(0f, 0f, 0f, 0f);
		RectTransform list = Ui.Rect("Rows", viewport);
		list.anchorMin = new Vector2(0f, 1f);
		list.anchorMax = new Vector2(1f, 1f);
		list.pivot = new Vector2(0.5f, 1f);
		list.anchoredPosition = Vector2.zero;
		ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
		scroll.viewport = viewport;
		scroll.content = list;
		scroll.horizontal = false;
		scroll.vertical = true;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.scrollSensitivity = 8f;
		rows = Ui.Group(list.gameObject);
		rows.interactable = true;
		rows.blocksRaycasts = true;
		return list;
	}

	/// <summary>The board's rows top to bottom; returns how tall they came out.</summary>
	private float BuildRows()
	{
		float width = 655f - 2f * Pad;
		float y = 0f;
		y = Section("HUD", y, 6f);
		y = Card(y, width, 0f, (card, inner) =>
		{
			float row = 2f * Unit;
			(string, MelonPreferences_Entry<bool>)[] hud =
			{
				("Hyland HUD", settings.Enabled),
				("Glass hotbar", settings.Hotbar),
				("Bank panel", settings.MoneyPanel),
				("Fade when idle", settings.IdleFade),
				("Compass and clock", settings.Compass),
				("Curfew warnings", settings.TimeWarnings),
				("Friends on compass", settings.Friends),
			};
			for (int i = 0; i < hud.Length; i++)
			{
				// Lines between switches, none under the last (the board's).
				row = SwitchRow(card, inner, row, hud[i].Item1, hud[i].Item2, line: i < hud.Length - 1);
			}
			row += 10f * Unit;
			row = SliderRow(card, inner, row, "Slot gap", 0, 20, () => settings.SlotGap.Value, (v) => SetEntry(settings.SlotGap, Mathf.RoundToInt(v)), (v) => Mathf.RoundToInt(v).ToString());
			return row + 12f * Unit;
		});

		y = Section("PHONE", y, 16f);
		y = Card(y, width, 0f, (card, inner) =>
		{
			float row = 2f * Unit;
			row = SwitchRow(card, inner, row, "Glass phone", settings.GlassPhone, line: true);
			row = SwitchRow(card, inner, row, "Number key hints", settings.KeyHints, line: false);
			return row + 2f * Unit;
		});

		y = Section("SOUND", y, 16f);
		y = Card(y, width, 4f, (card, inner) =>
		{
			float row = 10f * Unit;
			// The slider runs in 5% steps: 0–20 on the slider is 0–100%.
			row = SliderRow(card, inner, row, "Rain volume", 0, 20, () => settings.RainVolume.Value / 5f, (v) => SetEntry(settings.RainVolume, Mathf.RoundToInt(v) * 5), (v) => Mathf.RoundToInt(v) * 5 + "%");
			row = Meter(card, inner, row + 6f * Unit) + 8f * Unit;
			Divider(card, inner, row);
			row = SwitchRow(card, inner, row, "Much quieter indoors", settings.QuieterIndoors, line: false);
			Text caption = Label("Caption", card, 11f, Theme.PhoneMuted, TextAnchor.UpperLeft);
			caption.text = "On top of Settings › Audio › Weather.";
			TopLeft(caption.rectTransform, new Vector2(14f * Unit, -row), new Vector2(inner, 16f * Unit));
			return row + 16f * Unit + 10f * Unit;
		});
		return y;
	}

	private float Section(string title, float y, float marginTop)
	{
		y += marginTop * Unit;
		Text label = Label(title, content, 11f, Theme.PhoneAccent, TextAnchor.UpperLeft);
		label.text = title;
		TopLeft(label.rectTransform, new Vector2(Pad, -y), new Vector2(300f, 14f * Unit));
		return y + 14f * Unit + 6f * Unit;
	}

	/// <summary>One of the board's rounded glass groups. <paramref name="fill"/> lays out its rows (left padding 14, given the
	/// inner width) and returns their height.</summary>
	private float Card(float y, float width, float padBottom, Func<RectTransform, float, float> fill)
	{
		Image image = Ui.Image("Group", content, sprites.PhoneGlass(16 * Unit), Color.white, sliced: true);
		RectTransform card = image.rectTransform;
		float height = fill(card, width - 2f * 14f * Unit) + padBottom * Unit;
		TopLeft(card, new Vector2(Pad, -y), new Vector2(width, height));
		// Children were placed from the card's top-left; the card is top-left pivoted too, so they stay put.
		return y + height;
	}

	private float SwitchRow(RectTransform card, float inner, float y, string label, MelonPreferences_Entry<bool> entry, bool line)
	{
		float height = 44f * Unit;
		Text text = Label(label, card, 14f, Theme.PhoneText, TextAnchor.MiddleLeft);
		text.text = label;
		TopLeft(text.rectTransform, new Vector2(14f * Unit, -y), new Vector2(inner - 50f * Unit, height));

		Image track = Ui.Image("Switch", card, sprites.Pill(12 * Unit, Theme.White), Ui.Color(Theme.PhoneTrack), sliced: true);
		track.raycastTarget = true;
		TopLeft(track.rectTransform, new Vector2(14f * Unit + inner - 40f * Unit, -y - (height - 24f * Unit) / 2f), new Vector2(40f * Unit, 24f * Unit));
		Button button = track.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener((UnityAction)new Action(() => SetEntry(entry, !entry.Value)));
		Image knob = Ui.Image("Knob", track.rectTransform, sprites.Disc(9 * Unit, Rgba.Hex(0xF1F3F5)), Color.white);
		knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0f, 0.5f);
		knob.rectTransform.sizeDelta = new Vector2(18f * Unit, 18f * Unit);
		float on = entry.Value ? 1f : 0f;
		toggles.Add(new Toggle { Entry = entry, Track = track, Knob = knob.rectTransform, Position = on });
		PlaceKnob(toggles[toggles.Count - 1]);
		if (line)
		{
			Divider(card, inner, y + height);
		}
		return y + height;
	}

	private float SliderRow(RectTransform card, float inner, float y, string label, int min, int max, Func<float> read, Action<float> write, Func<float, string> format)
	{
		Text name = Label(label, card, 14f, Theme.PhoneText, TextAnchor.UpperLeft);
		name.text = label;
		TopLeft(name.rectTransform, new Vector2(14f * Unit, -y), new Vector2(inner, 18f * Unit));
		Text value = Label(label + " value", card, 14f, Theme.PhoneMuted, TextAnchor.UpperRight);
		TopLeft(value.rectTransform, new Vector2(14f * Unit, -y), new Vector2(inner, 18f * Unit));
		y += 18f * Unit + 6f * Unit;

		// A uGUI Slider: its Fill Area and Handle Slide Area are what it sizes the fill and moves the handle in.
		RectTransform area = Ui.Rect(label + " slider", card);
		TopLeft(area, new Vector2(14f * Unit, -y), new Vector2(inner, 18f * Unit));
		Image hit = area.gameObject.AddComponent<Image>();
		hit.color = new Color(0f, 0f, 0f, 0f);
		Image track = Ui.Image("Track", area, sprites.Pill(2 * Unit, Theme.White), Ui.Color(Theme.PhoneTrack), sliced: true);
		track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
		track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
		track.rectTransform.sizeDelta = new Vector2(0f, 4f * Unit);
		RectTransform fillArea = Ui.Rect("Fill Area", area);
		fillArea.anchorMin = new Vector2(0f, 0.5f);
		fillArea.anchorMax = new Vector2(1f, 0.5f);
		fillArea.sizeDelta = new Vector2(0f, 4f * Unit);
		Image fill = Ui.Image("Fill", fillArea, sprites.Pill(2 * Unit, Theme.White), Ui.Color(Theme.PhoneAccent), sliced: true);
		fill.rectTransform.sizeDelta = Vector2.zero;
		RectTransform handleArea = Ui.Rect("Handle Slide Area", area);
		handleArea.anchorMin = Vector2.zero;
		handleArea.anchorMax = Vector2.one;
		handleArea.offsetMin = new Vector2(8f * Unit, 0f);
		handleArea.offsetMax = new Vector2(-8f * Unit, 0f);
		Image handle = Ui.Image("Handle", handleArea, sprites.Disc(8 * Unit, Theme.PhoneAccent), Color.white);
		// The Slider stretches the handle to its area's height (18); 2 less makes it the board's 16 round thumb.
		handle.rectTransform.sizeDelta = new Vector2(16f * Unit, -2f * Unit);

		Slider slider = area.gameObject.AddComponent<Slider>();
		slider.transition = Selectable.Transition.None;
		slider.fillRect = fill.rectTransform;
		slider.handleRect = handle.rectTransform;
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = min;
		slider.maxValue = max;
		slider.wholeNumbers = true;
		slider.SetValueWithoutNotify(read());
		slider.onValueChanged.AddListener((UnityAction<float>)new Action<float>(write));
		ranges.Add(new Range { Slider = slider, Value = value, Read = read, Format = format });
		value.text = format(read());
		return y + 18f * Unit;
	}

	/// <summary>The board's 16-bar level meter under the rain slider: lit bars follow the volume.</summary>
	private float Meter(RectTransform card, float inner, float y)
	{
		const int Bars = 16;
		float gap = 3f * Unit, width = (inner - (Bars - 1) * gap) / Bars, bottom = y + 18f * Unit;
		for (int i = 0; i < Bars; i++)
		{
			Image bar = Ui.Image("Bar", card, sprites.Pill(2 * Unit, Theme.White), Ui.Color(Theme.PhoneLine), sliced: true);
			RectTransform rect = bar.rectTransform;
			rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 0f);
			rect.anchoredPosition = new Vector2(14f * Unit + i * (width + gap), -bottom);
			meter.Add((rect, bar, (4f + (i * 7) % 11) * Unit));
			rect.sizeDelta = new Vector2(width, meter[i].Height);
		}
		return bottom;
	}

	private void Divider(RectTransform card, float inner, float y)
	{
		Image line = Ui.Image("Line", card, null!, Ui.Color(Theme.PhoneLine));
		TopLeft(line.rectTransform, new Vector2(14f * Unit, -y), new Vector2(inner, Mathf.Max(1f, Unit)));
	}

	private Text Label(string name, Transform parent, float boardSize, Rgba color, TextAnchor anchor)
	{
		RectTransform rect = Ui.Rect(name, parent);
		Text text = rect.gameObject.AddComponent<Text>();
		text.font = font;
		text.fontSize = Mathf.RoundToInt(boardSize * Unit);
		text.color = Ui.Color(color);
		text.alignment = anchor;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		return text;
	}

	private void SetEntry<T>(MelonPreferences_Entry<T> entry, T value)
	{
		if (!EqualityComparer<T>.Default.Equals(entry.Value, value))
		{
			entry.Value = value;
			// Written half a second after the last change, so a slider drag is one write, not twenty.
			saveAt = Time.unscaledTime + 0.5f;
		}
	}

	internal void Update(float dt)
	{
		if (saveAt >= 0f && Time.unscaledTime >= saveAt)
		{
			saveAt = -1f;
			settings.Save();
		}
		// The game's apps put their look offset back when the phone comes up again with them open (App.OnPhoneOpened).
		if (phone.IsOpen && !phoneWasOpen && isOpen)
		{
			phone.SetLookOffsetMultiplier(1f);
		}
		phoneWasOpen = phone.IsOpen;
		if (isOpen && phone.IsOpen && GameInput.GetButtonDown(GameInput.ButtonCode.PrimaryClick) && IsHoveringPhoneButton())
		{
			SetOpen(false);
		}
		if (!root.gameObject.activeSelf)
		{
			return;
		}
		foreach (Toggle toggle in toggles)
		{
			toggle.Position = Motion.Approach(toggle.Position, toggle.Entry.Value ? 1f : 0f, 0.22f, dt);
			PlaceKnob(toggle);
		}
		foreach (Range range in ranges)
		{
			float value = range.Read();
			if (!Mathf.Approximately(range.Slider.value, value))
			{
				range.Slider.SetValueWithoutNotify(value);
			}
			range.Value.text = range.Format(value);
		}
		int lit = Mathf.RoundToInt(settings.RainVolume.Value / 100f * meter.Count);
		for (int i = 0; i < meter.Count; i++)
		{
			(RectTransform bar, Image image, float height) = meter[i];
			bool on = i < lit;
			image.color = Ui.Color(on ? Theme.Friend : Theme.PhoneLine);
			bar.sizeDelta = new Vector2(bar.sizeDelta.x, Motion.Approach(bar.sizeDelta.y, height + (on ? 3f * Unit : 0f), 0.15f, dt));
		}
		Animate();
	}

	private void PlaceKnob(Toggle toggle)
	{
		toggle.Knob.anchoredPosition = new Vector2(Mathf.Lerp(3f, 19f, toggle.Position) * Unit + 9f * Unit, 0f);
		toggle.Track.color = Color.Lerp(Ui.Color(Theme.PhoneTrack), Ui.Color(Theme.PhoneAccent), toggle.Position);
	}

	/// <summary>The board's motion: the screen zooms out of its icon (0.34 s) and back into it on close (0.3 s); the rows
	/// rise in 0.14 s after it starts.</summary>
	private void Animate()
	{
		float t = Time.unscaledTime - animatedAt;
		float k = closing ? 1f - Motion.EaseOutCubic(t / 0.3f) : Motion.EaseOutCubic(t / 0.34f);
		float scale = Mathf.Lerp(0.12f, 1f, k);
		root.localScale = new Vector3(scale, scale, 1f);
		root.localPosition = Vector3.Lerp(origin, Vector3.zero, k);
		group.alpha = k;
		// Only a fade for the rows: moving them would fight the ScrollRect, which owns their position.
		contentGroup.alpha = closing ? 1f : Motion.EaseOutCubic((t - 0.14f) / 0.36f);
		if (closing && t >= 0.3f)
		{
			FinishClosing();
		}
	}

	private void SetOpen(bool open)
	{
		if (open == isOpen)
		{
			return;
		}
		isOpen = open;
		animatedAt = Time.unscaledTime;
		if (open)
		{
			closing = false;
			// Grow out of the icon: its center in the apps canvas's space (both canvases lie on the phone's screen).
			origin = apps.canvas.transform.InverseTransformPoint(iconButton.transform.position);
			origin.z = 0f;
			apps.SetIsOpen(true);
			home.SetIsOpen(false);
			phone.SetLookOffsetMultiplier(1f);
			Phone.ActiveApp = root.gameObject;
			home.LastSelectedSelectable = iconSelectable;
			group.blocksRaycasts = true;
			root.gameObject.SetActive(true);
		}
		else
		{
			if (Phone.ActiveApp == root.gameObject)
			{
				Phone.ActiveApp = null;
			}
			// The home screen comes back under the shrinking app; the apps canvas goes when it's done (Animate).
			closing = true;
			group.blocksRaycasts = false;
			home.SetIsOpen(true);
			phone.SetIsHorizontal(false);
			phone.SetLookOffsetMultiplier(1f);
		}
	}

	/// <summary>App.Exit: Escape (or a controller's back) closes the open app before it closes the phone.</summary>
	private void OnExit(ExitAction action)
	{
		if (!action.Used && phone.State.IsAcceptingInput && phone.IsOpen && isOpen)
		{
			action.Use();
			SetOpen(false);
		}
	}

	/// <summary>Phone.closeApps: the game closes whatever app is open before it opens one itself (a message, a call).</summary>
	private void OnCloseApps()
	{
		if (isOpen)
		{
			SetOpen(false);
			FinishClosing();
		}
	}

	private void FinishClosing()
	{
		closing = false;
		root.gameObject.SetActive(false);
		// Another app may have opened while this one shrank: then its canvas stays up.
		if (Phone.ActiveApp == null)
		{
			apps.SetIsOpen(false);
		}
	}

	/// <summary>App.IsHoveringButton: the mouse is over the phone's physical button.</summary>
	private static bool IsHoveringPhoneButton()
	{
		GameplayMenu menu = Singleton<GameplayMenu>.Instance;
		if (menu == null || menu.OverlayCamera == null)
		{
			return false;
		}
		Ray ray = menu.OverlayCamera.ScreenPointToRay(GameInput.MousePosition);
		return Physics.Raycast(ray, out RaycastHit hit, 2f, 1 << LayerMask.NameToLayer("Overlay")) && hit.collider.gameObject.name == "Button";
	}

	private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
	{
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		disposed = true;
		Exception? failure = null;
		if (isOpen || closing)
		{
			Ui.Step(ref failure, () =>
			{
				if (Phone.ActiveApp == root.gameObject)
				{
					Phone.ActiveApp = null;
				}
				if (Phone.ActiveApp == null)
				{
					apps.SetIsOpen(false);
				}
				home.SetIsOpen(true);
				phone.SetLookOffsetMultiplier(1f);
			});
		}
		Ui.Step(ref failure, () => GameInput.DeregisterExitListener(exitListener));
		Ui.Step(ref failure, () =>
		{
			Il2CppSystem.Delegate? left = Il2CppSystem.Delegate.Remove(phone.closeApps, closeListener);
			phone.closeApps = left?.Cast<Il2CppSystem.Action>();
		});
		if (iconButton != null)
		{
			Ui.Step(ref failure, () => home.appIcons.Remove(iconButton));
			Ui.Step(ref failure, () => home.uiPanel.RemoveSelectable(iconSelectable, true));
			Ui.Step(ref failure, () => UnityEngine.Object.Destroy(iconButton.gameObject));
		}
		if (root != null)
		{
			Ui.Step(ref failure, () => UnityEngine.Object.Destroy(root.gameObject));
		}
		Ui.Rethrow(failure);
	}
}
