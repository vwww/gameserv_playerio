partial class Room {
	bool optIndependent;
	int optTeams;
	int MIN_PLAYERS_ACTIVE;

	void ParseMode() {
		optIndependent = this.ParseGameProp("optIndependent", false);
		optTeams = this.ParseGameProp("optTeams", 0, 0, 45);
		MIN_PLAYERS_ACTIVE = optTeams == 0 ? optIndependent ? 1 : 2 : optTeams;
		RoomData.Clear();

		this.WriteGameProp("optIndependent", optIndependent);
		this.WriteGameProp("optTeams", optTeams);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.PutInt((optIndependent ? 1 : 0) | (optTeams << 1));
	}
}
