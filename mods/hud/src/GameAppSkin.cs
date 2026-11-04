using System;
using System.Collections.Generic;
using Il2CppScheduleOne.UI.Phone;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HylandHud;

/// <summary>
/// The graphics a <see cref="GameAppSkin"/> has taken over. <see cref="AppsSkin"/>'s generic recolor leaves these alone,
/// and puts back its own color on one before the app skin records it, so the two never fight over a graphic.
/// </summary>
internal sealed class AppClaims
{
	/// <summary>Each claimed graphic's color as the game had it, so the recolor can still judge an app by its own
	/// background after an app skin has darkened it.</summary>
	private readonly Dictionary<int, Color> ids = new Dictionary<int, Color>();

	/// <summary>Set by <see cref="AppsSkin"/>: puts the game's color back on a graphic it had themed.</summary>
	internal Action<Graphic>? Releasing;

	internal void Claim(Graphic graphic)
	{
		int id = graphic.GetInstanceID();
		if (!ids.ContainsKey(id))
		{
			Releasing?.Invoke(graphic);
			ids[id] = graphic.color;
		}
	}

	internal void Unclaim(int id)
	{
		ids.Remove(id);
	}

	internal bool Has(int id)
	{
		return ids.ContainsKey(id);
	}

	internal bool TryOriginal(int id, out Color color)
	{
		return ids.TryGetValue(id, out color);
	}
}

/// <summary>
/// One of the game's phone apps redesigned the way the HUD is: the game's own objects keep every bit of their logic
/// (buttons, lists, inputs, what the game's scripts point at), their look is hidden, and the board's shapes are drawn
/// behind them as "Hyland …" objects. The game's text is restyled where it stands. Rows the game spawns later are found
/// on the next pass. Colors are held: when the game sets one again (a hover, a refresh), it counts as the new original
/// and the board's color goes back on. Everything is undone in <see cref="Dispose"/>, in reverse.
/// </summary>
internal abstract class GameAppSkin : IDisposable
{
	/// <summary>Board sizes × this (the board's screen is 316 wide upright, 316 tall sideways).</summary>
	protected const float Unit = 655f / 316f;

	private sealed class Paint
	{
		internal Graphic Graphic = null!;

		internal Color Original;

		internal Color Applied;

		internal Func<Color, Color> Map = null!;

		/// <summary>Held every pass (the game sets it again now and then), or set once (the game sets it only at spawn:
		/// Messages' bubbles run to the thousands).</summary>
		internal bool Held;
	}

	protected readonly Sprites sprites;

	protected readonly Font font;

	protected readonly Action<string> log;

	private readonly AppClaims claims;

	private readonly List<Action> undo = new List<Action>();

	private readonly Dictionary<int, Paint> paints = new Dictionary<int, Paint>();

	private readonly HashSet<int> styled = new HashSet<int>();

	private bool announced;

	private float nextPass;

	protected GameAppSkin(HomeScreen home, Sprites sprites, AppClaims claims, Action<string> log)
	{
		this.sprites = sprites;
		this.claims = claims;
		this.log = log;
		font = home.timeText.font;
	}

	/// <summary>The app's root object name under AppsCanvas.</summary>
	internal abstract string AppName { get; }

	/// <summary>Runs while this app is the open one: lays out what's new, then holds every color.</summary>
	internal void Update(GameObject? active)
	{
		if (active == null || active.name != AppName)
		{
			return;
		}
		EveryFrame(active);
		float now = Time.unscaledTime;
		if (now < nextPass)
		{
			return;
		}
		nextPass = now + 0.1f;
		Apply(active);
		Hold();
		if (!announced)
		{
			announced = true;
			log($"Phone: {AppName} redesigned like the board.");
		}
	}

	/// <summary>One pass over the open app (10 a second). Uses <see cref="First"/> so each object is set up once.</summary>
	protected abstract void Apply(GameObject app);

	/// <summary>Every frame while the app is open, before <see cref="Apply"/>: for rects the game places again on every
	/// open, so ours goes back on before that frame draws. Keep it to a few rects.</summary>
	protected virtual void EveryFrame(GameObject app)
	{
	}

	/// <summary>True the first time it sees this object: the place to set it up.</summary>
	protected bool First(UnityEngine.Object item)
	{
		return item != null && styled.Add(item.GetInstanceID());
	}

