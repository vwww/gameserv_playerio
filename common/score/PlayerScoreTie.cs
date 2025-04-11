struct PlayerScoreTie {
	public int wins;
	public int losses;
	public int ties;
	public int streak;

	public bool CanReset() => wins != 0 || losses != 0 || ties != 0 || streak != 0;

	public void Reset() {
		wins = 0;
		losses = 0;
		ties = 0;
		streak = 0;
	}

	public void AddWin(int win = 1) => Update(win, 0, 0);

	public void AddLoss(int loss = 1) => Update(0, loss, 0);

	public void AddTie(int ties = 1) => Update(0, 0, ties);

	public void Update(int win, int lose, int tie) {
		if (win > lose) {
			if (streak < 0) {
				streak = 0;
			}
			streak++;
		} else if (lose > win) {
			if (streak > 0) {
				streak = 0;
			}
			streak--;
		}
		wins += win;
		losses += lose;
		ties += tie;
	}

	public void WriteTo(ByteWriter b) {
		b.PutInt(streak);
		b.PutInt(wins);
		b.PutInt(losses);
		b.PutInt(ties);
	}
}
