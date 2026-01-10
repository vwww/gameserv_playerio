partial class Room {
	int optTurnTime;
	ushort optScoreTarget;

	bool optSkipEmpty;
	bool optSkipPass;
	bool optSkipOnlyMove;
	bool optPre;
	bool optPost;

	const int MIN_PLAYERS_ACTIVE = 2;
	const int MAX_PLAYERS_ACTIVE = 4;

	void ParseMode() {
		optTurnTime = this.ParseGameProp("optTurnTime", 20000, 5000, 60000);
		optScoreTarget = (ushort)this.ParseGameProp("optScoreTarget", 121, 1, 10000);
		optSkipEmpty = this.ParseGameProp("optSkipEmpty", true);
		optSkipPass = this.ParseGameProp("optSkipPass", true);
		optSkipOnlyMove = this.ParseGameProp("optSkipOnlyMove", false);
		optPre = this.ParseGameProp("optPre", false);
		optPost = this.ParseGameProp("optPost", true);

		RoomData.Clear();

		this.WriteGameProp("optTurnTime", optTurnTime);
		this.WriteGameProp("optScoreTarget", optScoreTarget);
		this.WriteGameProp("optSkipEmpty", optSkipEmpty);
		this.WriteGameProp("optSkipPass", optSkipPass);
		this.WriteGameProp("optSkipOnlyMove", optSkipOnlyMove);
		this.WriteGameProp("optPre", optPre);
		this.WriteGameProp("optPost", optPost);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.PutULong(((ulong)optTurnTime << 5)
			| (optSkipEmpty ? (1 << 0) : 0u)
			| (optSkipPass ? (1 << 1) : 0u)
			| (optSkipOnlyMove ? (1 << 2) : 0u)
			| (optPre ? (1 << 3) : 0u)
			| (optPost ? (1 << 4) : 0u));
		w.PutULong(optScoreTarget - 1u);
	}
}
