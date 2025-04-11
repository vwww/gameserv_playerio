partial class RoundPlayerInfo {
	public CardCount hand = new((int)CardRank.NUM);
	public CardCount discardClaim = new((int)CardRank.NUM);
}

partial class RoundDiscInfo {
	public ulong handSize;
	public int trickNum;
	public ulong duration;
}

partial class Room {
	int ROUND_TIME => optTurnTime;

	DateTime startTime;
	DateTime callTime;
	DateTime noCallTime;

	int trickNum;
	int trickTurn;
	ulong trickCount;
	CardRank trickRank;
	readonly CardCount discardActualLast = new((int)CardRank.NUM);
	readonly CardCount discardActualTotal = new((int)CardRank.NUM);
	readonly CardCount discardClaimTotal = new((int)CardRank.NUM);

	bool challengePossible;
	bool challengeMoveLegit;

	int passIndex;

	CardRank pendingMoveRank;
	CardCount pendingMoveCards = new((int)CardRank.NUM);

	void ProcessMsgMove(Player player, ByteReader msg) {
		var rankNew = (CardRank)(sbyte)msg.Get();
		var cardCountNew = msg.GetCardCount((int)CardRank.NUM);

		if (state == GameState.ACTIVE && CanMakeMoveNoFinal(player)) {
			player.Send(new ByteWriter()
				.PutType(MsgS2C.MOVE_CONFIRM)
				.PutInt((int)(pendingMoveRank = rankNew))
				.PutCardCount(pendingMoveCards = cardCountNew)
				.ToArray());
		}
	}

	bool CanMakeMove(Player player) {
		return CanMakeMoveNoFinal(player) && DateTime.UtcNow >= callTime;
	}

	bool CanMakeMoveNoFinal(Player player) {
		return playerInfo[turnIndex].owner == player.cn;
	}

	void ProcessMsgMoveCall(Player player) {
		if (!challengePossible || DateTime.UtcNow < noCallTime) return;

		var callerNum = playerInfo.FindIndex((p) => p.owner == player.cn);
		if (callerNum < 0) return;
		var caller = playerInfo[callerNum];

		var prevNum = (turnIndex == 0 ? playerInfo.Count : turnIndex) - 1;
		if (prevNum == callerNum) return;
		var prevPlayer = playerInfo[prevNum];

		var victim = challengeMoveLegit ? caller : playerInfo[prevNum];

		victim.hand.Add(discardActualTotal);

		var msgPenalty = new ByteWriter()
				.PutType(MsgS2C.PLAYER_PRIVATE_INFO_PENALTY)
				.PutCardCount(discardActualTotal);
		players[victim.owner].Send(msgPenalty);

		if (optCheck != ModeCheck.ARBITER) {
			var msgReveal = new ByteWriter()
					.PutType(MsgS2C.PLAYER_PRIVATE_INFO_REVEAL)
					.PutCardCount(discardActualLast);
			if (optCheck == ModeCheck.CALLER) {
				player.Send(msgReveal);
			} else {
				if (optCheck == ModeCheck.PUBLIC_ALL) {
					discardActualTotal.Sub(discardActualLast);
					msgReveal.PutCardCount(discardActualTotal);
				}
				Broadcast(msgReveal);
			}
		}

		var msgCall = new ByteWriter()
				.PutType(challengeMoveLegit ? MsgS2C.CALL_FAIL : MsgS2C.CALL_WIN)
				.PutInt(callerNum);
		Broadcast(msgCall);

		foreach (var p in playerInfo) {
			p.discardClaim.Reset();
		}

		discardActualTotal.Reset();
		discardClaimTotal.Reset();

		challengePossible = false;

		// reset turn timer
		turnTimer?.Stop();
		turnTimer = null;

		CheckWin(false);
	}

