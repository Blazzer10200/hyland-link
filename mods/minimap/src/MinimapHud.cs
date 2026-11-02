using System;
using System.Collections.Generic;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone.Map;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HylandMinimap;

internal sealed class MinimapHud : IDisposable
{
	private readonly MapApp source;

	private readonly GameObject root;

	private readonly RectTransform frame;

	private readonly RectTransform outline;

	private readonly RectTransform viewport;

	private readonly RectTransform mapLayer;

	private readonly Image maskImage;

	private readonly Image mapImage;

	private readonly RectTransform arrow;

	private readonly CanvasGroup group;

	private readonly Sprite circleSprite;

	private readonly Sprite arrowSprite;

	private readonly Texture2D circleTexture;

	private readonly Texture2D arrowTexture;

	private readonly Canvas canvas;

	private readonly RectTransform rim;

	private readonly Image rimImage;

	private readonly Action<string> log;

	private Texture2D? rimTexture;

	private Sprite? rimSprite;

	private float rimScale;

	private float borderWidth;

	private float outlineWidth;

	private Color borderColor;

	private Color outlineColor;

	private bool loggedMapArt;

	private readonly CustomerMarkers customers;

	private readonly WorldMarkers worldMarkers;

	private readonly System.Collections.Generic.Dictionary<int, (Player Player, RectTransform Marker)> peers = new System.Collections.Generic.Dictionary<int, (Player, RectTransform)>();

	private readonly HashSet<int> livePeerIds = new HashSet<int>();

	private readonly System.Collections.Generic.List<int> stalePeerIds = new System.Collections.Generic.List<int>();

	private float nextPeerRefresh;

	private float size;

	private float zoom;

	private bool circular;

	private bool disposed;

	internal bool IsAlive
	{
		get
		{
			if (root != null)
			{
				return !disposed;
			}
			return false;
		}
	}

	internal bool IsBoundTo(MapApp app)
	{
		return source == app;
	}

	internal MinimapHud(MapApp app, Action<string> log)
	{
		source = app;
		this.log = log;
		root = new GameObject("Hyland Minimap HUD");
		try
		{
			root.AddComponent<RectTransform>();
			canvas = root.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 5;
			UnityEngine.UI.CanvasScaler canvasScaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
			canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
			canvasScaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			canvasScaler.matchWidthOrHeight = 0.5f;
			group = root.AddComponent<CanvasGroup>();
			group.interactable = false;
			group.blocksRaycasts = false;
			(Texture2D, Sprite) tuple = MakeMask(256, Shapes.InCircle);
			circleTexture = tuple.Item1;
			circleSprite = tuple.Item2;
			tuple = MakeMask(128, Shapes.InArrow);
			arrowTexture = tuple.Item1;
			arrowSprite = tuple.Item2;
			outline = NewRect("Outer outline", root.transform);
			outline.gameObject.AddComponent<Image>().raycastTarget = false;
			frame = NewRect("Frame", root.transform);
			Image image = frame.gameObject.AddComponent<Image>();
			image.raycastTarget = false;
			image.color = new Color(0.06f, 0.075f, 0.09f, 0.98f);
			viewport = NewRect("Viewport", root.transform);
			maskImage = viewport.gameObject.AddComponent<Image>();
			maskImage.raycastTarget = false;
			maskImage.color = Color.white;
			viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
			// Drawn over the map: the Mask's stencil edge can't be smoothed, so a smooth rim covers it (Shapes.RimPixel).
			rim = NewRect("Rim", root.transform);
			rimImage = rim.gameObject.AddComponent<Image>();
			rimImage.raycastTarget = false;
			RectTransform rectTransform = NewRect("Backdrop", viewport);
			Stretch(rectTransform);
			Image image2 = rectTransform.gameObject.AddComponent<Image>();
			image2.raycastTarget = false;
			image2.color = new Color(0.08f, 0.1f, 0.12f, 1f);
			mapLayer = NewRect("Map rotation", viewport);
			mapImage = NewRect("Native map", mapLayer).gameObject.AddComponent<Image>();
			mapImage.raycastTarget = false;
			mapImage.color = Color.white;
			mapImage.type = Image.Type.Simple;
			mapImage.preserveAspect = false;
			RectTransform parent = NewRect("Customers", viewport);
			Font font = null;
			if (source.LabelGroup != null)
			{
				font = source.LabelGroup.GetComponentInChildren<Text>(includeInactive: true)?.font;
			}
			HUD instance = Singleton<HUD>.Instance;
			if (font == null && instance != null && instance.fpsLabel != null)
			{
				font = instance.fpsLabel.font;
			}
			if (font == null)
			{
				font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			}
			customers = new CustomerMarkers(parent, circleSprite, font);
			worldMarkers = new WorldMarkers(viewport, circleSprite, font);
			worldMarkers.PlaceVehiclesBehind(parent);
			arrow = NewRect("You", viewport);
			arrow.sizeDelta = new Vector2(24f, 24f);
			Image image3 = arrow.gameObject.AddComponent<Image>();
			image3.sprite = arrowSprite;
			image3.color = new Color(1f, 0.9f, 0.32f, 1f);
			image3.raycastTarget = false;
			root.SetActive(value: false);
		}
		catch
		{
			UnityEngine.Object.Destroy(root);
			if (customers != null)
			{
				customers.Dispose();
			}
			if (worldMarkers != null)
			{
				worldMarkers.Dispose();
			}
			if (circleSprite != null)
			{
				UnityEngine.Object.Destroy(circleSprite);
			}
			if (arrowSprite != null)
			{
				UnityEngine.Object.Destroy(arrowSprite);
			}
			if (circleTexture != null)
			{
				UnityEngine.Object.Destroy(circleTexture);
			}
			if (arrowTexture != null)
			{
				UnityEngine.Object.Destroy(arrowTexture);
			}
			throw;
		}
	}

