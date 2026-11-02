using System;

namespace HylandMinimap;

internal readonly struct Rgba
{
	internal readonly float R, G, B, A;

	internal Rgba(float r, float g, float b, float a)
	{
		R = r;
		G = g;
		B = b;
		A = a;
	}

	internal static Rgba Lerp(Rgba from, Rgba to, float t)
	{
		return new Rgba(from.R + (to.R - from.R) * t, from.G + (to.G - from.G) * t, from.B + (to.B - from.B) * t, from.A + (to.A - from.A) * t);
	}

	internal Rgba WithAlpha(float a)
	{
		return new Rgba(R, G, B, a);
	}
}

/// <summary>
/// Pure shape math for the generated sprites (no Unity types), so the edges can be rendered and checked outside the game.
/// </summary>
internal static class Shapes
{
	internal const int Samples = 4;

	/// <summary>Share of pixel (px, py) in a size×size texture that <paramref name="inside"/> covers, on a 4×4 grid.
	/// <paramref name="inside"/> takes coordinates in -1..1, y up.</summary>
	internal static float Coverage(int px, int py, int size, Func<double, double, bool> inside)
	{
		int hits = 0;
		for (int sy = 0; sy < Samples; sy++)
		{
			for (int sx = 0; sx < Samples; sx++)
			{
				double x = (px + (sx + 0.5) / Samples) / size * 2.0 - 1.0;
				double y = (py + (sy + 0.5) / Samples) / size * 2.0 - 1.0;
				if (inside(x, y))
				{
					hits++;
				}
			}
		}
		return hits / (float)(Samples * Samples);
	}

	internal static bool InCircle(double x, double y)
	{
		return x * x + y * y <= 1.0;
	}

	internal static bool InArrow(double x, double y)
	{
		bool body = y > -0.65 && y < 0.9 && Math.Abs(x) < (0.9 - y) * 0.48;
		bool notch = y < -0.1 && Math.Abs(x) < (-0.1 - y) * 0.38;
		return body && !notch;
	}

	/// <summary>How much of a pixel at distance <paramref name="r"/> lies between <paramref name="inner"/> and
	/// <paramref name="outer"/> (all in pixels): a one-pixel ramp on each edge.</summary>
	internal static float Band(double r, double inner, double outer)
	{
		return (float)(Math.Clamp(r - inner + 0.5, 0.0, 1.0) * Math.Clamp(outer - r + 0.5, 0.0, 1.0));
	}

	/// <summary>The rim drawn over the map's edge, by distance from the center in pixels. Unity's Mask cuts the map with a
	/// hard stencil edge at <paramref name="mapR"/>, so the rim starts a pixel inside it and covers that edge smoothly.
	/// Border color up to <paramref name="borderR"/>, outline color out to <paramref name="outerR"/>.</summary>
	internal static Rgba RimPixel(double r, double mapR, double borderR, double outerR, Rgba border, Rgba outline)
	{
		float coverage = Band(r, mapR - 1.0, outerR);
		if (coverage <= 0f)
		{
			return default;
		}
		Rgba color = Rgba.Lerp(border, outline, (float)Math.Clamp(r - borderR + 0.5, 0.0, 1.0));
		return color.WithAlpha(color.A * coverage);
	}
}
