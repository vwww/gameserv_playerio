static class MathUtil {
	public static int Clamp(int val, int min, int max) {
		return Math.Min(Math.Max(val, min), max);
	}

	public static long Clamp(long val, long min, long max) {
		return Math.Min(Math.Max(val, min), max);
	}

	public static ulong Clamp(ulong val, ulong min, ulong max) {
		return Math.Min(Math.Max(val, min), max);
	}

	public static bool Clamp(ref double f, double min, double max) {
		if (f < min) {
			f = min;
			return true;
		} else if (f > max) {
			f = max;
			return true;
		}
		return false;
	}

	public static bool ClampAbs(ref double f, double magnitude) {
		return Clamp(ref f, -magnitude, +magnitude);
	}
}
