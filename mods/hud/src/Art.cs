using System;

namespace HylandHud;

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

	internal static Rgba Hex(int rgb, float a = 1f)
	{
		return new Rgba(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
	}

	internal Rgba WithAlpha(float a)
	{
		return new Rgba(R, G, B, a);
	}

	/// <summary>Straight-alpha "top over bottom".</summary>
	internal static Rgba Over(Rgba top, Rgba bottom)
	{
		float a = top.A + bottom.A * (1f - top.A);
		if (a <= 0f)
		{
			return default;
		}
		float k = bottom.A * (1f - top.A);
		return new Rgba((top.R * top.A + bottom.R * k) / a, (top.G * top.A + bottom.G * k) / a, (top.B * top.A + bottom.B * k) / a, a);
	}
}

/// <summary>The concept's colors (Downloads/hud-prototype.html), in one place.</summary>
internal static class Theme
{
	// No backdrop blur in uGUI, so the glass is a touch darker than the concept's .56 to keep text readable.
	internal static readonly Rgba GlassFill = Hex(0x0A0D12, 0.62f);
	internal static readonly Rgba GlassStroke = new Rgba(1f, 1f, 1f, 0.12f);
	internal static readonly Rgba Accent = Hex(0xFFD34D);
	internal static readonly Rgba Cash = Hex(0x6EE787);
	internal static readonly Rgba Bank = Hex(0x7DC4FF);
	internal static readonly Rgba Sun = Hex(0xFFD34D);
	internal static readonly Rgba Dusk = Hex(0xFF9F43);
	internal static readonly Rgba Moon = Hex(0xCFE3FF);
	internal static readonly Rgba White = new Rgba(1f, 1f, 1f, 1f);

	// The phone (the "Hyland HUD" canvas, phone board).
	internal static readonly Rgba PhoneGlass = Hex(0x0F1317, 0.88f);
	internal static readonly Rgba PhoneStroke = Hex(0x2E3845);
	internal static readonly Rgba PhoneChip = Hex(0x1B2128);
	internal static readonly Rgba PhoneWallpaper = Hex(0x0D1014);
	internal static readonly Rgba PhoneApp = Hex(0x0F1317);
	internal static readonly Rgba PhoneText = Hex(0xE8EAED);
	internal static readonly Rgba PhoneMuted = Hex(0x8A95A3);
	internal static readonly Rgba PhoneTrack = Hex(0x39424D);
	internal static readonly Rgba PhoneLine = Hex(0x262D35);
	internal static readonly Rgba PhoneAccent = Hex(0xFFE652);
	internal static readonly Rgba PhoneCase = Hex(0x1B1F24);
	internal static readonly Rgba Online = Hex(0x7DDC6F);
	internal static readonly Rgba Friend = Hex(0x5CC4FF);
	/// <summary>The game's own badge red (AppIcon prefab, Notifications).</summary>
	internal static readonly Rgba Badge = Hex(0xFF4949);

	private static Rgba Hex(int rgb, float a = 1f)
	{
		return Rgba.Hex(rgb, a);
	}
}

/// <summary>A generated image: rows bottom-up (Unity's order), plus the 9-slice border in pixels (0 = not sliced).</summary>
internal sealed class Bitmap
{
	internal readonly int Width;

	internal readonly int Height;

	internal readonly Rgba[] Pixels;

	/// <summary>Texture pixels per HUD unit (at the 1920x1080 reference).</summary>
	internal readonly int Scale;

	internal readonly int Border;

	internal Bitmap(int width, int height, int scale, int border)
	{
		Width = width;
		Height = height;
		Scale = scale;
		Border = border;
		Pixels = new Rgba[width * height];
	}

	/// <summary>Fills every pixel from HUD-unit coordinates measured from the center, y up.</summary>
	internal Bitmap Fill(Func<double, double, Rgba> pixel)
	{
		for (int row = 0; row < Height; row++)
		{
			for (int col = 0; col < Width; col++)
			{
				double x = ((col + 0.5) - Width / 2.0) / Scale;
				double y = ((row + 0.5) - Height / 2.0) / Scale;
				Pixels[row * Width + col] = pixel(x, y);
			}
		}
		return this;
	}
}

