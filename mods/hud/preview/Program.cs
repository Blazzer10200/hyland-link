using System;
using System.IO;
using System.IO.Compression;

namespace HylandHud;

/// <summary>Draws the HUD's real generated sprites in the concept's 1920x1080 layout (no text: that's the game's font)
/// plus a contact sheet of every sprite, so edges and icons can be checked without the game.</summary>
internal static class Program
{
	private static int Main(string[] args)
	{
		if (args.Length != 1)
		{
			Console.Error.WriteLine("usage: dotnet run --project mods/hud/preview -- <output folder>");
			return 2;
		}
		Directory.CreateDirectory(args[0]);
		string layout = Path.Combine(args[0], "hud-layout.png");
		Png.Write(layout, Layout());
		string sheet = Path.Combine(args[0], "hud-sprites.png");
		Png.Write(sheet, Sheet());
		string phone = Path.Combine(args[0], "hud-phone.png");
		Png.Write(phone, Phone());
		Console.WriteLine(layout);
		Console.WriteLine(sheet);
		Console.WriteLine(phone);
		return 0;
	}

	/// <summary>Board pixels to the phone canvas's units, as in PhoneSkin and HylandApp.</summary>
	private const double U = 655.0 / 316.0;

	/// <summary>The glass phone in the game's home canvas units (655×1201): the home screen as PhoneSkin lays it out, and
	/// the top of the Hyland app as HylandApp does, each in PhoneSkin's frame. Gray bars stand in for text (the game's font
	/// draws it).</summary>
	private static Canvas Phone()
	{
		const int W = 655, H = 1201, Gap = 110;
		Canvas c = new Canvas(W * 2 + Gap * 3, H + Gap * 2);
		c.Fill(0, 0, c.Width, c.Height, Rgba.Hex(0x24282E));
		Home(c, Gap, Gap);
		Frame(c, Gap, Gap);
		App(c, Gap * 2 + W, Gap);
		Frame(c, Gap * 2 + W, Gap);
		return c;
	}

	/// <summary>PhoneSkin.BuildBody's frame, speaker slot and home bar around a 655×1201 screen at (ox, oy).</summary>
	private static void Frame(Canvas c, double ox, double oy)
	{
		double side = 12 * U, top = 36 * U, bottom = 24 * U;
		c.Draw(Art.Frame(46 * U, 30 * U, side, top, bottom, Theme.PhoneCase, Theme.PhoneCaseEdge, Art.PhoneEdge), ox - side, oy - top, 655 + 2 * side, 1201 + top + bottom, 1f);
		c.Draw(Art.Glass(4 * U, Theme.PhoneSpeaker, Theme.PhoneSpeaker.WithAlpha(0f)), ox + 655 / 2.0 - 20 * U, oy - 14 * U - 4 * U, 40 * U, 8 * U, 1f);
		c.Draw(Art.Glass(2 * U, Theme.PhoneHomeBar, Theme.PhoneHomeBar.WithAlpha(0f)), ox + 655 / 2.0 - 40 * U, oy + 1201 - 10 * U - 2 * U, 80 * U, 4 * U, 1f);
	}

