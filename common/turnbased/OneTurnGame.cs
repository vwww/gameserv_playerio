partial class Room {
	const int INTERMISSION_TIME = 5000;

	const byte MAX_PLAYERS_ACTIVE = 0;

	void CheckLeavingPlayer(Player _player) {
		if (numActive < MIN_PLAYERS_ACTIVE) {
			turnTimer?.Stop();
			turnTimer = null;
			RoundFinish();
		}
	}

	void RoundStarted() {
		turnEnd = DateTime.UtcNow + TimeSpan.FromMilliseconds(ROUND_TIME);
		turnTimer = ScheduleCallback(ChooseWinners, ROUND_TIME);
	}

	void ChooseWinners() {
		lock (players) {
			RoundEnded();

			turnTimer = null;
			RoundFinish();
		}
	}
}
