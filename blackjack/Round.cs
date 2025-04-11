partial class RoundPlayerInfo {
	public List<HandBet> hands = new();
	public int handIndex;
	public ulong bet;
	public ulong insurance;
	public bool ready;
}

partial class RoundDiscInfo {
	public HandBet[] hands;
	public ulong insurance;
	public long score, scoreChange;
	public bool dealerCanBJ;
}

partial class Room {
	int ROUND_TIME => optTurnTime;

	const long MAX_BALANCE = 9_000_000_000_000_000;
	const long MIN_BALANCE = -MAX_BALANCE;

	DateTime startTime;

	GamePhase gamePhase;
	bool extendMove;

	Hand dealerHand = new();
	readonly CardCount cardCountHole = new((int)CardValue.NUM);
	bool cardCountShoeHasHole;
	readonly CardCount cardCountShoeClient = new((int)CardValue.NUM);
	readonly CardCount cardCountShoeActual = new((int)CardValue.NUM);

	void ProcessMsgMoveBet(Player player, ByteReader msg) {
		var amount = msg.GetULong();

		if (state != GameState.ACTIVE || gamePhase != GamePhase.BET) return;

		var pn = playerInfo.FindIndex(p => p.owner == player.cn);
		if (pn == -1) return;

		var p = playerInfo[pn];

		var betMax = player.balance < -100 ? 100
			: player.balance < (MAX_BALANCE - 200) / 2
				? player.balance + 200
				: MAX_BALANCE - player.balance;
		amount = Math.Max(Math.Min(amount, (ulong)betMax), 2);
		amount ^= amount & 1; // must be multiple of 2

		p.bet = amount;

		Broadcast(new ByteWriter()
			.PutType(MsgS2C.END_TURN_AMOUNT)
			.PutInt(pn)
			.PutULong(amount)
			.ToArray());
	}

	void ProcessMsgMoveInsurance(Player player, ByteReader msg) {
		var amount = msg.GetULong();

		if (state != GameState.ACTIVE || gamePhase != (optInsureLate ? GamePhase.POST : GamePhase.PRE)) return;

		var pn = playerInfo.FindIndex(p => p.owner == player.cn);
		if (pn == -1) return;

		var p = playerInfo[pn];

		var maxInsurancePerHand = p.bet >> 1;
		amount = Math.Min(amount, (ulong)p.hands.Count(h => h.bet >= 0) * maxInsurancePerHand);
		if (!optInsurePartial) {
			// enforce multiple
			amount = amount / maxInsurancePerHand * maxInsurancePerHand;
		}

		p.insurance = amount;

		Broadcast(new ByteWriter()
			.PutType(MsgS2C.END_TURN_AMOUNT)
			.PutInt(pn)
			.PutULong(amount)
			.ToArray());
	}

	void ProcessMsgMoveReady(Player player) {
		if (state != GameState.ACTIVE || gamePhase == GamePhase.PLAY) return;

		var pn = playerInfo.FindIndex(p => p.owner == player.cn);
		if (pn == -1) return;

		Broadcast(new ByteWriter()
			.PutType(MsgS2C.END_TURN_READY)
			.PutInt(pn)
			.ToArray());

		var p = playerInfo[pn];

		p.ready = true;
		if (playerInfo.All(p => p.ready)) {
			TurnEnd();
		}
	}

