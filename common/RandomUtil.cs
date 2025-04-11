static class RandomUtil {
	public static void Shuffle<T>(this RNG r, IList<T> list) {
		for (int i = 0; i < list.Count - 1; i++) {
			int j = r.Next(i, list.Count);

			if (i != j) {
				Util.Swap(list, i, j);
			}
		}
	}

	public static T Choice<T>(this RNG r, IList<T> list) {
		return list.Count == 0 ? default : list[r.Next(list.Count)];
	}

#if false
	public static string RandomString(this RNG r, int length) {
		const string allowedChars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNOPQRSTUVWXYZ0123456789";
		char[] chars = new char[length];

		for (int i = 0; i < length; i++) {
			chars[i] = allowedChars[r.Next(allowedChars.Length)];
		}

		return new string(chars);
	}
#endif
}

public class RNG {
	readonly System.Security.Cryptography.RNGCryptoServiceProvider rnd = new();

	public byte NextByte() {
		var buf = new byte[1];
		rnd.GetBytes(buf);
		return buf[0];
	}

	public byte NextBit() {
		return (byte)(NextByte() & 1);
	}

	public bool NextBool() => NextBit() != 0;

	public int Next(int maxValue) {
		if (maxValue <= 0) {
			throw new ArgumentException();
		}

		return (int)UInt64Unchecked((ulong)maxValue);
	}

	public int Next(int minValue, int maxValue) => minValue + Next(maxValue - minValue);

	public ulong NextUInt64(ulong maxValue) {
		if (maxValue == 0) {
			throw new ArgumentException();
		}

		return UInt64Unchecked(maxValue);
	}

	public ulong NextUInt64() {
		var buf = new byte[8];
		rnd.GetBytes(buf);
		return BitConverter.ToUInt64(buf, 0);
	}

	protected ulong UInt64Unchecked(ulong n) {
		if ((n & (n - 1)) == 0) {
			// mask for power of two
			return NextUInt64() & (n - 1);
		}

		// https://cs.opensource.google/go/go/+/refs/tags/go1.24.4:src/math/rand/v2/rand.go;l=119
		// https://lemire.me/blog/2016/06/27/a-fast-alternative-to-the-modulo-reduction/
		// https://lemire.me/blog/2016/06/30/fast-random-shuffling/
		var hi = BigMul64(NextUInt64(), n, out var lo);
		if (lo < n) {
			var thresh = (ulong)-(long)n % n;
			while (lo < thresh) {
				hi = BigMul64(NextUInt64(), n, out lo);
			}
		}
		return hi;
	}

	public double NextDouble() {
		return (double)(NextUInt64() & ((1uL << 53) - 1)) / (1uL << 53);
	}

	private ulong BigMul64(ulong x, ulong y, out ulong lo) {
		lo = x * y; // x0y0 + ((x1y0 + x0y1) << 16)

		ulong x0 = (uint)x;
		ulong x1 = x >> 32;
		ulong y0 = (uint)y;
		ulong y1 = y >> 32;

		ulong x0y0 = x0 * y0;
		ulong x0y1 = x0 * y1;
		ulong x1y0 = x1 * y0;

		return (x1 * y1) + (x1y0 >> 32) + (x0y1 >> 32)
			+ (((ulong)(uint)x1y0 + (uint)x0y1 + (x0y0 >> 32)) >> 32); // carry could be 0, 1, or 2
	}
}
