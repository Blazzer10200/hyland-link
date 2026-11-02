using System;
using System.Collections.Generic;
using Il2CppScheduleOne.PlayerScripts;
using UnityEngine;
using UnityEngine.UI;

namespace HylandMinimap;

internal sealed class WorldMarkers : IDisposable
{
	private sealed class Marker
	{
		internal WorldMarkerSource Source;

		internal RectTransform Root;

		internal Image Ring;

		internal RectTransform FaceMask;

		internal Image Face;

		internal RectTransform GlyphRect;

		internal Image Glyph;

		internal RectTransform Badge;

		internal Image BadgeGlyph;

		internal RectTransform LabelRect;

		internal Text Label;

		internal float Radius;

		internal float LabelWidth;

		internal int NameSize;

		internal bool UsesName;
	}

	private readonly RectTransform dealerLayer;

	private readonly RectTransform vehicleLayer;

	private readonly Sprite circleSprite;

	private readonly Font? font;

	private readonly WorldMarkerSprites sprites;

	private readonly Dictionary<int, Marker> markers = new Dictionary<int, Marker>();

	private readonly HashSet<int> liveIds = new HashSet<int>();

	private readonly HashSet<int> dealerNpcIds = new HashSet<int>();

	private readonly HashSet<int> occupiedVehicleIds = new HashSet<int>();

	private readonly List<int> staleIds = new List<int>();

	private float nextRefresh;

	private bool showDealers;

	private bool showVehicles;

	private bool showVehicleNames;

	private CustomerMarkerStyle dealerStyle;

	private float dealerSize;

	private float vehicleSize;

	private int dealerNameSize;

	private int vehicleNameSize;

	private Color dealerColor;

	private Color vehicleColor;

	private bool disposed;

	internal IReadOnlySet<int> DealerNpcIds => dealerNpcIds;

	internal WorldMarkers(RectTransform parent, Sprite circleSprite, Font? font)
	{
		this.circleSprite = circleSprite;
		this.font = font;
		sprites = new WorldMarkerSprites();
		try
		{
			vehicleLayer = NewRect("Owned vehicles", parent);
			dealerLayer = NewRect("Hired dealers", parent);
		}
		catch
		{
			if (vehicleLayer != null)
			{
				UnityEngine.Object.Destroy(vehicleLayer.gameObject);
			}
			if (dealerLayer != null)
			{
				UnityEngine.Object.Destroy(dealerLayer.gameObject);
			}
			sprites.Dispose();
			throw;
		}
	}

	internal void PlaceVehiclesBehind(RectTransform customers)
	{
		vehicleLayer.SetSiblingIndex(customers.GetSiblingIndex());
	}

	internal void Apply(Settings settings)
	{
		showDealers = settings.ShowDealers.Value;
		showVehicles = settings.ShowVehicles.Value;
		showVehicleNames = settings.ShowVehicleNames.Value;
		dealerStyle = (Enum.IsDefined(typeof(CustomerMarkerStyle), settings.DealerStyle.Value) ? settings.DealerStyle.Value : CustomerMarkerStyle.Portraits);
		dealerSize = (float)MapMath.ClampFinite(settings.DealerMarkerSize.Value, 12.0, 40.0, 24.0);
		vehicleSize = (float)MapMath.ClampFinite(settings.VehicleMarkerSize.Value, 12.0, 32.0, 18.0);
		dealerNameSize = (int)MapMath.ClampFinite(settings.DealerNameSize.Value, 8.0, 18.0, 11.0);
		vehicleNameSize = (int)MapMath.ClampFinite(settings.VehicleNameSize.Value, 8.0, 18.0, 11.0);
		dealerColor = ClampColor(settings.DealerColor.Value, new Color(1f, 0.7f, 0.2f, 1f));
		vehicleColor = ClampColor(settings.VehicleColor.Value, new Color(0.5f, 0.8f, 1f, 1f));
		dealerLayer.gameObject.SetActive(showDealers);
		vehicleLayer.gameObject.SetActive(showVehicles);
		dealerNpcIds.Clear();
		foreach (Marker value in markers.Values)
		{
			ApplyAppearance(value);
		}
		nextRefresh = 0f;
	}