	private static void Home(Canvas c, double ox, double oy)
	{
		c.Fill(ox, oy, 655, 1201, Theme.PhoneWallpaper);
		c.Draw(Art.Wallpaper(655, 1161), ox, oy + 40, 655, 1161, 1f, sliced: false);
		// Info bar: the time left, the green dot and "5G" right.
		// Moved in 10 board px from the game's 10, clear of the frame's rounded corners (PhoneSkin.BarInset).
		Text(c, ox + 10 + 10 * U, oy + 8, 150, 24 * 0.9);
		c.Draw(Art.Disc(3, Theme.Online), ox + 655 - 10 - 10 * U - 40 - 8 * U - 6 * U, oy + 20 - 3 * U, 6 * U, 6 * U, 1f, sliced: false);
		Text(c, ox + 655 - 10 - 10 * U - 40, oy + 8, 40, 24 * 0.9);

		double cardTop = oy + 40 + 8 * U, cardH = 154 * U, cardW = 655 - 32 * U, cardX = ox + 16 * U;
		c.Draw(Art.Glass(20 * U, Theme.PhoneGlass, Theme.PhoneStroke, Art.PhoneEdge), cardX, cardTop, cardW, cardH, 1f);
		Text(c, cardX + 16 * U, cardTop + 14 * U, 110 * U, 40 * U, Theme.PhoneText);
		Text(c, cardX + 16 * U, cardTop + 62 * U, 70 * U, 13 * U, Theme.PhoneMuted);
		c.Draw(Art.Render(Art.Icon.Rain, 128), cardX + cardW - 38 * U - 17 * U, cardTop + 30 * U - 17 * U, 34 * U, 34 * U, 1f, sliced: false);
		double chipW = (cardW - 40 * U) / 2, chipH = 48 * U, chipY = cardTop + cardH - 14 * U - chipH;
		Bitmap chip = Art.Glass(12 * U, Theme.PhoneChip, Theme.PhoneChip.WithAlpha(0f));
		for (int i = 0; i < 2; i++)
		{
			double x = cardX + 16 * U + i * (chipW + 8 * U);
			c.Draw(chip, x, chipY, chipW, chipH, 1f);
			Text(c, x + 10 * U, chipY + 7 * U, 40 * U, 11 * U, Theme.PhoneMuted);
			Text(c, x + 10 * U, chipY + 25 * U, 70 * U, 15 * U, i == 0 ? Theme.PhoneAccent : Theme.PhoneText);
		}

		const double tile = 58 * U, radius = 17 * U;
		double gap = 73 * U - tile, gridW = 4 * tile + 3 * gap, gx = ox + (655 - gridW) / 2, gy = cardTop + cardH + 26 * U;
		Art.Icon[] apps = { Art.Icon.AppMessages, Art.Icon.AppJournal, Art.Icon.AppProducts, Art.Icon.AppContacts, Art.Icon.AppMap, Art.Icon.AppDealers, Art.Icon.AppDeliveries, Art.Icon.AppHyland };
		for (int i = 0; i < apps.Length; i++)
		{
			double x = gx + i % 4 * (tile + gap), y = gy + i / 4 * 98 * U;
			bool hyland = apps[i] == Art.Icon.AppHyland;
			c.Draw(Art.Glass(radius, Theme.PhoneGlass, hyland ? Theme.PhoneAccent : Theme.PhoneStroke, Art.PhoneEdge), x, y, tile, tile, 1f);
			c.Draw(Art.Render(apps[i], 128), x + (tile - 26 * U) / 2, y + (tile - 26 * U) / 2, 26 * U, 26 * U, 1f, sliced: false);
			c.Draw(Art.Glass(5 * U, Theme.PhoneLine, Theme.PhoneTrack, U), x + 3 * U - 8 * U, y + tile - 3 * U - 8 * U, 16 * U, 16 * U, 1f);
			Text(c, x + tile / 2 - 22 * U, y + tile + 8 * U, 44 * U, 11 * U, hyland ? Theme.PhoneAccent : Theme.PhoneText);
			if (apps[i] == Art.Icon.AppMessages || apps[i] == Art.Icon.AppDeliveries)
			{
				c.Draw(Art.Glass(9 * U, Theme.Badge, Theme.Badge.WithAlpha(0f)), x + tile - 4 * U - 9 * U, y + 4 * U - 9 * U, 18 * U, 18 * U, 1f);
			}
		}
	}

