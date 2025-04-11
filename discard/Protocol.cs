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
	MOVE_CONFIRM,
	END_TURN,
	PLAYER_ELIMINATE,
	PLAYER_ELIMINATE_EARLY,
	PLAYER_PRIVATE_INFO_MY_HAND,
	PLAYER_PRIVATE_INFO_ALT_MOVE,
	PLAYER_PRIVATE_INFO_MOVE,
}

enum MsgC2S {
	RESET,
	RENAME,
	PONG,
	CHAT,
	ACTIVE,
	READY,
	MOVE_HAND0,
	MOVE_HAND1,
	MOVE_TARGET,
	MOVE_GUESS,
	MOVE_END,
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
			case MsgC2S.MOVE_HAND0: ProcessMsgMoveHand(player, msg, false); break;
			case MsgC2S.MOVE_HAND1: ProcessMsgMoveHand(player, msg, true); break;
			case MsgC2S.MOVE_TARGET: ProcessMsgMoveTarget(player, msg); break;
			case MsgC2S.MOVE_GUESS: ProcessMsgMoveGuess(player, msg); break;
			case MsgC2S.MOVE_END: ProcessMsgMoveEnd(player); break;
			default: return false;
		}
		return true;
	}
}
