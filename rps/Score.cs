partial class Player {
	public int score;
	public PlayerScoreTie roundScore = new();

	// ulong because there are more games
	public ulong battleWins;
	public ulong battleLosses;
	public ulong battleTies;

	public bool CanResetScore() => roundScore.CanReset() || battleWins != 0 || battleLosses != 0 || battleTies != 0;
	public void ResetScore() {
		roundScore.Reset();
		battleWins = 0;
		battleLosses = 0;
		battleTies = 0;
	}
}

partial class Room {
	static void WriteWelcomePlayer(ByteWriter w, Player p) {
		w.PutInt(p.score);
		p.roundScore.WriteTo(w);
		w.PutULong(p.battleWins);
		w.PutULong(p.battleLosses);
		w.PutULong(p.battleTies);
	}
}
