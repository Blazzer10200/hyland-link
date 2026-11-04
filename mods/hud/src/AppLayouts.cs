using System;
using System.Collections.Generic;
using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ContactsScreen = Il2CppScheduleOne.UI.Phone.ContactsApp.ContactsApp;
using DealersScreen = Il2CppScheduleOne.UI.Phone.Messages.DealerManagementApp;
using DeliveriesScreen = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryApp;
using JournalScreen = Il2CppScheduleOne.UI.Phone.JournalApp;
using MapScreen = Il2CppScheduleOne.UI.Phone.Map.MapApp;
using MessagesScreen = Il2CppScheduleOne.UI.Phone.Messages.MessagesApp;
using ProductsScreen = Il2CppScheduleOne.UI.Phone.ProductManagerApp.ProductManagerApp;

namespace HylandHud;

/// <summary>
/// The board's frame around the game's own apps, built by moving the game's pieces (so the game's logic keeps driving
/// them) rather than copying them. Every app gets the board's header (a round back button that closes the app the game's
/// way, and the title), the board's insets (16 at the sides, 12 at the bottom) and a backdrop behind it all, and its own
/// title bar goes. Per app: Deliveries' tabs move from the bottom to the top, the map sits in a rounded card, Dealers
/// loses the padding its big title needed, and Messages gets the header on its list only (a chat keeps the game's bar
/// with the face and the name). Each app is laid out the first time it opens; every change is undone in
/// <see cref="Dispose"/>, in reverse.
/// </summary>
internal sealed class AppLayouts : IDisposable
{
	/// <summary>Board sizes × this (the board's screen is 316 wide upright, 316 tall sideways).</summary>
	private const float Unit = 655f / 316f;

	private const float Pad = 16f * Unit;

	/// <summary>The board's header: 14 (sideways) or 16 (upright) above a 36 back button, 10 below.</summary>
	private const float WideHeader = (14f + 36f + 10f) * Unit;

	private const float TallHeader = (16f + 36f + 10f) * Unit;

	internal const string Prefix = "Hyland ";

	private readonly Sprites sprites;

	private readonly Font font;

	private readonly Action<string> log;

	private readonly List<Action> undo = new List<Action>();

	private readonly HashSet<int> done = new HashSet<int>();

	private readonly HomeScreen home;

	/// <summary>The game's apps in the order they made their home-screen icons (home.appIcons): each zooms out of its own.</summary>
	private static readonly string[] IconOrder = { "Messages", "Journal", "ProductManagerApp", "ContactsApp", "MapApp", "DealerManagement", "DeliveryApp" };

	private GameObject? shown;

	private RectTransform? zooming;

	private CanvasGroup? zoomGroup;

	private Vector3 zoomFrom;

	private Vector3 zoomRest;

	private float zoomAt;

	private Action? closeAfterZoom;

	internal AppLayouts(HomeScreen home, Sprites sprites, Action<string> log)
	{
		this.home = home;
		this.sprites = sprites;
		this.log = log;
		font = home.timeText.font;
	}

	internal void Update()
	{
		GameObject? app = Phone.ActiveApp;
		if (app != null && app.name != HylandApp.RootName && done.Add(app.GetInstanceID()))
		{
			LayOut(app);
		}
		if (app != shown)
		{
			shown = app;
			EndZoom();
			if (app != null && Array.IndexOf(IconOrder, app.name) >= 0)
			{
				StartZoom(app);
			}
		}
		Animate();
	}

	/// <summary>The board's motion for the game's apps: the screen zooms out of its icon (0.34 s), like the Hyland app.</summary>
	private void StartZoom(GameObject app)
	{
		if (!Prepare(app))
		{
			return;
		}
		zoomAt = Time.unscaledTime;
		closeAfterZoom = null;
		Animate();
	}

