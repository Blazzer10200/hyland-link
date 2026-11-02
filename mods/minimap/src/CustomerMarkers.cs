using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.NPCs;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HylandMinimap;

internal sealed class CustomerMarkers : IDisposable
{
	private sealed class Marker
	{
		internal RectTransform Root;

		internal RectTransform HaloRect;

		internal Image Halo;

		internal RectTransform RingRect;

		internal Image Ring;

		internal RectTransform PortraitMask;

		internal Image Portrait;

		internal RectTransform LabelRect;

		internal Text Label;

		internal RectTransform PurchasedBadge;

		internal Image PurchasedGlyph;

		internal RectTransform DealBadge;

		internal Image DealGlyph;

		internal RectTransform RegionBadge;

		internal Image RegionGlyph;

		internal Transform TrackingTransform;

		internal Sprite? Mugshot;

		internal CustomerStatus Status;

		internal bool Potential;

		internal bool UsesName;

		internal float Radius;

		internal float LabelWidth;
	}

	private readonly RectTransform layer;

	private readonly Sprite circleSprite;

	private readonly Font? font;

	private readonly CustomerBadgeSprites badgeSprites;

	private readonly System.Collections.Generic.Dictionary<int, Marker> markers = new System.Collections.Generic.Dictionary<int, Marker>();

	private readonly HashSet<int> liveIds = new HashSet<int>();

	private readonly System.Collections.Generic.List<int> staleIds = new System.Collections.Generic.List<int>();

	private readonly System.Collections.Generic.List<Rect> occupiedLabels = new System.Collections.Generic.List<Rect>();

	private float nextRefresh;

	private float markerSize = 20f;

	private int nameSize = 11;

	private bool show = true;

	private bool showPotential = true;

	private bool highlightPotential = true;

	private bool showAlreadyPurchased = true;

	private bool showActiveDeals = true;

	private bool showRegionLockedCustomers = true;

	private CustomerMarkerStyle style;

	private Color customerColor = new Color(0.25f, 0.9f, 0.65f, 1f);

	private Color potentialColor = new Color(0.75f, 0.35f, 1f, 1f);

	private Color purchasedColor = new Color(0.2f, 0.85f, 0.3f, 1f);

	private Color dealColor = new Color(0.25f, 0.65f, 1f, 1f);

	private Color regionLockedColor = new Color(0.7f, 0.72f, 0.77f, 1f);

	private bool disposed;

	private IReadOnlySet<int>? dealerNpcIds;

	internal System.Collections.Generic.List<Rect> OccupiedLabels => occupiedLabels;

	internal CustomerMarkers(RectTransform parent, Sprite circleSprite, Font? font)
	{
		this.circleSprite = circleSprite;
		this.font = font;
		badgeSprites = new CustomerBadgeSprites();
		try
		{
			layer = NewRect("Customers", parent);
			layer.localRotation = Quaternion.identity;
		}
		catch
		{
			if (layer != null)
			{
				UnityEngine.Object.Destroy(layer.gameObject);
			}
			badgeSprites.Dispose();
			throw;
		}
	}

