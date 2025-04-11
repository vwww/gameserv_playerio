class TicTacToe {
	private readonly TicTacToePatterns patterns = new();

	public bool IsFull(int flags) {
		return patterns.Occupied.All(o => (flags & o) != 0);
	}

	public bool IsWin(int flags, bool isO) {
		return (isO ? patterns.Win2 : patterns.Win1).Any(w => (flags & w) == w);
	}

	public bool IsNearWin(int flags, bool isO) {
		return (isO ? patterns.NearWin2 : patterns.NearWin1).Any(w => (flags & w.Value) == w.Key);
	}

	public int AddMove(int board, int position, bool isO) {
		return board | (1 << ((position << 1) + (isO ? 1 : 0)));
	}
}
