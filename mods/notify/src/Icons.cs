using System;
using HylandHud;
using UnityEngine;

namespace HylandNotify;

/// <summary>
/// The board's 24×24 line icons (2-wide strokes, as in its SVGs), drawn white so an Image tints them. Kept here rather
/// than in Hyland HUD's <c>Art.Icon</c> so the two mods don't edit the same file; the shape math is HUD's.
/// </summary>
internal static class Icons
{
	internal enum Kind
	{
		Eye,
		Siren,
		Cuff,
		Hand
	}

	private const double Line = 2.0;

	internal static Sprite Get(Sprites sprites, Kind kind)
	{
		return sprites.Get("notify-icon-" + kind, () => Render(Distance(kind), 64), mipmaps: true);
	}

	/// <summary>An outline only (no fill) of a rounded rect, for colored borders and alarm rings; 9-sliced.</summary>
	internal static Sprite Outline(Sprites sprites, double radius)
	{
		return sprites.Get("notify-outline" + radius, () => Art.Glass(radius, Theme.White.WithAlpha(0f), Theme.White));
	}

	private static Func<double, double, double> Distance(Kind kind)
	{
		switch (kind)
		{
		case Kind.Eye:
			// M2 12s4-7 10-7 10 7 10 7-4 7-10 7-10-7-10-7z: a lens through (2,12), (12,5), (22,12), (12,19), as two
			// circles of radius 10.64 centered 3.64 above and below the middle; then the pupil, r 3.
			return (x, y) => Math.Min(
				Math.Abs(Math.Max(Art.Circle(x, y, 12, 15.643, 10.643), Art.Circle(x, y, 12, 8.357, 10.643))) - Line / 2,
				Math.Abs(Art.Circle(x, y, 12, 12, 3)) - Line / 2);
		case Kind.Siren:
			// M7 18v-6a5 5 0 0 1 10 0v6 M5 21h14 M12 3v2 M4.2 7.2l1.4 1.4 M19.8 7.2l-1.4 1.4
			return (x, y) => Min(
				Math.Max(Math.Abs(Art.Circle(x, y, 12, 12, 5)) - Line / 2, y - 12),
				Art.Stroke(x, y, 7, 12, 7, 18, Line), Art.Stroke(x, y, 17, 12, 17, 18, Line),
				Art.Stroke(x, y, 5, 21, 19, 21, Line), Art.Stroke(x, y, 12, 3, 12, 5, Line),
				Art.Stroke(x, y, 4.2, 7.2, 5.6, 8.6, Line), Art.Stroke(x, y, 19.8, 7.2, 18.4, 8.6, Line));
		case Kind.Cuff:
			// Two rings (r 4 at (7,14) and (17,10)) and the chain between them.
			return (x, y) => Min(
				Math.Abs(Art.Circle(x, y, 7, 14, 4)) - Line / 2,
				Math.Abs(Art.Circle(x, y, 17, 10, 4)) - Line / 2,
				Art.Stroke(x, y, 10.5, 12.5, 13.5, 9.5, Line));
		case Kind.Hand:
			// A raised hand's outline: three fingers over a palm.
			return (x, y) => Math.Abs(Min(
				Art.Stroke(x, y, 9.5, 5, 9.5, 13, 3), Art.Stroke(x, y, 12.5, 4, 12.5, 12, 3),
				Art.Stroke(x, y, 15.5, 5.5, 15.5, 13, 3), Art.RoundedBox(x - 12.5, y - 15.5, 4.5, 4.5, 3))) - Line / 2;
		default:
			throw new ArgumentOutOfRangeException(nameof(kind));
		}
	}

	private static double Min(params double[] values)
	{
		double min = double.MaxValue;
		foreach (double value in values)
		{
			min = Math.Min(min, value);
		}
		return min;
	}

	private static Bitmap Render(Func<double, double, double> distance, int sizePx)
	{
		double pxPerUnit = sizePx / 24.0;
		Bitmap bitmap = new Bitmap(sizePx, sizePx, 1, 0);
		for (int row = 0; row < sizePx; row++)
		{
			for (int col = 0; col < sizePx; col++)
			{
				// Bitmap rows are bottom-up; the SVG's y runs down.
				double x = (col + 0.5) / pxPerUnit;
				double y = (sizePx - row - 0.5) / pxPerUnit;
				float coverage = (float)Math.Clamp(0.5 - distance(x, y) * pxPerUnit, 0.0, 1.0);
				bitmap.Pixels[row * sizePx + col] = Theme.White.WithAlpha(coverage);
			}
		}
		return bitmap;
	}
}
