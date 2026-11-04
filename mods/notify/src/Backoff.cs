using System;
using MelonLoader;
using UnityEngine;

namespace HylandNotify;

/// <summary>
/// Retry timing for one surface. After a failure it waits 5, 10, 20, 40, then 60 s between tries, and only five clean
/// minutes reset that, so a fault that hits a few frames after every rebuild backs off instead of flickering.
/// </summary>
internal sealed class Backoff
{
	private readonly string part;

	private float retryAt;

	private float lastFailure = float.NegativeInfinity;

	private int failures;

	internal Backoff(string part)
	{
		this.part = part;
	}

	internal bool Due => Time.unscaledTime >= retryAt;

	internal void Succeeded()
	{
		if (failures > 0 && Time.unscaledTime - lastFailure > 300f)
		{
			failures = 0;
		}
	}

	internal void Failed(Exception ex, MelonLogger.Instance log)
	{
		float delay = Math.Min(60f, 5f * (1 << Math.Min(failures, 4)));
		retryAt = Time.unscaledTime + delay;
		lastFailure = Time.unscaledTime;
		failures++;
		string error = ex.GetType().Name + ": " + ex.Message;
		if (failures <= 3)
		{
			log.Warning("Hyland Notifications " + part + " unavailable, the game's own is back; retrying in " + delay + "s: " + error);
		}
		else if (failures == 4)
		{
			log.Warning("Hyland Notifications " + part + " still unavailable; retrying every minute without logging again: " + error);
		}
	}

	internal void Reset()
	{
		retryAt = 0f;
		lastFailure = float.NegativeInfinity;
		failures = 0;
	}
}
