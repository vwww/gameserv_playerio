partial class RoundPlayerInfo {
	public readonly List<byte> cards = new(6);
	public int played;
	public uint score;
	public bool passed;
}

partial class RoundDiscInfo {
	public uint score;
}

partial class Room {
	int ROUND_TIME => optTurnTime;

	DateTime startTime;

	const int DECK_SIZE = 52;
	readonly byte[] deck = Enumerable.Range(0, DECK_SIZE).Select(x => (byte)x).ToArray();
	readonly byte[] crib = new byte[4];
	readonly List<byte> play = new(13); // max 13 cards: 1+1+1+1+2+2+2+2+3+3+3+3+4 = 28, next 4 would exceed 31
	int deckIndex;
	int cribIndex;
	byte starter;
	byte trickCount;

	GamePhase gamePhase;
	int handNum;
	int trickNum;
	int trickTurn;

	int dealer;

	byte pendingMove;

	void ProcessMsgMoveKeep(Player player, ByteReader msg) {
		var discard = msg.Get();

		if (state == GameState.ACTIVE && gamePhase == GamePhase.DEAL && player.active) {
			var pn = playerInfo.FindIndex(p => p.owner == player.cn);
			if (pn == -1) {
				return;
			}

			var p = playerInfo[pn];
			if (p.passed) {
				return;
			}

			HandleMoveKeep(player, p, pn, discard & 7, discard >> 3);

			p.passed = true;
			if (playerInfo.All(p => p.passed)) {
				TurnEnd();
			} else {
				Broadcast(new ByteWriter()
					.PutType(MsgS2C.MOVE_READY)
					.PutInt(pn));
			}
		}
	}

	void HandleMoveKeep(Player player, RoundPlayerInfo p, int pn, int discard0, int discard1) {
		if (discard0 >= p.cards.Count) {
			discard0 = 0;
		}
		crib[cribIndex++] = p.cards[discard0];

		if (playerInfo.Count == 2) {
			if (discard1 >= p.cards.Count || discard0 == discard1) {
				discard1 = (byte)(discard0 == 0 ? 1 : 0);
			}
			crib[cribIndex++] = p.cards[discard1];

			for (int i = 0, j = 0; i < p.cards.Count; i++) {
				if (i != discard0 && i != discard1) {
					p.cards[j++] = p.cards[i];
				}
			}
			p.cards.RemoveRange(4, 2);
		} else {
			p.cards.RemoveAt(discard0);
		}

		var msg = new ByteWriter()
			.PutType(MsgS2C.MOVE_CONFIRM);
		msg.Add((byte)(discard0 | (discard1 << 3)));
		player.Send(msg);
	}

	void ProcessMsgMovePlay(Player player, ByteReader msg) {
		var moveNew = msg.Get();

		if (state == GameState.ACTIVE && CanMakeMove(player)) {
			pendingMove = moveNew;

			TurnEnd();
		}
	}

	void ProcessMsgMoveReady(Player player, bool post) {
		if (state == GameState.ACTIVE && gamePhase == (post ? GamePhase.POST : GamePhase.PRE) && player.active) {
			var pn = playerInfo.FindIndex(p => p.owner == player.cn);
			if (pn == -1) {
				return;
			}

			var p = playerInfo[pn];
			if (p.passed) {
				return;
			}

			p.passed = true;
			if (playerInfo.All(p => p.passed)) {
				TurnEnd();
			} else {
				Broadcast(new ByteWriter()
					.PutType(MsgS2C.MOVE_READY)
					.PutInt(pn));
			}
		}
	}

	bool CanMakeMove(Player player) {
		return gamePhase == GamePhase.PLAY && playerInfo[turnIndex].owner == player.cn;
	}

	partial void WriteWelcome3(ByteWriter b) {
		if (state != GameState.ACTIVE) return;

		b.PutULong(((ulong)handNum << 2) | (uint)gamePhase);
		b.PutInt(dealer);
		if (gamePhase != GamePhase.DEAL) {
			b.PutInt(trickNum);
			b.PutInt(trickTurn);
			b.Add(trickCount);
			b.Add(starter);
			b.Add((byte)play.Count);
			play.ForEach(b.Add);

			if (gamePhase == GamePhase.POST) {
				foreach (var c in crib) {
					b.Add(c);
				}
			}
		}
	}

