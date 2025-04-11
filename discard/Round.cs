partial class RoundPlayerInfo {
	public List<int> discarded = new();
	public int discardedSum;
	public bool immune;
	public int hand;
}

partial class RoundDiscInfo {
	public List<int> discarded = new();
	public int discardedSum;
}

partial class Room {
	int ROUND_TIME => optTurnTime;

	readonly List<int> drawPile = new(15);
	readonly int[] discardCount = new int[8];

	bool useHand;
	int target;
	int guess;

	void SendMoveConfirm(Player player) {
		player.Send(new ByteWriter()
			.PutType(MsgS2C.MOVE_CONFIRM)
			.PutInt((useHand ? 1 : 0) | (guess << 1))
			.PutInt(target)
			.ToArray());
	}

	void ProcessMsgMoveHand(Player player, ByteReader msg, bool useHandNew) {
		if (state == GameState.ACTIVE && CanMakeMove(player)) {
			useHand = useHandNew;
			SendMoveConfirm(player);
		}
	}

	void ProcessMsgMoveTarget(Player player, ByteReader msg) {
		var targetNew = msg.GetInt();

		if (state == GameState.ACTIVE && CanMakeMove(player)) {
			target = targetNew;
			SendMoveConfirm(player);
		}
	}

	void ProcessMsgMoveGuess(Player player, ByteReader msg) {
		var guessNew = msg.GetInt();

		if (state == GameState.ACTIVE && CanMakeMove(player)) {
			guess = guessNew;
			SendMoveConfirm(player);
		}
	}

	bool CanMakeMove(Player player) {
		return playerInfo[turnIndex].owner == player.cn;
	}

	partial void WriteWelcome3(ByteWriter b) {
		b.PutInt(drawPile.Count - (state == GameState.ACTIVE ? 1 : 0));
		foreach (var c in discardCount) {
			b.PutInt(c);
		}
	}

	void WritePlayerInfo(ByteWriter b, RoundPlayerInfo p) {
		b.PutInt((p.discarded.Count << 1) | (p.immune ? 1 : 0));
		foreach (var c in p.discarded) {
			b.PutInt(c);
		}
	}

	void WriteDiscInfo(ByteWriter b, RoundDiscInfo d) {
		b.PutInt(d.discarded.Count);
		foreach (var c in d.discarded) {
			b.PutInt(c);
		}
	}

	bool WriteEliminateInfo(ByteWriter b, RoundDiscInfo d, int pn, RoundPlayerInfo p, Player c, bool early, out bool newMove, out bool moveStarted) {
		newMove = early;
		if (moveStarted = (early && pn == turnIndex)) {
			var lastIndex = drawPile.Count - 1;
			var otherCard = drawPile[lastIndex];
			drawPile.RemoveAt(lastIndex);

			p.discarded.Add(otherCard);
			p.discardedSum += otherCard;
			discardCount[otherCard - 1]++;

			b.PutInt(otherCard);
		}

		b.PutInt(p.hand);

		d.discarded = p.discarded; // old list won't be used elsewhere
		d.discarded.Add(p.hand);
		d.discardedSum = p.discardedSum + p.hand;

		c.score.AddRank(playerInfo.Count, playerInfo.Count + discInfo.Count);

		discardCount[p.hand - 1]++;

		if (turnIndex > pn) {
			turnIndex--;
		} else if (turnIndex == playerInfo.Count - 1) {
			turnIndex = 0;
		}

		return false;
	}

	void SetTurnOrder() {
		rng.Shuffle(playerInfo);
		// turnIndex = 0;

		// all suitable players for swapping with first
		var firstCandidates = playerInfo.Select((p, i) => players[p.owner].score.lastRank == 1 ? i : -1).Where(i => i != -1).ToList();

		int pn = rng.Choice(firstCandidates); // will be 0 if list is empty
		if (pn != 0) {
			Util.Swap(playerInfo, 0, pn);
		}
	}

	bool IsGameOver() => playerInfo.Count <= 1 || drawPile.Count <= 1;

	void GameOver() {
		var winMsg = new ByteWriter().PutType(MsgS2C.END_ROUND);
		foreach (var p in playerInfo) {
			winMsg.PutInt(p.hand);
		}
		Broadcast(winMsg.ToArray());

		var sortedPlayers = playerInfo.OrderByDescending(p => p.hand).ThenByDescending(p => p.discardedSum);

		RoundPlayerInfo prev = null;
		int rank = 1, ordinalRank = 1;
		foreach (var p in sortedPlayers) {
			if (prev != null && !(prev.hand == p.hand && prev.discarded.Sum() == p.discarded.Sum())) {
				rank = ordinalRank;
			}

			players[p.owner].score.AddRank(rank, playerInfo.Count + discInfo.Count);

			prev = p;
			ordinalRank++;
		}
	}

	partial void WriteRoundStartInfo3(ByteWriter b) {
		drawPile.Clear();
		for (int i = 0; i < optDecks; i++) {
			drawPile.AddRange(new[] { 1, 1, 1, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8 });
		}
		rng.Shuffle(drawPile);
		Array.Clear(discardCount, 0, discardCount.Length);

		// deal cards
		foreach (var p in playerInfo) {
			var lastIndex = drawPile.Count - 1;

			p.hand = drawPile[lastIndex];
			drawPile.RemoveAt(lastIndex);

			players[p.owner].Send(new ByteWriter()
				.PutType(MsgS2C.PLAYER_PRIVATE_INFO_MY_HAND)
				.PutInt(p.hand)
				.ToArray());
		}
	}

