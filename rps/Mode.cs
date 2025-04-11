partial class Room {
	bool optClassic;
	bool optInverted;
	bool optCount;
	int optRoundTime;
	long optBotBalance;
	int MIN_PLAYERS_ACTIVE;

	void ParseMode() {
		optClassic = this.ParseGameProp("optClassic", false);
		optInverted = this.ParseGameProp("optInverted", false);
		optCount = this.ParseGameProp("optCount", false);
		optRoundTime = this.ParseGameProp("optRoundTime", 5000, 3000, 30000);
		optBotBalance = this.ParseGameProp("optBotBalance", 0, -1L << 53, 1L << 53);
		MIN_PLAYERS_ACTIVE = optBotBalance == 0 || optBotBalance == -1 ? 2 : 1;
		RoomData.Clear();

		this.WriteGameProp("optClassic", optClassic);
		this.WriteGameProp("optInverted", optInverted);
		this.WriteGameProp("optCount", optCount);
		this.WriteGameProp("optRoundTime", optRoundTime);
		this.WriteGameProp("optBotBalance", optBotBalance);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.Add((byte)(
			(optClassic ? (1 << 0) : 0)
			| (optInverted ? (1 << 1) : 0)
			| (optCount ? (1 << 2) : 0)
		));
		w.PutInt(optRoundTime);
		w.PutLong(optBotBalance);
	}
}