	private static void App(Canvas c, double ox, double oy)
	{
		c.Fill(ox, oy, 655, 1201, Theme.PhoneApp);
		double pad = 16 * U, y = oy + 16 * U;
		c.Draw(Art.Glass(18 * U, Theme.PhoneChip, Theme.PhoneStroke, Art.PhoneEdge), ox + pad, y, 36 * U, 36 * U, 1f);
		c.Draw(Art.Render(Art.Icon.Back, 128), ox + pad + 9 * U, y + 9 * U, 18 * U, 18 * U, 1f, sliced: false);
		Text(c, ox + pad + 46 * U, y + 8 * U, 70 * U, 20 * U, Theme.PhoneText);
		y += 36 * U + 10 * U;

		double width = 655 - 2 * pad, inner = width - 28 * U;
		y += 6 * U;
		Text(c, ox + pad, y, 26 * U, 11 * U, Theme.PhoneAccent);
		y += 20 * U;
		double rows = 2 * U + 7 * 44 * U + 10 * U + 42 * U + 12 * U;
		c.Draw(Art.Glass(16 * U, Theme.PhoneGlass, Theme.PhoneStroke, Art.PhoneEdge), ox + pad, y, width, rows, 1f);
		double row = y + 2 * U, left = ox + pad + 14 * U;
		Bitmap knob = Art.Disc(9 * U, Rgba.Hex(0xF1F3F5));
		for (int i = 0; i < 7; i++)
		{
			bool on = i != 3;
			Text(c, left, row + 15 * U, 110 * U, 14 * U, Theme.PhoneText);
			double tx = left + inner - 40 * U, ty = row + 10 * U;
			c.Draw(Art.Glass(12 * U, on ? Theme.PhoneAccent : Theme.PhoneTrack, Theme.White.WithAlpha(0f)), tx, ty, 40 * U, 24 * U, 1f);
			c.Draw(knob, tx + (on ? 19 : 3) * U, ty + 3 * U, 18 * U, 18 * U, 1f, sliced: false);
			row += 44 * U;
			if (i < 6)
			{
				c.Fill(left, row, inner, Math.Max(1, U), Theme.PhoneLine);
			}
		}
		row += 10 * U;
		Text(c, left, row, 60 * U, 14 * U, Theme.PhoneText);
		Text(c, left + inner - 12 * U, row, 12 * U, 14 * U, Theme.PhoneMuted);
		row += 24 * U;
		double fill = 6 / 20.0;
		c.Draw(Art.Glass(2 * U, Theme.PhoneTrack, Theme.White.WithAlpha(0f)), left, row + 7 * U, inner, 4 * U, 1f);
		c.Draw(Art.Glass(2 * U, Theme.PhoneAccent, Theme.White.WithAlpha(0f)), left, row + 7 * U, 8 * U + (inner - 16 * U) * fill, 4 * U, 1f);
		c.Draw(Art.Disc(8 * U, Theme.PhoneAccent), left + 8 * U + (inner - 16 * U) * fill - 8 * U, row + 1 * U, 16 * U, 16 * U, 1f, sliced: false);
		y += rows;

		y += 16 * U;
		Text(c, ox + pad, y, 40 * U, 11 * U, Theme.PhoneAccent);
		y += 20 * U;
		c.Draw(Art.Glass(16 * U, Theme.PhoneGlass, Theme.PhoneStroke, Art.PhoneEdge), ox + pad, y, width, 92 * U, 1f);
	}

	/// <summary>A rounded bar where a line of text would sit.</summary>
	private static void Text(Canvas c, double x, double y, double w, double h, Rgba? color = null)
	{
		c.Fill(x, y + h * 0.2, w, h * 0.6, (color ?? Theme.White).WithAlpha(0.35f));
	}

