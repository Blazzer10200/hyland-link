using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Money;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI.Phone;
using GameplayMenu = Il2CppScheduleOne.UI.GameplayMenu;
using Il2CppScheduleOne.Weather;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// The "glass phone": restyles the game's own home screen in place (mods/hud/PHONE.md has the prefab it starts from).
/// The game's wallpaper, info bar, icon grid and icons keep working (clicks, number keys, badges, controller selection);
/// they're recolored, resized and given glass tiles and glyphs, and a clock card goes above them. Everything changed is
/// recorded first and put back in <see cref="Dispose"/>. Sizes are the board's (316×640 screen) times
/// <see cref="Unit"/>, for the home canvas's 655×1201.
/// </summary>
internal sealed class PhoneSkin : IDisposable
{
	/// <summary>Canvas units per board pixel: the board's phone screen is 316 wide, the game's home canvas 655.</summary>
	private const float Unit = 655f / 316f;

	private const float Tile = 58f * Unit;

	private const float Radius = 17f * Unit;

	/// <summary>How far the info bar's time and "5G" move in, clear of the frame's 30 px screen corners.</summary>
	private const float BarInset = 10f * Unit;

	/// <summary>Where a rect of the game's sat, to put it back exactly.</summary>
	private readonly struct RectState
	{
		private readonly Vector2 anchorMin, anchorMax, pivot, position, size;

		internal RectState(RectTransform rect)
		{
			anchorMin = rect.anchorMin;
			anchorMax = rect.anchorMax;
			pivot = rect.pivot;
			position = rect.anchoredPosition;
			size = rect.sizeDelta;
		}

		internal void Apply(RectTransform rect)
		{
			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.pivot = pivot;
			rect.anchoredPosition = position;
			rect.sizeDelta = size;
		}
	}

	private sealed class Icon
	{
		internal RectTransform Root = null!;

		internal Button Button = null!;

		internal CanvasGroup Group = null!;

		internal Vector3 Scale;

		internal GameObject Shade = null!;

		internal bool ShadeActive;

		internal Image Outline = null!;

		internal Sprite OutlineSprite = null!;

		internal RectTransform Art = null!;

		internal Image ArtImage = null!;

		internal RectState ArtRect;

		internal Sprite ArtSprite = null!;

		internal bool ArtKeepAspect;

		internal RectTransform Badge = null!;

		internal Image BadgeImage = null!;

		internal Sprite BadgeSprite = null!;

		internal RectState BadgeRect;

		internal Image.Type BadgeType;

		internal Text BadgeText = null!;

		internal int BadgeFontSize;

		internal Text Label = null!;

		internal int LabelFontSize;

		internal Color LabelColor;

		internal bool LabelBestFit;

		internal HorizontalWrapMode LabelOverflow;

		internal RectTransform Glass = null!;

		internal RectTransform Key = null!;

		internal Text KeyText = null!;

		internal float Press;

		internal bool BadgeWasOn;

		internal float BadgeShownAt = -1f;
	}

	private readonly Phone phone;

	private readonly HomeScreen home;

	private readonly Sprites sprites;

	private readonly Settings settings;

	private readonly Font font;

	private readonly Dictionary<int, Icon> icons = new Dictionary<int, Icon>();

	private readonly List<(MeshRenderer Renderer, bool Enabled)> hidden = new List<(MeshRenderer, bool)>();

	private readonly List<(Collider Collider, bool Enabled)> colliders = new List<(Collider, bool)>();

	private readonly List<(CanvasScaler Scaler, float Pixels)> scalers = new List<(CanvasScaler, float)>();

	private float nextSharpen;

	private Image? backdrop;

	private bool timeEnabled;

	private Image? background;

	private Color backgroundColor;

	private GameObject? guilloche;

	private bool guillocheActive;

	private Image? barImage;

	private Color barColor;

	private GameObject? barShadow;

	private bool barShadowActive;

	private Text? timeText;

	private Color timeColor;

	private int timeSize;

	private Vector2 dataPosition;

	private Text? dataText;

	private Color dataColor;

	private int dataSize;

	private GridLayoutGroup? grid;

	private Vector2 gridCell, gridSpacing;

	private int gridColumns;

	private RectTransform? gridRect;

	private Vector2 gridPosition;

	private RectTransform? wallpaper, card, wake, onlineDot, frame;

	private CanvasGroup? cardGroup;

	private Image? wakeImage;

	private Text? bigTime, ampm, day, bank, online;

	private RectTransform? rain;

	private readonly List<Text> initials = new List<Text>();

	private readonly List<RectTransform> drops = new List<RectTransform>();

	private bool shown;

	private bool phoneWasOpen;

	private float openedAt = -1f;

	private float nextRefresh;

	private bool disposed;

