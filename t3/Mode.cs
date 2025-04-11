partial class Room {
	int optTurnTime;
	bool optInverted;
	bool optChecked;
	bool optQuick;

	void ParseMode() {
		optTurnTime = this.ParseGameProp("optTurnTime", 10000, 1500, 20000);
		optInverted = this.ParseGameProp("optInverted", false);
		optChecked = this.ParseGameProp("optChecked", false);
		optQuick = this.ParseGameProp("optQuick", false);
		RoomData.Clear();

		this.WriteGameProp("optTurnTime", optTurnTime);
		this.WriteGameProp("optInverted", optInverted);
		this.WriteGameProp("optChecked", optChecked);
		this.WriteGameProp("optQuick", optQuick);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.PutInt(optTurnTime);
		w.Add((byte)(
			(optInverted ? (1 << 0) : 0)
			| (optChecked ? (1 << 1) : 0)
			| (optQuick ? (1 << 2) : 0)
		));
	}
}
