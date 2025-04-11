partial class RoundPlayerInfo {
	public int owner;
}

partial class RoundDiscInfo {
	public string ownerName;
	public int ownerNum;
}

partial class Room {
	readonly List<RoundPlayerInfo> playerInfo = new();
	readonly List<RoundDiscInfo> discInfo = new();
	int turnIndex;
	int discIndex;

	readonly RNG rng = new();

	partial void WriteWelcome2(ByteWriter b) {
		WriteWelcomeRoundPlayers(b);
		WriteWelcome3(b);
	}
	partial void WriteWelcome3(ByteWriter b);

	partial void WriteRoundStartInfo2(ByteWriter b) {
		SetTurnOrder();

		WriteRoundStartPlayers(b);
		WriteRoundStartInfo3(b);
	}
	partial void WriteRoundStartInfo3(ByteWriter b);

	void WriteWelcomeRoundPlayers(ByteWriter b) {
		// Write player infos
		foreach (var p in playerInfo) {
			b.PutInt(p.owner);
			WritePlayerInfo(b, p);
		}
		b.PutInt(-1);
		if (playerInfo.Count > 0) {
			b.PutInt(turnIndex);
		}

		// Write disc infos
		foreach (var d in discInfo) {
			b.PutString(d.ownerName);
			b.PutInt(d.ownerNum);
			WriteDiscInfo(b, d);
		}
		b.Add(0);
		if (discInfo.Count > 0) {
			b.PutInt(discIndex);
		}
	}

	void WriteRoundStartPlayers(ByteWriter b) {
		foreach (var p in playerInfo) {
			b.PutInt(p.owner);
		}
		b.PutInt(-1);
	}

	void InitPlayers() {
		playerInfo.AddRange(
			roundCurPlayers.Select(c => new RoundPlayerInfo { owner = c.cn })
		);
		turnIndex = 0;
		discIndex = 0;
	}

	void CheckLeavingPlayer(Player player) {
		var i = playerInfo.FindIndex((p) => p.owner == player.cn);
		if (i != -1) {
			if (EliminatePlayer(i, out bool moveStart, true)) {
				turnTimer?.Stop();
				turnTimer = null;

				CheckWin(moveStart);
			}
		}
	}

	void GameOverCleanup() {
		playerInfo.Clear();
		discInfo.Clear();
	}

	void RotatePlayers() {
		if (++turnIndex == playerInfo.Count) {
			turnIndex = 0;
		}
	}

	bool EliminatePlayer(int pn, out bool moveStarted, bool early = false) {
		var p = playerInfo[pn];
		var d = new RoundDiscInfo();
		var c = players[p.owner];

		d.ownerName = c.name;
		d.ownerNum = p.owner;

		var b = new ByteWriter();
		b.PutType(early ? MsgS2C.PLAYER_ELIMINATE_EARLY : MsgS2C.PLAYER_ELIMINATE);
		b.PutInt(pn);
		bool insertTop = WriteEliminateInfo(b, d, pn, p, c, early, out bool newMove, out moveStarted);
		Broadcast(b.ToArray());

		playerInfo.RemoveAt(pn);
		discInfo.Insert(discIndex, d);

		if (insertTop) {
			discIndex++;
		}

		return newMove;
	}
}
