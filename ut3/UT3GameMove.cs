// can't mark as readonly (Player.IO doesn't allow System.Runtime.CompilerServices.IsReadOnlyAttribute)
/*readonly*/
struct UT3GameMove {
	public readonly int Board;
	public readonly int Position;

	public UT3GameMove(int board, int position) {
		Board = board;
		Position = position;
	}
}
