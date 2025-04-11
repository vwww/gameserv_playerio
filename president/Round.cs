partial class RoundPlayerInfo {
	public int role;
	public CardCount hand = new((int)CardRank.NUM);
	public CardCount discarded = new((int)CardRank.NUM);
}

partial class RoundDiscInfo {
	public int trickNum;
	public int trickTurn;
	public ulong duration;
	public CardCount hand = new((int)CardRank.NUM);
	public CardCount discarded = new((int)CardRank.NUM);
	public int prevRole;
	public int newRole;
}

partial class Room {
	int ROUND_TIME => optTurnTime;

	DateTime startTime;

	GamePhase gamePhase;

	int giveFlags;
	int pres;
	int scum;
	int vicePres;
	int highScum;

	bool revolution;
	int trickNum;
	int trickTurn;
	ulong trickCount;
	ulong trickTotal;
	CardRank trickRank;
	bool trick1Fewer;
	bool penalty;

	int passIndex;

	CardRank pendingMoveRank;
	ulong pendingMoveBase;
	ulong pendingMoveJokers;

	void ProcessMsgMove(Player player, ByteReader msg, int type) {
		var rankNew = (CardRank)(type != 0 ? (sbyte)msg.Get() : -1);
		var jokersNew = type != 0 ? msg.GetULong() : 0;
		var baseNew = type > 1 ? msg.GetULong() : 0;

		if (state == GameState.ACTIVE && CanMakeMove(player)) {
			pendingMoveRank = rankNew;
			pendingMoveBase = baseNew;
			pendingMoveJokers = jokersNew;

			// acknowledge with sanitized move
			FixMove(playerInfo[turnIndex].hand, scum == turnIndex);

			player.Send(new ByteWriter()
				.PutType(MsgS2C.MOVE_CONFIRM)
				.PutInt((int)pendingMoveRank)
				.PutULong(pendingMoveJokers)
				.PutULong(pendingMoveBase)
				.ToArray());
		}
	}

	void ProcessMsgMoveTransfer(Player player, ByteReader msg) {
		var a = (CardRank)(sbyte)msg.Get();
		var b = (CardRank)(sbyte)msg.Get();

		if (state == GameState.ACTIVE && gamePhase == GamePhase.GIVE_CARDS) {
			if ((giveFlags & 2) != 0 && pres >= 0 && playerInfo[pres].owner == player.cn) {
				HandleGiveDownCards(pres, scum, 2, a, b);
				giveFlags &= ~2;
			} else if ((giveFlags & 1) != 0 && vicePres >= 0 && playerInfo[vicePres].owner == player.cn) {
				HandleGiveDownCards(vicePres, highScum, 1, a, b);
				giveFlags &= ~1;
			} else {
				return;
			}

			if (giveFlags == 0) {
				TurnEnd();
			}
		}
	}

	void HandleGiveUpCards(int src, int dst, int num) {
		var msg = new ByteWriter()
			.PutType(MsgS2C.PLAYER_PRIVATE_INFO_GIVE);

		var p = playerInfo[src];
		var t = playerInfo[dst];

		while (num-- != 0) {
			bool useJoker = optKeepJokers ? p.hand.count[(int)CardRank.Joker] == p.hand.total : p.hand.count[(int)CardRank.Joker] != 0;
			var rank = useJoker ? (int)CardRank.Joker : Array.FindLastIndex(p.hand.count, (int)CardRank.Joker - 1, c => c != 0);

			p.hand.SubCards(rank, 1);
			t.hand.AddCards(rank, 1);

			msg.PutInt(rank);
		}

		players[p.owner].Send(msg);
		players[t.owner].Send(msg);
	}

	void HandleGiveDownCards(int src, int dst, int num, CardRank a, CardRank b) {
		var p = playerInfo[src];
		var t = dst < 0 ? null : playerInfo[dst];

		var msg = new ByteWriter();

		if (t != null) {
			msg.PutType(MsgS2C.PLAYER_PRIVATE_INFO_GIVE);
		} else {
			msg.PutType(MsgS2C.END_TURN_TRANSFER_DISCARD)
				.PutInt(src);
		}

		void HandleCard(CardRank a) {
			var rank = a >= 0 && a < CardRank.NUM && p.hand.count[(int)a] != 0 ? (int)a : Array.FindIndex(p.hand.count, c => c != 0);

			p.hand.SubCards(rank, 1);
			if (t != null) {
				t.hand.AddCards(rank, 1);
			} else {
				p.discarded.AddCards(rank, 1);
			}

			msg.PutInt(rank);
		}

		HandleCard(a);
		if (num == 2) {
			HandleCard(b);
		}

		if (t != null) {
			Broadcast(new ByteWriter()
				.PutType(MsgS2C.END_TURN_TRANSFER)
				.PutInt(src)
				.ToArray());

			players[p.owner].Send(msg);
			players[t.owner].Send(msg);
		} else {
			Broadcast(msg);
		}
	}