	internal void Apply(Settings settings)
	{
		size = (float)MapMath.ClampFinite(settings.Size.Value, 120.0, 480.0, 240.0);
		zoom = (float)MapMath.ClampZoom(settings.Zoom.Value);
		group.alpha = (float)MapMath.ClampFinite(settings.Opacity.Value, 0.1, 1.0, 0.95);
		circular = settings.Shape.Value != MapShape.Square;
		maskImage.sprite = (circular ? circleSprite : null);
		Image component = frame.GetComponent<Image>();
		Image component2 = outline.GetComponent<Image>();
		borderColor = ClampColor(settings.BorderColor.Value, new Color(0.18f, 0.22f, 0.27f, 1f));
		outlineColor = ClampColor(settings.OutlineColor.Value, new Color(0.02f, 0.025f, 0.03f, 1f));
		component.color = borderColor;
		component2.color = outlineColor;
		float num = (settings.BorderEnabled.Value ? ((float)MapMath.ClampFinite(settings.BorderWidth.Value, 0.0, 24.0, 4.0)) : 0f);
		float num2 = (settings.OutlineEnabled.Value ? ((float)MapMath.ClampFinite(settings.OutlineWidth.Value, 0.0, 12.0, 2.0)) : 0f);
		borderWidth = num;
		outlineWidth = num2;
		// A circle gets the rim instead of the plain discs behind it; the square's straight edges are already clean.
		frame.gameObject.SetActive(!circular && num > 0f);
		outline.gameObject.SetActive(!circular && num2 > 0f);
		rim.gameObject.SetActive(circular);
		MapCorner mapCorner = (Enum.IsDefined(typeof(MapCorner), settings.Corner.Value) ? settings.Corner.Value : MapCorner.TopRight);
		bool flag = mapCorner == MapCorner.TopRight || mapCorner == MapCorner.BottomRight;
		bool flag2 = mapCorner == MapCorner.TopRight || mapCorner == MapCorner.TopLeft;
		Vector2 anchor = new Vector2(flag ? 1 : 0, flag2 ? 1 : 0);
		float num3 = (float)MapMath.ClampFinite(settings.Margin.Value, 0.0, 200.0, 24.0) + num + num2 + size * 0.5f;
		Vector2 position = new Vector2(flag ? (0f - num3) : num3, flag2 ? (0f - num3) : num3);
		position += new Vector2((float)MapMath.ClampFinite(settings.OffsetX.Value, -1000.0, 1000.0, 0.0), (float)MapMath.ClampFinite(settings.OffsetY.Value, -1000.0, 1000.0, 0.0));
		Place(viewport, anchor, position, size);
		Place(frame, anchor, position, size + 2f * num);
		Place(outline, anchor, position, size + 2f * (num + num2));
		Place(rim, anchor, position, size + 2f * RimBand());
		rimScale = 0f;
		RebuildRim();
		nextPeerRefresh = 0f;
		customers.Apply(settings);
		worldMarkers.Apply(settings);
	}

