partial class Room {
	int ROUND_TIME => optTurnTime;

	private UT3GameOptions options;
	private readonly UT3GameState gameState = new();
	private UT3GameMove pendingMove;

	void ProcessMsgMove(Player player, ByteReader msg) {
		int board = msg.GetInt();
		int position = msg.GetInt();

		if (state == GameState.ACTIVE && CanMakeMove(player)) {
			pendingMove = new UT3GameMove(board, position);

			if (gameState.IsLegalMove(pendingMove)) {
				TurnEnd();
			}
		}
	}

	partial void WriteWelcome3(ByteWriter b) {
		foreach (var move in gameState.GetMoveHistory()) {
			b.PutInt(move.Board);
			b.PutInt(move.Position);
		}
		b.PutInt(-1);
	}

	partial void WriteRoundStartInfo3(ByteWriter b) {
		options = new UT3GameOptions(optInverted, optQuick, optChecked, optAnyBoard);
		gameState.Reset(options);
	}

	Winner CheckWinner() => (Winner)(gameState.Winner + 1);

	void GameOverCleanup2() => gameState.Reset(options); // save memory

	void MoveStarted() => pendingMove = new UT3GameMove(-1, -1);

	void MoveEnded() {
		if (!gameState.IsLegalMove(pendingMove)) {
			pendingMove = rng.Choice(gameState.GetLegalMoves());
		}

		gameState.AddMove(pendingMove);

		curPlayer = !curPlayer;

		var msg = new ByteWriter()
			.PutType(MsgS2C.END_TURN)
			.PutInt(pendingMove.Board)
			.PutInt(pendingMove.Position);

		Broadcast(msg.ToArray());
	}
}