	/// <summary>Picks up the app's container for a zoom: where it rests and where its icon is.</summary>
	private bool Prepare(GameObject app)
	{
		Transform? container = app.transform.Find("Container");
		if (container == null)
		{
			return false;
		}
		RectTransform rect = container.Cast<RectTransform>();
		zoomGroup = container.GetComponent<CanvasGroup>();
		if (zoomGroup == null)
		{
			CanvasGroup added = container.gameObject.AddComponent<CanvasGroup>();
			zoomGroup = added;
			undo.Add(() =>
			{
				if (added != null)
				{
					UnityEngine.Object.Destroy(added);
				}
			});
		}
		zooming = rect;
		zoomRest = rect.localPosition;
		zoomFrom = zoomRest;
		int index = Array.IndexOf(IconOrder, app.name);
		Il2CppSystem.Collections.Generic.List<Button> icons = home.appIcons;
		if (index >= 0 && icons != null && index < icons.Count && icons[index] != null && rect.parent != null)
		{
			zoomFrom = rect.parent.InverseTransformPoint(icons[index].transform.position);
			zoomFrom.z = zoomRest.z;
		}
		return true;
	}

	/// <summary>The back button: the app shrinks into its icon over the home screen (0.3 s), then the game closes it.
	/// Escape and the phone's button close it the game's way, at once.</summary>
	private void ZoomClosed(Action close)
	{
		// The open zoom is over by now (it lets go of the container when it ends), so this one picks it up again.
		if (closeAfterZoom != null || shown == null || (zooming == null && !Prepare(shown)))
		{
			close();
			return;
		}
		closeAfterZoom = close;
		zoomAt = Time.unscaledTime;
		home.SetIsOpen(true);
	}

	private void Animate()
	{
		if (zooming == null || zoomGroup == null)
		{
			return;
		}
		float t = Time.unscaledTime - zoomAt;
		bool closing = closeAfterZoom != null;
		float k = closing ? 1f - Motion.EaseOutCubic(t / 0.3f) : Motion.EaseOutCubic(t / 0.34f);
		float scale = Mathf.Lerp(0.12f, 1f, k);
		zooming.localScale = new Vector3(scale, scale, 1f);
		zooming.localPosition = Vector3.Lerp(zoomFrom, zoomRest, k);
		zoomGroup.alpha = k;
		if (closing && t >= 0.3f)
		{
			Action close = closeAfterZoom!;
			EndZoom();
			close();
		}
		else if (!closing && t >= 0.34f)
		{
			EndZoom();
		}
	}

	/// <summary>Puts the app back exactly at rest (full size, in place, opaque).</summary>
	private void EndZoom()
	{
		if (zooming != null)
		{
			zooming.localScale = Vector3.one;
			zooming.localPosition = zoomRest;
		}
		if (zoomGroup != null)
		{
			zoomGroup.alpha = 1f;
		}
		zooming = null;
		zoomGroup = null;
		closeAfterZoom = null;
	}

	private void LayOut(GameObject app)
	{
		RectTransform root = app.GetComponent<RectTransform>();
		Transform? container = app.transform.Find("Container");
		if (root == null || container == null)
		{
			return;
		}
		RectTransform box = container.Cast<RectTransform>();
		switch (app.name)
		{
		case "Journal":
			Frame(root, box, "Journal", wide: true, () => app.GetComponent<JournalScreen>().SetOpen(false));
			Hide(box, "Topbar");
			// Their tops sat under the 60 tall bar that's gone.
			Top(box, "Tasks", 0f);
			Top(box, "Details", 0f);
			break;
		case "ProductManagerApp":
			Frame(root, box, "My Products", wide: true, () => app.GetComponent<ProductsScreen>().SetOpen(false));
			// A layout child: the rows below move up into its place.
			Hide(box, "Header");
			break;
		case "ContactsApp":
			Frame(root, box, "Contacts", wide: true, () => app.GetComponent<ContactsScreen>().SetOpen(false));
			break;
		case "MapApp":
			Frame(root, box, "Map", wide: true, () => app.GetComponent<MapScreen>().SetOpen(false));
			Hide(box, "Topbar");
			Top(box, "Scroll View", 0f);
			RoundMask(box, "Scroll View/Viewport", 13f * Unit);
			break;
		case "DeliveryApp":
			Frame(root, box, "Deliveries", wide: true, () => app.GetComponent<DeliveriesScreen>().SetOpen(false));
			TabsOnTop(box);
			break;
		case "DealerManagement":
			Frame(root, box, "Dealer Management", wide: false, () => app.GetComponent<DealersScreen>().SetOpen(false));
			Hide(box, "Background/Title");
			Padding(box, "Background/Content/Container", 0, 0, 8, 20);
			break;
		case "Messages":
			MessagesList(app, box);
			break;
		default:
			// Another mod's app: left as it is.
			return;
		}
		log($"Phone: {app.name} laid out like the board.");
	}