/// <summary>Pure shape math and the HUD's generated images (no Unity types), so they can be rendered and checked outside
/// the game (see preview/).</summary>
internal static class Art
{
	/// <summary>Texture pixels per HUD unit: 2 keeps edges smooth up to 4K.</summary>
	internal const int Scale = 2;

	internal static double SmoothStep(double edge0, double edge1, double x)
	{
		double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
		return t * t * (3.0 - 2.0 * t);
	}

	/// <summary>Signed distance to a rounded box centered on the origin (negative inside).</summary>
	internal static double RoundedBox(double x, double y, double halfW, double halfH, double radius)
	{
		double qx = Math.Abs(x) - (halfW - radius);
		double qy = Math.Abs(y) - (halfH - radius);
		double outside = Math.Sqrt(Math.Max(qx, 0.0) * Math.Max(qx, 0.0) + Math.Max(qy, 0.0) * Math.Max(qy, 0.0));
		return outside + Math.Min(Math.Max(qx, qy), 0.0) - radius;
	}

	internal static double Circle(double x, double y, double cx, double cy, double r)
	{
		double dx = x - cx, dy = y - cy;
		return Math.Sqrt(dx * dx + dy * dy) - r;
	}

	/// <summary>Distance to the segment a→b minus half the stroke width: an SVG line with round caps.</summary>
	internal static double Stroke(double x, double y, double ax, double ay, double bx, double by, double width)
	{
		double px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
		double h = Math.Clamp((px * dx + py * dy) / (dx * dx + dy * dy), 0.0, 1.0);
		double ex = px - dx * h, ey = py - dy * h;
		return Math.Sqrt(ex * ex + ey * ey) - width / 2.0;
	}

	/// <summary>Signed distance to triangle p0 p1 p2 (Inigo Quilez's sdTriangle).</summary>
	internal static double Triangle(double x, double y, double x0, double y0, double x1, double y1, double x2, double y2)
	{
		double e0x = x1 - x0, e0y = y1 - y0, e1x = x2 - x1, e1y = y2 - y1, e2x = x0 - x2, e2y = y0 - y2;
		double v0x = x - x0, v0y = y - y0, v1x = x - x1, v1y = y - y1, v2x = x - x2, v2y = y - y2;
		double h0 = Math.Clamp((v0x * e0x + v0y * e0y) / (e0x * e0x + e0y * e0y), 0.0, 1.0);
		double h1 = Math.Clamp((v1x * e1x + v1y * e1y) / (e1x * e1x + e1y * e1y), 0.0, 1.0);
		double h2 = Math.Clamp((v2x * e2x + v2y * e2y) / (e2x * e2x + e2y * e2y), 0.0, 1.0);
		double s = Math.Sign(e0x * e2y - e0y * e2x);
		double d0x = v0x - e0x * h0, d0y = v0y - e0y * h0;
		double d1x = v1x - e1x * h1, d1y = v1y - e1y * h1;
		double d2x = v2x - e2x * h2, d2y = v2y - e2y * h2;
		double dist = Math.Min(Math.Min(d0x * d0x + d0y * d0y, d1x * d1x + d1y * d1y), d2x * d2x + d2y * d2y);
		double side = Math.Min(Math.Min(s * (v0x * e0y - v0y * e0x), s * (v1x * e1y - v1y * e1x)), s * (v2x * e2y - v2y * e2x));
		return -Math.Sqrt(dist) * Math.Sign(side);
	}

	/// <summary>Pixel coverage from a signed distance in pixels: a one-pixel ramp across the edge.</summary>
	internal static float Coverage(double distancePx)
	{
		return (float)Math.Clamp(0.5 - distancePx, 0.0, 1.0);
	}