	void ProcessMsgMove(Player player, ByteReader msg) {
		var move = msg.Get();

		if (state != GameState.ACTIVE) return;

		var pn = optSpeed
			? playerInfo.FindIndex(p => p.owner == player.cn)
			: playerInfo[turnIndex].owner == player.cn
				? turnIndex
				: -1;
		// player not found
		if (pn == -1) return;

		var p = playerInfo[pn];

		if (gamePhase != GamePhase.PLAY) {
			if (gamePhase == GamePhase.PRE && (TurnMove)move == TurnMove.SURRENDER
				&& p.handIndex == 0
				&& (optSurrender == ModeSurrender.ANY || optSurrender == ModeSurrender.NOT_ACE && dealerHand.cards.Last() != CardValue.Ace)) {
				// surrender in pre-phase
				Broadcast(new ByteWriter()
					.PutType(MsgS2C.END_TURN)
					.PutInt(pn)
					// move is implied as SURRENDER during PRE phase
					.ToArray());

				var handBetSurrender = p.hands[p.handIndex];
				handBetSurrender.bet = -handBetSurrender.bet;

				p.handIndex++;

				p.ready = true;
				if (playerInfo.All(p => p.ready)) {
					TurnEnd();
				}
			}
			return;
		}

		// no hands left to play
		if (p.handIndex == p.hands.Count) return;

		var handBet = p.hands[p.handIndex];
		var hand = handBet.hand;

		var m = new ByteWriter()
			.PutType(MsgS2C.END_TURN);

		if (optSpeed) {
			m.PutInt(pn);
		}

		m.Add(move);

		bool handFinished;
		switch ((TurnMove)move) {
			case TurnMove.DOUBLE: {
				if (opt21 ||
					hand.cards.Count > 2 ||
					p.hands.Count > 1 && (!optSplitDouble || !optSplitAceAdd && hand.cards[0] == CardValue.Ace) ||
					optDouble != ModeDouble.ANY && (hand.valueHard < (optDouble == ModeDouble.ON_10_11 ? 10 : 9) || hand.valueHard > 11)) return;

				handBet.bet <<= 1;
				var card = DrawCard();
				m.Add((byte)card);

				hand.Add(card);
				handFinished = true;
				break;
			}

			case TurnMove.HIT: {
				if (!optSplitAceAdd && p.hands.Count > 1 && hand.cards[0] == CardValue.Ace) return;

				var card = DrawCard();
				m.Add((byte)card);

				hand.Add(card);
				handFinished = hand.value >= 21;
				break;
			}
			case TurnMove.SPLIT: {
				if (opt21 ||
					hand.cards.Count > 2 ||
					hand.cards[0] != hand.cards[1] ||
					p.hands.Count > (hand.cards[0] == CardValue.Ace ? optSplitAce : optSplitNonAce)) return;

				var card0 = DrawCard();
				var card1 = DrawCard();
				m.Add((byte)card0);
				m.Add((byte)card1);

				p.hands.Insert(p.handIndex + 1, handBet.Split(card0, card1));

				handFinished = hand.value >= 21;
				break;
			}
			case TurnMove.SURRENDER:
				if (opt21 ||
					!optHitSurrender && hand.cards.Count > 2 ||
					optSurrender == ModeSurrender.OFF ||
					optSurrender == ModeSurrender.NOT_ACE && dealerHand.cards.Last() == CardValue.Ace ||
					p.hands.Count > 1 && !optSplitSurrender) return;

				handBet.bet = -handBet.bet;
				handFinished = true;
				break;
			case TurnMove.STAND: // always allowed
				handFinished = true;
				break;
			default:
				return;
		}
		Broadcast(m.ToArray());

		extendMove = true;

		if (handFinished) {
			while (++p.handIndex < p.hands.Count
				&& (p.hands[p.handIndex].hand.value >= 21 || p.hands[p.handIndex].bet < 0));

			if (optSpeed) {
				extendMove = playerInfo.Any(p => p.handIndex < p.hands.Count);
			} else {
				// skip surrendered/finished players
				while (playerInfo[turnIndex].handIndex == playerInfo[turnIndex].hands.Count) {
					if (++turnIndex == playerInfo.Count) {
						turnIndex = 0;
						extendMove = false;
						break;
					}
				}
			}
		}

		TurnEnd();
	}

	bool CanMakeMove(Player _player) {
		// don't let players end the turn
		return false;
	}

	partial void WriteWelcome3(ByteWriter b) {
		b.PutCardCount(cardCountShoeClient);

		if (state != GameState.ACTIVE) return;

		b.PutInt((int)gamePhase);

		if (gamePhase == GamePhase.BET) return;

		byte dealerFlags = (byte)((byte)dealerHand.cards.Last() | (byte)(cardCountShoeHasHole ? (1 << 4) : 0));
		b.Add(dealerFlags);
		if (optDecks != 0 && optDealer != ModeDealer.NO_HOLE) {
			b.PutCardCount(cardCountHole);
		}
	}

	void WritePlayerInfo(ByteWriter b, RoundPlayerInfo p) {
		if (state != GameState.ACTIVE) return;

		b.PutHandBets(p.hands);
		b.PutInt(p.handIndex);
		b.PutULong((p.ready ? 1u : 0) | ((gamePhase == GamePhase.BET ? p.bet : p.insurance) << 1));
	}