	void WritePlayerInfo(ByteWriter b, RoundPlayerInfo p) {
		b.PutULong((p.score << 1) | (p.passed ? 1 : 0u));
		b.PutInt(p.played);
		for (var i = 0; i < p.played; i++) {
			b.Add(p.cards[i]);
		}
	}

	void WriteDiscInfo(ByteWriter b, RoundDiscInfo d) {
		b.PutULong(d.score);
	}

	bool WriteEliminateInfo(ByteWriter b, RoundDiscInfo d, int pn, RoundPlayerInfo p, Player c, bool early, out bool newMove, out bool moveStarted) {
		d.score = p.score;

		newMove = moveStarted = true;
		EndHand();

		c.score.AddRank(playerInfo.Count, playerInfo.Count + discInfo.Count);

		var lastIndex = playerInfo.Count - 1;
		// fix turnIndex
		if (turnIndex > pn) {
			turnIndex--;
		} else if (turnIndex == lastIndex) {
			turnIndex = 0;
		}

		return false;
	}

	void SetTurnOrder() {
		rng.Shuffle(playerInfo);
		// turnIndex = 0;
	}

	bool IsGameOver() => playerInfo.Count <= 1 || gamePhase == GamePhase.END;

	void GameOver() {
		var duration = (ulong)(DateTime.UtcNow - startTime).TotalMilliseconds;

		var winMsg = new ByteWriter().PutType(MsgS2C.END_ROUND);
		winMsg.PutULong(duration);
		Broadcast(winMsg.ToArray());

		var sortedPlayers = playerInfo.OrderByDescending(p => p.score);

		RoundPlayerInfo prev = null;
		int rank = 1, ordinalRank = 1;
		foreach (var p in sortedPlayers) {
			if (prev != null && prev.score != p.score) {
				rank = ordinalRank;
			}

			players[p.owner].score.AddRank(rank, playerInfo.Count + discInfo.Count);

			prev = p;
			ordinalRank++;
		}
	}

	partial void WriteRoundStartInfo3(ByteWriter b) {
		startTime = DateTime.UtcNow;
		handNum = 0;

		gamePhase = GamePhase.DEAL;
	}

	void StartHand() {
		trickNum = trickTurn = 0;
		trickCount = 0;
		play.Clear();

		deckIndex = cribIndex = 0;

		// pick dealer
		dealer = rng.Next(playerInfo.Count);
		if ((turnIndex = dealer + 1) == playerInfo.Count) {
			turnIndex = 0;
		}

		Broadcast(new ByteWriter()
			.PutType(MsgS2C.END_TURN)
			.PutInt(dealer));

		// deal cards
		var cardsPerPlayer = playerInfo.Count > 2 ? 5 : 6;

		foreach (var p in playerInfo) {
			var msg = new ByteWriter()
				.PutType(MsgS2C.PLAYER_PRIVATE_INFO_HAND);

			p.cards.Clear();
			p.played = 0;
			for (var i = 0; i < cardsPerPlayer; i++) {
				var c = DrawDeck();
				p.cards.Add(c);
				msg.Add(c);
			}
			// p.passed = false;

			players[p.owner].Send(msg);
		}

		if (playerInfo.Count == 3) {
			crib[cribIndex++] = DrawDeck();
		}
	}

	byte DrawDeck() {
		int i = rng.Next(deckIndex, DECK_SIZE);

		if (deckIndex != i) {
			Util.Swap(deck, deckIndex, i);
		}

		return deck[deckIndex++];
	}

	void MoveStarted() {
		if (gamePhase == GamePhase.DEAL) {
			StartHand();
		}

		pendingMove = 4;
	}

	bool SkipMove() {
		var p = playerInfo[turnIndex];

		if (p.played == p.cards.Count) {
			return optSkipEmpty;
		}

		var validMoveCount = GetValidMoves(p, GetRankLimit()).Count();
		return validMoveCount == 0 && optSkipPass || validMoveCount == 1 && optSkipOnlyMove;
	}

	void MoveEnded() {
		do EndPhase();
		while (gamePhase == GamePhase.PLAY && SkipMove());
	}