	internal PhoneSkin(Phone phone, HomeScreen home, Sprites sprites, Settings settings, Action<string> log)
	{
		this.phone = phone;
		this.home = home;
		this.sprites = sprites;
		this.settings = settings;
		font = home.timeText.font;
		// A rebuild while the phone is up (a setting flipped in the Hyland app) doesn't replay the opening.
		phoneWasOpen = phone.IsOpen;
		RectTransform screen = home.transform.Cast<RectTransform>();
		try
		{
			// The wallpaper: the game's gray and its line pattern give way to the dark screen with faint rings.
			Transform bg = Find(screen, "Background");
			background = bg.GetComponent<Image>();
			backgroundColor = background.color;
			guilloche = Find(bg, "Image").gameObject;
			guillocheActive = guilloche.activeSelf;
			background.color = Ui.Color(Theme.PhoneWallpaper);
			guilloche.SetActive(false);
			Rect bgRect = bg.Cast<RectTransform>().rect;
			Image paper = Ui.Image("Hyland wallpaper", bg, sprites.Wallpaper(Mathf.RoundToInt(bgRect.width), Mathf.RoundToInt(bgRect.height)), Color.white);
			wallpaper = paper.rectTransform;
			Stretch(wallpaper);
			wallpaper.SetAsFirstSibling();
			// The game's Background stops 40 short of the top for its info bar, whose gray band goes below: without this the
			// screen's gray mesh showed through as a band behind the bar.
			backdrop = Ui.Image("Hyland backdrop", screen, null!, Ui.Color(Theme.PhoneWallpaper));
			Stretch(backdrop.rectTransform);
			backdrop.rectTransform.SetAsFirstSibling();

			// The info bar keeps its texts (the game writes the time every minute); its gray band and shadow go.
			Transform bar = Find(screen, "InfoBar");
			barImage = Find(bar, "Image").GetComponent<Image>();
			barColor = barImage.color;
			barImage.color = new Color(0f, 0f, 0f, 0f);
			for (int i = 0; i < bar.childCount; i++)
			{
				Transform child = bar.GetChild(i);
				if (child.name == "Image" && child != barImage.transform)
				{
					barShadow = child.gameObject;
				}
			}
			if (barShadow != null)
			{
				barShadowActive = barShadow.activeSelf;
				barShadow.SetActive(false);
			}
			timeText = home.timeText;
			timeColor = timeText.color;
			timeSize = timeText.fontSize;
			timeText.color = Ui.Color(Rgba.Hex(0xC9D1DB));
			timeText.fontSize = Mathf.RoundToInt(12f * Unit);
			dataText = Find(bar, "Data").GetComponent<Text>();
			dataColor = dataText.color;
			dataSize = dataText.fontSize;
			dataText.color = Ui.Color(Rgba.Hex(0xC9D1DB));
			dataText.fontSize = Mathf.RoundToInt(12f * Unit);
			// The clock card shows the time, so the bar's own goes (the game keeps writing it). "5G" moves in from the frame's
			// rounded screen corner.
			timeEnabled = timeText.enabled;
			timeText.enabled = false;
			dataPosition = dataText.rectTransform.anchoredPosition;
			dataText.rectTransform.anchoredPosition = dataPosition - new Vector2(BarInset, 0f);
			// The board's green "connected" dot, left of "5G".
			onlineDot = Ui.Image("Hyland online dot", dataText.transform, sprites.Disc(3, Theme.Online), Color.white).rectTransform;
			onlineDot.anchorMin = onlineDot.anchorMax = new Vector2(1f, 0.5f);
			onlineDot.sizeDelta = new Vector2(6f * Unit, 6f * Unit);
			onlineDot.anchoredPosition = new Vector2(-dataText.preferredWidth - 8f * Unit, 0f);

			// The grid: the game's 3 × 190 becomes 4 tiles a row under the card.
			grid = home.appIconContainer.GetComponent<GridLayoutGroup>();
			gridRect = home.appIconContainer;
			gridCell = grid.cellSize;
			gridSpacing = grid.spacing;
			gridColumns = grid.constraintCount;
			gridPosition = gridRect.anchoredPosition;
			// The board's pitch: 73 across, 98 down (the label sits under the tile, inside that gap).
			float gap = 73f * Unit - Tile;
			float width = 4f * Tile + 3f * gap;
			float cardTop = screen.rect.height / 2f - 40f - 8f * Unit;
			float cardHeight = 154f * Unit;
			grid.cellSize = new Vector2(Tile, Tile);
			grid.spacing = new Vector2(gap, 98f * Unit - Tile);
			grid.constraintCount = 4;
			gridRect.anchoredPosition = new Vector2(-width / 2f, cardTop - cardHeight - 26f * Unit);

			BuildCard(screen, cardTop, cardHeight);

			// The screen wakes from black when the phone comes up; nothing under it can be clicked meanwhile.
			wakeImage = Ui.Image("Hyland wake", screen, null!, new Color(0f, 0f, 0f, 0f));
			wake = wakeImage.rectTransform;
			Stretch(wake);
			wake.SetAsLastSibling();

			BuildBody(phone, screen);
			Refresh();
			log($"Phone: home {screen.rect.width:F0}x{screen.rect.height:F0}, {home.appIconContainer.childCount} icons, {hidden.Count} model mesh(es) hidden for our frame (sorting {frame?.GetComponent<Canvas>().sortingOrder}, layer {screen.gameObject.layer}), home bar is the button ({colliders.Count} old collider(s) off, Overlay layer {LayerMask.NameToLayer("Overlay")}).");
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	internal bool IsBoundTo(HomeScreen instance)
	{
		return home == instance && card != null;
	}

	private void BuildCard(RectTransform screen, float top, float height)
	{
		float width = screen.rect.width - 2f * 16f * Unit;
		card = Ui.Image("Hyland card", screen, sprites.PhoneGlass(20 * Unit), Color.white, sliced: true).rectTransform;
		Ui.Place(card, new Vector2(0f, top - height / 2f), new Vector2(width, height));
		cardGroup = Ui.Group(card.gameObject);
		float left = -width / 2f + 16f * Unit;
		bigTime = Label("Time", card, 40f, Theme.PhoneText, TextAnchor.UpperLeft, new Vector2(left, height / 2f - 14f * Unit), new Vector2(200f * Unit, 46f * Unit));
		bigTime.fontStyle = FontStyle.Bold;
		ampm = Label("AM/PM", card, 16f, Theme.PhoneMuted, TextAnchor.LowerLeft, new Vector2(left, height / 2f - 14f * Unit), new Vector2(60f * Unit, 40f * Unit));
		day = Label("Day", card, 13f, Theme.PhoneMuted, TextAnchor.UpperLeft, new Vector2(left, height / 2f - 62f * Unit), new Vector2(200f * Unit, 18f * Unit));

		rain = Ui.Image("Rain", card, sprites.Glyph(Art.Icon.Rain), Color.white).rectTransform;
		Ui.Place(rain, new Vector2(width / 2f - 38f * Unit, height / 2f - 30f * Unit), new Vector2(34f * Unit, 34f * Unit));
		for (int i = 0; i < 3; i++)
		{
			RectTransform drop = Ui.Image("Drop", rain, sprites.Pill(1, Theme.Friend), Color.white, sliced: true).rectTransform;
			drop.sizeDelta = new Vector2(2f * Unit, 6f * Unit);
			drops.Add(drop);
		}

		float chipW = (width - 32f * Unit - 8f * Unit) / 2f, chipH = 48f * Unit, chipY = -height / 2f + 14f * Unit + chipH / 2f;
		RectTransform bankChip = Ui.Image("Bank", card, sprites.Pill(12 * Unit, Theme.PhoneChip), Color.white, sliced: true).rectTransform;
		Ui.Place(bankChip, new Vector2(left + chipW / 2f, chipY), new Vector2(chipW, chipH));
		Label("Label", bankChip, 11f, Theme.PhoneMuted, TextAnchor.UpperLeft, new Vector2(-chipW / 2f + 10f * Unit, chipH / 2f - 7f * Unit), new Vector2(chipW, 14f * Unit)).text = "Bank";
		bank = Label("Value", bankChip, 15f, Theme.PhoneAccent, TextAnchor.UpperLeft, new Vector2(-chipW / 2f + 10f * Unit, chipH / 2f - 23f * Unit), new Vector2(chipW - 12f * Unit, 20f * Unit));
		bank.fontStyle = FontStyle.Bold;

		RectTransform onlineChip = Ui.Image("Online", card, sprites.Pill(12 * Unit, Theme.PhoneChip), Color.white, sliced: true).rectTransform;
		Ui.Place(onlineChip, new Vector2(left + chipW * 1.5f + 8f * Unit, chipY), new Vector2(chipW, chipH));
		Label("Label", onlineChip, 11f, Theme.PhoneMuted, TextAnchor.UpperLeft, new Vector2(-chipW / 2f + 10f * Unit, chipH / 2f - 7f * Unit), new Vector2(chipW, 14f * Unit)).text = "On the server";
		float ax = -chipW / 2f + 10f * Unit + 8f * Unit, ay = chipH / 2f - 31f * Unit;
		for (int i = 0; i < 3; i++)
		{
			// Back to front, so each avatar overlaps the one before it like the board's.
			RectTransform avatar = Ui.Image("Avatar", onlineChip, sprites.Disc(8, Theme.Friend), Color.white).rectTransform;
			Ui.Place(avatar, new Vector2(ax + i * 7f * Unit, ay), new Vector2(16f * Unit, 16f * Unit));
			Text letter = Label("Initial", avatar, 9f, Rgba.Hex(0x052033), TextAnchor.MiddleCenter, Vector2.zero, avatar.sizeDelta, centered: true);
			letter.fontStyle = FontStyle.Bold;
			initials.Add(letter);
		}
		online = Label("Count", onlineChip, 13f, Theme.PhoneText, TextAnchor.MiddleLeft, new Vector2(ax + 2f * 7f * Unit + 12f * Unit, ay), new Vector2(chipW, 18f * Unit), centeredY: true);
	}

	/// <summary>A legacy Text in the phone's own font (the game's home screen uses OpenSans through uGUI Text), sized in
	/// board pixels and placed by its top-left corner unless asked otherwise.</summary>
	private Text Label(string name, Transform parent, float boardSize, Rgba color, TextAnchor anchor, Vector2 position, Vector2 size, bool centered = false, bool centeredY = false)
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
		rect.sizeDelta = size;
		if (centered)
		{
			rect.anchoredPosition = position;
		}
		else
		{
			rect.pivot = centeredY ? new Vector2(0f, 0.5f) : new Vector2(0f, 1f);
			rect.anchoredPosition = position;
		}
		return text;
	}

	/// <summary>The board's phone body in place of the game's model, whose shape isn't a recolor away: every mesh but the
	/// screen is hidden (only on the local phone), and our frame, speaker slot and home bar go on a world-space canvas of
	/// their own, lined up with the home screen. Its own canvas because the game turns the home and apps canvases on and off
	/// as apps open; sorted above both, so the frame's rounded hole rounds their corners too.</summary>
	private void BuildBody(Phone phone, RectTransform screen)
	{
		GameObject model = phone.phoneModel;
		if (model != null)
		{
			foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
			{
				// The screen mesh stays as the dark backing behind the canvases.
				if (renderer.gameObject.name == "Screen")
				{
					continue;
				}
				hidden.Add((renderer, renderer.enabled));
				renderer.enabled = false;
				// The home button's collider is what an open app's click test hits (App.IsHoveringButton: a ray on the Overlay
				// layer that must hit a collider named "Button"). Ours sits on the home bar instead, so this one goes too.
				if (renderer.gameObject.name == "Button")
				{
					foreach (Collider collider in renderer.GetComponents<Collider>())
					{
						colliders.Add((collider, collider.enabled));
						collider.enabled = false;
					}
				}
			}
		}

		Canvas homeCanvas = screen.GetComponentInParent<Canvas>();
		Transform parent = homeCanvas.transform.parent;
		Rect rect = screen.rect;
		frame = Ui.Rect("Hyland phone frame", parent);
		frame.pivot = new Vector2(0.5f, 0.5f);
		frame.sizeDelta = rect.size;
		frame.position = screen.TransformPoint(rect.center);
		frame.rotation = screen.rotation;
		Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one, screenScale = screen.lossyScale;
		frame.localScale = new Vector3(screenScale.x / parentScale.x, screenScale.y / parentScale.y, screenScale.z / parentScale.z);
		Canvas canvas = frame.gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.WorldSpace;
		canvas.worldCamera = homeCanvas.worldCamera;
		canvas.sortingLayerID = homeCanvas.sortingLayerID;
		AppsCanvas apps = PlayerSingleton<AppsCanvas>.Instance;
		int appsOrder = apps != null && apps.canvas != null ? apps.canvas.sortingOrder : homeCanvas.sortingOrder;
		canvas.sortingOrder = Math.Max(homeCanvas.sortingOrder, appsOrder) + 1;
		CanvasScaler homeScaler = homeCanvas.GetComponent<CanvasScaler>();
		CanvasScaler? appsScaler = apps != null && apps.canvas != null ? apps.canvas.GetComponent<CanvasScaler>() : null;
		foreach (CanvasScaler? s in new[] { homeScaler, appsScaler })
		{
			if (s != null)
			{
				scalers.Add((s, s.dynamicPixelsPerUnit));
			}
		}
		if (homeScaler != null)
		{
			CanvasScaler scaler = frame.gameObject.AddComponent<CanvasScaler>();
			scaler.dynamicPixelsPerUnit = homeScaler.dynamicPixelsPerUnit;
			scaler.referencePixelsPerUnit = homeScaler.referencePixelsPerUnit;
		}

		// The board's body: 12 at the sides, 36 above the screen (the speaker slot), 24 below, 46 outer and 30 screen corners.
		float side = 12f * Unit, top = 36f * Unit, bottom = 24f * Unit;
		Image body = Ui.Image("Body", frame, sprites.PhoneFrame(46f * Unit, 30f * Unit, side, top, bottom), Color.white, sliced: true);
		Ui.Place(body.rectTransform, new Vector2(0f, (top - bottom) / 2f), new Vector2(rect.width + 2f * side, rect.height + top + bottom));
		Image speaker = Ui.Image("Speaker", frame, sprites.Pill(4 * Unit, Theme.PhoneSpeaker), Color.white, sliced: true);
		Ui.Place(speaker.rectTransform, new Vector2(0f, rect.height / 2f + 14f * Unit), new Vector2(40f * Unit, 8f * Unit));
		Image bar = Ui.Image("Home bar", frame, sprites.Pill(2 * Unit, Theme.PhoneHomeBar), Color.white, sliced: true);
		Ui.Place(bar.rectTransform, new Vector2(0f, -rect.height / 2f + 10f * Unit), new Vector2(80f * Unit, 4f * Unit));
		// Same layer as the home screen, so whichever camera draws the phone draws this too.
		foreach (Transform t in frame.GetComponentsInChildren<Transform>(true))
		{
			t.gameObject.layer = screen.gameObject.layer;
		}

		// The home bar is the phone's button: clicking it closes the open app, the game's rule for its own button. Named and
		// layered for that test; it covers the bar and the bezel under it (not more: on sideways apps the bar runs along an
		// edge of the app). Deep both ways, so it's in front of the screen whichever way the canvas faces.
		RectTransform hit = Ui.Rect("Button", frame);
		hit.gameObject.layer = LayerMask.NameToLayer("Overlay");
		hit.localPosition = new Vector3(0f, -rect.height / 2f + 4f * Unit, 0f);
		BoxCollider box = hit.gameObject.AddComponent<BoxCollider>();
		box.size = new Vector3(110f * Unit, 24f * Unit, 40f);
	}

	/// <summary>Text on the phone is rasterized at dynamicPixelsPerUnit pixels per canvas unit: the game's 5 (home) and 2
	/// (apps) against ~1 on screen at 1440p, so glyphs were drawn up to 5× too big and shrunk without mipmaps (fonts have
	/// none), which reads as crunchy and low-res. This draws them at about the size they show (×1.25), measured through
	/// the camera that draws the phone. Changed only on a real difference: every text rebuilds when it does.</summary>
	private void Sharpen()
	{
		Camera? camera = Singleton<GameplayMenu>.Instance?.OverlayCamera;
		RectTransform screen = home.transform.Cast<RectTransform>();
		if (camera == null)
		{
			return;
		}
		Rect rect = screen.rect;
		Vector3 left = camera.WorldToScreenPoint(screen.TransformPoint(new Vector3(rect.xMin, rect.center.y, 0f)));
		Vector3 right = camera.WorldToScreenPoint(screen.TransformPoint(new Vector3(rect.xMax, rect.center.y, 0f)));
		float perUnit = Vector2.Distance(left, right) / rect.width;
		if (perUnit <= 0f || float.IsNaN(perUnit))
		{
			return;
		}
		float pixels = Mathf.Clamp(perUnit * 1.25f, 0.75f, 5f);
		foreach ((CanvasScaler scaler, float _) in scalers)
		{
			if (scaler != null && Mathf.Abs(scaler.dynamicPixelsPerUnit - pixels) > 0.1f)
			{
				scaler.dynamicPixelsPerUnit = pixels;
			}
		}
	}

	internal void Update(float dt)
	{
		// The motion plays when the phone comes up, like the board: not when you back out of an app to the home screen.
		bool open = phone.IsOpen;
		if (open && !phoneWasOpen)
		{
			openedAt = Time.unscaledTime;
			foreach (Icon icon in icons.Values)
			{
				icon.BadgeShownAt = -1f;
			}
		}
		phoneWasOpen = open;
		if (open && Time.unscaledTime >= nextSharpen)
		{
			nextSharpen = Time.unscaledTime + 0.5f;
			Sharpen();
		}
		shown = open && home.isOpen;
		if (!shown)
		{
			return;
		}
		AdoptIcons();
		if (Time.unscaledTime >= nextRefresh)
		{
			nextRefresh = Time.unscaledTime + 0.25f;
			Refresh();
		}
		Animate(dt);
	}

	/// <summary>Restyles icons as the game makes them (each app adds its own when it starts) and drops gone ones.</summary>
	private void AdoptIcons()
	{
		Transform container = home.appIconContainer;
		for (int i = 0; i < container.childCount; i++)
		{
			Transform child = container.GetChild(i);
			int id = child.GetInstanceID();
			if (!icons.ContainsKey(id))
			{
				icons.Add(id, Restyle(child.Cast<RectTransform>()));
			}
		}
		foreach (Icon icon in icons.Values)
		{
			if (icon.Root != null)
			{
				icon.Key.gameObject.SetActive(false);
			}
		}
		if (!settings.KeyHints.Value)
		{
			return;
		}
		// 1–9 open home.appIcons[0..8], the order the apps made their icons in (HomeScreen.Update).
		Il2CppSystem.Collections.Generic.List<Button> order = home.appIcons;
		for (int i = 0; i < order.Count && i < 9; i++)
		{
			Button button = order[i];
			if (button != null && icons.TryGetValue(button.transform.GetInstanceID(), out Icon? icon) && icon.Root != null)
			{
				icon.Key.gameObject.SetActive(true);
				icon.KeyText.text = (i + 1).ToString();
			}
		}
	}

	private IEnumerable<Icon> Ordered()
	{
		List<Icon> list = new List<Icon>(icons.Values);
		list.RemoveAll((icon) => icon.Root == null);
		list.Sort((a, b) => a.Root.GetSiblingIndex().CompareTo(b.Root.GetSiblingIndex()));
		return list;
	}

	private Icon Restyle(RectTransform root)
	{
		Icon icon = new Icon { Root = root, Button = root.GetComponent<Button>(), Scale = root.localScale };
		icon.Shade = Find(root, "Shade").gameObject;
		icon.ShadeActive = icon.Shade.activeSelf;
		icon.Outline = Find(root, "Outline").GetComponent<Image>();
		icon.OutlineSprite = icon.Outline.sprite;
		icon.Art = Find(Find(root, "Mask"), "Image").Cast<RectTransform>();
		icon.ArtImage = icon.Art.GetComponent<Image>();
		icon.ArtRect = new RectState(icon.Art);
		icon.ArtSprite = icon.ArtImage.sprite;
		icon.ArtKeepAspect = icon.ArtImage.preserveAspect;
		icon.Badge = Find(root, "Notifications").Cast<RectTransform>();
		icon.BadgeImage = icon.Badge.GetComponent<Image>();
		icon.BadgeSprite = icon.BadgeImage.sprite;
		icon.BadgeRect = new RectState(icon.Badge);
		icon.BadgeType = icon.BadgeImage.type;
		icon.BadgeText = Find(icon.Badge, "Text").GetComponent<Text>();
		icon.BadgeFontSize = icon.BadgeText.fontSize;
		icon.Label = Find(root, "Label").GetComponent<Text>();
		icon.LabelFontSize = icon.Label.fontSize;
		icon.LabelColor = icon.Label.color;
		icon.LabelBestFit = icon.Label.resizeTextForBestFit;
		icon.LabelOverflow = icon.Label.horizontalOverflow;

		icon.Shade.SetActive(false);
		// The game tints Outline clear normally and near-white on hover or controller selection: with our accent ring as its
		// sprite, hover and selection show the ring.
		icon.Outline.sprite = sprites.Ring(Radius + 3f, Theme.PhoneAccent);
		Image glass = Ui.Image("Hyland glass", root, sprites.PhoneGlass(Radius), Color.white, sliced: true);
		icon.Glass = glass.rectTransform;
		Stretch(icon.Glass);
		icon.Glass.SetSiblingIndex(icon.Outline.transform.GetSiblingIndex());
		Art.Icon? glyph = GlyphFor(icon.ArtSprite, root.name);
		if (glyph != null)
		{
			icon.ArtImage.sprite = sprites.Glyph(glyph.Value);
			icon.ArtImage.preserveAspect = true;
			// The board's 26 px glyph, centered in the tile.
			Pin(icon.Art, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f * Unit, 26f * Unit));
		}
		// The game's red badge sprite becomes a white pill its own red tints. Its center sits 4 in from the tile's corner, so
		// it sticks out 5 like the board's.
		icon.BadgeImage.sprite = sprites.Pill(9 * Unit, Theme.White);
		icon.BadgeImage.type = Image.Type.Sliced;
		Pin(icon.Badge, Vector2.one, new Vector2(-4f * Unit, -4f * Unit), new Vector2(18f * Unit, 18f * Unit));
		icon.BadgeText.fontSize = Mathf.RoundToInt(11f * Unit);
		// The game's label shrinks long names to fit (best fit), so "Messages" came out smaller than "Map". One size, like
		// the board, and long names run past the tile's width instead.
		icon.Label.resizeTextForBestFit = false;
		icon.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
		icon.Label.fontSize = Mathf.RoundToInt(11f * Unit);
		icon.Label.color = glyph == Art.Icon.AppHyland ? Ui.Color(Theme.PhoneAccent) : Ui.Color(Theme.PhoneText);
		if (glyph == Art.Icon.AppHyland)
		{
			glass.sprite = sprites.Get("hylandTile", () => Art.Glass(Radius, Theme.PhoneGlass, Theme.PhoneAccent, Art.PhoneEdge));
		}

		Image key = Ui.Image("Hyland key", root, sprites.Get("keyChip", () => Art.Glass(5 * Unit, Theme.PhoneLine, Theme.PhoneTrack, Unit)), Color.white, sliced: true);
		icon.Key = key.rectTransform;
		icon.Key.anchorMin = icon.Key.anchorMax = Vector2.zero;
		icon.Key.sizeDelta = new Vector2(16f * Unit, 16f * Unit);
		icon.Key.anchoredPosition = new Vector2(3f * Unit, 3f * Unit);
		icon.KeyText = Label("Text", icon.Key, 10f, Rgba.Hex(0xC9D1DB), TextAnchor.MiddleCenter, Vector2.zero, icon.Key.sizeDelta, centered: true);

		// Fades and pops the whole tile. Interactable and raycast-blocking, so the game's button keeps working.
		icon.Group = root.gameObject.AddComponent<CanvasGroup>();
		return icon;
	}