	bool CanMakeMove(Player player) {
		return gamePhase != GamePhase.GIVE_CARDS && playerInfo[turnIndex].owner == player.cn;
	}

	partial void WriteWelcome3(ByteWriter b) {
		if (state != GameState.ACTIVE) return;

		int hasFlags = 0;
		if (pres != -1) hasFlags |= 1 << 4;
		if (scum != -1) hasFlags |= 1 << 5;
		if (vicePres != -1) hasFlags |= 1 << 6;
		if (highScum != -1) hasFlags |= 1 << 7;
		b.Add((byte)((int)gamePhase | ((gamePhase == GamePhase.GIVE_CARDS ? giveFlags : revolution ? 1 : 0) << 2) | hasFlags));

		if (pres != -1) b.PutInt(pres);
		if (scum != -1) b.PutInt(scum);
		if (vicePres != -1) b.PutInt(vicePres);
		if (highScum != -1) b.PutInt(highScum);

		b.PutInt(trickNum);
		b.PutInt(trickTurn);
		if (trickTurn != 0) {
			b.PutInt((int)trickRank);
			b.PutULong((trickCount << 1) | (trick1Fewer ? 1u : 0)); // trickCount uses 54 bits
			b.PutULong(trickTotal);
		}
		b.PutInt(passIndex);
	}

	void WritePlayerInfo(ByteWriter b, RoundPlayerInfo p) {
		b.PutCardCount(p.discarded);
		b.PutULong((p.hand.total << 1) | (p.passed ? 1u : 0)); // hand total uses 59 bits
	}

	void WriteDiscInfo(ByteWriter b, RoundDiscInfo d) {
		b.PutInt(d.trickNum);
		b.PutInt(d.trickTurn);
		b.PutULong(d.duration);
		b.PutCardCount(d.discarded);
		b.PutCardCount(d.hand);
		b.PutInt(d.prevRole);
		b.PutInt(d.newRole);
	}

	bool WriteEliminateInfo(ByteWriter b, RoundDiscInfo d, int pn, RoundPlayerInfo p, Player c, bool early, out bool newMove, out bool moveStarted) {
		b.PutULong(d.duration = (ulong)(DateTime.UtcNow - startTime).TotalMilliseconds);
		d.trickNum = trickNum;
		d.trickTurn = trickTurn;

		d.discarded.Copy(p.discarded);
		d.hand.Copy(p.hand);
		b.PutCardCount(d.hand);

		d.prevRole = p.role;

		newMove = moveStarted = false;
		if (early) {
			if (gamePhase == GamePhase.GIVE_CARDS) {
				if (pres == pn) {
					if (scum >= 0) {
						HandleGiveDownCards(pres, scum, 2, 0, 0);
					}
					giveFlags &= ~2;
				} else if (vicePres == pn) {
					if (highScum >= 0) {
						HandleGiveDownCards(vicePres, highScum, 1, 0, 0);
					}
					giveFlags &= ~1;
				}

				if (giveFlags == 0) {
					MoveEnded();
					newMove = moveStarted = true;
				} else if (playerInfo.Count == 2) {
					newMove = true;
				}
			} else {
				if (moveStarted = pn == turnIndex) {
					if (gamePhase == GamePhase.PLAYING_MUST_EQUALIZE && optEqualize == ModeEqualize.CONTINUE_OR_SKIP) {
						turnIndex = NextUnpassed(turnIndex);
					} else {
						NextTurnAfterPass(p);
					}
					gamePhase = GamePhase.PLAYING;
				}

				newMove = true;
			}
		}

		bool insertTop = !early && !penalty;
		var rank = discIndex + (insertTop ? 1 : playerInfo.Count);
		var totalPlayers = playerInfo.Count + discInfo.Count;
		c.AddResult(rank, totalPlayers);

		d.newRole = c.score.roleLast;

		var lastIndex = playerInfo.Count - 1;
		// fix turnIndex
		if (turnIndex > pn) {
			turnIndex--;
		} else if (turnIndex == lastIndex) {
			turnIndex = 0;
		}

		if (early) {
			// fix passIndex
			if (passIndex > pn) {
				passIndex--;
			} else if (passIndex == lastIndex) {
				passIndex = 0;
			}

			FixGiveIndex(ref pres, pn);
			FixGiveIndex(ref scum, pn);
			FixGiveIndex(ref vicePres, pn);
			FixGiveIndex(ref highScum, pn);
		} else {
			passIndex = turnIndex;
		}

		return insertTop;
	}