	internal void RefreshSources()
	{
		if (disposed || Time.unscaledTime < nextRefresh)
		{
			return;
		}
		nextRefresh = Time.unscaledTime + 1f;
		liveIds.Clear();
		dealerNpcIds.Clear();
		if (showDealers)
		{
			foreach (WorldMarkerSource item in WorldMarkerReader.ReadDealers())
			{
				Register(item);
			}
		}
		if (showVehicles)
		{
			foreach (WorldMarkerSource item2 in WorldMarkerReader.ReadVehicles())
			{
				Register(item2);
			}
		}
		staleIds.Clear();
		foreach (int key in markers.Keys)
		{
			if (!liveIds.Contains(key))
			{
				staleIds.Add(key);
			}
		}
		foreach (int staleId in staleIds)
		{
			DestroyMarker(markers[staleId]);
			markers.Remove(staleId);
		}
	}

	private void Register(WorldMarkerSource source)
	{
		try
		{
			if (source.Target == null || source.Tracking == null || !liveIds.Add(source.Id))
			{
				return;
			}
			if (!markers.TryGetValue(source.Id, out Marker value) || value.Root == null)
			{
				if (value != null)
				{
					DestroyMarker(value);
				}
				value = CreateMarker((source.Kind == WorldMarkerKind.Dealer) ? dealerLayer : vehicleLayer);
				markers[source.Id] = value;
			}
			value.Source = source;
			value.Label.text = source.Name;
			ApplyAppearance(value);
			if (source.Kind == WorldMarkerKind.Dealer)
			{
				dealerNpcIds.Add(source.Id);
			}
		}
		catch
		{
			liveIds.Remove(source.Id);
		}
	}

	internal void Update(Player localPlayer, bool showCoop, MapProjection projection, Vector2 center, float zoom, float rotation, float viewportSize, bool circle, List<Rect> occupiedLabels)
	{
		if (disposed || (!showDealers && !showVehicles))
		{
			return;
		}
		occupiedVehicleIds.Clear();
		if (showVehicles)
		{
			WorldMarkerReader.FillOccupiedVehicleIds(localPlayer, showCoop, occupiedVehicleIds);
		}
		foreach (Marker value in markers.Values)
		{
			if (value.Source.Kind == WorldMarkerKind.Dealer)
			{
				UpdateMarker(value, projection, center, zoom, rotation, viewportSize, circle, occupiedLabels, occupiedVehicleIds);
			}
		}
		foreach (Marker value2 in markers.Values)
		{
			if (value2.Source.Kind != WorldMarkerKind.Dealer)
			{
				UpdateMarker(value2, projection, center, zoom, rotation, viewportSize, circle, occupiedLabels, occupiedVehicleIds);
			}
		}
	}

	private void UpdateMarker(Marker marker, MapProjection projection, Vector2 center, float zoom, float rotation, float size, bool circle, List<Rect> occupiedLabels, HashSet<int>? occupiedVehicles)
	{
		try
		{
			if (marker.Root == null)
			{
				return;
			}
			WorldMarkerSource source = marker.Source;
			if (!((source.Kind == WorldMarkerKind.Dealer) ? showDealers : showVehicles) || source.Target == null || source.Tracking == null || !source.Tracking.gameObject.activeInHierarchy || (source.Kind != WorldMarkerKind.Dealer && occupiedVehicles != null && occupiedVehicles.Contains(source.Id)))
			{
				marker.Root.gameObject.SetActive(value: false);
				return;
			}
			Vector2 vector = projection.Project(source.Tracking.position);
			(double, double) tuple = MapMath.RelativePoint(vector.x, vector.y, center.x, center.y, zoom, rotation);
			float num = size / 2f;
			bool flag = num > marker.Radius && MapMath.IsInside(tuple.Item1, tuple.Item2, num - marker.Radius, circle);
			marker.Root.gameObject.SetActive(flag);
			if (flag)
			{
				marker.Root.anchoredPosition = new Vector2((float)tuple.Item1, (float)tuple.Item2);
				marker.Root.localRotation = Quaternion.identity;
				bool active = marker.UsesName && font != null && marker.LabelWidth > 0f && PlaceLabel(marker, (float)tuple.Item1, (float)tuple.Item2, num, circle, occupiedLabels);
				marker.LabelRect.gameObject.SetActive(active);
			}
		}
		catch
		{
			if (marker.Root != null)
			{
				marker.Root.gameObject.SetActive(value: false);
			}
		}
	}

