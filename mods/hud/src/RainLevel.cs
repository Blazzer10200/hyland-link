using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Audio;
using Il2CppScheduleOne.Weather;
using UnityEngine;

namespace HylandHud;

/// <summary>
/// Our rain level, on the game's rain sounds (mods/hud/PHONE.md, "Rain"). The game sets each rain source's
/// <c>VolumeMultiplier</c> from its weather profile whenever you move in or out (<c>WeatherEffectController.UpdateAudio</c>),
/// so this keeps the last value the game wrote and plays it times ours: the Rain volume setting, and with "Much quieter
/// indoors" a further drop to 35% as the game's own enclosure blend goes to inside. The game's Weather slider still works
/// on top. <see cref="Dispose"/> hands every source back the game's value.
/// </summary>
internal sealed class RainLevel : IDisposable
{
	private const float Indoors = 0.35f;

	private sealed class Source
	{
		internal AudioSourceController Controller = null!;

		internal WeatherEffectController Owner = null!;

		/// <summary>The game's own multiplier: what it last wrote.</summary>
		internal float Game;

		/// <summary>What we last wrote, to tell the game's writes from ours.</summary>
		internal float Ours = float.NaN;
	}

	private readonly Settings settings;

	private readonly List<Source> sources = new List<Source>();

	private readonly HashSet<IntPtr> known = new HashSet<IntPtr>();

	private float nextScan;

	internal RainLevel(Settings settings)
	{
		this.settings = settings;
	}

	internal void Update()
	{
		// Rain controllers come and go with weather volumes; a look every 2 s is plenty.
		if (Time.unscaledTime >= nextScan)
		{
			nextScan = Time.unscaledTime + 2f;
			Scan();
		}
		float level = Mathf.Clamp(settings.RainVolume.Value, 0, 100) / 100f;
		for (int i = sources.Count - 1; i >= 0; i--)
		{
			Source source = sources[i];
			if (source.Controller == null || source.Owner == null)
			{
				known.Remove(source.Controller?.Pointer ?? IntPtr.Zero);
				sources.RemoveAt(i);
				continue;
			}
			float current = source.Controller.VolumeMultiplier;
			if (!Mathf.Approximately(current, source.Ours))
			{
				source.Game = current;
			}
			float factor = level;
			if (settings.QuieterIndoors.Value)
			{
				factor *= Mathf.Lerp(1f, Indoors, Mathf.Clamp01(source.Owner._enclosureBlend));
			}
			float target = source.Game * factor;
			if (!Mathf.Approximately(target, current))
			{
				source.Controller.VolumeMultiplier = target;
			}
			source.Ours = target;
		}
	}

	private void Scan()
	{
		foreach (RainController controller in UnityEngine.Object.FindObjectsOfType<RainController>())
		{
			Il2CppSystem.Collections.Generic.List<AudioSourceController> list = controller._audioSources;
			for (int i = 0; list != null && i < list.Count; i++)
			{
				AudioSourceController audio = list[i];
				if (audio != null && known.Add(audio.Pointer))
				{
					sources.Add(new Source { Controller = audio, Owner = controller });
				}
			}
		}
	}

	public void Dispose()
	{
		Exception? failure = null;
		foreach (Source source in sources)
		{
			if (source.Controller != null && !float.IsNaN(source.Ours))
			{
				Ui.Step(ref failure, () => source.Controller.VolumeMultiplier = source.Game);
			}
		}
		sources.Clear();
		known.Clear();
		Ui.Rethrow(failure);
	}
}
