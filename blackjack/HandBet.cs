class Hand {
	public readonly List<CardValue> cards = new();
	public byte value = 0;
	public byte valueHard = 0;
	private bool hasAce = false;
	public bool isSoft = false;

	public void Add(CardValue card, bool front = false) {
		if (front) {
			cards.Insert(0, card);
		} else {
			cards.Add(card);
		}

		if (card == CardValue.Ace) {
			hasAce = true;
		}

		valueHard += (byte)(card + 1);
		isSoft = hasAce && valueHard <= 11;
		value = (byte)(valueHard + (isSoft ? 10 : 0));
	}

	public Hand Split(CardValue card0, CardValue card1) {
		var newHand = new Hand();
		newHand.Add(cards[1]);
		newHand.Add(card1);

		valueHard = (byte)((byte)cards[0] + (byte)(cards[1] = card0) + 2);
		hasAce = cards[0] == CardValue.Ace || cards[1] == CardValue.Ace;
		isSoft = hasAce && valueHard <= 11;
		value = (byte)(valueHard + (isSoft ? 10 : 0));

		return newHand;
	}

	public bool IsNaturalBlackjack(bool isSplit) {
		return cards.Count == 2 && value == 21 && !(isSplit && cards[0] == CardValue.Ace);
	}
}

class HandBet {
	public Hand hand = new();
	public long bet = new();

	public HandBet Split(CardValue card0, CardValue card1) {
		return new HandBet() {
			hand = hand.Split(card0, card1),
			bet = bet,
		};
	}
}

static class HandBetUtil {
	public static ByteWriter PutHandBet(this ByteWriter msg, HandBet hb) {
		msg.Add((byte)hb.hand.cards.Count);
		foreach (var c in hb.hand.cards) {
			msg.Add((byte)c);
		}
		msg.PutLong(hb.bet);
		return msg;
	}

	public static ByteWriter PutHandBets(this ByteWriter msg, ICollection<HandBet> hbs) {
		msg.Add((byte)hbs.Count);
		foreach (var hb in hbs) {
			msg.PutHandBet(hb);
		}
		return msg;
	}
}
