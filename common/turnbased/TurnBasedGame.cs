using Timer = PlayerIO.GameLibrary.Timer;

enum GameState {
	WAITING,
	INTERMISSION,
	ACTIVE,
}

partial class Player {
	public bool ready;
}

partial class Room {
	readonly List<Player> roundCurPlayers = new();
	readonly List<Player> roundQueue = new();

	GameState state;
	DateTime turnEnd;
	Timer turnTimer;

	void ProcessMsgReady(Player player) {
		player.ready = !player.ready;

		Broadcast(new ByteWriter()
			.PutType(MsgS2C.READY)
			.PutInt(player.cn));

		if (player.ready && state == GameState.INTERMISSION && IsAllReady()) {
			RoundStart();
		}
	}

	bool IsAllReady() {
		return roundCurPlayers.All(c => c.ready)
			&& roundQueue.All(c => c.ready);
	}

	void RoundWait() {
		state = GameState.WAITING;
		Broadcast(new ByteWriter().PutType(MsgS2C.ROUND_WAIT));

		turnTimer?.Stop();
		turnTimer = null;
	}

	void RoundIntermission() {
		turnTimer?.Stop();

		foreach (var p in players) {
			if (p == null) continue;
			p.ready = false;
		}

		state = GameState.INTERMISSION;
		turnEnd = DateTime.UtcNow + TimeSpan.FromMilliseconds(INTERMISSION_TIME);
		Broadcast(new ByteWriter().PutType(MsgS2C.ROUND_INTERM));

		turnTimer = ScheduleCallback(RoundStart, INTERMISSION_TIME);
	}

	void RoundStart() {
		lock (players) {
			turnTimer?.Stop();

			state = GameState.ACTIVE;

			roundCurPlayers.AddRange(roundQueue);
			roundQueue.Clear();

			var b = new ByteWriter().PutType(MsgS2C.ROUND_START);
			foreach (var p in roundCurPlayers) {
				b.PutInt(p.cn);
			}
			b.PutInt(-1);
			WriteRoundStartInfo(b);
			Broadcast(b);

			RoundStarted();
		}
	}

	partial void WriteRoundStartInfo(ByteWriter b);

	void RoundFinish() {
		if (numActive >= MIN_PLAYERS_ACTIVE) {
			RoundIntermission();
		} else {
			RoundWait();
		}
	}

	partial void WriteWelcome(ByteWriter w) {
		w.PutInt((int)state);
		if (state == GameState.INTERMISSION || state == GameState.ACTIVE) {
			w.PutInt((int)(turnEnd - DateTime.UtcNow).TotalMilliseconds);
		}

		if (state == GameState.INTERMISSION) {
			foreach (var p in players) {
				if (p?.ready ?? false) {
					w.PutInt(p.cn);
				}
			}
			w.PutInt(-1);
		}

		foreach (var p in roundCurPlayers) {
			w.PutInt(p.cn);
		}
		w.PutInt(-1);

		foreach (var p in roundQueue) {
			w.PutInt(p.cn);
		}
		w.PutInt(-1);

		WriteWelcome2(w);
	}
	partial void WriteWelcome2(ByteWriter w);

	void PlayerActivated2(Player player) {
		if (state == GameState.WAITING && numActive >= MIN_PLAYERS_ACTIVE) {
			RoundIntermission();
		}
		roundQueue.Add(player);
	}

	void PlayerDeactivated2(Player player) {
		if (state == GameState.ACTIVE) {
			CheckLeavingPlayer(player);
		}

		roundCurPlayers.Remove(player);
		roundQueue.Remove(player);

		if (state == GameState.INTERMISSION) {
			if (numActive < MIN_PLAYERS_ACTIVE) {
				RoundWait();
			} else if (IsAllReady()) {
				RoundStart();
			}
		}
	}
}