	protected static Transform? Find(Transform parent, string path)
	{
		return parent != null ? parent.Find(path) : null;
	}

	protected static T? Find<T>(Transform parent, string path) where T : Component
	{
		Transform? target = Find(parent, path);
		return target != null ? target.GetComponent<T>() : null;
	}

	/// <summary>Holds a color on one of the game's graphics, worked out from the game's own (so it follows a state change).</summary>
	protected void Recolor(Graphic? graphic, Func<Color, Color> map, bool hold = true)
	{
		if (graphic == null)
		{
			return;
		}
		int id = graphic.GetInstanceID();
		if (paints.ContainsKey(id))
		{
			return;
		}
		claims.Claim(graphic);
		Color original = graphic.color;
		Paint paint = new Paint { Graphic = graphic, Original = original, Map = map, Applied = map(original), Held = hold };
		paints[id] = paint;
		graphic.color = paint.Applied;
	}

	protected void Recolor(Graphic? graphic, Color color, bool hold = true)
	{
		Recolor(graphic, _ => color, hold);
	}

	/// <summary>Hides one of the game's graphics but keeps it hit-testable: a button's image still takes the click.</summary>
	protected void Ghost(Graphic? graphic, bool hold = true)
	{
		Recolor(graphic, c => new Color(c.r, c.g, c.b, 0f), hold);
	}

	/// <summary>Records a value on <paramref name="owner"/>, sets ours, and puts the recorded one back in
	/// <see cref="Dispose"/> if the owner still exists (the game destroys rows and panels as it goes).</summary>
	protected void Set<T>(UnityEngine.Object owner, Func<T> get, Action<T> set, T value)
	{
		T was = get();
		undo.Add(() =>
		{
			if (owner != null)
			{
				set(was);
			}
		});
		set(value);
	}

	/// <summary>Swaps an image's sprite (sliced when it has borders), recorded.</summary>
	protected void Swap(Image? image, Sprite sprite)
	{
		if (image == null)
		{
			return;
		}
		Sprite was = image.sprite;
		Image.Type type = image.type;
		undo.Add(() =>
		{
			if (image != null)
			{
				image.sprite = was;
				image.type = type;
			}
		});
		image.sprite = sprite;
		image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
	}

	/// <summary>Changes a button's tint colors, recorded (bubbles and rows get their color from the tint, not the image).</summary>
	protected void Tints(Selectable? selectable, Func<ColorBlock, ColorBlock> change)
	{
		if (selectable == null)
		{
			return;
		}
		ColorBlock was = selectable.colors;
		undo.Add(() =>
		{
			if (selectable != null)
			{
				selectable.colors = was;
			}
		});
		selectable.colors = change(was);
	}

	protected void Ghost(Transform parent, string path)
	{
		Ghost(Find<Graphic>(parent, path));
	}

	/// <summary>Restyles one of the game's labels (legacy Text or TextMeshPro): size in board px, color held.</summary>
	protected void Label(Graphic? label, float? boardSize = null, Color? color = null, bool? bold = null, TextAnchor? align = null, bool hold = true)
	{
		if (label == null)
		{
			return;
		}
		Text? legacy = label.TryCast<Text>();
		TMP_Text? pro = legacy == null ? label.TryCast<TMP_Text>() : null;
		if (legacy != null)
		{
			int size = legacy.fontSize;
			FontStyle style = legacy.fontStyle;
			bool best = legacy.resizeTextForBestFit;
			TextAnchor anchor = legacy.alignment;
			VerticalWrapMode overflow = legacy.verticalOverflow;
			undo.Add(() =>
			{
				if (legacy != null)
				{
					legacy.fontSize = size;
					legacy.fontStyle = style;
					legacy.resizeTextForBestFit = best;
					legacy.alignment = anchor;
					legacy.verticalOverflow = overflow;
				}
			});
			if (align != null)
			{
				legacy.alignment = align.Value;
			}
			if (boardSize != null)
			{
				legacy.resizeTextForBestFit = false;
				legacy.fontSize = Mathf.RoundToInt(boardSize.Value * Unit);
				// Legacy Text drops a whole line that doesn't fit its box (OpenSans' line is ~1.36 font sizes): at a new
				// size, let it overflow instead of vanishing. A caller wanting one cut line sets Truncate after.
				legacy.verticalOverflow = VerticalWrapMode.Overflow;
			}
			if (bold != null)
			{
				legacy.fontStyle = bold.Value ? FontStyle.Bold : FontStyle.Normal;
			}
		}
		else if (pro != null)
		{
			float size = pro.fontSize;
			bool auto = pro.enableAutoSizing;
			FontStyles style = pro.fontStyle;
			undo.Add(() =>
			{
				if (pro != null)
				{
					pro.fontSize = size;
					pro.enableAutoSizing = auto;
					pro.fontStyle = style;
				}
			});
			if (boardSize != null)
			{
				pro.enableAutoSizing = false;
				pro.fontSize = boardSize.Value * Unit;
			}
			if (bold != null)
			{
				pro.fontStyle = bold.Value ? FontStyles.Bold : FontStyles.Normal;
			}
		}
		if (color != null)
		{
			Recolor(label, color.Value, hold);
		}
	}

