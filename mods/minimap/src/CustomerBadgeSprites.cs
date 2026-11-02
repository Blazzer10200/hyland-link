using System;
using UnityEngine;

namespace HylandMinimap;

internal sealed class CustomerBadgeSprites : IDisposable
{
	private Texture2D? checkTexture;

	private Texture2D? clockTexture;

	private Texture2D? lockTexture;

	private Sprite? checkSprite;

	private Sprite? clockSprite;

	private Sprite? lockSprite;

	private bool disposed;

	internal Sprite Check => checkSprite;

	internal Sprite Clock => clockSprite;

	internal Sprite Lock => lockSprite;

	internal CustomerBadgeSprites()
	{
		try
		{
			(checkTexture, checkSprite) = CreateGlyph((double x, double y) => Math.Min(SegmentDistance(x, y, -0.68, -0.02, -0.2, -0.45), SegmentDistance(x, y, -0.2, -0.45, 0.68, 0.5)) - 0.115);
			(clockTexture, clockSprite) = CreateGlyph((double x, double y) =>
			{
				double val = Math.Abs(Math.Sqrt(x * x + y * y) - 0.7) - 0.075;
				double val2 = SegmentDistance(x, y, 0.0, 0.0, 0.0, 0.42) - 0.07;
				double val3 = SegmentDistance(x, y, 0.0, 0.0, 0.32, -0.15) - 0.07;
				return Math.Min(val, Math.Min(val2, val3));
			});
			(lockTexture, lockSprite) = CreateGlyph((double x, double y) =>
			{
				double val = Math.Max(Math.Abs(x) - 0.5, Math.Max(-0.65 - y, y - 0.1));
				double val2 = Math.Abs(Math.Sqrt(x * x + (y - 0.18) * (y - 0.18)) - 0.33) - 0.09;
				double val3 = Math.Sqrt(x * x + (y + 0.22) * (y + 0.22)) - 0.085;
				return Math.Max(val2: 0.0 - Math.Min(val3, Math.Max(Math.Abs(x) - 0.05, Math.Max(-0.48 - y, y + 0.24))), val1: Math.Min(val, val2));
			});
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	private static double SegmentDistance(double x, double y, double ax, double ay, double bx, double by)
	{
		double num = bx - ax;
		double num2 = by - ay;
		double num3 = Math.Clamp(((x - ax) * num + (y - ay) * num2) / (num * num + num2 * num2), 0.0, 1.0);
		double num4 = x - (ax + num3 * num);
		double num5 = y - (ay + num3 * num2);
		return Math.Sqrt(num4 * num4 + num5 * num5);
	}

	private static (Texture2D Texture, Sprite Sprite) CreateGlyph(Func<double, double, double> distance)
	{
		Texture2D texture2D = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
		try
		{
			texture2D.name = "Hyland Minimap status glyph";
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.filterMode = FilterMode.Bilinear;
			Color[] array = new Color[4096];
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					double num = distance(((double)j + 0.5) / 64.0 * 2.0 - 1.0, ((double)i + 0.5) / 64.0 * 2.0 - 1.0);
					float a = (float)Math.Clamp(0.5 - num * 64.0 / 2.0, 0.0, 1.0);
					array[i * 64 + j] = new Color(1f, 1f, 1f, a);
				}
			}
			texture2D.SetPixels(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f);
			if (sprite == null)
			{
				throw new InvalidOperationException("Unable to create a customer status sprite.");
			}
			return (Texture: texture2D, Sprite: sprite);
		}
		catch
		{
			UnityEngine.Object.Destroy(texture2D);
			throw;
		}
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			if (checkSprite != null)
			{
				UnityEngine.Object.Destroy(checkSprite);
			}
			if (clockSprite != null)
			{
				UnityEngine.Object.Destroy(clockSprite);
			}
			if (lockSprite != null)
			{
				UnityEngine.Object.Destroy(lockSprite);
			}
			if (checkTexture != null)
			{
				UnityEngine.Object.Destroy(checkTexture);
			}
			if (clockTexture != null)
			{
				UnityEngine.Object.Destroy(clockTexture);
			}
			if (lockTexture != null)
			{
				UnityEngine.Object.Destroy(lockTexture);
			}
			checkSprite = (clockSprite = (lockSprite = null));
			checkTexture = (clockTexture = (lockTexture = null));
		}
	}
}