	void WriteDiscInfo(ByteWriter b, RoundDiscInfo d) {
		b.PutULong((d.insurance << 1) | (d.dealerCanBJ ? 1u : 0));
		b.PutHandBets(d.hands);
		b.PutLong(d.score);
		b.PutLong(d.scoreChange);
	}

	bool WriteEliminateInfo(ByteWriter _b, RoundDiscInfo d, int pn, RoundPlayerInfo p, Player c, bool _early, out bool newMove, out bool moveStarted) {
		newMove = moveStarted = false;
		if (gamePhase == GamePhase.PLAY
				? optSpeed
					? playerInfo.All(x => x == p || x.handIndex == x.hands.Count)
					: pn == turnIndex
				: playerInfo.All(x => x == p || x.ready)) {
			if (gamePhase != GamePhase.BET || playerInfo.Count > 1) {
				MoveEnded();
			}
			newMove = moveStarted = true;
		}

		d.hands = p.hands.ToArray();
		d.insurance = p.insurance;

		if (gamePhase != GamePhase.END) {
			long scoreChange = -(long)p.insurance;
			ResolveHands(p.hands, 21, d.dealerCanBJ = DealerCanBJ(), c, ref scoreChange);
			if (optInverted) {
				scoreChange = -scoreChange;
			}
			c.balance = MathUtil.Clamp(c.balance + scoreChange, MIN_BALANCE, MAX_BALANCE);

			d.score = c.balance;
			d.scoreChange = scoreChange;
		}

		var lastIndex = playerInfo.Count - 1;
		// fix turnIndex
		if (turnIndex > pn) {
			turnIndex--;
		} else if (turnIndex == lastIndex) {
			turnIndex = 0;
		}

		return true;
	}

	void SetTurnOrder() {
		rng.Shuffle(playerInfo);
	}

	bool IsGameOver() => playerInfo.Count < 1 || gamePhase == GamePhase.END;

	void GameOver() {
		if (gamePhase != GamePhase.END) {
			EndPhase();
		}

		foreach (var p in playerInfo) {
			p.hands.Clear();
			p.handIndex = 0;
		}
	}

	partial void WriteRoundStartInfo3(ByteWriter b) {
		startTime = DateTime.UtcNow;
		// turnIndex = 0;

		gamePhase = GamePhase.BET;
		dealerHand = new Hand();
		cardCountShoeHasHole = false;

		for (var i = 0; i < playerInfo.Count; i++) {
			var p = playerInfo[i];

			// p.hands.Clear();
			// p.handIndex = 0;
			p.bet = Math.Max(2, (ulong)Math.Min(MAX_BALANCE - players[p.owner].balance, 100));
			p.insurance = 0;
			p.ready = false;
		}
	}

	void MoveStarted() {
		extendMove = false;
	}

	void MoveEnded() {
		if (extendMove) {
			return;
		}

		if (gamePhase == GamePhase.PLAY && !optSpeed) {
			var p = playerInfo[turnIndex];
			if (p.handIndex != p.hands.Count) {
				// auto stand one move instead of ending phase
				var m = new ByteWriter()
					.PutType(MsgS2C.END_TURN);
				m.Add((byte)TurnMove.STAND);
				Broadcast(m.ToArray());

				while (++p.handIndex != p.hands.Count) {
					if (p.hands[p.handIndex].hand.value < 21 && p.hands[p.handIndex].bet >= 0) {
						return;
					}
				}

				// skip surrendered/finished players
				while (++turnIndex != playerInfo.Count) {
					p = playerInfo[turnIndex];
					if (p.handIndex != p.hands.Count) {
						return;
					}
				}

				turnIndex = 0;
			}
		}

		// either all stand, or bets and insurance should already be valid
		EndPhase();
	}