	protected void Label(Transform parent, string path, float? boardSize = null, Color? color = null, bool? bold = null, TextAnchor? align = null)
	{
		Label(Find<Graphic>(parent, path), boardSize, color, bold, align);
	}

	/// <summary>One of the board's shapes behind everything else in <paramref name="target"/>, filling it (plus
	/// <paramref name="outset"/> on every side, board px). Out of layout and raycasts.</summary>
	protected Image Behind(Transform target, string name, Sprite sprite, Color color, float outset = 0f)
	{
		Image image = Shape(target, name, sprite, color);
		image.transform.SetAsFirstSibling();
		Fill(image.rectTransform, outset * Unit);
		return image;
	}

	/// <summary>One of the board's shapes in front of <paramref name="target"/>'s children, placed by the caller.</summary>
	protected Image Front(Transform target, string name, Sprite sprite, Color color)
	{
		Image image = Shape(target, name, sprite, color);
		image.transform.SetAsLastSibling();
		return image;
	}

	private Image Shape(Transform target, string name, Sprite sprite, Color color)
	{
		Image image = Ui.Image(AppLayouts.Prefix + name, target, sprite, color, sliced: sprite != null && sprite.border != Vector4.zero);
		image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
		undo.Add(() =>
		{
			if (image != null)
			{
				UnityEngine.Object.Destroy(image.gameObject);
			}
		});
		return image;
	}

	/// <summary>A label of ours (the game's font), out of layout and raycasts.</summary>
	protected Text NewLabel(Transform parent, string name, float boardSize, Color color, TextAnchor anchor)
	{
		RectTransform rect = Ui.Rect(AppLayouts.Prefix + name, parent);
		rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
		Text text = rect.gameObject.AddComponent<Text>();
		text.font = font;
		text.fontSize = Mathf.RoundToInt(boardSize * Unit);
		text.color = color;
		text.alignment = anchor;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		undo.Add(() =>
		{
			if (rect != null)
			{
				UnityEngine.Object.Destroy(rect.gameObject);
			}
		});
		return text;
	}

	/// <summary>Records a rect's placement, then lets <paramref name="change"/> move it.</summary>
	protected void Move(RectTransform? rect, Action<RectTransform> change)
	{
		if (rect == null)
		{
			return;
		}
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
		change(rect);
	}

	protected void Move(Transform parent, string path, Action<RectTransform> change)
	{
		Transform? target = Find(parent, path);
		Move(target != null ? target.Cast<RectTransform>() : null, change);
	}

	/// <summary>Records a layout group's padding and spacing (board px), then sets them. Null keeps the game's.</summary>
	protected void Spacing(HorizontalOrVerticalLayoutGroup? group, float? spacing, RectOffset? padding = null)
	{
		if (group == null)
		{
			return;
		}
		float was = group.spacing;
		RectOffset pad = group.padding;
		int l = pad.left, r = pad.right, t = pad.top, b = pad.bottom;
		undo.Add(() =>
		{
			if (group != null)
			{
				group.spacing = was;
				group.padding = new RectOffset(l, r, t, b);
			}
		});
		if (spacing != null)
		{
			group.spacing = spacing.Value * Unit;
		}
		if (padding != null)
		{
			group.padding = padding;
		}
		LayoutRebuilder.MarkLayoutForRebuild(group.transform.Cast<RectTransform>());
	}

