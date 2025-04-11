partial class Room {
	int optTurnTime;
	int optCallTime;
	ulong optDecks;
	ModeCheck optCheck;
	ModeTricks optTricks;
	bool optCountSame;
	bool optCountMore;
	bool optCountLess;
	bool optCountZero;
	bool optRankStartA;
	bool optRankStartO;
	bool optRankStartK;
	bool optRank0;
	bool optRank1u;
	bool optRank1uw;
	bool optRank1d;
	bool optRank1dw;
	bool optRank2u;
	bool optRank2uw;
	bool optRank2d;
	bool optRank2dw;
	bool optRankother;

	const int MIN_PLAYERS_ACTIVE = 2;
	const int MAX_PLAYERS_ACTIVE = 0;

	void ParseMode() {
		optTurnTime = this.ParseGameProp("optTurnTime", 20000, 5000, 60000);
		optCallTime = Math.Min(this.ParseGameProp("optCallTime", 3000, 2000, 10000), optTurnTime);
		optDecks = this.ParseGameProp("optDecks", 1, 1, 1ul << 51);
		optCheck = (ModeCheck)this.ParseGameProp("optCheck", (int)ModeCheck.PUBLIC, 0, (int)ModeCheck.NUM - 1);
		optTricks = (ModeTricks)this.ParseGameProp("optTricks", (int)ModeTricks.FORCED, 0, (int)ModeTricks.NUM - 1);
		optCountSame = this.ParseGameProp("optCountSame", true);
		optCountMore = this.ParseGameProp("optCountMore", true);
		optCountLess = this.ParseGameProp("optCountLess", true);
		optCountZero = this.ParseGameProp("optCountZero", true);
		optRankStartA = this.ParseGameProp("optRankStartA", true);
		optRankStartO = this.ParseGameProp("optRankStartO", true);
		optRankStartK = this.ParseGameProp("optRankStartK", true);
		optRank0 = this.ParseGameProp("optRank0", true);
		optRank1u = this.ParseGameProp("optRank1u", true);
		optRank1uw = this.ParseGameProp("optRank1uw", true);
		optRank1d = this.ParseGameProp("optRank1d", true);
		optRank1dw = this.ParseGameProp("optRank1dw", true);
		optRank2u = this.ParseGameProp("optRank2u", false);
		optRank2uw = this.ParseGameProp("optRank2uw", false);
		optRank2d = this.ParseGameProp("optRank2d", false);
		optRank2dw = this.ParseGameProp("optRank2dw", false);
		optRankother = this.ParseGameProp("optRankother", false);

		if (!(optCountSame || optCountMore || optCountLess)) {
			optCountSame = optCountMore = optCountLess = true;
		}
		if (!(optRankStartA || optRankStartO || optRankStartK)) {
			optRankStartA = optRankStartO = optRankStartK = true;
		}

		RoomData.Clear();

		this.WriteGameProp("optTurnTime", optTurnTime);
		this.WriteGameProp("optCallTime", optCallTime);
		this.WriteGameProp("optDecks", optDecks);
		this.WriteGameProp("optCheck", (int)optCheck);
		this.WriteGameProp("optTricks", (int)optTricks);
		this.WriteGameProp("optCountSame", optCountSame);
		this.WriteGameProp("optCountMore", optCountMore);
		this.WriteGameProp("optCountLess", optCountLess);
		this.WriteGameProp("optCountZero", optCountZero);
		this.WriteGameProp("optRankStartA", optRankStartA);
		this.WriteGameProp("optRankStartO", optRankStartO);
		this.WriteGameProp("optRankStartK", optRankStartK);
		this.WriteGameProp("optRank0", optRank0);
		this.WriteGameProp("optRank1u", optRank1u);
		this.WriteGameProp("optRank1uw", optRank1uw);
		this.WriteGameProp("optRank1d", optRank1d);
		this.WriteGameProp("optRank1dw", optRank1dw);
		this.WriteGameProp("optRank2u", optRank2u);
		this.WriteGameProp("optRank2uw", optRank2uw);
		this.WriteGameProp("optRank2d", optRank2d);
		this.WriteGameProp("optRank2dw", optRank2dw);
		this.WriteGameProp("optRankother", optRankother);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.PutInt(optTurnTime);
		w.PutInt(optCallTime);
		w.PutULong(optDecks);
		w.Add((byte)(
			(byte)optCheck
			| ((byte)optTricks << 2)
			| (optCountSame ? (1 << 4) : 0)
			| (optCountMore ? (1 << 5) : 0)
			| (optCountLess ? (1 << 6) : 0)
			| (optCountZero ? (1 << 7) : 0)
		));
		w.Add((byte)(
			(optRankStartA ? (1 << 0) : 0)
			| (optRankStartO ? (1 << 1) : 0)
			| (optRankStartK ? (1 << 2) : 0)
			| (optRank0 ? (1 << 3) : 0)
			| (optRank1u ? (1 << 4) : 0)
			| (optRank1uw ? (1 << 5) : 0)
			| (optRank1d ? (1 << 6) : 0)
			| (optRank1dw ? (1 << 7) : 0)
		));
		w.Add((byte)(
			(optRank2u ? (1 << 0) : 0)
			| (optRank2uw ? (1 << 1) : 0)
			| (optRank2d ? (1 << 2) : 0)
			| (optRank2dw ? (1 << 3) : 0)
			| (optRankother ? (1 << 4) : 0)
		));
	}
}

enum ModeCheck {
	ARBITER,
	CALLER,
	PUBLIC,
	PUBLIC_ALL,
	NUM,
}

enum ModeTricks {
	FORCED,
	PASS_TURN,
	PASS_TRICK,
	SINGLE_TURN,
	NUM,
}
