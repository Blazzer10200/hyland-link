using System;
using UnityEngine;

namespace HylandMinimap;

internal sealed class WorldMarkerSprites : IDisposable
{
	private Texture2D? dealerTexture;

	private Texture2D? vehicleTexture;

	private Texture2D? bikeTexture;

	private Sprite? dealerSprite;

	private Sprite? vehicleSprite;

	private Sprite? bikeSprite;

	private bool disposed;

	private static readonly (double X, double Y)[] Cabin = new (double, double)[4]
	{
		(-0.5, 0.22),
		(-0.35, 0.66),
		(0.35, 0.66),
		(0.5, 0.22)
	};

	private static readonly (double X, double Y)[] Windshield = new (double, double)[4]
	{
		(-0.35, 0.29),
		(-0.26, 0.53),
		(0.26, 0.53),
		(0.35, 0.29)
	};

	internal Sprite Dealer => dealerSprite;

	internal Sprite Vehicle => vehicleSprite;

	internal Sprite Bike => bikeSprite;

	internal WorldMarkerSprites()
	{
		try
		{
			(dealerTexture, dealerSprite) = CreateGlyph("Dealer", DealerDistance);
			(vehicleTexture, vehicleSprite) = CreateGlyph("Vehicle", VehicleDistance);
			(bikeTexture, bikeSprite) = CreateGlyph("Bike", BikeDistance);
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	private static double DealerDistance(double x, double y)
	{
		double val = RoundedBox(x, y, 0.0, -0.16, 0.7, 0.43, 0.07);
		double val2 = RoundedBox(x, y, 0.0, 0.4, 0.32, 0.24, 0.05);
		double num = RoundedBox(x, y, 0.0, 0.43, 0.17, 0.11, 0.025);
		double val3 = Math.Min(val, Math.Max(val2, 0.0 - num));
		double num2 = RoundedBox(x, y, 0.0, -0.07, 0.65, 0.035, 0.02);
		return Math.Min(val2: RoundedBox(x, y, 0.0, -0.07, 0.1, 0.105, 0.025), val1: Math.Max(val3, 0.0 - num2));
	}

	private static double VehicleDistance(double x, double y)
	{
		double val = RoundedBox(x, y, 0.0, -0.1, 0.68, 0.38, 0.07);
		double val2 = PolygonDistance(x, y, Cabin);
		double val3 = RoundedBox(x, y, -0.48, -0.53, 0.12, 0.15, 0.025);
		double val4 = Math.Min(val2: Math.Min(val3, RoundedBox(x, y, 0.48, -0.53, 0.12, 0.15, 0.025)), val1: Math.Min(val, val2));
		double val5 = PolygonDistance(x, y, Windshield);
		double val6 = Math.Min(RoundedBox(x, y, -0.4, 0.02, 0.13, 0.06, 0.02), RoundedBox(x, y, 0.4, 0.02, 0.13, 0.06, 0.02));
		double val7 = RoundedBox(x, y, 0.0, -0.22, 0.22, 0.04, 0.02);
		return Math.Max(val4, 0.0 - Math.Min(val5, Math.Min(val6, val7)));
	}

	private static double BikeDistance(double x, double y)
	{
		double val = Ring(x, y, -0.48, -0.32, 0.24, 0.075);
		double val2 = Ring(x, y, 0.48, -0.32, 0.24, 0.075);
		double val3 = Math.Min(SegmentDistance(x, y, -0.48, -0.32, -0.1, 0.12), SegmentDistance(x, y, -0.1, 0.12, 0.21, -0.32)) - 0.07;
		val3 = Math.Min(val3, SegmentDistance(x, y, -0.48, -0.32, 0.21, -0.32) - 0.07);
		double val4 = RoundedBox(x, y, -0.02, 0.08, 0.23, 0.14, 0.07);
		double val5 = SegmentDistance(x, y, -0.31, 0.23, -0.07, 0.23) - 0.08;
		double val6 = SegmentDistance(x, y, 0.48, -0.32, 0.3, 0.3) - 0.07;
		double val7 = SegmentDistance(x, y, 0.3, 0.3, 0.16, 0.38) - 0.07;
		return Math.Min(Math.Min(val, val2), Math.Min(Math.Min(val3, val4), Math.Min(val5, Math.Min(val6, val7))));
	}

	private static double RoundedBox(double x, double y, double centerX, double centerY, double halfWidth, double halfHeight, double radius)
	{
		double val = Math.Abs(x - centerX) - halfWidth + radius;
		double num = Math.Abs(y - centerY) - halfHeight + radius;
		return Math.Sqrt(Math.Max(val, 0.0) * Math.Max(val, 0.0) + Math.Max(num, 0.0) * Math.Max(num, 0.0)) + Math.Min(Math.Max(val, num), 0.0) - radius;
	}

	private static double Ring(double x, double y, double centerX, double centerY, double radius, double halfStroke)
	{
		double num = x - centerX;
		double num2 = y - centerY;
		return Math.Abs(Math.Sqrt(num * num + num2 * num2) - radius) - halfStroke;
	}

	private static double PolygonDistance(double x, double y, ReadOnlySpan<(double X, double Y)> vertices)
	{
		double num = double.PositiveInfinity;
		bool flag = false;
		for (int i = 0; i < vertices.Length; i++)
		{
			(double, double) tuple = vertices[i];
			(double, double) tuple2 = vertices[(i + 1) % vertices.Length];
			num = Math.Min(num, SegmentDistance(x, y, tuple.Item1, tuple.Item2, tuple2.Item1, tuple2.Item2));
			if (tuple.Item2 > y != tuple2.Item2 > y && x < (tuple2.Item1 - tuple.Item1) * (y - tuple.Item2) / (tuple2.Item2 - tuple.Item2) + tuple.Item1)
			{
				flag = !flag;
			}
		}
		if (!flag)
		{
			return num;
		}
		return 0.0 - num;
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

	private static (Texture2D Texture, Sprite Sprite) CreateGlyph(string name, Func<double, double, double> distance)
	{
		Texture2D texture2D = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
		Sprite sprite = null;
		try
		{
			texture2D.name = "Hyland Minimap " + name + " glyph";
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
			sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f);
			if (sprite == null)
			{
				throw new InvalidOperationException("Unable to create a world marker sprite.");
			}
			sprite.name = texture2D.name;
			return (Texture: texture2D, Sprite: sprite);
		}
		catch
		{
			if (sprite != null)
			{
				UnityEngine.Object.Destroy(sprite);
			}
			UnityEngine.Object.Destroy(texture2D);
			throw;
		}
	}

	public void Dispose()
	{
		if (!disposed)
		{
			disposed = true;
			if (dealerSprite != null)
			{
				UnityEngine.Object.Destroy(dealerSprite);
			}
			if (vehicleSprite != null)
			{
				UnityEngine.Object.Destroy(vehicleSprite);
			}
			if (bikeSprite != null)
			{
				UnityEngine.Object.Destroy(bikeSprite);
			}
			if (dealerTexture != null)
			{
				UnityEngine.Object.Destroy(dealerTexture);
			}
			if (vehicleTexture != null)
			{
				UnityEngine.Object.Destroy(vehicleTexture);
			}
			if (bikeTexture != null)
			{
				UnityEngine.Object.Destroy(bikeTexture);
			}
			dealerSprite = (vehicleSprite = (bikeSprite = null));
			dealerTexture = (vehicleTexture = (bikeTexture = null));
		}
	}
}
