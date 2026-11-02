using System;

namespace HylandMinimap;

internal static class MapMath
{
	internal const double MinimumZoom = 0.1;

	internal const double MaximumZoom = 12.0;

	internal const double DefaultZoom = 3.0;

	internal static double ClampZoom(double value)
	{
		return ClampFinite(value, 0.1, 12.0, 3.0);
	}

	internal static float NextZoom(double current, double step, bool increase)
	{
		double num = ClampFinite(step, 0.1, 3.0, 0.5);
		return (float)Math.Clamp(ClampZoom(current) + (increase ? num : (0.0 - num)), 0.1, 12.0);
	}

	internal static double ClampFinite(double value, double min, double max, double fallback)
	{
		if (!double.IsFinite(value))
		{
			return fallback;
		}
		return Math.Clamp(value, min, max);
	}

	internal static (double X, double Y) Rotate(double x, double y, double degrees)
	{
		double num = degrees * Math.PI / 180.0;
		double num2 = Math.Sin(num);
		double num3 = Math.Cos(num);
		return (X: x * num3 - y * num2, Y: x * num2 + y * num3);
	}

	internal static double Heading(double dx, double dy)
	{
		return Math.Atan2(dx, dy) * 180.0 / Math.PI;
	}

	internal static (double X, double Y) RelativePoint(double x, double y, double centerX, double centerY, double zoom, double rotation)
	{
		return Rotate((x - centerX) * zoom, (y - centerY) * zoom, rotation);
	}

	internal static bool IsInside(double x, double y, double halfSize, bool circle)
	{
		if (!circle)
		{
			if (Math.Abs(x) <= halfSize)
			{
				return Math.Abs(y) <= halfSize;
			}
			return false;
		}
		return x * x + y * y <= halfSize * halfSize;
	}

	internal static (double X, double Y, double Width, double Height) FitAspect(double x, double y, double width, double height, double aspect, double pivotX, double pivotY)
	{
		if (width <= 0.0 || height <= 0.0 || aspect <= 0.0 || !double.IsFinite(aspect))
		{
			return (X: x, Y: y, Width: width, Height: height);
		}
		if (width / height > aspect)
		{
			double num = height * aspect;
			return (X: x + (width - num) * pivotX, Y: y, Width: num, Height: height);
		}
		double num2 = width / aspect;
		return (X: x, Y: y + (height - num2) * pivotY, Width: width, Height: num2);
	}
}
