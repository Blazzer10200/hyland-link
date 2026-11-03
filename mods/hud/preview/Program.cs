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
		Console.WriteLine(layout);
		Console.WriteLine(sheet);
		return 0;
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
			Art.Render(Art.Icon.Bank, 64)
		};
		Canvas c = new Canvas(13 * 140 + 20, 180);
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
