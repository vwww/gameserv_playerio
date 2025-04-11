partial class Player {
	public ulong kills, deaths;
	public ulong score;

	public byte hue;

	public bool CanResetScore() => kills != 0 || deaths != 0 || score != 0;
	public void ResetScore() => kills = deaths = score = 0;
}

partial class Room {
	static void WriteWelcomePlayer(ByteWriter w, Player p) {
		w.Add(p.hue);
		w.PutULong(p.kills);
		w.PutULong(p.deaths);
		w.PutULong(p.score);
	}
}
