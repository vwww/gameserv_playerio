partial class Room {
	byte optBotBalance;
	byte optSkill;
	byte optBotWin;
	byte optTransfer;
	byte optOverlap;
	uint optDimension;

	double skillWeight, randomWeight;
	double optBotWeight, optBotWeightInv;
	double transferRatio;

	double massStart, massMin, massMax;
	double invDimension;

	double overlapSmall;

	const uint DIMENSION_SCALE = 1_000_000;

	void ParseMode() {
		optBotBalance = (byte)this.ParseGameProp("optBotBalance", 16, 0, MAX_PLAYERS);
		optSkill = (byte)this.ParseGameProp("optSkill", 80, 0, 100);
		optBotWin = (byte)this.ParseGameProp("optBotWin", 90, 0, 100);
		optTransfer = (byte)this.ParseGameProp("optTransfer", 75, 0, 100);
		optOverlap = (byte)this.ParseGameProp("optOverlap", 0, 0, 100);
		optDimension = (uint)this.ParseGameProp("optDimension", 2 * DIMENSION_SCALE, 0, 100 * DIMENSION_SCALE);

		skillWeight = optSkill / 100.0;
		randomWeight = (1 - skillWeight) / 2;

		optBotWeight = optBotWin / 100.0;
		optBotWeightInv = 1 - optBotWeight;

		transferRatio = optTransfer / 100.0;
		var dimension = (double)optDimension / DIMENSION_SCALE;
		invDimension = (double)DIMENSION_SCALE / optDimension;

		overlapSmall = 1 - optOverlap / 50.0;

		massStart = Math.Pow(PL_RAD_START, dimension);
		massMin = Math.Pow(PL_RAD_MIN, dimension);
		massMax = Math.Pow(PL_RAD_MAX, dimension);

		RoomData.Clear();

		this.WriteGameProp("optBotBalance", optBotBalance);
		this.WriteGameProp("optSkill", optSkill);
		this.WriteGameProp("optBotWin", optBotWin);
		this.WriteGameProp("optTransfer", optTransfer);
		this.WriteGameProp("optOverlap", optOverlap);
		this.WriteGameProp("optDimension", optDimension);
		UpdateActiveCount();
	}

	void WriteWelcomeMode(ByteWriter w) {
		w.PutULong(optBotBalance);
		w.Add(optSkill);
		w.Add(optBotWin);
		w.Add(optTransfer);
		w.Add(optOverlap);
		w.PutULong(optDimension);
	}
}
