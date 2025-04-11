struct PlayerScoreRank {
	public int score;
	public int streak;
	public int wins;
	public int losses;
	public int lastRank;
	public int bestRank;
	public int worstRank;

	public bool CanReset() {
		return score != 0
			|| streak != 0
			|| wins != 0
			|| losses != 0
			|| lastRank != 0
			|| bestRank != 0
			|| worstRank != 0;
	}

	public void Reset() {
		score = 0;
		streak = 0;
		wins = 0;
		losses = 0;
		lastRank = 0;
		bestRank = 0;
		worstRank = 0;
	}

	public void AddRank(int rank, int playerCount) {
		score += playerCount - rank + 1;
		if (rank == 1) {
			wins++;
			if (streak < 0) {
				streak = 0;
			}
			streak++;
		} else {
			losses++;
			if (streak > 0) {
				streak = 0;
			}
			streak--;
		}
		lastRank = rank;
		bestRank = rank < bestRank || bestRank == 0 ? rank : bestRank;
		worstRank = rank > worstRank ? rank : worstRank;
	}

	public void WriteTo(ByteWriter b) {
		b.PutInt(score);
		b.PutInt(streak);
		b.PutInt(wins);
		b.PutInt(losses);
		b.PutInt(lastRank);
		b.PutInt(bestRank);
		b.PutInt(worstRank);
	}
}
