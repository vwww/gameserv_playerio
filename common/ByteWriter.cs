class ByteWriter : List<byte> {
	public ByteWriter PutUShort(ushort val) {
		Add((byte)(val >> 8));
		Add((byte)val);
		return this;
	}

	// any int, more efficient for smaller int
	public ByteWriter PutInt(int n) {
		if (n > -127 && n < 128) {
			Add((byte)n);
		} else if (n >= -0x8000 && n < 0x8000) {
			Add(0x80); // -128
			Add((byte)n);
			Add((byte)(n >> 8));
		} else {
			Add(0x81); // -127
			Add((byte)n);
			Add((byte)(n >> 8));
			Add((byte)(n >> 16));
			Add((byte)(n >> 24));
		}
		return this;
	}

	public ByteWriter PutFloat64(double n) {
		byte[] b = BitConverter.GetBytes(n);
		if (BitConverter.IsLittleEndian) Array.Reverse(b);
		AddRange(b);
		return this;
	}

	public ByteWriter PutULong(ulong n) {
		// fast path for small values
		if (n < 0x80) {
			this.Add((byte)n);
			return this;
		}

		byte i;
		if (n < 0x810204080) {
			if (n < 0x204080) {
				if (n < 0x4080) {
					i = 6;
					n -= 0x80;
				} else {
					i = 5;
					n -= 0x4080;
				}
			} else {
				if (n < 0x10204080) {
					i = 4;
					n -= 0x204080;
				} else {
					i = 3;
					n -= 0x10204080;
				}
			}
		} else {
			if (n < 0x2040810204080) {
				if (n < 0x40810204080) {
					i = 2;
					n -= 0x810204080;
				} else {
					i = 1;
					n -= 0x40810204080;
				}
			} else {
				i = 0;
				if (n < 0x102040810204080) {
					n = (n - 0x2040810204080) | 0xfe00000000000000;
				} else {
					Add(0xff);
					// special case, 0 bias
				}
			}
		}

		byte[] b = BitConverter.GetBytes(n);
		if (BitConverter.IsLittleEndian) Array.Reverse(b);

		if (i != 0) {
			b[i] |= (byte)(0xfe << i);
		}

		AddRange(b.Skip(i));
		return this;
	}

	public ByteWriter PutLong(long n) {
		// zig-zag encode
		return PutULong((ulong)((n << 1) ^ (n >> 63)));
	}

	public ByteWriter PutString(char[] s) {
		return PutString(new string(s));
	}

	public ByteWriter PutString(string s) {
		foreach (var c in s) {
			var b = c > 0x7F ? (byte)'?' : (byte)c;
			if (b == 0) {
				break;
			}
			Add(b);
		}
		Add(0);
		return this;
	}

	public ByteWriter PutString(byte[] s) {
		foreach (var b in s) {
			if (b == 0) {
				break;
			}
			Add(b > 0x7F ? (byte)'?' : b);
		}
		Add(0);
		return this;
	}

	public ByteWriter PutType(MsgS2C type) {
		PutInt((int)type);
		return this;
	}

	public static implicit operator byte[](ByteWriter w) => w.ToArray();
}
