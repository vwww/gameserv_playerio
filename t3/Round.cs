partial class Room {
	int ROUND_TIME => optTurnTime;

	readonly TicTacToe t3 = new();
	readonly T3QuickTable t3QuickTable = new();

	readonly List<int> moveHistory = new();
	int board;
	int boardCount;
	int pendingMove;

	void ProcessMsgMove(Player player, ByteReader msg) {
		int move = msg.GetInt();

		if (state == GameState.ACTIVE && CanMakeMove(player)) {
			pendingMove = move;

			if (IsLegalMove(move)) {
				TurnEnd();
			}
		}
	}

	partial void WriteWelcome3(ByteWriter b) {
		foreach (int move in moveHistory) {
			b.PutInt(move);
		}
		b.PutInt(-1);
	}

	partial void WriteRoundStartInfo3(ByteWriter b) {
		board = 0;
		boardCount = 0;
		moveHistory.Clear();
	}

	Winner CheckWinner() {
		var isO = !curPlayer;
		if (t3.IsWin(board, isO)) {
			return isO == optInverted ? Winner.P0 : Winner.P1;
		}

		if (optQuick) {
			int forcedWin = t3QuickTable.LoadTable(optChecked ? optInverted ? 3 : 2 : 1)[board];
			if (forcedWin != 0) {
				return forcedWin == 3 ? Winner.DRAW : (forcedWin != 1) == optInverted ? Winner.P0 : Winner.P1;
			}
		}

		if (boardCount == 9) {
			return Winner.DRAW;
		}

		if (optChecked && !Enumerable.Range(0, 9).Any(IsLegalMove)) {
			// checkmate
			return isO ? Winner.P1 : Winner.P0;
		}

		return Winner.NONE;
	}

	void GameOverCleanup2() {
		moveHistory.Clear(); // save memory
		board = pendingMove = 0; // not needed, but might as well reset
	}

	void MoveStarted() {
		pendingMove = -1;
	}

	void MoveEnded() {
		if (!IsLegalMove(pendingMove)) {
			var legalMoves = Enumerable.Range(0, 9).Where(IsLegalMove).ToList();
			pendingMove = rng.Choice(legalMoves);
		}

		var mark = 1 << (boardCount & 1);
		board |= mark << (pendingMove << 1);
		boardCount++;
		moveHistory.Add(pendingMove);

		curPlayer = !curPlayer;

		var msg = new ByteWriter()
			.PutType(MsgS2C.END_TURN)
			.PutInt(pendingMove);

		Broadcast(msg.ToArray());
	}

	bool IsLegalMove(int move) {
		if (!(0 <= move && move < 9
			&& ((board >> (move << 1)) & 3) == 0)) {
			return false;
		}

		if (optChecked) {
			var mark = curPlayer ? 2 : 1;
			var boardNew = board | (mark << (move << 1));

			if (optInverted ? t3.IsWin(boardNew, curPlayer) : t3.IsNearWin(boardNew, !curPlayer)) {
				return false;
			}
		}

		return true;
	}
}