	/// <summary>Our glyph for one of the game's seven apps (by its icon sprite) or the Hyland app; null keeps another mod's
	/// own icon art inside the glass tile.</summary>
	private static Art.Icon? GlyphFor(Sprite sprite, string rootName)
	{
		if (rootName == HylandApp.IconName)
		{
			return Art.Icon.AppHyland;
		}
		switch (sprite != null ? sprite.name : "")
		{
		case "appicon_messages": return Art.Icon.AppMessages;
		case "appicon_journal": return Art.Icon.AppJournal;
		case "appicon_productmanager": return Art.Icon.AppProducts;
		case "appicon_contacts": return Art.Icon.AppContacts;
		case "appicon_map": return Art.Icon.AppMap;
		case "appicon_dealers": return Art.Icon.AppDealers;
		case "appicon_deliveries": return Art.Icon.AppDeliveries;
		default: return null;
		}
	}

	/// <summary>The card's live values: a few times a second is plenty.</summary>
	private void Refresh()
	{
		TimeManager time = NetworkSingleton<TimeManager>.Instance;
		if (time != null && bigTime != null && ampm != null && day != null)
		{
			string clock = TimeManager.Get12HourTime(time.CurrentTime, true);
			int space = clock.LastIndexOf(' ');
			bigTime.text = space > 0 ? clock.Substring(0, space) : clock;
			ampm.text = space > 0 ? clock.Substring(space + 1) : "";
			ampm.rectTransform.anchoredPosition = new Vector2(bigTime.rectTransform.anchoredPosition.x + bigTime.preferredWidth + 4f * Unit, bigTime.rectTransform.anchoredPosition.y - 4f * Unit);
			day.text = time.CurrentDay.ToString();
		}
		MoneyManager money = NetworkSingleton<MoneyManager>.Instance;
		if (money != null && bank != null)
		{
			bank.text = MoneyManager.FormatAmount(money.sync___get_value_onlineBalance(), false, false);
		}
		if (online != null)
		{
			List<string> names = new List<string>();
			Il2CppSystem.Collections.Generic.List<Player> players = Player.PlayerList;
			for (int i = 0; players != null && i < players.Count; i++)
			{
				Player player = players[i];
				if (player != null && !CompassSkin.Friends.IsServerPlayer(player))
				{
					names.Add(player.PlayerName ?? "");
				}
			}
			online.text = names.Count + " online";
			for (int i = 0; i < initials.Count; i++)
			{
				bool on = i < names.Count;
				initials[i].transform.parent.gameObject.SetActive(on);
				if (on)
				{
					initials[i].text = names[i].Length > 0 ? char.ToUpperInvariant(names[i][0]).ToString() : "?";
				}
			}
			float shownAvatars = Math.Min(names.Count, initials.Count);
			RectTransform count = online.rectTransform;
			float first = initials.Count > 0 ? initials[0].transform.parent.Cast<RectTransform>().anchoredPosition.x : 0f;
			count.anchoredPosition = new Vector2(first + Math.Max(0f, shownAvatars - 1f) * 7f * Unit + (shownAvatars > 0 ? 12f * Unit : -8f * Unit), count.anchoredPosition.y);
		}
		if (rain != null)
		{
			rain.gameObject.SetActive(IsRaining());
		}
	}