	void FixGiveIndex(ref int g, int pn) {
		if (g > pn) {
			g--;
		} else if (g == pn) {
			g = -1;
		}
	}

	void SetTurnOrder() {
		foreach (var p in playerInfo) {
			p.role = 0;
		}

		var playersByPriority = playerInfo.OrderBy((pl) => {
			var p = players[pl.owner].priority;
			return p == 0 ? MAX_PLAYERS : p;
		}).ToList();

		rng.Shuffle(playerInfo);

		pres = scum = -1;
		vicePres = highScum = -1;
		if (playerInfo.Count >= 2) {
			var presP = playersByPriority[0];
			var scumP = playersByPriority[playerInfo.Count - 1];

			if (players[presP.owner].priority == 1) {
				// could build map of values to playerInfo index, but scanning playerInfo 4 times is still OK

				// ensure president is dealt first card
				Util.Swap(playerInfo, 0, playerInfo.IndexOf(presP));
				pres = 0;
				presP.role = 2;

				scum = playerInfo.IndexOf(scumP);
				scumP.role = -2;

				if (playerInfo.Count >= 4) {
					presP = playersByPriority[1];
					scumP = playersByPriority[playerInfo.Count - 2];
				}
			}

			if (players[presP.owner].priority == 2) {
				vicePres = playerInfo.IndexOf(presP);
				presP.role = 1;
				highScum = playerInfo.IndexOf(scumP);
				scumP.role = -1;
			}
		}

		passIndex = -1;

		foreach (var p in players) {
			if (p != null) {
				p.priority = 0;
			}
		}
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
		players[playerInfo[0].owner].AddResult(rank, totalPlayers);
	}

	partial void WriteRoundStartInfo3(ByteWriter b) {
		startTime = DateTime.UtcNow;
		revolution = false;
		trickNum = trickTurn = 0;
		trickCount = trickTotal = 0;

		b.PutInt(scum);
		b.PutInt(highScum);
		if (highScum >= 0) {
			b.PutInt(vicePres);
		}

		var D = optDecks;

		// deal cards
		var D4 = D << 2;
		var privateCardCounts = rng.DealCards(new[] { D4, D4, D4, D4, D4, D4, D4, D4, D4, D4, D4, D4, D4, (ulong)optJokers * D }, (52 + (ulong)optJokers) * D, (uint)playerInfo.Count);

		for (var i = 0; i < playerInfo.Count; i++) {
			var p = playerInfo[i];

			p.hand.total = 0;
			foreach (var c in (p.hand.count = privateCardCounts[i])) {
				p.hand.total += c;
			}
			p.discarded.Reset();
			p.passed = false;

			players[p.owner].Send(new ByteWriter()
				.PutType(MsgS2C.PLAYER_PRIVATE_INFO_HAND)
				.PutCardCount(p.hand)
				.ToArray());
		}

		giveFlags = (pres < 0 ? 0 : 2) | (vicePres < 0 ? 0 : 1);

		if (giveFlags == 0) {
			turnIndex = 0;

			var cardNum = rng.NextUInt64(D4);
			ulong lastN3;
			while (cardNum >= (lastN3 = playerInfo[turnIndex].hand.count[(int)CardRank.N3])) {
				turnIndex++;
				cardNum -= lastN3;
			}

			gamePhase = GamePhase.PLAYING_MUST_3;
		} else {
			turnIndex = optFirstTrick switch {
				ModeFirstTrick.SCUM => scum < 0 ? highScum : scum,
				ModeFirstTrick.PRESIDENT => pres < 0 ? vicePres : pres,
				_ => rng.Next(playerInfo.Count),
			};
			gamePhase = GamePhase.GIVE_CARDS;
		}

		b.PutInt(turnIndex);
	}