	/// <summary>A 9-sliced rounded panel: fill with a stroke on its inner edge, baked in real colors. The shape touches the
	/// texture's edges, so stretching keeps the sides straight.</summary>
	internal static Bitmap Glass(double radius, Rgba fill, Rgba stroke, double strokeWidth = 1.0)
	{
		int side = (int)Math.Ceiling(radius + 2.0) * 2 * Scale;
		double half = side / 2.0 / Scale;
		return new Bitmap(side, side, Scale, (int)Math.Ceiling(radius + 1.0) * Scale).Fill((x, y) =>
		{
			double d = RoundedBox(x, y, half, half, radius) * Scale;
			float shape = Coverage(d);
			if (shape <= 0f)
			{
				return default;
			}
			float ring = (float)Math.Clamp(d + strokeWidth * Scale + 0.5, 0.0, 1.0);
			Rgba color = Rgba.Over(stroke.WithAlpha(stroke.A * ring), fill);
			return color.WithAlpha(color.A * shape);
		});
	}

	/// <summary>A 9-sliced soft shadow for a panel with this corner radius; its image is the panel grown by
	/// <paramref name="blur"/> on every side.</summary>
	internal static Bitmap Shadow(double radius, double blur, float alpha)
	{
		int side = (int)Math.Ceiling(radius + blur + 2.0) * 2 * Scale;
		double half = side / 2.0 / Scale - blur;
		return new Bitmap(side, side, Scale, (int)Math.Ceiling(radius + blur + 1.0) * Scale).Fill((x, y) =>
		{
			double d = RoundedBox(x, y, half, half, radius);
			return new Rgba(0f, 0f, 0f, alpha * (float)(1.0 - SmoothStep(-blur * 0.35, blur, d)));
		});
	}

	/// <summary>The selected-slot underline: a <paramref name="width"/>×3 bar with a soft glow around it.</summary>
	internal static Bitmap GlowBar(double width, Rgba color, double glow = 8.0)
	{
		const double height = 3.0;
		int w = (int)Math.Ceiling(width + glow * 2.0) * Scale;
		int h = (int)Math.Ceiling(height + glow * 2.0) * Scale;
		return new Bitmap(w, h, Scale, 0).Fill((x, y) =>
		{
			double d = RoundedBox(x, y, width / 2.0, height / 2.0, height / 2.0);
			float core = Coverage(d * Scale);
			float halo = 0.55f * Halo(d, glow);
			return color.WithAlpha(Math.Max(core, halo * (1f - core)));
		});
	}

	/// <summary>The compass's heading marker: a 14×8 triangle pointing up, with a glow.</summary>
	internal static Bitmap Pointer(Rgba color)
	{
		const int scale = 3;
		const double glow = 5.0;
		int w = (int)((14 + glow * 2) * scale), h = (int)((8 + glow * 2) * scale);
		return new Bitmap(w, h, scale, 0).Fill((x, y) =>
		{
			double d = Triangle(x, y, -7.0, -4.0, 7.0, -4.0, 0.0, 4.0);
			float core = Coverage(d * scale);
			float halo = 0.6f * Halo(d, glow);
			return color.WithAlpha(Math.Max(core, halo * (1f - core)));
		});
	}

	/// <summary>Glow strength at distance <paramref name="d"/> outside a shape: an exponential falloff that reaches exactly
	/// zero at <paramref name="glow"/>, so it never shows the image's rectangle.</summary>
	private static float Halo(double d, double glow)
	{
		d = Math.Max(d, 0.0);
		return (float)(Math.Exp(-d / (glow * 0.35)) * (1.0 - SmoothStep(glow * 0.5, glow, d)));
	}

	internal static Bitmap Dot()
	{
		const int scale = 4;
		return new Bitmap(4 * scale + 2, 4 * scale + 2, scale, 0).Fill((x, y) => Theme.White.WithAlpha(Coverage(Circle(x, y, 0, 0, 2.0) * scale)));
	}

	/// <summary>The concept's edge mask for the compass: clear at both ends, solid from 22% to 78%.</summary>
	internal static float EdgeMask(double u)
	{
		return (float)Math.Clamp(Math.Min(u, 1.0 - u) / 0.22, 0.0, 1.0);
	}