	private static Canvas Layout()
	{
		Canvas c = new Canvas(1920, 1080);
		c.Backdrop();
		Bitmap glass22 = Art.Glass(22, Theme.GlassFill, Theme.GlassStroke);
		Bitmap glass16 = Art.Glass(16, Theme.GlassFill, Theme.GlassStroke);
		Bitmap glass15 = Art.Glass(15, Theme.GlassFill, Theme.GlassStroke);
		Bitmap glass10 = Art.Glass(10, Theme.GlassFill, Theme.GlassStroke);
		Bitmap slot = Art.Glass(14, Theme.White.WithAlpha(0.33f), Theme.White);
		// Compass: top 20, 480 wide, centered.
		double cx = 960, top = 20;
		Bitmap dot = Art.Dot();
		double heading = 20; // degrees, so the strip is mid-turn like the concept's demo
		for (int i = 0; i < 8; i++)
		{
			double x = cx + Delta(heading, i * 45.0) * 160.0 / 90.0;
			float mask = Art.EdgeMask((x - (cx - 240)) / 480.0);
			if (i % 2 == 1)
			{
				c.Draw(dot, x - 2, top + 12, 4, 4, 0.45f * mask);
			}
			else
			{
				// Stand-in for the letter (the game's font draws it): a short bar where the glyph sits.
				c.Fill(x - (i == 0 ? 6 : 5), top + 4, i == 0 ? 12 : 10, i == 0 ? 20 : 16, Theme.White.WithAlpha((i == 0 ? 1f : 0.72f) * mask * 0.35f));
			}
		}
		c.Draw(Art.FadeLine(), cx - 240, top + 33, 480, 1, 0.35f);
		c.Draw(Art.Pointer(Theme.Accent), cx - 12, top + 34 - 5, 24, 18, 1f);
		// Clock pill under it.
		double pillW = 8 + 22 + 8 + 64 + 12;
		c.Draw(glass15, cx - pillW / 2, top + 54, pillW, 30, 1f);
		double ix = cx - pillW / 2 + 8, iy = top + 54 + 4;
		c.Draw(Art.Render(Art.Icon.DuskSun, 64), ix, iy, 22, 22, 1f);
		c.Draw(Art.Render(Art.Icon.DuskHorizon, 64), ix, iy, 22, 22, 1f);
		// Hotbar: the mod's layout (gap 6, a double gap with the separator, then the cash slot), 28 from the bottom.
		const double w = 72, gap = 6;
		double[] centers = new double[9];
		double run = 0;
		for (int i = 0; i < 8; i++)
		{
			centers[i] = run + w / 2 + gap;
			run += w + gap;
		}
		run += gap;
		centers[8] = run + w / 2 + gap;
		run += w + gap;
		double offset = cx - run / 2;
		double railLeft = offset + gap - 12, railRight = offset + centers[8] + w / 2 + 12;
		double railBottom = 1080 - 28, railTop = railBottom - (w + 20);
		c.Draw(Art.Shadow(22, 16, 0.3f), railLeft - 16, railTop - 16 + 6, railRight - railLeft + 32, railBottom - railTop + 32, 1f);
		c.Draw(glass22, railLeft, railTop, railRight - railLeft, railBottom - railTop, 1f);
		const int selected = 1;
		for (int i = 0; i < 9; i++)
		{
			double s = i == selected ? 1.07 : 1.0;
			double lift = i == selected ? 10 : 0;
			double x = offset + centers[i], y = railTop + 10 + w / 2 - lift;
			c.Draw(slot, x - w * s / 2, y - w * s / 2, w * s, w * s, i == selected ? 0.4f : 0.12f);
		}
		double sepX = offset + centers[7] + w / 2 + gap;
		c.Fill(sepX - 0.5, railTop + 22, 1, w - 24, Theme.White.WithAlpha(0.18f));
		double selX = offset + centers[selected];
		c.Draw(Art.GlowBar(w, Theme.Accent), selX - (w + 16) / 2, railBottom - 7.5 - 9.5, w + 16, 19, 1f);
		double nameW = 110;
		c.Draw(glass10, selX - nameW / 2, railTop - 34, nameW, 26, 1f);
		// Bank panel: 12 right of the rail, bottoms aligned (the cash slot on the rail shows the cash).
		double mw = 14 + 18 + 8 + 62 + 14, mh = 10 + 20 + 10;
		double mx = railRight + 12, my = railBottom - mh;
		c.Draw(Art.Shadow(16, 16, 0.3f), mx - 16, my - 16 + 6, mw + 32, mh + 32, 1f);
		c.Draw(glass16, mx, my, mw, mh, 1f);
		c.Draw(Art.Render(Art.Icon.Bank, 64), mx + 14, my + 10 + 1, 18, 18, 1f);
		return c;
	}

	private static double Delta(double from, double to)
	{
		double d = (to - from) % 360.0;
		return d > 180 ? d - 360 : d < -180 ? d + 360 : d;
	}

