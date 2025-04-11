public class CardCount {
	public ulong[] count;
	public ulong total = 0;

	public CardCount(int size) {
		count = new ulong[size];
	}

	public void Reset() {
		Array.Clear(count, 0, count.Length);
		total = 0;
	}

	public void Add(CardCount other) {
		for (var i = 0; i < count.Length; i++) {
			count[i] += other.count[i];
		}
		total += other.total;
	}

	public void Sub(CardCount other) {
		for (var i = 0; i < count.Length; i++) {
			count[i] -= other.count[i];
		}
		total -= other.total;
	}

	public void Copy(CardCount other) {
		Array.Copy(other.count, count, count.Length);
		total = other.total;
	}

	public void AddCards(int type, ulong num) {
		count[type] += num;
		total += num;
	}

	public void SubCards(int type, ulong num) {
		count[type] -= num;
		total -= num;
	}
}

static class CardCountUtil {
	public static CardCount GetCardCount(this ByteReader msg, int size) {
		var cardCount = new CardCount(size);
		for (var i = 0; i < size; i++) {
			var c = msg.GetULong();
			cardCount.total += (cardCount.count[i] = c);
		}
		return cardCount;
	}

	public static ByteWriter PutCardCount(this ByteWriter msg, CardCount c) {
		foreach (var v in c.count) {
			msg.PutULong(v);
		}
		return msg;
	}
}
