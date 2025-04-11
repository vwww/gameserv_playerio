partial class Player {
	public PlayerScoreTie score = new();
	public long balance;

	public bool CanResetScore() => score.CanReset();
	public void ResetScore() {
		balance = 0;
		score.Reset();
	}
}

partial class Room {
	static void WriteWelcomePlayer(ByteWriter w, Player p) {
		w.PutLong(p.balance);
		p.score.WriteTo(w);
	}
}