	/// <summary>Board px to a layout group's RectOffset.</summary>
	protected static RectOffset Pad(float left, float right, float top, float bottom)
	{
		return new RectOffset(Mathf.RoundToInt(left * Unit), Mathf.RoundToInt(right * Unit), Mathf.RoundToInt(top * Unit), Mathf.RoundToInt(bottom * Unit));
	}

	/// <summary>Records a row's preferred height (board px) on its LayoutElement, adding one if it has none.</summary>
	protected void Height(GameObject? row, float boardHeight)
	{
		Prefer(row, null, boardHeight);
	}

	/// <summary>Records an item's preferred width and/or height (board px) on its LayoutElement, adding one if it has none.</summary>
	protected void Prefer(GameObject? row, float? boardWidth, float? boardHeight)
	{
		if (row == null)
		{
			return;
		}
		LayoutElement? element = row.GetComponent<LayoutElement>();
		if (element == null)
		{
			element = row.AddComponent<LayoutElement>();
			LayoutElement added = element;
			undo.Add(() =>
			{
				if (added != null)
				{
					UnityEngine.Object.Destroy(added);
				}
			});
		}
		else
		{
			float minW = element.minWidth, prefW = element.preferredWidth, minH = element.minHeight, prefH = element.preferredHeight;
			LayoutElement kept = element;
			undo.Add(() =>
			{
				if (kept != null)
				{
					kept.minWidth = minW;
					kept.preferredWidth = prefW;
					kept.minHeight = minH;
					kept.preferredHeight = prefH;
				}
			});
		}
		if (boardWidth != null)
		{
			element.minWidth = boardWidth.Value * Unit;
			element.preferredWidth = boardWidth.Value * Unit;
		}
		if (boardHeight != null)
		{
			element.minHeight = boardHeight.Value * Unit;
			element.preferredHeight = boardHeight.Value * Unit;
		}
	}

	/// <summary>A grid's cell size and spacing (game units), recorded.</summary>
	protected void Grid(GridLayoutGroup? grid, Vector2 cell, Vector2 spacing)
	{
		if (grid == null)
		{
			return;
		}
		Vector2 wasCell = grid.cellSize, wasSpacing = grid.spacing;
		undo.Add(() =>
		{
			if (grid != null)
			{
				grid.cellSize = wasCell;
				grid.spacing = wasSpacing;
			}
		});
		grid.cellSize = cell;
		grid.spacing = spacing;
		LayoutRebuilder.MarkLayoutForRebuild(grid.transform.Cast<RectTransform>());
	}

