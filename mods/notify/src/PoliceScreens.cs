using System;
using System.Collections.Generic;
using HylandHud;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandNotify;

/// <summary>
/// The police paperwork (offense ticket, arrest notice, hospital bill) restyled in place from paper to glass. These are
/// screens you click, so they stay the game's own objects: the paper sprite becomes glass, the dark ink turns light, the
/// headings printed on the paper are drawn as text, and the close button takes our blue. Every change is recorded and
/// put back on teardown.
/// </summary>
internal sealed class PoliceScreens : Surface
{
	/// <summary>Records each thing it changes so <see cref="Restore"/> can put it back exactly.</summary>
	private sealed class Restyle
	{
		private readonly List<Action> undo = new List<Action>();

		private readonly HashSet<int> touched = new HashSet<int>();

		private readonly List<GameObject> added = new List<GameObject>();

		internal void Tint(Graphic? graphic, Color color)
		{
			if (graphic == null)
			{
				return;
			}
			if (touched.Add(graphic.GetInstanceID()))
			{
				Color old = graphic.color;
				undo.Add(() =>
				{
					if (graphic != null)
					{
						graphic.color = old;
					}
				});
			}
			graphic.color = color;
		}

		internal void Paper(Image? image, Sprite glass)
		{
			if (image == null)
			{
				return;
			}
			Sprite oldSprite = image.sprite;
			Image.Type oldType = image.type;
			bool oldAspect = image.preserveAspect;
			Tint(image, Color.white);
			undo.Add(() =>
			{
				if (image != null)
				{
					image.sprite = oldSprite;
					image.type = oldType;
					image.preserveAspect = oldAspect;
				}
			});
			image.sprite = glass;
			image.type = Image.Type.Sliced;
			image.preserveAspect = false;
		}

		internal T Add<T>(T component) where T : Component
		{
			added.Add(component.gameObject);
			return component;
		}

		internal void Restore()
		{
			Exception? failure = null;
			foreach (GameObject go in added)
			{
				Ui.Step(ref failure, () =>
				{
					if (go != null)
					{
						UnityEngine.Object.Destroy(go);
					}
				});
			}
			for (int i = undo.Count - 1; i >= 0; i--)
			{
				Ui.Step(ref failure, undo[i]);
			}
			undo.Clear();
			touched.Clear();
			added.Clear();
			Ui.Rethrow(failure);
		}
	}

	private Restyle? offense;

	private Restyle? arrest;

	private Restyle? hospital;

	private float nextTry;

	internal PoliceScreens()
		: base("police screens")
	{
	}

	internal override bool On(Settings settings)
	{
		return settings.Screens.Value;
	}

	internal override void Update(Frame f)
	{
		ArrestNoticeScreen arrestScreen = Singleton<ArrestNoticeScreen>.Instance;
		if (arrest != null && arrestScreen != null && arrestScreen.isOpen)
		{
			// Its rows are made fresh on every open.
			TintEntries(arrest, arrestScreen.CrimeEntryContainer, Glass.Text);
			TintEntries(arrest, arrestScreen.PenaltyEntryContainer, Glass.Orange);
		}
		if (offense != null && arrest != null && hospital != null || Time.unscaledTime < nextTry)
		{
			return;
		}
		nextTry = Time.unscaledTime + 2f;
		OffenceNoticeUI offenseUi = Singleton<OffenceNoticeUI>.Instance;
		if (offense == null && offenseUi != null && offenseUi.container != null)
		{
			offense = Offense(f, offenseUi);
		}
		if (arrest == null && arrestScreen != null)
		{
			arrest = Arrest(f, arrestScreen);
		}
		HospitalBillScreen bill = Singleton<HospitalBillScreen>.Instance;
		if (hospital == null && bill != null && bill.Container != null)
		{
			hospital = Hospital(f, bill);
		}
	}

	internal override void Teardown()
	{
		Restyle? a = offense;
		Restyle? b = arrest;
		Restyle? c = hospital;
		offense = null;
		arrest = null;
		hospital = null;
		nextTry = 0f;
		Steps(() => a?.Restore(), () => b?.Restore(), () => c?.Restore());
	}

	private static Restyle Offense(Frame f, OffenceNoticeUI ui)
	{
		Restyle r = new Restyle();
		Transform root = ui.container.transform;
		Transform background = root.Find("Background");
		Transform parent = background != null ? background : root;
		try
		{
			r.Paper(background?.GetComponent<Image>(), f.Sprites.Glass(16));
			// The sheet is 475×700; its two lists sit at fixed spots, so the headings go just above each.
			Heading(f, r, parent, "CITATION", Glass.Orange, 11f, 34f, spaced: true);
			Heading(f, r, parent, "Offense notice", Glass.Text, 28f, 52f);
			Heading(f, r, parent, "You were stopped and charged by the police.", Glass.Muted, 13f, 92f);
			Divider(f, r, parent, 126f, 475f);
			Heading(f, r, parent, "CHARGES", Glass.Muted, 11f, 214f, spaced: true);
			Heading(f, r, parent, "PENALTIES", Glass.Muted, 11f, 422f, spaced: true);
			for (int i = 0; i < ui.charges.Count; i++)
			{
				r.Tint(ui.charges[i], Glass.Text);
			}
			for (int i = 0; i < ui.penalties.Count; i++)
			{
				r.Tint(ui.penalties[i], Glass.Orange);
			}
		}
		catch
		{
			r.Restore();
			throw;
		}
		return r;
	}