	/// <summary>The compass baseline: one unit tall, faded at both ends by <see cref="EdgeMask"/>.</summary>
	internal static Bitmap FadeLine()
	{
		Bitmap bitmap = new Bitmap(256, 2, 1, 0);
		for (int row = 0; row < bitmap.Height; row++)
		{
			for (int col = 0; col < bitmap.Width; col++)
			{
				bitmap.Pixels[row * bitmap.Width + col] = Theme.White.WithAlpha(EdgeMask((col + 0.5) / bitmap.Width));
			}
		}
		return bitmap;
	}

	internal enum Icon
	{
		SunCore,
		SunRays,
		DuskSun,
		DuskHorizon,
		Moon,
		Cash,
		Bank,
		AppMessages,
		AppJournal,
		AppProducts,
		AppContacts,
		AppMap,
		AppDealers,
		AppDeliveries,
		AppHyland,
		Back,
		Rain
	}

	/// <summary>The phone's wallpaper: <see cref="Theme.PhoneWallpaper"/> with five faint rings around a point low on the
	/// right, at <paramref name="width"/>×<paramref name="height"/> canvas units drawn at half resolution (they're soft lines,
	/// and the image is stretched to the screen).</summary>
	internal static Bitmap Wallpaper(int width, int height)
	{
		int w = width / 2, h = height / 2;
		double cx = width * 0.79, cy = height * 0.19;
		double[] radii = { 0.28, 0.47, 0.68, 0.9, 1.14 };
		Rgba[] rings = { Rgba.Hex(0x1A2129), Rgba.Hex(0x171D24), Rgba.Hex(0x151A20), Rgba.Hex(0x13181D), Rgba.Hex(0x12161B) };
		Bitmap bitmap = new Bitmap(w, h, 1, 0);
		for (int row = 0; row < h; row++)
		{
			for (int col = 0; col < w; col++)
			{
				double x = (col + 0.5) * 2.0, y = (row + 0.5) * 2.0;
				Rgba color = Theme.PhoneWallpaper;
				for (int i = 0; i < radii.Length; i++)
				{
					// 4.5 canvas units wide: the board's 1.5 px lines, the phone screen being about 2× the board's.
					float line = Coverage((Math.Abs(Circle(x, y, cx, cy, radii[i] * width)) - 2.25) / 2.0);
					if (line > 0f)
					{
						color = Rgba.Over(rings[i].WithAlpha(line), color);
					}
				}
				bitmap.Pixels[row * w + col] = color;
			}
		}
		return bitmap;
	}

	/// <summary>A filled disc, for avatars and switch knobs.</summary>
	internal static Bitmap Disc(double radius, Rgba color)
	{
		int side = (int)Math.Ceiling(radius + 1.0) * 2 * Scale;
		return new Bitmap(side, side, Scale, 0).Fill((x, y) => color.WithAlpha(color.A * Coverage(Circle(x, y, 0, 0, radius) * Scale)));
	}