	private Marker CreateMarker(RectTransform parent)
	{
		RectTransform rectTransform = NewRect("World marker", parent);
		try
		{
			Image image = rectTransform.gameObject.AddComponent<Image>();
			image.sprite = circleSprite;
			image.raycastTarget = false;
			RectTransform rectTransform2 = NewRect("Portrait clip", rectTransform);
			Image image2 = rectTransform2.gameObject.AddComponent<Image>();
			image2.sprite = circleSprite;
			image2.raycastTarget = false;
			rectTransform2.gameObject.AddComponent<Mask>().showMaskGraphic = false;
			RectTransform rectTransform3 = NewRect("Portrait", rectTransform2);
			Stretch(rectTransform3);
			Image image3 = rectTransform3.gameObject.AddComponent<Image>();
			image3.raycastTarget = false;
			RectTransform rectTransform4 = NewRect("Vehicle icon", rectTransform);
			Image image4 = rectTransform4.gameObject.AddComponent<Image>();
			image4.raycastTarget = false;
			RectTransform rectTransform5 = NewRect("Dealer badge", rectTransform);
			Image image5 = rectTransform5.gameObject.AddComponent<Image>();
			image5.sprite = circleSprite;
			image5.color = new Color(0.015f, 0.025f, 0.035f, 0.98f);
			image5.raycastTarget = false;
			RectTransform rectTransform6 = NewRect("Briefcase", rectTransform5);
			Stretch(rectTransform6);
			rectTransform6.offsetMin = new Vector2(1f, 1f);
			rectTransform6.offsetMax = new Vector2(-1f, -1f);
			Image image6 = rectTransform6.gameObject.AddComponent<Image>();
			image6.sprite = sprites.Dealer;
			image6.raycastTarget = false;
			RectTransform rectTransform7 = NewRect("Name", rectTransform);
			rectTransform7.pivot = new Vector2(0f, 0.5f);
			Text text = rectTransform7.gameObject.AddComponent<Text>();
			if (font != null)
			{
				text.font = font;
			}
			text.color = Color.white;
			text.alignment = TextAnchor.MiddleLeft;
			text.horizontalOverflow = HorizontalWrapMode.Overflow;
			text.verticalOverflow = VerticalWrapMode.Overflow;
			text.supportRichText = false;
			text.resizeTextForBestFit = false;
			text.raycastTarget = false;
			Shadow shadow = rectTransform7.gameObject.AddComponent<Shadow>();
			shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
			shadow.effectDistance = new Vector2(1f, -1f);
			shadow.useGraphicAlpha = true;
			rectTransform.gameObject.SetActive(value: false);
			return new Marker
			{
				Root = rectTransform,
				Ring = image,
				FaceMask = rectTransform2,
				Face = image3,
				GlyphRect = rectTransform4,
				Glyph = image4,
				Badge = rectTransform5,
				BadgeGlyph = image6,
				LabelRect = rectTransform7,
				Label = text
			};
		}
		catch
		{
			UnityEngine.Object.Destroy(rectTransform.gameObject);
			throw;
		}
	}