	partial void WriteWelcome3(ByteWriter b) {
		if (state != GameState.ACTIVE) return;

		b.PutInt((trickNum << 1) | (challengePossible ? 1 : 0));
		b.PutInt(trickTurn);
		if (trickTurn != 0) {
			b.PutInt((int)trickRank);
			b.PutULong(trickCount); // trickCount uses 54 bits
		}
		b.PutCardCount(discardClaimTotal);
		if (challengePossible) {
			b.PutInt(callTime > DateTime.UtcNow ? (int)(callTime - DateTime.UtcNow).TotalMilliseconds : 0);
		}
		if (optTricks != ModeTricks.FORCED) {
			b.PutInt(passIndex);
		}
	}

	void WritePlayerInfo(ByteWriter b, RoundPlayerInfo p) {
		b.PutCardCount(p.discardClaim);
		b.PutULong((p.hand.total << 1) | (p.passed ? 1u : 0)); // hand total uses 59 bits
	}

	void WriteDiscInfo(ByteWriter b, RoundDiscInfo d) {
		b.PutULong(d.handSize);
		b.PutInt(d.trickNum);
		b.PutULong(d.duration);
	}

	bool WriteEliminateInfo(ByteWriter b, RoundDiscInfo d, int pn, RoundPlayerInfo p, Player c, bool early, out bool newMove, out bool moveStarted) {
		b.PutULong(d.duration = (ulong)(DateTime.UtcNow - startTime).TotalMilliseconds);
		d.trickNum = trickNum;

		moveStarted = false;
		if (newMove = early) {
			discardActualTotal.Add(p.hand);
			discardClaimTotal.AddCards((int)CardRank.Joker, (d.handSize = p.hand.total));

			if (pn + 1 == (turnIndex == 0 ? playerInfo.Count : turnIndex)) {
				challengePossible = false;
			} else {
				noCallTime = DateTime.UtcNow.AddMilliseconds(750);

				if ((moveStarted = pn == turnIndex) && optTricks != ModeTricks.FORCED) {
					NextTurnAfterPass(p);
				}
			}
		}

		var rank = discIndex + (early ? playerInfo.Count : 1);
		var totalPlayers = playerInfo.Count + discInfo.Count;
		c.score.AddRank(rank, totalPlayers);

		var lastIndex = playerInfo.Count - 1;
		// fix turnIndex
		if (turnIndex > pn) {
			turnIndex--;
		} else if (turnIndex == lastIndex) {
			turnIndex = 0;
		}

		if (optTricks != ModeTricks.FORCED) {
			if (early) {
				// fix passIndex
				if (passIndex > pn) {
					passIndex--;
				} else if (passIndex == lastIndex) {
					passIndex = 0;
				}
			} else {
				passIndex = turnIndex;
			}
		}

		return !early;
	}

	void SetTurnOrder() {
		rng.Shuffle(playerInfo);
		turnIndex = rng.Next(playerInfo.Count);

		passIndex = -1;
	}

	bool IsGameOver() => playerInfo.Count <= 1;

	void GameOver() {
		var duration = (ulong)(DateTime.UtcNow - startTime).TotalMilliseconds;

		var winMsg = new ByteWriter().PutType(MsgS2C.END_ROUND);
		winMsg.PutULong(duration);
		Broadcast(winMsg.ToArray());

		// handle last player
		var rank = discIndex + 1;
		var totalPlayers = 1 + discInfo.Count;
		players[playerInfo[0].owner].score.AddRank(rank, totalPlayers);

		challengePossible = false;
	}

	partial void WriteRoundStartInfo3(ByteWriter b) {
		startTime = DateTime.UtcNow;
		trickNum = trickTurn = 0;
		trickCount = 0;

		discardActualTotal.Reset();
		discardClaimTotal.Reset();

		var D = optDecks;

		// deal cards
		var privateCardCounts = rng.DealCards(new[] { 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 4 * D, 2 * D }, 54 * D, (uint)playerInfo.Count);

		for (var i = 0; i < playerInfo.Count; i++) {
			var p = playerInfo[i];

			p.hand.total = 0;
			foreach (var c in (p.hand.count = privateCardCounts[i])) {
				p.hand.total += c;
			}
			p.discardClaim.Reset();
			p.passed = false;

			players[p.owner].Send(new ByteWriter()
				.PutType(MsgS2C.PLAYER_PRIVATE_INFO_HAND)
				.PutCardCount(p.hand)
				.ToArray());
		}

		b.PutInt(turnIndex);
	}

