partial class Player {
	public PlayerScore score = new();

	public uint color;

	public bool CanResetScore() => score.CanReset();
	public void ResetScore() => score.Reset();
}

partial class Room {
	static void WriteWelcomePlayer(ByteWriter w, Player p) {
		w.Add((byte)(p.color >> 16));
		w.PutUShort((ushort)p.color);

		p.score.WriteTo(w);
	}
}
