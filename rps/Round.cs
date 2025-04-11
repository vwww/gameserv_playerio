partial class Player {
	public int move;
}

partial class Room {
	int ROUND_TIME => optRoundTime;

	readonly RNG rng = new();

	void ProcessMsgMove(Player player, ByteReader msg) {
		int move = msg.GetInt() & 3;
		player.move = move;
		player.Send(new ByteWriter()
				.PutType(MsgS2C.MOVE_CONFIRM)
				.PutInt(move)
				.ToArray());
	}

	void RoundEnded() {
		var playerOrder = roundCurPlayers.ToArray();
		var moves = new byte[playerOrder.Length];
		rng.Shuffle(playerOrder);

		// Count players with each move
		var count = new ulong[3];
		var optStreakPlayers = new List<int>();
		for (int i = 0; i < playerOrder.Length; i++) {
			var p = playerOrder[i];
			var move = p.move;

			if (move == 0) {
				// Resolve unspecified moves
				if (!optClassic && p.roundScore.streak <= -3) {
					// Resolve later as a group
					optStreakPlayers.Add(i);
					continue;
				}

				move = rng.Next(3) + 1;
			}
			move--;
			moves[i] = (byte)move;

			count[move]++;
		}

		ulong botCount;
		if (optBotBalance < 0) {
			botCount = Math.Max(0, (ulong)-optBotBalance - (uint)playerOrder.Length);
		} else {
			botCount = (ulong)optBotBalance;
		}
		var countBotOnly = MultinomialSampler.SampleEqualP(rng, botCount, 3);
		for (int i = 0; i < 3; i++) {
			count[i] += countBotOnly[i];
		}

		// Make optimal moves for players with streak
		if (optStreakPlayers.Count > 0) {
			var move = rng.Choice(RPSBattle.CalculateBestMoves(count, (uint)optStreakPlayers.Count, optInverted, optCount));
			foreach (var p in optStreakPlayers) {
				moves[p] = move;
			}
			count[move] += (ulong)optStreakPlayers.Count;
		}

		var battleResults = RPSBattle.CalculateBattleResults(count, optInverted, optCount);
		var ltw = RPSBattle.CalculateLTW(count, battleResults);

		// deterministic random wording
		/*
		var detrndBytes = new byte[4];
		rnd.NextBytes(detrndBytes);
		uint detRndResult = BitConverter.ToUInt32(detrndBytes, 0);
		*/
		var detRndResult = rng.Next(0x10); // only need 4 bits

		var winMsg = new ByteWriter()
			.PutType(MsgS2C.END_ROUND)
			.PutInt(detRndResult)
			.PutInt(battleResults[0])
			.PutInt(battleResults[1])
			.PutInt(battleResults[2])
			.PutULong(count[0])
			.PutULong(count[1])
			.PutULong(count[2])
			.PutInt(playerOrder.Length);

		for (int i = 0; i < playerOrder.Length; i++) {
			var p = playerOrder[i];
			int move = moves[i];
			ulong[] pLTW = ltw[move];

			p.battleLosses += pLTW[0];
			p.battleTies += pLTW[1];
			p.battleWins += pLTW[2];

			if (pLTW[2] > pLTW[0]) {
				p.roundScore.AddWin();
				p.score += p.roundScore.streak > 2 && !optClassic ? p.roundScore.streak - 1 : 1;
			} else if (pLTW[2] == pLTW[0]) {
				p.roundScore.ties++;
			} else {
				p.roundScore.AddLoss();
				p.score--;
			}

			winMsg.PutInt(p.cn);
			winMsg.PutInt(move);
		}

		Broadcast(winMsg.ToArray());
	}
}
