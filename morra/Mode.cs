partial class Room {
	bool optInverted;
	bool optAddRandom;
	int optTeams;
	int MIN_PLAYERS_ACTIVE;

	void ParseMode() {
		optInverted = this.ParseGameProp("optInverted", false);
		optAddRandom = this.ParseGameProp("optAddRandom", false);
		optTeams = this.ParseGameProp("optTeams", 0, 0, 45);
		MIN_PLAYERS_ACTIVE = optTeams == 0 ? 2 : optTeams;
		RoomData.Clear();

		this.WriteGameProp("optInverted", optInverted);
		this.WriteGameProp("optAddRandom", optAddRandom);
		this.WriteGameProp("optTeams", optTeams);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.Add((byte)(
			(optInverted ? (1 << 0) : 0)
			| (optAddRandom ? (1 << 1) : 0)
		));
		w.PutInt(optTeams);
	}
}
