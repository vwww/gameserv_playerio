partial class Player {
	public PlayerScore score = new();

	public bool CanResetScore() => score.CanReset();
	public void ResetScore() => score.Reset();
}

partial class Room {
	static void WriteWelcomePlayer(ByteWriter w, Player p) => p.score.WriteTo(w);
}