	void MoveStarted() {
		pendingMoveRank = (CardRank)(-1);
		pendingMoveCards.Reset();
	}

	void MoveEnded() {
		// previous player wins if hand is empty and unchallenged
		var prevPN = (turnIndex == 0 ? playerInfo.Count : turnIndex) - 1;
		if (playerInfo[prevPN].hand.total == 0) {
			EliminatePlayer(prevPN, out bool _);
		}

		var p = playerInfo[turnIndex];


		var msg = new ByteWriter()
				.PutType(MsgS2C.END_TURN);

		if (pendingMoveRank < 0 && trickTurn != 0 && optTricks != ModeTricks.FORCED) {
			// skip/pass

			msg.PutInt(-1);

			if (optTricks != ModeTricks.FORCED) {
				NextTurnAfterPass(p);
			}

			challengePossible = false;
		} else {
			// sanitize move
			var moveCardCount = pendingMoveCards.total;

			if (moveCardCount == 0 && (!optCountZero || trickTurn == 0) || moveCardCount > 6 * optDecks) {
				moveCardCount = 6 * optDecks;
			}

			if (trickTurn == 0) {
				// start new trick

				// enforce start rank
				if (pendingMoveRank < 0 || pendingMoveRank >= CardRank.Joker || !CheckRankStart(pendingMoveRank)) {
					if (optRankStartO) {
						pendingMoveRank = (CardRank)rng.Next(1, (int)CardRank.Joker - 1);
					} else if (optRankStartA) {
						pendingMoveRank = CardRank.Ace;
					} else {
						pendingMoveRank = CardRank.FKing;
					}
				}

				// any valid count is allowed
			} else {
				// play within current trick

				// enforce rank
				if (pendingMoveRank < 0 || pendingMoveRank >= CardRank.Joker || !CheckRank(pendingMoveRank, trickRank)) {
					pendingMoveRank = rng.Choice(Enumerable.Range(0, (int)CardRank.Joker).Cast<CardRank>().Where((x) => CheckRank(x, trickRank)).ToList());
				}

				// enforce count
				if (!(moveCardCount == 0 ? optCountZero : moveCardCount == trickCount ? optCountSame : moveCardCount < trickCount ? optCountLess : optCountMore)) {
					if (optCountSame) {
						moveCardCount = trickCount;
					} else if (optCountLess) {
						moveCardCount = trickCount - 1;
					} else if (optCountMore) {
						moveCardCount = trickCount + 1;
					} else {
						moveCardCount = 0;
					}
				}
			}

			// may or must play all cards if not enough
			if (moveCardCount >= p.hand.total) {
				moveCardCount = p.hand.total;

				pendingMoveCards.Copy(p.hand);
			} else {
				// limit cards to hand
				for (var i = 0; i < (int)CardRank.NUM; i++) {
					if (pendingMoveCards.count[i] > p.hand.count[i]) {
						pendingMoveCards.count[i] = p.hand.count[i];
						pendingMoveCards.total -= pendingMoveCards.count[i] - p.hand.count[i];
					}
				}

				if (pendingMoveCards.total < moveCardCount) {
					// add lowest value cards
					var cardsToAdd = moveCardCount - pendingMoveCards.total;
					for (var i = 0; i < (int)CardRank.NUM && cardsToAdd != 0; i++) {
						var adjust = Math.Min(p.hand.count[i] - pendingMoveCards.count[i], cardsToAdd);
						pendingMoveCards.AddCards(i, adjust);
						cardsToAdd -= adjust;
					}
				} else if (pendingMoveCards.total > moveCardCount) {
					// remove highest value cards
					var cardsToRemove = pendingMoveCards.total - moveCardCount;
					for (var i = (int)CardRank.NUM - 1; i >= 0 && cardsToRemove != 0; i--) {
						var adjust = Math.Min(pendingMoveCards.count[i], cardsToRemove);
						pendingMoveCards.SubCards(i, adjust);
						cardsToRemove -= adjust;
					}
				}
			}

			// apply move
			bool endTrick = !NextRankPossible(pendingMoveRank) || NextCountImpossible(moveCardCount);

			if (optTricks != ModeTricks.FORCED) {
				passIndex = -1;

				var nextIndex = NextUnpassed(turnIndex);
				if (nextIndex == turnIndex) {
					endTrick = true;
				}

				if (!endTrick) {
					if (optTricks == ModeTricks.SINGLE_TURN && trickTurn != 0) {
						NextTurnAfterPass(p);
					} else {
						if (optTricks == ModeTricks.PASS_TURN) {
							UnsetPassed();
						}
						turnIndex = nextIndex;
					}
				} else if (p.hand.total == 0) {
					RotatePlayers();
				}
			}

			if (endTrick) {
				NextTrick();
			} else {
				trickCount = moveCardCount;
				trickRank = pendingMoveRank;
				trickTurn++;
			}
			discardActualTotal.Add(pendingMoveCards);
			p.hand.Sub(pendingMoveCards);

			p.discardClaim.AddCards((int)pendingMoveRank, moveCardCount);
			discardClaimTotal.AddCards((int)pendingMoveRank, moveCardCount);

			discardActualLast.Copy(pendingMoveCards);

			if (moveCardCount != 0) {
				challengePossible = true;
				challengeMoveLegit = pendingMoveCards.count[(int)pendingMoveRank] + pendingMoveCards.count[(int)CardRank.Joker] == moveCardCount;
				callTime = DateTime.UtcNow.AddMilliseconds(optCallTime);
				noCallTime = DateTime.UtcNow.AddMilliseconds(750);
			} else {
				challengePossible = false;
			}

			if (pendingMoveCards.total != 0) {
				var msgMover = new ByteWriter()
						.PutType(MsgS2C.PLAYER_PRIVATE_INFO_MOVE)
						.PutCardCount(pendingMoveCards);
				players[p.owner].Send(msgMover.ToArray());
			}

			msg.PutInt((int)pendingMoveRank);
			msg.PutULong(pendingMoveCards.total);
		}

		if (optTricks == ModeTricks.FORCED) {
			RotatePlayers();
		}

		Broadcast(msg.ToArray());
	}