	internal void Apply(Settings settings)
	{
		if (disposed || layer == null)
		{
			return;
		}
		show = settings.ShowCustomers.Value;
		showPotential = settings.ShowPotentialCustomers.Value;
		highlightPotential = settings.HighlightPotentialCustomers.Value;
		showAlreadyPurchased = settings.ShowAlreadyPurchased.Value;
		showActiveDeals = settings.ShowActiveDeals.Value;
		showRegionLockedCustomers = settings.ShowRegionLockedCustomers.Value;
		style = (Enum.IsDefined(typeof(CustomerMarkerStyle), settings.CustomerStyle.Value) ? settings.CustomerStyle.Value : CustomerMarkerStyle.Portraits);
		markerSize = (float)MapMath.ClampFinite(settings.CustomerMarkerSize.Value, 12.0, 40.0, 20.0);
		nameSize = (int)MapMath.ClampFinite(settings.CustomerNameSize.Value, 8.0, 18.0, 11.0);
		customerColor = ClampColor(settings.CustomerColor.Value, new Color(0.25f, 0.9f, 0.65f, 1f));
		potentialColor = ClampColor(settings.PotentialCustomerColor.Value, new Color(0.75f, 0.35f, 1f, 1f));
		purchasedColor = ClampColor(settings.AlreadyPurchasedColor.Value, new Color(0.2f, 0.85f, 0.3f, 1f));
		dealColor = ClampColor(settings.ActiveDealColor.Value, new Color(0.25f, 0.65f, 1f, 1f));
		regionLockedColor = ClampColor(settings.RegionLockedColor.Value, new Color(0.7f, 0.72f, 0.77f, 1f));
		layer.gameObject.SetActive(show);
		foreach (Marker value in markers.Values)
		{
			ApplyAppearance(value);
		}
		nextRefresh = 0f;
	}

	internal void Update(Settings settings, MapProjection projection, Vector2 center, float zoom, float rotation, float viewportSize, bool circle, IReadOnlySet<int>? dealerNpcIds = null, EdgePins? pins = null)
	{
		this.dealerNpcIds = dealerNpcIds;
		occupiedLabels.Clear();
		if (disposed || layer == null || !show)
		{
			return;
		}
		if (Time.unscaledTime >= nextRefresh)
		{
			RefreshSources();
		}
		float num = viewportSize / 2f;
		foreach (Marker value in markers.Values)
		{
			if (value.Root == null)
			{
				continue;
			}
			if (value.TrackingTransform == null)
			{
				value.Root.gameObject.SetActive(value: false);
				continue;
			}
			Vector2 vector = projection.Project(value.TrackingTransform.position);
			(double, double) tuple = MapMath.RelativePoint(vector.x, vector.y, center.x, center.y, zoom, rotation);
			bool flag = num > value.Radius && MapMath.IsInside(tuple.Item1, tuple.Item2, num - value.Radius, circle);
			value.Root.gameObject.SetActive(flag);
			if (!flag && (value.Status & CustomerStatus.ActiveDeal) != 0)
			{
				pins?.Add(tuple.Item1, tuple.Item2, dealColor);
			}
			if (flag)
			{
				value.Root.anchoredPosition = new Vector2((float)tuple.Item1, (float)tuple.Item2);
				value.Root.localRotation = Quaternion.identity;
				bool flag2 = value.UsesName && font != null && value.LabelWidth > 0f;
				if (flag2)
				{
					flag2 = PlaceLabel(value, (float)tuple.Item1, (float)tuple.Item2, num, circle);
				}
				value.LabelRect.gameObject.SetActive(flag2);
			}
		}
	}