	void MoveStarted() {
		if (gamePhase == GamePhase.GIVE_CARDS) {
			if (pres >= 0) {
				HandleGiveUpCards(scum, pres, 2);
			}

			if (vicePres >= 0) {
				HandleGiveUpCards(highScum, vicePres, 1);
			}

			if (optMustGiveLowest) {
				TurnEnd();
			}

			return;
		}

		pendingMoveRank = (CardRank)(-1);
		pendingMoveBase = pendingMoveJokers = 0;
	}

	void MoveEnded() {
		if (gamePhase == GamePhase.GIVE_CARDS) {
			// auto give lowest
			if ((giveFlags & 2) != 0 && pres >= 0) {
				HandleGiveDownCards(pres, scum, 2, 0, 0);
			}
			if ((giveFlags & 1) != 0 && vicePres >= 0) {
				HandleGiveDownCards(vicePres, highScum, 1, 0, 0);
			}

			gamePhase = GamePhase.PLAYING;
			return;
		}

		var p = playerInfo[turnIndex];

		var msg = new ByteWriter()
				.PutType(MsgS2C.END_TURN);

		var toElim = -1;

		// sanitize move
		FixMove(p.hand, scum == turnIndex);

		if (pendingMoveRank < 0) {
			// skip/pass

			msg.PutInt(-1);

			if (gamePhase == GamePhase.PLAYING_MUST_EQUALIZE && optEqualize == ModeEqualize.CONTINUE_OR_SKIP) {
				turnIndex = NextUnpassed(turnIndex);
			} else {
				NextTurnAfterPass(p);
			}

			gamePhase = GamePhase.PLAYING;
		} else {
			bool firstTurn = trickTurn == 0;

			var maxRank = revolution ? CardRank.N3 : CardRank.N2;
			var rank = pendingMoveRank;
			var count = firstTurn ? pendingMoveBase + pendingMoveJokers : trickCount;

			if (opt1Fewer2) {
				if (trick1Fewer = (!firstTurn && count > 1 && rank == maxRank)) {
					count--;
				} else if (rank == CardRank.Joker) {
					rank = maxRank;
				}
			}

			bool endTrick = opt8 && rank == CardRank.N8;
			bool forceSkip = false;
			if (count >= 4 && optRev != ModeRevolution.OFF) {
				var revOK =
				  optRev == ModeRevolution.ON_STRICT ? pendingMoveJokers == 0 :
				  optRev == ModeRevolution.ON_RELAXED ? count - pendingMoveJokers >= 4 :
				  optRev == ModeRevolution.ON;
				if (revOK) {
					revolution = !revolution;
					maxRank = revolution ? CardRank.N3 : CardRank.N2;
					if (optRevEndTrick) {
						endTrick = true;
					}
				}
			}

			if (!firstTurn && trickRank == rank) {
				trickTotal += count;
			} else {
				trickTotal = count;
			}

			if (opt4inARow && trickTotal >= 4) {
				endTrick = true;
			}

			gamePhase = GamePhase.PLAYING;

			if (!firstTurn && trickRank == rank) {
				if (p.role == -2 ? optEqualizeEndTrickByScum : optEqualizeEndTrickByOthers) {
					endTrick = true;
				}

				if (!endTrick && optEqualize >= ModeEqualize.CONTINUE_OR_SKIP) {
					if (optEqualize == ModeEqualize.FORCE_SKIP) {
						forceSkip = true;
					} else {
						gamePhase = GamePhase.PLAYING_MUST_EQUALIZE;
					}
				}
			}

			var nextIndex = NextUnpassed(turnIndex);
			if (nextIndex == turnIndex || rank == maxRank && (optEqualize == ModeEqualize.DISALLOW || optEqualizeOnlyScum && scum != nextIndex)) {
				endTrick = true;
			}

			// apply move
			var baseCards = count - pendingMoveJokers;
			p.discarded.AddCards((int)rank, baseCards);
			p.discarded.AddCards((int)CardRank.Joker, pendingMoveJokers);
			p.hand.SubCards((int)rank, baseCards);
			p.hand.SubCards((int)CardRank.Joker, pendingMoveJokers);

			if (p.hand.total == 0) {
				toElim = turnIndex;
				penalty = rank == CardRank.N2 && optPenalizeFinal2 && baseCards != 0 || pendingMoveJokers != 0 && optPenalizeFinalJoker;
			}

			passIndex = -1;
			if (!endTrick) {
				if (optPass == ModePass.SINGLE_TURN && !firstTurn) {
					NextTurnAfterPass(p);
				} else {
					if (optPass == ModePass.PASS_TURN) {
						UnsetPassed();
					}
					turnIndex = nextIndex;

					if (forceSkip) {
						turnIndex = NextUnpassed(turnIndex);
					}
				}
			}

			if (endTrick) {
				NextTrick();
			} else {
				trickRank = rank;
				if (trickTurn++ == 0) {
					trickCount = count;
				}
			}

			msg.PutInt((int)pendingMoveRank);
			msg.PutULong(pendingMoveJokers);
			if (firstTurn) {
				msg.PutULong(pendingMoveBase);
			}
		}

		Broadcast(msg.ToArray());

		if (toElim >= 0) {
			EliminatePlayer(toElim, out bool _);
		}
	}