	void EndPhase() {
		var msg = new ByteWriter()
			.PutType(MsgS2C.END_TURN);

		switch (gamePhase) {
			case GamePhase.DEAL: {
				// auto-discard and set to unpassed
				for (var i = 0; i < playerInfo.Count; i++) {
					var p = playerInfo[i];
					if (p.passed) {
						p.passed = false;
					} else {
						HandleMoveKeep(players[p.owner], p, i, 4, 5);
					}
				}

				// deal starter card
				msg.PutInt(starter = DrawDeck());

				if (!(starter >> 2 == (int)CardRank.FJack && AddScore(playerInfo[dealer], 2))) {
					gamePhase = GamePhase.PLAY;
				}

				break;
			}

			case GamePhase.PLAY:
				MovePlay(msg);
				break;

			case GamePhase.PRE:
				MoveShow(msg);
				break;

			case GamePhase.POST:
				gamePhase = GamePhase.DEAL;
				UnsetPassed();
				break;
		}

		Broadcast(msg);
	}

	byte GetRankLimit() {
		var rankLimit = 31 - 1 - trickCount;
		if (rankLimit >= 10 - 1) {
			rankLimit = (int)CardRank.NUM - 1;
		}
		return (byte)rankLimit;
	}

	IEnumerable<int> GetValidMoves(RoundPlayerInfo p, byte rankLimit) {
		return Enumerable.Range(p.played, p.cards.Count - p.played)
				.Where(i => (p.cards[i] >> 2) <= rankLimit);
	}

	void MovePlay(ByteWriter msg) {
		var p = playerInfo[turnIndex];

		// validate move
		pendingMove += (byte)p.played;
		var rankLimit = GetRankLimit();
		if (pendingMove >= p.cards.Count || (p.cards[pendingMove] >> 2) > rankLimit) {
			var validMoveList = GetValidMoves(p, rankLimit).ToList();
			if (validMoveList.Count == 0) {
				// must pass
				msg.Add(0xff);

				playerInfo[turnIndex].passed = true;

				if (RotateUnpassed()) {
					MoveFinalNewTrick();
				}
				return;
			}
			pendingMove = (byte)rng.Choice(validMoveList);
		}

		Util.Swap(p.cards, pendingMove, p.played);
		var card = p.cards[p.played++];
		msg.Add(card);

		play.Add(card);
		var cardRank = card >> 2;
		trickCount += (byte)Math.Min(cardRank + 1, 10);

		// check 15
		var scoreDelta = trickCount == 15 ? 2 : 0u;

		// check pairs
		if (play.Count >= 2 && play[play.Count - 2] >> 2 == cardRank) {
			if (play.Count >= 3 && play[play.Count - 3] >> 2 == cardRank) {
				if (play.Count >= 4 && play[play.Count - 4] >> 2 == cardRank) {
					scoreDelta += 12;
				} else {
					scoreDelta += 6;
				}
			} else {
				scoreDelta += 2;
			}
		}

		// check run
		var bestRun = play.Count - 1;
		var bitmask = 0;
		var lowRank = cardRank;
		var highRank = cardRank;
		for (var i = bestRun; i >= 0; i--) {
			var rank = play[i] >> 2;
			if ((bitmask & (1 << rank)) != 0) {
				break;
			}
			bitmask |= 1 << rank;

			if (lowRank > rank) {
				lowRank = rank;
			}
			if (highRank < rank) {
				highRank = rank;
			}

			if (highRank - lowRank + 1 == play.Count - i) {
				bestRun = i;
			}
		}
		if (bestRun <= play.Count - 3) {
			scoreDelta += (uint)(play.Count - bestRun);
		}

		if (AddScore(p, scoreDelta)) {
			return;
		}

		if (playerInfo.Any(p => p.played < p.cards.Count)) {
			MovePlayPost();
		} else if (!MoveFinal()) {
			if (optPre) {
				gamePhase = GamePhase.PRE;
				UnsetPassed();
			} else {
				MoveShow(msg);
			}
		}
	}

	void MovePlayPost() {
		trickTurn++;

		if (trickCount == 31) {
			MoveFinalNewTrick();
		} else {
			RotateUnpassed();

			if (optSkipEmpty) {
				while (playerInfo[turnIndex].played == playerInfo[turnIndex].cards.Count) {
					playerInfo[turnIndex].passed = true;
					if (RotateUnpassed()) {
						MoveFinalNewTrick();
						break;
					}
				}
			}
		}
	}