	/// <summary>
	/// The backdrop, the header and the insets: the game's container moves in under the header. Both pieces live inside
	/// the container and reach out past its edges, because the game closes an app by switching its container off (the
	/// root stays on): on the root they'd stay up over the next app.
	/// </summary>
	private void Frame(RectTransform root, RectTransform box, string title, bool wide, Action close)
	{
		float header = wide ? WideHeader : TallHeader, bottom = 12f * Unit;
		Inset(box, Pad, bottom, Pad, header);
		Image backdrop = Ui.Image(Prefix + "backdrop", box, null!, Ui.Color(Theme.PhoneApp));
		backdrop.raycastTarget = false;
		Free(backdrop.gameObject);
		RectTransform fill = backdrop.rectTransform;
		fill.anchorMin = Vector2.zero;
		fill.anchorMax = Vector2.one;
		fill.offsetMin = new Vector2(-Pad, -bottom);
		fill.offsetMax = new Vector2(Pad, header);
		fill.SetAsFirstSibling();
		undo.Add(() => Destroy(backdrop));
		RectTransform bar = Header(box, title, wide, close);
		// Above the container's top edge, as wide as the screen.
		bar.pivot = new Vector2(0.5f, 0f);
		bar.offsetMin = new Vector2(-Pad, 0f);
		bar.offsetMax = new Vector2(Pad, header);
		undo.Add(() => Destroy(bar));
	}

	/// <summary>Keeps one of ours out of the game's layout groups (Products' and Deliveries' containers are ones).</summary>
	private static void Free(GameObject item)
	{
		item.AddComponent<LayoutElement>().ignoreLayout = true;
	}

	/// <summary>The board's header, along the top of <paramref name="parent"/>: the back button and the title.</summary>
	private RectTransform Header(Transform parent, string title, bool wide, Action close)
	{
		float top = (wide ? 14f : 16f) * Unit, left = (wide ? 18f : 16f) * Unit, size = 36f * Unit;
		RectTransform bar = Ui.Rect(Prefix + "header", parent);
		Free(bar.gameObject);
		bar.anchorMin = new Vector2(0f, 1f);
		bar.anchorMax = Vector2.one;
		bar.pivot = new Vector2(0.5f, 1f);
		bar.anchoredPosition = Vector2.zero;
		bar.sizeDelta = new Vector2(0f, top + size + 10f * Unit);
		bar.SetAsLastSibling();
		Image back = Ui.Image("Back", bar, sprites.Get("backButton", () => Art.Glass(18 * Unit, Theme.PhoneChip, Theme.PhoneStroke, Art.PhoneEdge), mipmaps: true), Color.white, sliced: true);
		back.raycastTarget = true;
		TopLeft(back.rectTransform, new Vector2(left, -top), new Vector2(size, size));
		Button button = back.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener((UnityAction)(() => ZoomClosed(close)));
		Image arrow = Ui.Image("Arrow", back.rectTransform, sprites.Glyph(Art.Icon.Back), Color.white);
		arrow.raycastTarget = false;
		arrow.rectTransform.sizeDelta = new Vector2(18f * Unit, 18f * Unit);
		RectTransform name = Ui.Rect("Title", bar);
		Text text = name.gameObject.AddComponent<Text>();
		text.font = font;
		text.fontSize = Mathf.RoundToInt(20f * Unit);
		text.color = Ui.Color(Theme.PhoneText);
		text.alignment = TextAnchor.MiddleLeft;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		text.text = title;
		TopLeft(name, new Vector2(left + size + 10f * Unit, -top), new Vector2(600f, size));
		return bar;
	}

	/// <summary>Messages: the header replaces the list's big "Messages" title, so it's on the list only; a chat keeps the
	/// game's own bar (the face, the name, its back arrow).</summary>
	private void MessagesList(GameObject app, RectTransform box)
	{
		Transform? home = box.Find("Home");
		if (home == null)
		{
			return;
		}
		Hide(box, "Home/Header");
		Top(box, "Home/Scroll View", TallHeader);
		// Messages has no inset frame (the chat needs the full screen): the list stops above the home bar instead of
		// running under it.
		Transform? list = box.Find("Home/Scroll View");
		if (list != null)
		{
			RectTransform rect = list.Cast<RectTransform>();
			Keep(rect);
			rect.offsetMin = new Vector2(rect.offsetMin.x, 24f * Unit);
		}
		RectTransform bar = Header(home, "Messages", wide: false, () => app.GetComponent<MessagesScreen>().SetOpen(false));
		undo.Add(() => Destroy(bar));
	}