	private void RefreshSources()
	{
		nextRefresh = Time.unscaledTime + 1f;
		liveIds.Clear();
		Il2CppSystem.Collections.Generic.List<Customer> unlockedCustomers = Customer.UnlockedCustomers;
		if (unlockedCustomers != null)
		{
			for (int i = 0; i < unlockedCustomers.Count; i++)
			{
				Register(unlockedCustomers[i], potential: false);
			}
		}
		if (showPotential || showRegionLockedCustomers)
		{
			Il2CppSystem.Collections.Generic.List<Customer> lockedCustomers = Customer.LockedCustomers;
			if (lockedCustomers != null)
			{
				for (int j = 0; j < lockedCustomers.Count; j++)
				{
					Customer customer = lockedCustomers[j];
					if (customer == null)
					{
						continue;
					}
					if (CustomerStatusReader.GetRegionLockState(customer) == true)
					{
						if (showRegionLockedCustomers)
						{
							Register(customer, potential: false, regionLocked: true);
						}
					}
					else if (showPotential)
					{
						NPCPoI potentialCustomerPoI = customer.potentialCustomerPoI;
						if (potentialCustomerPoI != null && potentialCustomerPoI.enabled)
						{
							Register(customer, potential: true);
						}
					}
				}
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

	private void Register(Customer? customer, bool potential, bool regionLocked = false)
	{
		if (customer == null)
		{
			return;
		}
		NPC nPC = customer.NPC;
		// HasNPCData: pooled special customers are active before the game gives them their data, and MugshotSprite /
		// FullName read through it, so one of those would throw and take the whole minimap down.
		if (nPC == null || nPC.gameObject == null || !nPC.gameObject.activeInHierarchy || !nPC.HasNPCData)
		{
			return;
		}
		NPCHealth health = nPC.Health;
		if (health != null && health.IsDead)
		{
			return;
		}
		Transform transform = nPC.transform;
		if (transform == null)
		{
			return;
		}
		int instanceID = nPC.GetInstanceID();
		IReadOnlySet<int>? readOnlySet = dealerNpcIds;
		if ((readOnlySet != null && readOnlySet.Contains(instanceID)) || !liveIds.Add(instanceID))
		{
			return;
		}
		if (!markers.TryGetValue(instanceID, out Marker value) || value.Root == null)
		{
			if (value != null)
			{
				DestroyMarker(value);
			}
			value = CreateMarker(transform);
			markers[instanceID] = value;
		}
		value.TrackingTransform = transform;
		value.Potential = potential && !regionLocked;
		value.Status = (regionLocked ? CustomerStatus.RegionLocked : ((!potential) ? CustomerStatusReader.Read(customer, showAlreadyPurchased, showActiveDeals) : CustomerStatus.None));
		value.Mugshot = nPC.MugshotSprite;
		value.Label.text = (string.IsNullOrWhiteSpace(nPC.FullName) ? "Customer" : nPC.FullName);
		ApplyAppearance(value);
	}

	private Marker CreateMarker(Transform tracking)
	{
		RectTransform rectTransform = NewRect("Customer marker", layer);
		try
		{
			RectTransform rectTransform2 = NewRect("Potential halo", rectTransform);
			Image image = rectTransform2.gameObject.AddComponent<Image>();
			image.sprite = circleSprite;
			image.raycastTarget = false;
			RectTransform rectTransform3 = NewRect("Customer ring or dot", rectTransform);
			Image image2 = rectTransform3.gameObject.AddComponent<Image>();
			image2.sprite = circleSprite;
			image2.raycastTarget = false;
			MarkerOutline.AddTo(rectTransform3.gameObject);
			RectTransform rectTransform4 = NewRect("Portrait clip", rectTransform);
			Image image3 = rectTransform4.gameObject.AddComponent<Image>();
			image3.sprite = circleSprite;
			image3.raycastTarget = false;
			rectTransform4.gameObject.AddComponent<Mask>().showMaskGraphic = false;
			RectTransform rectTransform5 = NewRect("Portrait", rectTransform4);
			rectTransform5.anchorMin = Vector2.zero;
			rectTransform5.anchorMax = Vector2.one;
			Vector2 offsetMin = (rectTransform5.offsetMax = Vector2.zero);
			rectTransform5.offsetMin = offsetMin;
			Image image4 = rectTransform5.gameObject.AddComponent<Image>();
			image4.preserveAspect = false;
			image4.raycastTarget = false;
			RectTransform rectTransform6 = NewRect("Customer name", rectTransform);
			rectTransform6.pivot = new Vector2(0f, 0.5f);
			Text text = rectTransform6.gameObject.AddComponent<Text>();
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
			Shadow shadow = rectTransform6.gameObject.AddComponent<Shadow>();
			shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
			shadow.effectDistance = new Vector2(1f, -1f);
			shadow.useGraphicAlpha = true;
			var (purchasedBadge, purchasedGlyph) = CreateBadge("Already purchased", rectTransform, badgeSprites.Check);
			var (dealBadge, dealGlyph) = CreateBadge("Active accepted deal", rectTransform, badgeSprites.Clock);
			var (regionBadge, regionGlyph) = CreateBadge("Region locked", rectTransform, badgeSprites.Lock);
			rectTransform.gameObject.SetActive(value: false);
			return new Marker
			{
				Root = rectTransform,
				HaloRect = rectTransform2,
				Halo = image,
				RingRect = rectTransform3,
				Ring = image2,
				PortraitMask = rectTransform4,
				Portrait = image4,
				LabelRect = rectTransform6,
				Label = text,
				PurchasedBadge = purchasedBadge,
				PurchasedGlyph = purchasedGlyph,
				DealBadge = dealBadge,
				DealGlyph = dealGlyph,
				RegionBadge = regionBadge,
				RegionGlyph = regionGlyph,
				TrackingTransform = tracking
			};
		}
		catch
		{
			UnityEngine.Object.Destroy(rectTransform.gameObject);
			throw;
		}
	}

	private (RectTransform Badge, Image Glyph) CreateBadge(string name, RectTransform parent, Sprite symbol)
	{
		RectTransform rectTransform = NewRect(name, parent);
		Image image = rectTransform.gameObject.AddComponent<Image>();
		image.sprite = circleSprite;
		image.color = new Color(0.015f, 0.025f, 0.035f, 0.98f);
		image.raycastTarget = false;
		RectTransform rectTransform2 = NewRect("Status symbol", rectTransform);
		rectTransform2.anchorMin = Vector2.zero;
		rectTransform2.anchorMax = Vector2.one;
		rectTransform2.offsetMin = new Vector2(1f, 1f);
		rectTransform2.offsetMax = new Vector2(-1f, -1f);
		Image image2 = rectTransform2.gameObject.AddComponent<Image>();
		image2.sprite = symbol;
		image2.raycastTarget = false;
		rectTransform.gameObject.SetActive(value: false);
		return (Badge: rectTransform, Glyph: image2);
	}

	private void ApplyAppearance(Marker marker)
	{
		if (!(marker.Root == null))
		{
			bool flag = style == CustomerMarkerStyle.Portraits && marker.Mugshot != null;
			bool flag2 = (marker.Status & CustomerStatus.RegionLocked) != 0;
			bool flag3 = !flag2 && marker.Potential && highlightPotential;
			float num = (flag ? markerSize : Math.Clamp(markerSize * 0.42f, 5f, 14f));
			float num2 = num + 6f;
			Color color;
			if (flag2)
			{
				color = regionLockedColor;
			}
			else
			{
				color = (flag3 ? potentialColor : customerColor);
			}
			marker.Root.sizeDelta = new Vector2(num, num);
			marker.RingRect.sizeDelta = new Vector2(num, num);
			marker.Ring.color = color;
			marker.HaloRect.sizeDelta = new Vector2(num2, num2);
			marker.Halo.color = new Color(color.r, color.g, color.b, color.a * 0.4f);
			marker.HaloRect.gameObject.SetActive(flag3);
			marker.PortraitMask.sizeDelta = new Vector2(Math.Max(4f, num - 4f), Math.Max(4f, num - 4f));
			marker.PortraitMask.gameObject.SetActive(flag);
			marker.Portrait.sprite = marker.Mugshot;
			marker.Portrait.color = Color.white;
			marker.Radius = (flag3 ? num2 : num) / 2f;
			bool flag4 = !flag2 && showAlreadyPurchased && (marker.Status & CustomerStatus.AlreadyPurchased) != 0;
			bool flag5 = !flag2 && showActiveDeals && (marker.Status & CustomerStatus.ActiveDeal) != 0;
			bool flag6 = flag2 && showRegionLockedCustomers;
			float num3 = Math.Clamp(num * 0.45f, 8f, 14f);
			float num4 = Math.Max(num * 0.4f, num3 * 0.65f);
			RectTransform purchasedBadge = marker.PurchasedBadge;
			Vector2 sizeDelta = (marker.DealBadge.sizeDelta = new Vector2(num3, num3));
			purchasedBadge.sizeDelta = sizeDelta;
			marker.RegionBadge.sizeDelta = new Vector2(num3, num3);
			marker.PurchasedBadge.anchoredPosition = new Vector2((flag4 & flag5) ? (0f - num4) : num4, 0f - num4);
			marker.DealBadge.anchoredPosition = new Vector2(num4, 0f - num4);
			marker.RegionBadge.anchoredPosition = new Vector2(num4, 0f - num4);
			RectTransform purchasedBadge2 = marker.PurchasedBadge;
			Quaternion localRotation = (marker.DealBadge.localRotation = Quaternion.identity);
			purchasedBadge2.localRotation = localRotation;
			marker.RegionBadge.localRotation = Quaternion.identity;
			marker.PurchasedGlyph.color = purchasedColor;
			marker.DealGlyph.color = dealColor;
			marker.RegionGlyph.color = regionLockedColor;
			marker.PurchasedBadge.gameObject.SetActive(flag4);
			marker.DealBadge.gameObject.SetActive(flag5);
			marker.RegionBadge.gameObject.SetActive(flag6);
			if (flag4 | flag5 | flag6)
			{
				float val = MathF.Sqrt(2f * num4 * num4) + num3 / 2f;
				marker.Radius = Math.Max(marker.Radius, val);
			}
			marker.UsesName = !flag;
			marker.Label.fontSize = nameSize;
			marker.LabelWidth = ((marker.UsesName && font != null) ? Math.Max(1f, marker.Label.preferredWidth + 4f) : 0f);
			marker.LabelRect.sizeDelta = new Vector2(marker.LabelWidth, nameSize + 6);
			marker.LabelRect.gameObject.SetActive(value: false);
		}
	}

	private bool PlaceLabel(Marker marker, float x, float y, float halfSize, bool circle)
	{
		int num = nameSize + 6;
		Rect bounds = new Rect(x + marker.Radius + 4f, y - (float)num / 2f, marker.LabelWidth, num);
		Rect bounds2 = new Rect(x - marker.Radius - 4f - marker.LabelWidth, y - (float)num / 2f, marker.LabelWidth, num);
		if (TryPlaceLabel(marker, bounds, x, halfSize, circle))
		{
			return true;
		}
		return TryPlaceLabel(marker, bounds2, x, halfSize, circle);
	}

	private bool TryPlaceLabel(Marker marker, Rect bounds, float markerX, float halfSize, bool circle)
	{
		if (!MapMath.IsInside(bounds.xMin, bounds.yMin, halfSize, circle) || !MapMath.IsInside(bounds.xMin, bounds.yMax, halfSize, circle) || !MapMath.IsInside(bounds.xMax, bounds.yMin, halfSize, circle) || !MapMath.IsInside(bounds.xMax, bounds.yMax, halfSize, circle))
		{
			return false;
		}
		foreach (Rect occupiedLabel in occupiedLabels)
		{
			if (occupiedLabel.Overlaps(bounds))
			{
				return false;
			}
		}
		occupiedLabels.Add(bounds);
		marker.LabelRect.anchoredPosition = new Vector2(bounds.xMin - markerX, 0f);
		marker.LabelRect.localRotation = Quaternion.identity;
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

	private static Color ClampColor(Color value, Color fallback)
	{
		return new Color((float)MapMath.ClampFinite(value.r, 0.0, 1.0, fallback.r), (float)MapMath.ClampFinite(value.g, 0.0, 1.0, fallback.g), (float)MapMath.ClampFinite(value.b, 0.0, 1.0, fallback.b), (float)MapMath.ClampFinite(value.a, 0.0, 1.0, fallback.a));
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
			if (layer != null)
			{
				layer.gameObject.SetActive(value: false);
				UnityEngine.Object.Destroy(layer.gameObject);
			}
			markers.Clear();
			liveIds.Clear();
			staleIds.Clear();
			occupiedLabels.Clear();
			badgeSprites.Dispose();
		}
	}
}
