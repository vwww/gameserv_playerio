partial class Player {
	public ulong move;
}

partial class Room {
	const int ROUND_TIME = 3000;

	private const ulong MOVE_MASK = 0x003F_FFFF_FFFF_FFFF;
	private const int MAX_MOVE_RND = 2_000_000_000;

	readonly RNG rng = new();

	void ProcessMsgMove(Player player, ByteReader msg) {
		ulong move = msg.GetULong() & MOVE_MASK;
		player.move = move;
		player.Send(new ByteWriter()
				.PutType(MsgS2C.MOVE_CONFIRM)
				.PutULong(move)
				.ToArray());
	}

	void RoundEnded() {
		if (roundCurPlayers.Count == 0) {
			return;
		}

		int teamCount = optTeams == 0 ? roundCurPlayers.Count : optTeams;
		bool addRandom = optAddRandom;
		bool inverted = optInverted;

		int moveRnd = addRandom ? rng.Next(MAX_MOVE_RND) : -1;
		ulong moveSum = addRandom ? (ulong)moveRnd : 0;

		foreach (var p in roundCurPlayers) {
			moveSum += p.move;
		}

		int winner = (int)(moveSum % (ulong)teamCount);

		var teamPlayers = roundCurPlayers.ToArray();
		rng.Shuffle(teamPlayers);

		var winMsg = new ByteWriter()
			.PutType(MsgS2C.END_ROUND)
			.PutInt(teamCount)
			.PutULong(moveSum)
			.PutInt(moveRnd)
			.PutInt(teamPlayers.Length);

		for (int i = 0; i < teamPlayers.Length; i++) {
			var p = teamPlayers[i];
			int team = i % teamCount;
			bool isWinner = (team == winner) ^ inverted;
			if (isWinner) {
				p.score.AddWin();
			} else {
				p.score.AddLoss();
			}
			winMsg.PutInt(p.cn);
			winMsg.PutULong(p.move);
		}

		Broadcast(winMsg.ToArray());
	}
}
