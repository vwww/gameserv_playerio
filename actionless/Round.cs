partial class Room {
	const int ROUND_TIME = 3000;

	readonly RNG rng = new();

	void RoundEnded() {
		if (roundCurPlayers.Count == 0) {
			return;
		}

		Player[] teamPlayers = roundCurPlayers.ToArray();
		rng.Shuffle(teamPlayers);

		var winCount = optTeams > 0 ? optTeams : roundCurPlayers.Count;
		var win = new bool[winCount];
		if (optIndependent) {
			for (int i = 0; i < win.Length; i++) {
				win[i] = rng.NextBool();
			}
		} else {
			win[rng.Next(win.Length)] = true;
		}

		var winMsg = new ByteWriter()
			.PutType(MsgS2C.END_ROUND)
			.PutInt(winCount);

		// pack results into bytes
		{
			int i = 0;
			byte b = 0;

			foreach (var winResult in win) {
				if (winResult) {
					b |= (byte)(1 << i);
				}
				i = (i + 1) & 7;

				if (i == 0) {
					winMsg.Add(b);
					b = 0;
				}
			}

			if (i != 0) {
				winMsg.Add(b);
			}
		}
		winMsg.PutInt(teamPlayers.Length);
		for (int i = 0; i < teamPlayers.Length; i++) {
			var p = teamPlayers[i];
			bool isWinner = win[optTeams > 0 ? (i % optTeams) : i];
			if (isWinner) {
				p.score.AddWin();
			} else {
				p.score.AddLoss();
			}
			winMsg.PutInt(p.cn);
		}

		Broadcast(winMsg);
	}
}