	/// <summary>Deliveries: the board's tabs sit on top, under the header. The container is a vertical layout, so the
	/// tab bar just moves ahead of the contents; the game's tab logic doesn't notice.</summary>
	private void TabsOnTop(RectTransform box)
	{
		Transform? bar = box.Find("NavigationBar");
		Transform? contents = box.Find("Contents");
		if (bar == null || contents == null)
		{
			return;
		}
		int was = bar.GetSiblingIndex();
		bar.SetSiblingIndex(contents.GetSiblingIndex());
		undo.Add(() =>
		{
			if (bar != null)
			{
				bar.SetSiblingIndex(was);
			}
		});
		// Gamepad bumper hints sat just above the bar; on top they'd cover the first row.
		Hide(bar, "Left bumper prompt");
		Hide(bar, "Right bumper prompt");
		LayoutRebuilder.MarkLayoutForRebuild(box);
	}

	private void Hide(Transform parent, string path)
	{
		Transform? target = parent.Find(path);
		if (target == null || !target.gameObject.activeSelf)
		{
			return;
		}
		GameObject item = target.gameObject;
		item.SetActive(false);
		undo.Add(() =>
		{
			if (item != null)
			{
				item.SetActive(true);
			}
		});
	}

	/// <summary>Moves a stretched rect's top edge to <paramref name="top"/> below its parent's.</summary>
	private void Top(Transform parent, string path, float top)
	{
		Transform? target = parent.Find(path);
		if (target == null)
		{
			return;
		}
		RectTransform rect = target.Cast<RectTransform>();
		Keep(rect);
		rect.offsetMax = new Vector2(rect.offsetMax.x, -top);
	}

	private void Inset(RectTransform rect, float left, float bottom, float right, float top)
	{
		Keep(rect);
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(left, bottom);
		rect.offsetMax = new Vector2(-right, -top);
	}

	/// <summary>Rounds a mask: its image gets a rounded white panel (the mask reads only its shape, it's never drawn).</summary>
	private void RoundMask(Transform parent, string path, float radius)
	{
		Transform? target = parent.Find(path);
		Image? image = target != null ? target.GetComponent<Image>() : null;
		if (image == null || target!.GetComponent<Mask>() == null)
		{
			return;
		}
		Sprite sprite = image.sprite;
		Image.Type type = image.type;
		image.sprite = sprites.Pill(radius, Theme.White);
		image.type = Image.Type.Sliced;
		undo.Add(() =>
		{
			if (image != null)
			{
				image.sprite = sprite;
				image.type = type;
			}
		});
	}

	private void Padding(Transform parent, string path, int left, int right, int top, int bottom)
	{
		Transform? target = parent.Find(path);
		LayoutGroup? group = target != null ? target.GetComponent<LayoutGroup>() : null;
		if (group == null)
		{
			return;
		}
		RectOffset padding = group.padding;
		int l = padding.left, r = padding.right, t = padding.top, b = padding.bottom;
		group.padding = new RectOffset(left, right, top, bottom);
		LayoutRebuilder.MarkLayoutForRebuild(group.transform.Cast<RectTransform>());
		undo.Add(() =>
		{
			if (group != null)
			{
				group.padding = new RectOffset(l, r, t, b);
			}
		});
	}

	private void Keep(RectTransform rect)
	{
		Vector2 anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot, position = rect.anchoredPosition, size = rect.sizeDelta;
		undo.Add(() =>
		{
			if (rect != null)
			{
				rect.anchorMin = anchorMin;
				rect.anchorMax = anchorMax;
				rect.pivot = pivot;
				rect.anchoredPosition = position;
				rect.sizeDelta = size;
			}
		});
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

	private static void Destroy(Component component)
	{
		if (component != null)
		{
			UnityEngine.Object.Destroy(component.gameObject);
		}
	}

	public void Dispose()
	{
		Exception? failure = null;
		Ui.Step(ref failure, EndZoom);
		shown = null;
		for (int i = undo.Count - 1; i >= 0; i--)
		{
			Action step = undo[i];
			Ui.Step(ref failure, step);
		}
		undo.Clear();
		done.Clear();
		Ui.Rethrow(failure);
	}
}