	void EndPhase() {
		var msg = new ByteWriter().PutType(MsgS2C.END_ROUND);
		switch (gamePhase) {
			case GamePhase.BET:
				// deal cards: players, hole/face-up card, players, face-up card
				foreach (var p in playerInfo) {
					var handbet = new HandBet();
					handbet.hand.Add(DrawCard());
					handbet.bet = (long)p.bet;
					p.hands.Add(handbet);
					p.ready = false;
				}
				dealerHand.Add(DrawCard(optDealer != ModeDealer.NO_HOLE));
				foreach (var p in playerInfo) {
					var hand = p.hands[0].hand;
					hand.Add(DrawCard());
					p.handIndex = hand.value >= 21 ? 1 : 0;
				}
				if (optDealer != ModeDealer.NO_HOLE) {
					dealerHand.Add(DrawCard());
				}

				var dealerFaceUp = dealerHand.cards.Last();

				foreach (var p in playerInfo) {
					msg.Add((byte)((int)p.hands[0].hand.cards[0] | ((byte)p.hands[0].hand.cards[1] << 4)));
				}
				msg.Add((byte)dealerFaceUp);

				// fast end
				if (playerInfo.Count == 0) {
					goto default;
				}

				// skip finished players
				while (playerInfo[turnIndex].handIndex != 0) {
					if (++turnIndex == playerInfo.Count) {
						goto default;
					}
				}

				// peek early (late surrender, no insurance) check
				if (optDealer == ModeDealer.HOLE0 && (dealerFaceUp == CardValue.Tens || dealerFaceUp == CardValue.Ace && opt21)) {
					if (dealerHand.value == 21) {
						msg.Add(1);
						goto default;
					} else {
						msg.Add(0);
						if (cardCountShoeHasHole) {
							var impossible = dealerFaceUp == CardValue.Ace ? (int)CardValue.Tens : (int)CardValue.Ace;
							cardCountHole.SubCards(impossible, cardCountHole.count[impossible]);
						}
					}
				}

				gamePhase = !opt21 && (
					!optInsureLate && dealerFaceUp == CardValue.Ace
					|| optDealer == ModeDealer.HOLE1 && dealerFaceUp == CardValue.Tens)
						? GamePhase.PRE : GamePhase.PLAY;
				break;
			case GamePhase.PRE:
				// fast end
				if (playerInfo.Count == 0) {
					goto default;
				}

				// skip surrendered players
				while (playerInfo[turnIndex].handIndex != 0) {
					if (++turnIndex == playerInfo.Count) {
						goto default;
					}
				}

				// peek late (early surrender) check
				dealerFaceUp = dealerHand.cards.Last();
				if (optDealer >= ModeDealer.HOLE0 && dealerFaceUp == CardValue.Ace ||
					optDealer == ModeDealer.HOLE1 && dealerFaceUp == CardValue.Tens) {
					if (dealerHand.value == 21) {
						msg.Add(1);
						goto default;
					} else {
						msg.Add(0);
						if (cardCountShoeHasHole) {
							var impossible = dealerFaceUp == CardValue.Ace ? (int)CardValue.Tens : (int)CardValue.Ace;
							cardCountHole.SubCards(impossible, cardCountHole.count[impossible]);
						}
					}
				}

				gamePhase = GamePhase.PLAY;
				break;
			case GamePhase.PLAY:
				// fast end
				if (playerInfo.Count == 0) {
					goto default;
				}

				if (optInsureLate && dealerHand.cards.Last() == CardValue.Ace) {
					gamePhase = GamePhase.POST;
					foreach (var p in playerInfo) {
						// p.ready = false;
						p.handIndex = p.hands.Count;
					}
					break;
				}
				goto default;
			/*
			case GamePhase.POST:
				goto default;
			*/
			default:
				gamePhase = GamePhase.END;

				turnIndex = 0;

				var duration = (ulong)(DateTime.UtcNow - startTime).TotalMilliseconds;
				msg.PutULong(duration);

				if (optDealer != ModeDealer.NO_HOLE) {
					if (cardCountShoeHasHole) {
						cardCountShoeHasHole = false;
						cardCountShoeClient.Copy(cardCountShoeActual);
					}
					msg.Add((byte)dealerHand.cards[0]);
				}

				if (playerInfo.Any(p => p.hands.Any(h => h.bet > 0 && h.hand.value <= 21 && !h.hand.IsNaturalBlackjack(p.hands.Count > 1)))) {
					while (DealerShouldHit()) {
						var card = DrawCard();
						dealerHand.Add(card);
						msg.Add((byte)card);
					}
				}

				var dealerBJ = dealerHand.IsNaturalBlackjack(false);

				foreach (var p in playerInfo) {
					var c = players[p.owner];

					long scoreChange = (dealerBJ ? (long)(p.insurance << 1) : -(long)p.insurance);
					ResolveHands(p.hands, dealerHand.value, dealerBJ, c, ref scoreChange);
					if (optInverted) {
						scoreChange = -scoreChange;
					}
					c.balance = MathUtil.Clamp(c.balance + scoreChange, MIN_BALANCE, MAX_BALANCE);
				}
				break;
		}

		Broadcast(msg.ToArray());
	}