	private static Canvas Sheet()
	{
		Bitmap[] items =
		{
			Art.Glass(22, Theme.GlassFill, Theme.GlassStroke), Art.Glass(14, Theme.White.WithAlpha(0.33f), Theme.White),
			Art.Shadow(22, 16, 0.3f), Art.GlowBar(72, Theme.Accent), Art.Pointer(Theme.Accent), Art.Dot(),
			Art.Render(Art.Icon.SunCore, 64), Art.Render(Art.Icon.SunRays, 64), Art.Render(Art.Icon.DuskSun, 64),
			Art.Render(Art.Icon.DuskHorizon, 64), Art.Render(Art.Icon.Moon, 64), Art.Render(Art.Icon.Cash, 64),
			Art.Render(Art.Icon.Bank, 64), Art.Render(Art.Icon.AppMessages, 128), Art.Render(Art.Icon.AppJournal, 128),
			Art.Render(Art.Icon.AppProducts, 128), Art.Render(Art.Icon.AppContacts, 128), Art.Render(Art.Icon.AppMap, 128),
			Art.Render(Art.Icon.AppDealers, 128), Art.Render(Art.Icon.AppDeliveries, 128), Art.Render(Art.Icon.AppHyland, 128),
			Art.Render(Art.Icon.Back, 128), Art.Render(Art.Icon.Rain, 128)
		};
		Canvas c = new Canvas(items.Length * 140 + 20, 180);
		c.Fill(0, 0, c.Width, c.Height, Rgba.Hex(0x3A4048));
		for (int i = 0; i < items.Length; i++)
		{
			Bitmap b = items[i];
			double scale = Math.Min(120.0 / b.Width, 120.0 / b.Height);
			double dw = b.Width * scale, dh = b.Height * scale;
			c.Draw(b, 20 + i * 140 + (120 - dw) / 2, 30 + (120 - dh) / 2, dw, dh, 1f, sliced: false);
		}
		return c;
	}
}

/// <summary>An RGBA canvas in HUD units (1 pixel = 1 unit), top-down.</summary>
internal sealed class Canvas
{
	internal readonly int Width;

	internal readonly int Height;

	internal readonly Rgba[] Pixels;

	internal Canvas(int width, int height)
	{
		Width = width;
		Height = height;
		Pixels = new Rgba[width * height];
	}

	/// <summary>A stand-in for the game world: dusk sky over a darker street.</summary>
	internal void Backdrop()
	{
		for (int y = 0; y < Height; y++)
		{
			double t = (double)y / Height;
			Rgba sky = Lerp(Rgba.Hex(0x6E7F96), Rgba.Hex(0xC98A5E), Math.Clamp(t / 0.55, 0, 1));
			Rgba ground = Lerp(Rgba.Hex(0x4A4740), Rgba.Hex(0x2B2A27), Math.Clamp((t - 0.55) / 0.45, 0, 1));
			Rgba row = t < 0.55 ? sky : ground;
			for (int x = 0; x < Width; x++)
			{
				Pixels[y * Width + x] = row;
			}
		}
	}

	private static Rgba Lerp(Rgba a, Rgba b, double t)
	{
		float k = (float)t;
		return new Rgba(a.R + (b.R - a.R) * k, a.G + (b.G - a.G) * k, a.B + (b.B - a.B) * k, 1f);
	}

	internal void Fill(double x, double y, double w, double h, Rgba color)
	{
		for (int py = (int)Math.Floor(y); py < (int)Math.Ceiling(y + h); py++)
		{
			for (int px = (int)Math.Floor(x); px < (int)Math.Ceiling(x + w); px++)
			{
				Blend(px, py, color);
			}
		}
	}

	/// <summary>Draws <paramref name="b"/> into the rect (x, y, w, h), 9-sliced when it has a border, like a uGUI Image.
	/// Bitmap rows are bottom-up; the canvas is top-down.</summary>
	internal void Draw(Bitmap b, double x, double y, double w, double h, float alpha, bool sliced = true)
	{
		double border = sliced && b.Border > 0 ? (double)b.Border / b.Scale : 0;
		for (int py = (int)Math.Floor(y); py < (int)Math.Ceiling(y + h); py++)
		{
			for (int px = (int)Math.Floor(x); px < (int)Math.Ceiling(x + w); px++)
			{
				double u = px + 0.5 - x, v = py + 0.5 - y;
				double sx = Map(u, w, border, b.Width, b.Scale, sliced && border > 0);
				double sy = Map(v, h, border, b.Height, b.Scale, sliced && border > 0);
				Rgba s = Sample(b, sx, b.Height - sy);
				Blend(px, py, s.WithAlpha(s.A * alpha));
			}
		}
	}

	private static double Map(double u, double size, double border, int texSize, int scale, bool sliced)
	{
		if (!sliced)
		{
			return u / size * texSize;
		}
		if (u < border)
		{
			return u * scale;
		}
		if (u > size - border)
		{
			return texSize - (size - u) * scale;
		}
		double mid = size - border * 2;
		return border * scale + (u - border) / Math.Max(mid, 1e-6) * (texSize - border * scale * 2);
	}