	bool MoveFinal() {
		if (AddScore(playerInfo[turnIndex], trickCount == 31 ? 2 : 1u)) {
			return true;
		}

		// next trick
		trickNum++;
		trickTurn = 0;
		trickCount = 0;
		play.Clear();
		UnsetPassed();
		return false;
	}

	void MoveFinalNewTrick() {
		if (!MoveFinal()) {
			RotatePlayers();

			if (optSkipEmpty) {
				while (playerInfo[turnIndex].played == playerInfo[turnIndex].cards.Count) {
					// current player doesn't have cards to play

					playerInfo[turnIndex].passed = true;
					RotatePlayers();
				}
			}
		}
	}

	bool RotateUnpassed() {
		var i = turnIndex;
		do {
			if (++i == playerInfo.Count) {
				i = 0;
			}
		} while (i != turnIndex && playerInfo[i].passed);
		return turnIndex == (turnIndex = i);
	}

	void MoveShow(ByteWriter msg) {
		turnIndex = dealer;
		do {
			RotatePlayers();

			if (MoveShowHand(msg, playerInfo[turnIndex].cards, false)) {
				return;
			}
		} while (turnIndex != dealer);

		if (MoveShowHand(msg, crib, true)) {
			return;
		}

		EndHand();
	}

	void EndHand() {
		handNum++;
		gamePhase = optPost ? GamePhase.POST : GamePhase.DEAL;
		UnsetPassed();
	}

	void UnsetPassed() {
		foreach (var p in playerInfo) {
			p.passed = false;
		}
	}

	readonly byte[] PAIR_BONUS = { 0, 0, 2, 6, 12 };

	bool MoveShowHand(ByteWriter msg, IList<byte> hand, bool isCrib) {
		foreach (var c in hand) {
			msg.Add(c);
		}

		var cards = new byte[hand.Count + 1];
		cards[0] = starter;
		hand.CopyTo(cards, 1);

		// 15
		var countByRank = new byte[(int)CardRank.NUM];
		var ways = new uint[15 + 1];
		ways[0] = 1;

		foreach (var c in cards) {
			var rank = c >> 2;
			countByRank[rank]++;
			for (int j = 15, i = j - Math.Min(rank + 1, 10); i >= 0; j--, i--) {
				ways[j] += ways[i];
			}
		}

		uint scoreDelta = ways[15] << 1;

		// runs
		uint maxRunLength = 0;
		uint maxRunStart = 0;
		for (uint i = 0, j = 0; i < (int)CardRank.NUM; i++) {
			if (countByRank[i] == 0) {
				j = i + 1;
			} else if (maxRunLength <= i - j) {
				maxRunLength = i - j + 1;
				maxRunStart = j;
			}
		}
		if (maxRunLength >= 3) {
			var runs = countByRank[maxRunStart];
			for (var i = 1; i < maxRunLength; i++) {
				runs *= countByRank[maxRunStart + i];
			}

			scoreDelta += runs * maxRunLength;
		}

		// pairs
		foreach (var c in countByRank) {
			scoreDelta += PAIR_BONUS[c];
		}

		var flushSuit = hand[0] & 3;
		var startSuit = starter & 3;
		if (flushSuit == (hand[1] & 3) && flushSuit == (hand[2] & 3) && flushSuit == (hand[3] & 3)) {
			// flush
			scoreDelta += flushSuit == startSuit ? 5 : isCrib ? 0 : 4u;
		}

		if (hand.Contains((byte)(((int)CardRank.FJack << 2) | startSuit))) {
			// jack of same suit as starter
			scoreDelta++;
		}

		return AddScore(playerInfo[turnIndex], scoreDelta);
	}

	bool AddScore(RoundPlayerInfo player, uint points) {
		bool end = (player.score += points) >= optScoreTarget;
		if (end) {
			gamePhase = GamePhase.END;
		}
		return end;
	}
}

enum CardRank {
	Ace,
	N2,
	N3,
	N4,
	N5,
	N6,
	N7,
	N8,
	N9,
	N10,
	FJack,
	FQueen,
	FKing,
	NUM,
}

enum GamePhase {
	DEAL,
	PLAY,
	PRE,
	POST,
	END,
}