	void MoveStarted() {
		var p = playerInfo[turnIndex];
		p.immune = false;

		var otherCard = drawPile[drawPile.Count - 1];

		players[p.owner].Send(new ByteWriter()
			.PutType(MsgS2C.PLAYER_PRIVATE_INFO_ALT_MOVE)
			.PutInt(otherCard)
			.PutType(MsgS2C.MOVE_CONFIRM) // init random move
			.PutInt(((useHand = rng.NextBool()) ? 1 : 0) | ((guess = 0) << 1))
			.PutInt(target = -1)
			.ToArray());
	}

	void MoveEnded() {
		var p = playerInfo[turnIndex];

		var lastIndex = drawPile.Count - 1;
		var otherCard = drawPile[lastIndex];
		drawPile.RemoveAt(lastIndex);

		// force move when 7 is present with 5 or 6
		if (p.hand == 7 && (otherCard == 5 || otherCard == 6)) {
			useHand = true;
		} else if (otherCard == 7 && (p.hand == 5 || p.hand == 6)) {
			useHand = false;
		}

		// apply move
		var used = p.hand;
		if (useHand) {
			p.hand = otherCard;
		} else {
			used = otherCard;
		}
		if (used == 8) {
			// when voluntarily discarding 8, discard during elimination step
			p.hand = 8;
		} else {
			p.discarded.Add(used);
			p.discardedSum += used;
			discardCount[used - 1]++;
		}

		target = SelectTarget(target, used == 5 ? -1 : turnIndex); // 5 can target self
		var targetPlayer = target == -1 ? null : playerInfo[target];

		var msg = new ByteWriter()
			.PutType(MsgS2C.END_TURN)
			.PutInt(used)
			.PutInt(target);

		var toElim = -1;

		switch (used) {
			case 1:
				// target another player and guess; if correct, he discards without effect and loses (game ends if one player remains)
				if (guess < 2 || guess > 8) guess = rng.Choice(new[] { 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8 });
				bool elim = targetPlayer?.hand == guess;
				msg.PutInt(elim ? -guess : guess);
				if (elim) {
					toElim = target;
				}
				break;
			case 2:
				// target another player; look at his hand
				break;
			case 3:
				// target another player; compare; lower player loses (no action on tie)
				if (targetPlayer != null) {
					if (targetPlayer.hand != p.hand) {
						var lose = p.hand < targetPlayer.hand;
						msg.PutInt(lose ? -p.hand : targetPlayer.hand);
						toElim = (lose ? turnIndex : target);
					} else {
						msg.PutInt(0);
					}
				}
				break;
			case 4:
				// cannot be targeted until next turn
				p.immune = true;
				break;
			case 5:
				// target one player (including self); he discards without effect (unless 8) and draws new card
				if (targetPlayer == null) break; // should not happen (target should always exist)

				var discarded = targetPlayer.hand;
				msg.PutInt(discarded);

				if (discarded == 8) {
					toElim = target;
				} else {
					targetPlayer.discarded.Add(discarded);
					targetPlayer.discardedSum += discarded;
					discardCount[discarded - 1]++;

					targetPlayer.hand = drawPile[lastIndex - 1];
					drawPile.RemoveAt(lastIndex - 1);
				}
				break;
			case 6:
				// target another player; trade cards
				if (targetPlayer != null) {
					Util.Swap(ref p.hand, ref targetPlayer.hand);
				}
				break;
			case 7:
				// no action, but must discard if other is 5 or 6
				break;
			case 8:
				// lose
				toElim = turnIndex;
				break;
		}

		Broadcast(msg.ToArray());

		if (targetPlayer != null) {
			switch (used) {
				case 2:
					players[p.owner].Send(new ByteWriter()
						.PutType(MsgS2C.PLAYER_PRIVATE_INFO_MOVE)
						.PutInt(2)
						.PutInt(target)
						.PutInt(targetPlayer.hand)
						.ToArray());
					break;

				case 3:
					players[p.owner].Send(new ByteWriter()
						.PutType(MsgS2C.PLAYER_PRIVATE_INFO_MOVE)
						.PutInt(3)
						.PutInt(target)
						.PutInt(targetPlayer.hand)
						.ToArray());

					players[targetPlayer.owner].Send(new ByteWriter()
						.PutType(MsgS2C.PLAYER_PRIVATE_INFO_MOVE)
						.PutInt(3)
						.PutInt(turnIndex)
						.PutInt(p.hand)
						.ToArray());
					break;

				case 5:
					if (toElim != -1) break;
					players[targetPlayer.owner].Send(new ByteWriter()
						.PutType(MsgS2C.PLAYER_PRIVATE_INFO_MY_HAND)
						.PutInt(targetPlayer.hand)
						.ToArray());
					break;

				case 6:
					players[p.owner].Send(new ByteWriter()
						.PutType(MsgS2C.PLAYER_PRIVATE_INFO_MOVE)
						.PutInt(6)
						.PutInt(target)
						.PutInt(p.hand)
						.ToArray());

					players[targetPlayer.owner].Send(new ByteWriter()
						.PutType(MsgS2C.PLAYER_PRIVATE_INFO_MOVE)
						.PutInt(6)
						.PutInt(turnIndex)
						.PutInt(targetPlayer.hand)
						.ToArray());
					break;
			}
		}

		RotatePlayers();

		if (toElim >= 0) {
			EliminatePlayer(toElim, out bool _);
		}
	}

	int SelectTarget(int pn, int exclude = -1) {
		if (0 <= pn && pn < playerInfo.Count && pn != exclude) {
			return pn;
		}

		var validTargets = playerInfo.Select((p, i) => i != exclude && !p.immune ? i : -1).Where(i => i != -1).ToList();
		if (validTargets.Count == 0) {
			return -1;
		}

		return rng.Choice(validTargets);
	}
}
