partial class Room {
	int optTurnTime;
	bool optSpeed;
	bool optInverted;
	ulong optDecks;
	bool optDealerHitSoft;
	ModeDealer optDealer;
	bool opt21;
	ModeDouble optDouble;
	bool optSplitDouble;
	bool optSplitSurrender;
	bool optHitSurrender;
	ModeSurrender optSurrender;
	byte optSplitNonAce;
	byte optSplitAce;
	bool optSplitAceAdd;
	bool optInsurePartial;
	bool optInsureLate;

	const int MIN_PLAYERS_ACTIVE = 1;
	const int MAX_PLAYERS_ACTIVE = 0;

	void ParseMode() {
		optTurnTime = this.ParseGameProp("optTurnTime", 20000, 5000, 60000);
		optSpeed = this.ParseGameProp("optSpeed", true);
		optInverted = this.ParseGameProp("optInverted", false);
		optDecks = this.ParseGameProp("optDecks", 1, 0, 1ul << 51);
		optDealerHitSoft = this.ParseGameProp("optDealerHitSoft", true);
		optDealer = (ModeDealer)this.ParseGameProp("optDealer", (int)ModeDealer.HOLE1, 0, (int)ModeDealer.NUM - 1);
		opt21 = this.ParseGameProp("opt21", false);
		if (!opt21) {
			optDouble = (ModeDouble)this.ParseGameProp("optDouble", (int)ModeDouble.ANY, 0, (int)ModeDouble.NUM - 1);
			optSplitDouble = this.ParseGameProp("optSplitDouble", true);
			optSplitSurrender = this.ParseGameProp("optSplitSurrender", false);
			optHitSurrender = this.ParseGameProp("optHitSurrender", false);
			optSurrender = (ModeSurrender)this.ParseGameProp("optSurrender", (int)ModeSurrender.ANY, 0, (int)ModeSurrender.NUM - 1);
			optSplitNonAce = (byte)this.ParseGameProp("optSplitNonAce", 3, 0, 254);
			optSplitAce = (byte)this.ParseGameProp("optSplitAce", 1, 0, 254);
			optSplitAceAdd = this.ParseGameProp("optSplitAceAdd", true);
			optInsurePartial = this.ParseGameProp("optInsurePartial", true);
			optInsureLate = this.ParseGameProp("optInsureLate", false);

			if (optDealer >= ModeDealer.HOLE0) {
				optInsureLate = false;

				if (optDealer == ModeDealer.HOLE1 && (opt21 || optSurrender == ModeSurrender.OFF)) {
					optDealer = ModeDealer.HOLE0;
				}
			}
		}

		RefillShoe();

		RoomData.Clear();

		this.WriteGameProp("optTurnTime", optTurnTime);
		this.WriteGameProp("optSpeed", optSpeed);
		this.WriteGameProp("optInverted", optInverted);
		this.WriteGameProp("optDecks", optDecks);
		this.WriteGameProp("optDealerHitSoft", optDealerHitSoft);
		this.WriteGameProp("optDealer", (int)optDealer);
		this.WriteGameProp("opt21", opt21);
		this.WriteGameProp("optDouble", (int)optDouble);
		this.WriteGameProp("optSplitDouble", optSplitDouble);
		this.WriteGameProp("optSplitSurrender", optSplitSurrender);
		this.WriteGameProp("optHitSurrender", optHitSurrender);
		this.WriteGameProp("optSurrender", (int)optSurrender);
		this.WriteGameProp("optSplitNonAce", optSplitNonAce);
		this.WriteGameProp("optSplitAce", optSplitAce);
		this.WriteGameProp("optSplitAceAdd", optSplitAceAdd);
		this.WriteGameProp("optInsurePartial", optInsurePartial);
		this.WriteGameProp("optInsureLate", optInsureLate);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.PutInt(optTurnTime);
		w.PutULong(optDecks);
		w.Add((byte)(
			(optSpeed ? (1 << 0) : 0)
			| (optInverted ? (1 << 1) : 0)
			| (opt21 ? (1 << 2) : 0)
			| (optDealerHitSoft ? (1 << 3) : 0)
			| ((int)optDealer << 4)
			| (optInsurePartial ? (1 << 6) : 0)
			| (optInsureLate ? (1 << 7) : 0)
		));
		if (!opt21) {
			w.Add(optSplitNonAce);
			w.Add(optSplitAce);
			w.Add((byte)(
				(int)optDouble
				| ((int)optSurrender << 2)
				| (optSplitDouble ? (1 << 4) : 0)
				| (optSplitSurrender ? (1 << 5) : 0)
				| (optHitSurrender ? (1 << 6) : 0)
				| (optSplitAceAdd ? (1 << 7) : 0)
			));
		}
	}
}

enum ModeDealer {
	NO_HOLE,
	HOLE_NO_PEEK,
	HOLE0,
	HOLE1,
	NUM,
}

enum ModeDouble {
	ANY,
	ON_9_10_11,
	ON_10_11,
	NUM,
}

enum ModeSurrender {
	OFF,
	NOT_ACE,
	ANY,
	NUM,
}