	internal void SetVisible(bool visible)
	{
		if (root.activeSelf != visible)
		{
			root.SetActive(visible);
		}
	}

	internal void Update(Player player, PlayerCamera? camera, MapPositionUtility projection, Settings settings)
	{
		Image backgroundImage = source.BackgroundImage;
		Sprite sprite;
		if (backgroundImage.overrideSprite != null)
		{
			sprite = backgroundImage.overrideSprite;
		}
		else
		{
			sprite = ((backgroundImage.sprite != null) ? backgroundImage.sprite : source.MainMapSprite);
		}
		if (sprite == null)
		{
			SetVisible(visible: false);
			return;
		}
		RectTransform rectTransform = backgroundImage.rectTransform;
		Rect drawingRect = DrawingRect(rectTransform.rect, rectTransform.pivot, sprite, backgroundImage.preserveAspect);
		if (drawingRect.width <= 0f || drawingRect.height <= 0f)
		{
			SetVisible(visible: false);
			return;
		}
		float scale = HudScale();
		RebuildRim();
		// Zoom 1 = one pixel of the game's map art per screen pixel: the sharpest that art can look. Past that it only blurs.
		float z = zoom * sprite.rect.width / (drawingRect.width * scale);
		if (!float.IsFinite(z) || z <= 0f)
		{
			z = zoom;
		}
		if (!loggedMapArt)
		{
			loggedMapArt = true;
			Texture2D texture = sprite.texture;
			log($"Map art {sprite.rect.width:F0}x{sprite.rect.height:F0} px (texture {texture?.width}x{texture?.height}, {texture?.filterMode}, {texture?.mipmapCount} mips), drawn at {drawingRect.width:F0} units, HUD scale {scale:F2}: zoom 1 = {z / zoom:F3}x the phone map.");
		}
		mapImage.sprite = sprite;
		mapImage.rectTransform.sizeDelta = new Vector2(drawingRect.width, drawingRect.height);
		mapImage.rectTransform.localScale = new Vector3(z, z, 1f);
		RectTransform rectTransform2 = ((player.PoI != null) ? player.PoI.UI : null);
		Transform parent;
		Vector3 anchorOffset;
		if (rectTransform2 != null && rectTransform2.parent != null)
		{
			parent = rectTransform2.parent;
			anchorOffset = rectTransform2.localPosition - new Vector3(rectTransform2.anchoredPosition.x, rectTransform2.anchoredPosition.y, 0f);
		}
		else
		{
			parent = source.PoIContainer;
			Vector2 center = source.PoIContainer.rect.center;
			anchorOffset = new Vector3(center.x, center.y, 0f);
		}
		MapProjection projection2 = new MapProjection(projection, parent, anchorOffset, rectTransform, drawingRect);
		Transform transform = ((player.IsInVehicle && player.CurrentVehicle != null) ? player.CurrentVehicle.transform : player.transform);
		Vector3 position = transform.position;
		Vector2 vector = projection2.Project(position);
		Vector3 vector2 = ((camera != null && camera.Camera != null) ? camera.Camera.transform.forward : transform.forward);
		vector2.y = 0f;
		if (vector2.sqrMagnitude < 0.001f)
		{
			vector2 = transform.forward;
		}
		Vector2 vector3 = projection2.Project(position + vector2) - vector;
		float num = (float)MapMath.Heading(vector3.x, vector3.y);
		float num2 = (settings.Rotate.Value ? num : 0f);
		mapLayer.localRotation = Quaternion.Euler(0f, 0f, num2);
		mapImage.rectTransform.anchoredPosition = -vector * z;
		arrow.localRotation = Quaternion.Euler(0f, 0f, num2 - num);
		worldMarkers.RefreshSources();
		customers.Update(settings, projection2, vector, z, num2, size, circular, worldMarkers.DealerNpcIds);
		worldMarkers.Update(player, settings.ShowCoop.Value, projection2, vector, z, num2, size, circular, customers.OccupiedLabels);
		RefreshPeers(player, settings.ShowCoop.Value);
		foreach (var value in peers.Values)
		{
			var (player2, _) = value;
			if (player2 == null)
			{
				value.Marker.gameObject.SetActive(value: false);
				continue;
			}
			Transform transform2 = ((player2.IsInVehicle && player2.CurrentVehicle != null) ? player2.CurrentVehicle.transform : player2.transform);
			Vector2 vector4 = projection2.Project(transform2.position);
			(double, double) tuple2 = MapMath.RelativePoint(vector4.x, vector4.y, vector.x, vector.y, z, num2);
			bool flag = MapMath.IsInside(tuple2.Item1, tuple2.Item2, size / 2f - 7f, circular);
			value.Marker.gameObject.SetActive(flag);
			if (flag)
			{
				value.Marker.anchoredPosition = new Vector2((float)tuple2.Item1, (float)tuple2.Item2);
			}
		}
	}