	private static Restyle Arrest(Frame f, ArrestNoticeScreen screen)
	{
		Restyle r = new Restyle();
		Transform bill = screen.transform.Find("Bill");
		try
		{
			if (bill != null)
			{
				r.Paper(bill.GetComponent<Image>(), f.Sprites.Glass(16));
				// The sheet is 409×614; charges start 218 down, penalties 401 down.
				Heading(f, r, bill, "ARRESTED", Glass.Red, 11f, 34f, spaced: true);
				Heading(f, r, bill, "Arrest notice", Glass.Text, 28f, 52f);
				Heading(f, r, bill, "What you were booked for and what it cost you.", Glass.Muted, 13f, 92f);
				Divider(f, r, bill, 126f, 409f);
				Heading(f, r, bill, "CHARGES", Glass.Muted, 11f, 196f, spaced: true);
				Heading(f, r, bill, "PENALTIES", Glass.Muted, 11f, 379f, spaced: true);
			}
			Button(r, screen.transform.Find("Button"));
		}
		catch
		{
			r.Restore();
			throw;
		}
		return r;
	}

	private static Restyle Hospital(Frame f, HospitalBillScreen screen)
	{
		Restyle r = new Restyle();
		Transform bill = screen.Container.Find("Bill");
		try
		{
			if (bill != null)
			{
				r.Paper(bill.GetComponent<Image>(), f.Sprites.Glass(16));
				Heading(f, r, bill, "HOSPITAL", Glass.Blue, 11f, 32f, spaced: true);
				Heading(f, r, bill, "Hospital bill", Glass.Text, 28f, 50f);
				Divider(f, r, bill, 96f, 800f);
				// Captions over the game's three fields (their spots on the old form) and beside the total.
				Caption(f, r, bill, "PATIENT", screen.PatientNameLabel);
				Caption(f, r, bill, "PATIENT NO.", bill.Find("PatientNumber")?.GetComponent<TextMeshProUGUI>());
				Caption(f, r, bill, "BILL NO.", screen.BillNumberLabel);
				Caption(f, r, bill, "TOTAL PAID", screen.PaidAmountLabel);
				r.Tint(screen.PatientNameLabel, Glass.Text);
				r.Tint(bill.Find("PatientNumber")?.GetComponent<TextMeshProUGUI>(), Glass.Text);
				r.Tint(screen.BillNumberLabel, Glass.Text);
				r.Tint(screen.PaidAmountLabel, Glass.Orange);
			}
			Button(r, screen.Container.Find("Button"));
		}
		catch
		{
			r.Restore();
			throw;
		}
		return r;
	}

	private static void TintEntries(Restyle r, RectTransform? container, Color color)
	{
		if (container == null)
		{
			return;
		}
		foreach (Text text in container.GetComponentsInChildren<Text>(true))
		{
			r.Tint(text, color);
		}
		foreach (TMP_Text text in container.GetComponentsInChildren<TMP_Text>(true))
		{
			r.Tint(text, color);
		}
	}

	private static void Button(Restyle r, Transform? button)
	{
		if (button == null)
		{
			return;
		}
		r.Tint(button.GetComponent<Image>(), Glass.Blue);
		Transform label = button.Find("Text (TMP)");
		r.Tint(label != null ? label.GetComponent<TMP_Text>() : null, Glass.Ink);
	}

	private static void Heading(Frame f, Restyle r, Transform sheet, string text, Color color, float size, float top, bool spaced = false)
	{
		TextMeshProUGUI label = r.Add(Glass.Label("Hyland heading", sheet, f.Font, size, color));
		label.text = text;
		if (spaced)
		{
			label.characterSpacing = 10f;
			label.fontStyle = FontStyles.Bold;
		}
		else if (size > 20f)
		{
			label.fontStyle = FontStyles.Bold;
		}
		RectTransform rect = label.rectTransform;
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = new Vector2(32f, -top);
		// A full line for the size (a shorter rect makes TMP drop the line).
		rect.sizeDelta = new Vector2(-64f, Mathf.Ceil(size * 1.5f));
	}

	private static void Divider(Frame f, Restyle r, Transform sheet, float top, float width)
	{
		Image line = r.Add(Glass.Solid("Hyland divider", sheet, f.Sprites, 0.5, Glass.Line));
		Glass.Pin(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(width - 64f, 1f));
	}

	/// <summary>A small caption just above one of the game's labels, aligned to its left edge.</summary>
	private static void Caption(Frame f, Restyle r, Transform sheet, string text, TMP_Text? field)
	{
		if (field == null)
		{
			return;
		}
		TextMeshProUGUI label = r.Add(Glass.Label("Hyland caption", field.transform, f.Font, 9f, Glass.Muted));
		label.text = text;
		label.characterSpacing = 8f;
		label.alignment = field.alignment == TextAlignmentOptions.Right || field.alignment == TextAlignmentOptions.MidlineRight || field.alignment == TextAlignmentOptions.TopRight ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;
		RectTransform rect = label.rectTransform;
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 0f);
		rect.anchoredPosition = new Vector2(0f, 3f);
		rect.sizeDelta = new Vector2(0f, 14f);
	}
}