	void ResolveHands(ICollection<HandBet> hands, byte dealerValue, bool dealerBJ, Player c, ref long scoreChange) {
		foreach (var h in hands) {
			if (h.bet < 0) {
				// surrendered
				scoreChange += h.bet >> 1;
				c.score.AddLoss();
			} else if (h.hand.value > 21) {
				// bust
				scoreChange -= h.bet;
				c.score.AddLoss();
			} else if (h.hand.IsNaturalBlackjack(hands.Count > 1)) {
				if (dealerBJ) {
					// push blackjack
					c.score.AddTie();
				} else {
					// natural blackjack
					scoreChange += h.bet + (h.bet >> 1);
					c.score.AddWin();
				}
			} else if (!dealerBJ && h.hand.value == dealerValue) {
				// push
				c.score.AddTie();
			} else if (dealerValue > 21 || h.hand.value > dealerValue) {
				// win
				scoreChange += h.bet;
				c.score.AddWin();
			} else {
				// lose
				scoreChange -= h.bet;
				c.score.AddLoss();
			}
		}
	}

	CardValue DrawCard(bool hole = false) {
		var cardNum = rng.NextUInt64(cardCountShoeActual.total);
		byte i = 0;
		while (cardNum >= cardCountShoeActual.count[i]) {
			cardNum -= cardCountShoeActual.count[i];
			i++;
		}

		if (optDecks != 0) {
			if (hole) {
				cardCountShoeHasHole = true;
				cardCountHole.Copy(cardCountShoeClient);
			} else {
				cardCountShoeClient.SubCards(i, 1);

				if (cardCountShoeHasHole && cardCountHole.count[i] > cardCountShoeClient.count[i]) {
					cardCountHole.SubCards(i, cardCountHole.count[i] - cardCountShoeClient.count[i]);
				}
			}

			CheckHoleCardClient();

			cardCountShoeActual.SubCards(i, 1);
			if (cardCountShoeActual.total == 0) {
				RefillShoe();
			}
		}

		return (CardValue)i;
	}

	void CheckHoleCardClient() {
		if (!cardCountShoeHasHole) return;

		var holeCard = Array.FindIndex(cardCountHole.count, (v) => v == cardCountHole.total);
		if (holeCard < 0) return;

		cardCountShoeHasHole = false;

		cardCountHole.count[holeCard] = cardCountHole.total = 1;

		cardCountShoeClient.SubCards(holeCard, 1);
	}

	void RefillShoe() {
		cardCountShoeHasHole = false;

		var n = optDecks != 0 ? optDecks << 2 : 1;

		for (int i = 0; i < 9; i++) {
			cardCountShoeClient.count[i] = cardCountShoeActual.count[i] = n;
		}
		cardCountShoeClient.count[9] = cardCountShoeActual.count[9] = n << 2;

		cardCountShoeClient.total = cardCountShoeActual.total = 13 * n;
	}

	bool DealerShouldHit() {
		return dealerHand.value < 17 || optDealerHitSoft && dealerHand.value == 17 && dealerHand.isSoft;
	}

	bool DealerCanBJ() {
		if (gamePhase == GamePhase.BET) {
			return false;
		}

		var dealerFaceUp = dealerHand.cards.Last();
		return (dealerFaceUp == CardValue.Ace || dealerFaceUp == CardValue.Tens)
			&& (optDealer < ModeDealer.HOLE0 ||
				!opt21
				&& gamePhase == GamePhase.PRE
				&& (optDealer == ModeDealer.HOLE1 || dealerFaceUp == CardValue.Ace));
	}
}

enum CardValue : byte {
	Ace,
	N2,
	N3,
	N4,
	N5,
	N6,
	N7,
	N8,
	N9,
	Tens,
	NUM,
}

enum GamePhase {
	BET,
	PRE,
	PLAY,
	POST,
	END,
}

enum TurnMove {
	HIT,
	STAND,
	DOUBLE,
	SPLIT,
	SURRENDER,
	NUM,
}