	private static bool IsRaining()
	{
		EnvironmentManager environment = NetworkSingleton<EnvironmentManager>.Instance;
		if (environment == null || !environment.HasActiveWeatherVolumes())
		{
			return false;
		}
		environment.GetCurrentWeather(out string active, out string neighbour, out float blend);
		string id = blend > 0.5f && !string.IsNullOrEmpty(neighbour) ? neighbour : active;
		return id != null && id.IndexOf("Rain", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	/// <summary>The board's motion, from the moment the phone came up: the screen wakes from black (0.38 s), the card drops
	/// in (0.42 s), the tiles pop in one after another (0.46 s each, 40 ms apart, from 0.16 s), each badge pops once as its
	/// tile lands. Pressing a tile squishes it.</summary>
	private void Animate(float dt)
	{
		float t = openedAt < 0f ? 10f : Time.unscaledTime - openedAt;
		if (wakeImage != null)
		{
			wakeImage.color = new Color(0f, 0f, 0f, shown ? 1f - Motion.EaseOutCubic(t / 0.38f) : 0f);
			wakeImage.raycastTarget = shown && t < 0.3f;
		}
		if (card != null && cardGroup != null)
		{
			float k = Motion.EaseOutCubic(t / 0.42f);
			cardGroup.alpha = k;
			card.localPosition = new Vector3(card.localPosition.x, CardY() + (1f - k) * 14f * Unit, 0f);
		}
		if (drops.Count > 0 && rain != null && rain.gameObject.activeSelf)
		{
			for (int i = 0; i < drops.Count; i++)
			{
				float phase = (Time.unscaledTime + i * 0.35f) % 1f;
				drops[i].localPosition = new Vector3((-8f + i * 8f) * Unit, (-14f - phase * 14f) * Unit, 0f);
				drops[i].GetComponent<Image>().color = new Color(1f, 1f, 1f, phase < 0.3f ? phase / 0.3f : 1f - (phase - 0.3f) / 0.7f);
			}
		}
		int index = 0;
		foreach (Icon icon in Ordered())
		{
			float local = (t - 0.16f - index * 0.04f) / 0.46f;
			index++;
			// 0.6 → 1.06 at 70% → 1, fading in over the first 70%: the board's hp-icon curve.
			float scale = local <= 0f ? 0.6f : local >= 1f ? 1f : local < 0.7f ? Mathf.Lerp(0.6f, 1.06f, Motion.EaseOutCubic(local / 0.7f)) : Mathf.Lerp(1.06f, 1f, (local - 0.7f) / 0.3f);
			icon.Group.alpha = Mathf.Clamp01(local / 0.7f);
			bool pressed = icon.Button != null && icon.Button.IsPressed();
			icon.Press = Motion.Approach(icon.Press, pressed ? 1f : 0f, 0.14f, dt);
			icon.Root.localScale = icon.Scale * scale;
			float squish = 1f - 0.12f * icon.Press;
			icon.Glass.localScale = new Vector3(squish, squish, 1f);
			icon.Art.parent.localScale = new Vector3(squish, squish, 1f);
			// Badges pop once, when their tile lands or when a count first appears.
			bool badgeOn = icon.Badge.gameObject.activeSelf;
			if (badgeOn && (!icon.BadgeWasOn || icon.BadgeShownAt < 0f))
			{
				icon.BadgeShownAt = Mathf.Max(Time.unscaledTime, openedAt + 0.16f + (index - 1) * 0.04f + 0.38f);
			}
			icon.BadgeWasOn = badgeOn;
			float b = icon.BadgeShownAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - icon.BadgeShownAt) / 0.26f);
			float pop = b < 0.7f ? Mathf.Lerp(0f, 1.12f, b / 0.7f) : Mathf.Lerp(1.12f, 1f, (b - 0.7f) / 0.3f);
			icon.Badge.localScale = new Vector3(pop, pop, 1f);
		}
	}

