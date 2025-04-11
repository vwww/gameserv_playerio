static class Util {
	public static void Swap<T>(ref T lhs, ref T rhs) {
		T temp = lhs;
		lhs = rhs;
		rhs = temp;
	}

	public static void Swap<T>(IList<T> list, int i, int j) {
		T temp = list[i];
		list[i] = list[j];
		list[j] = temp;
	}

	public static string FilterName(char[] rawName, int maxLen) {
		// remove disallowed characters
		// and
		// "  trim     and   collapse spaces "
		// -> "trim and collapse spaces"
		var buf = new List<char>(maxLen);
		var p = ' ';
		foreach (var c in rawName) {
			if ((c >= 'A' && c < 'Z')
			|| (c >= 'a' && c < 'z')
			|| (c >= '0' && c < '9')
			|| c == '_'
			|| c == ' ' && p != ' ') {
				buf.Add(c);
				p = c;

				if (buf.Count == maxLen) break;
			}
		}

		if (buf.Count != 0 && buf[buf.Count - 1] == ' ') {
			buf.RemoveAt(buf.Count - 1);
		}

		return buf.Count != 0 ? new string(buf.ToArray()) : "unnamed";
	}

	// trims leading and trailing whitespace, collapses multiples spaces to one
	// '\t', '\n', '\v', '\f', '\r' are converted to space ' '
	public static char[] FilterChat(char[] text) {
		var str = new List<char>();

		bool hasSpace = false;

		foreach (var c in text) {
			if (IsSpace(c)) {
				if (str.Count > 0) {
					hasSpace = true;
				}
			} else if (IsPrint(c)) {
				if (hasSpace) {
					str.Add(' ');
					hasSpace = false;
				}
				str.Add(c);
			}
		}

		return str.ToArray();
	}

	public static bool IsSpace(char c) => c == 32 || (c >= 9 && c <= 13);

	public static bool IsPrint(char c) => c >= 32 && c < 0x7F;
}
