sealed class UT3GameState {
	private readonly TicTacToe t3 = new();

	private UT3GameOptions options;

	private readonly List<UT3GameMove> moveHistory = new();

	private bool isOMove;

	private int mustMoveBoard;

	private int flags;
	private int flagsFinal;

	private readonly int[] boards = new int[9];

	public int Winner { get; private set; }

	public UT3GameState() {
		Reset(new UT3GameOptions());
	}

	public void Reset(UT3GameOptions options) {
		this.options = options;
		moveHistory.Clear();
		mustMoveBoard = -1;
		flags = 0;
		flagsFinal = 0;
		Array.Clear(boards, 0, boards.Length);
		Winner = -1;
	}

	public void AddMove(UT3GameMove move) {
		// Make the move
		boards[move.Board] |= 1 << ((move.Position << 1) + (isOMove ? 1 : 0));
		moveHistory.Add(move);

		// Check for a winner
		if (t3.IsWin(boards[move.Board], isOMove)) {
			// Mark the board as won
			flags = t3.AddMove(flags, move.Board, isOMove);
			flagsFinal |= 1 << move.Board;

			if (options.Quick || t3.IsWin(flags, isOMove)) {
				// win (board is won)
				Winner = (isOMove ^ options.Inverted) ? 1 : 0;
				return;
			}
		} else if (t3.IsFull(boards[move.Board])) {
			// Mark the board as full
			flagsFinal |= 1 << move.Board;
		}

		if (flagsFinal == 0b111_111_111) {
			Winner = 2; // tie (all full)
			return;
		}

		// Allow any board if it is already won or full
		if (options.AnyBoard || (flagsFinal & (1 << move.Position)) != 0) {
			mustMoveBoard = -1;
		} else {
			mustMoveBoard = move.Position;
		}

		// switch the current player
		isOMove = !isOMove;

		// Check for stalemate
		if (options.Checked) {
			bool hasLegalMoves =
				Enumerable.Range(0, 9)
					.Any(b =>
						(mustMoveBoard == -1 || mustMoveBoard == b)
						&& Enumerable.Range(0, 9)
							.Any(m =>
								IsLegalMove(new UT3GameMove(b, m))
							)
					);

			if (!hasLegalMoves) {
				Winner = isOMove ? 0 : 1; // win (checkmate)
			}
		}
	}

	public bool IsLegalMove(UT3GameMove move) {
		if (!(0 <= move.Board && move.Board < 9 // invalid board
			&& 0 <= move.Position && move.Position < 9 // invalid position
			&& (mustMoveBoard == move.Board || mustMoveBoard == -1 && (flagsFinal & (1 << move.Board)) == 0) // wrong board
		)) {
			return false;
		}

		int currentBoard = boards[move.Board];
		if ((currentBoard & (3 << (move.Position << 1))) != 0) {
			// already occupied
			return false;
		}

		if (options.Checked) {
			if (options.Inverted) {
				// inverted: can't move if it'd cause a win
				if (t3.IsWin(t3.AddMove(currentBoard, move.Position, isOMove), isOMove)
					&& (options.Quick || t3.IsWin(t3.AddMove(flags, move.Board, isOMove), isOMove))) {
					return false;
				}
			} else {
				// uninverted: can't move if it'd allow a loss
				int newFlags = boards[move.Position];
				if (move.Board == move.Position) {
					newFlags = t3.AddMove(newFlags, move.Position, isOMove);
				}

				if (options.AnyBoard || !options.Quick &&
					((flagsFinal & (1 << move.Position)) != 0
					|| t3.IsFull(newFlags)
					|| t3.IsWin(newFlags, isOMove))) {
					// next move can be on any board
					for (int i = 0; i < 9; i++) {
						var flagsAfterMove = boards[i];
						if (i == move.Board) {
							flagsAfterMove = t3.AddMove(flagsAfterMove, move.Position, isOMove);
						}

						if ((flagsFinal & (1 << i)) == 0
							&& t3.IsNearWin(flagsAfterMove, !isOMove)
							&& (options.Quick || t3.IsWin(t3.AddMove(flags, i, !isOMove), !isOMove))) {
							return false;
						}
					}
				} else if (t3.IsNearWin(newFlags, !isOMove)
						&& (options.Quick || t3.IsWin(t3.AddMove(flags, move.Position, !isOMove), !isOMove))) {
					return false;
				}
			}
		}

		return true;
	}

	public List<UT3GameMove> GetLegalMoves() {
		void AddLegalMoves(ICollection<UT3GameMove> legalMoves, int board) {
			for (int i = 0; i < 9; i++) {
				var move = new UT3GameMove(board, i);
				if (IsLegalMove(move)) {
					legalMoves.Add(move);
				}
			}
		}

		var validMoves = new List<UT3GameMove>();

		if (mustMoveBoard == -1) {
			for (int i = 0; i < 9; i++) {
				AddLegalMoves(validMoves, i);
			}
		} else {
			AddLegalMoves(validMoves, mustMoveBoard);
		}

		return validMoves;
	}

	public IEnumerable<UT3GameMove> GetMoveHistory() => moveHistory;
}