	private float CardY()
	{
		RectTransform screen = home.transform.Cast<RectTransform>();
		return screen.rect.height / 2f - 40f - 8f * Unit - 154f * Unit / 2f;
	}

	private static Transform Find(Transform parent, string name)
	{
		Transform? child = parent.Find(name);
		if (child == null)
		{
			throw new InvalidOperationException($"The phone has no '{name}' under '{parent.name}'.");
		}
		return child;
	}

	/// <summary>Center-pivots a rect on one anchor point, at an offset and size from it.</summary>
	private static void Pin(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
	{
		rect.anchorMin = anchor;
		rect.anchorMax = anchor;
		rect.pivot = new Vector2(0.5f, 0.5f);
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
		// Every step runs even if one throws, so a half-destroyed scene can't leave the rest restyled.
		Exception? failure = null;
		foreach (Icon icon in icons.Values)
		{
			if (icon.Root == null)
			{
				continue;
			}
			Ui.Step(ref failure, () =>
			{
				icon.Root.localScale = icon.Scale;
				icon.Shade.SetActive(icon.ShadeActive);
				icon.Outline.sprite = icon.OutlineSprite;
				icon.ArtImage.sprite = icon.ArtSprite;
				icon.ArtImage.preserveAspect = icon.ArtKeepAspect;
				icon.ArtRect.Apply(icon.Art);
				icon.Art.parent.localScale = Vector3.one;
				icon.BadgeImage.sprite = icon.BadgeSprite;
				icon.BadgeImage.type = icon.BadgeType;
				icon.BadgeRect.Apply(icon.Badge);
				icon.Badge.localScale = Vector3.one;
				icon.BadgeText.fontSize = icon.BadgeFontSize;
				icon.Label.fontSize = icon.LabelFontSize;
				icon.Label.color = icon.LabelColor;
				icon.Label.resizeTextForBestFit = icon.LabelBestFit;
				icon.Label.horizontalOverflow = icon.LabelOverflow;
			});
			Ui.Step(ref failure, () => UnityEngine.Object.DestroyImmediate(icon.Group));
			Ui.Step(ref failure, () => UnityEngine.Object.Destroy(icon.Glass.gameObject));
			Ui.Step(ref failure, () => UnityEngine.Object.Destroy(icon.Key.gameObject));
		}
		icons.Clear();
		if (grid != null && gridRect != null)
		{
			Ui.Step(ref failure, () =>
			{
				grid.cellSize = gridCell;
				grid.spacing = gridSpacing;
				grid.constraintCount = gridColumns;
				gridRect.anchoredPosition = gridPosition;
			});
		}
		if (background != null)
		{
			Ui.Step(ref failure, () => background.color = backgroundColor);
		}
		if (guilloche != null)
		{
			Ui.Step(ref failure, () => guilloche.SetActive(guillocheActive));
		}
		if (barImage != null)
		{
			Ui.Step(ref failure, () => barImage.color = barColor);
		}
		if (barShadow != null)
		{
			Ui.Step(ref failure, () => barShadow.SetActive(barShadowActive));
		}
		if (timeText != null)
		{
			Ui.Step(ref failure, () =>
			{
				timeText.color = timeColor;
				timeText.fontSize = timeSize;
				timeText.enabled = timeEnabled;
			});
		}
		if (dataText != null)
		{
			Ui.Step(ref failure, () =>
			{
				dataText.color = dataColor;
				dataText.fontSize = dataSize;
				dataText.rectTransform.anchoredPosition = dataPosition;
			});
		}
		foreach ((MeshRenderer renderer, bool enabled) in hidden)
		{
			Ui.Step(ref failure, () =>
			{
				if (renderer != null)
				{
					renderer.enabled = enabled;
				}
			});
		}
		hidden.Clear();
		foreach ((Collider collider, bool enabled) in colliders)
		{
			Ui.Step(ref failure, () =>
			{
				if (collider != null)
				{
					collider.enabled = enabled;
				}
			});
		}
		colliders.Clear();
		foreach ((CanvasScaler scaler, float pixels) in scalers)
		{
			Ui.Step(ref failure, () =>
			{
				if (scaler != null)
				{
					scaler.dynamicPixelsPerUnit = pixels;
				}
			});
		}
		scalers.Clear();
		foreach (RectTransform? added in new[] { wallpaper, card, wake, onlineDot, frame, backdrop?.rectTransform })
		{
			if (added != null)
			{
				Ui.Step(ref failure, () => UnityEngine.Object.Destroy(added.gameObject));
			}
		}
		Ui.Rethrow(failure);
	}
}