	private static Rgba Sample(Bitmap b, double sx, double sy)
	{
		double fx = Math.Clamp(sx - 0.5, 0, b.Width - 1), fy = Math.Clamp(sy - 0.5, 0, b.Height - 1);
		int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(x0 + 1, b.Width - 1), y1 = Math.Min(y0 + 1, b.Height - 1);
		double tx = fx - x0, ty = fy - y0;
		Rgba a = b.Pixels[y0 * b.Width + x0], c = b.Pixels[y0 * b.Width + x1], d = b.Pixels[y1 * b.Width + x0], e = b.Pixels[y1 * b.Width + x1];
		// Premultiplied bilinear, like the GPU's filtering of an RGBA texture's edges.
		double w0 = (1 - tx) * (1 - ty), w1 = tx * (1 - ty), w2 = (1 - tx) * ty, w3 = tx * ty;
		double alpha = a.A * w0 + c.A * w1 + d.A * w2 + e.A * w3;
		if (alpha <= 0)
		{
			return default;
		}
		double r = (a.R * a.A * w0 + c.R * c.A * w1 + d.R * d.A * w2 + e.R * e.A * w3) / alpha;
		double g = (a.G * a.A * w0 + c.G * c.A * w1 + d.G * d.A * w2 + e.G * e.A * w3) / alpha;
		double bl = (a.B * a.A * w0 + c.B * c.A * w1 + d.B * d.A * w2 + e.B * e.A * w3) / alpha;
		return new Rgba((float)r, (float)g, (float)bl, (float)alpha);
	}

	private void Blend(int x, int y, Rgba top)
	{
		if (x < 0 || y < 0 || x >= Width || y >= Height || top.A <= 0f)
		{
			return;
		}
		Pixels[y * Width + x] = Rgba.Over(top, Pixels[y * Width + x]);
	}
}

internal static class Png
{
	private static readonly uint[] Crc = BuildCrc();

	internal static void Write(string path, Canvas c)
	{
		byte[] raw = new byte[c.Height * (c.Width * 4 + 1)];
		int i = 0;
		for (int y = 0; y < c.Height; y++)
		{
			raw[i++] = 0;
			for (int x = 0; x < c.Width; x++)
			{
				Rgba p = c.Pixels[y * c.Width + x];
				raw[i++] = Byte(p.R);
				raw[i++] = Byte(p.G);
				raw[i++] = Byte(p.B);
				raw[i++] = Byte(p.A);
			}
		}
		using MemoryStream compressed = new MemoryStream();
		using (ZLibStream z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
		{
			z.Write(raw, 0, raw.Length);
		}
		using FileStream file = File.Create(path);
		file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
		byte[] header = new byte[13];
		BigEndian(header, 0, (uint)c.Width);
		BigEndian(header, 4, (uint)c.Height);
		header[8] = 8;
		header[9] = 6;
		Chunk(file, "IHDR", header);
		Chunk(file, "IDAT", compressed.ToArray());
		Chunk(file, "IEND", Array.Empty<byte>());
	}

	private static byte Byte(float v)
	{
		return (byte)Math.Clamp((int)Math.Round(v * 255f), 0, 255);
	}

	private static void Chunk(Stream s, string type, byte[] data)
	{
		byte[] len = new byte[4];
		BigEndian(len, 0, (uint)data.Length);
		s.Write(len);
		byte[] body = new byte[4 + data.Length];
		for (int i = 0; i < 4; i++)
		{
			body[i] = (byte)type[i];
		}
		Array.Copy(data, 0, body, 4, data.Length);
		s.Write(body);
		uint crc = 0xFFFFFFFF;
		foreach (byte b in body)
		{
			crc = Crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
		}
		byte[] tail = new byte[4];
		BigEndian(tail, 0, crc ^ 0xFFFFFFFF);
		s.Write(tail);
	}

	private static void BigEndian(byte[] buffer, int offset, uint value)
	{
		buffer[offset] = (byte)(value >> 24);
		buffer[offset + 1] = (byte)(value >> 16);
		buffer[offset + 2] = (byte)(value >> 8);
		buffer[offset + 3] = (byte)value;
	}

	private static uint[] BuildCrc()
	{
		uint[] table = new uint[256];
		for (uint n = 0; n < 256; n++)
		{
			uint c = n;
			for (int k = 0; k < 8; k++)
			{
				c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
			}
			table[n] = c;
		}
		return table;
	}
}
