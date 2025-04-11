class ByteReader {
	private readonly byte[] buf;
	private int pos;
	private readonly int len;

	public ByteReader(byte[] buf) : this(buf, buf.Length) { }

	public ByteReader(byte[] buf, int len) {
		this.buf = buf;
		this.pos = 0;
		this.len = len;
	}

	public bool Overread { get; private set; } = false;
	public int Remaining => len - pos;

	public byte Get() {
		if (pos == len) {
			Overread = true;
			return 0;
		}
		return buf[pos++];
	}

	public ushort GetUShort() {
		return (ushort)((Get() << 8) | Get());
	}

	// any int, more efficient for smaller int
	public int GetInt() {
		int n = (sbyte)Get(); // sign extend
		if (n == -128) {
			n = Get();
			n |= Get() << 8;
		} else if (n == -127) {
			n = Get();
			n |= Get() << 8;
			n |= Get() << 16;
			n |= Get() << 24;
		}
		return n;
	}

	public double GetFloat64() {
		var b = new[] { Get(), Get(), Get(), Get(), Get(), Get(), Get(), Get() };
		if (BitConverter.IsLittleEndian) Array.Reverse(b);
		return BitConverter.ToDouble(b, 0);
	}

	static uint GetULongIndex(uint x) {
		x ^= 0xff;
		// smear bits to right
		x |= x >> 1;
		x |= x >> 2;
		x |= x >> 4;
		// 8-bit pop count
		x = ((((x * 0x08040201u) >> 3) & 0x11111111U) * 0x11111111U) >> 28;
		// subtract one before returning
		return x - 1;
	}

	public ulong GetULong() {
		byte c = Get();

		// special cases
		if (c < 0x80) return c;

		var b = new byte[8];
		if (c == 0xff) {
			Array.Copy(buf, pos, b, 0, 8);
			pos += 8;
			if (BitConverter.IsLittleEndian) Array.Reverse(b);
			return BitConverter.ToUInt64(b, 0);
		}

		var i = (int)GetULongIndex(c);
		var len = 7 - i;

		b[i] = (byte)(c & (0xff >> len));
		Array.Copy(buf, pos, b, i + 1, len);
		pos += len;
		if (BitConverter.IsLittleEndian) Array.Reverse(b);
		return BitConverter.ToUInt64(b, 0) + i switch {
			0 => 0x2040810204080,
			1 => 0x40810204080,
			2 => 0x810204080,
			3 => 0x10204080,
			4 => 0x204080,
			5 => 0x4080,
			6 => 0x80,
			_ => 0,
		};
	}

	public long GetLong() {
		// zig-zag decode
		long n = (long)GetULong();
		return (n >> 1) ^ -(n & 1);
	}

	public char[] GetString(int maxLen) {
		var str = new List<char>();
		while (str.Count <= maxLen) {
			var c = Get();
			if (c == 0 || str.Count == maxLen) break;
			str.Add(c > 0x7F ? '?' : (char)c);
		}
		return str.ToArray();
	}
}
