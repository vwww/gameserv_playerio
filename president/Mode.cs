partial class Room {
	int optTurnTime;
	ulong optDecks;
	int optJokers;
	bool optKeepJokers;
	bool optMustGiveLowest;
	ModeRevolution optRev;
	bool optRevEndTrick;
	bool opt1Fewer2;
	ModePass optPass;
	ModeEqualize optEqualize;
	bool optEqualizeEndTrickByScum;
	bool optEqualizeEndTrickByOthers;
	bool optEqualizeOnlyScum;
	bool opt4inARow;
	bool opt8;
	bool optPenalizeFinal2;
	bool optPenalizeFinalJoker;
	ModeFirstTrick optFirstTrick;

	const int MIN_PLAYERS_ACTIVE = 2;
	int MAX_PLAYERS_ACTIVE => optDecks == 1 ? (52 + optJokers) >> 1 : 0;
	// floor((52 + optJokers) * optDecks / 2)
	// scum must be able to give 2 cards
	// but we can only have 45 players anyway

	void ParseMode() {
		optTurnTime = this.ParseGameProp("optTurnTime", 20000, 5000, 60000);
		optDecks = this.ParseGameProp("optDecks", 1, 1, 1ul << 51);
		optJokers = this.ParseGameProp("optJokers", 2, 0, 2);
		optKeepJokers = this.ParseGameProp("optKeepJokers", false);
		optMustGiveLowest = this.ParseGameProp("optMustGiveLowest", false);
		optRev = (ModeRevolution)this.ParseGameProp("optRev", (int)ModeRevolution.ON_RELAXED, 0, (int)ModeRevolution.NUM - 1);
		optRevEndTrick = this.ParseGameProp("optRevEndTrick", false);
		opt1Fewer2 = this.ParseGameProp("opt1Fewer2", true);
		optPass = (ModePass)this.ParseGameProp("optPass", (int)ModePass.PASS_TRICK, 0, (int)ModePass.NUM - 1);
		optEqualize = (ModeEqualize)this.ParseGameProp("optEqualize", (int)ModeEqualize.ALLOW, 0, (int)ModeEqualize.NUM - 1);
		optEqualizeEndTrickByScum = this.ParseGameProp("optEqualizeEndTrickByScum", false);
		optEqualizeEndTrickByOthers = this.ParseGameProp("optEqualizeEndTrickByOthers", false);
		optEqualizeOnlyScum = this.ParseGameProp("optEqualizeOnlyScum", false);
		opt4inARow = this.ParseGameProp("opt4inARow", false);
		opt8 = this.ParseGameProp("opt8", false);
		optPenalizeFinal2 = this.ParseGameProp("optPenalizeFinal2", false);
		optPenalizeFinalJoker = this.ParseGameProp("optPenalizeFinalJoker", false);
		optFirstTrick = (ModeFirstTrick)this.ParseGameProp("optFirstTrick", (int)ModeFirstTrick.SCUM, 0, (int)ModeFirstTrick.NUM - 1);

		RoomData.Clear();

		this.WriteGameProp("optTurnTime", optTurnTime);
		this.WriteGameProp("optDecks", optDecks);
		this.WriteGameProp("optJokers", optJokers);
		this.WriteGameProp("optKeepJokers", optKeepJokers);
		this.WriteGameProp("optMustGiveLowest", optMustGiveLowest);
		this.WriteGameProp("optRev", (int)optRev);
		this.WriteGameProp("optRevEndTrick", optRevEndTrick);
		this.WriteGameProp("opt1Fewer2", opt1Fewer2);
		this.WriteGameProp("optPass", (int)optPass);
		this.WriteGameProp("optEqualize", (int)optEqualize);
		this.WriteGameProp("optEqualizeEndTrickByScum", optEqualizeEndTrickByScum);
		this.WriteGameProp("optEqualizeEndTrickByOthers", optEqualizeEndTrickByOthers);
		this.WriteGameProp("optEqualizeOnlyScum", optEqualizeOnlyScum);
		this.WriteGameProp("opt4inARow", opt4inARow);
		this.WriteGameProp("opt8", opt8);
		this.WriteGameProp("optPenalizeFinal2", optPenalizeFinal2);
		this.WriteGameProp("optPenalizeFinalJoker", optPenalizeFinalJoker);
		this.WriteGameProp("optFirstTrick", (int)optFirstTrick);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.PutInt(optTurnTime);
		w.PutULong(optDecks);
		w.Add((byte)(
			optJokers
			| ((int)optRev << 2)
			| ((int)optPass << 4)
			| ((int)optFirstTrick << 6)
		));
		w.Add((byte)(
			(int)optEqualize
			| (optKeepJokers ? (1 << 3) : 0)
			| (optMustGiveLowest ? (1 << 4) : 0)
			| (optRevEndTrick ? (1 << 5) : 0)
			| (opt1Fewer2 ? (1 << 6) : 0)
			| (optEqualizeEndTrickByScum ? (1 << 7) : 0)
		));
		w.Add((byte)(
			(optEqualizeEndTrickByOthers ? (1 << 0) : 0)
			| (optEqualizeOnlyScum ? (1 << 1) : 0)
			| (opt4inARow ? (1 << 2) : 0)
			| (opt8 ? (1 << 3) : 0)
			| (optPenalizeFinal2 ? (1 << 4) : 0)
			| (optPenalizeFinalJoker ? (1 << 5) : 0)
		));
	}
}

enum ModeRevolution {
	OFF,
	ON_STRICT,
	ON_RELAXED,
	ON,
	NUM,
}

enum ModePass {
	PASS_TURN,
	PASS_TRICK,
	SINGLE_TURN,
	NUM,
}

enum ModeEqualize {
	DISALLOW,
	ALLOW,
	CONTINUE_OR_SKIP,
	CONTINUE_OR_PASS,
	FORCE_SKIP,
	NUM,
}

enum ModeFirstTrick {
	SCUM,
	PRESIDENT,
	RANDOM,
	NUM,
}