	/// <summary>The concept's 24×24 SVG icons as layers of (color, distance in viewBox units, y down like SVG). Parts
	/// that move on their own (the sun's rays, the setting sun) are separate icons.</summary>
	private static (Rgba Color, Func<double, double, double> Distance)[] Layers(Icon icon)
	{
		switch (icon)
		{
		case Icon.SunCore:
			return new (Rgba, Func<double, double, double>)[] { (Theme.Sun, (x, y) => Circle(x, y, 12, 12, 4.2)) };
		case Icon.SunRays:
			return new (Rgba, Func<double, double, double>)[] { (Theme.Sun, (x, y) => Min(
				Stroke(x, y, 12, 2, 12, 5, 1.8), Stroke(x, y, 12, 19, 12, 22, 1.8), Stroke(x, y, 2, 12, 5, 12, 1.8),
				Stroke(x, y, 19, 12, 22, 12, 1.8), Stroke(x, y, 4.9, 4.9, 7, 7, 1.8), Stroke(x, y, 17, 17, 19.1, 19.1, 1.8),
				Stroke(x, y, 4.9, 19.1, 7, 17, 1.8), Stroke(x, y, 17, 7, 19.1, 4.9, 1.8))) };
		case Icon.DuskSun:
			// Half disc above y = 14.5 plus three short rays.
			return new (Rgba, Func<double, double, double>)[] { (Theme.Dusk, (x, y) => Min(
				Math.Max(Circle(x, y, 12, 14.5, 6.5), y - 14.5), Stroke(x, y, 12, 3, 12, 5.5, 1.8),
				Stroke(x, y, 5.2, 6.2, 7, 8, 1.8), Stroke(x, y, 18.8, 6.2, 17, 8, 1.8))) };
		case Icon.DuskHorizon:
			return new (Rgba, Func<double, double, double>)[]
			{
				(Theme.White.WithAlpha(0.85f), (x, y) => Stroke(x, y, 3, 14.5, 21, 14.5, 1.8)),
				(Theme.White.WithAlpha(0.45f), (x, y) => Stroke(x, y, 6, 18.5, 18, 18.5, 1.8))
			};
		case Icon.Moon:
			// The SVG crescent: an r 8.5 disc minus the r 7 arc (SVG grows that radius to 7.755 so it spans the chord).
			return new (Rgba, Func<double, double, double>)[] { (Theme.Moon, (x, y) => Min(
				Math.Max(Circle(x, y, 14.28, 12.0, 8.5), -Circle(x, y, 17.5, 10.65, 7.755)),
				Circle(x, y, 5.5, 6, 1.1), Circle(x, y, 8.5, 3.5, 0.8))) };
		case Icon.Cash:
			return new (Rgba, Func<double, double, double>)[] { (Theme.Cash, (x, y) => Min(
				Math.Abs(RoundedBox(x - 12, y - 12, 10, 6, 2)) - 1.0, Math.Abs(Circle(x, y, 12, 12, 2.5)) - 1.0)) };
		case Icon.Bank:
			return new (Rgba, Func<double, double, double>)[] { (Theme.Bank, (x, y) => Min(
				Math.Abs(RoundedBox(x - 12, y - 12, 10, 7, 2)) - 1.0, Stroke(x, y, 2, 10, 22, 10, 2.0))) };
		// The phone's app glyphs: the board's 24×24 SVG strokes (width 2, round caps and joins), one color each.
		case Icon.AppMessages:
			// M4 5h16v11H9l-5 4z
			return One(Rgba.Hex(0x7DDC6F), (x, y) => Path(x, y, 4, 5, 20, 5, 20, 16, 9, 16, 4, 20, 4, 5));
		case Icon.AppJournal:
			return One(Rgba.Hex(0xFF8A5C), (x, y) => Min(Math.Abs(RoundedBox(x - 12, y - 12, 6, 9, 1)) - 1.0,
				Stroke(x, y, 9, 8, 15, 8, 2), Stroke(x, y, 9, 12, 15, 12, 2)));
		case Icon.AppProducts:
			// A box: the hexagon, its top face's front edges and the middle seam.
			return One(Rgba.Hex(0xFF8A7A), (x, y) => Min(Path(x, y, 3, 8, 12, 3, 21, 8, 21, 16, 12, 21, 3, 16, 3, 8),
				Path(x, y, 3, 8, 12, 13, 21, 8), Stroke(x, y, 12, 13, 12, 21, 2)));
		case Icon.AppContacts:
			return One(Theme.Friend, (x, y) => Min(Ring(x, y, 12, 9, 4), Arc(x, y, 12, 21, 8)));
		case Icon.AppMap:
			return One(Rgba.Hex(0x7DDC6F), (x, y) => Min(Path(x, y, 3, 6, 9, 3, 15, 6, 21, 3, 21, 18, 15, 21, 9, 18, 3, 21, 3, 6),
				Stroke(x, y, 9, 3, 9, 18, 2), Stroke(x, y, 15, 6, 15, 21, 2)));
		case Icon.AppDealers:
			return One(Rgba.Hex(0xA86BFF), (x, y) => Min(Ring(x, y, 8, 8, 3), Ring(x, y, 16, 8, 3), Arc(x, y, 8, 20, 6), Arc(x, y, 16, 20, 6)));
		case Icon.AppDeliveries:
			return One(Rgba.Hex(0xE58BFF), (x, y) => Min(Path(x, y, 2, 6, 14, 6, 14, 16, 2, 16, 2, 6),
				Path(x, y, 14, 10, 18, 10, 21, 13, 21, 16, 14, 16), Ring(x, y, 6, 18, 2), Ring(x, y, 17, 18, 2)));
		case Icon.AppHyland:
			// Three sliders; the knobs are punched out in the wallpaper color, ringed in the accent. First layer on top.
			return new (Rgba, Func<double, double, double>)[]
			{
				(Theme.PhoneAccent, (x, y) => Min(Ring(x, y, 9, 6, 2), Ring(x, y, 15, 12, 2), Ring(x, y, 11, 18, 2))),
				(Theme.PhoneWallpaper, (x, y) => Min(Circle(x, y, 9, 6, 2), Circle(x, y, 15, 12, 2), Circle(x, y, 11, 18, 2))),
				(Theme.PhoneAccent, (x, y) => Min(Stroke(x, y, 5, 6, 19, 6, 2), Stroke(x, y, 5, 12, 19, 12, 2), Stroke(x, y, 5, 18, 19, 18, 2)))
			};
		case Icon.Back:
			return One(Theme.PhoneText, (x, y) => Path(x, y, 15, 5, 8, 12, 15, 19));
		case Icon.Rain:
			// The board's cloud: three lobes on a flat bottom, outlined.
			return One(Theme.Friend, (x, y) => Math.Abs(Min(Circle(x, y, 7.5, 14, 4.2), Circle(x, y, 12.5, 10.5, 5.8),
				Circle(x, y, 17.5, 13.5, 4.2), RoundedBox(x - 12.5, y - 15.5, 5, 2.5, 0))) - 1.0);
		default:
			throw new ArgumentOutOfRangeException(nameof(icon));
		}
	}

