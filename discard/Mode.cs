partial class Room {
	int optTurnTime;
	int optDecks;

	const int MIN_PLAYERS_ACTIVE = 2;
	int MAX_PLAYERS_ACTIVE => 15 * optDecks;

	void ParseMode() {
		optTurnTime = this.ParseGameProp("optTurnTime", 20000, 5000, 60000);
		optDecks = this.ParseGameProp("optDecks", 1, 1, 3);
		RoomData.Clear();

		this.WriteGameProp("optTurnTime", optTurnTime);
		this.WriteGameProp("optDecks", optDecks);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.PutInt(optTurnTime);
		w.PutInt(optDecks);
	}
}
