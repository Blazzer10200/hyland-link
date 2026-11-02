namespace HylandHud;

internal readonly struct TimeWarning
{
	internal readonly string Text;

	/// <summary>Red instead of amber: curfew is on, or time has stopped.</summary>
	internal readonly bool Urgent;

	internal TimeWarning(string text, bool urgent)
	{
		Text = text;
		Urgent = urgent;
	}
}

/// <summary>
/// What the clock pill warns about, from the game's own times (hhmm, like <c>TimeManager.CurrentTime</c>): curfew runs
/// 21:00–05:00 (<c>CurfewManager.CURFEW_START_TIME</c> / <c>CURFEW_END_TIME</c>, its signs count down from 20:00), and
/// time stops at 04:00 until everyone sleeps (<c>TimeManager.Update</c> skips the clock at 400). Pure, no Unity types.
/// </summary>
internal static class TimeWarnings
{
	internal const int CurfewHeadsUp = 2000;

	internal const int CurfewStart = 2100;

	internal const int SleepHeadsUp = 300;

	internal const int TimeStops = 400;

	internal static int ToMinutes(int hhmm)
	{
		return hhmm / 100 * 60 + hhmm % 100;
	}

	internal static int MinutesUntil(int now, int target)
	{
		return (ToMinutes(target) - ToMinutes(now) + 1440) % 1440;
	}

	/// <summary>The most urgent warning, or null for none. Sleep beats curfew: at 3 AM curfew has been on for hours, but
	/// the clock is about to stop.</summary>
	internal static TimeWarning? For(int now, bool curfewEnabled, bool curfewActive)
	{
		if (now == TimeStops)
		{
			return new TimeWarning("SLEEP NOW", urgent: true);
		}
		if (now >= SleepHeadsUp && now < TimeStops)
		{
			return new TimeWarning("SLEEP " + MinutesUntil(now, TimeStops) + "M", curfewActive);
		}
		if (curfewActive)
		{
			return new TimeWarning("CURFEW", urgent: true);
		}
		if (curfewEnabled && now >= CurfewHeadsUp && now < CurfewStart)
		{
			return new TimeWarning("CURFEW " + MinutesUntil(now, CurfewStart) + "M", urgent: false);
		}
		return null;
	}
}