	private void RefreshPeers(Player local, bool show)
	{
		if (!show)
		{
			if (peers.Count > 0)
			{
				ClearPeers();
			}
		}
		else
		{
			if (Time.unscaledTime < nextPeerRefresh)
			{
				return;
			}
			nextPeerRefresh = Time.unscaledTime + 1f;
			livePeerIds.Clear();
			Il2CppSystem.Collections.Generic.List<Player> playerList = Player.PlayerList;
			if (playerList != null)
			{
				for (int i = 0; i < playerList.Count; i++)
				{
					Player player = playerList[i];
					if (!(player == null) && !(player == local))
					{
						int instanceID = player.GetInstanceID();
						livePeerIds.Add(instanceID);
						if (!peers.ContainsKey(instanceID))
						{
							RectTransform rectTransform = NewRect("Co-op player", viewport);
							rectTransform.sizeDelta = new Vector2(12f, 12f);
							Image image = rectTransform.gameObject.AddComponent<Image>();
							image.sprite = circleSprite;
							image.color = new Color(0.2f, 0.87f, 1f, 1f);
							image.raycastTarget = false;
							peers.Add(instanceID, (player, rectTransform));
						}
					}
				}
			}
			arrow.SetAsLastSibling();
			stalePeerIds.Clear();
			foreach (int key in peers.Keys)
			{
				if (!livePeerIds.Contains(key))
				{
					stalePeerIds.Add(key);
				}
			}
			foreach (int stalePeerId in stalePeerIds)
			{
				UnityEngine.Object.Destroy(peers[stalePeerId].Marker.gameObject);
				peers.Remove(stalePeerId);
			}
		}
	}

	private static Rect DrawingRect(Rect rect, Vector2 pivot, Sprite sprite, bool preserveAspect)
	{
		if (!preserveAspect || sprite.rect.height <= 0f || sprite.rect.width <= 0f)
		{
			return rect;
		}
		float num = sprite.rect.width / sprite.rect.height;
		(double, double, double, double) tuple = MapMath.FitAspect(rect.x, rect.y, rect.width, rect.height, num, pivot.x, pivot.y);
		return new Rect((float)tuple.Item1, (float)tuple.Item2, (float)tuple.Item3, (float)tuple.Item4);
	}

	private static Color ClampColor(Color color, Color fallback)
	{
		return new Color((float)MapMath.ClampFinite(color.r, 0.0, 1.0, fallback.r), (float)MapMath.ClampFinite(color.g, 0.0, 1.0, fallback.g), (float)MapMath.ClampFinite(color.b, 0.0, 1.0, fallback.b), (float)MapMath.ClampFinite(color.a, 0.0, 1.0, fallback.a));
	}

	private static RectTransform NewRect(string name, Transform parent)
	{
		RectTransform rectTransform = new GameObject(name).AddComponent<RectTransform>();
		rectTransform.SetParent(parent, worldPositionStays: false);
		Vector2 vector = (rectTransform.pivot = new Vector2(0.5f, 0.5f));
		Vector2 anchorMin = (rectTransform.anchorMax = vector);
		rectTransform.anchorMin = anchorMin;
		rectTransform.sizeDelta = Vector2.zero;
		return rectTransform;
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		Vector2 offsetMin = (rect.offsetMax = Vector2.zero);
		rect.offsetMin = offsetMin;
	}

