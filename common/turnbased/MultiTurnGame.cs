partial class Room {
	const int INTERMISSION_TIME = 30000;

	partial void WriteRoundStartInfo(ByteWriter b) {
		InitPlayers();

		WriteRoundStartInfo2(b);
	}
	partial void WriteRoundStartInfo2(ByteWriter b);

	void ProcessMsgMoveEnd(Player player) {
		if (state == GameState.ACTIVE && CanMakeMove(player)) {
			TurnEnd();
		}
	}

	void RoundStarted() => CheckWin(true);

	void TurnEnd() {
		lock (players) {
			turnTimer?.Stop();
			turnTimer = null;

			MoveEnded();
			CheckWin(true);
		}
	}

	void CheckWin(bool startTurn) {
		if (IsGameOver()) {
			GameOver();
			GameOverCleanup();
			RoundFinish();
		} else {
			turnEnd = DateTime.UtcNow + TimeSpan.FromMilliseconds(ROUND_TIME);
			turnTimer = ScheduleCallback(TurnEnd, ROUND_TIME);

			if (startTurn) {
				MoveStarted();
			}
		}
	}
}
