using System;
using System.Collections.Generic;
using UnityEngine;

namespace HylandHud;

/// <summary>Turns <see cref="Bitmap"/>s into sprites, caches them by key and destroys them all on <see cref="Dispose"/>.</summary>
internal sealed class Sprites : IDisposable
{
	private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

	private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

	internal Sprite Get(string key, Func<Bitmap> make, bool mipmaps = false)
	{
		if (cache.TryGetValue(key, out Sprite sprite) && sprite != null)
		{
			return sprite;
		}
		sprite = Create(make(), mipmaps);
		cache[key] = sprite;
		return sprite;
	}

	internal Sprite Glass(double radius)
	{
		return Get("glass" + radius, () => Art.Glass(radius, Theme.GlassFill, Theme.GlassStroke));
	}

	internal Sprite Shadow(double radius, double blur)
	{
		return Get("shadow" + radius + "/" + blur, () => Art.Shadow(radius, blur, 0.3f));
	}

	/// <summary>A white panel (fill a third of the stroke's strength) that the game's slot colors tint.</summary>
	internal Sprite Slot(double radius)
	{
		return Get("slot" + radius, () => Art.Glass(radius, Theme.White.WithAlpha(0.33f), Theme.White));
	}

	internal Sprite Icon(Art.Icon icon)
	{
		return Get("icon" + icon, () => Art.Render(icon, 64), mipmaps: true);
	}

	private Sprite Create(Bitmap bitmap, bool mipmaps)
	{
		Texture2D texture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.RGBA32, mipmaps);
		owned.Add(texture);
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
		Color[] pixels = new Color[bitmap.Pixels.Length];
		for (int i = 0; i < pixels.Length; i++)
		{
			Rgba p = bitmap.Pixels[i];
			pixels[i] = new Color(p.R, p.G, p.B, p.A);
		}
		texture.SetPixels(pixels);
		texture.Apply(updateMipmaps: mipmaps, makeNoLongerReadable: true);
		// 100 is the canvas's reference pixels per unit, so a sliced border of N pixels draws N / Scale HUD units wide.
		float ppu = 100f * bitmap.Scale;
		Vector4 border = new Vector4(bitmap.Border, bitmap.Border, bitmap.Border, bitmap.Border);
		Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, bitmap.Width, bitmap.Height), new Vector2(0.5f, 0.5f), ppu, 0u, SpriteMeshType.FullRect, border);
		owned.Add(sprite);
		return sprite;
	}

	public void Dispose()
	{
		foreach (UnityEngine.Object item in owned)
		{
			if (item != null)
			{
				UnityEngine.Object.Destroy(item);
			}
		}
		owned.Clear();
		cache.Clear();
	}
}