	private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, float diameter)
	{
		Vector2 anchorMin = (rect.anchorMax = anchor);
		rect.anchorMin = anchorMin;
		rect.anchoredPosition = position;
		rect.sizeDelta = new Vector2(diameter, diameter);
	}

	/// <summary>Thickness of the rim outside the map, in HUD units. At least 1, so the map's hard edge is always covered.</summary>
	private float RimBand()
	{
		return Math.Max(borderWidth + outlineWidth, 1f);
	}

	private float HudScale()
	{
		float scaleFactor = canvas.scaleFactor;
		return (float.IsFinite(scaleFactor) && scaleFactor > 0f) ? scaleFactor : 1f;
	}

	/// <summary>Bakes the border and outline into one ring texture at its real on-screen pixel size (so each edge ramp is
	/// one screen pixel), and again whenever the HUD scale changes, e.g. after a resolution change.</summary>
	private void RebuildRim()
	{
		float scale = HudScale();
		if (!circular || (rimSprite != null && Math.Abs(scale - rimScale) < 0.001f))
		{
			return;
		}
		rimScale = scale;
		float band = RimBand();
		float diameter = size + 2f * band;
		int px = Math.Clamp((int)Math.Ceiling(diameter * scale), 64, 2048);
		double perUnit = px / (double)diameter;
		double mapR = size * 0.5 * perUnit;
		double borderR = (size * 0.5 + borderWidth) * perUnit;
		double outerR = (size * 0.5 + band) * perUnit;
		// A missing layer takes the other one's color, so the band never shows a color that's switched off.
		Rgba outlineRgba = ToRgba((outlineWidth > 0f || borderWidth <= 0f) ? outlineColor : borderColor);
		Rgba borderRgba = ((borderWidth > 0f) ? ToRgba(borderColor) : outlineRgba);
		Color[] pixels = new Color[px * px];
		double center = px * 0.5;
		for (int y = 0; y < px; y++)
		{
			for (int x = 0; x < px; x++)
			{
				double dx = x + 0.5 - center;
				double dy = y + 0.5 - center;
				Rgba p = Shapes.RimPixel(Math.Sqrt(dx * dx + dy * dy), mapR, borderR, outerR, borderRgba, outlineRgba);
				pixels[y * px + x] = new Color(p.R, p.G, p.B, p.A);
			}
		}
		Texture2D texture = new Texture2D(px, px, TextureFormat.RGBA32, mipChain: false);
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.filterMode = FilterMode.Bilinear;
		texture.SetPixels(pixels);
		texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, px, px), new Vector2(0.5f, 0.5f), 100f);
		rimImage.sprite = sprite;
		if (rimSprite != null)
		{
			UnityEngine.Object.Destroy(rimSprite);
		}
		if (rimTexture != null)
		{
			UnityEngine.Object.Destroy(rimTexture);
		}
		rimSprite = sprite;
		rimTexture = texture;
	}

	private static Rgba ToRgba(Color color)
	{
		return new Rgba(color.r, color.g, color.b, color.a);
	}

	/// <summary>A white shape with 4×4-supersampled coverage as alpha (averaging white with clear would darken the edge),
	/// plus mipmaps so the small marker dots that reuse the circle stay smooth too.</summary>
	private static (Texture2D Texture, Sprite Sprite) MakeMask(int size, Func<double, double, bool> inside)
	{
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true);
		try
		{
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.filterMode = FilterMode.Trilinear;
			Color[] array = new Color[size * size];
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					array[i * size + j] = new Color(1f, 1f, 1f, Shapes.Coverage(j, i, size, inside));
				}
			}
			texture2D.SetPixels(array);
			texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: true);
			Sprite item = Sprite.Create(texture2D, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
			return (Texture: texture2D, Sprite: item);
		}
		catch
		{
			UnityEngine.Object.Destroy(texture2D);
			throw;
		}
	}

	private void ClearPeers()
	{
		foreach (var value in peers.Values)
		{
			if (value.Marker != null)
			{
				UnityEngine.Object.Destroy(value.Marker.gameObject);
			}
		}
		peers.Clear();
		nextPeerRefresh = 0f;
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			if (root != null)
			{
				root.SetActive(value: false);
				UnityEngine.Object.Destroy(root);
			}
			customers.Dispose();
			worldMarkers.Dispose();
			peers.Clear();
			UnityEngine.Object.Destroy(circleSprite);
			UnityEngine.Object.Destroy(arrowSprite);
			UnityEngine.Object.Destroy(circleTexture);
			UnityEngine.Object.Destroy(arrowTexture);
			if (rimSprite != null)
			{
				UnityEngine.Object.Destroy(rimSprite);
			}
			if (rimTexture != null)
			{
				UnityEngine.Object.Destroy(rimTexture);
			}
		}
	}
}
