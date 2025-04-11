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
	END_TURN_TRANSFER,
	END_TURN_TRANSFER_DISCARD,
	PLAYER_ELIMINATE,
	PLAYER_ELIMINATE_EARLY,
	PLAYER_PRIVATE_INFO_HAND,
	PLAYER_PRIVATE_INFO_GIVE,
}

enum MsgC2S {
	RESET,
	RENAME,
	PONG,
	CHAT,
	ACTIVE,
	READY,
	MOVE_PASS,
	MOVE_CONTINUE,
	MOVE_START,
	MOVE_TRANSFER,
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
			case MsgC2S.MOVE_PASS: ProcessMsgMove(player, msg, 0); break;
			case MsgC2S.MOVE_CONTINUE: ProcessMsgMove(player, msg, 1); break;
			case MsgC2S.MOVE_START: ProcessMsgMove(player, msg, 2); break;
			case MsgC2S.MOVE_TRANSFER: ProcessMsgMoveTransfer(player, msg); break;
			case MsgC2S.MOVE_END: ProcessMsgMoveEnd(player); break;
			default: return false;
		}
		return true;
	}
}