	private void ApplyAppearance(Marker marker)
	{
		if (!(marker.Root == null))
		{
			bool flag = marker.Source.Kind == WorldMarkerKind.Dealer;
			bool flag2 = flag && dealerStyle == CustomerMarkerStyle.Portraits && marker.Source.Portrait != null;
			float num;
			if (flag)
			{
				num = (flag2 ? dealerSize : Math.Clamp(dealerSize * 0.42f, 5f, 14f));
			}
			else
			{
				num = vehicleSize;
			}
			Color color = (flag ? dealerColor : vehicleColor);
			marker.Root.sizeDelta = new Vector2(num, num);
			marker.Ring.color = (flag ? color : new Color(0.015f, 0.025f, 0.035f, 0.96f));
			marker.FaceMask.sizeDelta = new Vector2(Math.Max(4f, num - 4f), Math.Max(4f, num - 4f));
			marker.Face.sprite = marker.Source.Portrait;
			marker.FaceMask.gameObject.SetActive(flag2);
			marker.GlyphRect.sizeDelta = new Vector2(Math.Max(8f, num - 2f), Math.Max(8f, num - 2f));
			marker.Glyph.sprite = ((marker.Source.Kind == WorldMarkerKind.Bike) ? sprites.Bike : sprites.Vehicle);
			marker.Glyph.color = color;
			marker.GlyphRect.gameObject.SetActive(!flag);
			float num2 = Math.Clamp(num * 0.5f, 10f, 14f);
			float num3 = Math.Max(num * 0.38f, num2 * 0.6f);
			marker.Badge.sizeDelta = new Vector2(num2, num2);
			marker.Badge.anchoredPosition = new Vector2(num3, 0f - num3);
			marker.BadgeGlyph.color = dealerColor;
			marker.Badge.gameObject.SetActive(flag);
			marker.Radius = (flag ? Math.Max(num / 2f, MathF.Sqrt(2f * num3 * num3) + num2 / 2f) : (num / 2f));
			marker.UsesName = (flag ? (!flag2) : showVehicleNames);
			marker.NameSize = (flag ? dealerNameSize : vehicleNameSize);
			marker.Label.fontSize = marker.NameSize;
			marker.LabelWidth = ((marker.UsesName && font != null) ? Math.Max(1f, marker.Label.preferredWidth + 4f) : 0f);
			marker.LabelRect.sizeDelta = new Vector2(marker.LabelWidth, marker.NameSize + 6);
			marker.LabelRect.gameObject.SetActive(value: false);
		}
	}

	private static bool PlaceLabel(Marker marker, float x, float y, float halfSize, bool circle, List<Rect> occupied)
	{
		int num = marker.NameSize + 6;
		Rect bounds = new Rect(x + marker.Radius + 4f, y - (float)num / 2f, marker.LabelWidth, num);
		Rect bounds2 = new Rect(x - marker.Radius - 4f - marker.LabelWidth, y - (float)num / 2f, marker.LabelWidth, num);
		if (!TryPlaceLabel(marker, bounds, x, halfSize, circle, occupied))
		{
			return TryPlaceLabel(marker, bounds2, x, halfSize, circle, occupied);
		}
		return true;
	}

	private static bool TryPlaceLabel(Marker marker, Rect bounds, float x, float halfSize, bool circle, List<Rect> occupied)
	{
		if (!MapMath.IsInside(bounds.xMin, bounds.yMin, halfSize, circle) || !MapMath.IsInside(bounds.xMin, bounds.yMax, halfSize, circle) || !MapMath.IsInside(bounds.xMax, bounds.yMin, halfSize, circle) || !MapMath.IsInside(bounds.xMax, bounds.yMax, halfSize, circle))
		{
			return false;
		}
		foreach (Rect item in occupied)
		{
			if (item.Overlaps(bounds))
			{
				return false;
			}
		}
		occupied.Add(bounds);
		marker.LabelRect.anchoredPosition = new Vector2(bounds.xMin - x, 0f);
		return true;
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

	private static Color ClampColor(Color color, Color fallback)
	{
		return new Color((float)MapMath.ClampFinite(color.r, 0.0, 1.0, fallback.r), (float)MapMath.ClampFinite(color.g, 0.0, 1.0, fallback.g), (float)MapMath.ClampFinite(color.b, 0.0, 1.0, fallback.b), (float)MapMath.ClampFinite(color.a, 0.0, 1.0, fallback.a));
	}

	private static void DestroyMarker(Marker marker)
	{
		if (!(marker.Root == null))
		{
			marker.Root.gameObject.SetActive(value: false);
			UnityEngine.Object.Destroy(marker.Root.gameObject);
		}
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			if (dealerLayer != null)
			{
				dealerLayer.gameObject.SetActive(value: false);
				UnityEngine.Object.Destroy(dealerLayer.gameObject);
			}
			if (vehicleLayer != null)
			{
				vehicleLayer.gameObject.SetActive(value: false);
				UnityEngine.Object.Destroy(vehicleLayer.gameObject);
			}
			sprites.Dispose();
			markers.Clear();
			dealerNpcIds.Clear();
			occupiedVehicleIds.Clear();
			liveIds.Clear();
			staleIds.Clear();
		}
	}
}