	bool CheckRankStart(CardRank a) {
		if (a == CardRank.Ace) {
			return optRankStartA;
		} else if (a == CardRank.FKing) {
			return optRankStartK;
		} else {
			return optRankStartO;
		}
	}

	bool CheckRank(CardRank a, CardRank b) {
		if (a == b) {
			return optRank0;
		} else if (a == b + 1) {
			return optRank1u;
		} else if (a == b + 2) {
			return optRank2u;
		} else if (a == b - 1) {
			return optRank1d;
		} else if (a == b - 2) {
			return optRank2d;
		} else if (a == CardRank.Joker - 1 && b == 0) {
			return optRank1uw;
		} else if (a == 0 && b == CardRank.Joker - 1) {
			return optRank1dw;
		} else if (a == CardRank.Joker - 1 && b == (CardRank)1 || a == CardRank.Joker - 2 && b == 0) {
			return optRank2uw;
		} else if (a == 0 && b == CardRank.Joker - 2 || a == (CardRank)1 && b == CardRank.Joker - 1) {
			return optRank2dw;
		} else {
			return optRankother;
		}
	}

	bool NextRankPossible(CardRank rank) {
		return optRank0 || optRankother
		|| (rank == CardRank.Joker - 1 ? optRank1uw : optRank1u)
		|| (rank == 0 ? optRank1dw : optRank1d)
		|| (rank == CardRank.Joker - 2 ? optRank2uw : optRank2u)
		|| (rank == (CardRank)1 ? optRank2dw : optRank2d);
	}

	bool NextCountImpossible(ulong count) {
		return !optCountSame && (count <= 1 && !optCountMore || count == 6 * optDecks && !optCountLess);
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
	Joker,
	NUM,
}