	/// <summary>Renders an icon at <paramref name="sizePx"/> square; the first layer is drawn on top.</summary>
	internal static Bitmap Render(Icon icon, int sizePx)
	{
		(Rgba Color, Func<double, double, double> Distance)[] layers = Layers(icon);
		double pxPerUnit = sizePx / 24.0;
		Bitmap bitmap = new Bitmap(sizePx, sizePx, 1, 0);
		for (int row = 0; row < sizePx; row++)
		{
			for (int col = 0; col < sizePx; col++)
			{
				// Bitmap rows are bottom-up; the SVG's y runs down.
				double x = (col + 0.5) / pxPerUnit;
				double y = (sizePx - row - 0.5) / pxPerUnit;
				Rgba color = default;
				for (int i = layers.Length - 1; i >= 0; i--)
				{
					float coverage = Coverage(layers[i].Distance(x, y) * pxPerUnit);
					if (coverage > 0f)
					{
						color = Rgba.Over(layers[i].Color.WithAlpha(layers[i].Color.A * coverage), color);
					}
				}
				bitmap.Pixels[row * sizePx + col] = color;
			}
		}
		return bitmap;
	}

	private static (Rgba Color, Func<double, double, double> Distance)[] One(Rgba color, Func<double, double, double> distance)
	{
		return new (Rgba, Func<double, double, double>)[] { (color, distance) };
	}

	/// <summary>An open or closed polyline through (x0, y0), (x1, y1)…, stroked 2 wide with round joins.</summary>
	private static double Path(double x, double y, params double[] points)
	{
		double d = double.MaxValue;
		for (int i = 0; i + 3 < points.Length; i += 2)
		{
			d = Math.Min(d, Stroke(x, y, points[i], points[i + 1], points[i + 2], points[i + 3], 2.0));
		}
		return d;
	}

	/// <summary>A circle outline, stroke 2.</summary>
	private static double Ring(double x, double y, double cx, double cy, double r)
	{
		return Math.Abs(Circle(x, y, cx, cy, r)) - 1.0;
	}

	/// <summary>The upper half of a circle outline (SVG "a r r 0 0 1 2r 0"), stroke 2, round ends.</summary>
	private static double Arc(double x, double y, double cx, double cy, double r)
	{
		double half = Math.Max(Ring(x, y, cx, cy, r), y - cy);
		return Math.Min(half, Math.Min(Circle(x, y, cx - r, cy, 1.0), Circle(x, y, cx + r, cy, 1.0)));
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
}
