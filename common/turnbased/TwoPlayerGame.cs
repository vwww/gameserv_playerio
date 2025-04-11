enum Winner {
	NONE = 0,
	P0,
	P1,
	DRAW,
}

partial class Player {
	public PlayerScoreTie score = new();

	public bool CanResetScore() => score.CanReset();
	public void ResetScore() => score.Reset();
}

partial class Room {
	int p0 = -1, p1 = -1;
	bool curPlayer;
	int drawOffer;

	readonly RNG rng = new();

	Winner winner;

	const int MIN_PLAYERS_ACTIVE = 2;
	const int MAX_PLAYERS_ACTIVE = 2;

	partial void WriteWelcome2(ByteWriter b) {
		WriteRoundPlayers(b);
		WriteWelcome3(b);
	}
	partial void WriteWelcome3(ByteWriter b);

	partial void WriteRoundStartInfo2(ByteWriter b) {
		WriteRoundPlayers(b);
		WriteRoundStartInfo3(b);
	}
	partial void WriteRoundStartInfo3(ByteWriter b);

	void WriteRoundPlayers(ByteWriter b) {
		b.PutInt(p0);
		b.PutInt(p1);
	}

	void WriteWelcomePlayer(ByteWriter b, Player player) {
		player.score.WriteTo(b);
	}

	void InitPlayers() {
		curPlayer = false; // p0 always goes first

		var turnOrder = rng.NextBit();
		p0 = roundCurPlayers[turnOrder].cn;
		p1 = roundCurPlayers[turnOrder ^ 1].cn;

		// drawOffer = 0; // not needed
	}

	void CheckLeavingPlayer(Player player) {
		if (player.cn == p0) {
			EndRoundEarly(Winner.P1);
		} else if (player.cn == p1) {
			EndRoundEarly(Winner.P0);
		}
	}

	bool CanMakeMove(Player player) => player.cn == (curPlayer ? p1 : p0);

	bool IsGameOver() => (winner = CheckWinner()) != Winner.NONE;

	void GameOver() => EndRound(winner, false);

	void GameOverCleanup() {
		drawOffer = 0;
		p0 = p1 = -1;

		GameOverCleanup2();
	}

	void EndRound(Winner winner, bool earlyEnd) {
		turnTimer?.Stop();
		turnTimer = null;

		if (winner == Winner.P0) {
			players[p0].score.AddWin();
			players[p1].score.AddLoss();
		} else if (winner == Winner.P1) {
			players[p0].score.AddLoss();
			players[p1].score.AddWin();
		} else {
			players[p0].score.AddTie();
			players[p1].score.AddTie();
		}

		Broadcast(new ByteWriter()
			.PutType(MsgS2C.END_ROUND)
			.PutInt(((int)winner - 1) | (earlyEnd ? (1 << 2) : 0))
			.ToArray());
	}

	void EndRoundEarly(Winner winner) {
		EndRound(winner, true);
		GameOverCleanup();
		RoundFinish();
	}

	void ProcessMsgForfeit(Player player) {
		if (state == GameState.ACTIVE) {
			if (player.cn == p0) {
				EndRoundEarly(Winner.P1);
			} else if (player.cn == p1) {
				EndRoundEarly(Winner.P0);
			}
		}
	}

	void ProcessMsgOfferDraw(Player player) {
		if (state == GameState.ACTIVE) {
			int p = player.cn == p0 ? 1 : player.cn == p1 ? 2 : 0;
			if (p == 0) {
				return;
			}

			if (drawOffer == 0) {
				drawOffer = p;

				var drawOfferMsg = new ByteWriter()
					.PutType(MsgS2C.OFFER_DRAW)
					.PutInt(drawOffer)
					.PutInt(player.cn)
					.ToArray();

				players[p0].Send(drawOfferMsg);
				players[p1].Send(drawOfferMsg);
			} else if (drawOffer != p) {
				EndRoundEarly(Winner.DRAW);
			}
		}
	}

	void ProcessMsgRejectDraw(Player player) {
		if (state == GameState.ACTIVE && drawOffer != 0) {
			if (player.cn == p0 || player.cn == p1) {
				drawOffer = 0;

				var drawRejectMsg = new ByteWriter()
					.PutType(MsgS2C.OFFER_DRAW)
					.PutInt(drawOffer)
					.PutInt(player.cn)
					.ToArray();

				players[p0].Send(drawRejectMsg);
				players[p1].Send(drawRejectMsg);
			}
		}
	}
}
