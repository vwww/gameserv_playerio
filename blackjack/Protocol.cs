enum MsgS2C {
	WELCOME,
	JOIN,
	LEAVE,
	RESET,
	RENAME,
	PING,
	PING_TIME,
	CHAT,
	ACTIVE,
	ROUND_WAIT,
	ROUND_INTERM,
	ROUND_START,
	READY,
	END_ROUND,
	END_TURN,
	END_TURN_AMOUNT,
	END_TURN_READY,
	PLAYER_ELIMINATE,
	PLAYER_ELIMINATE_EARLY = PLAYER_ELIMINATE,
}

enum MsgC2S {
	RESET,
	RENAME,
	PONG,
	CHAT,
	ACTIVE,
	READY,
	MOVE_BET,
	MOVE_INSURANCE,
	MOVE,
	MOVE_READY,
}

partial class Room {
	private partial bool ProcessMessage(Player player, ByteReader msg) {
		switch ((MsgC2S)msg.GetInt()) {
			case MsgC2S.RESET: ProcessMsgReset(player); break;
			case MsgC2S.RENAME: ProcessMsgRename(player, msg); break;
			case MsgC2S.PONG: ProcessMsgPong(player, msg); break;
			case MsgC2S.CHAT: ProcessMsgChat(player, msg); break;
			case MsgC2S.ACTIVE: ProcessMsgActive(player); break;
			case MsgC2S.READY: ProcessMsgReady(player); break;
			case MsgC2S.MOVE_BET: ProcessMsgMoveBet(player, msg); break;
			case MsgC2S.MOVE_INSURANCE: ProcessMsgMoveInsurance(player, msg); break;
			case MsgC2S.MOVE: ProcessMsgMove(player, msg); break;
			case MsgC2S.MOVE_READY: ProcessMsgMoveReady(player); break;
			default: return false;
		}
		return true;
	}
}