	void FixMove(CardCount hand, bool isScum) {
		var maxRank = revolution ? CardRank.N3 : CardRank.N2;

		var maxJokers = hand.count[(int)CardRank.Joker];
		// limit to hand
		if (pendingMoveJokers > maxJokers) {
			pendingMoveJokers = maxJokers;
		}

		if (trickTurn == 0) {
			if (gamePhase == GamePhase.PLAYING_MUST_3
				? pendingMoveRank == CardRank.N3
				: pendingMoveRank >= 0 && pendingMoveRank < CardRank.Joker) {
				// limit to hand
				var maxBase = hand.count[(int)pendingMoveRank];
				pendingMoveBase = Math.Min(pendingMoveBase, maxBase);
				if (pendingMoveBase != 0 || gamePhase != GamePhase.PLAYING_MUST_3 && pendingMoveJokers != 0) {
					return;
				}

				if (maxBase != 0) {
					pendingMoveBase = maxBase;
					return;
				}

				if (maxJokers != 0) {
					pendingMoveJokers = 1;
					return;
				}
			}

			if (hand.total == maxJokers) {
				// jokers only, play all as max rank
				pendingMoveRank = maxRank;
				pendingMoveBase = 0;
				pendingMoveJokers = maxJokers;
			} else {
				// play lowest rank in hand
				pendingMoveRank = (CardRank)(revolution
					? Array.FindLastIndex(hand.count, c => c != 0)
					: Array.FindIndex(hand.count, c => c != 0));
				pendingMoveBase = hand.count[(int)pendingMoveRank];
				// use all jokers for final turn
				pendingMoveJokers = pendingMoveBase == hand.total ? maxJokers : 0;
			}
		} else {
			if (pendingMoveRank >= 0) {
				bool canUseJokerFlag = opt1Fewer2 && trickCount != 1; // trickTurn != 0 is satisfied
				bool notMaxRank = !canUseJokerFlag || trickRank != maxRank || trick1Fewer;
				var trickRankMsg = notMaxRank ? trickRank : CardRank.Joker;

				if (pendingMoveRank >= CardRank.Joker && !canUseJokerFlag) {
					pendingMoveRank = maxRank;
				}

				if (CheckRank(pendingMoveRank, trickRankMsg, isScum)) {
					var rank = pendingMoveRank;
					var count = trickCount;
					if (pendingMoveRank >= CardRank.Joker) {
						rank = maxRank;
					} else if (pendingMoveRank == maxRank && canUseJokerFlag && notMaxRank) {
						count--;
					}

					var maxBase = hand.count[(int)rank];

					// check enough cards
					if (maxBase + maxJokers >= count) {
						if (pendingMoveJokers > count) {
							pendingMoveJokers = count;
							pendingMoveBase = 0;
						} else if (pendingMoveJokers + maxBase < count) {
							pendingMoveJokers = count - maxBase;
							pendingMoveBase = maxBase;
						} else {
							pendingMoveBase = count - pendingMoveJokers;
						}
						return;
					}
				}
			}

			pendingMoveRank = (CardRank)(-1);
		}
	}

	bool CheckRank(CardRank a, CardRank trickRankMsg, bool isScum) {
		return a == trickRankMsg
			? optEqualize != ModeEqualize.DISALLOW && (!optEqualizeOnlyScum || isScum)
			: gamePhase != GamePhase.PLAYING_MUST_EQUALIZE && (
				revolution
					? trickRankMsg != CardRank.Joker && (a < trickRankMsg || a == CardRank.Joker)
					: a > trickRankMsg);
	}
}

enum CardRank {
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
	Ace,
	N2,
	Joker,
	NUM,
}

enum GamePhase {
	GIVE_CARDS,
	PLAYING,
	PLAYING_MUST_3,
	PLAYING_MUST_EQUALIZE,
}
