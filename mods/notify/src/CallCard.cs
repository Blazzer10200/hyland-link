using HylandHud;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI.Phone;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The incoming call (the game slides a card in from the left): caller's face with a ringing pulse, "Incoming call" and the
/// name, at the top of the left column. Answering stays the game's (the phone).
/// </summary>
internal sealed class CallCard : Surface
{
	private const float Height = 64f;

	private Muffle? muffle;

	private RectTransform? card;

	private CanvasGroup? group;

	private Image? picture;

	private Image? ring;

	private TextMeshProUGUI? initial;

	private TextMeshProUGUI? caller;

	private readonly Ease appear = new Ease(0f, 0.25f);

	private float openedAt;

	private bool wasOpen;

	internal CallCard()
		: base("call")
	{
	}

	internal override bool On(Settings settings)
	{
		return settings.Call.Value;
	}

	internal override void Update(Frame f)
	{
		CallNotification call = Singleton<CallNotification>.Instance;
		if (call == null || call.Container == null)
		{
			return;
		}
		if (muffle == null || !muffle.Covers(call.Container.gameObject))
		{
			Teardown();
			muffle = new Muffle(call.Container.gameObject);
		}
		muffle.Hold();
		if (card == null)
		{
			Build(f);
		}
		bool open = call.IsOpen;
		if (open && !wasOpen)
		{
			openedAt = Time.unscaledTime;
		}
		wasOpen = open;
		if (open)
		{
			string name = call.ActiveCallData != null && call.ActiveCallData.CallerID != null ? call.ActiveCallData.CallerID.Name : "";
			Sprite sprite = call.ProfilePicture != null ? call.ProfilePicture.sprite : null;
			caller!.text = name.Length > 0 ? name : "Unknown caller";
			picture!.sprite = sprite;
			picture.enabled = sprite != null;
			initial!.text = sprite == null && name.Length > 0 ? name.Substring(0, 1) : "";
		}
		appear.Target = open ? 1f : 0f;
		float a = appear.Step(f.Dt);
		card!.gameObject.SetActive(a > 0.001f);
		if (a <= 0.001f)
		{
			return;
		}
		group!.alpha = a;
		float slide = HylandHud.Motion.EaseOutCubic(a);
		Glass.Pin(card, new Vector2(0f, 1f), new Vector2(f.Layout.ColumnX - 40f * (1f - slide), -f.ColumnY), new Vector2(ScreenLayout.ColumnWidth, Height));
		// Two rings a beat apart, like a phone ringing.
		float t = (Time.unscaledTime - openedAt) % 1.2f / 1.2f;
		ring!.color = Glass.Tint(Glass.Green, 0.55f * (1f - t));
		float s = 40f + 18f * HylandHud.Motion.EaseOutCubic(t);
		ring.rectTransform.sizeDelta = new Vector2(s, s);
		f.ColumnY += Height * a + ScreenLayout.Gap * a;
	}

	internal override void Teardown()
	{
		Muffle? m = muffle;
		RectTransform? c = card;
		muffle = null;
		card = null;
		appear.Value = appear.Target = 0f;
		wasOpen = false;
		Steps(() => m?.Release(), () => Destroy(c));
	}

	private void Build(Frame f)
	{
		Image glass = Glass.Panel("Hyland call", f.Root, f.Sprites);
		card = glass.rectTransform;
		group = Ui.Group(card.gameObject);
		Vector2 face = new Vector2(32f, -Height / 2f);
		ring = Glass.Solid("Ring", card, f.Sprites, 20, Glass.Green);
		Centre(ring.rectTransform, face, 40f);
		Image avatar = Glass.Avatar("Avatar", card, f.Sprites, 40f, out picture);
		Centre(avatar.rectTransform, face, 40f);
		initial = Glass.Label("Initial", avatar.transform, f.Font, 16f, Glass.Text, TextAlignmentOptions.Center);
		Glass.Fill(initial.rectTransform);
		TextMeshProUGUI label = Glass.Label("Label", card, f.Font, 11f, Glass.Green);
		label.text = "INCOMING CALL";
		label.characterSpacing = 8f;
		label.fontStyle = FontStyles.Bold;
		Glass.Pin(label.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -14f), new Vector2(ScreenLayout.ColumnWidth - 76f, 16f));
		caller = Glass.Label("Caller", card, f.Font, 16f, Glass.Text);
		caller.fontStyle = FontStyles.Bold;
		Glass.Pin(caller.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -30f), new Vector2(ScreenLayout.ColumnWidth - 76f, 24f));
	}

	private static void Centre(RectTransform rect, Vector2 at, float size)
	{
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = at;
		rect.sizeDelta = new Vector2(size, size);
	}
}