	/// <summary>Stretches a rect over its parent (keeping its position offset, so the game's slides still work).</summary>
	protected void Stretch(Transform? target)
	{
		Move(target != null ? target.Cast<RectTransform>() : null, r =>
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = Vector2.one;
			r.pivot = new Vector2(0.5f, 0.5f);
			r.sizeDelta = Vector2.zero;
		});
	}

	/// <summary>Places a rect stretched across its parent between <paramref name="left"/> and <paramref name="right"/>
	/// (game units in from each side), keeping its vertical placement.</summary>
	protected static void Across(RectTransform rect, float left, float right)
	{
		rect.offsetMin = new Vector2(left, rect.offsetMin.y);
		rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
	}

	/// <summary>A button tinted the board's way: white image tint (so the image's own color shows), a step lighter on hover.</summary>
	protected void ChipTints(Selectable? selectable)
	{
		Tints(selectable, b =>
		{
			b.normalColor = Color.white;
			b.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
			b.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
			b.selectedColor = Color.white;
			b.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
			b.colorMultiplier = 1f;
			return b;
		});
	}

	/// <summary>The first object called <paramref name="name"/> anywhere under <paramref name="root"/>, inactive ones too.</summary>
	protected static Transform? Deep(Transform root, string name)
	{
		foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
		{
			if (item.name == name)
			{
				return item;
			}
		}
		return null;
	}

	/// <summary>The board's round back button (the same sprite as <see cref="AppLayouts"/>' header).</summary>
	protected Sprite BackChip()
	{
		return sprites.Get("backButton", () => Art.Glass(18 * Unit, Theme.PhoneChip, Theme.PhoneStroke, Art.PhoneEdge), mipmaps: true);
	}

	/// <summary>The board's 1 px divider along the top or bottom edge of <paramref name="parent"/>.</summary>
	protected Image Line(Transform parent, bool top, Color color)
	{
		Image line = Front(parent, top ? "top line" : "bottom line", null!, color);
		RectTransform rect = line.rectTransform;
		float y = top ? 1f : 0f;
		rect.anchorMin = new Vector2(0f, y);
		rect.anchorMax = new Vector2(1f, y);
		rect.pivot = new Vector2(0.5f, y);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = new Vector2(0f, Unit);
		return line;
	}

	/// <summary>Switches off one of the game's objects that only decorates (nothing in the game's code turns it back on).</summary>
	protected void Off(Transform parent, string path)
	{
		Transform? target = Find(parent, path);
		if (target != null)
		{
			Off(target.gameObject);
		}
	}

	protected void Off(GameObject item)
	{
		if (item == null || !item.activeSelf)
		{
			return;
		}
		item.SetActive(false);
		undo.Add(() =>
		{
			if (item != null)
			{
				item.SetActive(true);
			}
		});
	}

	protected static void Fill(RectTransform rect, float outset = 0f)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.offsetMin = new Vector2(-outset, -outset);
		rect.offsetMax = new Vector2(outset, outset);
	}

	/// <summary>Places a rect by its top-left corner in its parent's top-left space, board px.</summary>
	protected static void TopLeft(RectTransform rect, float x, float y, float width, float height)
	{
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = new Vector2(x * Unit, -y * Unit);
		rect.sizeDelta = new Vector2(width * Unit, height * Unit);
	}

	/// <summary>The board's colors, as Unity colors.</summary>
	protected static Color C(Rgba color)
	{
		return Ui.Color(color);
	}

	protected static Color C(int rgb, float alpha = 1f)
	{
		return Ui.Color(Rgba.Hex(rgb, alpha));
	}

	/// <summary>The board's card: rounded glass with the slate edge (radius in board px).</summary>
	protected Sprite Card(float boardRadius)
	{
		return sprites.PhoneGlass(boardRadius * Unit);
	}

	/// <summary>A solid rounded panel (radius in board px), white so it can be tinted.</summary>
	protected Sprite Solid(float boardRadius)
	{
		return sprites.Pill(boardRadius * Unit, Theme.White);
	}

	private readonly List<int> gone = new List<int>();

	private void Hold()
	{
		foreach (KeyValuePair<int, Paint> entry in paints)
		{
			Paint paint = entry.Value;
			if (!paint.Held)
			{
				continue;
			}
			Graphic graphic = paint.Graphic;
			if (graphic == null)
			{
				// The game destroyed it (Journal's card on every hover, an unfavourited product).
				gone.Add(entry.Key);
				continue;
			}
			Color now = graphic.color;
			if (Same(now, paint.Applied))
			{
				continue;
			}
			// The game set it again: that's its color now, and ours follows from it.
			paint.Original = now;
			paint.Applied = paint.Map(now);
			graphic.color = paint.Applied;
		}
		foreach (int id in gone)
		{
			paints.Remove(id);
			claims.Unclaim(id);
		}
		gone.Clear();
	}

	private static bool Same(Color a, Color b)
	{
		return Mathf.Abs(a.r - b.r) < 0.003f && Mathf.Abs(a.g - b.g) < 0.003f && Mathf.Abs(a.b - b.b) < 0.003f && Mathf.Abs(a.a - b.a) < 0.003f;
	}

	public void Dispose()
	{
		Exception? failure = null;
		foreach (KeyValuePair<int, Paint> entry in paints)
		{
			Paint paint = entry.Value;
			Ui.Step(ref failure, () =>
			{
				if (paint.Graphic != null && Same(paint.Graphic.color, paint.Applied))
				{
					paint.Graphic.color = paint.Original;
				}
			});
			claims.Unclaim(entry.Key);
		}
		paints.Clear();
		for (int i = undo.Count - 1; i >= 0; i--)
		{
			Action step = undo[i];
			Ui.Step(ref failure, step);
		}
		undo.Clear();
		styled.Clear();
		Ui.Rethrow(failure);
	}
}
