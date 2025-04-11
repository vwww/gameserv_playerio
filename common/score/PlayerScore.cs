struct PlayerScore {
	public int wins;
	public int losses;
	public int streak;

	public bool CanReset() => wins != 0 || losses != 0 || streak != 0;

	public void Reset() {
		wins = 0;
		losses = 0;
		streak = 0;
	}
	public void AddWin(int num = 1) {
		if (streak < 0) streak = 0;
		streak++;
		wins += num;
	}

	public void AddLoss(int num = 1) {
		if (streak > 0) streak = 0;
		streak--;
		losses += num;
	}

	public void WriteTo(ByteWriter b) {
		b.PutInt(wins);
		b.PutInt(losses);
		b.PutInt(streak);
	}
}
